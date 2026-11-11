using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace CapriDesktop
{
    internal static class HandsMeter
    {
        private const int MostMisses = 3;
        private static readonly object gate = new object();
        private static readonly Stopwatch clock = new Stopwatch();
        private static int turns, actions, byName, byCursor, lists, shots, framedShots;
        private static long thinking, sent, reading;
        private static readonly Dictionary<string, int> misses = new Dictionary<string, int>();

        internal static void Start()
        {
            lock (gate)
            {
                turns = actions = byName = byCursor = lists = shots = framedShots = 0;
                thinking = sent = reading = 0;
                misses.Clear();
                clock.Reset();
                clock.Start();
            }
            ByName.Forget();
            Listing.Forget();
        }

        internal static Func<string, string, string, ClaudeReply> Timed(Func<string, string, string, ClaudeReply> drive)
        {
            if (drive == null) return null;
            return delegate(string system, string tools, string messages)
            {
                Stopwatch took = Stopwatch.StartNew();
                ClaudeReply reply = drive(system, tools, messages);
                int calls = 0, looks = 0;
                if (reply != null && reply.Content != null) foreach (ClaudeBlock block in reply.Content) if (block.Type == "tool_use") { calls++; if (block.Name == "screenshot") looks++; }
                Turn(took.ElapsedMilliseconds, (messages ?? "").Length, calls);
                if (looks > 0) lock (gate) shots += looks;
                return reply;
            };
        }

        internal static Func<string, string, List<KeyValuePair<string, ToolArguments>>, string> Thought(Func<string, string, List<KeyValuePair<string, ToolArguments>>, string> think)
        {
            if (think == null) return null;
            return delegate(string system, string user, List<KeyValuePair<string, ToolArguments>> calls)
            {
                Stopwatch took = Stopwatch.StartNew();
                string written = think(system, user, calls);
                Turn(took.ElapsedMilliseconds, (system ?? "").Length + (user ?? "").Length, Math.Max(calls == null ? 0 : calls.Count, 1));
                return written;
            };
        }

        internal static void Turn(long milliseconds, long size, int calls)
        {
            lock (gate)
            {
                turns++;
                actions += calls;
                thinking += milliseconds;
                sent += size;
            }
        }

        internal static void Listed()
        {
            lock (gate) lists++;
        }

        internal static void Shot(bool framed)
        {
            if (framed) lock (gate) framedShots++;
        }

        internal static void Read(long milliseconds)
        {
            lock (gate) reading += milliseconds;
        }

        internal static void Pressed(string how)
        {
            lock (gate) byName++;
        }

        internal static void Cursor(string why)
        {
            lock (gate)
            {
                byCursor++;
                string reason = string.IsNullOrEmpty(why) ? "no reason" : why;
                int seen;
                misses.TryGetValue(reason, out seen);
                misses[reason] = seen + 1;
            }
        }

        internal static string Line()
        {
            lock (gate) return Line(clock.ElapsedMilliseconds, turns, actions, thinking, sent, lists, byName, byCursor, misses) + ", screenshot calls " + shots + ", framed screenshots " + framedShots + ", list reads " + Seconds(reading) + " s";
        }

        internal static string Line(long total, int turnCount, int actionCount, long model, long bytes, int listed, int named, int cursor, IDictionary<string, int> why)
        {
            StringBuilder line = new StringBuilder();
            line.Append(Seconds(total)).Append(" s, ").Append(turnCount).Append(turnCount == 1 ? " turn, " : " turns, ").Append(actionCount).Append(actionCount == 1 ? " action" : " actions");
            line.Append(", model ").Append(Seconds(model)).Append(" s, sent ").Append(Size(bytes)).Append(", lists ").Append(listed);
            line.Append(", by name ").Append(named).Append(", by cursor ").Append(cursor);
            if (why != null && why.Count > 0)
            {
                List<KeyValuePair<string, int>> sorted = new List<KeyValuePair<string, int>>(why);
                sorted.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });
                List<string> parts = new List<string>();
                for (int i = 0; i < sorted.Count && i < MostMisses; i++) parts.Add(sorted[i].Key + (sorted[i].Value > 1 ? " x" + sorted[i].Value : ""));
                line.Append(" (").Append(string.Join(", ", parts.ToArray())).Append(")");
            }
            return line.ToString();
        }

        internal static void Finish(string outcome)
        {
            string line = Line();
            lock (gate) clock.Stop();
            ActivityLog.Write("hands", "job " + (outcome ?? "failed"), line);
        }

        private static string Seconds(long milliseconds)
        {
            return (milliseconds / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Size(long bytes)
        {
            if (bytes >= 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
            return (bytes / 1024) + " KB";
        }
    }

    internal static class Listing
    {
        internal const int MinControls = 6;
        internal const int MostControls = 40;
        internal const int MostUnseen = 2;
        internal const int MostWords = 15;
        internal const int MostText = 600;
        internal const string Opening = "No new picture this time: these are the window's controls now, with coordinates in the latest picture. Call screenshot when you need to see it.";

        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);

        internal static Func<IntPtr, Sight> Read = delegate(IntPtr window)
        {
            if (window == IntPtr.Zero || !IsWindow(window)) return null;
            Sight front = Computer.Glimpse();
            if (front == null || front.Window != window) return null;
            return Computer.See(new OpenWindow(window, front.Title, front.Program));
        };

        private static readonly HashSet<IntPtr> thin = new HashSet<IntPtr>();

        internal static void Forget()
        {
            lock (thin) thin.Clear();
        }

        internal static string Instead(IntPtr looked, Picture shown, int unseen)
        {
            if (!ForkSettings.Current.HandsListOnly || shown == null || looked == IntPtr.Zero || unseen >= MostUnseen) return null;
            if (Computer.Front() != looked) return null;
            lock (thin) if (thin.Contains(looked)) return null;
            Stopwatch took = Stopwatch.StartNew();
            Sight sight = Read(looked);
            HandsMeter.Read(took.ElapsedMilliseconds);
            if (sight == null || sight.Window != looked || Computer.GameWindow(sight)) return null;
            string text = sight.Problem == null ? Text(sight, shown) : null;
            if (text == null) lock (thin) thin.Add(looked);
            return text;
        }

        internal static string Text(Sight sight, Picture shown)
        {
            List<string> lines = new List<string>();
            foreach (Target target in sight.Targets)
            {
                if (string.IsNullOrEmpty(target.Name) && !target.Line) continue;
                if (target.Box.IsEmpty || !shown.Area.Contains(target.Middle)) continue;
                Point at = shown.FromScreen(target.Middle);
                string value = target.Line && !target.Secret && !string.IsNullOrEmpty(target.Value) ? ", holds " + Voice.Quote(target.Value.Length > 40 ? target.Value.Substring(0, 37) + "..." : target.Value) : "";
                lines.Add(target.Role + " " + Voice.Quote(target.Name ?? "") + (target.Focused ? ", focused" : "") + value + " at (" + at.X + ", " + at.Y + ")");
                if (lines.Count >= MostControls) break;
            }
            if (lines.Count < MinControls) return null;
            StringBuilder text = new StringBuilder();
            text.Append("\n").Append(Opening).Append("\n").Append(string.Join("\n", lines.ToArray()));
            List<string> words = new List<string>();
            int length = 0;
            foreach (string word in sight.Words)
            {
                if (string.IsNullOrWhiteSpace(word) || words.Count >= MostWords || length + word.Length > MostText) break;
                words.Add(word.Trim());
                length += word.Length;
            }
            if (words.Count > 0) text.Append("\nText on it: ").Append(string.Join(" | ", words.ToArray()));
            return text.ToString();
        }
    }

    internal static class Framing
    {
        internal const int MinSide = 200;
        internal const double MostOfScreen = 0.9;

        internal static Func<IntPtr, List<Rectangle>> Popups = OwnPopups;

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Box box);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int process);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);

        private delegate bool EnumProc(IntPtr window, IntPtr data);

        private struct Box { public int Left, Top, Right, Bottom; }

        private static readonly string[] Shell = { "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW" };

        internal static Rectangle? Frame(IntPtr before, out string title)
        {
            title = "";
            if (!ForkSettings.Current.HandsWindowOnly) return null;
            IntPtr front = Computer.Front();
            if (front == IntPtr.Zero || front != before) return null;
            Sight sight = Computer.Glimpse();
            if (sight == null || sight.Window != front || sight.Bounds.IsEmpty) return null;
            title = sight.Title ?? "";
            Rectangle screen = System.Windows.Forms.Screen.FromRectangle(sight.Bounds).Bounds;
            return Frame(sight.Bounds, screen, Popups(front));
        }

        internal static Rectangle? Frame(Rectangle window, Rectangle screen, IList<Rectangle> popups)
        {
            Rectangle area = Rectangle.Intersect(window, screen);
            if (area.Width < MinSide || area.Height < MinSide) return null;
            if ((double)area.Width * area.Height >= MostOfScreen * screen.Width * screen.Height) return null;
            if (popups != null) foreach (Rectangle popup in popups) if (!popup.IsEmpty && !area.Contains(popup)) return null;
            return area;
        }

        internal static string Note(Rectangle? before, Rectangle? now, Size size, string title)
        {
            if (before == now) return null;
            string shown = size.Width + "x" + size.Height;
            if (now == null) return "The picture shows the whole monitor again, " + shown + ". Coordinates mean this picture.";
            return "The picture now shows only the " + Voice.Quote(string.IsNullOrEmpty(title) ? "app" : title) + " window, " + shown + ". Coordinates mean this picture. A screenshot call on its own now shows the whole monitor.";
        }

        internal static bool Whole(ClaudeBlock[] batch, int index, int unseen)
        {
            if (batch != null) for (int i = 0; i < index && i < batch.Length; i++) if (Acts(batch[i])) return false;
            return unseen == 0;
        }

        private static bool Acts(ClaudeBlock call)
        {
            if (call == null) return false;
            if (call.Toolset != "computer") return call.Name != "switch_monitor";
            return call.Name != "screenshot" && call.Name != "zoom";
        }

        internal static string Text(string note)
        {
            return string.IsNullOrEmpty(note) ? "" : ",{\"type\":\"text\",\"text\":" + Voice.Quote(note) + "}";
        }

        private static List<Rectangle> OwnPopups(IntPtr front)
        {
            List<Rectangle> found = new List<Rectangle>();
            int owner;
            GetWindowThreadProcessId(front, out owner);
            if (owner == 0) return found;
            EnumWindows(delegate(IntPtr window, IntPtr data)
            {
                if (window == front || !IsWindowVisible(window) || IsIconic(window)) return true;
                int process;
                GetWindowThreadProcessId(window, out process);
                if (process != owner) return true;
                StringBuilder name = new StringBuilder(64);
                GetClassName(window, name, name.Capacity);
                if (Array.IndexOf(Shell, name.ToString()) >= 0) return true;
                bool popup = (GetWindowLong(window, -16) & unchecked((int)0x80000000)) != 0;
                if (!popup && GetWindow(window, 4) != front) return true;
                Box box;
                if (!GetWindowRect(window, out box) || box.Right <= box.Left || box.Bottom <= box.Top) return true;
                found.Add(Rectangle.FromLTRB(box.Left, box.Top, box.Right, box.Bottom));
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }
}
