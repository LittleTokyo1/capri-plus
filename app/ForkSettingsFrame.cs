using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tone = CapriDesktop.Look;

namespace CapriDesktop
{
    internal sealed partial class ForkSettingsForm
    {
        private const int Band = 6;
        private const int TitleRow = 46;
        private const int FooterHeight = 52;
        private const string UnsavedNote = "UNSAVED CHANGES";
        private KitPanel footer;
        private bool unsaved;

        internal bool Unsaved { get { return unsaved; } }

        private void Frame()
        {
            KitKey shrink = new KitKey { Glyph = 1, Size = new Size(30, 24), TabStop = false, AccessibleName = "Minimise", Anchor = AnchorStyles.Top | AnchorStyles.Right };
            KitKey shut = new KitKey { Glyph = 2, Size = new Size(30, 24), TabStop = false, AccessibleName = "Close", Anchor = AnchorStyles.Top | AnchorStyles.Right };
            shut.Location = new Point(ClientSize.Width - 20 - shut.Width, 11);
            shrink.Location = new Point(shut.Left - 2 - shrink.Width, 11);
            shrink.Click += delegate { WindowState = FormWindowState.Minimized; };
            shut.Click += delegate { Close(); };
            Controls.Add(shrink);
            Controls.Add(shut);
        }

        private void Footer()
        {
            footer = new KitPanel { Size = new Size(ClientSize.Width - 2 * Band, FooterHeight), Margin = Padding.Empty };
            KitKey save = new KitKey { Text = "SAVE", Loud = true, Accent = Neon.Cyan, Size = new Size(110, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            KitKey cancel = new KitKey { Text = "CANCEL", Size = new Size(110, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            cancel.Location = new Point(footer.Width - 18 - cancel.Width, 12);
            save.Location = new Point(cancel.Left - 10 - save.Width, 12);
            save.Click += delegate { SaveAndClose(); };
            cancel.Click += delegate { Close(); };
            footer.Controls.Add(save);
            footer.Controls.Add(cancel);
            footer.Paint += delegate(object sender, PaintEventArgs e) { PaintFooter(e.Graphics); };
            root.Controls.Add(footer, 0, 3);
            footer.Dock = DockStyle.Fill;
        }

        private void PaintFooter(Graphics graphics)
        {
            float s = Kit.Unit(footer);
            using (Pen edge = new Pen(Kit.Edge, Math.Max(1, s))) graphics.DrawLine(edge, 7 * s, 0, footer.Width - 7 * s, 0);
            if (!unsaved) return;
            using (Brush yellow = new SolidBrush(Tone.Yellow)) graphics.FillRectangle(yellow, 22 * s, footer.Height / 2f - 3 * s, 6 * s, 6 * s);
            using (Font small = Kit.Type(8.5f, true))
                TextRenderer.DrawText(graphics, UnsavedNote, small, new Rectangle((int)(34 * s), 0, footer.Width, footer.Height), Neon.Faint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        private void SaveAndClose()
        {
            Apply();
            if (Saved != null) Saved();
            Close();
        }

        private void Changed()
        {
            if (unsaved) return;
            unsaved = true;
            if (footer != null) footer.Invalidate();
        }

        private void Watch(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control == search || control == bar) continue;
                KitNumber number = control as KitNumber;
                if (number != null) { number.ValueChanged += delegate { Changed(); }; continue; }
                KitPick pick = control as KitPick;
                if (pick != null) { pick.SelectedIndexChanged += delegate { Changed(); }; continue; }
                KeyBox key = control as KeyBox;
                if (key != null) { key.Bound += delegate { Changed(); }; continue; }
                CheckBox box = control as CheckBox;
                if (box != null) { box.CheckedChanged += delegate { Changed(); }; continue; }
                KitSlider slider = control as KitSlider;
                if (slider != null) { slider.Changed += delegate { Changed(); }; continue; }
                TextBox text = control as TextBox;
                if (text != null) { text.TextChanged += delegate { Changed(); }; continue; }
                Watch(control);
            }
        }

        internal int HitTest(Point point)
        {
            float s = Kit.Unit(this);
            int band = (int)Math.Round(Band * s);
            bool left = point.X < band, right = point.X >= ClientSize.Width - band, top = point.Y < band, bottom = point.Y >= ClientSize.Height - band;
            if (top && left) return 13;
            if (top && right) return 14;
            if (bottom && left) return 16;
            if (bottom && right) return 17;
            if (left) return 10;
            if (right) return 11;
            if (top) return 12;
            if (bottom) return 15;
            return point.Y < TitleRow * s ? 2 : 1;
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg != 0x0084 || WindowState != FormWindowState.Normal) { base.WndProc(ref message); return; }
            long value = message.LParam.ToInt64();
            Point point = PointToClient(new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF)));
            message.Result = new IntPtr(HitTest(point));
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Kit.Backdrop(e.Graphics, this, Kit.Unit(this)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Neon.Frame(graphics, ClientSize, s, 16 * s, 6 * s, 64 * s, 34 * s);
            using (Font title = Neon.Display(15f))
            using (Font small = Kit.Type(9f))
            {
                Size big = TextRenderer.MeasureText("CAPRI+", title);
                Size word = TextRenderer.MeasureText("settings", small);
                int x = (int)(26 * s), y = (int)(9 * s);
                TextRenderer.DrawText(graphics, "CAPRI+", title, new Point(x, y), Neon.Rose, TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(graphics, "settings", small, new Point(x + big.Width, y + big.Height - word.Height - (int)(3 * s)), Neon.Faint, TextFormatFlags.NoPrefix);
            }
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            float s = Kit.Unit(this);
            Neon.Clip(this, 16 * s, 6 * s);
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { search.Box.Focus(); search.Box.SelectAll(); return true; }
            if (keyData == (Keys.Control | Keys.S)) { SaveAndClose(); return true; }
            if (keyData != Keys.Escape) return base.ProcessCmdKey(ref message, keyData);
            if (search.Box.Text.Length > 0) search.Box.Text = "";
            else Close();
            return true;
        }
    }
}
