using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class NameGate
    {
        private static readonly Regex Her = new Regex(@"\b(?:capri|kapri|capry|caprie|capree|kapree)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool Held(bool on, bool game, bool spoken, string text)
        {
            if (!on || !game || !spoken) return false;
            return !Her.IsMatch(text ?? "") && NameEars.Strip(text) == null;
        }
    }

    internal sealed partial class Companion
    {
        private bool NameGateHeld(string text, string prompt)
        {
            bool spoken = prompt == "> you: " && !typedLine;
            if (!NameGate.Held(ForkSettings.Current.NameInGames, Lockout.Active, spoken, text)) return false;
            ActivityLog.Write("voice", "held, no name in a game", text.Length > 60 ? text.Substring(0, 57) + "..." : text);
            return true;
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox nameInGames;

        private void NameGatePage(ForkSettings current)
        {
            Section("NAME NEEDED IN GAMES  (talking with friends)");
            nameInGames = Check("Name needed in games: while a game runs she only answers spoken lines with her name in them (\"Capri, ...\"), so voice chat can stay on while I talk with friends; off, she answers everything as usual", current.NameInGames);
        }

        private void FillNameGate()
        {
            settings.NameInGames = nameInGames.Checked;
        }
    }
}
