// ============================================================================
//  D415 最小示例：读深度
//
//  只用 rs2::pipeline，不单独创建 rs2::context。
//  原因：每个 context 都会独立创建一次设备对象，同一个相机被两个句柄打开时，
//  SDK 内部用于串行化的命名互斥体会冲突，报
//  "CreateNamedMutex returned Mutex_TotalFailure / rs2_create_device"。
//  设备信息从 start() 之后的 profile 里取，不需要第二个 context。
//
//  编译前提（手动配置）：
//    - 平台 x64
//    - 附加包含目录：D:\realsenseD415\RealSense SDK 2.0\include
//    - 附加库目录  ：D:\realsenseD415\RealSense SDK 2.0\lib\x64
//    - 附加依赖项  ：realsense2.lib
//    - 生成后事件把 realsense2.dll 拷到输出目录
// ============================================================================

#include <librealsense2/rs.hpp>
#include <iostream>
#include <iomanip>

int main() try
{
    rs2::pipeline pipe;          // 只建这一个，全局唯一
    rs2::config   cfg;

    // 640x480、16 位深度（单位毫米）、30 帧/秒
    cfg.enable_stream(RS2_STREAM_DEPTH, 640, 480, RS2_FORMAT_Z16, 30);

    pipe.start(cfg);             // 启动流

    // ------- 设备信息从已启动的 pipeline 里取，不另建 context -------
    rs2::device dev = pipe.get_active_profile().get_device();
    std::cout << "型号   : " << dev.get_info(RS2_CAMERA_INFO_NAME) << "\n"
              << "固件   : " << dev.get_info(RS2_CAMERA_INFO_FIRMWARE_VERSION) << "\n"
              << "序列号 : " << dev.get_info(RS2_CAMERA_INFO_SERIAL_NUMBER) << "\n\n"
              << "深度流已启动，开始取帧...\n";

    // ---------------------------- 循环读 100 帧 ----------------------------
    for (int i = 0; i < 100; ++i)
    {
        rs2::frameset  frames = pipe.wait_for_frames();   // 阻塞到下一帧
        rs2::depth_frame depth = frames.get_depth_frame();
        if (!depth) continue;

        int   w = depth.get_width();
        int   h = depth.get_height();
        float d = depth.get_distance(w / 2, h / 2);       // 画面中心，单位：米

        std::cout << "第 " << std::setw(3) << i + 1 << " 帧   中心点 "
                  << std::fixed << std::setprecision(0) << d * 1000 << " mm\n";
    }

    pipe.stop();
    std::cout << "\n完成。\n";
    return 0;
}
catch (const rs2::error& e)
{
    std::cerr << "RealSense 出错：" << e.what()
              << "\n  出错函数：" << e.get_failed_function() << "\n";
    return 1;
}
