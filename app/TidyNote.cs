using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class TidyNote
    {
        internal const int Hours = 14;
        internal const int MostChars = 900;
        private static readonly Regex Entry = new Regex(@"^- (\d{4}-\d{2}-\d{2} \d{2}:\d{2}) ");
        private static readonly Regex Link = new Regex(@"\s*\(\[\[Journal/[^\]]*\]\]\)|\[\[|\]\]");

        internal static string Block(DateTime now)
        {
            string root = Vault.Root;
            if (root == null) return "";
            try { return Block(File.ReadAllLines(Path.Combine(root, "Capri", "tidy.md"), Encoding.UTF8), now); }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }

        internal static string Block(string[] lines, DateTime now)
        {
            int start = -1;
            DateTime at = DateTime.MinValue;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                Match found = Entry.Match(lines[i] ?? "");
                if (!found.Success) continue;
                if (!DateTime.TryParseExact(found.Groups[1].Value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) return "";
                start = i;
                break;
            }
            if (start < 0 || now < at || (now - at).TotalHours > Hours) return "";
            List<string> filed = new List<string>();
            for (int i = start + 1; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (!line.StartsWith("- ", StringComparison.Ordinal) || Entry.IsMatch(lines[i])) break;
                filed.Add("- " + Link.Replace(line.Substring(2), "").Trim());
            }
            if (filed.Count == 0) return "";
            string text = string.Join("\n", filed.ToArray());
            if (text.Length > MostChars) text = text.Substring(0, MostChars) + "...";
            return "\nWhat you filed into his notes at your tidy at " + at.ToString("HH:mm", CultureInfo.InvariantCulture) + " (if he asks what you noted, wrote down or filed, tell him these; otherwise don't bring them up):\n" + text;
        }
    }
}
