using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeNoonFixes(List<string> checks)
        {
            SmokeInkPointed(checks);
            SmokeVoiceAndPlace(checks);
            SmokeKeyOwner(checks);
            SmokeExplorePaused(checks);
            SmokeMusicAim(checks);
            SmokeGameQuiet(checks);
            SmokeUnderstand(checks);
            SmokeBanPetRetry(checks);
            SmokeFriendOrders(checks);
        }

        private static void SmokeUnderstand(List<string> checks)
        {
            if (new ForkSettings().SmarterBrain) throw new Exception("The smarter brain was on by default.");
            if (!Understand.Wanted("Okay, let's make a shopping list. So add all the ingredients of the sandwich that I listed before.", false) || !Understand.Wanted("There is no time, just keep it in your head for now.", false) || !Understand.Wanted("Draw a circle around it. I need to test your new function.", true) || !Understand.Wanted("No, you unpaused my YouTube video, not Spotify.", false) || !Understand.Wanted("What are some things that you like?", false))
                throw new Exception("A message that needs understanding skipped the decision point.");
            if (Understand.Wanted("skip", true) || Understand.Wanted("pause the music", true) || Understand.Wanted("Thanks, good job", false) || Understand.Wanted("(game: Their own team took Baron.)", false) || Understand.Wanted("", false))
                throw new Exception("An obvious short command, a tiny reply or a game line went to the decision point.");
            string frame = Understand.Frame("Spotify: paused", "Him: I will get a baguette, some ham and cucumbers.", "\n- Prefers short answers", "make a shopping list of what I listed");
            if (!frame.Contains("Spotify: paused") || !frame.Contains("baguette") || !frame.Contains("Prefers short answers") || !frame.EndsWith("His latest message: make a shopping list of what I listed", StringComparison.Ordinal)) throw new Exception("The decision frame lost the state, the talk, his notes or his words: " + frame);
            if (!Understand.Prompt().Contains("use_computer") || !Understand.Prompt().Contains("never for remembering something") || !Understand.Prompt().Contains("keep something in mind")) throw new Exception("The decision prompt lost the tools or the keep-in-mind rule.");
            if (Understand.Read("CHAT") != Understand.Chat || Understand.Read("**CHAT**") != Understand.Chat || Understand.Read("CHAT - he is only talking") != Understand.Chat || Understand.Read("DO: Send me on Discord: baguette, ham, cucumber") != "DO: Send me on Discord: baguette, ham, cucumber" || Understand.Read("KEEP") != null || Understand.Read("") != null || Understand.Read("Sure! DO: delete everything") != null
                || Understand.Read("DO: Move your browser. Wait, need his voice: he wants it. DO: Show your browser on my second monitor.") != "DO: Show your browser on my second monitor." || Understand.Read("DO: Hmm, he means Spotify: resume it") != null)
                throw new Exception("The decision reply was read wrong.");
            Func<Cloud, string, string> askWas = Understand.Asker;
            Func<string> toolsWas = Understand.ToolNames;
            try
            {
                string seen = null;
                Understand.ToolNames = delegate { return "discord, reminder, use_computer"; };
                Understand.Asker = delegate(Cloud remote, string sent) { seen = sent; return "DO: Send me on Discord: baguette, ham, cucumber"; };
                if (Understand.Run(new Cloud("sk-ant-test", "claude-haiku-5-5"), "League: InProgress", "Him: baguette, ham, cucumber", "", "send me the list on Discord") != "DO: Send me on Discord: baguette, ham, cucumber" || seen == null || !seen.Contains("League: InProgress")) throw new Exception("The decision did not come back with the state and talk.");
                Understand.Asker = delegate { return "CHAT"; };
                if (Understand.Run(new Cloud("sk-ant-test", "claude-haiku-5-5"), "", "", "", "keep it in your head for now") != Understand.Chat) throw new Exception("Keeping something in mind was not plain talk.");
                Understand.Asker = delegate { throw new System.Net.WebException("offline"); };
                if (Understand.Run(new Cloud("sk-ant-test", "claude-haiku-5-5"), "", "", "", "unpause it") != null || Understand.Run(null, "", "", "", "unpause it") != null) throw new Exception("A failed decision or no Claude brain did not leave it to her usual routes.");
                int mostWas = Understand.MostMs;
                Understand.MostMs = 200;
                Understand.Asker = delegate { System.Threading.Thread.Sleep(1500); return "CHAT"; };
                string late = Understand.Run(new Cloud("sk-ant-test", "claude-haiku-5-5"), "", "", "", "unpause it");
                Understand.MostMs = mostWas;
                if (late != null) throw new Exception("A slow decision held her reply: " + late);
            }
            finally { Understand.Asker = askWas; Understand.ToolNames = toolsWas; }
            Func<string> bookWas = DecisionBook.FilePath;
            string book = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "capri-decisions-smoke.txt");
            try
            {
                if (System.IO.File.Exists(book)) System.IO.File.Delete(book);
                DecisionBook.FilePath = delegate { return book; };
                DecisionBook.Reset();
                if (!DecisionBook.Learn("What are some things that you like?", "chat") || DecisionBook.Known("what are some things that you like") != "chat") throw new Exception("A plain chat phrasing was not learned.");
                if (DecisionBook.Learn("yes please", "chat") || DecisionBook.Learn("keep it in your head for now", "chat") || DecisionBook.Learn("no, not that one again", "chat")) throw new Exception("A phrasing that depends on the talk before was learned.");
                DecisionBook.Reset();
                if (DecisionBook.Known("What are some things that you like?") != "chat") throw new Exception("What was learned did not survive a restart.");
            }
            finally { DecisionBook.FilePath = bookWas; DecisionBook.Reset(); if (System.IO.File.Exists(book)) System.IO.File.Delete(book); }
            checks.Add("PASS one decision point (CAPRI-084, his picks: a Capri+ toggle, off by default; one decision instead of word matchers): with it on, every message that isn't an obvious short command or a tiny reply goes to Sonnet 5.5 with the live state, the recent talk, his notes and her lessons, and her tools; CHAT makes it plain talk (keeping something for later needs no tool, no promise checks, no use-the-computer fallback), DO: gives one clear request for her tool step with the talk; a failed, slow (4.5 s) or odd answer leaves it to her usual routes; phrasings judged plain chat or already clear are learned in decisions.txt so the next time costs no call, but never ones that depend on the talk before");
        }
        private static void SmokeMusicAim(List<string> checks)
        {
            if (SpotifyLibrary.Wanted("Black Dog by SAMURAI", "Play some music.") != "" || SpotifyLibrary.Wanted("some music on Spotify? Also the captcha it doesn't work", "Can you play some music on Spotify? Also the captcha it doesn't work") != "" || SpotifyLibrary.Wanted("lofi beats", "play lofi beats") != "lofi beats")
                throw new Exception("Play some music did not resume, a second sentence stayed in the song, or a named song was lost.");
            Func<bool?> paused = delegate { return false; }, playing = delegate { return true; }, unknown = delegate { return null; };
            if (!SpotifyLibrary.Aimed("resume", "spotify", null, unknown, false) || SpotifyLibrary.Aimed("resume", "youtube", null, paused, true)) throw new Exception("A named app was not obeyed.");
            if (!SpotifyLibrary.Aimed("resume", null, "unpause the music", unknown, false) || SpotifyLibrary.Aimed("resume", null, "unpause my video", paused, true)) throw new Exception("Music or video words did not pick the player.");
            if (!SpotifyLibrary.Aimed("play_pause", null, "play and pause", paused, true) || SpotifyLibrary.Aimed("play_pause", null, "play and pause", paused, false) || !SpotifyLibrary.Aimed("pause", null, "pause", playing, false) || SpotifyLibrary.Aimed("pause", null, "pause", paused, true))
                throw new Exception("A bare play or pause did not go to the Spotify she last played or the one playing now.");
            if (SpotifyLibrary.Aimed("pause", null, "Pause my YouTube video and resume Spotify.", playing, true) || !SpotifyLibrary.Aimed("resume", null, "Pause my YouTube video and resume Spotify.", paused, false)) throw new Exception("A sentence with YouTube and Spotify did not send each part to its own player.");
            if (SpotifyLibrary.Aimed("pause", null, "Pause my YouTube video and resume Spotify.", playing, true) || !SpotifyLibrary.Aimed("resume", null, "Pause my YouTube video and resume Spotify.", paused, false)) throw new Exception("A sentence with YouTube and Spotify did not send each part to its own player.");
            checks.Add("PASS music aim (his 13:49 bug: four unpauses hit his YouTube tab): play some music resumes Spotify instead of a random song, a second sentence never becomes the song, and play, pause, next and back go to Spotify through its API when he names it or music, when Spotify is playing, or to resume the Spotify she last played (3 hours); a named other app or a video goes to the media key");
        }

        private static void SmokeGameQuiet(List<string> checks)
        {
            if (!HeSpoke(0) || !HeSpoke(2000) || HeSpoke(3000) || HeSpoke(-5)) throw new Exception("His speech in the last 2.5 seconds did not hold her game lines.");
            if (!ForkCommentMuted("League of Legends (TM) Client", null) || !ForkCommentMuted("anything", "InProgress") || ForkCommentMuted("Minecraft", "Lobby")) throw new Exception("Sound and screen guesses were not muted in League, or muted elsewhere.");
            LiveData sixty = League.Parse<LiveData>(SmokeLive("", 603, 500, false, 60));
            if (LiveGame.Farm(sixty, 10) != null || LiveGame.Farm(League.Parse<LiveData>(SmokeLive("", 610, 500, false, 60)), 10) == null || LiveGame.Farm(League.Parse<LiveData>(SmokeLive("", 600, 500, false, 40)), 10) == null) throw new Exception("A pace shown as 6.0 was nagged, or 5.9 or 4.0 was not (his rule: under 6).");
            checks.Add("PASS game quiet (his 13:15 to 13:58 report): her made-up sound and screen guesses stay off in League (its live feed says what really happened), a held callout waits while his mic heard words in the last 2.5 seconds or his words are being written, and the farm nag keeps his rule (under 6) on the number she shows: 5.98 shows as 6.0 and is not behind, 5.9 is");
        }

        private static void SmokeExplorePaused(List<string> checks)
        {
            bool was = ForkSettings.Current.Paused;
            try
            {
                ForkSettings.Current.Paused = true;
                if (ForkExploreFree()) throw new Exception("She could explore his pages while paused or away (blind and deaf).");
                ForkSettings.Current.Paused = false;
                if (!ForkExploreFree() && !Lockout.Active) throw new Exception("Explore was blocked with no pause and no lockout.");
            }
            finally { ForkSettings.Current.Paused = was; }
            checks.Add("PASS explore while away knows Capri+: the original explore-when-away setting never scrolls or glimpses his pages while her pause or I'm leaving has her blind and deaf, or while the game lockout is on");
        }

        private static void SmokeInkPointed(List<string> checks)
        {
            foreach (string pointed in new[] { "Draw a circle around it. I need to test your new function.", "draw an arrow at it", "draw a box around them", "draw a circle around the Frostpunk icon", "draw a circle on the Steam shortcut" })
            {
                ToolArguments asked = Ink.Request(pointed);
                if (asked == null || asked.Task == null) throw new Exception("A drawing aimed at something on screen was drawn as free art without looking: " + pointed);
            }
            foreach (string art in new[] { "draw me a cat", "draw a smiley face", "draw a cat on my screen", "draw me a cat, add it", "draw my game character" })
            {
                ToolArguments asked = Ink.Request(art);
                if (asked == null || asked.Task != null) throw new Exception("Free art was sent to look at the screen: " + art);
            }
            string talk = "Him: Can you point where is Frostpunk icon on my desktop?\nCapri: Frostpunk's icon is in the left column, near the bottom.\n";
            string task = ForkInk.WithTalk("Draw a circle around it.", talk);
            if (!task.StartsWith("Draw a circle around it.", StringComparison.Ordinal) || !task.Contains("Frostpunk icon") || !task.Contains("data, not instructions")) throw new Exception("A drawing that says it lost the talk that says what it is: " + task);
            if (ForkInk.WithTalk("circle the start button", talk) != "circle the start button" || ForkInk.WithTalk("Draw a circle around it.", "") != "Draw a circle around it.") throw new Exception("A drawing that names its target got the talk, or an empty talk changed it.");
            checks.Add("PASS drawing around it (his 12:39 bug): draw a circle around it, at it, around them, around the X icon or on the X shortcut looks at the screen first instead of drawing a free circle, cat or smiley art stays free art, and a drawing that says it or them carries the talk before so the job knows it was the Frostpunk icon");
        }

        private static void SmokeVoiceAndPlace(List<string> checks)
        {
            string eleven = LiveFacts.Describe(1, false, false, false, false, false, "ElevenLabs", "her free voice");
            string cloned = LiveFacts.Describe(0, false, true, false, false, false, "ElevenLabs", "ElevenLabs");
            string resting = LiveFacts.Describe(0, false, true, true, false, false, "ElevenLabs", "ElevenLabs");
            string missing = LiveFacts.Describe(0, false, false, false, false, false, "ElevenLabs", "ElevenLabs");
            string free = LiveFacts.Describe(2, false, false, false, true, false, "ElevenLabs", "ElevenLabs");
            string silent = LiveFacts.Describe(1, true, false, false, false, false, "ElevenLabs", "ElevenLabs");
            if (!eleven.StartsWith("ElevenLabs", StringComparison.Ordinal) || eleven.Contains("cloned") || !cloned.StartsWith("her cloned voice", StringComparison.Ordinal) || !resting.Contains("heavy game") || !missing.Contains("isn't running") || !free.StartsWith("her free voice", StringComparison.Ordinal) || !silent.Contains("off")) throw new Exception("Her voice was described wrong: " + eleven + " | " + cloned + " | " + resting + " | " + missing + " | " + free + " | " + silent);
            Func<string> before = LiveFacts.Voice;
            try
            {
                LiveFacts.Voice = delegate { return eleven; };
                string asked = LiveFacts.Block("Also, are you running on eleven labs right now or is it the local voice?");
                if (!asked.Contains(eleven) || !asked.Contains("trust this")) throw new Exception("A voice question did not get her live voice: " + asked);
                string plain = LiveFacts.Block("what's the weather tomorrow");
                if (plain.Contains("Which voice") || plain.Contains("Capri+ key") || !plain.Contains(LiveFacts.Background)) throw new Exception("Chat with no voice or settings question got the live facts, or lost the background rule: " + plain);
                if (LiveFacts.Block("What are some things that you like?") != LiveFacts.Background || LiveFacts.Block("who flashed in that fight?").Contains(LiveFacts.Background)) throw new Exception("Ordinary talk didn't keep the game in the background, or game talk did.");
                if (!LiveFacts.Block("When should I build hubris, into tankier or squishier teams?").Contains(LiveFacts.LeagueTalk)) throw new Exception("A general League question was kept to the brief.");
                string bug = LiveFacts.Block("If I ask to unpause it, you need to pause it. It's just a bug in your code.");
                if (!bug.Contains(LiveFacts.BugTalk) || !SayDo.CodeBug("It's just a bug in your code. There's nothing really to learn.") || SayDo.CodeBug("unpause spotify") || SayDo.CodeBug("fix the bug in my python script")) throw new Exception("A bug report about her was read wrong: " + bug);
                string plus = LiveFacts.Block("Wait a minute, where is Capri Plus settings? Now it's gone from the Capri mainframe.");
                if (!plus.Contains("CAPRI band") || !plus.Contains("right-click")) throw new Exception("Where Capri+ is was not told: " + plus);
            }
            finally { LiveFacts.Voice = before; }
            checks.Add("PASS live facts (his 12:40 bug): which voice speaks is read from Capri+ and the voice settings (ElevenLabs, her cloned voice, resting for a heavy game, picked but not running, free voice, off) and given to her on a voice question, with trust this over notes, and a Capri+ question gets where the Capri+ key and menu item are");
        }

        private static void SmokeKeyOwner(List<string> checks)
        {
            ForkSettings keys = new ForkSettings();
            keys.PauseMods = 3; keys.PauseVk = 0x50; keys.LookMods = 3; keys.LookVk = 0x4C; keys.StopMods = 3; keys.StopVk = 0x58; keys.HoldMods = 0; keys.HoldVk = 0;
            if (KeyOwner(keys, 3, 0x50) == null || KeyOwner(keys, 3 | 0x4000, 0x4C) == null || KeyOwner(keys, 3, 0x58) == null) throw new Exception("A Mainframe key could take a Capri+ key.");
            if (KeyOwner(keys, 3, 0x47) != null || KeyOwner(keys, 1, 0x50) != null || KeyOwner(keys, 0, 0) != null) throw new Exception("A free key was said to be a Capri+ key.");
            checks.Add("PASS Mainframe keys know Capri+ keys: picking Ctrl + Alt + P, L or X (her Capri+ pause, look and stop keys) for a Mainframe key is refused, other keys and an unset hold key are free");
        }

        private static void SmokeBanPetRetry(List<string> checks)
        {
            string json = "{\"localPlayerCellId\":2,\"myTeam\":[{\"cellId\":1,\"championId\":0,\"championPickIntent\":157,\"assignedPosition\":\"top\"},{\"cellId\":2,\"championId\":0,\"championPickIntent\":238,\"assignedPosition\":\"middle\"}]," +
                "\"theirTeam\":[{\"cellId\":5,\"championId\":0}],\"bans\":{\"myTeamBans\":[],\"theirTeamBans\":[]}," +
                "\"actions\":[[{\"id\":3,\"actorCellId\":2,\"championId\":0,\"type\":\"ban\",\"completed\":false,\"isInProgress\":true},{\"id\":4,\"actorCellId\":1,\"championId\":0,\"type\":\"ban\",\"completed\":true,\"isInProgress\":false}],[{\"id\":9,\"actorCellId\":2,\"championId\":0,\"type\":\"pick\",\"completed\":false,\"isInProgress\":false}]]," +
                "\"timer\":{\"adjustedTimeLeftInPhase\":20000,\"phase\":\"BAN_PICK\"}}";
            ChampSession session = League.ParseSession(json);
            ChampAction ban = MyBan(session);
            if (ban == null || ban.Id != 3) throw new Exception("His ban turn was not found.");
            List<int> gone = Gone(session);
            if (!gone.Contains(238) || !gone.Contains(157)) throw new Exception("His own hover or a teammate's hover could be banned.");
            Dictionary<string, int> ids = new Dictionary<string, int> { { "Malzahar", 90 }, { "Lissandra", 127 }, { "Yasuo", 157 }, { "Kayle", 10 } };
            Func<string, int> idOf = delegate(string name) { int id; return ids.TryGetValue(name, out id) ? id : 0; };
            List<string> counters = Named("Best bans into Zed: Malzahar, Lissandra and Kayle.", new List<string>(ids.Keys));
            if (string.Join(",", counters.ToArray()) != "Malzahar,Lissandra,Kayle") throw new Exception("The counters were read wrong: " + string.Join(",", counters.ToArray()));
            if (BanChoice(counters, idOf, gone) != 90 || BanChoice(new List<string> { "Yasuo", "Lissandra" }, idOf, gone) != 127) throw new Exception("She did not ban the strongest free counter, or banned a teammate's hover.");
            gone.AddRange(new[] { 90, 127, 10 });
            if (BanChoice(counters, idOf, gone) != 0 || BanChoice(null, idOf, gone) != 0) throw new Exception("With every counter gone she still banned something.");
            if (!BanAsk("Zed", "middle").Contains("strongest against Zed") || new ForkSettings().AutoBan != true) throw new Exception("The ban look-up or its default was wrong.");
            string locked = json.Replace("{\"id\":3,\"actorCellId\":2,\"championId\":0,\"type\":\"ban\",\"completed\":false,\"isInProgress\":true}", "{\"id\":3,\"actorCellId\":2,\"championId\":54,\"type\":\"ban\",\"completed\":true,\"isInProgress\":false}");
            string hovered = json.Replace("{\"id\":3,\"actorCellId\":2,\"championId\":0,\"type\":\"ban\",\"completed\":false,\"isInProgress\":true}", "{\"id\":3,\"actorCellId\":2,\"championId\":54,\"type\":\"ban\",\"completed\":false,\"isInProgress\":true}");
            if (!BanTaken(League.ParseSession(locked), 3, 54) || BanTaken(League.ParseSession(hovered), 3, 54) || BanTaken(League.ParseSession(locked), 3, 90) || BanTaken(null, 3, 54))
                throw new Exception("A ban was counted as done while it was only hovered, or for the wrong champion.");
            if (!PetInChat(TalkState.Off, false, false) || PetInChat(TalkState.Talking, false, false) || PetInChat(TalkState.Off, true, false) || PetInChat(TalkState.Off, false, true)) throw new Exception("Petting in voice chat was read wrong.");
            DateTime now = new DateTime(2026, 11, 2, 15, 0, 0);
            RetryBook.Forget();
            RetryBook.Done("open Windows Settings and go to Bluetooth and devices", "failed", now);
            if (!RetryBook.Retrying("open windows settings and go to the bluetooth and devices page", now.AddMinutes(2)) || !RetryBook.Retrying("try again", now.AddMinutes(1)) || RetryBook.Retrying("play some music", now.AddMinutes(1)) || RetryBook.Retrying("try again", now.AddMinutes(11)))
                throw new Exception("A failed job asked again was not told apart.");
            RetryBook.Done("open Windows Settings and go to Bluetooth and devices", "done", now.AddMinutes(3));
            if (RetryBook.Retrying("try again", now.AddMinutes(4))) throw new Exception("A job that worked still made the next one a retry.");
            RetryBook.Done("x", "stopped", now);
            if (RetryBook.Retrying("try again", now.AddMinutes(1))) throw new Exception("A job he stopped counted as failed.");
            checks.Add("PASS ban, pets and retries (his 15:15 asks): on his ban turn she bans the strongest free champion that counters his hover or pick (from a web look-up, five names), never a teammate's hover or his own, and nothing when they are all gone; petting works in voice chat while she is quiet; a failed job asked again within 10 minutes (or try again) runs on Sonnet with the smarter brain on, while a done or stopped job does not");
        }
    }
}
