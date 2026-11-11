using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed class Chatterbox
    {
        internal enum Phase { Off, Starting, Ready, Failed }

        internal static Func<ProcessStartInfo, Process> Launch = delegate(ProcessStartInfo info) { return Process.Start(info); };
        internal static Func<DateTime> Now = delegate { return DateTime.UtcNow; };
        internal static Func<string, bool> Exists = delegate(string path) { return File.Exists(path); };
        internal static string Home = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri"), "voice");
        private const int StartSeconds = 150;
        private const int RetrySeconds = 120;
        private readonly object gate = new object();
        private Process process;
        private int port;
        private string token;
        private Phase phase;
        private DateTime used, failedAt;
        private HttpWebRequest pending;
        private bool cut;
        private int run;

        internal string Why;

        internal Phase State { get { lock (gate) return phase; } }

        internal bool Running { get { lock (gate) return phase == Phase.Starting || phase == Phase.Ready; } }

        internal static string Root()
        {
            string lab = ForkSettings.Current.ChatterboxLab;
            return string.IsNullOrWhiteSpace(lab) ? Home : lab.Trim().Trim('"');
        }

        internal static string Python() { return Path.Combine(Path.Combine(Path.Combine(Root(), "cb"), "Scripts"), "pythonw.exe"); }

        internal static string Reference() { return Path.Combine(Root(), "refv2.wav"); }

        internal static bool Present()
        {
            try { return Exists(Python()) && Exists(Reference()); }
            catch (ArgumentException) { return false; }
        }

        internal const string CutOff = "Her cloned voice was cut off.";

        internal bool Busy { get { lock (gate) return pending != null; } }

        private static string Quote(string text) { return "\"" + text + "\""; }

        private static int FreePort()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
            finally { probe.Stop(); }
        }

        internal void Warm()
        {
            lock (gate)
            {
                if (phase == Phase.Starting || phase == Phase.Ready) return;
                if (phase == Phase.Failed && (Now() - failedAt).TotalSeconds < RetrySeconds) return;
                string script;
                try
                {
                    Directory.CreateDirectory(Home);
                    script = Path.Combine(Home, "capri_tts.py");
                    if (!File.Exists(script) || File.ReadAllText(script) != Script) File.WriteAllText(script, Script, new UTF8Encoding(false));
                }
                catch (Exception error) { Fail("couldn't write her voice script: " + error.Message); return; }
                port = FreePort();
                token = Guid.NewGuid().ToString("N");
                ProcessStartInfo info = new ProcessStartInfo(Python(), Quote(script) + " --port " + port + " --parent " + Process.GetCurrentProcess().Id + " --token " + token + " --ref " + Quote(Reference()));
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WorkingDirectory = Home;
                info.EnvironmentVariables["HF_HOME"] = Path.Combine(Path.Combine(Root(), "cache"), "hf");
                info.EnvironmentVariables["TORCH_HOME"] = Path.Combine(Path.Combine(Root(), "cache"), "torch");
                info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                try { process = Launch(info); }
                catch (Exception error) { Fail("couldn't start her voice: " + error.Message); return; }
                if (process == null) { Fail("couldn't start her voice"); return; }
                Begin();
            }
        }

        internal void Attach(int onPort, string withToken)
        {
            lock (gate)
            {
                port = onPort;
                token = withToken;
                process = null;
                Begin();
            }
        }

        private void Begin()
        {
            phase = Phase.Starting;
            Why = null;
            cut = false;
            used = Now();
            int mine = ++run;
            ThreadPool.QueueUserWorkItem(delegate { Poll(mine); });
        }

        private void Poll(int mine)
        {
            DateTime began = DateTime.UtcNow;
            while (true)
            {
                lock (gate)
                {
                    if (run != mine) return;
                    if (process != null && process.HasExited) { Fail("her voice program stopped"); return; }
                }
                if ((DateTime.UtcNow - began).TotalSeconds > StartSeconds) { lock (gate) if (run == mine) Fail("her voice took too long to load"); return; }
                string body = Get("/health");
                if (body != null)
                {
                    lock (gate)
                    {
                        if (run != mine) return;
                        if (body == Forbidden) { Fail("another program answered on her voice's port"); return; }
                        string state = Field(body, "state");
                        if (state == "ready") { phase = Phase.Ready; used = Now(); return; }
                        if (state == "failed") { Fail(Field(body, "error")); return; }
                    }
                }
                Thread.Sleep(300);
            }
        }

        private const string Forbidden = "forbidden";

        private string Get(string path)
        {
            int on;
            string key;
            lock (gate) { on = port; key = token; }
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + on + path);
                request.Proxy = null;
                request.Timeout = 2000;
                request.Headers["X-Capri-Token"] = key;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException error)
            {
                HttpWebResponse bad = error.Response as HttpWebResponse;
                return bad != null && (int)bad.StatusCode == 403 ? Forbidden : null;
            }
            catch (IOException) { return null; }
        }

        private static string Field(string json, string name)
        {
            Match found = Regex.Match(json ?? "", "\"" + name + "\"\\s*:\\s*\"([^\"]*)\"");
            return found.Success ? found.Groups[1].Value : "";
        }

        private void Fail(string why)
        {
            lock (gate)
            {
                phase = Phase.Failed;
                Why = why;
                failedAt = Now();
                Kill();
            }
            Brain.Log("her cloned voice", -1, why);
        }

        private void Kill()
        {
            Process old = process;
            process = null;
            if (old == null) return;
            try { if (!old.HasExited) old.Kill(); }
            catch (Exception) { }
            try { old.Dispose(); }
            catch (Exception) { }
        }

        internal byte[] Speak(string words)
        {
            HttpWebRequest request;
            lock (gate)
            {
                if (phase != Phase.Ready) throw new VoiceException("Her cloned voice isn't ready yet.");
                request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/speak");
                request.Proxy = null;
                request.Method = "POST";
                request.Timeout = Math.Min(30000, 4000 + 60 * (words ?? "").Length);
                request.ContentType = "text/plain; charset=utf-8";
                request.ServicePoint.Expect100Continue = false;
                request.Headers["X-Capri-Token"] = token;
                pending = request;
                cut = false;
                used = Now();
            }
            byte[] body = Encoding.UTF8.GetBytes(words ?? "");
            byte[] wav;
            try
            {
                request.ContentLength = body.Length;
                using (Stream sending = request.GetRequestStream()) sending.Write(body, 0, body.Length);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (MemoryStream kept = new MemoryStream())
                {
                    response.GetResponseStream().CopyTo(kept);
                    wav = kept.ToArray();
                }
            }
            catch (Exception error)
            {
                bool wasCut;
                lock (gate) wasCut = cut;
                if (wasCut) throw new VoiceException(CutOff);
                WebException web = error as WebException;
                HttpWebResponse bad = web == null ? null : web.Response as HttpWebResponse;
                if (web != null && web.Status == WebExceptionStatus.ConnectFailure)
                {
                    Fail("her voice program stopped answering");
                    throw new VoiceException("Her cloned voice stopped answering.");
                }
                if (bad != null && (int)bad.StatusCode == 503) throw new VoiceException("Her cloned voice couldn't make that line (busy, still loading or out of GPU memory).");
                throw new VoiceException("Her cloned voice didn't answer: " + error.Message);
            }
            finally
            {
                lock (gate)
                {
                    if (pending == request) pending = null;
                    used = Now();
                }
            }
            if (wav.Length < 44 || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F') throw new VoiceException("Her cloned voice sent something that wasn't audio.");
            return wav;
        }

        internal void Abort()
        {
            lock (gate)
            {
                if (pending == null) return;
                cut = true;
                try { pending.Abort(); }
                catch (Exception) { }
            }
        }

        internal void Stop()
        {
            lock (gate)
            {
                run++;
                phase = Phase.Off;
                cut = true;
                if (pending != null)
                {
                    try { pending.Abort(); }
                    catch (Exception) { }
                }
                Kill();
            }
        }

        internal void Tick()
        {
            lock (gate)
            {
                if (phase != Phase.Ready) return;
                if (process != null && process.HasExited) { Fail("her voice program stopped"); return; }
                if ((Now() - used).TotalMinutes >= ForkSettings.Current.IdleMinutes) Stop();
            }
        }

        internal const string Script = @"import os, sys, re, io, json, time, threading
from http.server import BaseHTTPRequestHandler, HTTPServer

args = dict(zip(sys.argv[1::2], sys.argv[2::2]))
port = int(args['--port'])
parent = int(args['--parent'])
token = args['--token']
ref = args['--ref']
here = os.path.dirname(os.path.abspath(__file__))
log = open(os.path.join(here, 'server.log'), 'a', buffering=1, encoding='utf-8')
sys.stdout = sys.stdout or log
sys.stderr = sys.stderr or log
state = {'s': 'loading', 'error': ''}
holder = {}


def note(text):
    log.write(time.strftime('%H:%M:%S ') + text + '\n')


def watch():
    import ctypes
    kernel = ctypes.windll.kernel32
    handle = kernel.OpenProcess(0x00100000, False, parent)
    if not handle:
        os._exit(0)
    kernel.WaitForSingleObject(handle, 0xFFFFFFFF)
    os._exit(0)


def load():
    try:
        import torch
        free, total = torch.cuda.mem_get_info()
        if free < 3.8 * 2 ** 30:
            state['s'] = 'failed'
            state['error'] = 'low VRAM'
            return
        from chatterbox.tts_turbo import ChatterboxTurboTTS
        model = ChatterboxTurboTTS.from_pretrained(device='cuda')
        model.prepare_conditionals(ref)
        holder['model'] = model
        state['s'] = 'ready'
        note('ready')
    except Exception as error:
        state['s'] = 'failed'
        state['error'] = str(error)[:200].replace(chr(34), ' ')
        note('load failed: ' + repr(error))


def speak(text):
    import numpy as np
    import soundfile as sf
    model = holder['model']
    parts = [p.strip() for p in re.split(r'(?<=[.!?])\s+', text) if p.strip()]
    pieces = []
    for part in parts:
        wav = model.generate(part)
        pieces.append(wav.squeeze().cpu().numpy())
        pieces.append(np.zeros(int(model.sr * 0.08), dtype='float32'))
    audio = np.concatenate(pieces) if pieces else np.zeros(1, dtype='float32')
    buffer = io.BytesIO()
    sf.write(buffer, audio, model.sr, format='WAV', subtype='PCM_16')
    return buffer.getvalue()


class Handler(BaseHTTPRequestHandler):
    timeout = 15

    def log_message(self, *args):
        pass

    def allowed(self):
        return self.headers.get('X-Capri-Token') == token and self.headers.get('Host') == '127.0.0.1:' + str(port)

    def reply(self, code, body, kind):
        self.send_response(code)
        self.send_header('Content-Type', kind)
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if not self.allowed():
            self.reply(403, b'', 'text/plain')
        elif self.path == '/health':
            self.reply(200, json.dumps({'state': state['s'], 'error': state['error']}).encode('utf-8'), 'application/json')
        else:
            self.reply(404, b'', 'text/plain')

    def do_POST(self):
        if self.path != '/speak' or not self.allowed():
            self.reply(403, b'', 'text/plain')
            return
        size = int(self.headers.get('Content-Length', '0'))
        text = self.rfile.read(size).decode('utf-8', 'replace')
        if state['s'] != 'ready' or not text.strip():
            self.reply(503, b'', 'text/plain')
            return
        try:
            self.reply(200, speak(text), 'audio/wav')
        except Exception as error:
            note('speak failed: ' + repr(error))
            try:
                import torch
                torch.cuda.empty_cache()
            except Exception:
                pass
            self.reply(503, b'', 'text/plain')


threading.Thread(target=watch, daemon=True).start()
threading.Thread(target=load, daemon=True).start()
HTTPServer(('127.0.0.1', port), Handler).serve_forever()
";
    }
}
