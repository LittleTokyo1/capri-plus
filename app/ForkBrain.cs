using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    // Capri+ on her brain: a fresh look at the screen goes with every chat message they send her, so she always knows what's up.
    internal sealed partial class Brain
    {
        internal const string GlimpseNote = "(A screenshot of their screen right now is attached. Use it when it helps, don't describe it unless asked.)\n";
        private static readonly System.Text.RegularExpressions.Regex AsideLine = new System.Text.RegularExpressions.Regex(@"\b(?:nothing|no need|not much) (?:here )?to (?:hype|react to|react|comment on|comment|say|cheer)\b|\b(?:so )?i'?ll (?:stay|keep) quiet\b|\bstaying quiet\b|\bno reaction needed\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        internal static bool Aside(string said)
        {
            return AsideLine.IsMatch((said ?? "").Replace('’', '\''));
        }
        internal Func<byte[]> Eyes;
        // What they asked, for tools that answer differently when they wanted a page opened rather than an answer.
        [ThreadStatic] internal static string Asking;
        // True while the turn on this thread came in by voice; set by Companion just before Ask.
        [ThreadStatic] internal static bool Voiced;
        [ThreadStatic] internal static string Unsure;
        internal static Func<string, string> NowNote = delegate { return ""; };

        internal static string NowSafe(string kind)
        {
            string note;
            try { note = NowNote(kind) ?? ""; }
            catch (Exception) { note = ""; }
            if (kind != CapriDesktop.Now.Chat || Unsure == null) return note;
            note += "\nHis message could mean two different things. Ask him this one short question and do nothing yet: " + Unsure;
            Unsure = null;
            return note;
        }

        internal static string TurnNotes(bool tools) { return (tools ? Fork.HandsRules : "") + Fork.HearingRules + SelfKnowledge.Block() + Forgetting.Rule + (Voiced ? Fork.VoiceTurn : ""); }

        // Swaps the last user message for the image + text parts. Only Claude reads images this way; others keep plain text.
        internal const string CodeNote = "(Leave coding, terminal and Claude Code windows out of your reply unless they ask about them.)\n";
        internal const string SameNote = "(Your last reply already talked about their screen, so don't mention the screen this time unless they ask about it.)\n";
        private static readonly System.Text.RegularExpressions.Regex ScreenTalk = new System.Text.RegularExpressions.Regex(@"\b(screen|window|terminal|claude code|claude window|tab|desktop|wallpaper|monitor|checklist|smoke test)s?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        internal static string GlimpseFor(string previous)
        {
            return GlimpseNote + CodeNote + (previous != null && ScreenTalk.IsMatch(previous) ? SameNote : "");
        }

        private string Glimpse(List<ChatMessage> messages)
        {
            Func<byte[]> eyes = Eyes;
            Cloud remote = Remote;
            if (remote == null || !remote.Claude || messages.Count == 0) return null;
            byte[] shared = PhoneShare.Take(DateTime.Now);
            if (shared == null && eyes == null) return null;
            byte[] jpeg = shared;
            if (jpeg == null)
            {
                try { jpeg = eyes(); }
                catch (Exception) { return null; }
            }
            if (jpeg == null || jpeg.Length == 0) return null;
            ChatMessage last = messages[messages.Count - 1];
            if (last.Role != "user") return null;
            ChatMessage before = messages.Count > 1 ? messages[messages.Count - 2] : null;
            string text = (shared != null ? PhoneShare.Note : GlimpseFor(before != null && before.Role == "assistant" ? before.Content : null)) + last.Content;
            messages[messages.Count - 1] = new ChatMessage("user", Parts);
            return "[{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/jpeg\",\"data\":\"" + Convert.ToBase64String(jpeg) + "\"}},{\"type\":\"text\",\"text\":" + Voice.Quote(text) + "}]";
        }

        // A line she said on her own (a game reaction, League advice) goes into her chat history, so "what did you just say?" works.
        internal void Own(string situation, string said)
        {
            if (string.IsNullOrWhiteSpace(said)) return;
            situation = System.Text.RegularExpressions.Regex.Replace(situation ?? "", @"\s+", " ").Trim();
            if (situation.Length > 160) situation = situation.Substring(0, 157) + "...";
            Remember("(" + situation + ")", said.Length > 700 ? said.Substring(0, 697) + "..." : said);
        }

        internal string[] Recent()
        {
            lock (gate)
            {
                string[] said = new string[history.Count];
                for (int i = 0; i < said.Length; i++) said[i] = history[i].Content;
                return said;
            }
        }

        internal string GlimpseForTest(List<ChatMessage> messages) { return Glimpse(messages); }
    }
}
