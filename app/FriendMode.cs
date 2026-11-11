using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    // Friends mode: her cloned voice out, the listener in. On by hand, or by itself in a Discord call with friends while her mic is muted.
    internal static class FriendMode
    {
        internal const int Off = 0;
        internal const int On = 1;
        internal const int Auto = 2;
        internal const int OnSeconds = 60;
        internal const int OffSeconds = 120;
        internal static readonly string[] Names = { "Off", "On", "Automatic: on in a Discord call with friends while her mic is muted" };
        private static bool active;
        private static bool lastCondition;
        private static DateTime since = DateTime.MinValue;

        internal static bool Active { get { return active; } }

        internal static string Step(int setting, bool inCall, bool muted, bool game, DateTime now)
        {
            bool want;
            if (setting == On) want = true;
            else if (setting != Auto) want = false;
            else
            {
                if (game) return null;
                bool condition = active ? inCall : inCall && muted;
                if (condition != lastCondition)
                {
                    lastCondition = condition;
                    since = now;
                }
                double held = (now - since).TotalSeconds;
                want = condition ? active || held >= OnSeconds : active && held < OffSeconds;
            }
            if (want == active) return null;
            active = want;
            lastCondition = want;
            since = now;
            if (setting == On) return "switched on";
            if (setting != Auto) return "switched off";
            return want ? "in a Discord call with friends and her mic muted" : "the Discord call with friends ended";
        }

        internal static void Reset()
        {
            active = false;
            lastCondition = false;
            since = DateTime.MinValue;
        }
    }

    internal sealed partial class Companion
    {
        private const int FriendCheckSeconds = 5;
        private DateTime friendCheckAt = DateTime.MinValue;

        private void FriendModeTick()
        {
            DateTime now = DateTime.Now;
            if ((now - friendCheckAt).TotalSeconds < FriendCheckSeconds) return;
            friendCheckAt = now;
            ForkSettings settings = ForkSettings.Current;
            List<string> call = settings.FriendsMode == FriendMode.Auto ? DiscordPresence.InCallWithHim(settings.DiscordOwner) : null;
            string why = FriendMode.Step(settings.CallMode ? FriendMode.Off : settings.FriendsMode, call != null && call.Count > 0, ears.Muted, Lockout.Active, now);
            if (why != null) ActivityLog.Write("friends mode", FriendMode.Active ? "on" : "off", why + (FriendMode.Active ? ": her cloned voice unloads, the listener starts" : ": her voice and listening go back to your settings"));
        }
    }
}
