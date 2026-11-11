using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class PhoneAction
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "kind")] public string Kind;
        [DataMember(Name = "at")] public string At;
        [DataMember(Name = "minutes")] public int Minutes;
        [DataMember(Name = "text")] public string Text;
        [DataMember(Name = "on")] public bool On;
        [DataMember(Name = "made")] public string Made;
        [DataMember(Name = "event")] public string Event;
    }

    internal static class PhoneActions
    {
        internal const int KeepHours = 24;
        internal const int MostMinutes = 1440;
        internal const int MostQueued = 20;
        private const int MostText = 120;
        private const int BackupMinutes = 15;
        private const string Stamp = "yyyy-MM-dd HH:mm";
        internal static readonly string[] Kinds = { "alarm", "timer", "reminder", "music", "focus" };
        internal const string Off = "Your phone link is off in Capri+ PHONE, so I can't reach your phone.";
        internal const string Full = "Your phone already has 20 things waiting. Run the Capri sync shortcut first.";
        internal const string Spec = "Set something on their iPhone that only the phone itself can do: a Clock alarm (action alarm with start), a timer (action timer with minutes), an entry in the iPhone Reminders app (action reminder with text and start), music played on the phone (action music, text is what to play) or a Focus turned on or off (action focus, text is the Focus name like Sleep or Do Not Disturb, with mode). Use it when they want it on their phone or iPhone, and for an alarm or timer they ask for from the Capri app on their iPhone. Work out the exact local time yourself from the current time you were given and put it in start, like 2026-11-05T07:00. The result says whether their phone sets it now or it waits for their phone (with a Google Calendar alert as backup); tell them exactly that, and never say it is set on the phone when it waits.";
        internal const string Schema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"What to set on the phone\",\"enum\":[\"alarm\",\"timer\",\"reminder\",\"music\",\"focus\"]},\"start\":{\"type\":\"string\",\"description\":\"For alarm and reminder, the exact local time, like 2026-11-05T07:00\"},\"minutes\":{\"type\":\"string\",\"description\":\"For timer, how many minutes\"},\"text\":{\"type\":\"string\",\"description\":\"The alarm's label, the reminder, what to play, or the Focus name\"},\"mode\":{\"type\":\"string\",\"description\":\"For focus, on or off\",\"enum\":[\"on\",\"off\"]}},\"required\":[\"action\"]}";
        internal static string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "phone-actions.json");
        internal static Func<bool> Linked = delegate { ForkSettings current = ForkSettings.Current; return current != null && current.PhoneOn; };
        internal static Func<bool> CalendarOn = delegate { return Google.Connected; };
        internal static Func<string, DateTime, int, string[]> Calendar = Google.CreateEvent;
        internal static Func<string, string> Uncalendar = delegate(string id) { return Google.Remove("event", id); };
        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;
        private static readonly object gate = new object();
        private static List<PhoneAction> handed;

        internal static bool Valid(ToolArguments args)
        {
            if (args == null) return false;
            string kind = args.Action ?? "";
            if (kind == "alarm") return Named(args.Start);
            if (kind == "timer") return Minutes(args.Minutes) > 0;
            if (kind == "reminder") return Named(args.Text) && Named(args.Start);
            if (kind == "music") return Named(args.Text);
            if (kind == "focus") return Named(args.Text) && (args.Mode == "on" || args.Mode == "off");
            return false;
        }

        internal static string Do(ToolArguments args, DateTime now)
        {
            if (!Valid(args)) return "Tell me what to set on your phone: an alarm and its time, a timer and its minutes, a reminder and its time, music, or a Focus on or off.";
            if (!Linked()) return Off;
            PhoneAction action = new PhoneAction { Id = NewId(), Kind = args.Action, Text = Clean(args.Text), Made = now.ToString(Stamp, Plain), On = args.Mode != "off" };
            DateTime at = DateTime.MinValue;
            if (Timed(action.Kind))
            {
                if (!RemindTool.Moment(args.Start, now, out at)) return "When? Give me the time, like 07:00 or tomorrow at 9.";
                if (at <= now) return "That time has already passed. When should it be?";
                action.At = at.ToString(Stamp, Plain);
            }
            if (action.Kind == "timer") action.Minutes = Minutes(args.Minutes);
            string shown = Describe(action, now);
            lock (gate)
            {
                if (handed != null)
                {
                    handed.Add(action);
                    ActivityLog.Write("phone", "action handed", action.Kind);
                    return "Your phone sets it now: " + shown + ".";
                }
                List<PhoneAction> queue = Fresh(Read(), now);
                if (queue.Count >= MostQueued) return Full;
                string backup = Timed(action.Kind) && CalendarOn() ? Backup(action, at) : "";
                queue.Add(action);
                Save(queue);
                ActivityLog.Write("phone", "action queued", action.Kind);
                return "Waiting for your phone: " + shown + ". It goes on when your Capri sync shortcut runs." + backup;
            }
        }

        internal static bool Begin()
        {
            lock (gate)
            {
                if (handed != null) return false;
                handed = new List<PhoneAction>();
                return true;
            }
        }

        internal static List<PhoneAction> End()
        {
            lock (gate)
            {
                List<PhoneAction> got = handed ?? new List<PhoneAction>();
                handed = null;
                return got;
            }
        }

        internal static List<PhoneAction> Pending(DateTime now)
        {
            lock (gate)
            {
                List<PhoneAction> all = Read();
                List<PhoneAction> kept = Fresh(all, now);
                if (kept.Count != all.Count) Save(kept);
                return kept;
            }
        }

        internal static List<string> Done(List<string> ids, DateTime now, out int count)
        {
            List<string> events = new List<string>();
            lock (gate)
            {
                List<PhoneAction> all = Read();
                List<PhoneAction> left = new List<PhoneAction>();
                foreach (PhoneAction one in all)
                {
                    if (!ids.Contains(one.Id)) { left.Add(one); continue; }
                    if (!string.IsNullOrEmpty(one.Event)) events.Add(one.Event);
                }
                count = all.Count - left.Count;
                if (count > 0) Save(Fresh(left, now));
            }
            if (count > 0) ActivityLog.Write("phone", "actions done", count.ToString(Plain));
            return events;
        }

        internal static List<string> Ids(string text)
        {
            List<string> ids = new List<string>();
            foreach (string part in (text ?? "").Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                if (Regex.IsMatch(part, @"\A[0-9a-f]{8}\z") && !ids.Contains(part)) ids.Add(part);
            return ids;
        }

        internal static string Json(List<PhoneAction> actions, DateTime now)
        {
            return "{\"actions\":" + List(actions, now) + "}";
        }

        internal static string List(List<PhoneAction> actions, DateTime now)
        {
            StringBuilder json = new StringBuilder("[");
            foreach (PhoneAction one in actions)
            {
                if (json.Length > 1) json.Append(',');
                DateTime at;
                string time = Moment(one.At, out at) ? at.ToString("HH:mm", Plain) : "";
                json.Append("{\"id\":").Append(PhoneServer.Json(one.Id)).Append(",\"kind\":").Append(PhoneServer.Json(one.Kind))
                    .Append(",\"at\":").Append(PhoneServer.Json(one.At ?? "")).Append(",\"time\":").Append(PhoneServer.Json(time))
                    .Append(",\"minutes\":").Append(one.Minutes.ToString(Plain)).Append(",\"text\":").Append(PhoneServer.Json(one.Text ?? ""))
                    .Append(",\"on\":").Append(one.On ? "true" : "false").Append(",\"said\":").Append(PhoneServer.Json(Describe(one, now))).Append('}');
            }
            return json.Append(']').ToString();
        }

        internal static string Describe(PhoneAction action, DateTime now)
        {
            DateTime at;
            string when = !Moment(action.At, out at) ? "" : at.Date == now.Date ? at.ToString("HH:mm", Plain) : at.Date == now.Date.AddDays(1) ? "tomorrow " + at.ToString("HH:mm", Plain) : at.ToString("ddd d MMM HH:mm", Plain);
            string text = action.Text ?? "";
            if (action.Kind == "alarm") return "alarm " + when + (text.Length > 0 ? " (" + text + ")" : "");
            if (action.Kind == "timer") return "timer " + action.Minutes.ToString(Plain) + " min";
            if (action.Kind == "reminder") return "reminder \"" + text + "\" " + when;
            if (action.Kind == "music") return "play " + text;
            return "Focus " + text + (action.On ? " on" : " off");
        }

        private static string Backup(PhoneAction action, DateTime at)
        {
            string clock = at.ToString("HH:mm", Plain);
            string title = action.Kind == "alarm" ? "Alarm " + clock + (action.Text.Length > 0 ? ": " + action.Text : "") : action.Text + " (" + clock + ")";
            string[] made;
            try { made = Calendar(title, at.AddMinutes(Google.AlertMinutes), BackupMinutes); }
            catch (Exception error) { made = new string[] { null, error.Message }; }
            if (made == null || made.Length < 2 || made[0] == null) return " The calendar backup didn't work: " + (made != null && made.Length > 1 ? made[1] : "no answer");
            action.Event = made[0];
            return " Google Calendar alert as backup.";
        }

        private static List<PhoneAction> Fresh(List<PhoneAction> all, DateTime now)
        {
            List<PhoneAction> kept = new List<PhoneAction>();
            foreach (PhoneAction one in all)
            {
                DateTime made, at;
                if (one == null || Array.IndexOf(Kinds, one.Kind) < 0 || string.IsNullOrEmpty(one.Id) || !Moment(one.Made, out made)) continue;
                if ((now - made).TotalHours >= KeepHours) continue;
                if (one.Kind == "alarm" && (!Moment(one.At, out at) || at <= now)) continue;
                if (one.Text == null) one.Text = "";
                kept.Add(one);
            }
            return kept;
        }

        private static List<PhoneAction> Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<PhoneAction>();
                using (FileStream stream = File.OpenRead(FilePath))
                {
                    List<PhoneAction> read = (List<PhoneAction>)new DataContractJsonSerializer(typeof(List<PhoneAction>)).ReadObject(stream);
                    return read ?? new List<PhoneAction>();
                }
            }
            catch (IOException) { return new List<PhoneAction>(); }
            catch (UnauthorizedAccessException) { return new List<PhoneAction>(); }
            catch (SerializationException) { return new List<PhoneAction>(); }
        }

        private static void Save(List<PhoneAction> actions)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                using (MemoryStream buffer = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(List<PhoneAction>)).WriteObject(buffer, actions);
                    File.WriteAllBytes(FilePath, buffer.ToArray());
                }
            }
            catch (IOException error) { ActivityLog.Write("phone", "actions not saved", error.GetType().Name); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("phone", "actions not saved", error.GetType().Name); }
        }

        private static bool Timed(string kind) { return kind == "alarm" || kind == "reminder"; }

        private static bool Named(string text) { return !string.IsNullOrWhiteSpace(text); }

        private static bool Moment(string text, out DateTime at)
        {
            return DateTime.TryParseExact(text ?? "", Stamp, Plain, DateTimeStyles.None, out at);
        }

        private static int Minutes(string text)
        {
            int minutes;
            if (!int.TryParse((text ?? "").Trim(), NumberStyles.Integer, Plain, out minutes)) return 0;
            return minutes >= 1 && minutes <= MostMinutes ? minutes : 0;
        }

        private static string Clean(string text)
        {
            StringBuilder kept = new StringBuilder();
            foreach (char one in Regex.Replace(text ?? "", @"\s+", " ").Trim())
                if (one >= ' ' && kept.Length < MostText) kept.Append(one);
            return kept.ToString();
        }

        private static string NewId()
        {
            byte[] random = new byte[4];
            using (RandomNumberGenerator maker = RandomNumberGenerator.Create()) maker.GetBytes(random);
            StringBuilder hex = new StringBuilder();
            foreach (byte one in random) hex.Append(one.ToString("x2", Plain));
            return hex.ToString();
        }
    }
}
