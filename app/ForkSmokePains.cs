using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static string PainTool(string name, string json)
        {
            return "{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"function\":{\"name\":\"" + name + "\",\"arguments\":" + json + "}}]},\"done_reason\":\"stop\"}";
        }

        private void SmokePains(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptFile = ForkSettings.FilePath;
            string keptLog = ActivityLog.FilePath;
            string keptStore = RemindTool.Store;
            string keptMemory = Brain.MemoryPath;
            string keptOllama = Brain.Ollama;
            string keptRoot = Vault.Root;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            Func<Cloud, string, string> keptDecide = SayDo.Decide;
            Func<Cloud, string, string> keptSummarize = SayDo.Summarize;
            Func<List<string>> keptRoots = Launchable.Roots;
            Action<string> keptStart = Launchable.Start;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            string folder = Path.Combine(Path.GetTempPath(), "capri-pains-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Brain.MemoryPath = Path.Combine(folder, "me.md");
                RemindTool.Store = Path.Combine(folder, "schedule.txt");
                PeopleNotes.Extract = delegate { return "NONE"; };
                PainQuick(checks);
                PainMemory(checks, folder);
                PainTimed(checks, folder);
                PainCalendar(checks);
                PainReport(checks);
                PainWords(checks);
                PainTurns(checks);
                PainLaunch(checks, folder);
                PainSmall(checks, folder);
            }
            finally
            {
                Quick.Post = keptPost;
                SayDo.Decide = keptDecide;
                SayDo.Summarize = keptSummarize;
                Launchable.Roots = keptRoots;
                Launchable.Start = keptStart;
                PeopleNotes.Extract = keptExtract;
                Brain.Ollama = keptOllama;
                Brain.MemoryPath = keptMemory;
                Vault.Root = keptRoot;
                RemindTool.Store = keptStore;
                ActivityLog.FilePath = keptLog;
                ForkSettings.FilePath = keptFile;
                ForkSettings.Current = keptSettings;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private static void PainQuick(List<string> checks)
        {
            Cloud claude = new Cloud("sk-ant-test", "claude-sonnet-5-5");
            List<string> bodies = new List<string>();
            int status;
            Quick.Post = delegate(string body, string key)
            {
                bodies.Add(body);
                return Tuple.Create(200, "{\"content\":[{\"type\":\"server_tool_use\",\"id\":\"x\",\"name\":\"web_search\"}],\"stop_reason\":\"pause_turn\",\"usage\":{\"input_tokens\":10,\"output_tokens\":7}}");
            };
            string none = Quick.Ask(claude, "sys", Voice.Quote("hi"), "[{\"type\":\"web_search_20250305\",\"name\":\"web_search\"}]", 100, out status);
            string log = File.ReadAllText(ActivityLog.FilePath);
            if (none != null || bodies.Count != 2 || !bodies[0].Contains("\"tools\"") || bodies[1].Contains("\"tools\"") || !bodies[1].Contains("could not search the web")) throw new Exception("A reply with no text was not retried once without the search tool: " + bodies.Count);
            if (!log.Contains("quick call  empty reply  ->  stop pause_turn, parts server_tool_use, 7 tokens") || !log.Contains("quick call  retry without tools")) throw new Exception("A reply with no text left no reason in the activity log: " + log);
            Quick.Post = delegate(string body, string key) { return Tuple.Create(200, "{\"content\":[{\"type\":\"text\",\"text\":\"cut off ans\"}],\"stop_reason\":\"max_tokens\",\"usage\":{\"input_tokens\":10,\"output_tokens\":900}}"); };
            string cut = Quick.Ask(claude, "sys", Voice.Quote("hi"), null, 100, out status);
            if (cut != "cut off ans" || Quick.Stopped != "max_tokens") throw new Exception("A reply that ran out of tokens was not marked: " + Quick.Stopped);
            Quick.Post = delegate(string body, string key) { return Tuple.Create(500, (string)null); };
            string failed = Quick.Ask(claude, "sys", Voice.Quote("hi"), null, 100, out status);
            if (failed != null || Quick.Stopped != null || status != 500 || !File.ReadAllText(ActivityLog.FilePath).Contains("quick call  failed  ->  HTTP 500")) throw new Exception("A failed call left no line in the activity log.");
            if (Quick.NotSent(null) != "no brain set up" || Quick.NotSent(new Cloud("sk-proj-test", "gpt-5-mini")) != "the brain is not Claude" || Quick.NotSent(claude) != null) throw new Exception("The reason a call is not sent was wrong.");
            checks.Add("PASS quick calls say why they got nothing: a reply with no text is logged with how it stopped, which parts it had and its tokens, and retried once without the search tool; a reply cut by the token limit is marked so offers, check-ins and game lines that were cut off are dropped; a failed call and a call that was never sent are logged with their reason");
        }

        private static void PainMemory(List<string> checks, string folder)
        {
            string vault = Path.Combine(folder, "vault");
            Names.Reset();
            if (!Vault.Open(vault, null)) throw new Exception("The memory vault did not open.");
            Brain.MemoryPath = Path.Combine(vault, Vault.CoreName);
            string reply = null;
            PeopleNotes.Extract = delegate { return reply; };
            const string said = "My sister's name is Freya and she moved to Utrecht last week";
            if (PeopleNotes.Learn(null, said, null) != null || PeopleNotes.Pending != 1) throw new Exception("A pass with no reply was not kept for a retry.");
            if (!File.ReadAllText(ActivityLog.FilePath).Contains("memory  pass  ->  no reply (")) throw new Exception("The memory pass did not say why it got no reply.");
            ForkSettings.Current.SummarizeChats = false;
            PeopleNotes.Learn(null, "I start work at nine on Mondays at the rental company", null);
            if (!File.ReadAllText(ActivityLog.FilePath).Contains("no reply (notes after chats are off)")) throw new Exception("A pass with notes after chats off did not name that as the reason.");
            ForkSettings.Current.SummarizeChats = true;
            reply = "NONE";
            for (int i = 0; i < 4 && PeopleNotes.Pending > 0; i++) PeopleNotes.RetryDue(null, DateTime.Now.AddMinutes(60));
            if (PeopleNotes.Pending != 0) throw new Exception("The kept passes were not retried after she woke.");
            checks.Add("PASS memory passes with no model reply: the activity log says why (notes after chats off, no brain, spend cap, or no text), the pass is kept for a retry, and being paused no longer stops what he tells her over Discord from being remembered");
        }

        private static void PainTimed(List<string> checks, string folder)
        {
            DateTime now = new DateTime(2026, 10, 30, 12, 25, 0);
            string store = RemindTool.Path;
            ToolArguments job = new ToolArguments { Action = "add", Start = "2026-10-30T12:43", Text = "resume where you left off", Task = "open the Claude window and send resume where you left off" };
            if (!Hands.Valid("reminder", job) || Hands.Valid("reminder", new ToolArguments { Action = "add", Text = "x" }) || Hands.Valid("reminder", new ToolArguments { Action = "add", Start = "2026-10-30T12:43" })) throw new Exception("The reminder tool took a call it should not, or refused a good one.");
            string set = RemindTool.Do(job, now);
            if (set != "Set: 12:43 today: resume where you left off (a job I run then).") throw new Exception("A timed job was set wrong: " + set);
            List<ScheduleItem> kept = Scheduler.Load(store);
            if (kept.Count != 1 || kept[0].Job != "open the Claude window and send resume where you left off" || kept[0].Text != "resume where you left off") throw new Exception("A timed job was not stored with its job.");
            List<ScheduleItem> missed = new List<ScheduleItem>();
            if (Scheduler.Take(store, now.AddMinutes(20), missed).Count != 0 || Scheduler.Load(store).Count != 1) throw new Exception("A job was spoken as a plain reminder.");
            if (Scheduler.Take(store, now.AddMinutes(17), missed, true).Count != 0) throw new Exception("A job came before its time.");
            List<ScheduleItem> due = Scheduler.Take(store, now.AddMinutes(19), missed, true);
            if (due.Count != 1 || !due[0].Job.Contains("Claude window") || Scheduler.Load(store).Count != 0) throw new Exception("A job was not taken once at its time.");
            string plain = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-10-30T13:00", Text = "call him" }, now);
            if (plain != "Set: 13:00 today: call him." || Scheduler.Take(store, now.AddMinutes(40), missed, true).Count != 0 || Scheduler.Take(store, now.AddMinutes(40), missed).Count != 1) throw new Exception("A plain reminder was set or taken wrong: " + plain);
            string old = Path.Combine(folder, "old-schedule.txt");
            File.WriteAllText(old, "abc12345\t2026-10-30 12:40\t\tcall mum\n");
            List<ScheduleItem> loaded = Scheduler.Load(old);
            if (loaded.Count != 1 || loaded[0].Text != "call mum" || loaded[0].Job.Length != 0) throw new Exception("An old four-column schedule file did not load.");
            if (!RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-10-30T12:00", Text = "too late" }, now).Contains("already passed") || !RemindTool.Do(new ToolArguments { Action = "add", Start = "soon", Text = "x" }, now).StartsWith("When?", StringComparison.Ordinal)) throw new Exception("A time in the past or no real time was accepted.");
            string weekly = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-10-31T09:00", Text = "plan the week", Every = "mon" }, now);
            if (weekly != "Set: every Monday at 09:00: plan the week.") throw new Exception("A weekly reminder was set wrong: " + weekly);
            if (!RemindTool.Do(new ToolArguments { Action = "list" }, now).StartsWith("You have 1.", StringComparison.Ordinal) || !Hands.Do("reminder", new ToolArguments { Action = "clear" }).StartsWith("Cleared 1 reminder: ", StringComparison.Ordinal) || Hands.Do("reminder", new ToolArguments { Action = "clear" }) != "You had no reminders to clear." || Hands.Do("reminder", new ToolArguments { Action = "list" }) != "Nothing is scheduled.") throw new Exception("Listing or clearing reminders through the tool was wrong.");
            if (!Hands.Tools.Contains("\"reminder\"") || !Hands.ClaudeTools.Contains("\"name\":\"reminder\"") || !Fork.HandsRules.Contains("reminder tool")) throw new Exception("The reminder tool or the rule to use it is not offered to her.");
            checks.Add("PASS timed jobs and reminders as a tool: she can set a reminder or a job for a time she worked out herself (in 18 minutes, tomorrow, every Monday), the schedule stores the job beside the text and old files still load, a job is taken once at its time and never spoken as a plain reminder, a past or missing time is refused, and listing and clearing work");
        }

        private static void PainCalendar(List<string> checks)
        {
            DateTime now = new DateTime(2026, 10, 30, 12, 25, 0);
            string store = RemindTool.Path;
            Func<string, DateTime, int, string[]> kept = RemindTool.Calendar;
            List<string> calls = new List<string>();
            try
            {
                RemindTool.Calendar = delegate(string title, DateTime start, int minutes) { calls.Add(title + " " + start.ToString("yyyy-MM-dd HH:mm") + " " + minutes); return new[] { "evt9", start.ToString("ddd d MMM HH:mm") }; };
                Scheduler.Clear(store);
                if (!Hands.Valid("reminder", new ToolArguments { Action = "add", Text = "dentist", Appointment = "2026-11-02T15:00" }) || Hands.Valid("reminder", new ToolArguments { Action = "add", Appointment = "2026-11-02T15:00" })) throw new Exception("An appointment alone must be a valid reminder call, but not without text.");
                string both = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T14:00", Text = "dentist", Appointment = "2026-11-02T15:00" }, now);
                if (!both.StartsWith("Set: ", StringComparison.Ordinal) || !both.Contains("Google Calendar") || !both.Contains("phone alert") || !both.Contains(Google.AlertMinutes + " minutes before") || calls.Count != 1 || calls[0] != "dentist 2026-11-02 15:00 60") throw new Exception("An appointment did not reach the calendar for the hour from its time: " + both + " / " + string.Join("|", calls.ToArray()));
                List<ScheduleItem> kept1 = Scheduler.Load(store);
                if (kept1.Count != 1 || kept1[0].Due != new DateTime(2026, 11, 2, 14, 0, 0)) throw new Exception("The Discord reminder was not kept at the time he gave.");
                Scheduler.Clear(store);
                calls.Clear();
                RemindTool.Do(new ToolArguments { Action = "add", Text = "exam", Appointment = "2026-11-02T15:00" }, now);
                List<ScheduleItem> early = Scheduler.Load(store);
                if (early.Count != 1 || early[0].Due != new DateTime(2026, 11, 2, 14, 30, 0) || calls.Count != 1) throw new Exception("With only an appointment time the reminder was not " + Google.AlertMinutes + " minutes before it.");
                Scheduler.Clear(store);
                RemindTool.Do(new ToolArguments { Action = "add", Text = "call", Appointment = "2026-10-30T12:40" }, now);
                List<ScheduleItem> soon = Scheduler.Load(store);
                if (soon.Count != 1 || soon[0].Due != new DateTime(2026, 10, 30, 12, 40, 0)) throw new Exception("An appointment less than the alert time away must remind at the appointment.");
                Scheduler.Clear(store);
                calls.Clear();
                RemindTool.Calendar = delegate(string title, DateTime start, int minutes) { return new string[] { null, Google.NotConnected }; };
                string failed = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T14:00", Text = "dentist", Appointment = "2026-11-02T15:00" }, now);
                if (!failed.StartsWith("Set: ", StringComparison.Ordinal) || !failed.Contains("couldn't put it on your Google Calendar") || !failed.Contains("not signed in") || Scheduler.Load(store).Count != 1) throw new Exception("A failed calendar add hid the reminder, or was said as done: " + failed);
                Scheduler.Clear(store);
                RemindTool.Calendar = delegate(string title, DateTime start, int minutes) { throw new InvalidOperationException("boom"); };
                if (!RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T14:00", Text = "dentist", Appointment = "2026-11-02T15:00" }, now).Contains("couldn't put it on your Google Calendar") || Scheduler.Load(store).Count != 1) throw new Exception("An error from the calendar lost the reminder.");
                Scheduler.Clear(store);
                RemindTool.Calendar = delegate(string title, DateTime start, int minutes) { calls.Add(title); return new[] { "evt9", "x" }; };
                if (!RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T14:00", Text = "dentist", Appointment = "2026-10-29T15:00" }, now).Contains("already passed") || !RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T14:00", Text = "dentist", Appointment = "someday" }, now).StartsWith("When is the appointment", StringComparison.Ordinal) || Scheduler.Load(store).Count != 0 || calls.Count != 0) throw new Exception("A past or unreadable appointment was accepted.");
                RemindTool.CalendarOn = delegate { return false; };
                string plain = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T14:00", Text = "take pills" }, now);
                string weekly = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T14:00", Text = "gym", Every = "mon", Appointment = "2026-11-02T15:00" }, now);
                if (plain.Contains("Calendar") || weekly.Contains("on your Google Calendar") || !weekly.Contains("calendar") || calls.Count != 0) throw new Exception("A reminder reached the calendar while Google was signed out, or a repeating one did: " + plain + " / " + weekly);
                RemindTool.CalendarOn = delegate { return true; };
                calls.Clear();
                string later = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T09:00", Text = "register for Dutch" }, now);
                string today = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-10-30T18:00", Text = "stretch" }, now);
                string jobbed = RemindTool.Do(new ToolArguments { Action = "add", Start = "2026-11-02T10:00", Text = "check the download", Task = "open qBittorrent" }, now);
                if (!later.Contains("on your Google Calendar") || today.Contains("Calendar") || jobbed.Contains("Calendar") || calls.Count != 1 || calls[0] != "register for Dutch") throw new Exception("A timed reminder on a later day did not reach the calendar, or a same-day or job one did: " + later + " / " + today + " / " + string.Join("|", calls.ToArray()));
                if (!Hands.Tools.Contains("appointment") || !Hands.ClaudeTools.Contains("\"appointment\"") || Fork.MessageLimit < 100000) throw new Exception("The reminder tool does not offer the appointment field, or the message limit is wrong.");
                if (!Hands.Tools.Contains("never add_event as well") || !Hands.Tools.Contains("remind them " + Google.AlertMinutes + " minutes before")) throw new Exception("The google tool does not warn against a second event, or the reminder tool's alert time is not built from the constant.");
            }
            finally
            {
                RemindTool.Calendar = kept;
                RemindTool.CalendarOn = delegate { return Google.Connected; };
                Scheduler.Clear(store);
            }
            checks.Add("PASS calendar alerts: a reminder the model marks as an appointment also becomes a one hour Google Calendar event with a phone popup " + Google.AlertMinutes + " minutes before (timed events only), the Discord reminder stays at the time he gave (or " + Google.AlertMinutes + " minutes before the appointment when only that is known), a failed or crashing calendar add is said plainly and keeps the reminder, a past or unreadable appointment is refused, a one-off timed reminder on a later day also goes on the calendar with the same popup while Google is signed in, same-day, job and repeating reminders stay off it; typed and Discord messages may be 1000 characters");
        }

        private static void PainReport(List<string> checks)
        {
            string list = "Satisfactory 11.2 GB, ProjectZomboid 10.3 GB, P3R 18.9 GB, Lethal Company 3.6 GB, IRON NEST 3.6 GB, Plan B Terraform 2.3 GB, Slay the Spire 2.8 GB, Castlevania Belmont's Curse 5.1 GB, " + new string('x', 700);
            string kept = Job.Report(list, "Done.");
            if (kept != list || list.Length < 800) throw new Exception("A long job report was cut: " + kept.Length + " of " + list.Length);
            string huge = Job.Report(new string('y', 4000), "Done.");
            if (huge.Length != 1500 || !huge.EndsWith("...", StringComparison.Ordinal)) throw new Exception("A huge job report was not capped at 1500: " + huge.Length);
            if (Job.Report("  a \n  b ", "Done.") != "a b" || Job.Report("", "Done.") != "Done." || Job.Report(null, "x") != "x") throw new Exception("A short or empty job report was handled wrong.");
            checks.Add("PASS job reports: what a PC job says it found or did is kept up to 1500 characters (it was cut at 200, which cut her game-size list on Discord), a longer one ends in dots, an empty one says Done");
            int keptWordiness = Brain.Wordiness;
            try
            {
                Brain.Wordiness = 0;
                string games = "Here is the list: ";
                for (int i = 0; i < 60; i++) games += "Game" + i + " " + (i + 1) + ".9 GB, ";
                games = games.TrimEnd(' ', ',') + ".";
                if (games.Length < 700 || Brain.Clean(games) != games) throw new Exception("Even on the shortest reply length a long list was cut: " + Brain.Clean(games).Length + " of " + games.Length);
                string endless = "";
                for (int i = 0; i < 400; i++) endless += "Game" + i + " " + (i + 1) + ".9 GB, ";
                string cut = Brain.Clean(endless);
                if (cut.Length > 1903 || cut.Length < 1000 || System.Text.RegularExpressions.Regex.IsMatch(cut, @"\d\.$")) throw new Exception("A very long list was cut inside a number: " + cut.Substring(Math.Max(0, cut.Length - 30)));
            }
            finally { Brain.Wordiness = keptWordiness; }
            if (!Hands.Tools.Contains("run a use_computer job that scans the disk")) throw new Exception("The look tool does not say a full list needs a scan job.");
            checks.Add("PASS reply length: even on Short a reply may hold a long list (about 1900 characters; it was 220, which cut her list of games), and a longer one is cut at a full stop or a space, never at the decimal point inside a number");
        }

        private static void PainWords(List<string> checks)
        {
            foreach (string claim in new[] { "Yezzirrr, I'm on it: going into capri-automation to right-click start-capri.ps1 and run it with PowerShell. Sorry for the gonk loop.", "On it. I'll set a reminder for 12:40 to open the Claude window and send \"resume where you left off\".", "Reminder's set for 12:40. I'll open the Claude window and send it.", "You're right, choom, I said On it and never actually ran it. I'll go run start-capri.ps1 properly now.", "I'll go run start-capri.ps1." })
                if (!SayDo.Claims(claim)) throw new Exception("Today's claim with no action slipped through: " + claim);
            foreach (string talk in new[] { "Doctor first is the right call, so get seen and let OWR know you're out.", "Ha, takes one to know one.", "Aw, that's rough. Drink water and rest up. Want me to keep it quiet while you lie down?", "Morning. Still running on fumes, but I'm good.", "Calendar's clear today. Your inbox has capri CI runs failing again." })
                if (SayDo.Claims(talk)) throw new Exception("Plain talk counted as a claim: " + talk);
            if (!SayDo.Claims("You're right, I only looked at monitor one. Checking monitor two now, choom.")) throw new Exception("\"Checking monitor two now\" with no look behind it slipped through.");
            if (SayDo.Claims("Looking at your screen, the League client isn't showing an update bar I can see, so I can't tell what's stuck from here.")) throw new Exception("Describing the screen counted as a claim.");
            foreach (string job in new[] { "Okay. Can you filter my Steam library by installed games?", "order me a pizza with the upsize coupon from newyork pizza", "download the patch notes" })
                if (!Hands.Wants(job)) throw new Exception("Asking her to do this was not taken as a job: " + job);
            foreach (string talk in new[] { "I ordered a pizza earlier", "the filter is weird", "should I buy it?", "Delete the metalhead thing from your notes.", "disable check-ins" })
                if (Hands.Wants(talk)) throw new Exception("Plain talk was taken as a job: " + talk);
            foreach (string managing in new[] { "cancel the reminder", "clear my reminders", "delete that reminder", "list my routines" })
                if (!SayDo.Scheduling(managing)) throw new Exception("Managing a reminder could turn into a mouse job after a refusal: " + managing);
            if (SayDo.Scheduling("the reminder app is weird") || SayDo.Scheduling("cancel my order")) throw new Exception("Talk about a reminder or a cancel counted as managing reminders.");
            foreach (string refusal in new[] { "Yezzirrr, I can filter it for you, but I can't click through Steam from here without doing it blind. Click the Games and Software dropdown at the top left of the library, pick Installed, and I'll read what's left.", "Ha, you caught me, choom. The Games and Software filter is still set to all, so the Installed step is up to you. Click that dropdown and pick Installed.", "I can’t type into that box for you, so you'll have to paste it yourself." })
                if (!Hands.Refuses(refusal)) throw new Exception("Her refusal to do the job he asked for was missed: " + refusal);
            foreach (string fine in new[] { "Killing League in Task Manager is fine, but close League first.", "I can't see which shops near you stock it from that screenshot.", "Give it a minute and it should finish on its own.", "Click play when you're ready, choom.", "Ha, takes one to know one." })
                if (Hands.Refuses(fine)) throw new Exception("Plain talk counted as a refusal: " + fine);
            foreach (string complaint in new[] { "you said that you're doing it, but you didn't do it bug", "you didn\u2019t send the message to claude and you also didn\u2019t inform me. bug.", "now when I called tou out you should have went and fixed your mistake but you didnt. bug .", "you forgot to remind me", "nothing happened" })
                if (!SayDo.Complains(complaint)) throw new Exception("A complaint that she did not do it was missed: " + complaint);
            foreach (string fine in new[] { "thanks works now. still needs to be fixed so it works from the first time bug", "ur goated", "no, it's okay. It's just a reminder. I already did it", "Yeah, mm I think I'm gonna go to the doctor first" })
                if (SayDo.Complains(fine)) throw new Exception("Thanks or chat counted as a complaint: " + fine);
            foreach (string later in new[] { "in 18minutes my claude usage resumes, when that happens can you open the claude window and send a message \u201cresume where you left off\u201d thanks.", "yeah, I need to remember to call him eventually can you add me a reminder?", "remind me at 5 to stretch", "tomorrow at 9 can you send mum a message happy birthday", "at 6pm please open spotify" })
                if (!SayDo.Scheduling(later)) throw new Exception("A request for later was taken as now: " + later);
            foreach (string soon in new[] { "I work nine to five every day at OBR", "send me a screenshot of my second monitor", "what time is it at 5", "I will be there in 5 minutes", "my sister's birthday is in 8 days", "hello there" })
                if (SayDo.Scheduling(soon)) throw new Exception("Plain talk was taken as a request for later: " + soon);
            List<ChatMessage> started = new List<ChatMessage>
            {
                new ChatMessage("user", "start capri ps1"), new ChatMessage("assistant", "No app called capri ps1 is installed on this PC."),
                new ChatMessage("user", "we talked about this it means go to capri automation and select the capri ps1 file and eun with powershell"), new ChatMessage("assistant", "Yezzirrr, I'm on it: going into capri-automation to run it.")
            };
            string redo = SayDo.Redo("you said that you're doing it, but you didn't do it bug", started);
            if (redo == null || !redo.Contains("start capri ps1") || !redo.Contains("capri automation")) throw new Exception("A complaint did not bring back what he asked: " + redo);
            if (SayDo.Redo("thanks works now. still needs to be fixed so it works from the first time bug", started) != null) throw new Exception("Thanks re-ran the ask.");
            List<ChatMessage> chat = new List<ChatMessage> { new ChatMessage("user", "hey how are you"), new ChatMessage("assistant", "good, choom") };
            if (SayDo.Redo("you didn't do it", chat) != null) throw new Exception("A complaint with no ask before it re-ran something.");
            List<ChatMessage> timer = new List<ChatMessage>
            {
                new ChatMessage("user", "in 18minutes my claude usage resumes, when that happens can you open the claude window and send a message \u201cresume where you left off\u201d thanks."), new ChatMessage("assistant", "On it. I'll set a reminder for 12:40."),
                new ChatMessage("user", "and then let me know when thats done"), new ChatMessage("assistant", "Reminder's set for 12:40.")
            };
            string again = SayDo.Redo("you didn\u2019t send the message to claude and you also didn\u2019t inform me. bug.", timer);
            if (again == null || !again.Contains("18minutes") || !again.Contains("let me know")) throw new Exception("The timed ask was not brought back whole: " + again);
            foreach (string vague in new[] { "Remember it the stuff they said about ps1 capri file", "the stuff I said about ps1 capri file", "that", "it" })
                if (!SayDo.Vague(vague)) throw new Exception("A note that names no fact was taken as a fact: " + vague);
            foreach (string fact in new[] { "They main Jett", "Their sister's name is Freya", "start capri ps1 means run start-capri.ps1" })
                if (SayDo.Vague(fact)) throw new Exception("A real fact was taken as vague: " + fact);
            if (SayDo.Parse("CHAT") != null || SayDo.Parse("garbage") != null || SayDo.Parse("REMIND: only one") != null || SayDo.Parse(null) != null) throw new Exception("A judge answer with nothing to do made a plan.");
            SayDoPlan question = SayDo.Parse("ASK: What time should I remind you, choom?");
            if (question == null || question.Kind != SayDoPlan.Ask || question.Task != "What time should I remind you, choom?") throw new Exception("A question from the judge was not read.");
            SayDoPlan act = SayDo.Parse("ACT: Run start-capri.ps1 from capri-automation");
            SayDoPlan remind = SayDo.Parse("REMIND: 2026-10-30T12:43 | resume where you left off | open the Claude window and send it | -");
            SayDoPlan daily = SayDo.Parse("remind: 2026-10-31T09:00 | take pills | - | daily");
            if (act == null || act.Kind != SayDoPlan.Act || act.Task != "Run start-capri.ps1 from capri-automation" || remind == null || remind.Kind != SayDoPlan.Remind || remind.Reminder.Start != "2026-10-30T12:43" || remind.Reminder.Task != "open the Claude window and send it" || remind.Reminder.Every != null || daily == null || daily.Reminder.Every != "daily" || daily.Reminder.Task != null) throw new Exception("A judge answer was read wrong.");
            string frame = SayDo.Frame(new DateTime(2026, 10, 30, 12, 42, 0), SayDo.Talk(started), "you didn't do it", "On it.", true);
            if (!frame.Contains("complaining") || !frame.Contains("Capri just said: On it.") || !frame.Contains("Him: start capri ps1")) throw new Exception("The judge's frame lacks the talk, her words or the complaint: " + frame);
            checks.Add("PASS claims and complaints: today's words (\"Yezzirrr, I'm on it: going into...\", \"Reminder's set\", \"I'll go run\") count as a claim and plain talk (\"Doctor first is the right call\", \"Ha, takes one to know one\") does not; \"you said but you didn't\" and \"bug\" next to a negative bring back the earlier ask whole while thanks and \"works now ... bug\" do not; requests for later are told from requests for now; \"the stuff I said about X\" is known to name no fact; the judge's answers are read into act or remind plans");
        }

        private static void PainTurns(List<string> checks)
        {
            List<string> frames = new List<string>();
            List<string> asked = new List<string>();
            System.Net.Sockets.TcpListener fake;
            Brain.Ollama = FakeServer(new[] { SmokeSay("Yezzirrr, I'm on it: going into capri-automation to run it."), SmokeSay("chat"), SmokeSay("Ran start-capri.ps1 for you.") }, asked, out fake);
            try
            {
                List<string> jobs = new List<string>();
                SayDo.Decide = delegate(Cloud remote, string frame) { frames.Add(frame); return "ACT: Find start-capri.ps1 in capri-automation and run it with PowerShell"; };
                Brain claimer = new Brain();
                claimer.Model = "pains-test";
                claimer.Act = delegate(string tool, ToolArguments args) { jobs.Add(tool + "|" + args.Task); return "Ran start-capri.ps1 from C:\\x."; };
                string said = claimer.Ask("it is the script file we talked about, you know the one");
                if (said != "Ran start-capri.ps1 for you." || jobs.Count != 1 || !jobs[0].StartsWith("use_computer|", StringComparison.Ordinal) || !jobs[0].Contains("start-capri.ps1") || frames.Count != 1 || !frames[0].Contains("Capri just said: Yezzirrr, I'm on it")) throw new Exception("A claim with no tool did not become a job: " + said + " / " + string.Join(" ; ", jobs.ToArray()));
            }
            finally { fake.Stop(); }

            asked.Clear();
            frames.Clear();
            Brain.Ollama = FakeServer(new[] { SmokeSay("I'll go easy on you, choom. Rest up.") }, asked, out fake);
            try
            {
                List<string> jobs = new List<string>();
                SayDo.Decide = delegate(Cloud remote, string frame) { frames.Add(frame); return "CHAT"; };
                Brain talker = new Brain();
                talker.Model = "pains-test";
                talker.Act = delegate(string tool, ToolArguments args) { jobs.Add(tool); return "Did it."; };
                string said = talker.Ask("I feel sick today");
                if (said != "I'll go easy on you, choom. Rest up." || jobs.Count != 0 || frames.Count != 1) throw new Exception("Plain talk with a claim-like word became a job or was changed: " + said);
            }
            finally { fake.Stop(); }

            asked.Clear();
            frames.Clear();
            Brain.Ollama = FakeServer(new[] { SmokeSay("Set, I'll tell you when it has run.") }, asked, out fake);
            try
            {
                List<string> acts = new List<string>();
                string start = DateTime.Now.AddMinutes(18).ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                SayDo.Decide = delegate(Cloud remote, string frame) { frames.Add(frame); return "REMIND: " + start + " | resume where you left off | open the Claude window and send resume where you left off | -"; };
                Brain scheduler = new Brain();
                scheduler.Model = "pains-test";
                scheduler.Act = delegate(string tool, ToolArguments args) { acts.Add(tool); return Hands.Do(tool, args); };
                string said = scheduler.Ask("in 18 minutes can you open the claude window and send resume where you left off");
                List<ScheduleItem> stored = Scheduler.Load(RemindTool.Path);
                if (said != "Set, I'll tell you when it has run." || acts.Count != 1 || acts[0] != "reminder" || stored.Count != 1 || !stored[0].Job.Contains("Claude window") || (stored[0].Due - DateTime.Now).TotalMinutes < 15 || asked.Count != 1) throw new Exception("A request for later did not become a scheduled job: " + said + " / " + string.Join(",", acts.ToArray()) + " / " + stored.Count);
                Scheduler.Clear(RemindTool.Path);
            }
            finally { fake.Stop(); }

            asked.Clear();
            frames.Clear();
            Brain.Ollama = FakeServer(new[] { SmokeSay("chat"), SmokeSay("Ran it."), SmokeSay("Noted, choom.") }, asked, out fake);
            try
            {
                List<string> jobs = new List<string>();
                SayDo.Decide = delegate(Cloud remote, string frame) { frames.Add(frame); return "ACT: Run start-capri.ps1 from capri-automation with PowerShell"; };
                Brain redoer = new Brain();
                redoer.Model = "pains-test";
                redoer.Act = delegate(string tool, ToolArguments args) { jobs.Add(tool + "|" + args.Task); return "Ran start-capri.ps1 from C:\\x."; };
                redoer.Remember("start capri ps1", "No app called capri ps1 is installed on this PC.");
                redoer.Remember("we talked about this it means go to capri automation and select the capri ps1 file and run it with powershell", "Yezzirrr, I'm on it: going into capri-automation to run it.");
                string redone = redoer.Ask("you said that you're doing it, but you didn't do it bug");
                if (redone != "Ran it." || jobs.Count != 1 || !jobs[0].StartsWith("use_computer|", StringComparison.Ordinal) || frames.Count != 1 || !frames[0].Contains("capri ps1") || !frames[0].Contains("complaining")) throw new Exception("A complaint did not run the earlier ask: " + redone + " / " + string.Join(" ; ", jobs.ToArray()));
                string thanks = redoer.Ask("thanks works now. still needs to be fixed so it works from the first time bug");
                if (thanks != "Noted, choom." || jobs.Count != 1 || frames.Count != 1) throw new Exception("Thanks re-ran the ask: " + thanks);
            }
            finally { fake.Stop(); }

            asked.Clear();
            frames.Clear();
            Brain.Ollama = FakeServer(new[] { SmokeSay("Ha, true."), SmokeSay("chat"), SmokeSay("Reminder's set!") }, asked, out fake);
            try
            {
                List<string> jobs = new List<string>();
                SayDo.Decide = delegate(Cloud remote, string frame) { frames.Add(frame); return frame.Contains("Reminder's set") ? "ASK: What time should I remind you, choom?" : "CHAT"; };
                Brain chatty = new Brain();
                chatty.Model = "pains-test";
                chatty.Act = delegate(string tool, ToolArguments args) { jobs.Add(tool + "|" + args.Task); return "Did it."; };
                chatty.Remember("open spotify", "Opened Spotify.");
                string joke = chatty.Ask("you never told me that lol");
                if (joke != "Ha, true." || jobs.Count != 0 || frames.Count != 1) throw new Exception("A complaint-shaped chat line replayed an old ask: " + joke + " / " + string.Join(" ; ", jobs.ToArray()));
                string question = chatty.Ask("can you add me a reminder to call the doctor");
                if (question != "What time should I remind you, choom?" || jobs.Count != 0) throw new Exception("A reminder with no time was claimed as set: " + question);
            }
            finally { fake.Stop(); }

            asked.Clear();
            Brain.Ollama = FakeServer(new[] { PainTool("open_app", "{\"name\":\"zorblax thing\"}"), SmokeSay("Ran it from capri-automation.") }, asked, out fake);
            try
            {
                List<string> acts = new List<string>();
                Brain opener = new Brain();
                opener.Model = "pains-test";
                opener.Act = delegate(string tool, ToolArguments args)
                {
                    acts.Add(tool + "|" + (args.Name ?? args.Task));
                    return tool == "open_app" ? "No app called zorblax thing is installed on this PC." : "Ran start-capri.ps1 from C:\\x.";
                };
                string said = opener.Ask("open the zorblax thing");
                if (said != "Ran it from capri-automation." || acts.Count != 2 || acts[0] != "open_app|zorblax thing" || !acts[1].StartsWith("use_computer|Open or run", StringComparison.Ordinal) || !acts[1].Contains("open the zorblax thing") || !acts[1].Contains(".ps1")) throw new Exception("A name that is not an app ended at No app called: " + said + " / " + string.Join(" ; ", acts.ToArray()));
            }
            finally { fake.Stop(); }

            asked.Clear();
            List<string> memoFrames = new List<string>();
            SayDo.Summarize = delegate(Cloud remote, string frame) { memoFrames.Add(frame); return "They want start capri ps1 to mean finding start-capri.ps1 in capri-automation and running it with PowerShell."; };
            Brain.Ollama = FakeServer(new[] { SmokeSay("Noted.") }, asked, out fake);
            try
            {
                List<string> saved = new List<string>();
                Brain noter = new Brain();
                noter.Model = "pains-test";
                noter.Act = delegate(string tool, ToolArguments args) { saved.Add(tool + "|" + args.Text); return "Saved to my notes."; };
                noter.Remember("start capri ps1 means find the ps1 file in capri automation and run it with powershell", "What should I remember, choom?");
                string said = noter.Ask("the stuff I said about ps1 capri file");
                if (said != "Noted." || saved.Count != 1 || saved[0] != "remember|They want start capri ps1 to mean finding start-capri.ps1 in capri-automation and running it with PowerShell." || memoFrames.Count != 1 || !memoFrames[0].Contains("start capri ps1 means find the ps1 file")) throw new Exception("A note that names no fact was saved as it was said: " + string.Join(" ; ", saved.ToArray()));
                SayDo.Summarize = delegate { return null; };
                string fallback = SayDo.Memo(null, new List<ChatMessage> { new ChatMessage("user", "start capri ps1 means run the script"), new ChatMessage("assistant", "What should I remember?") }, "the stuff I said", "the stuff I said");
                if (fallback != "They told Capri: start capri ps1 means run the script") throw new Exception("The note without a summary was wrong: " + fallback);
            }
            finally { fake.Stop(); }
            checks.Add("PASS say equals do: a claim with no tool behind it (\"I'm on it: going into...\") goes to a small judge and becomes the job she claimed, a claim that was only talk stays talk, a request for later becomes a scheduled reminder or job, a complaint that she did not do it runs the earlier ask with the whole talk while thanks does not, a name that is not an app falls through to a job that looks for it as a script or file, and \"remember the stuff I said\" saves what was said; \"checking monitor two now\" counts as a claim, and a refusal to do a PC job he asked for (\"I can't click through Steam from here\", \"that step is up to you\") becomes the job");
        }

        private static void PainLaunch(List<string> checks, string folder)
        {
            string root = Path.Combine(folder, "launch");
            Directory.CreateDirectory(Path.Combine(root, "capri-automation"));
            Directory.CreateDirectory(Path.Combine(root, "other"));
            File.WriteAllText(Path.Combine(root, "capri-automation", "start-capri.ps1"), "x");
            File.WriteAllText(Path.Combine(root, "Capri.lnk"), "x");
            File.WriteAllText(Path.Combine(root, "other", "build-tool.bat"), "x");
            File.WriteAllText(Path.Combine(root, "other", "run-tool.cmd"), "x");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "x");
            File.WriteAllText(Path.Combine(root, "setup-capri.bat"), "x");
            File.WriteAllText(Path.Combine(root, "other", "game.exe"), "x");
            List<string> started = new List<string>();
            Launchable.Roots = delegate { return new List<string> { root }; };
            Launchable.Start = delegate(string path) { started.Add(path); };
            string ran = Launchable.Run("capri ps1");
            if (ran == null || !ran.StartsWith("Ran start-capri.ps1 from ", StringComparison.Ordinal) || started.Count != 1 || !started[0].EndsWith("start-capri.ps1", StringComparison.Ordinal)) throw new Exception("\"capri ps1\" did not run the script: " + ran);
            if (Launchable.Run("start capri ps1") == null || started.Count != 2) throw new Exception("\"start capri ps1\" did not run the script.");
            if (Launchable.Run("zorblax") != null || Launchable.Run("tool") != null || Launchable.Run("notes") != null || Launchable.Run("the") != null || Launchable.Run("setup capri") != null || Launchable.Run("game") != null || started.Count != 2) throw new Exception("Something that fits nothing, two files equally, a text file or only filler words was run.");
            string built = Launchable.Run("build tool");
            if (built == null || !built.StartsWith("Ran build-tool.bat", StringComparison.Ordinal) || started.Count != 3) throw new Exception("\"build tool\" did not run the one file that fits: " + built);
            string routed;
            ToolArguments routedArgs;
            if (!Hands.Direct("start capri ps1", out routed, out routedArgs) || routed != "open_app" || routedArgs.Name != "capri ps1" || Launchable.Wants("run the build script") != "build script" || Launchable.Wants("start League") != null || Launchable.Wants("find capri ps1") != null)
                throw new Exception("\"start capri ps1\" went to a file search instead of running the script, or a run without a script word was taken as one: " + routed);
            string scriptAt = Path.Combine(root, "capri-automation", "start-capri.ps1");
            if (Launchable.PathAsked("right click " + scriptAt + " and run it with PowerShell") != scriptAt || Launchable.PathAsked("In File Explorer, go to " + Path.Combine(root, "capri-automation") + ", right-click start-capri.ps1 and choose Run with PowerShell.") != scriptAt || Launchable.PathAsked("show me " + scriptAt + " in File Explorer") != null || Launchable.PathAsked("run " + Path.Combine(root, "missing.ps1")) != null)
                throw new Exception("A job that names a script path to run was not taken as running that script, or a path to show or a missing script was.");
            string opened = Hands.Do("open_app", new ToolArguments { Name = "capri ps1" });
            if (opened == null || !opened.StartsWith("Ran start-capri.ps1 from ", StringComparison.Ordinal) || started.Count != 4) throw new Exception("open_app gave up on a script instead of running it: " + opened);
            Launchable.Roots = delegate { return new List<string>(); };
            string nothing = Hands.Do("open_app", new ToolArguments { Name = "zorblax thing" });
            if (nothing != "No app called zorblax thing is installed on this PC." || started.Count != 4) throw new Exception("An unknown name did not keep its plain answer: " + nothing);
            checks.Add("PASS open anything: a name that is not an installed app is looked for as a script or shortcut (.ps1, .bat, .cmd, .lnk) under his Desktop, Documents, user profile and Start menu, the one file that fits is run (a .ps1 with PowerShell) and she says which one, two equally good files or none keep the plain answer, and text files, programs and installers are never run");
        }

        private static void PainSmall(List<string> checks, string folder)
        {
            if (Forgetting.Wanted("don't forget the stuff I said about ps1") || Forgetting.Wanted("never forget my birthday") || !Forgetting.Wanted("forget the gym line") || !Forgetting.Wanted("don't forget this one, but wipe my old notes")) throw new Exception("\"Don't forget\" was taken for a request to forget, or a real one was refused.");
            string vault = Path.Combine(folder, "notes-vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Freya.md"), "# Freya\n");
            string log = Path.Combine(folder, "today.log");
            File.WriteAllText(log, "2026-10-30 00:17:39  vault  notes used  ->  Sister.md\n2026-10-30 08:55:01  vault  her notes file is C:\\x\\me.md  ->  vault open\n2026-10-30 09:59:27  vault  note renamed  ->  People/Sister.md to People/Freya.md\n2026-10-30 12:05:32  remember  49 characters of text  ->  Saved to my notes: x\n");
            string today = NotesTool.Run(vault, new ToolArguments { Action = "today" }, new DateTime(2026, 10, 30, 13, 0, 0), log);
            if (today.Contains("notes used") || today.Contains("vault open") || !today.Contains("note renamed") || !today.Contains("Saved to my notes")) throw new Exception("What she saved today counted notes she only read: " + today);
            if (!Hands.Wants("what did you save today?") || !Hands.Wants("what do you remember about me") || Hands.Wants("what do you think of discord")) throw new Exception("\"What did you save\" did not reach her tools, or chat did.");
            if (!SayDo.BackRef("just find it white click it and press start") || !SayDo.BackRef("do that again") || SayDo.BackRef("what does that code mean?") || SayDo.BackRef("open my mystery app") || SayDo.BackRef("open Spotify and play some lofi music for me")) throw new Exception("A line that points back at the talk was not told from a fresh ask.");
            if (!Fork.HandsRules.Contains("attached to your reply by itself")) throw new Exception("Her rules do not say the Discord screenshot is attached by itself.");
            if (!Scheduler.Listing("What reminder do I have?") || !Scheduler.Listing("do I have any reminders tomorrow") || !Scheduler.Listing("what reminders do I have") || Scheduler.Listing("what is a reminder")) throw new Exception("Asking which reminders he has was not heard, or chat was.");
            string onlineTool;
            ToolArguments onlineArgs;
            if (!Hands.Heard("Who, who's online on Discord?", out onlineTool, out onlineArgs) || onlineTool != "discord" || onlineArgs.Action != "online" || onlineArgs.Name != null) throw new Exception("Who's online on Discord did not reach the discord tool: " + onlineTool);
            if (!Hands.Heard("Um, is Domte online?", out onlineTool, out onlineArgs) || onlineTool != "discord" || onlineArgs.Name != "Domte") throw new Exception("Is X online did not reach the discord tool with the name: " + onlineTool + " / " + onlineArgs.Name);
            if ((Hands.Heard("is Rudce online on League", out onlineTool, out onlineArgs) && onlineTool == "discord") || (Hands.Heard("is the server online", out onlineTool, out onlineArgs) && onlineTool == "discord")) throw new Exception("A League or server question went to Discord presence.");
            if (!Agent.Prompt.Contains("You are not a window") || !Agent.Prompt.Contains(Fork.HandsRules)) throw new Exception("Her agent-mode voice prompt does not carry her hands rules.");
            checks.Add("PASS agent-mode voice prompt: carries her hands rules (you are not a window, monitor words) like her other brains");
            foreach (string notMic in new[] { "don't mute your mic", "dont mute your mic", "never turn off your mic" })
                if (Hands.Heard(notMic, out onlineTool, out onlineArgs) && onlineTool == "her_mic") throw new Exception("\"" + notMic + "\" switched her mic.");
            foreach (string micAsk in new[] { "Capri, mute your mic.", "Cathy, mute your mic.", "Kathy mute your mic", "hey mute your mic", "mute your mic", "turn off your mic", "unmute your mic", "turn your mic back on" })
            {
                if (!Hands.Heard(micAsk, out onlineTool, out onlineArgs) || onlineTool != "her_mic") throw new Exception("\"" + micAsk + "\" did not reach her own mic: " + onlineTool + " / " + onlineArgs.Name);
                string wantAction = micAsk.Contains("unmute") || micAsk.Contains("back on") ? "unmute" : "mute";
                if (onlineArgs.Action != wantAction) throw new Exception("\"" + micAsk + "\" switched her mic the wrong way: " + onlineArgs.Action);
            }
            if (!Hands.Heard("Capri, mute my mic.", out onlineTool, out onlineArgs) || onlineTool != "shortcut" || onlineArgs.Name != "discord_mute") throw new Exception("\"Mute my mic\" no longer reaches his Discord mute: " + onlineTool);
            string[,] misheardName = { { "Kathy, play some music.", "play_music" }, { "Cappy, pause the music.", "media" }, { "Gabri, pause.", "media" }, { "Caprin, skip this song.", "media" }, { "Capri, mute your mike.", "her_mic" } };
            for (int i = 0; i < misheardName.GetLength(0); i++)
                if (!Hands.Heard(misheardName[i, 0], out onlineTool, out onlineArgs) || onlineTool != misheardName[i, 1]) throw new Exception("Her misheard name in front of \"" + misheardName[i, 0] + "\" kept the command from her: " + onlineTool);
            foreach (string talk in new[] { "Rudce, mute your mic", "Pieter, skip this song", "Kevin, pause the music", "Kathy said play some music", "Carry, carry the team please", "Copy, that is cool", "Gary, play the game with me tomorrow", "Cappy, how are you", "Copper, next song" })
                if (Hands.Heard(talk, out onlineTool, out onlineArgs)) throw new Exception("\"" + talk + "\" (talk, or a friend's name) became a command: " + onlineTool);
            if (NameEars.SoundsLikeHer("Pieter") || NameEars.SoundsLikeHer("Rudce") || !NameEars.SoundsLikeHer("Caprin") || !NameEars.SoundsLikeHer("Kathy")) throw new Exception("Her name's sound-alike check is off.");
            checks.Add("PASS misheard name: one word in front that sounds like Capri (Kathy, Cappy, Gabri, Caprin) still gives the command after it, only when the rest is a whole command; a friend's name, talk and \"mike\" for mic are handled (a friend's \"Rudce, mute your mic\" no longer mutes her)");
            Func<bool, string> keptMic = Hands.HerMic;
            Func<string, bool> keptGame = Lockout.Running;
            try
            {
                List<bool> switched = new List<bool>();
                Hands.HerMic = delegate(bool mute) { switched.Add(mute); return mute ? "Mic muted." : "Mic on."; };
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                string muted = Hands.Do("her_mic", new ToolArguments { Action = "mute" });
                string unmuted = Hands.Do("her_mic", new ToolArguments { Action = "unmute" });
                if (muted != "Mic muted." || unmuted != "Mic on." || switched.Count != 2 || !switched[0] || switched[1]) throw new Exception("Her own mic was not switched in a game: " + muted + " / " + unmuted);
                Computer.Arm();
                System.Threading.Thread.Sleep(200);
                if (Computer.Why != LockoutStopped) throw new Exception("A job stopped by the game lockout said: " + Computer.Why);
            }
            finally
            {
                Hands.HerMic = keptMic;
                Lockout.Running = keptGame;
                Lockout.Recheck();
                Computer.Arm();
            }
            checks.Add("PASS her mic and lockout stops: \"mute your mic\" and \"unmute your mic\" switch her own voice chat mic with no keys pressed, also in a game (\"mute my mic\" stays his Discord mute), and a job stopped by the game lockout says so instead of \"You pressed Esc\"");
            if (QuitFrom("at CapriDesktop.Mainframe.<.ctor>b__12()\n at CapriDesktop.Companion.Quit()") != "the Mainframe's Quit key" || QuitFrom("at System.Windows.Forms.ToolStripItem.OnClick(EventArgs e)\n at CapriDesktop.Companion.Quit()") != "her right-click or tray menu" || QuitFrom("at CapriDesktop.Companion.Somewhere()") != "her own code") throw new Exception("Where a quit came from was not told apart.");
            Func<string, bool> keptMenuGame = Lockout.Running;
            try
            {
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                if (!MenuHeldInGame()) throw new Exception("Her right-click menu would open over a game.");
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                if (MenuHeldInGame()) throw new Exception("Her right-click menu was held with no game running.");
            }
            finally
            {
                Lockout.Running = keptMenuGame;
                Lockout.Recheck();
            }
            checks.Add("PASS quit guard: her right-click menu never opens over a running game, Quit Capri asks once before closing, and every quit is logged with where it came from (her menu or the tray, the Mainframe's Quit key, her own code)");
            Func<string, bool> keptFriend = DiscordPresence.Friend;
            try
            {
                DiscordPresence.Friend = delegate(string name) { return string.Equals((name ?? "").Trim(), "pieter", StringComparison.OrdinalIgnoreCase); };
                foreach (string shortAsk in new[] { "is pieter on rn", "is pieter on right now?", "is Pieter online rn", "is pieter on discord rn" })
                    if (!Hands.Heard(shortAsk, out onlineTool, out onlineArgs) || onlineTool != "discord" || onlineArgs.Action != "online" || !string.Equals(onlineArgs.Name, "pieter", StringComparison.OrdinalIgnoreCase)) throw new Exception("\"" + shortAsk + "\" did not reach the discord tool with the name: " + onlineTool + " / " + onlineArgs.Name);
                foreach (string notFriend in new[] { "is pieter on", "is the game on rn", "is spotify on rn", "is league on rn", "is discord on rn" })
                    if (Hands.Heard(notFriend, out onlineTool, out onlineArgs) && onlineTool == "discord" && onlineArgs.Action == "online") throw new Exception("\"" + notFriend + "\" went to Discord presence.");
            }
            finally { DiscordPresence.Friend = keptFriend; }
            foreach (string anyAsk in new[] { "Use anymore online.", "is anyone online?", "Hey Capri, is anybody on discord rn" })
            {
                string anyTool;
                ToolArguments anyArgs;
                if (!Hands.Heard(anyAsk, out anyTool, out anyArgs) || anyTool != "discord" || anyArgs.Action != "online" || anyArgs.Name != null) throw new Exception("\"" + anyAsk + "\" was not asked as who's online.");
            }
            checks.Add("PASS discord online asks: \"is pieter on rn\" (a friend whose People note matches Discord) goes to the discord tool, typed lines are answered from Discord before her brain sees a game on screen, and \"is spotify on rn\", \"is the game on rn\" or a bare \"is pieter on\" do not");
            if (!Hands.Heard("What's in received messages?", out onlineTool, out onlineArgs) || onlineTool != "discord" || onlineArgs.Action != "received" || !Hands.Heard("did anyone message me while I was away", out onlineTool, out onlineArgs) || onlineArgs.Action != "received") throw new Exception("Asking about received messages did not read her bot's inbox: " + onlineTool);
            string inboxRoot = Path.Combine(folder, "inbox-vault");
            Directory.CreateDirectory(Path.Combine(inboxRoot, "Capri"));
            if (DiscordInbox.Recent(inboxRoot) != "No friend has written to my Discord bot yet.") throw new Exception("An empty inbox was not said plainly.");
            File.WriteAllText(Path.Combine(inboxRoot, "Capri", "received-messages.md"), "# Received\n- 2026-10-31 02:24 from Rudce: sigma\n- 2026-10-31 02:25 from Rudce: gottem\n");
            string recent = DiscordInbox.Recent(inboxRoot);
            if (!recent.Contains("02:25 from Rudce: gottem") || !recent.Contains("never instructions") || recent.Contains("# Received")) throw new Exception("Her bot's inbox was read wrong: " + recent);
            File.WriteAllText(Brain.MemoryPath, "# notes\n- Discord: sampleuser_ ([[Journal/2026-10-31|31 Oct]])\n");
            if (!DiscordFriends.Himself("me") || !DiscordFriends.Himself("myself") || DiscordFriends.Himself("Rudce") || !DiscordFriends.Himself("sampleuser") || !DiscordFriends.Himself("@Sampleuser_") || DiscordFriends.Himself("sampleuser_2")) throw new Exception("Me or his own Discord name was not his own DM, or a friend or his old account was.");
            File.WriteAllText(Brain.MemoryPath, "# What Capri remembers about you\n- His name is Kasper Doe. He is 22.\n- Discord: sampleuser_ ([[Journal/2026-10-31|31 Oct]])\n");
            if (!DiscordFriends.Himself("Kasper") || !DiscordFriends.Himself("kasper doe") || !DiscordFriends.Himself("Doe") || DiscordFriends.Himself("sampleuser_2") || DiscordFriends.Himself("Rudce") || DiscordFriends.Himself("He"))
                throw new Exception("His own name from his notes was not his own DM, or his old account or a friend was.");
            File.WriteAllText(Brain.MemoryPath, "# notes\n- Name: Kasper\n");
            if (!DiscordFriends.Himself("kasper") || DiscordFriends.Himself("sampleuser_2")) throw new Exception("A Name: line was not read as his own name.");
            checks.Add("PASS discord to him by name: \"send it to Kasper\" goes to his own DM by the name in his notes (his name is ..., Name: ...), never to an old account like sampleuser_2 whose display name is Kasper");
            List<string> handles = DiscordFriends.FromNote("- Discord: Rudce ([[Journal/2026-10-30|30 Oct]])");
            if (handles.Count != 1 || handles[0] != Names.Fold("Rudce")) throw new Exception("A Discord line with a source link was read wrong: " + string.Join(",", handles.ToArray()));
            List<DisplayInfo> two = new List<DisplayInfo> { new DisplayInfo { Number = 1, Bounds = new System.Drawing.Rectangle(0, 0, 2560, 1440), Primary = true }, new DisplayInfo { Number = 2, Bounds = new System.Drawing.Rectangle(2560, 0, 1920, 1080) } };
            string second = Screens.Seen(two[1].Bounds, two, new System.Drawing.Point(100, 100));
            string byMouse = Screens.Seen(null, two, new System.Drawing.Point(3000, 500));
            if (!second.Contains("monitor 2") || !second.Contains("never say only one screen") || !byMouse.Contains("monitor 2") || Screens.Seen(null, new List<DisplayInfo> { two[0] }, System.Drawing.Point.Empty) != "") throw new Exception("A look did not name the monitor she saw: " + second + " / " + byMouse);
            string keptAsk = AskInbox.FilePath;
            try
            {
                AskInbox.FilePath = Path.Combine(folder, "ask.txt");
                AskInbox.Drop("first line");
                AskInbox.Drop("second\nline");
                if (AskInbox.Drop("  ") == 0 || AskInbox.Take() != "first line" || AskInbox.Take() != "second line" || AskInbox.Take() != null || File.Exists(AskInbox.FilePath)) throw new Exception("A local --ask line was lost, merged or taken twice.");
            }
            finally { AskInbox.FilePath = keptAsk; AskInbox.Reset(); }
            string keptLanguage = Brain.Language;
            try
            {
                Brain.Language = "English";
                string english = Brain.Character;
                Brain.Language = null;
                string unset = Brain.Character;
                Brain.Language = "French";
                string french = Brain.Character;
                if (!english.Contains("Always reply in English") || !unset.Contains("Always reply in English") || !english.Contains("never write Lithuanian") || french.Contains("Always reply in English") || !french.Contains("Always reply in French")) throw new Exception("On English she was not told to answer in English whatever language she hears, or the other languages lost their rule.");
            }
            finally { Brain.Language = keptLanguage; }
            checks.Add("PASS small pains: \"don't forget\" never wipes anything, \"what did you save today\" lists saves and not the notes she only read, and it reaches her notes tool; her rules say the screenshot is attached to a Discord reply by itself; on English her prompt says to answer in English whatever language she hears and never write Lithuanian, the other languages keep their own rule");
        }
    }
}
