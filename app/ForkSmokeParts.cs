using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeMemoryRetry(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "capri-retry-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Names.Reset();
                if (!Vault.Open(folder, null)) throw new Exception("The memory vault did not open.");
                string core = System.IO.Path.Combine(folder, Vault.CoreName);
                Brain.MemoryPath = core;
                ActivityLog.FilePath = System.IO.Path.Combine(folder, "activity.log");
                string reply = null;
                PeopleNotes.Extract = delegate { return reply; };
                const string said = "I work nine to five every day at the rental company";
                if (PeopleNotes.Learn(null, said, null) != null || PeopleNotes.Pending != 1) throw new Exception("A pass with no reply from the model was dropped instead of being kept for a retry.");
                if (PeopleNotes.RetryDue(null, DateTime.Now) != null || PeopleNotes.Pending != 1) throw new Exception("A retry ran before its wait was over.");
                reply = "ME | Note | Works nine to five every day at the rental company";
                if (PeopleNotes.RetryDue(null, DateTime.Now.AddMinutes(3)) == null || PeopleNotes.Pending != 0 || !System.IO.File.ReadAllText(core).Contains("nine to five")) throw new Exception("The retry did not save his work hours.");
                reply = null;
                PeopleNotes.Learn(null, "I start work at eight on Saturdays at the shop", null);
                PeopleNotes.RetryDue(null, DateTime.Now.AddMinutes(3));
                if (PeopleNotes.Pending != 1) throw new Exception("A second failed try was not kept for a third.");
                PeopleNotes.RetryDue(null, DateTime.Now.AddMinutes(30));
                if (PeopleNotes.Pending != 1) throw new Exception("A pass was given up on after three tries instead of kept for a day.");
                Func<bool> keptStop = NightReview.Stop;
                NightReview.Stop = delegate { return false; };
                try { for (int night = 0; night < MemoryQueue.MostNights; night++) PeopleNotes.DrainNight(null, DateTime.Now.AddDays(1 + night), MemoryQueue.NightMost); }
                finally { NightReview.Stop = keptStop; }
                if (PeopleNotes.Pending != 0 || MemoryQueue.LostLines(DateTime.Now).Count != 1) throw new Exception("A pass that kept failing on " + MemoryQueue.MostNights + " nights was never given up on as lost.");
                if (!PeopleNotes.Instructions.Contains("shifts") || !PeopleNotes.Instructions.Contains("usually online")) throw new Exception("The memory pass is not told that work hours and schedules are worth keeping.");
            }
            finally
            {
                PeopleNotes.Extract = keptExtract;
                ActivityLog.FilePath = keptLog;
                ForkSettings.Current = keptSettings;
                Names.Reset();
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS memory retry: a memory pass the model did not answer is kept and tried again after 2 and then 8 minutes (not before), saves his work hours on the retry, keeps trying (waits capped at 30 minutes) and only gives up after a day, and the pass is told that work and school hours, shifts and when people are usually online are worth keeping");
        }

        private bool TookDiscordAnswer(string said)
        {
            return ForkDiscordAnswer(asking, ref said, TalkState.Talking);
        }

        private void SmokeParts(List<string> checks)
        {
            List<string> split = Discord.Parts("one\n[next]\ntwo [NEXT] three\n\n[next]\n[next]\nfour\n[next]\nfive\n[next]\nsix");
            if (split.Count != 4 || split[0] != "one" || split[1] != "two" || split[2] != "three" || split[3] != "four") throw new Exception("Her reply was not split into separate messages: " + string.Join(" | ", split.ToArray()));
            if (Discord.Parts("  ").Count != 0 || Discord.Parts(null).Count != 0 || Discord.Parts("[next]").Count != 0) throw new Exception("Nothing to send still made a message.");
            List<string> single = Discord.Parts("just one message, with no marker");
            if (single.Count != 1 || single[0] != "just one message, with no marker") throw new Exception("A reply with no marker was changed.");
            if (Discord.Plain("a\n[next]\nb") != "a b" || Discord.Plain("no marker here") != "no marker here") throw new Exception("The marker was not removed for the PC.");

            List<string> marked = Discord.Parts("Phantom Blade is at 42% [screenshot]");
            if (marked.Count != 1 || marked[0] != "Phantom Blade is at 42%" || !Discord.MarkedShot("at 42% [Screenshot]") || Discord.MarkedShot("a plain reply") || Discord.Plain("at 42% [screenshot]") != "at 42%") throw new Exception("The [screenshot] marker was not found, or not removed from the text.");

            discordLine = null;
            string said = "x [next] y";
            if (ForkDiscordAnswer(asking, ref said, TalkState.Talking) || said != "x y") throw new Exception("On the PC the marker would be shown or spoken: " + said);
            if (!Fork.HearingRules.Contains("[next]")) throw new Exception("Her rules do not tell her how to send several messages in a row.");
            checks.Add("PASS messages in a row: a [next] line between parts of her reply makes separate Discord messages (empty parts dropped, at most four, nothing changed without the marker), the marker is removed from what she shows or says on the PC, and her rules tell her how to use it");
        }
    }
}
