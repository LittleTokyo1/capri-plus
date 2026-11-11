using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class HuskSay
    {
        internal const double DoingGap = 10, ScanGap = 2;
        internal const int MostHelpers = 12;
        private static readonly string[] Coding = { "windowsterminal", "powershell", "pwsh", "cmd", "code", "cursor", "claude", "conhost", "wezterm-gui", "alacritty" };

        internal static bool IsCoding(string process) { return process != null && Array.IndexOf(Coding, process.ToLowerInvariant()) >= 0; }

        internal static string Asking(HuskEvent seen) { return "Claude Code in " + seen.Project + " " + seen.Text + "."; }

        internal static string Answered(HuskEvent seen) { return "Claude Code's done in " + seen.Project + ": " + HuskWatch.FirstLine(seen.Text, 110); }

        internal static string Doing(HuskEvent seen) { return "Claude (" + seen.Project + "): " + seen.Text; }

        internal static int More(int wanted, int already, bool allowed)
        {
            if (!allowed) return 0;
            return Math.Max(0, Math.Min(MostHelpers, wanted) - already);
        }
    }

    internal sealed partial class Companion
    {
        private HuskWatch huskWatch;
        private bool huskScanning, cloneCalm;
        private double huskScanAt, huskDoingAt;
        private int huskClones;
        private string huskHeld;

        private void HuskTick(double now)
        {
            if (!ForkSettings.Current.CodeMode || testing)
            {
                if (huskWatch != null) HuskOff();
                return;
            }
            if (huskScanning || now < huskScanAt) return;
            huskScanAt = now + HuskSay.ScanGap;
            if (huskWatch == null) huskWatch = new HuskWatch(HuskWatch.DefaultRoot());
            HuskWatch watch = huskWatch;
            huskScanning = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<HuskEvent> seen = null;
                try { seen = watch.Scan(DateTime.Now); }
                catch (Exception error) { Brain.Log("code mode", -1, Voice.Short(error.Message)); }
                int helpers = watch.HelperTotal();
                Post(delegate
                {
                    huskScanning = false;
                    if (watch != huskWatch || seen == null) return;
                    HuskShow(seen, helpers);
                });
            });
        }

        private void HuskOff()
        {
            huskWatch = null;
            huskHeld = null;
            if (cloneCalm && cloneWindow != null) ClonesOff();
            cloneCalm = false;
            huskClones = 0;
        }

        private void HuskShow(List<HuskEvent> seen, int helpers)
        {
            bool game = Lockout.Active || Computer.GameOpen() != null;
            double now = clock.Elapsed.TotalSeconds;
            if (!game && huskHeld != null && !LineBusy && !hidden)
            {
                string held = huskHeld;
                huskHeld = null;
                Tell("(Claude Code)", held);
            }
            foreach (HuskEvent one in seen)
            {
                if (ProjectRun.Hers(one.Session)) continue;
                if (one.Kind == HuskKind.Asking)
                {
                    ActivityLog.Write("code mode", "Claude Code in " + one.Project, one.Text);
                    if (game || hidden || LineBusy) huskHeld = HuskSay.Asking(one);
                    else Tell("(Claude Code)", HuskSay.Asking(one));
                }
                else if (one.Kind == HuskKind.Answered)
                {
                    if (game || hidden || HuskSay.IsCoding(FrontProcess())) continue;
                    ActivityLog.Write("code mode", "Claude Code in " + one.Project, "finished");
                    if (!LineBusy) Remark("(Claude Code)", HuskSay.Answered(one));
                }
                else if (one.Kind == HuskKind.Doing)
                {
                    if (game || hidden || LineBusy || speech != null || now < huskDoingAt) continue;
                    huskDoingAt = now + HuskSay.DoingGap;
                    Remark("(Claude Code)", HuskSay.Doing(one), true);
                }
            }
            HuskHelpers(helpers, game);
        }

        private void HuskHelpers(int helpers, bool game)
        {
            bool allowed = !game && CloneRefusal() == null && (cloneWindow == null || cloneCalm);
            if (helpers == 0 || !allowed)
            {
                if (cloneCalm && cloneWindow != null) ClonesOff();
                if (cloneWindow == null) { cloneCalm = false; huskClones = 0; }
                return;
            }
            int more = HuskSay.More(helpers, cloneWindow == null ? 0 : huskClones, allowed);
            if (more <= 0) return;
            if (cloneWindow == null) { cloneCalm = true; huskClones = 0; }
            Clone("spawn", more);
            if (cloneWindow != null) cloneWindow.Swarm.Quiet = cloneCalm;
            huskClones += more;
            ActivityLog.Write("code mode", "Claude Code sent " + helpers + " helper(s)", "a calm clone for each");
        }

        private static string FrontProcess()
        {
            int id;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out id);
            try { using (Process front = Process.GetProcessById(id)) return front.ProcessName; }
            catch (ArgumentException) { return null; }
            catch (InvalidOperationException) { return null; }
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox codeMode;

        private void CodeModePage(ForkSettings current)
        {
            Section("CODE MODE  (she is Claude Code's body while it works)");
            codeMode = Check("Code mode: she reads my Claude Code sessions on this PC (read only, nothing installed) and shows what they're doing, says when one asks me something, tells me when one finishes while I'm looking elsewhere, and sends a calm clone for each of its helpers", current.CodeMode);
            Note("Never in a game: an ask waits until it's over. Only tool names and file names are shown; nothing from the sessions leaves the PC.");
        }

        private void FillCodeMode()
        {
            settings.CodeMode = codeMode.Checked;
        }
    }
}
