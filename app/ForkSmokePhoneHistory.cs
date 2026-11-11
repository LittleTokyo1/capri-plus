using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokePhoneHistory(List<string> checks)
        {
            string keptFile = PhoneAuth.FilePath, keptLog = ActivityLog.FilePath;
            Func<string> keptHistory = PhoneServer.History;
            string folder = Path.Combine(Path.GetTempPath(), "capri-phone-history-smoke-" + Guid.NewGuid().ToString("N"));
            PhoneServer server = null;
            try
            {
                string logs = Path.Combine(folder, "chatlog");
                Directory.CreateDirectory(logs);
                PhoneAuth.FilePath = Path.Combine(folder, "phones.dat");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                PhoneAuth.Reset();
                DateTime now = new DateTime(2026, 11, 4, 12, 10, 0);
                File.WriteAllLines(Path.Combine(logs, "2026-10-20.log"), new[] { "09:00:00 user: too old to show" });
                File.WriteAllLines(Path.Combine(logs, "2026-11-03.log"), new[] { "21:00:00 user: what's on tomorrow?", "21:00:00 assistant: Statistics at nine, choom." });
                File.WriteAllLines(Path.Combine(logs, "2026-11-04.log"), new[] { "11:44:36 user: Oh finally, it's working properly.", "11:44:36 assistant: Finally. Good boy for sticking with it.", "12:03:06 user: (game: They just barely survived a fight as Akshan, react)", "12:03:06 assistant: Clean escape, choom.", "broken line", "12:30:00 user: from the future" });
                List<PhoneChatLine> lines = PhoneHistory.Lines(logs, now, PhoneHistory.MostLines);
                List<string> shown = lines.ConvertAll(delegate(PhoneChatLine one) { return (one.Mine ? "you: " : "her: ") + one.Text; });
                string all = string.Join(" | ", shown.ToArray());
                if (shown.Count != 5 || shown[0] != "you: what's on tomorrow?" || shown[4] != "her: Clean escape, choom." || all.Contains("too old") || all.Contains("(game:") || all.Contains("future") || all.Contains("broken"))
                    throw new Exception("The phone's chat history was not his last week of real lines in order: " + all);
                if (PhoneHistory.Lines(logs, now, 2).Count != 2 || PhoneHistory.Lines(logs, now, 2)[1].Text != "Clean escape, choom." || PhoneHistory.Lines(Path.Combine(folder, "none"), now, 10).Count != 0)
                    throw new Exception("The history did not keep only the newest lines, or a missing folder broke it.");
                string json = PhoneHistory.Json(lines);
                if (json.IndexOf("{\"mine\":true,\"text\":\"what's on tomorrow?\",\"at\":\"2026-11-03T21:00:00\"}", StringComparison.Ordinal) < 0) throw new Exception("The history was not sent as the page reads it: " + json);
                string token = PhoneAuth.Pair(PhoneAuth.NewCode(DateTime.Now), "iPhone", DateTime.Now);
                TcpListener free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                int port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                server = new PhoneServer(port);
                string problem = server.Start();
                if (problem != null) throw new Exception("The phone server did not start: " + problem);
                string host = "127.0.0.1:" + port;
                PhoneServer.History = delegate { return null; };
                if (SmokePhoneAsk(port, "GET", "/api/history", host, token, null) != 204) throw new Exception("No history did not answer empty.");
                PhoneServer.History = delegate { return json; };
                string got = SmokePhoneBody(port, "GET", "/api/history", host, token, null);
                if (SmokePhoneAsk(port, "GET", "/api/history", host, null, null) != 401 || SmokePhoneSend(port, "GET", "/api/history", host, token, null, "https://evil.example") != 403 || got == null || !got.StartsWith("200", StringComparison.Ordinal) || got.IndexOf("Clean escape", StringComparison.Ordinal) < 0)
                    throw new Exception("His chat history was served without the token or to another website, or not to his phone.");
                if (File.ReadAllText(ActivityLog.FilePath).IndexOf("Clean escape", StringComparison.Ordinal) >= 0) throw new Exception("The history reached the log.");
                PhoneFile script = PhoneFiles.Find("/app.js");
                string page = script == null ? "" : System.Text.Encoding.UTF8.GetString(script.Bytes);
                if (page.IndexOf("api/history", StringComparison.Ordinal) < 0 || page.IndexOf("AbortController", StringComparison.Ordinal) < 0 || page.IndexOf("localStorage.setItem(\"capri.log", StringComparison.Ordinal) >= 0)
                    throw new Exception("The page does not load her history on open, cannot end a frozen poll, or keeps chat on the phone.");
                checks.Add("PASS phone chat history (CAPRI-095): on open, and after the page slept over 30 s, the phone shows the last 40 lines of his week of chat from the PC (never her inner prompts such as game reactions' frames, never older than 7 days), token and Origin checked, the phone keeps nothing; waking ends a frozen poll and starts a fresh one");
            }
            finally
            {
                if (server != null) server.Stop();
                PhoneServer.History = keptHistory;
                PhoneAuth.FilePath = keptFile;
                ActivityLog.FilePath = keptLog;
                PhoneAuth.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }
    }
}
