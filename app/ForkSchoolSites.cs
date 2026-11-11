using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class SchoolSites
    {
        internal static string[] Names()
        {
            ForkSettings current = ForkSettings.Current;
            return current == null || current.SchoolSites == null ? new string[0] : current.SchoolSites;
        }

        internal static string Listed(string join)
        {
            string[] names = Names();
            return names.Length == 0 ? "your school sites" : string.Join(join, names);
        }

        internal static bool Named(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string name in Names())
            {
                string plain = (name ?? "").Trim();
                if (plain.Length == 0) continue;
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(plain).Replace(@"\ ", @"\s*") + @"\b", RegexOptions.IgnoreCase)) return true;
            }
            return false;
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private TextBox schoolSites;

        private void SchoolSitesPage(ForkSettings current)
        {
            Section("SCHOOL SITES");
            schoolSites = Field("School sites: the names of your school's websites, one per line (and how her ears may mishear them); kept only on this PC", string.Join(Environment.NewLine, current.SchoolSites ?? new string[0]), true);
        }

        private void FillSchoolSites()
        {
            settings.SchoolSites = Lines(schoolSites.Text);
        }
    }
}
