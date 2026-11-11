using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeArrivals(List<string> checks)
        {
            string keptLog = ActivityLog.FilePath;
            string keptStored = DiscordPresence.Stored;
            string folder = Path.Combine(Path.GetTempPath(), "capri-arrivals-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            List<FriendNews> keptNews = new List<FriendNews>(friendNews);
            DateTime t = DateTime.Now;
            try
            {
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                DiscordPresence.Stored = Path.Combine(folder, "discord-presence.json");
                Arrivals.Forget();
                PresenceHold.Forget();
                friendNews.Clear();
                if (!Arrivals.Fresh("506", t) || !Arrivals.Fresh("", t)) throw new Exception("A friend nobody told about yet was not fresh news.");
                Arrivals.Mark("506", t);
                if (Arrivals.Fresh("506", t.AddMinutes(30)) || !Arrivals.Fresh("507", t.AddMinutes(1)) || !Arrivals.Fresh("506", t.AddMinutes(Arrivals.Minutes))) throw new Exception("A told arrival was not kept by Discord id for " + Arrivals.Minutes + " minutes.");

                List<PresenceFriend> on = new List<PresenceFriend> { new PresenceFriend { Id = "506", Name = "Oskar" } };
                PresenceHold.Hold(new PresenceHeads { Id = "506", Name = "Oskar", Text = "Oskar just came online on Discord.", When = t });
                ArrivalsHeld(on, t.AddMinutes(35));
                bool orderFirst = friendNews.Count == 0;

                Arrivals.Forget();
                PresenceHold.Hold(new PresenceHeads { Id = "506", Name = "Oskar", Text = "Oskar just came online on Discord.", When = t });
                ArrivalsHeld(on, t.AddMinutes(35));
                bool heldOnce = friendNews.Count == 1 && friendNews[0].Id == "506";
                bool pending = ArrivalTold("506", t.AddMinutes(36)) && !ArrivalTold("507", t.AddMinutes(36));
                PresenceHold.Hold(new PresenceHeads { Id = "506", Name = "Oskar", Text = "Oskar just came online on Discord.", When = t });
                ArrivalsHeld(on, t.AddMinutes(36));
                bool notTwice = friendNews.Count == 1;

                string lines = File.Exists(ActivityLog.FilePath) ? File.ReadAllText(ActivityLog.FilePath) : "";
                if (!orderFirst || !heldOnce || !pending || !notTwice) throw new Exception("One arrival was not told once: after an order " + orderFirst + ", held once " + heldOnce + ", pending news blocks " + pending + ", not twice " + notTwice);
                if (!lines.Contains("  discord  friend online already told  ->  Oskar")) throw new Exception("A dropped second arrival line was not logged: " + lines);
                SmokeArrivalHistory(checks, folder);
                checks.Add("PASS one arrival is told once: a friend coming online is kept by Discord id, so a friend order, the held game heads-up and the friend watch tell it once (" + Arrivals.Minutes + " minutes), and the dropped line is logged");
            }
            finally
            {
                ActivityLog.FilePath = keptLog;
                DiscordPresence.Stored = keptStored;
                DiscordPresence.Forget();
                Arrivals.Forget();
                PresenceHold.Forget();
                friendNews.Clear();
                friendNews.AddRange(keptNews);
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private void SmokeArrivalHistory(List<string> checks, string folder)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            Func<bool> keptReachable = Delivery.Reachable;
            Func<string, bool> keptSend = Delivery.Send;
            bool failing = false;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                ForkSettings.Current.HomeAway = true;
                Delivery.Reachable = delegate { return true; };
                Delivery.Send = delegate(string text) { return !failing; };
                Arrivals.Forget();
                friendNews.Clear();
                friendsArrived.Clear();
                const string line = "Oskar came online, so I messaged them: \"Hop on?\"";
                friendNews.Add(new FriendNews { Id = "506", Text = line });
                FriendWatchTick(DateTime.Now, ForkSettings.Current);
                WaitFriend();
                bool kept = HistoryHas("Sent to his phone on Discord: " + line) && !Arrivals.Fresh("506", DateTime.Now);
                failing = true;
                friendNews.Add(new FriendNews { Id = "507", Text = "Vaidas came online during your game and is still on Discord." });
                FriendWatchTick(DateTime.Now, ForkSettings.Current);
                WaitFriend();
                bool putBack = Arrivals.Fresh("507", DateTime.Now) && !HistoryHas("Vaidas came online during your game");
                if (!kept || !putBack) throw new Exception("A friend line sent to his DM was not in her history and marked told (" + kept + "), or a failed send was (" + putBack + ").");
                checks.Add("PASS told lines go into her history: a friend line sent to his DM is in her chat history and marks that friend told; a failed send writes no history and leaves the friend untold");
            }
            finally
            {
                Delivery.Reachable = keptReachable;
                Delivery.Send = keptSend;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                Arrivals.Forget();
                friendNews.Clear();
                friendsArrived.Clear();
            }
        }

        private void WaitFriend()
        {
            for (int wait = 0; wait < 500 && friendSending; wait++) { System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(10); }
            System.Windows.Forms.Application.DoEvents();
        }

        private bool HistoryHas(string text)
        {
            foreach (ChatMessage message in brain.History) if (message.Content != null && message.Content.Contains(text)) return true;
            return false;
        }
    }
}
