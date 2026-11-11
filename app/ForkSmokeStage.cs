using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        // On the stage he is playing on the other monitor: his real game and music must not steer the test.
        private void StageQuiet()
        {
            if (!Stage.Away) return;
            Native.FakedGaming = false;
            FullScreen = delegate { return false; };
            Lockout.Running = delegate { return false; };
            MusicPlaying = delegate { return false; };
        }

        // A real full-screen game of his can't steer the test (upstream's HD check now puts the fake game state back instead of clearing it).
        private void SmokeStillQuiet(List<string> checks)
        {
            if (Native.Gaming() || FullScreen() || Lockout.Active) throw new Exception("A real game on his screen steers the smoke test now (full screen " + Native.Gaming() + "/" + FullScreen() + ", lockout " + Lockout.Active + ").");
            checks.Add("PASS smoke while he plays: her full-screen game checks and the game lockout still read the smoke's own no-game state, never his real game");
        }
    }
}