using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CapriDesktop
{
    internal static class FreshStart
    {
        private const int ReportMinutes = 60;
        private const int WindowHours = 3;
        private const int LeastUpMinutes = 30;
        private const int WaitSeconds = 60;
        private const int KillWaitSeconds = 10;
        private const string AfterFlag = "--after";
        private static DateTime reported = DateTime.MinValue;

        internal static bool Restarted;

        internal static Func<string, string, bool> Launch = delegate(string exe, string args)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo(exe, args);
                start.UseShellExecute = false;
                start.WorkingDirectory = System.IO.Path.GetDirectoryName(exe);
                using (Process.Start(start)) return true;
            }
            catch (Exception) { return false; }
        };

        internal static void Reset()
        {
            reported = DateTime.MinValue;
        }

        internal static long Megabytes()
        {
            try
            {
                using (Process self = Process.GetCurrentProcess())
                {
                    self.Refresh();
                    return self.WorkingSet64 / (1024 * 1024);
                }
            }
            catch (Exception) { return 0; }
        }

        internal static void Report(DateTime now)
        {
            if (reported != DateTime.MinValue && (now - reported).TotalMinutes < ReportMinutes) return;
            reported = now;
            ActivityLog.Write("memory", "process", Megabytes() + " MB working set");
        }

        internal static string Due(DateTime now, ForkSettings settings, long megabytes, DateTime started)
        {
            if (!settings.FreshStartOn) return null;
            DateTime mark = now.Date.AddHours(settings.FreshStartHour);
            if (now >= mark && now < mark.AddHours(WindowHours) && started < mark) return "daily";
            if (megabytes >= settings.FreshStartMB && (now - started).TotalMinutes >= LeastUpMinutes) return "memory";
            return null;
        }

        internal static bool Safe(bool game, bool job, bool busy)
        {
            return !game && !job && !busy && PeopleNotes.Passes == 0;
        }

        internal static bool Go(string reason, long megabytes, string exe, int pid, Action close)
        {
            if (!Launch(exe, AfterFlag + " " + pid))
            {
                ActivityLog.Write("fresh start", reason, "could not start the new Capri, staying open");
                return false;
            }
            ActivityLog.Write("fresh start", reason, megabytes + " MB");
            close();
            return true;
        }

        internal static string[] After(string[] args)
        {
            List<string> rest = new List<string>(args ?? new string[0]);
            int at = rest.IndexOf(AfterFlag);
            if (at < 0) return rest.ToArray();
            int pid = 0;
            bool known = at + 1 < rest.Count && int.TryParse(rest[at + 1], out pid);
            rest.RemoveRange(at, known ? 2 : 1);
            Restarted = true;
            if (known) WaitFor(pid);
            return rest.ToArray();
        }

        private static void WaitFor(int pid)
        {
            try
            {
                using (Process old = Process.GetProcessById(pid))
                {
                    if (old.WaitForExit(WaitSeconds * 1000)) return;
                    string mine;
                    using (Process self = Process.GetCurrentProcess()) mine = self.ProcessName;
                    if (!string.Equals(old.ProcessName, mine, StringComparison.OrdinalIgnoreCase)) return;
                    old.Kill();
                    old.WaitForExit(KillWaitSeconds * 1000);
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
