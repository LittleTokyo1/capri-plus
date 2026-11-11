using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const string MixCableIn = "{0.0.0.00000000}.{stand-in-cable}", MixCableOut = "{0.0.1.00000000}.{stand-in-cable-out}", MixHeadset = "{0.0.1.00000000}.{stand-in-headset}";

        private static short[] MixHum(int rate, double hertz, double level, double seconds)
        {
            short[] hum = new short[(int)(rate * seconds)];
            for (int i = 0; i < hum.Length; i++) hum[i] = (short)Math.Round(level * 32767 * Math.Sin(2 * Math.PI * hertz * i / rate));
            return hum;
        }

        private static void MixWait(Func<bool> done, string problem)
        {
            Stopwatch waited = Stopwatch.StartNew();
            while (!done() && waited.ElapsedMilliseconds < 4500) Thread.Sleep(5);
            if (!done()) throw new Exception(problem);
        }

        private static float MixPeak(StandInDevices pc)
        {
            float[] kept;
            lock (pc.Cables) kept = pc.Cables.Count == 0 ? new float[0] : pc.Cables[pc.Cables.Count - 1].Kept;
            float most = 0;
            foreach (float sample in kept) most = Math.Max(most, Math.Abs(sample));
            return most;
        }

        private static StandInDevices MixPc(bool cable)
        {
            StandInDevices pc = new StandInDevices();
            if (cable) pc.CableThere = new MixDevice(MixCableIn, "CABLE Input (VB-Audio Virtual Cable)", MixCableOut);
            pc.Mics.Add(new MixDevice(MixHeadset, "Headset Microphone (stand-in)"));
            pc.DefaultMic = MixHeadset;
            pc.Level = 0;
            return pc;
        }

        private static float MixVoicePeak(float loudness)
        {
            StandInDevices pc = MixPc(true);
            float was = MicMix.Loudness;
            MicMix.Loudness = loudness;
            MicMix running = new MicMix(pc, null);
            running.Start();
            try
            {
                MixWait(delegate { return running.Mixing && Interlocked.Read(ref running.Written) > 4800; }, "Her mic mix never started mixing.");
                MicMix.Hear(running, MixHum(24000, 1000, 0.3, 0.5), 24000);
                MixWait(delegate { return MixPeak(pc) > 0.02f && running.Waiting == 0; }, "Her voice never reached the cable.");
                Thread.Sleep(60);
                return MixPeak(pc);
            }
            finally
            {
                running.Stop();
                MicMix.Loudness = was;
                if (running.Running || MicMix.Current == running || !pc.Opened[0].Disposed || !pc.Cables[0].Disposed) throw new Exception("Her mic mix didn't let its devices go.");
            }
        }

        internal static void SmokeVoiceInMic(List<string> checks, string folder)
        {
            if (!MicMix.IsCableIn("CABLE Input (VB-Audio Virtual Cable)") || MicMix.IsCableIn("Speakers (Realtek(R) Audio)") || !MicMix.IsCableOut("CABLE Output (VB-Audio Virtual Cable)") || !MicMix.IsCableOut("Microphone (Steam Streaming Microphone)") || MicMix.IsCableOut("Microphone (Yeti Stereo Microphone)"))
                throw new Exception("The virtual cable's ends were told apart wrong.");
            List<MixDevice> mics = new List<MixDevice> { new MixDevice(MixCableOut, "CABLE Output (VB-Audio Virtual Cable)"), new MixDevice(MixHeadset, "Headset Microphone (Arctis 7)") };
            MixDevice heard = MicMix.MicFor(mics, MixCableOut, MixCableOut);
            if (heard == null || heard.Id != MixHeadset) throw new Exception("Her mix would hear the cable's own far end as his mic.");
            float full = MixVoicePeak(1f), half = MixVoicePeak(0.5f);
            if (full < 0.1f || half / full < 0.4f || half / full > 0.6f) throw new Exception("Her loudness in his mic didn't follow the slider: " + full + " at 100%, " + half + " at 50%.");
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.VoiceInMic || fresh.MixLoud != 100 || !string.IsNullOrEmpty(fresh.MixMic) || !string.IsNullOrEmpty(fresh.MixCable)) throw new Exception("Her voice in your mic isn't off by default.");
            IMixDevices devicesWas = MicMix.Devices;
            MicMix currentWas = MicMix.Current;
            Func<string, Process> setupWas = CableSetup.Start;
            try
            {
                StandInDevices none = MixPc(false);
                MicMix.Devices = none;
                fresh.VoiceInMic = true;
                string missing = VoiceInMic.Apply(fresh);
                if (missing != MicMix.Missing || VoiceInMic.Running != null) throw new Exception("With no virtual cable the switch didn't say it needs VB-CABLE: " + missing);
                StandInDevices pc = MixPc(true);
                MicMix.Devices = pc;
                fresh.MixLoud = 150;
                VoiceInMic.Apply(fresh);
                MicMix on = VoiceInMic.Running;
                if (on == null || MicMix.Current != on || Math.Abs(MicMix.Loudness - 1.5f) > 0.001f) throw new Exception("The switch didn't start her mix at the slider's loudness.");
                MixWait(delegate { return on.Mixing; }, "Her mix never opened the stand-in cable.");
                string line = VoiceInMic.Apply(fresh);
                if (VoiceInMic.Running != on || line != MicMix.PickLine(MicMix.CableEnd)) throw new Exception("A second look restarted her mix or lost its line: " + line);
                fresh.MixMic = MixHeadset;
                VoiceInMic.Apply(fresh);
                if (VoiceInMic.Running != on || on.Chosen != MixHeadset) throw new Exception("A new mic pick wasn't handed to the running mix.");
                fresh.VoiceInMic = false;
                string off = VoiceInMic.Apply(fresh);
                if (VoiceInMic.Running != null || MicMix.Current != null || off != VoiceInMic.Off) throw new Exception("Switching it off didn't stop her mix.");
                MixWait(delegate { return pc.Cables.Count > 0 && pc.Cables[0].Disposed; }, "Switching it off didn't let the cable go.");
                string started = null;
                CableSetup.Start = delegate(string setup) { started = setup; return null; };
                string said, unpacked;
                FileStream kept;
                Process none2 = CableSetup.Run(out said, out unpacked, out kept);
                bool held = kept != null && File.Exists(unpacked);
                CableSetup.Tidy(unpacked, kept);
                if (none2 != null || said != CableSetup.Finish || started == null || !held || !Path.GetFileName(started).StartsWith("VBCABLE_Setup", StringComparison.Ordinal) || Directory.Exists(Path.GetDirectoryName(unpacked))) throw new Exception("VB-CABLE's setup wasn't unpacked from her pack, held, handed over and tidied.");
            }
            finally
            {
                VoiceInMic.Stop();
                MicMix.Devices = devicesWas;
                MicMix.Current = currentWas;
                CableSetup.Start = setupWas;
            }
            checks.Add("PASS her voice in your mic (096-02): cable ends told apart, the mix never hears the cable as his mic, her voice reaches the cable on the mix thread and the loudness slider halves it at 50%, off by default, no cable says it needs VB-CABLE, on starts one mix and keeps it, a new mic pick reaches it, off stops it and lets the cable go, VB-CABLE's own setup unpacked from her pack, checked, held and tidied");
        }
    }
}
