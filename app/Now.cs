using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CapriDesktop
{
    internal sealed class NowState
    {
        internal DateTime When;
        internal string Status = "";
        internal string Channel = "";
        internal bool Away;
        internal string Game = "";
        internal List<string> Call = new List<string>();
        internal bool Listen;
        internal string Window = "";
        internal string Job = "";
        internal List<string> Did = new List<string>();
        internal List<string> Plans = new List<string>();
        internal List<string> Calendar = new List<string>();
        internal string Offer = "";
        internal bool Spoken;
        internal string Hints = "";
        internal List<string> Phone = new List<string>();
    }

    internal static class Now
    {
        internal const string Friend = "friend";
        internal const string Full = "full";
        internal const string Understanding = "understanding";
        internal const string Chat = "chat";
        internal const string Own = "own";
        internal const string Tools = "tools";

        internal static NowState For(DateTime when, string status)
        {
            NowState state = new NowState();
            state.When = when;
            state.Status = status ?? "";
            return state;
        }

        internal static string Block(NowState state, string kind)
        {
            if (state == null) return "";
            string time = "Now: " + state.When.ToString("dddd HH:mm", CultureInfo.InvariantCulture);
            if (kind == Friend) return time + "\nHim right now: " + state.Status;
            if (kind == Understanding) return Heard(state, time);
            if (kind == Chat || kind == Tools) return Talk(state);
            if (kind == Own) { NowState quiet = Now.For(state.When, state.Status); quiet.Away = state.Away; quiet.Call = state.Call; quiet.Listen = state.Listen; quiet.Job = state.Job; return Talk(quiet); }
            StringBuilder text = new StringBuilder(time);
            if (state.Status.Length > 0) text.Append("\nHim right now: ").Append(state.Status);
            if (state.Channel.Length > 0) text.Append("\nHe wrote from: ").Append(state.Channel);
            text.Append(state.Away ? "\nHe is away from his PC." : "\nHe is at his PC.");
            if (state.Game.Length > 0) text.Append("\nGame: ").Append(state.Game);
            if (state.Call.Count > 0) text.Append("\nIn his Discord call: ").Append(string.Join(", ", state.Call.ToArray()));
            if (state.Listen) text.Append("\nListening mode is on.");
            if (state.Window.Length > 0) text.Append("\nIn front: ").Append(state.Window);
            if (state.Job.Length > 0) text.Append("\nHer open job: ").Append(state.Job);
            Lines(text, "What she did last", state.Did);
            Lines(text, "Plans due", state.Plans);
            Lines(text, "Calendar", state.Calendar);
            if (state.Offer.Length > 0) text.Append("\nHer pending offer: ").Append(state.Offer);
            PhoneLine(text, state);
            return text.ToString();
        }

        private static string Heard(NowState state, string time)
        {
            StringBuilder text = new StringBuilder(time);
            if (state.Channel.Length > 0) text.Append("\nHe used: ").Append(state.Channel);
            if (state.Spoken) text.Append("\nHe said it aloud: the mic and her ears mishear, so read sound-alike words and names generously.");
            text.Append(state.Away ? "\nHe is away from his PC." : "\nHe is at his PC.");
            if (state.Call.Count > 0) text.Append("\nIn his Discord call: ").Append(string.Join(", ", state.Call.ToArray()));
            if (state.Listen) text.Append("\nListening mode is on (she also hears the people around him).");
            if (state.Job.Length > 0) text.Append("\nHer open job: ").Append(state.Job).Append('.');
            if (state.Hints.Length > 0) text.Append(state.Hints);
            return text.ToString();
        }

        private static string Talk(NowState state)
        {
            StringBuilder text = new StringBuilder();
            if (state.Channel.Length > 0) text.Append("\nHe used: ").Append(state.Channel).Append('.');
            text.Append(state.Away ? "\nHe is away from his PC." : "\nHe is at his PC.");
            if (state.Call.Count > 0) text.Append("\nIn his Discord call right now: ").Append(string.Join(", ", state.Call.ToArray())).Append('.');
            if (state.Listen) text.Append("\nListening mode is on: she also hears the people around him.");
            if (state.Job.Length > 0) text.Append("\nHer open job: ").Append(state.Job).Append('.');
            PhoneLine(text, state);
            return text.ToString();
        }

        private static void PhoneLine(StringBuilder text, NowState state)
        {
            if (state.Phone.Count > 0) text.Append("\nFrom his iPhone: ").Append(string.Join("; ", state.Phone.ToArray())).Append('.');
        }

        private static void Lines(StringBuilder text, string label, List<string> lines)
        {
            if (lines.Count == 0) return;
            text.Append('\n').Append(label).Append(":\n- ").Append(string.Join("\n- ", lines.ToArray()));
        }
    }
}
