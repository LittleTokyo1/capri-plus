using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CapriDesktop
{
    internal static class AdminKeys
    {
        internal const string Line = "This game runs as admin, so Windows hides your keys from me while it's in front. Start me as admin to use push-to-talk here.";
        internal static Func<IntPtr, bool> AboveHer = Proven;
        private static int selfElevated = -1;

        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr window, out int process);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(int access, bool inherit, int id);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, int access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int kind, out int value, int length, out int written);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        internal static bool Warn(IntPtr front, ref IntPtr last, bool pushSet, Func<IntPtr, bool> above, Func<string> game, HashSet<string> warned)
        {
            if (front == last) return false;
            last = front;
            if (!pushSet || front == IntPtr.Zero || !above(front)) return false;
            string name = game();
            return name != null && warned.Add(name);
        }

        private static bool Proven(IntPtr window)
        {
            if (window == IntPtr.Zero) return false;
            if (selfElevated < 0)
            {
                using (Process own = Process.GetCurrentProcess()) selfElevated = Elevated(own.Id) ? 1 : 0;
            }
            if (selfElevated == 1) return false;
            int id;
            GetWindowThreadProcessId(window, out id);
            return id != 0 && Elevated(id);
        }

        private static bool Elevated(int id)
        {
            IntPtr process = OpenProcess(0x1000, false, id);
            if (process == IntPtr.Zero) return false;
            try
            {
                IntPtr token;
                if (!OpenProcessToken(process, 0x0008, out token)) return false;
                try
                {
                    int elevated, size;
                    return GetTokenInformation(token, 20, out elevated, 4, out size) && elevated != 0;
                }
                finally { CloseHandle(token); }
            }
            finally { CloseHandle(process); }
        }
    }

    internal sealed partial class Companion
    {
        private IntPtr keyFront;
        private readonly HashSet<string> keyWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private void CheckKeyAccess()
        {
            if (!AdminKeys.Warn(Hands.Foreground(), ref keyFront, preferences.PushVk != 0, AdminKeys.AboveHer, GameAhead, keyWarnings)) return;
            mainframe.VoiceNote(AdminKeys.Line, false);
            if (CommentFree() && !LineBusy) Remark(AdminKeys.Line, AdminKeys.Line);
        }
    }
}
