using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class OwnClock
    {
        internal static bool On()
        {
            ForkSettings current = ForkSettings.Current;
            return current == null || current.OwnClock;
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox ownClock;

        private void OwnClockPage(ForkSettings current)
        {
            Section("HER OWN CLOCK");
            ownClock = Check("Her own clock: \"what time is it\" and \"what's the date\" are read from this PC's clock, never a Google search or a guess (a time in another city still searches)", current.OwnClock);
        }

        private void FillOwnClock()
        {
            settings.OwnClock = ownClock.Checked;
        }
    }
}
