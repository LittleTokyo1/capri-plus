using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeNoteTidy(List<string> checks)
        {
            string messy = "# Rudiminas\r\n\r\n# Rudiminas\r\nRudiminas is a friend of Kasper; on Discord he goes by Rudce.\r\n- Also called: Rudce, Domitan, Dominikas\r\n- Discord: Rudce\r\n- League: not known yet\r\n- Is: friend\r\n- Plays: drums\r\n- Discord: rudce.x\r\n- Likes the gym\r\n- Likes the gym\r\nRelated: [[Kasper]]\r\n";
            string clean = NoteTidy.Clean(messy, "Rudiminas");
            if (clean.Contains("# Rudiminas") || clean.Contains("not known yet") || clean.Contains("Is: friend") || clean.Split(new[] { "Likes the gym" }, StringSplitOptions.None).Length != 2) throw new Exception("The messy note was not cleaned: " + clean);
            if (!clean.Contains("- Discord: Rudce, rudce.x") || clean.Split(new[] { "Discord:" }, StringSplitOptions.None).Length != 2 || !clean.Contains("Plays: drums") || !clean.Contains("Related: [[Kasper]]") || !clean.Contains("on Discord he goes by Rudce") || !clean.Contains("Also called: Rudce, Domitan, Dominikas") || !clean.Contains("\r\n")) throw new Exception("The cleaned note lost a fact, or its line endings: " + clean);
            if (NoteTidy.Clean(clean, "Rudiminas") != clean) throw new Exception("Cleaning a clean note changed it again.");
            if (NoteTidy.Clean("# Other\n- Is: friend of Sam\n", "Rudiminas") != "# Other\n- Is: friend of Sam\n") throw new Exception("A heading that is not the file's title was removed, or a lone Is line was dropped.");

            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            string folder = Path.Combine(Path.GetTempPath(), "capri-tidynotes-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Names.Reset();
                if (!Vault.Open(folder, null)) throw new Exception("The vault did not open.");
                Brain.MemoryPath = Path.Combine(folder, Vault.CoreName);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                string note = Path.Combine(folder, "People", "Rudiminas.md");
                File.WriteAllText(note, messy);
                DateTime now = new DateTime(2026, 10, 30, 22, 0, 0);
                if (NoteTidy.Run(folder, now) != 1 || File.ReadAllText(note) != clean) throw new Exception("The nightly clean did not rewrite the note.");
                string[] backups = Directory.Exists(Path.Combine(folder, "Capri", "backups")) ? Directory.GetFiles(Path.Combine(folder, "Capri", "backups"), "*Rudiminas*") : new string[0];
                if (backups.Length != 1 || File.ReadAllText(backups[0]) != messy) throw new Exception("The old note was not backed up before it was cleaned.");
                if (NoteTidy.Run(folder, now) != 0) throw new Exception("A second clean changed a clean note.");

                Names.Reset();
                File.WriteAllText(note, "# Rudiminas\n\nRudiminas is a friend of Kasper; on Discord he goes by Rudce.\n- Discord: Rudce\nRelated: [[Kasper]]\n");
                Names.Reset();
                List<PersonFact> repeated = new List<PersonFact> { new PersonFact { Name = "Rudiminas", Field = "Is", Value = "friend" }, new PersonFact { Name = "Rudiminas", Field = "Discord", Value = "Rudce" } };
                PeopleNotes.Apply(repeated);
                string after = File.ReadAllText(note);
                if (after.Contains("- Is: friend") || after.Split(new[] { "Discord: Rudce" }, StringSplitOptions.None).Length != 2) throw new Exception("She added what the note already said: " + after);
                List<PersonFact> fresh = new List<PersonFact> { new PersonFact { Name = "Zita", Field = "Discord", Value = "zita.x" } };
                Names.Reset();
                PeopleNotes.Apply(fresh);
                string created = File.ReadAllText(Path.Combine(folder, "People", "Zita.md"));
                if (created.Contains("# Zita") || !created.Contains("Discord: zita.x")) throw new Exception("A new People note still starts with its own title: " + created);
            }
            finally
            {
                ActivityLog.FilePath = keptLog;
                ForkSettings.Current = keptSettings;
                Names.Reset();
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS note tidy: a People note loses its title heading (Obsidian shows the file name), placeholder lines like League: not known yet, exact duplicates, repeated Discord lines (merged into one) and Is or Plays values the note already says elsewhere, keeping every fact, the Related line and his line endings; the old note is backed up, a second run changes nothing, and she no longer adds a line the note already says or starts a new note with its own title");
        }
    }
}
