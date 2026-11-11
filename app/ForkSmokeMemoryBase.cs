using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeMemoryBase(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            string folder = Path.Combine(Path.GetTempPath(), "capri-base-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Names.Reset();
                MemoryQueue.Reset();
                string vault = Path.Combine(folder, "vault");
                if (!Vault.Open(vault, null)) throw new Exception("The memory vault did not open.");
                Brain.MemoryPath = Path.Combine(vault, Vault.CoreName);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                BaseShape(checks);
                BaseLines(checks, vault);
                BaseProcess(checks);
            }
            finally
            {
                Quick.Post = keptPost;
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

        private static void BaseShape(List<string> checks)
        {
            PeopleNotes.Extract = delegate { return "LESSON | Typed into the Claude window instead of running start-capri.ps1 | Run it in PowerShell\nThat was a mixup with the windows"; };
            PeopleNotes.Learn(null, "thanks choom idk what ur saying will investigate later for now i mark as a bug", null, "Oof, my keystrokes landed in a Claude Code window.");
            string log = File.ReadAllText(ActivityLog.FilePath);
            if (!log.Contains("reply not understood (2 lines, starts LESSON: lesson refused, not 3 parts)")) throw new Exception("A reply the pass could not use did not log its shape: " + log);
            if (log.Contains("Claude window") || log.Contains("mixup") || log.Contains("investigate")) throw new Exception("The shape line leaked words from the reply or from what he said: " + log);
            PeopleNotes.Extract = delegate { return "Rudiminas | Birthday | 30 October"; };
            PeopleNotes.Learn(null, "oh btw it's rudiminas birthday today and he turns twenty", null, null);
            log = File.ReadAllText(ActivityLog.FilePath);
            if (!log.Contains("reply not understood (1 line, starts a word: unknown field)") || log.Contains("Rudiminas")) throw new Exception("A fact with a field she does not keep did not log its shape without the name: " + log);
            checks.Add("PASS memory pass shape: a reply the pass cannot use logs its shape only (line count, first marker, why each line was refused: replayed the 15:46 'mark as a bug' pass whose lesson named a .ps1 file), never his words or the reply's");
        }

        private static void BaseLines(List<string> checks, string vault)
        {
            File.WriteAllText(Path.Combine(vault, "People", "Rudiminas.md"), "# Rudiminas\n- Is: friend\n");
            PeopleNotes.Extract = null;
            Quick.Post = delegate { return Tuple.Create(200, SmokeReply("Rudiminas | Note | Birthday is 30 October\nLESSON | Said happy birthday but did not note his birthday | Save a birthday the moment he says it")); };
            PeopleNotes.Learn(new Cloud("sk-ant-test", "claude-haiku-5-5"), "hmm you didn't note down rudiminas birthday, I told you it was today", null, "Happy birthday to Rudiminas, choom!");
            string note = File.ReadAllText(Path.Combine(vault, "People", "Rudiminas.md"));
            string lessons = Lessons.PathIn(vault);
            if (!note.Contains("30 October") || !File.Exists(lessons) || !File.ReadAllText(lessons).Contains("Save a birthday the moment he says it")) throw new Exception("A two-line memory answer through the real model call lost a line: " + note + " | " + File.ReadAllText(ActivityLog.FilePath));
            PeopleNotes.Extract = delegate { return "Sister | Note | Birthday is 6 November. LESSON | Forgot the sister's birthday | Save birthdays when he says them"; };
            PeopleNotes.Learn(null, "you forgot my sister's birthday, it's 6 November", null, "Got it!");
            if (!File.Exists(Path.Combine(vault, "People", "Sister.md")) || !File.ReadAllText(lessons).Contains("Save birthdays when he says them")) throw new Exception("Two answers the model joined on one line were not split: " + File.ReadAllText(ActivityLog.FilePath));
            checks.Add("PASS memory pass lines: a fact and a lesson in one answer both land (the real call kept its line breaks; the 17:31 live run lost case e because every quick call reply was flattened to one line), and two answers joined on one line after a full stop are split");
        }

        private static void BaseProcess(List<string> checks)
        {
            DateTime start = new DateTime(2026, 10, 30, 16, 0, 0);
            FreshStart.Reset();
            FreshStart.Report(start);
            FreshStart.Report(start.AddMinutes(30));
            FreshStart.Report(start.AddMinutes(61));
            int lines = 0;
            foreach (string line in File.ReadAllLines(ActivityLog.FilePath))
                if (line.Contains("memory  process  ->  ")) lines++;
            if (lines != 2 || !System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(ActivityLog.FilePath), @"memory  process  ->  \d+ MB working set")) throw new Exception("The hourly process memory line was not written once an hour: " + lines);
            if (FreshStart.Megabytes() < 1) throw new Exception("Her own memory use could not be read.");
            checks.Add("PASS process memory log: her working set in MB is written to activity.log once an hour (memory  process), so growth over a long run shows up");
        }
    }
}
