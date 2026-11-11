using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeAfterGame(List<string> checks, string folder)
        {
            SmokeGameEnd();
            string keptLog = ActivityLog.FilePath;
            try
            {
                ActivityLog.FilePath = Path.Combine(folder, "after-game-activity.log");
                SmokeAfterGameStore(folder);
                SmokeAfterGameTool(folder);
            }
            finally
            {
                ActivityLog.FilePath = keptLog;
            }
            checks.Add("PASS reminders after the game: \"remind me after the game to ...\" waits for the end of the game he is in or his next one (League's end-of-game phase, or another game gone 15 s), never on a champ select flicker or a dodge, told once, listed and cleared like other reminders, never on Google Calendar, a job in it runs after the game, gone after 12 hours");
        }

        private static void SmokeGameEnd()
        {
            DateTime t = new DateTime(2026, 11, 3, 20, 0, 0);
            GameEnd flicker = new GameEnd();
            flicker.Tick(t, "League of Legends", "GameStart");
            for (int s = 1; s <= 40; s++)
                if (flicker.Tick(t.AddSeconds(s), null, "Lobby")) throw new Exception("A one-second game flicker counted as a game that ended.");
            GameEnd dodge = new GameEnd();
            for (int s = 0; s <= 120; s += 2) dodge.Tick(t.AddSeconds(s), "League of Legends", "ChampSelect");
            for (int s = 122; s <= 180; s += 2)
                if (dodge.Tick(t.AddSeconds(s), null, "Lobby")) throw new Exception("A dodge in champ select counted as a game that ended.");
            GameEnd match = new GameEnd();
            for (int s = 0; s <= 600; s += 2)
                if (match.Tick(t.AddSeconds(s), s < 20 ? null : "League of Legends", "InProgress")) throw new Exception("The game counted as ended while it was in progress.");
            if (match.Tick(t.AddSeconds(602), "League of Legends", "Reconnect")) throw new Exception("A reconnect counted as the end of the game.");
            if (!match.Tick(t.AddSeconds(604), null, "WaitingForStats")) throw new Exception("League's end-of-game phase did not end the game.");
            if (match.Tick(t.AddSeconds(606), null, "EndOfGame") || match.Tick(t.AddSeconds(640), null, "Lobby")) throw new Exception("One game ended twice.");
            GameEnd other = new GameEnd();
            for (int s = 0; s <= 100; s += 2) other.Tick(t.AddSeconds(s), "Cyberpunk2077", null);
            if (other.Tick(t.AddSeconds(102), null, null) || other.Tick(t.AddSeconds(110), null, null)) throw new Exception("Another game counted as ended before it was gone 15 seconds.");
            if (!other.Tick(t.AddSeconds(118), null, null)) throw new Exception("Another game closed for 16 seconds did not count as ended.");
            if (other.Tick(t.AddSeconds(130), null, null)) throw new Exception("A closed game ended twice.");
        }

        private static void SmokeAfterGameStore(string folder)
        {
            DateTime now = new DateTime(2026, 11, 3, 20, 0, 0);
            string[] said = { "remind me after the game to drink water", "remind me to drink water after this match", "remind me when the game ends to drink water", "remind me to drink water once my game is over", "remind me to drink water after the current League game ends" };
            foreach (string line in said)
            {
                ScheduleItem heard = Scheduler.Parse(line, now);
                if (heard == null || heard.Repeat != Scheduler.AfterGame || heard.Text != "drink water") throw new Exception("\"" + line + "\" was not an after-the-game reminder: " + (heard == null ? "none" : heard.Repeat + " / " + heard.Text));
            }
            ScheduleItem timed = Scheduler.Parse("remind me in 10 minutes to check when the game starts", now);
            if (timed == null || timed.Repeat == Scheduler.AfterGame || timed.Due != now.AddMinutes(10)) throw new Exception("A timed reminder that mentions a game became an after-the-game reminder.");
            string path = Path.Combine(folder, "after-game-schedule.txt");
            if (File.Exists(path)) File.Delete(path);
            Scheduler.Add(path, Scheduler.Parse(said[0], now));
            Scheduler.Add(path, new ScheduleItem { Due = now.AddHours(Scheduler.AfterGameHours), Repeat = Scheduler.AfterGame, Text = "open Spotify", Job = "open Spotify" });
            string listed = Scheduler.Describe(Scheduler.Load(path).Find(delegate(ScheduleItem item) { return item.Text == "drink water"; }), now);
            string listedJob = Scheduler.Describe(Scheduler.Load(path).Find(delegate(ScheduleItem item) { return item.Text == "open Spotify"; }), now);
            if (listedJob != "after your game: open Spotify (a job I run then)") throw new Exception("An after-the-game job was listed wrong: " + listedJob);
            if (listed != "after your game: drink water") throw new Exception("An after-the-game reminder was listed wrong: " + listed);
            List<ScheduleItem> missed = new List<ScheduleItem>();
            if (Scheduler.Take(path, now.AddHours(2), missed).Count != 0 || Scheduler.Take(path, now.AddHours(2), missed, true).Count != 0 || missed.Count != 0 || Scheduler.Load(path).Count != 2) throw new Exception("An after-the-game reminder was told or changed on the clock.");
            List<ScheduleItem> told = AfterGameTold(path, now.AddHours(1));
            if (told.Count != 1 || told[0].Text != "drink water") throw new Exception("The game's end did not tell the after-the-game reminder once.");
            List<ScheduleItem> jobs = Scheduler.Take(path, now.AddHours(1).AddMinutes(1), null, true);
            if (jobs.Count != 1 || jobs[0].Job != "open Spotify" || AfterGameTold(path, now.AddHours(1)).Count != 0 || Scheduler.Load(path).Count != 0) throw new Exception("A job after the game did not become a job that runs once the game is over.");
            Scheduler.Add(path, Scheduler.Parse(said[0], now));
            Scheduler.Take(path, now.AddHours(Scheduler.AfterGameHours + 1), missed);
            if (missed.Count != 1 || Scheduler.Load(path).Count != 0) throw new Exception("An after-the-game reminder with no game for 12 hours was kept.");
            File.Delete(path);
        }

        private static void SmokeAfterGameTool(string folder)
        {
            string keptStore = RemindTool.Store;
            Func<string, DateTime, int, string[]> keptCalendar = RemindTool.Calendar;
            Func<bool> keptOn = RemindTool.CalendarOn;
            int calendar = 0;
            try
            {
                RemindTool.Store = Path.Combine(folder, "after-game-tool.txt");
                if (File.Exists(RemindTool.Store)) File.Delete(RemindTool.Store);
                RemindTool.Calendar = delegate(string title, DateTime at, int minutes) { calendar++; return new[] { "x", "y" }; };
                RemindTool.CalendarOn = delegate { return true; };
                DateTime now = new DateTime(2026, 11, 3, 23, 30, 0);
                ToolArguments args = new ToolArguments { Action = "add", Start = "after_game", Text = "stretch" };
                if (!Hands.Valid("reminder", args)) throw new Exception("The reminder tool refused an after-the-game reminder.");
                string said = RemindTool.Do(args, now);
                if (said != "Set: after your game: stretch." || calendar != 0) throw new Exception("The reminder tool did not set an after-the-game reminder, or put it on the calendar: " + said);
                List<ScheduleItem> items = Scheduler.Load(RemindTool.Store);
                if (items.Count != 1 || items[0].Repeat != Scheduler.AfterGame) throw new Exception("The tool's after-the-game reminder was not stored as one.");
                if (!Scheduler.AfterGameAsked("after the game") || !Scheduler.AfterGameAsked("after this match") || Scheduler.AfterGameAsked("2026-11-03T23:45") || Scheduler.AfterGameAsked("")) throw new Exception("An after-the-game start was told apart wrong.");
                if (!SayDo.Judge.Contains("after_game")) throw new Exception("The say-do judge cannot set a reminder for after the game.");
                File.Delete(RemindTool.Store);
            }
            finally
            {
                RemindTool.Store = keptStore;
                RemindTool.Calendar = keptCalendar;
                RemindTool.CalendarOn = keptOn;
            }
        }
    }
}
