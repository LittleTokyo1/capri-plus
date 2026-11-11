using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal const int FlyPauseMilliseconds = 1300;

        internal static bool NewWindow(ref IntPtr last, IntPtr window)
        {
            if (window == IntPtr.Zero || window == last) return false;
            last = window;
            return true;
        }

        internal static Ledge Perch(List<Ledge> spots, IntPtr window)
        {
            if (spots == null || window == IntPtr.Zero) return null;
            foreach (Ledge ledge in spots) if (ledge.Window == window && !ledge.Floor) return ledge;
            return null;
        }

        internal static int LandX(Ledge ledge, int at, int margin)
        {
            if (ledge.Right - ledge.Left < 2 * margin) return (ledge.Left + ledge.Right) / 2;
            return Math.Max(ledge.Left + margin, Math.Min(ledge.Right - margin, at));
        }

        private bool FlyWanted()
        {
            return ForkSettings.Current.FlyToWindows && preferences.Motion && !hidden && !Lockout.Active;
        }

        private void FlyBeside(IntPtr window)
        {
            if (!FlyWanted() || riding || onPanel || dragging || hanging) return;
            Ledge perch = Perch(Spots(), window);
            if (perch == null) return;
            int x = LandX(perch, anchor.X, preferences.Size / 4);
            ActivityLog.Write("fly", "beside the window of a job", null);
            GlitchOut(delegate { Hop(perch, x); }, false);
        }
    }
}
