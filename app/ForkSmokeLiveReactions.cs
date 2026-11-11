using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeLiveReactions(List<string> checks)
        {
            if (!LiveReactions.Held("League of Legends", null, false)) throw new Exception("Live reactions ran over a League game.");
            foreach (string phase in new[] { "GameStart", "InProgress", "Reconnect" })
                if (!LiveReactions.Held(null, phase, false)) throw new Exception("Live reactions ran in League's " + phase + " phase.");
            if (LiveReactions.Held("Minecraft", null, false) || LiveReactions.Held("RobloxPlayerBeta", "None", false) || LiveReactions.Held(null, "Lobby", false)) throw new Exception("Live reactions were held in another game.");
            if (!LiveReactions.Held("Minecraft", null, true)) throw new Exception("A live reaction could go up over an answer she's still giving.");
            string close;
            Tunable found = Tuning.Find("live reactions", "off", out close);
            if (found == null || found.Field != "pref:NoReactions" || !found.Toggle) throw new Exception("Her setting tool can't find live reactions by voice.");
            if (Array.IndexOf(ForkUpstreamSettings.Routed, "NoReactions") < 0) throw new Exception("Live reactions aren't routed into our build.");
            if (new Preferences().NoReactions) throw new Exception("Live reactions should be on by default outside League.");
            checks.Add("PASS live reactions (096-18): on by default in other games, never in League (its game or its in-game phases), never over an answer she's still giving, found by voice, routed into our build with a Capri+ switch");
        }
    }
}
