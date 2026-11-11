using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed class SceneSpec
    {
        public string Name;
        public string Called;
        public string[] Apps;
        public string FirstMonitor;
        public bool Code;
        public bool Sites;
        public bool Kali;
        public bool Model;
        public string[] Moods;
        public int Volume;
    }

    internal static class Scene
    {
        internal const string Work = "work", Relax = "relax", Pentest = "pentest";
        internal const string CodeWindow = "Claude";
        private const int SettleMilliseconds = 5000;
        private const int ModelSeconds = 180;
        private const string OllamaAt = "http://127.0.0.1:11434";

        internal static readonly SceneSpec[] All =
        {
            new SceneSpec { Name = Work, Called = "work scene", Apps = new[] { "Spotify", "Google Chrome" }, FirstMonitor = "Google Chrome", Code = true, Sites = true, Moods = new[] { "focus", "study", "lofi", "lo-fi", "concentrat", "deep work", "chill" }, Volume = 30 },
            new SceneSpec { Name = Relax, Called = "home scene", Apps = new[] { "Spotify", "Discord", "Stremio", "Opera GX", "League of Legends" }, FirstMonitor = "Opera GX", Moods = new[] { "chill", "vibe", "relax", "favou", "favorite", "mix" }, Volume = 50 },
            new SceneSpec { Name = Pentest, Called = "pentest scene", Apps = new[] { "Spotify", "Ollama" }, FirstMonitor = "VirtualBox", Code = true, Kali = true, Model = true, Moods = new[] { "phonk", "hack", "cyber", "synth", "dark", "night", "techno" }, Volume = 35 }
        };

        internal static Func<string, ToolArguments, string> Do = delegate(string tool, ToolArguments args) { return Hands.Do(tool, args); };
        internal static Func<string> StartVm = Vm;
        internal static Func<string> WarmModel = Model;
        internal static Func<string, string> StartCode = Code;
        internal static Func<SceneSpec, string> Music = Play;
        internal static Func<string> CodeScript = delegate
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "capri-automation", "start-capri.ps1");
            return File.Exists(path) ? path : null;
        };
        internal static Action<int> Settle = delegate(int milliseconds) { Thread.Sleep(milliseconds); };

        internal static string[] Sites(string[] work, string[] school)
        {
            if (work != null && work.Length > 0) return work;
            return school ?? new string[0];
        }

        internal static SceneSpec Find(string name)
        {
            foreach (SceneSpec spec in All) if (spec.Name == name) return spec;
            return null;
        }

        internal static List<string> Plan(SceneSpec spec, string code, string[] sites)
        {
            List<string> steps = new List<string>();
            steps.Add("minimize");
            foreach (string app in spec.Apps) steps.Add("open:" + app);
            if (spec.Sites && sites != null) foreach (string site in sites) if (!string.IsNullOrWhiteSpace(site)) steps.Add("site:" + site.Trim());
            if (spec.Kali) steps.Add("vm");
            if (spec.Model) steps.Add("model");
            if (spec.Code && code != null) steps.Add("code");
            steps.Add("monitor1:" + spec.FirstMonitor);
            if (spec.Code && code != null) steps.Add("monitor2:" + CodeWindow);
            steps.Add("music");
            return steps;
        }

        internal static List<string> Run(SceneSpec spec, bool game, string[] sites)
        {
            List<string> done = new List<string>();
            if (game)
            {
                done.Add("refused: a game is running");
                return done;
            }
            Func<string> script = CodeScript;
            string code = script == null ? null : script();
            bool settled = false;
            foreach (string step in Plan(spec, code, sites))
            {
                if (!settled && (step.StartsWith("monitor", StringComparison.Ordinal) || step == "music"))
                {
                    Settle(SettleMilliseconds);
                    settled = true;
                }
                done.Add(step + " -> " + (Step(step, spec, code) ?? ""));
            }
            ActivityLog.Write("scene", spec.Name, string.Join(" | ", done.ToArray()));
            return done;
        }

        private static string Step(string step, SceneSpec spec, string code)
        {
            try
            {
                if (step == "minimize") return Minimize() + " minimized";
                if (step.StartsWith("open:", StringComparison.Ordinal)) return Do("open_app", new ToolArguments { Name = step.Substring(5) });
                if (step.StartsWith("site:", StringComparison.Ordinal)) return Do("open_website", new ToolArguments { Site = step.Substring(5), Browser = "chrome" });
                if (step == "vm") return StartVm();
                if (step == "model") return WarmModel();
                if (step == "code") return StartCode(code);
                if (step.StartsWith("monitor1:", StringComparison.Ordinal)) return Do("app_window", new ToolArguments { Name = step.Substring(9), Action = "move_to_monitor", Monitor = "1" });
                if (step.StartsWith("monitor2:", StringComparison.Ordinal)) return Do("app_window", new ToolArguments { Name = step.Substring(9), Action = "move_to_monitor", Monitor = "2" });
                if (step == "music") return Music(spec);
            }
            catch (Exception error) { return error.GetType().Name + ": " + error.Message; }
            return null;
        }

        internal static string Playlist(SceneSpec spec, IList<string> names)
        {
            if (names == null || names.Count == 0) return null;
            foreach (string mood in spec.Moods)
                foreach (string name in names)
                    if (name != null && name.ToLowerInvariant().Contains(mood)) return name;
            return names[0];
        }

        private static string Play(SceneSpec spec)
        {
            string pick = Playlist(spec, SpotifyLibrary.PlaylistNames());
            string played = SpotifyLibrary.Play(pick ?? "", "playlist");
            if (!played.StartsWith("Playing", StringComparison.Ordinal))
            {
                Settle(SettleMilliseconds);
                played = SpotifyLibrary.Play(pick ?? "", "playlist");
            }
            return played + (SpotifyLibrary.Volume(spec.Volume) ? " Volume " + spec.Volume + "%." : " Volume not set.");
        }

        private static string Vm()
        {
            string manage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Oracle", "VirtualBox", "VBoxManage.exe");
            if (!File.Exists(manage)) return "no VirtualBox";
            Match found = Regex.Match(Quiet(manage, "list vms", 20000) ?? "", "\"([^\"]*kali[^\"]*)\"", RegexOptions.IgnoreCase);
            if (!found.Success) return "no Kali VM";
            if ((Quiet(manage, "list runningvms", 20000) ?? "").IndexOf("\"" + found.Groups[1].Value + "\"", StringComparison.Ordinal) >= 0) return found.Groups[1].Value + " already running";
            string started = Quiet(manage, "startvm \"" + found.Groups[1].Value + "\" --type gui", 60000);
            return started == null ? "VM start failed" : found.Groups[1].Value + " started";
        }

        private static string Model()
        {
            string tags = Fetch("GET", "/api/tags", null, 10);
            if (tags == null)
            {
                Settle(SettleMilliseconds);
                tags = Fetch("GET", "/api/tags", null, 10);
            }
            if (tags == null) return "Ollama not answering";
            Match found = Regex.Match(tags, "\"name\"\\s*:\\s*\"([^\"]*kali[^\"]*)\"", RegexOptions.IgnoreCase);
            if (!found.Success) return "no Kali model";
            string loaded = Fetch("POST", "/api/generate", "{\"model\":\"" + found.Groups[1].Value + "\",\"keep_alive\":\"30m\"}", ModelSeconds);
            return loaded == null ? found.Groups[1].Value + " did not load" : found.Groups[1].Value + " loaded";
        }

        private static string Code(string script)
        {
            if (script == null) return "no Claude Code script";
            Process.Start(new ProcessStartInfo("powershell.exe", "-NoExit -ExecutionPolicy Bypass -File \"" + script + "\"") { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(script) });
            return "Claude Code session started";
        }

        private static string Quiet(string exe, string arguments, int wait)
        {
            ProcessStartInfo start = new ProcessStartInfo(exe, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (Process run = Process.Start(start))
            {
                string output = run.StandardOutput.ReadToEnd();
                if (!run.WaitForExit(wait)) return null;
                return run.ExitCode == 0 ? output : null;
            }
        }

        private static string Fetch(string method, string path, string body, int seconds)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(OllamaAt + path);
                request.Method = method;
                request.Timeout = request.ReadWriteTimeout = seconds * 1000;
                if (body != null)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    request.ContentType = "application/json";
                    using (Stream sent = request.GetRequestStream()) sent.Write(bytes, 0, bytes.Length);
                }
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream())) return reader.ReadToEnd();
            }
            catch (WebException) { return null; }
            catch (IOException) { return null; }
        }

        private delegate bool WindowFound(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowFound found, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        private const int ExStyle = -20;
        private const int ToolWindow = 0x80;
        private const uint Owner = 4;
        private const int MinimizeQuietly = 7;
        private const int RestoreQuietly = 4;

        internal static Func<List<IntPtr>> Windows = delegate
        {
            List<IntPtr> found = new List<IntPtr>();
            uint mine = (uint)Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr window, IntPtr data)
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner != mine && IsWindowVisible(window) && GetWindowTextLength(window) > 0 && GetWindow(window, Owner) == IntPtr.Zero && (GetWindowLong(window, ExStyle) & ToolWindow) == 0) found.Add(window);
                return true;
            }, IntPtr.Zero);
            return found;
        };
        internal static Func<IntPtr, bool> Minimized = IsIconic;
        internal static Func<IntPtr, int, bool> Show = ShowWindow;
        private static readonly object gate = new object();
        private static readonly List<IntPtr> touched = new List<IntPtr>();

        internal static int Minimize()
        {
            List<IntPtr> now = Windows();
            lock (gate)
            {
                touched.Clear();
                foreach (IntPtr window in now)
                {
                    if (Minimized(window)) continue;
                    Show(window, MinimizeQuietly);
                    touched.Add(window);
                }
                return touched.Count;
            }
        }

        internal static int Restore()
        {
            lock (gate)
            {
                int back = 0;
                foreach (IntPtr window in touched)
                {
                    if (!Minimized(window)) continue;
                    Show(window, RestoreQuietly);
                    back++;
                }
                touched.Clear();
                return back;
            }
        }
    }
}
