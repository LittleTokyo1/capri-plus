using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class JournalDays
    {
        private const int DayStart = 5;
        private static readonly string[] Months = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
        private static readonly Regex DayMonth = new Regex(@"\b(?<d>\d{1,2})(?:st|nd|rd|th)?(?:\s+of)?\s+(?<m>january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|jun|jul|aug|sept|sep|oct|nov|dec)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MonthDay = new Regex(@"\b(?<m>january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|jun|jul|aug|sept|sep|oct|nov|dec)\.?\s+(?<d>\d{1,2})(?:st|nd|rd|th)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Weekday = new Regex(@"\b(?:on|last)\s+(?<w>monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static List<DateTime> Named(string text, DateTime now)
        {
            List<DateTime> days = new List<DateTime>();
            string said = (text ?? "").ToLowerInvariant();
            DateTime today = now.AddHours(-DayStart).Date;
            if (Regex.IsMatch(said, @"\bday before yesterday\b")) Add(days, today.AddDays(-2));
            else if (Regex.IsMatch(said, @"\b(yesterday|last night)\b")) Add(days, today.AddDays(-1));
            if (Regex.IsMatch(said, @"\b(today|tonight|this morning)\b")) Add(days, today);
            foreach (Match match in Weekday.Matches(said))
            {
                DayOfWeek wanted = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), match.Groups["w"].Value, true);
                DateTime day = today;
                while (day.DayOfWeek != wanted) day = day.AddDays(-1);
                if (match.Value.StartsWith("last", StringComparison.Ordinal) && day == today) day = day.AddDays(-7);
                Add(days, day);
            }
            foreach (Regex pattern in new[] { DayMonth, MonthDay })
                foreach (Match match in pattern.Matches(said))
                {
                    int month = Array.IndexOf(Months, match.Groups["m"].Value.Substring(0, 3).ToLowerInvariant()) + 1;
                    int day = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
                    if (month < 1 || day < 1 || day > DateTime.DaysInMonth(today.Year, month)) continue;
                    DateTime date = new DateTime(today.Year, month, day);
                    if (date > today) date = date.AddYears(-1);
                    Add(days, date);
                }
            return days;
        }

        internal static bool IsDay(string root, string file, List<DateTime> days)
        {
            if (days.Count == 0 || root == null) return false;
            foreach (DateTime day in days)
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(DayJournal.PathFor(root, day)), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Add(List<DateTime> days, DateTime day)
        {
            if (!days.Contains(day)) days.Add(day);
        }
    }
}
