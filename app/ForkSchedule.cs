using System;
using System.Collections.Generic;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int ScheduleCheckSeconds = 5;
        private const int ScheduleRetrySeconds = 60;
        private DateTime lastScheduleCheck = DateTime.MinValue;
        private DateTime scheduleRetryAt = DateTime.MinValue;
        private bool scheduleSending;
        private bool scheduleJobBusy;

        private bool ScheduleMaySpeak()
        {
            return ScheduleOpen(ForkSettings.Current.Paused, dragging, speech != null, talkState, typer.Visible, HisTurn());
        }

        private static bool ScheduleOpen(bool paused, bool held, bool speaking, TalkState talk, bool typing, bool hisTurn)
        {
            return !paused && !held && !speaking && (talk == TalkState.Off || talk == TalkState.Online) && !typing && !hisTurn;
        }

        private List<ScheduleItem> ScheduleDue(string path, DateTime now)
        {
            List<ScheduleItem> missed = new List<ScheduleItem>();
            List<ScheduleItem> due = Scheduler.Take(path, now, missed);
            foreach (ScheduleItem item in missed) ActivityLog.Write("schedule", "missed", item.Text);
            return due;
        }

        private static string ScheduleTexts(List<ScheduleItem> due)
        {
            List<string> texts = new List<string>();
            foreach (ScheduleItem item in due) texts.Add(item.Text);
            return string.Join("; ", texts.ToArray());
        }

        private void ScheduleSent(string path, List<ScheduleItem> due, string texts, bool sent, DateTime now)
        {
            if (sent)
            {
                ActivityLog.Write("schedule", "sent", texts);
                ScheduleParked(due, now);
                return;
            }
            foreach (ScheduleItem item in due) Scheduler.Add(path, new ScheduleItem { Due = item.Due, Repeat = "", Text = item.Text });
            scheduleRetryAt = now.AddSeconds(ScheduleRetrySeconds);
            ActivityLog.Write("schedule", "retry", texts);
        }

        private static void ScheduleParked(List<ScheduleItem> due, DateTime now)
        {
            foreach (ScheduleItem item in due) PlanLinks.Done(PlanLinks.Reminder, PlanLinks.IdOf(item.Text), now);
        }

        private void ScheduleRemote(DateTime now)
        {
            if (scheduleSending || now < scheduleRetryAt) return;
            string path = Scheduler.FilePath;
            List<ScheduleItem> due = ScheduleDue(path, now);
            if (due.Count == 0) return;
            string texts = ScheduleTexts(due);
            scheduleSending = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool sent = false;
                try { sent = Delivery.Send("Reminder: " + texts + "."); }
                catch (Exception error) { Brain.Log("schedule", -1, error.GetType().Name + ": " + error.Message); }
                Post(delegate
                {
                    scheduleSending = false;
                    ScheduleSent(path, due, texts, sent, DateTime.Now);
                    if (sent) brain.Own("reminder", "Reminder sent to his phone on Discord: " + texts + ".");
                    if (sent) Digest.Add("remind", texts, DateTime.Now);
                });
            });
        }

        private void ScheduleJobs(DateTime now)
        {
            if (scheduleJobBusy || Lockout.Active) return;
            List<ScheduleItem> missed = new List<ScheduleItem>();
            List<ScheduleItem> due = Scheduler.Take(Scheduler.FilePath, now, missed, true);
            foreach (ScheduleItem item in missed) ActivityLog.Write("schedule", "missed job", item.Job);
            if (due.Count == 0) return;
            scheduleJobBusy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> lines = new List<string>();
                foreach (ScheduleItem item in due)
                {
                    string done;
                    try { done = DoTool("use_computer", new ToolArguments { Task = item.Job, Asked = item.Job }); }
                    catch (Exception error) { done = "That didn't work: " + error.Message; }
                    lines.Add("The job you asked for (" + item.Text + ") ran: " + Pursuit.Shorter(string.IsNullOrWhiteSpace(done) ? "no result" : done, 1000));
                    ActivityLog.Write("schedule", "job", item.Job + " -> " + Pursuit.Shorter(done, 120));
                }
                string told = string.Join(" ", lines.ToArray());
                bool sent = false;
                if (Delivery.NotHome() && Delivery.Reachable())
                {
                    try { sent = Delivery.Send(told); }
                    catch (Exception error) { Brain.Log("schedule", -1, error.GetType().Name + ": " + error.Message); }
                }
                Post(delegate
                {
                    scheduleJobBusy = false;
                    brain.Own("reminder", told);
                    if (sent) Digest.Add("job", told, DateTime.Now);
                    if (!sent) Tell("(timed job)", told);
                });
            });
        }

        private void ForkScheduleTick()
        {
            if (testing) return;
            DateTime now = DateTime.Now;
            if ((now - lastScheduleCheck).TotalSeconds < ScheduleCheckSeconds) return;
            lastScheduleCheck = now;
            ScheduleJobs(now);
            if (Delivery.NotHome() && Delivery.Reachable())
            {
                ScheduleRemote(now);
                return;
            }
            if (!ScheduleMaySpeak()) return;
            List<ScheduleItem> due = ScheduleDue(Scheduler.FilePath, now);
            if (due.Count == 0) return;
            string line = "Reminder, choom: " + ScheduleTexts(due) + ".";
            if (hidden) SetHidden(false);
            heard = "(reminder)";
            box.Prompt = "> sys: ";
            brain.Own("reminder", line);
            ActivityLog.Write("schedule", "told", line);
            ScheduleParked(due, now);
            Answer(++asking, line, TalkState.Talking, chatting);
        }

        internal static string ClearedSaying(string path, DateTime now)
        {
            List<ScheduleItem> items = Scheduler.Load(path);
            if (items.Count == 0) return "You had no reminders to clear.";
            List<string> gone = new List<string>();
            foreach (ScheduleItem item in items) gone.Add(Scheduler.Describe(item, now));
            Scheduler.Clear(path);
            ActivityLog.Write("schedule", "cleared", string.Join(" | ", gone.ToArray()));
            return "Cleared " + items.Count + (items.Count == 1 ? " reminder: " : " reminders: ") + string.Join(". ", gone.ToArray()) + ".";
        }

        private bool ScheduleHeard(string text, bool voiceChat)
        {
            if (testing || string.IsNullOrWhiteSpace(text) || text.Length > 200) return false;
            DateTime now = DateTime.Now;
            string said;
            if (Scheduler.Listing(text))
            {
                List<ScheduleItem> items = Scheduler.Load(Scheduler.FilePath);
                List<string> told = new List<string>();
                foreach (ScheduleItem item in items) told.Add(Scheduler.Describe(item, now));
                said = items.Count == 0 ? "Nothing is scheduled." : "You have " + items.Count + ". " + string.Join(". ", told.ToArray()) + ".";
            }
            else if (Scheduler.Clearing(text))
            {
                said = ClearedSaying(Scheduler.FilePath, now);
            }
            else
            {
                ScheduleItem item = Scheduler.Parse(text, now);
                if (item == null) return false;
                Scheduler.Add(Scheduler.FilePath, item);
                PlanLinks.Seen(PlanLinks.Reminder, PlanLinks.IdOf(item.Text), Brain.Asking, item.Text, now);
                string described = Scheduler.Describe(item, now);
                ActivityLog.Write("schedule", "added", described);
                said = "Okay, " + described + ".";
                if (RemindTool.Dated(item, now) && RemindTool.CalendarOn())
                {
                    string title = item.Text;
                    DateTime at = item.Due;
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { RemindTool.OnCalendar(title, at, RemindTool.ReminderMinutes); });
                    said += " It's going on your Google Calendar too, with a phone alert.";
                }
            }
            if (!voiceChat) Tell("(reminder)", said);
            return true;
        }
    }
}
