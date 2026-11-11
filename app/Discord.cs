using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal static partial class Discord
    {
        internal static string Base = "https://discord.com/api/v10";
        internal static Action<string> Told;
        internal static string Pairing;
        internal static string Failed;
        internal const int MaxFile = 8 * 1024 * 1024;

        private const int Wait = 15000;
        private const int Chunk = 1900;
        private const int PerPoll = 5;
        private const int PollSeconds = 4;
        private const int MostParts = 4;
        internal static int PartPauseMilliseconds = 1200;
        private static readonly Regex ShotMarker = new Regex(@"\s*\[screenshot\]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Marker =new Regex(@"\s*\[next\]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private const int PairingMinutes = 10;
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private static DateTime holdUntil = DateTime.MinValue;
        private static DateTime lastPoll = DateTime.MinValue;
        private static DateTime pairingUntil = DateTime.MinValue;

        internal static readonly Regex Shots = new Regex(
            @"\b(screen ?shots?|print ?screen)\b|\b(show|send|give|let) me\b[^.?!]*\b(my|the|your) (screen|desktop|monitor)s?\b|\bwhat('?s| is| are)\b[^.?!]*\bon (my|the) (screen|desktop|monitor)s?\b|\bsee (my|the) (screen|desktop)\b",
            RegexOptions.IgnoreCase);

        private static readonly Regex Verbs = new Regex(@"\b(send|upload|attach|share|post|give me)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Paths = new Regex(@"""([A-Za-z]:\\[^""\r\n]+)""|([A-Za-z]:\\[^\s""<>|?*]+\.\w{1,8})");
        private static readonly Regex Secrets = new Regex(@"(^|\\)(api\.env|fork\.json|settings\.json|chat\.json|personality\.md|activity\.log|errors\.txt|heartbeat\.json)$|\.(pem|key|kdbx|pfx|p12|env)$|id_rsa|password|capri bot|client_secret", RegexOptions.IgnoreCase);
        private static readonly Regex TokenShape = new Regex(@"[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{20,}");

        internal static bool Connected { get { return !string.IsNullOrEmpty(Voice.Unprotect(ForkSettings.Current.DiscordToken)); } }

        internal static bool Ready
        {
            get
            {
                ForkSettings settings = ForkSettings.Current;
                return settings.DiscordPaired && Connected && !string.IsNullOrEmpty(settings.DiscordChannel) && !string.IsNullOrEmpty(settings.DiscordOwner);
            }
        }

        internal static void Reset()
        {
            Pairing = null;
            Failed = null;
            holdUntil = DateTime.MinValue;
            lastPoll = DateTime.MinValue;
        }

        private static readonly Regex ShortShot = new Regex(@"\b(?:send|give|take|need|want)\b[^.?!]*\b(?:sc|ss)\b", RegexOptions.IgnoreCase);

        internal static bool WantsShot(string line) { return Shots.IsMatch(line ?? "") || ShortShot.IsMatch(line ?? ""); }

        private static readonly HashSet<string> ShotWords = new HashSet<string>(
            ("send take give show get me a an the my of screenshot screenshots screen screens shot shots sc ss monitor monitors display displays main second first third other another primary left right 1 2 3 one two three both all now please pls again okay ok hey capri can could you just and it for i want need desktop").Split(' '),
            StringComparer.OrdinalIgnoreCase);

        // A line that only asks for a picture ("send me a screenshot of my second monitor") is answered at once from code; right after one, a bare "now of the second monitor" counts too.
        internal static bool OnlyShot(string line, bool afterShot)
        {
            string said = (line ?? "").Trim();
            if (said.Length == 0 || said.Length > 90) return false;
            if (!WantsShot(said) && !(afterShot && Screens.Wording(said) != null)) return false;
            foreach (string word in Regex.Split(said.ToLowerInvariant(), @"[^a-z0-9']+")) if (word.Length > 0 && !ShotWords.Contains(word)) return false;
            return true;
        }

        internal static bool Due(DateTime now)
        {
            return Active() && (now - lastPoll).TotalSeconds >= PollSeconds;
        }

        private static bool Active()
        {
            ForkSettings settings = ForkSettings.Current;
            if (!settings.DiscordOn || !Connected || Failed != null || DateTime.Now < holdUntil) return false;
            if (Pairing != null && DateTime.Now > pairingUntil) Pairing = null;
            return (settings.DiscordPaired || Pairing != null) && !string.IsNullOrEmpty(settings.DiscordChannel);
        }

        internal static string Import(string folder)
        {
            const string Missing = "I can't find the Discord token file on your Desktop. Save the bot token in a text file named \"capri bot\" there first.";
            try
            {
                if (folder == null) folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string[] files = Directory.GetFiles(folder, "capri bot*");
                if (files.Length == 0) return Missing;
                if (files.Length > 1) return "There is more than one \"capri bot\" file on your Desktop. Leave only one.";
                string text = File.ReadAllText(files[0]);
                Match shaped = TokenShape.Match(text);
                string token = shaped.Success ? shaped.Value : null;
                if (token == null)
                {
                    foreach (string line in text.Split('\n'))
                    {
                        string plain = line.Trim();
                        if (plain.Length >= 30 && plain.IndexOf(' ') < 0) { token = plain; break; }
                    }
                }
                if (token == null) return "That file doesn't hold a Discord bot token.";
                ForkSettings settings = ForkSettings.Current;
                settings.DiscordToken = Voice.Protect(token);
                settings.DiscordOwner = null;
                settings.DiscordChannel = null;
                settings.DiscordLast = null;
                settings.DiscordPaired = false;
                Reset();
                ForkSettings.Save();
                return "Got the Discord bot token.";
            }
            catch (Exception) { return "I couldn't read the Discord token file."; }
        }

        // Finds his DM with the bot and asks for the code that is shown on the PC only. Runs on a pool thread.
        internal static string Pair()
        {
            ForkSettings settings = ForkSettings.Current;
            string said = "";
            if (!Connected)
            {
                said = Import(null);
                if (!Connected) return said;
                said += " ";
            }
            Failed = null;
            holdUntil = DateTime.MinValue;
            int status;
            Dictionary<string, object> app = Parse(Api("GET", "/oauth2/applications/@me", null, out status));
            if (app == null) return said + Problem(status);
            string owner = Text(app.ContainsKey("owner") ? app["owner"] as Dictionary<string, object> : null, "id");
            Dictionary<string, object> team = app.ContainsKey("team") ? app["team"] as Dictionary<string, object> : null;
            if (team != null && Text(team, "owner_user_id").Length > 0) owner = Text(team, "owner_user_id");
            if (owner.Length == 0) return said + "I couldn't tell who owns the bot.";
            Dictionary<string, object> channel = Parse(Api("POST", "/users/@me/channels", "{\"recipient_id\":" + Voice.Quote(owner) + "}", out status));
            string channelId = Text(channel, "id");
            if (channelId.Length == 0) return said + Problem(status);
            settings.DiscordOwner = owner;
            settings.DiscordChannel = channelId;
            settings.DiscordPaired = false;
            settings.DiscordLast = null;
            string greeting;
            if (!Post("Hi, it's Capri. To link this chat, type the six character code I show on your PC screen.", out greeting))
            {
                Pairing = null;
                return said + "I can't message you on Discord. The bot has to share a server with you, and your privacy settings there must allow direct messages from server members.";
            }
            Pairing = NewCode();
            pairingUntil = DateTime.Now.AddMinutes(PairingMinutes);
            settings.DiscordLast = greeting;
            ForkSettings.Save();
            return said + "Discord pairing code: " + Pairing + ". Send it to the bot in your Discord DM within " + PairingMinutes + " minutes.";
        }

        // One poll of his DM, on a pool thread; each line from him is handed to heard.
        internal static void Poll(Action<string> heard)
        {
            if (!Active()) return;
            lastPoll = DateTime.Now;
            ForkSettings settings = ForkSettings.Current;
            string after = settings.DiscordLast;
            bool baseline = string.IsNullOrEmpty(after);
            int status;
            string reply = Api("GET", "/channels/" + settings.DiscordChannel + "/messages?limit=" + (baseline ? "1" : "20") + (baseline ? "" : "&after=" + after), null, out status);
            if (status < 200 || status >= 300) return;
            List<Line> lines = Lines(reply);
            if (baseline)
            {
                settings.DiscordLast = lines.Count > 0 ? lines[lines.Count - 1].Id.ToString(CultureInfo.InvariantCulture) : "0";
                ForkSettings.Save();
                return;
            }
            int handled = 0;
            string last = after;
            List<string> batch = new List<string>();
            bool pairing = Pairing != null && !settings.DiscordPaired;
            foreach (Line line in lines)
            {
                bool his = !line.Bot && line.Author == settings.DiscordOwner;
                last = line.Id.ToString(CultureInfo.InvariantCulture);
                if (!his) continue;
                if (pairing)
                {
                    if (line.Content.Trim().ToUpperInvariant() != Pairing) continue;
                    settings.DiscordPaired = true;
                    Pairing = null;
                    ForkSettings.Save();
                    Send("Paired. Message me here and I'll do it on the PC.");
                    Action<string> done = Told;
                    if (done != null) done("Discord is paired. Message the bot and I'll answer there.");
                    break;
                }
                if (handled >= PerPoll) { last = (line.Id - 1).ToString(CultureInfo.InvariantCulture); break; }
                handled++;
                if (line.Content.Trim().Length > 0) batch.Add(line.Content.Trim());
            }
            if (last != after)
            {
                settings.DiscordLast = last;
                ForkSettings.Save();
            }
            if (batch.Count > 0) heard(string.Join("\n", batch.ToArray()));
        }

        internal static void Heads(string text)
        {
            ForkSettings settings = ForkSettings.Current;
            if (!Ready || string.IsNullOrWhiteSpace(text) || !Delivery.HeadsMay(settings, DateTime.Now, Delivery.NotHome())) return;
            if (Send("Heads-up: " + text)) Heartbeat.Told(text);
        }

        internal static bool Send(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(ForkSettings.Current.DiscordChannel)) return false;
            bool sent = true;
            foreach (string part in Split(text.Trim(), Chunk))
            {
                string id;
                sent &= Post(part, out id);
            }
            return sent;
        }

        internal static bool SendFile(byte[] data, string name, string caption)
        {
            string channel = ForkSettings.Current.DiscordChannel;
            if (data == null || data.Length == 0 || data.Length > MaxFile || string.IsNullOrEmpty(channel)) return false;
            string boundary = "capri" + Guid.NewGuid().ToString("N");
            string safe = Regex.Replace(name ?? "file", @"[^A-Za-z0-9._ -]", "_");
            string json = "{\"content\":" + Voice.Quote(caption ?? "") + ",\"allowed_mentions\":{\"parse\":[]},\"attachments\":[{\"id\":0,\"filename\":" + Voice.Quote(safe) + "}]}";
            using (MemoryStream body = new MemoryStream())
            {
                Append(body, "--" + boundary + "\r\nContent-Disposition: form-data; name=\"payload_json\"\r\nContent-Type: application/json\r\n\r\n" + json + "\r\n");
                Append(body, "--" + boundary + "\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"" + safe + "\"\r\nContent-Type: application/octet-stream\r\n\r\n");
                body.Write(data, 0, data.Length);
                Append(body, "\r\n--" + boundary + "--\r\n");
                int status;
                Api("POST", "/channels/" + channel + "/messages", body.ToArray(), "multipart/form-data; boundary=" + boundary, out status);
                return status >= 200 && status < 300;
            }
        }

        // His request from Discord, answered: the words, then a screen picture or a file when his own words asked for one.
        internal static bool MarkedShot(string text)
        {
            return ShotMarker.IsMatch(text ?? "");
        }

        internal static List<string> Parts(string text)
        {
            List<string> found = new List<string>();
            foreach (string piece in Marker.Split(ShotMarker.Replace(text ?? "", " "))) if (piece.Trim().Length > 0 && found.Count < MostParts) found.Add(piece.Trim());
            return found;
        }

        internal static string Plain(string text)
        {
            string plain = Marker.Replace(text ?? "", " ");
            return ShotMarker.IsMatch(plain) ? ShotMarker.Replace(plain, " ").Trim() : plain;
        }

        internal static void Reply(string text, byte[] shot, string file)
        {
            try
            {
                List<string> parts = Parts(text);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (i > 0 && PartPauseMilliseconds > 0) System.Threading.Thread.Sleep(PartPauseMilliseconds);
                    Send(parts[i]);
                }
                if (shot != null) SendFile(shot, "screen.jpg", null);
                if (file != null) SendFile(File.ReadAllBytes(file), Path.GetFileName(file), null);
            }
            catch (Exception error) { Brain.Log("discord reply", -1, error.GetType().Name); }
        }

        // The one file his own words ask her to send, or null. Only an existing, small file that is not a key, a setting or his vault.
        internal static string FileIn(string line)
        {
            if (string.IsNullOrEmpty(line) || !Verbs.IsMatch(line)) return null;
            Match found = Paths.Match(line);
            if (!found.Success) return null;
            string path = found.Groups[1].Success ? found.Groups[1].Value : found.Groups[2].Value;
            try
            {
                path = Path.GetFullPath(path);
                if (!File.Exists(path) || Secrets.IsMatch(path) || new FileInfo(path).Length == 0 || new FileInfo(path).Length > MaxFile) return null;
                string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri") + Path.DirectorySeparatorChar;
                if (path.StartsWith(data, StringComparison.OrdinalIgnoreCase)) return null;
                if (!string.IsNullOrEmpty(Vault.Root) && path.StartsWith(Vault.Root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return null;
                return path;
            }
            catch (Exception) { return null; }
        }

        internal static List<string> Split(string text, int size)
        {
            List<string> parts = new List<string>();
            while (text.Length > size)
            {
                int cut = text.LastIndexOf(' ', size);
                if (cut < size / 2) cut = size;
                parts.Add(text.Substring(0, cut).TrimEnd());
                text = text.Substring(cut).TrimStart();
            }
            if (text.Length > 0) parts.Add(text);
            return parts;
        }

        private sealed class Line
        {
            internal ulong Id;
            internal string Author, Content;
            internal bool Bot;
        }

        private static List<Line> Lines(string reply)
        {
            List<Line> lines = new List<Line>();
            try
            {
                IEnumerable items = new JavaScriptSerializer().DeserializeObject(reply) as IEnumerable;
                if (items == null) return lines;
                foreach (object item in items)
                {
                    Dictionary<string, object> entry = item as Dictionary<string, object>;
                    Dictionary<string, object> author = entry != null && entry.ContainsKey("author") ? entry["author"] as Dictionary<string, object> : null;
                    ulong id;
                    if (entry == null || author == null || !ulong.TryParse(Text(entry, "id"), out id)) continue;
                    lines.Add(new Line { Id = id, Author = Text(author, "id"), Content = Text(entry, "content"), Bot = author.ContainsKey("bot") && author["bot"] is bool && (bool)author["bot"] });
                }
            }
            catch (Exception) { }
            lines.Sort(delegate(Line a, Line b) { return a.Id.CompareTo(b.Id); });
            return lines;
        }

        private static bool Post(string text, out string id)
        {
            int status;
            string reply = Api("POST", "/channels/" + ForkSettings.Current.DiscordChannel + "/messages", "{\"content\":" + Voice.Quote(text) + ",\"allowed_mentions\":{\"parse\":[]}}", out status);
            id = Text(Parse(reply), "id");
            return status >= 200 && status < 300;
        }

        private static string NewCode()
        {
            byte[] bytes = new byte[6];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            StringBuilder code = new StringBuilder();
            foreach (byte b in bytes) code.Append(Alphabet[b % Alphabet.Length]);
            return code.ToString();
        }

        private static string Problem(int status)
        {
            if (status == 0) return "I couldn't reach Discord just now.";
            if (status == 401) return Failed ?? "Discord rejected the bot token.";
            return "Discord said no (" + status + ").";
        }

        private static string Api(string method, string path, string body, out int status)
        {
            return Api(method, path, body == null ? null : Encoding.UTF8.GetBytes(body), "application/json", out status);
        }

        private static string Api(string method, string path, byte[] body, string type, out int status)
        {
            if (DateTime.Now < holdUntil) { status = 429; return ""; }
            string token = Voice.Unprotect(ForkSettings.Current.DiscordToken);
            if (string.IsNullOrEmpty(token)) { status = 401; return ""; }
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Base + path);
            request.Method = method;
            request.Timeout = Wait;
            request.ReadWriteTimeout = Wait;
            request.UserAgent = "DiscordBot (capri, 1)";
            request.Headers["Authorization"] = "Bot " + token;
            string reply;
            try
            {
                if (body != null)
                {
                    request.ContentType = type;
                    request.ContentLength = body.Length;
                    using (Stream stream = request.GetRequestStream()) stream.Write(body, 0, body.Length);
                }
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    status = (int)response.StatusCode;
                    reply = Read(response);
                }
            }
            catch (WebException error)
            {
                HttpWebResponse response = error.Response as HttpWebResponse;
                if (response == null) { status = 0; return ""; }
                using (response)
                {
                    status = (int)response.StatusCode;
                    reply = Read(response);
                }
            }
            if (status == 429) Limit(reply);
            else if (status == 401) Reject();
            return reply;
        }

        private static void Limit(string reply)
        {
            double seconds = 5;
            Dictionary<string, object> data = Parse(reply);
            if (data != null && data.ContainsKey("retry_after"))
            {
                try { seconds = Convert.ToDouble(data["retry_after"], CultureInfo.InvariantCulture); }
                catch (Exception) { }
            }
            holdUntil = DateTime.Now.AddSeconds(Math.Max(1, Math.Min(300, seconds)) + 1);
        }

        private static void Reject()
        {
            if (Failed != null) return;
            Failed = "Discord rejected my bot token, so I've stopped reading your DMs. Put a fresh token in the \"capri bot\" file on your Desktop and say \"connect my Discord\".";
            Action<string> tell = Told;
            if (tell != null) tell(Failed);
        }

        private static string Read(HttpWebResponse response)
        {
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
        }

        private static void Append(Stream stream, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static Dictionary<string, object> Parse(string json)
        {
            try { return new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>; }
            catch (Exception) { return null; }
        }

        private static string Text(Dictionary<string, object> data, string key)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || value == null) return "";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
