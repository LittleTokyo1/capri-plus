using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeHusk(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.CodeMode) throw new Exception("Code mode should be off until he turns it on.");
            string close;
            Tunable found = Tuning.Find("code mode", "on", out close);
            if (found == null || found.Field != "CodeMode") throw new Exception("Her setting tool can't find code mode by voice.");
            if (HuskWatch.ProjectName(@"C:\x\C--Users-you-Documents-GitHub-capri") != "capri") throw new Exception("The project name wasn't read from its folder.");
            if (HuskWatch.FirstLine("**Done.** Built it and pushed.\nMore here", 110) != "Done.") throw new Exception("The finished line wasn't cut to its first sentence: " + HuskWatch.FirstLine("**Done.** Built it and pushed.\nMore here", 110));
            if (!HuskSay.IsCoding("WindowsTerminal") || HuskSay.IsCoding("League of Legends")) throw new Exception("Looking at a terminal wasn't told apart from a game.");
            if (HuskSay.More(3, 0, true) != 3 || HuskSay.More(40, 0, true) != HuskSay.MostHelpers || HuskSay.More(3, 2, true) != 1 || HuskSay.More(3, 0, false) != 0) throw new Exception("Calm clones for helpers were counted wrong.");

            string root = Path.Combine(Path.GetTempPath(), "capri-husk-smoke-" + Guid.NewGuid().ToString("N"));
            string project = Path.Combine(root, "C--Users-you-Documents-GitHub-capri");
            string file = Path.Combine(project, "s1.jsonl");
            try
            {
                Directory.CreateDirectory(project);
                File.WriteAllText(file, Assistant("text", "\"text\":\"old history\"", "end_turn") + "\n");
                File.WriteAllText(Path.Combine(project, "old.jsonl"), Assistant("text", "\"text\":\"stale\"", "end_turn") + "\n");
                File.SetLastWriteTime(Path.Combine(project, "old.jsonl"), DateTime.Now.AddMinutes(-10));
                HuskWatch watch = new HuskWatch(root);
                if (watch.Scan(DateTime.Now).Count != 0) throw new Exception("Old history in a session was told again.");
                File.AppendAllText(file,
                    Assistant("tool_use", "\"id\":\"t0\",\"name\":\"Edit\",\"input\":{\"file_path\":\"C:\\\\x\\\\Foo.cs\"}", "tool_use") + "\n" +
                    Assistant("text", "\"text\":\"Now I'll check it.\"", "tool_use") + "\n" +
                    Assistant("tool_use", "\"id\":\"t1\",\"name\":\"AskUserQuestion\",\"input\":{}", "tool_use") + "\n" +
                    Assistant("tool_use", "\"id\":\"t1\",\"name\":\"AskUserQuestion\",\"input\":{}", "tool_use") + "\n" +
                    "{\"type\":\"assistant\",\"isSidechain\":true,\"message\":{\"stop_reason\":\"tool_use\",\"content\":[{\"type\":\"tool_use\",\"id\":\"s9\",\"name\":\"Read\",\"input\":{}}]}}\n" +
                    "not json\n" +
                    "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"t1\"}]}}\n" +
                    Assistant("text", "\"text\":\"Done. Every check passed.\"", "end_turn") + "\n" +
                    "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"tool_use\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t2\",\"name\":\"Bash\",\"input\":{\"description\":\"half a line");
                List<HuskEvent> seen = watch.Scan(DateTime.Now);
                if (seen.Count != 3) throw new Exception("Expected editing, one ask and one finish, got " + seen.Count + ": " + Told(seen));
                if (seen[0].Kind != HuskKind.Doing || seen[0].Text != "editing Foo.cs" || seen[0].Project != "capri") throw new Exception("What Claude Code was doing wasn't shown: " + Told(seen));
                if (seen[1].Kind != HuskKind.Asking || HuskSay.Asking(seen[1]) != "Claude Code in capri is asking you something.") throw new Exception("Claude Code asking him wasn't told once: " + Told(seen));
                if (seen[2].Kind != HuskKind.Answered || HuskSay.Answered(seen[2]) != "Claude Code's done in capri: Done.") throw new Exception("Claude Code finishing wasn't told: " + Told(seen));
                File.AppendAllText(file, "\"}}]}}\n");
                File.AppendAllText(file, Assistant("tool_use", "\"id\":\"t3\",\"name\":\"PowerShell\",\"input\":{\"description\":\"Build the app\"}", "tool_use") + "\n");
                seen = watch.Scan(DateTime.Now);
                if (seen.Count != 2 || seen[0].Text != "running: half a line" || seen[1].Text != "running: Build the app") throw new Exception("A half-written line broke the next one: " + Told(seen));

                string helpers = Path.Combine(project, "s1", "subagents");
                Directory.CreateDirectory(helpers);
                File.WriteAllText(Path.Combine(helpers, "agent-a.jsonl"), "{}\n");
                File.WriteAllText(Path.Combine(helpers, "agent-b.jsonl"), "{}\n");
                File.AppendAllText(file, "{}\n");
                seen = watch.Scan(DateTime.Now);
                if (seen.Count != 1 || seen[0].Kind != HuskKind.Helpers || seen[0].Count != 2 || watch.HelperTotal() != 2) throw new Exception("Two busy helpers weren't counted: " + Told(seen));
                foreach (string agent in Directory.GetFiles(helpers)) File.SetLastWriteTime(agent, DateTime.Now.AddMinutes(-2));
                seen = watch.Scan(DateTime.Now);
                if (watch.HelperTotal() != 0) throw new Exception("Finished helpers kept their clones.");
                File.SetLastWriteTime(file, DateTime.Now.AddMinutes(-3));
                if (watch.Scan(DateTime.Now).Count != 0) throw new Exception("A quiet session told something.");
                File.AppendAllText(file, Assistant("tool_use", "\"id\":\"t9\",\"name\":\"AskUserQuestion\",\"input\":{}", "tool_use") + "\n");
                seen = watch.Scan(DateTime.Now);
                if (seen.Count != 1 || seen[0].Kind != HuskKind.Asking) throw new Exception("After a long quiet tool run the next ask was lost: " + Told(seen));
                Directory.CreateDirectory(Path.Combine(project, "s2", "subagents"));
                string idle = Path.Combine(project, "s2.jsonl");
                File.WriteAllText(idle, "{}\n");
                watch.Scan(DateTime.Now);
                File.WriteAllText(Path.Combine(project, "s2", "subagents", "agent-c.jsonl"), "{}\n");
                File.AppendAllText(idle, "{}\n");
                watch.Scan(DateTime.Now);
                if (watch.HelperTotal() != 1) throw new Exception("A busy helper of a second session wasn't counted.");
                File.SetLastWriteTime(idle, DateTime.Now.AddMinutes(-3));
                File.SetLastWriteTime(Path.Combine(project, "s2", "subagents", "agent-c.jsonl"), DateTime.Now.AddMinutes(-3));
                watch.Scan(DateTime.Now);
                if (watch.HelperTotal() != 0) throw new Exception("A closed session's helpers kept their calm clones.");
            }
            finally { try { Directory.Delete(root, true); } catch (IOException) { } }
            checks.Add("PASS code mode (096-06): off by default and found by voice, reads only new lines of live Claude Code sessions (old history and quiet sessions skipped, a half-written line waits), shows what it's doing by file name, tells an ask once and a finish by its first sentence, skips helpers' own lines, counts busy helpers for calm clones and lets them go when they finish (also for a quiet or closed session), and after a long quiet tool run the next ask still comes through");
        }

        private static string Assistant(string kind, string body, string stop)
        {
            return "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"" + stop + "\",\"content\":[{\"type\":\"" + kind + "\"," + body + "}]}}";
        }

        private static string Told(List<HuskEvent> seen)
        {
            List<string> parts = new List<string>();
            foreach (HuskEvent one in seen) parts.Add(one.Kind + ":" + one.Text + ":" + one.Count);
            return string.Join(" | ", parts.ToArray());
        }
    }
}