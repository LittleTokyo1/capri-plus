using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    // Decides when the user talking over her counts as an interruption: a few loud chunks in a row, louder than whatever of her
    // own voice the mic picked up in her first moments (speakers echo; headphones don't).
    internal sealed class BargeGate
    {
        internal const double Floor = 0.06;
        internal const int Settle = 8;
        internal const int Chunks = 3;
        private const int Keep = 6;
        private readonly List<short[]> recent = new List<short[]>();
        private int seen, run;
        private double echo;

        internal void Reset()
        {
            seen = 0;
            run = 0;
            echo = 0;
            recent.Clear();
        }

        internal bool Hear(short[] chunk, double floor = Floor)
        {
            double level = Speaker.Loudness(chunk, 0, chunk.Length);
            seen++;
            recent.Add(chunk);
            if (recent.Count > Keep) recent.RemoveAt(0);
            if (seen <= Settle) { echo = Math.Max(echo, level); return false; }
            run = level >= Math.Max(floor, echo * 2) ? run + 1 : 0;
            return run >= Chunks;
        }

        internal List<short[]> Recent() { return new List<short[]>(recent); }
    }

    // Capri+ voice chat: one brain (hers) for talking, her ElevenLabs voice for speaking, Scribe for hearing, and cutting her off.
    internal sealed partial class Companion
    {
        private readonly BargeGate bargeGate = new BargeGate();
        private List<short[]> bargeSeed;
        private bool bargePending;

        internal static bool StartVoiceMay(ForkSettings settings, bool chatting, bool ready)
        {
            return settings.VoiceOnStart && !settings.Paused && !settings.NightOn && !chatting && ready;
        }

        // With her own brain picked, voice chat is always the local path (ears, her brain, speech), even with an ElevenLabs key.
        private bool ForkVoiceChat()
        {
            if (ForkSettings.Current.VoiceBrain != 0 || !FreeChatOn) return false;
            StartLocal();
            return true;
        }

        // Her lines in local voice chat use the ElevenLabs voice when there is one; Answer still falls back to the free voice
        // when the budget is spent or the credits ran out.
        private bool ForkElevenLocal()
        {
            return local && ForkSettings.Current.VoiceBrain == 0 && voice.Ready && !preferences.Quiet && !preferences.Silent;
        }

        private const int CutoffHoldMs = 3500;
        private readonly Cutoff cutoff = new Cutoff();
        private System.Windows.Forms.Timer cutoffTimer;
        private bool typedLine, releasing;

        // A spoken line that stops mid-sentence ("open up, um,") waits a few seconds for the rest instead of being taken as finished.
        private bool ForkHold(ref string text, ref string shown)
        {
            if (!local || testing || typedLine || releasing) return false;
            string merged = cutoff.Take(text);
            if (merged == null)
            {
                if (cutoffTimer == null)
                {
                    cutoffTimer = new System.Windows.Forms.Timer { Interval = CutoffHoldMs };
                    cutoffTimer.Tick += delegate
                    {
                        cutoffTimer.Stop();
                        string waited = cutoff.Release();
                        if (waited == null || !local) return;
                        releasing = true;
                        try { Hear(waited, waited, "> you: "); }
                        finally { releasing = false; }
                    };
                }
                cutoffTimer.Stop();
                cutoffTimer.Start();
                return true;
            }
            if (cutoffTimer != null) cutoffTimer.Stop();
            if (merged != text) shown = merged;
            text = merged;
            return false;
        }

        private bool ForkScribeOn() { return ForkSettings.Current.VoiceBrain == 0 && ForkSettings.Current.ScribeEars && voice.Key != null; }

        // What they said, from the offline ears, or from ElevenLabs Scribe when that's switched on and there's a key.
        private string ForkHear(short[] samples)
        {
            string heard = ForkHeard(samples);
            if (listenOn && !pushing && ListenTrace.Note(heard)) ActivityLog.Write("listening", "her name", "heard");
            ListenMine(samples, heard);
            return heard;
        }

        private string ForkHeard(short[] samples)
        {
            string key = voice.Key;
            if (key != null && ForkSettings.Current.VoiceBrain == 0 && ForkSettings.Current.ScribeEars)
            {
                try { return Spoke(Hearing.Clean(Scribe.Hear(key, samples))); }
                catch (Exception error) { Brain.Log("speech to text", -1, Voice.Short(error.Message)); }
            }
            return Spoke(Hearing.Text(samples));
        }

        // Cuts her off mid-sentence: the audio stops, the mouth stops and the rest of the words show at once.
        internal bool ForkSilence()
        {
            chatterbox.Abort();
            double now = clock.Elapsed.TotalSeconds;
            if (now >= voiceUntil && !speaker.Busy) return false;
            voice.Stop();
            speaker.Clear();
            Sfx.Untype();
            voiceUntil = 0;
            Say(null);
            if (reply != null && pace > 0) replyStart = Math.Min(replyStart, now - reply.Length / pace - 0.1);
            CheckEars(now);
            return true;
        }

        private double micLevel;
        private int micAt;
        private int micFresh = 1000;

        internal const int MicHoldMs = 350;
        private double micPeak;
        private int micPeakAt;

        private void ForkLevel(double value)
        {
            int tick = Environment.TickCount == 0 ? 1 : Environment.TickCount;
            micLevel = value;
            if (value >= micPeak || tick - micPeakAt > MicHoldMs) { micPeak = value; micPeakAt = tick; }
            micAt = tick;
        }

        private double MicShown()
        {
            return Environment.TickCount - micPeakAt <= MicHoldMs ? Math.Max(micLevel, micPeak) : micLevel;
        }

        internal double MicNow()
        {
            Ears listening = ears;
            if (listening != null && listening.Muted) return -2;
            if (micAt == 0 || Environment.TickCount - micAt > micFresh) return -1;
            return Math.Min(60, MicShown() * 1000);
        }

        // On the mic thread, for every chunk in local voice chat. True when the chunk was taken here.
        private bool ForkBarge(short[] chunk)
        {
            if (bargePending)
            {
                lock (waking) { if (bargeSeed != null) bargeSeed.Add(chunk); }
                return true;
            }
            double now = clock.Elapsed.TotalSeconds;
            TalkState state = talkState;
            bool talking = local && chatting && ForkSettings.Current.BargeIn && earsReady && !ears.Muted && !transcribing && now < voiceUntil && (state == TalkState.Talking || state == TalkState.Online);
            if (!talking) { bargeGate.Reset(); return false; }
            if (!bargeGate.Hear(chunk, WakeLevel * 3)) return false;
            lock (waking) { bargeSeed = bargeGate.Recent(); bargePending = true; }
            bargeGate.Reset();
            Post(ForkBargedIn);
            return true;
        }

        // They started talking over her: she stops, and what they've said so far starts their next line.
        private void ForkBargedIn()
        {
            List<short[]> seed;
            lock (waking) { seed = bargeSeed; bargeSeed = null; bargePending = false; }
            if (!chatting || !local) return;
            ForkSilence();
            CheckEars(clock.Elapsed.TotalSeconds);
            if (seed == null || earsShut || dictation != null) return;
            lock (waking) { utterance = seed; quietRun = 0; loudRun = 0; preroll.Clear(); }
        }

        internal const double VoicedHold = 1.0;

        private double ForkBubbleHold(int length) { return voiceUntil > 0 ? VoicedHold : BubbleHold(length); }
    }
}
