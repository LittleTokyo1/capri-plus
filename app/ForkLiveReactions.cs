using System;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class LiveReactions
    {
        private static readonly string[] LeagueInGame = { "GameStart", "InProgress", "Reconnect" };
        internal static Func<bool> On = delegate { return false; };
        internal static Action Flip = delegate { };

        internal static bool Held(string game, string leaguePhase, bool answering)
        {
            if (answering) return true;
            if (game != null && game.IndexOf("League", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return leaguePhase != null && Array.IndexOf(LeagueInGame, leaguePhase) >= 0;
        }
    }

    internal sealed partial class Companion
    {
        private bool ForkReactionsHeld()
        {
            return LiveReactions.Held(Lockout.Game, Lockout.LeaguePhase, talkState == TalkState.Thinking || talkState == TalkState.Talking);
        }

        private void LiveReactionsOn()
        {
            LiveReactions.On = delegate { return ReactionsOn; };
            LiveReactions.Flip = ToggleReactions;
            Switched("live reactions", delegate { return ReactionsOn; }, ToggleReactions);
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox liveReactionsBox;
        private bool liveReactionsOpened;

        private void LiveReactionsPage()
        {
            liveReactionsOpened = LiveReactions.On();
            Section("LIVE REACTIONS");
            liveReactionsBox = Check("Live reactions: in games other than League she puts a quick line over the game for kills, deaths, wins and losses (League keeps her own game lines); never over an answer she's giving", liveReactionsOpened);
        }

        private void FillLiveReactions()
        {
            if (liveReactionsBox.Checked != liveReactionsOpened && LiveReactions.On() == liveReactionsOpened) LiveReactions.Flip();
        }
    }
}
