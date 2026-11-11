using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // A game reaction may name only the champions of the moment it reacts to (and his own), so she never invents a play.
    internal static class GameClaims
    {
        internal static string Unfounded(string line, string moment, LiveData data)
        {
            if (string.IsNullOrWhiteSpace(line) || data == null || data.Players == null) return null;
            LivePlayer me = LiveGame.Mine(data);
            string said = Plain(line), happened = Plain(moment);
            foreach (LivePlayer player in data.Players)
            {
                if (player == null || string.IsNullOrWhiteSpace(player.Champion)) continue;
                if (me != null && string.Equals(player.Champion, me.Champion, StringComparison.OrdinalIgnoreCase)) continue;
                string name = Plain(player.Champion).Trim();
                if (name.Length < 2) continue;
                if (Names(said, name) && !Names(happened, name)) return player.Champion;
            }
            return null;
        }

        private static bool Names(string text, string name)
        {
            return Regex.IsMatch(text, " " + Regex.Escape(name) + "s? ");
        }

        private static string Plain(string text)
        {
            StringBuilder kept = new StringBuilder();
            foreach (char c in (text ?? "").ToLowerInvariant())
            {
                if (char.IsLetter(c)) kept.Append(c);
                else if (c == '\'' || c == '’') continue;
                else kept.Append(' ');
            }
            return " " + Regex.Replace(kept.ToString(), @"\s+", " ").Trim() + " ";
        }
    }
}
