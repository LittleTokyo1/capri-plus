using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private sealed class SmokeKept
        {
            private ForkSettings settings;
            private string path, log, phase, brief;
            private Func<string, bool> running;
            private Func<int, bool> keys;
            private Func<string> front;
            private bool watch, checkIns;

            internal static SmokeKept Take(Companion app)
            {
                SmokeKept kept = new SmokeKept();
                kept.settings = ForkSettings.Current;
                kept.path = ForkSettings.FilePath;
                kept.log = ActivityLog.FilePath;
                kept.phase = Lockout.LeaguePhase;
                kept.brief = League.Brief;
                kept.running = Lockout.Running;
                kept.keys = app.KeyIsDown;
                kept.front = Foreground;
                kept.watch = app.preferences.Watch;
                kept.checkIns = app.preferences.CheckIns;
                return kept;
            }

            internal void Put(Companion app)
            {
                ForkSettings.Current = settings;
                ForkSettings.FilePath = path;
                ActivityLog.FilePath = log;
                Lockout.Running = running;
                Lockout.LeaguePhase = phase;
                Lockout.Recheck();
                League.Brief = brief;
                app.KeyIsDown = keys;
                Foreground = front;
                app.preferences.Watch = watch;
                app.preferences.CheckIns = checkIns;
            }
        }

        private void Section(List<string> checks, SmokeKept kept, string name, Action run)
        {
            if (!SmokeRun.EnterSection(name)) return;
            try { run(); }
            catch (Exception problem)
            {
                TestFailed = true;
                checks.Add("FAIL section " + name + ": " + problem.Message);
                kept.Put(this);
            }
            finally { SmokeRun.LeaveSection(name, checks); }
        }

        internal static void SmokeRunner(List<string> checks, string folder)
        {
            Directory.CreateDirectory(folder);
            SmokeOptions plain = SmokeOptions.Parse(new[] { "--smoke-test", folder });
            SmokeOptions some = SmokeOptions.Parse(new[] { "--smoke-test", folder, "--only", "SmokePhone, SmokeShortcutWiring" });
            if (plain.Only.Length != 0 || some.Only.Length != 2 || some.Only[1] != "SmokeShortcutWiring") throw new Exception("--only was read wrong.");
            foreach (string[] bad in new[] { new[] { "--smoke-test", folder, "--fast" }, new[] { "--smoke-test", folder, "--only", "SmokePhone,," } })
            {
                bool refused = false;
                try { SmokeOptions.Parse(bad); } catch (ArgumentException) { refused = true; }
                if (!refused) throw new Exception("A bad smoke option was taken: " + string.Join(" ", bad));
            }
            SmokeRun.Saved live = SmokeRun.Save();
            try { SmokeRunnerParts(folder, some); }
            finally { SmokeRun.Restore(live); }
            checks.Add("PASS smoke runner: --only names sections, progress.txt round trip, a section's own 180 s limit then back to its step, checks written per section, an unmatched name told, a hung section named with where it was stuck, totals counted");
        }

        private static void SmokeRunnerParts(string folder, SmokeOptions some)
        {
            int me = Process.GetCurrentProcess().Id;
            SmokeRun.Open(folder, some);
            SmokeRun.Begin("step", "3", 420);
            string[] progress = SmokeRun.ReadProgress(me);
            if (progress == null || progress[0] != "step" || progress[1] != "3" || progress[3] != "420") throw new Exception("progress.txt didn't round-trip.");
            if (SmokeRun.ReadProgress(me + 1) != null) throw new Exception("Another process's progress was read as ours.");
            if (!SmokeRun.Wants("smokephone") || SmokeRun.Wants("SmokeLockout")) throw new Exception("--only picked the wrong sections.");
            List<string> found = new List<string> { "PASS one" };
            if (SmokeRun.EnterSection("SmokeLockout")) throw new Exception("A section left out by --only ran.");
            if (!SmokeRun.EnterSection("SmokePhone")) throw new Exception("A section named in --only didn't run.");
            progress = SmokeRun.ReadProgress(me);
            if (progress == null || progress[0] != "section" || progress[1] != "SmokePhone" || progress[3] != "180") throw new Exception("A section didn't take its own time limit.");
            found.Add("PASS two");
            SmokeRun.LeaveSection("SmokePhone", found);
            progress = SmokeRun.ReadProgress(me);
            if (progress == null || progress[0] != "step" || progress[1] != "3") throw new Exception("Leaving a section didn't go back to its step.");
            if (File.ReadAllLines(Path.Combine(folder, "smoke-test.txt")).Length != 2) throw new Exception("A section's checks weren't written when it ended.");
            SmokeRun.ValidateSelection(found);
            if (!found.Contains("FAIL --only: no section matches SmokeShortcutWiring") || found.Contains("FAIL --only: no section matches SmokePhone")) throw new Exception("An --only name that matched nothing wasn't told.");
            File.WriteAllText(Path.Combine(folder, "hangs.txt"), "section SmokeOther: Stuck at: A" + Environment.NewLine + "section SmokePhone: Stuck at: Wait < SmokePhone" + Environment.NewLine);
            string[] hung = new[] { "section", "SmokePhone", "0", "180" };
            if (SmokeRun.Stuck(hung) != ". Stuck at: Wait < SmokePhone" || SmokeRun.Stuck(new[] { "section", "SmokeOther", "0", "180" }) != "") throw new Exception("The hang note was read wrong.");
            if (SmokeRun.Outcome(hung, true, 180, -1, SmokeRun.Stuck(hung)) != "FAIL section SmokePhone: timed out after 180 s. Stuck at: Wait < SmokePhone") throw new Exception("A hung section wasn't named.");
            if (SmokeRun.Outcome(new[] { "step", "4", "0", "420" }, false, 420, 1, "") != "FAIL step 4: worker exited 1") throw new Exception("A step that ended her wasn't named.");
            if (SmokeRun.Outcome(null, true, 180, -1, "") != "FAIL smoke startup: timed out after 180 s") throw new Exception("A hang before the first step wasn't told.");
            if (SmokeRun.Outcome(new[] { "done", "closing", "0", "30" }, false, 30, 0, "") != null) throw new Exception("A finished run was told as a failure.");
            if (SmokeRun.Outcome(new[] { "done", "closing", "0", "30" }, true, 30, -1, "") != "FAIL smoke shutdown: timed out after 30 s") throw new Exception("A stuck shutdown wasn't told.");
            if (SmokeRun.Totals(new List<string> { "PASS a", "PASS b", "LIMIT c", "FAIL d" }) != "TOTAL PASS 2 / LIMIT 1 / FAIL 1 / SKIP 0") throw new Exception("The totals line was counted wrong.");
            SmokeRun.Close();
            ManualResetEvent hold = new ManualResetEvent(false);
            Thread stuck = new Thread(delegate() { hold.WaitOne(); });
            stuck.IsBackground = true;
            stuck.Start();
            Thread.Sleep(100);
            string where = SmokeRun.Where(stuck);
            hold.Set();
            stuck.Join(2000);
            if (!where.StartsWith(" Stuck at: ", StringComparison.Ordinal) || where.IndexOf("WaitOne", StringComparison.Ordinal) < 0) throw new Exception("Where a stuck thread waits wasn't read: " + where);
        }
    }
}
