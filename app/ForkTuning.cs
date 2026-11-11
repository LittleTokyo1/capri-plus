using System;

namespace CapriDesktop
{
    // CAPRI-064: her setting tool's hold on the Mainframe's settings, and what follows any change she makes.
    internal sealed partial class Companion
    {
        private const int TuneWait = 5000;

        private void TuningOn()
        {
            Tuning.Ui = TuneOnUi;
            Tuning.Changed = Tuned;
            Tuning.Bind("voice volume", delegate { return Volume; }, SetVolume);
            Tuning.Bind("sound effects", delegate { return SoundLevel; }, SetSoundLevel);
            Tuning.Bind("voice pitch", delegate { return VoicePitch; }, SetVoicePitch);
            Tuning.Bind("voice speed", delegate { return VoiceSpeed; }, SetVoiceSpeed);
            Tuning.Bind("voice robot", delegate { return VoiceRobot; }, SetVoiceRobot);
            Tuning.Bind("voice glitch", delegate { return VoiceGlitch; }, SetVoiceGlitch);
            Tuning.Bind("her size", delegate { return CharacterSize; }, SetCharacterSize);
            Tuning.Bind("wandering", delegate { return WanderLevel; }, SetWanderLevel);
            Tuning.Bind("pop-ins", delegate { return PopPace; }, SetPopPace);
            Tuning.Bind("bubble time", delegate { return BubbleTime; }, SetBubbleTime);
            Tuning.Bind("reply length", delegate { return Wordiness; }, SetWordiness);
            Tuning.Bind("talkativeness", delegate { return Personality.Talk(ForkSettings.Current.Talk, Brain.Wordiness); }, delegate(int value) { ForkSettings.Current.Talk = value; });
            Stepped("falls asleep after", delegate { return SleepAfter; }, SetSleepAfter);
            Stepped("glance gap", delegate { return GlanceGap; }, SetGlanceGap);
            Stepped("chat memory", delegate { return Recall; }, SetRecall);
            Stepped("perch fps", delegate { return PerchFps; }, SetPerchFps);
            Switched("dance to music", delegate { return Dancing; }, ToggleDance);
            Switched("glitch in at startup", delegate { return Intro; }, ToggleIntro);
            Switched("typing sound", delegate { return TypingSound; }, ToggleTyping);
            Switched("mini player", delegate { return PlayerOn; }, TogglePlayer);
            Switched("always on top", delegate { return OnTop; }, ToggleOnTop);
            Switched("watch my screen", delegate { return Watching; }, ToggleWatch);
            Switched("keep her caught up", delegate { return CaughtUp; }, ToggleCaughtUp);
            Switched("show on discord", delegate { return OnDiscord; }, ToggleDiscord);
            Switched("start with windows", delegate { return StartsWithWindows; }, ToggleStartup);
            Switched("use her cursor", delegate { return UsesCursor; }, ToggleCursor);
            Switched("free voice in games", delegate { return HdGameRest; }, ToggleHdGameRest);
            Switched("remember our chats", delegate { return Remembers; }, ToggleRemembering);
            Tuning.Bind("security preset", delegate { return Security.Preset; }, delegate(int value) { if (value != Security.Custom) Security.Pick(value); });
            Switched("blackwall mode", delegate { return Computer.Bypass; }, ToggleBypass);
            Switched("hey capri", delegate { return HeyCapriOn; }, delegate { SetHeyCapri(!HeyCapriOn); });
        }

        private static void Stepped(string key, Func<int> get, Action<int> set)
        {
            Tunable one = Tuning.Named(key);
            if (one == null || one.Steps == null) return;
            int[] steps = one.Steps;
            Tuning.Bind(key, delegate
            {
                int now = get(), near = 0;
                for (int i = 0; i < steps.Length; i++) if (Math.Abs(steps[i] - now) < Math.Abs(steps[near] - now)) near = i;
                return near;
            }, delegate(int index) { set(steps[Math.Max(0, Math.Min(steps.Length - 1, index))]); });
        }

        private static void Switched(string key, Func<bool> get, Action flip)
        {
            Tuning.Bind(key, delegate { return get() ? 1 : 0; }, delegate(int value) { if (get() != (value != 0)) flip(); });
        }

        private bool TuneOnUi(Action act)
        {
            if (IsDisposed) return false;
            if (!InvokeRequired)
            {
                act();
                return true;
            }
            IAsyncResult done = BeginInvoke(act);
            if (!done.AsyncWaitHandle.WaitOne(TuneWait))
            {
                ActivityLog.Write("setting", "waited", "the window thread was busy; the change goes through when it is free");
                return false;
            }
            EndInvoke(done);
            return true;
        }

        private void Tuned(Tunable one)
        {
            ForkSettings settings = ForkSettings.Current;
            if (one.Field == "Talk") SetWordiness(Personality.Band(settings.Talk));
            if (one.Field == "pref:Wordiness") settings.Talk = Personality.Center(Brain.Wordiness);
            if (preferences.CheckIns != (settings.Proactive == 2)) ToggleCheckIns();
            ApplyForkSettings();
            if (one.Field == "VoiceEngine") ForkVoiceFromSettings(true, false);
            mainframe.Sync();
            ForkFormVoice();
            ChipSay((one.Key + "  " + Tuning.Now(one)).ToUpperInvariant(), Neon.Cyan);
        }
    }
}
