using System;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static bool ThrowOn(ForkSettings settings) { return settings != null && settings.ThrowHer; }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox throwHer;

        private void ThrowPage(ForkSettings current)
        {
            Section("THROW HER  (fling her with the mouse)");
            throwHer = Check("Throw her: let go of her while the mouse is still moving fast and she flies by its speed, bounces off the screen edges and the taskbar and lands on a ledge; three throws in two minutes and she sulks until I say sorry, pat her or leave her be", current.ThrowHer);
            Note("Off: she just drops where I let go, like before.");
        }

        private void FillThrow()
        {
            settings.ThrowHer = throwHer.Checked;
        }
    }
}