using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class WatchNames
    {
        private const int LeastWord = 4;
        private static readonly Regex Word = new Regex(@"\p{L}[\p{L}']*");
        private static readonly string[] Plain = { "stop", "stops", "watch", "watching", "watches", "screen", "look", "looking", "comes", "come", "coming", "online", "offline", "discord", "when", "then", "with", "your", "mine", "that", "this", "them", "they", "message", "ping", "tell", "every", "time", "thing", "things", "everything", "starts", "start", "show", "shows", "pops", "goes", "game", "hop", "onto", "into", "from", "about", "please", "just", "anymore" };

        internal static bool Close(string watch, string said)
        {
            List<string> asked = Words(said);
            if (asked.Count == 0) return false;
            List<string> watched = Words(watch);
            foreach (string word in asked)
            {
                bool found = false;
                foreach (string other in watched) if (Near(word, other)) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        private static List<string> Words(string text)
        {
            List<string> words = new List<string>();
            foreach (Match match in Word.Matches((text ?? "").ToLowerInvariant()))
                if (match.Value.Length >= LeastWord && Array.IndexOf(Plain, match.Value) < 0) words.Add(match.Value);
            return words;
        }

        private static bool Near(string a, string b)
        {
            if (a == b) return true;
            if (a.Length < 5 || b.Length < 5) return false;
            string soundA = Names.Phon(a), soundB = Names.Phon(b);
            return soundA == soundB || Names.Distance(soundA, soundB) <= (Math.Max(a.Length, b.Length) >= 8 ? 2 : 1);
        }
    }
}
