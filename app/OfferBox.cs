using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class OfferBox : Overlay
    {
        internal const double Seconds = 120;
        internal const double ClosingSeconds = 2;
        private const int Columns = 34;
        private const string Speaker = "CAPRI: ";
        private const string Label = "> sys: (offer)";
        private const string Note = "OR JUST SAY IT";
        internal event Action<int> Picked;
        internal string Question;
        internal double Opened = -1;
        internal double Ended = -1;
        internal string Ending;
        private int hover;
        private bool down;
        private RectangleF yes;
        private RectangleF no;
        private Rectangle panelOnScreen;
        private float lastScale = 1;
        private Point lastHead;
        private Point lastFeet;
        private Rectangle lastArea;
        private Rectangle lastAbove;
        private double lastNow;

        internal OfferBox() : base("Capri asks") { Cursor = Look.Pointer; }

        internal Rectangle PanelOnScreen { get { return panelOnScreen; } }

        internal void Open(string question, double now)
        {
            Question = question;
            Opened = now;
            Ended = -1;
            Ending = null;
            hover = 0;
            down = false;
        }

        internal void Close(string line, double now)
        {
            if (Ended >= 0) return;
            Ending = line;
            Ended = now;
            hover = 0;
        }

        internal double Share(double now) { return Opened < 0 ? 0 : Math.Max(0, Math.Min(1, 1 - (now - Opened) / Seconds)); }

        internal bool Expired(double now) { return Opened >= 0 && now - Opened >= Seconds; }

        internal bool Done(double now) { return Opened < 0 || (Ended >= 0 && now - Ended > ClosingSeconds); }

        internal int PartAt(Point point)
        {
            if (Ended >= 0 || Opened < 0) return 0;
            if (yes.Contains(point)) return 1;
            if (no.Contains(point)) return 2;
            return 0;
        }

        internal Point KeyCentre(int part)
        {
            RectangleF key = part == 1 ? yes : no;
            return new Point((int)(key.Left + key.Width / 2), (int)(key.Top + key.Height / 2));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int part = PartAt(e.Location);
            if (part == hover) return;
            hover = part;
            Cursor = part == 0 ? Look.Pointer : Cursors.Hand;
            Redraw();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover == 0 && !down) return;
            hover = 0;
            down = false;
            Redraw();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || PartAt(e.Location) == 0) return;
            down = true;
            Redraw();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            down = false;
            if (e.Button != MouseButtons.Left) return;
            int part = PartAt(e.Location);
            if (Picked != null) Picked(part);
        }

        private void Redraw()
        {
            if (Opened >= 0 && Visible) Update(lastNow, lastScale, lastHead, lastFeet, lastArea, lastAbove);
        }

        internal void Update(double now, float s, Point head, Point feet, Rectangle area, Rectangle above)
        {
            lastNow = now;
            lastScale = s;
            lastHead = head;
            lastFeet = feet;
            lastArea = area;
            lastAbove = above;
            Point at;
            Bitmap image = Render(now, s, head, feet, area, above, out at);
            Present(image, at);
        }

        private Bitmap Render(double now, float s, Point head, Point feet, Rectangle area, Rectangle above, out Point at)
        {
            string all = Speaker + (Question ?? "") + (Ending ?? "");
            using (Font text = Look.Mono(14 * s, FontStyle.Regular, GraphicsUnit.Pixel, all))
            using (Font italic = Look.Mono(14 * s, FontStyle.Italic, GraphicsUnit.Pixel, all))
            using (Font bold = Look.Mono(14 * s, FontStyle.Bold, GraphicsUnit.Pixel, all))
            using (Font small = Neon.Type(12.5f * s, true, GraphicsUnit.Pixel))
            {
                float cw = Look.CharWidth(text);
                float line = 19 * s;
                float margin = 12 * s;
                List<string> lines = Look.Wrap(Speaker + (Question ?? ""), Columns);
                float panelWidth = 24 * s + Columns * cw + 14 * s;
                float keysTop = 12 * s + line * (1 + lines.Count) + 8 * s;
                float panelHeight = keysTop + 24 * s + 6 * s + 3 * s + 10 * s;
                int width = (int)Math.Ceiling(panelWidth + margin * 2);
                int height = (int)Math.Ceiling(panelHeight + margin * 2);
                int left = Math.Max(area.Left, Math.Min(area.Right - width, (int)(head.X - margin - panelWidth * 0.7f)));
                int y;
                if (above.Width > 0 && above.Top - height - 6 * s >= area.Top) y = (int)(above.Top - height + margin - 6 * s);
                else if (above.Width > 0) y = Math.Min(area.Bottom - height, feet.Y);
                else if (head.Y - height >= area.Top) y = head.Y - height;
                else y = Math.Min(area.Bottom - height, feet.Y);
                at = new Point(left, y);
                RectangleF panel = new RectangleF(margin, margin, panelWidth, panelHeight);
                panelOnScreen = new Rectangle(left + (int)panel.X, y + (int)panel.Y, (int)panel.Width, (int)panel.Height);
                float x = panel.Left + 24 * s;
                yes = new RectangleF(x, panel.Top + keysTop, 64 * s, 24 * s);
                no = new RectangleF(yes.Right + 8 * s, yes.Top, 64 * s, 24 * s);
                Bitmap image = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Kit.Panel(graphics, panel, s, 10 * s, 4 * s, 36 * s, 14 * s);
                    Kit.Rail(graphics, panel, Neon.Hot, s);
                    float row = panel.Top + 12 * s;
                    using (Brush dim = new SolidBrush(Neon.Faint))
                    using (Brush red = new SolidBrush(Neon.Rose))
                    using (Brush ink = new SolidBrush(Neon.White))
                    {
                        graphics.DrawString(Label, italic, dim, x, row, StringFormat.GenericTypographic);
                        for (int i = 0; i < lines.Count; i++)
                        {
                            float top = row + (i + 1) * line;
                            int name = i == 0 ? Math.Min(lines[i].Length, Speaker.Length - 1) : 0;
                            if (name > 0) graphics.DrawString(lines[i].Substring(0, name), bold, red, x, top, StringFormat.GenericTypographic);
                            graphics.DrawString(lines[i].Substring(name), text, ink, x + name * cw, top, StringFormat.GenericTypographic);
                        }
                        if (Ended >= 0)
                        {
                            graphics.DrawString("· " + (Ending ?? ""), text, dim, x, yes.Top + 3 * s, StringFormat.GenericTypographic);
                            return image;
                        }
                        Kit.DrawKey(graphics, yes, "YES", small, hover == 1, down && hover == 1, true, Neon.Cyan, s);
                        Kit.DrawKey(graphics, no, "NO", small, hover == 2, down && hover == 2, false, Neon.Rose, s);
                        SizeF size = graphics.MeasureString(Note, small, PointF.Empty, StringFormat.GenericTypographic);
                        graphics.DrawString(Note, small, dim, panel.Right - 14 * s - size.Width, yes.Top + (yes.Height - size.Height) / 2, StringFormat.GenericTypographic);
                        Kit.Dots(graphics, x, yes.Bottom + 6 * s, Columns * cw, 3 * s, Share(now), Neon.Cyan, Kit.Track, s);
                    }
                }
                return image;
            }
        }
    }
}
