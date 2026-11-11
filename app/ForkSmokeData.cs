using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static string RealData { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri"); } }

        // A smoke run never reads or writes his own files: these have no settings folder of their own, so they move out at once.
        private static void SmokeDataAway(bool smoke)
        {
            if (!smoke) return;
            System.Threading.ThreadPool.SetMinThreads(64, 64);
            string away = Path.Combine(Path.GetTempPath(), "capri-smoke-data-" + Guid.NewGuid().ToString("N"));
            AskInbox.FilePath = Path.Combine(away, "ask.txt");
            OpenAsks.FilePath = Path.Combine(away, "open-asks.txt");
            PhoneActions.FilePath = Path.Combine(away, "phone-actions.json");
            PhoneAuth.FilePath = Path.Combine(away, "phones.dat");
            ProjectHook.Folder = Path.Combine(away, "project-hooks");
            SchoolMail.FilePath = Path.Combine(away, "school-mail.txt");
            StudyRoutines.FilePath = Path.Combine(away, "study-routines.txt");
            BrowserRecall.FilePath = Path.Combine(away, "browser-recall.txt");
        }

        private static void SmokeDataKept(List<string> checks)
        {
            string real = RealData.TrimEnd('\\') + "\\";
            foreach (string path in new[] { AskInbox.FilePath, OpenAsks.FilePath, PhoneActions.FilePath, PhoneAuth.FilePath, ProjectHook.Folder, SchoolMail.FilePath, StudyRoutines.FilePath, BrowserRecall.FilePath })
                if (path == null || path.StartsWith(real, StringComparison.OrdinalIgnoreCase)) throw new Exception("A smoke run can reach his own data: " + path);
            checks.Add("PASS the smoke never touches his own data: the --ask inbox, open asks, phone actions and pairings, project hooks, school mail, study routines and her kept browser findings all live in a temp folder for the run");
        }
    }
}
