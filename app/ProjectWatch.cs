using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class WatchedProject
    {
        [DataMember] public string Folder = "";
        [DataMember] public string Day = "";
        [DataMember] public string Head = "";
    }

    [DataContract]
    internal sealed class ProjectWatchState
    {
        [DataMember] public long NextTry;
        [DataMember] public List<WatchedProject> Seen = new List<WatchedProject>();
    }

    internal static class ProjectWatch
    {
        internal const string Mark = "WATCH REPORT:";
        internal const int RetryMinutes = 60, ToldChars = 300;
        internal static readonly string[] Tools = { "Read", "Grep", "Glob", "AskUserQuestion" };
        internal const string Task = "Watch only: you may read files, never edit, commit, push or run commands, and never touch this project's server. Read this project's CLAUDE.md, its checkpoint and its notes (what they say to read first, then only what the next action touches). Then end with one message that starts with " + Mark + " and says in a few short lines where it stands, what is next, any risks, your suggestions, and any question for the user.";
        internal static string StatePath;
        internal static Func<ProjectEntry, string, DateTime, string> Begin;
        private static readonly object gate = new object();

        internal static string Watch(bool on, string[] lines, int perDay, DateTime now)
        {
            if (!on || ProjectRun.Gaming()) return null;
            lock (gate)
            {
                ProjectWatchState state = Load();
                if (now.Ticks < state.NextTry) return null;
                string today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                foreach (ProjectEntry entry in ProjectRun.Entries(lines))
                {
                    if (!entry.Watch) continue;
                    WatchedProject seen = Seen(state, entry.Folder);
                    if (seen.Day == today) continue;
                    string head = ProjectRun.Git(entry.Folder, new[] { "rev-parse", "--verify", "--quiet", "HEAD" }) ?? "";
                    if (head.Length > 0 && head == seen.Head)
                    {
                        seen.Day = today;
                        Save(state);
                        ActivityLog.Write("project watch", entry.Name, "nothing new since the last watch (" + head.Substring(0, Math.Min(7, head.Length)) + ")");
                        continue;
                    }
                    string refused = Begin != null ? Begin(entry, Task, now) : StartIt(on, lines, perDay, entry, now);
                    if (refused != null)
                    {
                        state.NextTry = now.AddMinutes(RetryMinutes).Ticks;
                        Save(state);
                        ActivityLog.Write("project watch", entry.Name, "waiting to start: " + refused);
                        return null;
                    }
                    seen.Day = today;
                    seen.Head = head;
                    Save(state);
                    ActivityLog.Write("project watch", entry.Name, "started a read-only watch session");
                    return "I started a read-only watch session in " + entry.Name + ": it reads where the project stands and reports back. It can't edit or run commands and never touches its server.";
                }
                return null;
            }
        }

        internal static string Report(string said)
        {
            string text = said ?? "";
            int mark = text.LastIndexOf(Mark, StringComparison.Ordinal);
            return mark < 0 ? null : text.Substring(mark + Mark.Length).Trim();
        }

        internal static string Keep(string project, string report, DateTime now)
        {
            string root = Vault.Root;
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(report)) return null;
            string name = Regex.Replace(project ?? "project", "[\\\\/:*?\"<>|]+", "-") + " watch.md";
            string folder = Path.Combine(Path.Combine(root, "Capri"), "projects");
            string path = Path.Combine(folder, name);
            try
            {
                Directory.CreateDirectory(folder);
                StringBuilder text = new StringBuilder();
                if (!File.Exists(path)) text.Append("# ").Append(project).Append(" watch\n\nWhat her read-only watch sessions of ").Append(project).Append(" found, newest last.\n");
                text.Append("\n## ").Append(now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append("\n\n").Append(report.Replace("\r", "").Trim()).Append('\n');
                File.AppendAllText(path, text.ToString(), new UTF8Encoding(false));
                return "Capri/projects/" + name;
            }
            catch (IOException error) { ActivityLog.Write("project watch", project, "report not kept: " + error.Message); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("project watch", project, "report not kept: " + error.Message); }
            return null;
        }

        internal static string Told(string project, string report, string kept)
        {
            return "Your " + project + " watch: " + Pursuit.Shorter(Regex.Replace(report ?? "", @"\s+", " ").Trim(), ToldChars) + (kept == null ? "" : " (all of it in " + kept + ")");
        }

        private static WatchedProject Seen(ProjectWatchState state, string folder)
        {
            string wanted = (folder ?? "").TrimEnd('\\', '/');
            foreach (WatchedProject one in state.Seen)
                if (one.Folder.Equals(wanted, StringComparison.OrdinalIgnoreCase)) return one;
            WatchedProject added = new WatchedProject { Folder = wanted };
            state.Seen.Add(added);
            return added;
        }

        private static string StartIt(bool on, string[] lines, int perDay, ProjectEntry entry, DateTime now)
        {
            string name;
            return ProjectRun.Start(on, lines, perDay, entry.Folder, Task, now, out name);
        }

        private static string FilePath()
        {
            if (StatePath != null) return StatePath;
            return ForkSettings.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ForkSettings.FilePath), "project-watch.json");
        }

        internal static ProjectWatchState Load()
        {
            string path = FilePath();
            if (path == null || !File.Exists(path)) return new ProjectWatchState();
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    ProjectWatchState state = (ProjectWatchState)new DataContractJsonSerializer(typeof(ProjectWatchState)).ReadObject(stream) ?? new ProjectWatchState();
                    if (state.Seen == null) state.Seen = new List<WatchedProject>();
                    return state;
                }
            }
            catch (Exception error)
            {
                ActivityLog.Write("project watch", "state file", "unreadable, starting fresh: " + error.Message);
                return new ProjectWatchState();
            }
        }

        private static void Save(ProjectWatchState state)
        {
            string path = FilePath();
            if (path == null) return;
            try
            {
                using (FileStream stream = File.Create(path)) new DataContractJsonSerializer(typeof(ProjectWatchState)).WriteObject(stream, state);
            }
            catch (IOException error) { ActivityLog.Write("project watch", "state file", "not saved: " + error.Message); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("project watch", "state file", "not saved: " + error.Message); }
        }
    }

    internal static partial class ProjectRun
    {
        internal static void Reported(HeardTurn one, DateTime now)
        {
            if (one == null || one.Turn == null || one.Turn.Kind != ProjectHook.Stop) return;
            string project, report;
            bool proposal;
            lock (gate)
            {
                ProjectRunState state = Load();
                if ((!state.Watch && !state.Propose) || state.Session != one.Turn.Session || state.Reported >= one.Turn.At) return;
                proposal = state.Propose;
                report = proposal ? ProjectPropose.Report(one.Turn.Text) : ProjectWatch.Report(one.Turn.Text);
                if (string.IsNullOrEmpty(report)) return;
                state.Reported = one.Turn.At;
                Save(state);
                project = ProjectName(state);
            }
            if (proposal)
            {
                Tell(ProjectPropose.Keep(report, now));
                return;
            }
            string kept = ProjectWatch.Keep(project, report, now);
            ActivityLog.Write("project watch", project, "reported: " + Pursuit.Shorter(HuskWatch.FirstLine(report, 200), 200));
            Tell(ProjectWatch.Told(project, report, kept));
        }

        private static void WatchEnded(ProjectRunState state, string how)
        {
            if ((!state.Watch && !state.Propose) || state.Reported != 0) return;
            ProjectTurn turn = ProjectHook.Turn(state.Session);
            string last = turn == null ? "" : Regex.Replace(turn.Text ?? "", @"\s+", " ").Trim();
            string ended = state.Propose ? "My project proposal session ended (" + how + ") without a proposal" : "Your " + ProjectName(state) + " watch session ended (" + how + ") without its report";
            Tell(ended + (last.Length > 0 ? "; its last words: " + Pursuit.Shorter(last, ProjectWatch.ToldChars) : "."));
        }
    }
}
