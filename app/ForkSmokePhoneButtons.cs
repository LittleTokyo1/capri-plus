using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokePhoneButtons(List<string> checks)
        {
            string keptFile = PhoneAuth.FilePath, keptLog = ActivityLog.FilePath;
            Func<string, string> keptKey = PhoneServer.Key;
            Func<string> keptSettings = PhoneServer.Settings;
            Func<string, string, string> keptSetting = PhoneServer.Setting;
            string folder = Path.Combine(Path.GetTempPath(), "capri-phone-buttons-smoke-" + Guid.NewGuid().ToString("N"));
            PhoneServer server = null;
            try
            {
                Directory.CreateDirectory(folder);
                PhoneAuth.FilePath = Path.Combine(folder, "phones.dat");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                PhoneAuth.Reset();
                Tunable plain = new Tunable { Key = "glances", Words = "how often she glances", Toggle = true, Get = delegate { return 1; } };
                Tunable locked = new Tunable { Key = "lockout", Words = "the game lockout", Toggle = true, Get = delegate { return 1; }, Locked = "never by voice" };
                Tunable listed = new Tunable { Key = "friends", Words = "auto-reply friends", Listed = true, GetList = delegate { return new string[0]; } };
                Tunable inner = new Tunable { Key = "paused", Words = "paused", Toggle = true, Field = "Paused", Get = delegate { return 0; } };
                if (!PhoneButtons.Shown(plain) || PhoneButtons.Shown(locked) || PhoneButtons.Shown(listed) || PhoneButtons.Shown(inner) || PhoneButtons.Shown(null))
                    throw new Exception("A locked, internal or list setting was offered on the phone, or a plain one was not.");
                string json = PhoneButtons.Json(new List<Tunable> { plain, locked, listed, inner });
                if (json != "{\"settings\":[{\"key\":\"glances\",\"words\":\"how often she glances\",\"now\":\"on\",\"choices\":[\"on\",\"off\"]}]}") throw new Exception("The phone's settings list was built as: " + json);
                Tunable real = Tuning.All.Find(delegate(Tunable one) { return one.Locked != null && one.Toggle && one.Get != null; });
                if (real == null) throw new Exception("No locked setting to test the phone against.");
                int before = real.Get();
                string answer = PhoneButtons.Change(real.Key, before != 0 ? "off" : "on", DateTime.Now);
                if (answer != PhoneButtons.NotHere || real.Get() != before || PhoneButtons.Json(Tuning.All).IndexOf("\"key\":" + PhoneServer.Json(real.Key), StringComparison.Ordinal) >= 0)
                    throw new Exception("The locked setting " + real.Key + " was listed or changed from the phone: " + answer);
                if (PhoneButtons.Change("no such setting at all", "on", DateTime.Now) != PhoneButtons.NotFound) throw new Exception("An unknown setting was not refused.");
                string token = PhoneAuth.Pair(PhoneAuth.NewCode(DateTime.Now), "iPhone", DateTime.Now);
                TcpListener free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                int port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                server = new PhoneServer(port);
                string problem = server.Start();
                if (problem != null) throw new Exception("The phone server did not start: " + problem);
                string host = "127.0.0.1:" + port;
                List<string> pressed = new List<string>();
                PhoneServer.Key = delegate(string key) { pressed.Add(key); return "Stopping what I'm doing."; };
                PhoneServer.Settings = delegate { return json; };
                PhoneServer.Setting = delegate(string key, string value) { return key + " to " + value; };
                if (SmokePhoneAsk(port, "POST", "/api/key", host, null, "key=stop") != 401 || SmokePhoneSend(port, "POST", "/api/key", host, token, "key=stop", "https://evil.example") != 403 || SmokePhoneAsk(port, "POST", "/api/key", host, token, "key=use_computer") != 400 || pressed.Count != 0)
                    throw new Exception("A phone key was pressed without the token, from another website, or one that isn't a key.");
                string stop = SmokePhoneBody(port, "POST", "/api/key", host, token, "key=stop");
                string listedNow = SmokePhoneBody(port, "GET", "/api/settings", host, token, null);
                string set = SmokePhoneBody(port, "POST", "/api/setting", host, token, "key=glances&value=off");
                if (stop == null || stop.IndexOf("Stopping", StringComparison.Ordinal) < 0 || pressed.Count != 1 || pressed[0] != "stop" || listedNow == null || listedNow.IndexOf("glances", StringComparison.Ordinal) < 0 || set == null || set.IndexOf("glances to off", StringComparison.Ordinal) < 0 || SmokePhoneAsk(port, "POST", "/api/setting", host, token, "key=glances") != 400)
                    throw new Exception("A phone key or quick setting did not reach her.");
                checks.Add("PASS phone job keys and quick settings (CAPRI-095 phase 4): only STOP JOB, PAUSE, WAKE and LOOK NOW exist as keys (any other name is refused), token and Origin checked; quick settings list and change only plain toggles and choices through Tuning.Find and Tuning.Change, never a locked, internal or list setting (the real locked one stays as it was)");
            }
            finally
            {
                if (server != null) server.Stop();
                PhoneServer.Key = keptKey;
                PhoneServer.Settings = keptSettings;
                PhoneServer.Setting = keptSetting;
                PhoneAuth.FilePath = keptFile;
                ActivityLog.FilePath = keptLog;
                PhoneAuth.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private void SmokePhoneKeys(List<string> checks)
        {
            ForkSettings kept = ForkSettings.Current;
            Func<string, bool> keptRunning = Lockout.Running;
            try
            {
                ForkSettings.Current = new ForkSettings();
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                if (!Lockout.Active) throw new Exception("The faked game did not turn the lockout on.");
                stopAsked = false;
                string stopped = PhoneKey("stop");
                if (!stopAsked || stopped == null) throw new Exception("STOP from the phone did not stop her mid-match.");
                if (PhoneKey("pause") == null || !ForkSettings.Current.Paused || PhoneKey("pause") != "I'm already dark.") throw new Exception("PAUSE from the phone did not make her go dark, or did it twice.");
                if (PhoneKey("look") != PhoneView.Paused) throw new Exception("LOOK NOW from the phone looked while she was paused.");
                if (PhoneKey("wake") != "Back online." || ForkSettings.Current.Paused) throw new Exception("WAKE from the phone did not bring her back.");
                if (PhoneKey("use_computer") != null || PhoneKey(null) != null) throw new Exception("A phone key that isn't one of hers did something.");
                if (ForkTool("use_computer", new ToolArguments { Task = "open the shop and buy gold", Asked = "open the shop" }) != Lockout.Refusal) throw new Exception("A hands job was not refused mid-match.");
                checks.Add("PASS phone keys mid-match: STOP and PAUSE work during a game, LOOK NOW refuses while she is paused and otherwise asks her own check-my-screen turn (every tool guard inside), WAKE brings her back, and hands jobs stay refused in code mid-match");
            }
            finally
            {
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                ForkSettings.Current = kept;
                stopAsked = false;
                CloseTalk();
            }
        }
    }
}
