using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal static class StudyRoutines
    {
        internal const string Briefing = "daily-briefing";
        internal const string Drafts = "process-drafts";
        internal const int BriefingHour = 8;
        internal const int FirstHour = 8;
        internal const int LastHour = 22;
        internal const int DraftsHours = 2;
        internal const int JobMinutes = 40;
        internal const int TaskCheckMinutes = 30;
        internal static readonly string[] Tools = { "Read", "Glob", "Grep", "Edit", "Write", "ToolSearch", "Artifact", "ArtifactData", "mcp__claude-in-chrome", "mcp__claude_ai_Gmail__send_message" };
        internal static string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "study-routines.txt");
        internal static Func<string, bool> WindowsTaskOn = TaskOn;
        private static readonly Dictionary<string, KeyValuePair<DateTime, bool>> taskSeen = new Dictionary<string, KeyValuePair<DateTime, bool>>();
        private static readonly object gate = new object();
        private static readonly List<string> checking = new List<string>();

        internal static readonly Regex BriefingAsked = new Regex(@"\b(?:run|do|start|make|redo)\s+(?:my\s+|the\s+|a\s+)?(?:new\s+)?(?:study|school)\s+briefing\b|\b(?:study|school)\s+briefing\s+now\b", RegexOptions.IgnoreCase);
        internal static readonly Regex DraftsAsked = new Regex(@"\b(?:process|put|move|push)\s+(?:my\s+|the\s+)?(?:approved\s+)?(?:school\s+)?drafts?\b(?:\s+\w+){0,3}?\s+(?:in(?:to)?\s+)?(?:outlook|now)\b|\brun\s+(?:my\s+|the\s+)?drafts?\s+(?:routine|run|job)\b", RegexOptions.IgnoreCase);

        internal static string Asked(string text)
        {
            string said = text ?? "";
            if (BriefingAsked.IsMatch(said)) return Briefing;
            if (DraftsAsked.IsMatch(said)) return Drafts;
            return null;
        }

        internal static string TaskName(string routine) { return routine == Briefing ? "Study Briefing" : "Study Drafts"; }

        internal static string Label(string routine) { return routine == Briefing ? "study briefing" : "drafts run"; }

        private static bool TaskOn(string name)
        {
            KeyValuePair<DateTime, bool> seen;
            bool known;
            lock (gate)
            {
                known = taskSeen.TryGetValue(name, out seen);
                if (known && (DateTime.Now - seen.Key).TotalMinutes < TaskCheckMinutes) return seen.Value;
                if (checking.Contains(name)) return !known || seen.Value;
                checking.Add(name);
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool on = true;
                try { on = QueryTask(name); }
                catch (Exception error) { Brain.Log("study routines", -1, error.Message); }
                lock (gate)
                {
                    taskSeen[name] = new KeyValuePair<DateTime, bool>(DateTime.Now, on);
                    checking.Remove(name);
                }
            });
            return !known || seen.Value;
        }

        private static readonly Dictionary<string, KeyValuePair<DateTime, string>> xmlSeen = new Dictionary<string, KeyValuePair<DateTime, string>>();
        private static readonly List<string> reading = new List<string>();

        internal static string TaskXml(string name)
        {
            KeyValuePair<DateTime, string> seen;
            bool known;
            lock (gate)
            {
                known = xmlSeen.TryGetValue(name, out seen);
                if ((known && (DateTime.Now - seen.Key).TotalMinutes < TaskCheckMinutes) || reading.Contains(name)) return known ? seen.Value : null;
                reading.Add(name);
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                string xml = "";
                try { xml = ReadTask(name); }
                catch (Exception error) { Brain.Log("study routines", -1, error.Message); }
                lock (gate)
                {
                    xmlSeen[name] = new KeyValuePair<DateTime, string>(DateTime.Now, xml);
                    reading.Remove(name);
                }
            });
            return known ? seen.Value : null;
        }

        private static string ReadTask(string name)
        {
            ProcessStartInfo start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), "/query /tn \"" + name + "\" /xml")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process run = Process.Start(start))
            {
                string output = run.StandardOutput.ReadToEnd();
                run.WaitForExit(10000);
                return run.ExitCode == 0 ? output : "";
            }
        }

        private static bool QueryTask(string name)
        {
            ProcessStartInfo start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), "/query /tn \"" + name + "\" /xml")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process run = Process.Start(start))
            {
                string output = run.StandardOutput.ReadToEnd();
                run.WaitForExit(10000);
                return run.ExitCode == 0 && TaskEnabled(output);
            }
        }

        internal static bool TaskEnabled(string xml)
        {
            string text = xml ?? "";
            if (text.IndexOf("<Task", StringComparison.Ordinal) < 0) return false;
            Match settings = Regex.Match(text, @"<Settings>.*?</Settings>", RegexOptions.Singleline);
            return !Regex.IsMatch(settings.Success ? settings.Value : text, @"<Enabled>\s*false\s*</Enabled>", RegexOptions.IgnoreCase);
        }

        internal static Dictionary<string, DateTime> Load()
        {
            Dictionary<string, DateTime> last = new Dictionary<string, DateTime>();
            try
            {
                if (!File.Exists(FilePath)) return last;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int cut = line.IndexOf('=');
                    DateTime when;
                    if (cut > 0 && DateTime.TryParseExact(line.Substring(cut + 1).Trim(), "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out when)) last[line.Substring(0, cut).Trim()] = when;
                }
            }
            catch (Exception error) { Brain.Log("study routines", -1, error.Message); }
            return last;
        }

        internal static void Mark(string routine, DateTime when)
        {
            lock (gate)
            {
                try
                {
                    Dictionary<string, DateTime> last = Load();
                    last[routine] = when;
                    List<string> lines = new List<string>();
                    foreach (KeyValuePair<string, DateTime> pair in last) lines.Add(pair.Key + "=" + pair.Value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    File.WriteAllLines(FilePath, lines.ToArray());
                }
                catch (Exception error) { Brain.Log("study routines", -1, error.Message); }
            }
        }

        internal static bool Due(string routine, DateTime now, Dictionary<string, DateTime> last)
        {
            DateTime done;
            if (last == null || !last.TryGetValue(routine, out done)) done = DateTime.MinValue;
            if (now.Hour < FirstHour || now.Hour > LastHour) return false;
            if (routine == Briefing) return now.Hour >= BriefingHour && done.Date < now.Date;
            return (now - done).TotalHours >= DraftsHours;
        }

        internal static string Prompt(string routine, DateTime now)
        {
            string prompt = "Current local time: " + now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture) + " (use this as 'now' and 'today'). Read routine/" + routine + ".md in the current folder and carry it out exactly.";
            string page = StudyDrafts.PageLink();
            if (page != null) prompt += " When you are done with it, also: " + StudyDrafts.RefreshSteps(page);
            return prompt;
        }

        internal static bool SignIn(ClaudeJobResult result)
        {
            return result != null && Regex.IsMatch(result.Output ?? "", @"\b(?:log[ -]?in|sign[ -]?in|signed out|MFA)\b[^.\n]{0,60}\b(?:need|needed|needs|page|again|required|asks?)\b|\b(?:need|needs)\s+(?:you\s+)?to\s+(?:log|sign)\s+in\b", RegexOptions.IgnoreCase);
        }

        internal static string Said(string routine, ClaudeJobResult result, StudyBook book)
        {
            if (result == null) return "I couldn't start your " + Label(routine) + ".";
            if (result.Session == null) return "I couldn't start your " + Label(routine) + ": " + result.Problem + ".";
            if (SignIn(result)) return "Your " + Label(routine) + " ran, but Outlook or one of your school sites wants you to sign in once in Chrome.";
            if (!result.Ok) return "Your " + Label(routine) + " stopped: " + result.Problem + ".";
            if (routine == Briefing)
            {
                string points = book == null ? "" : Study.Briefing(book);
                return points.Length > 0 ? "Your study briefing is in. To do: " + points : "Your study briefing is in, nothing to do today.";
            }
            return "Drafts run done: " + Pursuit.Shorter(result.Last, StudyDrafts.SaidChars);
        }
    }
}
