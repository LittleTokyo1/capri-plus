using System;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class HologramSwitch
    {
        internal static Func<bool> On = delegate { return false; };
        internal static Action<bool> Set = delegate { };
    }

    internal sealed partial class Companion
    {
        private void HologramSwitchOn()
        {
            HologramSwitch.On = delegate { return preferences.Hologram; };
            HologramSwitch.Set = delegate(bool on) { if (preferences.Hologram != on) SetHologram(on); };
            Switched("hologram", delegate { return preferences.Hologram; }, delegate { SetHologram(!preferences.Hologram); });
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox hologramBox;
        private bool hologramOpened;

        private void HologramPage()
        {
            hologramOpened = HologramSwitch.On();
            Section("HOLOGRAM");
            hologramBox = Check("Hologram: she shows at 60% with a faint cyan tint and clicks go through her, her talk pill and her bubble; turn it off here, from her tray icon or by saying \"hologram off\"", hologramOpened);
        }

        private void FillHologram()
        {
            if (hologramBox.Checked != hologramOpened) HologramSwitch.Set(hologramBox.Checked);
        }
    }
}
