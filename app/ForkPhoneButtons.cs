using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int PhoneKeyWaitMs = 5000;
        internal const string PhoneLate = "Your PC didn't answer in time.";

        private string PhoneKey(string key)
        {
            if (Array.IndexOf(PhoneButtons.Keys, key ?? "") < 0) return null;
            ActivityLog.Write("phone", "key " + key, null);
            if (key == "look") return ForkSettings.Current.Paused ? PhoneView.Paused : PhoneAsk("check my screen", false);
            string said = null;
            ManualResetEvent done = new ManualResetEvent(false);
            Action press = delegate
            {
                try { said = PhonePress(key); }
                finally { done.Set(); }
            };
            if (testing) press(); else Post(press);
            return done.WaitOne(PhoneKeyWaitMs) ? said : PhoneLate;
        }

        private string PhonePress(string key)
        {
            if (key == "stop")
            {
                bool busy = job != null;
                StopJob(clock.Elapsed.TotalSeconds);
                return busy ? "Stopping what I'm doing." : "Nothing of mine is running right now.";
            }
            if (key == "pause")
            {
                if (ForkSettings.Current.Paused) return "I'm already dark.";
                SetPaused(true);
                return "Going dark. Eyes and ears off till you wake me.";
            }
            if (!ForkSettings.Current.Paused) return "I'm already awake.";
            SetPaused(false);
            return "Back online.";
        }
    }
}
