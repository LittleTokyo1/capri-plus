using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private DigestBox digestBox;
        private DateTime digestUtc;
        private Chip chip;
        private bool chipReady;
        private bool chatWas;
        private bool mutedWas;
        private bool pausedWas;
        private bool bypassWas;
        private bool autonomyWas;
        private double micHeldAt = -10;

        private void DigestTick()
        {
            if (testing) return;
            ForkSettings settings = ForkSettings.Current;
            if (digestBox != null && digestBox.Opened >= 0)
            {
                DateTime utc = DateTime.UtcNow;
                if (Delivery.AtPcMilliseconds(utc) < (utc - digestUtc).TotalMilliseconds) { DigestClear(); return; }
                if (Lockout.Active || hidden || settings.Paused) { DigestHide(); return; }
                DigestDraw();
                return;
            }
            bool quiet = Autonomy.Quiet(DateTime.Now, settings.QuietFrom, settings.QuietTo);
            bool busy = talkState != TalkState.Off || speech != null || typer.Visible || clock.Elapsed.TotalSeconds < voiceUntil;
            if (!Digest.May(Delivery.NotHome(), Lockout.Active, settings.Paused, quiet, hidden, busy, Digest.Count)) return;
            if (digestBox == null)
            {
                digestBox = new DigestBox();
                digestBox.Picked += DigestClear;
                digestBox.Read += DigestRead;
            }
            List<DigestItem> items = Digest.Items();
            digestBox.Open(items, clock.Elapsed.TotalSeconds);
            digestUtc = DateTime.UtcNow;
            ActivityLog.Write("digest", "shown", items.Count + " while he was away");
            Murmur(Digest.Intro(items.Count), DateTime.Now, false);
            DigestDraw();
        }

        private void DigestClear()
        {
            Digest.Clear();
            DigestHide();
            ActivityLog.Write("digest", "cleared", null);
        }

        private void DigestRead(string text)
        {
            DigestOpen(text);
        }

        private void DigestHide()
        {
            if (digestBox == null) return;
            digestBox.Opened = -1;
            if (digestBox.Visible) digestBox.Hide();
        }

        private void DigestDraw()
        {
            if (digestBox == null || digestBox.Opened < 0 || IsDisposed) return;
            float s = TalkScale();
            Point head = Head();
            Point feet = Feet();
            Rectangle area = Screen.FromPoint(head).WorkingArea;
            Rectangle above = box.Visible ? box.PanelOnScreen : offerBox != null && offerBox.Visible ? offerBox.PanelOnScreen : Rectangle.Empty;
            digestBox.Update(clock.Elapsed.TotalSeconds, s, head, new Point(feet.X, feet.Y + (int)(44 * s)), area, above);
        }

        private void ChipTick()
        {
            if (testing) return;
            double now = clock.Elapsed.TotalSeconds;
            ForkSettings settings = ForkSettings.Current;
            bool muted = chatting && ears != null && ears.Muted;
            if (chipReady)
            {
                if (chatting != chatWas) ChipShow("voice", chatting);
                else if (chatting && muted != mutedWas && now - micHeldAt > 1) ChipShow("mic", muted);
                if (settings.Paused != pausedWas) ChipShow("paused", settings.Paused);
                if (preferences.Bypass != bypassWas) ChipShow("blackwall", preferences.Bypass);
                if (settings.AutonomyOn != autonomyWas) ChipShow("autonomy", settings.AutonomyOn);
            }
            chipReady = true;
            chatWas = chatting;
            mutedWas = muted;
            pausedWas = settings.Paused;
            bypassWas = preferences.Bypass;
            autonomyWas = settings.AutonomyOn;
            if (chip == null || !chip.Live) return;
            if (chip.Expired(now) || hidden) chip.Stop();
            else ChipDraw();
        }

        private void ChipShow(string what, bool on)
        {
            ChipSay(Chip.For(what, on), Chip.Colour(what, on));
        }

        private void ChipSay(string text, Color tone)
        {
            if (testing) return;
            if (chip == null) chip = new Chip();
            chip.Set(text, tone, clock.Elapsed.TotalSeconds);
            ChipDraw();
        }

        private void ChipDraw()
        {
            if (chip == null || !chip.Live || IsDisposed) return;
            float s = TalkScale();
            Point head = Head();
            Rectangle area = Screen.FromPoint(head).WorkingArea;
            chip.Update(clock.Elapsed.TotalSeconds, s, head, area, box.Visible ? box.PanelOnScreen : Rectangle.Empty);
        }
    }
}
