using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeNameForms(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptPhase = Lockout.LeaguePhase;
            bool keptLock = ForkSettings.Current.LockLeagueClient;
            Func<string, bool> keptRunning = Lockout.Running;
            string folder = Path.Combine(Path.GetTempPath(), "capri-name-forms-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Names.Reset();
                NameQuestions.Reset();
                Lockout.Running = delegate(string name) { return false; };
                Lockout.LeaguePhase = null;
                Lockout.Recheck();
                if (!Vault.Open(folder, null)) throw new Exception("The name forms vault did not open.");
                string people = Path.Combine(folder, "People");
                File.WriteAllText(Path.Combine(people, "Rudiminas.md"), "# Rudiminas\n- Also called: Rudce, Dominikas\n- Plays: drums\n");
                File.WriteAllText(Path.Combine(people, "Vaidas.md"), "# Vaidas\n- Plays: League of Legends\n");
                File.WriteAllText(Path.Combine(people, "Wren Hu.md"), "# Wren Hu\n- Teaches physics\n");
                File.WriteAllText(Path.Combine(people, "Ezra Bilson.md"), "# Ezra Bilson\n- Plays: football\n");
                File.WriteAllText(Path.Combine(people, "Oskar.md"), "# Oskar\n- Also called: Kunigas\n");
                File.WriteAllText(Path.Combine(people, "Teodor.md"), "# Teodor\n- Plays: chess\n");
                File.WriteAllText(Path.Combine(people, "Pieter.md"), "# Pieter\n- Plays: Valorant\n");
                NameFormsSure(people);
                NameFormsHeld(people);
                NameFormsAsked(people);
            }
            finally
            {
                Names.Reset();
                NameQuestions.Reset();
                Lockout.Running = keptRunning;
                Lockout.LeaguePhase = keptPhase;
                ForkSettings.Current.LockLeagueClient = keptLock;
                Lockout.Recheck();
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS name forms: a Lithuanian case form of a known name (Vaidasas, Dominikai) and a title with a surname (Mrs. Hu) find the person for sure; a nickname (Rudcia), a short or cut form (Dom, Rudc, Bill, Dominik) and a near short form (Rudta) are held and asked, never saved to anyone; new names (Domantas, Arnoldas, Mantas, Jonas, Lukas) stay new and Tomas never lands in Oskar; a held name is asked once a day and never in a game, and his answer (yes, or the person's name) adds the heard name to that person");
        }

        private static void NameFormsSure(string people)
        {
            foreach (string[] pair in new[] { new[] { "Vaidasas", "Vaidas" }, new[] { "Dominikai", "Rudiminas" }, new[] { "Mrs. Hu", "Wren Hu" }, new[] { "Rudce", "Rudiminas" } })
            {
                PersonGuess guess = PeopleNotes.Identify(pair[0]);
                if (guess.Unclear || guess.Notes.Count != 1 || Path.GetFileNameWithoutExtension(guess.Notes[0]) != pair[1]) throw new Exception("A sure name form did not find its person: " + pair[0]);
            }
            NameHit title = Names.Find("email Mrs. Hu about the lab").Find(delegate(NameHit hit) { return hit.Exact; });
            if (title == null || title.Name != "Wren Hu" || title.Unsure) throw new Exception("A title with a surname in a sentence was missed.");
            if (Names.Find("pay the bill tonight").Count != 0 || Names.Find("the dom is big").Count != 0) throw new Exception("Plain words in a sentence matched a person.");
        }

        private static void NameFormsHeld(string people)
        {
            foreach (string[] pair in new[] { new[] { "Rudcia", "Rudiminas" }, new[] { "Dom", "Rudiminas" }, new[] { "Rudc", "Rudiminas" }, new[] { "Rudta", "Rudiminas" }, new[] { "Bill", "Ezra Bilson" }, new[] { "Dominik", "Rudiminas" } })
            {
                PersonGuess guess = PeopleNotes.Identify(pair[0]);
                if (!guess.Unclear || guess.Notes.Count != 0 || !guess.Maybe.Exists(delegate(string note) { return Path.GetFileNameWithoutExtension(note) == pair[1]; })) throw new Exception("A maybe name form was not held for its person: " + pair[0]);
            }
            foreach (string fresh in new[] { "Domantas", "Arnoldas", "Mantas", "Jonas", "Lukas" })
            {
                PersonGuess guess = PeopleNotes.Identify(fresh);
                if (guess.Unclear || guess.Notes.Count != 0) throw new Exception("A new name was taken for a known person: " + fresh);
            }
            if (PeopleNotes.Identify("Tomas").Notes.Count != 0) throw new Exception("Tomas landed in Oskar.");
            File.WriteAllText(Path.Combine(people, "Martynas.md"), "# Martynas\n- Plays: basketball\n");
            if (PeopleNotes.Identify("Martyna").Notes.Count != 0 || PeopleNotes.Identify("Martyne").Notes.Count != 0) throw new Exception("A woman's form of a man's name landed in his note.");
            File.Delete(Path.Combine(people, "Martynas.md"));
        }

        private void NameFormsAsked(string people)
        {
            string rudiminas = Path.Combine(people, "Rudiminas.md");
            PeopleResult held = PeopleNotes.Apply(PeopleNotes.Parse("Rudcia | Plays | chess"));
            if (held.Held.Count != 1 || held.Saved.Count != 0 || File.ReadAllText(rudiminas).Contains("chess")) throw new Exception("A held name saved something: " + held.Held.Count);
            string first = PeopleNotes.Spoken("Nice.", held, false);
            if (first.IndexOf("Which one?", StringComparison.Ordinal) < 0) throw new Exception("A held name was not asked: " + first);
            Names.Correct("yes");
            if (!File.ReadAllText(rudiminas).Contains("Rudcia")) throw new Exception("His yes did not add the heard name: " + File.ReadAllText(rudiminas));
            if (PeopleNotes.Identify("Rudcia").Notes.Count != 1) throw new Exception("A learned name was not found again.");
            if (PeopleNotes.Spoken("Nice.", held, false).IndexOf("Which one?", StringComparison.Ordinal) >= 0) throw new Exception("The same name was asked twice in a day.");
            PeopleResult cut = PeopleNotes.Apply(PeopleNotes.Parse("Rudta | Plays | chess"));
            if (PeopleNotes.Spoken("Nice.", cut, false).IndexOf("Which one?", StringComparison.Ordinal) < 0) throw new Exception("A second held name was not asked.");
            Names.Correct("no, it's Vaidas");
            if (!File.ReadAllText(Path.Combine(people, "Vaidas.md")).Contains("Rudta")) throw new Exception("His named answer did not add the heard name to that person.");
            PeopleResult quiet = PeopleNotes.Apply(PeopleNotes.Parse("Bill | Plays | chess"));
            if (PeopleNotes.Spoken("Nice.", quiet, false).IndexOf("Which one?", StringComparison.Ordinal) < 0) throw new Exception("A third held name was not asked.");
            Names.Correct("someone new");
            if (File.ReadAllText(Path.Combine(people, "Ezra Bilson.md")).Contains("Bill")) throw new Exception("Someone new still added an alias.");
            ForkSettings.Current.LockLeagueClient = true;
            Lockout.LeaguePhase = "InProgress";
            Lockout.Recheck();
            PeopleResult game = PeopleNotes.Apply(PeopleNotes.Parse("Rudc | Plays | chess"));
            if (game.Held.Count != 1 || PeopleNotes.Spoken("Nice.", game, false) != "Nice.") throw new Exception("A held name was asked during a game.");
            string claimed = PeopleNotes.Spoken("Saving it now.", game, false);
            if (claimed.IndexOf("Saving it", StringComparison.Ordinal) >= 0 || claimed.IndexOf("didn't save", StringComparison.Ordinal) < 0) throw new Exception("A false save claim survived when the held name was not asked: " + claimed);
        }
    }
}
