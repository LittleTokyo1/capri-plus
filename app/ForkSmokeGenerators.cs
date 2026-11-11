using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeGenerators(List<string> checks)
        {
            foreach (string asked in new[] { "status?", "Status report", "capri, what's the status", "catch me up", "what did I miss?", "give me a rundown" })
                if (!StatusBoard.AskedFor(asked)) throw new Exception("\"" + asked + "\" was not taken as asking for a status.");
            foreach (string other in new[] { "what's the status of my Dutch registration", "my discord status is wrong", "set my status to away", "catch me up on the lore of Arcane" })
                if (StatusBoard.AskedFor(other)) throw new Exception("\"" + other + "\" was taken as a status rundown.");
            if (StatusBoard.Block("tell me a joke", DateTime.Now, null) != "") throw new Exception("A normal line got the status board.");
            DateTime ended = new DateTime(2026, 11, 4, 20, 0, 0);
            LastGame.Seen("Zed, level 16, 7/2/5, 214 cs at 31 min.", ended);
            if (!LastGame.Block("how did that game go?", ended.AddMinutes(10), false).Contains("214 cs") || LastGame.Block("how did that game go?", ended.AddHours(4), false) != "" || LastGame.Block("how did that game go?", ended.AddMinutes(10), true) != "" || LastGame.Block("what game should I play", ended.AddMinutes(10), false) != "")
                throw new Exception("The last game was not given when he asked about it after the game, or given too late, in a game, or for other talk.");
            LastGame.Forget();

            string keptPath = ForkSettings.FilePath, keptRoot = Vault.Root, folder = Path.Combine(Path.GetTempPath(), "capri-generators-" + Guid.NewGuid().ToString("N"));
            bool wasOff = ProactiveGate.Off;
            Directory.CreateDirectory(Path.Combine(folder, "Capri"));
            try
            {
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Vault.Root = folder;
                ProactiveGate.Off = true;
                AwayCheckIns.Reset();
                File.WriteAllText(Plans.PathIn(folder), "# Plans and promises\n\n- [ ] Book the dentist ([[Journal/2026-11-01|1 Nov]]) \U0001F4C5 2026-11-04\n");
                DateTime morning = new DateTime(2026, 11, 4, 9, 30, 0);
                CheckPlan first = AwayCheckIns.PcPlan(morning, 8, folder);
                if (first == null || first.Kind != "morning" || first.Counted || !first.Extra.Contains("Book the dentist")) throw new Exception("No morning brief at the PC, or it lacked the plan due today.");
                if (AwayCheckIns.PcPlan(new DateTime(2026, 11, 4, 7, 30, 0), 8, folder) != null || AwayCheckIns.PcPlan(new DateTime(2026, 11, 4, 15, 0, 0), 8, folder) != null) throw new Exception("A brief at the PC came in quiet hours or in the afternoon.");
                AwayCheckIns.Spoken(first, "Morning! The dentist booking is due today.", false);
                if (AwayCheckIns.PcPlan(morning.AddMinutes(10), 8, folder) == null) throw new Exception("A brief that was never said counted as given.");
                AwayCheckIns.Spoken(first, "Morning! The dentist booking is due today.", true);
                if (AwayCheckIns.PcPlan(morning.AddMinutes(20), 8, folder) != null) throw new Exception("The morning brief would be given twice.");
                CheckPlan evening = AwayCheckIns.PcPlan(new DateTime(2026, 11, 4, 21, 0, 0), 8, folder);
                if (evening == null || evening.Kind != "evening") throw new Exception("No night wrap-up at the PC in the evening.");
            }
            finally
            {
                ForkSettings.FilePath = keptPath;
                Vault.Root = keptRoot;
                ProactiveGate.Off = wasOff;
                AwayCheckIns.Reset();
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
            checks.Add("PASS generators: \"status?\" (and catch me up, what did I miss) gives her brain one board of the moment, other status talk does not; at the PC a morning brief (after quiet hours, before 13:00, with plans due) and a night wrap-up (20:00 to 23:00), never twice a day, shared with the Discord morning and evening check-ins, and counted as given only once she really said it; the after-game word only when he asks (\"how did that game go\" within 3 h gets the last live game feed, never in a game)");
        }
    }
}
