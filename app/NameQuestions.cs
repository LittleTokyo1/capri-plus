using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class NameQuestions
    {
        private const int AnswerMinutes = 10;
        private const int ShortAnswer = 60;
        private const int MostWords = 6;
        private static readonly Regex Yes = new Regex(@"^\s*(yes|yeah|yep|yup|ya|right|correct|exactly|that'?s (him|her|right|the one))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Word = new Regex(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
        private static readonly object gate = new object();
        private static readonly Dictionary<string, DateTime> asked = new Dictionary<string, DateTime>();
        private static string heard;
        private static List<string> maybe = new List<string>();
        private static DateTime at;

        internal static bool ShouldAsk(string held, DateTime now)
        {
            if (Lockout.Active) return false;
            string key = Names.Fold(held ?? "");
            lock (gate)
            {
                DateTime last;
                if (asked.TryGetValue(key, out last) && last.Date == now.Date) return false;
                asked[key] = now;
                return true;
            }
        }

        internal static void Asked(PersonGuess guess)
        {
            lock (gate)
            {
                heard = guess == null ? null : guess.Heard;
                maybe = guess == null ? new List<string>() : new List<string>(guess.Maybe);
                at = DateTime.UtcNow;
            }
        }

        internal static void Reset()
        {
            lock (gate)
            {
                asked.Clear();
                heard = null;
                maybe = new List<string>();
            }
        }

        internal static bool Answer(string said)
        {
            string name;
            List<string> notes;
            DateTime when;
            lock (gate)
            {
                name = heard;
                notes = maybe;
                when = at;
                heard = null;
                maybe = new List<string>();
            }
            if (name == null || string.IsNullOrEmpty(said) || DateTime.UtcNow - when > TimeSpan.FromMinutes(AnswerMinutes)) return false;
            string line = said.Trim();
            if (line.Length > ShortAnswer || Word.Matches(line).Count > MostWords) return false;
            string note = Named(line);
            if (note == null && Yes.IsMatch(line) && notes.Count == 1) note = notes[0];
            if (note == null) return false;
            bool added = Names.AddAlias(note, name);
            ActivityLog.Write("names", "answer learned", added ? "a heard name was added to the person he picked" : "he picked a person, the heard name was already there or too short");
            return true;
        }

        private static string Named(string line)
        {
            string note = null;
            foreach (NameHit hit in Names.Find(line))
            {
                if (!hit.Exact || hit.Self || hit.Unsure) continue;
                if (note != null && !string.Equals(note, hit.Note, StringComparison.OrdinalIgnoreCase)) return null;
                note = hit.Note;
            }
            return note;
        }
    }
}
