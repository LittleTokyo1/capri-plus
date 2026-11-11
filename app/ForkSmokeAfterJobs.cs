using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeAfterJobs(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPhase = Lockout.LeaguePhase, keptHeard = heard;
            Func<string, bool> keptRunning = Lockout.Running;
            Action<string> keptRun = AfterJobRun;
            TalkState keptTalk = talkState;
            List<string> ran = new List<string>();
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.LockLeagueClient = true;
                Lockout.Running = delegate { return false; };
                AfterJobs.Reset();
                AfterJobRun = delegate(string line) { ran.Add(line); };
                Lockout.LeaguePhase = "InProgress";
                Lockout.Recheck();
                string refused = Hands.Do("use_computer", new ToolArguments { Task = "open Steam and update Cyberpunk" });
                if (refused != Lockout.Refusal) throw new Exception("A PC job in a game wasn't refused: " + refused);
                DateTime now = DateTime.Now;
                if (AfterJobsAsk(now) != "") throw new Exception("She asked about a held job while the game was still on.");
                Hands.Do("use_computer", new ToolArguments { Task = "open Steam and update Cyberpunk" });
                Lockout.LeaguePhase = null;
                Lockout.Recheck();
                string asked = AfterJobsAsk(now);
                if (asked != " Before it you asked me to open Steam and update Cyberpunk. Want me to do it now?") throw new Exception("After the game she asked about the held job as: " + asked);
                if (AfterJobsAsk(now) != "") throw new Exception("A held job was asked about twice.");
                heard = null;
                if (!ForkHeard("yes", false) || ran.Count != 1 || ran[0] != "The game is over, so do now what I asked you before it: open Steam and update Cyberpunk.") throw new Exception("His yes after the game didn't run the held job: " + string.Join(" || ", ran.ToArray()));
                CloseTalk();
                Lockout.LeaguePhase = "InProgress";
                Lockout.Recheck();
                Hands.Do("use_computer", new ToolArguments { Task = "check my mail" });
                Lockout.LeaguePhase = null;
                Lockout.Recheck();
                AfterJobsAsk(now);
                if (!ForkHeard("no", false) || ran.Count != 1) throw new Exception("His no after the game still ran the held job.");
                CloseTalk();
                AfterJobs.Hold("use_computer", new ToolArguments { Task = "old job" }, now.AddHours(-4));
                if (AfterJobsAsk(now) != "") throw new Exception("A job held 4 hours ago was still asked about.");
                string strip = "The user wants a joke. I should answer in one short line. Why did the chicken cross the road?";
                ForkDiscordAnswer(asking, ref strip, TalkState.Talking);
                if (strip != "Why did the chicken cross the road?") throw new Exception("Her reasoning reached what she says: " + strip);
                if (!Heartbeat.PlanNotFact.Contains("never say its condition is true now")) throw new Exception("Heartbeat plan lines don't say they are plans.");
                checks.Add("PASS after-game jobs and honest lines: a PC job refused in a game is held (3 hours, once each), after the game she asks whether to do it now, his yes runs it through her normal path and his no drops it; her reasoning is cut from every line she shows or says, and heartbeat plan lines say they are plans, not facts");
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                Lockout.Running = keptRunning;
                Lockout.LeaguePhase = keptPhase;
                Lockout.Recheck();
                AfterJobRun = keptRun;
                AfterJobs.Reset();
                afterJobs = null;
                CloseTalk();
                talkState = keptTalk;
                heard = keptHeard;
            }
        }
    }
}
