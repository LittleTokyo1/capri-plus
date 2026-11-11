using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeClipChain(List<string> checks)
        {
            ClipSettings old = new ClipSettings(null, 0, 0, 0, 0);
            if (old.Seconds != 30 || old.Fps != 30 || old.Resolution != 0 || old.Mbps != 0 || old.Folder != ClipBuffer.Folder) throw new Exception("Settings saved before the clip chain didn't load as 30 seconds, 30 fps, the game's size and auto bitrate in her usual folder.");
            ClipSettings odd = new ClipSettings(null, 31, 61, 1000, 7);
            if (odd.Seconds != 30 || odd.Fps != 30 || odd.Resolution != 0 || odd.Mbps != 0) throw new Exception("A clip setting outside its choices was kept.");
            ClipSettings picked = new ClipSettings(null, 60, 60, 720, 8);
            if (picked.Fit(2560, 1440) != new Size(1280, 720) || old.Fit(2560, 1440) != new Size(1920, 1080) || old.Fit(98, 66) != new Size(98, 66)) throw new Exception("Clips weren't sized to the resolution he picked.");
            if (picked.Bits(1280, 720) != 8000000 || old.Bits(98, 66) != 250000) throw new Exception("Clips didn't use the bitrate he picked.");
            foreach (string field in new[] { "ClipFolder", "ClipSeconds", "ClipFps", "ClipResolution", "ClipMbps" })
            {
                System.Reflection.FieldInfo found = typeof(Preferences).GetField(field);
                if (found == null || found.GetCustomAttributes(typeof(System.Runtime.Serialization.DataMemberAttribute), false).Length == 0) throw new Exception("His clip choice isn't saved: " + field);
            }
            using (ClipBuffer buffer = new ClipBuffer())
            {
                buffer.Configure(picked);
                if (!buffer.Settings.Same(picked)) throw new Exception("The clip buffer didn't take his clip settings.");
            }
            string close;
            string[][] voiced = { new[] { "clip length", "pref:ClipSeconds" }, new[] { "clip frame rate", "pref:ClipFps" }, new[] { "clip resolution", "pref:ClipResolution" }, new[] { "clip bitrate", "pref:ClipMbps" } };
            foreach (string[] pair in voiced)
            {
                Tunable found = Tuning.Find(pair[0], "set", out close);
                if (found == null || found.Field != pair[1] || found.Steps == null) throw new Exception("Her setting tool can't find " + pair[0] + " by voice.");
            }
            Tunable clipThat = Tuning.Find("clip that", "off", out close);
            if (clipThat == null || clipThat.Field != "ClipThat") throw new Exception("The clip settings took the clip that switch's words.");
            if (Hands.ClipSeconds("clip that") != -1 || Hands.ClipSeconds("Capri scroll that BD") != -1) throw new Exception("Clip that didn't leave the length to his default.");
            if (Hands.ClipSeconds("clip the last 2 minutes") != 120) throw new Exception("A length he said was lost.");
            if (Hands.Tools.IndexOf("Omit seconds for his default length", StringComparison.Ordinal) < 0) throw new Exception("Her clip tool still says clips are always 30 seconds.");
            if (ClipChain.FolderProblem(Path.Combine(Path.GetTempPath(), "capri-no-such-folder-" + Guid.NewGuid().ToString("N"))) == null) throw new Exception("A clips folder that doesn't exist was taken.");
            if (ClipChain.FolderProblem(Path.GetTempPath()) != null) throw new Exception("A folder she can write to was refused for clips.");
            ClipSettings opened = new ClipSettings(null, 30, 30, 0, 0), changedByVoice = new ClipSettings(null, 60, 30, 0, 0);
            ClipSettings stale = ClipChain.Merge(opened, changedByVoice, 30, 60, 0, 0);
            if (stale.Seconds != 60 || stale.Fps != 60) throw new Exception("A Capri+ window opened before a voice change wrote the old clip length back.");
            if (ClipChain.NewFolder(opened, opened.Folder + "\\") || ClipChain.NewFolder(opened, " ") || !ClipChain.NewFolder(opened, Path.GetTempPath())) throw new Exception("An untouched clips folder box counted as a new folder.");
            if (ClipChain.LengthNames.Length != ClipSettings.Lengths.Length || ClipChain.RateNames.Length != ClipSettings.Rates.Length || ClipChain.SizeNames.Length != ClipSettings.Sizes.Length || ClipChain.BitrateNames.Length != ClipSettings.Bitrates.Length) throw new Exception("The Capri+ clip pickers don't match the clip choices.");
            checks.Add("PASS clip chain (096-15, slice 2): his clip length, frame rate, resolution and bitrate are saved, old settings load as before, the clip buffer takes them, she finds and changes them by voice without taking clip that's words, clip that with no length uses his default, a clips folder must exist and be writable, Capri+ shows the same choices, and a Capri+ window opened before a voice change never writes the old choice back");
        }
    }
}
