using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int PhoneShotWaitMs = 5000;
        internal Func<Bitmap> PhoneScreen;

        private string PhoneShotRefusal()
        {
            return PhoneView.Refusal(ForkSettings.Current.Paused && !ForkSettings.Current.HomeAway, Blocklisted());
        }

        private byte[] PhoneShot()
        {
            byte[] made = null;
            bool late = false;
            object hold = new object();
            ManualResetEvent done = new ManualResetEvent(false);
            Func<Bitmap> screen = PhoneScreen ?? CaptureScreen;
            Action take = delegate
            {
                try
                {
                    lock (hold) if (late) return;
                    if (PhoneShotRefusal() != null) return;
                    using (Bitmap picture = screen()) made = picture == null ? null : Jpeg(picture, DiscordPictureSize);
                }
                catch (Exception error) { Brain.Log("phone", -1, error.GetType().Name); }
                finally { done.Set(); }
            };
            if (testing) take(); else Post(take);
            bool inTime = done.WaitOne(PhoneShotWaitMs);
            lock (hold) { late = !inTime; return inTime ? made : null; }
        }

        private string PhoneStatus()
        {
            List<string> jobs = new List<string>();
            Job hands = job;
            if (hands != null && !string.IsNullOrEmpty(hands.Task)) jobs.Add("Hands: " + hands.Task);
            if (LongJob.Current != null) jobs.Add(LongJob.Status(DateTime.Now));
            if (studyJobRunning) jobs.Add("A study job in your Chrome");
            return PhoneView.Json(ForkSettings.Current.Paused, Lockout.Active, jobs, Digest.Items());
        }
    }
}
