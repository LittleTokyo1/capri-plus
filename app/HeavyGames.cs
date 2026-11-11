using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class HeavyGames
    {
        internal const int Strikes = 2;
        internal const int WindowMinutes = 10;
        private static readonly Regex NotGames = new Regex(@"^(capri|explorer|chrome|msedge|firefox|opera|opera_gx|brave|code|devenv|windowsterminal|powershell|pwsh|cmd|discord|spotify|vlc|obs64|steam|steamwebhelper|epicgameslauncher|leagueclient|leagueclientux|riotclientux|riotclientservices|vortex|obsidian|notepad|notepad\+\+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly object gate = new object();
        private static string strikeName;
        private static int strikes;
        private static DateTime firstAt;

        internal static Func<string> Front = FrontFullScreen;

        internal static bool Starved(string message)
        {
            string text = message ?? "";
            return text.StartsWith("Her cloned voice didn't answer", StringComparison.Ordinal) || text.Contains("out of GPU memory");
        }

        internal static string Learn(string front, string[] known, DateTime now)
        {
            if (string.IsNullOrEmpty(front) || NotGames.IsMatch(front)) return null;
            foreach (string name in known ?? new string[0]) if (string.Equals(name, front, StringComparison.OrdinalIgnoreCase)) return null;
            lock (gate)
            {
                if (!string.Equals(front, strikeName, StringComparison.OrdinalIgnoreCase) || (now - firstAt).TotalMinutes > WindowMinutes)
                {
                    strikeName = front;
                    strikes = 1;
                    firstAt = now;
                    return null;
                }
                if (++strikes < Strikes) return null;
                strikeName = null;
                strikes = 0;
                return front;
            }
        }

        internal static void Forget()
        {
            lock (gate)
            {
                strikeName = null;
                strikes = 0;
            }
        }

        internal static string[] With(string[] known, string name)
        {
            string[] grown = new string[(known == null ? 0 : known.Length) + 1];
            if (known != null) Array.Copy(known, grown, known.Length);
            grown[grown.Length - 1] = name;
            return grown;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Box { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct ScreenInfo { public int Size; public Box Monitor, Work; public uint Flags; }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Box box);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref ScreenInfo info);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        private static string FrontFullScreen()
        {
            try
            {
                IntPtr window = GetForegroundWindow();
                Box box;
                if (window == IntPtr.Zero || !GetWindowRect(window, out box)) return null;
                ScreenInfo info = new ScreenInfo { Size = Marshal.SizeOf(typeof(ScreenInfo)) };
                if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref info)) return null;
                if (box.Left > info.Monitor.Left || box.Top > info.Monitor.Top || box.Right < info.Monitor.Right || box.Bottom < info.Monitor.Bottom) return null;
                uint id;
                GetWindowThreadProcessId(window, out id);
                if (id == 0) return null;
                using (Process process = Process.GetProcessById((int)id)) return process.ProcessName;
            }
            catch (Exception) { return null; }
        }
    }
}
