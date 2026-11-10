// ============================================================================
//  VisionClient.cs —— C# 侧对 vs_vision.dll 的封装（直接加进 WinForms 工程即可）
//
//  接口是纯 C ABI，所以 MinGW 编的 DLL 用 DllImport 一样能调，和编译器无关。
//
//  ★ 部署要注意的一条：
//    vs_vision.dll 依赖 realsense2.dll、libopencv_*.dll、libstdc++-6.dll 等，
//    而 Windows 找依赖 DLL 时看的是【宿主程序的目录】，不是 DLL 自己的目录。
//    两个办法，任选其一：
//      a) 把 vision 整个目录里的 dll 全拷到 C# 的 bin\Debug 下（最省事）
//      b) 启动时调用 Vision.PrepareDllDir(@"D:\...\vision")  （下面的实现已包含）
//
//  ★ 用法（WinForms， timer1.Interval = 50）：
//      // 窗体加载
//      if (!Vision.Init())                      // 读 vision.ini
//          MessageBox.Show(Vision.LastError);
//      else if (!Vision.Start())
//          MessageBox.Show(Vision.LastError);
//
//      // timer1_Tick
//      if (Vision.TryRead(out var r) && r.valid == 1)
//      {
//          double angle = r.angleDeg;           // 摆角，右正
//          double rate  = r.rateDps;            // 角速度
//          double offX  = r.offXMm;             // 相对竖直零位的水平位移
//          // …… 喂给防摇控制器
//      }
//      else
//      {
//          // 丢检：不要拿上一帧角度继续发脉冲，保持上一拍输出或减速停车
//      }
//
//      // 窗体关闭
//      Vision.Stop();
//      Vision.Release();
// ============================================================================

using System;
using System.Runtime.InteropServices;

namespace AntiSway.Vision
{
    /// <summary>一帧视觉结果。字段顺序/大小必须与 vs_api.h 里的 VsResult 完全一致。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VsResult
    {
        public int valid;          // 1 = 本帧检出吊重
        public int frameIndex;
        public int fpsX10;
        public int boxX, boxY, boxW, boxH;

        public ulong tsDeviceUs;   // 相机时间戳(us)，做时间对齐用这个
        public ulong tsHostUs;     // 主机收到时刻(us, 1970 起)

        public float u, v;         // 质心像素
        public float zMm;          // 质心深度 mm

        public float camXMm, camYMm, camZMm;  // 相机坐标 mm（X 右 / Y 下 / Z 前）

        public float offXMm;       // 水平偏移 mm，右正
        public float dropYMm;      // 竖直下降 mm，下正

        public float angleDeg;     // 摆角 deg，右正  ← 防摇要的就是这个
        public float rateDps;      // 角速度 deg/s（跟踪微分器输出，已滤波）

        public float confidence;

        public int cls;                        // 类别 id（配合 vision.ini 的 yolo.names）
        public int res0, res1, res2, res3;     // res0 = 连续丢检帧数，其余预留
    }

    public static class Vision
    {
        private const string Dll = "vs_vision.dll";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_init([MarshalAs(UnmanagedType.LPUTF8Str)] string iniPath);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_start();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_stop();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vs_release();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_get_result(out VsResult r);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_calibrate_zero();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_save_calibration();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_snapshot([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_get_preview_size(out int w, out int h);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_get_preview(byte[] buf, int bufLen);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_get_state(byte[] buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int vs_get_last_error(byte[] buf, int len);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr vs_version();

        /// <summary>DLL 版本号，形如 "1.0.0"。用来确认加载的是哪一份。</summary>
        public static string Version
        {
            get
            {
                try
                {
                    IntPtr p = vs_version();
                    return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p);
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }

        /// <summary>最近一次失败的原因（中文）。</summary>
        public static string LastError { get; private set; } = "";

        /// <summary>
        /// 让 Windows 到指定目录找 vs_vision.dll 及其依赖。DLL 和 C# 程序不在同一目录时必须先调这个。
        /// </summary>
        public static bool PrepareDllDir(string visionDir)
        {
            return SetDllDirectory(visionDir);
        }

        /// <summary>读配置并加载检测器。iniPath 传 null 表示用 DLL 同目录的 vision.ini。</summary>
        public static bool Init(string iniPath = null)
        {
            if (vs_init(iniPath) == 0) { LastError = ""; return true; }
            LastError = ReadError();
            return false;
        }

        /// <summary>开相机、起采集线程。失败会等约 0.1~15 s（没插相机会立刻返回）。</summary>
        public static bool Start()
        {
            if (vs_start() == 0) { LastError = ""; return true; }
            LastError = ReadError();
            return false;
        }

        public static void Stop() { vs_stop(); }

        public static void Release() { vs_release(); }

        /// <summary>取最新一帧。返回 false 表示这一拍没有新数据，不要用它做控制。</summary>
        public static bool TryRead(out VsResult r)
        {
            return vs_get_result(out r) == 1;
        }

        /// <summary>把吊重当前静止位置定成摆角 0 位（"竖直中心线"）。吊重要静止。</summary>
        public static bool CalibrateZero()
        {
            if (vs_calibrate_zero() == 0) return true;
            LastError = ReadError();
            return false;
        }

        /// <summary>把零位写进 vision_calib.ini，下次启动直接生效。</summary>
        public static bool SaveCalibration()
        {
            if (vs_save_calibration() == 0) return true;
            LastError = ReadError();
            return false;
        }

        public static bool Snapshot(string pathJpg)
        {
            return vs_snapshot(pathJpg) == 0;
        }

        /// <summary>取当前帧尺寸（像素）。还没有画面时返回 false。</summary>
        public static bool GetPreviewSize(out int width, out int height)
        {
            return vs_get_preview_size(out width, out height) == 0;
        }

        /// <summary>
        /// 把当前帧（带检测框和读数）拷进 buffer，格式 BGR 行优先，每行 width*3 字节。
        /// 返回写入字节数；负值表示失败（-2 = 缓冲区太小）。
        /// </summary>
        public static int CopyPreview(byte[] buffer)
        {
            if (buffer == null) return -1;
            return vs_get_preview(buffer, buffer.Length);
        }

        /// <summary>"running=1 frames=1234 fps=29.7 ..." 一行状态，丢日志里排查用。</summary>
        public static string State
        {
            get
            {
                var buf = new byte[512];
                int n = vs_get_state(buf, buf.Length);
                return System.Text.Encoding.UTF8.GetString(buf, 0, n);
            }
        }

        private static string ReadError()
        {
            var buf = new byte[512];
            int n = vs_get_last_error(buf, buf.Length);
            return System.Text.Encoding.UTF8.GetString(buf, 0, n);
        }
    }
}
