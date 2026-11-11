using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeReplay(List<string> checks)
        {
            string[] log =
            {
                "2026-11-01 10:13:23  autonomy  offered 1  ->  reminders: Want a nudge at six to ping Sam?",
                "2026-11-01 10:13:23  heartbeat  sent  ->  Want a nudge at six to ping Sam?",
                "2026-11-01 10:13:40  autonomy  offered with keys  ->  Want a nudge at six to ping Sam?",
                "2026-11-01 12:00:00  checkin  sent plan  ->  That was the plan for today: ping Sam.",
                "2026-11-01 13:13:23  heartbeat  sent  ->  Want a nudge at six to ping Sam?",
                "2026-11-01 18:10:36  discord  friend order done  ->  Oskar came online, so I messaged them: \"Hop on?\"",
                "2026-11-01 18:16:35  discord  friend watch told  ->  Oskar came online, so I messaged them: \"Hop on?\"",
                "2026-11-01 18:16:36  discord  friend online held through a game  ->  Oskar",
                "2026-11-01 18:16:50  discord  friend watch told  ->  Oskar came online during your game and is still on Discord.",
                "2026-11-01 18:20:00  look_at_screen  ->  a game",
                "2026-11-02 09:00:00  heartbeat  sent  ->  Want a nudge at six to ping Sam?"
            };
            List<ReplayCase> cases = ReplayEval.Cases(log);
            if (cases.Count != 6 || cases[0].Kind != "heartbeat sent" || cases[3].Person != "oskar" || cases[4].Person != "oskar") throw new Exception("The replay did not keep one case per line he saw (offers queued, the same line within two minutes, other areas left out): " + cases.Count);
            Dictionary<string, string> hand = new Dictionary<string, string>();
            hand[cases[1].Id] = "stale-done";
            ReplayEval.Label(cases, hand);
            if (cases[0].Label != "" || cases[1].Label != "stale-done" || cases[2].Label != ReplayEval.Duplicate || cases[3].Label != "" || cases[4].Label != ReplayEval.Duplicate || cases[5].Label != ReplayEval.Duplicate) throw new Exception("The replay labels were wrong: " + cases[0].Label + "/" + cases[1].Label + "/" + cases[2].Label + "/" + cases[3].Label + "/" + cases[4].Label + "/" + cases[5].Label);
            string score = ReplayEval.Score(cases);
            if (!score.Contains("stale + duplicate + cut-off: 4 of 6") || !score.Contains("repeats found by code: 3") || score.Contains("Sam") || score.Contains("Oskar")) throw new Exception("The replay score was wrong or printed his lines: " + score);
            checks.Add("PASS autonomy replay set: her own lines are read from the activity log once per line he saw, a repeat within a day and an arrival told twice are found by code, hand labels win, and the score prints counts and ids only");
        }
    }
}
