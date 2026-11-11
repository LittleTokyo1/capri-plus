using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeOwnClock(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.OwnClock) throw new Exception("Her own clock should be on by default.");
            string close;
            Tunable found = Tuning.Find("her own clock", "off", out close);
            if (found == null || found.Field != "OwnClock") throw new Exception("Her setting tool can't find her own clock by voice.");
            if (Brain.Now().IndexOf("without searching for it", StringComparison.Ordinal) < 0) throw new Exception("Her time line doesn't tell her brain to answer from it.");
            ForkSettings was = ForkSettings.Current;
            try
            {
                ForkSettings.Current = new ForkSettings();
                if (Hands.Lookup("what time is it?", "Let me check, choom.") != null || Hands.Lookup("hey capri, what's the date today?", "I can't check that.") != null || Hands.Lookup("what time is it in tokyo?", "Let me check, choom.") == null)
                    throw new Exception("Asking the time still went to Google, or a city's time stopped searching.");
                if (Hands.ClockLine(new DateTime(2026, 11, 1, 15, 25, 0)) != "It's 3:25 PM on Sunday 1 November 2026.") throw new Exception("Her clock line reads wrong: " + Hands.ClockLine(new DateTime(2026, 11, 1, 15, 25, 0)));
                if (!Hands.ClockDodged("Hey Capri, what time is it?", "Let me check, choom.") || !Hands.ClockDodged("do you know what time it is?", "Time for you to get a watch.")
                    || Hands.ClockDodged("what time is it?", "3:25 PM, choom.") || Hands.ClockDodged("what day is it?", "Sunday, choom.") || Hands.ClockDodged("what time is the game?", "Let me check."))
                    throw new Exception("A hedge on the time wasn't caught, or a real answer was replaced.");

                List<string> asked = new List<string>();
                System.Net.Sockets.TcpListener fake = null;
                string ollamaWas = Brain.Ollama;
                try
                {
                    Brain.Ollama = FakeServer(new[] { SmokeSay("Let me check that, choom."), SmokeSay("Let me check that, choom."), SmokeSay("Let me check that, choom.") }, asked, out fake);
                    int acts = 0;
                    Brain clocked = new Brain();
                    clocked.Model = "clock-check";
                    clocked.Act = delegate { acts++; return "Opened."; };
                    string reply = clocked.Ask("Hey Capri, what time is it?");
                    if (!reply.StartsWith("It's ", StringComparison.Ordinal) || reply.IndexOf(" on ", StringComparison.Ordinal) < 0 || acts != 0) throw new Exception("Her brain hedged on the time and she didn't read her clock: " + reply + " / " + acts);
                }
                finally
                {
                    Brain.Ollama = ollamaWas;
                    if (fake != null) fake.Stop();
                }

                ForkSettings.Current.OwnClock = false;
                if (Hands.ClockDodged("Hey Capri, what time is it?", "Let me check, choom.")) throw new Exception("Her clock answered with the switch off.");
            }
            finally { ForkSettings.Current = was; }
            checks.Add("PASS her own clock (096-14): on by default and found by voice, what time or date it is never goes to Google (a city's time still does), a brain that hedges on the time gets her clock's line instead with no tool run, a real answer is kept, and the switch off leaves it to her brain");
        }
    }
}
