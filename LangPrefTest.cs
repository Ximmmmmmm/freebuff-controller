// LangPref behavior regression tests (console, exit code 0/1).
// Compatible with C# 5 and the .NET Framework compiler shipped with Windows.

using System;
using System.IO;
using System.Text;

namespace FreebuffController
{
    internal static class LangPrefTest
    {
        private static int failures;
        private static string root;

        private static void Check(string name, bool ok)
        {
            Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name);
            if (!ok) failures++;
        }

        private static string NewDir(string name)
        {
            string dir = Path.Combine(root, name + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string Read(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        private static int Count(string text, string token)
        {
            int count = 0;
            if (text == null) return 0;
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                if (line.TrimEnd('\r') == token) count++;
            return count;
        }

        private static void Main()
        {
            root = Path.Combine(Path.GetTempPath(),
                "freebuff-controller-langpref-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("FREEBUFF_CONTROLLER_NO_LANG", null);
            Environment.SetEnvironmentVariable("FREEBUFF_ZH_NO_LANG", null);

            Console.WriteLine("--- copy source instructions and add one preference block");
            string sourceHome = NewDir("source-");
            string instanceHome = NewDir("instance-");
            string sourceText = "# 用户自己的规范\r\n\r\n不要自动提交。\r\n";
            File.WriteAllText(Path.Combine(sourceHome, ".AGENTS.md"), sourceText,
                new UTF8Encoding(false));
            LangPref.Prepare(instanceHome, sourceHome);
            string target = Path.Combine(instanceHome, ".AGENTS.md");
            string first = Read(target);
            Check("目标文件已创建", first != null);
            Check("用户内容被复制", first.StartsWith("# 用户自己的规范\r\n"));
            Check("源文件未被修改", Read(Path.Combine(sourceHome, ".AGENTS.md")) == sourceText);
            Check("只存在一个控制器段", Count(first, LangPref.Begin) == 1);
            Check("包含中文回复偏好", first.Contains("默认使用简体中文回复"));
            Check("保留用户 CRLF", first.Contains("不要自动提交。\r\n"));

            Console.WriteLine("--- repeated preparation is idempotent");
            LangPref.Prepare(instanceHome, sourceHome);
            Check("重复准备仍只有一个段落", Count(Read(target), LangPref.Begin) == 1);
            Check("重复准备内容不增长", Read(target) == first);
            File.AppendAllText(target, "\r\n# 实例私有补充\r\n", new UTF8Encoding(false));
            LangPref.Prepare(instanceHome, sourceHome);
            Check("后续准备保留实例私有内容", Read(target).Contains("# 实例私有补充\r\n"));

            Console.WriteLine("--- skip removes a previous private preference");
            Environment.SetEnvironmentVariable("FREEBUFF_CONTROLLER_NO_LANG", "1");
            LangPref.Prepare(instanceHome, sourceHome);
            Environment.SetEnvironmentVariable("FREEBUFF_CONTROLLER_NO_LANG", null);
            Check("跳过后移除偏好但保留私有内容", !Read(target).Contains(LangPref.Begin)
                && Read(target).Contains("# 实例私有补充\r\n"));
            string skippedSettings = Read(Path.Combine(instanceHome, ".claude", "settings.json"));
            Check("跳过时移除 language 设置", skippedSettings == null || !skippedSettings.Contains("Chinese"));
            Check("跳过未改动源文件", Read(Path.Combine(sourceHome, ".AGENTS.md")) == sourceText);

            Console.WriteLine("--- no source creates an isolated preference file");
            string emptyHome = NewDir("empty-");
            string emptyTarget = Path.Combine(emptyHome, ".AGENTS.md");
            LangPref.Prepare(emptyHome, NewDir("missing-source-"));
            Check("无源文件时仍创建目标", File.Exists(emptyTarget));
            Check("无源文件时包含中文偏好", Read(emptyTarget).Contains("默认使用简体中文回复"));
            string emptySettings = Path.Combine(emptyHome, ".claude", "settings.json");
            Check("无源文件时写入中文 language 设置", Read(emptySettings).Contains("\"language\":\"Chinese\""));

            Console.WriteLine("--- existing settings are preserved while language is set");
            string settingsHome = NewDir("settings-");
            Directory.CreateDirectory(Path.Combine(settingsHome, ".claude"));
            string settingsPath = Path.Combine(settingsHome, ".claude", "settings.json");
            File.WriteAllText(settingsPath,
                "{\"skipDangerousModePermissionPrompt\":true,\"language\":\"English\"}",
                new UTF8Encoding(false));
            LangPref.Prepare(settingsHome, NewDir("settings-source-"));
            string settingsResult = Read(settingsPath);
            Check("已有 settings 仍保留其他配置", settingsResult.Contains("skipDangerousModePermissionPrompt"));
            Check("已有 settings 的 language 被改为中文", settingsResult.Contains("\"language\":\"Chinese\""));

            Console.WriteLine("--- malformed block is left untouched");
            string brokenHome = NewDir("broken-");
            string brokenTarget = Path.Combine(brokenHome, ".AGENTS.md");
            string broken = LangPref.Begin + "\n保留这段残缺内容。\n";
            File.WriteAllText(brokenTarget, broken, new UTF8Encoding(false));
            string result = LangPref.Prepare(brokenHome, NewDir("broken-source-"));
            Check("残缺段返回 WARN", result.StartsWith("WARN", StringComparison.Ordinal));
            Check("残缺段文件不变", Read(brokenTarget) == broken);

            Console.WriteLine("--- legacy skip variable is honored");
            string legacyHome = NewDir("legacy-");
            Environment.SetEnvironmentVariable("FREEBUFF_ZH_NO_LANG", "1");
            LangPref.Prepare(legacyHome, NewDir("legacy-source-"));
            Environment.SetEnvironmentVariable("FREEBUFF_ZH_NO_LANG", null);
            Check("旧环境变量也能跳过", !File.Exists(Path.Combine(legacyHome, ".AGENTS.md")));

            try { Directory.Delete(root, true); } catch { }
            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("全部通过");
                return;
            }
            Console.WriteLine(failures + " 条断言失败");
            Environment.Exit(1);
        }
    }
}
