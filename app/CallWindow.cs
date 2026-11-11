using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class CallView
    {
        internal double You, Capri, Discord;
        internal bool Muted, Deaf, Live;
        internal bool Captions = true;
        internal TimeSpan Length;
        internal string[] Lines = new string[0];
    }

    internal static class CallLook
    {
        internal const double Quiet = 0.01, Full = 0.25;
        internal const int CaptionLines = 3;
        internal const double EndedSeconds = 3;

        internal static string Clock(TimeSpan length)
        {
            if (length < TimeSpan.Zero) length = TimeSpan.Zero;
            return length.TotalHours >= 1 ? ((int)length.TotalHours) + ":" + length.Minutes.ToString("00") + ":" + length.Seconds.ToString("00") : ((int)length.TotalMinutes).ToString("00") + ":" + length.Seconds.ToString("00");
        }

        internal static float Ring(double level)
        {
            if (level < Quiet) return 0;
            return (float)Math.Min(1, Math.Sqrt(level / Full));
        }

        internal static bool Lit(double level) { return level >= Quiet * 2; }

        internal static string Status(CallView view)
        {
            if (!view.Live) return "WAITING FOR THE CALL";
            return (view.Deaf ? "NOT HEARING DISCORD  " : "IN CALL  ") + Clock(view.Length);
        }

        internal static string[] Captions(CallView view, int most)
        {
            if (!view.Captions || view.Lines == null) return new string[0];
            List<string> shown = new List<string>();
            for (int i = Math.Max(0, view.Lines.Length - most); i < view.Lines.Length; i++)
            {
                string line = view.Lines[i] ?? "";
                int space = line.IndexOf(' ');
                shown.Add(space == 8 ? line.Substring(9) : line);
            }
            return shown.ToArray();
        }

        internal static string Ended(TimeSpan length) { return "CALL ENDED  " + Clock(length); }

        internal static string Pill(CallView view) { return "CALL  " + Clock(view.Length); }
    }

    internal sealed class CallWindow : Form
    {
        internal const int Mic = 0, Deafen = 1, CaptionKey = 2, HangUp = 3, Shrink = 4;
        private static readonly string[] Names = { "YOU", "CAPRI", "DISCORD" };
        private static readonly Color DiscordBlue = Color.FromArgb(88, 101, 242);
        internal event Action<int> Pressed;
        private readonly Func<CallView> view;
        private readonly float s;
        private readonly Font title, small, caption, key, big;
        private readonly Timer tick = new Timer();
        private readonly RectangleF[] keys = new RectangleF[5];
        private readonly RectangleF[] tiles = new RectangleF[3];
        private string ended;
        private bool dragging;
        private Point grabbed;

        internal CallWindow(Func<CallView> view)
        {
            this.view = view;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Text = "Capri call";
            using (Graphics measure = CreateGraphics()) s = Math.Max(1f, measure.DpiX / 96f);
            ClientSize = new Size((int)(430 * s), (int)(300 * s));
            title = Neon.Type(13 * s, true, GraphicsUnit.Pixel);
            small = Neon.Type(10 * s, false, GraphicsUnit.Pixel);
            caption = Neon.Type(11 * s, false, GraphicsUnit.Pixel);
            key = Neon.Type(10.5f * s, true, GraphicsUnit.Pixel);
            big = Neon.Type(20 * s, true, GraphicsUnit.Pixel);
            float pad = 12 * s, wide = (ClientSize.Width - pad * 4) / 3;
            for (int i = 0; i < 3; i++) tiles[i] = new RectangleF(pad + i * (wide + pad), 44 * s, wide, 118 * s);
            float keyWide = (ClientSize.Width - pad * 5) / 4;
            for (int i = 0; i < 4; i++) keys[i] = new RectangleF(pad + i * (keyWide + pad), ClientSize.Height - 46 * s, keyWide, 32 * s);
            keys[Shrink] = new RectangleF(ClientSize.Width - 34 * s, 6 * s, 26 * s, 22 * s);
            tick.Interval = 100;
            tick.Tick += delegate { Invalidate(); };
        }

        internal const int NoActivate = 0x08000000, ToolWindow = 0x80;

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams { get { CreateParams made = base.CreateParams; made.ExStyle |= NoActivate | ToolWindow; return made; } }

        internal bool Quiet { get { return ShowWithoutActivation && (CreateParams.ExStyle & NoActivate) != 0; } }

        internal Point TileCentre(int index)
        {
            RectangleF tile = tiles[index];
            return new Point((int)(tile.Left + tile.Width / 2), (int)(tile.Top + 46 * s));
        }

        internal Point KeyCentre(int index)
        {
            RectangleF box = keys[index];
            return new Point((int)(box.Left + box.Width / 2), (int)(box.Top + box.Height / 2));
        }

        internal void End(TimeSpan length) { ended = CallLook.Ended(length); Invalidate(); }

        internal void Begin() { ended = null; Invalidate(); }

        internal void Press(Point at)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!keys[i].Contains(at)) continue;
                Action<int> handler = Pressed;
                if (handler != null) handler(i);
                return;
            }
        }

        internal void Draw(Bitmap into)
        {
            using (Graphics graphics = Graphics.FromImage(into)) Paint(graphics);
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); tick.Start(); }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) tick.Start(); else tick.Stop(); }

        protected override void OnPaint(PaintEventArgs e) { Paint(e.Graphics); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            foreach (RectangleF box in keys) if (box.Contains(e.Location)) { Press(e.Location); return; }
            if (e.Y > 36 * s) return;
            dragging = true;
            grabbed = e.Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) Location = new Point(Location.X + e.X - grabbed.X, Location.Y + e.Y - grabbed.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; }

        private void Paint(Graphics graphics)
        {
            CallView now = view() ?? new CallView();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Kit.Backdrop(graphics, this, s);
            Kit.Panel(graphics, new RectangleF(2 * s, 2 * s, ClientSize.Width - 4 * s, ClientSize.Height - 4 * s), s, 14 * s, 6 * s, 0, 0);
            using (Brush rose = new SolidBrush(Neon.Rose)) graphics.DrawString("CALL", title, rose, 14 * s, 10 * s);
            using (Brush faint = new SolidBrush(Neon.Faint)) graphics.DrawString(ended ?? CallLook.Status(now), small, faint, 70 * s, 13 * s);
            Kit.DrawKey(graphics, keys[Shrink], "_", key, false, false, false, Neon.Cyan, s);
            if (ended != null)
            {
                using (Brush white = new SolidBrush(Neon.White))
                using (StringFormat middle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    graphics.DrawString(ended, big, white, new RectangleF(0, 44 * s, ClientSize.Width, 160 * s), middle);
                return;
            }
            DrawTile(graphics, 0, now.You, Neon.Cyan, now.Muted ? "MUTED" : null);
            DrawTile(graphics, 1, now.Capri, Neon.Rose, null);
            DrawTile(graphics, 2, now.Discord, DiscordBlue, now.Deaf ? "OFF" : null);
            float y = 170 * s;
            using (Brush white = new SolidBrush(Neon.White))
            using (StringFormat one = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter })
                foreach (string line in CallLook.Captions(now, CallLook.CaptionLines))
                {
                    graphics.DrawString(line, caption, white, new RectangleF(14 * s, y, ClientSize.Width - 28 * s, 18 * s), one);
                    y += 19 * s;
                }
            Kit.DrawKey(graphics, keys[Mic], now.Muted ? "UNMUTE" : "MIC", key, false, false, false, Neon.Cyan, s);
            Kit.DrawKey(graphics, keys[Deafen], now.Deaf ? "HEAR" : "DEAFEN", key, false, false, false, Neon.Cyan, s);
            Kit.DrawKey(graphics, keys[CaptionKey], now.Captions ? "CC ON" : "CC OFF", key, false, false, false, Neon.Cyan, s);
            Kit.DrawKey(graphics, keys[HangUp], "HANG UP", key, false, false, true, Neon.Hot, s);
        }

        private void DrawTile(Graphics graphics, int index, double level, Color accent, string note)
        {
            RectangleF tile = tiles[index];
            using (Brush well = new SolidBrush(Neon.Field)) graphics.FillRectangle(well, tile);
            using (Pen edge = new Pen(Neon.Edge, 1 * s)) graphics.DrawRectangle(edge, tile.X, tile.Y, tile.Width, tile.Height);
            Point centre = TileCentre(index);
            float radius = 24 * s, ring = CallLook.Ring(level) * 12 * s;
            bool lit = CallLook.Lit(level) && note == null;
            if (ring > 0 && note == null)
                using (Pen glow = new Pen(Color.FromArgb(150, accent), 3 * s)) graphics.DrawEllipse(glow, centre.X - radius - ring, centre.Y - radius - ring, (radius + ring) * 2, (radius + ring) * 2);
            using (Brush face = new SolidBrush(lit ? accent : Neon.Well)) graphics.FillEllipse(face, centre.X - radius, centre.Y - radius, radius * 2, radius * 2);
            using (Pen rim = new Pen(lit ? Neon.White : accent, 1.5f * s)) graphics.DrawEllipse(rim, centre.X - radius, centre.Y - radius, radius * 2, radius * 2);
            using (Brush ink = new SolidBrush(note != null ? Neon.Faint : Neon.White))
            using (StringFormat middle = new StringFormat { Alignment = StringAlignment.Center })
                graphics.DrawString(note ?? Names[index], small, ink, new RectangleF(tile.Left, tile.Bottom - 24 * s, tile.Width, 18 * s), middle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tick.Dispose();
                title.Dispose();
                small.Dispose();
                caption.Dispose();
                key.Dispose();
                big.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class CallPill : Form
    {
        private readonly Func<CallView> view;
        private readonly Font font;
        private readonly Timer tick = new Timer();
        internal event Action Opened;

        internal CallPill(Func<CallView> view)
        {
            this.view = view;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            float s;
            using (Graphics measure = CreateGraphics()) s = Math.Max(1f, measure.DpiX / 96f);
            ClientSize = new Size((int)(150 * s), (int)(30 * s));
            font = Neon.Type(11 * s, true, GraphicsUnit.Pixel);
            tick.Interval = 500;
            tick.Tick += delegate { Invalidate(); };
            Click += delegate { Action handler = Opened; if (handler != null) handler(); };
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) tick.Start(); else tick.Stop(); }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams { get { CreateParams made = base.CreateParams; made.ExStyle |= CallWindow.NoActivate | CallWindow.ToolWindow; return made; } }

        internal bool Quiet { get { return ShowWithoutActivation && (CreateParams.ExStyle & CallWindow.NoActivate) != 0; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            CallView now = view() ?? new CallView();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Kit.Backdrop(e.Graphics, this, 1);
            float h = ClientSize.Height;
            using (Brush dot = new SolidBrush(Neon.Hot)) e.Graphics.FillEllipse(dot, h * 0.3f, h * 0.3f, h * 0.4f, h * 0.4f);
            using (Brush white = new SolidBrush(Neon.White)) e.Graphics.DrawString(CallLook.Pill(now), font, white, h, h * 0.2f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tick.Dispose(); font.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
