// ============================================================================
//  vs_dll.cpp —— 导出给 C# 的 C 接口实现层
//
//  这一层只做三件事：参数校验、错误字符串保存、调用 VisionPipeline。
//  不要在这里加业务逻辑，tuner 和 dll 共用同一份 pipeline。
// ============================================================================
#define VS_BUILD_DLL

#include "vs_api.h"
#include "vs_pipeline.h"

#include <cstring>
#include <string>

static std::string g_lastErr;
static uint64_t    g_lastSeq = 0;

/* 结构体一旦改字段，C# 那边的 C# 结构体必须同步改，这里卡住大小防手滑 */
static_assert(sizeof(VsResult) == 112, "VsResult 布局变了，C# 里的 VsResult 也要跟着改");

static void SetErr(const std::string& e) { g_lastErr = e; }

extern "C" {

VS_API int32_t vs_init(const char* ini_path_utf8)
{
    std::string err;
    std::string path = ini_path_utf8 ? ini_path_utf8 : "";
    if (!VisionPipeline::inst().init(path, &err))
    {
        SetErr(err);
        return -1;
    }
    g_lastErr.clear();
    g_lastSeq = 0;
    return 0;
}

VS_API int32_t vs_start(void)
{
    std::string err;
    if (!VisionPipeline::inst().start(&err))
    {
        SetErr(err);
        return -2;
    }
    g_lastErr.clear();
    return 0;
}

VS_API int32_t vs_stop(void)
{
    VisionPipeline::inst().stop();
    return 0;
}

VS_API void vs_release(void)
{
    VisionPipeline::inst().release();
}

VS_API int32_t vs_get_result(VsResult* out)
{
    if (!out) return 0;
    VisionPipeline& p = VisionPipeline::inst();
    uint64_t s = p.sequence();
    if (s == 0 || s == g_lastSeq) return 0;
    g_lastSeq = s;
    *out = p.result();
    return 1;
}

VS_API int32_t vs_calibrate_zero(void)
{
    std::string err;
    if (!VisionPipeline::inst().calibrateZero(&err))
    {
        SetErr(err);
        return -1;
    }
    return 0;
}

VS_API int32_t vs_save_calibration(void)
{
    std::string err;
    if (!VisionPipeline::inst().saveCalibration(&err))
    {
        SetErr(err);
        return -1;
    }
    return 0;
}

VS_API int32_t vs_snapshot(const char* path_utf8)
{
    if (!path_utf8) return -1;
    std::string err;
    VisionPipeline& pipe = VisionPipeline::inst();
    /* out.snapshot_raw=1 时，上位机原来的「存图」按钮直接输出无标注原始帧：
       C# 侧一行代码都不用改（本机 MSBuild/cl.exe 被禁用，改了也编不了）。
       无论开关怎么设，vs_snapshot_raw() 始终输出原始帧。 */
    bool raw = pipe.config().getB("out.snapshot_raw", false);
    bool ok = raw ? pipe.snapshotRaw(path_utf8, &err)
                  : pipe.snapshot(path_utf8, &err);
    if (!ok)
    {
        SetErr(err);
        return -1;
    }
    return 0;
}

VS_API int32_t vs_snapshot_raw(const char* path_utf8)
{
    if (!path_utf8) return -1;
    std::string err;
    if (!VisionPipeline::inst().snapshotRaw(path_utf8, &err))
    {
        SetErr(err);
        return -1;
    }
    return 0;
}

VS_API int32_t vs_get_preview_size(int32_t* width, int32_t* height)
{
    if (!width || !height) return -1;
    cv::Mat color, depthVis;
    if (!VisionPipeline::inst().preview(color, depthVis) || color.empty()) return -1;
    *width = color.cols;
    *height = color.rows;
    return 0;
}

VS_API int32_t vs_get_preview(uint8_t* buf, int32_t buf_len)
{
    if (!buf || buf_len <= 0) return -1;

    cv::Mat color, depthVis;
    if (!VisionPipeline::inst().preview(color, depthVis) || color.empty()) return -1;

    const int need = color.cols * color.rows * 3;
    if (buf_len < need) return -2;
    if (!color.isContinuous()) color = color.clone();

    memcpy(buf, color.data, (size_t)need);
    return need;
}

VS_API int32_t vs_get_state(char* buf, int32_t buf_len)
{
    if (!buf || buf_len <= 0) return 0;
    std::string s = VisionPipeline::inst().stateText();
    int n = (int)s.size();
    if (n > buf_len - 1) n = buf_len - 1;
    memcpy(buf, s.data(), (size_t)n);
    buf[n] = 0;
    return n;
}

VS_API int32_t vs_get_last_error(char* buf, int32_t buf_len)
{
    if (!buf || buf_len <= 0) return 0;
    std::string s = g_lastErr.empty() ? VisionPipeline::inst().lastError() : g_lastErr;
    int n = (int)s.size();
    if (n > buf_len - 1) n = buf_len - 1;
    memcpy(buf, s.data(), (size_t)n);
    buf[n] = 0;
    return n;
}

VS_API const char* vs_version(void)
{
    return "1.0.0";
}

} /* extern "C" */
