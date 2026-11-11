using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class OwnForms
    {
        private static readonly string[] Titles = { "Capri+ settings", "Capri Mainframe" };

        internal static bool Is(string title)
        {
            return Array.Exists(Titles, name => string.Equals(name, (title ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static class Monitors
    {
        internal static Func<List<DisplayInfo>> All = Screens.All;
        internal static Func<List<OpenWindow>> Windows = delegate { return Hands.ReadOpen(OwnForms.Is); };
        internal static Func<IntPtr, Rectangle> Where = delegate(IntPtr handle) { return Screen.FromHandle(handle).Bounds; };
        internal static Func<IntPtr, bool> Folded = IsIconic;
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        private static readonly Regex ChatApp = new Regex(@"\b(gmail|outlook|thunderbird|discord|whatsapp|telegram|signal|slack|teams|messenger)\b", RegexOptions.IgnoreCase);
        private static readonly Regex MailWords = new Regex(@"\b(inbox|mail)\b|@", RegexOptions.IgnoreCase);
        private const int MostNames = 6;
        private const int NameLength = 40;
        private static readonly Regex Mentioned = new Regex(@"\b(?:on|at|in|from)\s+(?:my\s+|the\s+)?(first|second|third|1st|2nd|3rd|main|primary|left|right|\d)\s*(?:monitor|screen|display)\b|\b(?:monitor|screen|display)\s+(\d)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Known = new Regex(@"^(first|second|third|one|two|three|four|main|primary|left|right|\d+)$");
        private static readonly Regex Incognito = new Regex(@"InPrivate|Incognito|Private Browsing|Private window", RegexOptions.IgnoreCase);

        internal static string Label(DisplayInfo display) { return "Monitor " + display.Number + (display.Primary ? " (main)" : ""); }

        internal static DisplayInfo Choose(string which)
        {
            string want = Regex.Replace((which ?? "").ToLowerInvariant(), @"\(.*?\)|\b(monitor|screen|display|number|no)\b\.?", "").Trim();
            want = want == "1st" ? "first" : want == "2nd" ? "second" : want == "3rd" ? "third" : want;
            if (!Known.IsMatch(want)) return null;
            return Screens.Pick(All(), want, Rectangle.Empty, Stage.Pointer());
        }

        internal static DisplayInfo Named(string words)
        {
            Match found = Mentioned.Match(words ?? "");
            if (!found.Success) return null;
            return Choose(found.Groups[1].Success ? found.Groups[1].Value : found.Groups[2].Value);
        }

        private static bool Private(OpenWindow window)
        {
            string[] blocked = ForkSettings.Current.Blocklist;
            if (blocked == null) return false;
            foreach (string name in blocked)
                if (name.Length > 0 && ((window.Title ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 || (window.Program ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            return false;
        }

        internal static bool Exists(Rectangle bounds)
        {
            foreach (DisplayInfo display in All()) if (display.Bounds == bounds) return true;
            return false;
        }

        internal static bool Hides(DisplayInfo display)
        {
            List<OpenWindow> windows;
            try { windows = Windows(); }
            catch (Exception) { return false; }
            foreach (OpenWindow window in windows)
            {
                if (!Private(window)) continue;
                try { if (Where(window.Handle) == display.Bounds) return true; }
                catch (Exception) { }
            }
            return false;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out WindowBox box);

        private struct WindowBox { public int Left, Top, Right, Bottom; }

        internal static Func<IntPtr, Rectangle> Box = delegate(IntPtr handle)
        {
            WindowBox box;
            return GetWindowRect(handle, out box) ? Rectangle.FromLTRB(box.Left, box.Top, box.Right, box.Bottom) : Rectangle.Empty;
        };

        internal static Sight Watched(Rectangle? monitor)
        {
            if (!monitor.HasValue) return null;
            Sight front = Computer.Glimpse();
            try { if (front != null && front.Window != IntPtr.Zero && Where(front.Window) == monitor.Value) return null; }
            catch (Exception) { return null; }
            List<OpenWindow> windows;
            try { windows = Windows(); }
            catch (Exception) { return new Sight(); }
            foreach (OpenWindow window in windows)
            {
                try
                {
                    if (Folded(window.Handle) || Where(window.Handle) != monitor.Value) continue;
                    return new Sight { Window = window.Handle, Title = window.Title ?? "", Program = window.Program ?? "", Bounds = Box(window.Handle) };
                }
                catch (Exception) { }
            }
            return new Sight();
        }

        internal static string Listing()
        {
            List<DisplayInfo> displays = All();
            if (displays.Count < 2) return "";
            List<OpenWindow> windows;
            try { windows = Windows(); }
            catch (Exception) { windows = new List<OpenWindow>(); }
            StringBuilder text = new StringBuilder("The PC has " + displays.Count + " monitors; call switch_monitor to look at and act on another one:");
            foreach (DisplayInfo display in displays)
            {
                List<string> names = new List<string>();
                foreach (OpenWindow window in windows)
                {
                    if (names.Count >= MostNames || Private(window) || Folded(window.Handle)) continue;
                    Rectangle on;
                    try { on = Where(window.Handle); }
                    catch (Exception) { continue; }
                    if (on != display.Bounds) continue;
                    string title = Regex.Replace(window.Title ?? "", @"\s+", " ").Trim();
                    if (Incognito.IsMatch(title)) continue;
                    Match app = ChatApp.Match((window.Program ?? "") + " " + title);
                    if (app.Success) title = char.ToUpperInvariant(app.Value[0]) + app.Value.Substring(1).ToLowerInvariant();
                    else if (MailWords.IsMatch(title)) title = "Mail";
                    names.Add(title.Length > NameLength ? title.Substring(0, NameLength - 3) + "..." : title);
                }
                text.Append("\n" + Label(display) + ", " + display.Bounds.Width + "x" + display.Bounds.Height + (names.Count > 0 ? ": " + string.Join(", ", names.ToArray()) : ": no app windows"));
            }
            return text.ToString();
        }
    }

    internal static class ManyEyes
    {
        internal const int PanelWidth = 1600;
        internal const int LabelHeight = 32;
        internal const int Longest = 1568;
        internal const string Told = " The picture holds his monitors one under another, each under its label; say which monitor something is on when it matters.";

        internal static bool Wanted(int monitors)
        {
            return ForkSettings.Current.LookBothMonitors && monitors > 1;
        }

        internal static string Said(int monitors)
        {
            return " (That was " + (monitors == 2 ? "both of his monitors" : "all " + monitors + " of his monitors") + " at once, one under another.)";
        }

        internal static string Titles(string front, List<string> others)
        {
            List<string> all = new List<string> { front ?? "" };
            if (others != null) foreach (string title in others) if (!string.IsNullOrWhiteSpace(title)) all.Add(title);
            return string.Join(" | ", all.ToArray());
        }

        internal static string OpenTitles(string front)
        {
            List<string> others = new List<string>();
            try { foreach (OpenWindow window in Monitors.Windows()) others.Add(window.Title); }
            catch (Exception) { }
            return Titles(front, others);
        }

        internal static Size Panel(Size monitor, int width)
        {
            if (monitor.Width <= 0 || monitor.Height <= 0) return Size.Empty;
            return new Size(width, Math.Max(1, (int)Math.Round((double)monitor.Height * width / monitor.Width)));
        }

        internal static Bitmap Shoot(List<DisplayInfo> displays, Func<DisplayInfo, bool> hides, Func<Rectangle, Bitmap> grab, Func<Bitmap, bool> black, out Rectangle every)
        {
            every = Rectangle.Empty;
            if (displays == null || !Wanted(displays.Count)) return null;
            foreach (DisplayInfo display in displays) if (hides(display)) return null;
            List<KeyValuePair<string, Bitmap>> shots = new List<KeyValuePair<string, Bitmap>>();
            try
            {
                foreach (DisplayInfo display in displays)
                {
                    Bitmap shot = grab(display.Bounds);
                    if (shot == null) return null;
                    shots.Add(new KeyValuePair<string, Bitmap>(Monitors.Label(display), shot));
                    if (black(shot)) return null;
                    every = every.IsEmpty ? display.Bounds : Rectangle.Union(every, display.Bounds);
                }
                return Stack(shots);
            }
            finally
            {
                foreach (KeyValuePair<string, Bitmap> shot in shots) shot.Value.Dispose();
            }
        }

        internal static Bitmap Stack(List<KeyValuePair<string, Bitmap>> shots)
        {
            if (shots == null || shots.Count < 2) return null;
            List<Size> sizes = new List<Size>();
            int height = 0;
            foreach (KeyValuePair<string, Bitmap> shot in shots)
            {
                Size size = Panel(shot.Value.Size, PanelWidth);
                sizes.Add(size);
                height += LabelHeight + size.Height;
            }
            Bitmap all = new Bitmap(PanelWidth, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (Graphics paint = Graphics.FromImage(all))
            using (Font font = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                paint.Clear(Color.Black);
                paint.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                paint.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                int top = 0;
                for (int i = 0; i < shots.Count; i++)
                {
                    paint.DrawString(shots[i].Key, font, Brushes.White, 10, top + 6);
                    top += LabelHeight;
                    paint.DrawImage(shots[i].Value, new Rectangle(0, top, sizes[i].Width, sizes[i].Height));
                    top += sizes[i].Height;
                }
            }
            return all;
        }
    }
}
