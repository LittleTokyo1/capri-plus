using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class DigestItem
    {
        internal DateTime When;
        internal string Source = "";
        internal string Text = "";
        internal bool Seen;
        internal bool Replied;
    }

    internal sealed class SavedDigest
    {
        public long When;
        public string Source, Text;
        public bool Seen, Replied;
    }

    internal static class Digest
    {
        internal const int MostShown = 7;
        private const int Keep = 50;
        private const int KeepHours = 24;
        private const int SourceWidth = 7;
        private const int TextWidth = 36;
        private static readonly object gate = new object();
        private static readonly List<DigestItem> items = new List<DigestItem>();
        private static string loadedFrom;

        internal static string FilePath
        {
            get { return ActivityLog.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ActivityLog.FilePath), "digest.json"); }
        }

        internal static void Reset()
        {
            lock (gate)
            {
                items.Clear();
                loadedFrom = null;
            }
        }

        private static void Load()
        {
            string path = FilePath;
            if (loadedFrom == path) return;
            loadedFrom = path;
            items.Clear();
            if (path == null || !File.Exists(path)) return;
            try
            {
                List<SavedDigest> saved = new JavaScriptSerializer().Deserialize<List<SavedDigest>>(File.ReadAllText(path));
                if (saved != null) foreach (SavedDigest one in saved) if ((DateTime.Now - new DateTime(one.When)).TotalHours <= KeepHours) items.Add(new DigestItem { When = new DateTime(one.When), Source = one.Source ?? "", Text = one.Text ?? "", Seen = one.Seen, Replied = one.Replied });
            }
            catch (Exception error) { Brain.Log("digest.json", -1, error.GetType().Name + ": " + error.Message); }
        }

        private static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            List<SavedDigest> saved = items.ConvertAll(delegate(DigestItem one) { return new SavedDigest { When = one.When.Ticks, Source = one.Source, Text = one.Text, Seen = one.Seen, Replied = one.Replied }; });
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".new";
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(saved));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) { Brain.Log("digest.json", -1, error.GetType().Name + ": " + error.Message); }
        }

        internal static int Count { get { lock (gate) { Load(); return items.FindAll(delegate(DigestItem one) { return !one.Seen; }).Count; } } }

        internal static void Add(string source, string text, DateTime when)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lock (gate)
            {
                Load();
                items.Add(new DigestItem { When = when, Source = source ?? "", Text = text.Trim() });
                if (items.Count > Keep) items.RemoveRange(0, items.Count - Keep);
                Save();
            }
        }

        internal static List<DigestItem> Items() { lock (gate) { Load(); return items.FindAll(delegate(DigestItem one) { return !one.Seen; }); } }

        internal static List<DigestItem> All() { lock (gate) { Load(); return new List<DigestItem>(items); } }

        internal static void Clear() { lock (gate) { Load(); foreach (DigestItem one in items) one.Seen = true; Save(); } }

        internal static void MarkReplied(DigestItem item)
        {
            if (item == null) return;
            lock (gate)
            {
                Load();
                foreach (DigestItem one in items)
                    if (one.When == item.When && one.Text == item.Text) { one.Replied = true; one.Seen = true; }
                Save();
            }
        }

        internal static int Remove(DigestItem item)
        {
            if (item == null) return 0;
            lock (gate)
            {
                Load();
                int gone = items.RemoveAll(delegate(DigestItem one) { return one.When == item.When && one.Text == item.Text; });
                if (gone > 0) Save();
                return gone;
            }
        }

        internal static int RemoveReplied()
        {
            lock (gate)
            {
                Load();
                int gone = items.RemoveAll(delegate(DigestItem one) { return one.Replied; });
                if (gone > 0) Save();
                return gone;
            }
        }

        internal static string Intro(int count){ return count + (count == 1 ? " thing" : " things") + " while you were out."; }

        internal static bool May(bool notHome, bool game, bool paused, bool quiet, bool hidden, bool busy, int count)
        {
            return count > 0 && !notHome && !game && !paused && !quiet && !hidden && !busy;
        }

        internal static List<string> Rows(List<DigestItem> list, int most)
        {
            List<string> rows = new List<string>();
            int skip = list.Count > most ? list.Count - (most - 1) : 0;
            if (skip > 0) rows.Add("+" + skip + " more");
            for (int i = skip; i < list.Count; i++)
            {
                DigestItem item = list[i];
                string text = item.Text.Replace('\n', ' ').Replace('\r', ' ');
                if (text.Length > TextWidth) text = text.Substring(0, TextWidth - 3) + "...";
                string source = item.Source.Length > SourceWidth ? item.Source.Substring(0, SourceWidth) : item.Source.PadRight(SourceWidth);
                rows.Add(item.When.ToString("HH:mm", CultureInfo.InvariantCulture) + "  " + source + " " + text);
            }
            return rows;
        }

        internal static string Full(List<DigestItem> list, int most, int row)
        {
            int skip = list.Count > most ? list.Count - (most - 1) : 0;
            int shown = list.Count - skip + (skip > 0 ? 1 : 0);
            if (row < 0 || row >= shown) return null;
            if (skip > 0 && row == 0)
            {
                List<string> lines = new List<string>();
                for (int i = 0; i < skip; i++) lines.Add(list[i].When.ToString("HH:mm", CultureInfo.InvariantCulture) + " " + list[i].Text);
                return string.Join("\n", lines.ToArray());
            }
            return list[skip + row - (skip > 0 ? 1 : 0)].Text;
        }
    }

    internal sealed class DigestBox : Overlay
    {
        private const int Columns = 51;
        private const string Speaker = "CAPRI: ";
        private const string Label = "> sys: (while you were away)";
        private const string Note = "CLICK A LINE TO OPEN IT";
        private const string Cross = "x";
        internal event Action Picked;
        internal event Action<string> Read;
        internal double Opened = -1;
        private List<string> rows = new List<string>();
        private List<DigestItem> items = new List<DigestItem>();
        private List<Rectangle> rowBoxes = new List<Rectangle>();
        private Rectangle closeBox;
        private string intro = "";
        private Rectangle panelOnScreen;

        internal DigestBox() : base("While you were away") { Cursor = Look.Pointer; }

        internal Rectangle PanelOnScreen { get { return panelOnScreen; } }

        internal Rectangle CloseBox { get { return closeBox; } }

        internal Rectangle RowBox(int row) { return row >= 0 && row < rowBoxes.Count ? rowBoxes[row] : Rectangle.Empty; }

        internal void Open(List<DigestItem> list, double now)
        {
            items = new List<DigestItem>(list);
            rows = Digest.Rows(list, Digest.MostShown);
            intro = Digest.Intro(list.Count);
            Opened = now;
        }

        internal int Hit(Point at)
        {
            if (closeBox.Contains(at)) return -2;
            for (int i = 0; i < rowBoxes.Count; i++) if (rowBoxes[i].Contains(at)) return i;
            return -1;
        }

        internal void Press(Point at)
        {
            int hit = Hit(at);
            if (hit == -2) { if (Picked != null) Picked(); return; }
            if (hit < 0) return;
            string text = Digest.Full(items, Digest.MostShown, hit);
            if (text != null && Read != null) Read(text);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) Press(e.Location);
        }

        internal void Update(double now, float s, Point head, Point feet, Rectangle area, Rectangle above)
        {
            Point at;
            Bitmap image = Render(s, head, feet, area, above, out at);
            Present(image, at);
        }

        private Bitmap Render(float s, Point head, Point feet, Rectangle area, Rectangle above, out Point at)
        {
            string all = Speaker + intro + string.Join("", rows.ToArray());
            using (Font text = Look.Mono(14 * s, FontStyle.Regular, GraphicsUnit.Pixel, all))
            using (Font italic = Look.Mono(14 * s, FontStyle.Italic, GraphicsUnit.Pixel, all))
            using (Font bold = Look.Mono(14 * s, FontStyle.Bold, GraphicsUnit.Pixel, all))
            using (Font small = Look.Mono(12 * s, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                float cw = Look.CharWidth(text);
                float line = 19 * s;
                float margin = 12 * s;
                float panelWidth = 24 * s + Columns * cw + 14 * s;
                float panelHeight = 12 * s + line * 2 + 6 * s + line * rows.Count + 6 * s + 15 * s + 10 * s;
                int width = (int)Math.Ceiling(panelWidth + margin * 2);
                int height = (int)Math.Ceiling(panelHeight + margin * 2);
                int left = Math.Max(area.Left, Math.Min(area.Right - width, (int)(head.X - margin - panelWidth * 0.7f)));
                int y;
                if (above.Width > 0 && above.Top - height - 6 * s >= area.Top) y = (int)(above.Top - height + margin - 6 * s);
                else if (above.Width == 0 && head.Y - height >= area.Top) y = head.Y - height;
                else y = Math.Max(area.Top, Math.Min(area.Bottom - height, feet.Y));
                at = new Point(left, y);
                RectangleF panel = new RectangleF(margin, margin, panelWidth, panelHeight);
                panelOnScreen = new Rectangle(left + (int)panel.X, y + (int)panel.Y, (int)panel.Width, (int)panel.Height);
                Bitmap image = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(image))
                using (Brush dim = new SolidBrush(Neon.Faint))
                using (Brush red = new SolidBrush(Neon.Rose))
                using (Brush ink = new SolidBrush(Neon.White))
                using (Brush cyan = new SolidBrush(Neon.Cyan))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Kit.Panel(graphics, panel, s, 10 * s, 4 * s, 36 * s, 14 * s);
                    Kit.Rail(graphics, panel, Neon.Hot, s);
                    float x = panel.Left + 24 * s;
                    float row = panel.Top + 12 * s;
                    graphics.DrawString(Label, italic, dim, x, row, StringFormat.GenericTypographic);
                    float key = 26 * s;
                    closeBox = new Rectangle((int)(panel.Right - 10 * s - key), (int)(panel.Top + 4 * s), (int)key, (int)key);
                    SizeF cross = graphics.MeasureString(Cross, bold, PointF.Empty, StringFormat.GenericTypographic);
                    graphics.DrawString(Cross, bold, red, closeBox.X + (key - cross.Width) / 2, closeBox.Y + (key - cross.Height) / 2, StringFormat.GenericTypographic);
                    row += line;
                    graphics.DrawString(Speaker.TrimEnd(), bold, red, x, row, StringFormat.GenericTypographic);
                    graphics.DrawString(intro, text, ink, x + Speaker.Length * cw, row, StringFormat.GenericTypographic);
                    row += line + 6 * s;
                    rowBoxes = new List<Rectangle>();
                    foreach (string shown in rows)
                    {
                        rowBoxes.Add(new Rectangle((int)panel.Left, (int)(row - 2 * s), (int)panel.Width, (int)line));
                        if (shown.StartsWith("+", StringComparison.Ordinal)) graphics.DrawString(shown, text, dim, x, row, StringFormat.GenericTypographic);
                        else
                        {
                            graphics.DrawString(shown.Substring(0, 5), text, dim, x, row, StringFormat.GenericTypographic);
                            graphics.DrawString(shown.Substring(7, 7), text, cyan, x + 7 * cw, row, StringFormat.GenericTypographic);
                            graphics.DrawString(shown.Substring(15), text, ink, x + 15 * cw, row, StringFormat.GenericTypographic);
                        }
                        row += line;
                    }
                    SizeF size = graphics.MeasureString(Note, small, PointF.Empty, StringFormat.GenericTypographic);
                    graphics.DrawString(Note, small, dim, panel.Right - 14 * s - size.Width, panel.Bottom - 25 * s, StringFormat.GenericTypographic);
                }
                return image;
            }
        }
    }
}
