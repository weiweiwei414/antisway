@echo off
rem ===========================================================================
rem  build.bat —— 编译 vs_vision.dll（给 C# 调）+ vs_tuner.exe（现场调参）
rem
rem  ★ 为什么用 Qt 自带的 g++（GCC 7.3）而不是系统里的 GCC 13：
rem    C:\opencv 里的 OpenCV 4.7 是 GCC 7.3 编的，它依赖 libstdc++-6.dll 里的
rem    老符号（例如 _ZNSi5seekgESt4fposIiE）。用 GCC 13 编出来的程序一旦和它
rem    混在同一个进程里，就会出现两套 C++ 运行时：
rem       - 加载 libopencv_imgcodecs470.dll 直接报"找不到指定的程序"
rem       - 侥幸跑起来也会在退出时 / 检测函数里随机崩（0xC0000005）
rem    所以编译器必须和 OpenCV 的构建工具链一致。换 OpenCV 就得同步换这一行。
rem
rem  ★ 本机 MSBuild / cl.exe 被安全策略拦，不能用 VS 工程编译；
rem    要迁回 VS，把源文件加进 vcxproj，并且用 OpenCV 的 MSVC 版。
rem ===========================================================================
setlocal

set CXX=D:\Qt5.12\Tools\mingw730_64\bin\g++.exe
set MINGW=D:\Qt5.12\Tools\mingw730_64\bin
set RS=D:\realsenseD415\RealSense SDK 2.0
set CV=C:\opencv\OpenCV4.7_MinGW_build_with_contrib-master

set STD=-std=c++17
set OPT=-O2
set INC=-I. -isystem "%RS%\include" -isystem "%CV%\include"
set LIBDIR=-L"%CV%\x64\mingw\lib"
set LIBS=%LIBDIR% -lopencv_core470 -lopencv_imgproc470 -lopencv_dnn470 -lopencv_imgcodecs470
set RSLIB="%RS%\bin\x64\realsense2.dll"
set SRC=vs_util.cpp vs_detector.cpp vs_pipeline.cpp

echo [1/6] vs_vision.dll  ...
%CXX% %STD% %OPT% %INC% -shared -o vs_vision.dll vs_dll.cpp %SRC% %LIBS% %RSLIB%
if errorlevel 1 goto fail

echo [2/6] vs_tuner.exe   ...
%CXX% %STD% %OPT% %INC% -o vs_tuner.exe vs_tuner.cpp %SRC% %LIBS% -lopencv_highgui470 %RSLIB%
if errorlevel 1 goto fail

echo [3/6] test_math.exe   （自检，不需要相机）
%CXX% %STD% %OPT% %INC% -o test_math.exe test_math.cpp %SRC% %LIBS% %RSLIB%
if errorlevel 1 goto fail

echo [4/6] modelcheck.exe   （候选模型/图片体检）
%CXX% %STD% %OPT% %INC% -o modelcheck.exe vs_modelcheck.cpp vs_util.cpp vs_detector.cpp %LIBS% %RSLIB%
if errorlevel 1 goto fail

echo [5/6] 拷贝运行时 DLL ...
copy /y "%RS%\bin\x64\realsense2.dll" . >nul
for %%d in (core imgproc dnn imgcodecs highgui) do copy /y "%CV%\x64\mingw\bin\libopencv_%%d470.dll" . >nul
copy /y "%MINGW%\libstdc++-6.dll" . >nul
copy /y "%MINGW%\libgcc_s_seh-1.dll" . >nul
copy /y "%MINGW%\libwinpthread-1.dll" . >nul

echo [6/6] 自检
test_math.exe
if errorlevel 1 (
    echo.
    echo *** 自检没过，先别往下走 ***
    exit /b 1
)

echo.
echo   编好了。
echo   vs_vision.dll  C# 用，接口见 vs_api.h / VisionClient.cs
echo   vs_tuner.exe   现场调参与标定：z 定零位，s 存图，r 存原始帧(采训练集)，w 打印阈值，q 退出
echo.
exit /b 0

:fail
echo.
echo *** 编译失败，看上面第一条 error ***
exit /b 1
