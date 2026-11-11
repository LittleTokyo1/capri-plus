using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class NoteTidy
    {
        private const int MostBytes = 20000;
        private static readonly string[] Merged = { "Discord", "League", "Also called", "Plays", "Is" };
        private static readonly string[] Repeated = { "Is", "Plays" };
        private static readonly Regex Heading = new Regex(@"^#\s+(.*)$", RegexOptions.CultureInvariant);
        private static readonly Regex Placeholder = new Regex(@"^\s*[-*]?\s*[\p{L} ]+\s*:\s*(not known( yet)?|unknown|n/?a|none|\?+)\s*\.?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static Regex Field(string name)
        {
            return new Regex(@"^(\s*[-*]?\s*)" + Regex.Escape(name) + @"\s*:\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string Fold(string text)
        {
            return Names.Fold(NoteEdit.Bare((text ?? "").Trim()).TrimStart('-', '*', ' ')).Trim();
        }

        private static List<string> Values(string text)
        {
            List<string> found = new List<string>();
            foreach (string part in (text ?? "").Split(',', ';'))
                if (part.Trim().Length > 0) found.Add(part.Trim());
            return found;
        }

        internal static string Clean(string body, string title)
        {
            if (string.IsNullOrEmpty(body)) return body;
            string newline = body.Contains("\r\n") ? "\r\n" : "\n";
            List<string> all = new List<string>(body.Replace("\r\n", "\n").Split('\n'));
            int ending = NoteEdit.Ending(all);
            if (ending < 0) return Tidied(body, title);
            string head = string.Join(newline, all.GetRange(0, ending).ToArray());
            string tail = string.Join(newline, all.GetRange(ending, all.Count - ending).ToArray());
            string cleaned = Tidied(head + newline, title);
            return cleaned + (cleaned.EndsWith(newline, StringComparison.Ordinal) ? "" : newline) + tail;
        }

        private static string Tidied(string body, string title)
        {
            if (string.IsNullOrEmpty(body)) return body;
            string newline = body.Contains("\r\n") ? "\r\n" : "\n";
            List<string> lines = new List<string>(body.Replace("\r\n", "\n").Split('\n'));
            bool ending = lines.Count > 0 && lines[lines.Count - 1].Length == 0;
            if (ending) lines.RemoveAt(lines.Count - 1);

            while (lines.Count > 0)
            {
                int first = lines.FindIndex(delegate(string line) { return line.Trim().Length > 0; });
                if (first < 0) break;
                Match head = Heading.Match(lines[first].Trim());
                if (!head.Success || Names.Fold(head.Groups[1].Value) != Names.Fold(title)) break;
                lines.RemoveRange(0, first + 1);
            }
            while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);

            lines.RemoveAll(delegate(string line) { return Placeholder.IsMatch(line); });

            foreach (string name in Merged)
            {
                Regex own = Field(name);
                int at = -1;
                List<string> values = new List<string>();
                for (int i = 0; i < lines.Count; i++)
                {
                    Match match = own.Match(lines[i].TrimEnd());
                    if (!match.Success) continue;
                    if (at < 0) at = i;
                    foreach (string value in Values(match.Groups[2].Value))
                        if (!values.Exists(delegate(string known) { return Names.Fold(known) == Names.Fold(value); })) values.Add(value);
                }
                if (at < 0) continue;
                Match lead = own.Match(lines[at].TrimEnd());
                string merged = lead.Groups[1].Value + name + ": " + string.Join(", ", values.ToArray());
                for (int i = lines.Count - 1; i > at; i--)
                    if (own.IsMatch(lines[i].TrimEnd())) lines.RemoveAt(i);
                lines[at] = merged;
            }

            List<string> seen = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                string key = Fold(lines[i]);
                if (key.Length == 0 || lines[i].TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
                if (seen.Contains(key)) { lines.RemoveAt(i); i--; }
                else seen.Add(key);
            }

            foreach (string name in Repeated)
            {
                Regex own = Field(name);
                for (int i = 0; i < lines.Count; i++)
                {
                    Match match = own.Match(lines[i].TrimEnd());
                    if (!match.Success) continue;
                    List<string> keep = new List<string>();
                    foreach (string value in Values(match.Groups[2].Value))
                    {
                        string wanted = " " + Names.Fold(value) + " ";
                        bool said = false;
                        for (int j = 0; j < lines.Count && !said; j++)
                        {
                            if (j == i || lines[j].TrimStart().StartsWith("#", StringComparison.Ordinal) || own.IsMatch(lines[j].TrimEnd())) continue;
                            said = (" " + Names.Fold(lines[j]) + " ").IndexOf(wanted, StringComparison.Ordinal) >= 0;
                        }
                        if (!said) keep.Add(value);
                    }
                    if (keep.Count == Values(match.Groups[2].Value).Count) continue;
                    if (keep.Count == 0) { lines.RemoveAt(i); i--; }
                    else lines[i] = match.Groups[1].Value + name + ": " + string.Join(", ", keep.ToArray());
                }
            }

            string result = string.Join(newline, lines.ToArray());
            return ending ? result + newline : result;
        }

        internal static int Run(string root, DateTime now)
        {
            if (string.IsNullOrEmpty(root)) return 0;
            string folder;
            try { folder = Path.GetFullPath(Path.Combine(root, "People")) + Path.DirectorySeparatorChar; }
            catch (ArgumentException) { return 0; }
            if (!Directory.Exists(folder)) return 0;
            int changed = 0;
            foreach (string path in Directory.GetFiles(folder, "*.md"))
            {
                try
                {
                    string full = Path.GetFullPath(path);
                    if (!full.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || new FileInfo(full).Length > MostBytes) continue;
                    string body = File.ReadAllText(full);
                    string clean = Clean(body, Path.GetFileNameWithoutExtension(full));
                    if (clean == body) continue;
                    string backups = Path.Combine(root, "Capri", "backups");
                    Directory.CreateDirectory(backups);
                    File.WriteAllText(Path.Combine(backups, "People-" + Path.GetFileNameWithoutExtension(full) + "-" + now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".md"), body);
                    File.WriteAllText(full, clean);
                    changed++;
                    ActivityLog.Write("tidy", "cleaned note", "People/" + Path.GetFileName(full));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (changed > 0) Names.Reset();
            return changed;
        }
    }
}
