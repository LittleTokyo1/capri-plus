using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class MicReport
    {
        private const double QuietLevel = 1;
        private static readonly Regex Ask = new Regex(@"^(?:(?:hey|ok|okay) )?(?:capri )?(?:can you hear me(?: now| ok| okay| clearly)?|could you hear me|do you hear me|are you (?:listening|hearing me)(?: to me)?|is my (?:mic|microphone) (?:working|on|live)|is the (?:mic|microphone) (?:working|on)|mic check|(?:test|check) (?:my )?(?:mic|microphone))$", RegexOptions.CultureInvariant);

        internal static bool Asked(string said) { return Ask.IsMatch(Scheduler.Plain(said)); }

        private static string Ago(double seconds)
        {
            if (seconds < 60) return Math.Max(1, (int)seconds) + " seconds ago";
            if (seconds < 3600) { int minutes = (int)(seconds / 60); return minutes + (minutes == 1 ? " minute ago" : " minutes ago"); }
            return "over an hour ago";
        }

        internal static string Say(bool chatting, bool ready, double level, double secondsSinceWords, string talkKey, string muteKey)
        {
            if (!chatting) return "Right now voice chat is off, so I'm not listening. Press " + talkKey + " to talk to me.";
            if (level <= -2) return "Your mic is muted, so I can't hear you. " + muteKey + " unmutes it.";
            if (!ready) return "My ears are still loading, give me a moment.";
            if (level < 0) return "I'm getting no signal from the mic right now. Check that it's plugged in and chosen as the Windows input.";
            string meter = Math.Round(level).ToString(CultureInfo.InvariantCulture) + " out of 60";
            string words = secondsSinceWords < 0 ? "I haven't picked up any words yet" : "I last heard words " + Ago(secondsSinceWords);
            if (level < QuietLevel) return "The mic is very quiet right now (level " + meter + "), and " + words + ".";
            return "Yes, I can hear you. The mic level is " + meter + ", " + (secondsSinceWords < 0 ? "but " : "and ") + words + ".";
        }
    }
}
