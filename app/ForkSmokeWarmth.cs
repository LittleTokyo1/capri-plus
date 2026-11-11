using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeWarmth(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.Warmth) throw new Exception("Warmer with you should be off by default.");
            string close;
            Tunable found = Tuning.Find("warmer with you", "on", out close);
            if (found == null || found.Field != "Warmth") throw new Exception("Her setting tool can't find warmer with you by voice.");
            if (Understand.Prompt().IndexOf("warmer with you", StringComparison.Ordinal) < 0 || Understand.Prompt().IndexOf("name needed in games", StringComparison.Ordinal) < 0) throw new Exception("Her understanding step does not know her setting names, so turn on <setting> can turn into a question.");
            if (Warmth.Line(false, Charm.Steps[5], false) != "")throw new Exception("The switch off still changed her.");
            if (Warmth.Line(true, Charm.Steps[0], false) != "") throw new Exception("Warmth showed at the first charm level.");
            string fond = Warmth.Line(true, Charm.Steps[2], false);
            string devoted = Warmth.Line(true, Charm.Steps[5], false);
            if (fond.Length == 0 || devoted.Length == 0 || fond == devoted) throw new Exception("Warmth does not grow with charm.");
            if (devoted.IndexOf("never explicit", StringComparison.Ordinal) < 0 || devoted.IndexOf("only for him", StringComparison.Ordinal) < 0) throw new Exception("The warmth line lost its limits.");
            if (Warmth.Line(true, Charm.Steps[5], true) != "") throw new Exception("Warmth showed with other people around.");
            Func<bool> keptAround = Warmth.OthersAround;
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-warmth-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Charm.Reset();
                ForkSettings on = new ForkSettings();
                on.Tidy();
                on.Warmth = true;
                ForkSettings.Current = on;
                Warmth.OthersAround = delegate { return false; };
                Charm.Set(Charm.Steps[5]);
                if (Charm.Block().IndexOf(devoted, StringComparison.Ordinal) < 0) throw new Exception("Warmth does not reach her chat prompt.");
                Warmth.OthersAround = delegate { return true; };
                if (Charm.Block().IndexOf(devoted, StringComparison.Ordinal) >= 0) throw new Exception("Warmth reached her prompt while others could hear.");
                Warmth.OthersAround = keptAround;
                on.VoiceInMic = true;
                if (!Warmth.OthersAround()) throw new Exception("Her voice in his mic did not count as others around.");
                on.VoiceInMic = false;
                Warmth.OthersAround = delegate { return false; };
                Charm.Set(Charm.Steps[1]);
                if (Charm.Block().IndexOf(devoted, StringComparison.Ordinal) >= 0) throw new Exception("A lower charm level kept the top warmth.");
            }
            finally
            {
                Warmth.OthersAround = keptAround;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                Charm.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
            checks.Add("PASS personality tuning: the Warmer with you switch is off by default and found by voice; on, her tone grows with the charm meter (none at the first level, more at each level), keeps its limits, reaches only her own chat with him and stays out while a call, a stream or her voice in his mic lets others hear");
        }
    }
}
