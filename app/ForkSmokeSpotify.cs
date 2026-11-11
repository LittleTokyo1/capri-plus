using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed class SpotifyFake
    {
        internal const string Id = "0123456789abcdef0123456789abcdef";
        internal const string RedHorse = "{\"is_playing\":true,\"item\":{\"type\":\"track\",\"uri\":\"spotify:track:T9\",\"name\":\"Red Horse\",\"is_local\":false}}";
        internal readonly List<string> Log = new List<string>();
        internal readonly List<string> Liked = new List<string>();
        internal string Playing;
        internal string Verifier;
        internal string Devices;
        internal string PlayRefused;
        internal readonly int Port;
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        private volatile bool stopped;

        internal SpotifyFake()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Thread thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Start();
        }

        internal string Base { get { return "http://127.0.0.1:" + Port; } }

        internal void Close()
        {
            stopped = true;
            listener.Stop();
        }

        internal static string Track(string id, string name) { return "{\"currently_playing_type\":\"track\",\"item\":{\"type\":\"track\",\"is_local\":false,\"name\":\"" + name + "\",\"uri\":\"spotify:track:" + id + "\"}}"; }

        internal int Count(string start) { lock (Log) return Log.FindAll(delegate(string line) { return line.StartsWith(start, StringComparison.Ordinal); }).Count; }

        private void Loop()
        {
            while (!stopped)
            {
                try
                {
                    if (!listener.Pending()) { Thread.Sleep(10); continue; }
                    using (TcpClient client = listener.AcceptTcpClient()) Serve(client);
                }
                catch (Exception) { }
            }
        }

        private void Serve(TcpClient client)
        {
            client.ReceiveTimeout = 5000;
            NetworkStream stream = client.GetStream();
            StringBuilder head = new StringBuilder();
            byte[] one = new byte[1];
            while (!head.ToString().EndsWith("\r\n\r\n") && stream.Read(one, 0, 1) == 1) head.Append((char)one[0]);
            string[] lines = head.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' ');
            int length = 0;
            string auth = "";
            foreach (string line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line.Substring(15).Trim());
                if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) auth = line.Substring(14).Trim();
            }
            byte[] bytes = new byte[length];
            int got = 0;
            while (got < length) { int n = stream.Read(bytes, got, length - got); if (n <= 0) break; got += n; }
            string body = Encoding.UTF8.GetString(bytes, 0, got);
            lock (Log) Log.Add(first[0] + " " + first[1] + " | " + auth + " | " + body);
            int code;
            string reply = Route(first[0], first[1], auth, body, out code);
            byte[] data = Encoding.UTF8.GetBytes(reply);
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " X\r\nContent-Type: application/json\r\nConnection: close\r\nContent-Length: " + data.Length + "\r\n\r\n");
            stream.Write(header, 0, header.Length);
            stream.Write(data, 0, data.Length);
        }

        private string Route(string method, string path, string auth, string body, out int code)
        {
            code = 200;
            if (path == "/api/token")
            {
                if (body.Contains("client_secret")) { code = 400; return "{\"error\":\"invalid_client\"}"; }
                if (body.Contains("grant_type=authorization_code"))
                {
                    Match verifier = Regex.Match(body, @"code_verifier=([^&]+)");
                    if (body.Contains("code=good-code") && verifier.Success && body.Contains("client_id=" + Id) && body.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A"))
                    {
                        Verifier = verifier.Groups[1].Value;
                        return "{\"access_token\":\"AT-sp-1\",\"refresh_token\":\"RT-sp-1\",\"expires_in\":3600}";
                    }
                }
                else if (body.Contains("client_id=" + Id) && body.Contains("refresh_token=RT-sp-1")) return "{\"access_token\":\"AT-sp-2\",\"refresh_token\":\"RT-sp-2\",\"expires_in\":3600}";
                else if (body.Contains("client_id=" + Id) && body.Contains("refresh_token=RT-sp-2")) return "{\"access_token\":\"AT-sp-3\",\"expires_in\":3600}";
                code = 400;
                return "{\"error\":\"invalid_grant\",\"error_description\":\"Refresh token revoked\"}";
            }
            if (!auth.StartsWith("Bearer AT-sp-", StringComparison.Ordinal)) { code = 401; return "{\"error\":{\"status\":401,\"message\":\"The access token expired\"}}"; }
            if (path == "/v1/me") return "{\"id\":\"kasper\",\"display_name\":\"Kasper\"}";
            if (path.StartsWith("/v1/me/player/currently-playing", StringComparison.Ordinal))
            {
                if (Playing == null) { code = 204; return ""; }
                return Playing;
            }
            if (path == "/v1/me/player/devices") return Devices ?? "{\"devices\":[{\"id\":\"PHONE1\",\"is_active\":false,\"type\":\"Smartphone\",\"is_restricted\":false},{\"id\":\"PC1\",\"is_active\":false,\"type\":\"Computer\",\"is_restricted\":false}]}";
            if (path.StartsWith("/v1/me/playlists", StringComparison.Ordinal)) return "{\"items\":[{\"name\":\"DG\",\"uri\":\"spotify:playlist:P1\"},{\"name\":\"Gym Mix\",\"uri\":\"spotify:playlist:P2\"}]}";
            if (path.StartsWith("/v1/search?", StringComparison.Ordinal))
            {
                if (path.Contains("type=track")) return "{\"tracks\":{\"items\":[{\"name\":\"Red Horse\",\"uri\":\"spotify:track:T9\"}]}}";
                if (path.Contains("type=artist")) return "{\"artists\":{\"items\":[{\"name\":\"Kendrick Lamar\",\"uri\":\"spotify:artist:A1\"}]}}";
                if (path.Contains("type=playlist")) return "{\"playlists\":{\"items\":[{\"name\":\"Chill Vibes\",\"uri\":\"spotify:playlist:P9\"}]}}";
                return "{}";
            }
            if (path.StartsWith("/v1/me/player/play", StringComparison.Ordinal) && method == "PUT")
            {
                if (PlayRefused != null) { code = 403; return "{\"error\":{\"status\":403,\"message\":\"" + PlayRefused + "\"}}"; }
                code = 204;
                return "";
            }
            Match uris = Regex.Match(path, @"[?&]uris=([^&]+)");
            string uri = uris.Success ? Uri.UnescapeDataString(uris.Groups[1].Value) : "";
            if (path.StartsWith("/v1/me/library/contains?", StringComparison.Ordinal) && method == "GET") lock (Liked) return "[" + (Liked.Contains(uri) ? "true" : "false") + "]";
            if (path.StartsWith("/v1/me/library?", StringComparison.Ordinal) && uri.StartsWith("spotify:track:", StringComparison.Ordinal))
            {
                lock (Liked)
                {
                    if (method == "PUT" && !Liked.Contains(uri)) Liked.Add(uri);
                    if (method == "DELETE") Liked.Remove(uri);
                }
                return "";
            }
            code = 404;
            return "{\"error\":{\"status\":404,\"message\":\"nothing here\"}}";
        }
    }

    internal sealed partial class Companion
    {
        private void SmokeSpotify(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            Func<string, bool> keptRunning = Lockout.Running;
            string keptAuth = SpotifyLibrary.AuthUrl, keptToken = SpotifyLibrary.TokenUrl, keptApi = SpotifyLibrary.ApiBase;
            Action<string> keptSaid = SpotifyLibrary.Said;
            int keptSeconds = SpotifyLibrary.SignInSeconds;
            Func<bool?> keptHearted = Tunes.Hearted;
            Func<Tune, string, bool> keptSend = Tunes.Send;
            Func<string> keptTitle = SpotifyHeart.Title;
            Func<bool> keptAppRunning = SpotifyLibrary.AppRunning;
            Func<string> keptLaunch = SpotifyLibrary.Launch;
            Action<int> keptPause = SpotifyLibrary.Pause;
            Func<bool?> keptPeek = SpotifyHeart.Peek;
            string folder = Path.Combine(Path.GetTempPath(), "capri-spotify-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            SpotifyFake fake = new SpotifyFake();
            StringBuilder everything = new StringBuilder();
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                SpotifyLibrary.AuthUrl = fake.Base + "/authorize";
                SpotifyLibrary.TokenUrl = fake.Base + "/api/token";
                SpotifyLibrary.ApiBase = fake.Base + "/v1";
                SpotifyLibrary.AppRunning = delegate { return true; };
                SpotifyLibrary.Pause = delegate { };
                SpotifyLibrary.Forget();

                foreach (string asked in new[] { "connect my spotify", "link spotify", "sign in to spotify", "hook up my spotify account", "log into spotify", "set up spotify", "reconnect spotify" })
                {
                    string tool;
                    ToolArguments args;
                    if (!SpotifyLibrary.Asks.IsMatch(asked) || !Hands.Heard(asked, out tool, out args) || tool != "spotify" || args.Action != "sign_in") throw new Exception("A Spotify sign-in ask did not reach the sign-in: " + asked);
                }
                foreach (string chat in new[] { "open spotify", "play spotify", "turn spotify down", "what's playing on spotify", "i love spotify", "like this song" })
                    if (SpotifyLibrary.Asks.IsMatch(chat)) throw new Exception("Ordinary talk was taken as a Spotify sign-in: " + chat);

                string openedUrl = null;
                string noId = SpotifyLibrary.SignIn(delegate(string url) { openedUrl = url; });
                if (SpotifyLibrary.Connected || openedUrl != SpotifyLibrary.Dashboard || !noId.Contains("developer.spotify.com") || !noId.Contains(SpotifyLibrary.Redirect) || !noId.Contains("Client ID")) throw new Exception("Sign-in with no Client ID did not explain the app setup: " + noId);
                if (SpotifyLibrary.Redirect != "http://127.0.0.1:8888/callback") throw new Exception("The redirect he registers is not the loopback address Spotify allows: " + SpotifyLibrary.Redirect);

                ForkSettings.Current.SpotifyId = Voice.Protect(SpotifyFake.Id);
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                bool opened = false;
                string locked = SpotifyLibrary.SignIn(delegate { opened = true; });
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                if (opened || locked != Lockout.Refusal) throw new Exception("Spotify sign-in opened a browser during a game: " + locked);

                string told = null;
                string consent = null;
                SpotifyLibrary.Said = delegate(string text) { told = text; };
                SpotifyLibrary.SignInSeconds = 1;
                string wrong = SpotifyLibrary.SignIn(delegate(string url) { consent = Visit(url, "wrong"); });
                SpotifyLibrary.Waiting.Join(15000);
                if (told == null || !told.Contains("timed out") || SpotifyLibrary.Connected) throw new Exception("A Spotify sign-in answered with the wrong state was accepted: " + told);

                SpotifyLibrary.SignInSeconds = 20;
                told = null;
                string started = SpotifyLibrary.SignIn(delegate(string url) { consent = Visit(url, null); });
                SpotifyLibrary.Waiting.Join(20000);
                string challenge = Regex.Match(consent ?? "", @"code_challenge=([^&]+)").Groups[1].Value;
                string made = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(fake.Verifier ?? ""))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                if (!started.Contains("Opened the Spotify sign-in page") || told == null || !told.Contains("Signed in to Spotify as Kasper")) throw new Exception("Spotify sign-in did not finish: " + started + " / " + told);
                if (!SpotifyLibrary.Connected || Voice.Unprotect(ForkSettings.Current.SpotifyRefresh) != "RT-sp-1") throw new Exception("The Spotify sign-in was not saved.");
                if (challenge.Length < 20 || challenge != made || !consent.Contains("code_challenge_method=S256") || !consent.Contains("response_type=code") || !consent.Contains("client_id=" + SpotifyFake.Id)
                    || !Regex.IsMatch(consent, @"scope=[^&]*user-read-currently-playing") || !Regex.IsMatch(consent, @"scope=[^&]*user-library-read") || !Regex.IsMatch(consent, @"scope=[^&]*user-library-modify")
                    || !Regex.IsMatch(consent, @"redirect_uri=http%3A%2F%2F127\.0\.0\.1%3A8888%2Fcallback(&|$)")) throw new Exception("The Spotify sign-in request is wrong: " + consent);
                string saved = File.ReadAllText(ForkSettings.FilePath);
                if (!saved.Contains("SpotifyRefresh") || saved.Contains("RT-sp-") || saved.Contains("AT-sp-") || saved.Contains(SpotifyFake.Id)) throw new Exception("A Spotify token or the Client ID reached fork.json unencrypted.");

                fake.Playing = SpotifyFake.Track("T1", "Red Horse");
                lock (fake.Liked) fake.Liked.Add("spotify:track:T1");
                bool? liked = SpotifyLibrary.Liked("Red Horse");
                bool? otherSong = SpotifyLibrary.Liked("Something Else");
                fake.Playing = null;
                bool? nothing = SpotifyLibrary.Liked("Red Horse");
                fake.Playing = "{\"currently_playing_type\":\"episode\",\"item\":{\"type\":\"episode\",\"name\":\"Red Horse\",\"uri\":\"spotify:episode:E1\"}}";
                bool? episode = SpotifyLibrary.Liked("Red Horse");
                fake.Playing = "{\"currently_playing_type\":\"track\",\"item\":{\"type\":\"track\",\"is_local\":true,\"name\":\"Red Horse\",\"uri\":\"spotify:local:x\"}}";
                bool? local = SpotifyLibrary.Liked("Red Horse");
                if (liked != true || otherSong != null || nothing != null || episode != null || local != null) throw new Exception("The liked state was read wrong: " + liked + " " + otherSong + " " + nothing + " " + episode + " " + local);
                fake.Playing = SpotifyFake.Track("T1", "Red  horse");
                if (SpotifyLibrary.Liked("red horse") != true) throw new Exception("A title that differs only by case and spaces was not matched.");

                fake.Playing = SpotifyFake.Track("T1", "Red Horse");
                bool? again = SpotifyLibrary.Set("Red Horse", true);
                bool stillLiked;
                lock (fake.Liked) stillLiked = fake.Liked.Contains("spotify:track:T1");
                if (again != true || !stillLiked || fake.Count("DELETE") != 0) throw new Exception("Liking an already liked song unliked it: " + again);
                bool? removed = SpotifyLibrary.Set("Red Horse", false);
                lock (fake.Liked) stillLiked = fake.Liked.Contains("spotify:track:T1");
                if (removed != true || stillLiked) throw new Exception("Unliking did not remove the song: " + removed);
                fake.Playing = SpotifyFake.Track("T2", "Next Song");
                int deletes = fake.Count("DELETE");
                bool? changed = SpotifyLibrary.Set("Red Horse", false);
                if (changed != null || fake.Count("DELETE") != deletes) throw new Exception("A press after the song changed touched the new song: " + changed);

                SpotifyLibrary.Forget();
                if (SpotifyLibrary.Liked("Next Song") != false || Voice.Unprotect(ForkSettings.Current.SpotifyRefresh) != "RT-sp-2") throw new Exception("A refresh that hands back a new refresh token did not save it.");
                SpotifyLibrary.Forget();
                if (SpotifyLibrary.Liked("Next Song") != false || Voice.Unprotect(ForkSettings.Current.SpotifyRefresh) != "RT-sp-2") throw new Exception("A refresh with no new refresh token lost the old one.");
                lock (fake.Log) if (fake.Log.Exists(delegate(string line) { return line.StartsWith("POST /api/token", StringComparison.Ordinal) && line.Contains("Basic "); })) throw new Exception("The Spotify token call sent a client secret header.");

                SpotifyHeart.Title = delegate { return "Next Song"; };
                bool chord = false;
                bool? originalState = null;
                Func<bool?> original = delegate { return originalState; };
                Func<Tune, string, bool> originalSend = delegate(Tune tune, string what) { chord = true; return true; };
                Tunes.Hearted = original;
                Tunes.Send = originalSend;
                SpotifyHeart.Install();
                Tune playing = new Tune { Title = "Next Song", Artist = "x", App = "Spotify.exe" };
                lock (fake.Liked) fake.Liked.Add("spotify:track:T2");
                if (Tunes.Hearted() != true) throw new Exception("The mini player heart did not read the liked state from Spotify with Spotify minimized.");
                if (!Tunes.Send(playing, "unlike") || chord) throw new Exception("An unlike did not go through Spotify's library.");
                lock (fake.Liked) stillLiked = fake.Liked.Contains("spotify:track:T2");
                if (stillLiked || Tunes.Hearted() != false) throw new Exception("The unlike did not stick.");
                if (!Tunes.Send(playing, "next") || !chord) throw new Exception("A press other than the heart did not reach the player.");

                chord = false;
                ForkSettings.Current.SpotifyRefresh = null;
                SpotifyLibrary.Forget();
                bool peeked = false;
                SpotifyHeart.Peek = delegate { peeked = true; return null; };
                originalState = null;
                if (Tunes.Send(playing, "like") || chord || !peeked) throw new Exception("A heart press with the state unknown pressed Spotify's like key blindly: chord " + chord + ", peeked " + peeked);
                peeked = false;
                SpotifyHeart.Peek = delegate { peeked = true; return true; };
                if (!Tunes.Send(playing, "like") || chord) throw new Exception("A song found liked on a peek was toggled off.");
                SpotifyHeart.Peek = delegate { return false; };
                if (!Tunes.Send(playing, "like") || !chord) throw new Exception("A song found unliked on a peek was not liked.");
                chord = false;
                originalState = true;
                if (!Tunes.Send(playing, "like") || chord) throw new Exception("An already liked song was toggled off by a like.");
                originalState = null;
                peeked = false;
                SpotifyHeart.Peek = delegate { peeked = true; return false; };
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                bool inGame = Tunes.Send(playing, "like");
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                if (inGame || peeked || chord) throw new Exception("A heart press in a game brought Spotify to the front.");

                ForkSettings.Current.SpotifyRefresh = Voice.Protect("RT-sp-2");
                SpotifyLibrary.Forget();
                if (!Regex.IsMatch(SpotifyLibrary.Scopes, @"\buser-modify-playback-state\b") || !Regex.IsMatch(SpotifyLibrary.Scopes, @"\buser-read-playback-state\b") || !Regex.IsMatch(SpotifyLibrary.Scopes, @"\bplaylist-read-private\b")) throw new Exception("The Spotify sign-in does not ask to play music: " + SpotifyLibrary.Scopes);
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                try
                {
                    string track = Hands.Do("play_music", new ToolArguments { Song = "Red Horse", Kind = "song", App = "spotify" });
                    string lastPlay;
                    lock (fake.Log) lastPlay = fake.Log.FindLast(delegate(string line) { return line.StartsWith("PUT /v1/me/player/play", StringComparison.Ordinal); }) ?? "";
                    if (track != "Playing Red Horse on Spotify." || !lastPlay.Contains("device_id=PC1") || !lastPlay.Contains("\"uris\":[\"spotify:track:T9\"]")) throw new Exception("A song in a game was not played through Spotify's API on the PC: " + track + " / " + lastPlay);
                    string list = Hands.Do("play_music", new ToolArguments { Song = "the usual DG playlist I like to listen to", Kind = "playlist" });
                    lock (fake.Log) lastPlay = fake.Log.FindLast(delegate(string line) { return line.StartsWith("PUT /v1/me/player/play", StringComparison.Ordinal); }) ?? "";
                    if (!list.StartsWith("Playing your DG playlist", StringComparison.Ordinal) || !lastPlay.Contains("\"context_uri\":\"spotify:playlist:P1\"")) throw new Exception("His own playlist was not found by name: " + list + " / " + lastPlay);
                    string artist = Hands.Do("play_music", new ToolArguments { Song = "Kendrick Lamar", Kind = "artist" });
                    lock (fake.Log) lastPlay = fake.Log.FindLast(delegate(string line) { return line.StartsWith("PUT /v1/me/player/play", StringComparison.Ordinal); }) ?? "";
                    if (!artist.Contains("Kendrick Lamar") || !lastPlay.Contains("\"context_uri\":\"spotify:artist:A1\"")) throw new Exception("An artist was not played: " + artist + " / " + lastPlay);
                    int plays = fake.Count("PUT /v1/me/player/play");
                    string resume = Hands.Do("play_music", new ToolArguments { Song = "some music" });
                    lock (fake.Log) lastPlay = fake.Log.FindLast(delegate(string line) { return line.StartsWith("PUT /v1/me/player/play", StringComparison.Ordinal); }) ?? "";
                    if (!resume.StartsWith("Playing", StringComparison.Ordinal) || fake.Count("PUT /v1/me/player/play") != plays + 1 || lastPlay.Contains("uris") || lastPlay.Contains("context_uri")) throw new Exception("\"Some music\" did not just press play on Spotify: " + resume + " / " + lastPlay);
                    fake.Devices = "{\"devices\":[]}";
                    plays = fake.Count("PUT /v1/me/player/play");
                    string closed = Hands.Do("play_music", new ToolArguments { Song = "Red Horse", Kind = "song" });
                    fake.Devices = null;
                    if (!closed.Contains("Spotify isn't open") || fake.Count("PUT /v1/me/player/play") != plays) throw new Exception("With Spotify closed she did not say so plainly: " + closed);
                    fake.PlayRefused = "Insufficient client scope";
                    string scope = Hands.Do("play_music", new ToolArguments { Song = "Red Horse", Kind = "song" });
                    fake.PlayRefused = "Player command failed: Premium required";
                    string premium = Hands.Do("play_music", new ToolArguments { Song = "Red Horse", Kind = "song" });
                    fake.PlayRefused = null;
                    if (!scope.Contains("connect my Spotify") || !premium.Contains("Premium")) throw new Exception("A refused play was not explained: " + scope + " / " + premium);
                    SmokeSpotifyClosedAndLiked(fake);
                    string handJob = Hands.Do("use_computer", new ToolArguments { Task = "Open Spotify, go to the main page, and play the playlist called DJ from the top left." });
                    if (!handJob.StartsWith("Not done", StringComparison.Ordinal) || !handJob.Contains("play_music")) throw new Exception("A cursor job to play music on Spotify was not turned to play_music: " + handJob);
                    string inGameJob = ForkTool("use_computer", new ToolArguments { Task = "Open Spotify, go to the main page, and play the playlist called DJ from the top left." });
                    if (inGameJob != SpotifyLibrary.HandsOff) throw new Exception("In a game a cursor job to play Spotify was refused by the lockout instead of being turned to play_music: " + inGameJob);
                    string cursorPlay = CursorJob("play_music", new ToolArguments { Song = "DJ", App = "spotify" }) ?? CursorJob("play_music", new ToolArguments { Song = "Red Horse", Kind = "song" }) ?? CursorJob("play_music", new ToolArguments { Song = "Liked Songs", Kind = "playlist" });
                    if (cursorPlay != null) throw new Exception("With Spotify connected, play_music still became a cursor job when her mouse is on for jobs: " + cursorPlay);
                    if (CursorJob("play_music", new ToolArguments { Song = "lofi", App = "youtube" }) == null) throw new Exception("YouTube music stopped being a cursor job.");
                    string volumeJob = Hands.Do("use_computer", new ToolArguments { Task = "Open Spotify settings and turn on crossfade" });
                    if (volumeJob.Contains("play_music")) throw new Exception("A Spotify job that is not about playing music was turned to play_music: " + volumeJob);
                }
                finally
                {
                    Lockout.Running = delegate { return false; };
                    Lockout.Recheck();
                }
                string outsideGame = CursorJob("play_music", new ToolArguments { Song = "DJ", App = "spotify" });
                if (outsideGame != null || Lockout.Active) throw new Exception("With Spotify connected and no game, play_music still became a cursor job: " + outsideGame);
                checks.Add("PASS Spotify playback: with Spotify connected she plays songs, his own playlists (by name, inside a longer ask), artists and plain \"some music\" through Spotify's API on his PC, also in a game, never with her cursor (also with her mouse on for jobs); a cursor job to play music on Spotify is turned back to play_music; Spotify closed, a missing permission and no Premium are said plainly");

                ForkSettings.Current.SpotifyRefresh = Voice.Protect("RT-dead");
                SpotifyLibrary.Forget();
                bool? dead = SpotifyLibrary.Liked("Next Song");
                if (dead != null || ForkSettings.Current.SpotifyRefresh != null || SpotifyLibrary.Problem != SpotifyLibrary.Expired) throw new Exception("A dead Spotify sign-in was not cleared and said plainly: " + SpotifyLibrary.Problem);

                everything.Append(noId).Append(told).Append(wrong).Append(started).Append(SpotifyLibrary.Problem);
                string all = everything.ToString();
                if (all.Contains("AT-sp") || all.Contains("RT-sp")) throw new Exception("A Spotify token appeared in what she says.");
                checks.Add("PASS Spotify heart: connect my Spotify signs in with PKCE and state (no client secret, loopback redirect, refuses in a game), tokens are encrypted and a new refresh token is kept, the heart reads the liked state from Spotify's library with Spotify minimized, liking a liked song never unlikes it, a press after the song changed touches nothing, and with the state unknown she never presses Spotify's like key blindly");
            }
            finally
            {
                fake.Close();
                SpotifyLibrary.AuthUrl = keptAuth;
                SpotifyLibrary.TokenUrl = keptToken;
                SpotifyLibrary.ApiBase = keptApi;
                SpotifyLibrary.AppRunning = keptAppRunning;
                SpotifyLibrary.Launch = keptLaunch;
                SpotifyLibrary.Pause = keptPause;
                SpotifyLibrary.Said = keptSaid;
                SpotifyLibrary.SignInSeconds = keptSeconds;
                SpotifyLibrary.Forget();
                Tunes.Hearted = keptHearted;
                Tunes.Send = keptSend;
                SpotifyHeart.Title = keptTitle;
                SpotifyHeart.Peek = keptPeek;
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }
    }
}
