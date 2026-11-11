using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeUpstreamWatch(List<string> checks)
        {
            string path = Path.Combine(Path.GetTempPath(), "capri-upstream-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            Func<string, string[], string> git = ProjectRun.Git;
            Func<bool> gaming = ProjectRun.Gaming;
            Func<ProjectEntry, string, DateTime, string> begin = UpstreamWatch.Begin;
            string statePath = UpstreamWatch.StatePath;
            Dictionary<string, string> remotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string tip = UpstreamWatch.Baseline, count = "0", log = "", refuse = null, task = null, folder = null;
            bool game = false;
            int fetches = 0, starts = 0;
            try
            {
                UpstreamWatch.StatePath = path;
                ProjectRun.Gaming = delegate { return game; };
                UpstreamWatch.Begin = delegate(ProjectEntry entry, string asked, DateTime when) { starts++; task = asked; folder = entry.Folder; return refuse; };
                ProjectRun.Git = delegate(string where, string[] args)
                {
                    string asked = string.Join(" ", args);
                    string url;
                    if (asked == "remote get-url upstream") return remotes.TryGetValue(where, out url) ? url : null;
                    if (asked == "fetch --quiet upstream") { fetches++; return ""; }
                    if (asked.StartsWith("rev-parse", StringComparison.Ordinal)) return tip;
                    if (asked.StartsWith("rev-list", StringComparison.Ordinal)) return count;
                    if (asked.StartsWith("log", StringComparison.Ordinal)) return log;
                    return null;
                };
                string[] lines = { "C:\\Work\\other | full", "C:\\Work\\capri-plain", "C:\\Work\\capri | full" };
                remotes["C:\\Work\\other"] = "https://github.com/someone/other.git";
                remotes["C:\\Work\\capri-plain"] = "https://github.com/lachydotmcg/capri-desktop";
                remotes["C:\\Work\\capri"] = "https://github.com/lachydotmcg/capri-desktop.git";
                DateTime day = new DateTime(2026, 11, 5, 10, 0, 0);
                if (UpstreamWatch.Watch(false, lines, 3, day) != null || fetches != 0) throw new Exception("The upstream watch ran with his projects switch off.");
                game = true;
                if (UpstreamWatch.Watch(true, lines, 3, day) != null || fetches != 0) throw new Exception("The upstream watch fetched during a game.");
                game = false;
                if (UpstreamWatch.Ours(lines) == null || UpstreamWatch.Ours(lines).Folder != "C:\\Work\\capri") throw new Exception("The upstream watch didn't pick the | full project whose upstream is the original Capri.");
                if (UpstreamWatch.Ours(new[] { "C:\\Work\\other | full" }) != null) throw new Exception("The upstream watch took a project whose upstream is some other repo.");
                if (UpstreamWatch.Watch(true, lines, 3, day) != null || fetches != 1 || starts != 0 || UpstreamWatch.Load().Seen != UpstreamWatch.Baseline) throw new Exception("Nothing new upstream since the 096 survey still started something or wasn't kept.");
                if (UpstreamWatch.Watch(true, lines, 3, day.AddHours(3)) != null || fetches != 1) throw new Exception("The upstream watch fetched twice in a day.");
                day = day.AddDays(1);
                tip = "b000000000000000000000000000000000000002";
                count = "2";
                log = "b000000 feat: a \"new\" thing\nb000001 fix: another";
                refuse = "one of my sessions is still running";
                if (UpstreamWatch.Watch(true, lines, 3, day) != null || starts != 1 || UpstreamWatch.Load().Seen != UpstreamWatch.Baseline) throw new Exception("A refused start still marked upstream's new commits as seen.");
                if (UpstreamWatch.Watch(true, lines, 3, day.AddMinutes(20)) != null || starts != 1) throw new Exception("The upstream watch retried before an hour passed.");
                refuse = null;
                string said = UpstreamWatch.Watch(true, lines, 3, day.AddMinutes(61));
                if (said == null || !said.Contains("2 new commits") || starts != 2 || folder != "C:\\Work\\capri" || UpstreamWatch.Load().Seen != tip) throw new Exception("New upstream commits didn't start one port session and move what she saw: " + said);
                if (!task.Contains("b000000 feat: a 'new' thing; b000001 fix: another") || !task.Contains("Never merge into main") || !task.Contains("own new worktree") || !task.Contains("CAPRI-096") || !task.Contains("Ko-fi") || task.Contains("\n")) throw new Exception("The port session's task lacks the commits or the landing, worktree and port rules: " + task);
                if (!UpstreamWatch.Task(new ProjectEntry { Folder = "C:\\Work\\capri-plain" }, UpstreamWatch.Baseline, tip, "2", log).Contains("Read and plan only")) throw new Exception("A project without | full was asked to port, not to plan.");
                day = day.AddDays(1);
                tip = "c000000000000000000000000000000000000003";
                count = null;
                string stuck = UpstreamWatch.Watch(true, lines, 3, day);
                if (stuck == null || !stuck.Contains("didn't start") || starts != 2) throw new Exception("A history upstream rewrote wasn't told, or still started a session: " + stuck);
                if (UpstreamWatch.Watch(true, lines, 3, day.AddDays(1)) != null || starts != 2) throw new Exception("The same rewritten upstream history was told twice.");
            }
            finally
            {
                ProjectRun.Git = git;
                ProjectRun.Gaming = gaming;
                UpstreamWatch.Begin = begin;
                UpstreamWatch.StatePath = statePath;
                try { File.Delete(path); } catch (IOException) { }
            }
            checks.Add("PASS the daily upstream watch (097 slice 6): only with his projects switch on and never in a game, once a day, in the listed project whose upstream is the original Capri (a | full one first); nothing new since the 096 survey starts nothing; new commits start one session that ports on its own branches in a new worktree and never lands (or plans, without | full), and only a started session marks them seen; a refused start retries after an hour; a rewritten upstream history is told once");
        }
    }
}
