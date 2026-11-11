using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void PlansEvents(string vault)
        {
            List<string> why = new List<string>();
            List<PersonFact> facts = PeopleNotes.Parse("EVENT | Bought Rengar after the game | 2026-11-03\nEVENT | Ordered the pizza | yesterday\nEVENT | x | 2026-11-03\nEVENT | Sent the proof email to Mrs. Hu | 2026-11-02", why);
            if (facts.FindAll(delegate(PersonFact fact) { return fact.Name == "EVENT"; }).Count != 2) throw new Exception("EVENT answers were parsed wrong: " + facts.Count + " (" + string.Join(", ", why.ToArray()) + ")");
            string journal = Path.Combine(vault, "Journal");
            Directory.CreateDirectory(journal);
            File.WriteAllText(Path.Combine(journal, "2026-11-03.md"), "# 2026-11-03\n\n- Talked about the exam\n");
            PeopleResult result = PeopleNotes.Apply(facts, true, new List<NoteLine>(), new DateTime(2026, 11, 3, 21, 0, 0));
            string day = File.ReadAllText(Path.Combine(journal, "2026-11-03.md"));
            if (result.Happened != 2 || day != "# 2026-11-03\n\n- event: Bought Rengar after the game\n- Talked about the exam\n" || !File.ReadAllText(Path.Combine(journal, "2026-11-02.md")).Contains("- event: Sent the proof email to Mrs. Hu")) throw new Exception("EVENT answers were not written to the day's journal: " + result.Happened + " " + day);
            if (PeopleNotes.Apply(PeopleNotes.Parse("EVENT | bought rengar after the game | 2026-11-03"), true, new List<NoteLine>(), new DateTime(2026, 11, 3, 22, 0, 0)).Happened != 0) throw new Exception("The same event was written twice.");
            if (PeopleNotes.Apply(PeopleNotes.Parse("EVENT | Booked the dentist | 2026-11-09"), true, new List<NoteLine>(), new DateTime(2026, 11, 3, 22, 0, 0)).Happened != 0) throw new Exception("An event in the future was written.");
            if (PeopleNotes.Apply(PeopleNotes.Parse("EVENT | Renewed the gym card | 2026-11-03"), false, new List<NoteLine>(), new DateTime(2026, 11, 3, 22, 0, 0)).Happened != 0) throw new Exception("A side pass wrote an event.");
            string block = DayEvents.Block(vault, new DateTime(2026, 11, 4, 9, 0, 0));
            if (block.IndexOf("- 3 Nov: Bought Rengar after the game", StringComparison.Ordinal) < 0 || block.IndexOf("- 2 Nov: Sent the proof email", StringComparison.Ordinal) < block.IndexOf("- 3 Nov", StringComparison.Ordinal) || DayEvents.Block(vault, new DateTime(2026, 11, 30, 9, 0, 0)) != "") throw new Exception("What he did lately was not shown newest first, or old events stayed: " + block);
            if (!PeopleNotes.Instructions.Contains("EVENT |") || !PeopleNotes.OverheardRules.Contains("Never send EVENT")) throw new Exception("The memory pass is not told about EVENT, or overheard talk may make one.");
            File.WriteAllText(Brain.MemoryPath, Brain.NotesHead + "- Works at the bakery on weekends ([[Journal/2026-10-30|30 Oct]])\n- Plays the bakery quiz on Fridays\n");
            List<NoteLine> dated = NoteEdit.Candidates("I quit the bakery");
            string shown = NoteEdit.Format(dated);
            if (shown.IndexOf("Works at the bakery on weekends (said 30 Oct 2026)", StringComparison.Ordinal) < 0 || shown.IndexOf("Plays the bakery quiz on Fridays\n", StringComparison.Ordinal) < 0 && !shown.EndsWith("Plays the bakery quiz on Fridays", StringComparison.Ordinal) || dated.Find(delegate(NoteLine one) { return one.Text.Contains("(said"); }) != null) throw new Exception("Note lines did not show the day they were said, or the day leaked into the line text: " + shown);
            if (!PeopleNotes.Instructions.Contains("(said ") || !PeopleNotes.Instructions.Contains("I think she is turning 13")) throw new Exception("The memory pass is not told about dated lines and hedges.");
            File.WriteAllText(Brain.MemoryPath, Brain.NotesHead + "- Works at the bakery on weekends ([[Journal/2026-10-20|20 Oct]])\n- Plays the bakery quiz on Fridays ([[Journal/2026-11-01|1 Nov]])\n");
            DateTime spokenDay = new DateTime(2026, 11, 3, 21, 0, 0);
            PeopleResult held = PeopleNotes.Apply(PeopleNotes.Parse("REPLACE | " + LineAbout("weekends").Id + " | NONE"), true, NoteEdit.Candidates("bakery"), spokenDay, null, true, true);
            if (held.Fixed != 0 || held.HeldOld != 1 || !File.ReadAllText(Brain.MemoryPath).Contains("Works at the bakery on weekends")) throw new Exception("A spoken-only change replaced a fact older than 7 days.");
            if (PeopleNotes.Apply(PeopleNotes.Parse("REPLACE | " + LineAbout("quiz").Id + " | NONE"), true, NoteEdit.Candidates("bakery"), spokenDay, null, true, true).Fixed != 1) throw new Exception("A spoken change of a recent fact was held.");
            if (PeopleNotes.Apply(PeopleNotes.Parse("REPLACE | " + LineAbout("weekends").Id + " | NONE"), true, NoteEdit.Candidates("bakery"), spokenDay.AddHours(1), null, true, true).Fixed != 1 || NoteEdit.Current(File.ReadAllText(Brain.MemoryPath)).Contains("on weekends")) throw new Exception("Saying the change a second time did not make it.");
            File.WriteAllText(Brain.MemoryPath, Brain.NotesHead + "- Works at the bakery on weekends ([[Journal/2026-10-20|20 Oct]])\n");
            if (PeopleNotes.Apply(PeopleNotes.Parse("REPLACE | " + LineAbout("weekends").Id + " | NONE"), true, NoteEdit.Candidates("bakery"), spokenDay, null, true, false).Fixed != 1 || NoteEdit.Current(File.ReadAllText(Brain.MemoryPath)).Contains("on weekends")) throw new Exception("A typed change of an old fact was held.");
        }

        private static NoteLine LineAbout(string word)
        {
            NoteLine found = NoteEdit.Candidates("bakery").Find(delegate(NoteLine one) { return one.Text.Contains(word); });
            if (found == null) throw new Exception("The test note line about " + word + " was not offered to the memory pass.");
            return found;
        }
    }
}
