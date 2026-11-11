using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeExplained(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.ExplainDiagrams) throw new Exception("Diagrams when explaining should be on by default.");
            string close;
            Tunable found = Tuning.Find("diagrams when explaining", "off", out close);
            if (found == null || found.Field != "ExplainDiagrams") throw new Exception("Her setting tool can't find diagrams when explaining by voice.");
            ForkSettings keptSettings = ForkSettings.Current;
            Func<bool> keptAtPc = Explained.AtPc;
            bool atPc = true;
            try
            {
                ForkSettings on = new ForkSettings();
                on.Tidy();
                ForkSettings.Current = on;
                Explained.AtPc = delegate { return atPc; };
                DateTime start = new DateTime(2026, 11, 6, 15, 0, 0);
                string[] asks = { "how does dns work", "explain how a car engine works", "can you explain photosynthesis?", "what's the difference between tcp and udp", "walk me through how vaccines work" };
                for (int i = 0; i < asks.Length; i++)
                {
                    Explained.Reset();
                    ToolArguments drawn = Explained.Request(asks[i], start);
                    if (drawn == null || drawn.Action != "replace" || !Ink.Teaches(drawn.Description)) throw new Exception("An explaining question did not get a diagram she talks through: " + asks[i]);
                }
                string[] not = { "how do i open discord", "how are you", "how does your listen tool work?", "how does streamer mode work", "how does file search work", "explain yourself", "what's the difference between you and siri", "turn the volume up", "how do your live reactions work?" };
                foreach (string one in not)
                {
                    Explained.Reset();
                    if (Explained.Request(one, start) != null) throw new Exception("A diagram came for a question that is not a topic to explain: " + one);
                }
                Explained.Reset();
                if (Explained.Request("how does dns work", start) == null) throw new Exception("The first explaining question got no diagram.");
                if (Explained.Request("how does dns work", start.AddSeconds(5)) == null) throw new Exception("The same turn asked twice lost its diagram.");
                if (Explained.Request("how does a router work", start.AddMinutes(3)) != null) throw new Exception("Diagrams came more often than once in 10 minutes.");
                if (Explained.Request("how does a router work", start.AddMinutes(11)) == null) throw new Exception("No diagram after the 10 minutes rest.");
                Explained.Reset();
                atPc = false;
                if (Explained.Request("how does dns work", start) != null) throw new Exception("A diagram came while he was away from the PC, in a game or over a fullscreen app.");
                atPc = true;
                on.ExplainDiagrams = false;
                if (Explained.Request("how does dns work", start) != null) throw new Exception("A diagram came with the switch off.");
                on.ExplainDiagrams = true;
                on.Diagrams = false;
                if (Explained.Request("how does dns work", start) != null) throw new Exception("A diagram came with diagrams off.");
                on.Diagrams = true;
                Explained.Reset();
                string tool;
                ToolArguments args;
                if (!Hands.Direct("how does dns work", out tool, out args) || tool != "draw") throw new Exception("An explaining question did not reach her draw tool: " + tool);
            }
            finally
            {
                Explained.AtPc = keptAtPc;
                ForkSettings.Current = keptSettings;
                Explained.Reset();
            }
            checks.Add("PASS diagrams when explaining: on by default and found by voice; how does X work, explain X, walk me through X and what's the difference between X and Y get a diagram she talks through, straight to her draw tool; never for questions about her, her settings, him or his PC, small talk or tasks; only at the PC with no game or fullscreen app, at most once in 10 minutes (the same turn asked twice keeps it), never with the switch or diagrams off");
        }
    }
}
