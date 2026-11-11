using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace CapriDesktop
{
    internal sealed class SmokeOptions
    {
        internal string[] Only = new string[0];

        internal static SmokeOptions Parse(string[] args)
        {
            SmokeOptions options = new SmokeOptions();
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--only" && i + 1 < args.Length)
                {
                    string[] names = args[++i].Split(',');
                    for (int n = 0; n < names.Length; n++)
                    {
                        names[n] = names[n].Trim();
                        if (names[n].Length == 0) throw new ArgumentException("--only needs a comma list of section names.");
                    }
                    options.Only = names;
                }
                else throw new ArgumentException("Unknown smoke option: " + args[i]);
            }
            return options;
        }
    }

    internal static class SmokeRun
    {
        private const string WorkerVariable = "CAPRI_SMOKE_WORKER";
        internal const int StartupSeconds = 180, StepSeconds = 420, SectionSeconds = 180, ClosingSeconds = 60;
        internal static SmokeOptions Asked = new SmokeOptions();
        private static string folder, kind, current;
        private static DateTime began;
        private static int limit;
        private static readonly object progressLock = new object();
        private static Thread watched, watcher;
        private static SmokeOptions options;
        private static readonly HashSet<string> matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Stack<bool> selected = new Stack<bool>();
        private static readonly Stack<string[]> outer = new Stack<string[]>();

        internal static bool Worker { get { return Environment.GetEnvironmentVariable(WorkerVariable) == "1"; } }

        internal static void Open(string output, SmokeOptions settings)
        {
            folder = output;
            options = settings ?? new SmokeOptions();
            kind = null;
            current = null;
            matched.Clear(); selected.Clear(); outer.Clear();
        }

        internal static void Close()
        {
            folder = null;
            options = null;
            kind = null;
            current = null;
            matched.Clear(); selected.Clear(); outer.Clear();
        }

        internal sealed class Saved
        {
            internal string Folder, Kind, Current;
            internal DateTime Began;
            internal int Limit;
            internal SmokeOptions Options;
            internal string[] Matched;
            internal bool[] Selected;
            internal string[][] Outer;
        }

        internal static Saved Save()
        {
            Saved saved = new Saved();
            lock (progressLock) { saved.Folder = folder; saved.Kind = kind; saved.Current = current; saved.Began = began; saved.Limit = limit; }
            saved.Options = options;
            saved.Matched = new List<string>(matched).ToArray();
            saved.Selected = selected.ToArray();
            saved.Outer = outer.ToArray();
            return saved;
        }

        internal static void Restore(Saved saved)
        {
            lock (progressLock) { folder = saved.Folder; kind = saved.Kind; current = saved.Current; began = saved.Began; limit = saved.Limit; }
            options = saved.Options;
            matched.Clear(); selected.Clear(); outer.Clear();
            foreach (string name in saved.Matched) matched.Add(name);
            for (int i = saved.Selected.Length - 1; i >= 0; i--) selected.Push(saved.Selected[i]);
            for (int i = saved.Outer.Length - 1; i >= 0; i--) outer.Push(saved.Outer[i]);
        }

        internal static void Start(string output, SmokeOptions settings)
        {
            Open(output, settings);
            Begin("startup", "startup", StartupSeconds);
            watched = Thread.CurrentThread;
            if (watcher != null) return;
            watcher = new Thread(Watch);
            watcher.IsBackground = true;
            watcher.Start();
        }

        private static void Watch()
        {
            long noted = 0;
            while (true)
            {
                Thread.Sleep(1000);
                string phase, name, where;
                long start;
                int seconds;
                lock (progressLock) { phase = kind; name = current; start = began.Ticks; seconds = limit; where = folder; }
                if (where == null || phase != "step" && phase != "section" || start == noted || seconds < 30 || DateTime.UtcNow.Ticks - start < TimeSpan.FromSeconds(seconds - 10).Ticks) continue;
                noted = start;
                string stack = Where(watched);
                try { File.AppendAllText(Path.Combine(where, "hangs.txt"), phase + " " + name + ":" + (stack.Length == 0 ? " Stuck at: (couldn't read it)" : stack) + Environment.NewLine); }
                catch (IOException) { }
            }
        }

        internal static string Stuck(string[] active)
        {
            try
            {
                string path = Path.Combine(folder, "hangs.txt");
                if (!File.Exists(path)) return "";
                string[] lines = File.ReadAllLines(path);
                string mark = active[0] + " " + active[1] + ":";
                return lines.Length != 0 && lines[lines.Length - 1].StartsWith(mark, StringComparison.Ordinal) ? "." + lines[lines.Length - 1].Substring(mark.Length) : "";
            }
            catch (IOException) { return ""; }
        }

        internal static string Where(Thread thread)
        {
            StackTrace trace = null;
            Thread reader = new Thread(delegate()
            {
                bool paused = false;
#pragma warning disable 618
                try
                {
                    thread.Suspend();
                    paused = true;
                    trace = new StackTrace(thread, false);
                }
                catch (Exception) { }
                finally { if (paused) { try { thread.Resume(); } catch (Exception) { } } }
#pragma warning restore 618
            });
            reader.IsBackground = true;
            reader.Priority = ThreadPriority.Highest;
            reader.Start();
            if (!reader.Join(3000) || trace == null) return "";
            List<string> frames = new List<string>();
            int ours = 0;
            for (int i = 0; i < trace.FrameCount && ours < 4; i++)
            {
                MethodBase method = trace.GetFrame(i).GetMethod();
                if (method == null) continue;
                Type type = method.DeclaringType;
                bool mine = type != null && type.Namespace == typeof(SmokeRun).Namespace;
                if (!mine && i >= 2) continue;
                if (mine) ours++;
                frames.Add((type == null ? "" : type.Name + ".") + method.Name);
            }
            return frames.Count == 0 ? "" : " Stuck at: " + string.Join(" < ", frames.ToArray());
        }

        internal static bool Wants(string name)
        {
            if (options == null || options.Only.Length == 0) return true;
            bool wanted = selected.Count != 0 && selected.Peek();
            foreach (string item in options.Only)
                if (string.Equals(name, item, StringComparison.OrdinalIgnoreCase)) { matched.Add(item); wanted = true; }
            return wanted;
        }

        internal static bool EnterSection(string name)
        {
            if (!Wants(name)) return false;
            if (folder == null) return true;
            selected.Push(true);
            outer.Push(new[] { kind, current, limit.ToString(CultureInfo.InvariantCulture) });
            Begin("section", name, SectionSeconds);
            return true;
        }

        internal static void LeaveSection(string name, List<string> checks)
        {
            if (folder == null || outer.Count == 0) return;
            Flush(checks);
            selected.Pop();
            string[] back = outer.Pop();
            if (back[0] == "section" || back[0] == "step")
            {
                kind = null;
                Begin(back[0], back[1], int.Parse(back[2], CultureInfo.InvariantCulture));
            }
            else Begin("startup", "between sections", StartupSeconds);
        }

        internal static void ValidateSelection(List<string> checks)
        {
            if (options == null) return;
            foreach (string item in options.Only)
                if (!matched.Contains(item)) checks.Add("FAIL --only: no section matches " + item);
            if (folder != null) Flush(checks);
        }

        internal static void Begin(string phase, string name, int seconds)
        {
            if (folder == null || kind == phase && current == name) return;
            lock (progressLock) { kind = phase; current = name; began = DateTime.UtcNow; limit = seconds; }
            AtomicLines(Path.Combine(folder, "progress.txt"), new[] {
                kind + " " + current,
                "started " + began.ToString("O", CultureInfo.InvariantCulture),
                "limit " + limit + " s",
                kind + "\t" + current + "\t" + began.Ticks + "\t" + limit,
                Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)
            });
        }

        internal static void Step(string name)
        {
            Begin(name == "startup" ? "startup" : "step", name, name == "startup" ? StartupSeconds : StepSeconds);
        }

        internal static void Flush(List<string> checks)
        {
            if (folder != null) AtomicLines(Path.Combine(folder, "smoke-test.txt"), checks.ToArray());
        }

        internal static void Complete(List<string> checks)
        {
            Flush(checks);
            Begin("done", "closing", ClosingSeconds);
        }

        private static void AtomicLines(string path, string[] lines)
        {
            string temporary = path + ".new";
            File.WriteAllLines(temporary, lines);
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    return;
                }
                catch (IOException) { if (attempt == 9) throw; Thread.Sleep(10); }
            }
        }

        private static List<string> Checks()
        {
            string path = Path.Combine(folder, "smoke-test.txt");
            return File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
        }

        internal static string Outcome(string[] active, bool timedOut, int seconds, int exitCode, string stuck)
        {
            string reason = timedOut ? "timed out after " + seconds + " s" : "worker exited " + exitCode;
            if (active != null && active[0] == "done") return timedOut ? "FAIL smoke shutdown: " + reason : null;
            if (active == null || active[0] == "startup") return "FAIL smoke startup: " + reason;
            if (timedOut) reason += stuck;
            if (active[0] == "section") return "FAIL section " + active[1] + ": " + reason;
            if (active[0] == "step") return "FAIL step " + active[1] + ": " + reason;
            return "FAIL smoke: " + reason;
        }

        internal static string Totals(List<string> checks)
        {
            return "TOTAL PASS " + Count(checks, "PASS ") + " / LIMIT " + Count(checks, "LIMIT ") + " / FAIL " + Count(checks, "FAIL ") + " / SKIP " + Count(checks, "SKIP ");
        }

        private static int Count(List<string> checks, string start)
        {
            int count = 0;
            foreach (string line in checks) if (line.StartsWith(start, StringComparison.Ordinal)) count++;
            return count;
        }

        private static bool IsFailure(string line) { return line.StartsWith("FAIL ", StringComparison.Ordinal); }

        internal static int Run(string[] args)
        {
            string output = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(output);
            string identity = WindowsIdentity.GetCurrent().User.Value;
            using (Mutex mutex = new Mutex(false, "Local\\Capri.Desktop." + identity + ".Test"))
            {
                bool held;
                try { held = mutex.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
                if (!held)
                {
                    File.WriteAllText(Path.Combine(output, "failure.txt"), "Another smoke test holds Capri's test lock. This run did not start.");
                    return 1;
                }
                try { return RunLocked(args, output); }
                finally { mutex.ReleaseMutex(); }
            }
        }

        private static int RunLocked(string[] args, string output)
        {
            folder = output;
            foreach (string name in new[] { "smoke-test.txt", "failure.txt", "progress.txt", "hangs.txt" })
                File.Delete(Path.Combine(folder, name));
            List<string> checks = new List<string>();
            try
            {
                SmokeOptions.Parse(args);
                using (SmokeJob job = new SmokeJob())
                using (Process child = new Process())
                {
                    ProcessStartInfo start = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);
                    start.UseShellExecute = false;
                    start.CreateNoWindow = true;
                    start.WindowStyle = ProcessWindowStyle.Hidden;
                    List<string> arguments = new List<string>();
                    foreach (string arg in args) arguments.Add(Quote(arg));
                    start.Arguments = string.Join(" ", arguments.ToArray());
                    start.EnvironmentVariables[WorkerVariable] = "1";
                    child.StartInfo = start;
                    child.Start();
                    job.Assign(child);
                    string[] active = null;
                    string stamp = null;
                    Stopwatch deadline = Stopwatch.StartNew();
                    int seconds = StartupSeconds;
                    bool timedOut = false;
                    while (!child.WaitForExit(100))
                    {
                        string[] progress = ReadProgress(child.Id);
                        if (progress != null && progress[2] != stamp)
                        {
                            active = progress;
                            stamp = progress[2];
                            seconds = int.Parse(progress[3], CultureInfo.InvariantCulture);
                            deadline.Restart();
                        }
                        if (deadline.Elapsed.TotalSeconds < seconds) continue;
                        timedOut = true;
                        job.Stop();
                        if (!child.WaitForExit(5000)) throw new Exception("The smoke worker did not stop.");
                        break;
                    }
                    string[] last = ReadProgress(child.Id);
                    if (last != null) active = last;
                    checks = Checks();
                    int code = timedOut ? -1 : child.ExitCode;
                    string line = Outcome(active, timedOut, seconds, code, timedOut && active != null ? Stuck(active) : "");
                    if (line == null && code != 0 && !checks.Exists(IsFailure)) line = "FAIL smoke worker exited " + code;
                    if (line != null)
                    {
                        checks.Add(line);
                        File.AppendAllText(Path.Combine(folder, "failure.txt"), line + Environment.NewLine);
                    }
                }
            }
            catch (Exception error)
            {
                checks = Checks();
                checks.Add("FAIL smoke: " + error.Message);
                File.AppendAllText(Path.Combine(folder, "failure.txt"), error + Environment.NewLine);
            }
            bool failed = checks.Exists(IsFailure);
            checks.Add(Totals(checks));
            Flush(checks);
            return failed ? 1 : 0;
        }

        internal static string[] ReadProgress(int process)
        {
            try
            {
                string[] lines;
                using (FileStream file = new FileStream(Path.Combine(folder, "progress.txt"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(file)) lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length < 5 || lines[4] != process.ToString(CultureInfo.InvariantCulture)) return null;
                string[] parts = lines[3].Split('\t');
                return parts.Length == 4 ? parts : null;
            }
            catch (IOException) { return null; }
        }

        private static string Quote(string value)
        {
            StringBuilder quoted = new StringBuilder("\"");
            int slashes = 0;
            foreach (char letter in value)
            {
                if (letter == '\\') { slashes++; continue; }
                quoted.Append('\\', letter == '"' ? slashes * 2 + 1 : slashes);
                quoted.Append(letter);
                slashes = 0;
            }
            quoted.Append('\\', slashes * 2);
            return quoted.Append('"').ToString();
        }
    }

    internal sealed class SmokeJob : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
        {
            internal long ProcessTime, JobTime;
            internal uint Flags;
            internal UIntPtr Minimum, Maximum;
            internal uint Processes;
            internal UIntPtr Affinity;
            internal uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Limits
        {
            internal BasicLimits Basic;
            internal ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
            internal UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int info, ref Limits limits, int size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(IntPtr job, uint code);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private IntPtr handle;

        internal SmokeJob()
        {
            handle = CreateJobObject(IntPtr.Zero, null);
            Limits limits = new Limits();
            limits.Basic.Flags = 0x2000;
            if (handle == IntPtr.Zero || !SetInformationJobObject(handle, 9, ref limits, Marshal.SizeOf(typeof(Limits))))
            {
                Dispose();
                throw new System.ComponentModel.Win32Exception();
            }
        }

        internal void Assign(Process process)
        {
            if (!AssignProcessToJobObject(handle, process.Handle))
            {
                int error = Marshal.GetLastWin32Error();
                if (!process.HasExited) process.Kill();
                throw new System.ComponentModel.Win32Exception(error);
            }
        }

        internal void Stop() { if (!TerminateJobObject(handle, 99)) throw new System.ComponentModel.Win32Exception(); }

        public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
    }
}
