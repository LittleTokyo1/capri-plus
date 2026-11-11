using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const string ProbeVariable = "CAPRI_PROBE";

        private void SmokeProbe(List<string> checks)
        {
            string file = Environment.GetEnvironmentVariable(ProbeVariable);
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;
            DateTime now = new DateTime(2026, 10, 30, 12, 0, 0);
            List<string> report = new List<string>();
            int count = 0;
            foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string phrase = raw.Trim();
                if (phrase.Length == 0 || phrase.StartsWith("#", StringComparison.Ordinal)) continue;
                count++;
                string tool;
                ToolArguments args;
                bool heard = Hands.Heard(phrase, out tool, out args);
                ScheduleItem item = Scheduler.Parse(phrase, now);
                List<string> found = new List<string>();
                string tweak = TweakFor(phrase);
                if (tweak != null) found.Add("setting=" + tweak);
                if (heard) found.Add("tool=" + tool + " " + Describe(args));
                if (item != null) found.Add("reminder=" + Scheduler.Describe(item, now));
                if (Scheduler.Listing(phrase)) found.Add("list reminders");
                if (Scheduler.Clearing(phrase)) found.Add("clear reminders");
                if (MicReport.Asked(phrase)) found.Add("mic question");
                report.Add(phrase + "  =>  " + (found.Count == 0 ? "(her brain decides)" : string.Join(" | ", found.ToArray())));
            }
            File.WriteAllLines(file + ".out", report.ToArray(), new UTF8Encoding(false));
            checks.Add("PASS probe: " + count + " phrases classified offline into " + file + ".out");
        }

        private void SmokeShots(List<string> checks)
        {
            string folder = Environment.GetEnvironmentVariable("CAPRI_SHOTS");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            using (ForkSettingsForm form = new ForkSettingsForm(new ForkSettings()))
            {
                form.Show();
                int count = 0;
                foreach (string tab in form.TabNames)
                {
                    form.SelectTab(tab);
                    form.Refresh();
                    using (System.Drawing.Bitmap picture = new System.Drawing.Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(picture, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                        picture.Save(Path.Combine(folder, "settings-" + (++count) + "-" + tab.Replace(' ', '-') + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                form.Hide();
            }
            checks.Add("PASS shots: the Capri+ settings tabs were drawn to " + folder);
        }

        private static string Describe(ToolArguments args)
        {
            if (args == null) return "";
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(args.Action)) parts.Add("action=" + args.Action);
            if (!string.IsNullOrEmpty(args.Name)) parts.Add("name=" + args.Name);
            if (!string.IsNullOrEmpty(args.Site)) parts.Add("site=" + args.Site);
            if (!string.IsNullOrEmpty(args.Search)) parts.Add("search=" + args.Search);
            if (!string.IsNullOrEmpty(args.Song)) parts.Add("song=" + args.Song);
            if (!string.IsNullOrEmpty(args.Task)) parts.Add("task=" + args.Task);
            if (!string.IsNullOrEmpty(args.Text)) parts.Add("text=" + args.Text);
            if (!string.IsNullOrEmpty(args.Focus)) parts.Add("focus=" + args.Focus);
            if (!string.IsNullOrEmpty(args.Monitor)) parts.Add("monitor=" + args.Monitor);
            return string.Join(", ", parts.ToArray());
        }
    }
}
