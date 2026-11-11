using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    internal static class Lately
    {
        internal const int Hours = 20;
        internal const int MostLines = 60;
        internal const int LineChars = 160;

        internal static string Folder;

        internal static string FolderNow { get { return Folder ?? ChatLog.FolderFor(ActivityLog.FilePath); } }

        internal static List<string> Talk(string folder, DateTime now)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return lines;
            DateTime from = now.AddHours(-Hours);
            for (DateTime day = from.Date; day <= now.Date; day = day.AddDays(1))
            {
                string file = Path.Combine(folder, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
                string[] all;
                try { if (!File.Exists(file)) continue; all = File.ReadAllLines(file, Encoding.UTF8); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                bool aside = false;
                foreach (string raw in all)
                {
                    bool asked = Aside(raw);
                    bool reaction = aside && raw.Length > 9 && raw.Substring(9).StartsWith("assistant: ", StringComparison.Ordinal);
                    if (raw.Length > 9 && raw.Substring(9).StartsWith("user: ", StringComparison.Ordinal)) aside = asked;
                    if (reaction) continue;
                    string line = Line(raw, day, from, now);
                    if (line != null) lines.Add(line);
                }
            }
            if (lines.Count > MostLines) lines.RemoveRange(0, lines.Count - MostLines);
            return lines;
        }

        private static bool Aside(string raw)
        {
            if (raw == null || raw.Length < 16) return false;
            string rest = raw.Substring(9);
            return rest.StartsWith("user: (game", StringComparison.Ordinal) || rest.StartsWith("user: (You glanced", StringComparison.Ordinal);
        }

        internal static string Line(string raw, DateTime day, DateTime from, DateTime now)
        {
            if (raw == null || raw.Length < 12) return null;
            DateTime at;
            if (!DateTime.TryParseExact(raw.Substring(0, 8), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) return null;
            at = day.Date.Add(at.TimeOfDay);
            if (at < from || at > now) return null;
            string rest = raw.Substring(9);
            string who;
            if (rest.StartsWith("user: ", StringComparison.Ordinal)) { who = "Him"; rest = rest.Substring(6); }
            else if (rest.StartsWith("assistant: ", StringComparison.Ordinal)) { who = "Capri"; rest = rest.Substring(11); }
            else return null;
            if (rest.StartsWith("(", StringComparison.Ordinal)) return null;
            if (rest.Length > LineChars) rest = rest.Substring(0, LineChars) + "...";
            return at.ToString("ddd HH:mm", CultureInfo.InvariantCulture) + " " + who + ": " + rest;
        }

        internal static string Block(DateTime now)
        {
            List<string> lines = Talk(FolderNow, now);
            if (lines.Count == 0) return "";
            return "\nWhat he and you said lately (newer than the journal and notes, so it wins over them: anything done, bought, sent or turned down here is finished):\n" + string.Join("\n", lines.ToArray());
        }
    }
}
