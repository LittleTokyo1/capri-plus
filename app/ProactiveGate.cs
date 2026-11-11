using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class SaidLine
    {
        internal DateTime At;
        internal string Kind;
        internal string Person;
        internal string Text;
    }

    // One gate for every line she starts herself (heartbeat, check-ins, offers, friend news): never the same thing twice,
    // never the same topic or person again too soon, never an offer in a game. His own reminders always pass.
    internal static class ProactiveGate
    {
        internal const string Reminder = "reminder";
        internal const string Game = "game";
        internal const string Brief = "brief";
        internal const string Followup = "followup";
        internal const string Friend = "friend";
        internal const string Care = "care";
        internal const int SameHours = 24;
        internal const int PersonHours = 20;
        internal const int TopicHours = 48;
        private const int KeepHours = 72;
        private const int SharedWords = 2;
        private const int ShortestWord = 5;
        internal static bool Off;
        internal static string FilePath
        {
            get { return ForkSettings.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ForkSettings.FilePath), "proactive-said.txt"); }
        }
        private static readonly object gate = new object();
        private static readonly string[] Common = { "about", "after", "again", "better", "clearer", "doing", "guide", "ideas", "structure", "style", "tips", "easier", "faster", "other", "people", "smoother", "stuck", "usually", "before", "being", "choom", "could", "maybe", "nudge", "reminder", "should", "something", "start", "still", "their", "there", "these", "thing", "things", "think", "those", "today", "tomorrow", "tonight", "where", "which", "while", "would", "youre", "yours" };

        internal static string Hold(IList<SaidLine> said, SaidLine next)
        {
            if (next == null || string.IsNullOrWhiteSpace(next.Text) || next.Kind == Reminder || next.Kind == Care) return null;
            string plain = Plain(next.Text);
            List<string> words = Salient(next.Text);
            bool report = Report(next.Text) || next.Kind == Brief || next.Kind == Followup || next.Kind == Friend;
            foreach (SaidLine one in said)
            {
                if (one == null || one.Kind == Reminder || one.Kind == Care) continue;
                double hours = (next.At - one.At).TotalHours;
                if (hours < 0) continue;
                if (hours < SameHours && Plain(one.Text) == plain) return "said already";
                if (hours < PersonHours && !string.IsNullOrEmpty(next.Person) && string.Equals(one.Person, next.Person, StringComparison.OrdinalIgnoreCase)) return "same person";
                if (!report && one.Kind != Friend && hours < TopicHours && !Report(one.Text) && Shared(words, Salient(one.Text)) >= SharedWords) return "same topic";
            }
            return null;
        }

        internal static string Check(string kind, string person, string text, DateTime now)
        {
            return Check(kind, person, text, now, null);
        }

        internal static string Check(string kind, string person, string text, DateTime now, string source)
        {
            if (Off) return null;
            SaidLine next = new SaidLine { At = now, Kind = kind, Person = person ?? "", Text = text ?? "" };
            string why = string.IsNullOrEmpty(source) ? null : Plans.Current(Vault.Root, source, now);
            if (why == null)
            {
                lock (gate)
                {
                    List<SaidLine> said = Load(now);
                    why = Hold(said, next);
                    if (why == null) { said.Add(next); Save(said); }
                }
            }
            ActivityLog.Write("proactive gate", kind + (string.IsNullOrEmpty(person) ? "" : " " + Path.GetFileNameWithoutExtension(person)), why == null ? "passed" : "held, " + why);
            return why;
        }

        internal static void Record(string kind, string person, string text, DateTime now)
        {
            if (Off || string.IsNullOrWhiteSpace(text)) return;
            lock (gate)
            {
                List<SaidLine> said = Load(now);
                said.Add(new SaidLine { At = now, Kind = kind, Person = person ?? "", Text = text });
                Save(said);
            }
        }

        private static readonly Regex GameWords = new Regex(@"\b(league|matchups?|match-ups?|lane|laning|runes?|builds?|ranked|jungle|ganks?|ganking|counters?|cs|farm|farming|waves?|dragon|baron|towers?|champions?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly string[] Starters = { "Want", "The", "Your", "You", "They", "This", "That", "What", "Did", "Morning", "Hey" };

        internal static bool AboutGame(string text, string brief)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (string.IsNullOrEmpty(brief)) return false;
            if (GameWords.IsMatch(text)) return true;
            foreach (Match name in Regex.Matches(text, @"\b[A-Z][a-z']{2,}\b"))
                if (Array.IndexOf(Starters, name.Value) < 0 && Regex.IsMatch(brief, @"\b" + Regex.Escape(name.Value) + @"\b")) return true;
            return false;
        }

        internal static void Unsay(string text)
        {
            if (Off) return;
            lock (gate)
            {
                List<SaidLine> said = Load(DateTime.Now);
                for (int i = said.Count - 1; i >= 0; i--)
                {
                    if (said[i].Text != Clean(text)) continue;
                    said.RemoveAt(i);
                    Save(said);
                    return;
                }
            }
        }

        private static readonly Regex Did = new Regex(@"^I(?:'ve| have)? (?:just )?(?:tidied|filed|messaged|sent|added|saved|wrote|booked|set|moved|cleared|put|made|started|finished|told|asked|ordered|paused|played|opened|closed|stopped)\b", RegexOptions.CultureInvariant);

        private static bool Report(string text) { return Did.IsMatch((text ?? "").TrimStart()); }

        internal static bool InGame(string text, string brief) { return Autonomy.IsOffer(text) && AboutGame(text, brief); }

        private static string Plain(string text) { return Regex.Replace(Regex.Replace((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{Nd} ]+", " "), @"\s+", " ").Trim(); }

        internal static List<string> Salient(string text)
        {
            List<string> found = new List<string>();
            foreach (string word in Plain((text ?? "").Replace("'s ", " ").Replace("’s ", " ")).Split(' '))
                if (word.Length >= ShortestWord && Array.IndexOf(Common, word) < 0 && !found.Contains(word)) found.Add(word);
            return found;
        }

        private static int Shared(List<string> first, List<string> second)
        {
            int count = 0;
            foreach (string word in first) if (second.Contains(word)) count++;
            return count;
        }

        private static List<SaidLine> Load(DateTime now)
        {
            List<SaidLine> said = new List<SaidLine>();
            string path = FilePath;
            if (path == null || !File.Exists(path)) return said;
            try
            {
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string[] parts = line.Split('\t');
                    DateTime at;
                    if (parts.Length < 4 || !DateTime.TryParseExact(parts[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) continue;
                    if ((now - at).TotalHours > KeepHours) continue;
                    said.Add(new SaidLine { At = at, Kind = parts[1], Person = parts[2], Text = parts[3] });
                }
            }
            catch (IOException) { }
            return said;
        }

        private static void Save(List<SaidLine> said)
        {
            List<string> lines = new List<string>();
            foreach (SaidLine one in said)
                lines.Add(one.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\t" + Clean(one.Kind) + "\t" + Clean(one.Person) + "\t" + Clean(one.Text));
            string path = FilePath;
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string Clean(string text) { return Regex.Replace(text ?? "", @"[\t\r\n]+", " "); }
    }
}
