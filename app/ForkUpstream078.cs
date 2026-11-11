namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void Picks078(Preferences picked)
        {
            picked.NoGaming = true;
            picked.CareWater = picked.CareStretch = picked.CareMeal = picked.CareLate = picked.CareBored = true;
            picked.CareGame = false;
            picked.FollowUps = true;
            picked.ClipGames = true;
            picked.ClipMoments = false;
        }

        private void ForkUpstream078()
        {
            if (ForkSettings.Current.Upstream078) return;
            Picks078(preferences);
            clips.Enable(preferences.ClipGames);
            SavePreferences();
            ForkSettings.Current.Upstream078 = true;
            ForkSettings.Save();
            ActivityLog.Write("settings", "upstream 0.7.8 picks applied", "gaming mode off, care nudges water stretch meals late bored, no game-break nudge, two-part replies, game clips on, auto clips off");
        }
    }
}
