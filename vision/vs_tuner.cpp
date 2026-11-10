// ============================================================================
//  vs_tuner.cpp —— 现场调参/标定用的控制台程序（和 dll 共用同一份 pipeline）
//
//  用法：
//    vs_tuner.exe            读同目录 vision.ini
//    vs_tuner.exe 其它.ini
//
//  操作：
//    拖动滑条   实时改 HSV 阈值，改完按 w 把数值打出来，抄进 vision.ini
//    z          把当前吊重位置定成竖直零位（摆角=0）★ 静止时按
//    s          存一张带标注的图到 exe 目录
//    w          打印当前阈值
//    q / Esc    退出
//
//  ★ 标定顺序（现场照这个来）：
//    1) 吊重静止垂下来，按 z
//    2) 按 s 存图，确认绿色框正好套在标记上、十字在质心
//    3) 用手把吊重拉开 10°，对着量角器或已知位移看 angle 读数
//       对不上就修 calib.rope_len_mm（越小读数越大）
//    4) 按 z 重新定零位，再 z→save，或直接在界面外把值写进 vision_calib.ini
// ============================================================================

#include "vs_pipeline.h"
#include "vs_api.h"

#include <opencv2/imgproc.hpp>
#include <opencv2/highgui.hpp>

#include <windows.h>
#include <cstdio>
#include <string>
#include <chrono>
#include <thread>

static VisionPipeline* g_pipe = nullptr;

static int gHMin = 0, gHMax = 180, gSMin = 80, gSMax = 255, gVMin = 60, gVMax = 255, gMinArea = 200;

static void applyTrackbars(int, void*)
{
    if (!g_pipe) return;
    char v[32];

    /* H 跨 0 度时 h_min > h_max，检测器内部走两段 or 的分支 */
    snprintf(v, sizeof(v), "%d", gHMin);    g_pipe->setDetectorParam("color.h_min", v);
    snprintf(v, sizeof(v), "%d", gHMax);    g_pipe->setDetectorParam("color.h_max", v);
    snprintf(v, sizeof(v), "%d", gSMin);    g_pipe->setDetectorParam("color.s_min", v);
    snprintf(v, sizeof(v), "%d", gSMax);    g_pipe->setDetectorParam("color.s_max", v);
    snprintf(v, sizeof(v), "%d", gVMin);    g_pipe->setDetectorParam("color.v_min", v);
    snprintf(v, sizeof(v), "%d", gVMax);    g_pipe->setDetectorParam("color.v_max", v);
    snprintf(v, sizeof(v), "%d", gMinArea); g_pipe->setDetectorParam("color.min_area", v);
}

static std::string TimeStampName(const char* prefix, const char* ext)
{
    SYSTEMTIME st;
    GetLocalTime(&st);
    char buf[512];
    snprintf(buf, sizeof(buf), "%s%s_%04d%02d%02d_%02d%02d%02d%s",
             ExeDirUtf8().c_str(), prefix, st.wYear, st.wMonth, st.wDay,
             st.wHour, st.wMinute, st.wSecond, ext);
    return std::string(buf);
}

int main(int argc, char** argv)
{
    SetConsoleOutputCP(65001);   /* 让控制台按 UTF-8 显示，中文不乱码 */

    std::string iniPath = (argc > 1) ? std::string(argv[1]) : std::string();

    VisionPipeline& pipe = VisionPipeline::inst();
    g_pipe = &pipe;

    std::string err;
    if (!pipe.init(iniPath, &err))
    {
        printf("[ERR] 初始化失败: %s\n", err.c_str());
        printf("      配置文件应在: %s\n", JoinPath(ExeDirUtf8(), "vision.ini").c_str());
        printf("按回车退出...\n");
        getchar();
        return 1;
    }

    const Ini& ini = pipe.config();

    if (!pipe.start(&err))
    {
        printf("[ERR] 启动相机失败: %s\n", err.c_str());
        printf("      排查顺序: 1) 相机是否插好 2) 别的程序是否占着相机\n");
        printf("                3) 目录完整性级别 (icacls 里应为 Medium)\n");
        printf("按回车退出...\n");
        getchar();
        return 2;
    }

    /* ---- 滑条窗口：初值取 ini 里的当前值 ---- */
    gHMin = ini.getI("color.h_min", 0);
    gHMax = ini.getI("color.h_max", 180);
    gSMin = ini.getI("color.s_min", 80);
    gSMax = ini.getI("color.s_max", 255);
    gVMin = ini.getI("color.v_min", 60);
    gVMax = ini.getI("color.v_max", 255);
    gMinArea = ini.getI("color.min_area", 200);

    const char* kTune = "tune (color HSV)";
    cv::namedWindow(kTune, cv::WINDOW_NORMAL);
    cv::resizeWindow(kTune, 460, 320);
    cv::createTrackbar("H_min", kTune, &gHMin, 180, applyTrackbars);
    cv::createTrackbar("H_max", kTune, &gHMax, 180, applyTrackbars);
    cv::createTrackbar("S_min", kTune, &gSMin, 255, applyTrackbars);
    cv::createTrackbar("S_max", kTune, &gSMax, 255, applyTrackbars);
    cv::createTrackbar("V_min", kTune, &gVMin, 255, applyTrackbars);
    cv::createTrackbar("V_max", kTune, &gVMax, 255, applyTrackbars);
    cv::createTrackbar("min_area", kTune, &gMinArea, 20000, applyTrackbars);
    applyTrackbars(0, nullptr);

    printf("\n");
    printf("  ================ 视觉引导防摇 · 视觉调参 ================\n");
    printf("  检测器      : %s\n", ini.getS("detector.type", "color").c_str());
    printf("  角度算法    : angle_mode=%d  rope_len=%.1f mm  k=%.5f deg/px\n",
           ini.getI("calib.angle_mode", 0),
           ini.getF("calib.rope_len_mm", 0.0f),
           ini.getF("calib.ang_k", 0.05f));
    printf("  零位        : u0=%.1f  v0=%.1f  z0=%.0f mm\n",
           ini.getF("calib.u0", 320.0f), ini.getF("calib.v0", 240.0f),
           ini.getF("calib.z0_mm", 0.0f));
    printf("  -------------------------------------------------------\n");
    printf("  z 定零位   s 存图   w 打印阈值   q 退出\n");
    printf("  ========================================================\n\n");

    const char* kMain = "vision";
    cv::namedWindow(kMain, cv::WINDOW_NORMAL);
    cv::resizeWindow(kMain, 1280, 520);

    auto tPrint = std::chrono::steady_clock::now();
    int  exitCode = 0;

    while (true)
    {
        cv::Mat color, depthVis;
        if (!pipe.preview(color, depthVis))
        {
            if (cv::waitKey(10) == 27) break;
            continue;
        }

        cv::Mat canvas;
        if (!depthVis.empty())
        {
            if (depthVis.size() != color.size())
                cv::resize(depthVis, depthVis, color.size());
            cv::hconcat(color, depthVis, canvas);
        }
        else
        {
            canvas = color;
        }

        cv::imshow(kMain, canvas);
        int key = cv::waitKey(1) & 0xFF;

        if (key == 'q' || key == 27) break;

        if (key == 'z')
        {
            std::string msg;
            if (pipe.calibrateZero(&msg))
            {
                std::string saveMsg;
                pipe.saveCalibration(&saveMsg);
                printf("[OK] 零位已定并写入 vision_calib.ini\n");
            }
            else
            {
                printf("[!!] 定零位失败: %s\n", msg.c_str());
            }
        }

        if (key == 's')
        {
            std::string p = TimeStampName("snap_", ".jpg");
            std::string msg;
            if (pipe.snapshot(p, &msg)) printf("[OK] 已存图(带标注): %s\n", p.c_str());
            else                        printf("[!!] 存图失败: %s\n", msg.c_str());
        }

        if (key == 'r')
        {
            /* 采 YOLO 训练集专用：完全没有检测框/文字，不受 ini 开关影响 */
            std::string p = TimeStampName("raw_", ".jpg");
            std::string msg;
            if (pipe.snapshotRaw(p, &msg)) printf("[OK] 已存原始帧: %s\n", p.c_str());
            else                           printf("[!!] 存原始帧失败: %s\n", msg.c_str());
        }

        if (key == 'w')
        {
            printf("\n; ---- 把下面这 7 行抄进 vision.ini 的 [color] 段 ----\n");
            printf("h_min=%d\nh_max=%d\ns_min=%d\ns_max=%d\nv_min=%d\nv_max=%d\nmin_area=%d\n\n",
                   gHMin, gHMax, gSMin, gSMax, gVMin, gVMax, gMinArea);
        }

        /* 每 0.5 s 往控制台打一行，方便复制到实验记录里 */
        auto now = std::chrono::steady_clock::now();
        if (std::chrono::duration<double>(now - tPrint).count() >= 0.5)
        {
            tPrint = now;
            VsResult r = pipe.result();
            printf("\rframe=%-6d fps=%-5.1f valid=%d  z=%-6.0fmm  angle=%+7.2f deg  rate=%+7.2f dps   ",
                   r.frame_index, r.fps_x10 / 10.0, r.valid, r.z_mm, r.angle_deg, r.rate_dps);
            fflush(stdout);
        }
    }

    pipe.stop();
    pipe.release();
    cv::destroyAllWindows();
    printf("\n已退出。\n");
    return exitCode;
}
