using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeIdeas(List<string> checks)
        {
            if (!Hands.Tools.Contains("\"name\":\"" + Ideas.Tool + "\"") || Brain.NeedsTools.IndexOf("ideas for improving you", StringComparison.Ordinal) < 0) throw new Exception("Her brain isn't offered the idea tool, or doesn't know ideas need it.");
            string root = Path.Combine(Path.GetTempPath(), "capri-ideas-smoke-" + Guid.NewGuid().ToString("N"));
            string vault = Vault.Root, statePath = Ideas.StatePath;
            Func<string, string[], string> git = ProjectRun.Git;
            Func<bool> gaming = ProjectRun.Gaming;
            Func<ProjectEntry, string, DateTime, string> begin = Ideas.Begin;
            string refuse = null, task = null;
            bool game = false;
            int starts = 0;
            try
            {
                Directory.CreateDirectory(root);
                Ideas.StatePath = Path.Combine(root, "ideas-run.json");
                ProjectRun.Gaming = delegate { return game; };
                Ideas.Begin = delegate(ProjectEntry entry, string asked, DateTime when) { starts++; task = asked; return refuse; };
                ProjectRun.Git = delegate(string where, string[] args) { return string.Join(" ", args) == "remote get-url upstream" && where == "C:\\Work\\capri" ? "https://github.com/lachydotmcg/capri-desktop.git" : null; };
                string[] lines = { "C:\\Work\\capri | full" };
                DateTime day = new DateTime(2026, 11, 5, 10, 0, 0);
                Vault.Root = null;
                if (Ideas.Run("add", "a calmer idle animation", "add an idea: a calmer idle animation", day) != Ideas.NoVault) throw new Exception("An idea was taken with no vault to keep it in.");
                Vault.Root = root;
                string file = Path.Combine(root, "Capri", Ideas.FileName);
                if (!Ideas.Run("add", "a calmer idle animation", "add an idea: a calmer idle animation", day).StartsWith("Saved", StringComparison.Ordinal) || !File.ReadAllText(file).Contains("- [ ] a calmer idle animation (added 2026-11-05)")) throw new Exception("An idea wasn't saved as an open line in Capri/ideas.md.");
                if (Ideas.Run("add", "A calmer idle animation", null, day).StartsWith("Saved", StringComparison.Ordinal)) throw new Exception("The same idea was saved twice.");
                Ideas.Run("add", "let her buy my groceries every week (the usual)", "idea: let her buy my groceries every week", day);
                string listed = Ideas.Run("list", null, null, day);
                if (!listed.Contains("a calmer idle animation (open)") || !listed.Contains("groceries")) throw new Exception("Her ideas list didn't read back: " + listed);
                if (Ideas.Run("approve", "groceries", null, day) != Ideas.NotHis) throw new Exception("An idea was approved without his own words.");
                if (Ideas.Watch(false, lines, 3, day) != null || starts != 0) throw new Exception("An idea started with his projects switch off.");
                game = true;
                if (Ideas.Watch(true, lines, 3, day) != null || starts != 0) throw new Exception("An idea started during a game.");
                game = false;
                string said = Ideas.Watch(true, lines, 3, day);
                string kept = File.ReadAllText(file);
                if (said == null || !said.Contains("need your yes") || !said.Contains("groceries") || !said.Contains("started a session") || starts != 1 || !kept.Contains("- [?] let her buy my groceries") || !kept.Contains("- [/] a calmer idle animation (started 2026-11-05)")) throw new Exception("The daily idea pass didn't ask first for a buying idea and start the plain one: " + said + " / " + kept);
                if (!task.Contains("a calmer idle animation") || !task.Contains("Never merge into main") || !task.Contains("own new worktree") || !task.Contains("stop and say so")) throw new Exception("The idea session's task lacks the idea or the landing and worktree rules: " + task);
                if (Ideas.Watch(true, lines, 3, day.AddHours(5)) != null || starts != 1) throw new Exception("The idea pass ran twice in a day.");
                if (!Ideas.Run("approve", "groceries idea", "yes build the groceries idea", day).StartsWith("Got it", StringComparison.Ordinal) || !File.ReadAllText(file).Contains("- [!] let her buy my groceries")) throw new Exception("His yes didn't mark the asked-first idea for the next session.");
                day = day.AddDays(1);
                refuse = "one of my sessions is still running";
                if (Ideas.Watch(true, lines, 3, day) != null || starts != 2 || !File.ReadAllText(file).Contains("- [!] let her buy")) throw new Exception("A refused start still marked the idea started.");
                if (Ideas.Watch(true, lines, 3, day.AddMinutes(20)) != null || starts != 2) throw new Exception("The idea pass retried before an hour passed.");
                refuse = null;
                said = Ideas.Watch(true, lines, 3, day.AddMinutes(61));
                if (said == null || !said.Contains("groceries") || starts != 3 || !File.ReadAllText(file).Contains("- [/] let her buy my groceries")) throw new Exception("The idea with his yes didn't start once the slot was free: " + said);
                if (!Ideas.Run("drop", "calmer idle animation", "drop the calmer idle animation idea", day).StartsWith("Dropped", StringComparison.Ordinal) || File.ReadAllText(file).Contains("calmer")) throw new Exception("Dropping an idea didn't remove it.");
                day = day.AddDays(1);
                Ideas.Run("add", "a sharper glitch intro", "idea: a sharper glitch intro", day);
                if (Ideas.Watch(true, new[] { "C:\\Work\\capri" }, 3, day) != null || starts != 3) throw new Exception("An idea started in a project that may only read and plan.");
            }
            finally
            {
                Vault.Root = vault;
                Ideas.StatePath = statePath;
                ProjectRun.Git = git;
                ProjectRun.Gaming = gaming;
                Ideas.Begin = begin;
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
            checks.Add("PASS her ideas list (097 slice 7): the idea tool keeps ideas in Capri/ideas.md (add, list, drop, never twice, never with no vault), once a day with his projects switch on and no game the first open idea starts one session in the | full capri project that builds it on its own branch in a new worktree and never lands; an idea that touches buying, keys, spending, SECURITY or the lockout is asked about first and starts only after his own yes; a refused start retries after an hour; a read-and-plan project never builds one");
        }
    }
}
