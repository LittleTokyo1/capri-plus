using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeClipThat(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.ClipThat) throw new Exception("Clip that on the spot should be on by default.");
            string close;
            Tunable found = Tuning.Find("clip that", "off", out close);
            if (found == null || found.Field != "ClipThat") throw new Exception("Her setting tool can't find clip that by voice.");
            if (Hands.Tools.IndexOf("scroll that BD", StringComparison.Ordinal) < 0) throw new Exception("Her clip tool doesn't name scroll that BD.");
            ForkSettings was = ForkSettings.Current;
            try
            {
                ForkSettings.Current = new ForkSettings();
                foreach (string clip in new[] { "clip that", "clip it", "save a clip", "clip that capri", "Capri, clip that.", "Capri scroll that BD", "capri, scroll that b.d.", "scroll that, bd" })
                    if (Hands.ClipSeconds(clip) != -1) throw new Exception("A clip phrase wasn't heard: " + clip);
                if (Hands.ClipSeconds("clip the last 2 minutes") != 120) throw new Exception("A clip length was read wrong.");
                foreach (string ordinary in new[] { "that", "save that file", "save that document", "how do I clip that", "don't clip that", "scroll that", "scroll that page down", "scroll that bd page" })
                    if (Hands.ClipSeconds(ordinary) != 0) throw new Exception("Plain talk was taken as a clip: " + ordinary);
                if (!Hands.ClipNow("clip that", false) || !Hands.ClipNow("Capri scroll that BD", false)) throw new Exception("Clip that waited for a game to be seen.");
                if (Hands.ClipNow("save that", false) || !Hands.ClipNow("save that", true)) throw new Exception("Save that alone was a clip with no game in front, or not one with a game.");
                ForkSettings.Current.ClipThat = false;
                if (Hands.ClipNow("clip that", false) || !Hands.ClipNow("clip that", true)) throw new Exception("The switch off didn't bring back the game check.");
            }
            finally { ForkSettings.Current = was; }
            checks.Add("PASS clip that (096-15, slice 1): on by default and found by voice, clip that and scroll that BD (however her ears spell it) save on the spot with no game check or brain, save that alone still waits for a game, plain talk about scrolling or saving stays out, and the switch off brings back the game check");
        }
    }
}
