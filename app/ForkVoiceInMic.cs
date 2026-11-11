using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class VoiceInMic
    {
        internal const string Off = "Off.";
        internal const int CheckSeconds = 3;
        internal static MicMix Running;
        private static readonly object gate = new object();
        private static string setupLine;
        private static Process setup;
        private static string setupFile;
        private static FileStream setupKept;

        internal static string Apply(ForkSettings settings)
        {
            lock (gate)
            {
                if (settings == null || !settings.VoiceInMic) { Stop(); return Off; }
                MicMix.Loudness = Math.Max(25, Math.Min(200, settings.MixLoud)) / 100f;
                string mic = Pick(settings.MixMic), cable = Pick(settings.MixCable);
                MixDevice there;
                try { there = MicMix.Devices.Cable(cable); }
                catch (Exception) { there = null; }
                if (there == null) { Stop(); return MicMix.Missing; }
                if (Running == null)
                {
                    Running = new MicMix(MicMix.Devices, null);
                    Running.Chosen = mic;
                    Running.CableChosen = cable;
                    Running.Start();
                    ActivityLog.Write("voice in mic", there.Name, "on");
                }
                else
                {
                    if (Running.Chosen != mic) Running.Chosen = mic;
                    if (Running.CableChosen != cable) Running.CableChosen = cable;
                }
                string problem = Running.Problem;
                if (problem != null) return problem;
                return there.Far == null ? MicMix.OffLine(MicMix.EndOf(there)) : MicMix.PickLine(MicMix.EndOf(there));
            }
        }

        internal static void Stop()
        {
            lock (gate)
            {
                if (Running == null) return;
                Running.Stop();
                Running = null;
                ActivityLog.Write("voice in mic", "", "off");
            }
        }

        private static string Pick(string id) { return string.IsNullOrEmpty(id) ? null : id; }

        internal static string Status(ForkSettings settings)
        {
            string line = settings != null && settings.VoiceInMic ? Apply(settings) : Ready() ? MicMix.PickLine(MicMix.CableEnd) : MicMix.Missing;
            string setting = setupLine;
            return setting == null ? line : setting;
        }

        internal static bool Ready()
        {
            try { return MicMix.Devices.Cable(null) != null; }
            catch (Exception) { return false; }
        }

        internal static List<MixDevice> Mics()
        {
            List<MixDevice> found = new List<MixDevice>();
            try { foreach (MixDevice mic in Endpoints.Active(1)) if (!MicMix.IsCableOut(mic.Name)) found.Add(mic); }
            catch (Exception) { }
            return found;
        }

        internal static List<MixDevice> Cables()
        {
            try { return MicMix.Devices.Cables(); }
            catch (Exception) { return new List<MixDevice>(); }
        }

        internal static string SetUp()
        {
            lock (gate)
            {
                if (setup != null) return CableSetup.Running;
                string line, file;
                FileStream kept;
                Process started = CableSetup.Run(out line, out file, out kept);
                setupLine = line;
                if (started == null) { CableSetup.Tidy(file, kept); return line; }
                setup = started;
                setupFile = file;
                setupKept = kept;
                ActivityLog.Write("voice in mic", "VB-CABLE setup", "opened on his click");
                try
                {
                    started.EnableRaisingEvents = true;
                    started.Exited += delegate { SetUpClosed(started); };
                    if (started.HasExited) SetUpClosed(started);
                }
                catch (Exception) { SetUpClosed(started); }
                return line;
            }
        }

        private static void SetUpClosed(Process closed)
        {
            lock (gate)
            {
                if (setup != closed) return;
                CableSetup.Tidy(setupFile, setupKept);
                setup = null;
                setupFile = null;
                setupKept = null;
                setupLine = CableSetup.Finish;
            }
            try { closed.Dispose(); }
            catch (Exception) { }
        }
    }

    internal sealed partial class Companion
    {
        private DateTime voiceInMicAt = DateTime.MinValue;

        private void VoiceInMicTick()
        {
            if (testing) return;
            DateTime now = DateTime.Now;
            if (Math.Abs((now - voiceInMicAt).TotalSeconds) < VoiceInMic.CheckSeconds) return;
            voiceInMicAt = now;
            VoiceInMic.Apply(ForkSettings.Current);
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox voiceInMic;
        private KitPick mixMic, mixCable;
        private KitSlider mixLoud;
        private List<string> mixMicIds = new List<string>(), mixCableIds = new List<string>();

        private void VoiceInMicPage(ForkSettings current)
        {
            Section("HER VOICE IN MY MIC  (friends in Discord or a game hear her and me through one mic)");
            voiceInMic = Check("Her voice in my mic: she plays my real mic plus her voice into a virtual cable; I pick CABLE Output as my mic in Discord", current.VoiceInMic);
            KitLabel state = new KitLabel { Text = VoiceInMic.Status(current), AutoSize = true, ForeColor = Neon.Cyan, Margin = new Padding(0, 4, 0, 4) };
            column.Controls.Add(state);
            List<MixDevice> mics = VoiceInMic.Mics(), cables = VoiceInMic.Cables();
            List<string> micNames = new List<string> { "The mic Windows uses" };
            mixMicIds = new List<string> { "" };
            foreach (MixDevice mic in mics) { micNames.Add(mic.Name); mixMicIds.Add(mic.Id); }
            List<string> cableNames = new List<string> { "VB-CABLE first, else Steam's" };
            mixCableIds = new List<string> { "" };
            foreach (MixDevice cable in cables) { cableNames.Add(MicMix.FarName(cable)); mixCableIds.Add(cable.Id); }
            mixMic = Pick("Input: the mic she mixes with her voice", micNames.ToArray(), Math.Max(0, mixMicIds.IndexOf(current.MixMic ?? "")));
            mixCable = Pick("Output: the cable she plays into, your mic in calls", cableNames.ToArray(), Math.Max(0, mixCableIds.IndexOf(current.MixCable ?? "")));
            mixLoud = Slider("Her loudness in my mic (100% = a steady speaking level)", Math.Max(25, Math.Min(200, current.MixLoud)), 25, 200);
            if (!VoiceInMic.Ready())
            {
                KitKey set = new KitKey { Text = "SET UP VB-CABLE", Loud = true, Accent = Neon.Cyan, Size = new Size(190, 30), Margin = new Padding(0, 4, 0, 4) };
                set.Click += delegate { state.Text = VoiceInMic.SetUp(); };
                column.Controls.Add(set);
            }
            Note(CableSetup.Credit + " " + CableSetup.Page + "  The setup is VB-Audio's own: Windows asks first, then click Install Driver and restart the PC.");
        }

        private void FillVoiceInMic()
        {
            settings.VoiceInMic = voiceInMic.Checked;
            settings.MixMic = mixMic.SelectedIndex >= 0 && mixMic.SelectedIndex < mixMicIds.Count ? mixMicIds[mixMic.SelectedIndex] : "";
            settings.MixCable = mixCable.SelectedIndex >= 0 && mixCable.SelectedIndex < mixCableIds.Count ? mixCableIds[mixCable.SelectedIndex] : "";
            settings.MixLoud = mixLoud.Value;
        }
    }
}
