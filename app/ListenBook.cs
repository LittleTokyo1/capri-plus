using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class ListenLine
    {
        internal DateTime At;
        internal string Who;
        internal string Text;
    }

    internal static class ListenBook
    {
        internal const int MostLines = 400;
        internal const int LeastWords = 2;
        internal const string Him = "Him";
        internal const string Friend = "a friend";
        private static readonly object gate = new object();
        private static readonly List<ListenLine> lines = new List<ListenLine>();

        internal static bool Add(DateTime at, string who, string text)
        {
            string said = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            if (said.Length < 4 || said.Split(' ').Length < LeastWords) return false;
            lock (gate)
            {
                lines.Add(new ListenLine { At = at, Who = who, Text = said });
                if (lines.Count > MostLines) lines.RemoveAt(0);
            }
            return true;
        }

        internal static int Count { get { lock (gate) return lines.Count; } }

        internal static List<ListenLine> Take()
        {
            lock (gate)
            {
                List<ListenLine> taken = new List<ListenLine>(lines);
                lines.Clear();
                return taken;
            }
        }

        internal static void Clear() { lock (gate) lines.Clear(); }

        internal static string Transcript(List<ListenLine> taken, List<string> inCall)
        {
            StringBuilder text = new StringBuilder("Overheard voice chat on Discord while his listening mode was on. In the call with him: ");
            text.Append(inCall != null && inCall.Count > 0 ? string.Join(", ", inCall.ToArray()) : "friends whose names you don't know");
            text.Append(". Lines from him are his own words from his mic. Lines from a friend are one of the others in the call, and which one is unknown unless the line says it. ");
            text.Append("Speech-to-text makes mistakes and much of it is Lithuanian: write every fact in English. Keep only clear, lasting facts about him, or about a friend the line names; never guess who said what, and keep nothing about a person who is not in his notes.\n");
            foreach (ListenLine line in taken)
                text.Append(line.At.ToString("HH:mm", CultureInfo.InvariantCulture)).Append(' ').Append(line.Who).Append(": ").Append(line.Text).Append('\n');
            return text.ToString();
        }
    }
}
