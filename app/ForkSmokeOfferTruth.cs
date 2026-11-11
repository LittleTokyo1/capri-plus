using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeOfferTruth(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-offer-truth-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Heartbeat.Reset();
                Autonomy.Reset();
                AwayCheckIns.Reset();
                Autonomy.ForgetSpoke();
                DateTime t = new DateTime(2026, 11, 2, 18, 0, 0, DateTimeKind.Local);
                SmokeOffer("research", "Want me to look up the best Zed runes this patch?", t);
                Autonomy.Spoke("Nice kill on their mid, choom!", t.AddSeconds(40));
                if (Autonomy.Answered("okay", t.AddSeconds(45)) != Autonomy.None) throw new Exception("An okay to her game line counted as a yes to the offer before it.");
                DateTime t2 = t.AddHours(1);
                SmokeOffer("reminders", "Want a nudge at 21:00 to stretch?", t2);
                if (Autonomy.Answered("okay", t2.AddSeconds(20)) != Autonomy.Yes) throw new Exception("An okay right after her offer wasn't a yes.");
                DateTime t3 = t.AddHours(2);
                SmokeOffer("research", "Want me to read up on Dutch level 2 registration?", t3);
                if (Autonomy.Answered("thanks", t3.AddSeconds(10)) != Autonomy.None) throw new Exception("A thanks counted as a yes to an offer.");
                DateTime t4 = t.AddHours(3);
                SmokeOffer("research", "Want me to look up cheap flights to Vilnius for Christmas?", t4);
                if (Autonomy.Answered("no", t4.AddSeconds(10)) != Autonomy.No) throw new Exception("His no to an offer wasn't heard.");
                const string again = "Shall I check flights to Vilnius around Christmas?";
                if (!Autonomy.Muted("research", again, t4.AddDays(3)) || Autonomy.Muted("research", again, t4.AddDays(15)) || Autonomy.Muted("reminders", again, t4.AddDays(3)) || Autonomy.Muted("research", "Want me to find a new Zed skin?", t4.AddDays(3)))
                    throw new Exception("A declined idea wasn't muted for 14 days by its area and words, or another idea was muted with it.");
                string avoid = Autonomy.Avoid(t4.AddDays(1));
                if (!avoid.Contains("He said no to this lately; never offer it or anything like it again: Want me to look up cheap flights to Vilnius for Christmas?")) throw new Exception("Her idea pass isn't shown the idea he declined: " + avoid);
                DateTime t5 = t.AddHours(4);
                SmokeOffer("calendar", "Want the gym on your calendar for Friday?", t5);
                SmokeOffer("calendar", "Shall I block an hour for Dutch practice on Sunday?", t5.AddHours(1));
                Autonomy.Learned(t.AddDays(3));
                string bar = Autonomy.Avoid(t.AddDays(3));
                if (!bar.Contains("He ignored your last 2 calendar offers") || bar.Contains("last 2 research offers") || bar.Contains("last 2 reminders offers")) throw new Exception("Two ignored offers in a row didn't raise that area's bar, or another area's did: " + bar);
                checks.Add("PASS honest offer answers: an okay counts for an offer only when the offer was her last line (an okay to a game line after it is not a yes), a thanks is never a yes, his no mutes that idea (same area, 2 shared words) for 14 days and her idea pass is told what he declined, and two ignored offers in a row raise that area's bar");
            }
            finally
            {
                Autonomy.ForgetSpoke();
                Autonomy.Reset();
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                Heartbeat.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private static void SmokeOffer(string area, string text, DateTime when)
        {
            Autonomy.Record(area, text, when);
            Autonomy.Delivered(text, when);
            Autonomy.Spoke(text, when);
        }
    }
}
