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
    internal sealed class IdeasRunState
    {
        [DataMember] public string Day = "";
        [DataMember] public long NextTry;
    }

    internal sealed class Idea
    {
        internal string Mark;
        internal string Text;
        internal string Note;
    }

    internal static class Ideas
    {
        internal const string Tool = "idea";
        internal const string FileName = "ideas.md";
        internal const string Open = " ", AsksFirst = "?", HisYes = "!", Started = "/";
        internal const int RetryMinutes = 60, MostListed = 20, LongestIdea = 300;
        internal const string Head = "# Her ideas\n> Ideas for improving herself. Once a day one goes to a Claude Code session that builds it on its own branch; nothing lands without his yes. [?] waits for his yes first, [!] has it, [/] was started.\n\n";
        internal const string NoVault = "I keep my ideas in my vault, and it isn't set up yet.";
        internal const string NotHis = "I build an idea I asked about only on your own yes, choom: say build it.";
        internal const string Spec = "Keep your list of ideas for improving yourself (Capri/ideas.md in your vault). Use action add when the user gives you an idea for yourself or a feature they want you to have, or when you think of one yourself (a fix for something you got wrong, something that would help them); text is the idea in one line. Use action list when they ask about your ideas, drop to remove one (text names it), and approve only when they themselves say yes to building an idea you said you'd ask about first. Once a day, when your project sessions are on and no game runs, the first open idea goes to one of their Claude Code sessions that builds it on its own branch; nothing lands without their yes. Never say you saved, dropped or started an idea without this tool.";
        internal const string Schema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"add, list, drop, or approve (only on their own yes)\",\"enum\":[\"add\",\"list\",\"drop\",\"approve\"]},\"text\":{\"type\":\"string\",\"description\":\"The idea in one line, or words naming the one to drop or approve\"}},\"required\":[\"action\"]}";
        internal const string Task = "Start the way this project's CLAUDE.md says. Build it our way on its own branch in its own new worktree off main (never work in the main checkout), smoke checks first, build until every line says PASS. Never merge into main or push main: every landing waits for his yes. If it turns out to touch the in-game lockout, the updater, keys or secrets, spend caps, buying, SECURITY, the never-look list or her project sessions' own rules, stop and say so instead of building it. No personal data in tracked files. End with what you built, on which branch, and how he can check it.";
        private static readonly Regex Line = new Regex(@"^\s*-\s*\[(.)\]\s*(.+?)\s*(?:\(([^()]*)\))?\s*$");
        private static readonly Regex Touchy = new Regex(@"\b(lockout|lock\s*out|updater|updates?|api\s*keys?|keys?|secrets?|tokens?|passwords?|spend\w*|caps?|budgets?|buy\w*|purchases?|orders?|pay\w*|security|blackwall|never[\s-]*look|blocklist|landing|project sessions|run my projects)\b", RegexOptions.IgnoreCase);
        internal static string StatePath;
        internal static Func<ProjectEntry, string, DateTime, string> Begin;
        private static readonly object gate = new object();

        internal static string Do(ToolArguments args)
        {
            return Run(args == null ? null : args.Action, args == null ? null : args.Text, args == null ? null : args.Asked, DateTime.Now);
        }

        internal static string Run(string action, string text, string asked, DateTime now)
        {
            string path = FilePath();
            if (path == null) return NoVault;
            lock (gate)
            {
                List<Idea> all = Read(path);
                if (action == "list") return Listed(all);
                if (!string.IsNullOrWhiteSpace(asked) && CallFriends.Friends(asked, now)) return "Ideas for me come from you, not from a friend in the call. Nothing changed.";
                string plain = Regex.Replace((text ?? "").Replace("\r", " ").Replace("\n", " "), @"\s+", " ").Trim();
                if (plain.Length == 0) return "Which idea? Nothing changed.";
                if (action == "add")
                {
                    if (plain.Length > LongestIdea) plain = plain.Substring(0, LongestIdea).Trim();
                    plain = plain.Replace("(", "[").Replace(")", "]");
                    if (Matching(all, plain) != null) return "That idea is already on my list.";
                    all.Add(new Idea { Mark = Open, Text = plain, Note = "added " + Day(now) });
                    Write(path, all);
                    ActivityLog.Write("ideas", "added", plain);
                    return "Saved to my ideas: " + plain + ".";
                }
                Idea found = Matching(all, plain);
                if (found == null) return "None of my ideas matches " + Voice.Quote(plain) + ". Nothing changed.";
                if (action == "drop")
                {
                    all.Remove(found);
                    Write(path, all);
                    ActivityLog.Write("ideas", "dropped", found.Text);
                    return "Dropped from my ideas: " + found.Text + ".";
                }
                if (action == "approve")
                {
                    if (string.IsNullOrWhiteSpace(asked)) return NotHis;
                    if (found.Mark == Started) return "I already started that one.";
                    found.Mark = HisYes;
                    found.Note = "your yes " + Day(now);
                    Write(path, all);
                    ActivityLog.Write("ideas", "his yes", found.Text);
                    return "Got it: " + found.Text + " goes to my next project session.";
                }
                return "Say add, list, drop or approve. Nothing changed.";
            }
        }

        internal static string Watch(bool on, string[] lines, int perDay, DateTime now)
        {
            if (!on || ProjectRun.Gaming()) return null;
            string path = FilePath();
            if (path == null) return null;
            lock (gate)
            {
                IdeasRunState state = Load();
                string today = Day(now);
                if (state.Day == today || now.Ticks < state.NextTry) return null;
                state.NextTry = now.AddMinutes(RetryMinutes).Ticks;
                List<Idea> all = Read(path);
                List<string> asking = new List<string>();
                Idea next = all.Find(delegate(Idea one) { return one.Mark == HisYes; });
                foreach (Idea one in all)
                {
                    if (one.Mark != Open) continue;
                    if (Touchy.IsMatch(one.Text)) { one.Mark = AsksFirst; one.Note = "asks first"; asking.Add(one.Text); continue; }
                    if (next == null) next = one;
                }
                if (asking.Count > 0) Write(path, all);
                string told = asking.Count == 0 ? null : "Before I build " + (asking.Count == 1 ? "this idea" : "these ideas") + " I need your yes, because " + (asking.Count == 1 ? "it touches" : "they touch") + " something you decide: " + string.Join("; ", asking.ToArray()) + ". Say build it if you want it.";
                if (next == null) { state.Day = today; Save(state); return told; }
                ProjectEntry entry = UpstreamWatch.Ours(lines);
                if (entry == null || !entry.Full)
                {
                    state.Day = today;
                    Save(state);
                    ActivityLog.Write("ideas", "waiting", "no | full capri project listed to build in");
                    return told;
                }
                string task = "Her idea for herself, from her ideas list (Capri/ideas.md in her vault): '" + next.Text.Replace("\"", "'") + "'. " + Task;
                string refused = Begin != null ? Begin(entry, task, now) : StartIt(on, lines, perDay, entry, task, now);
                if (refused != null)
                {
                    Save(state);
                    ActivityLog.Write("ideas", entry.Name, "waiting to start: " + refused);
                    return told;
                }
                next.Mark = Started;
                next.Note = "started " + today;
                Write(path, all);
                state.Day = today;
                Save(state);
                ActivityLog.Write("ideas", entry.Name, "started a session for: " + next.Text);
                string started = "I started a session in " + entry.Name + " to build one of my ideas: " + next.Text + " (nothing lands without your yes).";
                return told == null ? started : told + " " + started;
            }
        }

        internal static List<Idea> Read(string path)
        {
            List<Idea> all = new List<Idea>();
            if (!File.Exists(path)) return all;
            foreach (string raw in File.ReadAllLines(path))
            {
                Match found = Line.Match(raw);
                if (!found.Success) continue;
                all.Add(new Idea { Mark = found.Groups[1].Value, Text = found.Groups[2].Value.Trim(), Note = found.Groups[3].Success ? found.Groups[3].Value.Trim() : "" });
            }
            return all;
        }

        private static void Write(string path, List<Idea> all)
        {
            StringBuilder text = new StringBuilder(Head);
            foreach (Idea one in all) text.Append("- [").Append(one.Mark).Append("] ").Append(one.Text).Append(one.Note.Length > 0 ? " (" + one.Note + ")" : "").Append('\n');
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }

        private static string Listed(List<Idea> all)
        {
            if (all.Count == 0) return "My ideas list is empty.";
            StringBuilder text = new StringBuilder("My ideas:");
            for (int i = 0; i < all.Count && i < MostListed; i++)
            {
                Idea one = all[i];
                string how = one.Mark == Started ? "started" : one.Mark == AsksFirst ? "waits for your yes" : one.Mark == HisYes ? "next, with your yes" : one.Mark == Open ? "open" : "done";
                text.Append("\n- ").Append(one.Text).Append(" (").Append(how).Append(")");
            }
            return text.ToString();
        }

        private static Idea Matching(List<Idea> all, string words)
        {
            string wanted = words.ToLowerInvariant();
            foreach (Idea one in all) if (one.Text.ToLowerInvariant() == wanted) return one;
            Idea best = null;
            int top = 0;
            string[] asked = Regex.Split(wanted, @"\W+");
            foreach (Idea one in all)
            {
                string has = " " + Regex.Replace(one.Text.ToLowerInvariant(), @"\W+", " ") + " ";
                int score = 0;
                foreach (string word in asked) if (word.Length > 3 && has.Contains(" " + word + " ")) score++;
                if (score > top) { top = score; best = one; }
            }
            return top >= 2 || (top == 1 && asked.Length <= 2) ? best : null;
        }

        private static string StartIt(bool on, string[] lines, int perDay, ProjectEntry entry, string task, DateTime now)
        {
            string name;
            return ProjectRun.Start(on, lines, perDay, entry.Folder, task, now, out name);
        }

        private static string Day(DateTime now) { return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        private static string FilePath()
        {
            string root = Vault.Root;
            return string.IsNullOrEmpty(root) ? null : Path.Combine(Path.Combine(root, "Capri"), FileName);
        }

        private static string RunPath()
        {
            if (StatePath != null) return StatePath;
            return ForkSettings.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ForkSettings.FilePath), "ideas-run.json");
        }

        private static IdeasRunState Load()
        {
            string path = RunPath();
            if (path == null || !File.Exists(path)) return new IdeasRunState();
            try
            {
                using (FileStream stream = File.OpenRead(path))
                    return (IdeasRunState)new DataContractJsonSerializer(typeof(IdeasRunState)).ReadObject(stream) ?? new IdeasRunState();
            }
            catch (Exception error)
            {
                ActivityLog.Write("ideas", "state file", "unreadable, starting fresh: " + error.Message);
                return new IdeasRunState();
            }
        }

        private static void Save(IdeasRunState state)
        {
            string path = RunPath();
            if (path == null) return;
            try
            {
                using (FileStream stream = File.Create(path)) new DataContractJsonSerializer(typeof(IdeasRunState)).WriteObject(stream, state);
            }
            catch (IOException error) { ActivityLog.Write("ideas", "state file", "not saved: " + error.Message); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("ideas", "state file", "not saved: " + error.Message); }
        }
    }
}
