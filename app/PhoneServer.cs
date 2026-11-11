using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace CapriDesktop
{
    internal sealed class PhoneServer
    {
        internal const int MostBody = 1048576;
        internal const int ServePort = 8443;
        internal const int MostAudio = 16000 * 2 * 30;
        private const int MostHostShown = 80;
        private const string Policy = "default-src 'self'; connect-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'none'";
        internal static Func<string> TailnetName = delegate { return ""; };
        internal static Func<string, bool, string> Said;
        internal static Func<short[], string> Listened;
        internal static Func<int> Turn = delegate { return 0; };
        internal static Func<int, byte[]> Audio = delegate { return null; };
        internal static Func<string> State = delegate { return null; };
        internal static Func<string> History = delegate { return null; };
        internal static Func<string> Pc = delegate { return null; };
        internal static Func<string> ShotRefusal = delegate { return PhoneView.Paused; };
        internal static Func<byte[]> Shot = delegate { return null; };
        internal static Func<string, string> Key = delegate { return null; };
        internal static Func<string> Settings = delegate { return null; };
        internal static Func<string, string, string> Setting = delegate { return null; };
        internal static Func<string, string, string, string> Happened = delegate { return null; };
        internal static Action<List<string>> Unbacked = delegate { };

        internal static string Json(string text)
        {
            StringBuilder quoted = new StringBuilder("\"");
            foreach (char one in text ?? "")
            {
                if (one == '"' || one == '\\') quoted.Append('\\').Append(one);
                else if (one < ' ') quoted.Append("\\u").Append(((int)one).ToString("x4"));
                else quoted.Append(one);
            }
            return quoted.Append('"').ToString();
        }
        private readonly int port;
        private HttpListener listener;
        private bool loginChecked;

        internal PhoneServer(int port) { this.port = port; }

        internal bool Running { get { HttpListener mine = listener; return mine != null && mine.IsListening; } }

        internal string Start()
        {
            if (Running) return null;
            HttpListener made = new HttpListener();
            made.Prefixes.Add("http://127.0.0.1:" + port + "/");
            try { made.Start(); }
            catch (HttpListenerException error) { made.Close(); return error.Message; }
            listener = made;
            loginChecked = false;
            Thread loop = new Thread(Serve) { IsBackground = true, Name = "phone server" };
            loop.Start(made);
            ActivityLog.Write("phone", "server on", "127.0.0.1:" + port);
            return null;
        }

        internal void Stop()
        {
            HttpListener was = listener;
            listener = null;
            if (was == null) return;
            try { was.Stop(); was.Close(); }
            catch (ObjectDisposedException) { }
            ActivityLog.Write("phone", "server off", null);
        }

        private void Serve(object state)
        {
            HttpListener mine = (HttpListener)state;
            while (mine.IsListening)
            {
                HttpListenerContext context;
                try { context = mine.GetContext(); }
                catch (HttpListenerException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (InvalidOperationException) { return; }
                ThreadPool.QueueUserWorkItem(delegate { Handle(context); });
            }
        }

        internal static bool HostOk(string host, int port, string tailnet)
        {
            string asked = (host ?? "").Trim().ToLowerInvariant();
            if (asked.Length == 0) return false;
            if (asked == "127.0.0.1:" + port || asked == "localhost:" + port) return true;
            string name = (tailnet ?? "").Trim().ToLowerInvariant();
            return name.EndsWith(".ts.net", StringComparison.Ordinal) && (asked == name || asked == name + ":443" || asked == name + ":" + ServePort);
        }

        internal static bool OriginOk(string origin, string tailnet, int port)
        {
            if (origin == null) return true;
            string asked = origin.Trim().ToLowerInvariant();
            if (asked == "http://127.0.0.1:" + port || asked == "http://localhost:" + port) return true;
            string name = (tailnet ?? "").Trim().ToLowerInvariant();
            return name.EndsWith(".ts.net", StringComparison.Ordinal) && (asked == "https://" + name || asked == "https://" + name + ":" + ServePort);
        }

        internal static string Shown(string host)
        {
            StringBuilder kept = new StringBuilder();
            foreach (char one in (host ?? "").ToLowerInvariant())
                if (kept.Length < MostHostShown && ((one >= 'a' && one <= 'z') || (one >= '0' && one <= '9') || one == '.' || one == ':' || one == '-')) kept.Append(one);
            return kept.ToString();
        }

        internal static string Bearer(string header)
        {
            string value = (header ?? "").Trim();
            return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value.Substring(7).Trim() : null;
        }

        internal static string Field(string body, string name)
        {
            foreach (string pair in (body ?? "").Split('&'))
            {
                int cut = pair.IndexOf('=');
                if (cut <= 0) continue;
                if (Uri.UnescapeDataString(pair.Substring(0, cut).Replace('+', ' ')) == name) return Uri.UnescapeDataString(pair.Substring(cut + 1).Replace('+', ' '));
            }
            return null;
        }

        private void Handle(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            try
            {
                response.Headers["Content-Security-Policy"] = Policy;
                response.Headers["X-Content-Type-Options"] = "nosniff";
                response.Headers["Cache-Control"] = "no-store";
                if (request.RemoteEndPoint == null || !IPAddress.IsLoopback(request.RemoteEndPoint.Address)) { Refuse(response, 403, "not loopback"); return; }
                if (!HostOk(request.Headers["Host"], port, TailnetName())) { Refuse(response, 421, "host", Shown(request.Headers["Host"])); return; }
                if (!OriginOk(request.Headers["Origin"], TailnetName(), port)) { Refuse(response, 403, "origin"); return; }
                if (!loginChecked && !Local(request.Headers["Host"]))
                {
                    loginChecked = true;
                    ActivityLog.Write("phone", "tailscale login " + (string.IsNullOrEmpty(request.Headers["Tailscale-User-Login"]) ? "absent" : "present"), null);
                }
                string path = request.Url.AbsolutePath;
                if (request.HttpMethod == "GET" && !path.StartsWith("/api/", StringComparison.Ordinal))
                {
                    PhoneFile file = PhoneFiles.Find(path);
                    if (file == null) { Refuse(response, 404, "unknown file"); return; }
                    response.Headers["Cache-Control"] = file.Keep ? "max-age=86400" : "no-cache";
                    SendBytes(response, 200, file.Type, file.Bytes);
                    return;
                }
                if (path == "/pair" && request.HttpMethod == "POST")
                {
                    string body = Body(request);
                    if (body == null) { Refuse(response, 413, "too big"); return; }
                    DateTime now = DateTime.Now;
                    string token = PhoneAuth.Pair(Field(body, "code"), Field(body, "name"), now);
                    if (token == null) { Refuse(response, 403, PhoneAuth.Locked(now) ? "pairing locked" : "wrong code"); return; }
                    ActivityLog.Write("phone", "paired", null);
                    Send(response, 200, "application/json", "{\"token\":\"" + token + "\"}");
                    return;
                }
                if (PhoneAuth.Device(Bearer(request.Headers["Authorization"])) == null) { Refuse(response, 401, "no token"); return; }
                if (path == "/api/ping" && request.HttpMethod == "GET") { Send(response, 200, "application/json", "{\"ok\":true}"); return; }
                if (path == "/api/say" && request.HttpMethod == "POST") { Say(request, response); return; }
                if (path == "/api/actions" && request.HttpMethod == "GET")
                {
                    List<PhoneAction> waiting = PhoneActions.Pending(DateTime.Now);
                    ActivityLog.Write("phone", "actions fetched", waiting.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Send(response, 200, "application/json", PhoneActions.Json(waiting, DateTime.Now));
                    return;
                }
                if (path == "/api/actions/done" && request.HttpMethod == "POST")
                {
                    string body = Body(request);
                    if (body == null) { Refuse(response, 413, "too big"); return; }
                    int count;
                    List<string> backups = PhoneActions.Done(PhoneActions.Ids(PhoneShortcuts.Value(body, PhoneShortcuts.IsJson(request.ContentType), "ids")), DateTime.Now, out count);
                    if (backups.Count > 0) Unbacked(backups);
                    Send(response, 200, "application/json", "{\"done\":" + count + "}");
                    return;
                }
                if (path == "/api/event" && request.HttpMethod == "POST") { Happen(request, response); return; }
                if (path == "/api/share" && request.HttpMethod == "POST") { Share(request, response); return; }
                if (path == "/api/shortcutkey" && request.HttpMethod == "POST")
                {
                    string key = PhoneAuth.Mint(PhoneShortcuts.Device, DateTime.Now);
                    ActivityLog.Write("phone", "shortcuts key made", "the old one is off");
                    Send(response, 200, "application/json", "{\"token\":\"" + key + "\"}");
                    return;
                }
                if (path == "/api/hear" && request.HttpMethod == "POST") { Hear(request, response); return; }
                if (path == "/api/events" && request.HttpMethod == "GET") { Events(request, response); return; }
                if (path == "/api/pc" && request.HttpMethod == "GET")
                {
                    string seen = Pc();
                    if (seen == null) { response.StatusCode = 204; return; }
                    Send(response, 200, "application/json", seen);
                    return;
                }
                if (path == "/api/shot" && request.HttpMethod == "GET") { ScreenShot(response); return; }
                if (path == "/api/key" && request.HttpMethod == "POST")
                {
                    string body = Body(request);
                    string key = body == null ? null : Field(body, "key");
                    if (key == null || Array.IndexOf(PhoneButtons.Keys, key) < 0) { Refuse(response, 400, "unknown key"); return; }
                    string pressed = Key(key);
                    if (pressed == null) { Refuse(response, 429, "busy"); return; }
                    Send(response, 200, "application/json", "{\"said\":" + Json(pressed) + "}");
                    return;
                }
                if (path == "/api/settings" && request.HttpMethod == "GET")
                {
                    string listed = Settings();
                    if (listed == null) { response.StatusCode = 204; return; }
                    Send(response, 200, "application/json", listed);
                    return;
                }
                if (path == "/api/setting" && request.HttpMethod == "POST")
                {
                    string body = Body(request);
                    string key = body == null ? null : Field(body, "key"), value = body == null ? null : Field(body, "value");
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) { Refuse(response, 400, "bad setting"); return; }
                    string changed = Setting(key, value);
                    if (changed == null) { response.StatusCode = 503; return; }
                    Send(response, 200, "application/json", "{\"said\":" + Json(changed) + "}");
                    return;
                }
                if (path == "/api/history" && request.HttpMethod == "GET")
                {
                    string kept = History();
                    if (kept == null) { response.StatusCode = 204; return; }
                    Send(response, 200, "application/json", kept);
                    return;
                }
                if (path == "/api/ack" && request.HttpMethod == "POST")
                {
                    string body = Body(request);
                    int id;
                    if (body == null || !int.TryParse(Field(body, "id"), out id)) { Refuse(response, 400, "bad ack"); return; }
                    Send(response, 200, "application/json", PhoneLive.Ack(id) ? "{\"ok\":true}" : "{\"ok\":false}");
                    return;
                }
                if (path == "/api/state" && request.HttpMethod == "GET")
                {
                    string pose = State();
                    if (pose == null) { response.StatusCode = 204; return; }
                    Send(response, 200, "application/json", pose);
                    return;
                }
                if (path == "/api/voice" && request.HttpMethod == "GET")
                {
                    int turn;
                    byte[] wav = int.TryParse(request.QueryString["turn"], out turn) ? Audio(turn) : null;
                    if (wav == null) { response.StatusCode = 204; return; }
                    SendBytes(response, 200, "audio/wav", wav);
                    return;
                }
                Refuse(response, 404, "unknown route");
            }
            catch (HttpListenerException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                try { response.Close(); }
                catch (HttpListenerException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private static void Hear(HttpListenerRequest request, HttpListenerResponse response)
        {
            byte[] audio = Bytes(request, MostAudio);
            if (audio == null) { Refuse(response, 413, "too much audio"); return; }
            if (audio.Length % 2 != 0) { Refuse(response, 400, "broken audio"); return; }
            Func<short[], string> listened = Listened;
            Func<string, bool, string> said = Said;
            if (listened == null || said == null) { Refuse(response, 503, "no ears"); return; }
            short[] samples = new short[audio.Length / 2];
            Buffer.BlockCopy(audio, 0, samples, 0, audio.Length);
            ActivityLog.Write("phone", "voice line", (samples.Length / 16).ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms, page " + Hertz(request.QueryString["rate"]) + " Hz, mic " + Hertz(request.QueryString["mic"]) + " Hz" + PhoneMicTrace.Extra(request.QueryString));
            PhoneMicTrace.Keep(samples);
            string heard = (listened(samples) ?? "").Trim();
            if (heard.Length == 0) { Send(response, 200, "application/json", "{\"heard\":\"\",\"said\":\"\",\"turn\":0}"); return; }
            string answer = said(heard, request.QueryString["voice"] == "1");
            if (answer == null) { Refuse(response, 429, "busy"); return; }
            Send(response, 200, "application/json", "{\"heard\":" + Json(heard) + ",\"said\":" + Json(answer) + ",\"turn\":" + Turn() + "}");
        }

        private static void Say(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body = Body(request);
            if (body == null) { Refuse(response, 413, "too big"); return; }
            bool json = PhoneShortcuts.IsJson(request.ContentType);
            string text = (PhoneShortcuts.Value(body, json, "text") ?? "").Trim();
            Func<string, bool, string> said = Said;
            if (text.Length == 0 || said == null) { Refuse(response, 400, "nothing to say"); return; }
            bool run = PhoneShortcuts.Yes(PhoneShortcuts.Value(body, json, "run"));
            ActivityLog.Write("phone", run ? "shortcut line" : "line", null);
            Answer(response, said, text, PhoneShortcuts.Yes(PhoneShortcuts.Value(body, json, "voice")), run);
        }

        private static void Answer(HttpListenerResponse response, Func<string, bool, string> said, string text, bool voice, bool run)
        {
            bool handing = run && PhoneActions.Begin();
            List<PhoneAction> handed = new List<PhoneAction>();
            string answer;
            try { answer = said(text, voice); }
            finally { if (handing) handed = PhoneActions.End(); }
            if (answer == null) { Refuse(response, 429, "busy"); return; }
            int turn = Turn();
            StringBuilder made = new StringBuilder("{\"said\":").Append(Json(answer)).Append(",\"turn\":").Append(turn);
            if (run && voice) made.Append(",\"voice\":").Append(Json("api/voice?turn=" + turn));
            if (run) made.Append(",\"actions\":").Append(PhoneActions.List(handed, DateTime.Now));
            Send(response, 200, "application/json", made.Append('}').ToString());
        }

        private static void Happen(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body = Body(request);
            if (body == null) { Refuse(response, 413, "too big"); return; }
            bool json = PhoneShortcuts.IsJson(request.ContentType);
            string kind = (PhoneShortcuts.Value(body, json, "kind") ?? "").Trim().ToLowerInvariant();
            string value;
            if (Array.IndexOf(PhoneShortcuts.Events, kind) < 0) { Refuse(response, 400, "unknown event"); return; }
            if (!PhoneShortcuts.EventValue(kind, PhoneShortcuts.Value(body, json, "value"), out value)) { Refuse(response, 400, "bad event", kind); return; }
            ActivityLog.Write("phone", "event", kind);
            string told = Happened(kind, value, PhoneShortcuts.Name(PhoneShortcuts.Value(body, json, "name")));
            Send(response, 200, "application/json", "{\"ok\":true,\"said\":" + Json(told ?? "") + "}");
        }

        private static void Share(HttpListenerRequest request, HttpListenerResponse response)
        {
            byte[] bytes = Bytes(request, PhoneShortcuts.MostShare);
            if (bytes == null) { Refuse(response, 413, "share too big"); return; }
            Func<string, bool, string> said = Said;
            if (said == null) { Refuse(response, 503, "no brain"); return; }
            string type = (request.ContentType ?? "").Trim().ToLowerInvariant();
            string line = (request.QueryString["text"] ?? "").Trim();
            bool voice = PhoneShortcuts.Yes(request.QueryString["voice"]);
            if (type.StartsWith("image/", StringComparison.Ordinal))
            {
                if (!PhoneShare.Jpeg(bytes)) { Refuse(response, 415, "not a jpeg"); return; }
                PhoneShare.Keep(bytes, DateTime.Now);
                ActivityLog.Write("phone", "shared picture", (bytes.Length / 1024) + " KB, in memory only");
                try { Answer(response, said, PhoneShare.Asked + (line.Length > 0 ? " " + line : PhoneShare.Wonder), voice, false); }
                finally { PhoneShare.Take(DateTime.Now); }
                return;
            }
            string shared = Encoding.UTF8.GetString(bytes).Trim();
            if (PhoneShortcuts.IsJson(type))
            {
                string json = shared;
                shared = (PhoneShortcuts.JsonField(json, "link") ?? PhoneShortcuts.JsonField(json, "text") ?? "").Trim();
                if (line.Length == 0) line = (PhoneShortcuts.JsonField(json, "line") ?? "").Trim();
            }
            if (shared.Length == 0 && line.Length == 0) { Refuse(response, 400, "nothing shared"); return; }
            if (shared.Length > PhoneShortcuts.MostShareText) shared = shared.Substring(0, PhoneShortcuts.MostShareText);
            ActivityLog.Write("phone", "shared text", null);
            Answer(response, said, ((line.Length > 0 ? line : "I shared this from my phone:") + "\n" + shared).Trim(), voice, false);
        }

        private static void Events(HttpListenerRequest request, HttpListenerResponse response)
        {
            int after;
            if (!int.TryParse(request.QueryString["after"], out after)) after = -1;
            List<PhoneEvent> got = PhoneLive.Wait(after, PhoneLive.PollMs);
            if (got == null) { Refuse(response, 429, "too many waits"); return; }
            Send(response, 200, "application/json", PhoneLive.Json(got));
        }

        private static void ScreenShot(HttpListenerResponse response)
        {
            string refused = ShotRefusal();
            if (refused != null)
            {
                ActivityLog.Write("phone", "screenshot refused", refused == PhoneView.Paused ? "paused" : "never-look app in front");
                Send(response, 409, "text/plain; charset=utf-8", refused);
                return;
            }
            byte[] picture = Shot();
            if (picture == null) { ActivityLog.Write("phone", "screenshot failed", null); response.StatusCode = 503; return; }
            ActivityLog.Write("phone", "screenshot sent", (picture.Length / 1024) + " KB, kept nowhere");
            SendBytes(response, 200, "image/jpeg", picture);
        }

        private static int Hertz(string asked)
        {
            int rate;
            return int.TryParse(asked, out rate) && rate > 0 && rate <= 384000 ? rate : 0;
        }

        private static string Body(HttpListenerRequest request)
        {
            byte[] bytes = Bytes(request, MostBody);
            return bytes == null ? null : Encoding.UTF8.GetString(bytes);
        }

        private static byte[] Bytes(HttpListenerRequest request, int most)
        {
            if (!request.HasEntityBody) return new byte[0];
            byte[] buffer = new byte[most + 1];
            int total = 0, read;
            Stream input = request.InputStream;
            while (total < buffer.Length && (read = input.Read(buffer, total, buffer.Length - total)) > 0) total += read;
            if (total > most) return null;
            byte[] kept = new byte[total];
            Buffer.BlockCopy(buffer, 0, kept, 0, total);
            return kept;
        }

        private bool Local(string host)
        {
            string asked = (host ?? "").Trim().ToLowerInvariant();
            return asked == "127.0.0.1:" + port || asked == "localhost:" + port;
        }

        private static void Refuse(HttpListenerResponse response, int status, string why)
        {
            Refuse(response, status, why, null);
        }

        private static void Refuse(HttpListenerResponse response, int status, string why, string detail)
        {
            ActivityLog.Write("phone", "refused " + why, string.IsNullOrEmpty(detail) ? null : detail);
            Send(response, status, "text/plain; charset=utf-8", status == 401 ? "pair first" : "no");
        }

        private static void Send(HttpListenerResponse response, int status, string type, string text)
        {
            SendBytes(response, status, type, Encoding.UTF8.GetBytes(text));
        }

        private static void SendBytes(HttpListenerResponse response, int status, string type, byte[] bytes)
        {
            response.StatusCode = status;
            response.ContentType = type;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }
    }
}
