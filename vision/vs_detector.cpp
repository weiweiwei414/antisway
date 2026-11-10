#include "vs_detector.h"
#include "vs_gridsample.hpp"   // 让 OpenCV 4.7 认识 DySample 导出的 GridSample 层

#include <opencv2/imgproc.hpp>
#include <opencv2/dnn.hpp>

#include <cstdio>
#include <cstdlib>
#include <cmath>
#include <algorithm>
#include <mutex>

/* ============================ 无检测 ============================ */

class NullDetector : public Detector
{
public:
    bool init(const Ini&, const std::string&, std::string*) override { return true; }
    DetBox detect(const cv::Mat&) override { return DetBox(); }
    const char* typeName() const override { return "none"; }
};

/* ========================= 彩色标记检测 =========================
   HSV 阈值 → 开闭运算去噪 → 取最大连通域 → 轮廓矩求质心
   ROI 用来屏蔽背景里的同色干扰（比如背景挂了一块同色的布）
   ================================================================ */

class ColorDetector : public Detector
{
public:
    bool init(const Ini& ini, const std::string&, std::string*) override
    {
        hmin_ = ini.getI("color.h_min", 0);
        hmax_ = ini.getI("color.h_max", 180);
        smin_ = ini.getI("color.s_min", 80);
        smax_ = ini.getI("color.s_max", 255);
        vmin_ = ini.getI("color.v_min", 60);
        vmax_ = ini.getI("color.v_max", 255);

        minArea_ = ini.getI("color.min_area", 200);
        maxArea_ = ini.getI("color.max_area", 0);      /* 0 = 不限 */
        open_    = ini.getI("color.morph_open", 1);
        close_   = ini.getI("color.morph_close", 1);

        roiX_ = ini.getI("color.roi_x", 0);
        roiY_ = ini.getI("color.roi_y", 0);
        roiW_ = ini.getI("color.roi_w", 0);
        roiH_ = ini.getI("color.roi_h", 0);

        /* 质心用"最小外接圆圆心"还是"轮廓矩"：圆标用矩更稳，默认矩 */
        useMoments_ = ini.getB("color.use_moments", true);
        return true;
    }

    const char* typeName() const override { return "color"; }

    /* 现场调阈值：滑条回调直接进来，采集线程下一帧就生效 */
    void setParam(const std::string& key, const std::string& val) override
    {
        std::string k = ToLower(key);
        std::lock_guard<std::mutex> lk(mtx_);
        if      (k == "color.h_min")  hmin_ = atoi(val.c_str());
        else if (k == "color.h_max")  hmax_ = atoi(val.c_str());
        else if (k == "color.s_min")  smin_ = atoi(val.c_str());
        else if (k == "color.s_max")  smax_ = atoi(val.c_str());
        else if (k == "color.v_min")  vmin_ = atoi(val.c_str());
        else if (k == "color.v_max")  vmax_ = atoi(val.c_str());
        else if (k == "color.min_area")  minArea_ = atoi(val.c_str());
        else if (k == "color.morph_open")  open_ = atoi(val.c_str());
        else if (k == "color.morph_close") close_ = atoi(val.c_str());
    }

    DetBox detect(const cv::Mat& bgr) override
    {
        DetBox out;
        if (bgr.empty()) return out;

        /* 参数快照：拿锁拷一份，后面整帧计算都不占锁 */
        int hmin, hmax, smin, smax, vmin, vmax, minArea, maxArea, open, close;
        int roiX, roiY, roiW, roiH;
        bool useMoments;
        {
            std::lock_guard<std::mutex> lk(mtx_);
            hmin = hmin_; hmax = hmax_; smin = smin_; smax = smax_; vmin = vmin_; vmax = vmax_;
            minArea = minArea_; maxArea = maxArea_; open = open_; close = close_;
            roiX = roiX_; roiY = roiY_; roiW = roiW_; roiH = roiH_;
            useMoments = useMoments_;
        }

        cv::Rect roi(0, 0, bgr.cols, bgr.rows);
        if (roiW > 0 && roiH > 0)
        {
            roi = cv::Rect(roiX, roiY, roiW, roiH) & cv::Rect(0, 0, bgr.cols, bgr.rows);
            if (roi.width < 8 || roi.height < 8) return out;
        }

        cv::Mat hsv;
        cv::cvtColor(bgr(roi), hsv, cv::COLOR_BGR2HSV);

        cv::Mat mask;
        if (hmin <= hmax)
        {
            cv::inRange(hsv, cv::Scalar(hmin, smin, vmin),
                             cv::Scalar(hmax, smax, vmax), mask);
        }
        else
        {
            /* 红色跨 0 度：H 分两段或起来 */
            cv::Mat m1, m2;
            cv::inRange(hsv, cv::Scalar(0, smin, vmin),
                             cv::Scalar(hmax, smax, vmax), m1);
            cv::inRange(hsv, cv::Scalar(hmin, smin, vmin),
                             cv::Scalar(180, smax, vmax), m2);
            cv::bitwise_or(m1, m2, mask);
        }

        if (open > 0)
        {
            cv::Mat k = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(3, 3));
            cv::erode(mask, mask, k, cv::Point(-1, -1), open);
        }
        if (close > 0)
        {
            cv::Mat k = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(5, 5));
            cv::dilate(mask, mask, k, cv::Point(-1, -1), close);
        }

        std::vector<std::vector<cv::Point> > contours;
        cv::findContours(mask, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

        double bestArea = 0.0;
        int bestIdx = -1;
        for (size_t i = 0; i < contours.size(); ++i)
        {
            double a = cv::contourArea(contours[i]);
            if (a < (double)minArea) continue;
            if (maxArea > 0 && a > (double)maxArea) continue;
            if (a > bestArea) { bestArea = a; bestIdx = (int)i; }
        }
        if (bestIdx < 0) return out;

        cv::Rect br = cv::boundingRect(contours[bestIdx]);

        float px = (float)(br.x + br.width * 0.5);
        float py = (float)(br.y + br.height * 0.5);
        if (useMoments)
        {
            cv::Moments m = cv::moments(contours[bestIdx]);
            if (m.m00 > 1e-6)
            {
                px = (float)(m.m10 / m.m00);
                py = (float)(m.m01 / m.m00);
            }
        }
        else
        {
            cv::Point2f c;
            float r = 0.0f;
            cv::minEnclosingCircle(contours[bestIdx], c, r);
            px = c.x; py = c.y;
        }

        out.valid = true;
        out.x = br.x + roi.x;
        out.y = br.y + roi.y;
        out.w = br.width;
        out.h = br.height;
        out.cx = px + roi.x;
        out.cy = py + roi.y;
        /* 用"填充度"当置信度：圆形色块 ≈ 0.78，越接近实心越可信 */
        float boxArea = (float)(br.width * br.height);
        out.conf = boxArea > 0 ? (float)(bestArea / boxArea) : 0.0f;
        out.cls = 0;
        return out;
    }

private:
    mutable std::mutex mtx_;
    int  hmin_ = 0, hmax_ = 180, smin_ = 80, smax_ = 255, vmin_ = 60, vmax_ = 255;
    int  minArea_ = 200, maxArea_ = 0, open_ = 1, close_ = 1;
    int  roiX_ = 0, roiY_ = 0, roiW_ = 0, roiH_ = 0;
    bool useMoments_ = true;
};

/* ============================ YOLO 检测 ============================
  支持两类权重：
    darknet  .cfg + .weights        —— OpenCV 原生支持，最稳（yolov3/v4-tiny）
    onnx     单输出 [1,N,5+nc] 或 [1,5+nc,N]（yolov5 头）
             [1,N,4+nc]（yolov8/11 头，无 obj 项）

  head 配置：
    head=v5   conf = obj * classScore
    head=v8   conf = classScore（本身就带 sigmoid）
  类别过滤：
    class.yolo_class_id = -1 表示不筛
    class.yolo_names    = 多个类名用逗号分隔，仅用于日志
  输入尺寸必须和训练时一致，否则框会整体偏。
  ================================================================== */

class YoloDetector : public Detector
{
public:
    bool init(const Ini& ini, const std::string& baseDir, std::string* err) override
    {
        modelType_ = ToLower(ini.getS("yolo.model_type", "onnx"));
        inW_ = ini.getI("yolo.input_w", 640);
        inH_ = ini.getI("yolo.input_h", 640);
        conf_ = ini.getF("yolo.conf", 0.40f);
        nms_ = ini.getF("yolo.nms", 0.45f);
        classId_ = ini.getI("yolo.class_id", -1);
        swapRB_ = ini.getB("yolo.swap_rb", true);
        scale_ = ini.getF("yolo.scale", 1.0f / 255.0f);
        head_ = ToLower(ini.getS("yolo.head", "auto"));
        names_ = ini.getS("yolo.names", "");
        /* yolov8/v11 训练和验证都用 letterbox，这里默认开着；
           关掉就退回"拉伸"，纵横比失真，小目标精度会掉 */
        letterbox_ = ini.getB("yolo.letterbox", true);
        nc_ = ini.getI("yolo.nc", 0);

        /* 相对路径按 exe 目录解析，省得每次写绝对路径 */
        std::string cfg = ini.getS("yolo.cfg", "");
        std::string weights = ini.getS("yolo.weights", "");
        std::string onnx = ini.getS("yolo.onnx", "");
        cfg = AbsPath(cfg, baseDir);
        weights = AbsPath(weights, baseDir);
        onnx = AbsPath(onnx, baseDir);

        try
        {
            if (modelType_ == "darknet")
            {
                if (cfg.empty() || weights.empty())
                {
                    if (err) *err = "yolo.model_type=darknet 但 yolo.cfg / yolo.weights 没填";
                    return false;
                }
                std::vector<uchar> cfgBuf, wBuf;
                if (!ReadAll(cfg, cfgBuf))     { if (err) *err = "读不到 cfg 文件: " + cfg; return false; }
                if (!ReadAll(weights, wBuf))   { if (err) *err = "读不到 weights 文件: " + weights; return false; }
                net_ = cv::dnn::readNetFromDarknet(cfgBuf, wBuf);
            }
            else if (modelType_ == "onnx")
            {
                if (onnx.empty())
                {
                    if (err) *err = "yolo.model_type=onnx 但 yolo.onnx 没填";
                    return false;
                }
                std::vector<uchar> buf;
                if (!ReadAll(onnx, buf))
                {
                    if (err) *err = "读不到模型文件（路径错/没权限）: " + onnx;
                    return false;
                }
                net_ = cv::dnn::readNetFromONNX(buf);
            }
            else
            {
                if (err) *err = "未知的 yolo.model_type: " + modelType_;
                return false;
            }

            net_.setPreferableBackend(cv::dnn::DNN_BACKEND_OPENCV);
            net_.setPreferableTarget(cv::dnn::DNN_TARGET_CPU);
            outNames_ = net_.getUnconnectedOutLayersNames();
        }
        catch (const cv::Exception& e)
        {
            if (err) *err = std::string("模型加载失败: ") + e.what();
            return false;
        }
        return true;
    }

    const char* typeName() const override { return "yolo"; }

    DetBox detect(const cv::Mat& bgr) override
    {
        DetBox out;
        if (bgr.empty() || net_.empty()) return out;

        int   padX = 0, padY = 0;
        float ratio = 1.0f;
        cv::Mat input = bgr;
        if (letterbox_)
            input = YoloLetterbox(bgr, inW_, inH_, &padX, &padY, &ratio);

        cv::Mat blob;
        cv::dnn::blobFromImage(input, blob, scale_, cv::Size(inW_, inH_),
                               cv::Scalar(), swapRB_, false, CV_32F);

        net_.setInput(blob);
        std::vector<cv::Mat> outs;
        net_.forward(outs, outNames_);

        YoloParams p;
        p.inW = inW_;  p.inH = inH_;
        p.imgW = bgr.cols; p.imgH = bgr.rows;
        p.conf = conf_; p.nms = nms_;
        p.classId = classId_;
        p.nc = nc_;
        p.head = head_;
        p.stretch = !letterbox_;
        p.padX = padX; p.padY = padY; p.ratio = ratio;

        return ParseYoloOutputs(outs, p);
    }

private:
    /* ★ 为什么不直接 readNetFromONNX(路径)：
       OpenCV 内部用 std::ifstream 开模型文件，走的是 ANSI 窄路径。
       只要路径里有中文（比如 ...\运动控制卡\...），就一定报
       "Can't read ONNX file"，哪怕文件明明在那儿。
       所以这里自己按 UTF-16 读成字节流，再用内存缓冲区的重载喂进去。 */
    static bool ReadAll(const std::string& pathUtf8, std::vector<uchar>& out)
    {
        std::string raw;
        if (!ReadFileBytes(Utf8ToWide(pathUtf8), raw) || raw.empty()) return false;
        out.assign(raw.begin(), raw.end());
        return true;
    }

    static std::string AbsPath(const std::string& p, const std::string& baseDir)
    {
        if (p.empty()) return p;
        if (p.size() > 1 && (p[1] == ':' || p[0] == '\\' || p[0] == '/')) return p;
        return JoinPath(baseDir, p);
    }

    cv::dnn::Net net_;
    std::vector<std::string> outNames_;
    std::string modelType_ = "onnx";
    std::string head_ = "auto";
    std::string names_;
    int   inW_ = 640, inH_ = 640;
    float conf_ = 0.40f, nms_ = 0.45f, scale_ = 1.0f / 255.0f;
    int   classId_ = -1;
    int   nc_ = 0;
    bool  swapRB_ = true;
    bool  letterbox_ = true;
};

/* ========================== YOLO 后处理实现 ========================== */

cv::Mat YoloLetterbox(const cv::Mat& bgr, int inW, int inH,
                      int* padX, int* padY, float* ratio)
{
    const float r = std::min((float)inW / (float)bgr.cols, (float)inH / (float)bgr.rows);
    const int nw = (int)std::lround(bgr.cols * r);
    const int nh = (int)std::lround(bgr.rows * r);

    cv::Mat resized;
    cv::resize(bgr, resized, cv::Size(nw, nh), 0, 0, cv::INTER_LINEAR);

    cv::Mat canvas(inH, inW, bgr.type(), cv::Scalar(114, 114, 114));
    const int px = (inW - nw) / 2;
    const int py = (inH - nh) / 2;
    resized.copyTo(canvas(cv::Rect(px, py, nw, nh)));

    if (padX)  *padX = px;
    if (padY)  *padY = py;
    if (ratio) *ratio = r;
    return canvas;
}

/* 把网络输出统一成"候选 N 行 × 属性列"的排布 */
static cv::Mat AsCandidates(const cv::Mat& raw)
{
    cv::Mat m = raw;
    if (m.dims == 3 && m.size[0] == 1)
        m = cv::Mat(m.size[1], m.size[2], CV_32F, m.ptr<float>(0));
    if (m.dims != 2) return cv::Mat();

    /* 属性×候选 的排布（如 1×84×8400）要转置；
       判据：行数远小于列数。候选数不会只有几十个，属性数也不会超过百来列。 */
    if (m.rows < m.cols && m.rows < 128) m = m.t();
    return m;
}

DetBox ParseYoloOutputs(const std::vector<cv::Mat>& outs, const YoloParams& p)
{
    DetBox out;
    if (outs.empty()) return out;

    std::vector<cv::Rect>  boxes;
    std::vector<float>     scores;
    std::vector<int>       classes;
    bool anyNmsHead = false;

    for (size_t k = 0; k < outs.size(); ++k)
    {
        cv::Mat m = AsCandidates(outs[k]);
        if (m.empty() || m.cols < 5) continue;

        const int attr = m.cols;
        /* auto：6 列 = x1,y1,x2,y2,score,cls（导出时带 nms=True）；
           其余按 v8/v11 头（4+nc，无 obj）。yolov5 请显式写 head=v5。 */
        std::string head = p.head;
        if (head == "auto" || head.empty()) head = (attr == 6) ? "nms" : "v8";
        if (head == "nms") anyNmsHead = true;

        /* 类别列数：nc 没配就按列数推。分段(seg)模型的输出还带着掩膜系数，
           不卡住 nc 会把掩膜系数当类别分数，框能出但类别全错。 */
        int nc = p.nc;
        if (nc <= 0) nc = (head == "v5") ? (attr - 5) : (attr - 4);
        if (nc < 1) nc = 1;
        const int clsEnd = (head == "v5") ? (5 + nc) : (4 + nc);   /* 不含 */

        for (int r = 0; r < m.rows; ++r)
        {
            const float* q = m.ptr<float>(r);
            float x1, y1, x2, y2, score;
            int   cls = -1;

            if (head == "nms")
            {
                /* 已是 (x1,y1,x2,y2,score,cls)，坐标在输入图像素系 */
                x1 = q[0]; y1 = q[1]; x2 = q[2]; y2 = q[3];
                score = q[4];
                cls = (int)std::lround(q[5]);
                if (x2 <= x1 || y2 <= y1) continue;
            }
            else
            {
                float cxx = q[0], cyy = q[1], bw = q[2], bh = q[3];
                if (bw <= 0.0f || bh <= 0.0f) continue;

                if (head == "v5")
                {
                    float obj = q[4];
                    if (obj <= 0.0f) continue;
                    int best = 5;
                    for (int c = 6; c < clsEnd && c < attr; ++c) if (q[c] > q[best]) best = c;
                    score = obj * q[best];
                    cls = best - 5;
                }
                else /* v8 / v11：4 个框 + nc 个类别分数 */
                {
                    int best = 4;
                    for (int c = 5; c < clsEnd && c < attr; ++c) if (q[c] > q[best]) best = c;
                    score = q[best];
                    cls = best - 4;
                }
                x1 = cxx - bw * 0.5f;
                y1 = cyy - bh * 0.5f;
                x2 = cxx + bw * 0.5f;
                y2 = cyy + bh * 0.5f;
            }

            if (score < p.conf) continue;
            if (p.classId >= 0 && cls != p.classId) continue;

            /* 输入图像素 → 原图像素 */
            if (p.stretch)
            {
                const float sx = (float)p.imgW / (float)p.inW;
                const float sy = (float)p.imgH / (float)p.inH;
                x1 *= sx; x2 *= sx; y1 *= sy; y2 *= sy;
            }
            else
            {
                const float r = (p.ratio > 1e-6f) ? p.ratio : 1.0f;
                x1 = (x1 - (float)p.padX) / r;
                x2 = (x2 - (float)p.padX) / r;
                y1 = (y1 - (float)p.padY) / r;
                y2 = (y2 - (float)p.padY) / r;
            }

            boxes.push_back(cv::Rect((int)std::lround(x1), (int)std::lround(y1),
                                     (int)std::lround(x2 - x1), (int)std::lround(y2 - y1)));
            scores.push_back(score);
            classes.push_back(cls);
        }
    }

    if (boxes.empty()) return out;

    std::vector<int> keep;
    if (anyNmsHead)
    {
        /* 模型里已经做过 NMS，这里不再重复（重复 NMS 会吃掉相邻框） */
        for (size_t i = 0; i < boxes.size(); ++i) keep.push_back((int)i);
    }
    else
    {
        cv::dnn::NMSBoxes(boxes, scores, p.conf, p.nms, keep);
    }
    if (keep.empty()) return out;

    int bi = keep[0];
    for (size_t i = 1; i < keep.size(); ++i)
        if (scores[(size_t)keep[i]] > scores[(size_t)bi]) bi = keep[i];

    cv::Rect rr = boxes[(size_t)bi] & cv::Rect(0, 0, p.imgW, p.imgH);
    if (rr.width < 2 || rr.height < 2) return out;

    out.valid = true;
    out.x = rr.x; out.y = rr.y; out.w = rr.width; out.h = rr.height;
    out.cx = rr.x + rr.width * 0.5f;
    out.cy = rr.y + rr.height * 0.5f;
    out.conf = scores[(size_t)bi];
    out.cls = classes[(size_t)bi];
    return out;
}

/* ============================== 工厂 ============================== */

std::unique_ptr<Detector> MakeDetector(const std::string& type,
                                       const Ini& ini,
                                       const std::string& baseDir,
                                       std::string* err)
{
    std::string t = ToLower(type);
    std::unique_ptr<Detector> d;
    if (t == "color")      d.reset(new ColorDetector());
    else if (t == "yolo")  d.reset(new YoloDetector());
    else if (t == "none" || t.empty()) d.reset(new NullDetector());
    else
    {
        if (err) *err = "未知的 detector.type: " + type + "（可选 color / yolo / none）";
        return std::unique_ptr<Detector>();
    }

    if (!d->init(ini, baseDir, err)) return std::unique_ptr<Detector>();
    return d;
}
