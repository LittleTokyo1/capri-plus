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
    internal sealed partial class Companion
    {
        private const string SmokeOwnLine = "Time for some water, choom.";

        private static void SmokePhoneLive(List<string> checks)
        {
            string keptFile = PhoneAuth.FilePath, keptLog = ActivityLog.FilePath;
            Func<string> keptName = PhoneServer.TailnetName;
            int keptPoll = PhoneLive.PollMs;
            string folder = Path.Combine(Path.GetTempPath(), "capri-phone-live-smoke-" + Guid.NewGuid().ToString("N"));
            PhoneServer server = null;
            try
            {
                Directory.CreateDirectory(folder);
                PhoneAuth.FilePath = Path.Combine(folder, "phones.dat");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                PhoneAuth.Reset();
                PhoneLive.Reset();
                PhoneLive.PollMs = 4000;
                if (PhoneLive.Open() || PhoneLive.Offer(SmokeOwnLine, 2000) || PhoneLive.Last != 0) throw new Exception("A line was offered to a phone page that is not open.");
                string token = PhoneAuth.Pair(PhoneAuth.NewCode(DateTime.Now), "iPhone", DateTime.Now);
                TcpListener free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                int port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                server = new PhoneServer(port);
                string problem = server.Start();
                if (problem != null) throw new Exception("The phone server did not start: " + problem);
                string host = "127.0.0.1:" + port;
                if (SmokePhoneAsk(port, "GET", "/api/events?after=-1", host, null, null) != 401 || SmokePhoneSend(port, "GET", "/api/events?after=-1", host, token, null, "https://evil.example") != 403 || SmokePhoneAsk(port, "POST", "/api/ack", host, null, "id=1") != 401)
                    throw new Exception("Her live lines were open without the token or to another website.");
                string first = SmokePhoneBody(port, "GET", "/api/events?after=-1", host, token, null);
                if (first == null || !first.StartsWith("200", StringComparison.Ordinal) || first.IndexOf("\"events\":[]", StringComparison.Ordinal) < 0 || !PhoneLive.Open()) throw new Exception("The page's first call did not come back at once with nothing old: " + first);
                SmokeAckedLine(port, host, token);
                if (PhoneLive.Offer("Nobody is looking.", 600)) throw new Exception("A line the page never acked counted as shown.");
                bool discord = false;
                if (!Delivery.Route("Nobody is looking either.", delegate(string line) { discord = true; return true; }) || !discord) throw new Exception("A line with no ack did not go on to Discord.");
                if (SmokePhoneAsk(port, "POST", "/api/ack", host, token, "id=x") != 400 || PhoneLive.Ack(9999)) throw new Exception("A broken or unknown ack was taken.");
                DateTime utc = DateTime.UtcNow;
                ForkSettings home = new ForkSettings();
                if (!Delivery.NotHome(home, false, 2 * 60000L, long.MaxValue, 3 * 60000L) || Delivery.NotHome(home, true, 2 * 60000L, long.MaxValue, 3 * 60000L) || Delivery.NotHome(home, false, 2 * 60000L, 60000L, 3 * 60000L) || Delivery.RemoteMilliseconds(utc) > Delivery.DiscordMilliseconds(utc))
                    throw new Exception("A line from the phone did not count as him being on the phone like a Discord line, or it did in a game or after he used the PC.");
                string log = File.ReadAllText(ActivityLog.FilePath);
                if (log.IndexOf("own line shown", StringComparison.Ordinal) < 0 || log.IndexOf("own line not acked", StringComparison.Ordinal) < 0 || log.IndexOf("water", StringComparison.Ordinal) >= 0 || log.IndexOf("Nobody", StringComparison.Ordinal) >= 0)
                    throw new Exception("The live phone log lines are missing or carry her words.");
                checks.Add("PASS phone live lines (CAPRI-095): a line she starts herself (check-in, heartbeat, reminder, school mail, friend news) goes to the open phone page first over a long poll (/api/events, token, Host and Origin checked) and counts as delivered only when the page acks it within 5 s; no ack, or no open page, sends it on by her usual way (Discord); a line from the phone counts as him being on the phone like a Discord line (never in a game, not after he used the PC); log lines carry no words");
            }
            finally
            {
                if (server != null) server.Stop();
                PhoneLive.Reset();
                PhoneLive.PollMs = keptPoll;
                PhoneAuth.FilePath = keptFile;
                ActivityLog.FilePath = keptLog;
                PhoneServer.TailnetName = keptName;
                PhoneAuth.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private static void SmokeAckedLine(int port, string host, string token)
        {
            string polled = null;
            Thread page = new Thread(delegate() { polled = SmokePhoneBody(port, "GET", "/api/events?after=0", host, token, null); }) { IsBackground = true };
            page.Start();
            Thread.Sleep(300);
            bool shown = false;
            Thread offer = new Thread(delegate() { shown = PhoneLive.Offer(SmokeOwnLine, 3000); }) { IsBackground = true };
            offer.Start();
            if (!page.Join(3000) || polled == null || !polled.StartsWith("200", StringComparison.Ordinal) || polled.IndexOf(SmokeOwnLine, StringComparison.Ordinal) < 0)
                throw new Exception("Her own line did not reach the waiting phone page: " + (polled ?? "no answer"));
            Match id = Regex.Match(polled, "\"id\":(\\d+)");
            if (!id.Success || SmokePhoneAsk(port, "POST", "/api/ack", host, token, "id=" + id.Groups[1].Value) != 200) throw new Exception("The page could not ack her line.");
            if (!offer.Join(4000) || !shown) throw new Exception("An acked line did not count as shown on the phone.");
        }

        private static string SmokePhoneBody(int port, string method, string path, string host, string token, string body)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, port);
                    client.ReceiveTimeout = 10000;
                    NetworkStream stream = client.GetStream();
                    byte[] content = Encoding.UTF8.GetBytes(body ?? "");
                    StringBuilder ask = new StringBuilder();
                    ask.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\nHost: ").Append(host).Append("\r\nConnection: close\r\n");
                    if (token != null) ask.Append("Authorization: Bearer ").Append(token).Append("\r\n");
                    if (body != null) ask.Append("Content-Type: application/x-www-form-urlencoded\r\nContent-Length: ").Append(content.Length).Append("\r\n");
                    ask.Append("\r\n");
                    byte[] head = Encoding.ASCII.GetBytes(ask.ToString());
                    stream.Write(head, 0, head.Length);
                    if (content.Length > 0) stream.Write(content, 0, content.Length);
                    string all = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
                    string[] parts = all.Split(new[] { ' ' }, 3);
                    int split = all.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    return (parts.Length > 1 ? parts[1] : "0") + "\n" + (split < 0 ? "" : all.Substring(split + 4));
                }
            }
            catch (SocketException) { return null; }
            catch (IOException) { return null; }
        }
    }
}
