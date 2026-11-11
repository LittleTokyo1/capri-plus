using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeShortcutActions(List<string> checks)
        {
            string keptFile = PhoneActions.FilePath, keptLog = ActivityLog.FilePath;
            Func<bool> keptLinked = PhoneActions.Linked, keptCalendarOn = PhoneActions.CalendarOn;
            Func<string, DateTime, int, string[]> keptCalendar = PhoneActions.Calendar;
            string folder = Path.Combine(Path.GetTempPath(), "capri-shortcut-actions-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                PhoneActions.FilePath = Path.Combine(folder, "phone-actions.json");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                PhoneActions.End();
                DateTime now = new DateTime(2026, 11, 4, 22, 0, 0);
                List<string> calendared = new List<string>();
                PhoneActions.Linked = delegate { return true; };
                PhoneActions.CalendarOn = delegate { return true; };
                PhoneActions.Calendar = delegate(string title, DateTime at, int minutes) { calendared.Add(title + "@" + at.ToString("HH:mm")); return new string[] { "ev" + calendared.Count, "ok" }; };
                if (PhoneActions.Valid(new ToolArguments { Action = "alarm" }) || PhoneActions.Valid(new ToolArguments { Action = "timer", Minutes = "0" }) || PhoneActions.Valid(new ToolArguments { Action = "timer", Minutes = "ten" })
                    || PhoneActions.Valid(new ToolArguments { Action = "focus", Text = "Sleep" }) || PhoneActions.Valid(new ToolArguments { Action = "music" }) || PhoneActions.Valid(new ToolArguments { Action = "dance", Text = "x" }) || PhoneActions.Valid(null)
                    || !PhoneActions.Valid(new ToolArguments { Action = "alarm", Start = "07:00" }) || !PhoneActions.Valid(new ToolArguments { Action = "timer", Minutes = "10" }) || !PhoneActions.Valid(new ToolArguments { Action = "focus", Text = "Sleep", Mode = "on" }))
                    throw new Exception("The phone tool accepted a broken action or refused a good one.");
                string queued = PhoneActions.Do(new ToolArguments { Action = "alarm", Start = "07:00", Text = "school" }, now);
                List<PhoneAction> waiting = PhoneActions.Pending(now);
                if (queued.IndexOf("Waiting for your phone", StringComparison.Ordinal) < 0 || queued.IndexOf("Google Calendar", StringComparison.Ordinal) < 0 || waiting.Count != 1 || waiting[0].Kind != "alarm" || waiting[0].At != "2026-11-05 07:00" || waiting[0].Event != "ev1" || calendared.Count != 1 || calendared[0] != "Alarm 07:00: school@" + new DateTime(2026, 11, 5, 7, 0, 0).AddMinutes(Google.AlertMinutes).ToString("HH:mm"))
                    throw new Exception("A PC alarm did not wait for the phone with a calendar backup: " + queued);
                if (!PhoneActions.Begin() || PhoneActions.Begin()) throw new Exception("A phone turn could hand actions twice at once.");
                string handedSaid = PhoneActions.Do(new ToolArguments { Action = "timer", Minutes = "10" }, now);
                List<PhoneAction> handed = PhoneActions.End();
                if (handed.Count != 1 || handed[0].Kind != "timer" || handed[0].Minutes != 10 || handedSaid.IndexOf("Your phone sets it now", StringComparison.Ordinal) < 0 || PhoneActions.Pending(now).Count != 1 || calendared.Count != 1)
                    throw new Exception("A timer asked from a phone shortcut was queued or calendared instead of handed back: " + handedSaid);
                string json = PhoneActions.Json(handed, now);
                if (json.IndexOf("{\"actions\":[{\"id\":", StringComparison.Ordinal) != 0 || json.IndexOf("\"kind\":\"timer\"", StringComparison.Ordinal) < 0 || json.IndexOf("\"minutes\":10", StringComparison.Ordinal) < 0)
                    throw new Exception("The phone actions were sent as: " + json);
                string alarmJson = PhoneActions.Json(PhoneActions.Pending(now), now);
                if (alarmJson.IndexOf("\"at\":\"2026-11-05 07:00\"", StringComparison.Ordinal) < 0 || alarmJson.IndexOf("\"time\":\"07:00\"", StringComparison.Ordinal) < 0 || alarmJson.IndexOf("\"text\":\"school\"", StringComparison.Ordinal) < 0)
                    throw new Exception("The alarm was sent to the phone as: " + alarmJson);
                PhoneActions.Do(new ToolArguments { Action = "music", Text = "lofi" }, now.AddHours(-25));
                PhoneActions.Do(new ToolArguments { Action = "reminder", Text = "bins", Start = "22:30" }, now);
                if (PhoneActions.Pending(now).Count != 2) throw new Exception("An action older than 24 h was still sent to the phone.");
                int count;
                List<string> events = PhoneActions.Done(PhoneActions.Ids(waiting[0].Id + ",zzzz"), now, out count);
                if (count != 1 || events.Count != 1 || events[0] != "ev1" || PhoneActions.Pending(now).Count != 1) throw new Exception("Done from the phone did not remove the alarm and hand back its calendar backup.");
                PhoneActions.Do(new ToolArguments { Action = "alarm", Start = "07:30" }, now);
                if (PhoneActions.Pending(now).Count != 2 || PhoneActions.Pending(now.AddHours(10)).Count != 1) throw new Exception("An alarm whose time passed was still sent to the phone.");
                PhoneActions.Linked = delegate { return false; };
                if (PhoneActions.Do(new ToolArguments { Action = "timer", Minutes = "5" }, now) != PhoneActions.Off || PhoneActions.Pending(now).Count != 1) throw new Exception("The phone tool queued something with the phone link off.");
                checks.Add("PASS phone actions (CAPRI-095 phase 5): an alarm, timer, reminder, music or Focus must be whole to pass; from the PC it waits in phone-actions.json with a Google Calendar backup for alarms and reminders; in a phone shortcut turn it goes straight back to the shortcut (never queued, no calendar); actions older than 24 h and alarms whose time passed are dropped; done removes it and hands back its calendar event; nothing is queued with the phone link off");
            }
            finally
            {
                PhoneActions.End();
                PhoneActions.FilePath = keptFile;
                PhoneActions.Linked = keptLinked;
                PhoneActions.CalendarOn = keptCalendarOn;
                PhoneActions.Calendar = keptCalendar;
                ActivityLog.FilePath = keptLog;
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private static void SmokeShortcutServer(List<string> checks)
        {
            string keptFile = PhoneAuth.FilePath, keptActions = PhoneActions.FilePath, keptLog = ActivityLog.FilePath;
            Func<string, bool, string> keptSaid = PhoneServer.Said;
            Func<string, string, string, string> keptHappened = PhoneServer.Happened;
            Action<List<string>> keptUnbacked = PhoneServer.Unbacked;
            Func<bool> keptLinked = PhoneActions.Linked, keptCalendarOn = PhoneActions.CalendarOn;
            string folder = Path.Combine(Path.GetTempPath(), "capri-shortcut-server-smoke-" + Guid.NewGuid().ToString("N"));
            PhoneServer server = null;
            try
            {
                Directory.CreateDirectory(folder);
                PhoneAuth.FilePath = Path.Combine(folder, "phones.dat");
                PhoneActions.FilePath = Path.Combine(folder, "phone-actions.json");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                PhoneAuth.Reset();
                PhoneActions.End();
                PhoneShare.Take(DateTime.Now);
                PhoneActions.Linked = delegate { return true; };
                PhoneActions.CalendarOn = delegate { return false; };
                if (PhoneShortcuts.JsonField("{\"text\": \"hi \\\"you\\\" \\u00e9\", \"run\": true, \"n\": 7, \"deep\": {\"text\": \"no\"}}", "text") != "hi \"you\" \u00e9" || PhoneShortcuts.JsonField("{\"run\":true}", "run") != "true" || PhoneShortcuts.JsonField("{\"deep\":{\"text\":\"no\"}}", "text") != null || PhoneShortcuts.JsonField("not json", "text") != null)
                    throw new Exception("A Shortcuts JSON body was read wrong.");
                string token = PhoneAuth.Pair(PhoneAuth.NewCode(DateTime.Now), "iPhone", DateTime.Now);
                TcpListener free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                int port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                server = new PhoneServer(port);
                string problem = server.Start();
                if (problem != null) throw new Exception("The phone server did not start: " + problem);
                string host = "127.0.0.1:" + port;
                List<string> heard = new List<string>();
                PhoneServer.Said = delegate(string text, bool voice)
                {
                    heard.Add(text);
                    if (text.IndexOf("timer", StringComparison.Ordinal) >= 0) PhoneActions.Do(new ToolArguments { Action = "timer", Minutes = "10" }, DateTime.Now);
                    return "Done, choom.";
                };
                List<string> happened = new List<string>();
                PhoneServer.Happened = delegate(string kind, string value, string name) { happened.Add(kind + "=" + value); return "Noted."; };
                List<string> unbacked = new List<string>();
                PhoneServer.Unbacked = delegate(List<string> ids) { unbacked.AddRange(ids); };
                foreach (string route in new[] { "/api/actions", "/api/actions/done", "/api/event", "/api/share", "/api/shortcutkey" })
                    if (SmokeShortcutSend(port, route == "/api/actions" ? "GET" : "POST", route, host, null, "application/json", Encoding.UTF8.GetBytes("{}")).Split('\n')[0] != "401") throw new Exception("The shortcut route " + route + " answered without the token.");
                string run = SmokeShortcutSend(port, "POST", "/api/say", host, token, "application/json", Encoding.UTF8.GetBytes("{\"text\":\"set a timer for ten\",\"run\":true,\"voice\":false}"));
                if (!run.StartsWith("200\n", StringComparison.Ordinal) || run.IndexOf("\"said\":\"Done, choom.\"", StringComparison.Ordinal) < 0 || run.IndexOf("\"actions\":[{", StringComparison.Ordinal) < 0 || run.IndexOf("\"kind\":\"timer\"", StringComparison.Ordinal) < 0 || PhoneActions.Pending(DateTime.Now).Count != 0)
                    throw new Exception("A Talk to Capri shortcut line did not get its timer back: " + run);
                string page = SmokeShortcutSend(port, "POST", "/api/say", host, token, "application/x-www-form-urlencoded", Encoding.UTF8.GetBytes("text=a+timer+please&voice=0"));
                if (!page.StartsWith("200\n", StringComparison.Ordinal) || page.IndexOf("\"actions\"", StringComparison.Ordinal) >= 0 || PhoneActions.Pending(DateTime.Now).Count != 1)
                    throw new Exception("A line from the phone page got actions it can't run, or its timer did not wait for the sync: " + page);
                string voiced = SmokeShortcutSend(port, "POST", "/api/say", host, token, "application/json", Encoding.UTF8.GetBytes("{\"text\":\"hi\",\"run\":\"1\",\"voice\":\"1\"}"));
                if (voiced.IndexOf("\"voice\":\"api/voice?turn=", StringComparison.Ordinal) < 0 || voiced.IndexOf("\"actions\":[]", StringComparison.Ordinal) < 0) throw new Exception("A shortcut that wants her voice got no voice link: " + voiced);
                string pending = SmokeShortcutSend(port, "GET", "/api/actions", host, token, null, null);
                List<PhoneAction> left = PhoneActions.Pending(DateTime.Now);
                if (!pending.StartsWith("200\n", StringComparison.Ordinal) || left.Count != 1 || pending.IndexOf("\"id\":\"" + left[0].Id + "\"", StringComparison.Ordinal) < 0) throw new Exception("The Capri sync shortcut did not get the waiting actions: " + pending);
                string done = SmokeShortcutSend(port, "POST", "/api/actions/done", host, token, "application/json", Encoding.UTF8.GetBytes("{\"ids\":\"" + left[0].Id + "\"}"));
                if (done.IndexOf("\"done\":1", StringComparison.Ordinal) < 0 || PhoneActions.Pending(DateTime.Now).Count != 0) throw new Exception("Done from the sync shortcut did not clear the queue: " + done);
                if (SmokeShortcutSend(port, "POST", "/api/event", host, token, "application/json", Encoding.UTF8.GetBytes("{\"kind\":\"home\"}")).Split('\n')[0] != "200"
                    || SmokeShortcutSend(port, "POST", "/api/event", host, token, "application/json", Encoding.UTF8.GetBytes("{\"kind\":\"sleep\",\"value\":\"7,5\"}")).Split('\n')[0] != "200"
                    || SmokeShortcutSend(port, "POST", "/api/event", host, token, "application/json", Encoding.UTF8.GetBytes("{\"kind\":\"dance\"}")).Split('\n')[0] != "400"
                    || SmokeShortcutSend(port, "POST", "/api/event", host, token, "application/json", Encoding.UTF8.GetBytes("{\"kind\":\"steps\",\"value\":\"lots\"}")).Split('\n')[0] != "400"
                    || happened.Count != 2 || happened[0] != "home=" || happened[1] != "sleep=7.5")
                    throw new Exception("Phone events were not passed on, or an unknown or broken one was accepted: " + string.Join(", ", happened.ToArray()));
                byte[] picture = new byte[2048];
                picture[0] = 0xFF; picture[1] = 0xD8; picture[2] = 0xFF;
                int heardBefore = heard.Count;
                string shared = SmokeShortcutSend(port, "POST", "/api/share?text=what+is+this", host, token, "image/jpeg", picture);
                if (!shared.StartsWith("200\n", StringComparison.Ordinal) || heard.Count != heardBefore + 1 || heard[heard.Count - 1] != PhoneShare.Asked + " what is this" || PhoneShare.Take(DateTime.Now) != null)
                    throw new Exception("A shared picture did not reach her as a turn, or stayed kept after it: " + shared);
                byte[] png = new byte[64];
                png[0] = 0x89; png[1] = 0x50;
                if (SmokeShortcutSend(port, "POST", "/api/share", host, token, "image/png", png).Split('\n')[0] != "415" || SmokeShortcutSend(port, "POST", "/api/share", host, token, "image/jpeg", new byte[PhoneShortcuts.MostShare + 1]).Split('\n')[0] != "413")
                    throw new Exception("A picture that isn't a JPEG, or one over the size limit, was not refused.");
                string link = SmokeShortcutSend(port, "POST", "/api/share", host, token, "application/json", Encoding.UTF8.GetBytes("{\"link\":\"https://example.com/a\"}"));
                if (!link.StartsWith("200\n", StringComparison.Ordinal) || heard[heard.Count - 1].IndexOf("https://example.com/a", StringComparison.Ordinal) < 0) throw new Exception("A shared link did not reach her: " + link);
                foreach (string file in Directory.GetFiles(folder))
                    if (Path.GetFileName(file) != "phones.dat" && Path.GetFileName(file) != "activity.log" && Path.GetFileName(file) != "phone-actions.json") throw new Exception("Something shared from the phone was written to disk: " + file);
                string first = SmokeShortcutSend(port, "POST", "/api/shortcutkey", host, token, null, null);
                int cut = first.IndexOf("\"token\":\"", StringComparison.Ordinal);
                string key = cut < 0 ? null : first.Substring(cut + 9, first.IndexOf('"', cut + 9) - cut - 9);
                if (key == null || SmokePhoneAsk(port, "GET", "/api/ping", host, key, null) != 200) throw new Exception("The Shortcuts key was not made or does not work: " + first);
                SmokeShortcutSend(port, "POST", "/api/shortcutkey", host, token, null, null);
                if (SmokePhoneAsk(port, "GET", "/api/ping", host, key, null) != 401 || SmokePhoneAsk(port, "GET", "/api/ping", host, token, null) != 200) throw new Exception("A new Shortcuts key did not turn the old one off, or turned the phone app off.");
                string log = File.ReadAllText(ActivityLog.FilePath);
                if (log.IndexOf("timer for ten", StringComparison.Ordinal) >= 0 || log.IndexOf("what is this", StringComparison.Ordinal) >= 0 || log.IndexOf("example.com", StringComparison.Ordinal) >= 0 || log.IndexOf(key, StringComparison.Ordinal) >= 0)
                    throw new Exception("A shortcut's words, link or key went into activity.log.");
                checks.Add("PASS phone shortcuts server (CAPRI-095 phase 5): every shortcut route needs the token; Talk to Capri (JSON body, run on) gets her answer and its actions back while a phone page line leaves them for the sync; a voice link only when asked; Capri sync gets the queue and done clears it; events home, left, alarm, focus, sleep and steps pass on and anything else is refused; a shared JPEG is her turn and dropped after, a PNG or one over the limit refused, a shared link reaches her; nothing shared touches the disk; a new Shortcuts key turns the old one off; no words, links or keys in activity.log");
            }
            finally
            {
                if (server != null) server.Stop();
                PhoneServer.Said = keptSaid;
                PhoneServer.Happened = keptHappened;
                PhoneServer.Unbacked = keptUnbacked;
                PhoneActions.Linked = keptLinked;
                PhoneActions.CalendarOn = keptCalendarOn;
                PhoneActions.FilePath = keptActions;
                PhoneAuth.FilePath = keptFile;
                ActivityLog.FilePath = keptLog;
                PhoneAuth.Reset();
                PhoneActions.End();
                PhoneShare.Take(DateTime.Now);
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private void SmokeShortcutWiring(List<string> checks)
        {
            ForkSettings kept = ForkSettings.Current;
            try
            {
                ForkSettings.Current = new ForkSettings();
                bool listed = false, offered = false;
                string reminderSpec = "";
                for (int i = 0; i < Hands.Count; i++)
                {
                    if (Hands.Name(i) == "phone") { listed = true; offered = Agent.Offered(i); }
                    if (Hands.Name(i) == "reminder") reminderSpec = Hands.Description(i);
                }
                if (!listed || offered || reminderSpec.IndexOf("phone tool", StringComparison.Ordinal) < 0 || Hands.Name(Hands.Count - 1) != "listen") throw new Exception("The phone tool is missing, is offered to the voice agent, or the reminder tool still takes phone alarms.");
                if (!Hands.Valid("phone", new ToolArguments { Action = "alarm", Start = "2026-11-05T07:00" }) || Hands.Valid("phone", new ToolArguments { Action = "alarm" })) throw new Exception("Her own tool check does not know the phone tool.");
                PhoneFacts.Clear();
                DateTime now = new DateTime(2026, 11, 5, 8, 0, 0);
                PhoneFacts.Note("sleep", "7.5", null, now);
                PhoneFacts.Note("steps", "4210", null, now);
                PhoneFacts.Note("focus", "on", "Sleep", now.AddHours(-9));
                PhoneFacts.Note("alarm", null, null, now.AddMinutes(-58));
                string facts = string.Join("; ", PhoneFacts.Lines(now).ToArray());
                if (facts.IndexOf("slept 7.5 h", StringComparison.Ordinal) < 0 || facts.IndexOf("4210 steps", StringComparison.Ordinal) < 0 || facts.IndexOf("Focus Sleep on since 23:00", StringComparison.Ordinal) < 0 || facts.IndexOf("alarm stopped at 07:02", StringComparison.Ordinal) < 0 || PhoneFacts.Lines(now.AddDays(1)).Count != 0)
                    throw new Exception("Her phone facts were kept as: " + facts);
                NowState state = Now.For(now, "");
                state.Phone.AddRange(PhoneFacts.Lines(now));
                if (Now.Block(state, Now.Full).IndexOf("From his iPhone: slept 7.5 h", StringComparison.Ordinal) < 0 || Now.Block(state, Now.Chat).IndexOf("4210 steps", StringComparison.Ordinal) < 0) throw new Exception("Her phone facts did not reach what she knows right now.");
                lock (phoneGate) { phoneLine = "set a timer for 2 minutes"; phoneAnswer = null; phoneTicket = asking; }
                string channel = NowFor(null).Channel;
                lock (phoneGate) phoneTicket = asking + 1;
                string later = NowFor(null).Channel;
                lock (phoneGate) { phoneLine = null; phoneTicket = -1; }
                if (channel != "the Capri app on his iPhone" || later == channel || NowFor(null).Channel == channel) throw new Exception("A phone-app turn was told to her as " + channel + ", or another turn as the phone (" + later + ").");
                PhoneHappened("left", null, null);
                if (!ForkSettings.Current.HomeAway || !ForkSettings.Current.Paused) throw new Exception("Leaving home from the phone did not send her away.");
                PhoneHappened("home", null, null);
                if (ForkSettings.Current.HomeAway || ForkSettings.Current.Paused) throw new Exception("Coming home from the phone did not bring her back.");
                PhoneFacts.Clear();
                Brain eyes = new Brain();
                eyes.Remote = new Cloud("sk-ant-test", "claude-haiku-5-5");
                eyes.Eyes = delegate { return new byte[] { 9, 9, 9 }; };
                PhoneShare.Keep(new byte[] { 0xFF, 0xD8, 0xFF, 1 }, DateTime.Now);
                List<ChatMessage> messages = new List<ChatMessage> { new ChatMessage("system", "s"), new ChatMessage("user", "what is this") };
                string parts = eyes.GlimpseForTest(messages);
                if (parts == null || !parts.Contains("\"data\":\"/9j/AQ==\"") || !parts.Contains("shared from his iPhone") || PhoneShare.Take(DateTime.Now) != null) throw new Exception("A shared picture did not go with his line instead of the screen.");
                checks.Add("PASS phone shortcuts wiring (CAPRI-095 phase 5): the phone tool is hers (not the voice agent's) and the reminder tool hands phone alarms to it; sleep, steps, Focus and the stopped alarm reach what she knows now and expire with the day; a phone-app line is told as the iPhone app; leaving and coming home from the phone send her away and back; a shared picture goes with his line instead of a screenshot, once");
            }
            finally
            {
                lock (phoneGate) { phoneLine = null; phoneTicket = -1; }
                PhoneFacts.Clear();
                PhoneShare.Take(DateTime.Now);
                ForkSettings.Current = kept;
            }
        }

        private static string SmokeShortcutSend(int port, string method, string path, string host, string token, string type, byte[] content)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, port);
                    client.ReceiveTimeout = 10000;
                    NetworkStream stream = client.GetStream();
                    byte[] sent = content ?? new byte[0];
                    StringBuilder ask = new StringBuilder();
                    ask.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\nHost: ").Append(host).Append("\r\nConnection: close\r\n");
                    if (token != null) ask.Append("Authorization: Bearer ").Append(token).Append("\r\n");
                    if (type != null) ask.Append("Content-Type: ").Append(type).Append("\r\n");
                    ask.Append("Content-Length: ").Append(sent.Length).Append("\r\n\r\n");
                    byte[] head = Encoding.ASCII.GetBytes(ask.ToString());
                    stream.Write(head, 0, head.Length);
                    try { if (sent.Length > 0) stream.Write(sent, 0, sent.Length); }
                    catch (IOException) { }
                    string all = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
                    string[] parts = all.Split(new[] { ' ' }, 3);
                    int split = all.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    return (parts.Length > 1 ? parts[1] : "0") + "\n" + (split < 0 ? "" : all.Substring(split + 4));
                }
            }
            catch (IOException) { return "0\n"; }
            catch (SocketException) { return "0\n"; }
        }
    }
}
