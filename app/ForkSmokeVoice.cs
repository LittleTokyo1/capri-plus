using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    // Capri+ smoke checks for one-brain voice chat: her own brain, her ElevenLabs voice, Scribe ears, and cutting her off.
    internal sealed partial class Companion
    {
        private static void PumpUntil(Func<bool> done, int milliseconds)
        {
            Stopwatch waited = Stopwatch.StartNew();
            while (!done() && waited.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
        }

        private static short[] SmokeTone(int count, double amplitude)
        {
            short[] samples = new short[count];
            for (int i = 0; i < count; i++) samples[i] = (short)(Math.Sin(i * 0.3) * amplitude);
            return samples;
        }

        private void SmokeVoiceBrain(List<string> checks)
        {
            string keptKey = voice.Key, keptId = voice.VoiceId;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<Dictation> keptMake = Dictation.Make;
            Func<string, byte[]> keptSpeak = Speak, keptFree = FreeSpeak;
            Func<string, string, byte[], Tuple<int, string>> keptPost = Scribe.Post;
            Func<short[], string> keptRun = Hearing.Run;
            try
            {
                voice.Key = "sk_test_capri_1234";
                voice.VoiceId = "abc";
                SmokeVoiceSettings(checks);
                SmokeVoiceStart(checks);
                SmokeVoiceSpeech(checks);
                SmokeScribe(checks);
                SmokeBargeIn(checks);
                SmokeOwnLines(checks);
            }
            finally
            {
                voice.Key = keptKey;
                voice.VoiceId = keptId;
                ForkSettings.Current = keptSettings;
                Dictation.Make = keptMake;
                Speak = keptSpeak;
                FreeSpeak = keptFree;
                Scribe.Post = keptPost;
                Hearing.Run = keptRun;
                ShowVoice();
            }
        }

        private void SmokeVoiceSettings(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            if (ForkSettings.Current.VoiceBrain != 0 || !ForkSettings.Current.BargeIn) throw new Exception("Voice chat didn't default to her own brain with barge-in on.");
            ForkSettings wild = new ForkSettings { VoiceBrain = 7 };
            wild.Tidy();
            ForkSettings negative = new ForkSettings { VoiceBrain = -3 };
            negative.Tidy();
            if (wild.VoiceBrain != 1 || negative.VoiceBrain != 0) throw new Exception("The voice brain pick wasn't clamped.");
            ForkSettings picked = new ForkSettings { VoiceBrain = 1, BargeIn = false, ScribeEars = true };
            using (ForkSettingsForm form = new ForkSettingsForm(picked)) form.Apply();
            if (picked.VoiceBrain != 1 || picked.BargeIn || !picked.ScribeEars) throw new Exception("The settings window lost the voice brain, barge-in or Scribe pick.");
            using (ForkSettingsForm form = new ForkSettingsForm(ForkSettings.Current)) form.Apply();
            if (ForkSettings.Current.VoiceBrain != 0 || !ForkSettings.Current.BargeIn || ForkSettings.Current.ScribeEars) throw new Exception("The settings window changed the voice defaults.");
            ForkSettings loudMic = new ForkSettings { MicThreshold = 500 };
            loudMic.Tidy();
            ForkSettings quietMic = new ForkSettings { MicThreshold = 0 };
            quietMic.Tidy();
            ForkSettings.Current.MicThreshold = 50;
            bool moved = WakeLevel == 0.05;
            ForkSettings.Current.MicThreshold = 20;
            if (loudMic.MicThreshold != 100 || quietMic.MicThreshold != 3 || !moved || WakeLevel != 0.02 || new ForkSettings().MicThreshold != 20) throw new Exception("The mic sensitivity setting wasn't clamped or didn't drive the wake level.");
            Voice plain = new Voice();
            Voice chosen = new Voice { Model = "eleven_v3" };
            if (plain.ModelOrDefault != "eleven_v4_turbo" || chosen.ModelOrDefault != "eleven_v3") throw new Exception("Her ElevenLabs model wasn't upstream's v4 turbo by default.");
            checks.Add("PASS voice brain setting: defaults to her own brain, clamps to 0 or 1, shows in the settings window with the barge-in box, and her ElevenLabs model defaults to upstream's eleven_v4_turbo (0.7.8)");
        }

        private void SmokeVoiceStart(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            Dictation.Make = delegate { return new StandInDictation(); };
            ForkSettings.Current.VoiceBrain = 1;
            if (ForkVoiceChat() || chatting) throw new Exception("The ElevenLabs agent pick still started her own voice chat.");
            ForkSettings.Current.VoiceBrain = 0;
            ToggleChat();
            bool started = chatting && local && agent == null && earsReady && !pill.Muted;
            string status = mainframe.VoiceStatus;
            StopChat();
            if (!started || chatting || local) throw new Exception("With an ElevenLabs key and her own brain picked, voice chat didn't take the local path: " + status);
            checks.Add("PASS voice chat with her own brain: ToggleChat starts the local ears-brain-speech path even with an ElevenLabs key set, and the ElevenLabs agent pick leaves upstream's path alone");
        }

        private void SmokeVoiceSpeech(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            byte[] tone = Voice.Wave(SmokeTone(24000, 9000), 24000);
            List<string> eleven = new List<string>(), free = new List<string>();
            Speak = delegate(string words) { lock (eleven) eleven.Add(words); return tone; };
            FreeSpeak = delegate(string words) { lock (free) free.Add(words); return tone; };
            bool wasChatting = chatting, wasLocal = local;
            chatting = true;
            local = true;
            try
            {
                heard = "(voice)";
                Answer(++asking, "Hello choom.", TalkState.Talking, local && !ForkElevenLocal());
                PumpUntil(delegate { return reply != null && voiceUntil > 0; }, 5000);
                if (eleven.Count != 1 || free.Count != 0 || voiceUntil <= 0) throw new Exception("Local voice chat didn't speak with ElevenLabs: " + eleven.Count + " / " + free.Count);
                if (!voiceBeforeHold()) throw new Exception("Hold to talk didn't stop her speech.");
                CloseTalk();

                ForkSettings.Current.VoiceBrain = 1;
                Answer(++asking, "Hello choom.", TalkState.Talking, true);
                PumpUntil(delegate { return reply != null && voiceUntil > 0; }, 5000);
                if (eleven.Count != 1 || free.Count != 1) throw new Exception("The ElevenLabs agent pick changed which voice local chat used: " + eleven.Count + " / " + free.Count);
                CloseTalk();

                ForkSettings.Current.VoiceBrain = 0;
                ForkSettings.Current.VoiceBudget = 10;
                ForkSettings.Current.VoiceChars = 10;
                Answer(++asking, "Hello choom.", TalkState.Talking, local && !ForkElevenLocal());
                PumpUntil(delegate { return reply != null && voiceUntil > 0; }, 5000);
                if (eleven.Count != 1 || free.Count != 2) throw new Exception("A spent voice budget didn't move local chat to the free voice: " + eleven.Count + " / " + free.Count);
            }
            finally
            {
                CloseTalk();
                chatting = wasChatting;
                local = wasLocal;
                talkState = TalkState.Off;
            }
            checks.Add("PASS local voice chat speaks with ElevenLabs when a key and voice are set and the budget isn't spent (free voice when the agent pick, no key or a spent budget), and the hold-to-talk key stops her speech at once");
        }

        // Presses the hold-to-talk key while she's talking: audio, mouth and the voice timer all stop, the words stay.
        private bool voiceBeforeHold()
        {
            bool mouthMoving = speech != null;
            HoldToTalk(true);
            return mouthMoving && voiceUntil == 0 && speech == null && mouths == null && reply == "Hello choom." && !speaker.Busy;
        }

        private void SmokeScribe(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            List<string> keys = new List<string>(), types = new List<string>();
            List<byte[]> bodies = new List<byte[]>();
            Tuple<int, string> answer = Tuple.Create(200, "{\"language_code\":\"eng\",\"text\":\" Hello Capri,  what's up? \",\"words\":[]}");
            Scribe.Post = delegate(string key, string type, byte[] body) { keys.Add(key); types.Add(type); bodies.Add(body); return answer; };
            int localCalls = 0;
            Hearing.Run = delegate { localCalls++; return " local words "; };
            short[] samples = SmokeTone(3200, 8000);
            string offByDefault = ForkHear(samples);
            if (ForkSettings.Current.ScribeEars || ForkScribeOn() || keys.Count != 0 || offByDefault != "local words" || localCalls != 1) throw new Exception("Scribe ran by default, with a key set and no opt-in: " + offByDefault);
            localCalls = 0;
            ForkSettings.Current.ScribeEars = true;
            string text = ForkHear(samples);
            if (text != "Hello Capri, what's up?" || keys.Count != 1 || keys[0] != "sk_test_capri_1234" || localCalls != 0) throw new Exception("Scribe's words weren't used: " + text);
            string boundary = types[0].Substring(types[0].IndexOf("boundary=", StringComparison.Ordinal) + 9);
            string sent = Encoding.GetEncoding(28591).GetString(bodies[0]);
            int wavAt = sent.IndexOf("RIFF", StringComparison.Ordinal);
            if (!types[0].StartsWith("multipart/form-data; boundary=", StringComparison.Ordinal) || sent.IndexOf("name=\"model_id\"\r\n\r\nscribe_v1\r\n", StringComparison.Ordinal) < 0 || sent.IndexOf("name=\"file\"; filename=\"speech.wav\"", StringComparison.Ordinal) < 0 || !sent.EndsWith("\r\n--" + boundary + "--\r\n", StringComparison.Ordinal))
                throw new Exception("Scribe's request was built wrong: " + types[0]);
            int rate = BitConverter.ToInt32(bodies[0], wavAt + 24);
            short channels = BitConverter.ToInt16(bodies[0], wavAt + 22), bits = BitConverter.ToInt16(bodies[0], wavAt + 34);
            int dataSize = BitConverter.ToInt32(bodies[0], wavAt + 40);
            if (wavAt < 0 || sent.Substring(wavAt + 8, 4) != "WAVE" || rate != 16000 || channels != 1 || bits != 16 || dataSize != samples.Length * 2) throw new Exception("Scribe got the wrong audio: " + rate + " Hz, " + channels + " ch, " + bits + " bit, " + dataSize + " bytes");
            if (Scribe.Parse("{\"text\":\"hi\"}") != "hi" || Scribe.Parse("{\"language_code\":\"eng\"}") != "") throw new Exception("Scribe's reply was parsed wrong.");

            answer = Tuple.Create(500, (string)null);
            if (ForkHear(samples) != "local words" || localCalls != 1) throw new Exception("A failed Scribe call didn't fall back to the offline ears.");
            Scribe.Post = delegate { throw new IOException("Scribe test failure."); };
            if (ForkHear(samples) != "local words" || localCalls != 2) throw new Exception("A Scribe exception didn't fall back to the offline ears.");
            Scribe.Post = delegate(string key, string type, byte[] body) { keys.Add(key); return Tuple.Create(200, "{\"text\":\"scribe\"}"); };
            int before = keys.Count;
            ForkSettings.Current.VoiceBrain = 1;
            string agentPick = ForkHear(samples);
            ForkSettings.Current.VoiceBrain = 0;
            voice.Key = null;
            string noKey = ForkHear(samples);
            voice.Key = "sk_test_capri_1234";
            if (keys.Count != before || agentPick != "local words" || noKey != "local words" || ForkScribeOn() == false) throw new Exception("Scribe ran without a key or with the ElevenLabs agent picked.");
            checks.Add("PASS Scribe ears are opt-in: with an ElevenLabs key but ScribeEars off (the default) the offline Parakeet ears hear her and Scribe is never called");
            checks.Add("PASS Scribe ears: with ScribeEars on, an ElevenLabs key with her own brain sends the utterance as a 16 kHz mono 16-bit WAV in a multipart request (model scribe_v1, xi-api-key), uses the text field, and falls back to the offline ears on a failed call, an exception, no key or the agent pick");
        }

        private void SmokeBargeIn(List<string> checks)
        {
            BargeGate gate = new BargeGate();
            short[] quiet = SmokeTone(Ears.Chunk, 100), loud = SmokeTone(Ears.Chunk, 9000), echo = SmokeTone(Ears.Chunk, 2500), mild = SmokeTone(Ears.Chunk, 3600);
            for (int i = 0; i < BargeGate.Settle; i++) if (gate.Hear(quiet)) throw new Exception("Barge-in fired on silence.");
            if (gate.Hear(loud) || gate.Hear(loud) || !gate.Hear(loud)) throw new Exception("Three loud chunks in a row didn't interrupt her.");
            gate.Reset();
            for (int i = 0; i < BargeGate.Settle; i++) gate.Hear(echo);
            for (int i = 0; i < 12; i++) if (gate.Hear(mild)) throw new Exception("Her own echo interrupted her.");
            if (gate.Hear(loud) || gate.Hear(loud) || !gate.Hear(loud)) throw new Exception("Talking well above her echo didn't interrupt her.");

            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            bool wasChatting = chatting, wasLocal = local, wasReady = earsReady;
            chatting = true;
            local = true;
            earsReady = true;
            try
            {
                foreach (bool allowed in new[] { false, true })
                {
                    ForkSettings.Current.BargeIn = allowed;
                    talkState = TalkState.Talking;
                    reply = "Long answer.";
                    voiceUntil = clock.Elapsed.TotalSeconds + 30;
                    for (int i = 0; i < BargeGate.Settle; i++) Gather(quiet);
                    for (int i = 0; i < 3; i++) Gather(loud);
                    PumpUntil(delegate { return !bargePending; }, 3000);
                    bool stopped = voiceUntil == 0;
                    int taken;
                    lock (waking) taken = utterance == null ? 0 : utterance.Count;
                    if (allowed && (!stopped || taken < 3 || earsShut)) throw new Exception("Talking over her didn't stop her and start your line: " + stopped + " / " + taken);
                    if (!allowed && (stopped || taken != 0)) throw new Exception("Barge-in happened with it switched off.");
                    lock (waking) { utterance = null; preroll.Clear(); loudRun = 0; quietRun = 0; }
                    bargeGate.Reset();
                }
                dictation = new StandInDictation();
                ForkSettings.Current.BargeIn = true;
                talkState = TalkState.Talking;
                voiceUntil = clock.Elapsed.TotalSeconds + 30;
                for (int i = 0; i < BargeGate.Settle; i++) Gather(quiet);
                for (int i = 0; i < 3; i++) Gather(loud);
                PumpUntil(delegate { return !bargePending; }, 3000);
                bool dictated = voiceUntil == 0 && dictation != null;
                dictation = null;
                if (!dictated) throw new Exception("Talking over her didn't stop her when Windows speech was the ears.");
            }
            finally
            {
                voiceUntil = 0;
                chatting = wasChatting;
                local = wasLocal;
                earsReady = wasReady;
                talkState = TalkState.Off;
                reply = null;
                lock (waking) { utterance = null; preroll.Clear(); loudRun = 0; quietRun = 0; }
            }
            checks.Add("PASS barge-in: a few loud chunks in a row over her voice (also with Windows speech as the ears; louder than her own echo) stop her speech and seed your next line, nothing happens with the setting off, and silence or her echo never interrupts her");
        }

        private void SmokeOwnLines(List<string> checks)
        {
            brain.Forget();
            try
            {
                brain.Own("champ select research", "Ban Zed, pick Ahri.");
                brain.Own("game:   They (Zed)\njust died.", "  ");
                string[] said = brain.Recent();
                if (said.Length != 2 || said[0] != "(champ select research)" || said[1] != "Ban Zed, pick Ahri.") throw new Exception("Her own line didn't reach her history: " + string.Join(" / ", said));
                brain.Forget();
                brain.Own(new string('x', 400), new string('y', 900));
                said = brain.Recent();
                if (said[0].Length > 165 || said[1].Length > 700) throw new Exception("A long own line wasn't kept short.");
            }
            finally { brain.Forget(); }
            checks.Add("PASS her own lines are remembered: game reactions, League research popups and anything else she says unprompted go into her chat history as a short bracketed prompt plus her line (empty lines skipped, long ones clipped)");
        }
    }
}
