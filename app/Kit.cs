using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class Kit
    {
        internal static readonly Color Field = Neon.Field;
        internal static readonly Color Hover = Neon.Hover;
        internal static readonly Color Pressed = Neon.Pressed;
        internal static readonly Color Edge = Neon.Edge;
        internal static readonly Color Track = Neon.Dash;
        internal static readonly Color Violet = Color.FromArgb(122, 60, 255);
        internal static readonly Color Dull = Color.FromArgb(52, 22, 32);
        internal static readonly Color Greyed = Color.FromArgb(112, 94, 104);
        internal const string Header = "header";
        internal const string Caption = "caption";
        private const float Reach = 210;

        internal static float Unit(Control control)
        {
            Form form = control == null ? null : control.FindForm();
            if (form == null || form.AutoScaleMode != AutoScaleMode.Dpi) return 1f;
            return Math.Max(1f, form.CurrentAutoScaleDimensions.Width / 96f);
        }

        internal static Font Type(float points, bool bold = false) { return Neon.Type(points, bold); }

        internal static void Backdrop(Graphics graphics, Control control, float s)
        {
            Form form = control as Form;
            if (form != null) { Neon.Backdrop(graphics, form.ClientSize, Reach * s); return; }
            Neon.Behind(graphics, control, Reach * s);
        }

        internal static void Panel(Graphics graphics, RectangleF panel, float s, float big, float small, float along, float down)
        {
            using (GraphicsPath hull = Neon.Cut(panel, big, small, big, small))
            {
                using (Brush fill = new SolidBrush(Color.FromArgb(248, Neon.Well))) graphics.FillPath(fill, hull);
                float glow = Math.Min(panel.Height, 110 * s);
                graphics.SetClip(hull);
                using (LinearGradientBrush wine = new LinearGradientBrush(new RectangleF(0, panel.Top - 1, 16, glow + 2), Color.FromArgb(248, Neon.Glow), Color.FromArgb(248, Neon.Well), LinearGradientMode.Vertical))
                {
                    wine.Blend = Neon.Fade;
                    graphics.FillRectangle(wine, panel.Left, panel.Top, panel.Width, glow);
                }
                graphics.ResetClip();
            }
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform(panel.Left - 0.5f * s, panel.Top - 0.5f * s);
            Neon.Frame(graphics, new Size((int)Math.Round(panel.Width + s + 1), (int)Math.Round(panel.Height + s + 1)), s, big, small, along, down);
            graphics.Restore(state);
        }

        internal static void Rail(Graphics graphics, RectangleF panel, Color colour, float s)
        {
            using (Brush bar = new SolidBrush(colour)) graphics.FillRectangle(bar, panel.Left + 12 * s, panel.Top + 12 * s, 2.5f * s, panel.Height - 24 * s);
        }

        internal static void Dots(Graphics graphics, float x, float y, float width, float tall, double share, Color lit, Color dark, float s)
        {
            float pitch = 6 * s;
            int count = (int)(width / pitch);
            int on = (int)Math.Round(count * Math.Max(0, Math.Min(1, share)));
            using (Brush bright = new SolidBrush(lit))
            using (Brush off = new SolidBrush(dark))
                for (int i = 0; i < count; i++) graphics.FillRectangle(i < on ? bright : off, x + i * pitch, y, pitch - 2 * s, tall);
        }

        internal static GraphicsPath Key(RectangleF box, float s) { return Neon.Cut(box, 0, 0, 5 * s, 0); }

        internal static void Tones(Color accent, bool enabled, bool hot, bool down, bool lit, bool loud, bool picked, out Color face, out Color rim, out Color ink)
        {
            if (!enabled) { face = Field; rim = Dull; ink = Greyed; return; }
            if (picked) { face = hot ? Color.FromArgb(160, 248, 251) : Neon.Cyan; rim = face; ink = Neon.OnInk; return; }
            if (accent == Neon.Cyan)
            {
                face = down ? Color.FromArgb(18, 62, 67) : hot ? Color.FromArgb(13, 45, 49) : Neon.Deep;
                rim = lit ? Neon.Cyan : loud ? Neon.DeepEdge : Color.FromArgb(28, 84, 92);
                ink = Neon.Cyan;
                return;
            }
            if (accent == Neon.Hot)
            {
                face = down ? Color.FromArgb(72, 18, 30) : hot ? Color.FromArgb(54, 16, 28) : Color.FromArgb(34, 12, 22);
                rim = lit ? Color.FromArgb(255, 124, 122) : Neon.Hot;
                ink = Color.FromArgb(255, 166, 162);
                return;
            }
            face = down ? Pressed : hot ? Hover : Field;
            rim = lit ? Neon.Hot : Edge;
            ink = accent;
        }

        internal static void DrawKey(Graphics graphics, RectangleF box, string text, Font font, bool hot, bool down, bool loud, Color accent, float s)
        {
            Color face, rim, ink;
            Tones(accent, true, hot, down, hot, loud, false, out face, out rim, out ink);
            using (GraphicsPath plate = Key(box, s))
            {
                using (Brush fill = new SolidBrush(face)) graphics.FillPath(fill, plate);
                using (Pen pen = new Pen(rim, Math.Max(1f, s))) graphics.DrawPath(pen, plate);
            }
            using (Brush brush = new SolidBrush(ink))
            using (StringFormat centre = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                graphics.DrawString(text, font, brush, box, centre);
        }

        internal static void Drop(Graphics graphics, float left, float middle, float s, Color colour, bool filled)
        {
            PointF[] mark = { new PointF(left, middle - 3 * s), new PointF(left + 9 * s, middle - 3 * s), new PointF(left + 4.5f * s, middle + 3.5f * s) };
            if (filled) using (Brush brush = new SolidBrush(colour)) graphics.FillPolygon(brush, mark);
            using (Pen pen = new Pen(colour, 1.3f * s)) { pen.LineJoin = LineJoin.Miter; graphics.DrawPolygon(pen, mark); }
        }

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string app, string list);

        internal static void Dark(Control control)
        {
            try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        internal static string[] Split(string text)
        {
            string all = text ?? "";
            int colon = all.IndexOf(": ", StringComparison.Ordinal);
            int open = all.IndexOf(" (", StringComparison.Ordinal);
            int cut = colon < 0 ? open : open < 0 ? colon : Math.Min(colon, open);
            if (cut <= 0) return new[] { all, "" };
            int close = cut == open ? all.IndexOf(')', open) : -1;
            if (close < 0) return new[] { all.Substring(0, cut).Trim(), all.Substring(cut + 2).Trim() };
            string label = (all.Substring(0, cut) + all.Substring(close + 1)).Trim();
            return new[] { label, all.Substring(open + 2, close - open - 2).Trim() };
        }

        internal static ToolStripRenderer Menu() { return new Mainframe.MenuLook(); }

        internal static Label Heading(string text)
        {
            return new KitHead { Text = text.ToUpperInvariant() + " >", Tag = Header, ForeColor = Neon.White, Font = Type(10f, true) };
        }
    }
}
