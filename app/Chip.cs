using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class Chip : Overlay
    {
        internal const double Seconds = 1.6;
        private Color colour = Neon.Cyan;
        private double shown = -1;

        internal Chip() : base("Capri toast") { }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.ExStyle |= 0x00000020;
                return value;
            }
        }

        internal static string For(string what, bool on)
        {
            switch (what)
            {
                case "mic": return on ? "MIC MUTED" : "MIC ON";
                case "paused": return on ? "PAUSED" : "UNPAUSED";
                case "voice": return on ? "VOICE ON" : "VOICE OFF";
                case "autonomy": return on ? "AUTONOMY ON" : "AUTONOMY OFF";
                case "blackwall": return on ? "BLACKWALL ON" : "BLACKWALL OFF";
                default: return (what ?? "").ToUpperInvariant();
            }
        }

        internal static Color Colour(string what, bool on)
        {
            if (what == "mic" || what == "paused") return on ? Neon.Hot : Neon.Cyan;
            if (what == "voice") return on ? Neon.Cyan : Neon.Hot;
            return on ? Look.Yellow : Neon.Cyan;
        }

        internal bool Live { get { return shown >= 0; } }

        internal void Set(string text, Color tone, double now)
        {
            Text = text ?? "";
            colour = tone;
            shown = now;
        }

        internal bool Expired(double now) { return shown < 0 || now - shown >= Seconds; }

        internal void Stop()
        {
            shown = -1;
            if (Visible) Hide();
        }

        internal void Update(double now, float s, Point head, Rectangle area, Rectangle above)
        {
            string text = Text.ToUpperInvariant();
            using (Font font = Look.Mono(12 * s, FontStyle.Regular, GraphicsUnit.Pixel, text))
            {
                float cw = Look.CharWidth(font);
                float tall = 22 * s;
                float wide = text.Length * cw + 30 * s;
                float margin = 2 * s;
                int width = (int)Math.Ceiling(wide + margin * 2);
                int height = (int)Math.Ceiling(tall + margin * 2);
                int bottom = above.Width > 0 ? above.Top - (int)(6 * s) : head.Y - (int)(8 * s);
                int left = Math.Max(area.Left, Math.Min(area.Right - width, head.X - width / 2));
                int top = Math.Max(area.Top, Math.Min(area.Bottom - height, bottom - height));
                RectangleF plate = new RectangleF(margin, margin, wide, tall);
                Bitmap image = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(image))
                using (Brush square = new SolidBrush(colour))
                using (Brush ink = new SolidBrush(Neon.White))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Kit.Panel(graphics, plate, s, 6 * s, 3 * s, 14 * s, 8 * s);
                    graphics.FillRectangle(square, plate.Left + 9 * s, plate.Top + (tall - 6 * s) / 2f, 6 * s, 6 * s);
                    graphics.DrawString(text, font, ink, plate.Left + 24 * s, plate.Top + (tall - 14 * s) / 2f, StringFormat.GenericTypographic);
                }
                Present(image, new Point(left, top));
            }
        }
    }
}
