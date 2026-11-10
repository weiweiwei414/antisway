// ============================================================================
//  vs_gridsample.hpp —— 给 OpenCV 4.7 DNN 补一个 ONNX GridSample 层
//
//  背景：yolo11-mix 的 neck 用了 DySample（内容自适应上采样），导出 ONNX 会
//        生成 opset 16 的 GridSample 算子。OpenCV 4.7 的 DNN 没有这个层，
//        readNetFromONNX 直接报：
//            Can't create layer "...GridSample..." of type "GridSample"
//        本文件用 OpenCV 的自定义层机制注册一个同名实现，模型无需改动，
//        精度与原 PyTorch 模型完全一致。
//
//  实现的是本模型实际用到的组合（从 ONNX 属性读进来）：
//        mode=bilinear, padding_mode=border, align_corners=0
//  语义与 PyTorch F.grid_sample / ONNX GridSample 对齐：
//        grid 形状 [N, H_out, W_out, 2]，最后一维依次是 (x, y)，取值 [-1, 1]
//        align_corners=False: ix = ((gx + 1) * W - 1) / 2
//        border: 先把浮点坐标 clamp 到 [0, W-1] / [0, H-1]，再做双线性
//
//  用法：只需在 vs_detector.cpp 里 #include 本头文件。全局静态注册器会在
//        DLL/EXE 加载时自动调用 LayerFactory::registerLayer，无需改调用逻辑。
// ============================================================================

#ifndef VS_GRIDSAMPLE_HPP
#define VS_GRIDSAMPLE_HPP

#include <opencv2/dnn.hpp>

#include <algorithm>
#include <cmath>
#include <string>
#include <vector>

class GridSampleLayer CV_FINAL : public cv::dnn::Layer
{
public:
    explicit GridSampleLayer(const cv::dnn::LayerParams& p) : cv::dnn::Layer(p)
    {
        alignCorners_ = p.get<int>("align_corners", 0);
        mode_         = p.get<std::string>("mode", "bilinear");
        padding_      = p.get<std::string>("padding_mode", "border");
        // 本模型只用这一组语义；换成别的组合时这里会明确报错，避免静默算错
        CV_Assert(mode_ == "bilinear" && padding_ == "border" && alignCorners_ == 0);
    }

    static cv::Ptr<cv::dnn::Layer> create(cv::dnn::LayerParams& p)
    {
        return cv::Ptr<cv::dnn::Layer>(new GridSampleLayer(p));
    }

    bool getMemoryShapes(const std::vector<std::vector<int>>& inputs,
                         const int,
                         std::vector<std::vector<int>>& outputs,
                         std::vector<std::vector<int>>&) const CV_OVERRIDE
    {
        CV_Assert(inputs.size() == 2 && inputs[0].size() == 4 && inputs[1].size() == 4);
        // data [N,C,H,W] + grid [N,Ho,Wo,2] -> [N,C,Ho,Wo]
        outputs.assign(1, {inputs[0][0], inputs[0][1], inputs[1][1], inputs[1][2]});
        return false;
    }

    void forward(cv::InputArrayOfArrays inputs_arr,
                 cv::OutputArrayOfArrays outputs_arr,
                 cv::OutputArrayOfArrays) CV_OVERRIDE
    {
        std::vector<cv::Mat> ins, outs;
        inputs_arr.getMatVector(ins);
        outputs_arr.getMatVector(outs);
        CV_Assert(ins.size() == 2 && outs.size() == 1);

        const cv::Mat& data = ins[0];
        const cv::Mat& grid = ins[1];
        cv::Mat& out = outs[0];
        CV_Assert(data.dims == 4 && grid.dims == 4 && out.dims == 4);
        CV_Assert(data.type() == CV_32F && grid.type() == CV_32F && out.type() == CV_32F);
        CV_Assert(data.isContinuous() && grid.isContinuous() && out.isContinuous());

        const int N  = data.size[0], C = data.size[1];
        const int H  = data.size[2], W = data.size[3];
        const int Ho = grid.size[1], Wo = grid.size[2];
        CV_Assert(grid.size[3] == 2 && out.size[2] == Ho && out.size[3] == Wo);

        const float* gp = grid.ptr<float>();
        const float* dp = data.ptr<float>();
        float*       op = out.ptr<float>();
        const int iPlane = H * W, oPlane = Ho * Wo;

        for (int n = 0; n < N; ++n)
        {
            for (int oh = 0; oh < Ho; ++oh)
            {
                for (int ow = 0; ow < Wo; ++ow)
                {
                    const float* g = gp + (((n * Ho + oh) * Wo + ow) * 2);
                    // align_corners=0：归一化坐标 -> 像素坐标
                    float ix = ((g[0] + 1.f) * W - 1.f) * 0.5f;
                    float iy = ((g[1] + 1.f) * H - 1.f) * 0.5f;

                    // padding_mode=border：先夹取坐标，再取四个邻点
                    ix = std::min(std::max(ix, 0.f), static_cast<float>(W - 1));
                    iy = std::min(std::max(iy, 0.f), static_cast<float>(H - 1));
                    const float fx = std::floor(ix), fy = std::floor(iy);
                    const int x0 = std::min(static_cast<int>(fx), W - 1);
                    const int y0 = std::min(static_cast<int>(fy), H - 1);
                    const int x1 = std::min(x0 + 1, W - 1);
                    const int y1 = std::min(y0 + 1, H - 1);
                    const float wx = ix - fx, wy = iy - fy;
                    const float w00 = (1.f - wx) * (1.f - wy);
                    const float w10 = wx * (1.f - wy);
                    const float w01 = (1.f - wx) * wy;
                    const float w11 = wx * wy;
                    const int oIdx = oh * Wo + ow;

                    for (int c = 0; c < C; ++c)
                    {
                        const float* src = dp + (n * C + c) * iPlane;
                        op[(n * C + c) * oPlane + oIdx] =
                            w00 * src[y0 * W + x0] + w10 * src[y0 * W + x1] +
                            w01 * src[y1 * W + x0] + w11 * src[y1 * W + x1];
                    }
                }
            }
        }
    }

private:
    int alignCorners_;
    std::string mode_, padding_;
};

/* 全局静态注册器：DLL/EXE 一加载就生效，上层代码不用改 */
static struct GridSampleLayerRegistrar
{
    GridSampleLayerRegistrar()
    {
        cv::dnn::LayerFactory::registerLayer("GridSample", GridSampleLayer::create);
    }
} g_gridsample_registrar;

#endif  // VS_GRIDSAMPLE_HPP
