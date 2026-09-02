// LangPref 的行为回归测试（控制台，exit code 0/1）。
// 编译：见 test_langpref.bat。用例与汉化包 tools/test_lang_pref.sh 一一对应，
// 目的是保证「命令行 apply.sh」与「控制器应用汉化」两条路径行为一致。
// 兼容 C# 5。
using System;
using System.Collections.Generic;
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

        private static void Eq(string name, string expected, string actual)
        {
            Check(name + (expected == actual ? "" : " （期望 [" + expected + "]，实际 [" + actual + "]）"),
                expected == actual);
        }

        private static string FreshHome(out string target, out string fragment)
        {
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            string home = Path.Combine(root, "h" + id);
            Directory.CreateDirectory(home);
            Directory.CreateDirectory(Path.Combine(root, "f" + id));
            target = Path.Combine(home, ".AGENTS.md");
            fragment = Path.Combine(root, "f" + id, "lang-pref.md");
            // 真实正文：首尾即两个标记，中间是要注入的指令
            File.WriteAllText(fragment,
                LangPref.Begin + "\n"
                + "# 由 freebuff-zh 汉化包写入。\n"
                + "\n"
                + "- **始终使用简体中文回复**。\n"
                + "- 保持代码、命令与报错原文不翻译。\n"
                + LangPref.End + "\n",
                new UTF8Encoding(false));
            return home;
        }

        private static int CountMatches(string text, string token)
        {
            int n = 0;
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                if (line.TrimEnd('\r') == token) n++;
            return n;
        }

        private static string BlockOf(string target)
        {
            StringBuilder sb = new StringBuilder();
            bool inBlock = false;
            foreach (string line in File.ReadAllText(target, Encoding.UTF8).Split('\n'))
            {
                string t = line.TrimEnd('\r');
                if (t == LangPref.Begin) inBlock = true;
                if (!inBlock) continue;
                sb.Append(line);
                sb.Append('\n');
                if (t == LangPref.End) break;
            }
            return sb.ToString();
        }

        private static void Main()
        {
            root = Path.Combine(Path.GetTempPath(), "langpref-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("FREEBUFF_ZH_NO_LANG", null);

            Console.WriteLine("--- 用例 1：新建 + 段落与正文逐字节一致");
            string target, fragment;
            FreshHome(out target, out fragment);
            Console.WriteLine("  " + LangPref.Install(fragment, target));
            Eq("有且仅有 1 对标记", "1", CountMatches(File.ReadAllText(target), LangPref.Begin).ToString());
            Eq("段落正文与 lang-pref.md 完全一致", File.ReadAllText(fragment), BlockOf(target));

            Console.WriteLine("--- 用例 2：重复应用幂等");
            int lines1 = File.ReadAllLines(target).Length;
            LangPref.Install(fragment, target);
            LangPref.Install(fragment, target);
            Eq("跑三次仍只有 1 段", "1", CountMatches(File.ReadAllText(target), LangPref.Begin).ToString());
            Eq("行数不增长", lines1.ToString(), File.ReadAllLines(target).Length.ToString());

            Console.WriteLine("--- 用例 3：用户已有内容时追加且保序");
            FreshHome(out target, out fragment);
            string userOnly = "# 我自己的规范\r\n\r\n别自动提交。\r\n";
            File.WriteAllText(target, userOnly, new UTF8Encoding(false));
            LangPref.Install(fragment, target);
            string afterInstall = File.ReadAllText(target, Encoding.UTF8);
            Check("用户内容仍在最前", afterInstall.StartsWith("# 我自己的规范\r\n"));
            Check("用户 CRLF 未被归一化", afterInstall.Contains("别自动提交。\r\n"));
            Eq("仍只有 1 段", "1", CountMatches(afterInstall, LangPref.Begin).ToString());

            Console.WriteLine("--- 用例 4：移除后用户内容逐字节还原");
            LangPref.Uninstall(target);
            Eq("还原后与原文件一致", userOnly, File.ReadAllText(target, Encoding.UTF8));

            Console.WriteLine("--- 用例 5：纯本包文件移除后应删除");
            FreshHome(out target, out fragment);
            LangPref.Install(fragment, target);
            LangPref.Uninstall(target);
            Check("文件已删除", !File.Exists(target));

            Console.WriteLine("--- 用例 6：FREEBUFF_ZH_NO_LANG=1 跳过");
            FreshHome(out target, out fragment);
            Environment.SetEnvironmentVariable("FREEBUFF_ZH_NO_LANG", "1");
            string skip = LangPref.Install(fragment, target);
            Environment.SetEnvironmentVariable("FREEBUFF_ZH_NO_LANG", null);
            Check("未创建文件", !File.Exists(target));
            Check("说明里提到该环境变量", skip.Contains("FREEBUFF_ZH_NO_LANG"));

            Console.WriteLine("--- 用例 7：关闭标记存在时跳过");
            FreshHome(out target, out fragment);
            File.WriteAllText(target, "# 内容\n\n" + LangPref.OffToken + "\n", new UTF8Encoding(false));
            LangPref.Install(fragment, target);
            Eq("未追加段落", "0", CountMatches(File.ReadAllText(target), LangPref.Begin).ToString());
            Check("用户内容仍在", File.ReadAllText(target).Contains("# 内容"));

            Console.WriteLine("--- 用例 8：段落残缺（缺结束标记）时不改文件");
            FreshHome(out target, out fragment);
            LangPref.Install(fragment, target);
            string broken = File.ReadAllText(target, Encoding.UTF8).Replace(LangPref.End + "\n", "");
            broken += "\n我的尾巴。\n";
            File.WriteAllText(target, broken, new UTF8Encoding(false));
            string warn = LangPref.Uninstall(target);
            Check("返回 WARN 提示", warn.StartsWith("WARN"));
            Eq("文件未被改动", broken, File.ReadAllText(target, Encoding.UTF8));
            Check("段后正文仍在", File.ReadAllText(target).Contains("我的尾巴。"));

            Console.WriteLine("--- 用例 9：正文文件缺失时只警告不写入");
            FreshHome(out target, out fragment);
            string miss = LangPref.Install(Path.Combine(root, "nope-lang-pref.md"), target);
            Check("返回 WARN 提示", miss.StartsWith("WARN"));
            Check("未创建目标文件", !File.Exists(target));

            Console.WriteLine("--- 用例 10：段后正文在正常移除时被保留");
            FreshHome(out target, out fragment);
            LangPref.Install(fragment, target);
            File.AppendAllText(target, "\n# 后加的一段\n重要。\n", new UTF8Encoding(false));
            LangPref.Install(fragment, target); // 更新段落
            Eq("更新后仍 1 段", "1", CountMatches(File.ReadAllText(target), LangPref.Begin).ToString());
            LangPref.Uninstall(target);
            Check("移除后段后正文仍在", File.ReadAllText(target).Contains("重要。"));

            try { Directory.Delete(root, true); } catch { }
            Console.WriteLine();
            if (failures == 0) { Console.WriteLine("全部通过"); return; }
            Console.WriteLine(failures + " 条断言失败");
            Environment.Exit(1);
        }
    }
}
