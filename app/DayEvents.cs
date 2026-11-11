using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class DayEvents
    {
        internal const string Mark = "- event: ";
        private const int LeastChars = 4;
        private const int MostChars = 120;
        private const int BlockDays = 14;
        private const int MostShown = 12;
        private static readonly object Gate = new object();

        internal static bool Ok(string what)
        {
            string text = (what ?? "").Trim();
            return text.Length >= LeastChars && text.Length <= MostChars && text.IndexOfAny(new[] { '\n', '\r', '|' }) < 0 && text.IndexOf("://", StringComparison.Ordinal) < 0;
        }

        internal static bool Add(string root, string what, DateTime day)
        {
            if (root == null || !Ok(what)) return false;
            string stamp = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string folder = Path.Combine(root, "Journal");
            string path = Path.Combine(folder, stamp + ".md");
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    string body = File.Exists(path) ? File.ReadAllText(path) : "# " + stamp + "\n\n";
                    foreach (string known in Lines(body)) if (Fold(known) == Fold(what)) return false;
                    File.WriteAllText(path, Insert(body, Mark + what.Trim()));
                    return true;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        internal static string Insert(string body, string line)
        {
            List<string> rows = new List<string>((body ?? "").Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
            int at = 0;
            if (rows[0].StartsWith("# ", StringComparison.Ordinal))
            {
                if (rows.Count == 1 || rows[1].Trim().Length > 0) rows.Insert(1, "");
                at = 2;
            }
            while (at < rows.Count && rows[at].StartsWith(Mark, StringComparison.Ordinal)) at++;
            rows.Insert(at, line);
            return string.Join("\n", rows.ToArray()) + "\n";
        }

        internal static List<string> Lines(string body)
        {
            List<string> found = new List<string>();
            foreach (string row in (body ?? "").Replace("\r\n", "\n").Split('\n'))
                if (row.StartsWith(Mark, StringComparison.Ordinal)) found.Add(row.Substring(Mark.Length).Trim());
            return found;
        }

        internal static string Block()
        {
            return Block(Vault.Root, DateTime.Now);
        }

        internal static string Block(string root, DateTime now)
        {
            if (root == null) return "";
            List<string> shown = new List<string>();
            for (int back = 0; back < BlockDays && shown.Count < MostShown; back++)
            {
                DateTime day = now.Date.AddDays(-back);
                string path = Path.Combine(root, "Journal", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md");
                string body;
                try
                {
                    if (!File.Exists(path)) continue;
                    body = File.ReadAllText(path);
                }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                List<string> lines = Lines(body);
                for (int i = lines.Count - 1; i >= 0 && shown.Count < MostShown; i--) shown.Add("- " + day.ToString("d MMM", CultureInfo.InvariantCulture) + ": " + lines[i]);
            }
            if (shown.Count == 0) return "";
            return "\nWhat he did lately, from his own words (newest first; when he asks whether he did something, this is the answer, and never offer or remind him of what is already done):\n" + string.Join("\n", shown.ToArray());
        }

        private static string Fold(string text)
        {
            return Regex.Replace((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]", "");
        }
    }
}
