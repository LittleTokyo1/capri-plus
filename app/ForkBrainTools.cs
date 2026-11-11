using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed partial class Brain
    {
        internal const string ToolsWord = "TOOLS";

        internal const string NeedsTools = "\nWhen what they want can only come from one of your tools, answer with only the word " + ToolsWord + " and nothing else, and your tools run at once. Your tools can: tell who of their friends is on Discord right now and when they usually are, read what friends sent your Discord bot, message a friend or them on Discord, look at their screen, use their PC and apps, play music, search the web, set reminders, read their notes, change your own settings, pass their answer to one of their Claude Code sessions, land a branch a session left ready (only on their own yes), keep your ideas for improving yourself, and think up new projects for them (start one only on their own yes). Never say whether a friend is online, when they usually are or what anyone sent without the tool: you only know that from it. Never tell them you can't see or can't check something one of these tools gives, and never offer to check, look or do it (want me to look?): answer " + ToolsWord + " instead. When they say you should have used a tool, use it now: answer " + ToolsWord + ".";

        private static readonly Regex ToolsOnly = new Regex(@"(?:^|\s)\W?" + ToolsWord + @"\W*$");

        internal static bool WantsTools(string raw)
        {
            return ToolsOnly.IsMatch(Visible(raw ?? "").Trim());
        }

        internal const string NoTool = "None of my tools had anything for that one.";

        internal static bool LeftoverTools(bool acting, bool forgetting, string raw)
        {
            return !acting && !forgetting && WantsTools(raw);
        }

        internal static string Untagged(string raw)
        {
            string text = Regex.Replace(raw ?? "", @"(?:^|\s)\W?" + ToolsWord + @"\W*$", "").Trim();
            return text.Length == 0 ? NoTool : text;
        }

        internal static bool AboutFriend(string said)
        {
            foreach (NameHit hit in Names.Find(said ?? "")) if (!hit.Self) return true;
            return false;
        }

        internal static string CarryTask(string task, string request)
        {
            if (string.IsNullOrWhiteSpace(request) || string.Equals((task ?? "").Trim(), request.Trim(), StringComparison.OrdinalIgnoreCase)) return task;
            return task + "\nHis own words were: " + Voice.Quote(request.Trim()) + ". Any message text follows his words, not this summary.";
        }

        private static List<ChatMessage> Untooled(List<ChatMessage> messages)
        {
            List<ChatMessage> again = new List<ChatMessage>(messages);
            if (again.Count > 0) again[0] = new ChatMessage("system", again[0].Content + "\nNone of your tools fit this message, so answer it yourself in your own words, never with " + ToolsWord + ".");
            return again;
        }

        private bool Tooled(string model, string request, List<ChatMessage> backlog, List<KeyValuePair<string, ToolArguments>> calls, int ticket)
        {
            ActivityLog.Write("brain", "asked for her tools", Pursuit.Shorter(request, 120));
            try
            {
                Reach(model, request, backlog, calls, ticket);
                Hands.Rescue(request, calls);
            }
            catch (BrainException) { calls.Clear(); }
            return calls.Count > 0;
        }
    }
}
