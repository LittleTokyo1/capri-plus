using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeWarmNote(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.AwayWarmth) throw new Exception("Warm notes when he is away should be on by default.");
            string close;
            Tunable found = Tuning.Find("warm notes when away", "off", out close);
            if (found == null || found.Field != "AwayWarmth") throw new Exception("Her setting tool can't find warm notes when away by voice.");
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-warmnote-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                AwayCheckIns.Reset();
                Charm.Reset();
                ForkSettings on = new ForkSettings();
                on.Tidy();
                ForkSettings.Current = on;
                DateTime day = new DateTime(2026, 11, 6, 15, 0, 0);
                CheckPlan plan = AwayCheckIns.WarmPlan(day, on, null, Charm.Steps[3]);
                if (plan == null || plan.Kind != "warm" || plan.Key != "warm:" + day.ToString("yyyy-MM-dd")) throw new Exception("No warm note plan at Close on an afternoon, or it is not once a day.");
                if (!plan.Counted) throw new Exception("A warm note does not count toward the day's check-ins.");
                if (plan.Why.IndexOf("never a guilt trip", StringComparison.Ordinal) < 0 || plan.Extra.IndexOf("charm meter", StringComparison.Ordinal) < 0) throw new Exception("The warm note lost its limits or her charm tone.");
                if (AwayCheckIns.GateKind(plan.Kind) != "checkin") throw new Exception("A warm note skips the proactive gate's check-in rules.");
                if (AwayCheckIns.WarmPlan(day, on, null, Charm.Steps[2]) != null || AwayCheckIns.WarmPlan(day, on, null, Charm.Start) != null) throw new Exception("A warm note came below Close, or at the charm everyone starts with.");
                if (AwayCheckIns.WarmPlan(day.Date.AddHours(23), on, null, Charm.Steps[5]) != null || AwayCheckIns.WarmPlan(day.Date.AddHours(7), on, null, Charm.Steps[5]) != null) throw new Exception("A warm note came at night.");
                on.AwayWarmth = false;
                if (AwayCheckIns.WarmPlan(day, on, null, Charm.Steps[5]) != null) throw new Exception("A warm note came with the switch off.");
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                AwayCheckIns.Reset();
                Charm.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
            checks.Add("PASS warm notes: on by default and found by voice; when he is away she may send one small warm note a day on Discord from Close up (never below or at the starting charm, never at night, never with the switch off), in her charm tone with its limits, counted with the day's check-ins and through the proactive gate like every check-in");
        }
    }
}
