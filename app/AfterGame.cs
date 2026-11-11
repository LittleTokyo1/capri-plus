using System;

namespace CapriDesktop
{
    internal sealed class GameEnd
    {
        internal const int RealSeconds = 60;
        internal const int GoneSeconds = 15;
        private static readonly string[] StillIn = { "GameStart", "InProgress", "Reconnect" };
        private static readonly string[] Over = { "WaitingForStats", "PreEndOfGame", "EndOfGame" };
        private DateTime playingSince = DateTime.MinValue;
        private DateTime goneSince = DateTime.MinValue;
        private bool armed;

        internal bool Tick(DateTime now, string game, string phase)
        {
            bool over = Array.IndexOf(Over, phase) >= 0;
            bool playing = phase == "InProgress" || (game != null && phase != "ChampSelect" && !over);
            if (playing)
            {
                goneSince = DateTime.MinValue;
                if (playingSince == DateTime.MinValue) playingSince = now;
                if ((now - playingSince).TotalSeconds >= RealSeconds) armed = true;
                return false;
            }
            playingSince = DateTime.MinValue;
            if (!armed) return false;
            if (!over)
            {
                if (Array.IndexOf(StillIn, phase) >= 0) return false;
                if (goneSince == DateTime.MinValue) goneSince = now;
                if ((now - goneSince).TotalSeconds <= GoneSeconds) return false;
            }
            armed = false;
            goneSince = DateTime.MinValue;
            return true;
        }
    }
}
