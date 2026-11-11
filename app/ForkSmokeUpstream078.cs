using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeUpstream078(List<string> checks)
        {
            Preferences fresh = new Preferences();
            Picks078(fresh);
            if (!fresh.NoGaming || !fresh.CareWater || !fresh.CareStretch || !fresh.CareMeal || !fresh.CareLate || !fresh.CareBored || fresh.CareGame || !fresh.FollowUps || !fresh.ClipGames || fresh.ClipMoments)
                throw new Exception("His picks for upstream 0.7.8's new behaviour were not applied.");
            if (new ForkSettings().Upstream078) throw new Exception("The one-time 0.7.8 picks started as already applied.");
            checks.Add("PASS upstream 0.7.8 picks: once, over upstream's defaults, Gaming Mode stays off, the water, stretch, meal, late-night and bored nudges stay on, the game-break nudge goes off, two-part replies stay on, game clips on and automatic clips off");
        }
    }
}