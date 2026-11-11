using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class SentAsk
    {
        internal const int FreshHours = 3;
        private static readonly Regex Asked = new Regex(@"^\W*(?:(?:hey|yo|ok|okay)[\s,]+)?(?:capri[\s,]+)?what\s+(?:exactly\s+)?(?:did|have)\s+you\s+(?:just\s+)?(?:say|send|sent|write|wrote|tell|told|text|texted|message|messaged)(?:\s+(?:to\s+)?(?<who>[\p{L}'_.-]+))?(?:\s+(?:exactly|on discord))?\W*$", RegexOptions.IgnoreCase);
        private static readonly Regex Pronoun = new Regex(@"^(him|her|them|it|that|me)$", RegexOptions.IgnoreCase);

        internal static bool Wants(string text, out string who)
        {
            who = null;
            Match match = Asked.Match(text ?? "");
            if (!match.Success) return false;
            string named = match.Groups["who"].Value;
            if (named.Length > 0 && !Pronoun.IsMatch(named)) who = named;
            return true;
        }

        internal static string Answer(string root, string who, DateTime now)
        {
            if (string.IsNullOrEmpty(root)) return null;
            string file = Path.Combine(root, "Capri", "sent-messages.md");
            string[] lines;
            try
            {
                if (!File.Exists(file)) return null;
                lines = File.ReadAllLines(file);
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string raw = lines[i];
                if (!raw.StartsWith("- ", StringComparison.Ordinal) || raw.Length < 22) continue;
                DateTime when;
                if (!DateTime.TryParseExact(raw.Substring(2, 16), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out when)) continue;
                if ((now - when).TotalHours > FreshHours) return null;
                int at = raw.IndexOf(" to ", 18, StringComparison.Ordinal);
                int colon = at < 0 ? -1 : raw.IndexOf(": ", at + 4, StringComparison.Ordinal);
                if (colon < 0) continue;
                string to = raw.Substring(at + 4, colon - at - 4);
                if (who != null && to.IndexOf(who, StringComparison.OrdinalIgnoreCase) < 0 && who.IndexOf(to, StringComparison.OrdinalIgnoreCase) < 0) continue;
                return "I sent " + to + ": \"" + raw.Substring(colon + 2).Trim() + "\"";
            }
            return null;
        }
    }

    internal sealed partial class Companion
    {
        private bool SentHeard(string text, bool voiceChat)
        {
            if (voiceChat) return false;
            string who;
            if (!SentAsk.Wants(text, out who)) return false;
            string said = SentAsk.Answer(Vault.Root, who, DateTime.Now);
            if (said == null) return false;
            ActivityLog.Write("discord", "what she sent", said);
            brain.Own("he asked what she sent", said);
            Tell("(what I sent)", said);
            return true;
        }
    }
}
