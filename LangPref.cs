// AI 回复语言偏好：把 freebuff-zh 汉化包的「让智能体也用中文回复」写进 ~/.AGENTS.md。
//
// 与汉化包 tools/lang_pref.sh 严格同语义、同标记、同正文（正文取自汉化包 output/
// lang-pref.md），因此命令行与控制器「应用汉化」写出的段落逐字节一致。
// 之所以走这个文件：Freebuff 的 orchestrator 每次新建会话都会无条件读取
// ~/.AGENTS.md 并注入系统提示词，而界面上「包含 AGENTS.md」勾选只管项目根那一份。
//
// 只增删夹在两个标记之间的段落，用户自有内容按字节保留（逐行保留行尾 \r，
// 不做 CRLF 归一化）；段落残缺（有起始标记没结束标记）时一律不改文件。
// 兼容 C# 5（系统自带 csc.exe）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FreebuffController
{
    internal static class LangPref
    {
        public const string Begin = "# >>> freebuff-zh:lang-pref >>>";
        public const string End = "# <<< freebuff-zh:lang-pref <<<";
        // 永久关闭标记：必须写在标记段之外（段本身每次应用汉化都会被重写）
        public const string OffToken = "freebuff-zh:lang-pref:off";
        private const string SkipEnv = "FREEBUFF_ZH_NO_LANG";
        private const string TempSuffix = ".freebuff-zh.tmp";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string DefaultTarget()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(home)) return null;
            return Path.Combine(home, ".AGENTS.md");
        }

        public static string Install(string fragmentPath)
        {
            return Install(fragmentPath, DefaultTarget());
        }

        // 返回一句可直接显示在状态栏的说明；任何失败都不抛出，交由调用方拼接提示。
        public static string Install(string fragmentPath, string target)
        {
            if (target == null)
                return "WARN: 未定位到用户主目录，跳过 AI 回复语言设置。";
            if (Environment.GetEnvironmentVariable(SkipEnv) == "1")
                return "已按 " + SkipEnv + "=1 跳过 AI 回复语言设置。";
            try
            {
                List<string> lines = ReadLines(target);
                foreach (string l in lines)
                    if (l.Contains(OffToken))
                        return "检测到 " + target + " 里的关闭标记，未写入 AI 回复语言偏好。";

                if (string.IsNullOrEmpty(fragmentPath) || !File.Exists(fragmentPath)
                    || new FileInfo(fragmentPath).Length == 0)
                    return "WARN: 汉化包缺少 lang-pref.md，未配置 AI 回复语言（界面汉化不受影响）。";

                int b, e;
                int state = FindBlock(lines, out b, out e);
                if (state == 2)
                    return BeginBrokenMessage(target);
                List<string> keep = state == 0 ? CutBlock(lines, b, e) : new List<string>(lines);

                if (HasContent(keep)) keep.Add("");
                keep.AddRange(SplitBlock(File.ReadAllText(fragmentPath, Encoding.UTF8)));

                WriteAtomic(target, keep);
                return "AI 回复语言偏好已写入 " + target + "（新建会话生效）。";
            }
            catch (Exception ex)
            {
                return "WARN: 写入 " + target + " 失败：" + ex.Message + "（界面汉化不受影响）。";
            }
        }

        public static string Uninstall()
        {
            return Uninstall(DefaultTarget());
        }

        public static string Uninstall(string target)
        {
            if (target == null)
                return "未定位到用户主目录，未改动 ~/.AGENTS.md。";
            try
            {
                if (!File.Exists(target))
                    return "未找到 " + target + "，无需移除。";
                List<string> lines = ReadLines(target);
                int b, e;
                int state = FindBlock(lines, out b, out e);
                if (state == 2)
                    return BeginBrokenMessage(target);
                if (state == 1)
                    return target + " 没有本包写入的语言偏好段，保持原样。";

                List<string> keep = CutBlock(lines, b, e);
                if (!HasContent(keep))
                {
                    File.Delete(target);
                    return "已删除 " + target + "（移除语言偏好段后只剩空白内容）。";
                }
                WriteAtomic(target, keep);
                return "已从 " + target + " 移除语言偏好段（你自己的内容保留）。";
            }
            catch (Exception ex)
            {
                return "移除 " + target + " 的语言偏好段失败：" + ex.Message;
            }
        }

        // 0 = 找到完整段落，1 = 没有段落，2 = 残缺（有起始无结束，边界不可判）
        private static int FindBlock(List<string> lines, out int begin, out int end)
        {
            begin = -1;
            end = -1;
            for (int i = 0; i < lines.Count; i++)
                if (Trim(lines[i]) == Begin) { begin = i; break; }
            if (begin < 0) return 1;
            for (int i = lines.Count - 1; i > begin; i--)
                if (Trim(lines[i]) == End) { end = i; break; }
            if (end < 0) return 2;
            return 0;
        }

        // 剥掉段落，并一并去掉段前那个由本包插入的空行分隔符
        private static List<string> CutBlock(List<string> lines, int begin, int end)
        {
            int from = begin;
            if (from > 0 && Trim(lines[from - 1]).Length == 0) from--;
            List<string> keep = new List<string>();
            for (int i = 0; i < from; i++) keep.Add(lines[i]);
            for (int i = end + 1; i < lines.Count; i++) keep.Add(lines[i]);
            return keep;
        }

        private static string BeginBrokenMessage(string target)
        {
            return "WARN: " + target + " 里的语言偏好段不完整（缺结束标记），为避免误删你的内容未改动该文件。";
        }

        private static string Trim(string line)
        {
            return line.EndsWith("\r") ? line.Substring(0, line.Length - 1) : line;
        }

        // 按 \n 切行但保留行内原有的 \r，写回时不改变用户的换行风格
        private static List<string> ReadLines(string path)
        {
            if (!File.Exists(path)) return new List<string>();
            return SplitBlock(File.ReadAllText(path, Encoding.UTF8));
        }

        private static List<string> SplitBlock(string text)
        {
            List<string> outLines = new List<string>();
            if (text.Length == 0) return outLines;
            if (text.EndsWith("\n")) text = text.Substring(0, text.Length - 1);
            outLines.AddRange(text.Split('\n'));
            return outLines;
        }

        private static bool HasContent(List<string> lines)
        {
            foreach (string l in lines)
                if (l.Trim().Length > 0) return true;
            return false;
        }

        private static void WriteAtomic(string target, List<string> lines)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                sb.Append('\n');
            }
            string tmp = target + TempSuffix;
            File.WriteAllText(tmp, sb.ToString(), Utf8NoBom);
            if (File.Exists(target)) File.Delete(target);
            File.Move(tmp, target);
        }
    }
}
