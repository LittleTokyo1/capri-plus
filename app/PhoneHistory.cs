using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    internal sealed class PhoneChatLine
    {
        internal bool Mine;
        internal string Text;
        internal DateTime At;
    }

    internal static class PhoneHistory
    {
        internal const int MostLines = 40;
        internal const int LineChars = 600;

        internal static List<PhoneChatLine> Lines(string folder, DateTime now, int most)
        {
            List<PhoneChatLine> lines = new List<PhoneChatLine>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return lines;
            for (DateTime day = now.Date.AddDays(1 - ChatLog.KeepDays); day <= now.Date; day = day.AddDays(1))
            {
                string file = Path.Combine(folder, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
                string[] all;
                try { if (!File.Exists(file)) continue; all = File.ReadAllLines(file, Encoding.UTF8); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                foreach (string raw in all)
                {
                    PhoneChatLine line = Parse(raw, day);
                    if (line != null && line.At <= now) lines.Add(line);
                }
            }
            if (lines.Count > most) lines.RemoveRange(0, lines.Count - most);
            return lines;
        }

        internal static PhoneChatLine Parse(string raw, DateTime day)
        {
            if (raw == null || raw.Length < 12) return null;
            DateTime at;
            if (!DateTime.TryParseExact(raw.Substring(0, 8), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) return null;
            string rest = raw.Substring(9);
            bool mine = rest.StartsWith("user: ", StringComparison.Ordinal);
            if (!mine && !rest.StartsWith("assistant: ", StringComparison.Ordinal)) return null;
            string text = rest.Substring(mine ? 6 : 11).Trim();
            if (text.Length == 0 || (mine && text.StartsWith("(", StringComparison.Ordinal))) return null;
            if (text.Length > LineChars) text = text.Substring(0, LineChars - 3) + "...";
            return new PhoneChatLine { Mine = mine, Text = text, At = day.Date.Add(at.TimeOfDay) };
        }

        internal static string Json(List<PhoneChatLine> lines)
        {
            StringBuilder json = new StringBuilder("{\"lines\":[");
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append("{\"mine\":").Append(lines[i].Mine ? "true" : "false").Append(",\"text\":").Append(PhoneServer.Json(lines[i].Text)).Append(",\"at\":").Append(PhoneServer.Json(lines[i].At.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))).Append('}');
            }
            return json.Append("]}").ToString();
        }
    }
}
