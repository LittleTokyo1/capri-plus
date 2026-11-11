using System;
using System.IO;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class ClipChain
    {
        internal static readonly string[] LengthNames = { "15 seconds", "30 seconds", "45 seconds", "1 minute", "90 seconds", "2 minutes" };
        internal static readonly string[] RateNames = { "15 fps", "24 fps", "30 fps", "60 fps" };
        internal static readonly string[] SizeNames = { "The game's own (up to 1080p)", "480p", "720p", "1080p" };
        internal static readonly string[] BitrateNames = { "Auto", "2 Mbps", "4 Mbps", "8 Mbps", "12 Mbps", "20 Mbps" };
        internal static Func<ClipSettings> Current = delegate { return new ClipSettings(); };
        internal static Action<int, int, int, int> Choose = delegate { };
        internal static Func<string, string> ChooseFolder = delegate { return "Capri isn't running."; };

        internal static string FolderProblem(string path)
        {
            try
            {
                ClipSettings chosen = new ClipSettings(path);
                if (!Directory.Exists(chosen.Folder)) return "Pick an existing folder for your clips.";
                ClipSettings.CheckFolder(chosen.Folder);
                return null;
            }
            catch (Exception) { return "Couldn't save clips in that folder. Pick a folder you can write to."; }
        }

        internal static ClipSettings Merge(ClipSettings opened, ClipSettings now, int seconds, int fps, int resolution, int mbps)
        {
            return new ClipSettings(now.Folder, seconds != opened.Seconds ? seconds : now.Seconds, fps != opened.Fps ? fps : now.Fps, resolution != opened.Resolution ? resolution : now.Resolution, mbps != opened.Mbps ? mbps : now.Mbps);
        }

        internal static bool NewFolder(ClipSettings opened, string typed)
        {
            string folder = (typed ?? "").Trim();
            return folder.Length > 0 && !string.Equals(folder.TrimEnd('\\', '/'), opened.Folder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        internal static int At(int[] choices, int value) { return Math.Max(0, Array.IndexOf(choices, value)); }

        internal static int Of(int[] choices, int index) { return choices[Math.Max(0, Math.Min(choices.Length - 1, index))]; }
    }

    internal sealed partial class Companion
    {
        private void ClipChainOn()
        {
            ClipChain.Current = delegate { return ClipOptions; };
            ClipChain.Choose = SetClipOptions;
            ClipChain.ChooseFolder = SetClipFolder;
            Stepped("clip length", delegate { return ClipOptions.Seconds; }, delegate(int value) { SetClipOptions(value, ClipOptions.Fps, ClipOptions.Resolution, ClipOptions.Mbps); });
            Stepped("clip frame rate", delegate { return ClipOptions.Fps; }, delegate(int value) { SetClipOptions(ClipOptions.Seconds, value, ClipOptions.Resolution, ClipOptions.Mbps); });
            Stepped("clip resolution", delegate { return ClipOptions.Resolution; }, delegate(int value) { SetClipOptions(ClipOptions.Seconds, ClipOptions.Fps, value, ClipOptions.Mbps); });
            Stepped("clip bitrate", delegate { return ClipOptions.Mbps; }, delegate(int value) { SetClipOptions(ClipOptions.Seconds, ClipOptions.Fps, ClipOptions.Resolution, value); });
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private KitPick clipLength, clipRate, clipSize, clipBits;
        private TextBox clipFolder;
        private ClipSettings clipOpened;

        private void ClipChainPage()
        {
            ClipSettings now = ClipChain.Current();
            clipOpened = now;
            Section("CLIPS");
            clipLength = Pick("Clip length: what \"clip that\" saves when I don't say how long", ClipChain.LengthNames, ClipChain.At(ClipSettings.Lengths, now.Seconds));
            clipRate = Pick("Clip frame rate", ClipChain.RateNames, ClipChain.At(ClipSettings.Rates, now.Fps));
            clipSize = Pick("Clip resolution", ClipChain.SizeNames, ClipChain.At(ClipSettings.Sizes, now.Resolution));
            clipBits = Pick("Clip bitrate: Auto picks one from the size", ClipChain.BitrateNames, ClipChain.At(ClipSettings.Bitrates, now.Mbps));
            clipFolder = Field("Clips folder: an existing folder she can write to", now.Folder, false);
        }

        private void FillClipChain()
        {
            ClipSettings now = ClipChain.Current();
            ClipSettings chosen = ClipChain.Merge(clipOpened, now, ClipChain.Of(ClipSettings.Lengths, clipLength.SelectedIndex), ClipChain.Of(ClipSettings.Rates, clipRate.SelectedIndex), ClipChain.Of(ClipSettings.Sizes, clipSize.SelectedIndex), ClipChain.Of(ClipSettings.Bitrates, clipBits.SelectedIndex));
            if (!chosen.Same(now)) ClipChain.Choose(chosen.Seconds, chosen.Fps, chosen.Resolution, chosen.Mbps);
            if (!ClipChain.NewFolder(clipOpened, clipFolder.Text)) return;
            string folder = clipFolder.Text.Trim();
            string problem = ClipChain.ChooseFolder(folder);
            ActivityLog.Write("clips folder", folder, problem ?? "saved");
        }
    }
}
