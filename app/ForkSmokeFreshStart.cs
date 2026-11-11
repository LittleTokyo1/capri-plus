using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeFreshStart(List<string> checks)
        {
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string, bool> keptLaunch = FreshStart.Launch;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            string keptRoot = Vault.Root;
            string folder = Path.Combine(Path.GetTempPath(), "capri-fresh-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Directory.CreateDirectory(folder);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                FreshDue(checks);
                FreshSafe(checks, folder);
                FreshAfter(checks);
            }
            finally
            {
                FreshStart.Launch = keptLaunch;
                PeopleNotes.Extract = keptExtract;
                Vault.Root = keptRoot;
                ActivityLog.FilePath = keptLog;
                ForkSettings.Current = keptSettings;
                MemoryQueue.Reset();
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private static void FreshDue(List<string> checks)
        {
            ForkSettings settings = ForkSettings.Current;
            DateTime morning = new DateTime(2026, 10, 31, 5, 10, 0);
            if (!settings.FreshStartOn || settings.FreshStartHour != 5 || settings.FreshStartMB != 4096) throw new Exception("The fresh start defaults are not on, 05:00 and 4096 MB.");
            if (FreshStart.Due(morning, settings, 900, morning.AddHours(-20)) != "daily") throw new Exception("She did not plan her daily fresh start at 05:10 after running all day.");
            if (FreshStart.Due(morning, settings, 900, morning.AddMinutes(-9)) != null) throw new Exception("She would restart again right after a fresh start.");
            if (FreshStart.Due(morning.AddHours(7), settings, 900, morning.AddHours(-20)) != null) throw new Exception("A missed fresh start was done in the middle of the day.");
            if (FreshStart.Due(morning.AddHours(7), settings, 4200, morning.AddHours(-3)) != "memory") throw new Exception("Going past the memory cap did not plan a fresh start.");
            if (FreshStart.Due(morning.AddHours(7), settings, 4200, morning.AddHours(7).AddMinutes(-10)) != null) throw new Exception("A process that just started would restart over the cap in a loop.");
            settings.FreshStartOn = false;
            if (FreshStart.Due(morning, settings, 9000, morning.AddHours(-20)) != null) throw new Exception("The fresh start ran with the setting off.");
            settings.FreshStartOn = true;
            settings.FreshStartMB = 99999;
            settings.Tidy();
            if (settings.FreshStartMB != 10240) throw new Exception("The memory cap went past his 10 GB allowance: " + settings.FreshStartMB);
            settings.FreshStartMB = 4096;
            checks.Add("PASS daily fresh start plan: once a day from 05:00 (setting FreshStartHour, three hours to find a quiet moment, never again right after one) and when her memory passes the cap (FreshStartMB 4096, at most his 10 GB, not in the first 30 minutes), off with FreshStartOn");
        }

        private static void FreshSafe(List<string> checks, string folder)
        {
            if (!FreshStart.Safe(false, false, false)) throw new Exception("A quiet moment was not safe for a fresh start.");
            if (FreshStart.Safe(true, false, false) || FreshStart.Safe(false, true, false) || FreshStart.Safe(false, false, true)) throw new Exception("A fresh start was allowed during a game, a job or while she was busy.");
            string vault = Path.Combine(folder, "vault");
            if (!Vault.Open(vault, null)) throw new Exception("The fresh start vault did not open.");
            Names.Reset();
            MemoryQueue.Reset();
            int during = -1;
            PeopleNotes.Extract = delegate { during = PeopleNotes.Passes; return "NONE"; };
            PeopleNotes.Learn(null, "I usually play football on Sunday mornings with my cousins", null, null);
            if (during != 1 || PeopleNotes.Passes != 0) throw new Exception("A memory pass in flight was not counted, so a fresh start could cut it: " + during);
            List<string> launched = new List<string>();
            FreshStart.Launch = delegate(string exe, string args) { launched.Add(args); return true; };
            bool quit = false;
            FreshStart.Go("daily", 900, @"C:\capri\Capri.exe", 4321, delegate { quit = true; });
            string log = File.ReadAllText(ActivityLog.FilePath);
            if (launched.Count != 1 || launched[0] != "--after 4321" || !quit || !log.Contains("fresh start  daily  ->  900 MB")) throw new Exception("The fresh start did not log, start the new Capri waiting for the old one and then close: " + string.Join("|", launched.ToArray()) + " " + log);
            launched.Clear();
            quit = false;
            FreshStart.Launch = delegate { return false; };
            FreshStart.Go("daily", 900, @"C:\capri\Capri.exe", 4321, delegate { quit = true; });
            if (quit || !File.ReadAllText(ActivityLog.FilePath).Contains("could not start the new Capri")) throw new Exception("She closed even though the new Capri did not start.");
            checks.Add("PASS fresh start moment: never during a game, a job, talking or typing, or a memory pass in flight (counted while it runs); she logs 'fresh start', starts out\\windows\\Capri.exe --after <her pid> and only then closes, and stays open when the new one cannot start");
        }

        private static void FreshAfter(List<string> checks)
        {
            string[] none = FreshStart.After(new[] { "--after", "999999" });
            if (none.Length != 0 || !FreshStart.Restarted) throw new Exception("The --after start did not strip its arguments or did not start quietly.");
            FreshStart.Restarted = false;
            string[] plain = FreshStart.After(new[] { "--smoke-test", "x" });
            if (plain.Length != 2 || FreshStart.Restarted) throw new Exception("A normal start was taken for a fresh start.");
            ProcessStartInfo info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ping.exe"), "-n 3 127.0.0.1");
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            using (Process old = Process.Start(info))
            {
                Stopwatch waited = Stopwatch.StartNew();
                FreshStart.After(new[] { "--after", old.Id.ToString() });
                if (!old.HasExited || waited.ElapsedMilliseconds < 1000) throw new Exception("The new Capri did not wait for the old one to close: " + waited.ElapsedMilliseconds + " ms");
            }
            FreshStart.Restarted = false;
            checks.Add("PASS fresh start hand-over: Capri started with --after <pid> waits for that process to close before taking the one-Capri lock, so exactly one runs, and then starts quietly (no greeting, no arrival)");
        }
    }
}
