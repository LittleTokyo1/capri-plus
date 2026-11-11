using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class VoiceFiller
    {
        private static readonly Regex Noise = new Regex(@"^(?:(?:u+m+|u+h+m*|e+r+m*|h+m+|m+|a+h+|e+h+|うーん|ん|んー|ええと|えっと)\s*)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Reaction = new Regex(@"^(?:(?:okay|ok|okey|yeah|yea|yep|yup|true|true that|cool|nice|alright|all right|oh|ooh|oh no|oh yeah|oh my god|oh my day|oh damn|damn|wow|i see|got it|fine|sure|right|so|so uh|m+ ?h+m+|うん|嗯+|はい|हां)\s*)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static string Plain(string text)
        {
            string plain = Regex.Replace((text ?? "").ToLowerInvariant(), @"[\p{P}\p{S}]+", " ");
            plain = Regex.Replace(plain, @"\b(hey capri|capri)\b", " ");
            return Regex.Replace(plain, @"\s+", " ").Trim();
        }

        internal static bool Drop(string text, bool open)
        {
            string plain = Plain(text);
            if (plain.Length == 0) return false;
            if (Reaction.IsMatch(plain)) return !open;
            return Noise.IsMatch(plain);
        }

        private static readonly Regex Again = new Regex(@"^(?:sorry,?\s+|what,?\s+)?(?:can you\s+|could you\s+|please\s+)?(?:say (?:that|it) again|repeat (?:that|it|yourself|what you said|the last (?:thing|line))|what did you (?:just )?say|come again|(?:i\s+)?didn'?t (?:catch|hear) (?:that|it|you|what you said))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool RepeatAsked(string text)
        {
            string plain = Regex.Replace((text ?? "").Trim(), @"^(?:hey\s+)?capri[,\s]+", "", RegexOptions.IgnoreCase);
            return Again.IsMatch(plain);
        }

        internal const int MostRepeated = 5;
        private const int FewLines = 3;
        private static readonly Regex Several = new Regex(@"\b(?:repeat|say|tell me|what (?:were|was))\b.{0,20}?\b(?:the |your )?last (one|two|three|four|five|six|seven|eight|nine|ten|couple|couple of|few|\d{1,2}) (?:lines|things|messages|sentences|replies)\b(?!.{0,20}\b(?:songs?|tracks?|games?|videos?)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly string[] Numbers = { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };

        internal static int RepeatCount(string text)
        {
            string plain = Regex.Replace((text ?? "").Trim(), @"^(?:hey\s+)?capri[,\s]+", "", RegexOptions.IgnoreCase);
            Match many = Several.Match(plain);
            if (many.Success)
            {
                string word = many.Groups[1].Value.ToLowerInvariant();
                int count = Array.IndexOf(Numbers, word) + 1;
                if (count == 0) count = word.StartsWith("couple", StringComparison.Ordinal) ? 2 : word == "few" ? FewLines : int.Parse(word, CultureInfo.InvariantCulture);
                return Math.Max(1, Math.Min(MostRepeated, count));
            }
            return RepeatAsked(plain) ? 1 : 0;
        }

        internal static string Repeated(List<string> said, int count)
        {
            List<string> kept = said.FindAll(delegate(string line) { return !string.IsNullOrWhiteSpace(line) && !line.StartsWith("[", StringComparison.Ordinal); });
            if (kept.Count == 0) return null;
            return string.Join(" ", kept.GetRange(Math.Max(0, kept.Count - count), Math.Min(count, kept.Count)).ToArray());
        }

        internal static bool Asks(string said)
        {
            return (said ?? "").TrimEnd().EndsWith("?", StringComparison.Ordinal);
        }

        internal const int OpenQuestionSeconds = 120;

        internal static bool Fresh(DateTime lastTurn, DateTime now)
        {
            return lastTurn != DateTime.MaxValue && (now - lastTurn).TotalSeconds < OpenQuestionSeconds;
        }
    }

    internal sealed partial class Brain
    {
        internal bool Open() { return Waiting(); }

        internal string LastSaid()
        {
            lock (gate)
                for (int i = history.Count - 1; i >= 0; i--)
                    if (history[i].Role == "assistant") return history[i].Content;
            return null;
        }

        internal List<string> LastSaid(int count)
        {
            List<string> said = new List<string>();
            lock (gate)
                for (int i = history.Count - 1; i >= 0 && said.Count < count; i--)
                    if (history[i].Role == "assistant" && !string.IsNullOrWhiteSpace(history[i].Content) && !history[i].Content.StartsWith("[", StringComparison.Ordinal)) said.Insert(0, history[i].Content);
            return said;
        }
    }

    internal sealed partial class Companion
    {
        private bool RepeatHeard(string text, bool voiceChat)
        {
            int count = voiceChat ? 0 : VoiceFiller.RepeatCount(text);
            if (count == 0) return false;
            string last = VoiceFiller.Repeated(brain.LastSaid(count), count) ?? "I haven't said anything yet.";
            ActivityLog.Write("voice", count == 1 ? "repeated her last line" : "repeated her last " + count + " lines", null);
            heard = text;
            box.Prompt = "> you: ";
            Answer(++asking, last, TalkState.Talking, chatting);
            return true;
        }

        private bool FillerHeard(string text)
        {
            bool asked = (VoiceFiller.Asks(reply) || VoiceFiller.Asks(brain.LastSaid())) && VoiceFiller.Fresh(brain.LastTurnAt, Pursuit.Now());
            bool open = Security.Holding != null || asked || brain.Open() || MidJob(job);
            if (!VoiceFiller.Drop(text, open)) return false;
            ActivityLog.Write("voice", "filler skipped", VoiceFiller.Plain(text));
            mainframe.VoiceNote(LocalListening, false);
            return true;
        }
    }
}
