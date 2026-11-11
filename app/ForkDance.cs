using System;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const double DanceWaitLog = 10;
        private const double DanceQuiet = 1.5;
        private const double DanceStartLog = 600;
        private double danceWaitFrom = -1;
        private bool danceWaitLogged;
        private double danceHeardAt = -100;
        private double danceStartAt = -1000;
        private bool danceWas;

        private string DanceWhy()
        {
            double now = clock.Elapsed.TotalSeconds;
            return "clip " + (clip == null ? "none" : clip.Name) + ", asked " + danceAsked + ", hidden " + hidden + ", dragging " + dragging + ", pressed " + pressed + ", riding " + riding + ", falling " + falling + ", hanging " + hanging + ", walking " + walking + ", outro " + (outro != null) + ", busy " + DanceBusy(now) + ", speech " + (speech != null) + ", talk " + talkState + ", working " + (working != null) + ", chatting " + chatting + ", shushed " + shushed + ", napAsked " + napAsked + ", napping " + napping + ", asleep " + Asleep + ", ownNap " + ownNap + ", ownRest " + ownRest + ", noDance " + preferences.NoDance + ", motion " + preferences.Motion + ", musicBusy " + musicBusy + ", music " + musicOn;
        }

        private bool DanceBusy(double now)
        {
            if (speech != null) return true;
            if (working != null) return now < voiceUntil;
            if (talkState != TalkState.Off || now < voiceUntil) return true;
            if (!chatting) return false;
            if (!local) return true;
            if (utterance != null || transcribing || speaker.Busy || loudRun > 0) danceHeardAt = now;
            return now - danceHeardAt < DanceQuiet;
        }

        private void DanceWait(double now, bool waiting, bool busy)
        {
            bool on = clip == dance || clip == perchDance;
            if (on && !danceWas && !testing && now - danceStartAt >= DanceStartLog)
            {
                danceStartAt = now;
                ActivityLog.Write("dance", danceAsked ? "asked" : "music", "started" + (chatting ? ", voice chat on" : ""));
            }
            danceWas = on;
            if (!waiting) { danceWaitFrom = -1; danceWaitLogged = false; return; }
            if (danceWaitFrom < 0) danceWaitFrom = now;
            if (danceWaitLogged || testing || now - danceWaitFrom < DanceWaitLog) return;
            danceWaitLogged = true;
            ActivityLog.Write("dance", danceAsked ? "asked" : "music", "waiting, " + DanceBlocker(busy));
        }

        private string DanceBlocker(bool busy)
        {
            if (hidden) return "hidden";
            if (dragging || pressed) return "held by the mouse";
            if (riding) return "riding the cursor";
            if (falling) return "falling";
            if (hanging) return "hanging";
            if (walking) return "walking";
            if (outro != null) return "leaving";
            if (napAsked || napping) return "napping";
            if (busy) return speech != null ? "speaking" : talkState != TalkState.Off ? "bubble " + talkState : "voice chat busy";
            return "another animation";
        }
    }
}
