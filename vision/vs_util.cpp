#include "vs_util.h"

#include <windows.h>
#include <cstdio>
#include <cstring>
#include <algorithm>

std::wstring Utf8ToWide(const std::string& s)
{
    if (s.empty()) return std::wstring();
    int n = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), nullptr, 0);
    if (n <= 0) return std::wstring();
    std::wstring w((size_t)n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), &w[0], n);
    return w;
}

std::string WideToUtf8(const std::wstring& w)
{
    if (w.empty()) return std::string();
    int n = WideCharToMultiByte(CP_UTF8, 0, w.c_str(), (int)w.size(), nullptr, 0, nullptr, nullptr);
    if (n <= 0) return std::string();
    std::string s((size_t)n, '\0');
    WideCharToMultiByte(CP_UTF8, 0, w.c_str(), (int)w.size(), &s[0], n, nullptr, nullptr);
    return s;
}

std::string ExeDirUtf8(void)
{
    /* 注意：不能用 GetModuleFileNameW(nullptr) —— 那返回的是"宿主进程"的 exe，
       被 C# 加载时就是 C# 程序所在目录，vision.ini 会找不到。
       这里用本函数自身的地址反查所属模块，所以：
         - 在 vs_tuner.exe 里 = exe 目录
         - 在 vs_vision.dll 里 = dll 目录
       两个都符合"配置文件放在程序旁边"的直觉。 */
    HMODULE hm = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                            GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                            (LPCWSTR)(void*)&ExeDirUtf8, &hm))
        return std::string();

    wchar_t buf[MAX_PATH * 4] = { 0 };
    DWORD n = GetModuleFileNameW(hm, buf, (DWORD)(sizeof(buf) / sizeof(buf[0])));
    if (n == 0) return std::string();

    std::wstring p(buf, n);
    size_t pos = p.find_last_of(L"\\/");
    if (pos == std::wstring::npos) return std::string();
    return WideToUtf8(p.substr(0, pos + 1));
}

bool ReadFileBytes(const std::wstring& path, std::string& out)
{
    HANDLE h = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return false;

    LARGE_INTEGER sz;
    if (!GetFileSizeEx(h, &sz)) { CloseHandle(h); return false; }
    out.clear();
    if (sz.QuadPart > 0)
    {
        out.resize((size_t)sz.QuadPart);
        DWORD got = 0, total = 0;
        while (total < (DWORD)out.size())
        {
            if (!ReadFile(h, &out[total], (DWORD)out.size() - total, &got, nullptr) || got == 0)
                break;
            total += got;
        }
        out.resize(total);
    }
    CloseHandle(h);
    return true;
}

bool WriteFileBytes(const std::wstring& path, const void* data, size_t n)
{
    HANDLE h = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr,
                           CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return false;
    DWORD written = 0;
    BOOL ok = TRUE;
    if (n > 0 && data) ok = WriteFile(h, data, (DWORD)n, &written, nullptr);
    CloseHandle(h);
    return ok != FALSE && written == (DWORD)n;
}

bool EnsureDir(const std::wstring& path)
{
    if (path.empty()) return false;
    DWORD attr = GetFileAttributesW(path.c_str());
    if (attr != INVALID_FILE_ATTRIBUTES && (attr & FILE_ATTRIBUTE_DIRECTORY)) return true;
    return CreateDirectoryW(path.c_str(), nullptr) != FALSE;
}

bool FileExists(const std::wstring& path)
{
    DWORD attr = GetFileAttributesW(path.c_str());
    return attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY);
}

std::string JoinPath(const std::string& dir, const std::string& name)
{
    if (dir.empty()) return name;
    char last = dir[dir.size() - 1];
    if (last == '\\' || last == '/') return dir + name;
    return dir + "\\" + name;
}

/* ------------------------------- 字符串 ------------------------------- */

std::string Trim(const std::string& s)
{
    size_t a = 0, b = s.size();
    while (a < b && (unsigned char)s[a] <= ' ') ++a;
    while (b > a && (unsigned char)s[b - 1] <= ' ') --b;
    return s.substr(a, b - a);
}

std::string ToLower(const std::string& s)
{
    std::string r = s;
    for (size_t i = 0; i < r.size(); ++i)
        if (r[i] >= 'A' && r[i] <= 'Z') r[i] = (char)(r[i] - 'A' + 'a');
    return r;
}

/* --------------------------------- Ini -------------------------------- */

bool Ini::load(const std::wstring& path)
{
    std::string raw;
    if (!ReadFileBytes(path, raw)) return false;

    /* 去 UTF-8 BOM */
    if (raw.size() >= 3 && (unsigned char)raw[0] == 0xEF &&
        (unsigned char)raw[1] == 0xBB && (unsigned char)raw[2] == 0xBF)
        raw.erase(0, 3);

    std::string section;
    size_t pos = 0;
    while (pos <= raw.size())
    {
        size_t nl = raw.find('\n', pos);
        std::string line = raw.substr(pos, (nl == std::string::npos ? raw.size() : nl) - pos);
        pos = (nl == std::string::npos) ? raw.size() + 1 : nl + 1;

        if (!line.empty() && line[line.size() - 1] == '\r') line.erase(line.size() - 1);

        /* 注释：行首 ; 或 # ；行内 ; 之后也算注释 */
        size_t c1 = line.find(';');
        size_t c2 = line.find('#');
        size_t c = std::min(c1 == std::string::npos ? line.size() : c1,
                            c2 == std::string::npos ? line.size() : c2);
        line = Trim(line.substr(0, c));
        if (line.empty()) continue;

        if (line[0] == '[' && line[line.size() - 1] == ']')
        {
            section = ToLower(Trim(line.substr(1, line.size() - 2)));
            continue;
        }
        size_t eq = line.find('=');
        if (eq == std::string::npos) continue;

        std::string key = ToLower(Trim(line.substr(0, eq)));
        std::string val = Trim(line.substr(eq + 1));
        /* 允许 "值" 用引号包住，方便写带空格的路径 */
        if (val.size() >= 2 && val[0] == '"' && val[val.size() - 1] == '"')
            val = val.substr(1, val.size() - 2);

        if (!section.empty()) key = section + "." + key;
        kv_[key] = val;
    }
    return true;
}

void Ini::overlay(const Ini& other)
{
    for (std::map<std::string, std::string>::const_iterator it = other.kv_.begin();
         it != other.kv_.end(); ++it)
        kv_[it->first] = it->second;
}

std::string Ini::getS(const std::string& key, const std::string& def) const
{
    std::map<std::string, std::string>::const_iterator it = kv_.find(ToLower(key));
    return it == kv_.end() ? def : it->second;
}

int Ini::getI(const std::string& key, int def) const
{
    std::string v = getS(key);
    if (v.empty()) return def;
    return (int)strtol(v.c_str(), nullptr, 10);
}

float Ini::getF(const std::string& key, float def) const
{
    std::string v = getS(key);
    if (v.empty()) return def;
    return (float)atof(v.c_str());
}

bool Ini::getB(const std::string& key, bool def) const
{
    std::string v = ToLower(getS(key));
    if (v.empty()) return def;
    return v == "1" || v == "true" || v == "yes" || v == "on";
}

void Ini::set(const std::string& key, const std::string& val)
{
    kv_[ToLower(key)] = val;
}

bool Ini::save(const std::wstring& path, const std::string& header) const
{
    std::string out = header;
    if (!out.empty() && out[out.size() - 1] != '\n') out += "\r\n";

    /* 按 section 分组输出，section 顺序按首次出现 */
    std::vector<std::string> sections;
    for (std::map<std::string, std::string>::const_iterator it = kv_.begin(); it != kv_.end(); ++it)
    {
        std::string sec;
        size_t dot = it->first.find('.');
        if (dot != std::string::npos) sec = it->first.substr(0, dot);
        if (std::find(sections.begin(), sections.end(), sec) == sections.end())
            sections.push_back(sec);
    }
    for (size_t i = 0; i < sections.size(); ++i)
    {
        if (!sections[i].empty()) out += "[" + sections[i] + "]\r\n";
        for (std::map<std::string, std::string>::const_iterator it = kv_.begin(); it != kv_.end(); ++it)
        {
            std::string sec;
            size_t dot = it->first.find('.');
            std::string name = it->first;
            if (dot != std::string::npos) { sec = it->first.substr(0, dot); name = it->first.substr(dot + 1); }
            if (sec != sections[i]) continue;
            out += name + "=" + it->second + "\r\n";
        }
        out += "\r\n";
    }
    return WriteFileBytes(path, out.data(), out.size());
}
