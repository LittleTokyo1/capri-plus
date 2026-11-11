using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class Musing
    {
        private static readonly Regex Planning = new Regex(
            @"\bthe tool (result|results|output)\b|\bthe user (wants|asked|is asking|asks|said|says|meant|means|needs)\b|\b(so )?i (should|need to|have to|must|will) (answer|reply|respond|say that|say it)\b|\bin character\b|\bone short (sentence|line)\b|\bmy (reply|answer) (should|must|will)\b|\bwith no praise\b|\bthe (reply|answer) (should|must)\b",
            RegexOptions.IgnoreCase);
        private static readonly Regex Sentences = new Regex(@"(?<=[.!?])\s+");

        internal static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text) || !Planning.IsMatch(text)) return text;
            string[] parts = Sentences.Split(text.Trim());
            int last = -1;
            for (int i = 0; i < parts.Length; i++) if (Planning.IsMatch(parts[i])) last = i;
            List<string> kept = new List<string>();
            if (last >= 0 && last < parts.Length - 1)
            {
                for (int i = last + 1; i < parts.Length; i++) kept.Add(parts[i]);
            }
            else
            {
                foreach (string part in parts) if (!Planning.IsMatch(part)) kept.Add(part);
            }
            string left = string.Join(" ", kept.ToArray()).Trim();
            return left.Length == 0 ? text : left;
        }
    }
}
