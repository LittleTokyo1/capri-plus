using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeNameGate(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.NameInGames) throw new Exception("Name needed in games should be off by default.");
            string close;
            Tunable found = Tuning.Find("name needed in games", "on", out close);
            if (found == null || found.Field != "NameInGames") throw new Exception("Her setting tool can't find name needed in games by voice.");
            if (!NameGate.Held(true, true, true, "And also they've got a new video.") || !NameGate.Held(true, true, true, "Yeah. And you could also, uh")) throw new Exception("A line without her name got through in a game with the switch on.");
            foreach (string named in new[] { "Capri, pause the music", "yo capri what's my cs", "hey Kapri skip this song", "Pause music, Capri, can you hear me?", "Kathy, pause the music" })
                if (NameGate.Held(true, true, true, named)) throw new Exception("A line with her name was held: " + named);
            if (NameGate.Held(false, true, true, "And also they've got a new video.")) throw new Exception("The switch off still held a line.");
            if (NameGate.Held(true, false, true, "And also they've got a new video.")) throw new Exception("Outside a game a line without her name was held.");
            if (NameGate.Held(true, true, false, "pause the music")) throw new Exception("A typed, phone or Discord line was held for having no name.");
            if (!NameGate.Held(true, true, true, "capriccio is a word")) throw new Exception("A word that only starts with her name counted as her name.");
            checks.Add("PASS name needed in games: off by default and found by voice; with it on, in a game a spoken line without her name (the 15:50 lines) is held and one with Capri or Kapri anywhere, or a misheard name before a command, goes through; off, outside a game, or a typed, phone or Discord line changes nothing");
        }
    }
}
