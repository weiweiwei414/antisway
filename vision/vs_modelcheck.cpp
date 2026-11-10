// ============================================================================
//  vs_modelcheck.cpp —— 候选模型 / 候选图片 快速体检工具
//
//  用法：
//    modelcheck.exe 模型.onnx                    只看模型信息（类别表、输入尺寸）
//    modelcheck.exe 模型.onnx 图1.jpg 图2.png    再对每张图跑一遍，报最高分和框
//
//  它能回答两个问题：
//    1) 这个 onnx 到底有几类、叫什么、输入多大（从 onnx 元数据里读）
//    2) 这个模型认不认我这张图（分数多少、框在哪）
//
//  ★ 为什么阈值压到 0.01：
//    要看的是"这个模型在这张图上最高能给多少分"，而不是"过没过 0.4 的门槛"。
//    低于 0.1 基本等于没响应。
//
//  ★ 走的是 vs_detector 里生产同一条代码路径（letterbox、v11 头解析、NMS），
//    所以这里的结论和 DLL 里的行为一致，不会出现"工具说行、上位机说不行"。
// ============================================================================

#include "vs_detector.h"
#include "vs_util.h"

#include <opencv2/imgcodecs.hpp>
#include <windows.h>
#include <cstdio>
#include <cstring>
#include <string>

/* 在 onnx 字节流里找 "键 + 紧跟的 {…} 或 […]"。
   onnx 的 metadata_props 是明文键值对，所以直接扫字节就够，
   不用引 onnx 的 protobuf 依赖。 */
static std::string FindMeta(const std::string& raw, const char* key, char open, char close)
{
    size_t p = raw.find(key);
    while (p != std::string::npos)
    {
        size_t b = raw.find(open, p);
        if (b != std::string::npos && b - p < 64)
        {
            size_t e = raw.find(close, b);
            if (e != std::string::npos && e - b < 400)
                return raw.substr(b, e - b + 1);
        }
        p = raw.find(key, p + 1);
    }
    return std::string();
}

/* 从 "[640, 640]" 里取第一维 */
static int ParseFirstInt(const std::string& s, int def)
{
    for (size_t i = 0; i < s.size(); ++i)
    {
        if (s[i] >= '0' && s[i] <= '9')
        {
            int v = 0;
            while (i < s.size() && s[i] >= '0' && s[i] <= '9') { v = v * 10 + (s[i] - '0'); ++i; }
            return v;
        }
    }
    return def;
}

/* ---------- 把 vision.ini 里的 onnx / nc / names 改成新模型 ---------- */

static std::string TrimStr(const std::string& s)
{
    size_t a = 0, b = s.size();
    while (a < b && (unsigned char)s[a] <= ' ') ++a;
    while (b > a && (unsigned char)s[b - 1] <= ' ') --b;
    return s.substr(a, b - a);
}

/* 数 names 里有几类：形如 {0: 'a', 1: 'b'}，数 ": '" 出现次数 */
static int CountClasses(const std::string& names)
{
    int n = 0;
    size_t p = 0;
    while ((p = names.find(": '", p)) != std::string::npos) { ++n; p += 3; }
    return n;
}

/* 从 {0: 'grab', 1: 'fm'} 里按顺序抽出引号内的名字，拼成 "grab,fm"
   —— ini 的 names 要的是逗号分隔列表，不是整个字典 */
static std::string NamesToList(const std::string& raw)
{
    std::string out;
    std::string cur;
    bool in = false;
    for (size_t i = 0; i < raw.size(); ++i)
    {
        char c = raw[i];
        if (c == '\'')
        {
            if (in)
            {
                if (!out.empty()) out += ",";
                out += cur;
                cur.clear();
            }
            in = !in;
        }
        else if (in) cur += c;
    }
    return out;
}

/* 只改 [yolo] 段里指定的键，其它内容（注释、顺序、换行）原样保留 */
static std::string RewriteIniYolo(const std::string& text,
                                  const std::string& onnxFile,
                                  int nc,
                                  const std::string& names,
                                  bool* okOnnx, bool* okNc, bool* okNames)
{
    std::string out;
    std::string section;
    size_t pos = 0;
    *okOnnx = *okNc = *okNames = false;

    while (pos < text.size())
    {
        size_t nl = text.find('\n', pos);
        std::string line = (nl == std::string::npos) ? text.substr(pos)
                                                     : text.substr(pos, nl - pos);

        std::string body = line;
        std::string eol;
        if (!body.empty() && body[body.size() - 1] == '\r')
        {
            body.erase(body.size() - 1);
            eol = "\r";
        }

        const std::string t = TrimStr(body);

        if (!t.empty() && t[0] == '[' && t[t.size() - 1] == ']')
        {
            section = t.substr(1, t.size() - 2);
            for (size_t i = 0; i < section.size(); ++i)
                if (section[i] >= 'A' && section[i] <= 'Z') section[i] = (char)(section[i] + 32);
        }
        else if (section == "yolo" && !t.empty() && t[0] != ';' && t[0] != '#')
        {
            size_t eq = body.find('=');
            if (eq != std::string::npos)
            {
                std::string key = TrimStr(body.substr(0, eq));
                std::string lk = key;
                for (size_t i = 0; i < lk.size(); ++i)
                    if (lk[i] >= 'A' && lk[i] <= 'Z') lk[i] = (char)(lk[i] + 32);

                std::string val;
                bool hit = true;
                if      (lk == "onnx")  { val = onnxFile;           *okOnnx  = true; }
                else if (lk == "nc")    { val = std::to_string(nc); *okNc    = true; }
                else if (lk == "names") { val = names;              *okNames = true; }
                else hit = false;

                if (hit) body = key + "=" + val;
            }
        }

        out += body + eol;
        if (nl != std::string::npos) out += "\n";
        pos = (nl == std::string::npos) ? text.size() : nl + 1;
    }
    return out;
}

int main(int argc, char** argv)
{
    setvbuf(stdout, nullptr, _IONBF, 0);
    SetConsoleOutputCP(65001);

    if (argc < 2)
    {
        printf("用法:\n");
        printf("  modelcheck.exe 模型.onnx                看模型里有几类、叫什么\n");
        printf("  modelcheck.exe 模型.onnx 图1.jpg ...    再对图片跑一遍，报最高分和框\n");
        return 1;
    }

    const std::string model = argv[1];

    /* ---- 1) 先读元数据（不依赖 OpenCV，读不到也不影响后面）---- */
    std::string raw;
    if (!ReadFileBytes(Utf8ToWide(model), raw) || raw.empty())
    {
        printf("[ERR] 读不到模型文件: %s\n", model.c_str());
        return 1;
    }
    printf("=========== 模型信息 ===========\n");
    printf("文件      : %s  (%.1f MB)\n", model.c_str(), raw.size() / 1048576.0);

    std::string names = FindMeta(raw, "names", '{', '}');
    std::string imgsz = FindMeta(raw, "imgsz", '[', ']');
    std::string task  = FindMeta(raw, "task", '\'', '\'');
    std::string date  = FindMeta(raw, "date", '\'', '\'');

    printf("类别表    : %s\n", names.empty() ? "（元数据里没有，可从输出列数反推）" : names.c_str());
    printf("输入尺寸  : %s\n", imgsz.empty() ? "[640, 640]（默认）" : imgsz.c_str());
    if (!date.empty()) printf("导出时间  : %s\n", date.c_str());

    const int imgszPx = ParseFirstInt(imgsz, 640);

    /* ---- 2) 用生产同一条路径加载 ---- */
    Ini ini;
    ini.set("yolo.model_type", "onnx");
    ini.set("yolo.onnx", model);
    ini.set("yolo.input_w", std::to_string(imgszPx));
    ini.set("yolo.input_h", std::to_string(imgszPx));
    ini.set("yolo.nc", "0");          /* 0 = 按输出列数自动推 */
    ini.set("yolo.head", "auto");
    ini.set("yolo.conf", "0.01");     /* 压低阈值，看最高分 */
    ini.set("yolo.nms", "0.45");
    ini.set("yolo.letterbox", "1");
    ini.set("yolo.swap_rb", "1");
    ini.set("yolo.scale", "0.003922");

    std::string err;
    std::unique_ptr<Detector> det = MakeDetector("yolo", ini, ExeDirUtf8(), &err);
    if (!det)
    {
        printf("加载失败  : %s\n", err.c_str());
        return 1;
    }
    printf("OpenCV DNN: 加载成功（后续行为与上位机一致）\n");

    /* ---- --use：换成这个模型，并自动改好 vision.ini ---- */
    if (argc >= 3 && strcmp(argv[2], "--use") == 0)
    {
        if (names.empty())
        {
            printf("\n[ERR] 元数据里没有 names，判断不了类别，请手工改 vision.ini\n");
            return 1;
        }

        const std::string dir = ExeDirUtf8();
        const std::string dst = JoinPath(dir, "best.onnx");
        const std::string iniPath = JoinPath(dir, "vision.ini");

        if (!WriteFileBytes(Utf8ToWide(dst), raw.data(), raw.size()))
        {
            printf("\n[ERR] 写不进 %s（被占用？）\n", dst.c_str());
            return 1;
        }
        printf("\n已拷到 : %s\n", dst.c_str());

        std::string iniRaw;
        if (!ReadFileBytes(Utf8ToWide(iniPath), iniRaw))
        {
            printf("[ERR] 读不到 %s\n", iniPath.c_str());
            return 1;
        }
        /* 先备份，改坏了还能回退 */
        WriteFileBytes(Utf8ToWide(iniPath + ".bak"), iniRaw.data(), iniRaw.size());

        bool o1 = false, o2 = false, o3 = false;
        const int nc = CountClasses(names);
        const std::string nameList = NamesToList(names);
        std::string newIni = RewriteIniYolo(iniRaw, "best.onnx", nc, nameList, &o1, &o2, &o3);
        if (!WriteFileBytes(Utf8ToWide(iniPath), newIni.data(), newIni.size()))
        {
            printf("[ERR] 写不进 %s\n", iniPath.c_str());
            return 1;
        }

        printf("已改   : vision.ini  onnx=best.onnx  nc=%d  names=%s\n", nc, nameList.c_str());
        if (!o1 || !o2 || !o3)
            printf("[WARN] 有键没找到(onnx=%d nc=%d names=%d)，请手工检查 [yolo] 段\n",
                   (int)o1, (int)o2, (int)o3);
        printf("下一步 : 重启上位机（或重新点开始取流）生效\n");
        printf("         vision.ini.bak 是改之前的备份\n");
        return 0;
    }

    if (argc == 2) return 0;

    /* ---- 3) 逐张图跑 ---- */
    printf("\n=========== 图片测试（阈值 0.01，看最高分）===========\n");
    for (int i = 2; i < argc; ++i)
    {
        cv::Mat img = cv::imread(argv[i]);
        if (img.empty())
        {
            printf("%-40s 读不到（路径/格式问题）\n", argv[i]);
            continue;
        }

        DetBox b = det->detect(img);
        const char* verdict = "基本无响应";
        if (b.valid)
        {
            if (b.conf >= 0.50f)      verdict = "★ 强检出";
            else if (b.conf >= 0.25f) verdict = "○ 可检出（偏低）";
            else if (b.conf >= 0.10f) verdict = "△ 很弱";
        }

        /* 只打印文件名，路径太长看不清 */
        const char* slash = strrchr(argv[i], '\\');
        if (!slash) slash = strrchr(argv[i], '/');
        const char* fname = slash ? slash + 1 : argv[i];

        if (!b.valid)
            printf("%-34s %dx%d  无候选框          %s\n", fname, img.cols, img.rows, verdict);
        else
            printf("%-34s %dx%d  最高分=%.3f cls=%d box=(%d,%d,%d,%d)  %s\n",
                   fname, img.cols, img.rows, b.conf, b.cls, b.x, b.y, b.w, b.h, verdict);
    }

    printf("\n判读方法：\n");
    printf("  把相机对准目标、按【存图】，再用本工具跑那张图。\n");
    printf("  框能稳稳套住目标、分数 > 0.5 → 这个类就是你要的目标。\n");
    printf("  目标上也只有 0.0x → 模型没训到这个目标，回去查训练集。\n");
    return 0;
}
