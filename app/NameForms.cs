using System;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class NameForms
    {
        private const int SameStart = 3;
        private const int LeastSure = 5;
        private const int LeastBase = 4;
        private const int ShortMost = 6;
        private const double ShortGap = 1.5;
        internal static readonly string[] Titles = { "mr", "mrs", "ms", "miss", "dr", "mx" };
        private static readonly string[] Nicknames = { "ce", "cia", "ukas", "iukas", "elis", "ius", "ai", "as", "ute", "yte" };
        private static readonly string[][] Cases =
        {
            new[] { "as", "ai", "ui", "o", "u", "asas" },
            new[] { "ius", "iau", "iui", "iaus", "iu" },
            new[] { "is", "i", "io", "iui", "iu" },
            new[] { "ys", "y", "io", "iui", "iu" },
            new[] { "e", "es", "ei", "ele" }
        };

        internal static string Single(string name)
        {
            string plain = Names.Fold((name ?? "").Trim());
            return Regex.IsMatch(plain, "^[a-z]+$") ? plain : null;
        }

        internal static bool Sure(string heard, string known)
        {
            if (heard.Length < LeastSure || known.Length < LeastBase || heard == known) return false;
            foreach (string[] ending in Cases)
            {
                if (!known.EndsWith(ending[0], StringComparison.Ordinal)) continue;
                string stem = known.Substring(0, known.Length - ending[0].Length);
                if (stem.Length < SameStart) continue;
                for (int i = 1; i < ending.Length; i++)
                    if (heard == stem + ending[i]) return true;
            }
            return false;
        }

        internal static bool Maybe(string heard, string known)
        {
            if (heard.Length < SameStart || known.Length < LeastBase || heard == known) return false;
            string start = known.Substring(0, SameStart);
            foreach (string ending in Nicknames)
                if (heard == start + ending) return true;
            string sound = Names.Phon(heard), name = Names.Phon(known);
            if (sound.Length >= SameStart && name.Length > sound.Length && name.StartsWith(sound, StringComparison.Ordinal)) return true;
            if (sound.Length <= SameStart || sound.Length > ShortMost || name.Length <= SameStart || name.Length > ShortMost) return false;
            return sound.Substring(0, SameStart) == name.Substring(0, SameStart) && Names.Distance(sound, name) <= ShortGap;
        }

        internal static bool Title(string word)
        {
            return Array.IndexOf(Titles, word) >= 0;
        }
    }
}
