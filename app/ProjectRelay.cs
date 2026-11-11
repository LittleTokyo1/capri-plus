using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class RelayVerdict
    {
        internal bool Ask;
        internal string Text = "";
        internal string[] Answers = new string[0];
        internal string Source = "";
    }

    internal static class ProjectRelay
    {
        internal const int JudgeSeconds = 180, QuestionTail = 400;
        internal const string AnswerLead = "Answer from Capri for the user (source: ";
        private static readonly Regex AlwaysAsk = new Regex(@"\b(?:lock\s?out|updater|api\s+keys?|secrets?|tokens?|passwords?|credentials?|spend(?:ing)?|budget|caps?|buy(?:ing)?|purchases?|orders?|pay(?:ment)?s?|security|blackwall|never[- ]look|merg(?:e|es|ed|ing)|land(?:s|ed|ing)?|deploy(?:s|ed|ing|ment)?|releases?|publish(?:ed|ing)?|force[- ]push|main\s+branch|into\s+main|servers?|production|delete\s+(?:the\s+)?(?:branch|repo|history))\b", RegexOptions.IgnoreCase);
        internal static Func<string, string, string> Judge = RunJudge;
        internal const int JudgeMemoryMinutes = 60;
        private static readonly Dictionary<string, DateTime> judges = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        internal static void NoteJudge(string session, DateTime now)
        {
            lock (judges)
            {
                List<string> old = new List<string>();
                foreach (KeyValuePair<string, DateTime> one in judges) if ((now - one.Value).TotalMinutes > JudgeMemoryMinutes) old.Add(one.Key);
                foreach (string gone in old) judges.Remove(gone);
                judges[session] = now;
            }
        }

        internal static bool Judged(string session)
        {
            lock (judges) return judges.ContainsKey(session ?? "");
        }

        internal static bool Needs(ProjectTurn turn)
        {
            if (turn == null) return false;
            if (turn.Kind == ProjectHook.Ask || turn.Kind == ProjectHook.Plan) return true;
            string said = (turn.Text ?? "").TrimEnd();
            return said.Substring(Math.Max(0, said.Length - QuestionTail)).IndexOf('?') >= 0;
        }

        internal static bool Risky(string text)
        {
            return AlwaysAsk.IsMatch(text ?? "");
        }

        internal static string Asked(ProjectTurn turn)
        {
            if (turn.Kind == ProjectHook.Ask)
            {
                List<string> parts = new List<string>();
                for (int i = 0; i < turn.Questions.Length; i++) parts.Add(turn.Questions[i] + (i < turn.Options.Length && turn.Options[i].Length > 0 ? " (" + turn.Options[i] + ")" : ""));
                return string.Join(" ", parts.ToArray());
            }
            if (turn.Kind == ProjectHook.Plan) return turn.Text ?? "";
            string said = (turn.Text ?? "").TrimEnd();
            string tail = said.Substring(Math.Max(0, said.Length - QuestionTail));
            MatchCollection questions = Regex.Matches(tail, @"[^.!?\n]*\?");
            return questions.Count == 0 ? tail : questions[questions.Count - 1].Value.Trim();
        }

        internal static string Prompt(ProjectTurn turn)
        {
            StringBuilder prompt = new StringBuilder();
            prompt.AppendLine("You help Capri, the user's desktop companion, answer a Claude Code session that works in this folder. Only read; never change anything.");
            if (turn.Kind == ProjectHook.Ask)
            {
                prompt.AppendLine("The session asked the user these questions (options in brackets):");
                for (int i = 0; i < turn.Questions.Length; i++) prompt.AppendLine((i + 1) + ". " + turn.Questions[i] + (i < turn.Options.Length ? " [" + turn.Options[i] + "]" : ""));
            }
            else
            {
                prompt.AppendLine("The session ended its turn with this message to the user:");
                prompt.AppendLine(turn.Text ?? "");
            }
            if (!string.IsNullOrEmpty(turn.Transcript)) prompt.AppendLine("Its transcript, for what the question refers to (read only its last part): " + turn.Transcript);
            prompt.AppendLine("Decide only from this project's CLAUDE.md, its docs and notes (checkpoint, task notes) and the user's earlier picks written in them.");
            prompt.AppendLine("If they clearly answer it and it is routine (no taste, money, risk, safety or new direction), reply in exactly this form:");
            prompt.AppendLine("ANSWER: <the answer" + (turn.Kind == ProjectHook.Ask ? "; for each question the exact option label, in order, separated by ' || '; several labels for one question joined by ', '" : "") + ">");
            prompt.AppendLine("SOURCE: <the file that says so>");
            prompt.AppendLine("Otherwise reply exactly:");
            prompt.AppendLine("ASK_HIM: <the question for the user in one short line>");
            prompt.AppendLine("Anything about the game lockout, the updater, keys or secrets, spending, buying, SECURITY settings, the never-look list, landing or merging into main, deploying, releases or a server is always ASK_HIM.");
            return prompt.ToString();
        }

        internal static RelayVerdict Read(string output, ProjectTurn turn)
        {
            RelayVerdict ask = new RelayVerdict { Ask = true, Text = Short(Asked(turn)) };
            string answer = null, source = null;
            foreach (string raw in (output ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim().Trim('*', '`').Trim();
                if (line.StartsWith("ASK_HIM:", StringComparison.Ordinal)) { string asked = line.Substring(8).Trim(); if (asked.Length > 0) ask.Text = Short(asked); return ask; }
                if (line.StartsWith("ANSWER:", StringComparison.Ordinal)) answer = line.Substring(7).Trim();
                else if (line.StartsWith("SOURCE:", StringComparison.Ordinal)) source = line.Substring(7).Trim();
            }
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(source) || Risky(answer)) return ask;
            if (turn.Kind != ProjectHook.Ask) return new RelayVerdict { Text = AnswerLead + source + "): " + answer, Source = source };
            string[] picked = answer.Split(new[] { "||" }, StringSplitOptions.None);
            if (picked.Length != turn.Questions.Length) return ask;
            string[] answers = new string[picked.Length];
            for (int i = 0; i < picked.Length; i++)
            {
                answers[i] = Labels(picked[i], i < turn.Options.Length ? turn.Options[i] : "");
                if (answers[i] == null) return ask;
            }
            return new RelayVerdict { Answers = answers, Source = source };
        }

        internal static RelayVerdict Decide(string folder, ProjectTurn turn)
        {
            if (turn.Kind == ProjectHook.Plan || Risky(Asked(turn))) return new RelayVerdict { Ask = true, Text = Short(Asked(turn)) };
            return Read(Judge(folder, Prompt(turn)), turn);
        }

        internal static string Handle(HeardTurn one)
        {
            ProjectTurn turn = one.Turn;
            string project = Path.GetFileName((one.Folder ?? "").TrimEnd('\\', '/'));
            RelayVerdict verdict = null;
            string question = Asked(turn);
            try
            {
                if (Needs(turn)) verdict = ProjectRun.AutoAllowed(turn.Session, question) ? Decide(one.Folder, turn) : new RelayVerdict { Ask = true, Text = Short(question) };
            }
            catch (Exception error)
            {
                ActivityLog.Write("project session", project, "relay failed: " + error.GetType().Name + ": " + error.Message);
                verdict = new RelayVerdict { Ask = true, Text = Short(Asked(turn)) };
            }
            finally
            {
                bool answered = verdict != null && !verdict.Ask;
                if (turn.Kind != ProjectHook.Plan && ProjectHook.Current(turn.Session, turn.At)) ProjectHook.Reply(turn.Session, turn.At, answered ? verdict.Text : "", answered ? verdict.Answers : null);
            }
            if (verdict == null) return null;
            if (!verdict.Ask)
            {
                ProjectRun.AutoAnswered(turn.Session, question);
                ActivityLog.Write("project session", project, "answered from " + verdict.Source + ": " + (verdict.Answers.Length > 0 ? string.Join(" | ", verdict.Answers) : verdict.Text));
                return null;
            }
            if (turn.Kind != ProjectHook.Plan) ProjectRun.NoteAsked(turn.Session, turn.At);
            string line = ToHim(project, turn, verdict);
            ActivityLog.Write("project session", project, "for him: " + line);
            return line;
        }

        internal static string ToHim(string project, ProjectTurn turn, RelayVerdict verdict)
        {
            if (turn.Kind == ProjectHook.Plan) return "Your " + project + " session has a plan ready and waits for your yes: " + Short(HuskWatch.FirstLine(turn.Text, 160)) + " Approve it in its window or on your phone.";
            return "Your " + project + " session asks: " + verdict.Text + " Answer it in its window or on your phone.";
        }

        private static string Labels(string picked, string options)
        {
            string[] known = options.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries);
            List<string> chosen = new List<string>();
            foreach (string part in picked.Split(','))
            {
                string wanted = part.Trim().Trim('"', '\'').Trim();
                string match = null;
                foreach (string label in known) if (label.Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase)) match = label.Trim();
                if (match == null) return null;
                chosen.Add(match);
            }
            return chosen.Count == 0 ? null : string.Join(", ", chosen.ToArray());
        }

        private static string Short(string text)
        {
            return Pursuit.Shorter(Regex.Replace(text ?? "", @"\s+", " ").Trim(), 220);
        }

        internal static string[] JudgeArguments(string prompt, string session)
        {
            return new[] { "-p", prompt, "--model", "sonnet", "--session-id", session, "--permission-mode", "dontAsk", "--strict-mcp-config", "--tools", "Read,Grep,Glob", "--allowedTools", "Read", "Grep", "Glob", "--output-format", "text" };
        }

        private static string RunJudge(string folder, string prompt)
        {
            string session = Guid.NewGuid().ToString();
            NoteJudge(session, DateTime.Now);
            string[] arguments = JudgeArguments(prompt, session);
            StringBuilder line = new StringBuilder();
            foreach (string argument in arguments) line.Append(line.Length > 0 ? " " : "").Append(ClaudeJob.Quote(argument));
            ProcessStartInfo start = new ProcessStartInfo(ClaudeJob.Exe(), line.ToString())
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
                run.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs got) { };
                run.BeginOutputReadLine();
                run.BeginErrorReadLine();
                if (!run.WaitForExit(JudgeSeconds * 1000)) { try { run.Kill(); } catch (Exception) { } }
                run.WaitForExit(2000);
                lock (output) return output.ToString();
            }
        }
    }
}
