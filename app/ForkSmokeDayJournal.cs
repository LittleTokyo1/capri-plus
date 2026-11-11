using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeDayJournal(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-day-page-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (!Vault.Open(folder, null)) throw new Exception("The day page vault did not open.");
                string chats = ChatArchive.FolderIn(folder);
                Directory.CreateDirectory(chats);
                File.WriteAllText(Path.Combine(chats, "2026-11-01.md"), "# 2026-11-01\n\n04:30 him: still up from last night\n09:00 him: morning, school at ten\n14:00 him: I bought Rengar after the game\n14:01 her: I ordered you a pizza, all done\n15:00 him: my password is hunter2\n16:00 him: (heartbeat)\n");
                File.WriteAllText(Path.Combine(chats, "2026-11-02.md"), "# 2026-11-02\n\n02:00 him: finished the BMC report late\n06:00 him: next day line\n");
                DateTime day = new DateTime(2026, 11, 1);
                string frame = DayJournal.Frame(folder, day);
                if (frame == null || !frame.Contains("14:00 him: I bought Rengar") || !frame.Contains("02:00 him: finished the BMC report") || !frame.Contains("14:01 her: I ordered") || frame.Contains("04:30") || frame.Contains("06:00") || frame.Contains("hunter2") || frame.Contains("(heartbeat)")) throw new Exception("The day page frame took the wrong lines: " + frame);
                if (DayJournal.Closed(new DateTime(2026, 11, 3, 22, 0, 0)) != new DateTime(2026, 11, 2) || DayJournal.Closed(new DateTime(2026, 11, 3, 4, 0, 0)) != new DateTime(2026, 11, 1)) throw new Exception("The closed day is wrong around 05:00.");
                if (!DayJournal.Rules.Contains("never write that something happened because she said it") || DayJournal.Asker(null) != null || DayJournal.Run(folder, DateTime.Now, null) != 0) throw new Exception("The day page rules or the no-key path are wrong.");
                List<string> flat = DayJournal.Parse("- 14:00 Bought Rengar after the game. - 15:30 Went shopping - got bread. - 18:00 Ordered pizza");
                if (flat.Count != 3 || flat[1] != "- 15:30 Went shopping - got bread.") throw new Exception("A day page answer on one line was not split: " + string.Join(" | ", flat.ToArray()));
                DayEvents.Add(folder, "Bought Rengar", day);
                int asked = 0;
                Func<string, string, string> fake = delegate(string rules, string text)
                {
                    asked++;
                    return text.Contains("Rengar") ? "Here is the page:\n- 14:00 Bought Rengar after the game\n- 15:00 His password is hunter2\n## sneaky\n* 02:00 Finished the BMC report" : "NONE";
                };
                int written = DayJournal.Run(folder, new DateTime(2026, 11, 3, 22, 0, 0), fake);
                string page = File.ReadAllText(DayJournal.PathFor(folder, day));
                string quiet = File.ReadAllText(DayJournal.PathFor(folder, day.AddDays(1)));
                string early = File.ReadAllText(DayJournal.PathFor(folder, day.AddDays(-1)));
                if (written != 3 || asked != 3 || !early.Contains(DayJournal.Heading) || Regex.Matches(page, "## The day").Count != 1 || !page.Contains("- 14:00 Bought Rengar after the game") || !page.Contains("- 02:00 Finished the BMC report") || page.Contains("hunter2") || page.Contains("sneaky") || page.Contains("pizza") || !quiet.Contains(DayJournal.Quiet)) throw new Exception("The day pages were written wrong: " + page);
                if (page.IndexOf(DayEvents.Mark, StringComparison.Ordinal) > page.IndexOf(DayJournal.Heading, StringComparison.Ordinal)) throw new Exception("The events moved below the day page.");
                if (DayJournal.Run(folder, new DateTime(2026, 11, 3, 23, 0, 0), fake) != 0 || asked != 3) throw new Exception("A day page was asked for twice.");
                string recent = DayJournal.Recent(folder, new DateTime(2026, 11, 3, 22, 0, 0), 3);
                if (!recent.Contains("1 Nov 14:00 Bought Rengar after the game") || recent.Contains(DayJournal.Quiet.Substring(2)) || DayJournal.Recent(folder, new DateTime(2026, 11, 9, 22, 0, 0), 3).Length != 0) throw new Exception("The Core did not get the last day pages, or got old or quiet ones: " + recent);
                DayEvents.Add(folder, "Finished the BMC report", day);
                page = File.ReadAllText(DayJournal.PathFor(folder, day));
                if (page.IndexOf(DayEvents.Mark + "Finished the BMC report", StringComparison.Ordinal) < 0 || page.IndexOf(DayEvents.Mark + "Finished the BMC report", StringComparison.Ordinal) > page.IndexOf(DayJournal.Heading, StringComparison.Ordinal)) throw new Exception("A late event did not go under the title.");
                string night = DayJournal.Before(page, "- Filed 2 lines\n");
                if (night.IndexOf("- Filed 2 lines", StringComparison.Ordinal) > night.IndexOf(DayJournal.Heading, StringComparison.Ordinal) || Regex.Matches(night, "## The day").Count != 1) throw new Exception("A night line landed inside the day page.");
                DayJournal.Write(folder, day, new List<string> { "- 10:00 Rewritten line" });
                page = File.ReadAllText(DayJournal.PathFor(folder, day));
                if (Regex.Matches(page, "## The day").Count != 1 || !page.Contains("Rewritten line") || page.Contains("Bought Rengar after the game") || !page.Contains(DayEvents.Mark + "Bought Rengar")) throw new Exception("A rewritten day page was doubled or lost the events: " + page);
                File.WriteAllText(Path.Combine(folder, "People", "Rowan.md"), "Rowan is his father; he owns a cabin.\n");
                File.WriteAllText(Path.Combine(folder, "People", "Nadia.md"), "Nadia is his mother; she likes slides.\n");
                string parents = Vault.Related("what are my parents called?");
                if (!parents.Contains("People/Rowan.md") || !parents.Contains("People/Nadia.md")) throw new Exception("A question about his parents did not find his father and mother: " + parents);
                DateTime tuesday = new DateTime(2026, 11, 3, 22, 0, 0);
                List<DateTime> named = JournalDays.Named("did I order pizza on 30 October? and what about yesterday, or on friday, or Nov 1st", tuesday);
                if (named.Count != 3 || !named.Contains(new DateTime(2026, 10, 30)) || !named.Contains(new DateTime(2026, 11, 2)) || !named.Contains(new DateTime(2026, 11, 1))) throw new Exception("Date words were read wrong: " + named.Count);
                if (JournalDays.Named("what did I do today", new DateTime(2026, 11, 3, 2, 0, 0))[0] != new DateTime(2026, 11, 2) || JournalDays.Named("tell Teodor 3 times", tuesday).Count != 0 || JournalDays.Named("on 25 December", tuesday)[0] != new DateTime(2025, 12, 25)) throw new Exception("The 05:00 day, a name like Teodor or a future date was read wrong.");
                List<string> busy = new List<string>(); for (int i = 0; i < 13; i++) busy.Add("- 1" + (i % 10) + ":00 Talked about pizza toppings, round " + i); busy.Add("- 22:25 Ordered a Brooklyn pizza"); DayJournal.Write(folder, new DateTime(DateTime.Now.Year, 1, 2), busy);
                string ordered = Vault.Related("did I end up ordering the pizza on 2 January?");
                if (!ordered.Contains("Journal/" + DateTime.Now.Year + "-01-02.md") || !ordered.Contains("Ordered a Brooklyn pizza")) throw new Exception("A question naming a day did not find that day's journal page: " + ordered);
            }
            finally
            {
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS day pages: each closed day (05:00 to 05:00) gets one ## The day section in its journal from his own lines (her lines marked as hers, secrets, heartbeat and bracket lines left out, her action log and events added), written once a night for up to 3 missed days, never twice, a rewrite replaces it, secret lines in the answer are dropped, the night's journal lines and events stay above it, and with no key nothing runs; a question about his parents, siblings, grandparents or family also searches the father, mother, brother and sister words; a day named in a question (30 October, yesterday, on Friday, today, by the 05:00 day) brings that day's journal page, and of a long note the lines holding the most question words are shown (ordering finds ordered); the nightly Core also reads the last 3 day pages (quiet days left out)");
        }
    }
}
