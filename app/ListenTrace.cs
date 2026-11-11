using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class ListenTrace
    {
        private const int MostNear = 6;
        private static readonly object Gate = new object();
        private static readonly List<string> Near = new List<string>();
        private static readonly Regex Word = new Regex(@"\p{L}{3,9}");
        private static int heard, named;

        internal static bool Note(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            bool hers = Hands.HeardName(text);
            lock (Gate)
            {
                heard++;
                if (hers) { named++; return true; }
                foreach (Match word in Word.Matches(text))
                {
                    string plain = word.Value.ToLowerInvariant();
                    if (Near.Count < MostNear && !Near.Contains(plain) && NameEars.CloseToHer(plain)) Near.Add(plain);
                }
            }
            return false;
        }

        internal static string Line()
        {
            lock (Gate)
            {
                if (heard == 0) return null;
                string line = heard + " lines from his mic, her name in " + named + (Near.Count == 0 ? "" : ", near her name: " + string.Join(", ", Near.ToArray()));
                heard = 0;
                named = 0;
                Near.Clear();
                return line;
            }
        }
    }
}
