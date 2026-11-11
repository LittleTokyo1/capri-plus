using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal const int AfterJobAskMinutes = 10;
        private List<HeldJob> afterJobs;
        private DateTime afterJobsUntil;
        internal Action<string> AfterJobRun;

        private string AfterJobsAsk(DateTime now)
        {
            if (Lockout.Active) return "";
            List<HeldJob> jobs = AfterJobs.Take(now);
            if (jobs.Count == 0) return "";
            afterJobs = jobs;
            afterJobsUntil = now.AddMinutes(AfterJobAskMinutes);
            ActivityLog.Write("after game", "asked about held jobs", AfterJobs.Joined(jobs));
            return " Before it you asked me to " + AfterJobs.Joined(jobs) + ". Want me to do " + (jobs.Count == 1 ? "it" : "them") + " now?";
        }

        private bool AfterJobAnswered(string text)
        {
            List<HeldJob> jobs = afterJobs;
            if (jobs == null) return false;
            afterJobs = null;
            if (DateTime.Now > afterJobsUntil) return false;
            if (StudyDrafts.No.IsMatch(text ?? ""))
            {
                ActivityLog.Write("after game", "held jobs dropped", AfterJobs.Joined(jobs));
                Tell("(after your game)", "Okay, I dropped " + (jobs.Count == 1 ? "it" : "them") + ".");
                return true;
            }
            if (!StudyDrafts.Yes.IsMatch(text ?? "")) return false;
            if (Lockout.Active)
            {
                foreach (HeldJob job in jobs) AfterJobs.Hold(job.Tool, new ToolArguments { Task = job.Said }, DateTime.Now);
                Tell("(after your game)", "Another game's on, so I'll ask again after it.");
                return true;
            }
            string asked = "The game is over, so do now what I asked you before it: " + AfterJobs.Joined(jobs) + ".";
            ActivityLog.Write("after game", "held jobs run", AfterJobs.Joined(jobs));
            Action<string> run = AfterJobRun;
            if (run != null) run(asked);
            else Hear(asked);
            return true;
        }
    }
}
