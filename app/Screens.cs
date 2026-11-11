using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class DisplayInfo
    {
        internal int Number;
        internal Rectangle Bounds;
        internal Rectangle Work;
        internal bool Primary;
    }

    // Moving app windows between monitors. Monitor numbers are the ones in Windows' display settings.
    internal static class Screens
    {
        [StructLayout(LayoutKind.Sequential)] private struct Box { internal int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Box box);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, int flags, UIntPtr extra);

        internal static List<DisplayInfo> All()
        {
            List<DisplayInfo> found = new List<DisplayInfo>();
            int fallback = 1;
            foreach (Screen screen in Screen.AllScreens)
            {
                Match number = Regex.Match(screen.DeviceName ?? "", @"(\d+)$");
                found.Add(new DisplayInfo { Number = number.Success ? int.Parse(number.Groups[1].Value) : fallback, Bounds = screen.Bounds, Work = screen.WorkingArea, Primary = screen.Primary });
                fallback++;
            }
            found.Sort(delegate(DisplayInfo a, DisplayInfo b) { return a.Number.CompareTo(b.Number); });
            return found;
        }

        private static DisplayInfo Holding(List<DisplayInfo> displays, Point point)
        {
            foreach (DisplayInfo display in displays) if (display.Bounds.Contains(point)) return display;
            return displays.Count > 0 ? displays[0] : null;
        }

        private static readonly Regex AllWords = new Regex(@"\b(?:both|all|every)\b[^.?!]{0,12}\b(?:monitors|screens|displays)\b", RegexOptions.CultureInvariant);
        private static readonly Regex NumberWords = new Regex(@"\b(?:monitor|screen|display)s?\s+(?:number\s+|no\.?\s*)?(\d|one|two|three)\b", RegexOptions.CultureInvariant);
        private static readonly Regex SideWords = new Regex(@"\b(first|1st|second|2nd|third|3rd|other|another|main|primary|left|right)\s+(?:monitor|screen|display)\b|\b(?:monitor|screen|display)\s+(first|second|third|other|main|primary|left|right)\b", RegexOptions.CultureInvariant);

        // The monitor his words name ("second monitor", "monitor 2", "the other screen", "both monitors"), in the form Pick understands, or null when he named none.
        internal static string Wording(string text)
        {
            string said = (text ?? "").ToLowerInvariant();
            if (AllWords.IsMatch(said)) return "all";
            Match number = NumberWords.Match(said);
            if (number.Success) return number.Groups[1].Value;
            Match side = SideWords.Match(said);
            if (!side.Success) return null;
            string word = side.Groups[1].Success ? side.Groups[1].Value : side.Groups[2].Value;
            return word == "1st" ? "first" : word == "2nd" ? "second" : word == "3rd" ? "third" : word == "another" ? "other" : word;
        }

        // The part of the desktop he asked for (one monitor, or all of them), or null to take her usual look.
        internal static string Seen(Rectangle? area, List<DisplayInfo> displays, Point cursor)
        {
            if (displays == null || displays.Count < 2) return "";
            DisplayInfo shown = null;
            foreach (DisplayInfo display in displays)
                if (area.HasValue ? display.Bounds == area.Value : display.Bounds.Contains(cursor)) shown = display;
            if (shown == null) return area.HasValue ? " (That was all " + displays.Count + " of his monitors at once.)" : "";
            return " (That was monitor " + shown.Number + (shown.Primary ? ", his main one" : "") + ". He has " + displays.Count + " monitors; you looked at one of them, so never say only one screen is showing.)";
        }

        internal static Rectangle? Area(string text, List<DisplayInfo> displays, Point cursor)
        {
            string wording = Wording(text);
            if (wording == null || displays.Count == 0) return null;
            if (wording == "all")
            {
                Rectangle everything = displays[0].Bounds;
                foreach (DisplayInfo display in displays) everything = Rectangle.Union(everything, display.Bounds);
                return everything;
            }
            DisplayInfo picked = Pick(displays, wording, new Rectangle(cursor, Size.Empty), cursor);
            return picked == null ? (Rectangle?)null : picked.Bounds;
        }

        // Which monitor they mean: 1, 2, 3, "one", "main", "left", "right", "here" (where the mouse is) or "other" (not the one it's on).
        internal static DisplayInfo Pick(List<DisplayInfo> displays, string wanted, Rectangle window, Point cursor)
        {
            if (displays.Count == 0) return null;
            string want = (wanted ?? "").Trim().ToLowerInvariant();
            want = Regex.Replace(want, @"\b(monitor|screen|display|my|the|number|no)\b\.?", "").Trim();
            string[] words = { "zero", "one", "two", "three", "four", "five", "six" };
            int spoken = Array.IndexOf(words, want);
            if (spoken > 0) want = spoken.ToString();
            if (want == "first") want = "1";
            if (want == "second") want = "2";
            if (want == "third") want = "3";
            int number;
            if (int.TryParse(want, out number))
            {
                foreach (DisplayInfo display in displays) if (display.Number == number) return display;
                return number >= 1 && number <= displays.Count ? displays[number - 1] : null;
            }
            if (want == "main" || want == "primary") return displays.Find(display => display.Primary) ?? displays[0];
            if (want == "left" || want == "right")
            {
                DisplayInfo edge = displays[0];
                foreach (DisplayInfo display in displays) if (want == "left" ? display.Bounds.Left < edge.Bounds.Left : display.Bounds.Left > edge.Bounds.Left) edge = display;
                return edge;
            }
            if (want == "here" || want == "this") return Holding(displays, cursor);
            DisplayInfo on = Holding(displays, new Point(window.Left + window.Width / 2, window.Top + window.Height / 2));
            DisplayInfo other = displays.Find(display => display != on);
            return other ?? on;
        }

        // Same spot and size on the new monitor, shrunk to fit if the new one is smaller.
        internal static Rectangle Place(Rectangle window, Rectangle from, Rectangle to)
        {
            int width = Math.Min(window.Width, to.Width), height = Math.Min(window.Height, to.Height);
            int x = to.Left + (from.Width > 0 ? (int)((long)(window.Left - from.Left) * to.Width / from.Width) : 0);
            int y = to.Top + (from.Height > 0 ? (int)((long)(window.Top - from.Top) * to.Height / from.Height) : 0);
            x = Math.Max(to.Left, Math.Min(to.Right - width, x));
            y = Math.Max(to.Top, Math.Min(to.Bottom - height, y));
            return new Rectangle(x, y, width, height);
        }

        internal static string Move(IntPtr handle, string name, string monitor, bool maximize)
        {
            List<DisplayInfo> displays = All();
            Box box;
            if (!GetWindowRect(handle, out box)) return "Couldn't grab the " + name + " window.";
            Rectangle window = Rectangle.FromLTRB(box.Left, box.Top, box.Right, box.Bottom);
            Point middle = new Point(window.Left + window.Width / 2, window.Top + window.Height / 2);
            DisplayInfo target = string.IsNullOrWhiteSpace(monitor) && maximize ? Holding(displays, middle) : Pick(displays, monitor, window, Stage.Pointer());
            if (target == null) return "There's no monitor " + monitor + ".";
            bool zoomed = maximize || IsZoomed(handle);
            if (IsZoomed(handle) || IsIconic(handle))
            {
                ShowWindow(handle, 9);
                if (GetWindowRect(handle, out box)) window = Rectangle.FromLTRB(box.Left, box.Top, box.Right, box.Bottom);
            }
            DisplayInfo from = Holding(displays, new Point(window.Left + window.Width / 2, window.Top + window.Height / 2));
            Rectangle placed = Place(window, from.Work, target.Work);
            SetWindowPos(handle, IntPtr.Zero, placed.X, placed.Y, placed.Width, placed.Height, 0x0004 | 0x0010);
            if (zoomed) ShowWindow(handle, 3);
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            keybd_event(0x12, 0, 2, UIntPtr.Zero);
            SetForegroundWindow(handle);
            return (maximize && from == target ? "Maximized " + name : "Moved " + name + " to monitor " + target.Number + (zoomed ? ", full size" : "")) + ".";
        }
    }
}
