using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed class GatewayMessage
    {
        internal string Id = "", Channel = "", Author = "", Name = "", Text = "";
        internal bool Bot;
    }

    internal sealed class GatewayPresence
    {
        internal string User = "", Status = "", Desktop = "", Mobile = "";
    }

    internal sealed class GatewayVoice
    {
        internal string User = "", Channel = "";
    }

    internal sealed class GatewayStep
    {
        internal readonly List<string> Send = new List<string>();
        internal readonly List<GatewayMessage> Messages = new List<GatewayMessage>();
        internal readonly List<GatewayPresence> Presences = new List<GatewayPresence>();
        internal readonly List<GatewayVoice> Voices = new List<GatewayVoice>();
        internal readonly Dictionary<string, string> Channels = new Dictionary<string, string>();
        internal bool Reconnect, Fresh, Hold, Opened, Snapshot;
    }

    internal static class DiscordGateway
    {
        internal const int VoiceIntent = 128;
        internal const int VoiceChannelType = 2;
        internal const int StageChannelType = 13;
        internal const int BaseIntents = 1 | VoiceIntent | 4096;
        internal const int PresenceIntent = 256;
        internal static bool PresencesOff;
        internal static volatile bool Live;
        internal static Action<GatewayStep> Stepped;
        private static string presencesOffToken;

        internal static int Intents { get { return BaseIntents | (PresencesOff ? 0 : PresenceIntent); } }
        internal const int MostIdentifiesPerHour = 5;
        internal const int LowRemaining = 20;
        internal const int MostWaitSeconds = 300;
        private const int StableSeconds = 60;
        private const int HoldMinutes = 60;
        internal static string Url;
        internal static Action<GatewayMessage> Heard;
        internal static Action Opened;
        internal static string SessionId, ResumeUrl, SelfId;
        internal static long Seq = -1;
        internal static int Remaining = -1;
        internal static int Interval;
        internal static bool Acked = true;
        private static readonly object gate = new object();
        private static readonly object sending = new object();
        private static readonly List<DateTime> identifies = new List<DateTime>();
        private static Thread worker;
        private static CancellationTokenSource stop;
        private static ClientWebSocket socket;
        private static string runningToken;
        private static DateTime heldUntil = DateTime.MinValue;
        private static string refusedToken;
        internal static string Refused;
        private static bool holdTold;

        internal static bool Running { get { lock (gate) return worker != null; } }

        internal static void Forget()
        {
            lock (gate) identifies.Clear();
            SessionId = ResumeUrl = SelfId = null;
            Seq = -1;
            Remaining = -1;
            Interval = 0;
            Acked = true;
            heldUntil = DateTime.MinValue;
            holdTold = false;
            Refused = null;
            refusedToken = null;
            PresencesOff = false;
            presencesOffToken = null;
            Live = false;
        }

        internal static void Keep(bool wanted, string token)
        {
            if (Refused != null)
            {
                if (token == refusedToken) wanted = false;
                else Refused = null;
            }
            if (PresencesOff && token != presencesOffToken) PresencesOff = false;
            bool halt;
            lock (gate) halt = worker != null && (!wanted || token != runningToken);
            if (halt) Halt();
            if (!wanted || string.IsNullOrEmpty(token)) return;
            lock (gate)
            {
                if (worker != null) return;
                runningToken = token;
                stop = new CancellationTokenSource();
                CancellationTokenSource mine = stop;
                worker = new Thread(delegate() { Run(token, mine); });
                worker.IsBackground = true;
                worker.Name = "Capri Discord gateway";
                worker.Start();
            }
        }

        internal static void Halt()
        {
            Thread old;
            lock (gate)
            {
                old = worker;
                worker = null;
                runningToken = null;
                if (stop != null) stop.Cancel();
                ClientWebSocket open = socket;
                if (open != null) { try { open.Abort(); } catch (Exception) { } }
            }
            if (old != null && old != Thread.CurrentThread) old.Join(2000);
        }

        internal static bool MayIdentify(DateTime now)
        {
            lock (gate)
            {
                identifies.RemoveAll(delegate(DateTime when) { return (now - when).TotalMinutes >= 60; });
                return identifies.Count < MostIdentifiesPerHour && (Remaining < 0 || Remaining > LowRemaining);
            }
        }

        internal static int Identified(DateTime now)
        {
            lock (gate)
            {
                identifies.RemoveAll(delegate(DateTime when) { return (now - when).TotalMinutes >= 60; });
                return identifies.Count;
            }
        }

        internal static int WaitSeconds(int failures)
        {
            return Math.Min(MostWaitSeconds, 2 << Math.Max(0, Math.Min(failures, 8)));
        }

        internal static string Beat()
        {
            return "{\"op\":1,\"d\":" + (Seq < 0 ? "null" : Seq.ToString(CultureInfo.InvariantCulture)) + "}";
        }

        internal static string Identify(string token)
        {
            return "{\"op\":2,\"d\":{\"token\":" + Voice.Quote(token) + ",\"intents\":" + Intents + ",\"properties\":{\"os\":\"windows\",\"browser\":\"capri\",\"device\":\"capri\"}}}";
        }

        internal static string Resume(string token)
        {
            return "{\"op\":6,\"d\":{\"token\":" + Voice.Quote(token) + ",\"session_id\":" + Voice.Quote(SessionId) + ",\"seq\":" + Seq.ToString(CultureInfo.InvariantCulture) + "}}";
        }

        internal static GatewayStep Handle(string json, string token, DateTime now)
        {
            GatewayStep step = new GatewayStep();
            Dictionary<string, object> frame = Parse(json);
            if (frame == null) return step;
            long seq;
            if (frame.ContainsKey("s") && frame["s"] != null && long.TryParse(Convert.ToString(frame["s"], CultureInfo.InvariantCulture), out seq)) Seq = seq;
            int op = frame.ContainsKey("op") && frame["op"] is int ? (int)frame["op"] : -1;
            object d = frame.ContainsKey("d") ? frame["d"] : null;
            Dictionary<string, object> data = d as Dictionary<string, object>;
            if (op == 10)
            {
                Interval = Number(data, "heartbeat_interval");
                Acked = true;
                if (SessionId != null && Seq >= 0) step.Send.Add(Resume(token));
                else if (MayIdentify(now))
                {
                    lock (gate) identifies.Add(now);
                    step.Send.Add(Identify(token));
                }
                else step.Hold = true;
            }
            else if (op == 11) Acked = true;
            else if (op == 1) step.Send.Add(Beat());
            else if (op == 7) step.Reconnect = true;
            else if (op == 9)
            {
                if (!(d is bool && (bool)d)) Clear();
                step.Reconnect = true;
                step.Fresh = true;
            }
            else if (op == 0)
            {
                string type = frame.ContainsKey("t") ? Convert.ToString(frame["t"], CultureInfo.InvariantCulture) : "";
                if (type == "READY")
                {
                    SessionId = Text(data, "session_id");
                    ResumeUrl = Text(data, "resume_gateway_url");
                    if (SessionId.Length == 0) SessionId = null;
                    if (ResumeUrl.Length == 0) ResumeUrl = null;
                    SelfId = Text(data != null && data.ContainsKey("user") ? data["user"] as Dictionary<string, object> : null, "id");
                    step.Opened = true;
                    Live = true;
                }
                else if (type == "RESUMED") Live = true;
                else if (type == "GUILD_CREATE" && data != null)
                {
                    if (data.ContainsKey("presences") && data["presences"] is object[])
                    {
                        step.Snapshot = true;
                        foreach (object item in (object[])data["presences"])
                        {
                            GatewayPresence presence = Presence(item as Dictionary<string, object>);
                            if (presence != null) step.Presences.Add(presence);
                        }
                    }
                    VoiceRooms(data, step);
                }
                else if (type == "VOICE_STATE_UPDATE")
                {
                    GatewayVoice voice = VoiceOf(data);
                    if (voice != null) step.Voices.Add(voice);
                }
                else if (type == "PRESENCE_UPDATE")
                {
                    GatewayPresence presence = Presence(data);
                    if (presence != null) step.Presences.Add(presence);
                }
                else if (type == "MESSAGE_CREATE" && data != null && !data.ContainsKey("guild_id"))
                {
                    GatewayMessage message = Message(data);
                    if (message != null) step.Messages.Add(message);
                }
            }
            return step;
        }

        private static void VoiceRooms(Dictionary<string, object> data, GatewayStep step)
        {
            if (data.ContainsKey("channels") && data["channels"] is object[])
                foreach (object item in (object[])data["channels"])
                {
                    Dictionary<string, object> channel = item as Dictionary<string, object>;
                    int kind = Number(channel, "type");
                    string id = Text(channel, "id");
                    if (id.Length > 0 && (kind == VoiceChannelType || kind == StageChannelType)) step.Channels[id] = Text(channel, "name");
                }
            if (data.ContainsKey("voice_states") && data["voice_states"] is object[])
                foreach (object item in (object[])data["voice_states"])
                {
                    GatewayVoice voice = VoiceOf(item as Dictionary<string, object>);
                    if (voice != null && voice.Channel.Length > 0) step.Voices.Add(voice);
                }
        }

        internal static GatewayVoice VoiceOf(Dictionary<string, object> data)
        {
            string user = Text(data, "user_id");
            if (user.Length == 0) return null;
            return new GatewayVoice { User = user, Channel = Text(data, "channel_id") };
        }

        internal static GatewayPresence Presence(Dictionary<string, object> data)
        {
            Dictionary<string, object> user = data != null && data.ContainsKey("user") ? data["user"] as Dictionary<string, object> : null;
            string id = Text(user, "id");
            if (id.Length == 0) return null;
            Dictionary<string, object> client = data.ContainsKey("client_status") ? data["client_status"] as Dictionary<string, object> : null;
            return new GatewayPresence { User = id, Status = Text(data, "status"), Desktop = Text(client, "desktop"), Mobile = Text(client, "mobile") };
        }

        internal static string Closed(int code)
        {
            return Closed(code, null);
        }

        internal static string Closed(int code, string token)
        {
            if (code == 4014 && !PresencesOff)
            {
                presencesOffToken = token;
                PresencesOff = true;
                Clear();
                return null;
            }
            if (code == 4004) return "Discord rejected my bot token, so I've stopped listening for your friends' messages. Put a fresh token in the \"capri bot\" file on your Desktop and say \"connect my Discord\".";
            if (code >= 4010 && code <= 4014) return "Discord refused my live connection (code " + code + "), so I can't hear your friends' messages. The bot's intents in the Discord Developer Portal may need a look.";
            if (code == 4007 || code == 4009) Clear();
            return null;
        }

        internal static GatewayMessage Message(Dictionary<string, object> data)
        {
            Dictionary<string, object> author = data != null && data.ContainsKey("author") ? data["author"] as Dictionary<string, object> : null;
            if (author == null) return null;
            GatewayMessage message = new GatewayMessage
            {
                Id = Text(data, "id"),
                Channel = Text(data, "channel_id"),
                Author = Text(author, "id"),
                Text = Text(data, "content"),
                Bot = author.ContainsKey("bot") && author["bot"] is bool && (bool)author["bot"]
            };
            string global = Text(author, "global_name");
            message.Name = global.Length > 0 ? global : Text(author, "username");
            if (message.Text.Length == 0 && data.ContainsKey("attachments") && data["attachments"] is object[] && ((object[])data["attachments"]).Length > 0) message.Text = "(sent a picture or a file, no text)";
            return message.Id.Length == 0 || message.Author.Length == 0 ? null : message;
        }

        private static void Clear()
        {
            SessionId = null;
            ResumeUrl = null;
            Seq = -1;
        }

        private static void Run(string token, CancellationTokenSource mine)
        {
            int failures = 0;
            Random random = new Random();
            while (!mine.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;
                if (now < heldUntil) { Sleep(mine, 30); continue; }
                string url = SessionId != null && ResumeUrl != null ? ResumeUrl : Url ?? Fetch();
                if (url == null) { failures++; Sleep(mine, WaitSeconds(failures)); continue; }
                if (SessionId == null && !MayIdentify(now)) { HoldOff(now); continue; }
                DateTime began = DateTime.Now;
                int code;
                bool fresh;
                bool held = Converse(url, token, mine, out code, out fresh);
                if (mine.IsCancellationRequested) break;
                if (held) { HoldOff(DateTime.Now); continue; }
                bool presences = !PresencesOff;
                string fatal = Closed(code, token);
                if (presences && PresencesOff)
                {
                    ActivityLog.Write("discord", "presence intent refused", "reading messages only");
                    Action<string> said = Discord.Told;
                    if (said != null) said("Discord won't show me who's online: the Presence intent is off for my bot in the Discord Developer Portal. I'll keep reading your friends' messages.");
                }
                if (fatal != null)
                {
                    ActivityLog.Write("discord", "live connection refused", "code " + code);
                    if (code == 4004) Discord.Failed = fatal;
                    else
                    {
                        refusedToken = token;
                        Refused = fatal;
                    }
                    Action<string> told = Discord.Told;
                    if (told != null) told(fatal);
                    lock (gate) { if (worker == Thread.CurrentThread) { worker = null; runningToken = null; } }
                    return;
                }
                failures = (DateTime.Now - began).TotalSeconds >= StableSeconds ? 0 : failures + 1;
                Sleep(mine, fresh ? 1 + random.Next(5) : WaitSeconds(failures));
            }
        }

        private static void HoldOff(DateTime now)
        {
            heldUntil = now.AddMinutes(HoldMinutes);
            ActivityLog.Write("discord", "live connection held", "too many fresh logins this hour");
            if (holdTold) return;
            holdTold = true;
            Action<string> told = Discord.Told;
            if (told != null) told("Discord's live connection kept dropping, so I'm pausing it for an hour to keep my bot token safe. Friends' messages wait until then.");
        }

        private static string Fetch()
        {
            int status;
            Dictionary<string, object> data = Parse(Discord.Get("/gateway/bot", out status));
            if (status < 200 || status >= 300 || data == null) return null;
            Dictionary<string, object> limit = data.ContainsKey("session_start_limit") ? data["session_start_limit"] as Dictionary<string, object> : null;
            if (limit != null && limit.ContainsKey("remaining")) Remaining = Number(limit, "remaining");
            string url = Text(data, "url");
            return url.Length == 0 ? null : url;
        }

        private static bool Converse(string url, string token, CancellationTokenSource mine, out int code, out bool fresh)
        {
            code = 0;
            fresh = false;
            ClientWebSocket open = new ClientWebSocket();
            lock (gate) socket = open;
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                open.ConnectAsync(new Uri(url + (url.IndexOf('?') >= 0 ? "&" : "?") + "v=10&encoding=json"), mine.Token).Wait();
                byte[] buffer = new byte[65536];
                MemoryStream message = new MemoryStream();
                Task<WebSocketReceiveResult> pending = null;
                DateTime nextBeat = DateTime.MaxValue;
                Random random = new Random();
                while (!mine.IsCancellationRequested)
                {
                    if (pending == null) pending = open.ReceiveAsync(new ArraySegment<byte>(buffer), mine.Token);
                    int wait = nextBeat == DateTime.MaxValue ? 1000 : (int)Math.Max(0, Math.Min(1000, (nextBeat - DateTime.Now).TotalMilliseconds));
                    if (!pending.Wait(wait))
                    {
                        if (DateTime.Now < nextBeat) continue;
                        if (!Acked) return false;
                        Acked = false;
                        Write(open, Beat(), mine);
                        nextBeat = DateTime.Now.AddMilliseconds(Interval);
                        continue;
                    }
                    WebSocketReceiveResult got = pending.Result;
                    pending = null;
                    if (got.MessageType == WebSocketMessageType.Close)
                    {
                        code = open.CloseStatus.HasValue ? (int)open.CloseStatus.Value : 0;
                        return false;
                    }
                    message.Write(buffer, 0, got.Count);
                    if (!got.EndOfMessage) continue;
                    string json = Encoding.UTF8.GetString(message.ToArray());
                    message.SetLength(0);
                    GatewayStep step = Handle(json, token, DateTime.Now);
                    if (Interval > 0 && nextBeat == DateTime.MaxValue) nextBeat = DateTime.Now.AddMilliseconds(Interval * random.NextDouble());
                    foreach (string frame in step.Send) Write(open, frame, mine);
                    if (step.Hold) return true;
                    if (step.Opened)
                    {
                        ActivityLog.Write("discord", "live connection open", null);
                        Action opened = Opened;
                        if (opened != null) ThreadPool.QueueUserWorkItem(delegate { try { opened(); } catch (Exception error) { Brain.Log("discord inbox", -1, error.GetType().Name); } });
                    }
                    Action<GatewayMessage> heard = Heard;
                    if (heard != null) foreach (GatewayMessage one in step.Messages) heard(one);
                    Action<GatewayStep> stepped = Stepped;
                    if (stepped != null && (step.Opened || step.Presences.Count > 0 || step.Voices.Count > 0 || step.Channels.Count > 0)) stepped(step);
                    if (step.Reconnect) { fresh = step.Fresh; return false; }
                }
            }
            catch (Exception) { }
            finally
            {
                Live = false;
                lock (gate) if (socket == open) socket = null;
                try { open.Abort(); } catch (Exception) { }
                open.Dispose();
            }
            return false;
        }

        private static void Write(ClientWebSocket open, string text, CancellationTokenSource mine)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            lock (sending) open.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, mine.Token).Wait();
        }

        private static void Sleep(CancellationTokenSource mine, int seconds)
        {
            mine.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(Math.Max(1, seconds)));
        }

        private static Dictionary<string, object> Parse(string json)
        {
            try { return new JavaScriptSerializer { MaxJsonLength = 64000000 }.DeserializeObject(json ?? "") as Dictionary<string, object>; }
            catch (Exception) { return null; }
        }

        private static string Text(Dictionary<string, object> data, string key)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || value == null) return "";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int Number(Dictionary<string, object> data, string key)
        {
            int value;
            return int.TryParse(Text(data, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }
    }
}
