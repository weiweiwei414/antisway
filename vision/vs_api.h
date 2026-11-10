// ============================================================================
//  vs_api.h —— 视觉模块对外 C 接口（给 C# 通过 P/Invoke 调用）
//
//  设计要点：
//    1. 全部是 C ABI + 定长 POD 结构，不含 STL，跨语言/跨编译器安全；
//    2. 内部单线程采集，vs_get_result 只拷贝一份快照，非阻塞，C# 定时器里
//       100 Hz 调也不会拖慢采集；
//    3. 路径一律传 UTF-8（C# 侧用 Marshal 转），函数内部自己转宽字符，
//       中文路径不会炸。
//
//  C# 侧结构体必须加 [StructLayout(LayoutKind.Sequential)]，字段顺序不能动。
//  sizeof(VsResult) == 112 字节（7×int32=28，4 字节对齐填充，2×uint64=16，
//  11×float=44，cls+reserved[4]=20）。vs_dll.cpp 里有 static_assert 卡这个数。
// ============================================================================
#ifndef VS_API_H
#define VS_API_H

#include <stdint.h>

#if defined(VS_BUILD_DLL)
#  define VS_API __declspec(dllexport)
#elif defined(VS_USE_DLL)
#  define VS_API __declspec(dllimport)
#else
#  define VS_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct VsResult
{
    /* ---- 状态 ---- */
    int32_t  valid;         /* 本帧是否检出吊重: 1 有效 / 0 丢检(其余字段为上一帧或 0) */
    int32_t  frame_index;   /* 帧序号，从 1 开始 */
    int32_t  fps_x10;       /* 处理帧率 ×10，例如 297 表示 29.7 fps */
    int32_t  box_x, box_y, box_w, box_h;  /* 检测框，彩色图像素坐标 */

    /* ---- 时间戳（做时间对齐用，别用 DateTime.Now 代替） ---- */
    uint64_t ts_device_us;  /* 相机硬件时间戳，单位 us */
    uint64_t ts_host_us;    /* 主机收到该帧的时刻，单位 us（1970 起） */

    /* ---- 图像量 ---- */
    float    u, v;          /* 吊重质心像素坐标 */
    float    z_mm;          /* 质心深度，mm（检测框内有效深度中位数） */

    /* ---- 相机坐标系（X 右 / Y 下 / Z 前，单位 mm） ---- */
    float    cam_x_mm, cam_y_mm, cam_z_mm;

    /* ---- 机械量（相对标定的竖直零位） ---- */
    float    off_x_mm;      /* 水平偏移，右正（= 轴1 方向的摆动位移） */
    float    drop_y_mm;     /* 竖直下降，下正 */

    /* ---- 防摇核心观测量 ---- */
    float    angle_deg;     /* 摆角，deg，右正（相对竖直中心线） */
    float    rate_dps;      /* 角速度，deg/s，跟踪微分器输出 */

    float    confidence;    /* 检测置信度 0~1 */
    int32_t  cls;           /* 类别 id，配合 yolo.names 用；-1 = 未知 */
    int32_t  reserved[4];   /* reserved[0] = 连续丢检帧数，其余预留。C# 侧照抄 */
} VsResult;

/* 初始化：读 ini（UTF-8 路径），加载检测模型。成功返回 0，失败返回错误码。
   错误码: -1 配置文件打不开 / -2 检测器初始化失败 / -3 已在运行 */
VS_API int32_t vs_init(const char* ini_path_utf8);

/* 启动采集线程（内含相机开流）。0 = 已启动，其他 = 失败 */
VS_API int32_t vs_start(void);

/* 停止采集（会等采集线程退出，正常 <1 s）。0 = 已停 */
VS_API int32_t vs_stop(void);

/* 释放全部资源，可重复调用 */
VS_API void    vs_release(void);

/* 取最新一帧结果。返回 1 表示拿到新帧，0 表示没有更新（此时 out 不保证有效） */
VS_API int32_t vs_get_result(VsResult* out);

/* 把“当前吊重位置”记为竖直零位（摆角 = 0 的位置），只改内存。
   需吊重静止时调用。0 = 成功 */
VS_API int32_t vs_calibrate_zero(void);

/* 把内存里的标定值写进 <ini 同目录>\vision_calib.ini。0 = 成功 */
VS_API int32_t vs_save_calibration(void);

/* 存一张带标注的当前画面（BMP/JPG，按扩展名判断）。UTF-8 路径。0 = 成功
   注意：out.snapshot_raw=1 时本函数改存【未标注原始帧】，见下面说明。 */
VS_API int32_t vs_snapshot(const char* path_utf8);

/* 存一张【未标注】的原始彩色帧，画面里没有检测框、类别标签、中心线和读数，
   专门用来采集 YOLO 训练集（带框图会被模型学成特征）。UTF-8 路径。0 = 成功 */
VS_API int32_t vs_snapshot_raw(const char* path_utf8);

/* ---- 实时画面（给 C# 的 PictureBox 用）----
   取最新一帧"带检测框和读数的彩色图"的尺寸（像素）。0 = 成功，-1 = 还没有画面 */
VS_API int32_t vs_get_preview_size(int32_t* width, int32_t* height);

/* 把这一帧拷进调用方缓冲区，格式 BGR 行优先，每行 width*3 字节。
   buf_len 不足时返回 -2 且不写入。返回值 = 实际写入字节数（>0），失败为负。 */
VS_API int32_t vs_get_preview(uint8_t* buf, int32_t buf_len);

/* 取一行状态描述（键=值，空格分隔），供日志/调试用。返回写入长度 */
VS_API int32_t vs_get_state(char* buf, int32_t buf_len);

/* 取最近一次错误的文字说明（UTF-8），返回写入长度 */
VS_API int32_t vs_get_last_error(char* buf, int32_t buf_len);

/* 版本字符串，形如 "1.0.0" */
VS_API const char* vs_version(void);

#ifdef __cplusplus
}
#endif

#endif /* VS_API_H */
