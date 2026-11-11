using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed class ClaudeJobResult
    {
        internal bool Ok;
        internal string Session;
        internal string Output = "";
        internal string Problem;
        internal string Last
        {
            get
            {
                string[] lines = (Output ?? "").Replace("\r", "").Split('\n');
                for (int i = lines.Length - 1; i >= 0; i--) if (lines[i].Trim().Length > 0) return lines[i].Trim();
                return "";
            }
        }
    }

    internal static class ClaudeJob
    {
        internal const string Model = "claude-opus-5-5";
        internal const int Sonnet = 1;
        internal static readonly string[] ModelNames = { "Opus 5.5, high effort (best for school work, uses more of my plan)", "Sonnet 5.5 (lighter, saves tokens)" };
        internal const int PollSeconds = 20;

        internal static string[] ModelArguments(int pick)
        {
            return pick == Sonnet ? new[] { "--model", "sonnet" } : new[] { "--model", Model, "--effort", "high" };
        }
        internal const int OutputChars = 6000;
        internal const int CliSeconds = 60;
        internal const int GameCheckSeconds = 5;
        internal const int MissingPolls = 2;
        internal const string GameStarted = "a game started";
        internal const string Stuck = "it got stuck on a permission prompt";
        internal const string SignInPage = "it stopped at a sign-in page";
        internal const string PlanLimit = "my Claude plan limit is hit for now";
        private const int TailLines = 20;
        private static readonly Regex Started = new Regex(@"backgrounded\D{0,16}?\b([0-9a-f]{8})\b", RegexOptions.IgnoreCase);
        private static readonly Regex Ansi = new Regex(@"\x1B\[[0-9;?]*[A-Za-z]");
        internal const int CliTries = 3;
        internal const int CliRetrySeconds = 5;
        internal static Func<string, string[], string> Cli = delegate(string folder, string[] arguments) { return Retried(delegate { return RunCli(folder, arguments); }); };
        internal static Action<int> Wait = delegate(int seconds) { Thread.Sleep(seconds * 1000); };
        private static readonly object gate = new object();

        internal static string Retried(Func<string> call)
        {
            for (int tried = 1; ; tried++)
            {
                try { return call(); }
                catch (System.ComponentModel.Win32Exception)
                {
                    if (tried >= CliTries) throw;
                    ActivityLog.Write("claude job", "cli", "the claude exe wasn't there, trying again in " + CliRetrySeconds + " s");
                    Wait(CliRetrySeconds);
                }
            }
        }

        internal static string Exe()
        {
            string npm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules", "@anthropic-ai", "claude-code", "bin", "claude.exe");
            if (File.Exists(npm)) return npm;
            string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");
            return File.Exists(local) ? local : "claude";
        }

        internal static string Quote(string argument)
        {
            string flat = argument.Replace("\r", " ").Replace("\n", " ");
            if (flat.Length > 0 && flat.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return flat;
            return "\"" + Regex.Replace(Regex.Replace(flat, @"(\\*)""", "$1$1\\\""), @"(\\+)$", "$1$1") + "\"";
        }

        private static string RunCli(string folder, string[] arguments)
        {
            StringBuilder line = new StringBuilder();
            foreach (string argument in arguments) line.Append(line.Length > 0 ? " " : "").Append(Quote(argument));
            ProcessStartInfo start = new ProcessStartInfo(Exe(), line.ToString())
            {
                WorkingDirectory = folder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            ClaudeToken.Give(start, null);
            using (Process run = Process.Start(start))
            {
                StringBuilder output = new StringBuilder();
                run.OutputDataReceived += delegate(object sender, DataReceivedEventArgs got) { if (got.Data != null) lock (output) output.AppendLine(got.Data); };
                run.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs got) { if (got.Data != null) lock (output) output.AppendLine(got.Data); };
                run.BeginOutputReadLine();
                run.BeginErrorReadLine();
                if (!run.WaitForExit(CliSeconds * 1000)) { try { run.Kill(); } catch (Exception) { } }
                run.WaitForExit(2000);
                lock (output) return output.ToString();
            }
        }

        internal static string SessionOf(string started)
        {
            Match id = Started.Match(started ?? "");
            return id.Success ? id.Groups[1].Value.ToLowerInvariant() : null;
        }

        internal static string State(string agents, string session)
        {
            string text = agents ?? "";
            int open = text.IndexOfAny(new[] { '[', '{' });
            object parsed;
            try { parsed = open < 0 ? null : new JavaScriptSerializer().DeserializeObject(text.Substring(open)); }
            catch (Exception) { return null; }
            object[] list = parsed as object[];
            Dictionary<string, object> wrapped = parsed as Dictionary<string, object>;
            object inner;
            if (list == null && wrapped != null && (wrapped.TryGetValue("sessions", out inner) || wrapped.TryGetValue("agents", out inner))) list = inner as object[];
            if (list == null) return null;
            foreach (object one in list)
            {
                Dictionary<string, object> fields = one as Dictionary<string, object>;
                object id, status, state;
                if (fields == null || !fields.TryGetValue("id", out id) || !(id is string) || !((string)id).StartsWith(session, StringComparison.OrdinalIgnoreCase)) continue;
                fields.TryGetValue("status", out status);
                fields.TryGetValue("state", out state);
                return ((status as string) ?? "") + "/" + ((state as string) ?? "");
            }
            return "gone";
        }

        internal static bool Done(string state)
        {
            string s = state ?? "";
            return s == "gone" || s.EndsWith("/done", StringComparison.Ordinal) || s.EndsWith("/failed", StringComparison.Ordinal) || s.EndsWith("/stopped", StringComparison.Ordinal);
        }

        internal const int IdlePolls = 3;

        internal static bool Working(string agents, string folder)
        {
            string text = agents ?? "";
            int open = text.IndexOf('[');
            object[] list;
            try { list = open < 0 ? null : new JavaScriptSerializer().DeserializeObject(text.Substring(open)) as object[]; }
            catch (Exception) { return false; }
            if (list == null || string.IsNullOrEmpty(folder)) return false;
            string wanted = folder.TrimEnd('\\', '/');
            foreach (object one in list)
            {
                Dictionary<string, object> fields = one as Dictionary<string, object>;
                object cwd, state;
                if (fields == null || !fields.TryGetValue("cwd", out cwd) || !string.Equals(((cwd as string) ?? "").TrimEnd('\\', '/'), wanted, StringComparison.OrdinalIgnoreCase)) continue;
                fields.TryGetValue("state", out state);
                if (string.Equals(state as string, "working", StringComparison.OrdinalIgnoreCase) || string.Equals(state as string, "running", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        internal static bool RoutineWorking(string folder)
        {
            try { return Working(Cli(folder, new[] { "agents", "--json", "--all" }), folder); }
            catch (Exception) { return false; }
        }
        private static readonly Regex NamedMarker = new Regex(@"(?m)^(\W*)\p{Lu}\p{Ll}+\s*[,:—–-]\s+(?=(?:NOT\s+)?[A-Z]{3,}\b)");
        internal static string ProjectsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

        internal static string Answers(string root, string session) { return Answers(root, session, DateTime.MinValue); }

        internal static string Transcript(string root, string session, DateTime since)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(session) || !Directory.Exists(root)) return null;
            foreach (string folder in Directory.GetDirectories(root))
            {
                string[] files = Directory.GetFiles(folder, session + "*.jsonl");
                if (files.Length > 0) return File.GetLastWriteTime(files[0]) < since ? null : files[0];
            }
            return null;
        }

        internal static bool TurnEnded(string[] lines)
        {
            JavaScriptSerializer reader = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                if (line.IndexOf("\"assistant\"", StringComparison.Ordinal) < 0 && line.IndexOf("\"user\"", StringComparison.Ordinal) < 0) continue;
                Dictionary<string, object> entry;
                try { entry = reader.DeserializeObject(line) as Dictionary<string, object>; }
                catch (Exception) { continue; }
                object kind, side, message, stop;
                if (entry == null || !entry.TryGetValue("type", out kind)) continue;
                string type = kind as string;
                if (type != "assistant" && type != "user") continue;
                if (entry.TryGetValue("isSidechain", out side) && side is bool && (bool)side) continue;
                if (type == "user" || !entry.TryGetValue("message", out message)) return false;
                Dictionary<string, object> body = message as Dictionary<string, object>;
                return body != null && body.TryGetValue("stop_reason", out stop) && (stop as string) == "end_turn";
            }
            return false;
        }

        private static string WhyBlocked(string session, DateTime began, string output)
        {
            string[] lines;
            try
            {
                string file = Transcript(ProjectsFolder, session, began);
                lines = file == null ? new string[0] : File.ReadAllLines(file, Encoding.UTF8);
            }
            catch (Exception) { lines = new string[0]; }
            int from = Math.Max(0, lines.Length - TailLines);
            if (ProjectRun.LimitHit(string.Join("\n", lines, from, lines.Length - from))) return PlanLimit;
            if (OpenAsks.SignInAtEnd(output) || OpenAsks.LoginSeen(output)) return SignInPage;
            return TurnEnded(lines) ? null : Stuck;
        }

        internal static string Answers(string root, string session, DateTime since)
        {
            try
            {
                string found = Transcript(root, session, since);
                if (found == null) return null;
                JavaScriptSerializer reader = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                List<string> said = new List<string>();
                foreach (string line in File.ReadAllLines(found, Encoding.UTF8))
                {
                    if (line.IndexOf("\"assistant\"", StringComparison.Ordinal) < 0) continue;
                    Dictionary<string, object> entry = reader.DeserializeObject(line) as Dictionary<string, object>;
                    object kind, message, content;
                    if (entry == null || !entry.TryGetValue("type", out kind) || (kind as string) != "assistant" || !entry.TryGetValue("message", out message)) continue;
                    Dictionary<string, object> body = message as Dictionary<string, object>;
                    if (body == null || !body.TryGetValue("content", out content)) continue;
                    object[] parts = content as object[];
                    if (parts == null) continue;
                    foreach (object part in parts)
                    {
                        Dictionary<string, object> piece = part as Dictionary<string, object>;
                        object type, text;
                        if (piece != null && piece.TryGetValue("type", out type) && (type as string) == "text" && piece.TryGetValue("text", out text) && !string.IsNullOrWhiteSpace(text as string)) said.Add(((string)text).Trim());
                    }
                }
                return said.Count == 0 ? null : string.Join("\n", said.ToArray());
            }
            catch (Exception) { return null; }
        }

        internal static bool Idle(string state)
        {
            string s = state ?? "";
            return s.StartsWith("idle/", StringComparison.Ordinal) || s.EndsWith("/idle", StringComparison.Ordinal);
        }

        internal static string Unnamed(string output) { return NamedMarker.Replace(output ?? "", "$1"); }

        internal static bool Blocked(string state)
        {
            string s = state ?? "";
            return s.StartsWith("blocked/", StringComparison.Ordinal) || s.EndsWith("/blocked", StringComparison.Ordinal);
        }

        internal static ClaudeJobResult Run(string label, string folder, string prompt, string[] tools, bool chrome, int maxMinutes)
        {
            ClaudeJobResult result = new ClaudeJobResult();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { result.Problem = "the folder " + folder + " isn't there"; return result; }
            if (chrome && Lockout.Active) { result.Problem = "a game is running"; return result; }
            if (!Monitor.TryEnter(gate)) { result.Problem = "another Claude Code job is still running"; return result; }
            int mark = JobMarks.Begin(JobMarks.KindOf(label, folder, Study.Folder), DateTime.Now);
            try
            {
                List<string> arguments = new List<string> { "--bg", prompt };
                if (chrome) arguments.Add("--chrome");
                ForkSettings settings = ForkSettings.Current;
                arguments.AddRange(ModelArguments(settings == null ? 0 : settings.StudyModel));
                arguments.Add("--allowedTools");
                arguments.AddRange(tools);
                string started = Cli(folder, arguments.ToArray());
                result.Session = SessionOf(started);
                if (result.Session == null)
                {
                    result.Problem = "the background session didn't start: " + Pursuit.Shorter(Ansi.Replace(started ?? "", ""), 160);
                    ActivityLog.Write("claude job", label, "didn't start");
                    return result;
                }
                ActivityLog.Write("claude job", label, "started session " + result.Session);
                return Follow(label, folder, result, maxMinutes, chrome);
            }
            catch (Exception error)
            {
                result.Problem = error.GetType().Name + ": " + error.Message;
                ActivityLog.Write("claude job", label, "failed: " + result.Problem);
                return result;
            }
            finally
            {
                Monitor.Exit(gate);
                JobMarks.End(mark, result.Ok, DateTime.Now);
                if (result.Session != null) Wait(JobMarks.AnswerAfterSeconds);
            }
        }

        private static ClaudeJobResult Follow(string label, string folder, ClaudeJobResult result, int maxMinutes, bool chrome)
        {
            string session = result.Session;
            DateTime began = DateTime.Now;
            DateTime until = began.AddMinutes(maxMinutes);
            string state = null;
            bool seen = false;
            int missing = 0;
            int idle = 0;
            try
            {
                while (DateTime.Now < until)
                {
                    for (int waited = 0; waited < PollSeconds && !(chrome && Lockout.Active); waited += GameCheckSeconds) Wait(GameCheckSeconds);
                    if (chrome && Lockout.Active) { result.Problem = GameStarted; break; }
                    state = State(Cli(folder, new[] { "agents", "--json", "--all" }), session);
                    if (state == "gone" && !seen)
                    {
                        if (++missing >= MissingPolls) { result.Problem = "I couldn't find its session"; break; }
                        continue;
                    }
                    if (state != null && state != "gone") seen = true;
                    if (Done(state)) break;
                    if (Blocked(state)) { result.Problem = Stuck; break; }
                    idle = Idle(state) ? idle + 1 : 0;
                    if (idle >= IdlePolls) break;
                }
                if (result.Problem == null && !Done(state) && idle < IdlePolls) result.Problem = "it didn't finish in " + maxMinutes + " minutes";
                string raw = Ansi.Replace(Cli(folder, new[] { "logs", session }) ?? "", "");
                string own = Answers(ProjectsFolder, session, began);
                string logs = Unnamed(own ?? raw);
                result.Output = logs.Length > OutputChars ? logs.Substring(logs.Length - OutputChars) : logs;
                if (result.Problem == Stuck) result.Problem = WhyBlocked(session, began, result.Output);
                result.Ok = result.Problem == null;
            }
            finally
            {
                try { Cli(folder, new[] { "stop", session }); } catch (Exception) { }
                try { Cli(folder, new[] { "rm", session }); } catch (Exception) { }
                ActivityLog.Write("claude job", label, (result.Ok ? "done: " : "ended: " + result.Problem + "; ") + Pursuit.Shorter(result.Last, 160));
            }
            return result;
        }
    }
}
