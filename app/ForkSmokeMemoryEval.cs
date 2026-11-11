using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeMemoryEval(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            string folder = Path.Combine(Path.GetTempPath(), "capri-eval-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Directory.CreateDirectory(folder);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                List<string> passed = new List<string>();
                List<string> limits = new List<string>();
                foreach (MemoryCase test in MemoryEval.Cases())
                {
                    string verdict = MemoryEval.Run(test, folder, null, true);
                    if (verdict == "LIMIT") limits.Add(MemoryEval.Line(test, verdict));
                    else if (verdict == "PASS") passed.Add(test.Id);
                    else throw new Exception(MemoryEval.Line(test, verdict));
                }
                MemoryCase blind = MemoryEval.Cases()[0];
                blind.Fake = "NONE";
                string missed = MemoryEval.Run(blind, folder, null, true);
                if (missed == "PASS") throw new Exception("The test set passed a case where nothing was saved, so it cannot catch a miss.");
                if (missed.IndexOf("the model said: NONE", StringComparison.Ordinal) < 0) throw new Exception("A failed case does not show what the model answered, so a live miss cannot be read: " + missed);
                if (passed.Count + limits.Count != 23 || PeopleNotes.Pending != 0) throw new Exception("The test set did not hold the twenty-three cases from his real failures, plans, details, payment, orders, his address and a PIN, or a case left a pass waiting.");
                checks.Add("PASS memory test set: his real failures replayed through the real frame and writers with a faked model (" + string.Join(", ", passed.ToArray()) + "); a miss is caught and shows the model's answer; the same cases run against the real model with Capri.exe --memory-eval <folder> (ci/memory-eval.ps1), in temp vaults only");
                checks.AddRange(limits);
            }
            finally
            {
                PeopleNotes.Extract = keptExtract;
                ActivityLog.FilePath = keptLog;
                ForkSettings.Current = keptSettings;
                MemoryQueue.Reset();
                ChatSummary.Reset();
                Names.Reset();
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }
    }
}
