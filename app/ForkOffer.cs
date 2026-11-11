using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private OfferBox offerBox;
        private string offerShown;
        private DateTime offerUtc;
        private Action<string> offerSay;

        private void OfferLine(string line, DateTime now)
        {
            brain.Own("heartbeat", line);
            ActivityLog.Write("autonomy", "offered with keys", line);
            ShowOffer(line, now);
            Murmur(line, now, false);
            OfferDraw();
        }

        private void ShowOffer(string line, DateTime now)
        {
            if (offerBox == null)
            {
                offerBox = new OfferBox();
                offerBox.Picked += delegate(int part) { OfferPicked(part, DateTime.Now); };
            }
            offerShown = line;
            offerUtc = now.ToUniversalTime();
            offerBox.Open(line, clock.Elapsed.TotalSeconds);
        }

        private void OfferPicked(int part, DateTime now)
        {
            if (offerBox == null || offerBox.Opened < 0 || offerBox.Ended >= 0) return;
            double at = clock.Elapsed.TotalSeconds;
            if (part == 0) { OfferHide(); return; }
            if (!Autonomy.StillOpen(offerShown, now))
            {
                offerBox.Close("that offer is gone.", at);
                OfferDraw();
                return;
            }
            string said = part == 1 ? "yes" : "no";
            offerBox.Close(part == 1 ? "yes." : "left it.", at);
            OfferDraw();
            ActivityLog.Write("autonomy", "offer key", said);
            if (offerSay != null) offerSay(said);
            else Hear(said, said, "> you: ");
        }

        private void OfferHide()
        {
            if (offerBox == null) return;
            offerBox.Opened = -1;
            if (offerBox.Visible) offerBox.Hide();
        }

        private void OfferTick()
        {
            if (offerBox == null || offerBox.Opened < 0) return;
            double now = clock.Elapsed.TotalSeconds;
            DateTime utc = DateTime.UtcNow;
            if (Lockout.Active || hidden || ForkSettings.Current.Paused || offerBox.Done(now)) { OfferHide(); return; }
            if (offerBox.Ended < 0)
            {
                bool spoke = Delivery.AtPcMilliseconds(utc) < (utc - offerUtc).TotalMilliseconds;
                if (spoke || !Autonomy.StillOpen(offerShown, DateTime.Now)) { OfferHide(); return; }
                if (offerBox.Expired(now)) offerBox.Close("left it.", now);
            }
            OfferDraw();
        }

        private void OfferDraw()
        {
            if (offerBox == null || offerBox.Opened < 0 || IsDisposed) return;
            float s = TalkScale();
            Point head = Head();
            Point feet = Feet();
            Rectangle area = Screen.FromPoint(head).WorkingArea;
            offerBox.Update(clock.Elapsed.TotalSeconds, s, head, new Point(feet.X, feet.Y + (int)(44 * s)), area, box.Visible ? box.PanelOnScreen : Rectangle.Empty);
        }
    }
}
