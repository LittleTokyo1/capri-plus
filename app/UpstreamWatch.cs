using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class UpstreamWatchState
    {
        [DataMember] public string Seen = "";
        [DataMember] public string Day = "";
        [DataMember] public long NextTry;
        [DataMember] public string Stuck = "";
    }

    internal static class UpstreamWatch
    {
        internal const string Baseline = "a6e481b69577ff614776acb854be665d57f04524";
        internal const string Repo = "capri-desktop";
        internal const int RetryMinutes = 60, MostListed = 40;
        internal static string StatePath;
        internal static Func<ProjectEntry, string, DateTime, string> Begin;
        private static readonly object gate = new object();

        internal const string FullTask = "Start the way this project's CLAUDE.md says. Read each of these commits with what it depends on and check docs/notes/CAPRI-096.md for what we already have. Port what we lack our way (adapt it to the fork, never drop it in whole; no Ko-fi or donation lines; no personal data), one branch per feature in its own new worktree off main (never work in the main checkout), smoke checks first, build until every line says PASS. Never merge into main or push main: every landing waits for his yes. Add each port to the CAPRI-096 list. End with what you ported, on which branch, and what you skipped and why.";
        internal const string PlanTask = "Read and plan only, no edits. Start the way this project's CLAUDE.md says, read each of these commits with what it depends on, check docs/notes/CAPRI-096.md for what we already have, and plan the ports we lack (files, risks, pass conditions). End with that plan and what you would skip and why.";

        internal static string Watch(bool on, string[] lines, int perDay, DateTime now)
        {
            if (!on || ProjectRun.Gaming()) return null;
            lock (gate)
            {
                UpstreamWatchState state = Load();
                string today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (state.Day == today || now.Ticks < state.NextTry) return null;
                state.NextTry = now.AddMinutes(RetryMinutes).Ticks;
                ProjectEntry entry = Ours(lines);
                if (entry == null) return Quiet(state, today, "skipped", "no listed project has the " + Repo + " upstream");
                if (ProjectRun.Git(entry.Folder, new[] { "fetch", "--quiet", "upstream" }) == null) return Wait(state, entry, "fetch failed, trying again in an hour");
                string tip = ProjectRun.Git(entry.Folder, new[] { "rev-parse", "--verify", "--quiet", "upstream/main" });
                if (string.IsNullOrEmpty(tip)) return Wait(state, entry, "no upstream/main after the fetch, trying again in an hour");
                string seen = string.IsNullOrEmpty(state.Seen) ? Baseline : state.Seen;
                if (tip == seen) { state.Seen = tip; return Quiet(state, today, entry.Name, "nothing new since " + Short(tip)); }
                string count = ProjectRun.Git(entry.Folder, new[] { "rev-list", "--count", seen + ".." + tip });
                string list = ProjectRun.Git(entry.Folder, new[] { "log", "--format=%h %s", "-" + MostListed, seen + ".." + tip });
                if (count == null || list == null) return Stuck(state, today, entry, seen, tip);
                if (count == "0") { state.Seen = tip; return Quiet(state, today, entry.Name, "upstream moved back to " + Short(tip) + ", nothing new"); }
                string task = Task(entry, seen, tip, count, list);
                string refused = Begin != null ? Begin(entry, task, now) : StartIt(on, lines, perDay, entry, task, now);
                if (refused != null) return Wait(state, entry, count + " new commits waiting: " + refused);
                state.Seen = tip;
                state.Day = today;
                state.Stuck = "";
                Save(state);
                ActivityLog.Write("upstream watch", entry.Name, "started a " + (entry.Full ? "port" : "plan") + " session for " + count + " new commits " + Short(seen) + ".." + Short(tip));
                return "The original Capri has " + count + " new commit" + (count == "1" ? "" : "s") + "; I started a session in " + entry.Name + " to " + (entry.Full ? "port what we lack on branches (nothing lands without your yes)." : "read them and plan the ports (it can't edit).");
            }
        }

        internal static ProjectEntry Ours(string[] lines)
        {
            ProjectEntry found = null;
            foreach (ProjectEntry one in ProjectRun.Entries(lines))
            {
                string url = ProjectRun.Git(one.Folder, new[] { "remote", "get-url", "upstream" });
                if (url == null) continue;
                string plain = url.Trim().TrimEnd('/');
                if (plain.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) plain = plain.Substring(0, plain.Length - 4);
                if (!plain.EndsWith("/" + Repo, StringComparison.OrdinalIgnoreCase)) continue;
                if (one.Full) return one;
                if (found == null) found = one;
            }
            return found;
        }

        internal static string Task(ProjectEntry entry, string seen, string tip, string count, string list)
        {
            string commits = list.Replace("\"", "'").Replace("\r", "").Replace("\n", "; ");
            return "The original Capri (the remote named upstream) has " + count + " new commits since " + Short(seen) + " (newest " + Short(tip) + "): " + commits + ". " + (entry.Full ? FullTask : PlanTask);
        }

        private static string StartIt(bool on, string[] lines, int perDay, ProjectEntry entry, string task, DateTime now)
        {
            string name;
            return ProjectRun.Start(on, lines, perDay, entry.Folder, task, now, out name);
        }

        private static string Quiet(UpstreamWatchState state, string today, string what, string detail)
        {
            state.Day = today;
            Save(state);
            ActivityLog.Write("upstream watch", what, detail);
            return null;
        }

        private static string Wait(UpstreamWatchState state, ProjectEntry entry, string detail)
        {
            Save(state);
            ActivityLog.Write("upstream watch", entry.Name, detail);
            return null;
        }

        private static string Stuck(UpstreamWatchState state, string today, ProjectEntry entry, string seen, string tip)
        {
            bool told = state.Stuck == tip;
            state.Stuck = tip;
            Quiet(state, today, entry.Name, "can't line up " + Short(seen) + ".." + Short(tip));
            return told ? null : "I can't line up the original Capri's new commits with the last ones I saw (" + Short(seen) + " to " + Short(tip) + "), so I didn't start a port session; its history may have been rewritten.";
        }

        private static string Short(string sha) { return sha == null ? "" : sha.Substring(0, Math.Min(7, sha.Length)); }

        private static string FilePath()
        {
            if (StatePath != null) return StatePath;
            return ForkSettings.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ForkSettings.FilePath), "upstream-watch.json");
        }

        internal static UpstreamWatchState Load()
        {
            string path = FilePath();
            if (path == null || !File.Exists(path)) return new UpstreamWatchState();
            try
            {
                using (FileStream stream = File.OpenRead(path))
                    return (UpstreamWatchState)new DataContractJsonSerializer(typeof(UpstreamWatchState)).ReadObject(stream) ?? new UpstreamWatchState();
            }
            catch (Exception error)
            {
                ActivityLog.Write("upstream watch", "state file", "unreadable, starting fresh: " + error.Message);
                return new UpstreamWatchState();
            }
        }

        private static void Save(UpstreamWatchState state)
        {
            string path = FilePath();
            if (path == null) return;
            try
            {
                using (FileStream stream = File.Create(path)) new DataContractJsonSerializer(typeof(UpstreamWatchState)).WriteObject(stream, state);
            }
            catch (IOException error) { ActivityLog.Write("upstream watch", "state file", "not saved: " + error.Message); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("upstream watch", "state file", "not saved: " + error.Message); }
        }
    }
}
