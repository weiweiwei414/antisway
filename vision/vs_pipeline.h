// ============================================================================
//  vs_pipeline.h —— 采集 + 检测 + 三维解算 + 摆角滤波 的主链路
//
//  单实例（进程内单相机）。采集在独立线程跑，外部只读快照。
//
//  角度两种算法（ini 里 calib.angle_mode）：
//    0  物理法（默认）：θ = atan( ΔX / L )
//         ΔX = (u-u0)·Z/fx   —— 吊重水平位移，mm，Z 取当帧深度
//         L  = calib.rope_len_mm 吊重质心到悬点的绳长，mm
//       优点：不受图像分辨率/安装距离影响，换镜头换距离不用重标 k；
//             缺点是必须量一下绳长（卷筒 Φ10，行程看着给）。
//    1  两点法（论文写法）：θ = k·(u-u0)，k 单位 度/像素。
//       优点：只要 u0 和一个 k，最省事；缺点：角度大时 tan 非线性误差，
//             且相机前后移动就得重标 k。10° 以内误差 < 0.5%。
// ============================================================================
#ifndef VS_PIPELINE_H
#define VS_PIPELINE_H

#include <opencv2/core.hpp>

#include <algorithm>
#include <atomic>
#include <cmath>
#include <condition_variable>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#include "vs_api.h"
#include "vs_detector.h"
#include "vs_util.h"

/* 二阶离散跟踪微分器（韩京清）：同时给出滤波后的量与其微分。
   直接对摆角差分做控制，开关项会把噪声放大成抖振，所以必须过 TD。
   放在头文件里是为了自检程序 test_math.exe 能单独测它，不用把相机 SDK 拖进来。 */
struct TdFilter
{
    float x1 = 0.0f;   /* 跟踪值 */
    float x2 = 0.0f;   /* 微分值 */
    bool  inited = false;
    float r = 40.0f;   /* 跟踪速度，越大跟得越快也越抖，deg/s^2 量级 */

    void reset() { inited = false; x1 = x2 = 0.0f; }

    static float sgn(float x) { return x > 0.0f ? 1.0f : (x < 0.0f ? -1.0f : 0.0f); }

    void step(float v, float dt)
    {
        if (!inited) { x1 = v; x2 = 0.0f; inited = true; return; }
        if (dt <= 1e-4f) return;
        if (dt > 0.2f) dt = 0.2f;

        const float h  = dt;
        const float h0 = h * 3.0f;      /* 滤波因子取 3h，抑制像素抖动 */
        const float R  = r;
        const float d  = std::max(R * h0 * h0, 1e-9f);

        float a0 = h0 * x2;
        float y  = x1 - v + a0;
        float a1 = std::sqrt(d * (d + 8.0f * std::fabs(y)));
        float a2 = a0 + sgn(y) * (a1 - d) * 0.5f;
        float sy = (sgn(y + d) - sgn(y - d)) * 0.5f;
        float a  = (a0 + y - a2) * sy + a2;
        float sa = (sgn(a + d) - sgn(a - d)) * 0.5f;
        float fhan = -R * (a / d - sgn(a)) * sa - R * sgn(a);

        x1 += h * x2;
        x2 += h * fhan;
    }
};

class VisionPipeline
{
public:
    static VisionPipeline& inst();

    bool init(const std::string& iniPathUtf8, std::string* err);
    bool start(std::string* err);
    void stop();
    void release();
    bool running() const { return running_.load(); }

    VsResult result() const;
    uint64_t sequence() const;   /* 每出一帧 +1，供 C# 判断“有没有新数据” */
    bool calibrateZero(std::string* err);
    bool saveCalibration(std::string* err);
    bool snapshot(const std::string& pathUtf8, std::string* err);
    /* 存【未标注】的原始彩色帧：画面里没有检测框和文字，采集训练集专用 */
    bool snapshotRaw(const std::string& pathUtf8, std::string* err);
    std::string stateText() const;
    bool preview(cv::Mat& color, cv::Mat& depthVis) const;
    std::string lastError() const;
    const Ini& config() const { return ini_; }

    /* 离线单帧处理：喂一张彩色 + 一张深度(mm, CV_16U)，走和在线完全相同的
       检测/解算路径。不需要相机，用于自检、以及以后拿录像帧回放复算。 */
    bool processFrame(const cv::Mat& bgr, const cv::Mat& depth, VsResult& r);

    /* 调试/调参用：临时改 HSV 阈值（tuner 的滑条回调） */
    void setDetectorParam(const std::string& key, const std::string& val);

private:
    VisionPipeline() {}
    ~VisionPipeline() {}
    VisionPipeline(const VisionPipeline&);
    VisionPipeline& operator=(const VisionPipeline&);

    void loop();
    bool measure(const DetBox& box, const cv::Mat& depth, VsResult& r);
    void annotate(const cv::Mat& bgr, const VsResult& r, cv::Mat& out) const;
    void buildDepthVis(const cv::Mat& depth, cv::Mat& out) const;
    void pushResult(const VsResult& r, const cv::Mat& rawBgr,
                    const cv::Mat& annotated, const cv::Mat& depth);
    void setError(const std::string& e);

    /* 类别 id → 名字（来自 ini 的 yolo.names，逗号分隔） */
    std::string className(int cls) const;
    std::vector<std::string> classNames_;

    Ini ini_;
    Ini calib_;
    std::string exeDir_;
    std::string iniDir_;      /* ini 所在目录，相对路径都以它为基准 */
    std::string iniPath_;
    std::string calibPath_;
    std::unique_ptr<Detector> det_;

    std::thread th_;
    std::atomic<bool> run_{ false };
    std::atomic<bool> running_{ false };

    /* 启动握手：采集线程开流成功/失败都通过它通知 start()，
       不用轮询 + 猜超时（实测无相机时 SDK 要 15 s 才抛异常，
       轮询会误报成"启动超时"）。 */
    enum StartState { ST_IDLE = 0, ST_STARTING, ST_RUNNING, ST_FAILED };
    std::mutex              cvMtx_;
    std::condition_variable cv_;
    StartState              st_ = ST_IDLE;

    mutable std::mutex mtx_;
    VsResult latest_ = VsResult();   /* 首帧之前 C# 读到的是全 0，不是野值 */
    uint64_t seq_ = 0;
    cv::Mat prevColor_, prevDepth_;   /* 带检测框/读数的预览帧 */
    cv::Mat rawColor_;                /* 未标注原始帧，采集训练图用 */
    std::string err_;
    std::string state_;

    /* 相机内参（彩色） */
    float fx_ = 640.0f, fy_ = 640.0f, ppx_ = 320.0f, ppy_ = 240.0f;

    /* 标定：竖直零位 */
    float u0_ = 320.0f;
    float v0_ = 240.0f;
    float z0_ = 0.0f;
    float angK_ = 0.05f;       /* 度/像素，angle_mode=1 */
    float ropeLen_ = 0.0f;     /* mm，angle_mode=0 */
    int   angleMode_ = 0;

    /* 滤波 */
    TdFilter td_;
    float ema_ = 0.6f;
    float lastAngle_ = 0.0f;
    bool  haveLast_ = false;
    int64_t lastFrameUs_ = 0;

    /* 诊断 */
    int   lostCount_ = 0;
    int   frameCount_ = 0;
    std::string lastDetectorType_;
};

#endif /* VS_PIPELINE_H */
