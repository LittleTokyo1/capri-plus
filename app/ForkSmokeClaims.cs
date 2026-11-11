using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeClaims(List<string> checks, string folder)
        {
            string root = Path.Combine(folder, "claims-vault");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            ForkSettings keptSettings = ForkSettings.Current;
            Func<bool> keptReachable = Delivery.Reachable;
            Func<string, bool> keptSend = Delivery.Send;
            string keptLog = ActivityLog.FilePath;
            List<string> sent = new List<string>();
            bool failing = false;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ActivityLog.FilePath = Path.Combine(folder, "claims-activity.log");
                DateTime now = DateTime.Now;

                string heads = "I already messaged Rudce about tonight. Your dentist is at three.";
                if (Claims.Check(heads, root, now, "heartbeat") != "Your dentist is at three.") throw new Exception("A claim she messaged someone, with no line in her action log, was kept: " + Claims.Check(heads, root, now, "heartbeat"));
                if (Claims.Check("I've pinged Rudce for you.", root, now, "heartbeat") != null) throw new Exception("A line that was only an unbacked claim was still sent.");
                if (Claims.Check("Should I message Rudce about tonight?", root, now, "heartbeat") != "Should I message Rudce about tonight?") throw new Exception("An offer to message someone was taken for a claim.");
                if (Claims.Check(heads, null, now, "heartbeat") != heads) throw new Exception("With no vault a line was changed.");
                Did.Append(root, now, "messaged Rudce on Discord");
                if (Claims.Check(heads, root, now, "heartbeat") != heads) throw new Exception("A claim backed by her action log was dropped.");
                if (!File.ReadAllText(ActivityLog.FilePath).Contains("claim dropped from heartbeat")) throw new Exception("A dropped claim wasn't logged.");
                if (!Did.Block(root, now).Contains("messaged Rudce on Discord")) throw new Exception("The action log block for the heartbeat judge didn't read the vault it was given.");
                if (Did.Line("schedule", "sent after the game", "Game's over. You wanted me to remind you: drink water.", null) == null) throw new Exception("An after-game reminder sent to Discord isn't in her action log.");

                const string asked = "can you open Steam and update Cyberpunk";
                if (Claims.Unpromised("On it, choom! Have fun out there.", asked) != "Have fun out there.") throw new Exception("A promise with nothing behind it was kept: " + Claims.Unpromised("On it, choom! Have fun out there.", asked));
                if (Claims.Unpromised("I'll send it to Rudce right now.", asked) != Claims.Nothing) throw new Exception("A reply that was only an empty promise wasn't made honest.");
                if (Claims.Unpromised("I'm on your side, choom.", asked) != "I'm on your side, choom.") throw new Exception("Plain talk was cut as a promise.");
                if (Claims.Unpromised("Want me to set a reminder for six?", asked) != "Want me to set a reminder for six?") throw new Exception("An offer was cut as a promise.");
                if (Claims.Unpromised("I'll do my best, choom.", "you're the best") != "I'll do my best, choom.") throw new Exception("Banter with no ask was cut as an empty promise.");
                if (Claims.Unpromised("I'll set it for six.", "set a reminder at six") != Claims.Nothing || Claims.Unpromised("I'll send it right now.", "tell him shut up") != Claims.Nothing) throw new Exception("A promise after a plain ask with no tool behind it was kept.");
                if (Claims.Unpromised("I'll fix it later, choom.", "my code is broken again") != "I'll fix it later, choom.") throw new Exception("A promise after a statement with no ask was cut.");

                ForkSettings.Current.DiscordOn = true;
                Delivery.Reachable = delegate { return true; };
                Delivery.Send = delegate(string text) { lock (sent) sent.Add(text); return !failing; };
                string path = Path.Combine(folder, "claims-schedule.txt");
                if (File.Exists(path)) File.Delete(path);
                Scheduler.Add(path, new ScheduleItem { Due = now.AddHours(Scheduler.AfterGameHours), Repeat = Scheduler.AfterGame, Text = "drink water" });
                AfterJobs.Reset();
                AfterJobs.Hold("use_computer", new ToolArguments { Task = "update Cyberpunk" }, now);
                afterGameDue = true;
                afterGameAwayDone = false;
                if (AfterGameAway(now, false, path) || sent.Count != 0) throw new Exception("At the PC an after-game reminder went to Discord.");
                failing = true;
                if (!AfterGameAway(now, true, path)) throw new Exception("Away after the game, the reminder wasn't sent to Discord.");
                AfterGameWait();
                if (sent.Count != 1 || Scheduler.Load(path).Count != 1 || !afterGameDue) throw new Exception("A failed Discord send lost the after-game reminder.");
                failing = false;
                afterGameRetryAt = DateTime.MinValue;
                AfterGameAway(now, true, path);
                AfterGameWait();
                if (sent.Count != 2 || sent[1] != "Game's over. You wanted me to remind you: drink water." || Scheduler.Load(path).Count != 0) throw new Exception("Away after the game, the reminder reached Discord as: " + string.Join(" || ", sent.ToArray()));
                if (!afterGameDue || !AfterJobs.Waiting(now) || !AfterGameAway(now, true, path) || sent.Count != 2) throw new Exception("A held PC job was asked about on Discord, or the reminder went twice.");
                File.Delete(path);
                checks.Add("PASS honest claims and after-game Discord: a line she starts herself that says she messaged someone is checked against her action log and the claim is cut when nothing backs it; a reply promising a job with nothing behind it loses the promise; the heartbeat judge reads her action log; away after a game, the after-game reminder goes to his Discord once (kept on a failed send) while held PC jobs wait for him at the PC");
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                Delivery.Reachable = keptReachable;
                Delivery.Send = keptSend;
                ActivityLog.FilePath = keptLog;
                AfterJobs.Reset();
                afterGameDue = false;
                afterGameAwayDone = false;
                afterGameRetryAt = DateTime.MinValue;
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }

        private void AfterGameWait()
        {
            for (int wait = 0; wait < 500 && afterGameSending; wait++) { Application.DoEvents(); Thread.Sleep(10); }
            Application.DoEvents();
        }
    }
}
