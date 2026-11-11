using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeLogFixes(List<string> checks, string folder)
        {
            SmokeFarmGuard();
            SmokeSavedToday(folder);
            SmokeFiller();
            SmokeReplyGate(folder);
            SmokeLockedPicks();
            SmokeFriendWatch(folder);
            SmokeScreenRepeat();
            SmokeHeavyGames();
            SmokeOwnReleases(checks);
            SmokeIdleLockoutLog(folder);
            SmokeReminderInGame(checks);
            SmokeAfterGame(checks, folder);
            SmokeLogRound(checks, folder);
            SmokeListen(checks, folder);
            if (Fresh(new List<string>()) != "") throw new Exception("A first game line was told about earlier lines.");
            string fresh = Fresh(new List<string> { "Yezzirrr, double kill, choom!", "Ace! Preem team fight, choom." });
            if (!fresh.Contains("Yezzirrr, double kill, choom!") || !fresh.Contains("Preem team fight") || !fresh.Contains("don't open the way those did")) throw new Exception("Her game lines are not told what she said before, so they all open the same way: " + fresh);
            checks.Add("PASS log fixes: no false \"her job was stopped by the game lockout\" line when no job runs, her game lines are told her last four so they don't all open with yezzirrr and choom, a live game logs its mode once, the same pro build pushed twice within 3 s goes into his client once, no 0 cs nag after 5 minutes or off Summoner's Rift, what she saved today lists real saves (newest kept, no old-chats noise), lone um/hmm never answered and okay/yeah only when nothing of hers is open, a typo'd \"reply him\" reaches her tools while a friend's message is fresh, who's online falls back to Discord when League can't answer, champ select says who is locked in, \"let me know when people get online\" watches once per friend for the day, her screen note keeps code windows out and stops a repeat, and a full-screen game that starves her cloned voice twice in 10 minutes is learned as a heavy game (browsers and listed games never)");
        }

        private static void SmokeReminderInGame(List<string> checks)
        {
            Func<string, bool> keptGame = Lockout.Running;
            try
            {
                Lockout.Running = delegate(string name) { return true; };
                Lockout.Recheck();
                if (!Lockout.Active) throw new Exception("The test game did not turn the lockout on.");
                if (!ScheduleOpen(false, false, false, TalkState.Off, false, false)) throw new Exception("A due reminder still waits for the game to end.");
                if (ScheduleOpen(false, false, false, TalkState.Off, false, true) || ScheduleOpen(false, false, true, TalkState.Off, false, false) || ScheduleOpen(true, false, false, TalkState.Off, false, false)) throw new Exception("A reminder talks over him, over her own line, or while she is paused.");
            }
            finally
            {
                Lockout.Running = keptGame;
                Lockout.Recheck();
            }
            string frame = SayDo.Judge;
            if (!frame.Contains("in his own words (put the bread in the oven), never a line of yours")) throw new Exception("The reminder text can still be her own line instead of what he asked for.");
            checks.Add("PASS reminders in a game: a due reminder is said during a League game (the lockout no longer holds it till the end), never over him, over her own line or while paused, and its text is what he asked for, not her reply");
        }

        private static void SmokeIdleLockoutLog(string folder)
        {
            string keptLog = ActivityLog.FilePath;
            bool keptLogging = ForkSettings.Current.LogActions;
            Func<string, bool> keptGame = Lockout.Running;
            string path = Path.Combine(folder, "idle-lockout.log");
            try
            {
                ActivityLog.FilePath = path;
                ForkSettings.Current.LogActions = true;
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                Computer.Arm();
                System.Threading.Thread.Sleep(200);
                string written = File.Exists(path) ? File.ReadAllText(path) : "";
                if (written.Contains("her job was stopped")) throw new Exception("With no job running, the lockout still logged that her job was stopped.");
                if (Computer.Why != LockoutStopped) throw new Exception("The lockout no longer stops a job: " + Computer.Why);
            }
            finally
            {
                Lockout.Running = keptGame;
                Lockout.Recheck();
                Computer.Arm();
                ForkSettings.Current.LogActions = keptLogging;
                ActivityLog.FilePath = keptLog;
            }
        }

        private static void SmokeFarmGuard()
        {
            if (LiveGame.Farm(League.Parse<LiveData>(SmokeLive("", 900, 500, false, 0)), 15) != null) throw new Exception("A 0 cs reading after 5 minutes was nagged about.");
            string arena = SmokeLive("", 600, 500, false, 20).Replace("\"gameMode\":\"CLASSIC\"", "\"gameMode\":\"CHERRY\"");
            if (LiveGame.Farm(League.Parse<LiveData>(arena), 10) != null) throw new Exception("Farm was nagged about outside Summoner's Rift.");
            if (LiveGame.Farm(League.Parse<LiveData>(SmokeLive("", 600, 500, false, 40)), 10) == null) throw new Exception("A real low farm on the Rift is no longer nudged.");
        }

        private static void SmokeSavedToday(string folder)
        {
            string vault = Path.Combine(folder, "logfix-vault");
            Directory.CreateDirectory(vault);
            StringBuilder log = new StringBuilder();
            for (int i = 0; i < 30; i++) log.Append("2026-11-01 09:" + (10 + i).ToString("00") + ":00  vault  old chats used  ->  3 lines\n");
            log.Append("2026-11-01 10:00:00  vault  notes used  ->  Capri.md\n");
            for (int i = 0; i < NotesTool.MostLogLines + 3; i++) log.Append("2026-11-01 11:" + (10 + i).ToString("00") + ":00  vault  note saved  ->  me.md: saved line " + i + "\n");
            log.Append("2026-11-01 22:00:05  tidy  filed 7 (Areas/My PC and games 4)  ->  kept 11\n");
            log.Append("2026-11-01 22:00:44  night review  saved 2, lessons merged 0  ->  5 calls\n");
            string path = Path.Combine(folder, "logfix-today.log");
            File.WriteAllText(path, log.ToString());
            string today = NotesTool.Run(vault, new ToolArguments { Action = "today" }, new DateTime(2026, 11, 1, 22, 5, 0), path);
            if (today.Contains("old chats used") || today.Contains("notes used") || !today.Contains("filed 7") || !today.Contains("night review saved 2") || !today.Contains("saved line " + (NotesTool.MostLogLines + 2)) || today.Contains("saved line 0\n")) throw new Exception("What she saved today was wrong: " + today);
        }

        private static void SmokeFiller()
        {
            foreach (string noise in new[] { "Um", "Hmm.", "うーん。", "Capri, um", "Uh", "Mm." })
                if (!VoiceFiller.Drop(noise, true)) throw new Exception("A lone sound was answered: " + noise);
            foreach (string reaction in new[] { "Okay.", "Yeah.", "Cool.", "Oh no.", "True that", "Oh my day.", "Mm-hmm.", "うん。", "嗯嗯。" })
            {
                if (!VoiceFiller.Drop(reaction, false)) throw new Exception("A lone reaction with nothing open was answered: " + reaction);
                if (VoiceFiller.Drop(reaction, true)) throw new Exception("A reaction was dropped although she was waiting for an answer: " + reaction);
            }
            foreach (string again in new[] { "say that again I didint catch it", "Capri, say that again", "repeat that", "what did you just say?", "sorry, I didn't catch that", "can you repeat what you said" })
                if (!VoiceFiller.RepeatAsked(again)) throw new Exception("Asking her to say it again was missed: " + again);
            foreach (string other in new[] { "say hi to Rudce", "what did you save today", "repeat after me: hello", "what did he say", "play that again" })
                if (VoiceFiller.RepeatAsked(other)) throw new Exception("Another line was taken as asking her to repeat: " + other);
            string[] many = { "Can you repeat the last three lines you said? I I kind of forgot what we were talking about", "repeat the last 3 things you said", "what were the last two things you said", "say the last few lines again" };
            int[] counts = { 3, 3, 2, 3 };
            for (int i = 0; i < many.Length; i++)
                if (VoiceFiller.RepeatCount(many[i]) != counts[i]) throw new Exception("Asking for her last lines gave " + VoiceFiller.RepeatCount(many[i]) + " lines, not " + counts[i] + ": " + many[i]);
            if (VoiceFiller.RepeatCount("repeat that") != 1 || VoiceFiller.RepeatCount("play the last three songs") != 0 || VoiceFiller.RepeatCount("repeat the last 40 lines you said") != VoiceFiller.MostRepeated) throw new Exception("One line, a song ask or a huge number was counted wrong.");
            if (VoiceFiller.Repeated(new List<string> { "One.", "[note]", "Two.", "Three." }, 2) != "Two. Three." || VoiceFiller.Repeated(new List<string>(), 3) != null) throw new Exception("Her last lines were not given back in order.");
            foreach (string real in new[] { "Thank you. You're amazing.", "Okay uh queue me up for a ranked game.", "I didn't say nothing.", "What?", "skip", "go", "Yeah, can you open Spotify?", "" })
                if (VoiceFiller.Drop(real, false)) throw new Exception("A real line was dropped as filler: " + real);
            DateTime fillerNow = new DateTime(2026, 11, 3, 13, 29, 0);
            if (VoiceFiller.Fresh(fillerNow.AddMinutes(-8), fillerNow) || !VoiceFiller.Fresh(fillerNow.AddSeconds(-30), fillerNow) || VoiceFiller.Fresh(DateTime.MaxValue, fillerNow)) throw new Exception("An old question of hers still held a lone mm-hmm open.");
        }

        private static void SmokeReplyGate(string folder)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath, keptRoot = Vault.Root;
            Func<string> keptOnline = LeagueSocial.DiscordOnline;
            Func<League> keptClient = LeagueSocial.Client;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "logfix-fork.json");
                ForkSettings.Current.DiscordChannel = "chan1";
                ForkSettings.Current.DiscordOwner = "900";
                Vault.Root = null;
                DiscordInbox.Stored = Path.Combine(folder, "logfix-waiting.json");
                DiscordInbox.Clear();
                DateTime t = new DateTime(2026, 11, 1, 21, 46, 58);
                if (!DiscordInbox.Take(new GatewayMessage { Id = "lf1", Channel = "chan501", Author = "501", Name = "Rudce", Text = "you on tonight?" }, t)) throw new Exception("The smoke friend message was not taken.");
                string typo = "reply hikm whatever yuou want sm,th of your own accord";
                if (!DiscordInbox.Wants(typo, t.AddSeconds(26)) || !DiscordInbox.Wants("tell him I'm in", t.AddMinutes(2))) throw new Exception("A reply to the friend who just wrote did not reach her tools.");
                if (DiscordInbox.Wants("what's the weather tomorrow", t.AddMinutes(1)) || DiscordInbox.Wants(typo, t.AddMinutes(DiscordInbox.ReplyMinutes + 1))) throw new Exception("A line with no fresh friend message to answer was sent to her tools as a reply.");
                LeagueSocial.Client = delegate { return null; };
                LeagueSocial.DiscordOnline = delegate { return "On Discord now: Vaidas (online since 00:31)."; };
                string roster = LeagueSocial.Do(new ToolArguments { Action = "friends" });
                string named = LeagueSocial.Do(new ToolArguments { Action = "friends", Name = "Vaidas" });
                if (!roster.Contains("From Discord instead: On Discord now: Vaidas") || named.Contains("From Discord")) throw new Exception("Who's online did not fall back to Discord when League couldn't answer: " + roster + " | " + named);
            }
            finally
            {
                DiscordInbox.Clear();
                DiscordInbox.Stored = null;
                DiscordInbox.Forget();
                LeagueSocial.DiscordOnline = keptOnline;
                LeagueSocial.Client = keptClient;
                Vault.Root = keptRoot;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
            }
        }

        private static void SmokeLockedPicks()
        {
            DateTime pushed = new DateTime(2026, 11, 2, 0, 22, 41);
            Dictionary<int, DateTime> pushes = new Dictionary<int, DateTime>();
            if (PushAgain(pushes, 1, pushed) || !PushAgain(pushes, 1, pushed.AddMilliseconds(400)) || PushAgain(pushes, 2, pushed.AddMilliseconds(500)) || PushAgain(pushes, 1, pushed.AddSeconds(5)))
                throw new Exception("The same build pushed twice in a second went into his client twice, or a later or other push was dropped.");
            League fake = new League();
            fake.LoadNames(SmokeNames);
            string acted = SmokeSession.Replace("\"timer\"", "\"actions\":[[{\"id\":1,\"actorCellId\":5,\"championId\":238,\"type\":\"pick\",\"completed\":true},{\"id\":2,\"actorCellId\":2,\"championId\":103,\"type\":\"pick\",\"completed\":false},{\"id\":3,\"actorCellId\":6,\"championId\":157,\"type\":\"ban\",\"completed\":true}]],\"timer\"");
            string locked = fake.Locked(League.ParseSession(acted));
            if (!locked.Contains("Locked in for good") || !locked.Contains("Zed") || locked.Contains("Ahri") || locked.Contains("Yasuo") || !locked.Contains("only a hover")) throw new Exception("Locked picks were told wrong: " + locked);
            string hovering = fake.Locked(League.ParseSession(acted.Replace("\"completed\":true", "\"completed\":false")));
            if (!hovering.Contains("Nobody has locked in yet") || fake.Locked(League.ParseSession(SmokeSession)) != "" || fake.Locked(null) != "") throw new Exception("A draft with no lock-ins was told wrong: " + hovering);
        }

        private static void SmokeFriendWatch(string folder)
        {
            string keptRoot = Vault.Root;
            try
            {
                string vault = Path.Combine(folder, "logfix-people");
                Directory.CreateDirectory(Path.Combine(vault, "People"));
                File.WriteAllText(Path.Combine(vault, "People", "Vaidas.md"), "- Friend, plays League\n");
                File.WriteAllText(Path.Combine(vault, "People", "Pieter.md"), "- Friend from Lithuania\n");
                Vault.Root = vault;
                if (FriendWatch.Who("Can you let me know when people get online?") != "" || FriendWatch.Who("tell me when Vaidas comes online") != "Vaidas" || FriendWatch.Who("let me know if anyone is on") != "" || FriendWatch.Who("what's the weather tomorrow") != null || FriendWatch.Who("who from my friends is online?") != null)
                    throw new Exception("Asking to be told when friends come online was read wrong.");
                foreach (string other in new[] { "let me know when the stream is on", "tell me when the match is in progress", "let me know when Vaidas is in the lobby", "tell me when it's on sale", "let me know when the oven is on" })
                    if (FriendWatch.Who(other) != null) throw new Exception("A watch for something else was taken as a friend watch: " + other);
                PresenceFriend vaidas = new PresenceFriend { Id = "1", Name = "Vaidas" }, pieter = new PresenceFriend { Id = "2", Name = "Pieter" };
                DateTime t = new DateTime(2026, 11, 1, 16, 12, 57);
                string started = FriendWatch.Start("Can you let me know when people get online?", new List<PresenceFriend> { vaidas }, t);
                if (!started.Contains("any of your friends") || !started.Contains("Already on right now: Vaidas")) throw new Exception("Starting the friend watch said: " + started);
                if (FriendWatch.Arrived(new List<PresenceFriend> { vaidas }, t.AddMinutes(1)).Count != 0) throw new Exception("A friend already online when he asked was told again.");
                List<string> came = FriendWatch.Arrived(new List<PresenceFriend> { vaidas, pieter }, t.AddMinutes(2));
                if (came.Count != 1 || came[0] != "Pieter" || FriendWatch.Line(came) != "Pieter just came online on Discord.") throw new Exception("A friend coming online was not told once.");
                if (FriendWatch.Arrived(new List<PresenceFriend> { vaidas, pieter }, t.AddMinutes(3)).Count != 0) throw new Exception("A friend still online was told twice.");
                if (FriendWatch.Arrived(new List<PresenceFriend> { vaidas, pieter }, t.Date.AddDays(1).AddHours(1)).Count != 0 || FriendWatch.Active(t.Date.AddDays(1))) throw new Exception("The friend watch outlived the day.");
                FriendWatch.Start("tell me when Pieter comes online", new List<PresenceFriend>(), t);
                if (FriendWatch.Arrived(new List<PresenceFriend> { vaidas }, t.AddMinutes(1)).Count != 0) throw new Exception("A watch for one friend told about another.");
                came = FriendWatch.Arrived(new List<PresenceFriend> { vaidas, pieter }, t.AddMinutes(2));
                if (came.Count != 1 || came[0] != "Pieter" || FriendWatch.Active(t.AddMinutes(3))) throw new Exception("A watch for one friend did not tell once and end.");
            }
            finally
            {
                FriendWatch.Stop();
                Vault.Root = keptRoot;
            }
        }

        private static void SmokeOwnReleases(List<string> checks)
        {
            Func<string> keptFetch = OwnReleases.Fetch;
            try
            {
                OwnReleases.Fetch = delegate { return "{\"tag_name\":\"v0.7.9\",\"draft\":false,\"prerelease\":false,\"body\":\"## What's new\\n- **Filler** is skipped\\n- Friend watch\",\"assets\":[{\"id\":7,\"name\":\"Capri-Setup.exe\",\"browser_download_url\":\"https://github.com/" + OwnReleases.Repo + "/releases/download/v0.7.9/Capri-Setup.exe\",\"size\":1000},{\"id\":8,\"name\":\"SHA256SUMS.txt\",\"browser_download_url\":\"https://github.com/" + OwnReleases.Repo + "/releases/download/v0.7.9/SHA256SUMS.txt\",\"size\":80}]}"; };
                Release found = OwnReleases.Latest();
                if (found == null || found.Tag != "v0.7.9" || Updater.Installer(found) == null || OwnReleases.Notes != "What's new - Filler is skipped - Friend watch" || !IsNewer(found.Tag, new Version(0, 7, 8))) throw new Exception("Our own release was read wrong: " + (found == null ? "null" : found.Tag) + " | " + OwnReleases.Notes);
                OwnReleases.Fetch = delegate { return null; };
                if (OwnReleases.Latest() != null) throw new Exception("A missing releases repo was not read as no release.");
                if (!OwnReleases.Ours("/LittleTokyo1/capri-plus/releases/download/v0.7.9/Capri-Setup.exe") || OwnReleases.Ours("/LittleTokyo1/capri/releases/download/v1/Capri-Setup.exe") || OwnReleases.Ours("/lachydotmcg/capri-desktop/releases/download/v9/Capri-Setup.exe")) throw new Exception("A download outside our releases repo counted as ours.");
                long day = TimeSpan.TicksPerDay, now = 100 * day;
                if (OwnReleases.Due(0, now, 30) || !OwnReleases.Due(0, now, 60) || OwnReleases.Due(now - day + 1, now, 999) || !OwnReleases.Due(now - day, now, 999) || !OwnReleases.Due(now + 1, now, 999)) throw new Exception("The once-a-day update check timing is wrong.");
                foreach (string ask in new[] { "install the update", "Capri, install the update.", "update yourself", "apply the update now" })
                    if (!OwnReleases.Asked(ask)) throw new Exception("Asking to install the update was missed: " + ask);
                foreach (string other in new[] { "what's in the update", "update my calendar", "check for updates on steam", "install spotify" })
                    if (OwnReleases.Asked(other)) throw new Exception("Another line was taken as installing the update: " + other);
                string told = OwnReleases.Told("0.7.9", "Filler is skipped");
                if (!told.StartsWith("Capri 0.7.9 is out: Filler is skipped.", StringComparison.Ordinal) || !told.Contains("install the update")) throw new Exception("Her update line was: " + told);
            }
            finally
            {
                OwnReleases.Fetch = keptFetch;
                OwnReleases.Notes = "";
            }
            checks.Add("PASS own releases: the updater reads only LittleTokyo1/capri-plus (tag, installer, checksum file, notes), a missing repo is no release, downloads outside it are not ours, a check at most once a day from a minute after start, \"install the update\" is heard and other update words are not, and her line names the version and notes and waits for his word");
        }

        private void SmokeSpotifyClosedAndLiked(SpotifyFake fake)
        {
            bool running = false, launched = false;
            try
            {
                SpotifyLibrary.AppRunning = delegate { return running; };
                SpotifyLibrary.Launch = delegate { launched = true; running = true; return "Opened Spotify."; };
                string inGame = Hands.Do("play_music", new ToolArguments { Song = "Red Horse", Kind = "song" });
                if (inGame != SpotifyLibrary.ClosedInGame || launched) throw new Exception("With Spotify closed in a game she opened it or claimed it played: " + inGame);
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                string started = Hands.Do("play_music", new ToolArguments { Song = "Red Horse", Kind = "song" });
                if (!launched || started != "Playing Red Horse on Spotify.") throw new Exception("With Spotify closed she did not open it first and then play: " + started);
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                running = true;
                fake.Playing = SpotifyFake.RedHorse;
                string tool;
                ToolArguments args;
                if (!Hands.Heard("add this song to my liked songs", out tool, out args) || tool != "spotify" || args.Action != "like") throw new Exception("\"Add this song to my liked songs\" did not reach Spotify's like: " + tool);
                string added = ForkTool(tool, args);
                bool liked;
                lock (fake.Liked) liked = fake.Liked.Contains("spotify:track:T9");
                if (added != "Added Red Horse to your Liked Songs." || !liked) throw new Exception("Liking a song in a game did not go through Spotify's API: " + added);
                string removed = ForkTool("use_computer", new ToolArguments { Task = "Remove the currently playing song from my Liked Songs in Spotify" });
                lock (fake.Liked) liked = fake.Liked.Contains("spotify:track:T9");
                if (removed != "Removed Red Horse from your Liked Songs." || liked) throw new Exception("A hands job to unlike a song was not turned into Spotify's API: " + removed);
                foreach (string chat in new[] { "i like this song", "I love this song", "play my liked songs", "what's in my liked songs" })
                    if (SpotifyLibrary.LikeAsked(chat) != null) throw new Exception("Talk was taken as liking a song: " + chat);
                if (SpotifyLibrary.LikeTask("Play my Liked Songs playlist on Spotify") != null || SpotifyLibrary.LikeTask("Add the currently playing song to Liked Songs in Spotify") != "like") throw new Exception("A hands job about liked songs was read wrong.");
                fake.Playing = null;
                if (!SpotifyLibrary.LikeNow(true).StartsWith("Nothing is playing", StringComparison.Ordinal)) throw new Exception("Liking with nothing playing was not said plainly.");
            }
            finally
            {
                fake.Playing = null;
                SpotifyLibrary.AppRunning = delegate { return true; };
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
            }
        }

        private static void SmokeHeavyGames()
        {
            try
            {
                HeavyGames.Forget();
                DateTime t = new DateTime(2026, 10, 31, 19, 56, 31);
                string[] known = { "Cyberpunk2077" };
                if (!HeavyGames.Starved("Her cloned voice didn't answer: The operation has timed out") || !HeavyGames.Starved("Her cloned voice couldn't make that line (busy, still loading or out of GPU memory).") || HeavyGames.Starved(Chatterbox.CutOff) || HeavyGames.Starved("Her cloned voice stopped answering.")) throw new Exception("A starved cloned voice was told apart wrongly.");
                if (HeavyGames.Learn("Expeditions", known, t) != null) throw new Exception("One timeout already marked a game as heavy.");
                if (HeavyGames.Learn("Expeditions", known, t.AddSeconds(32)) != "Expeditions") throw new Exception("A second timeout in the same game did not mark it heavy.");
                if (HeavyGames.Learn("chrome", known, t) != null || HeavyGames.Learn("chrome", known, t.AddSeconds(5)) != null) throw new Exception("A browser was learned as a heavy game.");
                if (HeavyGames.Learn("cyberpunk2077", known, t) != null || HeavyGames.Learn("cyberpunk2077", known, t.AddSeconds(5)) != null) throw new Exception("A game already on the list was learned again.");
                if (HeavyGames.Learn("Jeeps", known, t) != null || HeavyGames.Learn("Jeeps", known, t.AddMinutes(HeavyGames.WindowMinutes + 1)) != null || HeavyGames.Learn(null, known, t) != null) throw new Exception("Timeouts far apart, or no game in front, marked a game heavy.");
                string[] grown = HeavyGames.With(known, "Expeditions");
                if (grown.Length != 2 || grown[0] != "Cyberpunk2077" || grown[1] != "Expeditions") throw new Exception("The heavy game list did not grow by one.");
            }
            finally { HeavyGames.Forget(); }
        }

        private static void SmokeScreenRepeat()
        {
            string fresh = Brain.GlimpseFor(null), calm = Brain.GlimpseFor("Nice one, choom."), again = Brain.GlimpseFor("Claude Code's still sitting on the dance fix, Audit-54.");
            if (!fresh.Contains(Brain.CodeNote) || fresh.Contains(Brain.SameNote) || calm.Contains(Brain.SameNote) || !again.Contains(Brain.SameNote) || !again.StartsWith(Brain.GlimpseNote, StringComparison.Ordinal)) throw new Exception("Her screen note did not keep code windows out or stop a repeat: " + again);
        }
    }
}
