using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class Forgetting
    {
        private const int LeastTopic = 4;
        private const int MostTopics = 3;
        internal const string NothingFound = "I couldn't find that in my notes or our chat, choom. Say the exact words and I'll drop it.";
        internal const string Rule = "\nForgetting: if they ask you to forget, remove, delete or wipe something from your memory, your notes or this chat, in any words or language, start your reply with [forget: topic] where the topic is what to drop in a few words. Work out what 'that' or 'it' means from the chat, and put every spelling you may have heard it as, split with |, for example [forget: green tea | greentea]. Write the topic in the words her notes use (her notes are in English), even when they asked in another language. To wipe everything use [forget: everything]. Then say one short line. You can always forget, so never say you can't. Use the marker only when they ask you to forget something.";
        private static readonly Regex Marker = new Regex(@"\[\s*forget\s*:\s*([^\]\r\n]{1,120})\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Everything = new Regex(@"^(everything|all|all of it|everything about me|my notes|my memory|your memory|my memories)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Keeping = new Regex(@"\b(?:don'?t|do not|dont|never|not to|won'?t)\s+(?:you\s+)?forget\b|\bforgot(?:ten)?\s+(?:to|that|about)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Deleting = new Regex(@"\b(?:wipe|erase|delete|remove|clear|drop)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // "Don't forget X" asks her to keep something, so a [forget] marker for it is dropped.
        internal static bool Wanted(string said)
        {
            return !Keeping.IsMatch(said ?? "") || Deleting.IsMatch(said ?? "");
        }

        internal static string Take(ref string raw)
        {
            string text = raw ?? "";
            Match found = Marker.Match(text);
            if (!found.Success) return null;
            raw = Marker.Replace(text, "").Trim();
            return found.Groups[1].Value.Trim();
        }

        private static readonly Regex Preamble = new Regex(@"^\s*(?:the\s+)?(?:line|note|fact|bit|part)s?\s+(?:in|from|on)\s+(?:my\s+|your\s+|the\s+)?[\p{L}'’-]+\s+(?:note|notes|file|page)\s+(?:that|which)\s+(?:says|said|reads)\s*:?\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex QuotedPart = new Regex(@"[""“]([^""“”]{4,120})[""”]", RegexOptions.Compiled);

        internal static string Plain(string what)
        {
            Match quoted = QuotedPart.Match(what ?? "");
            if (quoted.Success) return quoted.Groups[1].Value.Trim(' ', '.', ',');
            return Preamble.Replace(what ?? "", "").Trim(' ', '"', '\'', '“', '”', '.', ',');
        }

        internal static List<string> Topics(string said)
        {
            List<string> topics = new List<string>();
            foreach (string part in (said ?? "").Split('|'))
            {
                string topic = part.Trim(' ', '"', '\'', '*', '`', '.');
                if (Squash(topic).Length >= LeastTopic && topic.Length <= 40 && topics.Count < MostTopics) topics.Add(topic);
            }
            return topics;
        }

        internal static bool IsAll(List<string> topics)
        {
            return topics.Count > 0 && topics.TrueForAll(delegate(string topic) { return Everything.IsMatch(topic); });
        }

        internal static string Squash(string text)
        {
            return Regex.Replace(text ?? "", @"[\W_]+", "").ToLowerInvariant();
        }

        internal static string Tell(int notes, int chat)
        {
            List<string> parts = new List<string>();
            if (notes > 0) parts.Add(notes + (notes == 1 ? " note" : " notes") + " from my notes");
            if (chat > 0) parts.Add(chat + (chat == 1 ? " line" : " lines") + " of our chat");
            return parts.Count == 0 ? NothingFound : "Gone, choom. Removed " + string.Join(" and ", parts.ToArray()) + ".";
        }
    }
}
