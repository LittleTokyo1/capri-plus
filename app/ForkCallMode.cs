using System;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private readonly CallTalkOver callOver = new CallTalkOver();

        private void CallHeard(string who, string text)
        {
            if (!ForkSettings.Current.CallMode || string.IsNullOrWhiteSpace(text)) return;
            DateTime now = DateTime.Now;
            if (who != ListenBook.Friend)
            {
                CallFriends.Heard(who, text, now);
                if (CallFriends.Pending(now) != null) Post(delegate { CallAskHeard(text, false); });
                return;
            }
            if (CallFriends.Deaf) return;
            CallReply reply;
            try { reply = CallFriends.Answer(text, brain.Remote, now); }
            catch (Exception error) { Brain.Log("call mode", -1, Voice.Short(error.Message)); return; }
            if (reply == null) return;
            string line = CallFriends.Line(reply);
            ActivityLog.Write("call mode", "friend: " + text, reply.Request == null ? line : "asks him first: " + reply.Request);
            Post(delegate { if (ForkSettings.Current.CallMode) Tell("(a friend on the call) " + text, line); });
        }

        private void CallOverHeard(short[] samples)
        {
            if (!ForkSettings.Current.CallMode) return;
            CallLevel(samples);
            if (CallFriends.Deaf) return;
            if (!callOver.Feed(samples, clock.Elapsed.TotalSeconds < voiceUntil)) return;
            Post(delegate
            {
                if (clock.Elapsed.TotalSeconds >= voiceUntil) return;
                voice.Stop();
                voiceUntil = 0;
                ActivityLog.Write("call mode", "a friend talked over her", "she stopped");
            });
        }

        private bool CallAskHeard(string text, bool voiceChat)
        {
            string run;
            string said = CallFriends.Answered(text, DateTime.Now, out run);
            if (said == null) return false;
            ActivityLog.Write("call mode", "his answer: " + text, run == null ? "dropped" : "runs as his own: " + run);
            if (run == null)
            {
                if (voiceChat && agent != null) agent.Tell("Capri just did this in code, tell him in your own words: " + said);
                else Tell(text, said);
                return true;
            }
            Post(delegate { Hear(run, run, "> call ask: "); });
            return true;
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox callMode;

        private void CallModePage(ForkSettings current)
        {
            Section("CALL MODE  (she talks with my friends in my Discord call)");
            callMode = Check("Call mode: she hears my friends through Discord's own sound and answers them out loud; anything they ask her to do on my PC waits for my yes", current.CallMode);
            Note("Friends hear her when Her voice in my mic is on and CABLE Output is my mic in Discord. Her listener downloads once (like listening mode). Talking over her stops her. A friend's ask she can't do alone: she asks me, I say yes or no; after 10 minutes it is gone.");
        }

        private void FillCallMode()
        {
            settings.CallMode = callMode.Checked;
        }
    }
}
