using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeHistory(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            string folder = Path.Combine(Path.GetTempPath(), "capri-history-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Names.Reset();
                MemoryQueue.Reset();
                string vault = Path.Combine(folder, "vault");
                if (!Vault.Open(vault, null)) throw new Exception("The history vault did not open.");
                string core = Path.Combine(vault, Vault.CoreName);
                Brain.MemoryPath = core;
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                HistoryKept(checks, vault, core);
                HistoryReaders(checks, vault, core);
                HistoryWriters(checks, vault, core);
            }
            finally
            {
                PeopleNotes.Extract = keptExtract;
                ActivityLog.FilePath = keptLog;
                ForkSettings.Current = keptSettings;
                MemoryQueue.Reset();
                Names.Reset();
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private static void HistoryKept(List<string> checks, string vault, string core)
        {
            DateTime day = new DateTime(2026, 10, 30, 14, 0, 0);
            File.WriteAllText(core, "# About them\n- Goes to the gym on Mondays and Thursdays\n- Works at the shop on weekends\n- Plays League of Legends most evenings\n");
            PeopleNotes.Extract = delegate { return "REPLACE | L1 | NONE"; };
            PeopleNotes.Learn(null, "I don't go to the gym anymore", null, null, 1, null, day);
            string body = File.ReadAllText(core);
            int history = body.IndexOf("## History", StringComparison.Ordinal);
            if (history < 0 || body.IndexOf("Goes to the gym", StringComparison.Ordinal) < history || !body.Contains("- until 30 Oct 2026: Goes to the gym on Mondays and Thursdays") || !body.Contains("- Plays League of Legends most evenings")) throw new Exception("The gym correction did not keep the old line under History: " + body);
            PeopleNotes.Extract = delegate { return "REPLACE | L1 | Works at the bakery on weekends"; };
            PeopleNotes.Learn(null, "I work at the bakery on weekends now, not the shop", null, null, 1, null, day);
            body = File.ReadAllText(core);
            history = body.IndexOf("## History", StringComparison.Ordinal);
            int now = body.IndexOf("- Works at the bakery on weekends ([[Journal/2026-10-30|30 Oct]])", StringComparison.Ordinal);
            if (now < 0 || now > history || body.IndexOf("- until 30 Oct 2026: Works at the shop on weekends", StringComparison.Ordinal) < history) throw new Exception("A rewritten line did not keep the old one in History or lost its source link: " + body);
            string journal = Path.Combine(vault, "Journal", "2026-10-30.md");
            if (!File.Exists(journal) || !File.ReadAllText(journal).StartsWith("# 2026-10-30", StringComparison.Ordinal)) throw new Exception("The day a source link points at has no Journal note.");
            List<NoteLine> lines = NoteEdit.Candidates("bakery gym");
            if (lines.Exists(delegate(NoteLine line) { return line.Text.Contains("until"); }) || !lines.Exists(delegate(NoteLine line) { return line.Text == "Works at the bakery on weekends"; })) throw new Exception("History lines were offered for a correction, or a line with a source link was not: " + NoteEdit.Format(lines));
            string note = Path.Combine(vault, "People", "Rudiminas.md");
            File.WriteAllText(note, "# Rudiminas\n- Discord: rudce\n");
            Names.Reset();
            PeopleNotes.Extract = delegate { return "Rudiminas | Note | Birthday is 30 October\nRudiminas | League | Rudce#EUW"; };
            PeopleNotes.Learn(null, "oh btw it's rudiminas birthday today! his league name is Rudce#EUW", null, null, 1, null, day);
            string person = File.ReadAllText(note);
            if (!person.Contains("- Birthday is 30 October ([[Journal/2026-10-30|30 Oct]])") || person.Contains("Rudce#EUW ([[") || !person.Contains("Sources: [[Journal/2026-10-30|30 Oct]]")) throw new Exception("A People note fact did not get its source link, or a field line got one, or the Sources line is missing: " + person);
            if (Brain.Note("Plays League of Legends most evenings", day).StartsWith("Saved", StringComparison.Ordinal)) throw new Exception("A line already in me.md was saved again because of its source link.");
            if (!Brain.Note("Likes spicy ramen from the corner place", day).StartsWith("Saved", StringComparison.Ordinal) || !File.ReadAllText(core).Contains("- Likes spicy ramen from the corner place ([[Journal/2026-10-30|30 Oct]])")) throw new Exception("A dated me.md note did not get its source link.");
            body = File.ReadAllText(core);
            if (body.IndexOf("spicy ramen", StringComparison.Ordinal) > body.IndexOf("## History", StringComparison.Ordinal)) throw new Exception("A new me.md note was written under History.");
            if (NoteEdit.Remove(new List<string> { "gym" }) < 1 || File.ReadAllText(core).Contains("gym")) throw new Exception("Forgetting the gym left it in History.");
            checks.Add("PASS history instead of overwrite: a correction moves the old line to '## History' as '- until 30 Oct 2026: ...' and the new one keeps its place, prose facts end with a source link like ([[Journal/2026-10-30|30 Oct]]) to a Journal day note made if missing, field lines like League: get none and the note gets a Sources: line, History lines are never offered for corrections, a source link never makes a line count as new, new notes go above History, and forgetting removes History lines too");
        }

        private static void HistoryWriters(List<string> checks, string vault, string core)
        {
            string note = Path.Combine(vault, "People", "Rudiminas.md");
            File.WriteAllText(note, "# Rudiminas\n- Discord: rudce\nSources: [[Journal/2026-10-29|29 Oct]]\n\n## History\n- until 1 Oct 2026: Plays piano\n");
            Names.Reset();
            bool created;
            string title;
            List<string> titles = new List<string> { "Rudiminas" };
            List<string> bodies = new List<string> { "Rudiminas plays chess on Sundays ([[Journal/2026-10-30|30 Oct]])" };
            if (!Nightly.Append(vault, "People/Rudiminas", bodies, titles, out created, out title) || !Nightly.Append(vault, "People/Rudiminas", bodies, titles, out created, out title)) throw new Exception("The evening filing could not write the note.");
            string filed = File.ReadAllText(note);
            int chess = filed.Split(new[] { "plays chess" }, StringSplitOptions.None).Length - 1;
            int line = filed.IndexOf("- Rudiminas plays chess on Sundays ([[Journal/2026-10-30|30 Oct]])", StringComparison.Ordinal);
            if (chess != 1 || line < 0 || line > filed.IndexOf("Sources:", StringComparison.Ordinal)) throw new Exception("A filed line with a source link was filed twice, lost its link at the end or landed under Sources and History: " + filed);
            if (!Names.AddAlias(note, "Domka")) throw new Exception("A heard name was not added to the note.");
            string aliased = File.ReadAllText(note);
            int alias = aliased.IndexOf("Also called: Domka", StringComparison.Ordinal);
            if (alias < 0 || alias > aliased.IndexOf("Sources:", StringComparison.Ordinal)) throw new Exception("A new Also called line went under Sources or History, where she cannot read it: " + aliased);
            Names.Reset();
            if (!Names.Find("tell Domka hi").Exists(delegate(NameHit hit) { return hit.Name == "Rudiminas"; })) throw new Exception("The added name was not found again.");
            File.WriteAllText(core, "# About them\n- Does pottery on Fridays ([[Journal/2026-10-30|30 Oct]])\n\n## History\n- until 1 Sep 2026: Does pottery on Mondays\n- until 1 Aug 2026: Does pottery on Tuesdays\n- until 1 Jul 2026: Does pottery on Sundays\n");
            if (NoteEdit.Remove(new List<string> { "pottery" }) != 4 || File.ReadAllText(core).Contains("pottery")) throw new Exception("Forgetting a topic with History lines removed nothing: " + File.ReadAllText(core));
            checks.Add("PASS writers above History: the evening filing keeps a line's source link at the end and does not file it twice, a heard name goes above the Sources line and History so it is still found, and forgetting a topic removes its History lines without them counting towards the three-line safety cap");
        }

        private static void HistoryReaders(List<string> checks, string vault, string core)
        {
            string note = Path.Combine(vault, "People", "Rudiminas.md");
            File.WriteAllText(note, "# Rudiminas\n- Discord: rudce ([[Journal/2026-10-29|29 Oct]])\n- Also called: Domka ([[Journal/2026-10-29|29 Oct]])\n- Birthday is 30 October ([[Journal/2026-10-30|30 Oct]])\nSources: [[Journal/2026-10-30|30 Oct]]\n\n## History\n- until 1 Oct 2026: Birthday is 5 May ([[Journal/2026-09-01|1 Sep]])\n");
            File.WriteAllText(Path.Combine(vault, "People", "Vaidas.md"), "# Vaidas\n- Plays chess on Sundays ([[Journal/2026-11-02|2 Nov]])\n");
            Names.Reset();
            List<HeartBirthday> birthdays = Heartbeat.Birthdays(vault, new DateTime(2026, 10, 28));
            if (birthdays.Count != 1 || birthdays[0].Name != "Rudiminas" || birthdays[0].Days != 2) throw new Exception("A source link or a History line changed the birthday the heartbeat raises: " + birthdays.Count);
            List<string> accounts = DiscordFriends.FromNote(Vault.Text(note));
            if (accounts.Count != 1 || accounts[0] != "rudce") throw new Exception("A source link broke the Discord name in a People note: " + string.Join("|", accounts.ToArray()));
            List<NameHit> hits = Names.Find("tell Domka hi");
            if (!hits.Exists(delegate(NameHit hit) { return hit.Name == "Rudiminas"; })) throw new Exception("A source link broke an Also called name.");
            string related = Vault.Related("when is rudiminas birthday");
            if (!related.Contains("Birthday is 30 October") || related.Contains("5 May") || related.Contains("[[Journal")) throw new Exception("Vault notes in her prompt carried History or source links: " + related);
            File.WriteAllText(core, "# About them\n- Works at the bakery on weekends ([[Journal/2026-10-30|30 Oct]])\n\n## History\n- until 30 Oct 2026: Goes to the gym on Mondays\n");
            string plain = Context.For("what should I do this weekend");
            string past = Context.For("did I use to go to the gym");
            if (plain.Contains("gym") || !plain.Contains("Works at the bakery on weekends") || plain.Contains("[[Journal") || !past.Contains("Goes to the gym on Mondays") || !past.Contains("no longer true")) throw new Exception("History reached a normal turn, or a question about the past did not get it: " + past);
            NightBatch batch = Nightly.Gather(vault, core);
            if (batch.Lines.Exists(delegate(NightLine line) { return line.Text.Contains("until"); })) throw new Exception("The evening tidy-up would file History lines out of me.md.");
            string messy = "# Rudiminas\n- Birthday is 30 October ([[Journal/2026-10-30|30 Oct]])\n- Birthday is 30 October ([[Journal/2026-10-31|31 Oct]])\n\n## History\n- until 1 Oct 2026: Birthday is 5 May\n- until 1 Oct 2026: Birthday is 5 May\n";
            string clean = NoteTidy.Clean(messy, "Rudiminas");
            if (clean.Contains("31 Oct") || !clean.Contains("## History") || clean.Split(new[] { "Birthday is 5 May" }, StringSplitOptions.None).Length != 3) throw new Exception("The note tidy-up did not merge lines that differ only by source link, or changed History: " + clean);
            checks.Add("PASS readers ignore source links and History: the heartbeat raises the current birthday (not one in History or a date inside a link), Discord: and Also called: names still match with a link after them, vault notes and me.md reach her prompt without links or History, a question about the past gets History labelled no longer true, the evening tidy-up never files History lines, and the note tidy-up merges lines that differ only by link and leaves History as it is");
        }
    }
}
