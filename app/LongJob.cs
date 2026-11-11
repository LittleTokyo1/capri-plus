using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class LongWork
    {
        [DataMember] public string Task;
        [DataMember] public string Mode;
        [DataMember] public string Watch;
        [DataMember] public long Started;
        [DataMember] public long Deadline;
        [DataMember] public long NextRun;
        [DataMember] public int Every;
        [DataMember] public int Chunks;
        [DataMember] public int Fails;
        [DataMember] public int Runs;
        [DataMember] public string Progress;
        [DataMember] public string State;
        [DataMember] public string Question;
        [DataMember] public string Answer;
        [DataMember] public string Last;
        [DataMember] public long AskedAt;
        [DataMember] public bool Browser;
    }

    [DataContract]
    internal sealed class LongStore
    {
        [DataMember] public LongWork Work;
        [DataMember] public long AnswerUntil;
    }

    // CAPRI-055: one long job on his PC at a time (run, repeat or watch), in parts of at most ChunkMinutes each, with its
    // state on disk so a restart carries on, plus a time window in which she answers his friends' DMs herself.
    internal static class LongJob
    {
        internal const int DefaultHours = 8;
        internal const int MostHours = 24;
        internal const int ChunkMinutes = 20;
        internal const int MostFails = 3;
        internal const int RetryMinutes = 1;
        internal const int RepeatMinutes = 60;
        internal const int WatchMinutes = 10;
        internal const int LeastEvery = 2;
        internal const int MostProgress = 1500;
        internal const int WrapGraceMinutes = 5;
        internal const int AnswerHours = 3;
        internal const string PartEnd = "That part of the long job ran long, so I ended it and start the next.";
        internal const string Running = "running", Paused = "paused", Waiting = "waiting", Asked = "asked";
        internal const string TimeUp = "Time's up for the long job, so I stopped.";
        internal const string StoppedByHim = "You said stop, so I stopped the long job.";
        internal const string Back = "You're back, so I paused the long job.";
        internal const string Wrap = "Capri's long-job timer: stop here for now. Call done, and start your text with SO FAR and say exactly where you got to and what is left, or with FINISHED if their whole job is done.";

        private static readonly object gate = new object();
        private static LongStore store;
        internal static string Path;
        internal static volatile bool Strict;

        internal static string FilePath
        {
            get { return Path ?? (ForkSettings.FilePath == null ? null : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ForkSettings.FilePath), "long-jobs.json")); }
        }

        private static LongStore Store()
        {
            if (store != null) return store;
            store = new LongStore();
            string path = FilePath;
            try
            {
                if (path != null && File.Exists(path))
                    using (FileStream file = File.OpenRead(path)) store = (LongStore)new DataContractJsonSerializer(typeof(LongStore)).ReadObject(file) ?? new LongStore();
            }
            catch (Exception error) { ActivityLog.Write("long job", "state unreadable", error.GetType().Name); store = new LongStore(); }
            return store;
        }

        private static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                using (MemoryStream data = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(LongStore)).WriteObject(data, store);
                    File.WriteAllBytes(path, data.ToArray());
                }
            }
            catch (Exception error) { ActivityLog.Write("long job", "state not saved", error.GetType().Name); }
        }

        internal static void Forget()
        {
            lock (gate) store = null;
        }

        internal static LongWork Current
        {
            get { lock (gate) return Store().Work; }
        }

        internal static void Change(Action<LongWork> change)
        {
            lock (gate)
            {
                LongWork work = Store().Work;
                if (work == null) return;
                change(work);
                Save();
            }
        }

        internal static void End()
        {
            lock (gate)
            {
                Store().Work = null;
                Save();
            }
            Strict = false;
        }

        internal static bool Active { get { return Current != null; } }

        internal const string StartNote = "No long job is running yet, and they asked you to start one: call long_job with action start, and fill mode, task, every, text and end from their words.";

        internal static bool Missed(List<KeyValuePair<string, ToolArguments>> calls)
        {
            if (calls == null || calls.Count == 0 || Current != null) return false;
            foreach (KeyValuePair<string, ToolArguments> call in calls)
                if (call.Key != "long_job" || call.Value == null || (call.Value.Action != "status" && call.Value.Action != "stop")) return false;
            return true;
        }

        internal static int AnswerMode(int saved, DateTime now)
        {
            lock (gate) return now.Ticks < Store().AnswerUntil ? 2 : saved;
        }

        internal static string Valid(ToolArguments args)
        {
            if (args == null) return "no arguments";
            string action = args.Action ?? "start";
            if (action == "stop" || action == "status") return null;
            if (action != "start") return "unknown action";
            string mode = string.IsNullOrEmpty(args.Mode) ? "run" : args.Mode;
            if (!Regex.IsMatch(mode, @"\A(run|repeat|watch|answer)\z")) return "unknown mode";
            if (mode != "answer" && string.IsNullOrWhiteSpace(args.Task)) return "no task";
            return null;
        }

        internal static DateTime Until(ToolArguments args, DateTime now)
        {
            DateTime end;
            if (!string.IsNullOrWhiteSpace(args.End) && DateTime.TryParse(args.End.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out end))
            {
                if (Regex.IsMatch(args.End.Trim(), @"^\d{1,2}(:\d{2}){0,2}\s*([ap]\.?m\.?)?$", RegexOptions.IgnoreCase)) end = now.Date + end.TimeOfDay;
                if (end <= now && args.End.IndexOf('-') < 0) end = end.AddDays(1);
                if (end > now) return end > now.AddHours(MostHours) ? now.AddHours(MostHours) : end;
            }
            double hours;
            if (!string.IsNullOrWhiteSpace(args.Hours) && double.TryParse(args.Hours.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out hours) && hours > 0)
                return now.AddMinutes(Math.Min(MostHours, hours) * 60);
            return now.AddHours(DefaultHours);
        }

        internal static int Minutes(string every, int fallback)
        {
            int minutes;
            if (string.IsNullOrWhiteSpace(every) || !int.TryParse(Regex.Match(every, @"\d+").Value, out minutes) || minutes <= 0) return fallback;
            if (Regex.IsMatch(every, @"\d\s*h|\bhours?\b", RegexOptions.IgnoreCase)) minutes *= 60;
            return Math.Max(LeastEvery, Math.Min(MostHours * 60, minutes));
        }

        internal static string Do(ToolArguments args, DateTime now)
        {
            string bad = Valid(args);
            if (bad != null) return "That long job call didn't work: " + bad + ".";
            string action = args.Action ?? "start";
            if (action == "status") return Status(now);
            if (action == "stop") return Stop(args.Mode == "answer");
            string mode = string.IsNullOrEmpty(args.Mode) ? "run" : args.Mode;
            DateTime until = Until(args, now);
            if (mode == "answer")
            {
                lock (gate) { Store().AnswerUntil = until.Ticks; Save(); }
                ActivityLog.Write("long job", "answer DMs", "until " + until.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                return "I'll answer your friends' Discord DMs myself until " + Clock(until, now) + ", and still tell you who wrote.";
            }
            LongWork work;
            lock (gate)
            {
                LongWork running = Store().Work;
                if (running != null) return "I'm already on a long job: " + Voice.Quote(running.Task) + ". Say stop first, or ask how it's going.";
                work = new LongWork
                {
                    Task = args.Task.Trim(),
                    Mode = mode,
                    Browser = args.App == "browser",
                    Watch = mode == "watch" ? (string.IsNullOrWhiteSpace(args.Text) ? args.Task : args.Text).Trim() : null,
                    Started = now.Ticks,
                    Deadline = until.Ticks,
                    NextRun = now.Ticks,
                    Every = mode == "repeat" ? Minutes(args.Every, RepeatMinutes) : mode == "watch" ? Minutes(args.Every, WatchMinutes) : 0,
                    State = Running
                };
                Store().Work = work;
                Save();
            }
            ActivityLog.Write("long job", "started " + mode, work.Task + " until " + until.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            string what = mode == "watch" ? "I'll check every " + Every(work.Every) + " for " + Voice.Quote(work.Watch) : mode == "repeat" ? "I'll do it every " + Every(work.Every) : "Started";
            return what + " until " + Clock(until, now) + ". I'll message you on Discord when it's done or if I'm stuck, and pause if you come back to the PC. Say stop to end it.";
        }

        internal static string Every(int minutes)
        {
            if (minutes % 60 == 0) return minutes == 60 ? "hour" : minutes / 60 + " hours";
            return minutes + " minutes";
        }

        internal static string Clock(DateTime at, DateTime now)
        {
            return at.ToString(at.Date == now.Date ? "HH:mm" : "ddd HH:mm", CultureInfo.InvariantCulture);
        }

        internal static string Stop(bool answering)
        {
            if (answering)
            {
                lock (gate) { Store().AnswerUntil = 0; Save(); }
                ActivityLog.Write("long job", "answer DMs", "stopped");
                return "Okay, I stopped answering your DMs.";
            }
            LongWork work = Current;
            if (work == null) return "There's no long job running.";
            End();
            ActivityLog.Write("long job", "stopped", work.Task);
            return "Stopped the long job " + Voice.Quote(work.Task) + "." + (string.IsNullOrEmpty(work.Progress) ? "" : " Where I got to: " + work.Progress);
        }

        internal static string Status(DateTime now)
        {
            LongWork work = Current;
            long answering;
            lock (gate) answering = Store().AnswerUntil;
            string dms = now.Ticks < answering ? " I'm answering your friends' DMs until " + Clock(new DateTime(answering), now) + "." : "";
            if (work == null) return "No long job is running." + dms;
            StringBuilder said = new StringBuilder();
            said.Append("Long job ").Append(Voice.Quote(work.Task)).Append(", since ").Append(Clock(new DateTime(work.Started), now)).Append(", until ").Append(Clock(new DateTime(work.Deadline), now)).Append(". ");
            if (work.State == Paused) said.Append("Paused: say carry on or stop. ");
            else if (work.State == Waiting || work.State == Asked) said.Append("Waiting for you: ").Append(work.Question).Append(' ');
            else if (work.Mode == "watch") said.Append("Checked ").Append(work.Runs).Append(work.Runs == 1 ? " time, not yet. " : " times, not yet. ");
            else if (work.Mode == "repeat") said.Append("Ran ").Append(work.Runs).Append(work.Runs == 1 ? " time. " : " times. ");
            else said.Append("Working, part ").Append(work.Chunks + 1).Append(". ");
            if (!string.IsNullOrEmpty(work.Progress)) said.Append("So far: ").Append(work.Progress);
            return said.ToString().Trim() + dms;
        }

        internal static bool Due(LongWork work, DateTime now)
        {
            return work != null && work.State == Running && now.Ticks >= work.NextRun && now.Ticks < work.Deadline;
        }

        internal static bool Over(LongWork work, DateTime now)
        {
            return work != null && now.Ticks >= work.Deadline;
        }

        internal static string Chunk(LongWork work)
        {
            if (work.Mode == "watch")
                return "Only look, change nothing and buy or send nothing: check whether this has happened yet: " + Voice.Quote(work.Watch) + ". Open or switch to what you need to see it. Then call done and start your text with YES and what you see if it has happened, or NO and what you see if not.";
            StringBuilder task = new StringBuilder(work.Task);
            if (work.Mode == "run" && !string.IsNullOrEmpty(work.Progress)) task.Append("\nThis is a long job you are doing in parts. Where the earlier parts got to: ").Append(work.Progress).Append("\nCarry on from what the screen shows now; don't redo finished steps.");
            if (!string.IsNullOrEmpty(work.Answer)) task.Append("\n").Append(work.Answer);
            return task.ToString();
        }

        internal static string Shorter(string text)
        {
            text = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return text.Length > MostProgress ? "..." + text.Substring(text.Length - MostProgress) : text;
        }

        // What happens after one part: the next state, and the report for him (null when there is nothing to tell).
        internal static string After(LongWork work, string outcome, string result, List<string> steps, bool wrapped, DateTime now)
        {
            string said = Regex.Replace(result ?? "", @"\s+", " ").Trim();
            work.Answer = null;
            work.Last = said;
            if (outcome == "waiting" || outcome == "asked")
            {
                work.State = outcome == "waiting" ? Waiting : Asked;
                work.Question = said;
                work.AskedAt = now.Ticks;
                return "Your long job " + Voice.Quote(work.Task) + " needs you: " + said + (outcome == "waiting" ? " Say yes or no." : " Answer here and I'll carry on.") + " Or say stop.";
            }
            if (work.Mode == "watch")
            {
                work.Runs++;
                if (outcome == "done" && Regex.IsMatch(said, @"^\W*yes\b", RegexOptions.IgnoreCase)) return Finish(work, "It happened: " + Regex.Replace(said, @"^\W*yes\W*", "", RegexOptions.IgnoreCase));
                if (outcome != "done" && ++work.Fails >= MostFails) return Finish(work, "I kept failing to check " + Voice.Quote(work.Watch) + ", so I stopped watching. Last: " + said);
                if (outcome == "done") work.Fails = 0;
                work.Progress = Shorter("Last check " + now.ToString("HH:mm", CultureInfo.InvariantCulture) + ": " + said);
                work.NextRun = now.AddMinutes(work.Every).Ticks;
                return null;
            }
            if (work.Mode == "repeat")
            {
                work.Runs++;
                work.Progress = Shorter("Run " + work.Runs + " at " + now.ToString("HH:mm", CultureInfo.InvariantCulture) + ": " + said);
                if (outcome != "done" && ++work.Fails >= MostFails) return Finish(work, "Your repeating job " + Voice.Quote(work.Task) + " failed " + MostFails + " times in a row, so I stopped it. Last: " + said);
                if (outcome == "done") work.Fails = 0;
                work.NextRun = now.AddMinutes(work.Every).Ticks;
                return null;
            }
            work.Chunks++;
            if (outcome == "done" && wrapped && !Regex.IsMatch(said, @"^\W*finished\b", RegexOptions.IgnoreCase))
            {
                work.Progress = Shorter(Regex.Replace(said, @"^\W*so far\W*", "", RegexOptions.IgnoreCase));
                work.Fails = 0;
                work.NextRun = now.Ticks;
                return null;
            }
            if (outcome == "done") return Finish(work, "Done: " + Regex.Replace(said, @"^\W*finished\W*", "", RegexOptions.IgnoreCase));
            work.Progress = Shorter(Trail(work.Progress, steps) + (said.Length > 0 ? " Then: " + said : ""));
            if (++work.Fails >= MostFails) return Finish(work, "I got stuck on your long job " + Voice.Quote(work.Task) + " " + MostFails + " times, so I stopped. Last: " + said);
            work.NextRun = now.AddMinutes(RetryMinutes).Ticks;
            return null;
        }

        internal static string Trail(string progress, List<string> steps)
        {
            string trail = steps == null || steps.Count == 0 ? "" : string.Join("; ", steps.GetRange(Math.Max(0, steps.Count - 8), Math.Min(8, steps.Count)).ToArray());
            return Shorter((string.IsNullOrEmpty(progress) ? "" : progress + " ") + trail);
        }

        private static string Finish(LongWork work, string report)
        {
            work.State = "finished";
            return report;
        }

        internal static string TimeUpReport(LongWork work)
        {
            if (work.Mode == "watch") return "I watched for " + Voice.Quote(work.Watch) + " until the time was up and it hadn't happened. " + work.Progress;
            if (work.Mode == "repeat") return "Your repeating job " + Voice.Quote(work.Task) + " is over: it ran " + work.Runs + (work.Runs == 1 ? " time. " : " times. ") + work.Progress;
            return "Time's up on your long job " + Voice.Quote(work.Task) + ". " + (string.IsNullOrEmpty(work.Progress) ? "" : "Where I got to: " + work.Progress);
        }

        internal static string Block(DateTime now)
        {
            LongWork work = Current;
            if (work == null) return "";
            return "\nA long job is going on their PC: " + Voice.Quote(work.Task) + " (" + work.Mode + ", " + work.State + ", until " + Clock(new DateTime(work.Deadline), now) + "). If they ask about it use the long_job tool with status, and to end it with stop; don't start another.";
        }

        private static readonly Regex StopWords = new Regex(@"^\W*(please\s+)?(stop|cancel|end|abort|kill)(\s+(the|that|your|my|this))?(\s+long)?(\s+(job|task|it|work))?(\s+(now|please))?\W*$", RegexOptions.IgnoreCase);
        private static readonly Regex StopAnswering = new Regex(@"\b(stop|quit|no more)\s+(answering|replying to)\b", RegexOptions.IgnoreCase);
        private static readonly Regex GoOn = new Regex(@"^\W*(ok(ay)?\s+|yes\s+|yeah\s+)?(carry on|continue|go on|keep going|resume|go ahead|you can (carry on|continue|go on))\b", RegexOptions.IgnoreCase);
        private static readonly Regex HowGoing = new Regex(@"\b(how('s| is| are)\s+(the|my|your|that)\s+(long\s+)?(job|task|work)\b|(long\s+)?job status|status of the (long\s+)?job|where are you (at|with) the (long\s+)?job)", RegexOptions.IgnoreCase);
        private static readonly Regex Yes = new Regex(@"^\W*(yes|yeah|yep|yup|ya|sure|ok|okay|do it|go ahead|go for it|send it|press it)\b[\s.!,]*(please|pls)?[\s.!]*$", RegexOptions.IgnoreCase);
        private static readonly Regex No = new Regex(@"^\W*(no|nah|nope|don't|do not|leave it|skip it)\b", RegexOptions.IgnoreCase);

        // His words about the long job, read in code before her brain: stop, carry on, yes, no, an answer, how is it going.
        internal static string Heard(string text, DateTime now)
        {
            string said = (text ?? "").Trim();
            if (said.Length == 0) return null;
            if (StopAnswering.IsMatch(said)) return "stop answering";
            LongWork work = Current;
            if (work == null) return null;
            if (StopWords.IsMatch(said)) return "stop";
            if (HowGoing.IsMatch(said)) return "status";
            if (work.State == Paused && GoOn.IsMatch(said)) return "carry on";
            if (work.State == Waiting && Yes.IsMatch(said)) return "yes";
            if (work.State == Waiting && No.IsMatch(said)) return "no";
            if (work.State == Asked && said.Length <= 400 && (now - new DateTime(work.AskedAt)).TotalHours < AnswerHours) return "answer";
            return null;
        }

        // While the DM window is open a friend's message is answered at once (in his quiet hours too); only the forward to him waits for the digest.
        internal static string AnswerRoute(string route, DateTime now)
        {
            return route == "wait" && AnswerMode(0, now) == 2 ? "digest" : route;
        }

        // Low-level hook flags: injected mouse 0x01, injected key 0x10. Her own SendInput is never him, and Esc stays her stop key.
        internal static bool Real(int flags, bool key, int vk)
        {
            if (key && vk == 0x1B) return false;
            return (flags & (key ? 0x10 : 0x01)) == 0;
        }

        internal const long AwayMilliseconds = 120000;

        internal static bool CameBack(long lastReal, long now)
        {
            return now - lastReal >= AwayMilliseconds;
        }
    }
}
