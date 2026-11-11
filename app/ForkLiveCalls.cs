using System;

namespace CapriDesktop
{
    // Capri+ in a live League match: the CS per minute strip, "what's my CS" answered from the game, and objective calls.
    internal sealed partial class Companion
    {
        internal const double FreshLive = 10;
        internal const double HudGrace = 7;
        private readonly Objectives objectives = new Objectives();
        private CsHud hud;
        private volatile LiveData lastLive;
        private DateTime lastLiveAt = DateTime.MinValue;
        internal string ObjectiveCalled;

        // On the League worker thread, every poll of a running match.
        private void HudTurn(LiveData data)
        {
            lastLive = data;
            lastLiveAt = DateTime.Now;
            double pace = LiveGame.FarmPerMinute(LiveGame.Mine(data), data);
            if (!testing) Post(delegate { ShowHud(pace); });
        }

        private void ShowHud(double pace)
        {
            if (!ForkSettings.Current.CsHud || Lockout.LeaguePhase != "InProgress") { StopHud(); return; }
            if (hud == null || hud.IsDisposed) hud = new CsHud();
            hud.Show(pace);
        }

        private void StopHud()
        {
            if (hud != null && !hud.IsDisposed) hud.Stop();
        }

        private void HudOff()
        {
            lastLive = null;
            if (!testing) Post(StopHud);
        }

        // One missed read of the live data (it times out now and then) keeps the strip up; only a few seconds of nothing hide it.
        internal bool HudMissed()
        {
            if ((DateTime.Now - lastLiveAt).TotalSeconds < HudGrace) return false;
            HudOff();
            return true;
        }

        // "What's my CS?" mid-game: the game's own number, no model in between. Voice chat through ElevenLabs answers itself.
        private bool CsHeard(string text, bool voiceChat)
        {
            if (voiceChat || !LiveGame.CsAsked.IsMatch(text ?? "")) return false;
            LiveData data = lastLive;
            if (data == null || (DateTime.Now - lastLiveAt).TotalSeconds > FreshLive) return false;
            string said = LiveGame.CsAnswer(data);
            if (said == null) return false;
            ActivityLog.Write("cs per minute", text, said);
            Tell("(cs per minute)", said);
            return true;
        }

        // A fixed line a minute before dragon, Baron or Voidgrubs spawn; no model call, and it holds back her game reactions a moment.
        private void ObjectiveTurn(LiveData data)
        {
            ForkSettings settings = ForkSettings.Current;
            if (!settings.ObjectiveCalls || settings.Paused) return;
            string call = objectives.Turn(data);
            if (call == null) return;
            ObjectiveCalled = call;
            lastReaction = DateTime.Now;
            ActivityLog.Write("objective call", call, null);
            if (!testing) Post(delegate { GameLine(call); });
        }
    }
}
