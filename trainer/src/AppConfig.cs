// AppConfig.cs — 修改器配置持久化（%APPDATA%\DzbTrainer\config.json）
// 字段：gameDir(游戏根目录) / debug(调试模式) / autoLaunchGame(自动启动游戏)
// 自写极简 JSON 读写，零第三方依赖（.NET Framework 4 无 System.Text.Json）
using System;
using System.IO;
using System.Text;

namespace DzbTrainer
{
    public class AppConfig
    {
        public string GameDir = "";
        public bool Debug = false;
        public bool AutoLaunchGame = true;

        public static string ConfigDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DzbTrainer"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDir, "config.json"); }
        }

        public static string DebugLogPath
        {
            get { return Path.Combine(ConfigDir, "debug.log"); }
        }

        // 返回是否成功；失败时由调用方提示用户（避免改目录/调开关静默丢失）
        public bool Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var sb = new StringBuilder();
                sb.Append("{\"gameDir\":\"").Append(JsonEsc(GameDir))
                  .Append("\",\"debug\":").Append(Debug ? "true" : "false")
                  .Append(",\"autoLaunchGame\":").Append(AutoLaunchGame ? "true" : "false")
                  .Append("}");
                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch { return false; }
        }

        public static AppConfig Load()
        {
            var c = new AppConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string j = File.ReadAllText(ConfigPath, Encoding.UTF8);
                    c.GameDir = JsonGet(j, "gameDir");
                    c.Debug = JsonGet(j, "debug") == "true";
                    c.AutoLaunchGame = JsonGet(j, "autoLaunchGame") != "false";
                }
            }
            catch { }
            return c;
        }

        static string JsonGet(string json, string key)
        {
            string pat = "\"" + key + "\"";
            int i = json.IndexOf(pat, StringComparison.Ordinal);
            if (i < 0) return "";
            i += pat.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == ':')) i++;
            if (i >= json.Length) return "";
            if (json[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < json.Length && json[i] != '"')
                {
                    if (json[i] == '\\' && i + 1 < json.Length)
                    {
                        // JSON 转义（\\ 或 \”）→ 输出实际字符；Windows 路径的单反斜杠原样保留
                        if (json[i + 1] == '\\' || json[i + 1] == '"') { sb.Append(json[i + 1]); i += 2; continue; }
                        sb.Append(json[i]); i++; continue;
                    }
                    sb.Append(json[i]);
                    i++;
                }
                return sb.ToString();
            }
            int j = i;
            while (j < json.Length && (char.IsLetterOrDigit(json[j]) || json[j] == '-' || json[j] == '.')) j++;
            return json.Substring(i, j - i);
        }

        static string JsonEsc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
