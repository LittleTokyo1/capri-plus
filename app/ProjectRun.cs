using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class ProjectEntry
    {
        internal string Folder;
        internal bool Full;
        internal bool Watch;
        internal bool Propose;

        internal string Name
        {
            get { return Path.GetFileName((Folder ?? "").TrimEnd('\\', '/')); }
        }
    }

    internal sealed class HeardTurn
    {
        internal string Folder;
        internal ProjectTurn Turn;
    }

    internal sealed class ProjectProcess
    {
        internal int Pid;
        internal DateTime Started;
    }

    [DataContract]
    internal sealed class ProjectRunState
    {
        [DataMember] public string Day = "";
        [DataMember] public int Started;
        [DataMember] public string StoppedDay = "";
        [DataMember] public int Pid;
        [DataMember] public long PidStart;
        [DataMember] public string Session = "";
        [DataMember] public string Folder = "";
        [DataMember] public long Since;
        [DataMember] public long Heard;
        [DataMember] public long Asked;
        [DataMember] public int AutoAnswers;
        [DataMember] public string LastAuto = "";
        [DataMember] public string Pending = "";
        [DataMember] public string Run = "";
        [DataMember] public long Wrapping;
        [DataMember] public bool Full;
        [DataMember] public bool Watch;
        [DataMember] public bool Propose;
        [DataMember] public long Reported;
        [DataMember] public string Tips = "";
        [DataMember] public string Landing = "";
        [DataMember] public string LandingTip = "";
        [DataMember] public List<ProjectLanding> Ready = new List<ProjectLanding>();
    }

    internal static partial class ProjectRun
    {
        internal static readonly DateTime OpusUntil = new DateTime(2026, 11, 30);
        internal const int StandardPerDay = 3, MostPerDay = 12, QuietMinutes = 20, TailBytes = 65536, CheckSeconds = 30, MostAutoAnswers = 8;
        internal const string Task = "Start the way this project's CLAUDE.md says, then do the exact next action from its checkpoint.";
        internal const string Off = "running my projects is off in Capri+";
        internal const string GameOn = "a game is running";
        internal const string Busy = "a session I started is still running";
        internal const string LimitToday = "the Claude plan limit was hit today";
        private const string LimitMark = "\"apiError\":\"usage_limit_reached\"";
        private static readonly object gate = new object();
        private static readonly List<HeardTurn> heard = new List<HeardTurn>();
        internal static string StatePath;
        internal static string TranscriptRoot;
        internal static Func<string, string[], ProjectProcess> Launch = LaunchVisible;
        internal static Func<int, long, bool> Alive = IsAlive;
        internal static Action<int> Close = CloseProcess;
        internal static Func<bool> Gaming = delegate { return Lockout.Active || Computer.GameOpen() != null; };
        internal static Func<string> HookExe = delegate { return System.Windows.Forms.Application.ExecutablePath; };

        internal static List<ProjectEntry> Entries(string[] lines)
        {
            List<ProjectEntry> found = new List<ProjectEntry>();
            if (lines == null) return found;
            foreach (string line in lines)
            {
                string plain = (line ?? "").Trim();
                if (plain.Length == 0 || plain.StartsWith("#", StringComparison.Ordinal)) continue;
                string[] parts = plain.Split('|');
                string folder = Environment.ExpandEnvironmentVariables(parts[0].Trim().Trim('"').Trim());
                if (folder.Length == 0) continue;
                bool full = false, watch = false;
                for (int i = 1; i < parts.Length; i++)
                {
                    string mark = parts[i].Trim();
                    if (mark.Equals("full", StringComparison.OrdinalIgnoreCase)) full = true;
                    else if (mark.Equals("watch", StringComparison.OrdinalIgnoreCase)) watch = true;
                }
                found.Add(new ProjectEntry { Folder = folder, Full = full && !watch, Watch = watch });
            }
            return found;
        }

        internal static ProjectEntry Find(string[] lines, string project)
        {
            if (ProjectPropose.Is(project)) return ProjectPropose.Entry();
            List<ProjectEntry> all = Entries(lines);
            if (string.IsNullOrWhiteSpace(project)) return all.Count == 0 ? null : all[0];
            string wanted = project.Trim().TrimEnd('\\', '/');
            foreach (ProjectEntry one in all)
                if (one.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase) || one.Folder.TrimEnd('\\', '/').Equals(wanted, StringComparison.OrdinalIgnoreCase)) return one;
            return null;
        }

        internal static string ModelFor(DateTime day)
        {
            return day.Date <= OpusUntil ? ClaudeJob.Model : "opusplan";
        }

        internal static string RunName(ProjectEntry entry, DateTime now)
        {
            return "capri_run_" + Regex.Replace(entry.Name, @"[^A-Za-z0-9]+", "-") + "_" + now.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture);
        }

        internal static string[] Arguments(ProjectEntry entry, string task, string session, string name, DateTime day, string hooks)
        {
            List<string> arguments = new List<string> { "--model", ModelFor(day), "--effort", "high" };
            if (entry.Watch || entry.Propose)
            {
                string[] tools = entry.Propose ? ProjectPropose.Tools : ProjectWatch.Tools;
                arguments.AddRange(new[] { "--permission-mode", "dontAsk", "--strict-mcp-config", "--tools", string.Join(",", tools), "--allowedTools" });
                arguments.AddRange(tools);
            }
            else if (entry.Full) arguments.Add("--dangerously-skip-permissions");
            else arguments.AddRange(new[] { "--permission-mode", "plan" });
            arguments.AddRange(new[] { "--session-id", session, "--name", name, "--remote-control", name });
            if (!string.IsNullOrEmpty(hooks)) arguments.AddRange(new[] { "--settings", hooks });
            arguments.Add(string.IsNullOrWhiteSpace(task) ? Task : task.Trim());
            return arguments.ToArray();
        }

        internal static string TranscriptFile(string root, string folder, string session)
        {
            string full = Path.GetFullPath(folder).TrimEnd('\\', '/');
            return Path.Combine(root, Regex.Replace(full, "[^A-Za-z0-9]", "-"), session + ".jsonl");
        }

        internal static bool LimitHit(string tail)
        {
            return (tail ?? "").IndexOf(LimitMark, StringComparison.Ordinal) >= 0;
        }

        internal static bool Finished(string tail, DateTime written, DateTime now)
        {
            if ((now - written).TotalMinutes < QuietMinutes) return false;
            string[] lines = (tail ?? "").Replace("\r", "").Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                bool assistant = line.IndexOf("\"type\":\"assistant\"", StringComparison.Ordinal) >= 0;
                bool user = line.IndexOf("\"type\":\"user\"", StringComparison.Ordinal) >= 0;
                if (!assistant && !user) continue;
                if (line.IndexOf("\"isSidechain\":true", StringComparison.Ordinal) >= 0) continue;
                return assistant && line.IndexOf("\"stop_reason\":\"end_turn\"", StringComparison.Ordinal) >= 0;
            }
            return false;
        }

        internal static string Refusal(bool on, ProjectEntry entry, ProjectRunState state, bool game, int perDay, DateTime now)
        {
            if (!on) return Off;
            if (entry == null) return "that project isn't in my Capri+ PROJECTS list";
            if (!Directory.Exists(entry.Folder)) return "the folder of " + entry.Name + " isn't there";
            if (game) return GameOn;
            if (state.Pid != 0) return Busy;
            if (state.StoppedDay == Day(now)) return LimitToday;
            int most = Math.Max(1, Math.Min(MostPerDay, perDay));
            if (Count(state, now) >= most) return "I already started " + most + " session" + (most == 1 ? "" : "s") + " today";
            return null;
        }

        internal static string Start(bool on, string[] lines, int perDay, string project, string task, DateTime now, out string name)
        {
            name = null;
            lock (gate)
            {
                ProjectRunState state = Load();
                ProjectEntry entry = Find(lines, project);
                string refused = Refusal(on, entry, state, Gaming(), perDay, now);
                if (refused != null)
                {
                    ActivityLog.Write("project session", entry == null ? (project ?? "first project") : entry.Name, "not started: " + refused);
                    return refused;
                }
                name = entry.Name;
                string session = Guid.NewGuid().ToString();
                string run = RunName(entry, now);
                string tips = entry.Full ? KeepTips(state.Tips, entry.Folder) : "";
                ProjectProcess started = null;
                try { started = Launch(entry.Folder, Arguments(entry, task, session, run, now, ProjectHook.WriteSettings(HookExe()))); }
                catch (Exception error) { ActivityLog.Write("project session", entry.Name, "didn't start: " + error.GetType().Name + ": " + error.Message); }
                if (started == null) return "the session didn't start";
                if (state.Day != Day(now)) { state.Day = Day(now); state.Started = 0; }
                state.Started++;
                state.Pid = started.Pid;
                state.PidStart = started.Started.Ticks;
                state.Session = session;
                state.Folder = entry.Folder;
                state.Since = now.Ticks;
                state.Heard = 0;
                state.Asked = 0;
                state.AutoAnswers = 0;
                state.LastAuto = "";
                state.Pending = "";
                state.Run = run;
                state.Wrapping = 0;
                state.Full = entry.Full;
                state.Watch = entry.Watch;
                state.Propose = entry.Propose;
                state.Reported = 0;
                state.Tips = tips;
                state.Landing = "";
                state.LandingTip = "";
                Save(state);
                ProjectHook.Expect(session, true);
                ActivityLog.Write("project session", entry.Name, "started " + run + " (" + (entry.Propose ? "proposal: web research only" : entry.Watch ? "watch: reads only, no commands" : entry.Full ? "may edit and commit" : "read and plan only") + ", session " + session + ")");
                return null;
            }
        }

        internal static string Check(DateTime now)
        {
            lock (gate)
            {
                ProjectRunState state = Load();
                if (state.Pid == 0) return null;
                string file = TranscriptFile(TranscriptRoot ?? HuskWatch.DefaultRoot(), state.Folder, state.Session);
                string tail = Tail(file);
                Heard(state);
                bool alive = Alive(state.Pid, state.PidStart);
                if (LimitHit(tail))
                {
                    state.StoppedDay = Day(now);
                    ActivityLog.Write("project session", Path.GetFileName(state.Folder.TrimEnd('\\', '/')), "the Claude plan limit was hit; no new sessions today");
                    if (alive) Shut(state);
                    return End(state, "limit", now);
                }
                if (!alive) return End(state, "closed", now);
                DateTime written = File.Exists(file) ? File.GetLastWriteTime(file) : new DateTime(state.Since);
                if (state.Asked != 0 || !Finished(tail, written, now)) return null;
                Shut(state);
                return End(state, "finished", now);
            }
        }

        internal static string Told(ProjectTurn turn)
        {
            if (turn.Kind == ProjectHook.Ask)
            {
                List<string> parts = new List<string>();
                for (int i = 0; i < turn.Questions.Length; i++) parts.Add(turn.Questions[i] + (i < turn.Options.Length && turn.Options[i].Length > 0 ? " (" + turn.Options[i] + ")" : ""));
                return "asks: " + Pursuit.Shorter(string.Join(" | ", parts.ToArray()), 200);
            }
            return (turn.Kind == ProjectHook.Plan ? "plans (waits for your yes): " : "said: ") + Pursuit.Shorter(HuskWatch.FirstLine(turn.Text, 200), 200);
        }

        private static void Heard(ProjectRunState state)
        {
            ProjectTurn turn = ProjectHook.Turn(state.Session);
            if (turn == null || turn.At <= state.Heard) return;
            state.Heard = turn.At;
            if (state.Asked != 0 && turn.At > state.Asked) state.Asked = 0;
            Save(state);
            ActivityLog.Write("project session", Path.GetFileName(state.Folder.TrimEnd('\\', '/')), Told(turn));
            lock (heard) heard.Add(new HeardTurn { Folder = state.Folder, Turn = turn });
        }

        internal static List<HeardTurn> TakeHeard()
        {
            lock (heard)
            {
                List<HeardTurn> taken = new List<HeardTurn>(heard);
                heard.Clear();
                return taken;
            }
        }

        internal static void NoteAsked(string session, long turn)
        {
            lock (gate)
            {
                ProjectRunState state = Load();
                if (state.Session != session) return;
                state.Asked = turn;
                state.AutoAnswers = 0;
                state.LastAuto = "";
                Save(state);
            }
        }

        internal static bool AutoAllowed(string session, string question)
        {
            lock (gate)
            {
                ProjectRunState state = Load();
                if (state.Session != session) return true;
                return state.AutoAnswers < MostAutoAnswers && !string.Equals(state.LastAuto ?? "", question ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static void AutoAnswered(string session, string question)
        {
            lock (gate)
            {
                ProjectRunState state = Load();
                if (state.Session != session) return;
                state.AutoAnswers++;
                state.LastAuto = question ?? "";
                Save(state);
            }
        }

        internal static bool Hers(string session)
        {
            if (string.IsNullOrEmpty(session)) return false;
            if (ProjectRelay.Judged(session)) return true;
            lock (gate) return session.Equals(Load().Session, StringComparison.OrdinalIgnoreCase);
        }

        private static void Shut(ProjectRunState state)
        {
            try { Close(state.Pid); }
            catch (Exception error) { ActivityLog.Write("project session", Path.GetFileName(state.Folder.TrimEnd('\\', '/')), "couldn't close its window: " + error.Message); }
        }

        internal static ProjectRunState Load()
        {
            string path = FilePath();
            if (path == null || !File.Exists(path)) return new ProjectRunState();
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    ProjectRunState state = (ProjectRunState)new DataContractJsonSerializer(typeof(ProjectRunState)).ReadObject(stream);
                    if (state == null) return new ProjectRunState();
                    if (state.Ready == null) state.Ready = new List<ProjectLanding>();
                    return state;
                }
            }
            catch (Exception error)
            {
                ActivityLog.Write("project session", "state file", "unreadable, starting fresh: " + error.Message);
                return new ProjectRunState();
            }
        }

        private static string End(ProjectRunState state, string how, DateTime now)
        {
            string project = Path.GetFileName(state.Folder.TrimEnd('\\', '/'));
            int minutes = (int)Math.Max(0, (now - new DateTime(state.Since)).TotalMinutes);
            ActivityLog.Write("project session", project, "ended: " + how + " after " + minutes + " min (session " + state.Session + ")");
            if (how != FreshEnd)
            {
                AfterEnd(state, now);
                WatchEnded(state, how);
            }
            ProjectHook.Forget(state.Session);
            state.Landing = "";
            state.LandingTip = "";
            if (how != FreshEnd)
            {
                state.Tips = "";
                state.Full = false;
            }
            state.Watch = false;
            state.Propose = false;
            state.Reported = 0;
            state.Heard = 0;
            state.Asked = 0;
            state.Pending = "";
            state.Run = "";
            state.Wrapping = 0;
            state.Pid = 0;
            state.PidStart = 0;
            state.Session = "";
            state.Folder = "";
            state.Since = 0;
            Save(state);
            return how;
        }

        private static int Count(ProjectRunState state, DateTime now)
        {
            return state.Day == Day(now) ? state.Started : 0;
        }

        private static string Day(DateTime now)
        {
            return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string FilePath()
        {
            if (StatePath != null) return StatePath;
            return ForkSettings.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ForkSettings.FilePath), "project-runs.json");
        }

        private static void Save(ProjectRunState state)
        {
            string path = FilePath();
            if (path == null) return;
            try
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(ProjectRunState)).WriteObject(buffer, state);
                    File.WriteAllBytes(path, buffer.ToArray());
                }
            }
            catch (Exception error) { ActivityLog.Write("project session", "state file", "couldn't save: " + error.Message); }
        }

        private static string Tail(string file)
        {
            if (!File.Exists(file)) return "";
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(0, stream.Length - TailBytes);
                    stream.Seek(start, SeekOrigin.Begin);
                    byte[] bytes = new byte[stream.Length - start];
                    int got = 0;
                    while (got < bytes.Length)
                    {
                        int read = stream.Read(bytes, got, bytes.Length - got);
                        if (read <= 0) break;
                        got += read;
                    }
                    return Encoding.UTF8.GetString(bytes, 0, got);
                }
            }
            catch (IOException) { return ""; }
        }

        private static ProjectProcess LaunchVisible(string folder, string[] arguments)
        {
            StringBuilder line = new StringBuilder();
            foreach (string argument in arguments) line.Append(line.Length > 0 ? " " : "").Append(ClaudeJob.Quote(argument));
            ProcessStartInfo start = new ProcessStartInfo(ClaudeJob.Exe(), line.ToString())
            {
                WorkingDirectory = folder,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            };
            using (Process run = Process.Start(start))
            {
                if (run == null) return null;
                return new ProjectProcess { Pid = run.Id, Started = run.StartTime };
            }
        }

        private static bool IsAlive(int pid, long started)
        {
            try
            {
                using (Process run = Process.GetProcessById(pid))
                    return !run.HasExited && Math.Abs(run.StartTime.Ticks - started) < TimeSpan.TicksPerSecond;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }

        private static void CloseProcess(int pid)
        {
            using (Process run = Process.GetProcessById(pid)) run.Kill();
        }
    }
}
