// ============================================================================
//  vs_detector.h —— 吊重检测
//
//  两条并行路子，ini 里 detector.type 选：
//    color  彩色标记 + HSV 阈值 + 连通域质心   —— 不依赖训练，现场就能跑
//    yolo   OpenCV DNN 推理（darknet .cfg/.weights 或 onnx）
//
//  ★ 为什么保留 color：
//    摆角是实时反馈量，30 fps 下要求"每帧都有"。彩色球/色块在固定光照下
//    比小目标检测稳得多，先用它把标定、角度、接口全打通，再换 YOLO 只换一行
//    配置，其余代码不用动。两条路输出的都是 DetBox，下游无感。
// ============================================================================
#ifndef VS_DETECTOR_H
#define VS_DETECTOR_H

#include <opencv2/core.hpp>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

#include "vs_util.h"

struct DetBox
{
    bool  valid = false;
    int   x = 0, y = 0, w = 0, h = 0;   /* 原图像素 */
    float cx = 0.0f, cy = 0.0f;         /* 质心像素 */
    float conf = 0.0f;
    int   cls = -1;
};

class Detector
{
public:
    virtual ~Detector() {}
    virtual bool init(const Ini& ini, const std::string& baseDir, std::string* err) = 0;
    virtual DetBox detect(const cv::Mat& bgr) = 0;
    virtual const char* typeName() const = 0;

    /* 运行中热改参数（调色现场拉滑条用）。只支持 color.* 里的阈值类键；
       内部加锁，可在采集线程跑着的时候调。 */
    virtual void setParam(const std::string& /*key*/, const std::string& /*val*/) {}
};

/* type: "color" / "yolo" / "none"（none 恒返回无效，用于只测深度） */
std::unique_ptr<Detector> MakeDetector(const std::string& type,
                                       const Ini& ini,
                                       const std::string& baseDir,
                                       std::string* err);

/* ============================ YOLO 后处理 ============================
   单独抽出来是为了能脱离模型做单元测试（test_math.cpp 里有用例）。
   支持三种排布：
     v8     [1, 4+nc, N] 或 [1, N, 4+nc]   yolov8 / yolov11 默认导出，无 obj 项
     v5     [1, 5+nc, N] 或 [1, N, 5+nc]   yolov5，conf = obj × cls
     nms    [1, K, 6] = x1,y1,x2,y2,score,cls
            导出时带了 nms=True 才有；直接给框，不用再做 NMS
   head=auto 时：cols==6 判成 nms，其余按 v8（yolov11 走这条）
   ==================================================================== */
struct YoloParams
{
    int   inW = 640, inH = 640;          /* 网络输入尺寸 */
    int   imgW = 640, imgH = 480;        /* 原图尺寸 */
    float conf = 0.40f, nms = 0.45f;
    int   classId = -1;
    int   nc = 0;                        /* 类别数；0 = 按列数推算（attr-4） */
    std::string head = "auto";           /* auto / v5 / v8 / nms */
    int   padX = 0, padY = 0;            /* letterbox 左侧/上侧填充 */
    float ratio = 1.0f;                  /* letterbox 缩放比 */
    bool  stretch = false;               /* true = 拉伸到输入尺寸（无填充） */
};

DetBox ParseYoloOutputs(const std::vector<cv::Mat>& outs, const YoloParams& p);

/* 等比缩放 + 居中填充到 inW×inH（填充值 114，与 ultralytics 一致）。
   返回 3 通道图像；padX/padY/ratio 用于把坐标映射回原图。 */
cv::Mat YoloLetterbox(const cv::Mat& bgr, int inW, int inH,
                      int* padX, int* padY, float* ratio);

#endif /* VS_DETECTOR_H */
