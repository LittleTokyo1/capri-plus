using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeMemoryPass(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            Func<bool> keptStop = NightReview.Stop;
            string folder = Path.Combine(Path.GetTempPath(), "capri-memory-pass-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Names.Reset();
                if (!Vault.Open(folder, null)) throw new Exception("The memory vault did not open.");
                string core = Path.Combine(folder, Vault.CoreName);
                Brain.MemoryPath = core;
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                MemoryQueue.Reset();
                NightReview.Stop = delegate { return false; };
                MemoryPassGate();
                MemoryPassLadder();
                MemoryPassNight(core);
                MemoryPassLost();
                checks.Add("PASS memory pass never fails silently: his 'today' after 'tell me the date once more' and 'message Rudce, oh I already did' pass the gate; a call never sent is not counted as a try; a refused, private or notes-off line is never queued or shown; a failed pass retries on less thinking, an unreadable reply goes to Sonnet; after " + MemoryQueue.LiveTries + " tries the night review tries " + MemoryQueue.NightMost + " once a night (stopped by a game) without saving a fact twice; a pass dropped from a full queue is logged as lost and told to her");
            }
            finally
            {
                PeopleNotes.Extract = keptExtract;
                NightReview.Stop = keptStop;
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

        private static void MemoryPassGate()
        {
            if (!PeopleNotes.Gate("today", "Tell me the date once more, plain and simple, and I'll get it locked in.")) throw new Exception("His 'today' after she asked for the date (30 Oct 15:00) was still dropped by the gate.");
            if (PeopleNotes.Gate("lol", "Okay.") || PeopleNotes.Gate("today", "Nice, good luck with it.")) throw new Exception("A short line that answers nothing passed the gate.");
            if (!PeopleNotes.Gate("message Rudce, oh I already did") || PeopleNotes.Gate("message Rudce right now please")) throw new Exception("A command that says it is already done was dropped, or a plain command was let through.");
        }

        private static void MemoryPassLadder()
        {
            if (PeopleNotes.RungOf(1, null) != "medium" || PeopleNotes.RungOf(2, "empty") != "low" || PeopleNotes.RungOf(2, "garbled") != "sonnet" || PeopleNotes.RungOf(3, "empty") != "sonnet") throw new Exception("The retry ladder was wrong.");
            PeopleNotes.Extract = null;
            PeopleNotes.Learn(null, "I start work at eight on Saturdays at the shop", null);
            List<QueuedPass> waiting = MemoryQueue.Waiting();
            if (waiting.Count != 1 || waiting[0].Attempt != 0 || !File.ReadAllText(ActivityLog.FilePath).Contains("(not sent, not counted)")) throw new Exception("A pass that was never sent was counted as a try.");
            MemoryQueue.Clear();
            PeopleNotes.Extract = delegate { return "ME | Note | Their PIN is 4821"; };
            PeopleNotes.Learn(null, "my bank pin is 4821 so keep that somewhere safe for me", null);
            PeopleNotes.Extract = delegate { return null; };
            PeopleNotes.Learn(null, "the code for the door is pin 7319, my bike lock too", null);
            if (MemoryQueue.Count != 0 || PeopleNotes.PendingNote().Contains("4821") || PeopleNotes.PendingNote().Contains("7319")) throw new Exception("A refused or private line was queued for a retry or shown to her.");
            ForkSettings.Current.SummarizeChats = false;
            PeopleNotes.Learn(null, "I start work at eight on Saturdays at the shop", null);
            ForkSettings.Current.SummarizeChats = true;
            if (MemoryQueue.Count != 0) throw new Exception("With notes after chats off, a line was still queued.");
            PeopleNotes.Extract = delegate { return "That sounds like a lovely cat"; };
            PeopleNotes.Learn(null, "My cat Miso is three years old and likes tuna", null);
            waiting = MemoryQueue.Waiting();
            if (waiting.Count != 1 || waiting[0].Attempt != 1 || waiting[0].Failed != "garbled" || PeopleNotes.RungOf(waiting[0].Attempt + 1, waiting[0].Failed) != "sonnet") throw new Exception("A reply she could not read was dropped instead of queued for Sonnet.");
            MemoryQueue.Clear();
        }

        private static void MemoryPassNight(string core)
        {
            DateTime now = DateTime.Now;
            for (int i = 0; i < MemoryQueue.NightMost + 2; i++)
                MemoryQueue.Add(new QueuedPass { Text = "I play padel every Thursday evening, week " + i, Said = now.Ticks, Attempt = MemoryQueue.LiveTries });
            NightReview.Stop = delegate { return true; };
            bool stopped = PeopleNotes.DrainNight(null, now, MemoryQueue.NightMost) == 0 && MemoryQueue.Count == MemoryQueue.NightMost + 2;
            NightReview.Stop = delegate { return false; };
            int asked = 0;
            PeopleNotes.Extract = delegate { asked++; return "NONE"; };
            int tried = PeopleNotes.DrainNight(null, now, MemoryQueue.NightMost);
            if (!stopped || tried != MemoryQueue.NightMost || asked != MemoryQueue.NightMost || MemoryQueue.Count != 2) throw new Exception("The night drain was not capped at " + MemoryQueue.NightMost + " or not stopped by a game: stopped " + stopped + ", tried " + tried + ", left " + MemoryQueue.Count);
            if (PeopleNotes.DrainNight(null, now, MemoryQueue.NightMost) != 0 || MemoryQueue.Count != 2) throw new Exception("A second night drain on the same day tried again, so lines would be lost early.");
            MemoryQueue.Clear();
            now = now.AddDays(1);
            PeopleNotes.Extract = delegate { return "ME | Note | Plays padel on Thursdays"; };
            PeopleNotes.Learn(null, "I play padel on Thursdays now", null);
            MemoryQueue.Add(new QueuedPass { Text = "oh and padel is on thursdays btw", Said = now.Ticks, Attempt = MemoryQueue.LiveTries });
            PeopleNotes.DrainNight(null, now, MemoryQueue.NightMost);
            if (Regex.Matches(File.ReadAllText(core), "Plays padel on Thursdays").Count != 1) throw new Exception("A fact the night drain saved again was written twice.");
        }

        private static void MemoryPassLost()
        {
            MemoryQueue.Clear();
            for (int i = 0; i <= MemoryQueue.MostWaiting; i++) MemoryQueue.Add(new QueuedPass { Text = "waiting line " + i, Said = DateTime.Now.Ticks });
            if (MemoryQueue.Count != MemoryQueue.MostWaiting || MemoryQueue.LostLines(DateTime.Now).Count != 1 || !File.ReadAllText(ActivityLog.FilePath).Contains("lost (the queue was full): waiting line 0") || !PeopleNotes.PendingNote().Contains("You may have missed something he said")) throw new Exception("A pass dropped from a full queue was lost silently.");
            MemoryQueue.Clear();
        }
    }
}
