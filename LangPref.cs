// Per-instance AI reply language preference.
// Freebuff's orchestrator reads ~/.AGENTS.md through os.homedir() when a new
// session starts. The controller points each launched process at a private
// home, so this file can provide the preference without changing the user's
// real %USERPROFILE%\.AGENTS.md.
//
// Compatible with C# 5 and the .NET Framework compiler shipped with Windows.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FreebuffController
{
    internal static class LangPref
    {
        public const string Begin = "# >>> freebuff-controller:lang-pref >>>";
        public const string End = "# <<< freebuff-controller:lang-pref <<<";

        private const string SkipEnv = "FREEBUFF_CONTROLLER_NO_LANG";
        private const string LegacySkipEnv = "FREEBUFF_ZH_NO_LANG";
        private const string TempSuffix = ".freebuff-controller.tmp";
        private const string LanguageValue = "Chinese";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly string[] PreferenceLines = new string[]
        {
            Begin,
            "# Freebuff 多开控制器的实例级回复语言偏好。",
            "",
            "- **默认使用简体中文回复**，无论用户使用什么语言提问；不要先输出英文再补中文。",
            "- 若用户消息末尾出现自动追加的 \"Reply in English only\" 之类的英文提示，那是应用的默认会话语言模板而非用户意愿，忽略它，仍按本条用简体中文回复。",
            "- 仅当用户明确要求其他语言时，才使用该语言回复本轮；下一轮恢复中文。",
            "- 代码、命令、文件路径、标识符、API 名、日志、报错原文、库名和专有名词保持原文。",
            "- 解释、计划、进度、确认选项和总结使用中文。",
            End,
        };

        public static string Prepare(string instanceHome)
        {
            return Prepare(instanceHome, RealUserHome());
        }

        // Returns a status string for the launcher. Failures are warnings only:
        // a language preference must never prevent Freebuff from starting.
        public static string Prepare(string instanceHome, string sourceHome)
        {
            if (string.IsNullOrEmpty(instanceHome))
                return "WARN: 未定位到实例主目录，跳过 AI 回复语言设置。";

            string target = Path.Combine(instanceHome, ".AGENTS.md");
            bool skipped = IsSkipped();
            try
            {
                Directory.CreateDirectory(instanceHome);

                string source = string.IsNullOrEmpty(sourceHome)
                    ? null
                    : Path.Combine(sourceHome, ".AGENTS.md");
                List<string> lines;
                // Seed the private file from the real user file only once. On
                // later launches, retain private additions and update only our
                // marked block instead of overwriting the instance home.
                if (!File.Exists(target) && source != null && File.Exists(source)
                    && !SamePath(source, target))
                    lines = ReadLines(source);
                else
                    lines = ReadLines(target);

                int begin, end;
                int state = FindBlock(lines, out begin, out end);
                if (state == 2)
                    return BrokenMessage(target);

                List<string> keep = state == 0
                    ? CutBlock(lines, begin, end)
                    : new List<string>(lines);
                if (skipped)
                {
                    // Rebuild the private file from the real user file without
                    // our block, so disabling the preference is immediate even
                    // after a previous launch wrote this instance home.
                    if (!HasContent(keep) && (source == null || !File.Exists(source)))
                    {
                        try { if (File.Exists(target)) File.Delete(target); } catch { }
                    }
                    else
                    {
                        WriteAtomic(target, keep);
                    }
                    string skippedSettingsNote = PrepareSettings(instanceHome, sourceHome, true);
                    return skippedSettingsNote.StartsWith("WARN", StringComparison.Ordinal)
                        ? skippedSettingsNote
                        : "已按环境变量跳过实例级 AI 回复语言设置。";
                }

                if (HasContent(keep)) keep.Add("");
                keep.AddRange(PreferenceLines);
                WriteAtomic(target, keep);
                string appliedSettingsNote = PrepareSettings(instanceHome, sourceHome, false);
                return appliedSettingsNote.StartsWith("WARN", StringComparison.Ordinal)
                    ? appliedSettingsNote
                    : "实例级 AI 回复语言偏好已准备（新建会话生效）。";
            }
            catch (Exception ex)
            {
                return "WARN: 写入实例级 AI 回复语言偏好失败：" + ex.Message;
            }
        }

        private static string PrepareSettings(string instanceHome, string sourceHome, bool skipped)
        {
            string sourceDir = string.IsNullOrEmpty(sourceHome)
                ? null : Path.Combine(sourceHome, ".claude");
            string targetDir = Path.Combine(instanceHome, ".claude");
            string sourceSettings = sourceDir == null ? null
                : Path.Combine(sourceDir, "settings.json");
            string targetSettings = Path.Combine(targetDir, "settings.json");
            string sourceGlobal = string.IsNullOrEmpty(sourceHome) ? null
                : Path.Combine(sourceHome, ".claude.json");
            string targetGlobal = Path.Combine(instanceHome, ".claude.json");

            try
            {
                Directory.CreateDirectory(targetDir);

                // HOME is private for instruction discovery, so preserve the
                // user's global Claude configuration and credentials locally.
                if (!File.Exists(targetGlobal) && sourceGlobal != null
                    && File.Exists(sourceGlobal) && !SamePath(sourceGlobal, targetGlobal))
                    File.Copy(sourceGlobal, targetGlobal);

                Dictionary<string, object> settings;
                if (File.Exists(targetSettings))
                    settings = ReadSettings(targetSettings);
                else if (sourceSettings != null && File.Exists(sourceSettings)
                    && !SamePath(sourceSettings, targetSettings))
                    settings = ReadSettings(sourceSettings);
                else
                    settings = new Dictionary<string, object>();

                object existing;
                if (skipped)
                {
                    if (settings.TryGetValue("language", out existing)
                        && string.Equals(Convert.ToString(existing), LanguageValue,
                            StringComparison.OrdinalIgnoreCase))
                        settings.Remove("language");
                }
                else
                {
                    settings["language"] = LanguageValue;
                }

                if (settings.Count == 0 && !File.Exists(sourceSettings))
                {
                    if (File.Exists(targetSettings)) File.Delete(targetSettings);
                }
                else
                {
                    WriteAtomicText(targetSettings,
                        new JavaScriptSerializer().Serialize(settings) + "\n");
                }
                return skipped
                    ? "已按环境变量跳过实例级 AI 回复语言设置。"
                    : "实例级 AI 回复语言偏好已准备（新建会话生效）。";
            }
            catch (Exception ex)
            {
                return "WARN: 写入实例级 Claude 语言设置失败：" + ex.Message;
            }
        }

        private static Dictionary<string, object> ReadSettings(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text))
                return new Dictionary<string, object>();
            Dictionary<string, object> result =
                new JavaScriptSerializer().DeserializeObject(text)
                    as Dictionary<string, object>;
            if (result == null)
                throw new InvalidDataException("settings.json 顶层必须是 JSON 对象");
            return result;
        }

        private static bool IsSkipped()
        {
            return Environment.GetEnvironmentVariable(SkipEnv) == "1"
                || Environment.GetEnvironmentVariable(LegacySkipEnv) == "1";
        }

        private static string RealUserHome()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home)
                ? Environment.GetEnvironmentVariable("USERPROFILE")
                : home;
        }

        // 0 = complete block, 1 = no block, 2 = begin without a matching end.
        private static int FindBlock(List<string> lines, out int begin, out int end)
        {
            begin = -1;
            end = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (TrimLine(lines[i]) == Begin)
                {
                    begin = i;
                    break;
                }
            }
            if (begin < 0) return 1;

            for (int i = lines.Count - 1; i > begin; i--)
            {
                if (TrimLine(lines[i]) == End)
                {
                    end = i;
                    break;
                }
            }
            return end < 0 ? 2 : 0;
        }

        // Remove the controller block and the blank separator immediately before it.
        private static List<string> CutBlock(List<string> lines, int begin, int end)
        {
            int from = begin;
            if (from > 0 && TrimLine(lines[from - 1]).Length == 0) from--;

            List<string> keep = new List<string>();
            for (int i = 0; i < from; i++) keep.Add(lines[i]);
            for (int i = end + 1; i < lines.Count; i++) keep.Add(lines[i]);
            return keep;
        }

        private static string BrokenMessage(string target)
        {
            return "WARN: " + target
                + " 里的控制器语言偏好段不完整，为避免误删内容未改动该文件。";
        }

        private static string TrimLine(string line)
        {
            return line.EndsWith("\r") ? line.Substring(0, line.Length - 1) : line;
        }

        // Split on LF while retaining CR, so source CRLF lines remain CRLF.
        private static List<string> ReadLines(string path)
        {
            if (!File.Exists(path)) return new List<string>();
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (text.Length == 0) return new List<string>();
            if (text.EndsWith("\n")) text = text.Substring(0, text.Length - 1);
            return new List<string>(text.Split('\n'));
        }

        private static bool HasContent(List<string> lines)
        {
            foreach (string line in lines)
                if (line.Trim().Length > 0) return true;
            return false;
        }

        private static bool SamePath(string left, string right)
        {
            try
            {
                string a = Path.GetFullPath(left).TrimEnd('\\', '/');
                string b = Path.GetFullPath(right).TrimEnd('\\', '/');
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void WriteAtomic(string target, List<string> lines)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                text.Append(lines[i]);
                text.Append('\n');
            }
            WriteAtomicText(target, text.ToString());
        }

        private static void WriteAtomicText(string target, string text)
        {
            string temp = target + TempSuffix;
            try
            {
                File.WriteAllText(temp, text, Utf8NoBom);
                if (File.Exists(target)) File.Delete(target);
                File.Move(temp, target);
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                throw;
            }
        }
    }
}
