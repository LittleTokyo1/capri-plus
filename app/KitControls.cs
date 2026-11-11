using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal class KitLabel : Label
    {
        internal bool Upper;

        internal KitLabel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Neon.Ground;
            ForeColor = Neon.White;
            AutoSize = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Kit.Backdrop(e.Graphics, this, Kit.Unit(this)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Upper) { base.OnPaint(e); return; }
            TextRenderer.DrawText(e.Graphics, Text.ToUpperInvariant(), Font, ClientRectangle, ForeColor, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        }
    }

    internal sealed class KitHead : KitLabel
    {
        internal KitHead()
        {
            SetStyle(ControlStyles.UserPaint, true);
            AutoSize = false;
        }

        private string Shown { get { return Text.EndsWith(" >", StringComparison.Ordinal) ? Text.Substring(0, Text.Length - 2) : Text; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            using (Brush band = new SolidBrush(Neon.Band)) graphics.FillRectangle(band, 0, 0, Width, Height);
            using (Brush rule = new SolidBrush(Neon.Rule)) graphics.FillRectangle(rule, 0, 0, Width, Math.Max(1, (int)s));
            using (Brush tick = new SolidBrush(Neon.Hot)) graphics.FillRectangle(tick, 0, 7 * s, 2.5f * s, Math.Max(1, Height - 13 * s));
            int left = (int)(14 * s);
            TextRenderer.DrawText(graphics, Shown, Font, new Rectangle(left, 0, Width - left, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    internal class KitPanel : Panel
    {
        internal KitPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Neon.Ground;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Kit.Backdrop(e.Graphics, this, Kit.Unit(this)); }
    }

    internal class KitFlow : FlowLayoutPanel
    {
        internal KitFlow()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Neon.Ground;
        }

        protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(true); }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(true); }

        protected override void OnPaintBackground(PaintEventArgs e) { Kit.Backdrop(e.Graphics, this, Kit.Unit(this)); }
    }

    internal class KitTable : TableLayoutPanel
    {
        internal KitTable()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Neon.Ground;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Kit.Backdrop(e.Graphics, this, Kit.Unit(this)); }
    }

    internal class KitKey : Button
    {
        internal Color Accent = Neon.Rose;
        internal bool Loud;
        internal bool Picked;
        internal bool Chevron;
        internal bool Mark;
        internal bool Faded;
        internal int Glyph;
        private bool hot;
        private bool down;

        internal KitKey()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Neon.Ground;
            Cursor = Cursors.Hand;
        }

        internal void Unfocusable()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }

        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            Kit.Backdrop(graphics, this, s);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float line = Math.Max(1f, s);
            RectangleF box = new RectangleF(line / 2, line / 2, Width - line, Height - line);
            float x = Width / 2f;
            float y = Height / 2f;
            if (Glyph != 0)
            {
                if (hot) using (GraphicsPath plate = Neon.Cut(box, 0, 0, 4 * s, 0)) using (Brush fill = new SolidBrush(down ? Neon.Pressed : Neon.Hover)) graphics.FillPath(fill, plate);
                using (Pen pen = new Pen(hot || (Focused && ShowFocusCues) ? (Glyph == 2 ? Neon.Hot : Neon.Cyan) : Neon.Faint, 1.5f * s))
                {
                    if (Glyph == 1) graphics.DrawLine(pen, x - 5 * s, y, x + 5 * s, y);
                    else
                    {
                        graphics.DrawLine(pen, x - 4.5f * s, y - 4.5f * s, x + 4.5f * s, y + 4.5f * s);
                        graphics.DrawLine(pen, x - 4.5f * s, y + 4.5f * s, x + 4.5f * s, y - 4.5f * s);
                    }
                }
                return;
            }
            bool lit = hot || (Focused && ShowFocusCues);
            Color face, rim, ink;
            Kit.Tones(Accent, Enabled, hot, down, lit, Loud, Picked, out face, out rim, out ink);
            if (Faded && !lit && !Picked) { rim = Kit.Dull; ink = Neon.Faint; }
            if (Chevron && Enabled && !Picked && Accent == Neon.Rose) ink = Neon.Cyan;
            using (GraphicsPath plate = Kit.Key(box, s))
            {
                using (Brush fill = new SolidBrush(face)) graphics.FillPath(fill, plate);
                using (Pen pen = new Pen(rim, line)) graphics.DrawPath(pen, plate);
            }
            if (Mark) using (Brush yellow = new SolidBrush(Look.Yellow)) graphics.FillRectangle(yellow, Width - 8 * s, 4 * s, 4 * s, 4 * s);
            if (Chevron) Kit.Drop(graphics, Width - 19 * s, y, s, Enabled ? Neon.Cyan : ink, hot);
            Rectangle area = Chevron ? new Rectangle((int)(10 * s), 0, Width - (int)(32 * s), Height) : ClientRectangle;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | (Chevron ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(graphics, Text, Font, area, ink, flags);
        }
    }

    internal sealed class KitToggle : CheckBox
    {
        internal const int Switch = 120;
        private int over = 2;
        private Font small;

        internal KitToggle()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Neon.Ground;
            ForeColor = Neon.Rose;
            Cursor = Cursors.Hand;
            AutoCheck = false;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        internal string Label { get { return Kit.Split(Text)[0]; } }

        private int Part(int x)
        {
            float s = Kit.Unit(this), start = Width - Switch * s;
            return x < start ? -1 : x < start + Switch * s / 2 ? 0 : 1;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            if (small != null) small.Dispose();
            small = null;
            base.OnFontChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && small != null) small.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int part = Part(e.X);
            if (part != over) { over = part; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { over = 2; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            MouseEventArgs mouse = e as MouseEventArgs;
            int part = mouse == null ? -1 : Part(mouse.X);
            Checked = part < 0 ? !Checked : part == 1;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            Kit.Backdrop(graphics, this, s);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (small == null) small = Kit.Type(Math.Max(6f, Font.SizeInPoints - 0.75f), true);
            float line = Math.Max(1f, s), wide = Switch * s, gap = 4 * s, half = (wide - gap) / 2, start = Width - wide;
            RectangleF off = new RectangleF(start + line / 2, line / 2, half - line, Height - line);
            RectangleF on = new RectangleF(start + half + gap + line / 2, line / 2, half - line, Height - line);
            Color offFill, offRim, offInk, onFill, onRim, onInk;
            if (Checked)
            {
                offFill = over == 0 ? Color.FromArgb(50, 16, 28) : Color.FromArgb(32, 12, 19);
                offRim = over == 0 ? Color.FromArgb(120, 36, 50) : Color.FromArgb(50, 18, 28);
                offInk = over == 0 ? Color.FromArgb(170, 60, 72) : Color.FromArgb(84, 26, 38);
                onFill = onRim = Neon.Cyan;
                onInk = Neon.OnInk;
            }
            else
            {
                offFill = Color.FromArgb(155, 50, 46);
                offRim = Color.FromArgb(222, 69, 63);
                offInk = Color.FromArgb(255, 150, 144);
                onFill = over == 1 ? Color.FromArgb(12, 46, 50) : Neon.Deep;
                onRim = over == 1 ? Neon.DeepEdge : Color.FromArgb(16, 44, 48);
                onInk = over == 1 ? Color.FromArgb(110, 210, 216) : Color.FromArgb(28, 68, 72);
            }
            if (!Enabled) { offFill = onFill = Kit.Field; offRim = onRim = Kit.Dull; offInk = onInk = Kit.Greyed; }
            TextFormatFlags centre = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            using (GraphicsPath plate = Neon.Cut(off, 0, 0, 0, 5 * s))
            {
                using (Brush fill = new SolidBrush(offFill)) graphics.FillPath(fill, plate);
                using (Pen pen = new Pen(offRim, line)) graphics.DrawPath(pen, plate);
            }
            using (GraphicsPath plate = Neon.Cut(on, 0, 0, 5 * s, 0))
            {
                using (Brush fill = new SolidBrush(onFill)) graphics.FillPath(fill, plate);
                using (Pen pen = new Pen(onRim, line)) graphics.DrawPath(pen, plate);
            }
            TextRenderer.DrawText(graphics, "OFF", small, Rectangle.Round(off), offInk, centre);
            TextRenderer.DrawText(graphics, "ON", small, Rectangle.Round(on), onInk, centre);
            if (Focused && ShowFocusCues)
                using (GraphicsPath around = Neon.Cut(new RectangleF(start - 2 * s, line / 2, wide + 2 * s - line / 2, Height - line), 0, 0, 5 * s, 0))
                using (Pen pen = new Pen(Neon.Cyan, line))
                    graphics.DrawPath(pen, around);
            int room = Math.Max(1, (int)(start - 10 * s));
            TextRenderer.DrawText(graphics, Label, Font, new Rectangle(0, 0, room, Height), Enabled ? Neon.Rose : Neon.Faint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class KitLock : Label
    {
        private const string Flag = "ALWAYS ON";

        internal KitLock()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AutoSize = false;
            BackColor = Neon.Ground;
            ForeColor = Neon.Rose;
            Cursor = Cursors.Default;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            Kit.Backdrop(graphics, this, s);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            string[] parts = Kit.Split(Text);
            float line = Math.Max(1f, s), wide = KitToggle.Switch * s, tall = 22 * s;
            RectangleF plate = new RectangleF(Width - wide + line / 2, line / 2, wide - line, tall - line);
            using (GraphicsPath path = Neon.Cut(plate, 0, 0, 5 * s, 0))
            {
                using (Brush fill = new SolidBrush(Kit.Field)) graphics.FillPath(fill, path);
                using (Pen pen = new Pen(Kit.Dull, line)) graphics.DrawPath(pen, path);
            }
            using (Font small = Kit.Type(8.5f, true))
                TextRenderer.DrawText(graphics, Flag, small, Rectangle.Round(plate), Look.Yellow, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int room = Math.Max(1, (int)(Width - wide - 10 * s));
            TextRenderer.DrawText(graphics, parts[0], Font, new Rectangle(0, 0, room, (int)tall), Neon.Rose, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            using (Font hint = Kit.Type(8.5f))
                TextRenderer.DrawText(graphics, parts[1], hint, new Rectangle(0, (int)(tall + 2 * s), Width, Math.Max(1, Height - (int)(tall + 2 * s))), Neon.Faint, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        }
    }

    internal class KitEntry : Control
    {
        internal readonly TextBox Box = new TextBox();
        internal string Prompt;
        private string cue = "";
        private Color flash = Color.Empty;

        internal KitEntry(bool lines)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Neon.Ground;
            Cursor = Cursors.IBeam;
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Neon.Well;
            Box.ForeColor = Neon.White;
            Box.Multiline = lines;
            Box.AcceptsReturn = lines;
            Box.ScrollBars = lines ? ScrollBars.Vertical : ScrollBars.None;
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { flash = Color.Empty; Invalidate(); };
            Box.HandleCreated += delegate { ShowCue(); if (Box.Multiline) Kit.Dark(Box); };
            Controls.Add(Box);
        }

        internal string Cue { get { return cue; } set { cue = value ?? ""; ShowCue(); } }

        internal void Flash(Color colour) { flash = colour; Invalidate(); }

        private void ShowCue() { if (Box.IsHandleCreated && !Box.Multiline) Native.SendMessage(Box.Handle, 0x1501, new IntPtr(1), cue); }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Box.Font = Font; PerformLayout(); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            float s = Kit.Unit(this);
            int left = (int)(10 * s);
            if (Prompt != null) left += TextRenderer.MeasureText(Prompt, Font).Width + (int)(4 * s);
            int right = (int)(10 * s);
            if (Box.Multiline) Box.SetBounds(left, (int)(6 * s), Math.Max(1, Width - left - right), Math.Max(1, Height - (int)(12 * s)));
            else
            {
                int height = Box.PreferredHeight;
                Box.SetBounds(left, (Height - height) / 2, Math.Max(1, Width - left - right), height);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Box.Focus(); base.OnMouseDown(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            Kit.Backdrop(graphics, this, s);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float line = Math.Max(1f, s);
            using (GraphicsPath plate = Kit.Key(new RectangleF(line / 2, line / 2, Width - line, Height - line), s))
            {
                using (Brush fill = new SolidBrush(Neon.Well)) graphics.FillPath(fill, plate);
                Color edge = flash != Color.Empty ? flash : Box.Focused ? Neon.Cyan : Neon.WellEdge;
                using (Pen pen = new Pen(edge, line)) graphics.DrawPath(pen, plate);
            }
            if (Prompt == null) return;
            TextRenderer.DrawText(graphics, Prompt, Font, new Rectangle((int)(10 * s), 0, Width, Height), Neon.Faint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class KitPick : KitKey
    {
        private readonly string[] items;
        private int selected;
        internal event EventHandler SelectedIndexChanged;

        internal KitPick(string[] choices, int value)
        {
            items = choices ?? new string[0];
            Chevron = true;
            AccessibleRole = AccessibleRole.ComboBox;
            selected = Math.Max(0, Math.Min(items.Length - 1, value));
            Text = items.Length == 0 ? "" : items[selected];
            Click += delegate { Open(); };
        }

        internal string[] Items { get { return items; } }

        internal int SelectedIndex
        {
            get { return selected; }
            set
            {
                int next = Math.Max(0, Math.Min(items.Length - 1, value));
                if (next == selected) return;
                selected = next;
                Text = items[next];
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        protected override bool IsInputKey(Keys key) { return key == Keys.Up || key == Keys.Down || base.IsInputKey(key); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up) SelectedIndex = selected - 1;
            else if (e.KeyCode == Keys.Down && e.Alt) Open();
            else if (e.KeyCode == Keys.Down) SelectedIndex = selected + 1;
            base.OnKeyDown(e);
        }

        private void Open()
        {
            float s = Kit.Unit(this);
            ToolStripDropDownMenu menu = new ToolStripDropDownMenu { ShowImageMargin = false, ShowCheckMargin = true, Renderer = Kit.Menu(), Padding = new Padding(0, (int)(4 * s), 0, (int)(4 * s)) };
            for (int i = 0; i < items.Length; i++)
            {
                int index = i;
                ToolStripMenuItem item = new ToolStripMenuItem(items[i]) { Font = Font, ForeColor = Neon.White, AutoSize = false, Size = new Size(Width, (int)(26 * s)), Checked = i == selected };
                item.Click += delegate { SelectedIndex = index; };
                menu.Items.Add(item);
            }
            menu.Closed += delegate { BeginInvoke((MethodInvoker)delegate { menu.Dispose(); }); };
            menu.Show(this, new Point(0, Height));
        }
    }

    internal sealed class KitSlider : Control
    {
        private readonly int low;
        private readonly int high;
        private int value;
        private bool hot;
        private bool sliding;
        internal event EventHandler Changed;

        internal KitSlider(int minimum, int maximum, int start)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            low = minimum;
            high = Math.Max(minimum + 1, maximum);
            value = Math.Max(low, Math.Min(high, start));
            BackColor = Neon.Ground;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.Slider;
        }

        internal int Minimum { get { return low; } }

        internal int Maximum { get { return high; } }

        internal int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(low, Math.Min(high, value));
                if (next == this.value) return;
                this.value = next;
                AccessibleDescription = next.ToString(CultureInfo.InvariantCulture);
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }

        private float Grip { get { return 10 * Kit.Unit(this); } }

        private int At(int x)
        {
            float s = Kit.Unit(this);
            float travel = Math.Max(1, Width - 2 * s - Grip);
            return low + (int)Math.Round((x - s - Grip / 2) / travel * (high - low));
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e) { Focus(); sliding = true; Value = At(e.X); base.OnMouseDown(e); }

        protected override void OnMouseMove(MouseEventArgs e) { if (sliding) Value = At(e.X); base.OnMouseMove(e); }

        protected override void OnMouseUp(MouseEventArgs e) { sliding = false; Invalidate(); base.OnMouseUp(e); }

        protected override bool IsInputKey(Keys key) { return key == Keys.Left || key == Keys.Right || base.IsInputKey(key); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) Value = value - 1;
            else if (e.KeyCode == Keys.Right) Value = value + 1;
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            Kit.Backdrop(graphics, this, s);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float line = Math.Max(1f, s);
            RectangleF box = new RectangleF(line / 2, line / 2, Width - line, Height - line);
            Color rim = Focused && ShowFocusCues ? Neon.Cyan : hot || sliding ? Color.FromArgb(156, 40, 56) : Neon.Edge;
            float travel = Math.Max(1, Width - 2 * s - Grip);
            float knob = s + travel * (value - low) / (high - low);
            int steps = high - low;
            using (GraphicsPath plate = Kit.Key(box, s))
            {
                using (Brush fill = new SolidBrush(Neon.Field)) graphics.FillPath(fill, plate);
                using (Brush done = new SolidBrush(Color.FromArgb(42, 14, 25))) graphics.FillRectangle(done, line, line, Math.Max(0, knob - line), Height - 2 * line);
                int every = Math.Max(1, (int)Math.Ceiling(steps / 24.0));
                using (Brush tick = new SolidBrush(Neon.Dash))
                    for (int i = 0; i <= steps; i += every) graphics.FillRectangle(tick, s + Grip / 2 + travel * i / steps - 0.5f * s, Height - 5 * s, Math.Max(1, s), 3 * s);
                using (GraphicsPath grip = Neon.Cut(new RectangleF(knob, line, Grip, Height - 2 * line), 0, 0, 4 * s, 0))
                using (Brush red = new SolidBrush(hot || sliding || Focused ? Neon.Lit : Neon.Hot))
                    graphics.FillPath(red, grip);
                using (Pen pen = new Pen(rim, line)) graphics.DrawPath(pen, plate);
            }
        }
    }

    internal sealed class KitNumber : Control
    {
        private const int SmallSteps = 13;
        private readonly decimal low;
        private readonly decimal high;
        private readonly int places;
        private readonly decimal step;
        private decimal value;
        private readonly KitEntry entry;
        private readonly KitSlider slider;
        private string prefix = "";
        internal string Unit = "";
        internal event EventHandler ValueChanged;

        internal KitNumber(decimal minimum, decimal maximum, int decimals, decimal start)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            low = minimum;
            high = maximum;
            places = decimals;
            step = high - low > 100000 ? 1000 : 1;
            value = Clamp(start);
            BackColor = Neon.Ground;
            if (Small)
            {
                slider = new KitSlider((int)low, (int)high, (int)value);
                slider.Changed += delegate { Set(slider.Value); Invalidate(); };
                Controls.Add(slider);
                return;
            }
            entry = new KitEntry(false);
            entry.Box.TextAlign = HorizontalAlignment.Right;
            entry.Box.Text = Shown(value);
            entry.Box.Leave += delegate { Commit(entry.Box.Text); };
            entry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { Commit(entry.Box.Text); e.SuppressKeyPress = true; } };
            entry.Box.MouseWheel += delegate(object sender, MouseEventArgs e) { Set(value + (e.Delta > 0 ? step : -step)); };
            KitKey minus = new KitKey { Text = "-" };
            KitKey plus = new KitKey { Text = "+" };
            minus.Click += delegate { Set(value - step); };
            plus.Click += delegate { Set(value + step); };
            Controls.Add(entry);
            Controls.Add(minus);
            Controls.Add(plus);
        }

        internal bool Small { get { return places == 0 && high - low <= SmallSteps - 1; } }

        internal decimal Minimum { get { return low; } }

        internal decimal Maximum { get { return high; } }

        internal decimal Value { get { return value; } set { Set(value); } }

        internal string Prefix
        {
            get { return prefix; }
            set { prefix = value ?? ""; if (entry != null) entry.Box.Text = Shown(this.value); }
        }

        private decimal Clamp(decimal number) { return Math.Max(low, Math.Min(high, Math.Round(number, places, MidpointRounding.AwayFromZero))); }

        private string Shown(decimal number) { return (prefix.Length > 0 ? prefix + " " : "") + number.ToString("F" + places, CultureInfo.InvariantCulture); }

        private void Set(decimal number)
        {
            decimal next = Clamp(number);
            if (entry != null) entry.Box.Text = Shown(next);
            if (slider != null && slider.Value != (int)next) slider.Value = (int)next;
            if (next == value) return;
            value = next;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        internal void Commit(string text)
        {
            string plain = (text ?? "").Replace("$", "").Replace(",", "").Trim();
            decimal parsed;
            if (!decimal.TryParse(plain, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)) { Set(value); return; }
            decimal next = Clamp(parsed);
            if (next != Math.Round(parsed, places, MidpointRounding.AwayFromZero) && entry != null) entry.Flash(Look.Yellow);
            Set(next);
        }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); foreach (Control child in Controls) child.Font = Font; }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            float s = Kit.Unit(this);
            if (slider != null) { slider.SetBounds(0, 0, Math.Max(1, Width - (int)(60 * s)), Height); return; }
            if (entry == null || Controls.Count < 3) return;
            int key = (int)(26 * s), gap = (int)(4 * s);
            int field = Math.Max(1, Width - 2 * key - 2 * gap);
            entry.SetBounds(0, 0, field, Height);
            Controls[1].SetBounds(field + gap, 0, key, Height);
            Controls[2].SetBounds(field + 2 * gap + key, 0, key, Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Kit.Backdrop(e.Graphics, this, s);
            if (slider == null) return;
            using (Font bold = Kit.Type(10f, true))
                TextRenderer.DrawText(e.Graphics, value.ToString(CultureInfo.InvariantCulture) + Unit, bold, new Rectangle(slider.Right + (int)(8 * s), 0, Math.Max(1, Width - slider.Right - (int)(8 * s)), Height), Neon.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class KitMeter : Control
    {
        internal int Maximum = 60;
        private int level;
        private int threshold;

        internal KitMeter()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Neon.Ground;
            TabStop = false;
        }

        internal int Level { get { return level; } set { int next = Math.Max(0, Math.Min(Maximum, value)); if (next == level) return; level = next; Invalidate(); } }

        internal int Threshold { get { return threshold; } set { if (value == threshold) return; threshold = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            Kit.Backdrop(graphics, this, s);
            float width = Width - 4 * s;
            float top = (Height - 6 * s) / 2f;
            Kit.Dots(graphics, 0, top, width, 6 * s, (double)level / Maximum, level >= threshold && level > 0 ? Neon.Cyan : Neon.Lit, Kit.Track, s);
            float x = width * threshold / Maximum;
            using (Brush tick = new SolidBrush(Look.Yellow)) graphics.FillRectangle(tick, x, top - 4 * s, 2 * s, 14 * s);
        }
    }

    internal sealed class KitStat : Label
    {
        internal double Share;
        internal string Big = "";
        internal string Rest = "";
        internal string Below = "";

        internal KitStat()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            BackColor = Neon.Ground;
            ForeColor = Neon.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            Kit.Backdrop(graphics, this, s);
            using (Font bold = Kit.Type(10.5f, true))
            using (Font hint = Kit.Type(8.5f))
            {
                Size big = TextRenderer.MeasureText(Big, bold);
                TextRenderer.DrawText(graphics, Big, bold, new Point(0, 0), Neon.White, TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(graphics, Rest, Font, new Rectangle(big.Width, 0, Math.Max(1, Width - big.Width), big.Height), Neon.Faint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                float bar = big.Height + 4 * s;
                Color lit = Share >= 1 ? Neon.Hot : Share > 0.8 ? Look.Yellow : Neon.Cyan;
                Kit.Dots(graphics, 0, bar, Math.Min(Width, 360 * s), 6 * s, Share, lit, Kit.Track, s);
                TextRenderer.DrawText(graphics, Below, hint, new Rectangle(0, (int)(bar + 12 * s), Width, Math.Max(1, Height - (int)(bar + 12 * s))), Neon.Faint, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
            }
        }
    }
}
