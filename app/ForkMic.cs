using System;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private DateTime lastWordsAt = DateTime.MinValue;

        private string Spoke(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) lastWordsAt = DateTime.Now;
            return text;
        }

        // "Mute your mic": her own voice chat mic (the Ctrl+Alt+N one), no keys pressed, so it works in a game too.
        private string HerMicSwitch(bool mute)
        {
            if (!chatting) return "Voice chat is off, so I'm not listening anyway.";
            Post(delegate { if (chatting && ears != null && ears.Muted != mute) ToggleMic(); });
            ActivityLog.Write("her mic", mute ? "muted" : "unmuted", "by his words");
            return mute ? "Mic muted. Ctrl+Alt+N or the mic button turns it back on." : "Mic on, I'm listening.";
        }

        private bool HerMicHeard(string text, bool voiceChat)
        {
            string tool;
            ToolArguments args;
            if (voiceChat || !Hands.Heard(text, out tool, out args) || tool != "her_mic") return false;
            Tell("(mic)", Hands.Do(tool, args));
            return true;
        }

        private bool MicHeard(string text, bool voiceChat)
        {
            if (testing || !MicReport.Asked(text)) return false;
            double seconds = lastWordsAt == DateTime.MinValue ? -1 : (DateTime.Now - lastWordsAt).TotalSeconds;
            string said = MicReport.Say(chatting, earsReady, MicNow(), seconds, TalkKeyName, MuteKeyName);
            ActivityLog.Write("setting", "micreport", said);
            if (!voiceChat) Tell("(mic)", said);
            return true;
        }
    }
}
