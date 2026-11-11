using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed class BrowserFinding
    {
        internal DateTime At;
        internal string Task;
        internal string Result;
    }

    internal static class BrowserRecall
    {
        internal const int KeepHours = 36;
        internal const int MostKept = 5;
        internal const int SharedWords = 2;
        internal const int ShortestFinding = 40;
        internal static string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "browser-recall.txt");
        private static readonly object gate = new object();
        private static string heard;

        internal static string Heard
        {
            get { return Volatile.Read(ref heard); }
            set { Interlocked.Exchange(ref heard, value); }
        }

        private static readonly Regex DidFind = new Regex(@"\b(did|have) (you|u|ya) (find|get|see|pull up|dig up|look up)\b");
        private static readonly Regex ShowAgain = new Regex(@"\b(show|tell|read|give|send|list|repeat|remind)\b.*\b(again|earlier|from before|last time)\b");
        private static readonly Regex Fresh = new Regex(@"\b(more|new|newer|other|others|another|different|fresh|redo|refresh|latest|update|updated|cheaper)\b|\b(search|look|check|find|try)( \w+){0,3} again\b");
        private static readonly HashSet<string> Got = new HashSet<string>(new[] { "got", "had", "pulled", "picked", "showed", "listed", "gave", "dug", "saw", "looked" });
        private static readonly HashSet<string> You = new HashSet<string>(new[] { "you", "u", "ya", "youve", "you've" });
        private static readonly HashSet<string> Filler = new HashSet<string>(new[] { "already", "just", "have", "ve", "had", "earlier" });
        private static readonly HashSet<string> Plain = new HashSet<string>(new[] {
            "the", "and", "for", "that", "this", "these", "those", "with", "you", "your", "show", "find", "found", "search", "searched", "look", "looked",
            "option", "result", "one", "listing", "link", "pick", "cheap", "best", "good", "what", "were", "was", "again", "earlier", "some", "read",
            "list", "give", "tell", "from", "into", "have", "had", "did", "got", "can", "could", "please", "browser", "open", "them", "they", "their",
            "fit", "fits", "will", "would", "about", "any", "all", "on", "in", "of", "to", "me", "my", "it", "is", "are", "be", "an", "or", "if",
            "research", "several", "affordable", "name", "price", "rating", "product", "note", "where", "which", "there", "here", "now", "then",
            "just", "already", "before", "last", "time", "want", "need", "like", "get", "see", "send", "repeat", "remind", "say", "said" });

        internal static string Take()
        {
            return Interlocked.Exchange(ref heard, null);
        }

        internal static void Keep(string task, string result, string outcome, string summed, DateTime now)
        {
            string said = (result ?? "").Trim();
            if (outcome != "done" || said.Length < ShortestFinding || string.IsNullOrWhiteSpace(task)) return;
            string steps = Regex.Replace(Regex.Replace(summed ?? "", @"\s+", " ").Trim(), @"^Done\.\s*", "").TrimEnd('.');
            string rest = Regex.Replace(Regex.Replace(said, @"\s+", " "), @"^Done\.\s*", "");
            if (steps.Length > 0 && rest.StartsWith(steps, StringComparison.OrdinalIgnoreCase)) rest = rest.Substring(steps.Length);
            if (rest.Trim(' ', '.', ',').Length < ShortestFinding) return;
            lock (gate)
            {
                List<BrowserFinding> kept = Live(now);
                kept.Add(new BrowserFinding { At = now, Task = task.Trim(), Result = said });
                while (kept.Count > MostKept) kept.RemoveAt(0);
                Write(kept);
            }
        }

        internal static string Earlier(string line, string task, DateTime now)
        {
            string his = Words(line);
            string asked = his.Length > 0 ? his : Words(task);
            if (!Recalls(asked) || Fresh.IsMatch(asked)) return null;
            HashSet<string> topic = Topic(his + " " + Words(task));
            BrowserFinding best = null;
            lock (gate)
            {
                List<BrowserFinding> kept = Live(now);
                for (int i = kept.Count - 1; i >= 0 && best == null; i--)
                {
                    int shared = 0;
                    foreach (string word in Topic(Words(kept[i].Task))) if (topic.Contains(word)) shared++;
                    if (shared >= SharedWords) best = kept[i];
                }
            }
            if (best == null) return null;
            string when = best.At.Date == now.Date ? "at " : best.At.Date == now.Date.AddDays(-1) ? "yesterday at " : best.At.ToString("d MMM", CultureInfo.InvariantCulture) + " at ";
            ActivityLog.Write("browser", "recall", Pursuit.Shorter(best.Task, 90) + " (" + best.At.ToString("HH:mm", CultureInfo.InvariantCulture) + ")");
            return "From my search " + when + best.At.ToString("HH:mm", CultureInfo.InvariantCulture) + ": " + best.Result;
        }

        internal static bool Recalls(string words)
        {
            if (string.IsNullOrEmpty(words)) return false;
            if (DidFind.IsMatch(words) || ShowAgain.IsMatch(words)) return true;
            string[] tokens = words.Split(' ');
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!You.Contains(tokens[i])) continue;
                for (int j = i + 1; j < tokens.Length && j <= i + 3; j++)
                {
                    if (Near(tokens[j], "found") || Got.Contains(tokens[j])) return true;
                    if (!Filler.Contains(tokens[j])) break;
                }
            }
            return false;
        }

        private static string Words(string text)
        {
            return Regex.Replace((text ?? "").ToLowerInvariant().Replace("'", ""), @"[^\p{L}\p{N}]+", " ").Trim();
        }

        private static HashSet<string> Topic(string words)
        {
            HashSet<string> topic = new HashSet<string>();
            foreach (string token in words.Split(' '))
            {
                string word = token.Length > 3 && token.EndsWith("s", StringComparison.Ordinal) ? token.Substring(0, token.Length - 1) : token;
                if (word.Length < 3 || Plain.Contains(word) || Plain.Contains(token)) continue;
                topic.Add(word);
            }
            return topic;
        }

        private static bool Near(string a, string b)
        {
            if (a == b) return true;
            if (a.Length < 4 || Math.Abs(a.Length - b.Length) > 1) return false;
            int i = 0, j = 0, edits = 0;
            while (i < a.Length && j < b.Length)
            {
                if (a[i] == b[j]) { i++; j++; continue; }
                if (++edits > 1) return false;
                if (a.Length > b.Length) i++;
                else if (a.Length < b.Length) j++;
                else { i++; j++; }
            }
            return edits + (a.Length - i) + (b.Length - j) <= 1;
        }

        private static List<BrowserFinding> Live(DateTime now)
        {
            return Read().FindAll(delegate(BrowserFinding found) { return (now - found.At).TotalHours < KeepHours && found.At <= now.AddMinutes(1); });
        }

        private static List<BrowserFinding> Read()
        {
            List<BrowserFinding> kept = new List<BrowserFinding>();
            try
            {
                if (!File.Exists(FilePath)) return kept;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] parts = line.Split(new[] { '|' }, 3);
                    DateTime at;
                    if (parts.Length < 3 || !DateTime.TryParseExact(parts[0], "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) continue;
                    kept.Add(new BrowserFinding { At = at, Task = parts[1], Result = parts[2] });
                }
            }
            catch (IOException error) { Brain.Log("browser recall", -1, error.Message); }
            catch (UnauthorizedAccessException error) { Brain.Log("browser recall", -1, error.Message); }
            return kept;
        }

        private static void Write(List<BrowserFinding> kept)
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (BrowserFinding found in kept) lines.Add(found.At.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) + "|" + Regex.Replace(found.Task ?? "", @"[\r\n|]+", " ") + "|" + Regex.Replace(found.Result ?? "", @"[\r\n]+", " "));
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (IOException error) { Brain.Log("browser recall", -1, error.Message); }
            catch (UnauthorizedAccessException error) { Brain.Log("browser recall", -1, error.Message); }
        }
    }
}
