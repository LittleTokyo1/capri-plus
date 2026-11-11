using System;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private string quitFrom;

        internal static string QuitFrom(string stack)
        {
            string text = stack ?? "";
            if (text.Contains("CapriDesktop.Mainframe")) return "the Mainframe's Quit key";
            if (text.Contains("ToolStripItem") || text.Contains("ToolStripMenuItem")) return "her right-click or tray menu";
            return "her own code";
        }

        // A right-click on her while a game runs is the game's click, not a call for her menu (one landed on Quit Capri at the end of a game).
        internal static bool MenuHeldInGame()
        {
            return Lockout.Active;
        }

        private bool ForkMenuHeld()
        {
            if (testing || !MenuHeldInGame()) return false;
            ActivityLog.Write("menu", "held", "a game is running (" + Lockout.Game + ")");
            return true;
        }

        private bool ForkQuitAsked()
        {
            quitFrom = QuitFrom(Environment.StackTrace);
            if (testing) return true;
            DialogResult answer = MessageBox.Show("Quit Capri?", "Capri", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, MessageBoxOptions.DefaultDesktopOnly);
            ActivityLog.Write("quit", answer == DialogResult.Yes ? "asked, yes" : "asked, no", quitFrom);
            return answer == DialogResult.Yes;
        }

        private void ForkQuitLog(FormClosingEventArgs e)
        {
            if (testing) return;
            ActivityLog.Write("quit", e.CloseReason.ToString(), quitting ? quitFrom ?? "her own code" : "closed from outside Capri");
        }
    }
}
