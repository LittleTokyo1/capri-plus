using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CapriDesktop
{
    // CAPRI-074: what she says by voice during a game also shows as a small click-through note beside her, while she says it and 1.5 s more.
    internal sealed class GameNote : Overlay
    {
        internal const double Least = 6;
        internal const double Most = 14;
        internal const double Extra = 1.5;
        private const double PerWord = 0.45;
        private const float Widest = 300;
        private double shown = -1;
        private double ends;

        internal GameNote() : base("Capri game note") { }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.ExStyle |= 0x00000020;
                return value;
            }
        }

        internal static double Seconds(string text)
        {
            int words = (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Min(Most, Math.Max(Least, words * PerWord));
        }

        internal bool Live { get { return shown >= 0; } }

        internal void Set(string text, double now)
        {
            Text = text ?? "";
            shown = now;
            ends = now + Seconds(text);
        }

        // Her voice started: the note stays while she says it, then a moment more to finish reading.
        internal void Spoken(double now, double voiceSeconds)
        {
            if (shown >= 0) ends = now + voiceSeconds + Extra;
        }

        internal bool Expired(double now) { return shown < 0 || now >= ends; }

        internal void Stop()
        {
            shown = -1;
            if (Visible) Hide();
        }

        internal void Update(float s, Point head, Rectangle area)
        {
            using (Font font = Look.Mono(12 * s, FontStyle.Regular, GraphicsUnit.Pixel, Text))
            using (Bitmap probe = new Bitmap(1, 1))
            using (Graphics measure = Graphics.FromImage(probe))
            {
                float pad = 10 * s;
                float lead = 22 * s;
                SizeF size = measure.MeasureString(Text, font, (int)(Widest * s));
                float wide = size.Width + lead + pad;
                float tall = Math.Max(22 * s, size.Height + pad);
                float margin = 2 * s;
                int width = (int)Math.Ceiling(wide + margin * 2);
                int height = (int)Math.Ceiling(tall + margin * 2);
                int left = Math.Max(area.Left, Math.Min(area.Right - width, head.X - width / 2));
                int top = Math.Max(area.Top, Math.Min(area.Bottom - height, head.Y - (int)(8 * s) - height));
                RectangleF plate = new RectangleF(margin, margin, wide, tall);
                Bitmap image = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(image))
                using (Brush square = new SolidBrush(Look.Yellow))
                using (Brush ink = new SolidBrush(Neon.White))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Kit.Panel(graphics, plate, s, 6 * s, 3 * s, 14 * s, 8 * s);
                    graphics.FillRectangle(square, plate.Left + 9 * s, plate.Top + 8 * s, 6 * s, 6 * s);
                    graphics.DrawString(Text, font, ink, new RectangleF(plate.Left + lead, plate.Top + pad / 2, size.Width + 2 * s, size.Height + 2 * s));
                }
                Present(image, new Point(left, top));
            }
        }
    }
}
