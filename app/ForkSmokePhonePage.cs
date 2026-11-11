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
        private static void SmokePhonePage(List<string> checks)
        {
            const string name = "pc.tail1.ts.net";
            if (!PhoneServer.HostOk(name + ":" + PhoneServer.ServePort, 47123, name) || PhoneServer.HostOk(name + ":9999", 47123, name) || PhoneServer.HostOk(name + ":" + PhoneServer.ServePort, 47123, ""))
                throw new Exception("His Tailscale address on port 8443 was judged wrong.");
            if (!PhoneServer.OriginOk(null, name, 47123) || !PhoneServer.OriginOk("https://" + name + ":" + PhoneServer.ServePort, name, 47123) || !PhoneServer.OriginOk("http://127.0.0.1:47123", name, 47123)
                || PhoneServer.OriginOk("https://" + name + ":9999", name, 47123) || PhoneServer.OriginOk("http://127.0.0.1:1", name, 47123) || PhoneServer.OriginOk("https://evil.example", name, 47123) || PhoneServer.OriginOk("https://" + name + ":8443", "", 47123))
                throw new Exception("A page Origin was judged wrong.");
            string index = PhoneFiles.Text("index.html");
            if (index == null || index.IndexOf("app.js", StringComparison.Ordinal) < 0 || index.IndexOf("<style", StringComparison.OrdinalIgnoreCase) >= 0 || index.IndexOf("style=", StringComparison.OrdinalIgnoreCase) >= 0 || index.Replace("<script src=", "").IndexOf("<script", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new Exception("The phone page is missing or has inline script or style (the CSP blocks those).");
            string keptLog = ActivityLog.FilePath;
            Func<string> keptName = PhoneServer.TailnetName;
            string folder = Path.Combine(Path.GetTempPath(), "capri-phone-page-" + Guid.NewGuid().ToString("N"));
            PhoneServer server = null;
            try
            {
                Directory.CreateDirectory(folder);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                TcpListener free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                int port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                server = new PhoneServer(port);
                string problem = server.Start();
                if (problem != null) throw new Exception("The phone server did not start: " + problem);
                PhoneServer.TailnetName = delegate { return name; };
                string host = "127.0.0.1:" + port;
                foreach (string file in new[] { "/", "/app.js", "/app.css", "/manifest.webmanifest", "/icon.png", "/mono.ttf" })
                    if (SmokePhoneRead(port, file, host, null).Status != 200) throw new Exception("The phone page file " + file + " was not served without a token.");
                if (SmokePhoneRead(port, "/", name + ":" + PhoneServer.ServePort, "https://" + name + ":" + PhoneServer.ServePort).Status != 200) throw new Exception("The page was refused on his 8443 address.");
                if (SmokePhoneRead(port, "/", name + ":9999", null).Status != 421) throw new Exception("A wrong port on his name was served.");
                SmokePhoneReply clips = SmokePhoneRead(port, "/clips", host, null);
                if (clips.Status != 200 || clips.Body.IndexOf("\"idle\"", StringComparison.Ordinal) < 0) throw new Exception("The clip list was not served.");
                int listed = 0;
                foreach (Clip clip in Clip.Listed())
                {
                    if (clips.Body.IndexOf("\"" + clip.Name + "\"", StringComparison.Ordinal) < 0) throw new Exception("The clip list is missing " + clip.Name + ".");
                    SmokePhoneReply strip = SmokePhoneRead(port, "/sprite/" + clip.Name + ".png", host, null);
                    if (strip.Status != 200 || strip.Type != "image/png") throw new Exception("Her strip " + clip.Name + " was not served.");
                    listed++;
                }
                if (listed < 20) throw new Exception("Only " + listed + " clips were listed.");
                if (SmokePhoneRead(port, "/sprite/nothing.png", host, null).Status != 404 || SmokePhoneRead(port, "/sprite/..%2Fidle.png", host, null).Status != 404 || SmokePhoneRead(port, "/sprite/idle_flipped.png", host, null).Status != 404)
                    throw new Exception("A strip that is not in her clip list was served.");
                if (SmokePhoneAsk(port, "GET", "/api/ping", host, null, null) != 401) throw new Exception("The API answered without a token.");
                string log = File.Exists(ActivityLog.FilePath) ? File.ReadAllText(ActivityLog.FilePath) : "";
                if (log.IndexOf("refused host  ->  " + name + ":9999", StringComparison.Ordinal) < 0 || log.IndexOf("tailscale login", StringComparison.Ordinal) < 0) throw new Exception("The refused Host or the Tailscale login check was not logged.");
                checks.Add("PASS phone page (CAPRI-095 phase 1, slice 3): his Tailscale address on 8443 is taken and a foreign port or website is refused; the page, its script, style, manifest, icon and fonts are served without a token with no inline script or style, the clip list matches her clip table and every listed strip is served, nothing outside it is; the API still needs the token; a refused Host and whether Tailscale sent his login are logged");
            }
            finally
            {
                if (server != null) server.Stop();
                PhoneServer.TailnetName = keptName;
                ActivityLog.FilePath = keptLog;
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private sealed class SmokePhoneReply
        {
            internal int Status;
            internal string Type = "";
            internal string Body = "";
        }

        private static SmokePhoneReply SmokePhoneRead(int port, string path, string host, string origin)
        {
            SmokePhoneReply reply = new SmokePhoneReply();
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, port);
                    client.ReceiveTimeout = 5000;
                    NetworkStream stream = client.GetStream();
                    string ask = "GET " + path + " HTTP/1.1\r\nHost: " + host + "\r\nConnection: close\r\n" + (origin == null ? "" : "Origin: " + origin + "\r\n") + "\r\n";
                    byte[] head = Encoding.ASCII.GetBytes(ask);
                    stream.Write(head, 0, head.Length);
                    MemoryStream all = new MemoryStream();
                    stream.CopyTo(all);
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
            catch (IOException) { reply.Status = -1; }
            return reply;
        }
    }
}
