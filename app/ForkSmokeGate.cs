using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeProactiveGate(List<string> checks)
        {
            DateTime at = new DateTime(2026, 11, 3, 10, 0, 0);
            List<SaidLine> said = new List<SaidLine>();
            said.Add(new SaidLine { At = at, Kind = "heartbeat", Person = "", Text = "Your sister Mila's birthday is 6 November, a week from today, so sort a gift." });
            said.Add(new SaidLine { At = at, Kind = "heartbeat", Person = "", Text = "I tidied my notes: filed 8 lines." });
            said.Add(new SaidLine { At = at, Kind = "friend", Person = "people/jonas.md", Text = "Jonas came online, so I messaged him." });
            said.Add(new SaidLine { At = at, Kind = ProactiveGate.Reminder, Person = "", Text = "stand up" });
            Func<string, string, string, int, string> hold = delegate(string kind, string person, string text, int hours) { return ProactiveGate.Hold(said, new SaidLine { At = at.AddHours(hours), Kind = kind, Person = person, Text = text }); };
            if (hold("heartbeat", "", "Mila's birthday is 6 November, six days away, so plan something.", 13) != "same topic") throw new Exception("The same topic came back 13 hours later.");
            if (hold("heartbeat", "", "Mila's birthday is 6 November, so plan something.", 49) != null) throw new Exception("A topic stayed blocked after two days.");
            if (hold("heartbeat", "", "I tidied my notes: filed 3 lines.", 12) != null) throw new Exception("Her report of what she did was held as a repeat.");
            if (hold("friend", "people/jonas.md", "Jonas came online during your game.", 0) != "same person" || hold("friend", "people/jonas.md", "Jonas came online.", 25) != null) throw new Exception("The same friend was told twice, or never again.");
            if (hold("heartbeat", "", "I noticed Mila's birthday is 6 November, so plan something.", 13) != "same topic") throw new Exception("A nag that starts with I got past the topic rule as a report.");
            if (hold(ProactiveGate.Reminder, "", "stand up", 1) != null) throw new Exception("His own reminder was held.");
            if (hold(ProactiveGate.Brief, "", "Morning. Mila's birthday is in six days.", 20) != null || hold(ProactiveGate.Followup, "", "Did you sort the gift for Mila's birthday on 6 November?", 20) != null) throw new Exception("The morning brief or an after-event question was held as a repeat topic.");
            if (hold("heartbeat", "", "Want me to dig up what usually breaks Steam downloads?", 2) != null) throw new Exception("Two different offers were taken as one topic.");
            if (AwayCheckIns.GateKind("morning") != ProactiveGate.Brief || AwayCheckIns.GateKind("event") != ProactiveGate.Followup || AwayCheckIns.GateKind("plan") != "checkin") throw new Exception("A check-in kind reached the gate as the wrong kind.");

            string brief = "They're in a League of Legends match, 15 min in. They're Zed this game, level 9. Enemies: Zeri (3/0/1), Annie (1/3/0).";
            if (!ProactiveGate.AboutGame("Zeri wiped your Zed three times, want a matchup guide?", brief) || !ProactiveGate.AboutGame("Want me to look up what Annie builds into you?", brief))
                throw new Exception("An offer about his game was not let through in the game.");
            if (ProactiveGate.AboutGame("Want me to dig up how other cabin rental sites lay out their booking pages?", brief) || ProactiveGate.AboutGame("Want a nudge at half twelve so you wrap up this game and head to sleep?", brief) || ProactiveGate.AboutGame("Want me to dig up what usually breaks the build?", ""))
                throw new Exception("An offer that is not about his game would be said in the game.");
            string settingsWas = ForkSettings.FilePath, own = Path.Combine(Path.GetTempPath(), "capri-gate-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(own);
            try
            {
                ForkSettings.FilePath = Path.Combine(own, "fork.json");
                Heartbeat.Reset();
                Autonomy.Reset();
                string[] offers = { "Want me to dig up how other cabin rental sites lay out their booking pages?", "Zeri wiped your Zed three times, want a matchup guide?", "Want a nudge at half twelve so you wrap up this game and head to sleep?" };
                for (int i = 0; i < offers.Length; i++)
                {
                    Autonomy.Record("research", offers[i], at);
                    Heartbeat.Enqueue(offers[i], i == 0 ? at.AddHours(-1) : at, null);
                }
                string inGame = Heartbeat.Next(at, true, delegate(string text) { return ProactiveGate.InGame(text, brief); }, ProactiveGate.Game);
                string again = Heartbeat.Next(at, true, delegate(string text) { return ProactiveGate.InGame(text, brief); }, ProactiveGate.Game);
                int dropped = Heartbeat.DropSince(at.AddMinutes(-5), Autonomy.IsOffer);
                string after = Heartbeat.Next(at.AddMinutes(30), true), none = Heartbeat.Next(at.AddMinutes(30), true);
                if (dropped != 1 || none != null) throw new Exception("At the end of the game an offer made during it was not dropped, or one made before it was: " + dropped + " / " + none);
                if (inGame == null || !inGame.StartsWith("Zeri", StringComparison.Ordinal) || again != null || after == null || !after.Contains("cabin rental")) throw new Exception("In a game the offer about his game was not said, or the other offer did not wait for after the game: " + inGame + " / " + again + " / " + after);
            }
            finally
            {
                ForkSettings.FilePath = settingsWas;
                Heartbeat.Reset();
                Autonomy.Reset();
                try { Directory.Delete(own, true); } catch (IOException) { }
            }

            bool wasOff = ProactiveGate.Off;
            string gateFolder = Path.Combine(Path.GetTempPath(), "capri-gate-file-smoke-" + Guid.NewGuid().ToString("N"));
            ForkSettings.FilePath = Path.Combine(gateFolder, "fork.json");
            string path = ProactiveGate.FilePath;
            DateTime recent = DateTime.Now.AddMinutes(-10);
            try
            {
                ProactiveGate.Off = false;
                if (path != null && File.Exists(path)) File.Delete(path);
                if (ProactiveGate.Check("heartbeat", null, "Your train to Utrecht leaves at 17:10.", recent) != null || ProactiveGate.Check("heartbeat", null, "Your train to Utrecht leaves at 17:10.", recent.AddMinutes(5)) != "said already")
                    throw new Exception("The gate did not remember a line it let through.");
                ProactiveGate.Unsay("Your train to Utrecht leaves at 17:10.");
                if (ProactiveGate.Check("heartbeat", null, "Your train to Utrecht leaves at 17:10.", recent.AddMinutes(6)) != null) throw new Exception("A line that failed to send stayed counted as said.");
                ProactiveGate.Off = true;
                if (ProactiveGate.Check("heartbeat", null, "Your train to Utrecht leaves at 17:10.", recent.AddMinutes(7)) != null) throw new Exception("The gate ran while it was off for the smoke.");
            }
            finally
            {
                ProactiveGate.Off = wasOff;
                ForkSettings.FilePath = settingsWas;
                try { if (Directory.Exists(gateFolder)) Directory.Delete(gateFolder, true); } catch (IOException) { }
            }
            checks.Add("PASS one proactive gate: her own lines (heartbeat, check-ins) are held when the same line was said in 24 h, the same friend in 20 h or the same topic in 48 h (two shared rare words, offer template words left out); his own reminders, her reports of her own actions (tidied, filed, messaged; I noticed ... is still a repeat), the morning brief and after-event questions pass; in a game the heartbeat only takes offers about his game (League words or a champion in the live brief), the rest wait, and at the end of the game offers made during it are dropped while older ones are said; the gate remembers what it let through across restarts, forgets a line that failed to send, and stays off in the smoke");
        }
    }
}
