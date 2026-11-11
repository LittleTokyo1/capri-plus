using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const double JobBadgesFrameSeconds = 0.08;
        private JobBadges jobBadges;
        private DateTime jobBadgesProjectAt = DateTime.MinValue;
        private double jobBadgesDrawn = -1;

        private void JobBadgesTick()
        {
            if (testing) return;
            DateTime now = DateTime.Now;
            if ((now - jobBadgesProjectAt).TotalSeconds >= 1)
            {
                jobBadgesProjectAt = now;
                JobMarks.Project(ProjectRunning(), now);
            }
            List<JobMark> live = JobMarks.Live(now);
            if (live.Count == 0 || hidden || Lockout.Active || IsDisposed)
            {
                if (jobBadges != null && jobBadges.Visible) jobBadges.Hide();
                return;
            }
            double t = clock.Elapsed.TotalSeconds;
            if (t - jobBadgesDrawn < JobBadgesFrameSeconds) return;
            jobBadgesDrawn = t;
            if (jobBadges == null) jobBadges = new JobBadges();
            float s = TalkScale();
            Bitmap image = JobBadges.Draw(live, t, s);
            jobBadges.Present(image, JobBadgesAt(image.Size, s));
        }

        private Point JobBadgesAt(Size size, float s)
        {
            Point head = Head();
            Rectangle area = Screen.FromPoint(head).WorkingArea;
            int beside = (int)(preferences.Size * 0.22f);
            int left = head.X + beside;
            if (left + size.Width > area.Right) left = head.X - beside - size.Width;
            int top = Math.Max(area.Top, Math.Min(area.Bottom - size.Height, head.Y - (int)(4 * s)));
            return new Point(Math.Max(area.Left, left), top);
        }

        private static bool ProjectRunning()
        {
            return ProjectRun.Load().Pid != 0;
        }
    }
}
