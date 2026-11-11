using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeUnderstandReplay(List<string> checks)
        {
            Dictionary<string, string[]> days = new Dictionary<string, string[]>();
            days["2026-11-02"] = new[] { "# 2026-11-02", "", "10:00 him: open spotify", "10:00 her: On it.", "10:05 him: what's up", "10:05 her: Not much, choom. Want to hear a joke?", "10:07 him: send it to him", "10:07 her: Which one, Rudce or Vaidas?", "10:09 she saw: a game", "10:10 him: (heartbeat)", "10:10 her: Your dentist is at three." };
            days["2026-11-04"] = new[] { "# 2026-11-04", "", "11:00 him: play some music", "11:00 her: Sure." };
            string[] activity =
            {
                "2026-11-02 10:00:20  open_app  spotify  ->  opened",
                "2026-11-02 10:00:40  open_app  spotify again  ->  opened",
                "2026-11-02 10:05:30  memory  pass  ->  kept 0",
                "2026-11-02 10:20:00  open_app  late  ->  opened",
                "2026-11-04 11:00:10  play_music  something  ->  playing"
            };
            string situation = Situation.Line(new DateTime(2026, 11, 2, 10, 0, 5), "voice chat", null, new List<string> { "Rudce" }, false, false, "open | spotify");
            if (situation != "2026-11-02 10:00:05 | voice chat | game: none | call: Rudce | listen: off | home | open / spotify") throw new Exception("The situation line was not written as expected: " + situation);
            List<HeardCase> cases = ReplayUnderstand.Cases(days, activity, new[] { situation });
            if (cases.Count != 4) throw new Exception("The understanding set did not keep one case per real line of his (her own lines, what she saw and system lines left out): " + cases.Count);
            if (cases[0].Reading != "DO" || cases[0].What != "open_app" || cases[1].Reading != "CHAT" || cases[2].Reading != "ASK" || cases[3].Reading != "DO" || cases[3].What != "play_music") throw new Exception("Her readings were not taken from what she did: " + cases[0].Reading + cases[0].What + "/" + cases[1].Reading + "/" + cases[2].Reading + "/" + cases[3].Reading + cases[3].What);
            if (cases[0].Situation.IndexOf("call: Rudce", StringComparison.Ordinal) < 0 || cases[1].Situation.Length != 0) throw new Exception("The situation line was not joined to his line, or joined to the wrong one.");
            string folder = Path.Combine(Path.GetTempPath(), "capri-understand-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string labels = Path.Combine(folder, "understanding-labels.txt");
                File.WriteAllText(labels, cases[0].Id + " DO open_app/open_website # spotify\n" + cases[1].Id + " CHAT\n" + cases[2].Id + "\tDO discord\n" + cases[3].Id + " DO play_music\nbad line\n" + "u0000 MAYBE\n");
                ReplayUnderstand.Label(cases, ReplayUnderstand.ReadLabels(labels));
                if (!ReplayUnderstand.Right(cases[0]) || !ReplayUnderstand.Right(cases[1]) || ReplayUnderstand.Right(cases[2]) || !ReplayUnderstand.Right(cases[3])) throw new Exception("The understanding labels were read or compared wrongly.");
                string score = ReplayUnderstand.Score(cases);
                if (score.IndexOf("tune (to 3 Nov): labelled 3, right 2 (66 %), asked needlessly 1, missed asks 0, wrong reading 0", StringComparison.Ordinal) < 0 || score.IndexOf("test (from 4 Nov): labelled 1, right 1 (100 %)", StringComparison.Ordinal) < 0) throw new Exception("The understanding score was wrong: " + score);
                if (score.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0 || score.IndexOf("Rudce", StringComparison.Ordinal) >= 0) throw new Exception("The understanding score printed his words.");

                string vault = Path.Combine(folder, "vault");
                Directory.CreateDirectory(Path.Combine(vault, "People"));
                Directory.CreateDirectory(Path.Combine(vault, ".obsidian"));
                Directory.CreateDirectory(Path.Combine(vault, "Capri", "backups"));
                Directory.CreateDirectory(Path.Combine(vault, "Capri", "chats"));
                File.WriteAllText(Path.Combine(vault, "People", "Rudce.md"), "# Rudce\n");
                File.WriteAllText(Path.Combine(vault, ".obsidian", "x.md"), "x");
                File.WriteAllText(Path.Combine(vault, "Capri", "backups", "old.md"), "old");
                File.WriteAllText(Path.Combine(vault, "Capri", "chats", "2026-11-02.md"), "# 2026-11-02\n");
                File.WriteAllText(Path.Combine(vault, "People", "Rudce.sync-conflict-20261102-ABC.md"), "conflict");
                string snaps = Path.Combine(folder, "snaps");
                DateTime night = new DateTime(2026, 11, 2, 22, 0, 0);
                string taken = Situation.Snapshot(vault, snaps, night);
                string day = Path.Combine(snaps, "2026-11-02");
                if (taken != "2026-11-02 (2 notes)" || !File.Exists(Path.Combine(day, "People", "Rudce.md")) || !File.Exists(Path.Combine(day, "Capri", "chats", "2026-11-02.md")) || Directory.Exists(Path.Combine(day, ".obsidian")) || Directory.Exists(Path.Combine(day, "Capri", "backups"))) throw new Exception("The vault snapshot copied the wrong notes: " + taken);
                if (Situation.Snapshot(vault, snaps, night.AddHours(1)) != null) throw new Exception("A second snapshot on the same day was taken.");
                for (int extra = 1; extra <= Situation.KeepSnapshots + 2; extra++) Directory.CreateDirectory(Path.Combine(snaps, night.AddDays(-extra).ToString("yyyy-MM-dd")));
                Situation.Prune(snaps);
                if (Directory.GetDirectories(snaps).Length != Situation.KeepSnapshots || !Directory.Exists(day)) throw new Exception("Old vault snapshots were not pruned to the newest " + Situation.KeepSnapshots + ".");
            }
            finally
            {
                try { Directory.Delete(folder, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            List<RecallCase> recall = ReplayMemory.Read(new[]
            {
                "# id | cut | kind | question | expected",
                "m01 | 2026-11-03 19:30 | knowledge-update | What is my Discord name? | sampleuser_",
                "m02 | 2026-11-03 19:30 | abstention | When is Bill's birthday? | not on file # a note",
                "m03 | 2026-11-03 19:30 | made-up-kind | ? | ?",
                "m04 | 2026-13-40 19:30 | temporal | bad date | x",
                "m05 | 2026-11-03 19:30 | her-own-actions | Who did you message today? | Rudce and Vaidas"
            });
            if (recall.Count != 3 || recall[1].Expected != "not on file" || recall[2].Kind != "her-own-actions") throw new Exception("The memory cases were not read (kinds, dates and comments checked): " + recall.Count);
            if (ReplayMemory.Verdict("RIGHT") != ReplayMemory.Right || ReplayMemory.Verdict("**Wrong**") != ReplayMemory.Wrong || ReplayMemory.Verdict("maybe") != "NO VERDICT" || ReplayMemory.Verdict(null) != "NO VERDICT") throw new Exception("The memory judge's verdict was misread.");
            recall[0].Verdict = ReplayMemory.Right;
            recall[1].Verdict = ReplayMemory.Wrong;
            recall[2].Verdict = "NO ANSWER";
            recall[0].Vault = "live";
            recall[1].Vault = recall[2].Vault = "2026-11-03";
            string remembered = ReplayMemory.Score(recall);
            if (remembered.IndexOf("knowledge-update 1 of 1", StringComparison.Ordinal) < 0 || remembered.IndexOf("all 1 of 3 (33 %)", StringComparison.Ordinal) < 0 || remembered.IndexOf("not right: m02, m05 (no answer)", StringComparison.Ordinal) < 0 || remembered.IndexOf("snapshot 2, with today's vault 1", StringComparison.Ordinal) < 0 || remembered.IndexOf("sampleuser", StringComparison.Ordinal) >= 0) throw new Exception("The memory score was wrong or printed his facts: " + remembered);
            if (ReplayMemory.JudgeRule.IndexOf("made-up value is WRONG", StringComparison.Ordinal) < 0 || ReplayMemory.AnswerRule.IndexOf("never guess", StringComparison.Ordinal) < 0) throw new Exception("The memory set lost its no-guessing rules.");
            List<IdentityCase> names = ReplayIdentity.Read(new[] { "# id | heard | expected", "i01 | Rudce | Rudiminas", "i02 | Lukas | NEW", "bad line", "i03 | Dario | Rudiminas", "i04 | Domantas | NEW" });
            PersonGuess sure = new PersonGuess();
            sure.Notes.Add(@"C:\vault\People\Rudiminas.md");
            PersonGuess none = new PersonGuess();
            PersonGuess unsure = new PersonGuess();
            unsure.Unclear = true;
            if (names.Count != 4 || ReplayIdentity.Judge(names[0], sure) != ReplayIdentity.Right || ReplayIdentity.Judge(names[1], none) != ReplayIdentity.Right || ReplayIdentity.Judge(names[2], none) != ReplayIdentity.Missed || ReplayIdentity.Judge(names[3], sure) != ReplayIdentity.Merged || ReplayIdentity.Judge(names[2], unsure) != ReplayIdentity.Held) throw new Exception("The identity cases were read or judged wrong.");
            names[0].Verdict = ReplayIdentity.Right;
            names[1].Verdict = ReplayIdentity.Right;
            names[2].Verdict = ReplayIdentity.Missed;
            names[3].Verdict = ReplayIdentity.Merged;
            string who = ReplayIdentity.Score(names);
            if (who.IndexOf("right 2\n", StringComparison.Ordinal) < 0 || who.IndexOf("missed 1 (i03)", StringComparison.Ordinal) < 0 || who.IndexOf("wrong person 1 (i04)", StringComparison.Ordinal) < 0 || who.IndexOf("Rudce", StringComparison.Ordinal) >= 0) throw new Exception("The identity score was wrong or printed names: " + who);
            checks.Add("PASS identity replay set: names as her ears and the memory pass got them, each with the person it really is or NEW, run through her real person matcher on a copy of the vault and scored right, held (she would ask), missed (a new person would be made) or wrong person, with ids only");
            checks.Add("PASS memory replay set: questions about his days with a cut time and a kind (knowledge-update, temporal, multi-day, her-own-actions, abstention) are read from a local file, asked against that night's vault snapshot (a copy, never the real vault) or today's vault, judged RIGHT or WRONG by a small model where a made-up answer to a fact that isn't on file is WRONG, and scored per kind with ids only");
            checks.Add("PASS understanding replay set: his real lines from the chat archive with what she did (a tool within three minutes is DO, a clarifying question is ASK, else CHAT) and the situation line beside them (where, game, Discord call, listening, home or away); labels CHAT, DO <tool> or ASK, the score split into tune (to 3 Nov) and test days with asked-needlessly and missed asks, counts and ids only; the nightly vault snapshot keeps notes and chats (no app folders, backups or sync conflicts) once a day, 21 days");
        }
    }
}
