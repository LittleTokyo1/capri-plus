using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class ClipThat
    {
        internal static bool On()
        {
            ForkSettings current = ForkSettings.Current;
            return current == null || current.ClipThat;
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox clipThat;

        private void ClipThatPage(ForkSettings current)
        {
            Section("CLIP THAT");
            clipThat = Check("Clip that on the spot: \"clip that\" or \"scroll that BD\" saves the clip at once with no brain asked, even when her game check missed the game for a moment (\"save that\" alone still needs a game in front); Clip my games must be on", current.ClipThat);
        }

        private void FillClipThat()
        {
            settings.ClipThat = clipThat.Checked;
        }
    }
}
