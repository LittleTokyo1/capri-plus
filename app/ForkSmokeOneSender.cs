using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeOneSender(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            string keptLog = ActivityLog.FilePath;
            Func<bool> keptReachable = Delivery.Reachable;
            Func<string, bool> keptSend = Delivery.Send;
            Func<long> keptIdle = Autonomy.Idle;
            Func<bool> keptGaming = Autonomy.Gaming;
            DateTime keptLast = lastHeartLine;
            string folder = Path.Combine(Path.GetTempPath(), "capri-one-sender-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            List<string> sent = new List<string>();
            bool failing = false;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                ForkSettings settings = ForkSettings.Current;
                settings.DiscordOn = true;
                settings.QuietFrom = 0;
                settings.QuietTo = 0;
                settings.Proactive = 1;
                settings.HomeAway = true;
                Heartbeat.Reset();
                Autonomy.Reset();
                Delivery.Reset(DateTime.UtcNow);
                Autonomy.Gaming = delegate { return false; };
                Autonomy.Idle = delegate { return 0L; };
                Delivery.Reachable = delegate { return true; };
                Delivery.Send = delegate(string text) { lock (sent) sent.Add(text); return !failing; };
                DateTime now = DateTime.Now;

                const string offer = "Want a nudge at six to ping Sam about the squad?";
                Autonomy.Deliver(new List<AutoOffer> { new AutoOffer { Area = "reminders", Text = offer } }, now, folder);
                int direct = SentCount(sent, offer);
                HeartSendNow(now, true);
                HeartSendNow(now, true);
                if (direct != 0 || SentCount(sent, offer) != 1) throw new Exception("An offer while he was away did not reach Discord exactly once through her one sender: " + direct + " direct, " + SentCount(sent, offer) + " in all.");

                const string dentist = "Starts in 40 minutes: Dentist";
                Heartbeat.Enqueue(dentist, now, null, true);
                failing = true;
                HeartSendNow(now, true);
                bool kept = Heartbeat.Waiting() == 1;
                failing = false;
                HeartSendNow(now, true);
                HeartSendNow(now, true);
                if (!kept || Heartbeat.Waiting() != 0 || SentCount(sent, dentist) != 2) throw new Exception("A failed Discord send was not put back and sent once later: kept " + kept + ", tries " + SentCount(sent, dentist) + ", waiting " + Heartbeat.Waiting());

                const string birthday = "Birthday: Mum in 3 days";
                Heartbeat.Enqueue(birthday, now, null, true);
                failing = true;
                for (int i = 0; i < Heartbeat.SendTries + 1; i++) HeartSendNow(now, true);
                failing = false;
                if (SentCount(sent, birthday) != Heartbeat.SendTries || Heartbeat.Waiting() != 0) throw new Exception("A line that kept failing was tried " + SentCount(sent, birthday) + " times, not " + Heartbeat.SendTries + ", or stayed queued.");

                settings.HomeAway = false;
                settings.Proactive = 0;
                const string quietOffer = "Want me to look up the patch notes?";
                const string mail = "Mail from a@b.example just says hi.";
                Heartbeat.Enqueue(quietOffer, now, null, false);
                Heartbeat.Enqueue(mail, now, null, true);
                lastHeartLine = DateTime.MinValue;
                bool quietTook = HeartAway(settings, now);
                WaitHeart();
                lastHeartLine = DateTime.MinValue;
                HeartAway(settings, now);
                WaitHeart();
                if (!quietTook || SentCount(sent, mail) != 1 || SentCount(sent, quietOffer) != 0 || Heartbeat.Waiting() != 1) throw new Exception("On Quiet at home the heads-up did not go to Discord once, or an offer went too: mail " + SentCount(sent, mail) + ", offer " + SentCount(sent, quietOffer));
                settings.Proactive = 2;
                if (HeartAway(settings, now)) throw new Exception("On Chatty at home a heads-up went to Discord instead of being said.");

                string lines = File.Exists(ActivityLog.FilePath) ? File.ReadAllText(ActivityLog.FilePath) : "";
                if (!lines.Contains("  heartbeat  not sent, kept for later  ->  " + dentist) || !lines.Contains("  heartbeat  sent  ->  " + dentist)) throw new Exception("The one sender did not log a kept line and its later send: " + lines);
                checks.Add("PASS one sender per Discord line: an offer while he is away reaches Discord once through her one sender (no direct second send), a failed send is put back and sent once later (dropped after " + Heartbeat.SendTries + " tries), and on Quiet at home only heads-ups go to Discord");
            }
            finally
            {
                Delivery.Reachable = keptReachable;
                Delivery.Send = keptSend;
                Autonomy.Idle = keptIdle;
                Autonomy.Gaming = keptGaming;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                ActivityLog.FilePath = keptLog;
                lastHeartLine = keptLast;
                Delivery.Reset(DateTime.UtcNow);
                Heartbeat.Reset();
                Autonomy.Reset();
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private void HeartSendNow(DateTime now, bool notHome)
        {
            lastHeartLine = DateTime.MinValue;
            HeartDm(now, notHome);
            WaitHeart();
        }

        private void WaitHeart()
        {
            for (int wait = 0; wait < 500 && heartSending; wait++) { Application.DoEvents(); Thread.Sleep(10); }
            Application.DoEvents();
        }

        private static int SentCount(List<string> sent, string line)
        {
            int count = 0;
            lock (sent) foreach (string text in sent) if (text == "Heads-up: " + line) count++;
            return count;
        }
    }
}
