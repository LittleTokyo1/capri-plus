using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static byte[] SmokeWav()
        {
            byte[] wav = new byte[300];
            wav[0] = (byte)'R';
            wav[1] = (byte)'I';
            wav[2] = (byte)'F';
            wav[3] = (byte)'F';
            return wav;
        }

        private static TcpListener FakeVoice(string token, string state, List<string> seen, out int port)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            ThreadPool.QueueUserWorkItem(delegate
            {
                while (true)
                {
                    TcpClient client;
                    try { client = listener.AcceptTcpClient(); }
                    catch (Exception) { return; }
                    ThreadPool.QueueUserWorkItem(delegate { FakeVoiceReply(client, token, state, seen); });
                }
            });
            return listener;
        }

        private static void FakeVoiceReply(TcpClient client, string token, string state, List<string> seen)
        {
            try
            {
                using (client)
                {
                    NetworkStream stream = client.GetStream();
                    StringBuilder head = new StringBuilder();
                    byte[] one = new byte[1];
                    while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && stream.Read(one, 0, 1) == 1) head.Append((char)one[0]);
                    string text = head.ToString();
                    Match length = Regex.Match(text, "Content-Length: (\\d+)", RegexOptions.IgnoreCase);
                    byte[] body = new byte[length.Success ? int.Parse(length.Groups[1].Value) : 0];
                    int got = 0;
                    while (got < body.Length)
                    {
                        int more = stream.Read(body, got, body.Length - got);
                        if (more <= 0) break;
                        got += more;
                    }
                    string[] line = text.Substring(0, text.IndexOf("\r\n", StringComparison.Ordinal)).Split(' ');
                    string said = Encoding.UTF8.GetString(body);
                    lock (seen) seen.Add(line[0] + " " + line[1] + (said.Length > 0 ? " " + said : ""));
                    byte[] reply;
                    string kind = "application/json";
                    int code = 200;
                    if (!Regex.IsMatch(text, "X-Capri-Token: " + token, RegexOptions.IgnoreCase)) { code = 403; reply = new byte[0]; }
                    else if (line[1] == "/health") reply = Encoding.UTF8.GetBytes("{\"state\":\"" + state + "\",\"error\":\"\"}");
                    else if (said == "fail") { code = 503; reply = new byte[0]; }
                    else if (said == "junk") reply = Encoding.UTF8.GetBytes("not audio, only words that go on long enough to pass a length check");
                    else
                    {
                        if (said == "slow") Thread.Sleep(4000);
                        reply = SmokeWav();
                        kind = "audio/wav";
                    }
                    byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " X\r\nContent-Type: " + kind + "\r\nContent-Length: " + reply.Length + "\r\nConnection: close\r\n\r\n");
                    stream.Write(header, 0, header.Length);
                    stream.Write(reply, 0, reply.Length);
                }
            }
            catch (Exception) { }
        }

        private static Chatterbox ReadyVoice(int port, string token)
        {
            Chatterbox cb = new Chatterbox();
            cb.Attach(port, token);
            PumpUntil(delegate { return cb.State == Chatterbox.Phase.Ready; }, 5000);
            if (cb.State != Chatterbox.Phase.Ready) throw new Exception("Her cloned voice never became ready against the fake server: " + cb.State + " " + cb.Why);
            return cb;
        }

        private static bool IsWav(byte[] wav) { return wav != null && wav.Length > 4 && wav[0] == 'R' && wav[1] == 'I' && wav[2] == 'F' && wav[3] == 'F'; }

        private void SmokeChatterbox(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptHome = Chatterbox.Home;
            Func<ProcessStartInfo, Process> keptLaunch = Chatterbox.Launch;
            Func<DateTime> keptNow = Chatterbox.Now;
            Func<string, bool> keptExists = Chatterbox.Exists;
            Func<string, bool> keptRunning = Lockout.Running;
            Chatterbox keptVoice = chatterbox;
            bool keptBroke = voiceBroke;
            int keptEvery = intenseEvery;
            bool? keptGaming = Native.FakedGaming;
            string home = Path.Combine(Path.GetTempPath(), "capri-cb-smoke-" + Guid.NewGuid().ToString("N"));
            List<TcpListener> fakes = new List<TcpListener>();
            List<Process> sleepers = new List<Process>();
            try
            {
                smokeVoice = true;
                intenseEvery = 0;
                Native.FakedGaming = false;
                Chatterbox.Home = home;
                Chatterbox.Exists = delegate { return true; };
                Chatterbox.Launch = delegate(ProcessStartInfo info)
                {
                    Process sleeper = Process.Start(new ProcessStartInfo("ping", "-n 30 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true });
                    sleepers.Add(sleeper);
                    return sleeper;
                };
                SmokeChatterboxSettings(checks);
                SmokeChatterboxServer(checks, fakes);
                SmokeChatterboxLaunch(checks, sleepers);
                SmokeChatterboxPick(checks, fakes);
                SmokeChatterboxIdle(checks, fakes);
                SmokeChatterboxRest(checks, fakes, keptEvery);
                SmokeChatterboxMainframe(checks);
                checks.Add("LIMIT her cloned voice: the real Python server, the GPU and how she sounds were not run here; Kasper must verify by ear");
            }
            finally
            {
                chatterbox.Stop();
                chatterbox = keptVoice;
                voiceBroke = keptBroke;
                smokeVoice = false;
                intenseEvery = keptEvery;
                Native.FakedGaming = keptGaming;
                Chatterbox.Home = keptHome;
                Chatterbox.Launch = keptLaunch;
                Chatterbox.Now = keptNow;
                Chatterbox.Exists = keptExists;
                Lockout.Running = keptRunning;
                ForkSettings.Current = keptSettings;
                foreach (TcpListener fake in fakes) fake.Stop();
                foreach (Process sleeper in sleepers)
                {
                    try { if (!sleeper.HasExited) sleeper.Kill(); }
                    catch (Exception) { }
                }
                try { Directory.Delete(home, true); }
                catch (Exception) { }
            }
        }

        private void SmokeChatterboxMainframe(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            bool keptQuiet = preferences.Quiet, keptServer = preferences.ServerVoice, keptSilent = preferences.Silent;
            string keptCloud = preferences.CloudVoice;
            Func<string, bool> keptRunning = Lockout.Running;
            string[] keptHeavy = ForkSettings.Current.IntenseGames;
            try
            {
                preferences.Silent = false;
                preferences.ServerVoice = false;
                preferences.Quiet = false;
                preferences.CloudVoice = ElevenLabs;
                ShowVoice(false);
                string mine = Mainframe.MineName + " | Fallback: ";
                string start = mainframe.MineStatus;
                if (!start.StartsWith(mine + ElevenLabs, StringComparison.Ordinal) || !start.EndsWith(" | -", StringComparison.Ordinal) || !mainframe.MineAbove) throw new Exception("Her cloned voice was not its own row, alone and picked, with ElevenLabs as the fallback: " + start + ", above " + mainframe.MineAbove);
                if (mainframe.PickFree(ClonedVoiceName)) throw new Exception("PREBUILT LOCAL's list still offers her cloned voice.");
                int line = asking;
                if (!mainframe.PickFallback(false) || ForkSettings.Current.VoiceEngine != 0 || !preferences.Quiet || !mainframe.MineStatus.StartsWith(mine + "her free voice", StringComparison.Ordinal)) throw new Exception("The Mainframe fallback did not switch to her free voice and keep her cloned voice: " + mainframe.MineStatus);
                if (!mainframe.PickFallback(true) || preferences.Quiet || !mainframe.MineStatus.StartsWith(mine + ElevenLabs, StringComparison.Ordinal)) throw new Exception("The Mainframe fallback did not go back to ElevenLabs: " + mainframe.MineStatus);
                using (ForkSettingsForm form = new ForkSettingsForm(ForkSettings.Current))
                {
                    forkForm = form;
                    mainframe.PickVoiceRow(1);
                    string prebuilt = mainframe.MineStatus;
                    if (ForkSettings.Current.VoiceEngine != 2 || !prebuilt.StartsWith("PREBUILT LOCAL | -", StringComparison.Ordinal) || !form.VoiceShown.StartsWith(ForkSettingsForm.VoiceEngineNames[2], StringComparison.Ordinal)) throw new Exception("PREBUILT LOCAL in the Mainframe did not move her engine or the open Capri+ window: " + prebuilt + " / " + form.VoiceShown);
                    mainframe.PickMine();
                    if (ForkSettings.Current.VoiceEngine != 0 || !mainframe.MineStatus.StartsWith(mine, StringComparison.Ordinal) || !form.VoiceShown.StartsWith(ForkSettingsForm.VoiceEngineNames[0], StringComparison.Ordinal)) throw new Exception(Mainframe.MineName + " in the Mainframe did not move her engine or the open Capri+ window: " + mainframe.MineStatus + " / " + form.VoiceShown);
                    forkForm = null;
                    mainframe.PickVoiceRow(1);
                    form.Apply();
                    ForkVoiceSaved(form);
                    if (ForkSettings.Current.VoiceEngine != 2) throw new Exception("A Capri+ window opened before a Mainframe pick saved her old voice back over it.");
                    mainframe.PickMine();
                }
                using (ForkSettingsForm form = new ForkSettingsForm(ForkSettings.Current))
                {
                    forkForm = form;
                    form.ChooseVoice(1, 0);
                    ShowVoice(false);
                    forkForm = null;
                    if (!form.VoiceShown.StartsWith(ForkSettingsForm.VoiceEngineNames[1], StringComparison.Ordinal)) throw new Exception("A voice refresh threw away his unsaved Capri+ voice pick: " + form.VoiceShown);
                }
                string[] saved = new string[3];
                for (int engine = 1; engine >= 0; engine--)
                    for (int fallback = engine == 0 ? 1 : 0; fallback >= 0; fallback--)
                    {
                        ForkSettings.Current.Fallback = FallbackEleven ? 0 : 1;
                        using (ForkSettingsForm form = new ForkSettingsForm(ForkSettings.Current))
                        {
                            form.ChooseVoice(engine, fallback);
                            form.Apply();
                            ApplyForkSettings();
                            ForkVoiceSaved(form);
                        }
                        if (engine == 1) saved[1] = mainframe.MineStatus + (preferences.Quiet ? " quiet" : "");
                        else saved[fallback == 1 ? 2 : 0] = mainframe.MineStatus;
                    }
                ForkSettings.Current.Fallback = 0;
                using (ForkSettingsForm form = new ForkSettingsForm(ForkSettings.Current))
                {
                    form.ChooseVoice(2, 0);
                    form.Apply();
                    ForkVoiceSaved(form);
                }
                string free = mainframe.MineStatus;
                if (!saved[1].StartsWith("CLOUD VOICE | -", StringComparison.Ordinal) || saved[1].EndsWith(" quiet", StringComparison.Ordinal) || !saved[2].StartsWith(mine + "her free voice", StringComparison.Ordinal) || !saved[0].StartsWith(mine + ElevenLabs, StringComparison.Ordinal) || !free.StartsWith("PREBUILT LOCAL | -", StringComparison.Ordinal) || !preferences.Quiet)
                    throw new Exception("A Capri+ save did not move the Mainframe: ElevenLabs " + saved[1] + ", cloned with free fallback " + saved[2] + ", cloned with ElevenLabs " + saved[0] + ", free voice " + free);
                if (asking != line || SettingUpVoice) throw new Exception("A voice save said a voice-on line or started a download.");
                preferences.Silent = true;
                mainframe.PickMine();
                if (preferences.Silent || ForkSettings.Current.VoiceEngine != 0 || !mainframe.MineStatus.StartsWith(mine, StringComparison.Ordinal)) throw new Exception(Mainframe.MineName + " did not turn her voice back on from OFF: " + mainframe.MineStatus);
                ForkSettings.Current.IntenseGames = new[] { "smokeheavy" };
                Lockout.Running = delegate(string name) { return name == "smokeheavy"; };
                ShowVoice(false);
                if (ForkSettings.Current.VoiceEngine != 0 || !mainframe.MineStatus.StartsWith(mine, StringComparison.Ordinal)) throw new Exception("A heavy game changed her saved voice or the Mainframe row: " + mainframe.MineStatus);
            }
            finally
            {
                forkForm = null;
                Lockout.Running = keptRunning;
                ForkSettings.Current.IntenseGames = keptHeavy;
                preferences.Quiet = keptQuiet;
                preferences.ServerVoice = keptServer;
                preferences.Silent = keptSilent;
                preferences.CloudVoice = keptCloud;
                ForkSettings.Current.VoiceEngine = 0;
                ShowVoice(false);
            }
            checks.Add("PASS one voice choice: " + Mainframe.MineName + " is its own Mainframe row above OFF, picked by default with ElevenLabs as its fallback (or her free voice), PREBUILT LOCAL no longer lists it; every Mainframe pick moves the open Capri+ window, every Capri+ save (her voice and the fallback) moves the Mainframe, an older Capri+ window never saves its stale voice back, no save speaks or downloads, picking it from OFF turns her voice on, and a heavy game changes neither");
        }

        private void SmokeChatterboxSettings(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.VoiceEngine != 0 || fresh.IntenseVoice != 1 || fresh.IdleMinutes != 10 || fresh.IntenseGames.Length != 0 || fresh.ChatterboxLab != null) throw new Exception("The voice settings started wrong.");
            fresh.VoiceEngine = 9;
            fresh.IntenseVoice = -3;
            fresh.IdleMinutes = 0;
            fresh.Tidy();
            if (fresh.VoiceEngine != 2 || fresh.IntenseVoice != 0 || fresh.IdleMinutes != 1) throw new Exception("The voice settings were not clamped.");
            fresh.IdleMinutes = 900;
            fresh.Tidy();
            if (fresh.IdleMinutes != 120) throw new Exception("Idle minutes had no upper limit.");
            ForkSettings picked = new ForkSettings();
            picked.VoiceEngine = 1;
            picked.IntenseVoice = 2;
            picked.IdleMinutes = 33;
            picked.IntenseGames = new[] { "gameone", "gametwo" };
            picked.ChatterboxLab = "C:\\smoke\\lab";
            using (ForkSettingsForm form = new ForkSettingsForm(picked)) form.Apply();
            if (picked.VoiceEngine != 1 || picked.IntenseVoice != 2 || picked.IdleMinutes != 33 || picked.IntenseGames.Length != 2 || picked.IntenseGames[1] != "gametwo" || picked.ChatterboxLab != "C:\\smoke\\lab") throw new Exception("The settings window lost her voice settings.");
            checks.Add("PASS cloned voice settings: her cloned voice is the default engine, the intense-game voice defaults to lighter, idle unload to 10 minutes and the game list to empty, all clamp to their ranges, and the settings window shows and saves each one");
        }

        private void SmokeChatterboxServer(List<string> checks, List<TcpListener> fakes)
        {
            int port;
            List<string> seen = new List<string>();
            TcpListener first = FakeVoice("tok1", "ready", seen, out port);
            fakes.Add(first);
            Chatterbox cb = ReadyVoice(port, "tok1");
            byte[] wav = cb.Speak("hello there");
            lock (seen) if (!IsWav(wav) || !seen.Contains("POST /speak hello there") || !seen.Contains("GET /health")) throw new Exception("The server round trip was wrong: " + string.Join(" | ", seen.ToArray()));
            try { cb.Speak("junk"); throw new Exception("A reply that wasn't audio was accepted."); }
            catch (VoiceException) { }
            try { cb.Speak("fail"); throw new Exception("A GPU out-of-memory reply was accepted."); }
            catch (VoiceException error) { if (!error.Message.Contains("GPU memory")) throw new Exception("An out-of-memory reply was explained wrong: " + error.Message); }
            Chatterbox stranger = new Chatterbox();
            stranger.Attach(port, "other");
            PumpUntil(delegate { return stranger.State == Chatterbox.Phase.Failed; }, 5000);
            if (stranger.State != Chatterbox.Phase.Failed) throw new Exception("A server with the wrong token was trusted.");
            string script = Chatterbox.Script;
            if (!script.Contains("('127.0.0.1', port)") || !script.Contains("prepare_conditionals") || !script.Contains("mem_get_info") || !script.Contains("--parent") || !script.Contains("X-Capri-Token") || !script.Contains("'127.0.0.1:' + str(port)") || !script.Contains("sys.stderr = sys.stderr or log") || !script.Contains("timeout = 15") || script.Contains("audio_prompt_path") || script.Contains("'token': token")) throw new Exception("The Python voice script is missing a safety.");
            first.Stop();
            try { cb.Speak("hello"); throw new Exception("A dead server was spoken to as if alive."); }
            catch (VoiceException) { }
            if (cb.State != Chatterbox.Phase.Failed) throw new Exception("A crashed voice server was never noticed.");
            checks.Add("PASS cloned voice server: against a fake localhost server it waits for ready with the token on every call, posts her line as UTF-8, returns RIFF audio, refuses a reply that isn't audio, explains a GPU out-of-memory reply, never trusts a server that rejects its token, notices a crashed server, and its Python script binds 127.0.0.1 only, checks the Host header, survives pythonw's missing stderr, loads her reference once, checks free VRAM and exits with Capri");
        }

        private void SmokeChatterboxLaunch(List<string> checks, List<Process> sleepers)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.ChatterboxLab = "C:\\smoke\\lab";
            ProcessStartInfo captured = null;
            Func<ProcessStartInfo, Process> inner = Chatterbox.Launch;
            Chatterbox.Launch = delegate(ProcessStartInfo info) { captured = info; return inner(info); };
            Chatterbox cb = new Chatterbox();
            cb.Warm();
            if (captured == null) throw new Exception("Warm never started her voice.");
            string arguments = captured.Arguments;
            string script = Path.Combine(Chatterbox.Home, "capri_tts.py");
            if (captured.FileName != "C:\\smoke\\lab\\cb\\Scripts\\pythonw.exe" || !captured.CreateNoWindow || captured.UseShellExecute || !arguments.Contains("--port ") || !arguments.Contains("--parent " + Process.GetCurrentProcess().Id) || !arguments.Contains("C:\\smoke\\lab\\refv2.wav") || !arguments.Contains("capri_tts.py") || captured.EnvironmentVariables["HF_HOME"] != "C:\\smoke\\lab\\cache\\hf") throw new Exception("Her voice was started wrong: " + captured.FileName + " " + arguments);
            if (!File.Exists(script) || File.ReadAllText(script) != Chatterbox.Script) throw new Exception("Her voice script was not written.");
            int id = sleepers[sleepers.Count - 1].Id;
            Func<bool> gone = delegate
            {
                try { using (Process found = Process.GetProcessById(id)) return found.HasExited; }
                catch (ArgumentException) { return true; }
            };
            cb.Stop();
            PumpUntil(gone, 5000);
            if (!gone() || cb.State != Chatterbox.Phase.Off) throw new Exception("Stopping her voice left the program running.");
            Chatterbox.Launch = inner;
            ForkSettings.Current.ChatterboxLab = "\"C:\\smoke\\lab\"";
            if (Chatterbox.Python() != "C:\\smoke\\lab\\cb\\Scripts\\pythonw.exe") throw new Exception("A quoted folder from Copy as path was not cleaned.");
            ForkSettings.Current.ChatterboxLab = "C:\\smoke<lab|";
            if (Chatterbox.Present()) throw new Exception("A folder with illegal characters counted as installed.");
            checks.Add("PASS cloned voice launch: she starts the voice program with no window, on her own port, with Capri's process id so it exits with her, her reference clip and cache from the lab folder or her voice folder, writes the script, and stopping her voice ends the program");
        }

        private void SmokeChatterboxPick(List<string> checks, List<TcpListener> fakes)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.ChatterboxLab = "C:\\smoke\\lab";
            Func<string, byte[]> paid = delegate(string words) { return new byte[] { 1 }; };
            Func<string, byte[]> light = delegate(string words) { return new byte[] { 2 }; };
            int port;
            List<string> seen = new List<string>();
            fakes.Add(FakeVoice("tok2", "ready", seen, out port));
            chatterbox = ReadyVoice(port, "tok2");
            Func<string, byte[]> speak = paid, free = light;
            ForkVoicePick(ref speak, ref free);
            if (!IsWav(speak("hello")) || speak != free) throw new Exception("With the server ready she did not speak with her cloned voice.");
            byte[] fell = speak("fail");
            if (fell == null || fell.Length != 1 || fell[0] != 1) throw new Exception("A failed cloned line did not fall back to ElevenLabs.");

            chatterbox = new Chatterbox();
            speak = paid;
            free = light;
            ForkVoicePick(ref speak, ref free);
            byte[] early = speak("hi");
            if (early.Length != 1 || early[0] != 1 || chatterbox.State != Chatterbox.Phase.Starting) throw new Exception("Before her voice was ready she went silent or never warmed it up.");
            chatterbox.Stop();

            speak = null;
            free = light;
            chatterbox = new Chatterbox();
            ForkVoicePick(ref speak, ref free);
            byte[] onlyFree = speak("hi");
            if (onlyFree.Length != 1 || onlyFree[0] != 2) throw new Exception("With no ElevenLabs the free voice did not cover for her.");
            chatterbox.Stop();

            ForkSettings.Current.VoiceEngine = 1;
            speak = paid;
            free = light;
            ForkVoicePick(ref speak, ref free);
            if (speak != paid || free != light) throw new Exception("Picking ElevenLabs still routed through the cloned voice.");
            ForkSettings.Current.VoiceEngine = 2;
            speak = paid;
            free = light;
            ForkVoicePick(ref speak, ref free);
            if (speak != light || free != light) throw new Exception("Picking the free voice still used ElevenLabs.");
            ForkSettings.Current.VoiceEngine = 0;

            ForkSettings.Current.IntenseGames = new[] { "smokegame" };
            Lockout.Running = delegate(string name) { return name == "smokegame"; };
            chatterbox = ReadyVoice(port, "tok2");
            speak = paid;
            free = light;
            ForkVoicePick(ref speak, ref free);
            byte[] lighter = speak("hi");
            if (lighter.Length != 1 || lighter[0] != 1 || chatterbox.State != Chatterbox.Phase.Off) throw new Exception("An intense game did not stop her voice and switch to ElevenLabs.");
            ForkSettings.Current.IntenseVoice = 2;
            chatterbox = ReadyVoice(port, "tok2");
            speak = paid;
            free = light;
            ForkVoicePick(ref speak, ref free);
            if (speak("hi") != null || chatterbox.State != Chatterbox.Phase.Off) throw new Exception("Text-only during an intense game still spoke.");
            ForkSettings.Current.IntenseVoice = 0;
            chatterbox = ReadyVoice(port, "tok2");
            speak = paid;
            free = light;
            ForkVoicePick(ref speak, ref free);
            if (!IsWav(speak("hi"))) throw new Exception("Keep-her-voice during an intense game still switched.");
            ForkSettings.Current.IntenseVoice = 1;
            ForkSettings.Current.IntenseGames = new string[0];
            intenseAt = 0;
            Lockout.Running = delegate(string name) { return name == "League of Legends"; };
            chatterbox = ReadyVoice(port, "tok2");
            speak = paid;
            free = light;
            ForkVoicePick(ref speak, ref free);
            if (!IsWav(speak("hi"))) throw new Exception("League alone switched her voice.");
            checks.Add("PASS cloned voice speaks first: with the server ready she speaks through it in place of ElevenLabs and the free voice, a failed line or a server still loading falls back to ElevenLabs and then the free voice (and warms it up) so she never goes silent, ElevenLabs as the engine leaves her alone, and a listed intense game stops the server and uses the lighter voice or none while League alone keeps her voice");
        }

        private void SmokeChatterboxIdle(List<string> checks, List<TcpListener> fakes)
        {
            ForkSettings.Current = new ForkSettings();
            DateTime clock = new DateTime(2026, 10, 29, 5, 0, 0, DateTimeKind.Utc);
            Chatterbox.Now = delegate { return clock; };
            int port;
            List<string> seen = new List<string>();
            fakes.Add(FakeVoice("tok3", "ready", seen, out port));
            Chatterbox cb = ReadyVoice(port, "tok3");
            clock = clock.AddMinutes(5);
            cb.Tick();
            if (cb.State != Chatterbox.Phase.Ready) throw new Exception("Her voice unloaded too soon.");
            clock = clock.AddMinutes(6);
            cb.Tick();
            if (cb.State != Chatterbox.Phase.Off) throw new Exception("Her voice never unloaded when idle.");
            Chatterbox.Now = delegate { return DateTime.UtcNow; };

            Chatterbox slow = ReadyVoice(port, "tok3");
            VoiceException cutOff = null;
            Stopwatch took = Stopwatch.StartNew();
            Thread talking = new Thread(delegate()
            {
                try { slow.Speak("slow"); }
                catch (VoiceException error) { cutOff = error; }
            });
            talking.Start();
            PumpUntil(delegate { return slow.Busy; }, 3000);
            slow.Abort();
            bool ended = talking.Join(2500);
            if (!ended || cutOff == null || cutOff.Message != Chatterbox.CutOff || took.ElapsedMilliseconds > 3500) throw new Exception("Cutting her off did not stop the pending cloned line.");

            chatterbox = ReadyVoice(port, "tok3");
            Func<string, byte[]> backup = delegate(string words) { return new byte[] { 1 }; };
            Func<string, byte[]> none = null;
            ForkVoicePick(ref backup, ref none);
            byte[] spoken = new byte[] { 9 };
            Thread speaking = new Thread(delegate() { spoken = backup("slow"); });
            speaking.Start();
            PumpUntil(delegate { return chatterbox.Busy; }, 3000);
            chatterbox.Abort();
            if (!speaking.Join(2500) || spoken != null) throw new Exception("A line cut off mid-way fell through to ElevenLabs.");
            checks.Add("PASS cloned voice idle and barge-in: after the quiet minutes she unloads the voice (and not before), and cutting her off aborts the line being made so a late wav never plays");
        }

        private void SmokeChatterboxRest(List<string> checks, List<TcpListener> fakes, int every)
        {
            if (every > 1000) throw new Exception("A heavy game is looked for only every " + every + " ms while her voice is loaded.");
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.ChatterboxLab = "C:\\smoke\\lab";
            int port;
            List<string> seen = new List<string>();
            fakes.Add(FakeVoice("tok4", "ready", seen, out port));

            chatterbox = ReadyVoice(port, "tok4");
            if (VoiceRest() != null || chatterbox.State != Chatterbox.Phase.Ready) throw new Exception("Her voice was unloaded with nothing to rest for.");
            ForkSettings.Current.VoiceEngine = 1;
            if (VoiceRest() == null || chatterbox.State != Chatterbox.Phase.Off) throw new Exception("Picking another voice left her cloned voice on the graphics card.");
            ForkSettings.Current.VoiceEngine = 0;

            bool keptSilent = preferences.Silent;
            chatterbox = ReadyVoice(port, "tok4");
            preferences.Silent = true;
            try { if (VoiceRest() == null || chatterbox.State != Chatterbox.Phase.Off) throw new Exception("Voice OFF left her cloned voice on the graphics card."); }
            finally { preferences.Silent = keptSilent; }

            Native.FakedGaming = true;
            chatterbox = ReadyVoice(port, "tok4");
            Func<string, byte[]> paid = delegate(string words) { return new byte[] { 1 }; };
            Func<string, byte[]> light = delegate(string words) { return new byte[] { 2 }; };
            Func<string, byte[]> speak = paid, free = light;
            ForkVoicePick(ref speak, ref free);
            if (VoiceRest() != null || !IsWav(speak("hi")) || chatterbox.State != Chatterbox.Phase.Ready) throw new Exception("A full-screen game that is not on her list (League) unloaded her cloned voice.");
            Native.FakedGaming = false;

            ForkSettings.Current.IntenseGames = new[] { "smokeheavy" };
            Lockout.Running = delegate(string name) { return name == "smokeheavy"; };
            string why = VoiceRest();
            if (why == null || !why.Contains("heavy game") || chatterbox.State != Chatterbox.Phase.Off) throw new Exception("A listed heavy game left her cloned voice loaded or gave no reason: " + why);
            speak = paid;
            free = light;
            ForkVoicePick(ref speak, ref free);
            byte[] said = speak("hi");
            if (said == null || said.Length != 1 || said[0] != 1 || chatterbox.State != Chatterbox.Phase.Off) throw new Exception("During a listed heavy game she loaded her cloned voice again instead of using ElevenLabs.");
            ForkSettings.Current.IntenseGames = new string[0];
            Lockout.Running = delegate(string name) { return false; };

            string[] kept = new[] { "Cyberpunk2077" };
            string[] grown = ForkSettingsForm.AddGame(kept, "C:\\Games\\Witcher 3\\bin\\witcher3.exe");
            if (kept.Length != 1 || grown.Length != 2 || grown[1] != "witcher3") throw new Exception("Adding a game file was wrong: " + string.Join(", ", grown));
            if (ForkSettingsForm.AddGame(grown, "CYBERPUNK2077.EXE").Length != 2 || ForkSettingsForm.AddGame(grown, "  ").Length != 2) throw new Exception("A game already on the list or a blank one was added again.");
            string own = Process.GetCurrentProcess().ProcessName;
            foreach (string[] item in ForkSettingsForm.Windowed())
                if (item[0].Length == 0 || string.Equals(item[0], own, StringComparison.OrdinalIgnoreCase)) throw new Exception("The running program list had a blank name or Capri herself.");
            using (ForkSettingsForm form = new ForkSettingsForm(new ForkSettings()))
                if (form.Controls.Find(ForkSettingsForm.AddRunningName, true).Length != 1 || form.Controls.Find(ForkSettingsForm.AddFileName, true).Length != 1) throw new Exception("The heavy games box has no keys to add a running program or a game file.");

            bool keptQuiet = preferences.Quiet, keptServer = preferences.ServerVoice;
            string keptCloud = preferences.CloudVoice, keptStyle = preferences.VoiceStyle;
            FreeStyle keptKokoro = kokoro.Style;
            try
            {
                chatterbox = ReadyVoice(port, "tok4");
                ChooseVoiceTier(true);
                if (ForkSettings.Current.VoiceEngine != 1 || chatterbox.State != Chatterbox.Phase.Off) throw new Exception("Picking ElevenLabs in the Mainframe kept her cloned voice: engine " + ForkSettings.Current.VoiceEngine + ", " + chatterbox.State);
                ForkSettings.Current.VoiceEngine = 0;
                chatterbox = ReadyVoice(port, "tok4");
                ChooseVoiceStyle(LocalVoice.Styles[0].Name);
                if (ForkSettings.Current.VoiceEngine != 2 || chatterbox.State != Chatterbox.Phase.Off) throw new Exception("Picking her prebuilt voice in the Mainframe kept her cloned voice: engine " + ForkSettings.Current.VoiceEngine + ", " + chatterbox.State);
            }
            finally
            {
                preferences.Quiet = keptQuiet;
                preferences.ServerVoice = keptServer;
                preferences.CloudVoice = keptCloud;
                preferences.VoiceStyle = keptStyle;
                kokoro.Style = keptKokoro;
                ForkSettings.Current.VoiceEngine = 0;
            }
            int keptBubble = preferences.BubbleTime;
            double keptUntil = voiceUntil;
            try
            {
                preferences.BubbleTime = 2;
                voiceUntil = clock.Elapsed.TotalSeconds + 3;
                if (ForkBubbleHold(300) > 1.5) throw new Exception("A spoken reply's bubble waited the whole reading time after her voice ended.");
                voiceUntil = 0;
                if (ForkBubbleHold(300) != BubbleHold(300) || BubbleHold(300) < 10) throw new Exception("A typed reply lost its reading time.");
            }
            finally
            {
                preferences.BubbleTime = keptBubble;
                voiceUntil = keptUntil;
            }
            checks.Add("PASS spoken bubble: a reply she says out loud closes about a second after her voice ends whatever the bubble time setting, a typed-only reply keeps the reading time (long setting: up to 28 s)");
            checks.Add("PASS cloned voice rests:picking ElevenLabs or a prebuilt voice in the Mainframe switches her voice engine and unloads the cloned voice at once, voice OFF or another engine unloads it on the next tick, a listed heavy game unloads it with its reason and her lines go to ElevenLabs without loading it again, a full-screen game not on the list (League) keeps it, nothing unloads it without a reason, heavy games are looked for every second, and the heavy games box adds a running program or a game file by name (no .exe, no repeats, never Capri herself)");
        }
    }
}
