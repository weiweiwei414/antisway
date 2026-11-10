// ============================================================================
//  vs_util.h —— 路径 / 文件 / ini 解析
//
//  为什么不用 GetPrivateProfileString：
//    ini 里要写中文注释，GetPrivateProfileStringA 走的是 ANSI，遇到 UTF-8
//    注释会乱码甚至截断；自己按字节读进来再按 UTF-8 解，最省事。
//  为什么不用 std::ifstream 直接开路径：
//    Windows 下 narrow path 走 ANSI，中文路径直接失败。这里统一 CreateFileW。
// ============================================================================
#ifndef VS_UTIL_H
#define VS_UTIL_H

#include <string>
#include <map>
#include <vector>
#include <cstdint>

std::wstring Utf8ToWide(const std::string& s);
std::string  WideToUtf8(const std::wstring& s);

/* 当前 exe/dll 所在目录，UTF-8，末尾带反斜杠 */
std::string  ExeDirUtf8(void);

bool ReadFileBytes(const std::wstring& path, std::string& out);
bool WriteFileBytes(const std::wstring& path, const void* data, size_t n);
bool EnsureDir(const std::wstring& path);
bool FileExists(const std::wstring& path);
std::string JoinPath(const std::string& dir, const std::string& name);

/* ---- 极简 ini：section.key 拉平存储，键名不区分大小写 ---- */
class Ini
{
public:
    bool load(const std::wstring& path);      /* 文件不存在返回 false，不算错 */
    void overlay(const Ini& other);           /* 用另一个 ini 覆盖同名键（标定值用） */

    std::string getS(const std::string& key, const std::string& def = "") const;
    int         getI(const std::string& key, int def = 0) const;
    float       getF(const std::string& key, float def = 0.0f) const;
    bool        getB(const std::string& key, bool def = false) const;

    void set(const std::string& key, const std::string& val);

    /* 按 section 分组写回；header 写文件头注释 */
    bool save(const std::wstring& path, const std::string& header) const;

private:
    std::map<std::string, std::string> kv_;   /* 小写键 -> 值 */
};

std::string Trim(const std::string& s);
std::string ToLower(const std::string& s);

#endif /* VS_UTIL_H */
