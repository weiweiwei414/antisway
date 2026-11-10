# 桥式卸船机防摇实验台 · 上位机

两轴运动控制卡上位机（C# WinForms）+ Intel RealSense D415 视觉测量模块（C++ DLL），
**一个窗口**同时显示板卡状态与视觉摆角读数。

> 本分支 `vision-integration` 基于 `main` 建立，收录 2026-11-10 的视觉集成改动。
> `main` 保持 10-27 的旧版不动。

## 硬件

| 部件 | 型号 / 参数 |
|---|---|
| 运动控制卡 | 博派 ETH_NEC_NLM，2 轴，网口 |
| 轴1 | 直线模组（导程 72 mm）+ 57 闭环步进 + HB808C（3200 脉冲/转，编码器接驱动器） |
| 轴2 | 42 步进 + TB6600（1600 脉冲/转，开环）+ Φ10 卷筒，无编码器、无外部信号 |
| 当量 | 轴1 44.4444 脉冲/mm，轴2 50.9296 脉冲/mm |
| 信号 | 原点 Home1（常开）、X0/X1 限位（常闭），由卡内硬限位处理 |
| 视觉 | Intel RealSense D415，彩色/深度 640×480@30，深度对齐到彩色 |

连接参数（IP / 端口）见 `WindowsFormsApp1/Form1.cs` 里的 `GA_Open`，按现场网络修改。

## 目录

```
WindowsFormsApp1/   上位机工程：Form1.cs（逻辑）、Form1.Designer.cs（界面）、csproj、sln
vision/             D415 视觉模块 C++ 源码、build.bat、vision.ini、VisionClient.cs
vision-cpp/         D415 读深度的最小示例（早期联调，与主链路无关）
```

界面左侧是原来的控制卡面板（连接/复位、点动、连续、回零、两轴联动、限位状态），
右侧是视觉面板（实时画面、摆角、角速度、水平偏移、深度、FPS、开流/停止/定零位/存图），
左下角是视觉事件日志。板卡逻辑与视觉刷新用**两个独立 50 ms 定时器**，互不阻塞。

## 构建

### 1. 上位机（必须 x64）

`vs_vision.dll` 只有 64 位版本，csproj 已把所有配置的 `PlatformTarget` 固定为 x64。

```bash
dotnet build WindowsFormsApp1/WindowsFormsApp1.csproj -p:Configuration=Debug -p:Platform=x64
```

用 32 位进程运行会抛 `BadImageFormatException`。

### 2. 控制卡依赖 DLL（**仓库不含，需自行放置**）

从厂商例程包 `C#例程源代码及库文件(1).rar` 取以下三个文件：

- `MultiCardCS.dll`、`MultiCardCLR.dll` → `WindowsFormsApp1/libs/`
- `MultiCard.dll` → `WindowsFormsApp1/`（以及运行输出目录）

`WindowsFormsApp1.csproj` 通过 `HintPath` 引用前两个。

### 3. 视觉模块 DLL

在 `vision/` 目录下运行 `build.bat`。**编译器必须和 OpenCV 的构建工具链一致**：
本机 OpenCV 4.7 是 GCC 7.3 编的，脚本默认用
`D:\Qt5.12\Tools\mingw730_64\bin\g++.exe`。用新版 MinGW 会出现两套 C++ 运行时，
表现为加载期 `WinError 127` 或运行中 `0xC0000005`。换机器时按实际路径改
`build.bat` 顶部的 `CXX / MINGW / RS / CV` 四个变量。

运行时还需要把以下 DLL 放在 `vision/` 目录（随 RealSense SDK / OpenCV / MinGW 分发，
**本仓库不提供**）：

```
realsense2.dll  libopencv_core470.dll  libopencv_imgproc470.dll
libopencv_dnn470.dll  libopencv_imgcodecs470.dll  libopencv_highgui470.dll
libstdc++-6.dll  libgcc_s_seh-1.dll  libwinpthread-1.dll
```

模型 `best.onnx` 需自行训练导出，放在 `vision/` 与 `vision.ini` 同目录。
当前模型为单类 `grab`，导出要求 **opset 16**（neck 的 DySample 会生成 GridSample 算子，
由 `vs_gridsample.hpp` 注册自定义层补齐）；`vision.ini` 的 `input_w/h` 必须与导出 imgsz 一致。

### 4. 运行时目录查找

程序启动后从 exe 所在目录向上最多 8 级查找 `vision\vision.ini`，找到的目录通过
`SetDllDirectory` 加入原生 DLL 搜索路径。因此**不要把 DLL 拷进 `bin`**：
改完 C++ 只需重跑 `vision\build.bat`，C# 无需重新生成。
`axis-cal.ini` 模板在 `WindowsFormsApp1/`，需拷到实际输出目录 `bin\x64\Debug\`。

## 配置

- `vision/vision.ini`：相机分辨率、检测器（color/yolo/none）、YOLO 参数、标定、滤波、输出
- `vision/vision_calib.ini`：现场定零位结果（与 ini 同目录）
- `WindowsFormsApp1/axis-cal.ini`：`ppm1` / `ppm2` / `lmtnc`

## 当前限制（重要）

**视觉只读，不参与运动控制。** 摆角、角速度只用于界面显示和写日志，绝不下发板卡指令。

原因：`vision.ini` 里 `rope_len_mm=0`，摆角实际走 `ang_k × (u-u0) = 0.05·Δu` 的
**占位近似值**，不是真实摆角；悬点也未必在相机视野内。
在接入防摇闭环之前，必须先完成现场摆角标定（两点法或绳长 + 小车位置补偿），
并单独设计控制器。

其他已知约束：

- 轴1 编码器接在 HB808C 驱动器上、不进控制卡，`GA_GetAxisEncPos` 读不到是正常现象
- 轴2 无编码器、无限位，闭环只应控轴1；急停减速度按当量折算偏大，开环步进有丢步风险
- 停车一律用 `GA_Stop(mask, 0)`，不要用 `GA_SetVel(ax, 0)`
- 压到限位后需等待 5 秒才允许反向退出（`EscapeLimit`）
- 若 `运动控制卡` 一类目录被设为**低完整性级别**，D415 SDK 创建 `Global\` 互斥体会失败；
  用 `icacls "<目录>" /setintegritylevel "(OI)(CI)M" /T /C /Q` 修复
- 中文路径下 ONNX 通过字节流加载，不要改回窄字符路径接口

## 数据记录

- `vision/logs/vision_*.csv`：DLL 输出，含视觉量与相机/主机时间戳
- `日志/vision_axis_yyyy-MM-dd.csv`：C# 输出，每帧摆角 + 同一时刻轴1 位置/速度/联动方向，
  用于画「小车运动 ↔ 摆动」对齐曲线

两类日志与实验存图均不纳入版本控制。

## 第三方组件

Intel RealSense SDK、OpenCV、博派 MultiCard 库及 Qt/MinGW 运行时均**不随本仓库分发**，
使用时请遵守各自的许可条款。本仓库当前未指定开源许可证。
