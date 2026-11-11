using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class LeaguePanel : Form
    {
        private const int Wide = 460;
        private const int Top = 48;
        private const int RowHeight = 56;
        private const string Caption = "PRO BUILDS";
        internal event Action<int> PushAsked;
        internal readonly List<Button> Buttons = new List<Button>();
        private readonly List<string[]> rows = new List<string[]>();
        private readonly KitKey close;
        private string champion = "";
        private string status = "";
        private Color statusColour = Neon.Faint;
        private bool keys;
        private bool loaded;

        internal LeaguePanel()
        {
            Text = "Capri pro builds";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            Font = Kit.Type(9.5f);
            BackColor = Neon.Ground;
            ForeColor = Neon.White;
            DoubleBuffered = true;
            ClientSize = new Size(Wide, 200);
            close = new KitKey { Glyph = 2, Size = new Size(30, 24), Location = new Point(Wide - 20 - 30, 10), TabStop = false, AccessibleName = "Close" };
            close.Unfocusable();
            close.Click += delegate { Hide(); };
            Controls.Add(close);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.ExStyle |= 0x08000000 | 0x00000080;
                return value;
            }
        }

        internal Color StatusColour { get { return statusColour; } }

        private float Unit() { return loaded ? Kit.Unit(this) : 1f; }

        private int Scaled(int value) { return (int)Math.Round(value * Unit()); }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            loaded = true;
            Arrange();
        }

        internal static string[] Parts(string line)
        {
            string plain = Regex.Replace(line ?? "", @"^\s*\d+\)\s*", "");
            int cut = plain.IndexOf(". ", StringComparison.Ordinal);
            if (cut < 0) return new[] { plain, "" };
            return new[] { plain.Substring(0, cut), plain.Substring(cut + 2).Trim() };
        }

        internal void Fill(string champion, List<string> lines, bool buttons)
        {
            SuspendLayout();
            foreach (Button old in Buttons) { Controls.Remove(old); old.Dispose(); }
            Buttons.Clear();
            rows.Clear();
            this.champion = (champion ?? "").ToUpperInvariant();
            keys = buttons;
            for (int i = 0; i < lines.Count; i++)
            {
                rows.Add(Parts(lines[i]));
                if (!buttons) continue;
                int number = i + 1;
                KitKey push = new KitKey { Text = "PUSH " + number, Loud = true, Accent = Neon.Cyan, Font = Kit.Type(9f, true), TabStop = false, AccessibleName = "Push build " + number };
                push.Unfocusable();
                push.Click += delegate { if (PushAsked != null) PushAsked(number); };
                Buttons.Add(push);
                Controls.Add(push);
            }
            status = buttons ? "Push puts that pro's runes, summoner spells and item set into your client." : "Say \"push build 2\" to load one.";
            statusColour = Neon.Faint;
            Arrange();
            ResumeLayout();
            Invalidate();
        }

        private void Arrange()
        {
            ClientSize = new Size(Scaled(Wide), Scaled(Top + rows.Count * RowHeight + 40));
            close.Bounds = new Rectangle(ClientSize.Width - Scaled(50), Scaled(10), Scaled(30), Scaled(24));
            for (int i = 0; i < Buttons.Count; i++) Buttons[i].Bounds = new Rectangle(Scaled(28), Scaled(Top + i * RowHeight + 2), Scaled(70), Scaled(24));
            Shape();
        }

        private void Shape()
        {
            float s = Unit();
            Neon.Clip(this, 16 * s, 6 * s);
        }

        internal void Status(string text)
        {
            status = text ?? "";
            string plain = status.ToLowerInvariant();
            statusColour = plain.Contains("fail") || plain.Contains("didn't") || plain.Contains("couldn't") || plain.StartsWith("no pro builds", StringComparison.Ordinal) ? Neon.Hot : Neon.Cyan;
            Invalidate();
        }

        internal int HitTest(Point point)
        {
            if (close.Bounds.Contains(point)) return 1;
            return point.Y < Scaled(Top - 8) ? 2 : 1;
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg != 0x0084) { base.WndProc(ref message); return; }
            long value = message.LParam.ToInt64();
            message.Result = new IntPtr(HitTest(PointToClient(new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF)))));
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Kit.Backdrop(e.Graphics, this, Unit()); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = Unit();
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Neon.Frame(graphics, ClientSize, s, 16 * s, 6 * s, 52 * s, 26 * s);
            using (Font caption = Neon.Display(13f))
            using (Font small = Kit.Type(8.5f))
            using (Font number = Kit.Type(9.5f, true))
            {
                Size label = TextRenderer.MeasureText(Caption, caption);
                TextRenderer.DrawText(graphics, Caption, caption, new Point((int)(26 * s), (int)(12 * s)), Neon.Rose, TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(graphics, champion, caption, new Point((int)(26 * s) + label.Width + (int)(6 * s), (int)(12 * s)), Neon.White, TextFormatFlags.NoPrefix);
                if (rows.Count > 0) using (Brush red = new SolidBrush(Neon.Hot)) graphics.FillRectangle(red, 13 * s, Top * s, 2.5f * s, rows.Count * RowHeight * s - 8 * s);
                for (int i = 0; i < rows.Count; i++)
                {
                    int y = (int)((Top + i * RowHeight) * s);
                    int x = (int)((keys ? 28 + 70 + 10 : 50) * s);
                    if (!keys) TextRenderer.DrawText(graphics, (i + 1).ToString(), number, new Point((int)(28 * s), y + (int)(4 * s)), Neon.Cyan, TextFormatFlags.NoPrefix);
                    int wide = Width - x - (int)(20 * s);
                    TextRenderer.DrawText(graphics, rows[i][0], Font, new Rectangle(x, y, wide, (int)(32 * s)), Neon.White, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(graphics, rows[i][1], small, new Rectangle(x, y + (int)(33 * s), wide, (int)(16 * s)), Neon.Faint, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
                int bottom = (int)((Top + rows.Count * RowHeight + 4) * s);
                TextRenderer.DrawText(graphics, status, small, new Rectangle((int)(26 * s), bottom, Width - (int)(46 * s), Height - bottom - (int)(4 * s)), statusColour, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            }
        }

        internal void Beside(Rectangle her)
        {
            Rectangle screen = Screen.FromRectangle(her).WorkingArea;
            int x = her.Left - Width - 8;
            if (x < screen.Left) x = Math.Min(screen.Right - Width, her.Right + 8);
            int y = Math.Max(screen.Top, Math.Min(screen.Bottom - Height, her.Top));
            Location = new Point(x, y);
        }
    }
}
