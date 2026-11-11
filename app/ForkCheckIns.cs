using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool checkingIn;
        private DateTime nextCheckIn = DateTime.MinValue;

        private void ForkCheckInTick()
        {
            if (testing || checkingIn) return;
            DateTime now = DateTime.Now;
            if (now < nextCheckIn) return;
            nextCheckIn = now.AddSeconds(AwayCheckIns.TickSeconds);
            ForkSettings settings = ForkSettings.Current;
            if (!settings.AwayCheckIns) return;
            PcBriefTick(now, settings);
            checkingIn = true;
            Cloud remote = brain.Remote;
            int perDay = settings.HeartbeatPerDay;
            string vault = Vault.Root;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result = "error";
                string said = null;
                try { result = AwayCheckIns.Run(now, remote, vault, perDay, out said); }
                catch (Exception error) { Brain.Log("checkin", -1, error.GetType().Name + ": " + error.Message); }
                Post(delegate
                {
                    checkingIn = false;
                    if (said != null) brain.Own("check-in", "Sent to his phone on Discord: " + said);
                    if (result.StartsWith("idle", StringComparison.Ordinal) || result == "capped") SilenceLog.Once("checkin", result, CheckInWhy(result, now, perDay), now);
                    else ActivityLog.Write("checkin", result, said);
                });
            });
        }

        internal static string CheckInWhy(string result, DateTime now, int perDay)
        {
            ForkSettings settings = ForkSettings.Current;
            if (result == "idle: cap") return AwayCheckIns.SentToday(now) + " of " + (settings.CheckInsPerDay >= 1 ? settings.CheckInsPerDay : AwayCheckIns.PerDay) + " check-ins sent today, no more until tomorrow";
            if (result == "capped") return "all " + Budgets.Share(perDay) + " check-in wake-ups used today";
            return "first time today";
        }
    }
}
