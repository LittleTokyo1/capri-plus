using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static string SmallFixSource(string name)
        {
            foreach (string start in new[] { AppDomain.CurrentDomain.BaseDirectory, Environment.CurrentDirectory })
            {
                DirectoryInfo folder = new DirectoryInfo(start);
                while (folder != null)
                {
                    string file = Path.Combine(Path.Combine(folder.FullName, "app"), name);
                    if (File.Exists(file) && File.Exists(Path.Combine(Path.Combine(folder.FullName, "app"), "Mainframe.cs"))) return file;
                    folder = folder.Parent;
                }
            }
            return null;
        }

        internal static void SmokeNoDucking(List<string> checks)
        {
            Func<string, int, object> sessionWas = Mixer.Session;
            Func<object, bool, int> duckingWas = Mixer.Ducking;
            try
            {
                object fake = new object();
                List<string> asked = new List<string>();
                Mixer.Session = delegate(string id, int flow) { asked.Add((id ?? "default") + "/" + flow); return fake; };
                Mixer.Ducking = delegate(object session, bool optOut) { if (session != fake || !optOut) throw new Exception("wrong"); asked.Add("opted out"); return 0; };
                Mixer.NoDucking("mic-1", 1);
                Mixer.NoDucking(null, 0);
                if (string.Join(", ", asked.ToArray()) != "mic-1/1, opted out, default/0, opted out") throw new Exception("Her wave sounds didn't opt out of ducking on their own device: " + string.Join(", ", asked.ToArray()));
                Mixer.Session = delegate(string id, int flow) { throw new InvalidOperationException("no session"); };
                Mixer.NoDucking("mic-1", 1);
                Mixer.Session = delegate(string id, int flow) { return fake; };
                Mixer.Ducking = delegate(object session, bool optOut) { throw new InvalidOperationException("no opt-out"); };
                Mixer.NoDucking("mic-1", 1);
                Mixer.NoDucking((ISoundClient)null);
                Mixer.NoDucking((ILoopClient)null);
            }
            finally
            {
                Mixer.Session = sessionWas;
                Mixer.Ducking = duckingWas;
            }
            List<string> missing = new List<string>();
            int seen = 0;
            foreach (string name in new[] { "Ears.cs", "MicMix.cs", "LoopbackCapture.cs", "GameEars.cs", "CallEars.cs", "ListenEars.cs", "ChatterboxServer.cs", "PhoneServer.cs" })
            {
                string file = SmallFixSource(name);
                if (file == null) continue;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int reach = line.Contains("client.Initialize(0,") ? 2 : line.Contains("waveOutOpen(out device, -1") ? 2 : line.Contains("waveInOpen(out device, id") ? 8 : 0;
                    if (reach == 0) continue;
                    seen++;
                    bool opted = false;
                    for (int j = i + 1; j <= Math.Min(lines.Length - 1, i + reach); j++) if (lines[j].Contains("Mixer.NoDucking(")) opted = true;
                    if (!opted) missing.Add(name + ":" + (i + 1));
                }
            }
            if (SmallFixSource("Ears.cs") == null)
            {
                checks.Add("LIMIT no ducking: her source files aren't next to this build, so the stream list wasn't read");
                return;
            }
            if (missing.Count > 0 || seen < 6) throw new Exception("A sound stream of hers opens without opting out of Windows ducking: " + string.Join(", ", missing.ToArray()) + " (" + seen + " found)");
            checks.Add("PASS no ducking (096-20): every mic, speaker, cable and Discord stream she opens (" + seen + ") tells Windows not to turn his other apps down, on its own device, and a failed opt-out never stops the sound");
        }

        internal static void SmokeEarsTar(List<string> checks)
        {
            string tarExe = Path.Combine(Environment.SystemDirectory, "tar.exe");
            string root = Path.Combine(Path.GetTempPath(), "capri-tar-" + Guid.NewGuid().ToString("N"));
            bool systemWas = LocalEars.SystemBzip;
            int timeoutWas = LocalEars.TarTimeout;
            Func<ProcessStartInfo, Process> startWas = LocalEars.StartTar;
            try
            {
                if (systemWas != true || timeoutWas != 600000) throw new Exception("Her ears stopped trying Windows tar first, or changed its 10 minute limit.");
                string source = Path.Combine(root, "src");
                Directory.CreateDirectory(Path.Combine(source, "pack"));
                File.WriteAllText(Path.Combine(Path.Combine(source, "pack"), "a.txt"), "ears");
                string tarball = Path.Combine(root, "pack.tar.bz2");
                using (Process make = Process.Start(new ProcessStartInfo(tarExe, "-cjf \"" + tarball + "\" -C \"" + source + "\" pack") { UseShellExecute = false, CreateNoWindow = true }))
                {
                    if (!make.WaitForExit(30000) || make.ExitCode != 0 || !File.Exists(tarball))
                    {
                        checks.Add("LIMIT ears tar: Windows tar here can't make a bzip2 test archive");
                        return;
                    }
                }
                byte[] packed = File.ReadAllBytes(tarball);
                string into = Path.Combine(root, "into");
                foreach (string test in new[] { "hang", "start", "plain-hang" })
                {
                    File.WriteAllBytes(tarball, packed);
                    if (Directory.Exists(into)) Directory.Delete(into, true);
                    Directory.CreateDirectory(into);
                    List<Process> hung = new List<Process>();
                    LocalEars.SystemBzip = test != "plain-hang";
                    LocalEars.TarTimeout = 500;
                    LocalEars.StartTar = delegate(ProcessStartInfo info)
                    {
                        bool compressed = info.Arguments.StartsWith("-xjf ", StringComparison.Ordinal);
                        if (test == "start" && compressed) throw new System.ComponentModel.Win32Exception(2);
                        if ((test == "hang" && compressed) || (test == "plain-hang" && !compressed))
                        {
                            File.WriteAllText(Path.Combine(into, "partial.txt"), "partial");
                            info.FileName = Path.Combine(Environment.SystemDirectory, "ping.exe");
                            info.Arguments = "-t 127.0.0.1";
                            Process fake = Process.Start(info);
                            hung.Add(fake);
                            return fake;
                        }
                        LocalEars.TarTimeout = 30000;
                        return Process.Start(info);
                    };
                    Stopwatch took = Stopwatch.StartNew();
                    string problem = null;
                    try { LocalEars.Untar(tarball, into); }
                    catch (IOException failed) { problem = failed.Message; }
                    if (took.ElapsedMilliseconds > 20000) throw new Exception("Her ears setup waited too long on a stuck tar (" + test + ").");
                    foreach (Process fake in hung)
                    {
                        bool ended;
                        try { ended = fake.WaitForExit(5000); }
                        catch (Exception) { ended = true; }
                        if (!ended) { try { fake.Kill(); } catch (Exception) { } throw new Exception("A stuck tar was left running (" + test + ")."); }
                    }
                    if (test == "plain-hang")
                    {
                        if (problem != "Couldn't unpack her ears: Windows tar took too long." || Directory.Exists(into)) throw new Exception("A stuck plain tar didn't fail cleanly: " + problem);
                    }
                    else
                    {
                        string unpacked = Path.Combine(Path.Combine(into, "pack"), "a.txt");
                        if (problem != null || !File.Exists(unpacked) || File.ReadAllText(unpacked) != "ears" || File.Exists(Path.Combine(into, "partial.txt"))) throw new Exception("Her ears didn't unpack themselves after tar " + test + ": " + problem);
                    }
                    if (File.Exists(Path.Combine(root, "pack.tar"))) throw new Exception("The plain archive was left behind after " + test + ".");
                }
                checks.Add("PASS ears setup can't hang (096-20): a Windows tar stuck on the bzip2 archive is stopped at its limit and she unpacks it herself, a tar that can't start falls back the same way, a stuck plain tar fails with why and leaves no half-unpacked folder, and the limit is 10 minutes");
            }
            finally
            {
                LocalEars.SystemBzip = systemWas;
                LocalEars.TarTimeout = timeoutWas;
                LocalEars.StartTar = startWas;
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (Exception) { }
            }
        }

        internal static void SmokeFreezeLog(List<string> checks)
        {
            List<string> notes = new List<string>();
            FreezeWatch watch = null;
            Control host = null;
            ManualResetEvent ready = new ManualResetEvent(false);
            Thread ui = new Thread(delegate()
            {
                host = new Control();
                IntPtr made = host.Handle;
                watch = new FreezeWatch(host) { StallAfter = 400, LogAfter = 600000 };
                watch.Doing = delegate { return "on a job"; };
                watch.Use = delegate { return new WindowUse { Gdi = 9876, User = 5432, Handles = 2109, Private = 3L << 30, Working = 1L << 30 }; };
                watch.Note = delegate(string line) { lock (notes) notes.Add(line); };
                ready.Set();
                Application.Run();
            });
            ui.SetApartmentState(ApartmentState.STA);
            ui.IsBackground = true;
            ui.Start();
            if (!ready.WaitOne(10000)) throw new Exception("The test window for the freeze log didn't start.");
            try
            {
                watch.Start();
                Thread.Sleep(700);
                host.BeginInvoke((MethodInvoker)delegate { Thread.Sleep(1200); });
                Thread.Sleep(2500);
                host.BeginInvoke((MethodInvoker)delegate { Thread.Sleep(1200); });
                Thread.Sleep(2500);
            }
            finally
            {
                watch.Halt();
                try { host.BeginInvoke((MethodInvoker)delegate { host.Dispose(); Application.ExitThread(); }); } catch (Exception) { }
            }
            string all, first;
            int count;
            lock (notes) { count = notes.Count; first = count == 0 ? "" : notes[0]; all = string.Join(" / ", notes.ToArray()); }
            Match took = Regex.Match(first, @"^Her window didn't answer for (\d+\.\d) s, on a job\. GDI 9876, USER 5432, handles 2109, private 3072 MB, working set 1024 MB\. Stuck at: ");
            double seconds = took.Success ? double.Parse(took.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
            if (count != 1 || seconds < 0.7 || seconds > 2.5 || first.IndexOf("SmokeFreezeLog", StringComparison.Ordinal) < 0)
                throw new Exception("A stall wasn't written once with how long, what she was doing, what she holds and where: " + all);
            string logWas = Brain.LogPath;
            string log = Path.Combine(Path.GetTempPath(), "capri-freeze-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                Brain.LogPath = log;
                File.WriteAllText(log, "");
                Brain.Log("frozen", -1, first + new string('x', 400));
                Brain.Log("ears", -1, new string('y', 400));
                string[] kept = File.ReadAllLines(log);
                if (kept.Length != 2 || kept[0].IndexOf(first, StringComparison.Ordinal) < 0 || kept[1].Length != 300) throw new Exception("errors.txt cut a freeze's line short, or let another line run long.");
            }
            finally
            {
                Brain.LogPath = logWas;
                try { File.Delete(log); } catch (Exception) { }
            }
            checks.Add("PASS freeze log (096-20): her window stalling from 2 s goes in errors.txt once it answers, with how long, what she was doing, her GDI, USER, handles and memory, and where her code was held up; another stall within the minute stays out, and errors.txt keeps the whole line");
        }

        internal static void SmokeAdminKeys(List<string> checks)
        {
            HashSet<string> warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IntPtr last = IntPtr.Zero;
            IntPtr genshin = new IntPtr(101), manager = new IntPtr(102), roblox = new IntPtr(103), other = new IntPtr(104);
            Func<IntPtr, bool> above = delegate(IntPtr window) { return window == genshin || window == manager || window == other; };
            string name = null;
            Func<string> game = delegate { return name; };
            name = "Genshin Impact";
            if (!AdminKeys.Warn(genshin, ref last, true, above, game, warned)) throw new Exception("A game that runs as admin didn't get its line.");
            if (AdminKeys.Warn(genshin, ref last, true, above, game, warned)) throw new Exception("The same window got the line twice.");
            name = null;
            if (AdminKeys.Warn(manager, ref last, true, above, game, warned)) throw new Exception("Task Manager got the admin line.");
            name = "Genshin Impact";
            if (AdminKeys.Warn(genshin, ref last, true, above, game, warned)) throw new Exception("The same game got the line again.");
            name = "Roblox";
            if (AdminKeys.Warn(roblox, ref last, true, above, game, warned)) throw new Exception("A game that doesn't run as admin got the line.");
            name = "Other game";
            if (AdminKeys.Warn(other, ref last, false, above, game, warned)) throw new Exception("The line came with no push-to-talk key set.");
            if (AdminKeys.AboveHer(IntPtr.Zero)) throw new Exception("No window counted as an admin one.");
            if (AdminKeys.Line.IndexOf("push-to-talk", StringComparison.Ordinal) < 0 || AdminKeys.Line.IndexOf("as admin", StringComparison.Ordinal) < 0) throw new Exception("The admin line doesn't say why or what to do.");
            checks.Add("PASS admin games (096-20): with a push-to-talk key set, a game that runs as admin gets one line on why her keys can't reach it and to start her as admin; Task Manager, the same game again, a game that doesn't run as admin and no push-to-talk key get nothing; an unreadable token never counts, so anti-cheat games stay quiet");
            checks.Add("LIMIT admin games: whether a real game's token says it runs as admin needs that game on the PC");
        }

        internal static void SmokeNoBrainLines(List<string> checks)
        {
            Func<bool> was = FreeKeys.On;
            try
            {
                FreeKeys.On = delegate { return false; };
                foreach (string line in new[] { Brain.Offline, Brain.Blind, Brain.Unserved })
                    if (line.IndexOf("FREE KEYS in Capri+", StringComparison.Ordinal) < 0) throw new Exception("With free keys off a no-brain line still points at a hidden button: " + line);
                if (!Brain.Offline.EndsWith(Brain.Stuck, StringComparison.Ordinal) || Brain.Offline.IndexOf("Set up her brain", StringComparison.Ordinal) < 0) throw new Exception("The offline line lost Set up her brain or its Discord help.");
                FreeKeys.On = delegate { return true; };
                if (Brain.Offline.IndexOf("FREE KEYS", StringComparison.Ordinal) >= 0 || Brain.Blind.IndexOf("press Free key", StringComparison.Ordinal) < 0 || Brain.Unserved.IndexOf("Press Free key", StringComparison.Ordinal) < 0)
                    throw new Exception("With free keys on the lines didn't go back to the Free key button.");
            }
            finally { FreeKeys.On = was; }
            checks.Add("PASS no-brain lines (096-20): with FREE KEYS off, brain offline, DeepSeek can't see and Gemini not served say to turn on FREE KEYS in Capri+ (or Set up her brain) instead of a hidden Free key button; with it on they name Free key as before");
        }
    }
}
