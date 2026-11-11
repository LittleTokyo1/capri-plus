using System;
using System.Collections.Generic;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int AfterGameRetrySeconds = 60;
        private bool afterGameSending;
        private bool afterGameAwayDone;
        private DateTime afterGameRetryAt = DateTime.MinValue;

        internal static string AfterGameAwayLine(List<ScheduleItem> told)
        {
            return "Game's over. You wanted me to remind you: " + ScheduleTexts(told) + ".";
        }

        private bool AfterGameAway(DateTime now, bool away, string path)
        {
            if (afterGameSending) return true;
            if (!away) return false;
            if (afterGameAwayDone || now < afterGameRetryAt)
            {
                afterGameDue = AfterJobs.Waiting(now);
                return true;
            }
            List<ScheduleItem> told = AfterGameTold(path, now);
            afterGameAwayDone = true;
            afterGameDue = AfterJobs.Waiting(now);
            if (told.Count == 0) return true;
            string line = AfterGameAwayLine(told);
            afterGameSending = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool sent = false;
                try { sent = Delivery.Send(line); }
                catch (Exception error) { Brain.Log("schedule", -1, error.GetType().Name + ": " + error.Message); }
                Post(delegate { AfterGameSent(path, told, line, sent, DateTime.Now); });
            });
            return true;
        }

        private void AfterGameSent(string path, List<ScheduleItem> told, string line, bool sent, DateTime now)
        {
            afterGameSending = false;
            if (sent)
            {
                ActivityLog.Write("schedule", "sent after the game", line);
                ScheduleParked(told, now);
                brain.Own("reminder", "Sent to his phone on Discord: " + line);
                Digest.Add("remind", ScheduleTexts(told), now);
                return;
            }
            foreach (ScheduleItem item in told) Scheduler.Add(path, new ScheduleItem { Id = item.Id, Due = item.Due, Repeat = Scheduler.AfterGame, Text = item.Text });
            afterGameDue = true;
            afterGameAwayDone = false;
            afterGameRetryAt = now.AddSeconds(AfterGameRetrySeconds);
            ActivityLog.Write("schedule", "after the game retry", ScheduleTexts(told));
        }
    }
}
