using System;
using System.Runtime.InteropServices;

namespace CapriDesktop
{
    internal static class ScreenPower
    {
        private const int SystemCommand = 0x0112;
        private const int MonitorPower = 0xF170;
        private const int MonitorOn = -1;
        private const uint DisplayRequired = 0x00000002;
        private const uint MouseMove = 0x0001;
        private static readonly IntPtr Everyone = new IntPtr(0xFFFF);
        private static readonly object gate = new object();
        private static DateTime nudged = DateTime.MinValue;

        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
        [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);

        internal static Func<bool> Wake = delegate
        {
            bool posted = PostMessage(Everyone, SystemCommand, new IntPtr(MonitorPower), new IntPtr(MonitorOn));
            SetThreadExecutionState(DisplayRequired);
            mouse_event(MouseMove, 1, 0, 0, UIntPtr.Zero);
            mouse_event(MouseMove, -1, 0, 0, UIntPtr.Zero);
            return posted;
        };

        internal static DateTime NudgedAt { get { lock (gate) return nudged; } }

        internal static bool On()
        {
            lock (gate) nudged = DateTime.UtcNow;
            Func<bool> wake = Wake;
            bool done = wake != null && wake();
            ActivityLog.Write("power", "screens on", done ? "monitor-on message and a one-pixel mouse nudge sent" : "monitor-on message failed");
            return done;
        }

        internal static string Said(bool done)
        {
            return done ? "Sent the wake signal to your screens. If one stays dark, give the mouse a nudge." : "Couldn't send the wake signal to your screens.";
        }
    }
}
