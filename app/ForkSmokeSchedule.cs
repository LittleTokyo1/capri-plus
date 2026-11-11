using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeSchedule(List<string> checks)
        {
            DateTime now = new DateTime(2026, 10, 30, 12, 0, 0);
            object[,] asked =
            {
                { "remind me at 12:40 to call mum", "2026-10-30 12:40", "", "call mum" },
                { "Capri, remind me in 20 minutes to check the oven", "2026-10-30 12:20", "", "check the oven" },
                { "remind me in an hour to stretch", "2026-10-30 13:00", "", "stretch" },
                { "remind me in half an hour to eat", "2026-10-30 12:30", "", "eat" },
                { "remind me tomorrow at 9 to send the form", "2026-10-31 09:00", "", "send the form" },
                { "remind me tomorrow to pay rent", "2026-10-31 09:00", "", "pay rent" },
                { "set a timer for 10 minutes", "2026-10-30 12:10", "", "your timer" },
                { "remind me every day at 8 am to take my pills", "2026-10-31 08:00", "daily", "take my pills" },
                { "every monday at 9 remind me to plan the week", "2026-11-02 09:00", "mon", "plan the week" },
                { "remind me at 5 to stretch", "2026-10-30 17:00", "", "stretch" },
                { "can you remind me at 11 pm to go to sleep", "2026-10-30 23:00", "", "go to sleep" },
                { "remind me at 9 am to ring the bank", "2026-10-31 09:00", "", "ring the bank" },
                { "remind me at 12.40 to call mum", "2026-10-30 12:40", "", "call mum" },
                { "remind me at 12 40 to call mum", "2026-10-30 12:40", "", "call mum" },
                { "Remind me at 5 p.m. to stretch", "2026-10-30 17:00", "", "stretch" },
                { "remind me at 5 P.M. to stretch", "2026-10-30 17:00", "", "stretch" },
                { "remind me in five minutes to stand up", "2026-10-30 12:05", "", "stand up" },
                { "remind me in twenty minutes to check the oven", "2026-10-30 12:20", "", "check the oven" },
                { "remind me in forty five minutes to leave", "2026-10-30 12:45", "", "leave" },
                { "remind me at midnight to sleep", "2026-10-31 00:00", "", "sleep" },
                { "remind me tomorrow at noon to eat", "2026-10-31 12:00", "", "eat" },
                { "remind me at half past three to eat", "2026-10-30 15:30", "", "eat" },
                { "remind me at quarter past 5 to stretch", "2026-10-30 17:15", "", "stretch" },
                { "remind me at quarter to six to leave", "2026-10-30 17:45", "", "leave" },
                { "remind me at half past 9 am to ring them", "2026-10-31 09:30", "", "ring them" },
                { "remind me on friday to pay rent", "2026-11-06 09:00", "", "pay rent" },
                { "remind me on saturday at 3 pm to call nan", "2026-10-31 15:00", "", "call nan" },
                { "remind me on sunday to relax", "2026-11-01 09:00", "", "relax" }
            };
            for (int i = 0; i < asked.GetLength(0); i++)
            {
                ScheduleItem item = Scheduler.Parse((string)asked[i, 0], now);
                string want = (string)asked[i, 1];
                if (item == null || item.Due.ToString("yyyy-MM-dd HH:mm") != want || item.Repeat != (string)asked[i, 2] || item.Text != (string)asked[i, 3]) throw new Exception("\"" + asked[i, 0] + "\" was read as " + (item == null ? "nothing" : item.Due.ToString("yyyy-MM-dd HH:mm") + " / " + item.Repeat + " / " + item.Text) + ", not " + want + " / " + asked[i, 2] + " / " + asked[i, 3]);
            }
            string[] other = { "what time is it", "tell him to call me at 5", "what is a reminder", "remind him to call me", "tomorrow I have an exam", "open Spotify at 5", "remind me", "set a reminder", "play some music in an hour", "in 5 minutes I will be there" };
            foreach (string said in other) if (Scheduler.Parse(said, now) != null) throw new Exception("\"" + said + "\" was taken as a reminder.");
            if (!Scheduler.Listing("what reminders do I have") || !Scheduler.Listing("show my reminders") || Scheduler.Listing("what is a reminder") || !Scheduler.Clearing("cancel all my reminders") || !Scheduler.Clearing("clear my reminders") || Scheduler.Clearing("tell him to cancel the reminders")) throw new Exception("Listing or clearing reminders was wrong.");

            string folder = Path.Combine(Path.GetTempPath(), "capri-schedule-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string file = Path.Combine(folder, "schedule.txt");
                Scheduler.Add(file, Scheduler.Parse("remind me at 12:40 to call mum", now));
                Scheduler.Add(file, new ScheduleItem { Due = now.AddHours(1), Repeat = "", Text = "leave\tnow" });
                Scheduler.Add(file, Scheduler.Parse("remind me every day at 8 am to take my pills", now));
                List<ScheduleItem> kept = Scheduler.Load(file);
                if (kept.Count != 3 || kept[1].Text.Contains("\t") || kept[2].Repeat != "daily") throw new Exception("The schedule was not stored and read back: " + kept.Count);
                if (Scheduler.Load(Path.Combine(folder, "nothing.txt")).Count != 0) throw new Exception("A missing schedule was not empty.");
                List<ScheduleItem> missed = new List<ScheduleItem>();
                if (Scheduler.Take(file, now.AddMinutes(30), missed).Count != 0) throw new Exception("A reminder came early.");
                List<ScheduleItem> due = Scheduler.Take(file, now.AddMinutes(41), missed);
                if (due.Count != 1 || due[0].Text != "call mum" || missed.Count != 0 || Scheduler.Load(file).Count != 2) throw new Exception("The 12:40 reminder was not taken once: " + due.Count);
                if (Scheduler.Take(file, now.AddMinutes(42), missed).Count != 0) throw new Exception("A reminder came twice.");
                List<ScheduleItem> late = Scheduler.Take(file, now.AddHours(8), missed);
                if (late.Count != 0 || missed.Count != 1 || missed[0].Text != "leave now" || Scheduler.Load(file).Count != 1) throw new Exception("A reminder hours late was not dropped as missed: " + late.Count + " / " + missed.Count);
                List<ScheduleItem> morning = Scheduler.Take(file, new DateTime(2026, 10, 31, 8, 1, 0), missed);
                List<ScheduleItem> next = Scheduler.Load(file);
                if (morning.Count != 1 || morning[0].Text != "take my pills" || next.Count != 1 || next[0].Due.ToString("yyyy-MM-dd HH:mm") != "2026-11-01 08:00") throw new Exception("A daily reminder did not come once and move to the next day: " + morning.Count + " / " + (next.Count > 0 ? next[0].Due.ToString("yyyy-MM-dd HH:mm") : "none"));
                Scheduler.Clear(file);
                if (Scheduler.Load(file).Count != 0) throw new Exception("Clearing the reminders left some.");
                string told = Scheduler.Describe(Scheduler.Parse("remind me at 12:40 to call mum", now), now);
                if (!told.Contains("12:40") || !told.Contains("call mum")) throw new Exception("A reminder was described wrongly: " + told);
            }
            finally
            {
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS scheduled reminders: remind me at a time, in minutes or an hour, tomorrow, every day or every weekday, set a timer, are read into a stored list (plain file, one line each); the wake comes once at the time, a reminder hours late is dropped as missed, a daily one moves to the next day, listing and clearing work, and chat, other people, questions and bare words are not taken as reminders; she only says the reminder, nothing is clicked, typed or sent");
        }
    }
}
