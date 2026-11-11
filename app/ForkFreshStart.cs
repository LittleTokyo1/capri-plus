using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int FreshCheckSeconds = 60;
        private const int FreshFailMinutes = 30;
        private DateTime freshChecked = DateTime.MinValue;
        private DateTime freshBorn = DateTime.MinValue;
        private bool freshGoing;

        private void ForkFreshTick()
        {
            if (testing || freshGoing) return;
            DateTime now = DateTime.Now;
            if ((now - freshChecked).TotalSeconds < FreshCheckSeconds) return;
            freshChecked = now;
            int pid;
            using (Process self = Process.GetCurrentProcess())
            {
                pid = self.Id;
                if (freshBorn == DateTime.MinValue) freshBorn = self.StartTime;
            }
            long megabytes = FreshStart.Megabytes();
            string reason = FreshStart.Due(now, ForkSettings.Current, megabytes, freshBorn);
            if (reason == null) return;
            bool busy = speech != null || talkState != TalkState.Off || typer.Visible || dragging || (chatting && !ChatIdle()) || summarizing || foldingSummary || retryingMemory || beating || tidying;
            if (!FreshStart.Safe(Lockout.Game != null, job != null || working != null || longBusy, busy)) return;
            freshGoing = true;
            if (FreshStart.Go(reason, megabytes, Application.ExecutablePath, pid, delegate { quitting = true; Close(); })) return;
            freshGoing = false;
            freshChecked = now.AddMinutes(FreshFailMinutes);
        }
    }
}
