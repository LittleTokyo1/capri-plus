using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class Claims
    {
        internal const string Nothing = "Nothing's running for that yet, choom.";
        private static readonly Regex Told = new Regex(@"(?i:\bi(?:'ve| have)?\s+(?:already\s+|just\s+|also\s+)?(?:pinged|messaged|texted|dm'?e?d|wrote to|told|reminded|let|sent(?:\s+(?:a|the|your|that|it|this)?\s*(?:message|dm|text|note|it|that)?)?(?:\s+to)?)\s+)(?<who>\p{Lu}[\p{L}'-]+)");
        private static readonly Regex Promise = new Regex(
            @"(?:^\W*|,\s*)(?:i'?m\s+)?(?:on it|working on it)\b" +
            @"|\b(?:i'?ll|i will|i'?m (?:gonna|going to|about to)|let me|lemme)\s+(?:go\s+|now\s+|just\s+)?(?:do|run|set|open|send|start|launch|ping|remind|schedule|grab|pull up|fix|handle|message|text|dm|put on)\b" +
            @"|\bdoing (?:it|that) now\b" +
            @"|\b(?:reminder|alarm|timer)(?:'s| is| has been)?\s+(?:set|added|scheduled)\b" +
            @"|\bi(?:'ve| have)\s+(?:set|scheduled|queued)\s+(?:it|that|a|an|the|your)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Sentences = new Regex(@"(?<=[.!?])\s+");
        private static readonly Regex ActionAsk = new Regex(@"^\W*(?:(?:hey|yo|ok|okay)[\s,]+)?(?:capri[\s,]+)?(?:please\s+)?(?:(?:can|could|would|will)\s+(?:you|u)\b|(?:set|send|tell|message|msg|text|dm|ping|remind|open|launch|start|stop|close|play|pause|put|make|turn|find|look|check|book|order|buy|write|add|save|remember|run|show|pull up|queue|reply)\b)", RegexOptions.IgnoreCase);

        private static bool AskedToAct(string request)
        {
            string text = request ?? "";
            return Hands.Wants(text) || ActionAsk.IsMatch(text);
        }

        private static string Plain(string text)
        {
            return (text ?? "").Replace('’', '\'');
        }

        private static bool Asking(string sentence)
        {
            return sentence.TrimEnd().EndsWith("?", StringComparison.Ordinal);
        }

        private static bool Backs(string sentence, List<string> did)
        {
            foreach (Match claim in Told.Matches(Plain(sentence)))
            {
                string who = claim.Groups["who"].Value;
                if (!did.Exists(delegate(string one) { return one.IndexOf(who, StringComparison.OrdinalIgnoreCase) >= 0; })) return false;
            }
            return true;
        }

        internal static string Backed(string line, List<string> did)
        {
            if (string.IsNullOrWhiteSpace(line) || !Told.IsMatch(Plain(line))) return line;
            List<string> kept = new List<string>();
            bool cut = false;
            foreach (string part in Sentences.Split(line.Trim()))
            {
                if (Asking(part) || Backs(part, did)) kept.Add(part);
                else cut = true;
            }
            if (!cut) return line;
            string left = string.Join(" ", kept.ToArray()).Trim();
            return left.Length == 0 ? null : left;
        }

        internal static string Check(string line, string root, DateTime now, string where)
        {
            if (root == null || string.IsNullOrWhiteSpace(line)) return line;
            string kept = Backed(line, Did.Recent(root, now));
            if (kept != line) ActivityLog.Write("honesty", "claim dropped from " + where, line);
            return kept;
        }

        internal static string Unpromised(string said, string request)
        {
            if (!AskedToAct(request)) return said;
            if (string.IsNullOrWhiteSpace(said) || !Promise.IsMatch(Plain(Brain.Clean(said)))) return said;
            List<string> kept = new List<string>();
            bool cut = false;
            foreach (string part in Sentences.Split(said.Trim()))
            {
                if (!Asking(part) && Promise.IsMatch(Plain(Brain.Clean(part)))) cut = true;
                else kept.Add(part);
            }
            if (!cut) return said;
            string left = string.Join(" ", kept.ToArray()).Trim();
            ActivityLog.Write("honesty", "promise cut, nothing behind it", Brain.Clean(said));
            return Brain.Clean(left).Length == 0 ? Nothing : left;
        }
    }
}
