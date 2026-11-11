using System;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static Func<bool> InFriendsCall = delegate { return DiscordPresence.HeInCall(ForkSettings.Current.DiscordOwner); };

        private bool NameNeeded { get { return preferences.ListenCapri || InFriendsCall(); } }

        private bool CallNotForHer(string text, bool forHer)
        {
            if (forHer || preferences.ListenCapri) return forHer;
            ActivityLog.Write("voice", "in a call, not for her", Pursuit.Shorter(text ?? "", 60));
            return false;
        }
    }
}
