using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeGateSources(List<string> checks)
        {
            DateTime at = new DateTime(2026, 11, 4, 10, 0, 0);
            List<SaidLine> said = new List<SaidLine>();
            said.Add(new SaidLine { At = at, Kind = ProactiveGate.Care, Person = "", Text = "Grab a glass of water and drink it slowly." });
            said.Add(new SaidLine { At = at, Kind = ProactiveGate.Friend, Person = "Rudce", Text = "Rudce just came online on Discord." });
            Func<string, string, string, int, string> hold = delegate(string kind, string person, string text, int minutes) { return ProactiveGate.Hold(said, new SaidLine { At = at.AddMinutes(minutes), Kind = kind, Person = person, Text = text }); };
            if (hold(ProactiveGate.Care, "", "Grab a glass of water and drink it slowly.", 60) != null) throw new Exception("A care nudge said an hour ago held the next one.");
            if (hold("heartbeat", "", "Want me to set a water glass reminder for the drink you skipped?", 30) != null) throw new Exception("A care nudge blocked a heartbeat line as the same topic.");
            if (hold("heartbeat", "", "Want me to ping Rudce on Discord about the raid tonight?", 30) != null) throw new Exception("A friend heads-up blocked a heartbeat line as the same topic.");
            if (hold(ProactiveGate.Friend, "rudce", "Rudce came online during your game and is still on Discord.", 120) != "same person") throw new Exception("The same friend was told twice in a few hours.");
            if (!CareWatch.AtNight(CareKind.Late) || CareWatch.AtNight(CareKind.Water) || CareWatch.AtNight(CareKind.Meal)) throw new Exception("In quiet hours a care nudge other than the late-night one could be said, or the late one could not.");

            string settingsWas = ForkSettings.FilePath, rootWas = Vault.Root, own = Path.Combine(Path.GetTempPath(), "capri-gate-sources-" + Guid.NewGuid().ToString("N"));
            bool wasOff = ProactiveGate.Off;
            Directory.CreateDirectory(Path.Combine(own, "Capri"));
            try
            {
                ForkSettings.FilePath = Path.Combine(own, "fork.json");
                Vault.Root = own;
                ProactiveGate.Off = false;
                File.WriteAllText(Plans.PathIn(own), "# Plans and promises\n\n"
                    + "- [ ] Book the dentist ([[Journal/2026-11-01|1 Nov]]) \U0001F4C5 2026-11-06\n"
                    + "- [/] Hear back from DUO about the grant ([[Journal/2026-11-01|1 Nov]]) ⏳ 2026-11-20\n"
                    + "- [/] Hear back from the landlord ([[Journal/2026-11-01|1 Nov]]) ⏳ 2026-11-04\n"
                    + "- [ ] Sort the bank letter ([[Journal/2026-10-20|20 Oct]]) \U0001F4C5 2026-10-25 #stale\n"
                    + "- [x] Buy Rengar ([[Journal/2026-11-02|2 Nov]]) ✅ 2026-11-03\n"
                    + "- [-] Ping Rudiminas on Discord ([[Journal/2026-11-02|2 Nov]]) ❌ 2026-11-03\n");
                Dictionary<string, string> keys = Heartbeat.PlanKeys(own);
                string dentist = Key(keys, "Book the dentist"), duo = Key(keys, "DUO"), landlord = Key(keys, "landlord"), bank = Key(keys, "bank letter");
                string rengar = Plans.KeyOf("Buy Rengar"), ping = Plans.KeyOf("Ping Rudiminas on Discord");
                if (Plans.Current(own, dentist, at) != null || Plans.Current(own, landlord, at) != null) throw new Exception("An open plan, or a waiting plan on its follow-up day, was taken as no longer current.");
                if (Plans.Current(own, duo, at) != "waiting until 20 Nov" || Plans.Current(own, bank, at) != "plan stale" || Plans.Current(own, rengar, at) != "plan done" || Plans.Current(own, ping, at) != "plan dropped" || Plans.Current(own, "nothing like this", at) != "plan gone" || Plans.Current(own, null, at) != null)
                    throw new Exception("A plan that is done, dropped, stale, waiting or gone was still taken as current: " + Plans.Current(own, duo, at) + " / " + Plans.Current(own, bank, at) + " / " + Plans.Current(own, rengar, at) + " / " + Plans.Current(own, ping, at));

                string id;
                if (Heartbeat.Untag("Your dentist booking is due today, want to call them? [P1]", out id) != "Your dentist booking is due today, want to call them?" || id != "P1" || Heartbeat.Untag("Mail from the school about your room.", out id) != "Mail from the school about your room." || id != null)
                    throw new Exception("A plan tag on a heartbeat line was not taken off, or a line without one got a tag.");
                List<string> signals = Heartbeat.PlanSignals(new HeartState(), own, at);
                if (signals.Count != 1 || !signals[0].Contains("landlord") || !signals[0].EndsWith("[P3]", StringComparison.Ordinal) || keys["P3"] != landlord) throw new Exception("A plan signal for the heartbeat lacks its plan tag, or the tag points at another plan: " + string.Join(" | ", signals.ToArray()));

                Heartbeat.Reset();
                Heartbeat.Enqueue("Want me to nudge you about buying Rengar this afternoon?", at, null, true, rengar);
                Heartbeat.Enqueue("Your dentist booking is due today, want to call them?", at, null, true, dentist);
                string first = Heartbeat.Next(at.AddHours(2), true), second = Heartbeat.Next(at.AddHours(2), true);
                if (first == null || !first.Contains("dentist") || second != null) throw new Exception("A queued line about a plan that was done since was still said, or the one about an open plan was not: " + first + " / " + second);
                if (ProactiveGate.Check("heartbeat", null, "Want me to nudge you about buying Rengar this afternoon?", at.AddHours(3)) != null) throw new Exception("A line held for its old plan was still counted as said.");

                CheckPlan waiting = AwayCheckIns.DuePlan(at, own);
                if (waiting == null || waiting.Source != landlord || !waiting.Why.Contains("waiting")) throw new Exception("A waiting plan on its follow-up day was not raised as a check-in with its source, or broke the check-in: " + (waiting == null ? "none" : waiting.Why));

                ProactiveGate.Record(ProactiveGate.Friend, "Vaidas", "Vaidas just came online on Discord.", at);
                if (ProactiveGate.Check(ProactiveGate.Friend, "vaidas", "Vaidas came online during your game and is still on Discord.", at.AddHours(1)) != "same person") throw new Exception("A friend told on his own ask was told again by her heads-up an hour later.");
                ProactiveGate.Off = true;
                ProactiveGate.Record(ProactiveGate.Friend, "Pieter", "Pieter just came online on Discord.", at);
                ProactiveGate.Off = false;
                if (ProactiveGate.Check(ProactiveGate.Friend, "pieter", "Pieter just came online on Discord.", at.AddMinutes(5)) != null) throw new Exception("The gate noted a line while it was off for the smoke.");
            }
            finally
            {
                ProactiveGate.Off = wasOff;
                ForkSettings.FilePath = settingsWas;
                Vault.Root = rootWas;
                Heartbeat.Reset();
                try { Directory.Delete(own, true); } catch (IOException) { }
            }
            checks.Add("PASS gate sources and friends: care nudges are noted by the gate but never held as repeats and never hold another line (in quiet hours only the late-night nudge is said); friend heads-ups are held per friend for 20 h, a friend told on his own ask counts, and friend lines are no topic for other lines; a queued heartbeat line keeps the plan it came from and is dropped once that plan is done, dropped, stale, waiting before its follow-up day or gone; a waiting plan on its follow-up day reaches the check-ins with its source");
        }

        private static string Key(Dictionary<string, string> keys, string part)
        {
            foreach (KeyValuePair<string, string> pair in keys)
                if (pair.Value.IndexOf(Forgetting.Squash(part), StringComparison.Ordinal) >= 0) return pair.Value;
            throw new Exception("No plan key for " + part);
        }
    }
}
