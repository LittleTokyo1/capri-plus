using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Mainframe
    {
        private Label charmNote;
        private const int CharmWidth = 162;

        private void ForkCharmRow(int v)
        {
            charmNote = Lower(Caption("", 112, 181 + v, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Rose));
            charmNote.AutoSize = false;
            charmNote.AutoEllipsis = true;
            charmNote.Size = new Size(CharmWidth, charmNote.PreferredHeight + 4);
            charmNote.AccessibleName = "Charm meter";
        }

        internal void ShowCharm(string text)
        {
            charmText = text ?? "";
            if (charmNote == null) return;
            if (moodTurn) return;
            charmNote.Text = charmText;
            charmNote.AccessibleName = "Charm meter: " + charmText;
        }

        internal string CharmShown { get { return charmNote == null ? "" : charmText; } }

        internal bool CharmOverlaps
        {
            get
            {
                if (charmNote == null) return true;
                Rectangle mine = charmNote.Bounds;
                if (mine.Left < 0 || mine.Right > ClientSize.Width) return true;
                foreach (Control control in Controls)
                    if (control != charmNote && control is Key && control.Visible && control.Bounds.IntersectsWith(mine)) return true;
                return false;
            }
        }
    }
}
