using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private readonly GameEnd gameEnd = new GameEnd();
        private bool afterGameDue;

        private void AfterGameTick()
        {
            if (testing) return;
            if (gameEnd.Tick(DateTime.Now, Lockout.Game, Lockout.LeaguePhase))
            {
                afterGameDue = true;
                afterGameAwayDone = false;
                ActivityLog.Write("after game", "game over", Lockout.LeaguePhase ?? "game closed");
            }
            if (!afterGameDue) return;
            if (AfterGameAway(DateTime.Now, Delivery.NotHome() && Delivery.Reachable(), Scheduler.FilePath)) return;
            if (!ScheduleMaySpeak()) return;
            afterGameDue = false;
            List<ScheduleItem> told = AfterGameTold(Scheduler.FilePath, DateTime.Now);
            string jobs = AfterJobsAsk(DateTime.Now);
            if (told.Count == 0 && jobs.Length == 0) return;
            string line = (told.Count > 0 ? "Game's over, choom. You wanted me to remind you: " + ScheduleTexts(told) + "." : "Game's over, choom.") + jobs;
            if (hidden) SetHidden(false);
            heard = "(reminder)";
            box.Prompt = "> sys: ";
            brain.Own("reminder", line);
            ActivityLog.Write("schedule", "told after the game", line);
            ScheduleParked(told, DateTime.Now);
            Answer(++asking, line, TalkState.Talking, chatting);
        }

        internal static List<ScheduleItem> AfterGameTold(string path, DateTime now)
        {
            List<ScheduleItem> told = new List<ScheduleItem>();
            foreach (ScheduleItem item in Scheduler.TakeAfterGame(path, now))
            {
                if (string.IsNullOrEmpty(item.Job)) { told.Add(item); continue; }
                Scheduler.Add(path, new ScheduleItem { Due = now, Text = item.Text, Job = item.Job });
                ActivityLog.Write("schedule", "job after the game", item.Job);
            }
            return told;
        }
    }
}
