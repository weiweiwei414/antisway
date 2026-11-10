// ============================================================================
//  test_math.cpp —— 不需要相机的自检程序
//
//  没有 D415 接线时，用它验证"纯算法"部分有没有写错：
//    1. 彩色检测质心/包围框精度
//    2. 背景有同色干扰时会不会选错
//    3. 跟踪微分器的滤波与角速度输出
//    4. 摆角公式在内参下的数值是否合理
//
//  跑法：test_math.exe      全通过返回 0
// ============================================================================

#include "vs_detector.h"
#include "vs_util.h"
#include "vs_pipeline.h"

#include <opencv2/imgproc.hpp>
#include <windows.h>
#include <cmath>
#include <cstdio>
#include <string>

#ifndef M_PI
#  define M_PI 3.14159265358979323846
#endif

static int g_fail = 0;

static void check(bool cond, const char* what, double got, double want, double tol)
{
    if (cond) printf("  [PASS] %-42s got=%.3f want=%.3f\n", what, got, want);
    else     { printf("  [FAIL] %-42s got=%.3f want=%.3f (tol %.3f)\n", what, got, want, tol); ++g_fail; }
}

/* ---------------- 1&2：彩色标记检测 ---------------- */

static void testColorDetector()
{
    printf("\n[1] 彩色标记检测\n");

    Ini ini;
    ini.set("color.h_min", "0");
    ini.set("color.h_max", "15");
    ini.set("color.s_min", "120");
    ini.set("color.v_min", "80");
    ini.set("color.min_area", "150");

    std::string err;
    std::unique_ptr<Detector> det = MakeDetector("color", ini, "", &err);
    check(det != nullptr, "检测器创建", det ? 1 : 0, 1, 0);
    if (!det) { printf("       %s\n", err.c_str()); return; }

    /* 640x480 纯灰背景 + 红色圆，圆心 (400, 150)，半径 14 */
    cv::Mat img(480, 640, CV_8UC3, cv::Scalar(90, 90, 90));
    cv::circle(img, cv::Point(400, 150), 14, cv::Scalar(0, 0, 255), -1, cv::LINE_AA);

    DetBox b = det->detect(img);
    check(b.valid, "能检出", b.valid ? 1 : 0, 1, 0);
    check(std::fabs(b.cx - 400.0f) < 1.0f, "质心 x 误差 < 1 px", b.cx, 400, 1);
    check(std::fabs(b.cy - 150.0f) < 1.0f, "质心 y 误差 < 1 px", b.cy, 150, 1);
    check(b.w == b.h, "包围框接近正方形", b.w, b.h, 2);

    /* 再加一个更大的同色色块 —— 应该改选面积最大的那个 */
    cv::rectangle(img, cv::Point(40, 300), cv::Point(160, 420), cv::Scalar(0, 0, 255), -1);
    DetBox b2 = det->detect(img);
    check(b2.valid && std::fabs(b2.cx - 100.0f) < 2.0f, "同色干扰下选最大连通域", b2.cx, 100, 2);

    /* 去掉标记 → 应该判无效（而不是乱报一个） */
    cv::Mat empty(480, 640, CV_8UC3, cv::Scalar(90, 90, 90));
    DetBox b3 = det->detect(empty);
    check(!b3.valid, "无目标时返回无效", b3.valid ? 1 : 0, 0, 0);

    /* 红色跨 0 度：h_min=170 > h_max=10 走两段合并分支 */
    Ini ini2;
    ini2.set("color.h_min", "170");
    ini2.set("color.h_max", "10");
    ini2.set("color.s_min", "120");
    ini2.set("color.v_min", "80");
    ini2.set("color.min_area", "150");
    std::unique_ptr<Detector> det2 = MakeDetector("color", ini2, "", &err);
    DetBox b4 = det2 ? det2->detect(img) : DetBox();
    check(b4.valid, "H 跨 0 度的红色不漏检", b4.valid ? 1 : 0, 1, 0);
}

/* ---------------- 2：YOLO11 输出解析 ---------------- */

/* 造一个 [1, attr, cand] 的输出张量（ultralytics 的 onnx 就是这么排的） */
static cv::Mat makeOut(int attr, int cand)
{
    int sz[3] = { 1, attr, cand };
    return cv::Mat(3, sz, CV_32F, cv::Scalar(0));
}
static void setV(cv::Mat& m, int a, int c, float v)
{
    m.ptr<float>(0)[a * m.size[2] + c] = v;
}

static void testYoloParse()
{
    printf("\n[2] YOLO11 输出解析\n");

    YoloParams p;                    /* 640x480 图 → 640x640 letterbox */
    p.imgW = 640; p.imgH = 480;
    p.inW = 640;  p.inH = 640;
    p.padX = 0;   p.padY = 80;
    p.ratio = 1.0f;
    p.conf = 0.40f;
    p.head = "auto";

    /* (a) nc=1 的 v11 头：1×5×1000，只有 5 列 —— 旧代码会因为 cols<6 直接丢帧 */
    {
        cv::Mat o = makeOut(5, 1000);
        setV(o, 0, 5, 100); setV(o, 1, 5, 100);        /* 弱目标 */
        setV(o, 2, 5, 20);  setV(o, 3, 5, 20);
        setV(o, 4, 5, 0.10f);
        setV(o, 0, 9, 320); setV(o, 1, 9, 320);        /* 真目标，输入坐标 */
        setV(o, 2, 9, 80);  setV(o, 3, 9, 60);
        setV(o, 4, 9, 0.92f);

        DetBox b = ParseYoloOutputs(std::vector<cv::Mat>(1, o), p);
        check(b.valid, "单类别 5 列输出能检出", b.valid ? 1 : 0, 1, 0);
        check(std::fabs(b.cx - 320.0f) < 1.5f, "letterbox 去填充后 cx", b.cx, 320, 1.5);
        check(std::fabs(b.cy - 240.0f) < 1.5f, "letterbox 去填充后 cy", b.cy, 240, 1.5);
        check(std::fabs((float)b.w - 80.0f) < 2.0f && std::fabs((float)b.h - 60.0f) < 2.0f,
              "框宽高", (float)b.w, 80, 2);
        check(std::fabs(b.conf - 0.92f) < 0.01f, "取最高分", b.conf, 0.92, 0.01);
        check(b.cls == 0, "类别 = 0", (double)b.cls, 0, 0);
    }

    /* (b) 显式 head=v8 且 attr=6 时不误判成 nms 格式 */
    {
        cv::Mat o = makeOut(6, 1000);
        setV(o, 0, 3, 100); setV(o, 1, 3, 100);
        setV(o, 2, 3, 20);  setV(o, 3, 3, 20);
        setV(o, 4, 3, 0.10f); setV(o, 5, 3, 0.95f);    /* 两个类别分数 */

        YoloParams q = p;
        q.head = "v8";
        q.stretch = true;              /* 拉伸模式：sy = 480/640 = 0.75 */
        DetBox b = ParseYoloOutputs(std::vector<cv::Mat>(1, o), q);
        check(b.valid && b.cls == 1, "v8 头取最大类别", (double)b.cls, 1, 0);
        check(std::fabs(b.conf - 0.95f) < 0.01f, "v8 头置信度", b.conf, 0.95, 0.01);
        check(std::fabs((float)b.h - 15.0f) < 2.0f, "拉伸模式纵向换算", (float)b.h, 15, 2);
    }

    /* (c) 带 nms=True 导出：[1,300,6] x1,y1,x2,y2,score,cls */
    {
        cv::Mat o = makeOut(6, 300);
        setV(o, 0, 0, 10); setV(o, 1, 0, 20);
        setV(o, 2, 0, 110); setV(o, 3, 0, 220);
        setV(o, 4, 0, 0.80f); setV(o, 5, 0, 0.0f);

        YoloParams q = p;
        q.stretch = true;
        DetBox b = ParseYoloOutputs(std::vector<cv::Mat>(1, o), q);
        check(b.valid, "nms 输出格式能识别", b.valid ? 1 : 0, 1, 0);
        check(std::fabs((float)b.x - 10.0f) < 2.0f && std::fabs((float)b.y - 15.0f) < 2.0f,
              "nms 输出角点转框", (float)b.y, 15, 2);
        check(std::fabs((float)b.w - 100.0f) < 2.0f && std::fabs((float)b.h - 150.0f) < 2.0f,
              "nms 输出宽高", (float)b.h, 150, 2);
    }

    /* (d) 空输出 */
    {
        DetBox b = ParseYoloOutputs(std::vector<cv::Mat>(), p);
        check(!b.valid, "空输出返回无效", b.valid ? 1 : 0, 0, 0);
    }
}

/* ---------------- 3：跟踪微分器 ---------------- */

static void testTd()
{
    printf("\n[3] 跟踪微分器\n");

    TdFilter td;
    td.r = 60.0f;

    /* 阶跃 5 度，35 ms 一步，跑 1 秒应稳定到 5 */
    for (int i = 0; i < 30; ++i) td.step(5.0f, 0.035f);
    check(std::fabs(td.x1 - 5.0f) < 0.05f, "阶跃后跟踪值收敛", td.x1, 5, 0.05);

    /* 斜坡 1 度/帧 ≈ 28.6 度/秒，稳态时微分应接近该值 */
    TdFilter td2;
    td2.r = 200.0f;
    float v = 0.0f;
    for (int i = 0; i < 60; ++i) { v += 1.0f; td2.step(v, 0.035f); }
    check(std::fabs(td2.x2 - 28.57f) < 4.0f, "斜坡下角速度 ≈ 28.6 dps", td2.x2, 28.57, 4.0);
}

/* ---------------- 4：摆角公式 ---------------- */

static void testAngle()
{
    printf("\n[4] 摆角公式（物理法 tanθ=ΔX/L）\n");

    /* 模拟 D415 640x480 彩色典型内参 */
    const double fx = 615.0, fy = 615.0;
    const double rope = 800.0;     /* 绳长 800 mm */
    const double z    = 1500.0;    /* 距相机 1500 mm */

    /* 水平位移 100 mm 时，像素偏移 = 100*fx/z */
    double du = 100.0 * fx / z;
    double dx = du * z / fx;
    double ang = atan2(dx, rope) * 180.0 / M_PI;
    check(std::fabs(dx - 100.0) < 1e-6, "像素偏移反算位移 = 100 mm", dx, 100, 1e-6);
    check(std::fabs(ang - 7.125) < 0.01, "摆角 = atan(100/800)", ang, 7.125, 0.01);

    /* 分辨率检查：1 像素对应多少度 */
    double dx1 = 1.0 * z / fx;
    double ang1 = atan2(dx1, rope) * 180.0 / M_PI;
    printf("       1 px 对应 %.4f 度（论文指标要求 < 0.3 度）\n", ang1);
    check(ang1 < 0.3, "摆角分辨率优于 0.3 度", ang1, 0.0, 0.3);
}

/* ---------------- 5：整条解算链路（喂合成帧，不需要相机） ---------------- */

static void testPipelineOffline()
{
    printf("\n[5] 检测→深度→摆角 整条链路（合成帧）\n");

    /* 临时配置：把内参和绳长写死，不读 vision_calib.ini，结果才可复现 */
    std::string iniPath = JoinPath(ExeDirUtf8(), "_selftest.ini");
    std::string body =
        "[camera]\r\n"
        "fx=615\r\nfy=615\r\nppx=320\r\nppy=240\r\n"
        "[detector]\r\ntype=color\r\n"
        "[color]\r\nh_min=0\r\nh_max=15\r\ns_min=120\r\nv_min=80\r\nmin_area=150\r\n"
        "[calib]\r\n"
        "use_calib_file=0\r\n"
        "u0=320\r\nv0=240\r\n"
        "rope_len_mm=800\r\n"
        "angle_mode=0\r\n"
        "[out]\r\nlog_csv=0\r\n";
    WriteFileBytes(Utf8ToWide(iniPath), body.data(), body.size());

    VisionPipeline& vip = VisionPipeline::inst();
    std::string err;
    if (!vip.init(iniPath, &err))
    {
        printf("  [FAIL] 初始化失败: %s\n", err.c_str());
        ++g_fail;
        return;
    }

    /* 合成帧：红色标记在 (400, 300)，深度整幅 1500 mm */
    cv::Mat bgr(480, 640, CV_8UC3, cv::Scalar(90, 90, 90));
    cv::circle(bgr, cv::Point(400, 300), 14, cv::Scalar(0, 0, 255), -1, cv::LINE_AA);
    cv::Mat depth(480, 640, CV_16UC1, cv::Scalar(1500));

    VsResult r;
    bool ok = vip.processFrame(bgr, depth, r);
    check(ok, "离线单帧处理成功", ok ? 1 : 0, 1, 0);
    if (ok)
    {
        check(std::fabs(r.u - 400.0f) < 1.0f, "输出质心 u", r.u, 400, 1);
        check(std::fabs(r.z_mm - 1500.0f) < 1.0f, "输出深度中位数", r.z_mm, 1500, 1);

        /* ΔX = (400-320)*1500/615 = 195.12 mm  →  θ = atan(195.12/800) = 13.70° */
        check(std::fabs(r.off_x_mm - 195.12f) < 0.5f, "水平偏移", r.off_x_mm, 195.12, 0.5);
        check(std::fabs(r.angle_deg - 13.70f) < 0.1f, "摆角", r.angle_deg, 13.70, 0.1);

        /* 预览缓冲：C# 的 PictureBox 走的就是这条路 */
        cv::Mat pc, pd;
        bool hasPrev = vip.preview(pc, pd);
        check(hasPrev && pc.cols == 640 && pc.rows == 480, "预览图尺寸 640x480",
              hasPrev ? (double)pc.cols : 0, 640, 1);
        if (hasPrev && !pc.empty())
        {
            /* 标注层会盖掉圆心（蓝线从零位连到质心），所以数一小块里的红色像素 */
            int red = 0;
            for (int y = 292; y < 308; ++y)
                for (int x = 392; x < 408; ++x)
                {
                    cv::Vec3b p = pc.at<cv::Vec3b>(y, x);
                    if (p[2] > 200 && p[1] < 80 && p[0] < 80) ++red;
                }
            check(red > 10, "预览图是 BGR 且带原图标记", (double)red, 17, 17);
        }
    }

    /* 深度整幅为 0（模拟反光/黑色表面全丢）→ 应判无效而不是输出乱值 */
    cv::Mat zeroDepth(480, 640, CV_16UC1, cv::Scalar(0));
    VsResult r2;
    bool ok2 = vip.processFrame(bgr, zeroDepth, r2);
    check(!ok2, "深度全丢失时判无效", ok2 ? 1 : 0, 0, 0);

    vip.release();
    DeleteFileW(Utf8ToWide(iniPath).c_str());
}

/* ---------------- 6：拿真实 onnx 跑一遍推理（可选，命令行给路径才跑） ---------------- */

static void testYoloRealModel(const char* onnx)
{
    printf("\n[6] 真实模型推理：%s\n", onnx);
    if (!FileExists(Utf8ToWide(onnx)))
    {
        printf("     [SKIP] 文件不存在\n");
        return;
    }

    std::string iniPath = JoinPath(ExeDirUtf8(), "_selftest_yolo.ini");
    std::string body =
        "[camera]\r\nfx=615\r\nfy=615\r\nppx=320\r\nppy=240\r\n"
        "[detector]\r\ntype=yolo\r\n"
        "[yolo]\r\n"
        "model_type=onnx\r\n";
    body += std::string("onnx=") + onnx + "\r\n";
    body +=
        "input_w=640\r\ninput_h=640\r\n"
        "head=auto\r\nnc=1\r\nletterbox=1\r\n"
        "conf=0.02\r\nnms=0.45\r\nswap_rb=1\r\nscale=0.003922\r\n"
        "[calib]\r\nuse_calib_file=0\r\nu0=320\r\nv0=240\r\n"
        "rope_len_mm=800\r\nangle_mode=0\r\n"
        "[out]\r\nlog_csv=0\r\n";
    WriteFileBytes(Utf8ToWide(iniPath), body.data(), body.size());

    VisionPipeline& vip = VisionPipeline::inst();
    vip.release();
    std::string err;
    if (!vip.init(iniPath, &err))
    {
        printf("  [FAIL] 模型加载失败: %s\n", err.c_str());
        ++g_fail;
        DeleteFileW(Utf8ToWide(iniPath).c_str());
        return;
    }
    printf("  [PASS] 模型加载成功\n");

    /* 空白图，只验证"推理 + 后处理"整条不崩、坐标在界内；
       真实精度得拿现场图像另测 */
    cv::Mat bgr(480, 640, CV_8UC3, cv::Scalar(90, 90, 90));
    cv::Mat depth(480, 640, CV_16UC1, cv::Scalar(1500));

    VsResult r;
    bool ok = vip.processFrame(bgr, depth, r);
    printf("  [PASS] 推理未崩，有检出=%d", ok ? 1 : 0);
    if (ok)
        printf("  u=%.1f v=%.1f w=%d conf=%.3f", r.u, r.v, r.box_w, r.confidence);
    printf("\n");

    if (ok)
    {
        bool inside = (r.u >= 0 && r.u <= 640 && r.v >= 0 && r.v <= 480);
        check(inside, "输出坐标在原图范围内", r.u, 320, 320);
    }

    vip.release();
    DeleteFileW(Utf8ToWide(iniPath).c_str());
}

int main(int argc, char** argv)
{
    setvbuf(stdout, nullptr, _IONBF, 0);   /* 崩了也能看到最后一行 */
    SetConsoleOutputCP(65001);
    printf("================ 视觉模块自检 ================\n");
    printf("OpenCV %s\n", CV_VERSION);

    testColorDetector();
    testYoloParse();
    testTd();
    testAngle();
    testPipelineOffline();

    if (argc > 1) testYoloRealModel(argv[1]);
    else          printf("\n[6] 真实模型推理：未指定 onnx 路径，跳过\n"
                         "    用法: test_math.exe 你的模型.onnx\n");

    printf("\n================ 结果 ================\n");
    if (g_fail == 0) printf("全部通过\n");
    else             printf("失败 %d 项\n", g_fail);
    return g_fail == 0 ? 0 : 1;
}
