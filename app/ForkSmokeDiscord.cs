using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed class DiscordFake
    {
        internal const string Token = "MTA0NTY3ODkwMTIzNDU2Nzg5." + "Gabcde." + "secret-token-value-0123456789";
        internal readonly List<string> Log = new List<string>();
        internal readonly List<string[]> Msgs = new List<string[]>();
        internal readonly int Port;
        internal int Status;
        internal bool NoDm;
        internal string Members = "[]";
        internal string ClosedFriend = "";
        internal string History = "[]";
        internal readonly List<string> Friends = new List<string>();
        private long next = 1000;
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        private volatile bool stopped;

        internal DiscordFake()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Thread thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Start();
        }

        internal string Base { get { return "http://127.0.0.1:" + Port + "/api/v10"; } }

        internal void Close()
        {
            stopped = true;
            listener.Stop();
        }

        internal string Add(string author, bool bot, string content)
        {
            lock (Msgs)
            {
                string id = (next++).ToString();
                Msgs.Add(new[] { id, author, bot ? "1" : "0", content });
                return id;
            }
        }

        internal int Count(string text)
        {
            int n = 0;
            lock (Log) foreach (string line in Log) if (line.Contains(text)) n++;
            return n;
        }

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
            string auth = "", type = "";
            foreach (string line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line.Substring(15).Trim());
                if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) auth = line.Substring(14).Trim();
                if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase)) type = line.Substring(13).Trim();
            }
            byte[] bytes = new byte[length];
            int got = 0;
            while (got < length) { int n = stream.Read(bytes, got, length - got); if (n <= 0) break; got += n; }
            string body = Encoding.GetEncoding("ISO-8859-1").GetString(bytes, 0, got);
            lock (Log) Log.Add(first[0] + " " + first[1] + " | " + auth + " | " + type + " | " + body);
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
            if (Status == 401) { code = 401; return "{\"message\":\"401: Unauthorized\",\"code\":0}"; }
            if (Status == 429) { code = 429; return "{\"message\":\"You are being rate limited.\",\"retry_after\":2.5,\"global\":false}"; }
            if (auth != "Bot " + Token) { code = 401; return "{\"message\":\"401: Unauthorized\"}"; }
            if (path == "/api/v10/oauth2/applications/@me") return "{\"id\":\"777\",\"owner\":{\"id\":\"900\",\"username\":\"kasper\"}}";
            if (path == "/api/v10/users/@me/guilds") return "[{\"id\":\"g1\",\"name\":\"Squad\"}]";
            if (path.StartsWith("/api/v10/guilds/g1/members?", StringComparison.Ordinal) && method == "GET") return Members;
            if (path == "/api/v10/users/@me/channels")
            {
                Match friend = Regex.Match(body, "\"recipient_id\":\"(5\\d\\d)\"");
                return body.Contains("\"900\"") ? "{\"id\":\"chan1\"}" : friend.Success ? "{\"id\":\"chan" + friend.Groups[1].Value + "\"}" : "{\"id\":\"chanX\"}";
            }
            if (Regex.IsMatch(path, @"^/api/v10/channels/chan5\d\d/messages\?") && method == "GET") return History;
            if (Regex.IsMatch(path, @"^/api/v10/channels/chan5\d\d/messages$") && method == "POST")
            {
                if (path.Contains("chan" + ClosedFriend + "/")) { code = 403; return "{\"message\":\"Cannot send messages to this user\",\"code\":50007}"; }
                lock (Friends) Friends.Add(path.Substring(path.IndexOf("chan5", StringComparison.Ordinal), 7) + " | " + body);
                return "{\"id\":\"" + (next++) + "\"}";
            }
            if (path == "/api/v10/channels/chan1/messages" && method == "POST")
            {
                if (NoDm) { code = 403; return "{\"message\":\"Cannot send messages to this user\",\"code\":50007}"; }
                return "{\"id\":\"" + Add("555", true, body) + "\"}";
            }
            if (path.StartsWith("/api/v10/channels/chan1/messages?", StringComparison.Ordinal) && method == "GET")
            {
                Match after = Regex.Match(path, @"after=(\d+)");
                Match limit = Regex.Match(path, @"limit=(\d+)");
                long floor = after.Success ? long.Parse(after.Groups[1].Value) : 0;
                int max = limit.Success ? int.Parse(limit.Groups[1].Value) : 50;
                List<string> items = new List<string>();
                lock (Msgs)
                {
                    for (int i = Msgs.Count - 1; i >= 0 && items.Count < max; i--)
                    {
                        string[] m = Msgs[i];
                        if (long.Parse(m[0]) <= floor) continue;
                        items.Add("{\"id\":\"" + m[0] + "\",\"content\":" + Voice.Quote(m[3]) + ",\"author\":{\"id\":\"" + m[1] + "\"" + (m[2] == "1" ? ",\"bot\":true" : "") + "}}");
                    }
                }
                return "[" + string.Join(",", items.ToArray()) + "]";
            }
            code = 404;
            return "{\"message\":\"404: Not Found\"}";
        }
    }

    internal sealed partial class Companion
    {
        private static bool PumpFor(Func<bool> done)
        {
            PumpUntil(done, 6000);
            return done();
        }

        private static bool WaitFor(Func<bool> done)
        {
            for (int i = 0; i < 200 && !done(); i++) Thread.Sleep(15);
            return done();
        }

        private void SmokeDiscord(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath, keptBase = Discord.Base;
            Action<string> keptTold = Discord.Told;
            string folder = Path.Combine(Path.GetTempPath(), "capri-discord-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            DiscordFake fake = new DiscordFake();
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Discord.Base = fake.Base;
                Discord.Reset();
                List<string> told = new List<string>();
                Discord.Told = delegate(string text) { lock (told) told.Add(text); };
                ForkSettings settings = ForkSettings.Current;
                if (!settings.DiscordOn || Discord.Ready) throw new Exception("Discord should start on in the settings and not paired.");
                List<string> heard = new List<string>();
                Action<string> hear = delegate(string line) { heard.Add(line); };

                Discord.Poll(hear);
                if (fake.Log.Count != 0) throw new Exception("Discord called out without a bot token.");
                string empty = Path.Combine(folder, "empty");
                Directory.CreateDirectory(empty);
                if (!Discord.Import(empty).Contains("capri bot") || Discord.Connected) throw new Exception("An empty folder produced a token or did not name the token file.");

                string desk = Path.Combine(folder, "desk");
                Directory.CreateDirectory(desk);
                File.WriteAllText(Path.Combine(desk, "capri bot.txt"), "\r\n  Bot token:\r\n" + DiscordFake.Token + "  \r\n");
                string imported = Discord.Import(desk);
                if (!Discord.Connected || imported.Contains(DiscordFake.Token) || Voice.Unprotect(settings.DiscordToken) != DiscordFake.Token) throw new Exception("The token file was not imported into the encrypted setting: " + imported);
                ForkSettings.Save();
                if (File.ReadAllText(ForkSettings.FilePath).Contains(DiscordFake.Token)) throw new Exception("The bot token was saved in plain text.");

                fake.NoDm = true;
                string blocked = Discord.Pair();
                if (!blocked.Contains("can't message") || Discord.Ready || Discord.Pairing != null || blocked.Contains(DiscordFake.Token)) throw new Exception("A Discord that refuses DMs was not explained: " + blocked);
                fake.NoDm = false;
                fake.Add("900", false, "old chatter before pairing");
                string said = Discord.Pair();
                Match code = Regex.Match(said, @"\b([A-HJ-NP-Z2-9]{6})\b");
                if (!code.Success || Discord.Pairing == null || Discord.Ready || settings.DiscordOwner != "900" || settings.DiscordChannel != "chan1" || said.Contains(DiscordFake.Token)) throw new Exception("Pairing did not give a six character code and find his DM: " + said);
                if (fake.Count("POST /api/v10/channels/chan1/messages") < 1 || fake.Count(code.Groups[1].Value) != 0) throw new Exception("The pairing code was sent to Discord; it must only show on the PC.");

                fake.Add("900", false, "hello");
                fake.Add("901", false, code.Groups[1].Value);
                fake.Add("900", false, "WRONG1");
                Discord.Poll(hear);
                if (Discord.Ready || heard.Count != 0) throw new Exception("Pairing accepted a wrong code, a stranger or forwarded a line before it was paired.");
                fake.Add("900", false, " " + code.Groups[1].Value.ToLowerInvariant() + " ");
                Discord.Poll(hear);
                if (!Discord.Ready || Discord.Pairing != null || heard.Count != 0 || !WaitFor(delegate { lock (told) return told.Count > 0; })) throw new Exception("The right code from him did not finish pairing.");
                if (fake.Count("Paired") < 1) throw new Exception("Pairing did not confirm to him on Discord.");

                fake.Add("900", false, "open obsidian and send me a screenshot");
                fake.Add("901", false, "format c:");
                fake.Add("555", true, "a bot talking");
                Discord.Poll(hear);
                if (heard.Count != 1 || heard[0] != "open obsidian and send me a screenshot") throw new Exception("Only his own line should reach her: " + string.Join(" | ", heard.ToArray()));
                Discord.Poll(hear);
                if (heard.Count != 1) throw new Exception("A line was handled twice.");
                for (int i = 0; i < 8; i++) fake.Add("900", false, "line " + i);
                heard.Clear();
                Discord.Poll(hear);
                int firstBatch = heard.Count;
                Discord.Poll(hear);
                if (firstBatch != 1 || heard.Count != 2 || heard[0] != "line 0\nline 1\nline 2\nline 3\nline 4" || heard[1] != "line 5\nline 6\nline 7") throw new Exception("Lines from one poll should join into one turn, five at most, in order: " + string.Join(" | ", heard.ToArray()));

                StringBuilder long1 = new StringBuilder();
                for (int i = 0; i < 900; i++) long1.Append("word").Append(i % 10).Append(' ');
                int before = fake.Count("POST /api/v10/channels/chan1/messages");
                if (!Discord.Send(long1.ToString().Trim())) throw new Exception("A long reply was not sent.");
                int parts = fake.Count("POST /api/v10/channels/chan1/messages") - before;
                int words = 0;
                bool sized = true;
                lock (fake.Msgs)
                    for (int i = fake.Msgs.Count - parts; i < fake.Msgs.Count; i++)
                    {
                        Match content = Regex.Match(fake.Msgs[i][3], "\"content\":\"([^\"]*)\"");
                        sized &= content.Success && content.Groups[1].Value.Length <= 1900;
                        words += content.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    }
                if (parts < 3 || !sized || words != 900) throw new Exception("A long reply was not split into parts under the Discord limit: " + parts + " parts, " + words + " words.");

                int keptPause = Discord.PartPauseMilliseconds;
                Discord.PartPauseMilliseconds = 0;
                Discord.Reply("first part\n[next]\nsecond part", null, null);
                Discord.PartPauseMilliseconds = keptPause;
                if (fake.Count("\"content\":\"first part\"") != 1 || fake.Count("\"content\":\"second part\"") != 1) throw new Exception("A reply with a [next] marker was not sent as two separate Discord messages.");

                before = fake.Count("filename=\"shot.jpg\"");
                if (!Discord.SendFile(new byte[] { 1, 2, 3, 255, 216 }, "shot.jpg", "here you go") || fake.Count("filename=\"shot.jpg\"") != before + 1 || fake.Count("multipart/form-data") < 1 || fake.Count("here you go") < 1) throw new Exception("A picture was not uploaded as a file.");

                int keptQuietFrom = settings.QuietFrom, keptQuietTo = settings.QuietTo;
                settings.QuietFrom = 0;
                settings.QuietTo = 0;
                settings.Proactive = 2;
                before = fake.Log.Count;
                Func<long> keptIdle = Autonomy.Idle;
                Func<bool> keptGaming = Autonomy.Gaming;
                try
                {
                    Autonomy.Gaming = delegate { return false; };
                    Autonomy.Idle = delegate { return 0L; };
                    Discord.Heads("Starts in 40 minutes: Dentist");
                }
                finally
                {
                    Autonomy.Idle = keptIdle;
                    Autonomy.Gaming = keptGaming;
                }
                if (fake.Log.Count != before) throw new Exception("A heads-up went to Discord while she is on Chatty (home).");
                settings.Proactive = 0;
                Discord.Heads("Starts in 40 minutes: Dentist");
                if (fake.Count("Heads-up: Starts in 40 minutes: Dentist") != 1) throw new Exception("A heads-up did not go to Discord on Quiet (away).");
                settings.QuietFrom = keptQuietFrom;
                settings.QuietTo = keptQuietTo;
                settings.DiscordOn = false;
                before = fake.Log.Count;
                Discord.Heads("Birthday: Mum in 3 days");
                Discord.Poll(hear);
                if (fake.Log.Count != before) throw new Exception("Discord off still called out.");
                settings.DiscordOn = true;

                fake.Status = 429;
                Discord.Poll(hear);
                int limited = fake.Log.Count;
                Discord.Poll(hear);
                if (limited != fake.Log.Count || Discord.Due(DateTime.Now)) throw new Exception("A rate limit was not waited out.");
                Discord.Reset();
                fake.Status = 401;
                lock (told) told.Clear();
                Discord.Poll(hear);
                Discord.Poll(hear);
                int rejected = fake.Log.Count;
                Discord.Poll(hear);
                bool toldOnce;
                lock (told) toldOnce = told.Count == 1 && !told[0].Contains(DiscordFake.Token);
                if (Discord.Failed == null || rejected != fake.Log.Count || !toldOnce || Discord.Due(DateTime.Now)) throw new Exception("A rejected token was not told once and then left alone.");
                fake.Status = 0;
                Discord.Reset();

                if (!Discord.WantsShot("open obsidian and send me a screenshot") || !Discord.WantsShot("what's on my screen") || !Discord.WantsShot("show me my screen") || Discord.WantsShot("open obsidian")) throw new Exception("Screenshot requests were not recognised.");
                string note = Path.Combine(folder, "note.txt");
                File.WriteAllText(note, "hello");
                string secret = Path.Combine(folder, "fork.json");
                File.WriteAllText(secret, "{}");
                string big = Path.Combine(folder, "big.bin");
                File.WriteAllBytes(big, new byte[Discord.MaxFile + 10]);
                if (Discord.FileIn("send me " + note) != note || Discord.FileIn("please upload \"" + note + "\" here") != note || Discord.FileIn("what is in " + note) != null || Discord.FileIn("send me " + secret) != null || Discord.FileIn("send me " + big) != null || Discord.FileIn("send me C:\\nowhere\\none.txt") != null || Discord.FileIn("send me the " + Path.Combine(Path.GetTempPath(), "api.env")) != null) throw new Exception("File sending rules were wrong.");

                if (TweakFor("connect my discord") != "discordpair" || TweakFor("pair discord") != "discordpair" || TweakFor("turn discord off") != "discordoff" || TweakFor("turn discord on") != "discordon" || TweakFor("tell rudiminas on discord hi") != null) throw new Exception("Discord voice commands were wrong.");

                Discord.Reset();
                settings.DiscordPaired = true;
                settings.DiscordChannel = "chan1";
                settings.DiscordOwner = "900";
                discordLine = "what is on my screen";
                discordSince = DateTime.Now;
                discordTicket = asking;
                before = fake.Count("Done on the PC.");
                if (!TookDiscordAnswer("Done on the PC.") || discordLine != null) throw new Exception("A reply to a Discord line was not caught.");
                if (!WaitFor(delegate { return fake.Count("Done on the PC.") == before + 1; })) throw new Exception("The reply to a Discord line never reached Discord.");
                if (TookDiscordAnswer("Another line.")) throw new Exception("An ordinary reply was sent to Discord.");
                discordLine = "old";
                discordSince = DateTime.Now.AddMinutes(-30);
                if (TookDiscordAnswer("Late line.") || discordLine != null) throw new Exception("A stale Discord line caught a later reply.");

                Func<string, string> keptThink = Think;
                Func<int> keptRoll = Hands.AsideRoll;
                List<string> thought = new List<string>();
                try
                {
                    Hands.AsideRoll = delegate { return 1; };
                    Think =delegate(string text) { thought.Add(text); return "Opened it. Turn " + thought.Count + "."; };
                    discordLine = null;
                    discordTicket = -1;
                    fake.Add("900", false, "open obsidian");
                    fake.Add("900", false, "and send me a screenshot of it");
                    Action<string> asHeard = delegate(string line) { DiscordHeard(line); };
                    Discord.Poll(asHeard);
                    if (!DiscordBusy()) throw new Exception("A Discord turn in flight was not held.");
                    fake.Add("900", false, "another thing");
                    if (!PumpFor(delegate { return fake.Count("Opened it. Turn 1.") == 1; })) throw new Exception("Her answer to a Discord DM never reached Discord.");
                    if (thought.Count != 1 || !thought[0].Contains("open obsidian") || !thought[0].Contains("send me a screenshot of it")) throw new Exception("Two split DMs did not reach her as one turn: " + string.Join(" | ", thought.ToArray()));
                    if (DiscordBusy()) throw new Exception("A finished Discord turn still held the next one.");
                    Discord.Poll(asHeard);
                    if (!PumpFor(delegate { return fake.Count("Opened it. Turn 2.") == 1; }) || thought.Count != 2 || !thought[1].Contains("another thing")) throw new Exception("A DM that arrived during a turn was not answered as the next turn.");
                    discordLine = "stuck";
                    discordTicket = asking - 1;
                    if (DiscordBusy()) throw new Exception("A superseded Discord turn blocked polling.");
                }
                finally
                {
                    Think = keptThink;
                    Hands.AsideRoll = keptRoll;
                    discordLine = null;
                    discordTicket = -1;
                }

            if (!ShotToDiscord("send me a screenshot of my main monitor on discord") || !ShotToDiscord("send a screenshot to my phone") || !ShotToDiscord("can you send me a ss of my second monitor in discord")
                || ShotToDiscord("take a screenshot") || ShotToDiscord("send me a screenshot") || ShotToDiscord("what's new on discord") || ShotToDiscord("message pieter on discord"))
                throw new Exception("A screenshot for his Discord asked at the PC was read wrong.");
            if (ShotPlace("main") != "your main monitor" || ShotPlace("2") != "monitor 2" || ShotPlace(null) != "your screen") throw new Exception("The screenshot caption names the monitor wrong: " + ShotPlace("main"));
            string keptChannel = ForkSettings.Current.DiscordChannel;
            ForkSettings.Current.DiscordChannel = null;
            try { if (!ShotHeard("send me a screenshot of my main monitor on discord", true) || ShotHeard("take a screenshot", true)) throw new Exception("A screenshot for his Discord asked at the PC was not handled in code."); }
            finally { ForkSettings.Current.DiscordChannel = keptChannel; }
            if (!string.IsNullOrEmpty(keptChannel))
            {
                string asked = "send me a screenshot of my main monitor on discord now";
                if (!ShotHeard(asked, true)) throw new Exception("The direct screenshot ask was not handled.");
                PumpUntil(delegate { return brain.History.Exists(delegate(ChatMessage one) { return one.Role == "user" && one.Content == asked; }); }, 15000);
                List<ChatMessage> remembered = brain.History;
                int at = remembered.FindLastIndex(delegate(ChatMessage one) { return one.Role == "user" && one.Content == asked; });
                if (at < 0 || at + 1 >= remembered.Count || remembered[at + 1].Content.IndexOf("screenshot", StringComparison.OrdinalIgnoreCase) < 0) throw new Exception("A screenshot sent straight from code never reached her chat history, so she later says she sent nothing.");
            }
            checks.Add("PASS screenshot to his Discord from the PC (059): the send (or its failure) goes into her chat history so she knows it went out; \"send me a screenshot of my main monitor on discord\" or \"... to my phone\", typed or said at the PC, sends that screen to his DM from code (never with a never-look app in front); a plain \"take a screenshot\" or his own DM to the bot keep their old paths");
                checks.Add("PASS Discord: the token comes from the Desktop file into an encrypted setting, pairing needs the code shown on the PC and finds his DM, only his own lines reach her, split DMs join into one turn that is answered on Discord (not out loud) while the next waits, long replies split, pictures upload, heads-ups go to Discord only on Quiet, a rejected token or rate limit stops the polling, screenshot and file asks follow his words");
            }
            finally
            {
                fake.Close();
                Discord.Base = keptBase;
                Discord.Told = keptTold;
                Discord.Reset();
                discordLine = null;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }
    }
}
