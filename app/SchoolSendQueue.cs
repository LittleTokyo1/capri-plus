using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class SchoolSendQueue
    {
        internal const int RecentMinutes = 30;
        private static readonly Regex NotHere = new Regex(@"\b(?:discord|dms?|whatsapp|steam|telegram|sms|texts?)\b", RegexOptions.IgnoreCase);
        private static readonly Regex MailWords = new Regex(@"\b(?:e-?mails?|mails?|drafts?|school)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Refused = new Regex(@"\b(?:don'?t|do\s+not|never|not\s+yet)\s+(?:\w+\s+){0,2}send\b", RegexOptions.IgnoreCase);
        private static readonly Regex Writing = new Regex(@"\b(?:write|compose|prepare|redraft|re-draft)\b|(?:^|[,.;:!?]|\b(?:and|then|please|to|pls|(?:can|could|would|will)\s+you))\s*draft\b", RegexOptions.IgnoreCase);
        private static readonly Regex And = new Regex(@"\b(?:and|both)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Both = new Regex(@"\bsend\s+(?:them\s+both|both(?:\s+of\s+them)?|the\s+two|those\s+two|these\s+two)\b", RegexOptions.IgnoreCase);
        private static readonly Regex All = new Regex(@"\bsend\s+(?:them(?:\s+all)?|all(?:\s+of\s+them)?|these|those|(?:all\s+)?(?:the|my)\s+(?:waiting\s+|school\s+)?(?:drafts|e-?mails|mails))\b", RegexOptions.IgnoreCase);
        private static readonly Regex Named = new Regex(@"\bsend\s+(?:(?:the|that|my)\s+)?(?<name>(?!(?:an?|to|it|him|her|them|me)\b)[\p{L}\d'-]+(?:\s+(?!(?:an?|to|it)\b)[\p{L}\d'-]+){0,5}?)\s+(?:one|draft|e-?mail|mail|reply)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Word = new Regex(@"\b[\p{L}][\p{L}'-]+\b");
        private static readonly Regex Day = new Regex(@"^(\d{4}-\d{2}-\d{2})");
        private static readonly Regex Prefix = new Regex(@"^(?:\s*(?:re|fw|fwd|aw|antw)\s*:\s*)+", RegexOptions.IgnoreCase);
        private static readonly string[] Common = { "send", "both", "them", "all", "these", "those", "the", "my", "one", "ones", "draft", "drafts", "email", "emails", "mail", "mails", "and", "to", "it", "that", "this", "school", "please", "capri", "okay", "ok", "go", "ahead", "green", "light", "on", "for", "now", "yes", "yeah", "two", "first", "second", "of", "an", "can", "you", "just", "reply", "so", "want", "me", "out", "too", "also", "good", "looks", "fine" };

        internal static string Kind(string text)
        {
            string said = (text ?? "").Trim();
            if (said.Length == 0 || Refused.IsMatch(said)) return null;
            if (Both.IsMatch(said)) return "both";
            if (All.IsMatch(said)) return "all";
            if (Named.IsMatch(said)) return "named";
            return null;
        }

        internal static string Wanted(string text, bool recent, List<StudyDraft> waiting)
        {
            string said = (text ?? "").Trim();
            if (said.Length == 0 || NotHere.IsMatch(said)) return null;
            bool mail = MailWords.IsMatch(said) || recent;
            string kind = Kind(said);
            if (kind == "both" || kind == "all") return mail ? kind : null;
            if (kind == "named") return mail || Matching(waiting, said).Count > 0 ? kind : null;
            return recent && !Refused.IsMatch(said) && SchoolMail.SendIt.IsMatch(said) ? "last" : null;
        }

        internal static bool Sends(string text)
        {
            string said = (text ?? "").Trim();
            if (said.Length == 0 || NotHere.IsMatch(said) || Writing.IsMatch(said)) return false;
            return Kind(said) != null || (!Refused.IsMatch(said) && SchoolMail.SendIt.IsMatch(said));
        }

        internal static bool HandsSend(string task, bool recent)
        {
            return Sends(task) && (recent || MailWords.IsMatch(task ?? ""));
        }

        private static List<string> Words(string text)
        {
            List<string> words = new List<string>();
            foreach (Match one in Word.Matches(text ?? ""))
            {
                string key = one.Value.ToLowerInvariant();
                if (Array.IndexOf(Common, key) < 0 && !words.Contains(key)) words.Add(key);
            }
            return words;
        }

        internal static List<StudyDraft> Matching(List<StudyDraft> drafts, string text)
        {
            List<string> words = Words(text);
            if (drafts == null || words.Count == 0) return new List<StudyDraft>();
            return drafts.FindAll(delegate(StudyDraft one)
            {
                string about = ((one.Id ?? "").Replace('-', ' ').Replace('_', ' ') + " " + (one.To ?? "") + " " + (one.Cc ?? "") + " " + (one.Subject ?? "")).ToLowerInvariant();
                foreach (string word in words)
                    if (Regex.IsMatch(about, @"\b" + Regex.Escape(word) + (word.Length >= 4 ? "" : @"\b"))) return true;
                return false;
            });
        }

        internal static string Thread(StudyDraft draft)
        {
            string subject = Regex.Replace(Prefix.Replace(draft.Subject ?? "", ""), @"\s+", " ").Trim().ToLowerInvariant();
            return subject.Length == 0 ? "#" + draft.Id : subject;
        }

        private static bool Newer(StudyDraft later, StudyDraft earlier)
        {
            Match a = Day.Match(later.Id ?? ""), b = Day.Match(earlier.Id ?? "");
            if (a.Success && b.Success && a.Value != b.Value) return string.CompareOrdinal(a.Value, b.Value) > 0;
            return true;
        }

        internal static List<StudyDraft> Newest(List<StudyDraft> waiting, out List<StudyDraft> older)
        {
            List<StudyDraft> kept = new List<StudyDraft>();
            older = new List<StudyDraft>();
            foreach (StudyDraft one in waiting)
            {
                string thread = Thread(one);
                int at = kept.FindIndex(delegate(StudyDraft other) { return Thread(other) == thread; });
                if (at < 0) { kept.Add(one); continue; }
                if (Newer(one, kept[at])) { older.Add(kept[at]); kept[at] = one; }
                else older.Add(one);
            }
            return kept;
        }

        private static string When(StudyDraft draft)
        {
            Match day = Day.Match(draft.Id ?? "");
            DateTime date;
            if (!day.Success || !DateTime.TryParseExact(day.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return "";
            return " (" + date.ToString("d MMM", CultureInfo.InvariantCulture) + ")";
        }

        internal static string Listed(List<StudyDraft> drafts)
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < drafts.Count; i++) lines.Add((i + 1) + ") " + StudyDrafts.Short(drafts[i]) + When(drafts[i]));
            return string.Join("; ", lines.ToArray());
        }

        internal static List<StudyDraft> Pick(List<StudyDraft> drafts, string kind, string text, string lastId, out List<StudyDraft> older, out string ask)
        {
            ask = null;
            List<StudyDraft> none = new List<StudyDraft>();
            List<StudyDraft> waiting = StudyDrafts.Waiting(drafts);
            List<StudyDraft> newest = Newest(waiting, out older);
            if (kind == "last")
            {
                StudyDraft last = SchoolMail.Find(waiting, lastId);
                if (last == null) ask = "That draft isn't waiting on your drafts page any more, so there's nothing to send. " + StudyDrafts.Listing(drafts);
                return last == null ? none : new List<StudyDraft> { last };
            }
            if (newest.Count == 0) { ask = "No drafts are waiting on your drafts page, so there's nothing to send."; return none; }
            if (kind == "named")
            {
                List<StudyDraft> named = Matching(newest, text);
                if (named.Count == 0 && Words(text).Count == 0 && newest.Count == 1) named = newest;
                if (named.Count == 0) ask = "I can't tell which draft you mean, so I sent nothing. Waiting: " + Listed(newest) + ".";
                else if (named.Count > 1 && !And.IsMatch(text ?? "")) ask = "Which one: " + Listed(named) + "? I sent nothing yet.";
                return ask == null ? named : none;
            }
            if (kind == "both" && newest.Count > 2)
            {
                ask = newest.Count + " drafts are waiting: " + Listed(newest) + ". Which two? Name them, like send the Dutch one and the feedback one. I sent nothing yet.";
                return none;
            }
            return newest;
        }

        internal static string Flag(List<StudyDraft> older, List<StudyDraft> picked)
        {
            List<string> said = new List<string>();
            foreach (StudyDraft old in older ?? new List<StudyDraft>())
            {
                string thread = Thread(old);
                if (picked.Exists(delegate(StudyDraft one) { return Thread(one) == thread; }))
                    said.Add("I left out an older draft on the same thread " + StudyDrafts.Short(old) + When(old) + "; it stays on your drafts page.");
            }
            return string.Join(" ", said.ToArray());
        }

        internal static string Intro(int count)
        {
            return count <= 1 ? "" : (count == 2 ? "Two" : count.ToString(CultureInfo.InvariantCulture)) + " drafts to send, one at a time. First: ";
        }

        internal static bool Clear(ClaudeJobResult result)
        {
            string output = result == null ? "" : result.Output ?? "";
            return Regex.IsMatch(output, @"(?m)^\W*NOT SENT\b") || (result != null && result.Ok && Regex.IsMatch(output, @"(?m)^\W*SENT\b"));
        }
    }
}
