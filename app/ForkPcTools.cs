using System;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class ForkSettingsForm
    {
        private CheckBox pcChecks;

        private void PcChecksPage(ForkSettings current)
        {
            Section("PC CHECKS  (tools that only read)");
            pcChecks = Check("PC checks: she can look at how my PC is doing (CPU, memory, busiest apps, drives), test my internet (pings, Wi-Fi or Ethernet, DNS), list my open windows and check if an app is installed or running; all read only, also in a game", current.PcChecks);
        }

        private void FillPcChecks()
        {
            settings.PcChecks = pcChecks.Checked;
        }
    }
}