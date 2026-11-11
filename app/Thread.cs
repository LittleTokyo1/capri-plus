using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class Pursuit
    {
        internal static Func<DateTime> Now = delegate { return DateTime.UtcNow; };
        internal const int IdleMinutes = 10;
        internal const int MostSteps = 12;
        private const int ShortWords = 4;
        private static readonly Regex Back = new Regex(@"\b(it|this|that|them|there|again|instead|one|first|second|last|other|next|more|same)\b|-(le|la)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Words = new Regex(@"[\p{L}\p{N}]{4,}");
        private static readonly Regex Idle = new Regex(@"\A(that|this|with|what|from|your|have|just|then|them|there|please|pick|open|click|press)\z", RegexOptions.IgnoreCase);
        private static readonly Regex Quit = new Regex(@"^\W*(never ?mind|nevermind|forget (it|that)|drop (it|that)|cancel( that| it)?|leave it|stop( that| it)?)\W*$", RegexOptions.IgnoreCase);

        internal readonly string Goal;
        internal string Open;
        internal DateTime Touched;
        internal readonly List<string> Steps = new List<string>();

        internal Pursuit(string goal)
        {
            Goal = Shorter(goal, 200);
            Touched = Now();
        }

        internal bool Stale { get { return (Now() - Touched).TotalMinutes > IdleMinutes; } }

        internal static string Shorter(string text, int most)
        {
            text = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return text.Length > most ? text.Substring(0, most - 3) + "..." : text;
        }

        internal void Step(string text)
        {
            Steps.Add(Shorter(text, 200));
            while (Steps.Count > MostSteps) Steps.RemoveAt(0);
            Touched = Now();
        }

        internal static bool Dropping(string said) { return Quit.IsMatch(said ?? ""); }

        internal bool Continues(string said)
        {
            if (Open != null) return true;
            string text = (said ?? "").Trim();
            if (Back.IsMatch(text) || Regex.Matches(text, @"\S+").Count < ShortWords) return true;
            string known = Goal + " " + string.Join(" ", Steps.ToArray());
            foreach (Match word in Words.Matches(text))
                if (!Idle.IsMatch(word.Value) && Regex.IsMatch(known, @"(?<![\p{L}\p{N}])" + Regex.Escape(word.Value) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)) return true;
            return false;
        }

        internal string Facts()
        {
            StringBuilder text = new StringBuilder("Work in progress for them: " + Goal);
            if (Steps.Count > 0)
            {
                text.Append("\nSteps so far:");
                foreach (string step in Steps) text.Append("\n- " + step);
            }
            if (Open != null) text.Append("\nYou asked them and are waiting for the answer: " + Shorter(Open, 200));
            return text.ToString();
        }

        internal string Note()
        {
            return "\n" + Facts() + "\nTheir next line is part of this work first (an answer, a correction, the next step), and a new request only if it is clearly unrelated. Keep going toward the goal, use what you know about them and what you see, and never act on a guess. The mic mishears words and names (their name is in their notes): fix those from this work.";
        }
    }

    internal sealed class Cutoff
    {
        private static readonly Regex Hanging = new Regex(@"(?:,|\.\.\.|…|\b(?:um+|uh+|er+|erm|and|but|or|because|its|it's|that's|i'm))\s*$", RegexOptions.IgnoreCase);
        private string held;

        internal bool Holding { get { return held != null; } }

        internal static bool Dangling(string text) { return Hanging.IsMatch((text ?? "").Replace('’', '\'').Trim()); }

        internal string Take(string text)
        {
            text = (text ?? "").Trim();
            if (held != null) { text = held + " " + text; held = null; }
            if (!Dangling(text)) return text;
            held = text;
            return null;
        }

        internal string Release()
        {
            string waited = held;
            held = null;
            return waited;
        }
    }

    internal sealed partial class Brain
    {
        private const int TalkBack = 8;
        private const int BriefTalk = 6;
        private Pursuit pursuit;
        [ThreadStatic] internal static string Briefing;
        [ThreadStatic] internal static bool Carrying;

        private Pursuit Live()
        {
            if (pursuit != null && pursuit.Stale) pursuit = null;
            return pursuit;
        }

        internal string ThreadNote()
        {
            lock (gate)
            {
                Pursuit work = Live();
                return (work == null ? "" : work.Note()) + FoundNote();
            }
        }

        private bool Carry(string text)
        {
            lock (gate)
            {
                Pursuit work = Live();
                if (work == null) return false;
                if (Pursuit.Dropping(text) || !work.Continues(text)) { pursuit = null; return false; }
                return true;
            }
        }

        private bool Waiting()
        {
            lock (gate)
            {
                Pursuit work = Live();
                return work != null && work.Open != null;
            }
        }

        private List<ChatMessage> Backlog(bool follows, bool carrying, List<ChatMessage> recent, string request)
        {
            return Backlog(follows || SayDo.BackRef(request), carrying, recent);
        }

        private List<ChatMessage> Backlog(bool follows, bool carrying, List<ChatMessage> recent)
        {
            if (carrying)
                lock (gate) return history.GetRange(Math.Max(0, history.Count - TalkBack), Math.Min(TalkBack, history.Count));
            return follows ? recent : new List<ChatMessage>();
        }

        private void Begin(string request)
        {
            lock (gate)
                if (Live() == null) pursuit = new Pursuit(request);
        }

        private void Said(string said)
        {
            lock (gate)
            {
                Pursuit work = Live();
                if (work != null && !string.IsNullOrWhiteSpace(said)) work.Step("you said you would, so a job does it now: " + said);
            }
        }

        private void Noted(string tool, ToolArguments args, string result)
        {
            lock (gate)
            {
                Pursuit work = Live();
                if (work == null) return;
                string what = args == null ? "" : args.Task ?? args.Name ?? args.Site ?? args.Search ?? args.Focus ?? args.Action ?? "";
                work.Step(tool + (what.Length > 0 ? " (" + Pursuit.Shorter(what, 80) + ")" : "") + ": " + Pursuit.Shorter(result ?? "No result.", 120));
            }
        }

        private string Briefed()
        {
            lock (gate)
            {
                Pursuit work = Live();
                if (work == null) return null;
                StringBuilder text = new StringBuilder(work.Facts());
                int from = Math.Max(0, history.Count - BriefTalk);
                if (history.Count > from) text.Append("\nThe talk just before:");
                for (int i = from; i < history.Count; i++) text.Append("\n" + (history[i].Role == "user" ? "Him: " : "Capri: ") + Pursuit.Shorter(history[i].Content, 160));
                return text.ToString();
            }
        }

        internal int Turns;
        internal DateTime LastTurnAt = DateTime.MaxValue;

        internal string Talk(int most)
        {
            lock (gate)
            {
                StringBuilder text = new StringBuilder();
                for (int i = Math.Max(0, history.Count - most); i < history.Count; i++)
                {
                    string line = history[i].Content ?? "";
                    if (history[i].Role != "user" || line.StartsWith("(", StringComparison.Ordinal)) continue;
                    text.Append("User: " + Pursuit.Shorter(line, 300) + "\n");
                }
                return text.ToString();
            }
        }

        private void Closing(string said, bool ran)
        {
            lock (gate)
            {
                Turns++;
                LastTurnAt = Pursuit.Now();
                Pursuit work = Live();
                if (work == null) return;
                work.Open = ran && Regex.IsMatch(said ?? "", @"\?\W*$") ? said : null;
                if (ran) work.Touched = Pursuit.Now();
            }
        }
    }
}
