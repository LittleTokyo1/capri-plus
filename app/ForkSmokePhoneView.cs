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
        private void SmokePhoneViewWiring(List<string> checks)
        {
            ForkSettings kept = ForkSettings.Current;
            Func<System.Drawing.Bitmap> keptScreen = PhoneScreen;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Blocklist = new string[0];
                ForkSettings.Current.Paused = true;
                int grabbed = 0;
                PhoneScreen = delegate { grabbed++; return new System.Drawing.Bitmap(64, 40); };
                if (PhoneShotRefusal() != PhoneView.Paused || PhoneShot() != null || grabbed != 0) throw new Exception("Her screen was captured for the phone while she was paused.");
                ForkSettings.Current.Paused = false;
                byte[] jpeg = PhoneShot();
                if (PhoneShotRefusal() != null || jpeg == null || jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || grabbed != 1) throw new Exception("With nothing in the way, the phone's screenshot was not one JPEG from her capture.");
                string status = PhoneStatus();
                if (status.IndexOf("\"paused\":false", StringComparison.Ordinal) < 0 || status.IndexOf("\"jobs\":[", StringComparison.Ordinal) < 0 || status.IndexOf("\"digest\":[", StringComparison.Ordinal) < 0) throw new Exception("Her PC status for the phone was not built: " + status);
                checks.Add("PASS phone PC view wiring: her own pause refuses the phone's screenshot before any capture (checked again right before it), an allowed one is a JPEG from her screen capture, and her status lists jobs and away lines");
            }
            finally
            {
                ForkSettings.Current = kept;
                PhoneScreen = keptScreen;
            }
        }

        private static void SmokePhoneView(List<string> checks)
        {
            string keptFile = PhoneAuth.FilePath, keptLog = ActivityLog.FilePath;
            Func<string> keptPc = PhoneServer.Pc, keptRefusal = PhoneServer.ShotRefusal;
            Func<byte[]> keptShot = PhoneServer.Shot;
            string folder = Path.Combine(Path.GetTempPath(), "capri-phone-view-smoke-" + Guid.NewGuid().ToString("N"));
            PhoneServer server = null;
            try
            {
                Directory.CreateDirectory(folder);
                PhoneAuth.FilePath = Path.Combine(folder, "phones.dat");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                PhoneAuth.Reset();
                if (PhoneView.Refusal(true, false) != PhoneView.Paused || PhoneView.Refusal(false, true) != PhoneView.NeverLook || PhoneView.Refusal(true, true) != PhoneView.Paused || PhoneView.Refusal(false, false) != null)
                    throw new Exception("A screenshot was allowed while she is paused or a never-look app is in front, or refused when neither.");
                List<DigestItem> digest = new List<DigestItem>();
                for (int i = 0; i < 9; i++) digest.Add(new DigestItem { When = new DateTime(2026, 11, 4, 9, i, 0), Source = "study", Text = "line " + i });
                string json = PhoneView.Json(false, true, new List<string> { "Hands: open Spotify" }, digest);
                if (json.IndexOf("\"game\":true", StringComparison.Ordinal) < 0 || json.IndexOf("\"jobs\":[\"Hands: open Spotify\"]", StringComparison.Ordinal) < 0 || json.IndexOf("{\"source\":\"study\",\"text\":\"line 8\",\"at\":\"09:08\"}", StringComparison.Ordinal) < 0 || json.IndexOf("line 1\"", StringComparison.Ordinal) >= 0 || json.IndexOf("\"digest\":[{\"source\":\"study\",\"text\":\"line 8\"", StringComparison.Ordinal) < 0)
                    throw new Exception("The PC view did not list his jobs and the newest 7 away lines, newest first: " + json);
                string token = PhoneAuth.Pair(PhoneAuth.NewCode(DateTime.Now), "iPhone", DateTime.Now);
                TcpListener free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                int port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                server = new PhoneServer(port);
                string problem = server.Start();
                if (problem != null) throw new Exception("The phone server did not start: " + problem);
                string host = "127.0.0.1:" + port;
                int taken = 0;
                byte[] fake = Encoding.ASCII.GetBytes("JPEGBYTES");
                PhoneServer.Pc = delegate { return json; };
                PhoneServer.Shot = delegate { taken++; return fake; };
                PhoneServer.ShotRefusal = delegate { return PhoneView.NeverLook; };
                int pcBare = SmokePhoneAsk(port, "GET", "/api/pc", host, null, null), shotBare = SmokePhoneAsk(port, "GET", "/api/shot", host, null, null), shotEvil = SmokePhoneSend(port, "GET", "/api/shot", host, token, null, "https://evil.example");
            if (pcBare != 401 || shotBare != 401 || shotEvil != 403 || taken != 0)
                    throw new Exception("His PC view or a screenshot was open without the token or to another website: " + pcBare + " " + shotBare + " " + shotEvil + " " + taken);
                string refused = SmokePhoneBody(port, "GET", "/api/shot", host, token, null);
                if (refused == null || !refused.StartsWith("409", StringComparison.Ordinal) || refused.IndexOf("never-look", StringComparison.Ordinal) < 0 || taken != 0) throw new Exception("A screenshot was taken while a never-look app was in front.");
                PhoneServer.ShotRefusal = delegate { return null; };
                string shot = SmokePhoneBody(port, "GET", "/api/shot", host, token, null);
                string pc = SmokePhoneBody(port, "GET", "/api/pc", host, token, null);
                if (shot == null || !shot.StartsWith("200", StringComparison.Ordinal) || !shot.EndsWith("JPEGBYTES", StringComparison.Ordinal) || taken != 1 || pc == null || pc.IndexOf("open Spotify", StringComparison.Ordinal) < 0)
                    throw new Exception("His screenshot or PC view did not reach his phone.");
                PhoneServer.Shot = delegate { return null; };
                if (SmokePhoneAsk(port, "GET", "/api/shot", host, token, null) != 503) throw new Exception("A failed screenshot did not say so.");
                string log = File.ReadAllText(ActivityLog.FilePath);
                if (log.IndexOf("screenshot refused  ->  never-look app in front", StringComparison.Ordinal) < 0 || log.IndexOf("screenshot sent", StringComparison.Ordinal) < 0 || log.IndexOf("Spotify", StringComparison.Ordinal) >= 0 || Directory.GetFiles(folder, "*.jp*g", SearchOption.AllDirectories).Length != 0)
                    throw new Exception("The screenshot log lines are missing, carry his screen's words, or a picture landed on disk.");
                PhoneFile page = PhoneFiles.Find("/index.html"), script = PhoneFiles.Find("/app.js");
                string html = page == null ? "" : Encoding.UTF8.GetString(page.Bytes), js = script == null ? "" : Encoding.UTF8.GetString(script.Bytes);
                if (html.IndexOf("id=\"pcSheet\"", StringComparison.Ordinal) < 0 || html.IndexOf("LOOK NOW", StringComparison.Ordinal) < 0 || js.IndexOf("fetch(\"api/shot\"", StringComparison.Ordinal) < 0 || js.IndexOf("readAsDataURL", StringComparison.Ordinal) < 0 || js.IndexOf("localStorage.setItem(\"capri.shot", StringComparison.Ordinal) >= 0)
                    throw new Exception("The phone has no PC VIEW sheet, or keeps the screenshot.");
                checks.Add("PASS phone PC view (CAPRI-095 phase 3): a PC VIEW sheet in the red kit shows her running jobs and the newest 7 away lines; LOOK NOW takes a screenshot only when he asks, as a JPEG in memory straight to his phone (never on disk, never kept on the phone), refused while she is paused or a never-look app is in front; token and Origin checked; log lines carry no words");
            }
            finally
            {
                if (server != null) server.Stop();
                PhoneServer.Pc = keptPc;
                PhoneServer.ShotRefusal = keptRefusal;
                PhoneServer.Shot = keptShot;
                PhoneAuth.FilePath = keptFile;
                ActivityLog.FilePath = keptLog;
                PhoneAuth.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }
    }
}
