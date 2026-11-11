using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeOwnChats(List<string> checks)
        {
            if (!Brain.Persona.Contains(Fork.CounselRule)) throw new Exception("Her persona lost the honest-friend line.");
            if (!AwayCheckIns.FollowWhy.Contains("ill, stressed") || !AwayCheckIns.FollowWhy.Contains("Name what he told you") || !AwayCheckIns.FollowWhy.Contains("SKIP")) throw new Exception("Her follow-ups do not check in on him like a friend, or do not name what he said.");
            string keptPath = ForkSettings.FilePath, keptRoot = Vault.Root, folder = Path.Combine(Path.GetTempPath(), "capri-ownchats-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "Capri"));
            try
            {
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Vault.Root = folder;
                AwayCheckIns.Reset();
                DateTime afternoon = new DateTime(2026, 11, 4, 15, 0, 0);
                if (AwayCheckIns.PcPlan(afternoon, 8, folder) != null) throw new Exception("With nothing he told her, she still planned to start a chat at the PC.");
                File.WriteAllText(Path.Combine(folder, "Capri", "day.md"), "- I have a cold and feel awful today\n- school is stressing me out this week\n");
                CheckPlan follow = AwayCheckIns.PcPlan(afternoon, 8, folder);
                if (follow == null || follow.Kind != "follow" || follow.Key != "pcfollow:2026-11-04" || follow.Counted || !follow.Extra.Contains("I have a cold")) throw new Exception("No chat she starts herself at the PC from what he told her, or it would count as a check-in: " + (follow == null ? "none" : follow.Key));
                AwayCheckIns.Spoken(follow, "You said you have a cold, how are you holding up?", true);
                if (AwayCheckIns.PcPlan(afternoon.AddHours(1), 8, folder) != null) throw new Exception("She would start a second chat at the PC the same afternoon.");
            }
            finally
            {
                ForkSettings.FilePath = keptPath;
                Vault.Root = keptRoot;
                AwayCheckIns.Reset();
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
            checks.Add("PASS chats she starts herself: once an afternoon at the PC (14:00 to 20:00) she follows up on what he told her or checks in when he said he was ill, stressed or swamped, naming what he said, never from nothing; away it stays the Discord follow-up; her persona says when an idea is bad or a fact is wrong, once, then does what he decides");
        }
    }
}
