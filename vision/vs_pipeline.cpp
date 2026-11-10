#define NOMINMAX
#include <windows.h>

#include "vs_pipeline.h"

#include <librealsense2/rs.hpp>

#include <opencv2/imgproc.hpp>
#include <opencv2/imgcodecs.hpp>

#include <cstdio>
#include <cmath>
#include <cstring>
#include <algorithm>
#include <vector>
#include <chrono>

#ifndef M_PI
#  define M_PI 3.14159265358979323846
#endif
#define RAD2DEG (180.0 / M_PI)

/* ============================== 单例 ============================== */

VisionPipeline& VisionPipeline::inst()
{
    static VisionPipeline p;
    return p;
}

static uint64_t HostNowUs()
{
    using namespace std::chrono;
    return (uint64_t)duration_cast<microseconds>(system_clock::now().time_since_epoch()).count();
}

void VisionPipeline::setError(const std::string& e)
{
    std::lock_guard<std::mutex> lk(mtx_);
    err_ = e;
}

std::string VisionPipeline::lastError() const
{
    std::lock_guard<std::mutex> lk(mtx_);
    return err_;
}

/* ============================== 初始化 ============================== */

bool VisionPipeline::init(const std::string& iniPathUtf8, std::string* err)
{
    if (running_.load())
    {
        if (err) *err = "已经在运行，先 vs_stop";
        return false;
    }

    exeDir_ = ExeDirUtf8();
    iniPath_ = iniPathUtf8.empty() ? JoinPath(exeDir_, "vision.ini") : iniPathUtf8;

    /* 标定文件跟 ini 放同一目录，不要跟 DLL 走：
       否则 tuner 标定完，C# 宿主读的是另一份，两边各说各话。
       iniDir_ 同时是"相对路径的基准"——模型、日志目录都按它算，
       所以 C# 宿主即使把 DLL 放在 bin\x64\Debug，只要 ini 指到 vision\，
       onnx=best.onnx 也能找到。 */
    {
        size_t slash = iniPath_.find_last_of("\\/");
        iniDir_ = (slash == std::string::npos) ? exeDir_ : iniPath_.substr(0, slash + 1);
        calibPath_ = JoinPath(iniDir_, "vision_calib.ini");
    }

    ini_ = Ini();
    if (!ini_.load(Utf8ToWide(iniPath_)))
    {
        if (err) *err = "配置文件读不到: " + iniPath_;
        return false;
    }

    /* 标定值单独一份，现场反复标定不要动主配置。
       use_calib_file=0 时不读它，方便离线复算用"配置文件里写死的固定标定"。 */
    calib_ = Ini();
    if (ini_.getB("calib.use_calib_file", true) &&
        calib_.load(Utf8ToWide(calibPath_)))
        ini_.overlay(calib_);

    /* ---- 几何/标定 ---- */
    u0_        = ini_.getF("calib.u0", 320.0f);
    v0_        = ini_.getF("calib.v0", 240.0f);
    z0_        = ini_.getF("calib.z0_mm", 0.0f);
    angK_      = ini_.getF("calib.ang_k", 0.05f);
    ropeLen_   = ini_.getF("calib.rope_len_mm", 0.0f);
    angleMode_ = ini_.getI("calib.angle_mode", 0);

    /* ---- 滤波 ---- */
    ema_ = ini_.getF("filter.ema", 0.6f);
    if (ema_ <= 0.0f) ema_ = 1.0f;      /* 1.0 = 不滤波 */
    if (ema_ > 1.0f)  ema_ = 1.0f;
    td_.r = ini_.getF("filter.td_r", 60.0f);
    td_.reset();
    haveLast_ = false;

    /* 内参可以手工覆盖：0 = 用相机报的值。
       离线复算录像帧、或者怀疑相机上报的内参有问题时用得上。 */
    float fxo = ini_.getF("camera.fx", 0.0f);
    float fyo = ini_.getF("camera.fy", 0.0f);
    float pxo = ini_.getF("camera.ppx", 0.0f);
    float pyo = ini_.getF("camera.ppy", 0.0f);
    if (fxo > 0.0f) fx_ = fxo;
    if (fyo > 0.0f) fy_ = fyo;
    if (pxo > 0.0f) ppx_ = pxo;
    if (pyo > 0.0f) ppy_ = pyo;

    /* ---- 检测器 ---- */
    std::string dtype = ini_.getS("detector.type", "color");
    std::string derr;
    det_ = MakeDetector(dtype, ini_, iniDir_, &derr);
    if (!det_)
    {
        if (err) *err = derr;
        return false;
    }
    lastDetectorType_ = det_->typeName();

    /* ---- 类别名：yolo.names=payload,worker,... 逗号分隔，画框时用 ----
       只影响标注上显示的文字，检测逻辑不受影响 */
    classNames_.clear();
    {
        std::string s = ini_.getS("yolo.names", "");
        size_t start = 0;
        while (start <= s.size())
        {
            size_t comma = s.find(',', start);
            std::string item = Trim(s.substr(start, comma == std::string::npos
                                                    ? std::string::npos : comma - start));
            if (!item.empty()) classNames_.push_back(item);
            if (comma == std::string::npos) break;
            start = comma + 1;
        }
    }

    /* ---- 日志 ---- */
    {
        std::lock_guard<std::mutex> lk(mtx_);
        err_.clear();
    }
    return true;
}

void VisionPipeline::release()
{
    stop();
    det_.reset();
}

/* ============================== 启停 ============================== */

bool VisionPipeline::start(std::string* err)
{
    if (!det_)
    {
        if (err) *err = "还没 init";
        return false;
    }
    if (running_.load())
    {
        if (err) *err = "已经在运行";
        return false;
    }

    {
        std::lock_guard<std::mutex> lk(cvMtx_);
        st_ = ST_STARTING;
    }
    run_.store(true);
    th_ = std::thread(&VisionPipeline::loop, this);

    /* 等采集线程给出明确答复：开流成功，或者带着真实错误退出。
       无相机时 SDK 内部要重试十几秒才抛 "No device connected"，
       所以超时给 25 s；超时只是兜底，正常路径不靠它。 */
    bool ok = false;
    {
        std::unique_lock<std::mutex> lk(cvMtx_);
        ok = cv_.wait_for(lk, std::chrono::seconds(25),
                          [this] { return st_ == ST_RUNNING || st_ == ST_FAILED; });
        ok = ok && (st_ == ST_RUNNING);
    }
    if (ok) return true;

    run_.store(false);
    if (th_.joinable()) th_.join();
    std::string e = lastError();
    if (err) *err = e.empty() ? "相机开流失败，且 SDK 没给出原因" : e;
    return false;
}

void VisionPipeline::stop()
{
    run_.store(false);
    if (th_.joinable()) th_.join();
    running_.store(false);
    {
        std::lock_guard<std::mutex> lk(cvMtx_);
        st_ = ST_IDLE;
    }
    td_.reset();
    haveLast_ = false;
}

/* ============================== 主循环 ============================== */

void VisionPipeline::loop()
{
    const int dw = ini_.getI("camera.depth_width", 640);
    const int dh = ini_.getI("camera.depth_height", 480);
    const int cw = ini_.getI("camera.color_width", 640);
    const int ch = ini_.getI("camera.color_height", 480);
    const int fps = ini_.getI("camera.fps", 30);
    const std::string serial = ini_.getS("camera.serial", "");
    const bool doAlign = ini_.getB("camera.align_depth_to_color", true);

    const bool logCsv = ini_.getB("out.log_csv", true);
    std::string logDir = ini_.getS("out.log_dir", "logs");
    if (!logDir.empty() && logDir[0] != '\\' && !(logDir.size() > 1 && logDir[1] == ':'))
        logDir = JoinPath(iniDir_, logDir);   /* 相对 ini 目录，和模型路径一致 */
    if (logCsv) EnsureDir(Utf8ToWide(logDir));

    FILE* csv = nullptr;
    if (logCsv)
    {
        SYSTEMTIME st;
        GetLocalTime(&st);
        char name[512];
        snprintf(name, sizeof(name), "%s\\vision_%04d%02d%02d_%02d%02d%02d.csv",
                 logDir.c_str(), st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
        csv = _wfopen(Utf8ToWide(name).c_str(), L"w");
        if (csv)
            fprintf(csv, "host_us,dev_us,frame,valid,u,v,z_mm,off_x_mm,angle_deg,rate_dps,conf,lost\n");
    }

    try
    {
        /* 统一的"立刻失败"出口：设置原因 → 关日志 → 通知 start() */
        auto failNow = [&](const std::string& msg)
        {
            setError(msg);
            if (csv) { fclose(csv); csv = nullptr; }
            running_.store(false);
            {
                std::lock_guard<std::mutex> lk(cvMtx_);
                st_ = ST_FAILED;
            }
            cv_.notify_all();
        };

        /* 先枚举一次：没插相机时 SDK 的 pipe.start 要重试十几秒才抛异常，
           这里提前拦住，C# 那边 0.2 s 就能拿到明确原因。
           context 只建这一个，pipeline 复用它（设备枚举和取流共用同一份句柄）。 */
        rs2::context ctx;
        rs2::device_list devs = ctx.query_devices();
        if (devs.size() == 0)
        {
            failNow("设备数 0：没有检测到相机。检查 USB3 线是否插在蓝色口、"
                    "设备管理器里有没有 Intel RealSense，或相机是否被其它程序占用。");
            return;
        }
        if (!serial.empty())
        {
            bool found = false;
            for (size_t i = 0; i < devs.size(); ++i)
            {
                try
                {
                    if (devs[i].get_info(RS2_CAMERA_INFO_SERIAL_NUMBER) == serial) { found = true; break; }
                }
                catch (...) {}
            }
            if (!found)
            {
                char msg[256];
                snprintf(msg, sizeof(msg), "配置里指定的序列号 %s 没找到，实际接了 %d 台",
                         serial.c_str(), (int)devs.size());
                failNow(msg);
                return;
            }
        }

        rs2::pipeline pipe(ctx);
        rs2::config   cfg;
        if (!serial.empty()) cfg.enable_device(serial);
        cfg.enable_stream(RS2_STREAM_DEPTH, dw, dh, RS2_FORMAT_Z16, fps);
        cfg.enable_stream(RS2_STREAM_COLOR, cw, ch, RS2_FORMAT_BGR8, fps);

        rs2::pipeline_profile prof = pipe.start(cfg);

        /* 内参一律取彩色流：深度对齐后用的是彩色坐标系 */
        auto vsp = prof.get_stream(RS2_STREAM_COLOR).as<rs2::video_stream_profile>();
        rs2_intrinsics in = vsp.get_intrinsics();
        /* 只有没手工覆盖时才用相机上报值 */
        if (ini_.getF("camera.fx", 0.0f)  <= 0.0f) fx_  = in.fx;
        if (ini_.getF("camera.fy", 0.0f)  <= 0.0f) fy_  = in.fy;
        if (ini_.getF("camera.ppx", 0.0f) <= 0.0f) ppx_ = in.ppx;
        if (ini_.getF("camera.ppy", 0.0f) <= 0.0f) ppy_ = in.ppy;

        rs2::align align_to_color(RS2_STREAM_COLOR);

        running_.store(true);
        {
            std::lock_guard<std::mutex> lk(cvMtx_);
            st_ = ST_RUNNING;
        }
        cv_.notify_all();
        {
            std::lock_guard<std::mutex> lk(mtx_);
            state_ = "camera started";
            err_.clear();
        }

        /* ---- 帧率统计窗口 ---- */
        auto tFps = std::chrono::steady_clock::now();
        int  fpsCnt = 0;
        int  fps10 = 0;

        while (run_.load())
        {
            rs2::frameset fs;
            if (!pipe.try_wait_for_frames(&fs, 500)) continue;

            /* 深度对齐到彩色：不做这步，检测框和深度像素对不上，
               定位误差能到几十厘米（论文 4.5 节特别提过） */
            rs2::frameset aligned = doAlign ? align_to_color.process(fs) : fs;

            rs2::depth_frame dep = aligned.get_depth_frame();
            rs2::video_frame col = aligned.get_color_frame();
            if (!dep || !col) continue;

            cv::Mat bgr(col.get_height(), col.get_width(), CV_8UC3,
                        (void*)col.get_data());
            cv::Mat depth(dep.get_height(), dep.get_width(), CV_16UC1,
                          (void*)dep.get_data());

            VsResult r = VsResult();   /* 先清零：丢检时不要给 C# 递垃圾浮点数 */
            r.valid = 0;
            r.frame_index = ++frameCount_;
            r.ts_device_us = (uint64_t)(fs.get_timestamp() * 1000.0);
            r.ts_host_us = HostNowUs();

            DetBox box = det_->detect(bgr);

            /* ---- 帧间隔 → TD 积分步长 ---- */
            float dt = 0.033f;
            if (lastFrameUs_ > 0)
                dt = (float)((int64_t)r.ts_host_us - lastFrameUs_) * 1e-6f;
            if (dt < 0.005f) dt = 0.005f;
            if (dt > 0.2f)   dt = 0.2f;
            lastFrameUs_ = (int64_t)r.ts_host_us;

            if (box.valid && !depth.empty())
            {
                if (measure(box, depth, r))
                {
                    lostCount_ = 0;

                    /* EMA 先压一下像素抖动，再进 TD 求角速度 */
                    float a = r.angle_deg;
                    if (haveLast_) a = lastAngle_ + ema_ * (a - lastAngle_);
                    lastAngle_ = a;
                    haveLast_ = true;

                    r.angle_deg = a;
                    td_.step(a, dt);
                    r.rate_dps = td_.x2;
                }
                else
                {
                    box.valid = false;
                }
            }

            if (!box.valid)
            {
                /* 丢检：angle 保持上一帧（供界面显示），但把 valid 置 0、
                   角速度清零。C# 侧看到 valid==0 应当停止更新控制量，
                   不要拿上一帧的角度继续发脉冲。 */
                ++lostCount_;
                r.valid = 0;
                r.angle_deg = lastAngle_;
                r.rate_dps = 0.0f;
                td_.reset();
            }
            else
            {
                r.valid = 1;
                r.box_x = box.x; r.box_y = box.y;
                r.box_w = box.w; r.box_h = box.h;
                r.confidence = box.conf;
                r.cls = box.cls;
            }
            r.reserved[0] = lostCount_;

            /* ---- 帧率 ---- */
            ++fpsCnt;
            auto now = std::chrono::steady_clock::now();
            double el = std::chrono::duration<double>(now - tFps).count();
            if (el >= 1.0)
            {
                fps10 = (int)(fpsCnt * 10.0 / el);
                fpsCnt = 0;
                tFps = now;
            }
            r.fps_x10 = fps10;

            if (csv && r.valid)
            {
                fprintf(csv, "%llu,%llu,%d,%d,%.1f,%.1f,%.1f,%.1f,%.3f,%.3f,%.3f,%d\n",
                        (unsigned long long)r.ts_host_us, (unsigned long long)r.ts_device_us,
                        r.frame_index, r.valid, r.u, r.v, r.z_mm,
                        r.off_x_mm, r.angle_deg, r.rate_dps, r.confidence, lostCount_);
                fflush(csv);
            }

            cv::Mat annotated;
            annotate(bgr, r, annotated);
            pushResult(r, bgr, annotated, depth);
        }

        pipe.stop();
    }
    catch (const rs2::error& e)
    {
        setError(std::string("RealSense: ") + e.what() + " @ " + e.get_failed_function());
    }
    catch (const cv::Exception& e)
    {
        setError(std::string("OpenCV: ") + e.what());
    }
    catch (const std::exception& e)
    {
        setError(std::string("std: ") + e.what());
    }

    if (csv) fclose(csv);
    running_.store(false);
    {
        std::lock_guard<std::mutex> lk(cvMtx_);
        if (st_ != ST_FAILED) st_ = ST_FAILED;
    }
    cv_.notify_all();
}

/* ============================ 单帧解算 ============================
   1. 框内取有效深度中位数（取中心 60% 区域，避开边缘和背景串色）
   2. 反投影得到相机坐标
   3. 按 angle_mode 算摆角
   ================================================================= */

bool VisionPipeline::measure(const DetBox& box, const cv::Mat& depth, VsResult& r)
{
    if (depth.empty() || fx_ <= 0.0f || fy_ <= 0.0f) return false;

    int bw = box.w, bh = box.h;
    int x0 = box.x + bw / 5;
    int y0 = box.y + bh / 5;
    int w  = std::max(4, bw - bw * 2 / 5);
    int h  = std::max(4, bh - bh * 2 / 5);

    cv::Rect roi = cv::Rect(x0, y0, w, h) & cv::Rect(0, 0, depth.cols, depth.rows);
    if (roi.width < 2 || roi.height < 2) return false;

    std::vector<uint16_t> vals;
    vals.reserve((size_t)roi.width * roi.height / 2);
    for (int y = roi.y; y < roi.y + roi.height; ++y)
    {
        const uint16_t* row = depth.ptr<uint16_t>(y);
        for (int x = roi.x; x < roi.x + roi.width; ++x)
            if (row[x] > 0) vals.push_back(row[x]);
    }
    if (vals.size() < 20) return false;   /* 深度基本全丢，当本帧无效 */

    std::nth_element(vals.begin(), vals.begin() + vals.size() / 2, vals.end());
    float z = (float)vals[vals.size() / 2];   /* mm */

    const float u = box.cx;
    const float v = box.cy;

    r.u = u;
    r.v = v;
    r.z_mm = z;
    r.cam_x_mm = (u - ppx_) * z / fx_;
    r.cam_y_mm = (v - ppy_) * z / fy_;
    r.cam_z_mm = z;

    /* 相对竖直零位的位移（mm） */
    const float dx = (u - u0_) * z / fx_;      /* 右正 */
    const float dy = (v - v0_) * z / fy_;      /* 下正 */
    r.off_x_mm  = dx;
    r.drop_y_mm = dy;

    /* ---- 摆角 ---- */
    if (angleMode_ == 0 && ropeLen_ > 1.0f)
    {
        /* 物理法：tan θ = 水平位移 / 绳长 */
        r.angle_deg = (float)(std::atan2(dx, ropeLen_) * RAD2DEG);
    }
    else
    {
        /* 两点标定法：θ = k · Δu
           没有 k 的时候给一个由内参推的近似值，至少方向是对的：
           k ≈ (180/π) · z0 / (fx · L)，z0 未标定时按 1 m 兜底 */
        float k = angK_;
        if (k <= 0.0f)
        {
            float zRef = (z0_ > 1.0f) ? z0_ : 1000.0f;
            float L = (ropeLen_ > 1.0f) ? ropeLen_ : 1000.0f;
            k = (float)(RAD2DEG * zRef / (fx_ * L));
        }
        r.angle_deg = k * (u - u0_);
    }
    return true;
}

/* ============================== 标注 ============================== */

/* 类别 id → 名字。ini 里 yolo.names 没配就退回 class0/class1 */
std::string VisionPipeline::className(int cls) const
{
    if (cls >= 0 && cls < (int)classNames_.size()) return classNames_[(size_t)cls];
    if (cls >= 0)
    {
        char b[32];
        snprintf(b, sizeof(b), "class%d", cls);
        return std::string(b);
    }
    return "object";
}

/* 在框旁边画一个黑底多行标签，保证在亮背景上也看得清 */
static void DrawLabel(cv::Mat& img, const std::vector<std::string>& lines,
                      cv::Point org, const cv::Scalar& fg)
{
    if (img.empty() || lines.empty()) return;

    const double fs = 0.45;
    const int    th = 1;
    const int    pad = 4;
    const int    lh = 15;          /* 行高（像素） */

    int w = 0;
    for (size_t i = 0; i < lines.size(); ++i)
    {
        int baseline = 0;
        /* ★ 宽度是 getTextSize 的【返回值】，不是最后那个基线参数。
           少接返回值的话 w 恒为 0，标签宽度只算成 8 像素，clamp 和黑底全失效。 */
        cv::Size sz = cv::getTextSize(lines[i], cv::FONT_HERSHEY_SIMPLEX, fs, th, &baseline);
        w = std::max(w, sz.width);
    }
    const int boxW = w + pad * 2;
    const int boxH = (int)lines.size() * lh + pad;

    /* 默认贴在框的上沿；上方放不下就画到框内 */
    int bx = org.x;
    int by = org.y - boxH;
    if (by < 0) by = org.y;

    /* 四个方向都夹进画面内，否则文字会被边界裁掉、黑底也会画不出来 */
    if (bx + boxW > img.cols) bx = img.cols - boxW;
    if (bx < 0) bx = 0;
    if (by + boxH > img.rows) by = img.rows - boxH;
    if (by < 0) by = 0;

    cv::Rect bg(bx, by, std::min(boxW, img.cols), std::min(boxH, img.rows));
    bg &= cv::Rect(0, 0, img.cols, img.rows);
    if (bg.width > 0 && bg.height > 0)
        img(bg).setTo(cv::Scalar(0, 0, 0));

    for (size_t i = 0; i < lines.size(); ++i)
    {
        int ty = by + pad + lh * (int)i + lh - 5;
        if (ty > img.rows - 2) break;
        cv::putText(img, lines[i], cv::Point(bx + pad, ty),
                    cv::FONT_HERSHEY_SIMPLEX, fs, fg, th, cv::LINE_AA);
    }
}

void VisionPipeline::annotate(const cv::Mat& bgr, const VsResult& r, cv::Mat& out) const
{
    out = bgr.clone();

    /* 竖直中心线（零位） */
    cv::line(out, cv::Point((int)u0_, 0), cv::Point((int)u0_, out.rows),
             cv::Scalar(0, 255, 255), 1, cv::LINE_AA);

    if (r.valid)
    {
        cv::Rect box(r.box_x, r.box_y, r.box_w, r.box_h);
        cv::rectangle(out, box, cv::Scalar(0, 255, 0), 2, cv::LINE_AA);
        cv::circle(out, cv::Point((int)r.u, (int)r.v), 4, cv::Scalar(0, 0, 255), -1, cv::LINE_AA);
        cv::line(out, cv::Point((int)u0_, (int)v0_), cv::Point((int)r.u, (int)r.v),
                 cv::Scalar(255, 0, 0), 1, cv::LINE_AA);

        /* ---- 框上标签：类别 / 置信度 / 三维坐标 ---- */
        char l1[160], l2[160];
        snprintf(l1, sizeof(l1), "%s  id=%d  conf=%.2f",
                 className(r.cls).c_str(), r.cls, r.confidence);
        snprintf(l2, sizeof(l2), "X=%.0f Y=%.0f Z=%.0f mm",
                 r.cam_x_mm, r.cam_y_mm, r.cam_z_mm);

        std::vector<std::string> lines;
        lines.push_back(l1);
        lines.push_back(l2);
        DrawLabel(out, lines, cv::Point(box.x, box.y), cv::Scalar(0, 255, 0));
    }

    char buf[256];
    snprintf(buf, sizeof(buf), "angle=%+7.2f deg  rate=%+7.2f dps", r.angle_deg, r.rate_dps);
    cv::putText(out, buf, cv::Point(8, out.rows - 46), cv::FONT_HERSHEY_SIMPLEX, 0.55,
                cv::Scalar(0, 0, 0), 3, cv::LINE_AA);
    cv::putText(out, buf, cv::Point(8, out.rows - 46), cv::FONT_HERSHEY_SIMPLEX, 0.55,
                r.valid ? cv::Scalar(0, 255, 0) : cv::Scalar(0, 0, 255), 1, cv::LINE_AA);

    snprintf(buf, sizeof(buf), "off_x=%.1fmm drop_y=%.1fmm fps=%.1f %s",
             r.off_x_mm, r.drop_y_mm, r.fps_x10 / 10.0, r.valid ? "OK" : "LOST");
    cv::putText(out, buf, cv::Point(8, out.rows - 20), cv::FONT_HERSHEY_SIMPLEX, 0.5,
                cv::Scalar(0, 0, 0), 3, cv::LINE_AA);
    cv::putText(out, buf, cv::Point(8, out.rows - 20), cv::FONT_HERSHEY_SIMPLEX, 0.5,
                cv::Scalar(255, 255, 255), 1, cv::LINE_AA);
}

void VisionPipeline::buildDepthVis(const cv::Mat& depth, cv::Mat& out) const
{
    if (depth.empty()) { out.release(); return; }
    cv::Mat clamped;
    cv::min(depth, 3000, clamped);
    cv::max(clamped, 300, clamped);
    cv::Mat gray;
    clamped.convertTo(gray, CV_8U, 255.0 / 2700.0, -300.0 * 255.0 / 2700.0);
    cv::applyColorMap(gray, out, cv::COLORMAP_JET);
}

void VisionPipeline::pushResult(const VsResult& r,
                                const cv::Mat& rawBgr,
                                const cv::Mat& annotated,
                                const cv::Mat& depth)
{
    cv::Mat dvis;
    buildDepthVis(depth, dvis);

    std::lock_guard<std::mutex> lk(mtx_);
    latest_ = r;
    ++seq_;
    prevColor_ = annotated;
    /* 独立拷贝：frameset 的缓冲区会被 SDK 复用，浅拷贝会拿到下一帧/脏数据 */
    rawColor_  = rawBgr.clone();
    prevDepth_ = dvis;
}

/* ============================== 对外 ============================== */

VsResult VisionPipeline::result() const
{
    std::lock_guard<std::mutex> lk(mtx_);
    return latest_;
}

uint64_t VisionPipeline::sequence() const
{
    std::lock_guard<std::mutex> lk(mtx_);
    return seq_;
}

bool VisionPipeline::preview(cv::Mat& color, cv::Mat& depthVis) const
{
    std::lock_guard<std::mutex> lk(mtx_);
    if (prevColor_.empty()) return false;
    color = prevColor_.clone();
    depthVis = prevDepth_.empty() ? cv::Mat() : prevDepth_.clone();
    return true;
}

bool VisionPipeline::calibrateZero(std::string* err)
{
    VsResult r = result();
    if (!r.valid)
    {
        if (err) *err = "当前没有有效检测，先把吊重停在画面里静止";
        return false;
    }
    u0_ = r.u;
    v0_ = r.v;
    z0_ = r.z_mm;
    lastAngle_ = 0.0f;
    haveLast_ = false;
    td_.reset();
    if (err) *err = "零位已更新";
    return true;
}

bool VisionPipeline::saveCalibration(std::string* err)
{
    Ini c;
    char buf[64];
    snprintf(buf, sizeof(buf), "%.2f", u0_);  c.set("calib.u0", buf);
    snprintf(buf, sizeof(buf), "%.2f", v0_);  c.set("calib.v0", buf);
    snprintf(buf, sizeof(buf), "%.2f", z0_);  c.set("calib.z0_mm", buf);
    snprintf(buf, sizeof(buf), "%.6f", angK_); c.set("calib.ang_k", buf);
    snprintf(buf, sizeof(buf), "%.1f", ropeLen_); c.set("calib.rope_len_mm", buf);
    snprintf(buf, sizeof(buf), "%d", angleMode_); c.set("calib.angle_mode", buf);

    const char* hdr =
        "; vision_calib.ini —— 由 vs_save_calibration 自动生成，可手工改\r\n"
        "; u0/v0 是吊重静止时的质心像素，也就是“竖直中心线”\r\n"
        "; z0_mm 是标定时的深度，换相机距离要重标\r\n"
        "; rope_len_mm 是吊重质心到悬点的绳长(mm)，angle_mode=0 时必须填\r\n"
        "; ang_k 是两点标定得到的 度/像素，angle_mode=1 时用\r\n";
    if (!c.save(Utf8ToWide(calibPath_), hdr))
    {
        if (err) *err = "写 vision_calib.ini 失败: " + calibPath_;
        return false;
    }
    if (err) *err = "标定已写入 vision_calib.ini";
    return true;
}

/* 把一帧按扩展名编码落盘。两条存图路径共用，避免重复代码。 */
static bool WriteSnapshotFile(const cv::Mat& img,
                              const std::string& pathUtf8,
                              std::string* err)
{
    std::string p = pathUtf8;
    std::string ext = ".jpg";
    size_t dot = p.find_last_of('.');
    if (dot != std::string::npos) ext = ToLower(p.substr(dot));

    std::vector<uchar> buf;
    if (!cv::imencode(ext, img, buf))
    {
        if (err) *err = "编码失败(不支持的扩展名?): " + ext;
        return false;
    }
    if (!WriteFileBytes(Utf8ToWide(p), buf.data(), buf.size()))
    {
        if (err) *err = "写文件失败: " + p;
        return false;
    }
    return true;
}

bool VisionPipeline::snapshot(const std::string& pathUtf8, std::string* err)
{
    cv::Mat img;
    {
        std::lock_guard<std::mutex> lk(mtx_);
        /* 带检测框/读数的预览帧。采训练集请用 snapshotRaw()。 */
        const cv::Mat& src = prevColor_;
        if (src.empty())
        {
            if (err) *err = "还没有画面";
            return false;
        }
        img = src.clone();
    }
    return WriteSnapshotFile(img, pathUtf8, err);
}

bool VisionPipeline::snapshotRaw(const std::string& pathUtf8, std::string* err)
{
    cv::Mat img;
    {
        std::lock_guard<std::mutex> lk(mtx_);
        if (rawColor_.empty())
        {
            if (err) *err = "还没有画面";
            return false;
        }
        img = rawColor_.clone();
    }
    return WriteSnapshotFile(img, pathUtf8, err);
}

std::string VisionPipeline::stateText() const
{
    std::lock_guard<std::mutex> lk(mtx_);
    char buf[512];
    snprintf(buf, sizeof(buf),
             "running=%d frames=%d fps=%.1f det=%s angle=%.2f rate=%.2f lost=%d err=%s",
             running_.load() ? 1 : 0, frameCount_,
             latest_.fps_x10 / 10.0, lastDetectorType_.c_str(),
             latest_.angle_deg, latest_.rate_dps, lostCount_,
             err_.empty() ? "-" : err_.c_str());
    return std::string(buf);
}

void VisionPipeline::setDetectorParam(const std::string& key, const std::string& val)
{
    if (!det_) return;
    ini_.set(key, val);
    det_->setParam(key, val);
}

bool VisionPipeline::processFrame(const cv::Mat& bgr, const cv::Mat& depth, VsResult& r)
{
    r = VsResult();
    if (!det_ || bgr.empty()) return false;

    DetBox box = det_->detect(bgr);
    if (!box.valid) return false;
    if (!measure(box, depth, r)) return false;

    r.valid = 1;
    r.box_x = box.x; r.box_y = box.y;
    r.box_w = box.w; r.box_h = box.h;
    r.confidence = box.conf;
    r.cls = box.cls;

    /* 离线帧也送进预览缓冲，这样 C# 或 tuner 在回放时能看到同一张标注图 */
    cv::Mat annotated;
    annotate(bgr, r, annotated);
    pushResult(r, bgr, annotated, depth);
    return true;
}
