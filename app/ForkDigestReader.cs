using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class DigestReader : Form
    {
        internal const string RepliedMark = "REPLIED";
        private const int Preview = 46;
        private const int Title = 50;
        private const int Grip = 14;
        private const string Caption = "WHILE YOU WERE AWAY";
        private static readonly Regex Ask = new Regex(@"^\W*(?:(?:hey\s+)?capri[,\s]+)?(?:(?:can|could|would)\s+you\s+|please\s+)?(?:(?:show|open|pull\s+up|bring\s+up)\s+(?:me\s+)?(?:what\s+i\s+missed|(?:the\s+|my\s+)?(?:while\s+you\s+were\s+away|away|missed)(?:\s+(?:box|list|messages|window|stuff|things))?)|what\s+did\s+i\s+miss)\W*$", RegexOptions.IgnoreCase);
        internal event Action<DigestItem, string> Replied;
        internal event Action<string> ReadAloud;
        private readonly ListBox list = new ListBox();
        private readonly KitEntry full = new KitEntry(true);
        private readonly KitEntry reply = new KitEntry(true);
        private readonly KitHead about = new KitHead();
        private readonly KitHead answerHead = new KitHead { Text = "YOUR REPLY" };
        private readonly KitLabel hint = new KitLabel { Text = "Ctrl + Enter sends", AutoSize = false, ForeColor = Neon.Faint, TextAlign = ContentAlignment.MiddleLeft };
        private readonly KitKey send = new KitKey { Text = "SEND", Loud = true, Accent = Neon.Cyan, AccessibleName = "Send reply" };
        private readonly KitKey aloud = new KitKey { Text = "READ ALOUD", AccessibleName = "Read it aloud" };
        private readonly KitKey close = new KitKey { Glyph = 2, AccessibleName = "Close" };
        private readonly KitKey shrink = new KitKey { Glyph = 1, AccessibleName = "Minimise" };
        private readonly KitKey clear = new KitKey { Text = "CLEAR REPLIED", AccessibleName = "Remove the replied messages" };
        private int hover = -1;
        private bool overCross;
        private int downOn = -1;
        private readonly KitPanel side = new KitPanel();
        private readonly KitPanel gap = new KitPanel();
        private readonly KitPanel right = new KitPanel();
        private readonly KitPanel reading = new KitPanel();
        private readonly KitPanel answer = new KitPanel();
        private readonly KitPanel keys = new KitPanel();
        private readonly KitPanel between = new KitPanel();
        private List<DigestItem> items = new List<DigestItem>();
        private readonly List<string> rows = new List<string>();
        private bool loaded;

        internal DigestReader()
        {
            Text = "While you were away";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            DoubleBuffered = true;
            BackColor = Neon.Ground;
            ForeColor = Neon.White;
            Font = Kit.Type(9.5f);
            ClientSize = new Size(880, 540);
            MinimumSize = new Size(600, 380);
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Neon.Field;
            list.ForeColor = Neon.White;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.IntegralHeight = false;
            list.Dock = DockStyle.Fill;
            list.DrawItem += DrawRow;
            list.SelectedIndexChanged += delegate { ShowPicked(); };
            list.HandleCreated += delegate { Kit.Dark(list); };
            list.MouseMove += delegate(object sender, MouseEventArgs e) { Track(e.Location); };
            list.MouseLeave += delegate { list.Cursor = Cursors.Default; Hover(-1); };
            list.MouseDown += delegate(object sender, MouseEventArgs e) { downOn = e.Button == MouseButtons.Left ? CrossAt(e.Location) : -1; };
            list.MouseUp += delegate(object sender, MouseEventArgs e) { int at = CrossAt(e.Location); if (e.Button == MouseButtons.Left && at >= 0 && at == downOn) Drop(at); downOn = -1; };
            list.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Delete) { e.Handled = true; Drop(list.SelectedIndex); } };
            full.Box.ReadOnly = true;
            full.Dock = DockStyle.Fill;
            full.Font = Kit.Type(10.5f);
            reply.Dock = DockStyle.Fill;
            reply.Font = Kit.Type(10.5f);
            reply.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; Send(reply.Box.Text); } };
            about.Dock = DockStyle.Top;
            about.Font = Kit.Type(9f, true);
            answerHead.Dock = DockStyle.Top;
            answerHead.Font = Kit.Type(9f, true);
            send.Dock = DockStyle.Right;
            aloud.Dock = DockStyle.Right;
            hint.Dock = DockStyle.Fill;
            hint.Font = Kit.Type(8.5f);
            send.Click += delegate { Send(reply.Box.Text); };
            aloud.Click += delegate { if (ReadAloud != null && FullText.Length > 0) ReadAloud(FullText); };
            close.Unfocusable();
            shrink.Unfocusable();
            clear.Unfocusable();
            clear.Font = Kit.Type(8f, true);
            clear.Click += delegate { DropReplied(); };
            close.Click += delegate { Hide(); };
            shrink.Click += delegate { WindowState = FormWindowState.Minimized; };
            side.Dock = DockStyle.Left;
            side.Controls.Add(list);
            gap.Dock = DockStyle.Left;
            keys.Dock = DockStyle.Bottom;
            between.Dock = DockStyle.Right;
            keys.Controls.Add(hint);
            keys.Controls.Add(aloud);
            keys.Controls.Add(between);
            keys.Controls.Add(send);
            full.Box.ScrollBars = ScrollBars.None;
            reply.Box.ScrollBars = ScrollBars.None;
            full.Box.TextChanged += delegate { FitBars(full.Box); };
            reply.Box.TextChanged += delegate { FitBars(reply.Box); };
            full.Box.Resize += delegate { FitBars(full.Box); };
            reply.Box.Resize += delegate { FitBars(reply.Box); };
            answer.Dock = DockStyle.Bottom;
            answer.Controls.Add(reply);
            answer.Controls.Add(answerHead);
            answer.Controls.Add(keys);
            reading.Dock = DockStyle.Fill;
            reading.Controls.Add(full);
            right.Dock = DockStyle.Fill;
            right.Controls.Add(reading);
            right.Controls.Add(answer);
            right.Controls.Add(about);
            Controls.Add(right);
            Controls.Add(gap);
            Controls.Add(side);
            Controls.Add(close);
            Controls.Add(shrink);
            Controls.Add(clear);
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            Arrange();
        }

        private float Unit() { return loaded ? Kit.Unit(this) : 1f; }

        private int Scaled(float value) { return (int)Math.Round(value * Unit()); }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            loaded = true;
            Arrange();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Arrange();
        }

        private void Arrange()
        {
            Padding = new Padding(Scaled(16), Scaled(Title), Scaled(16), Scaled(16));
            side.Width = Scaled(Math.Max(280, Math.Min(360, ClientSize.Width / Math.Max(0.01f, Unit()) * 0.38f)));
            gap.Width = Scaled(14);
            list.ItemHeight = Math.Max(20, Scaled(70));
            about.Height = Scaled(26);
            answerHead.Height = Scaled(26);
            reading.Padding = new Padding(0, Scaled(8), 0, Scaled(12));
            answer.Height = Scaled(196);
            reply.Padding = Padding.Empty;
            keys.Height = Scaled(46);
            keys.Padding = new Padding(0, Scaled(10), 0, 0);
            between.Width = Scaled(8);
            send.Width = Scaled(110);
            aloud.Width = Scaled(130);
            close.Bounds = new Rectangle(ClientSize.Width - Scaled(48), Scaled(12), Scaled(30), Scaled(24));
            shrink.Bounds = new Rectangle(ClientSize.Width - Scaled(82), Scaled(12), Scaled(30), Scaled(24));
            clear.Bounds = new Rectangle(shrink.Left - Scaled(140), Scaled(12), Scaled(128), Scaled(24));
            close.BringToFront();
            shrink.BringToFront();
            clear.BringToFront();
            if (loaded) Neon.Clip(this, 16 * Unit(), 6 * Unit());
            Invalidate();
        }

        private static void FitBars(TextBox box)
        {
            if (box.Width <= 0 || box.Height <= 0) return;
            Size needed = TextRenderer.MeasureText(box.Text.Length == 0 ? " " : box.Text, box.Font, new Size(Math.Max(1, box.Width - 24), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
            ScrollBars wanted = needed.Height > box.Height ? ScrollBars.Vertical : ScrollBars.None;
            if (box.ScrollBars == wanted) return;
            bool focused = box.Focused;
            int start = box.SelectionStart, length = box.SelectionLength;
            box.ScrollBars = wanted;
            if (!focused) return;
            box.Focus();
            box.Select(start, length);
            box.ScrollToCaret();
        }

        internal List<string> Rows { get { return new List<string>(rows); } }

        internal string FullText { get { return full.Box.Text.Replace("\r\n", "\n"); } }

        internal string Waiting { get { int open = items.FindAll(delegate(DigestItem one) { return !one.Replied; }).Count; return open == 0 ? "ALL ANSWERED" : open + " WAITING"; } }

        internal int Picked { get { return list.SelectedIndex; } }

        internal bool CanClear { get { return clear.Enabled; } }

        internal static Rectangle Cross(Rectangle row, float s)
        {
            int size = (int)(18 * s);
            return new Rectangle(row.Right - (int)(8 * s) - size, row.Top + (int)(5 * s), size, size);
        }

        private int CrossAt(Point at)
        {
            int index = list.IndexFromPoint(at);
            if (index < 0 || index >= items.Count) return -1;
            return Cross(list.GetItemRectangle(index), Kit.Unit(list)).Contains(at) ? index : -1;
        }

        private void Track(Point at)
        {
            int index = list.IndexFromPoint(at);
            if (index >= items.Count || !list.ClientRectangle.Contains(at)) index = -1;
            bool onCross = index >= 0 && CrossAt(at) == index;
            list.Cursor = onCross ? Cursors.Hand : Cursors.Default;
            if (index == hover && onCross == overCross) return;
            overCross = onCross;
            hover = index;
            list.Invalidate();
        }

        internal void Hover(int index)
        {
            hover = index;
            if (index < 0) overCross = false;
            list.Invalidate();
        }

        internal void Drop(int index)
        {
            if (index < 0 || index >= items.Count) return;
            DigestItem item = items[index];
            Digest.Remove(item);
            ActivityLog.Write("digest", "removed", item.Source);
            Fill(Digest.All(), index);
        }

        internal void DropReplied()
        {
            DigestItem picked = list.SelectedIndex >= 0 && list.SelectedIndex < items.Count ? items[list.SelectedIndex] : null;
            int gone = Digest.RemoveReplied();
            if (gone == 0) return;
            ActivityLog.Write("digest", "cleared replied", gone + " removed");
            List<DigestItem> all = Digest.All();
            int pick = 0;
            for (int i = 0; i < all.Count; i++) if (picked != null && all[i].When == picked.When && all[i].Text == picked.Text) pick = i;
            Fill(all, pick);
        }

        internal static bool Asked(string text) { return Ask.IsMatch(text ?? ""); }

        internal static string ReplyLine(DigestItem item, string answer)
        {
            return "About this from while you were away (" + item.Source + ", " + item.When.ToString("HH:mm", CultureInfo.InvariantCulture) + "): \"" + item.Text + "\". My reply: " + answer;
        }

        internal static string Row(DigestItem item)
        {
            string text = item.Text.Replace('\n', ' ').Replace('\r', ' ');
            if (text.Length > Preview) text = text.Substring(0, Preview - 3) + "...";
            return item.When.ToString("HH:mm", CultureInfo.InvariantCulture) + "  " + item.Source + (item.Replied ? "  " + RepliedMark : "") + "\n" + text;
        }

        internal static string When(DigestItem item, DateTime now)
        {
            string time = item.When.ToString("HH:mm", CultureInfo.InvariantCulture);
            return item.When.Date == now.Date ? time : item.When.Date == now.Date.AddDays(-1) ? "YESTERDAY " + time : item.When.ToString("d MMM", CultureInfo.InvariantCulture).ToUpperInvariant() + " " + time;
        }

        internal static int NextOpen(List<DigestItem> all, int from)
        {
            if (all == null || all.Count == 0) return -1;
            for (int step = 1; step <= all.Count; step++)
            {
                int at = (Math.Max(0, from) + step) % all.Count;
                if (!all[at].Replied) return at;
            }
            return Math.Max(0, Math.Min(all.Count - 1, from));
        }

        internal void Show(List<DigestItem> all, int pick)
        {
            Fill(all, pick);
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            reply.Box.Focus();
        }

        private void Fill(List<DigestItem> all, int pick)
        {
            items = new List<DigestItem>(all ?? new List<DigestItem>());
            hover = -1;
            overCross = false;
            rows.Clear();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (DigestItem item in items)
            {
                string row = Row(item);
                rows.Add(row);
                list.Items.Add(row);
            }
            list.EndUpdate();
            clear.Enabled = items.Exists(delegate(DigestItem one) { return one.Replied; });
            Pick(items.Count == 0 ? -1 : Math.Max(0, Math.Min(items.Count - 1, pick)));
            Invalidate();
        }

        internal void Pick(int index)
        {
            if (index < 0 || index >= items.Count) { ShowPicked(); return; }
            if (list.SelectedIndex != index) list.SelectedIndex = index;
            ShowPicked();
        }

        internal void Send(string text)
        {
            string said = (text ?? "").Trim();
            int index = list.SelectedIndex;
            if (said.Length == 0 || index < 0 || index >= items.Count) return;
            reply.Box.Text = "";
            if (Replied != null) Replied(items[index], said);
        }

        private void ShowPicked()
        {
            int index = list.SelectedIndex;
            if (index < 0 || index >= items.Count)
            {
                about.Text = items.Count == 0 ? "NOTHING FROM THE LAST DAY" : "PICK A MESSAGE";
                full.Box.Text = "";
                send.Enabled = false;
                aloud.Enabled = false;
            }
            else
            {
                DigestItem item = items[index];
                about.Text = (item.Source ?? "").ToUpperInvariant() + "   " + When(item, DateTime.Now) + (item.Replied ? "   " + RepliedMark : "");
                full.Box.Text = (item.Text ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
                send.Enabled = true;
                aloud.Enabled = true;
            }
            about.Invalidate();
            list.Invalidate();
        }

        private void DrawRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= items.Count) return;
            float s = Kit.Unit(list);
            Graphics graphics = e.Graphics;
            Rectangle box = e.Bounds;
            DigestItem item = items[e.Index];
            bool picked = (e.State & DrawItemState.Selected) != 0;
            using (Brush back = new SolidBrush(picked ? Neon.Pressed : Neon.Field)) graphics.FillRectangle(back, box);
            using (Brush line = new SolidBrush(Neon.Dash)) graphics.FillRectangle(line, box.Left, box.Bottom - Math.Max(1, (int)s), box.Width, Math.Max(1, (int)s));
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (picked) using (Brush rail = new SolidBrush(Neon.Hot)) graphics.FillRectangle(rail, box.Left, box.Top, Math.Max(2, 3 * s), box.Height - Math.Max(1, (int)s));
            if (!item.Replied) using (Brush dot = new SolidBrush(Neon.Hot)) graphics.FillEllipse(dot, box.Left + 11 * s, box.Top + 14 * s, 6 * s, 6 * s);
            int left = box.Left + (int)(24 * s);
            int wide = box.Width - (int)(36 * s);
            using (Font tag = Kit.Type(8f, true))
            using (Font body = Kit.Type(9.5f))
            {
                string source = (item.Source ?? "").ToUpperInvariant();
                string time = When(item, DateTime.Now);
                Size timeSize = TextRenderer.MeasureText(time, tag);
                Rectangle top = new Rectangle(left, box.Top + (int)(8 * s), wide, (int)(18 * s));
                int timeRight = box.Right - (int)(12 * s);
                if (e.Index == hover || picked)
                {
                    Rectangle cross = Cross(box, s);
                    bool hot = e.Index == hover && overCross;
                    if (hot) using (Brush glow = new SolidBrush(Neon.Pressed)) graphics.FillRectangle(glow, cross);
                    using (Pen pen = new Pen(hot ? Neon.Hot : Neon.Faint, 1.5f * s))
                    {
                        float cx = cross.Left + cross.Width / 2f, cy = cross.Top + cross.Height / 2f, r = 4 * s;
                        graphics.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                        graphics.DrawLine(pen, cx - r, cy + r, cx + r, cy - r);
                    }
                    timeRight = cross.Left - (int)(6 * s);
                }
                TextRenderer.DrawText(graphics, time, tag, new Rectangle(timeRight - timeSize.Width, top.Top, timeSize.Width, top.Height), Neon.Faint, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                Size sourceSize = TextRenderer.MeasureText(source, tag);
                TextRenderer.DrawText(graphics, source, tag, top, Neon.Rose, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                if (item.Replied) TextRenderer.DrawText(graphics, RepliedMark, tag, new Rectangle(left + sourceSize.Width + (int)(4 * s), top.Top, (int)(90 * s), top.Height), Neon.Cyan, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                string text = (item.Text ?? "").Replace('\n', ' ').Replace('\r', ' ');
                Rectangle under = new Rectangle(left, box.Top + (int)(28 * s), wide, box.Height - (int)(34 * s));
                TextRenderer.DrawText(graphics, text, body, under, item.Replied ? Neon.Faint : Neon.White, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.TextBoxControl);
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg != 0x0084) { base.WndProc(ref message); return; }
            long value = message.LParam.ToInt64();
            Point at = PointToClient(new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF)));
            if (at.X >= ClientSize.Width - Scaled(Grip) && at.Y >= ClientSize.Height - Scaled(Grip)) { message.Result = new IntPtr(17); return; }
            message.Result = new IntPtr(at.Y < Scaled(Title - 8) ? 2 : 1);
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
            using (Font small = Kit.Type(8.5f, true))
            {
                Size label = TextRenderer.MeasureText(Caption, caption);
                TextRenderer.DrawText(graphics, Caption, caption, new Point((int)(26 * s), (int)(13 * s)), Neon.Rose, TextFormatFlags.NoPrefix);
                string waiting = Waiting;
                Size count = TextRenderer.MeasureText(waiting, small);
                TextRenderer.DrawText(graphics, waiting, small, new Point((int)(26 * s) + label.Width + (int)(10 * s), (int)(13 * s) + (label.Height - count.Height) / 2 + 1), waiting == "ALL ANSWERED" ? Neon.Cyan : Neon.Faint, TextFormatFlags.NoPrefix);
            }
            using (Pen grip = new Pen(Neon.Faint, Math.Max(1f, s)))
                for (int i = 1; i <= 2; i++)
                {
                    float d = (4 + i * 4) * s;
                    graphics.DrawLine(grip, ClientSize.Width - 7 * s - d, ClientSize.Height - 7 * s, ClientSize.Width - 7 * s, ClientSize.Height - 7 * s - d);
                }
        }

        internal void Draw(Bitmap image) { DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height)); }
    }

    internal sealed partial class Companion
    {
        private DigestReader digestReader;

        private void DigestOpen(string picked)
        {
            List<DigestItem> all = Digest.All();
            int pick = DigestReader.NextOpen(all, all.Count - 1);
            for (int i = 0; i < all.Count; i++) if (picked != null && all[i].Text == picked) pick = i;
            if (digestReader == null || digestReader.IsDisposed)
            {
                digestReader = new DigestReader();
                digestReader.Replied += DigestReplied;
                digestReader.ReadAloud += DigestReadAloud;
            }
            digestReader.Show(all, pick);
            ActivityLog.Write("digest", "window opened", all.Count + " from the last day");
        }

        internal const string DigestMenuName = "digest";

        internal static string DigestMenuText(int waiting)
        {
            return "While you were away" + (waiting > 0 ? " (" + waiting + " waiting)" : "");
        }

        private ToolStripMenuItem DigestMenuItem()
        {
            ToolStripMenuItem item = new ToolStripMenuItem(DigestMenuText(0), null, delegate { DigestOpen(null); });
            item.Name = DigestMenuName;
            menu.Opening += delegate { item.Text = DigestMenuText(Digest.All().FindAll(delegate(DigestItem one) { return !one.Replied; }).Count); };
            return item;
        }

        private void DigestReplied(DigestItem item, string text)
        {
            Digest.MarkReplied(item);
            ActivityLog.Write("digest", "replied", item.Source);
            int at = -1;
            List<DigestItem> all = Digest.All();
            for (int i = 0; i < all.Count; i++) if (all[i].When == item.When && all[i].Text == item.Text) at = i;
            if (digestReader != null && !digestReader.IsDisposed) digestReader.Show(all, DigestReader.NextOpen(all, at));
            typedLine = true;
            try { Hear(DigestReader.ReplyLine(item, text), text, "> you: "); }
            finally { typedLine = false; }
        }

        private void DigestReadAloud(string text)
        {
            ActivityLog.Write("digest", "read", null);
            heard = "(while you were away)";
            box.Prompt = Bubble.Sys;
            Answer(++asking, text, TalkState.Talking, chatting);
        }

        private bool DigestAsked(string text, bool voiceChat)
        {
            if (!DigestReader.Asked(text) || Delivery.NotHome()) return false;
            if (Digest.All().Count == 0) { Remark("digest", "Nothing from while you were away in the last day."); return true; }
            DigestOpen(null);
            Remark("digest", "Here's everything from while you were away.");
            return true;
        }
    }
}
