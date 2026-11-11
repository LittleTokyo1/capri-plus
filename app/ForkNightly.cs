using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool tidying;

        internal static bool TidyMay(ForkSettings settings, bool game, bool claude)
        {
            return settings.NightlyOn && !settings.Paused && !game && claude;
        }

        private void ForkNightlyTick()
        {
            if (testing || tidying) return;
            ForkSettings settings = ForkSettings.Current;
            Cloud remote = brain.Remote;
            if (!TidyMay(settings, Lockout.Active, remote != null && remote.Claude)) return;
            DateTime now = DateTime.Now;
            if (!Nightly.Due(now, settings.NightlyHour)) return;
            StartTidy(now, false);
        }

        private void StartTidy(DateTime now, bool force)
        {
            tidying = true;
            Cloud remote = brain.Remote;
            string root = Vault.Root, core = Brain.MemoryPath;
            ThreadPool.QueueUserWorkItem(delegate
            {
                TidyPrepare(force);
                try
                {
                    int cleaned = NoteTidy.Run(root, now);
                    if (cleaned > 0) ActivityLog.Write("tidy", "cleaned " + cleaned + (cleaned == 1 ? " person note" : " person notes"), "title heading, placeholders and repeated lines; backups in Capri/backups");
                }
                catch (Exception error) { Brain.Log("note tidy", -1, error.GetType().Name + ": " + error.Message); }
                try
                {
                    try { Nightly.Run(now, remote, root, core, force); }
                    catch (Exception error)
                    {
                        Brain.Log("nightly", -1, error.GetType().Name + ": " + error.Message);
                        Nightly.Fail(now);
                    }
                    try { if (ForkSettings.Current.SummarizeChats) DayJournal.Run(root, now, DayJournal.Asker(remote)); }
                    catch (Exception error) { Brain.Log("day page", -1, error.GetType().Name + ": " + error.Message); }
                    try { NightReview.Run(now, remote, root, core); }
                    catch (Exception error) { Brain.Log("night review", -1, error.GetType().Name + ": " + error.Message); }
                    try
                    {
                        string snap = Situation.Snapshot(root, Situation.SnapshotFolder(), now);
                        if (snap != null) ActivityLog.Write("replay", "vault snapshot", snap);
                    }
                    catch (Exception error) { Brain.Log("vault snapshot", -1, error.GetType().Name + ": " + error.Message); }
                    try { PeopleNotes.DrainNight(remote, now, MemoryQueue.NightMost); }
                    catch (Exception error) { Brain.Log("memory night", -1, error.GetType().Name + ": " + error.Message); }
                }
                finally { tidying = false; }
            });
        }

        private void TidyPrepare(bool force)
        {
            if (!force) return;
            try { SummarizeChat(); }
            catch (Exception error) { Brain.Log("chat notes", -1, error.GetType().Name + ": " + error.Message); }
        }

        private string TidyNow()
        {
            Cloud remote = brain.Remote;
            if (testing) return "Tidying is off while testing.";
            if (tidying) return "I'm already filing my notes.";
            if (Vault.Root == null) return "I don't have a vault to tidy yet.";
            if (remote == null || !remote.Claude) return "I need a Claude key for that, choom.";
            if (Lockout.Active) return "A game's running, so I'll tidy up after it.";
            StartTidy(DateTime.Now, true);
            return "Filing my notes now. What I did goes into Capri/tidy.md in the vault.";
        }
    }
}
