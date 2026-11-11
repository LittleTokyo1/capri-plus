using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeFreeKeysSwitch(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.FreeKeys) throw new Exception("Free keys should be off by default.");
            string close;
            Tunable found = Tuning.Find("free keys", "on", out close);
            if (found == null || found.Field != "FreeKeys") throw new Exception("Her setting tool can't find free keys by voice.");
            if (FreeKeys.MiddleShown(false, 0) || FreeKeys.MiddleShown(false, 2)) throw new Exception("With free keys off the MEDIUM row still shows.");
            if (!FreeKeys.MiddleShown(false, 1)) throw new Exception("A saved MEDIUM brain was hidden with free keys off.");
            if (!FreeKeys.MiddleShown(true, 0) || !FreeKeys.MiddleShown(true, 2)) throw new Exception("With free keys on the MEDIUM row stays hidden.");
            if (FreeKeys.Gap(true, 0) != 0 || FreeKeys.Gap(false, 1) != 0 || FreeKeys.Gap(false, 2) != FreeKeys.Row) throw new Exception("The hidden MEDIUM row did not close its gap.");
            foreach (string kind in new[] { "nvidia", "google", "openrouter" }) if (!Cloud.MediumKind(kind)) throw new Exception("MEDIUM doesn't take " + kind + " keys.");
            foreach (string kind in new[] { "openai", "anthropic", "deepseek" }) if (Cloud.MediumKind(kind)) throw new Exception("MEDIUM took a paid " + kind + " key.");
            if (Cloud.KindOf("AQ.free-test") != "google" || Cloud.KindOf("AIza-free-test") != "google" || Cloud.KindOf("sk-or-free-test") != "openrouter") throw new Exception("A Gemini or OpenRouter key wasn't recognised.");
            checks.Add("PASS free keys (096-19): off by default and found by voice; off, the MEDIUM INTELLIGENCE row and its Free key button are hidden unless a saved MEDIUM brain still uses it; on, MEDIUM takes free NVIDIA, Gemini (AIza and AQ. keys) and OpenRouter keys while paid OpenAI, Claude and DeepSeek keys go to MAX");
        }

        private void SmokeFreeKeysRow(List<string> checks)
        {
            bool was = ForkSettings.Current.FreeKeys;
            try
            {
                foreach (bool on in new[] { false, true })
                    foreach (int pick in new[] { 0, 1, 2 })
                    {
                        ForkSettings.Current.FreeKeys = on;
                        mainframe.ShowTiers(pick, null, null);
                        bool shown = FreeKeys.MiddleShown(on, pick);
                        if (mainframe.MiddleTierShown != shown) throw new Exception("The MEDIUM row showed " + mainframe.MiddleTierShown + " with free keys " + on + " on tier " + pick + ".");
                        if (!mainframe.TiersFit) throw new Exception("The BRAIN tiers don't fit with free keys " + on + " on tier " + pick + ": " + mainframe.TextStatus);
                        string crowded = mainframe.Crowded;
                        if (crowded != null) throw new Exception("The BRAIN panel overlaps with free keys " + on + " on tier " + pick + ": " + crowded);
                        if (mainframe.MaxRungStep != (shown ? 2 : 1)) throw new Exception("MAX INTELLIGENCE didn't move up into the hidden MEDIUM row's place.");
                    }
            }
            finally
            {
                ForkSettings.Current.FreeKeys = was;
                ShowTiers();
            }
            checks.Add("PASS free keys row (096-19): with free keys off MAX INTELLIGENCE moves up into the hidden MEDIUM row's place and the BRAIN tiers still fit with nothing overlapping (the Ollama hint too) on every tier; on, or with a saved MEDIUM brain, all three rows show");
        }
    }
}
