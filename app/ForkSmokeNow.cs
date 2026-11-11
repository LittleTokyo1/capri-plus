using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeNow(List<string> checks)
        {
            DateTime at = new DateTime(2026, 11, 3, 21, 30, 0);
            NowState state = Now.For(at, "in a game");
            state.Channel = "Discord DM";
            state.Game = "League of Legends, Zed mid, 3/1/2";
            state.Call.Add("Rudce");
            state.Listen = true;
            state.Window = "Visual Studio Code";
            state.Job = "find a pizza place";
            state.Did.Add("Sent the shopping list to his Discord");
            state.Plans.Add("Register for Dutch");
            state.Calendar.Add("Dentist at 15:00");
            state.Offer = "a break reminder";
            string friend = Now.Block(state, Now.Friend);
            if (friend != "Now: Tuesday 21:30\nHim right now: in a game") throw new Exception("The friend block is not exactly the time and his status: " + friend);
            foreach (string kept in new[] { "Rudce", "Zed", "Visual Studio", "pizza", "shopping", "Dutch", "Dentist", "break", "Listening", "Discord DM" })
                if (friend.Contains(kept)) throw new Exception("The friend block leaked " + kept);
            string full = Now.Block(state, Now.Full);
            foreach (string kept in new[] { "Rudce", "Zed", "Visual Studio", "pizza", "shopping", "Dutch", "Dentist", "break", "Listening", "Discord DM" })
                if (!full.Contains(kept)) throw new Exception("The full block lost " + kept);
            state.Spoken = true;
            state.Hints = "\nMore than one person could be meant by \"Jonas\".";
            string heard = Now.Block(state, Now.Understanding);
            if (!heard.StartsWith("Now: Tuesday 21:30", StringComparison.Ordinal) || !heard.Contains("He used: Discord DM") || !heard.Contains("mishear") || !heard.Contains("Rudce") || !heard.Contains("Listening mode is on") || !heard.Contains("could be meant by \"Jonas\"")) throw new Exception("The understanding block lost the time, channel, mic note, call, listening or name hints: " + heard);
            if (!heard.Contains("Her open job: find a pizza place")) throw new Exception("The understanding block lost her open job: " + heard);
            foreach (string kept in new[] { "shopping", "Dutch", "Dentist", "break", "Visual Studio" })
                if (heard.Contains(kept)) throw new Exception("The understanding block carried " + kept);
            state.Spoken = false;
            if (Now.Block(state, Now.Understanding).Contains("mishear")) throw new Exception("A typed line was called misheard.");
            string chat = Now.Block(state, Now.Chat);
            if (!chat.Contains("He used: Discord DM") || !chat.Contains("Rudce") || !chat.Contains("He is at his PC") || chat.Contains("Now:") || chat.Contains("Jonas")) throw new Exception("The chat block is wrong: " + chat);
            foreach (string kept in new[] { "shopping", "Dutch", "Dentist", "break", "Visual Studio" })
                if (chat.Contains(kept)) throw new Exception("The chat block carried " + kept);
            Func<string, string> noteWas = Brain.NowNote;
            Brain.NowNote = delegate { throw new InvalidOperationException("broken"); };
            string safe = Brain.NowSafe(Now.Chat);
            Brain.NowNote = noteWas;
            if (safe != "") throw new Exception("A broken Now note broke her chat prompt.");
            string own = Now.Block(state, Now.Own);
            if (own.Contains("He used") || !own.Contains("Rudce") || !own.Contains("He is at his PC")) throw new Exception("The block for her own lines is wrong: " + own);
            Brain.NowNote = delegate(string kind) { return "\n[now " + kind + "]"; };
            string sayDo = SayDo.Frame(at, "", "open spotify", null, false);
            string beat = Heartbeat.Frame(new List<string> { "a new mail" }, null, at);
            Brain.NowNote = noteWas;
            if (!sayDo.Contains("[now tools]") || !beat.Contains("[now own]")) throw new Exception("The say-do check or the heartbeat did not read its Now slice.");
            if (Understand.Read("ASK: Spotify or YouTube?") != "ASK: Spotify or YouTube?" || Understand.Read("ASK: maybe Spotify") != null || !Understand.Prompt().Contains("ASK: <one short question")) throw new Exception("The ask verdict was read wrong.");
            Brain.Unsure = "Spotify or YouTube?";
            string asking = Brain.NowSafe(Now.Chat);
            if (!asking.Contains("Ask him this one short question and do nothing yet: Spotify or YouTube?") || Brain.Unsure != null || Brain.NowSafe(Now.Chat).Contains("Spotify or YouTube")) throw new Exception("Her reply was not told to ask, or asked twice: " + asking);
            Brain.Unsure = "Spotify or YouTube?";
            if (Brain.NowSafe(Now.Tools).Contains("Spotify or YouTube") || Brain.Unsure == null) throw new Exception("The tool step took the question.");
            Brain.Unsure = null;
            state.Job = "find a pizza place";
            if (!Now.Block(state, Now.Chat).Contains("Her open job: find a pizza place") || !Now.Block(state, Now.Understanding).Contains("Her open job") || Now.Block(state, Now.Friend).Contains("pizza")) throw new Exception("Her open job did not reach his slices, or reached a friend.");
            string book = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "capri-decisions-smoke-" + Guid.NewGuid().ToString("N") + ".txt");
            Func<string> bookWas = DecisionBook.FilePath;
            Func<DateTime> todayWas = DecisionBook.Today;
            try
            {
                System.IO.File.WriteAllText(book, "chat\twhat are some things that you like\n");
                DecisionBook.FilePath = delegate { return book; };
                DecisionBook.Today = delegate { return new DateTime(2026, 11, 3); };
                DecisionBook.Reset();
                if (DecisionBook.Known("What are some things that you like?", "game") != "chat") throw new Exception("An old decision line was lost.");
                if (!DecisionBook.Learn("play something chill for the evening", "clear", "pc") || DecisionBook.Known("play something chill for the evening", "game") != null || DecisionBook.Known("play something chill for the evening", "pc") != "clear") throw new Exception("A decision was not kept by situation.");
                if (!DecisionBook.Unlearn() || DecisionBook.Known("play something chill for the evening", "pc") != null || DecisionBook.Unlearn()) throw new Exception("A correction did not unlearn the last reading once.");
                DecisionBook.Learn("play something chill for the evening", "clear", "pc");
                DecisionBook.Today = delegate { return new DateTime(2026, 11, 18); };
                if (DecisionBook.Known("play something chill for the evening", "pc") != null) throw new Exception("A decision older than 14 days was used.");
                DecisionBook.Learn("open the study tracker for me please", "clear", "pc");
                DecisionBook.Reset();
                string saved = System.IO.File.ReadAllText(book);
                if (saved.Contains("play something chill") || !saved.Contains("clear\tpc|open the study tracker for me please\t2026-11-18")) throw new Exception("The decision file kept an old line or lost the new one: " + saved);
                if (!Understand.Corrects("no, not that one") || Understand.Corrects("open spotify")) throw new Exception("A correction was read wrong.");
            }
            finally { DecisionBook.FilePath = bookWas; DecisionBook.Today = todayWas; DecisionBook.Reset(); if (System.IO.File.Exists(book)) System.IO.File.Delete(book); }
            if (!Hands.Prompt.Contains("only when two options are both real") || Hands.Prompt.Contains("never ask which option")) throw new Exception("The tool step still may never ask which option.");
            FriendLine line = new FriendLine();
            line.Name = "Rudce";
            line.Text = "you up?";
            string frame = AutoReply.Frame(line, "in a game", at, null);
            if (!frame.StartsWith("Now: Tuesday 21:30\nHim right now: in a game\nThe friend ", StringComparison.Ordinal)) throw new Exception("The auto-reply frame does not start with the friend block: " + frame);
            checks.Add("PASS one situation model, friend slice: Now.Block(friend) is only the time and his status, never his game details, call, listening mode, front window, her job, what she did, his plans, calendar or her offer, even when the snapshot holds them; the full block carries them all; both friend paths (auto-reply and answers to friends) start from it; the understanding block gives the time, the channel he used, a mishearing note only for spoken lines, home or away, his call, listening mode and name hints, never plans, what she did, calendar, offers, window or job; her chat prompt gets the chat block (channel, home or away, call, listening, no time twice, no name hints) and a broken one never breaks her prompt; her offers and check-ins get home or away, his call and listening mode, without the channel of his last line; the say-do check reads the chat slice, the heartbeat and the live game reactions read the own slice; her tool step (Reach) reads the tools slice; her open job reaches the understanding, chat and own slices but never a friend; an ASK verdict (a real question ending in ?) makes her chat reply ask that one question once, and the tool step never takes it; her saved readings are kept by situation (game, away, at the PC; old lines count for any), expire after 14 days and the last one is unlearned once when he corrects her within 3 minutes; her tool step may ask one short question when two options are both real");
        }
    }
}
