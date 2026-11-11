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
        private static void SmokePhone(List<string> checks)
        {
            string keptFile = PhoneAuth.FilePath, keptLog = ActivityLog.FilePath;
            Func<string> keptName = PhoneServer.TailnetName;
            string folder = Path.Combine(Path.GetTempPath(), "capri-phone-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                PhoneAuth.FilePath = Path.Combine(folder, "phones.dat");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                PhoneAuth.Reset();
                string token = SmokePhonePairing();
                SmokePhoneServer(token);
                checks.Add("PASS phone server core (CAPRI-095 phase 1, slice 1): it listens only on 127.0.0.1 and nothing listens until it is started (not wired yet); a six-character code pairs once within 5 minutes, five wrong codes lock pairing for 10 minutes, tokens are kept only as hashes in a DPAPI file; every route but the shell and /pair needs the token, a foreign Host is refused, revoke shuts a phone out; log lines carry no text");
            }
            finally
            {
                PhoneAuth.FilePath = keptFile;
                ActivityLog.FilePath = keptLog;
                PhoneServer.TailnetName = keptName;
                PhoneAuth.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private void SmokePhoneTurns(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            if (fresh.PhoneOn || fresh.PhonePort != 47123) throw new Exception("The phone app is not off by default.");
            Func<string, bool, string> keptSaid = PhoneServer.Said;
            Func<string> keptName = PhoneServer.TailnetName;
            string keptLog = ActivityLog.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-phone-turns-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                string said = "Hello from her.";
                phoneLine = "x";
                phoneAnswer = null;
                phoneTicket = asking + 5;
                if (ForkPhoneAnswer(asking, ref said, TalkState.Talking)) throw new Exception("A phone answer took another turn's ticket.");
                phoneTicket = -1;
                if (ForkPhoneAnswer(asking - 1, ref said, TalkState.Talking)) throw new Exception("An older turn's answer went to the phone.");
                if (!ForkPhoneAnswer(asking, ref said, TalkState.Talking) || phoneAnswer != "Hello from her.") throw new Exception("The phone turn's answer did not go to the phone.");
                if (ForkPhoneAnswer(asking, ref said, TalkState.Talking)) throw new Exception("One phone line got two answers.");
                phoneLine = null;
                phoneAnswer = null;
                phoneTicket = -1;
                string other = "Not for the phone.";
                if (ForkPhoneAnswer(asking, ref other, TalkState.Talking)) throw new Exception("An answer went to the phone with no phone line waiting.");
                TcpListener free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                int port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                ForkSettings on = new ForkSettings();
                on.PhoneOn = true;
                on.PhonePort = port;
                PhoneSwitch(on);
                if (phoneServer == null || !phoneServer.Running || PhoneServer.Said == null) throw new Exception("The phone server did not start when switched on.");
                on.PhoneOn = false;
                PhoneSwitch(on);
                if (phoneServer != null || SmokePhoneAsk(port, "GET", "/", "127.0.0.1:" + port, null, null) != -1) throw new Exception("The phone server did not stop when switched off.");
                checks.Add("PASS phone text turns (CAPRI-095 phase 1, slice 2): the phone app is off by default and starts and stops with its setting; a line from the phone goes to her as a phone turn and only that turn's answer goes back to it, once; a busy or empty line and a missing token are refused; the line never reaches the log");
            }
            finally
            {
                if (phoneServer != null) { phoneServer.Stop(); phoneServer = null; }
                phoneLine = null;
                phoneAnswer = null;
                phoneTicket = -1;
                PhoneServer.Said = keptSaid;
                PhoneServer.TailnetName = keptName;
                ActivityLog.FilePath = keptLog;
                CloseTalk();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private static string SmokePhonePairing()
        {
            DateTime now = new DateTime(2026, 11, 4, 2, 30, 0);
            if (PhoneAuth.Pair("ABCDEF", "x", now) != null) throw new Exception("A phone paired with no code shown on the PC.");
            string shown = PhoneAuth.NewCode(now);
            if (shown == null || shown.Length != PhoneAuth.CodeLength) throw new Exception("The pairing code was not made.");
            for (int i = 0; i < PhoneAuth.MostWrong - 1; i++) if (PhoneAuth.Pair("WRONG1", "x", now) != null) throw new Exception("A wrong pairing code paired a phone.");
            string token = PhoneAuth.Pair(shown.ToLowerInvariant(), "iPhone", now);
            if (token == null || PhoneAuth.Device(token) == null || PhoneAuth.Device(token + "x") != null || PhoneAuth.Device(null) != null) throw new Exception("The right code did not pair, or a wrong token was taken.");
            if (PhoneAuth.Pair(shown, "again", now) != null) throw new Exception("A pairing code worked twice.");
            byte[] stored = File.ReadAllBytes(PhoneAuth.FilePath);
            string raw = Encoding.UTF8.GetString(stored) + Encoding.Unicode.GetString(stored);
            if (raw.Contains(token) || raw.Contains(PhoneAuth.HashOf(token)) || raw.Contains("iPhone")) throw new Exception("The pairing file is not encrypted.");
            string second = PhoneAuth.NewCode(now);
            for (int i = 0; i < PhoneAuth.MostWrong; i++) PhoneAuth.Pair("WRONG1", "x", now);
            if (!PhoneAuth.Locked(now) || PhoneAuth.Pair(second, "x", now) != null || PhoneAuth.NewCode(now) != null || PhoneAuth.Locked(now.AddMinutes(PhoneAuth.LockMinutes + 1))) throw new Exception("Five wrong codes did not lock pairing for 10 minutes.");
            PhoneAuth.Reset();
            string late = PhoneAuth.NewCode(now);
            if (PhoneAuth.Pair(late, "x", now.AddMinutes(PhoneAuth.CodeMinutes + 1)) != null) throw new Exception("An old pairing code still paired.");
            PhoneAuth.Reset();
            if (!PhoneServer.HostOk("127.0.0.1:47123", 47123, "") || !PhoneServer.HostOk("LOCALHOST:47123", 47123, "") || PhoneServer.HostOk("evil.example", 47123, "") || PhoneServer.HostOk("192.168.1.20:47123", 47123, "")
                || PhoneServer.HostOk("pc.tail1.ts.net", 47123, "") || !PhoneServer.HostOk("pc.tail1.ts.net", 47123, "pc.tail1.ts.net") || PhoneServer.HostOk("pc.tail1.ts.net.evil.example", 47123, "pc.tail1.ts.net") || PhoneServer.HostOk(null, 47123, "pc.tail1.ts.net"))
                throw new Exception("A Host header was judged wrong.");
            if (PhoneServer.Bearer("Bearer abc") != "abc" || PhoneServer.Bearer("abc") != null || PhoneServer.Field("code=AB%2012&name=my+phone", "name") != "my phone") throw new Exception("A token header or form field was read wrong.");
            return token;
        }

        private static void SmokePhoneServer(string token)
        {
            TcpListener free = new TcpListener(IPAddress.Loopback, 0);
            free.Start();
            int port = ((IPEndPoint)free.LocalEndpoint).Port;
            free.Stop();
            PhoneServer server = new PhoneServer(port);
            if (server.Running || SmokePhoneAsk(port, "GET", "/", "127.0.0.1:" + port, null, null) != -1) throw new Exception("Something listened before the phone server was started.");
            string problem = server.Start();
            if (problem != null) throw new Exception("The phone server did not start on loopback: " + problem);
            try
            {
                string host = "127.0.0.1:" + port;
                if (SmokePhoneAsk(port, "GET", "/", host, null, null) != 200) throw new Exception("The phone shell page was not served.");
                if (SmokePhoneAsk(port, "GET", "/api/ping", host, null, null) != 401 || SmokePhoneAsk(port, "GET", "/api/ping", host, "nope", null) != 401) throw new Exception("A route answered without a valid token.");
                if (SmokePhoneAsk(port, "GET", "/api/ping", host, token, null) != 200) throw new Exception("A paired phone was refused.");
                if (SmokePhoneAsk(port, "GET", "/", "evil.example", null, null) != 421 || SmokePhoneAsk(port, "GET", "/api/ping", "pc.tail1.ts.net", token, null) != 421) throw new Exception("A foreign Host header was served (DNS rebinding).");
                PhoneServer.TailnetName = delegate { return "pc.tail1.ts.net"; };
                if (SmokePhoneAsk(port, "GET", "/api/ping", "pc.tail1.ts.net", token, null) != 200) throw new Exception("His tailnet name was refused.");
                if (SmokePhoneAsk(port, "POST", "/pair", host, null, "code=WRONG1&name=x") != 403) throw new Exception("A wrong code over the network was not refused.");
                if (SmokePhoneAsk(port, "POST", "/pair", host, null, "code=" + new string('A', PhoneServer.MostBody + 10)) != 413) throw new Exception("A huge pairing request was read.");
                PhoneServer.Said = delegate(string text, bool voice) { return text == "busy" ? null : "Hi from her."; };
                if (SmokePhoneAsk(port, "POST", "/api/say", host, token, "text=hello+there") != 200 || SmokePhoneAsk(port, "POST", "/api/say", host, null, "text=hello") != 401 || SmokePhoneAsk(port, "POST", "/api/say", host, token, "text=busy") != 429 || SmokePhoneAsk(port, "POST", "/api/say", host, token, "text=") != 400)
                    throw new Exception("A phone line was not answered, or was answered without the token, while busy or empty.");
                if (File.ReadAllText(ActivityLog.FilePath).IndexOf("hello", StringComparison.Ordinal) >= 0 || PhoneServer.Json("a\"b\n") != "\"a\\\"b\\u000a\"") throw new Exception("A phone line reached the log, or an answer was not escaped.");
                PhoneServer.Said = null;
                PhoneAuth.NewCode(DateTime.Now);
                for (int i = 0; i < PhoneAuth.MostWrong + 1; i++) if (SmokePhoneSend(port, "POST", "/pair", host, null, "code=WRONG1&name=x", "https://evil.example") != 403) throw new Exception("A pairing try from another website was not refused.");
                if (PhoneAuth.Locked(DateTime.Now) || SmokePhoneSend(port, "GET", "/", "pc.tail1.ts.net", null, null, "https://pc.tail1.ts.net") != 200) throw new Exception("Other websites could lock his pairing, or his own phone page was refused.");
                PhoneAuth.Reset();
                PhoneAuth.Revoke(PhoneAuth.Device(token));
                if (SmokePhoneAsk(port, "GET", "/api/ping", host, token, null) != 401) throw new Exception("A revoked phone still got in.");
                string log = File.Exists(ActivityLog.FilePath) ? File.ReadAllText(ActivityLog.FilePath) : "";
                if (log.IndexOf("phone  refused host", StringComparison.Ordinal) < 0 || log.IndexOf("WRONG1", StringComparison.Ordinal) >= 0 || log.IndexOf(token, StringComparison.Ordinal) >= 0) throw new Exception("The phone log lines are missing or carry text.");
            }
            finally { server.Stop(); }
            if (server.Running || SmokePhoneAsk(port, "GET", "/", "127.0.0.1:" + port, null, null) != -1) throw new Exception("The phone server still listened after it was stopped.");
        }

        private static int SmokePhoneAsk(int port, string method, string path, string host, string token, string body)
        {
            return SmokePhoneSend(port, method, path, host, token, body, null);
        }

        private static int SmokePhoneSend(int port, string method, string path, string host, string token, string body, string origin)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, port);
                    client.ReceiveTimeout = 5000;
                    NetworkStream stream = client.GetStream();
                    byte[] content = Encoding.UTF8.GetBytes(body ?? "");
                    StringBuilder ask = new StringBuilder();
                    ask.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\nHost: ").Append(host).Append("\r\nConnection: close\r\n");
                    if (token != null) ask.Append("Authorization: Bearer ").Append(token).Append("\r\n");
                    if (origin != null) ask.Append("Origin: ").Append(origin).Append("\r\n");
                    if (body != null) ask.Append("Content-Type: application/x-www-form-urlencoded\r\nContent-Length: ").Append(content.Length).Append("\r\n");
                    ask.Append("\r\n");
                    byte[] head = Encoding.ASCII.GetBytes(ask.ToString());
                    stream.Write(head, 0, head.Length);
                    if (content.Length > 0) stream.Write(content, 0, content.Length);
                    string status = new StreamReader(stream, Encoding.ASCII).ReadLine() ?? "";
                    string[] parts = status.Split(' ');
                    int code;
                    return parts.Length > 1 && int.TryParse(parts[1], out code) ? code : 0;
                }
            }
            catch (SocketException) { return -1; }
            catch (IOException) { return -1; }
        }
    }
}
