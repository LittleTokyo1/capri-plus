using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CapriDesktop
{
    // With CAPRI_STAGE=2 the smoke test plays on the second monitor, so he can keep playing on the first.
    internal static class Stage
    {
        internal const string Variable = "CAPRI_STAGE";
        internal static string Asked = Environment.GetEnvironmentVariable(Variable);
        private const int WatchMilliseconds = 100;
        private static readonly List<string> touched = new List<string>();
        private static System.Threading.Timer watch;
        private static EachWindow visit;
        private static string log;

        private delegate bool EachWindow(IntPtr window, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] private struct Box { internal int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool EnumWindows(EachWindow each, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Box box);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr window, out int process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int InternalGetWindowText(IntPtr window, StringBuilder text, int most);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int most);
        [StructLayout(LayoutKind.Sequential)] private struct InputSeen { internal uint Size; internal uint Time; }
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref InputSeen seen);
        [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
        [DllImport("powrprof.dll")] private static extern uint PowerReadACValueIndex(IntPtr root, IntPtr scheme, ref Guid group, ref Guid setting, out uint value);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        private static Guid videoGroup = new Guid("7516b95f-f776-4464-8c53-06167f40cc99");
        private static Guid videoOff = new Guid("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");

        internal static bool ScreenBlanked()
        {
            try
            {
                InputSeen seen = new InputSeen();
                seen.Size = (uint)Marshal.SizeOf(typeof(InputSeen));
                if (!GetLastInputInfo(ref seen)) return false;
                uint idle = unchecked((uint)Environment.TickCount - seen.Time) / 1000;
                IntPtr scheme;
                if (PowerGetActiveScheme(IntPtr.Zero, out scheme) != 0) return false;
                uint off;
                uint read;
                try { read = PowerReadACValueIndex(IntPtr.Zero, scheme, ref videoGroup, ref videoOff, out off); }
                finally { LocalFree(scheme); }
                return read == 0 && off > 0 && idle >= off;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, int flags);
        private static readonly string[] Panels = { "Cloud voice", "Custom local voice", "Voice layers", "Security" };

        internal static bool Away { get { return Other() != null; } }

        internal static Screen Main { get { return Other() ?? Screen.PrimaryScreen; } }

        private static Screen Other()
        {
            if (Asked != "2") return null;
            foreach (Screen screen in Screen.AllScreens) if (!screen.Primary) return screen;
            return null;
        }

        internal static Point Pointer()
        {
            if (!Away) return Cursor.Position;
            Rectangle work = Main.WorkingArea;
            return new Point(work.Left + work.Width / 2, work.Top + work.Height / 2);
        }

        internal static bool Fit(ref int left, ref int right, int y)
        {
            if (!Away) return true;
            Rectangle stage = Main.Bounds;
            if (y < stage.Top || y > stage.Bottom) return false;
            left = Math.Max(left, stage.Left);
            right = Math.Min(right, stage.Right);
            return right > left;
        }

        internal static void Centre(Form form)
        {
            if (!Away) return;
            form.StartPosition = FormStartPosition.Manual;
            Place(form);
            form.Load += delegate { Place(form); };
        }

        private static void Place(Form form)
        {
            Rectangle work = Main.WorkingArea;
            form.Location = new Point(work.Left + Math.Max(0, (work.Width - form.Width) / 2), work.Top + Math.Max(0, (work.Height - form.Height) / 2));
        }

        internal static void Watch(string folder)
        {
            if (!Away || watch != null) return;
            log = Path.Combine(folder, "stage-watch.txt");
            visit = Visit;
            watch = new System.Threading.Timer(delegate { EnumWindows(visit, IntPtr.Zero); }, null, 0, WatchMilliseconds);
        }

        private static bool Visit(IntPtr window, IntPtr data)
        {
            int owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != Process.GetCurrentProcess().Id || !IsWindowVisible(window)) return true;
            Box box;
            if (!GetWindowRect(window, out box)) return true;
            Rectangle area = Rectangle.FromLTRB(box.Left, box.Top, box.Right, box.Bottom);
            if (area.Width <= 1 || area.Height <= 1 || !area.IntersectsWith(Screen.PrimaryScreen.Bounds)) return true;
            string name = Name(window);
            if (Moved(window, name, area)) return true;
            Note(name, area);
            return true;
        }

        // Upstream's voice panels open over the Mainframe, which sits hidden at 0,0 in the test: on the stage they go to its monitor instead.
        private static bool Moved(IntPtr window, string name, Rectangle area)
        {
            foreach (string panel in Panels)
            {
                if (!name.StartsWith(panel + " [", StringComparison.Ordinal)) continue;
                Shift(window, area);
                return true;
            }
            if (name.StartsWith("(no title) [", StringComparison.Ordinal)) Shift(window, area);
            return false;
        }

        private static void Shift(IntPtr window, Rectangle area)
        {
            Rectangle work = Main.WorkingArea;
            SetWindowPos(window, IntPtr.Zero, work.Left + Math.Max(0, (work.Width - area.Width) / 2), work.Top + Math.Max(0, (work.Height - area.Height) / 2), 0, 0, 0x0001 | 0x0004 | 0x0010);
        }

        private static string Name(IntPtr window)
        {
            StringBuilder title = new StringBuilder(256), kind = new StringBuilder(256);
            GetClassName(window, kind, kind.Capacity);
            InternalGetWindowText(window, title, title.Capacity);
            string form = "";
            try { Control known = Control.FromHandle(window); if (known != null) form = " " + known.GetType().Name; }
            catch (Exception) { }
            return (title.Length > 0 ? title.ToString() : "(no title)") + " [" + kind + "]" + form;
        }

        private static void Note(string name, Rectangle area)
        {
            lock (touched)
            {
                if (touched.Contains(name)) return;
                touched.Add(name);
                try { File.AppendAllText(log, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + name + " at " + area + Environment.NewLine); }
                catch (IOException) { }
            }
        }

        internal static string Report()
        {
            if (!Away) return "LIMIT stage: the smoke test ran on the primary monitor (CAPRI_STAGE was not 2 or there is one monitor)";
            lock (touched)
                return touched.Count == 0
                    ? "PASS stage: with CAPRI_STAGE=2 no test window touched the primary monitor"
                    : "LIMIT stage: " + touched.Count + " test windows touched the primary monitor (see stage-watch.txt): " + string.Join("; ", touched.ToArray());
        }
    }
}
