using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal static class SpotifyLibrary
    {
        internal static string AuthUrl = "https://accounts.spotify.com/authorize";
        internal static string TokenUrl = "https://accounts.spotify.com/api/token";
        internal static string ApiBase = "https://api.spotify.com/v1";
        internal const string Dashboard = "https://developer.spotify.com/dashboard";
        internal const string Redirect = "http://127.0.0.1:8888/callback";
        internal const int Port = 8888;
        internal const string Scopes = "user-read-currently-playing user-library-read user-library-modify user-read-playback-state user-modify-playback-state playlist-read-private";
        internal const string NeedsScope = "Spotify needs one more permission before I can play music for you: say \"connect my Spotify\" once (after your game if you're in one) and allow it.";
        internal const string Dj = "spotify:playlist:37i9dQZF1EYkqdzj48dyYq";
        private static readonly Regex AnyMusic = new Regex(@"^(play\s+)?(me\s+)?((some|any|a|random|good|my)\s+)?(music|songs?|tunes|tracks?|something|spotify)(\s+(for me|on spotify))?$", RegexOptions.IgnoreCase);
        private static readonly Regex Filler = new Regex(@"\b(the|my|your|usual|playlist|album|that|which|i|like|love|to|listen|always|on|spotify|please|again|called|named|one)\b", RegexOptions.IgnoreCase);
        private static readonly Regex MusicWords = new Regex(@"\b(play|playlist|song|track|album|music|shuffle|queue|resume|dj)\b", RegexOptions.IgnoreCase);
        internal const string HandsOff = "Not done: music on Spotify goes through play_music (Spotify's own API), never with your hands. Call play_music with the song, playlist or artist.";

        internal static bool MusicJob(string task)
        {
            string text = task ?? "";
            return Regex.IsMatch(text, @"\bspotify\b", RegexOptions.IgnoreCase) && MusicWords.IsMatch(text) && !Regex.IsMatch(text, @"\b(volume|settings?|crossfade|log ?in|sign ?in|download)\b", RegexOptions.IgnoreCase);
        }

        // play_music through Spotify's Web API: null hands it back to the old path (YouTube, or Spotify not connected outside a game).
        internal static string Music(string song, string app, string kind)
        {
            if (app == "youtube" || NowPlaying(song)) return null;
            if (!Connected) return Lockout.Active ? "In a game I only play Spotify through its own API, and I'm not connected to it yet. Say \"connect my Spotify\" after your game." : null;
            return Play(song, kind);
        }

        private static readonly Regex GenericAsk = new Regex(@"^(?:(?:can you|could you|please|capri|hey capri|just)\W+)*(?:play|put on|start|resume|unpause|turn on)\s+(?:me\s+)?(?:some|any|my|the|a bit of)?\s*(?:music|songs?|tunes|spotify)(?:\s+(?:on spotify|for me|please|again|back on))*\W*$", RegexOptions.IgnoreCase);
        private static readonly Regex VideoWords = new Regex(@"\b(video|youtube|netflix|twitch|movie|film|episode|stream)\b", RegexOptions.IgnoreCase);
        private static readonly Regex SongWords = new Regex(@"\b(music|song|track|spotify|playlist|album|tunes)\b", RegexOptions.IgnoreCase);
        private static DateTime lastSpotify = DateTime.MinValue;
        internal static Func<bool?> PlayingNow = Playing;
        internal static Func<string, string> Controlled = Control;

        internal static string Wanted(string song, string asked)
        {
            string cut = Regex.Split(song ?? "", @"[?!]|\.\s")[0].Trim();
            if (asked != null && GenericAsk.IsMatch(Regex.Split(asked.Trim(), @"[?!]|\.\s")[0].Trim())) return "";
            return cut;
        }

        private static bool? Playing()
        {
            int status;
            string raw = Api("GET", "/me/player", out status);
            if (raw == null) return null;
            Dictionary<string, object> player = Parse(raw);
            if (player == null) return null;
            return Text(player, "is_playing") == "True";
        }

        private static string Control(string action)
        {
            int status;
            if (action == "next" || action == "previous")
                return Api("POST", "/me/player/" + action, out status) == null ? null : action == "next" ? "Skipped to the next track on Spotify." : "Went back a track on Spotify.";
            bool? playing = Playing();
            if (playing == null) return null;
            if (action == "pause" || (action == "play_pause" && playing.Value))
            {
                if (!playing.Value) return "Spotify is already paused.";
                return Api("PUT", "/me/player/pause", out status) == null ? null : "Paused Spotify.";
            }
            if (playing.Value) return "Spotify is already playing.";
            string device = Device();
            if (device == null || Api("PUT", "/me/player/play?device_id=" + Uri.EscapeDataString(device), out status) == null) return null;
            return Silent(device) ? NotStarted : "Spotify is playing again.";
        }

        internal static string Media(ToolArguments args, DateTime now)
        {
            string action = args == null ? "" : args.Action ?? "";
            if (!Regex.IsMatch(action, "^(play_pause|pause|resume|next|previous)$") || !Connected) return null;
            if (!Aimed(action, args.App, args.Asked, PlayingNow, now - lastSpotify < TimeSpan.FromHours(3))) return null;
            string done = Controlled(action) ?? "Spotify didn't take that (" + (Problem ?? "no answer") + "), so I left your other players alone.";
            lastSpotify = now;
            ActivityLog.Write("spotify media", action, done);
            return done;
        }

        internal static void Touched(DateTime now) { lastSpotify = now; }

        internal static string Clause(string asked, string action)
        {
            string verb = action == "pause" ? @"\bpause\b" : action == "next" ? @"\b(?:next|skip)\b" : action == "previous" ? @"\b(?:previous|back)\b" : @"\b(?:resume|unpause|play|start)\b";
            foreach (string part in Regex.Split(asked ?? "", @"\s*(?:,|;|\band then\b|\bthen\b|\band\b)\s*", RegexOptions.IgnoreCase))
                if (Regex.IsMatch(part, verb, RegexOptions.IgnoreCase)) return part;
            return asked ?? "";
        }

        internal static bool Aimed(string action, string app, string asked, Func<bool?> playingNow, bool recent)
        {
            string named = (app ?? "").Trim().ToLowerInvariant();
            if (named.Length > 0) return named == "spotify";
            string part = Clause(asked, action);
            if (VideoWords.IsMatch(part)) return false;
            if (SongWords.IsMatch(part)) return true;
            bool? playing = playingNow();
            if (playing == null) return false;
            bool resuming = action == "resume" || (action == "play_pause" && !playing.Value);
            return resuming ? recent : playing.Value;
        }

        internal static bool Takes(ToolArguments args)
        {
            return args != null && args.App != "youtube" && !NowPlaying(args.Song) && (Connected || Lockout.Active);
        }

        private static bool NowPlaying(string song)
        {
            return Regex.IsMatch((song ?? "").Trim(), @"^((what('|’)?s|what is)\s+)?((currently|now)\s+)?playing( now)?$|^(this|that|the current) (song|track)$", RegexOptions.IgnoreCase);
        }

        internal const string ClosedInGame = "Spotify isn't running on your PC, and I don't open apps during your game. Start Spotify yourself and ask again, or ask after the game.";
        internal const string NotStarted = "I asked Spotify to play, but nothing started on your PC. Is Spotify signed in there?";
        internal const int StartSeconds = 20;
        internal const int SettleSeconds = 3;
        internal static Func<bool> AppRunning = delegate { return System.Diagnostics.Process.GetProcessesByName("Spotify").Length > 0; };
        internal static Func<string> Launch = delegate { return Hands.Do("open_app", new ToolArguments { Name = "Spotify" }); };
        internal static Action<int> Pause = Thread.Sleep;

        private static string Started()
        {
            if (AppRunning()) return null;
            if (Lockout.Active) return ClosedInGame;
            string opened = Launch();
            ActivityLog.Write("spotify play", "Spotify was closed", opened);
            if (Hands.Failed(opened ?? "")) return "Spotify isn't running and I couldn't open it: " + opened;
            for (int waited = 0; waited < StartSeconds; waited++)
            {
                Pause(1000);
                if (waited + 1 >= SettleSeconds && AppRunning() && Device() != null) return null;
            }
            return "I opened Spotify, but it didn't get ready to play in time. Ask again in a moment.";
        }

        private static bool Silent(string device)
        {
            for (int tries = 0; tries < 3; tries++)
            {
                int status;
                string raw = Api("GET", "/me/player", out status);
                if (raw == null) return false;
                Dictionary<string, object> player = Parse(raw);
                Dictionary<string, object> on = player != null && player.ContainsKey("device") ? player["device"] as Dictionary<string, object> : null;
                if (player != null && Text(player, "is_playing") == "True" && (on == null || Text(on, "id") == device)) return false;
                Pause(1000);
            }
            return true;
        }

        internal static string Play(string song, string kind)
        {
            Problem = null;
            string closed = Started();
            if (closed != null) return closed;
            string device = Device();
            if (device == null) return Problem ?? "Spotify isn't open on your PC, so there's nothing for me to play on. Open Spotify once and ask again.";
            string wanted = (song ?? "").Trim();
            string body = null;
            string named = "your music";
            if (wanted.Length > 0 && !AnyMusic.IsMatch(wanted))
            {
                string uri = Find(wanted, kind, out named);
                if (uri == null) return Problem ?? "I couldn't find " + wanted + " on Spotify.";
                body = uri.StartsWith("spotify:track:", StringComparison.Ordinal) ? "{\"uris\":[\"" + uri + "\"]}" : "{\"context_uri\":\"" + uri + "\"}";
            }
            int status;
            string reply = Api("PUT", "/me/player/play?device_id=" + Uri.EscapeDataString(device), body, out status);
            if (reply == null)
            {
                string why = Problem ?? "";
                ActivityLog.Write("spotify play", named, why);
                if (Regex.IsMatch(why, @"scope|permission", RegexOptions.IgnoreCase) || status == 401) return NeedsScope;
                if (Regex.IsMatch(why, @"premium", RegexOptions.IgnoreCase)) return "Spotify only lets apps start music on Premium accounts, so I can't press play from here.";
                return why.Length > 0 ? why : "Spotify didn't start playing.";
            }
            if (Silent(device))
            {
                ActivityLog.Write("spotify play", named, "Spotify said yes but nothing plays on the PC");
                return NotStarted;
            }
            ActivityLog.Write("spotify play", named, "through the Spotify API");
            lastSpotify = DateTime.Now;
            return "Playing " + named + " on Spotify.";
        }

        private static string Device()
        {
            int status;
            Dictionary<string, object> data = Parse(Api("GET", "/me/player/devices", out status));
            object[] devices = data != null && data.ContainsKey("devices") ? data["devices"] as object[] : null;
            if (devices == null) { if (status == 401 || status == 403) Problem = NeedsScope; return null; }
            string pick = null;
            foreach (object one in devices)
            {
                Dictionary<string, object> device = one as Dictionary<string, object>;
                if (device == null || Text(device, "type") != "Computer" || Text(device, "is_restricted") == "True") continue;
                if (pick == null || Text(device, "is_active") == "True") pick = Text(device, "id");
            }
            return pick;
        }

        internal static List<string> PlaylistNames()
        {
            List<string> names = new List<string>();
            if (Started() != null || Device() == null) return names;
            int status;
            Dictionary<string, object> lists = Parse(Api("GET", "/me/playlists?limit=50", out status));
            object[] items = lists != null && lists.ContainsKey("items") ? lists["items"] as object[] : null;
            if (items == null) return names;
            foreach (object one in items)
            {
                string name = Text(one as Dictionary<string, object>, "name");
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
            return names;
        }

        internal static bool Volume(int percent)
        {
            int status;
            return Api("PUT", "/me/player/volume?volume_percent=" + Math.Max(0, Math.Min(100, percent)), null, out status) != null;
        }

        private static string Find(string wanted, string kind, out string named)
        {
            named = wanted;
            string plain = Fold(wanted);
            int status;
            Dictionary<string, object> lists = Parse(Api("GET", "/me/playlists?limit=50", out status));
            object[] items = lists != null && lists.ContainsKey("items") ? lists["items"] as object[] : null;
            string best = null, bestName = null;
            if (items != null)
                foreach (object one in items)
                {
                    Dictionary<string, object> list = one as Dictionary<string, object>;
                    string name = Text(list, "name"), uri = Text(list, "uri");
                    if (name == null || uri == null) continue;
                    string folded = Fold(name);
                    if (folded.Length == 0) continue;
                    bool whole = folded == plain;
                    bool inside = kind == "playlist" && Regex.IsMatch(" " + plain + " ", " " + Regex.Escape(folded) + " ");
                    if ((whole || inside) && (bestName == null || folded.Length > Fold(bestName).Length)) { best = uri; bestName = name; }
                }
            if (best != null) { named = "your " + bestName + " playlist"; return best; }
            if (plain == "dj" || plain == "dg" || plain == "spotify dj" || plain == "the dj") { named = "your Spotify DJ"; return Dj; }
            string type = kind == "playlist" || kind == "album" || kind == "artist" ? kind : "track";
            string query = Regex.Replace(wanted, @"\b(playlist|album)\b", "", RegexOptions.IgnoreCase).Trim();
            Dictionary<string, object> found = Parse(Api("GET", "/search?type=" + type + "&limit=1&q=" + Uri.EscapeDataString(query.Length > 0 ? query : wanted), out status));
            Dictionary<string, object> group = found != null && found.ContainsKey(type + "s") ? found[type + "s"] as Dictionary<string, object> : null;
            object[] hits = group != null && group.ContainsKey("items") ? group["items"] as object[] : null;
            Dictionary<string, object> hit = hits != null && hits.Length > 0 ? hits[0] as Dictionary<string, object> : null;
            if (hit == null || Text(hit, "uri") == null) return null;
            named = Text(hit, "name") ?? wanted;
            return Text(hit, "uri");
        }

        private static string Fold(string text)
        {
            return Regex.Replace(Filler.Replace((text ?? "").ToLowerInvariant(), " "), @"[^\p{L}\p{N}]+", " ").Trim();
        }
        internal const string Expired = "My Spotify sign-in has expired. Say \"connect my Spotify\" and I'll open the sign-in page again.";
        internal static Action<string> Said;
        internal static Thread Waiting;
        internal static int SignInSeconds = 180;
        internal static volatile string Problem;

        internal static readonly Regex Asks = new Regex(
            @"\b(?:connect|link|hook ?up|sign ?(?:me ?)?in(?: ?to)?|log ?in(?: ?to)?|reconnect|pair|set ?up)\s+(?:(?:to|with)\s+)?(?:my\s+|your\s+|the\s+)?spotify(?:\s+account)?\b",
            RegexOptions.IgnoreCase);

        private const int Wait = 15000;
        private static readonly object gate = new object();
        private static readonly object refreshing = new object();
        private static string accessToken;
        private static DateTime accessUntil;
        private static DateTime quietUntil;

        internal static bool Connected
        {
            get
            {
                ForkSettings settings = ForkSettings.Current;
                return !string.IsNullOrEmpty(Voice.Unprotect(settings.SpotifyRefresh)) && !string.IsNullOrEmpty(Voice.Unprotect(settings.SpotifyId));
            }
        }

        internal static void Forget()
        {
            lock (gate)
            {
                accessToken = null;
                quietUntil = DateTime.MinValue;
            }
        }

        internal static string SignIn(Action<string> open)
        {
            if (Lockout.Active) return Lockout.Refusal;
            string id = Voice.Unprotect(ForkSettings.Current.SpotifyId);
            if (string.IsNullOrEmpty(id))
            {
                try { open(Dashboard); }
                catch (Exception) { }
                return "To connect Spotify I need your own free Spotify app first, so I opened developer.spotify.com. Make an app there (pick Web API) with the redirect URI " + Redirect + ", then copy its Client ID into Capri+ settings (Spotify app Client ID) and say \"connect my Spotify\" again.";
            }
            TcpListener listener = new TcpListener(IPAddress.Loopback, Port);
            try { listener.Start(); }
            catch (SocketException) { return "Port " + Port + " on this PC is busy, so the Spotify sign-in can't come back to me. Close whatever uses it and say \"connect my Spotify\" again."; }
            string redirect = Redirect;
            string verifier = Random(48);
            string state = Random(16);
            string challenge = Base64Url(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            string url = AuthUrl + "?client_id=" + Uri.EscapeDataString(id) + "&response_type=code&redirect_uri=" + Uri.EscapeDataString(redirect) + "&scope=" + Uri.EscapeDataString(Scopes)
                + "&code_challenge_method=S256&code_challenge=" + challenge + "&state=" + state;
            try { open(url); }
            catch (Exception)
            {
                listener.Stop();
                return "I couldn't open the browser for the Spotify sign-in.";
            }
            Thread worker = new Thread(delegate() { Finish(listener, redirect, verifier, state, id); });
            worker.IsBackground = true;
            Waiting = worker;
            worker.Start();
            return "Opened the Spotify sign-in page. Finish it there and I'll tell you when it's done.";
        }

        private static void Finish(TcpListener listener, string redirect, string verifier, string state, string id)
        {
            string result;
            try
            {
                string code = null;
                string problem = null;
                DateTime until = DateTime.UtcNow.AddSeconds(SignInSeconds);
                while (code == null && problem == null && DateTime.UtcNow < until)
                {
                    if (!listener.Pending()) { Thread.Sleep(100); continue; }
                    using (TcpClient client = listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 3000;
                        string line = FirstLine(client.GetStream());
                        Match query = Regex.Match(line ?? "", @"^GET /callback\?(\S*) HTTP");
                        string page = "<html><body style=\"font-family:sans-serif\">You can close this tab and go back to Capri.</body></html>";
                        if (query.Success)
                        {
                            Dictionary<string, string> found = Query(query.Groups[1].Value);
                            string seen;
                            if (found.TryGetValue("state", out seen) && seen == state)
                            {
                                if (found.ContainsKey("code")) code = found["code"];
                                else if (found.ContainsKey("error")) problem = "Spotify sign-in was cancelled.";
                            }
                        }
                        byte[] reply = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\nContent-Length: " + Encoding.UTF8.GetByteCount(page) + "\r\n\r\n" + page);
                        client.GetStream().Write(reply, 0, reply.Length);
                    }
                }
                result = problem ?? (code == null ? "Spotify sign-in timed out. Say \"connect my Spotify\" to try again." : Exchange(code, redirect, verifier, id));
            }
            catch (Exception) { result = "Spotify sign-in failed."; }
            finally { listener.Stop(); }
            ActivityLog.Write("spotify sign in", null, result);
            Action<string> tell = Said;
            if (tell != null) tell(result);
        }

        private static string Exchange(string code, string redirect, string verifier, string id)
        {
            int status;
            string retry;
            string reply = Send("POST", TokenUrl, "grant_type=authorization_code&code=" + Uri.EscapeDataString(code) + "&redirect_uri=" + Uri.EscapeDataString(redirect)
                + "&client_id=" + Uri.EscapeDataString(id) + "&code_verifier=" + verifier, null, out status, out retry);
            Dictionary<string, object> data = Parse(reply);
            string access = Text(data, "access_token");
            string refresh = Text(data, "refresh_token");
            if (status != 200 || string.IsNullOrEmpty(access)) return "Spotify didn't accept the sign-in. Check the redirect URI in your Spotify app is " + Redirect + ", then say \"connect my Spotify\" again.";
            if (string.IsNullOrEmpty(refresh)) return "Spotify didn't give me a lasting sign-in. Say \"connect my Spotify\" to try again.";
            ForkSettings settings = ForkSettings.Current;
            settings.SpotifyRefresh = Voice.Protect(refresh);
            Remember(access, Text(data, "expires_in"));
            Dictionary<string, object> me = Parse(Api("GET", "/me", out status));
            string name = Text(me, "display_name");
            settings.SpotifyAccount = string.IsNullOrEmpty(name) ? Text(me, "id") : name;
            ForkSettings.Save();
            return "Signed in to Spotify" + (string.IsNullOrEmpty(settings.SpotifyAccount) ? "." : " as " + settings.SpotifyAccount + ".");
        }

        internal static bool? Liked(string title)
        {
            Problem = null;
            string uri = Playing(title);
            if (uri == null) return null;
            int status;
            string reply = Api("GET", "/me/library/contains?uris=" + Uri.EscapeDataString(uri), out status);
            object[] answer = Parsed(reply) as object[];
            if (answer == null || answer.Length != 1 || !(answer[0] is bool)) return null;
            return (bool)answer[0];
        }

        private static readonly Regex LikeWords = new Regex(@"\b(?:add|save|put)\s+(?:this|that|the|current|it)(?:\s+(?:song|track|tune|one))?\s+(?:to|in|into)\s+(?:my\s+)?(?:liked(?:\s+songs)?|likes|favou?rites)\b|^(?:please\s+)?(?:like|heart)\s+(?:this|that|the|current)\s+(?:song|track|tune)\b", RegexOptions.IgnoreCase);
        private static readonly Regex UnlikeWords = new Regex(@"\b(?:remove|take|delete)\s+(?:this|that|the|current|it)(?:\s+(?:song|track|tune|one))?\s+(?:from|out of)\s+(?:my\s+)?(?:liked(?:\s+songs)?|likes|favou?rites)\b|^(?:please\s+)?(?:unlike|unheart)\s+(?:this|that|the|current)\s+(?:song|track|tune)\b", RegexOptions.IgnoreCase);

        private static readonly Regex TaskLike = new Regex(@"\b(?:add|save|put|like|heart)\b[^.]{0,40}\b(?:song|track|tune|playing)\b[^.]{0,30}\b(?:liked(?:\s+songs)?|likes|favou?rites)\b|\b(?:like|heart)\s+(?:the\s+)?(?:current(?:ly playing)?|playing|this)\s+(?:song|track)\b", RegexOptions.IgnoreCase);
        private static readonly Regex TaskUnlike = new Regex(@"\b(?:remove|take|delete|unlike|unheart)\b[^.]{0,40}\b(?:song|track|tune|playing)\b[^.]{0,30}\b(?:liked(?:\s+songs)?|likes|favou?rites)\b", RegexOptions.IgnoreCase);

        internal static string LikeAsked(string text)
        {
            string plain = Regex.Replace((text ?? "").Trim(), @"^(?:hey\s+)?capri[,\s]+", "", RegexOptions.IgnoreCase);
            if (UnlikeWords.IsMatch(plain)) return "unlike";
            return LikeWords.IsMatch(plain) ? "like" : null;
        }

        internal static string LikeTask(string task)
        {
            string direct = LikeAsked(task);
            if (direct != null) return direct;
            if (TaskUnlike.IsMatch(task ?? "")) return "unlike";
            return TaskLike.IsMatch(task ?? "") ? "like" : null;
        }

        internal static string LikeNow(bool like)
        {
            if (!Connected) return "I need your Spotify connected to change your Liked Songs: say \"connect my Spotify\"" + (Lockout.Active ? " after your game." : ".");
            Problem = null;
            int status;
            Dictionary<string, object> now = Parse(Api("GET", "/me/player/currently-playing", out status));
            Dictionary<string, object> item = now != null && now.ContainsKey("item") ? now["item"] as Dictionary<string, object> : null;
            if (item == null) return Problem ?? "Nothing is playing on Spotify right now, so there's no song to " + (like ? "add." : "remove.");
            string uri = Text(item, "uri"), name = Text(item, "name") ?? "this song";
            if (uri == null || !uri.StartsWith("spotify:track:", StringComparison.Ordinal)) return "What's playing isn't a Spotify track, so it can't go in your Liked Songs.";
            string reply = Api(like ? "PUT" : "DELETE", "/me/library?uris=" + Uri.EscapeDataString(uri), out status);
            ActivityLog.Write("spotify heart", (like ? "like " : "unlike ") + name, reply != null ? "done in your Liked Songs (asked)" : Problem ?? "Spotify said no (" + status + ")");
            if (reply == null) return status == 401 || status == 403 ? NeedsScope : Problem ?? "Spotify didn't take it.";
            return like ? "Added " + name + " to your Liked Songs." : "Removed " + name + " from your Liked Songs.";
        }

        internal static bool? Set(string title, bool like)
        {
            Problem = null;
            string uri = Playing(title);
            if (uri == null) return null;
            int status;
            string reply = Api(like ? "PUT" : "DELETE", "/me/library?uris=" + Uri.EscapeDataString(uri), out status);
            bool done = reply != null;
            ActivityLog.Write("spotify heart", (like ? "like " : "unlike ") + title, done ? "done in your Liked Songs" : Problem ?? "Spotify said no (" + status + ")");
            return done;
        }

        private static string Playing(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            int status;
            Dictionary<string, object> now = Parse(Api("GET", "/me/player/currently-playing", out status));
            Dictionary<string, object> item = now != null && now.ContainsKey("item") ? now["item"] as Dictionary<string, object> : null;
            if (item == null || Text(item, "type") != "track" || (item.ContainsKey("is_local") && item["is_local"] is bool && (bool)item["is_local"])) return null;
            string uri = Text(item, "uri");
            if (uri == null || !uri.StartsWith("spotify:track:", StringComparison.Ordinal) || !Same(Text(item, "name"), title)) return null;
            return uri;
        }

        private static bool Same(string one, string other)
        {
            if (one == null || other == null) return false;
            return string.Equals(Regex.Replace(one.Trim(), @"\s+", " "), Regex.Replace(other.Trim(), @"\s+", " "), StringComparison.OrdinalIgnoreCase);
        }

        private static string Token()
        {
            lock (refreshing) return Refreshed();
        }

        private static string Refreshed()
        {
            lock (gate) if (accessToken != null && DateTime.UtcNow < accessUntil) return accessToken;
            ForkSettings settings = ForkSettings.Current;
            string refresh = Voice.Unprotect(settings.SpotifyRefresh);
            string id = Voice.Unprotect(settings.SpotifyId);
            if (string.IsNullOrEmpty(refresh) || string.IsNullOrEmpty(id)) { Problem = "I'm not signed in to Spotify yet. Say \"connect my Spotify\"."; return null; }
            int status;
            string retry;
            string reply = Send("POST", TokenUrl, "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refresh) + "&client_id=" + Uri.EscapeDataString(id), null, out status, out retry);
            Dictionary<string, object> data = Parse(reply);
            if (status == 400 && Text(data, "error") == "invalid_grant")
            {
                if (Voice.Unprotect(settings.SpotifyRefresh) == refresh)
                {
                    settings.SpotifyRefresh = null;
                    ForkSettings.Save();
                }
                Problem = Expired;
                ActivityLog.Write("spotify", "sign-in", "expired, he must say connect my Spotify again");
                return null;
            }
            string access = Text(data, "access_token");
            if (status != 200 || string.IsNullOrEmpty(access)) { Problem = "I couldn't reach Spotify just now."; return null; }
            string turned = Text(data, "refresh_token");
            if (!string.IsNullOrEmpty(turned) && turned != refresh)
            {
                settings.SpotifyRefresh = Voice.Protect(turned);
                ForkSettings.Save();
            }
            Remember(access, Text(data, "expires_in"));
            return access;
        }

        private static void Remember(string access, string seconds)
        {
            int left;
            if (!int.TryParse(seconds, out left) || left < 120) left = 120;
            lock (gate)
            {
                accessToken = access;
                accessUntil = DateTime.UtcNow.AddSeconds(left - 60);
            }
        }

        private static string Api(string method, string path, out int status)
        {
            return Api(method, path, null, out status);
        }

        private static string Api(string method, string path, string body, out int status)
        {
            status = 0;
            lock (gate) if (DateTime.UtcNow < quietUntil) { Problem = "Spotify asked me to wait a moment."; return null; }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                string token = Token();
                if (token == null) return null;
                string retry;
                string reply = Send(method, ApiBase + path, body, token, out status, out retry);
                if (status == 401 && attempt == 0)
                {
                    lock (gate) accessToken = null;
                    continue;
                }
                if (status >= 200 && status < 300) return reply ?? "";
                if (status == 429)
                {
                    int seconds;
                    if (!int.TryParse(retry, out seconds) || seconds < 1) seconds = 30;
                    lock (gate) quietUntil = DateTime.UtcNow.AddSeconds(Math.Min(seconds, 3600));
                    Problem = "Spotify asked me to wait a moment.";
                    return null;
                }
                Dictionary<string, object> error = Parse(reply);
                Dictionary<string, object> inner = error != null && error.ContainsKey("error") ? error["error"] as Dictionary<string, object> : null;
                string said = Clean(Text(inner, "message"));
                Problem = status == 0 ? "I couldn't reach Spotify just now." : "Spotify said no (" + status + ")" + (said.Length == 0 ? "." : ": " + said);
                if (status == 403) ActivityLog.Write("spotify", method + " " + Regex.Replace(path, @"\?.*$", ""), Problem);
                return null;
            }
            Problem = Expired;
            return null;
        }

        private static string Send(string method, string url, string form, string bearer, out int status, out string retry)
        {
            status = 0;
            retry = null;
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                request.Timeout = Wait;
                request.ReadWriteTimeout = Wait;
                if (bearer != null) request.Headers["Authorization"] = "Bearer " + bearer;
                if (form != null)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(form);
                    request.ContentType = form.StartsWith("{", StringComparison.Ordinal) ? "application/json" : "application/x-www-form-urlencoded";
                    request.ContentLength = bytes.Length;
                    using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
                }
                else if (method == "PUT" || method == "DELETE" || method == "POST") request.ContentLength = 0;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    status = (int)response.StatusCode;
                    using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
                }
            }
            catch (WebException error)
            {
                HttpWebResponse response = error.Response as HttpWebResponse;
                if (response == null) return null;
                using (response)
                {
                    status = (int)response.StatusCode;
                    retry = response.Headers["Retry-After"];
                    try { using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd(); }
                    catch (Exception) { return null; }
                }
            }
            catch (Exception) { return null; }
        }

        private static object Parsed(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return new JavaScriptSerializer().DeserializeObject(json); }
            catch (Exception) { return null; }
        }

        private static Dictionary<string, object> Parse(string json) { return Parsed(json) as Dictionary<string, object>; }

        private static string Text(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null) return null;
            return Convert.ToString(data[key], System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Clean(string text)
        {
            string plain = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return plain.Length > 160 ? plain.Substring(0, 160) : plain;
        }

        private static string FirstLine(Stream stream)
        {
            StringBuilder line = new StringBuilder();
            int read;
            while ((read = stream.ReadByte()) >= 0 && read != '\n' && line.Length < 4096) line.Append((char)read);
            return line.ToString().TrimEnd('\r');
        }

        private static Dictionary<string, string> Query(string query)
        {
            Dictionary<string, string> found = new Dictionary<string, string>();
            foreach (string pair in query.Split('&'))
            {
                int equals = pair.IndexOf('=');
                if (equals <= 0) continue;
                found[Uri.UnescapeDataString(pair.Substring(0, equals).Replace('+', ' '))] = Uri.UnescapeDataString(pair.Substring(equals + 1).Replace('+', ' '));
            }
            return found;
        }

        private static string Random(int bytes)
        {
            byte[] data = new byte[bytes];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(data);
            return Base64Url(data);
        }

        private static string Base64Url(byte[] data) { return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    }
}
