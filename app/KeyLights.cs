using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CapriDesktop
{
    internal static class KeyLights
    {
        internal const int Idle = 0, Talking = 1, Thinking = 2, Dark = 3;
        internal const int Rows = 6, Cols = 21;
        internal const double SweepSeconds = 1.5;
        private const double FadeSeconds = 3;
        private const int FrameMilliseconds = 50;
        private const double Glow = 0.35;
        internal const double Readable = 0.65;
        private const string Sdk = "wooting-rgb-sdk64.dll";
        internal const string SdkUrl = "https://github.com/WootingKb/wooting-rgb-sdk/releases/download/v1.8.0/wooting-rgb-sdk-v1.8.0-win-x64.zip";
        internal const string ZipHash = "0CBA843F1D4E58762D51B8D9371C28E31962B5484BEA3CF07345444C306F08A2";
        internal const string DllHash = "D01E9BBD4D05A826E9E0054561A5F61787FA18EFCD122142F7DBF79FA254BEC9";
        internal static readonly int[] Rose = { 255, 93, 102 };
        internal static readonly int[] Gold = { 255, 180, 60 };
        private static readonly int[][] MoodColors = { new[] { 255, 93, 102 }, new[] { 120, 20, 30 }, new[] { 232, 66, 70 }, new[] { 255, 120, 170 }, new[] { 255, 180, 60 }, new[] { 150, 60, 200 } };
        private static readonly double[] MoodLevels = { 0.35, 0.2, 0.6, 0.45, 0.5, 0.35 };
        private static readonly int[] WorkColor = { 94, 240, 246 }, PentestColor = { 60, 255, 120 };

        [DllImport(Sdk, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool wooting_rgb_kbd_connected();
        [DllImport(Sdk, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool wooting_rgb_array_set_full(byte[] colors);
        [DllImport(Sdk, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool wooting_rgb_array_update_keyboard();
        [DllImport(Sdk, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool wooting_rgb_reset();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibrary(string path);

        internal static Func<bool> Present = delegate
        {
            try
            {
                using (RegistryKey usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB"))
                {
                    if (usb == null) return false;
                    foreach (string name in usb.GetSubKeyNames()) if (name.StartsWith("VID_31E3", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch (Exception) { }
            return false;
        };

        internal static Func<string, bool> Fetch = Download;

        private static readonly object gate = new object();
        private static volatile int state, mood;
        private static DateTime darkAt = DateTime.UtcNow, flashAt = DateTime.MinValue;
        private static int[] flash;
        private static System.Threading.Timer timer;
        private static bool loaded, fetching, gaveUp, hooked;
        private static readonly DateTime started = DateTime.UtcNow;

        internal static bool Lit { get { lock (gate) return timer != null; } }

        internal static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "wooting", Sdk); }
        }

        internal static void Forget()
        {
            Stop();
            lock (gate)
            {
                fetching = gaveUp = false;
                flash = null;
            }
        }

        internal static void Set(int now, int feeling)
        {
            if (now == Dark && state != Dark) lock (gate) darkAt = DateTime.UtcNow;
            state = now;
            mood = feeling;
        }

        internal static void Sweep(int[] color)
        {
            if (color == null) return;
            lock (gate)
            {
                flash = color;
                flashAt = DateTime.UtcNow;
            }
        }

        internal static int[] SceneColor(string scene)
        {
            return scene == Scene.Work ? WorkColor : scene == Scene.Pentest ? PentestColor : Rose;
        }

        internal static bool Big(string moment)
        {
            return moment != null && (moment.IndexOf("PENTAKILL", StringComparison.Ordinal) >= 0 || moment.IndexOf("WON the game", StringComparison.Ordinal) >= 0);
        }

        internal static void Game(string moment)
        {
            if (Big(moment)) Sweep(Gold);
        }

        internal static double Floor(int[] color)
        {
            int peak = Math.Max(color[0], Math.Max(color[1], color[2]));
            return peak <= 0 ? 1 : Readable * 255 / peak;
        }

        internal static byte[] Frame(int now, int feeling, double seconds, double sinceDark, double sinceFlash, int[] sweep)
        {
            int[] color = Rose;
            double level;
            if (now == Dark) level = Math.Max(0, Glow * (1 - sinceDark / FadeSeconds));
            else if (now == Talking) level = Readable + (1 - Readable) * Math.Abs(Math.Sin(seconds * Math.PI * 1.5));
            else if (now == Thinking) level = Readable + (1 - Readable) * (1 + Math.Sin(seconds * 2 * Math.PI * 0.3)) / 4;
            else
            {
                int at = Math.Max(0, Math.Min(MoodColors.Length - 1, feeling));
                color = MoodColors[at];
                level = Floor(color) + (1 - Readable) * MoodLevels[at];
            }
            byte[] buffer = new byte[Rows * Cols * 3];
            double head = sweep != null && now != Dark && sinceFlash >= 0 && sinceFlash < SweepSeconds ? sinceFlash / SweepSeconds * (Cols + 4) : double.NaN;
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Cols; col++)
                {
                    double strength = double.IsNaN(head) ? 0 : Math.Max(0, 1 - Math.Abs(col - head) / 3);
                    int index = (row * Cols + col) * 3;
                    for (int part = 0; part < 3; part++)
                        buffer[index + part] = (byte)Math.Max(0, Math.Min(255, Math.Round(color[part] * level * (1 - strength) + (sweep == null ? 0 : sweep[part]) * strength)));
                }
            return buffer;
        }

        internal static void Run(bool on)
        {
            if (!on)
            {
                Stop();
                return;
            }
            lock (gate)
            {
                if (timer != null || gaveUp || fetching) return;
            }
            if (!Load()) return;
            lock (gate)
            {
                if (timer != null) return;
                timer = new System.Threading.Timer(Draw, null, 0, FrameMilliseconds);
                if (!hooked)
                {
                    hooked = true;
                    Application.ApplicationExit += delegate { Stop(); };
                }
            }
            ActivityLog.Write("key lights", "on", "Wooting keyboard follows her");
        }

        internal static void Stop()
        {
            lock (gate)
            {
                if (timer == null) return;
                timer.Dispose();
                timer = null;
                try { if (loaded) wooting_rgb_reset(); }
                catch (Exception error) { Brain.Log("key lights", -1, error.GetType().Name + ": " + error.Message); }
            }
        }

        private static void Draw(object unused)
        {
            lock (gate)
            {
                if (timer == null) return;
                DateTime now = DateTime.UtcNow;
                try
                {
                    byte[] frame = Frame(state, mood, (now - started).TotalSeconds, (now - darkAt).TotalSeconds, (now - flashAt).TotalSeconds, flash);
                    wooting_rgb_array_set_full(frame);
                    wooting_rgb_array_update_keyboard();
                }
                catch (Exception error)
                {
                    Brain.Log("key lights", -1, error.GetType().Name + ": " + error.Message);
                    timer.Dispose();
                    timer = null;
                    gaveUp = true;
                }
            }
        }

        private static bool Load()
        {
            lock (gate)
            {
                if (loaded) return true;
                if (gaveUp || fetching) return false;
                if (!Present())
                {
                    gaveUp = true;
                    return false;
                }
            }
            string path = FilePath;
            if (!File.Exists(path) || Hash(path) != DllHash)
            {
                lock (gate) fetching = true;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool got = false;
                    try { got = Fetch(path); }
                    catch (Exception error) { Brain.Log("key lights", -1, error.GetType().Name + ": " + error.Message); }
                    lock (gate)
                    {
                        fetching = false;
                        if (!got) gaveUp = true;
                    }
                    ActivityLog.Write("key lights", got ? "Wooting SDK fetched" : "Wooting SDK not fetched", got ? "hash checked" : null);
                });
                return false;
            }
            try
            {
                if (LoadLibrary(path) == IntPtr.Zero || !wooting_rgb_kbd_connected())
                {
                    lock (gate) gaveUp = true;
                    return false;
                }
            }
            catch (Exception error)
            {
                Brain.Log("key lights", -1, error.GetType().Name + ": " + error.Message);
                lock (gate) gaveUp = true;
                return false;
            }
            lock (gate) loaded = true;
            return true;
        }

        private static bool Download(string path)
        {
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            string zip = Path.Combine(folder, "sdk.zip");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (WebClient client = new WebClient()) client.DownloadFile(SdkUrl, zip);
            try
            {
                if (Hash(zip) != ZipHash) return false;
                string temporary = path + ".tmp";
                using (ZipArchive archive = ZipFile.OpenRead(zip))
                {
                    ZipArchiveEntry entry = archive.GetEntry(Sdk);
                    if (entry == null) return false;
                    entry.ExtractToFile(temporary, true);
                }
                if (Hash(temporary) != DllHash)
                {
                    File.Delete(temporary);
                    return false;
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
                return true;
            }
            finally { try { File.Delete(zip); } catch (IOException) { } }
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }

    internal sealed partial class Companion
    {
        private void ForkKeyLightsTick()
        {
            ForkSettings settings = ForkSettings.Current;
            bool on = settings.KeyLightsOn && !testing;
            KeyLights.Run(on);
            if (!on) return;
            int now = settings.NightOn ? KeyLights.Dark : talkState == TalkState.Talking ? KeyLights.Talking : talkState == TalkState.Thinking ? KeyLights.Thinking : KeyLights.Idle;
            KeyLights.Set(now, settings.Moods ? Mood.Now(DateTime.Now, Lockout.Active || !string.IsNullOrEmpty(League.Brief)) : Mood.Chill);
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox keyLights;

        private void KeyLightsRow(ForkSettings current)
        {
            keyLights = Check("Keyboard lights: a Wooting keyboard glows in her red kit and follows her (talking, thinking, mood, goodnight, scenes, level-ups, big game moments); off hands the lights back to Wootility; nothing happens without a Wooting keyboard", current.KeyLightsOn);
        }

        private void FillKeyLights()
        {
            settings.KeyLightsOn = keyLights.Checked;
        }
    }
}
