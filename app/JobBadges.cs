using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class JobBadges : Overlay
    {
        internal JobBadges() : base("Capri jobs") { ClickThrough = true; }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.ExStyle |= 0x00000020;
                return value;
            }
        }

        internal static Bitmap Draw(List<JobMark> marks, double t, float s)
        {
            int count = marks == null ? 0 : marks.Count;
            bool thinking = count > 0 && marks.Exists(delegate(JobMark one) { return one.Running && one.Kind != "project"; });
            float cell = 30 * s, gap = 4 * s, dots = thinking ? 16 * s : 0;
            int width = (int)Math.Ceiling(34 * s);
            int height = Math.Max(1, (int)Math.Ceiling(dots + count * (cell + gap) + 2 * s));
            Bitmap image = new Bitmap(Math.Max(1, width), height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (thinking) Dots(graphics, t, s);
                for (int i = 0; i < count; i++)
                {
                    RectangleF plate = new RectangleF(2 * s, dots + i * (cell + gap), 28 * s, cell - 2 * s);
                    Kit.Panel(graphics, plate, s, 5 * s, 2 * s, 10 * s, 6 * s);
                    JobMark one = marks[i];
                    if (one.Running) { Glyph(graphics, one.Kind, plate, s); if (one.Kind != "project") Pulse(graphics, plate, t, s); }
                    else if (one.Ok) Check(graphics, plate, s);
                    else Cross(graphics, plate, s);
                }
            }
            return image;
        }

        private static void Dots(Graphics graphics, double t, float s)
        {
            for (int i = 0; i < 3; i++)
            {
                int alpha = (int)Math.Round(90 + 165 * (0.5 + 0.5 * Math.Sin(t * 6 - i * 0.9)));
                using (Brush dot = new SolidBrush(Color.FromArgb(alpha, Neon.Hot)))
                    graphics.FillEllipse(dot, (8 + i * 7) * s - 2.4f * s, 7 * s - 2.4f * s, 4.8f * s, 4.8f * s);
            }
        }

        private static void Pulse(Graphics graphics, RectangleF plate, double t, float s)
        {
            int alpha = (int)Math.Round(120 + 135 * (0.5 + 0.5 * Math.Sin(t * 5)));
            using (Brush dot = new SolidBrush(Color.FromArgb(alpha, Neon.Hot)))
                graphics.FillRectangle(dot, plate.Right - 7 * s, plate.Bottom - 7 * s, 3 * s, 3 * s);
        }

        private static void Glyph(Graphics graphics, string kind, RectangleF plate, float s)
        {
            float cx = plate.Left + plate.Width / 2f, cy = plate.Top + plate.Height / 2f;
            using (Pen ink = new Pen(Neon.White, 1.6f * s))
            {
                if (kind == "mail")
                {
                    graphics.DrawRectangle(ink, cx - 7 * s, cy - 5 * s, 14 * s, 10 * s);
                    graphics.DrawLines(ink, new[] { new PointF(cx - 7 * s, cy - 5 * s), new PointF(cx, cy + 1 * s), new PointF(cx + 7 * s, cy - 5 * s) });
                }
                else if (kind == "chat")
                {
                    graphics.DrawRectangle(ink, cx - 7 * s, cy - 6 * s, 14 * s, 9 * s);
                    graphics.DrawLines(ink, new[] { new PointF(cx - 3 * s, cy + 3 * s), new PointF(cx - 5 * s, cy + 7 * s), new PointF(cx + 1 * s, cy + 3 * s) });
                }
                else if (kind == "study")
                {
                    graphics.DrawRectangle(ink, cx - 7 * s, cy - 5 * s, 6.5f * s, 10 * s);
                    graphics.DrawRectangle(ink, cx + 0.5f * s, cy - 5 * s, 6.5f * s, 10 * s);
                }
                else
                {
                    graphics.DrawLines(ink, new[] { new PointF(cx - 3 * s, cy - 5 * s), new PointF(cx - 7 * s, cy), new PointF(cx - 3 * s, cy + 5 * s) });
                    graphics.DrawLines(ink, new[] { new PointF(cx + 3 * s, cy - 5 * s), new PointF(cx + 7 * s, cy), new PointF(cx + 3 * s, cy + 5 * s) });
                    graphics.DrawLine(ink, cx + 1.5f * s, cy - 6 * s, cx - 1.5f * s, cy + 6 * s);
                }
            }
        }

        private static void Check(Graphics graphics, RectangleF plate, float s)
        {
            float cx = plate.Left + plate.Width / 2f, cy = plate.Top + plate.Height / 2f;
            using (Pen ink = new Pen(Neon.Cyan, 2.4f * s))
                graphics.DrawLines(ink, new[] { new PointF(cx - 6 * s, cy), new PointF(cx - 2 * s, cy + 4 * s), new PointF(cx + 6 * s, cy - 5 * s) });
        }

        private static void Cross(Graphics graphics, RectangleF plate, float s)
        {
            float cx = plate.Left + plate.Width / 2f, cy = plate.Top + plate.Height / 2f;
            using (Pen ink = new Pen(Neon.Hot, 2.4f * s))
            {
                graphics.DrawLine(ink, cx - 5 * s, cy - 5 * s, cx + 5 * s, cy + 5 * s);
                graphics.DrawLine(ink, cx + 5 * s, cy - 5 * s, cx - 5 * s, cy + 5 * s);
            }
        }
    }
}
