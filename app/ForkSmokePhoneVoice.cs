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
        private static void SmokePhoneVoiceServer(List<string> checks)
        {
            string keptFile = PhoneAuth.FilePath, keptLog = ActivityLog.FilePath;
            Func<string, bool, string> keptSaid = PhoneServer.Said;
            Func<short[], string> keptListened = PhoneServer.Listened;
            Func<int> keptTurn = PhoneServer.Turn;
            Func<int, byte[]> keptAudio = PhoneServer.Audio;
            Func<string> keptState = PhoneServer.State;
            string folder = Path.Combine(Path.GetTempPath(), "capri-phone-voice-" + Guid.NewGuid().ToString("N"));
            PhoneServer server = null;
            try
            {
                Directory.CreateDirectory(folder);
                PhoneAuth.FilePath = Path.Combine(folder, "phones.dat");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                PhoneAuth.Reset();
                DateTime now = DateTime.Now;
                string token = PhoneAuth.Pair(PhoneAuth.NewCode(now), "iPhone", now);
                TcpListener free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                int port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                server = new PhoneServer(port);
                string problem = server.Start();
                if (problem != null) throw new Exception("The phone server did not start: " + problem);
                string host = "127.0.0.1:" + port;
                byte[] wav = Voice.Wave(new short[1600], 16000);
                bool askedVoice = false;
                PhoneServer.Listened = delegate(short[] samples) { return samples.Length == 1600 ? "hello capri" : ""; };
                PhoneServer.Said = delegate(string text, bool voice) { askedVoice = voice; return "Hi from her."; };
                PhoneServer.Turn = delegate { return 7; };
                PhoneServer.Audio = delegate(int turn) { return turn == 7 ? wav : null; };
                byte[] speech = new byte[3200];
                if (SmokePhonePost(port, "/api/hear?voice=1", host, null, speech).Status != 401) throw new Exception("Phone audio was heard without a token.");
                SmokePhoneReply heard = SmokePhonePost(port, "/api/hear?voice=1&rate=44100&mic=48000x", host, token, speech);
                if (heard.Status != 200 || heard.Body.IndexOf("\"heard\":\"hello capri\"", StringComparison.Ordinal) < 0 || heard.Body.IndexOf("\"said\":\"Hi from her.\"", StringComparison.Ordinal) < 0 || heard.Body.IndexOf("\"turn\":7", StringComparison.Ordinal) < 0 || !askedVoice)
                    throw new Exception("A phone voice line was not heard and answered: " + heard.Status + " " + heard.Body);
                SmokePhoneReply silent = SmokePhonePost(port, "/api/hear", host, token, new byte[320]);
                if (silent.Status != 200 || silent.Body.IndexOf("\"heard\":\"\"", StringComparison.Ordinal) < 0) throw new Exception("Phone audio with no words was answered.");
                if (SmokePhonePost(port, "/api/hear", host, token, new byte[PhoneServer.MostAudio + 2]).Status != 413 || SmokePhonePost(port, "/api/hear", host, token, new byte[3]).Status != 400) throw new Exception("Too much or broken phone audio was read.");
                SmokePhoneReply typed = SmokePhonePost(port, "/api/say", host, token, Encoding.UTF8.GetBytes("text=hi&voice=0"));
                if (typed.Status != 200 || typed.Body.IndexOf("\"turn\":7", StringComparison.Ordinal) < 0 || askedVoice) throw new Exception("A typed phone line did not say whether it wants her voice.");
                if (SmokePhoneGet(port, "/api/voice?turn=7", host, null).Status != 401) throw new Exception("Her phone voice was served without a token.");
                if (SmokePhoneGet(port, "/api/voice?turn=7", host, token).Status != 200 || SmokePhoneGet(port, "/api/voice?turn=6", host, token).Status != 204 || SmokePhoneGet(port, "/api/voice?turn=x", host, token).Status != 204)
                    throw new Exception("Her phone voice was not served for its own turn only.");
                PhoneServer.State = delegate { return "{\"clip\":\"perch\",\"cell\":3,\"left\":true,\"mouth\":-1}"; };
                SmokePhoneReply seen = SmokePhoneGet(port, "/api/state", host, token);
                if (SmokePhoneGet(port, "/api/state", host, null).Status != 401 || seen.Status != 200 || seen.Body.IndexOf("\"clip\":\"perch\"", StringComparison.Ordinal) < 0) throw new Exception("Her PC pose was not handed to the phone, or was handed without the token.");
                string keptClip = PhoneMicTrace.LastClip;
                PhoneMicTrace.LastClip = Path.Combine(folder, "phone-last.wav");
                try
                {
                    SmokePhoneReply traced = SmokePhonePost(port, "/api/hear?voice=0&rate=48000&mic=48000&held=2000&how=rec&app=1&played=1500", host, token, speech);
                    if (traced.Status != 200) throw new Exception("A traced phone voice line was refused: " + traced.Status);
                    if (!File.Exists(PhoneMicTrace.LastClip) || new FileInfo(PhoneMicTrace.LastClip).Length != 44 + speech.Length) throw new Exception("The last phone clip was not kept as a local WAV.");
                    SmokePhonePost(port, "/api/hear?voice=0&held=abc&how=<b>&app=7&played=-3", host, token, speech);
                }
                finally { PhoneMicTrace.LastClip = keptClip; }
                string traceLog = File.ReadAllText(ActivityLog.FilePath);
                if (traceLog.IndexOf("phone  voice line  ->  100 ms, page 48000 Hz, mic 48000 Hz, held 2000 ms, recorder, home screen app, her voice 1500 ms before", StringComparison.Ordinal) < 0) throw new Exception("The phone voice line does not say how long he held, how it was recorded, where the page ran and when her voice last played.");
                if (traceLog.IndexOf("<b>", StringComparison.Ordinal) >= 0 || traceLog.IndexOf("held 0", StringComparison.Ordinal) >= 0) throw new Exception("Bad trace values from the phone reached the log.");
                if (PhoneMicTrace.Ears(true, true) != PhoneMicTrace.ChatEars || PhoneMicTrace.Ears(false, true) != PhoneMicTrace.OwnEars || PhoneMicTrace.Ears(true, false) != PhoneMicTrace.ChatEars || PhoneMicTrace.Ears(false, false) != PhoneMicTrace.WindowsEars)
                    throw new Exception("Phone audio does not use her own Parakeet ears when voice chat is off (the 2026-11-05 scrambled words were Windows speech).");
                string look = PhoneFiles.Text("app.css") ?? "";
                if (look.IndexOf(".stage { flex: 0 1 38vh; min-height: 120px;", StringComparison.Ordinal) < 0 || look.IndexOf("max-height: 20vh; overflow-y: auto;", StringComparison.Ordinal) < 0 || look.IndexOf(".talkrow, .typebox { flex-shrink: 0; }", StringComparison.Ordinal) < 0)
                    throw new Exception("A long reply on the phone can push the talk key and text box off the screen.");
                string longLine = new string('a', 20000);
                int heardLength = 0;
                Func<string, bool, string> keptLong = PhoneServer.Said;
                PhoneServer.Said = delegate(string text, bool voice) { heardLength = text.Length; return "Read it all."; };
                SmokePhoneReply longSaid = SmokePhonePost(port, "/api/say", host, token, Encoding.UTF8.GetBytes("text=" + longLine + "&voice=0"));
                PhoneServer.Said = keptLong;
                if (longSaid.Status != 200 || heardLength != longLine.Length || Fork.MessageLimit < 100000 || Understand.MostChars < 2000) throw new Exception("A long message from the phone was refused or cut (" + longSaid.Status + ", " + heardLength + " of " + longLine.Length + "), or her message limit is still small.");
                string room = PhoneFiles.Text("index.html") ?? "";
                if (room.IndexOf("maxlength=\"1000\"", StringComparison.Ordinal) >= 0) throw new Exception("The phone type box still stops at 1000 characters.");
                if (room.IndexOf("id=\"chatFull\"", StringComparison.Ordinal) < 0 || room.IndexOf("<textarea id=\"say\"", StringComparison.Ordinal) < 0 || look.IndexOf("body.chatfull .stage", StringComparison.Ordinal) < 0 || look.IndexOf("body.chatfull .log li.mine", StringComparison.Ordinal) < 0 || look.IndexOf("white-space: pre-wrap", StringComparison.Ordinal) < 0)
                    throw new Exception("The phone chat has no full screen key next to CHAT, no multi-line type box or no full messages layout.");
                string page = PhoneFiles.Text("app.js") ?? "";
                if (page.IndexOf("classList.toggle(\"chatfull\")", StringComparison.Ordinal) < 0 || page.IndexOf("function grow(", StringComparison.Ordinal) < 0) throw new Exception("The phone full screen chat key or the growing type box is not wired.");
                if (page.IndexOf("MediaRecorder", StringComparison.Ordinal) < 0 || page.IndexOf("decodeAudioData", StringComparison.Ordinal) < 0 || page.IndexOf("&held=", StringComparison.Ordinal) < 0 || page.IndexOf("&how=", StringComparison.Ordinal) < 0 || page.IndexOf("&app=", StringComparison.Ordinal) < 0)
                    throw new Exception("The phone page does not record through MediaRecorder with its own rate or does not send the mic trace.");
                checks.Add("PASS phone mic trace: the page records through MediaRecorder and decodes at the file's own rate (Web Audio tap kept as fallback), each voice line logs how long he held, recorder or tap, Home Screen app or Safari and when her voice last played (bad values dropped), and the last clip is kept as one local WAV; phone audio is heard by her own Parakeet ears even when voice chat is off (voice chat's ears while it runs, Windows speech only with no Parakeet ears)");
                PhoneServer.Listened = null;
                if (SmokePhonePost(port, "/api/hear", host, token, speech).Status != 503) throw new Exception("Phone audio was taken with no ears.");
                string log = File.ReadAllText(ActivityLog.FilePath);
                if (log.IndexOf("hello capri", StringComparison.Ordinal) >= 0 || log.IndexOf("Hi from her", StringComparison.Ordinal) >= 0 || log.IndexOf("phone  voice line  ->  100 ms, page 44100 Hz, mic 0 Hz", StringComparison.Ordinal) < 0) throw new Exception("The phone voice log lines are missing or carry words.");
                checks.Add("PASS phone voice server (CAPRI-095 phase 2): held-to-talk audio (16 kHz PCM16, at most 30 s) needs the token, goes through her ears and her answer comes back with its turn; no words means no turn; too much, broken or earless audio is refused; her voice for a phone turn is served for that turn only, with the token; the log never carries words");
            }
            finally
            {
                if (server != null) server.Stop();
                PhoneServer.Said = keptSaid;
                PhoneServer.Listened = keptListened;
                PhoneServer.Turn = keptTurn;
                PhoneServer.Audio = keptAudio;
                PhoneServer.State = keptState;
                PhoneAuth.FilePath = keptFile;
                ActivityLog.FilePath = keptLog;
                PhoneAuth.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private void SmokePhoneVoiceTurn(List<string> checks)
        {
            byte[] wav = Voice.Wave(new short[8000], 16000);
            Func<string, byte[]> keptPick = PhoneVoicePick;
            try
            {
                PhoneVoicePick = delegate(string words) { return wav; };
                string said = "Hello from her phone voice.";
                PhoneAsked("x", true);
                int turn = phoneTurn;
                if (!ForkPhoneAnswer(asking, ref said, TalkState.Talking)) throw new Exception("The phone voice turn's answer did not go to the phone.");
                byte[] got = PhoneAudio(turn);
                if (got != wav || PhoneAudio(turn - 1) != null) throw new Exception("Her voice for the phone turn was not handed to the phone, or went to another turn.");
                PhoneMouth(said, got);
                if (mouths == null || speech == null) throw new Exception("Her PC sprite did not talk along with the phone.");
                string pose = PhoneState();
                if (pose.IndexOf("\"clip\":\"", StringComparison.Ordinal) < 0 || pose.IndexOf("\"left\":", StringComparison.Ordinal) < 0 || pose.IndexOf("\"cell\":", StringComparison.Ordinal) < 0) throw new Exception("Her PC pose for the phone is missing a part: " + pose);
                phoneLine = null;
                phoneAnswer = null;
                string typedSaid = "Typed only.";
                PhoneAsked("y", false);
                int typedTurn = phoneTurn;
                if (!ForkPhoneAnswer(asking, ref typedSaid, TalkState.Talking) || PhoneAudio(typedTurn) != null) throw new Exception("A phone line that did not want her voice got one.");
                checks.Add("PASS phone voice turns (CAPRI-095 phase 2): a phone turn that wants her voice gets the same voice as the PC, made on the PC and handed only to that turn, never played on the PC speakers, while her PC sprite's mouth moves along; a typed line with her voice off gets none");
            }
            finally
            {
                PhoneVoicePick = keptPick;
                phoneLine = null;
                phoneAnswer = null;
                phoneTicket = -1;
                CloseTalk();
                Say(null);
            }
        }

        private static SmokePhoneReply SmokePhoneGet(int port, string path, string host, string token)
        {
            return SmokePhoneSendBytes(port, "GET", path, host, token, null);
        }

        private static SmokePhoneReply SmokePhonePost(int port, string path, string host, string token, byte[] body)
        {
            return SmokePhoneSendBytes(port, "POST", path, host, token, body);
        }

        private static SmokePhoneReply SmokePhoneSendBytes(int port, string method, string path, string host, string token, byte[] body)
        {
            SmokePhoneReply reply = new SmokePhoneReply();
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, port);
                    client.ReceiveTimeout = 10000;
                    NetworkStream stream = client.GetStream();
                    StringBuilder ask = new StringBuilder(method + " " + path + " HTTP/1.1\r\nHost: " + host + "\r\nConnection: close\r\n");
                    if (token != null) ask.Append("Authorization: Bearer ").Append(token).Append("\r\n");
                    if (body != null) ask.Append("Content-Type: application/octet-stream\r\nContent-Length: ").Append(body.Length).Append("\r\n");
                    ask.Append("\r\n");
                    byte[] head = Encoding.ASCII.GetBytes(ask.ToString());
                    stream.Write(head, 0, head.Length);
                    try { if (body != null && body.Length > 0) stream.Write(body, 0, body.Length); }
                    catch (IOException) { }
                    MemoryStream all = new MemoryStream();
                    try { stream.CopyTo(all); }
                    catch (IOException) { }
                    string text = Encoding.UTF8.GetString(all.ToArray());
                    int split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    string top = split < 0 ? text : text.Substring(0, split);
                    reply.Body = split < 0 ? "" : text.Substring(split + 4);
                    string[] lines = top.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string[] parts = lines[0].Split(' ');
                    int code;
                    reply.Status = parts.Length > 1 && int.TryParse(parts[1], out code) ? code : 0;
                    foreach (string line in lines)
                        if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase)) reply.Type = line.Substring(13).Trim();
                }
            }
            catch (SocketException) { reply.Status = -1; }
            return reply;
        }
    }
}
