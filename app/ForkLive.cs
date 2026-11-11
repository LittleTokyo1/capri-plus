using System;
using System.Collections.Generic;
using System.Threading;

namespace CapriDesktop
{
    // Capri+ in a live League match: what she knows about the game, and her reactions to kills, deaths, objectives and the result.
    internal sealed partial class Companion
    {
        private int lastEvent = -1;
        private bool live;
        private DateTime lastReaction = DateTime.MinValue;
        private volatile bool reacting;
        private readonly LiveGame.Scare scare = new LiveGame.Scare();
        private readonly List<int> farmMarks = new List<int>();
        private readonly List<string> gameLines = new List<string>();
        private const int FreshLines = 4;
        private readonly Dictionary<string, DateTime> reactedMoments = new Dictionary<string, DateTime>();
        internal const int SameMomentSeconds = 180;

        internal static List<string> Unrepeated(List<string> moments, Dictionary<string, DateTime> reacted, DateTime now)
        {
            return moments.FindAll(delegate(string moment)
            {
                DateTime when;
                return !reacted.TryGetValue(moment, out when) || (now - when).TotalSeconds >= SameMomentSeconds;
            });
        }

        internal static string Fresh(List<string> said)
        {
            List<string> recent;
            lock (said) recent = new List<string>(said);
            if (recent.Count == 0) return "";
            return "\nYour last lines this game were: " + string.Join(" | ", recent.ToArray()) + "\nSay this one fresh: don't open the way those did and don't reuse their words or pet names (like yezzirrr, choom, preem, nova); at most one slang word.";
        }

        // Every League poll while a match runs. Answers the reaction it made, when told to wait for it (the smoke test).
        internal string LiveTurn(bool wait)
        {
            LiveData data = LiveGame.Read();
            if (data == null) { HudMissed(); return null; }
            string brief = LiveGame.Describe(data);
            if (brief.Length == 0) { HudMissed(); return null; }
            live = true;
            League.Brief = brief + BuildBrief(data);
            LastGame.Seen(brief, DateTime.Now);
            LiveEvent[] events = data.Events == null || data.Events.List == null ? new LiveEvent[0] : data.Events.List;
            int newest = lastEvent;
            List<string> moments = new List<string>();
            foreach (LiveEvent happened in events)
            {
                if (happened.Id <= lastEvent) continue;
                if (happened.Id > newest) newest = happened.Id;
                if (lastEvent < 0) continue;
                string moment = LiveGame.Moment(happened, data, ForkSettings.Current.Proactive);
                KeyLights.Game(moment);
                if (moment != null) moments.Add(moment);
            }
            bool first = lastEvent < 0;
            if (first && data.Clock != null) ActivityLog.Write("live game", "mode " + (data.Clock.Mode ?? "?") + ", map " + data.Clock.Map, Objectives.Rift(data) ? "Summoner's Rift: farm and objective calls on" : "not the Rift: no farm or objective calls");
            lastEvent = Math.Max(newest, 0);
            HudTurn(data);
            ObjectiveTurn(data);
            if (ForkSettings.Current.Proactive >= 1)
            {
                string scared = scare.Watch(data), farm = FarmMoment(data, first);
                if (scared != null) moments.Add(scared);
                if (farm != null) moments.Add(farm);
            }
            moments = Unrepeated(moments, reactedMoments, DateTime.Now);
            if (first || moments.Count == 0 || ForkSettings.Current.Paused) return null;
            string what = string.Join(" ", moments.ToArray());
            bool big = what.Contains("PENTA") || what.Contains("game as") || what.Contains("aced") || what.Contains("Baron") || what.Contains("stolen");
            if (reacting || (!big && (DateTime.Now - lastReaction).TotalSeconds < 10)) return null;
            reacting = true;
            lastReaction = DateTime.Now;
            foreach (string moment in moments) reactedMoments[moment] = lastReaction;
            if (wait) return React(what, data);
            ThreadPool.QueueUserWorkItem(delegate { React(what, data); });
            return null;
        }

        // At 10, 15 and 20 minutes: a gentle word if their farm is behind. Marks already passed when she joins mid-game stay quiet.
        private string FarmMoment(LiveData data, bool first)
        {
            int minutes = data.Clock == null ? 0 : (int)(data.Clock.Time / 60);
            string said = null;
            foreach (int mark in LiveGame.FarmMarks)
            {
                if (minutes < mark || farmMarks.Contains(mark)) continue;
                farmMarks.Add(mark);
                if (!first && minutes == mark) said = LiveGame.Farm(data, mark);
            }
            return said;
        }

        internal const string GameSide = " Talk straight to him as you, never they or them. In the note, They means him and Their own team means his side, so say your team, never theirs; a steal by their own team is his team stealing it from the enemy. Only name champions the note names, and never guess a sound.";

        private string React(string what, LiveData data)
        {
            string line = null;
            try
            {
                string system = Brain.Character + Context.Core() + "\nYou're watching them play League of Legends right now, like a friend over their shoulder. " +
                    "React to what just happened in ONE short line, under 15 words, in character. Hype the good stuff, roast the deaths lovingly." + GameSide + Fresh(gameLines) + Brain.NowSafe(CapriDesktop.Now.Own);
                int status;
                line = Cleaned(Quick.Ask(brain.Remote, system, Voice.Quote(what), null, 80, "off", out status));
                if (line != null && (Quick.Stopped == "max_tokens" || System.Text.RegularExpressions.Regex.IsMatch(line, @"\b(under|over) \d+ words?\b|\bone short line\b|\bkeeping it short\b|\bthe reply must\b|\bi need to fix that\b|\bno japa", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) line = null;
                string invented = GameClaims.Unfounded(line, what, data);
                if (invented != null) { ActivityLog.Write("game moment", what, "line dropped, it names " + invented + " who is not in the moment"); line = null; }
                else ActivityLog.Write("game moment", what, line ?? "no line");
                if (line != null) brain.Own("game: " + what, line);
                if (line != null) lock (gameLines) { gameLines.Add(line); if (gameLines.Count > FreshLines) gameLines.RemoveAt(0); }
                if (line != null && !testing) Post(delegate { GameLine(line); });
            }
            catch (Exception error) { Brain.Log("game moment", -1, error.Message); }
            finally { reacting = false; }
            return line;
        }

        // His own ask comes first: while she thinks about it or answers it, a game line waits and is dropped when it gets old.
        internal const int GameHoldSeconds = 20;
        private string heldGame;
        private DateTime heldGameAt;

        private bool HisTurn()
        {
            return (box != null && box.Prompt == "> you: " && (talkState == TalkState.Thinking || talkState == TalkState.Talking)) || (typer != null && typer.Visible) || transcribing || HeSpoke(unchecked(Environment.TickCount - micTalked));
        }

        internal const int SpeechGapMs = 2500;

        internal static bool HeSpoke(int sinceWords) { return sinceWords >= 0 && sinceWords < SpeechGapMs; }

        internal static bool ForkCommentMuted(string game, string leaguePhase)
        {
            return leaguePhase == "InProgress" || System.Text.RegularExpressions.Regex.IsMatch(game ?? "", @"\bLeague of Legends\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private void GameLine(string line)
        {
            if (HisTurn())
            {
                if (heldGame == null) ActivityLog.Write("game line", "held", "he was talking to her");
                heldGame = line;
                heldGameAt = DateTime.UtcNow;
                return;
            }
            SayGame(line);
        }

        internal string GameHeldTick()
        {
            if (heldGame == null || HisTurn()) return null;
            string line = heldGame;
            heldGame = null;
            if ((DateTime.UtcNow - heldGameAt).TotalSeconds > GameHoldSeconds) { ActivityLog.Write("game line", "dropped", "too old after his ask"); return null; }
            SayGame(line);
            return line;
        }

        // Spoken normally; in voice chat it's a silent bubble so she doesn't talk over herself.
        private void SayGame(string line)
        {
            if (hidden) SetHidden(false);
            heard = "(game)";
            box.Prompt = "> sys: ";
            Answer(++asking, line, TalkState.Talking, chatting);
        }

        private void EndLive()
        {
            lastEvent = -1;
            scare.Reset();
            farmMarks.Clear();
            lock (gameLines) gameLines.Clear();
            objectives.Reset();
            HudOff();
            if (!live) return;
            live = false;
            EndGameBuild();
            if (!drafting) League.Brief = null;
        }
    }
}
