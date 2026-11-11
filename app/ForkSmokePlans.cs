using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokePlans(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            string keptSettingsFile = ForkSettings.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            string folder = Path.Combine(Path.GetTempPath(), "capri-plans-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                AwayCheckIns.Reset();
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Names.Reset();
                string vault = Path.Combine(folder, "vault");
                if (!Vault.Open(vault, null)) throw new Exception("The plans vault did not open.");
                Brain.MemoryPath = Path.Combine(vault, Vault.CoreName);
                PlansFile(vault);
                PlansParse(vault);
                PlansRaised(vault);
                PlansEvents(vault);
            }
            finally
            {
                ActivityLog.FilePath = keptLog;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptSettingsFile;
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                Names.Reset();
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS plans and promises (step 8): the memory pass answers PLAN for a promise, a plan or a maybe and DONE when he says an open plan is done or off; she keeps Capri/plans.md herself in the Obsidian Tasks format (- [ ] with a due date, the day it was said as a Journal link, - [x] with the done date, - [-] when dropped), never twice; open plans are listed to the memory pass by number; a plan due today or overdue is raised once a day by the heartbeat and by the away check-ins, a maybe never is");
            checks.Add("PASS events: what he did that matters later (bought, ordered, sent, handed in) comes back from the memory pass as EVENT with its date and is written once as '- event:' at the top of that day's journal (never in the future, never from a side pass or overheard talk), and her prompt lists what he did in the last 14 days, newest first; note lines reach the memory pass with the day they were said, and a change heard by voice only of a fact older than 7 days is held until he says it again within 7 days");
        }

        private static void PlansFile(string vault)
        {
            DateTime said = new DateTime(2026, 10, 30, 14, 0, 0);
            DateTime due = new DateTime(2026, 11, 5);
            if (!Plans.Add(vault, "Call mum about the trip", due, said) || Plans.Add(vault, "call mum about the trip.", due, said)) throw new Exception("A plan was not added once.");
            if (!Plans.Add(vault, "maybe: quit their job", null, said)) throw new Exception("A maybe was not added.");
            string body = File.ReadAllText(Plans.PathIn(vault));
            if (!body.StartsWith("# Plans and promises", StringComparison.Ordinal) || !body.Contains("- [ ] Call mum about the trip ([[Journal/2026-10-30|30 Oct]]) \U0001F4C5 2026-11-05") || !body.Contains("- [ ] maybe: quit their job ([[Journal/2026-10-30|30 Oct]])")) throw new Exception("plans.md is not in the Obsidian Tasks format: " + body);
            List<PlanLine> open = Plans.Open(vault);
            if (open.Count != 2 || open[0].Id != "P1" || !open[0].Due.HasValue || open[0].Due.Value != due || open[1].Due.HasValue || !open[1].Maybe) throw new Exception("The open plans were read wrong: " + open.Count);
            string listing = Plans.Listing(vault);
            if (!listing.Contains("P1 Call mum about the trip (due 5 Nov 2026)") || !listing.Contains("P2 maybe: quit their job")) throw new Exception("The open plans were not listed by number: " + listing);
            if (Plans.Due(vault, new DateTime(2026, 11, 4, 9, 0, 0)).Count != 0 || Plans.Due(vault, new DateTime(2026, 11, 5, 9, 0, 0)).Count != 1 || Plans.Due(vault, new DateTime(2026, 11, 6, 9, 0, 0)).Count != 1 || Plans.Due(vault, new DateTime(2026, 11, 7, 9, 0, 0)).Count != 0) throw new Exception("Due plans were picked wrong (raised on the day and the day after only).");
            if (Plans.Close(vault, "P9", false, due) || !Plans.Close(vault, "P1", false, due)) throw new Exception("Closing a plan by number was wrong.");
            body = File.ReadAllText(Plans.PathIn(vault));
            if (!body.Contains("- [x] Call mum about the trip ([[Journal/2026-10-30|30 Oct]]) \U0001F4C5 2026-11-05 ✅ 2026-11-05") || Plans.Open(vault).Count != 1 || Plans.Due(vault, new DateTime(2026, 11, 7, 9, 0, 0)).Count != 0) throw new Exception("A done plan was not ticked with its date: " + body);
            if (!Plans.Close(vault, "P1", true, due) || !File.ReadAllText(Plans.PathIn(vault)).Contains("- [-] maybe: quit their job ([[Journal/2026-10-30|30 Oct]]) ❌ 2026-11-05") || Plans.Open(vault).Count != 0) throw new Exception("A dropped plan was not marked as cancelled.");
            if (Plans.Add(vault, "Pay with card number 4111 1111 1111 1111", null, said)) throw new Exception("A plan with a card number was kept.");
        }

        private static void PlansParse(string vault)
        {
            Plans.Add(vault, "Hand in the thesis draft", new DateTime(2026, 11, 12), new DateTime(2026, 10, 30));
            List<string> why = new List<string>();
            List<PersonFact> facts = PeopleNotes.Parse("PLAN | Book the dentist for next week | 2026-11-06\nPLAN | maybe: move to Utrecht | none\nPLAN | Visit grandma | soon\nDONE | P1 | done\nDONE | X4 | done\nPLAN | see https://evil.example | none", why);
            if (facts.FindAll(delegate(PersonFact fact) { return fact.Name == "PLAN"; }).Count != 3 || facts.FindAll(delegate(PersonFact fact) { return fact.Name == "DONE"; }).Count != 1) throw new Exception("PLAN and DONE answers were parsed wrong: " + facts.Count + " (" + string.Join(", ", why.ToArray()) + ")");
            string frame = PeopleNotes.Frame("I handed in the thesis draft", null, new DateTime(2026, 11, 12, 10, 0, 0), new List<NoteLine>());
            if (!frame.Contains("P1 Hand in the thesis draft (due 12 Nov 2026)")) throw new Exception("The memory pass does not see the open plans.");
            PeopleResult result = PeopleNotes.Apply(facts, true, new List<NoteLine>(), new DateTime(2026, 11, 1, 12, 0, 0));
            string body = File.ReadAllText(Plans.PathIn(vault));
            if (result.Planned != 3 || result.Closed != 1 || !body.Contains("- [x] Hand in the thesis draft") || !body.Contains("- [ ] Book the dentist for next week ([[Journal/2026-11-01|1 Nov]]) \U0001F4C5 2026-11-06") || !body.Contains("- [ ] Visit grandma ([[Journal/2026-11-01|1 Nov]])") || body.Contains("Visit grandma ([[Journal/2026-11-01|1 Nov]]) \U0001F4C5")) throw new Exception("PLAN and DONE answers were not applied: " + result.Planned + "/" + result.Closed + " " + body);
            if (!PeopleNotes.Instructions.Contains("PLAN |") || !PeopleNotes.Instructions.Contains("DONE |")) throw new Exception("The memory pass is not told about PLAN and DONE.");
            PeopleResult side = PeopleNotes.Apply(PeopleNotes.Parse("PLAN | Renew the gym card | none"), false, new List<NoteLine>(), new DateTime(2026, 11, 1, 12, 0, 0));
            if (side.Planned != 0 || File.ReadAllText(Plans.PathIn(vault)).Contains("Renew the gym card")) throw new Exception("The side pass wrote a plan the remember tool is about to save.");
            if (PeopleNotes.Spoken("Noted, I'll keep that in mind.", result, false) != "Noted, I'll keep that in mind.") throw new Exception("An honest claim was cut when only a plan was written.");

            string core = Brain.MemoryPath;
            File.WriteAllText(core, Brain.NotesHead + "- Lives in Eindhoven\n");
            Func<string, string> keptExtract = PeopleNotes.Extract;
            try
            {
                PeopleNotes.Extract = delegate { return "PLAN | Call the landlord about the heating | 2026-11-08"; };
                string told = Saving.Save("remember to call the landlord about the heating on 8 November");
                string plans = File.ReadAllText(Plans.PathIn(vault));
                if (!told.Contains("Capri/plans.md") || !told.Contains("landlord") || File.ReadAllText(core).Contains("landlord") || plans.Split(new[] { "landlord" }, StringSplitOptions.None).Length != 2) throw new Exception("A remembered plan did not land once in plans.md alone: " + told);
            }
            finally { PeopleNotes.Extract = keptExtract; }
        }

        private static void PlansRaised(string vault)
        {
            DateTime day = new DateTime(2026, 11, 6, 10, 0, 0);
            HeartState saved = new HeartState();
            List<string> first = Heartbeat.PlanSignals(saved, vault, day);
            List<string> again = Heartbeat.PlanSignals(saved, vault, day.AddHours(2));
            List<string> next = Heartbeat.PlanSignals(saved, vault, day.AddDays(1));
            if (first.Count != 1 || !first[0].Contains("Book the dentist") || !first[0].Contains("today") || again.Count != 0 || next.Count != 1 || !next[0].Contains("overdue")) throw new Exception("The heartbeat did not raise a due plan once a day: " + string.Join(" | ", first.ToArray()) + " / " + again.Count + " / " + next.Count);
            CheckPlan plan = AwayCheckIns.DuePlan(day, vault);
            if (plan == null || plan.Kind != "plan" || !plan.Why.Contains("Book the dentist") || AwayCheckIns.DuePlan(new DateTime(2026, 11, 2, 10, 0, 0), vault) != null) throw new Exception("The away check-ins did not raise the plan due today, or raised one too early.");
        }
    }
}
