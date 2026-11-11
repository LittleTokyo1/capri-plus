using System;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // Her name as her ears mishear it at the start of a command ("Kathy, pause", "Caprin, skip", "Gabri, next song"): one word,
    // then a comma, that sounds like Capri. Only that word goes; the rest must still be a whole command on its own.
    internal static class NameEars
    {
        private const string Her = "kapri";
        private const int MostOff = 2;
        private static readonly Regex Leading = new Regex(@"^\s*(?:(?:hey|yo|ok|okay|so|um+|uh+|now)[,\s]+)?(?<name>\p{L}{3,9})(?:\s*,\s*|\s+)(?<rest>\S.*)$", RegexOptions.IgnoreCase);

        internal static string Strip(string said)
        {
            Match found = Leading.Match(said ?? "");
            if (!found.Success || !SoundsLikeHer(found.Groups["name"].Value)) return null;
            return found.Groups["rest"].Value;
        }

        internal static bool SoundsLikeHer(string word)
        {
            string key = Key(word);
            if (key.Length < 3 || key[0] != 'k') return false;
            return Distance(key, Her) <= MostOff;
        }

        internal static bool CloseToHer(string word)
        {
            string key = Key(word);
            return key.Length >= 3 && Distance(key, Her) <= MostOff;
        }

        private static string Key(string word)
        {
            string plain = Regex.Replace((word ?? "").ToLowerInvariant(), "[^a-z]", "");
            plain = Regex.Replace(plain, "(ie|ee|ey|y)$", "i");
            plain = Regex.Replace(plain, "[cgq]", "k").Replace('b', 'p');
            return Regex.Replace(plain, "(.)\\1+", "$1");
        }

        private static int Distance(string a, string b)
        {
            int[,] cost = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) cost[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) cost[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    cost[i, j] = Math.Min(Math.Min(cost[i - 1, j] + 1, cost[i, j - 1] + 1), cost[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return cost[a.Length, b.Length];
        }
    }
}
