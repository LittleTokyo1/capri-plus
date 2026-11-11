using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class ScheduleItem
    {
        internal string Id;
        internal DateTime Due;
        internal string Repeat = "";
        internal string Text = "";
        internal string Job = "";
    }

    internal static class Scheduler
    {
        internal const int LateHours = 6;
        internal const string AfterGame = "game";
        internal const int AfterGameHours = 12;
        private static readonly Regex AfterGameSaid = new Regex(@"\b(?:after|when|once) (?:i finish )?(?:the |this |my |our |your |next |the next |my next |the current |my current )?(?:(?:league|league of legends|lol|ranked|current) )?(?:game|match)(?: (?:ends|is over|is done|finishes))?\b", RegexOptions.CultureInvariant);
        private const int DefaultHour = 9;
        private static readonly object gate = new object();
        private static readonly string[] Days = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };
        private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        private const string Clock = @"(?<h>\d{1,2})(?::(?<m>\d{2}))?(?:\s?(?<ap>am|pm))?";
        private static readonly Regex Lead = new Regex(@"^(?:(?:hey|ok|okay) )?(?:capri )?(?:please )?(?:(?:can|could|would|will) you )?(?:please )?(?=every|daily|at|in|tomorrow|remind me|set (?:a |an )?(?:reminder|alarm|timer)|schedule|ping me)", RegexOptions.CultureInvariant);
        private static readonly Regex Keyword = new Regex(@"\b(remind me|reminder|alarm|timer|ping me|schedule)\b", RegexOptions.CultureInvariant);
        private static readonly Regex Daily = new Regex(@"\b(?:every day|daily) (?:at|around) " + Clock, RegexOptions.CultureInvariant);
        private static readonly Regex Weekly = new Regex(@"\bevery (?<d>sunday|monday|tuesday|wednesday|thursday|friday|saturday) (?:at|around) " + Clock, RegexOptions.CultureInvariant);
        private static readonly Regex Relative = new Regex(@"\b(?:in|for) (?:(?<n>\d{1,3}) (?<u>minutes?|mins?|hours?|hrs?)|(?<an>an hour)|(?<half>half an hour)|(?<one>a minute))\b", RegexOptions.CultureInvariant);
        private static readonly Regex Tomorrow = new Regex(@"\btomorrow(?: morning)?(?: (?:at|around) " + Clock + @")?", RegexOptions.CultureInvariant);
        private static readonly Regex OneDay = new Regex(@"\bon (?<d>sunday|monday|tuesday|wednesday|thursday|friday|saturday)(?: (?:at|around) " + Clock + ")?", RegexOptions.CultureInvariant);
        private static readonly Regex At = new Regex(@"\b(?:at|around) " + Clock, RegexOptions.CultureInvariant);
        private static readonly Regex Command = new Regex(@"\b(?:remind me|set (?:a |an )?(?:reminder|alarm|timer)|ping me|schedule)\b(?: for| to| that| about)?", RegexOptions.CultureInvariant);
        private static readonly Regex Listed = new Regex(@"^(?:(?:hey|ok|okay) )?(?:capri )?(?:please )?(?:(?:can|could) you )?(?:(?:what|which|any) (?:reminders?|timers?|alarms?)(?: do i have| have i got| are set| is set| are scheduled| are there)?(?: today| tomorrow)?|do i have (?:any )?(?:reminders?|timers?|alarms?)(?: set)?(?: today| tomorrow)?|(?:show|list|read|tell)(?: me)?(?: out)? (?:my|the) (?:reminders?|timers?|alarms?)|what is scheduled|whats scheduled)$", RegexOptions.CultureInvariant);
        private static readonly Regex Cleared = new Regex(@"^(?:(?:hey|ok|okay) )?(?:capri )?(?:please )?(?:(?:can|could) you )?(?:cancel|clear|delete|remove) (?:all )?(?:of )?(?:my |the )?(?:reminders|timers|alarms)$", RegexOptions.CultureInvariant);

        internal static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "schedule.txt"); }
        }

        internal static string Plain(string said)
        {
            return Regex.Replace(Regex.Replace((said ?? "").ToLowerInvariant().Replace("'", "").Replace("’", ""), "[^a-z0-9: ]+", " "), @"\s+", " ").Trim();
        }

        private static readonly string[] SmallWords = { "forty five", "forty-five", "fifteen", "twenty", "thirty", "eleven", "twelve", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
        private static readonly string[] SmallNumbers = { "45", "45", "15", "20", "30", "11", "12", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };

        internal static string Spoken(string said)
        {
            string text = said ?? "";
            text = Regex.Replace(text, @"\b([AaPp])\.\s?[Mm]\b\.?", "$1m");
            text = Regex.Replace(text, @"\b(\d{1,2})\.(\d{2})\b", "$1:$2");
            text = Regex.Replace(text, @"\b(at|around) (\d{1,2}) (\d{2})\b", "$1 $2:$3", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(at|around) (half past|quarter past|quarter to) (\d{1,2}|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\b", delegate(Match part)
            {
                string value = part.Groups[3].Value.ToLowerInvariant();
                int number = char.IsDigit(value[0]) ? int.Parse(value, CultureInfo.InvariantCulture) : int.Parse(SmallNumbers[Array.IndexOf(SmallWords, value)], CultureInfo.InvariantCulture);
                string kind = part.Groups[2].Value.ToLowerInvariant();
                if (kind == "quarter to") return part.Groups[1].Value + " " + (number == 1 ? 12 : number - 1) + ":45";
                return part.Groups[1].Value + " " + number + (kind == "half past" ? ":30" : ":15");
            }, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(at|around) noon\b", "$1 12:00 pm", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(at|around) midnight\b", "$1 12:00 am", RegexOptions.IgnoreCase);
            return Regex.Replace(text, @"\b(in|at|around|for) (forty[ -]five|fifteen|twenty|thirty|eleven|twelve|one|two|three|four|five|six|seven|eight|nine|ten)\b", delegate(Match word)
            {
                int at = Array.IndexOf(SmallWords, word.Groups[2].Value.ToLowerInvariant());
                return word.Groups[1].Value + " " + SmallNumbers[at];
            }, RegexOptions.IgnoreCase);
        }

        internal static bool Listing(string said) { return Listed.IsMatch(Plain(said)); }

        internal static bool Clearing(string said) { return Cleared.IsMatch(Plain(said)); }

        private static int Hour(Match clock, out int minute, out bool given)
        {
            minute = clock.Groups["m"].Success ? int.Parse(clock.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
            int hour = int.Parse(clock.Groups["h"].Value, CultureInfo.InvariantCulture);
            string ap = clock.Groups["ap"].Success ? clock.Groups["ap"].Value : "";
            given = ap.Length > 0;
            if (ap == "pm" && hour < 12) hour += 12;
            if (ap == "am" && hour == 12) hour = 0;
            if (hour > 23 || minute > 59) return -1;
            return hour;
        }

        private static DateTime Next(DateTime now, int hour, int minute, bool guess)
        {
            DateTime today = now.Date.AddHours(hour).AddMinutes(minute);
            if (today > now) return today;
            if (guess && hour < 12 && today.AddHours(12) > now) return today.AddHours(12);
            return today.AddDays(1);
        }

        internal static ScheduleItem Parse(string said, DateTime now)
        {
            string plain = Plain(Spoken(said));
            if (!Lead.IsMatch(plain) || !Keyword.IsMatch(plain)) return null;
            string repeat = "";
            DateTime due;
            Match found;
            int hour, minute;
            bool given;
            if ((found = Daily.Match(plain)).Success)
            {
                hour = Hour(found, out minute, out given);
                if (hour < 0) return null;
                repeat = "daily";
                due = Next(now, hour, minute, false);
            }
            else if ((found = Weekly.Match(plain)).Success)
            {
                hour = Hour(found, out minute, out given);
                if (hour < 0) return null;
                int day = Array.IndexOf(DayNames, char.ToUpperInvariant(found.Groups["d"].Value[0]) + found.Groups["d"].Value.Substring(1));
                repeat = Days[day];
                due = now.Date.AddHours(hour).AddMinutes(minute);
                while (due <= now || (int)due.DayOfWeek != day) due = due.AddDays(1);
            }
            else if ((found = Relative.Match(plain)).Success)
            {
                int minutes = found.Groups["half"].Success ? 30 : found.Groups["an"].Success ? 60 : found.Groups["one"].Success ? 1 : int.Parse(found.Groups["n"].Value, CultureInfo.InvariantCulture) * (found.Groups["u"].Value.StartsWith("h", StringComparison.Ordinal) ? 60 : 1);
                if (minutes <= 0) return null;
                due = now.AddMinutes(minutes);
                due = due.AddSeconds(-due.Second).AddMilliseconds(-due.Millisecond);
            }
            else if ((found = Tomorrow.Match(plain)).Success)
            {
                hour = DefaultHour;
                minute = 0;
                if (found.Groups["h"].Success)
                {
                    hour = Hour(found, out minute, out given);
                    if (hour < 0) return null;
                }
                due = now.Date.AddDays(1).AddHours(hour).AddMinutes(minute);
            }
            else if ((found = OneDay.Match(plain)).Success)
            {
                hour = DefaultHour;
                minute = 0;
                if (found.Groups["h"].Success)
                {
                    hour = Hour(found, out minute, out given);
                    if (hour < 0) return null;
                    if (!given && hour >= 1 && hour <= 8) hour += 12;
                }
                int target = Array.IndexOf(DayNames, char.ToUpperInvariant(found.Groups["d"].Value[0]) + found.Groups["d"].Value.Substring(1));
                due = now.Date.AddHours(hour).AddMinutes(minute);
                while (due <= now || (int)due.DayOfWeek != target) due = due.AddDays(1);
            }
            else if ((found = At.Match(plain)).Success)
            {
                hour = Hour(found, out minute, out given);
                if (hour < 0) return null;
                due = Next(now, hour, minute, !given);
            }
            else if ((found = AfterGameSaid.Match(plain)).Success)
            {
                repeat = AfterGame;
                due = now.AddHours(AfterGameHours);
            }
            else return null;
            string rest = (plain.Substring(0, found.Index) + " " + plain.Substring(found.Index + found.Length)).Trim();
            rest = Lead.Replace(rest, "");
            string timer = Regex.IsMatch(rest, @"\balarm\b") ? "your alarm" : Regex.IsMatch(rest, @"\btimer\b") ? "your timer" : "your reminder";
            rest = Regex.Replace(Command.Replace(rest, " "), @"\s+", " ").Trim();
            rest = Regex.Replace(rest, @"^(?:please )?(?:to |that |about |for )", "");
            rest = Regex.Replace(rest, @"\s*\b(?:please)$", "").Trim();
            return new ScheduleItem { Id = Guid.NewGuid().ToString("N").Substring(0, 8), Due = due, Repeat = repeat, Text = rest.Length == 0 ? timer : rest };
        }

        internal static bool AfterGameAsked(string start)
        {
            string plain = Plain((start ?? "").Replace('_', ' '));
            return plain.Length > 0 && Regex.IsMatch(plain, @"^(?:after|when|once)\b.*\b(?:game|match)\b", RegexOptions.CultureInvariant);
        }

        internal static List<ScheduleItem> TakeAfterGame(string path, DateTime now)
        {
            List<ScheduleItem> due = new List<ScheduleItem>();
            List<ScheduleItem> kept = new List<ScheduleItem>();
            foreach (ScheduleItem item in Load(path))
            {
                if (item.Repeat == AfterGame && item.Due > now) due.Add(item);
                else kept.Add(item);
            }
            if (due.Count > 0) lock (gate) Save(path, kept);
            return due;
        }

        internal static string Describe(ScheduleItem item, DateTime now)
        {
            if (item.Repeat == AfterGame) return "after your game: " + item.Text + (string.IsNullOrEmpty(item.Job) ? "" : " (a job I run then)");
            string time = item.Due.ToString("HH:mm", CultureInfo.InvariantCulture);
            string when = item.Repeat == "daily" ? "every day at " + time : item.Repeat.Length > 0 ? "every " + DayNames[Array.IndexOf(Days, item.Repeat)] + " at " + time : item.Due.Date == now.Date ? time + " today" : item.Due.Date == now.Date.AddDays(1) ? time + " tomorrow" : item.Due.ToString("ddd d MMM", CultureInfo.InvariantCulture) + " " + time;
            return when + ": " + item.Text + (string.IsNullOrEmpty(item.Job) ? "" : " (a job I run then)");
        }

        private static string Clean(string text)
        {
            return Regex.Replace(text ?? "", @"\s+", " ").Trim();
        }

        internal static List<ScheduleItem> Load(string path)
        {
            List<ScheduleItem> items = new List<ScheduleItem>();
            lock (gate)
            {
                if (path == null || !File.Exists(path)) return items;
                string[] lines;
                try { lines = File.ReadAllLines(path, Encoding.UTF8); }
                catch (IOException) { return items; }
                foreach (string line in lines)
                {
                    string[] part = line.Split('\t');
                    DateTime due;
                    if (part.Length < 4 || !DateTime.TryParseExact(part[1], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out due)) continue;
                    items.Add(new ScheduleItem { Id = part[0], Due = due, Repeat = part[2], Text = Clean(part[3]), Job = part.Length > 4 ? Clean(string.Join(" ", part, 4, part.Length - 4)) : "" });
                }
            }
            return items;
        }

        private static void Save(string path, List<ScheduleItem> items)
        {
            List<string> lines = new List<string>();
            foreach (ScheduleItem item in items) lines.Add(item.Id + "\t" + item.Due.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\t" + item.Repeat + "\t" + Clean(item.Text) + (string.IsNullOrEmpty(item.Job) ? "" : "\t" + Clean(item.Job)));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
        }

        internal static bool Drop(string path, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            List<ScheduleItem> items = Load(path);
            lock (gate)
            {
                if (items.RemoveAll(delegate(ScheduleItem item) { return item.Id == id; }) == 0) return false;
                Save(path, items);
                return true;
            }
        }

        internal static void Add(string path, ScheduleItem item)
        {
            List<ScheduleItem> items = Load(path);
            lock (gate)
            {
                if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
                items.Add(item);
                items.Sort(delegate(ScheduleItem a, ScheduleItem b) { return a.Due.CompareTo(b.Due); });
                Save(path, items);
            }
        }

        internal static void Clear(string path)
        {
            lock (gate)
            {
                if (path != null && File.Exists(path)) File.Delete(path);
            }
        }

        internal static List<ScheduleItem> Take(string path, DateTime now, List<ScheduleItem> missed)
        {
            return Take(path, now, missed, false);
        }

        internal static List<ScheduleItem> Take(string path, DateTime now, List<ScheduleItem> missed, bool jobs)
        {
            List<ScheduleItem> due = new List<ScheduleItem>();
            List<ScheduleItem> items = Load(path);
            if (items.Count == 0) return due;
            List<ScheduleItem> kept = new List<ScheduleItem>();
            bool changed = false;
            foreach (ScheduleItem item in items)
            {
                if (item.Repeat == AfterGame)
                {
                    if (item.Due > now) { kept.Add(item); continue; }
                    changed = true;
                    if (missed != null) missed.Add(item);
                    continue;
                }
                if (item.Due > now || (item.Job.Length > 0) != jobs) { kept.Add(item); continue; }
                changed = true;
                if ((now - item.Due).TotalHours > LateHours) { if (missed != null) missed.Add(item); }
                else due.Add(item);
                if (item.Repeat.Length == 0) continue;
                ScheduleItem next = new ScheduleItem { Id = item.Id, Due = item.Due, Repeat = item.Repeat, Text = item.Text, Job = item.Job };
                while (next.Due <= now) next.Due = next.Due.AddDays(item.Repeat == "daily" ? 1 : 7);
                kept.Add(next);
            }
            if (changed) lock (gate) Save(path, kept);
            return due;
        }
    }
}
