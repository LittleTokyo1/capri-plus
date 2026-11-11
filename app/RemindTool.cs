using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class RemindTool
    {
        private const int LongestText = 200;
        private const int LongestJob = 400;
        private const int AppointmentMinutes = 60;
        private static readonly string[] Formats = { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss" };
        private static readonly string[] Days = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };

        internal static string Store;
        internal static Func<string, DateTime, int, string[]> Calendar = Google.CreateEvent;

        internal static string Path { get { return Store ?? Scheduler.FilePath; } }

        internal static bool Valid(ToolArguments args)
        {
            string action = args.Action ?? "add";
            if (action == "list" || action == "clear") return true;
            return action == "add" && (!string.IsNullOrWhiteSpace(args.Start) || !string.IsNullOrWhiteSpace(args.Appointment)) && !string.IsNullOrWhiteSpace(args.Text);
        }

        internal static string Do(ToolArguments args, DateTime now)
        {
            string action = string.IsNullOrEmpty(args.Action) ? "add" : args.Action;
            if (action == "list") return List(now);
            if (action == "clear") return Companion.ClearedSaying(Path, now);
            return Add(args, now);
        }

        private static string List(DateTime now)
        {
            List<ScheduleItem> items = Scheduler.Load(Path);
            if (items.Count == 0) return "Nothing is scheduled.";
            List<string> told = new List<string>();
            foreach (ScheduleItem item in items) told.Add(Scheduler.Describe(item, now));
            return "You have " + items.Count + ". " + string.Join(". ", told.ToArray()) + ".";
        }

        internal static bool Moment(string said, DateTime now, out DateTime due)
        {
            string text = (said ?? "").Trim();
            if (DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out due)) return true;
            DateTime clock;
            if (!DateTime.TryParseExact(text, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out clock)) return false;
            due = now.Date.AddHours(clock.Hour).AddMinutes(clock.Minute);
            if (due <= now) due = due.AddDays(1);
            return true;
        }

        private static string Every(string said, ref DateTime due)
        {
            string text = (said ?? "").Trim().ToLowerInvariant();
            if (text.Length == 0 || text == "no" || text == "once" || text == "none") return "";
            if (text == "daily" || text == "every day" || text == "day") return "daily";
            if (text.Length < 3) return "";
            int day = Array.IndexOf(Days, text.Substring(0, 3));
            if (day < 0) return "";
            while ((int)due.DayOfWeek != day) due = due.AddDays(1);
            return Days[day];
        }

        private static string Add(ToolArguments args, DateTime now)
        {
            string text = Regex.Replace(args.Text ?? "", @"\s+", " ").Trim();
            if (text.Length == 0) return "What should I remind you about, choom?";
            if (text.Length > LongestText) text = text.Substring(0, LongestText);
            bool appointed = !string.IsNullOrWhiteSpace(args.Appointment);
            if (!appointed && Scheduler.AfterGameAsked(args.Start)) return AddAfterGame(text, args.Task, now);
            DateTime visit = DateTime.MinValue;
            if (appointed && !Moment(args.Appointment, now, out visit)) return "When is the appointment? Give me a day and a time, like tomorrow at 3.";
            if (appointed && visit <= now) return "That time has already passed. When should it be?";
            DateTime due;
            if (appointed && string.IsNullOrWhiteSpace(args.Start)) due = visit.AddMinutes(-Google.AlertMinutes) > now ? visit.AddMinutes(-Google.AlertMinutes) : visit;
            else if (!Moment(args.Start, now, out due)) return "When? Give me a day and a time, like tomorrow at 9.";
            if (due <= now) return "That time has already passed. When should it be?";
            string job = Regex.Replace(args.Task ?? "", @"\s+", " ").Trim();
            if (job.Length > LongestJob) job = job.Substring(0, LongestJob);
            string repeat = Every(args.Every, ref due);
            ScheduleItem item = new ScheduleItem { Due = due, Repeat = repeat, Text = text, Job = job };
            Scheduler.Add(Path, item);
            PlanLinks.Seen(PlanLinks.Reminder, PlanLinks.IdOf(item.Text), Brain.Asking, item.Text, now);
            string described = Scheduler.Describe(item, now);
            ActivityLog.Write("schedule", "added", described);
            string said = "Set: " + described + ".";
            if (!appointed) return Dated(item, now) && CalendarOn() ? said + OnCalendar(text, due, ReminderMinutes) : said;
            if (repeat.Length > 0) return said + " A repeating reminder stays off the calendar.";
            return said + OnCalendar(text, visit, AppointmentMinutes);
        }

        private static string AddAfterGame(string text, string task, DateTime now)
        {
            string job = Regex.Replace(task ?? "", @"\s+", " ").Trim();
            if (job.Length > LongestJob) job = job.Substring(0, LongestJob);
            ScheduleItem item = new ScheduleItem { Due = now.AddHours(Scheduler.AfterGameHours), Repeat = Scheduler.AfterGame, Text = text, Job = job };
            Scheduler.Add(Path, item);
            PlanLinks.Seen(PlanLinks.Reminder, PlanLinks.IdOf(item.Text), Brain.Asking, item.Text, now);
            string described = Scheduler.Describe(item, now);
            ActivityLog.Write("schedule", "added", described);
            return "Set: " + described + ".";
        }

        internal const int ReminderMinutes = 30;

        internal static Func<bool> CalendarOn = delegate { return Google.Connected; };

        internal static bool Dated(ScheduleItem item, DateTime now) { return item.Repeat.Length == 0 && string.IsNullOrEmpty(item.Job) && item.Due.Date > now.Date; }

        internal static string OnCalendar(string title, DateTime visit, int minutes)
        {
            string[] made;
            try { made = Calendar(title, visit, minutes); }
            catch (Exception error) { made = new string[] { null, error.Message }; }
            if (made == null || made.Length < 2 || made[0] == null) return " I couldn't put it on your Google Calendar: " + (made != null && made.Length > 1 ? made[1] : "no answer") + " The reminder is still set.";
            ActivityLog.Write("schedule", "calendar", title + " " + made[1]);
            return " It's on your Google Calendar for " + made[1] + " with a phone alert " + Google.AlertMinutes + " minutes before.";
        }
    }
}
