using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class ForkDiagrams
    {
        internal static bool On()
        {
            ForkSettings current = ForkSettings.Current;
            return current == null || current.Diagrams;
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox diagrams;

        private void DiagramsPage(ForkSettings current)
        {
            Section("DIAGRAMS");
            diagrams = Check("Diagrams: ask for a diagram, flowchart or mind map in any words and she draws it in pink on your screen and talks you through it; hold Esc or say \"remove it\" to wipe it", current.Diagrams);
            ExplainRow(current);
        }

        private void FillDiagrams()
        {
            settings.Diagrams = diagrams.Checked;
            FillExplain();
        }
    }
}
