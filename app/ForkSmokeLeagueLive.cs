using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CapriDesktop
{
    // Capri+ smoke checks for the CS per minute strip and answer, objective calls, and accepting and locking while he is away.
    internal sealed partial class Companion
    {
        private void SmokeCsHud(List<string> checks)
        {
            LiveData data = League.Parse<LiveData>(SmokeLive("", 600, 500, false, 80));
            if (LiveGame.CsAnswer(data) != "About 8.0 CS per minute: 80 CS at 10:00, straight from the game.") throw new Exception("His CS was answered as: " + LiveGame.CsAnswer(data));
            if (LiveGame.CsAnswer(League.Parse<LiveData>(SmokeLive("", 30, 500, false, 0))) != null) throw new Exception("A CS rate was given before the first minute.");
            foreach (string asked in new[] { "what's my cs", "how's my farm", "cs per minute?", "what is my creep score", "how many cs do I have", "my cs/min" })
                if (!LiveGame.CsAsked.IsMatch(asked)) throw new Exception("\"" + asked + "\" wasn't heard as a CS question.");
            foreach (string other in new[] { "push build 2", "what's the best build", "go to the farmers market", "focus the support" })
                if (LiveGame.CsAsked.IsMatch(other)) throw new Exception("\"" + other + "\" was heard as a CS question.");
            if (!LiveGame.Describe(data).Contains("80 cs (8.0 cs/min)") || !LiveGame.Describe(data).Contains("never a fraction of one") || !LiveGame.Describe(data).Contains("named earlier in the talk was another game")) throw new Exception("Her brief doesn't pin the CS rate or this game's champion: " + LiveGame.Describe(data));

            ForkSettings keptSettings = ForkSettings.Current;
            Func<Rectangle> keptWindow = CsHud.GameWindow;
            string keptPhase = Lockout.LeaguePhase;
            TalkState keptTalk = talkState;
            string keptHeard = heard;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                HudTurn(data);
                if (!CsHeard("Capri, what's my cs per minute?", false)) throw new Exception("His CS question went to the model while a game ran.");
                if (CsHeard("what's my cs per minute?", true)) throw new Exception("The CS answer talked over the ElevenLabs agent.");
                if (CsHeard("push build 2", false)) throw new Exception("Another line was taken as a CS question.");
                lastLiveAt = DateTime.Now.AddSeconds(-FreshLive - 5);
                if (CsHeard("what's my cs per minute?", false)) throw new Exception("An old game answered his CS question.");
                lastLive = null;

                if (CsHud.Label(7.83) != "CS/MIN 7.8" || CsHud.Label(-1) != "CS/MIN 0.0") throw new Exception("The strip reads " + CsHud.Label(7.83));
                Point spot = CsHud.Spot(new Rectangle(1920, 0, 1920, 1080), new Size(100, 30));
                if (spot.X != 1920 || spot.Y != 0) throw new Exception("The strip sits at " + spot + " instead of the game's very top-left corner.");
                lastLiveAt = DateTime.Now.AddSeconds(-2);
                if (HudMissed()) throw new Exception("One missed read of the live data hid the strip (it flickered).");
                lastLiveAt = DateTime.Now.AddSeconds(-HudGrace - 1);
                if (!HudMissed()) throw new Exception("The strip stayed up with no live data for seconds.");
                Rectangle area = Stage.Main.WorkingArea;
                Rectangle game = new Rectangle(area.Left, area.Top, Math.Min(1280, area.Width), Math.Min(720, area.Height));
                CsHud.GameWindow = delegate { return game; };
                Lockout.LeaguePhase = "InProgress";
                ShowHud(7.83);
                CreateParams style = (CreateParams)typeof(Form).GetProperty("CreateParams", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud, null);
                if (hud.Shown != "CS/MIN 7.8" || !game.Contains(hud.Bounds) || (style.ExStyle & 0x20) == 0 || (style.ExStyle & 0x08000000) == 0 || (style.ExStyle & 0x08) == 0)
                    throw new Exception("The strip wasn't shown click-through, topmost and inside the game: " + hud.Shown + " " + hud.Bounds);
                if (hud.Bounds.Location != game.Location) throw new Exception("The strip isn't in the game's top-left corner: " + hud.Bounds);
                int drawn = hud.Drawn;
                ShowHud(7.83);
                ShowHud(7.84);
                if (hud.Drawn != drawn || !hud.Visible) throw new Exception("The strip was redrawn with the same number (flicker).");
                ShowHud(8.2);
                if (hud.Drawn != drawn + 1 || hud.Shown != "CS/MIN 8.2") throw new Exception("A new number wasn't drawn.");
                CsHud.GameWindow = delegate { return Rectangle.Empty; };
                ShowHud(7.83);
                if (hud.Visible || hud.Shown != "") throw new Exception("The strip stayed up with the game not in front.");
                CsHud.GameWindow = delegate { return game; };
                ForkSettings.Current.CsHud = false;
                ShowHud(7.83);
                if (hud.Visible) throw new Exception("The strip showed with it switched off.");
                ForkSettings.Current.CsHud = true;
                Lockout.LeaguePhase = "ChampSelect";
                ShowHud(7.83);
                if (hud.Visible) throw new Exception("The strip showed outside a match.");

                string keptPrompt = box.Prompt;
                box.Prompt = "> you: ";
                talkState = TalkState.Thinking;
                int ticket = asking;
                GameLine("Nice kill, choom.");
                if (asking != ticket || heldGame != "Nice kill, choom.") throw new Exception("A game line cancelled his ask while she was thinking about it.");
                talkState = TalkState.Talking;
                if (GameHeldTick() != null || asking != ticket) throw new Exception("A held game line cut into her answer to him.");
                talkState = TalkState.Off;
                if (GameHeldTick() != "Nice kill, choom." || asking != ticket + 1 || heldGame != null) throw new Exception("A held game line wasn't said once she was done with his ask.");
                CloseTalk();
                box.Prompt = "> you: ";
                talkState = TalkState.Thinking;
                GameLine("Dragon in one minute.");
                heldGameAt = DateTime.UtcNow.AddSeconds(-GameHoldSeconds - 5);
                talkState = TalkState.Off;
                if (GameHeldTick() != null || heldGame != null) throw new Exception("A game line held too long was still said.");
                box.Prompt = "> sys: ";
                talkState = TalkState.Talking;
                ticket = asking;
                GameLine("Baron in one minute.");
                if (asking != ticket + 1) throw new Exception("A game line waited behind another system line.");
                CloseTalk();
                box.Prompt = keptPrompt;
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                CsHud.GameWindow = keptWindow;
                Lockout.LeaguePhase = keptPhase;
                if (hud != null) { hud.Dispose(); hud = null; }
                lastLive = null;
                CloseTalk();
                talkState = keptTalk;
                heard = keptHeard;
            }
            checks.Add("PASS cs per minute: \"what's my cs\" in a game is answered from the game's own number with no model (not before a minute, not from an old game, not over the ElevenLabs agent), her brief says cs/min is never a fraction of one, and a click-through topmost CS/MIN strip sits in the game's very top-left corner only while the match is in front and switched on, redrawn only when the number changes and kept through a missed read");
            checks.Add("PASS game lines wait for him: a game moment or objective call while she thinks about or answers his ask never cancels it; it is said once she is done (dropped after 20 s), and only his own ask holds it");
        }

        private static string SmokeDrake(int id, double time, string killer, string type)
        {
            return SmokeEvent(id, "DragonKill", time, "\"KillerName\":\"" + killer + "\",\"DragonType\":\"" + type + "\",\"Stolen\":\"False\"");
        }

        private void SmokeObjectives(List<string> checks)
        {
            Objectives calls = new Objectives();
            Func<string, double, string> at = delegate(string events, double time) { return calls.Turn(League.Parse<LiveData>(SmokeLive(events, time))); };
            string dragon = "Dragon spawns in one minute.", elder = "Elder Dragon spawns in one minute.", baronLine = "Baron spawns in one minute.";
            if (at("", 230) != null) throw new Exception("A dragon call came too early.");
            if (at("", 240) != dragon) throw new Exception("The first dragon call was: " + at("", 240));
            if (at("", 245) != null) throw new Exception("The dragon was called twice.");
            string grubs = at("", 420);
            if (grubs == null || !grubs.StartsWith("Voidgrubs spawn in one minute", StringComparison.Ordinal) || at("", 425) != null) throw new Exception("The Voidgrubs call was: " + grubs);
            if (at(SmokeDrake(1, 400, "sampleuser", "Fire"), 640) != dragon) throw new Exception("The dragon after a kill wasn't called five minutes on.");
            string soul = string.Join(",", new[] { SmokeDrake(1, 400, "sampleuser", "Fire"), SmokeDrake(2, 800, "amy", "Water"), SmokeDrake(3, 1210, "bob", "Air"), SmokeDrake(4, 1250, "sampleuser", "Earth"), SmokeDrake(5, 1600, "amy", "Chemtech") });
            string soulCall = at(soul, 1900);
            if (soulCall != elder) throw new Exception("The Elder after a soul wasn't called six minutes on: " + soulCall);
            string afterElder = at(soul + "," + SmokeDrake(6, 2000, "bob", "Elder"), 2300);
            if (afterElder != elder) throw new Exception("The Elder six minutes after an Elder wasn't called: " + afterElder);
            if (at("", 1140) != baronLine) throw new Exception("Baron at 20:00 wasn't called.");
            if (at(SmokeEvent(9, "BaronKill", 1300, "\"KillerName\":\"bob\""), 1600) != baronLine) throw new Exception("Baron six minutes after a kill wasn't called.");
            calls.Reset();
            if (at("", 270) != null) throw new Exception("Joining late called a spawn long past its call.");
            calls.Reset();
            if (calls.Turn(League.Parse<LiveData>(SmokeLive("", 240).Replace("\"gameMode\":\"CLASSIC\"", "\"gameMode\":\"ARAM\",\"mapNumber\":12"))) != null) throw new Exception("ARAM got a dragon call.");
            if (calls.Turn(League.Parse<LiveData>(SmokeLive("", 240).Replace("\"gameMode\":\"CLASSIC\"", "\"gameMode\":\"SWIFTPLAY\",\"mapNumber\":11"))) != null) throw new Exception("Swiftplay got Rift timers.");

            ForkSettings keptSettings = ForkSettings.Current;
            Func<string> keptFetch = LiveGame.Fetch;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            string keptBrief = League.Brief;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.Proactive = 0;
                List<string> asked = new List<string>();
                Quick.Post = delegate(string body, string key) { asked.Add(body); return Tuple.Create(200, SmokeReply("x")); };
                double time = 230;
                LiveGame.Fetch = delegate { return SmokeLive("{\"EventID\":0,\"EventName\":\"GameStart\"}", time); };
                EndLive();
                ObjectiveCalled = null;
                LiveTurn(true);
                time = 241;
                LiveTurn(true);
                if (ObjectiveCalled != dragon || asked.Count != 0) throw new Exception("The live dragon call was " + ObjectiveCalled + " with " + asked.Count + " model calls.");
                EndLive();
                ObjectiveCalled = null;
                ForkSettings.Current.ObjectiveCalls = false;
                LiveTurn(true);
                if (ObjectiveCalled != null) throw new Exception("An objective call came with calls switched off.");
                ForkSettings.Current.ObjectiveCalls = true;
                ForkSettings.Current.Paused = true;
                EndLive();
                LiveTurn(true);
                if (ObjectiveCalled != null) throw new Exception("An objective call came while she was paused.");
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                LiveGame.Fetch = keptFetch;
                Quick.Post = keptPost;
                League.Brief = keptBrief;
                EndLive();
                ObjectiveCalled = null;
                live = false;
            }
            checks.Add("PASS objective calls: a fixed line (no model) a minute before dragon (5:00, 5:00 after a kill), Elder (6:00 after a team's fourth drake or an Elder), Baron (20:00, 6:00 after a kill) and Voidgrubs (8:00), each once, never late after joining mid-game, only on Summoner's Rift classic, and not when switched off or paused");
        }

        private static string SmokeAwaySession(int championOnPick, int intent, bool inProgress, long left)
        {
            return SmokeAwaySession(championOnPick, intent, inProgress, "\"adjustedTimeLeftInPhase\":" + left + ",");
        }

        private static string SmokeAwaySession(int championOnPick, int intent, bool inProgress, string timer)
        {
            return "{\"localPlayerCellId\":2,\"myTeam\":[{\"cellId\":1,\"championId\":0,\"championPickIntent\":7,\"assignedPosition\":\"top\"},{\"cellId\":2,\"championId\":0,\"championPickIntent\":" + intent + ",\"assignedPosition\":\"middle\"}]," +
                "\"theirTeam\":[{\"cellId\":5,\"championId\":238}],\"bans\":{\"myTeamBans\":[103],\"theirTeamBans\":[]}," +
                "\"actions\":[[{\"id\":9,\"actorCellId\":2,\"championId\":" + championOnPick + ",\"type\":\"pick\",\"completed\":false,\"isInProgress\":" + (inProgress ? "true" : "false") + "}]]," +
                "\"timer\":{" + timer + "\"phase\":\"BAN_PICK\"}}";
        }

        private void SmokeLeagueAway(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            League keptLeague = league;
            Func<long> keptIdle = LeagueIdle;
            Func<string, string> keptDownload = ProBuilds.Download;
            Func<DateTime> keptClock = LeagueClock;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.FavMid = new[] { "Ahri", "LeBlanc", "Orianna" };
                ProBuilds.Download = delegate { return "{\"data\":{\"getProChampionMatchList\":{\"matchList\":[]}}}"; };
                DateTime clock = new DateTime(2026, 10, 31, 20, 0, 0, DateTimeKind.Utc);
                LeagueClock = delegate { return clock; };
                long epoch = (long)(clock - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                List<string> sent = new List<string>();
                Dictionary<string, string> replies = new Dictionary<string, string>();
                replies["/lol-matchmaking/v1/ready-check"] = "{\"state\":\"InProgress\",\"playerResponse\":\"None\",\"timer\":3}";
                replies["/lol-champ-select/v1/pickable-champion-ids"] = "[61,103,7,134,86]";
                league = SmokeLeagueClient(sent, replies);
                long idle = 20000;
                LeagueIdle = delegate { return idle; };
                lock (whileAway) whileAway.Clear();

                if (ReadyTurn() != "accepted" || sent.Count != 1 || !sent[0].StartsWith("POST /lol-matchmaking/v1/ready-check/accept", StringComparison.Ordinal)) throw new Exception("The match wasn't accepted while he was away: " + string.Join(" || ", sent.ToArray()));
                sent.Clear();
                replies["/lol-matchmaking/v1/ready-check"] = "{\"state\":\"InProgress\",\"playerResponse\":\"Declined\"}";
                if (ReadyTurn() != null || sent.Count != 0) throw new Exception("She overrode his own decline.");
                replies["/lol-matchmaking/v1/ready-check"] = "{\"state\":\"InProgress\",\"playerResponse\":\"Accepted\"}";
                if (ReadyTurn() != null || sent.Count != 0) throw new Exception("She accepted twice.");
                replies["/lol-matchmaking/v1/ready-check"] = "{\"state\":\"InProgress\",\"playerResponse\":\"None\"}";
                idle = 3000;
                ForkSettings.Current.AcceptEveryPop = true;
                if (ReadyTurn() != "accepted" || sent.Count != 1) throw new Exception("Every pop (his pick 2026-11-02) wasn't accepted while he was at the PC.");
                sent.Clear();
                ForkSettings.Current.AcceptEveryPop = false;
                if (ReadyTurn() != null || sent.Count != 0) throw new Exception("She accepted while he was at the PC with \"only when I'm away\".");
                ForkSettings.Current.AwayAlways = true;
                if (ReadyTurn() != "accepted") throw new Exception("\"Always\" didn't accept while he was at the PC.");
                ForkSettings.Current.AwayAlways = false;
                ForkSettings.Current.AutoAccept = false;
                idle = 20000;
                sent.Clear();
                if (ReadyTurn() != null || sent.Count != 0) throw new Exception("She accepted with accepting switched off.");
                ForkSettings.Current.AutoAccept = true;

                if (BackTurn() != null) throw new Exception("She told him what she did before he was back.");
                idle = 0;
                string back = BackTurn();
                if (back != "While you were away I accepted the match." || BackTurn() != null) throw new Exception("Coming back was answered as: " + back);
                idle = 20000;

                EndAway();
                sent.Clear();
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 20000))) != null || sent.Count != 0) throw new Exception("She locked with plenty of time left.");
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, false, 5000))) != null || sent.Count != 0) throw new Exception("She locked when it wasn't his turn.");
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 5000))) != null || sent.Count != 0) throw new Exception("She locked with 5 s left while he was away; he may still change his mind.");
                string locked = LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 1500)));
                if (locked != "Orianna" || sent.Count != 1 || sent[0] != "PATCH /lol-champ-select/v1/session/actions/9 {\"championId\":61,\"completed\":true}")
                    throw new Exception("She locked " + locked + " (Ahri banned, LeBlanc claimed, so Orianna): " + string.Join(" || ", sent.ToArray()));
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 1200))) != null || sent.Count != 1) throw new Exception("She locked the same pick twice.");
                EndAway();
                sent.Clear();
                string stale = "\"adjustedTimeLeftInPhase\":25000,\"internalNowInEpochMs\":" + (epoch - 23500) + ",\"totalTimeInPhase\":30000,";
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, stale))) != "Orianna") throw new Exception("A timer snapshot 20 s old (25 s left then, 1.5 s now) didn't lock.");
                EndAway();
                sent.Clear();
                string stuck = "\"adjustedTimeLeftInPhase\":25000,\"totalTimeInPhase\":30000,";
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, stuck))) != null) throw new Exception("She locked at the start of his turn.");
                clock = clock.AddSeconds(28.5);
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, stuck))) != "Orianna") throw new Exception("A timer that never counts down kept her from locking after 28.5 s of his 30 s turn.");
                EndAway();
                sent.Clear();
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, ""))) != null) throw new Exception("She locked at once with no timer.");
                clock = clock.AddSeconds(28.5);
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, ""))) != "Orianna") throw new Exception("With no timer she never locked.");
                EndAway();
                sent.Clear();
                if (LockTurn(League.ParseSession(SmokeAwaySession(134, 134, true, 1500))) != "Syndra" || !sent[0].Contains("\"championId\":134")) throw new Exception("She didn't lock his own free hover.");
                EndAway();
                sent.Clear();
                idle = 3000;
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 5000))) != null || sent.Count != 0) throw new Exception("She locked with 5 s left while he was at the PC.");
                if (!LeagueFast) throw new Exception("With 5 s left on his pick she didn't poll the client faster.");
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 1500))) != null || sent.Count != 0) throw new Exception("She locked with 1.5 s left while he was at the PC.");
                string lastSecond = LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 900)));
                if (lastSecond != "Orianna" || sent.Count != 1 || sent[0] != "PATCH /lol-champ-select/v1/session/actions/9 {\"championId\":61,\"completed\":true}") throw new Exception("With 1 s left and him at the PC she didn't lock (a dodge): " + lastSecond + " / " + string.Join(" || ", sent.ToArray()));
                EndAway();
                sent.Clear();
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 20000))) != null || LeagueFast) throw new Exception("She polled fast with 20 s left.");
                EndAway();
                idle = 20000;
                ForkSettings.Current.AutoLock = false;
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 1500))) != null || sent.Count != 0) throw new Exception("She locked with locking switched off.");
                ForkSettings.Current.AutoLock = true;

                ForkSettings.Current.FavMid = new[] { "Ahri", "LeBlanc" };
                EndAway();
                List<string> asked = new List<string>();
                ProBuilds.Download = delegate(string body) { asked.Add(body); return "{\"data\":{\"getProChampionMatchList\":{\"matchList\":[{\"championId\":103},{\"championId\":86},{\"championId\":86},{\"championId\":238}]}}}"; };
                string fromPros = LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 1500)));
                if (fromPros != "Garen" || asked.Count != 1 || !asked[0].Contains("\"role\":\"mid\"") || !sent[0].Contains("\"championId\":86")) throw new Exception("With his favourites gone she locked " + fromPros + " instead of what pros play mid.");
                EndAway();
                sent.Clear();
                ProBuilds.Download = delegate { return "{\"data\":{\"getProChampionMatchList\":{\"matchList\":[]}}}"; };
                string anyFree = LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 1500)));
                if (anyFree != "Orianna" || sent.Count != 1) throw new Exception("With his favourites gone and no pro list she let the timer run out instead of locking a free champ: " + anyFree);
                EndAway();
                sent.Clear();
                replies["/lol-champ-select/v1/pickable-champion-ids"] = "[103,7]";
                if (LockTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 1500))) != null || sent.Count != 0) throw new Exception("She locked something with nothing free.");
                replies["/lol-champ-select/v1/pickable-champion-ids"] = "[61,103,7,134,86]";
                idle = 0;
                back = BackTurn();
                if (back != "While you were away I locked in Orianna and locked in Orianna and locked in Orianna and locked in Orianna and locked in Syndra and locked in Garen and locked in Orianna.") throw new Exception("Coming back after locks was answered as: " + back);
                idle = 20000;

                EndAway();
                sent.Clear();
                ForkSettings.Current.FavMid = new[] { "Ahri", "LeBlanc", "Orianna" };
                string rehover = RehoverTurn(League.ParseSession(SmokeAwaySession(0, 103, false, 20000)));
                if (rehover != "Orianna" || sent.Count != 1 || sent[0] != "PATCH /lol-champ-select/v1/session/actions/9 {\"championId\":61}") throw new Exception("His hover was banned and she didn't hover the next free favourite: " + rehover + " / " + string.Join(" || ", sent.ToArray()));
                if (RehoverTurn(League.ParseSession(SmokeAwaySession(0, 103, false, 19000))) != null || sent.Count != 1) throw new Exception("She hovered again for the same banned hover.");
                if (RehoverTurn(League.ParseSession(SmokeAwaySession(0, 61, false, 18000))) != null || sent.Count != 1) throw new Exception("She replaced her own free hover.");
                if (RehoverTurn(League.ParseSession(SmokeAwaySession(0, 134, true, 9000))) != null || sent.Count != 1) throw new Exception("She replaced his own free hover.");
                if (RehoverTurn(League.ParseSession(SmokeAwaySession(0, 0, true, 9000))) != "Orianna" || sent.Count != 2) throw new Exception("With his hover cleared she didn't hover a free favourite.");
                ForkSettings.Current.HoverFavorite = false;
                EndAway();
                if (RehoverTurn(League.ParseSession(SmokeAwaySession(0, 103, false, 20000))) != null || sent.Count != 2) throw new Exception("She hovered with hovering switched off.");
                ForkSettings.Current.HoverFavorite = true;

                EndAway();
                sent.Clear();
                idle = 3000;
                replies["/lol-matchmaking/v1/ready-check"] = "{\"state\":\"InProgress\",\"playerResponse\":\"None\"}";
                LeagueSocial.Queued = DateTime.UtcNow;
                if (ReadyTurn() != "accepted") throw new Exception("A queue she started for him wasn't accepted while he was at the PC.");
                replies["/lol-matchmaking/v1/ready-check"] = "{\"state\":\"InProgress\",\"playerResponse\":\"Declined\"}";
                sent.Clear();
                if (ReadyTurn() != null || sent.Count != 0) throw new Exception("She overrode his decline of a queue she started.");
                LeagueSocial.Queued = DateTime.UtcNow.AddHours(-1);
                replies["/lol-matchmaking/v1/ready-check"] = "{\"state\":\"InProgress\",\"playerResponse\":\"None\"}";
                if (ReadyTurn() != null || sent.Count != 0) throw new Exception("An old queue of hers still made her accept at the PC.");
                ForkSettings.Current.AcceptEveryPop = true;
                Func<string, bool> keptRunning = Lockout.Running;
                try
                {
                    Lockout.Running = delegate(string name) { return name == "deadlock"; };
                    Lockout.Recheck();
                    string steered = ForkTool("use_computer", new ToolArguments { Task = "Press Find Match in the ranked lobby that's already up, then accept the queue when it pops." });
                    if (steered != LeagueSocial.HandsOff) throw new Exception("With the lockout on, a cursor job to queue in League was refused instead of going back to the league tool: " + steered);
                }
                finally
                {
                    Lockout.Running = keptRunning;
                    Lockout.Recheck();
                }
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                league = keptLeague;
                LeagueIdle = keptIdle;
                ProBuilds.Download = keptDownload;
                LeagueClock = keptClock;
                LeagueSocial.Queued = DateTime.MinValue;
                EndAway();
                lock (whileAway) whileAway.Clear();
            }
            checks.Add("PASS away in League: while he is away (15 s without mouse or keys, or always) she accepts the match (never over his decline, once), and only with 2 seconds left on his pick (never earlier, so he can still change his mind) locks his own free hover, else down his favourites, else what pros play in his lane, else any free champ he owns, never with time left, off, or twice; back at the PC she tells him what she did");
            checks.Add("PASS no dodge in League: at the PC she locks only with 1 s left (polling the client every 250 ms in the last 5 s), a banned or cleared hover is swapped for his next free favourite (never his own free hover, once per change, off with hovering off), and a queue she started for him is accepted at the PC (never over his decline, not after half an hour)");
        }
    }
}
