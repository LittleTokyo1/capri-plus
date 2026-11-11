using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class SoundApp
    {
        internal string Program;
        internal string Shown;
        internal float Level;
        internal bool Muted;
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundDeviceList
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out ISoundDevice device);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundSessions
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr grouping, int flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr grouping, int flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out ISoundSessionList sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundSessionList
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundSession
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName(IntPtr name, IntPtr context);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(IntPtr path, IntPtr context);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(IntPtr grouping, IntPtr context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int GetSessionIdentifier(out IntPtr id);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint id);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundSessionVolume
    {
        [PreserveSig] int SetMasterVolume(float level, IntPtr context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    internal static class Mixer
    {
        internal static Func<List<SoundApp>> List = ReadSessions;
        internal static Func<string, float?, bool?, bool> Change = WriteSessions;
        internal static readonly Guid SessionId = new Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD");
        internal static Func<string, int, object> Session = OpenSession;
        internal static Func<object, bool, int> Ducking = delegate(object session, bool optOut) { return ((ISoundSession)session).SetDuckingPreference(optOut); };

        internal static void NoDucking(ISoundClient client)
        {
            object session = null;
            try
            {
                Guid id = SessionId;
                if (client != null && client.GetService(ref id, out session) >= 0 && session != null) Ducking(session, true);
            }
            catch (Exception) { }
            finally { Drop(session); }
        }

        internal static void NoDucking(ILoopClient client)
        {
            object session = null;
            try
            {
                Guid id = SessionId;
                if (client != null && client.GetService(ref id, out session) >= 0 && session != null) Ducking(session, true);
            }
            catch (Exception) { }
            finally { Drop(session); }
        }

        internal static void NoDucking(string id, int flow)
        {
            object session = null;
            try
            {
                session = Session(id, flow);
                if (session != null) Ducking(session, true);
            }
            catch (Exception) { }
            finally { Drop(session); }
        }

        private static object OpenSession(string id, int flow)
        {
            ISoundDevices devices = null;
            ISoundDevice device = null;
            object manager = null;
            IntPtr control = IntPtr.Zero;
            try
            {
                devices = (ISoundDevices)new SoundDevices();
                int result = string.IsNullOrEmpty(id) ? devices.GetDefaultAudioEndpoint(flow, 0, out device) : devices.GetDevice(id, out device);
                if (result < 0 || device == null) return null;
                Guid managerId = typeof(ISoundSessions).GUID;
                if (device.Activate(ref managerId, 23, IntPtr.Zero, out manager) < 0 || manager == null) return null;
                if (((ISoundSessions)manager).GetAudioSessionControl(IntPtr.Zero, 0, out control) < 0 || control == IntPtr.Zero) return null;
                return Marshal.GetObjectForIUnknown(control);
            }
            finally
            {
                if (control != IntPtr.Zero) Marshal.Release(control);
                Drop(manager);
                Drop(device);
                Drop(devices);
            }
        }

        private static void Drop(object com)
        {
            if (com == null || !Marshal.IsComObject(com)) return;
            try { Marshal.ReleaseComObject(com); }
            catch (Exception) { }
        }
        private const int Step = 10;
        private const int Small = 5;
        private const int Big = 25;
        private static readonly Regex PcWords = new Regex(@"^(?:volume|sound|audio|music|pc|computer|system|everything|all|master|windows|it|speakers?|headphones|headset|media|song|video|player)$", RegexOptions.IgnoreCase);
        private static readonly Regex Size = new Regex(@"\s+(?:a\s+)?(?<size>(?:tiny\s+)?bit|little(?:\s+bit)?|lot|ton|tad)$", RegexOptions.IgnoreCase);
        private static readonly Regex[] Moves =
        {
            new Regex(@"^(?:turn|crank|dial|knock)\s+(?<dir>up|down)\s+(?<app>.+)$", RegexOptions.IgnoreCase),
            new Regex(@"^(?:turn|crank|dial|knock)\s+(?<app>.+?)\s+(?<dir>up|down)$", RegexOptions.IgnoreCase),
            new Regex(@"^(?<verb>lower|raise|quieten|reduce|increase|decrease|boost)\s+(?:the\s+)?(?:(?:volume|sound|audio)\s+(?:of|on|in|for)\s+)?(?<app>.+)$", RegexOptions.IgnoreCase),
            new Regex(@"^make\s+(?<app>.+?)\s+(?<dir>quieter|louder|softer|lower|higher)$", RegexOptions.IgnoreCase)
        };
        private static readonly Regex Levelled = new Regex(@"^(?:(?:set|put|turn)\s+)?(?:the\s+)?(?:(?:volume|sound|audio)\s+(?:of|on|in|for)\s+)?(?<app>.+?)(?:'s)?(?:\s+(?:volume|sound|audio))?\s+(?:to|at)\s+(?<n>\d{1,3})\s*(?:%|percent)?$", RegexOptions.IgnoreCase);
        private static readonly Regex Muting = new Regex(@"^(?<m>mute|unmute|un-mute|silence)\s+(?<app>.+)$", RegexOptions.IgnoreCase);
        private static readonly Regex Talkers = new Regex(@"^(?:discord|vesktop|webcord|teamspeak|ts3client|teams|ms teams|zoom|skype|mumble|slack)\b", RegexOptions.IgnoreCase);
        private static readonly Regex SoundWords = new Regex(@"\b(?:sound|audio|volume|music|noise|speakers?|output)\b", RegexOptions.IgnoreCase);

        private static bool MicMeant(string program, string said) { return Talkers.IsMatch(Plain(program)) && !SoundWords.IsMatch(said ?? ""); }

        internal static bool Heard(string text, out string tool, out ToolArguments args)
        {
            tool = null;
            args = new ToolArguments();
            string said = (text ?? "").Trim();
            string level = "";
            Match sized = Size.Match(said);
            string moved = sized.Success ? said.Substring(0, sized.Index) : said;
            Match muted = Muting.Match(said);
            Match levelled = Levelled.Match(said);
            Match found = null;
            foreach (Regex form in Moves)
            {
                Match tried = form.Match(moved);
                if (tried.Success) { found = tried; break; }
            }
            if (!muted.Success && !levelled.Success && found == null) return false;
            List<SoundApp> apps = Read();
            if (apps.Count == 0) return false;
            if (muted.Success)
            {
                string who = Resolve(muted.Groups["app"].Value, apps);
                if (who == null || MicMeant(who, said)) return false;
                tool = "media";
                args.Action = muted.Groups["m"].Value.ToLowerInvariant().StartsWith("un", StringComparison.Ordinal) ? "unmute" : "mute";
                args.App = who;
                return true;
            }
            if (levelled.Success)
            {
                string who = Resolve(levelled.Groups["app"].Value, apps);
                if (who == null) return false;
                tool = "media";
                args.Action = "volume_set";
                args.Level = levelled.Groups["n"].Value;
                args.App = who;
                return true;
            }
            if (sized.Success)
            {
                string size = sized.Groups["size"].Value.ToLowerInvariant();
                level = (size.Contains("lot") || size.Contains("ton") ? Big : Small).ToString(CultureInfo.InvariantCulture);
            }
            foreach (Regex form in Moves)
            {
                Match tried = form.Match(moved);
                if (!tried.Success) continue;
                string who = Resolve(tried.Groups["app"].Value, apps);
                if (who == null) continue;
                string way = (tried.Groups["dir"].Success ? tried.Groups["dir"].Value : tried.Groups["verb"].Value).ToLowerInvariant();
                tool = "media";
                args.Action = Regex.IsMatch(way, "^(up|raise|increase|boost|louder|higher)$") ? "volume_up" : "volume_down";
                args.Level = level.Length == 0 ? null : level;
                args.App = who;
                return true;
            }
            return false;
        }

        internal static bool Takes(ToolArguments args)
        {
            if (args == null || !Volume(args.Action) || string.IsNullOrWhiteSpace(args.App)) return false;
            return Resolve(args.App, Read()) != null;
        }

        internal static string Resolve(string wanted) { return Resolve(wanted, Read()); }

        internal static string Do(ToolArguments args, string said)
        {
            if (args == null || !Volume(args.Action)) return null;
            List<SoundApp> apps = Read();
            string wanted = args.App;
            if (string.IsNullOrWhiteSpace(wanted) || Want(wanted).Length == 0)
            {
                wanted = Named(said, apps);
                if (wanted == null) return null;
            }
            string program = Resolve(wanted, apps);
            if (program == null)
            {
                if (Want(wanted) == "spotify" || apps.Count == 0) return null;
                return "I can't hear " + wanted.Trim() + " playing anything right now. Making sound now: " + Listing(apps) + ".";
            }
            List<SoundApp> mine = apps.FindAll(app => app.Program == program);
            string shown = mine[0].Shown;
            if ((args.Action == "mute" || args.Action == "unmute") && said != null && MicMeant(program, said))
                return "Not done: for " + shown + ", mute means his mic. Call shortcut with discord_mute for his mic; only mute " + shown + "'s sound when he says sound or audio.";
            float now = 0;
            bool anyMuted = false, allMuted = true;
            foreach (SoundApp app in mine)
            {
                now = Math.Max(now, app.Level);
                anyMuted |= app.Muted;
                allMuted &= app.Muted;
            }
            string done = Apply(args, program, shown, now, anyMuted, allMuted);
            ActivityLog.Write("app volume", program + " " + args.Action + (string.IsNullOrEmpty(args.Level) ? "" : " " + args.Level), done);
            return done;
        }

        private static string Apply(ToolArguments args, string program, string shown, float now, bool anyMuted, bool allMuted)
        {
            string refused = "Couldn't change " + shown + "'s volume: Windows didn't let me.";
            string action = args.Action;
            int level;
            bool leveled = int.TryParse(args.Level, NumberStyles.Integer, CultureInfo.InvariantCulture, out level);
            if (action == "mute")
            {
                if (allMuted) return shown + " is already muted.";
                return Change(program, null, true) ? "Muted " + shown + "." : refused;
            }
            if (action == "unmute")
            {
                if (!anyMuted) return shown + " isn't muted.";
                return Change(program, null, false) ? "Unmuted " + shown + "." : refused;
            }
            if (action == "volume_set")
            {
                if (!leveled || level < 0 || level > 100) return "Couldn't set " + shown + "'s volume: it goes from 0 to 100.";
                return Change(program, level / 100f, level > 0 && anyMuted ? false : (bool?)null) ? "Set " + shown + " to " + level + "%." : refused;
            }
            bool up = action == "volume_up";
            int step = leveled && level > 0 && level <= 100 ? level : Step;
            int percent = (int)Math.Round(Math.Max(0f, Math.Min(1f, now + (up ? step : -step) / 100f)) * 100);
            if (!Change(program, percent / 100f, up && anyMuted ? false : (bool?)null)) return refused;
            return "Turned " + shown + (up ? " up" : " down") + " to " + percent + "%.";
        }

        private static bool Volume(string action) { return action == "volume_up" || action == "volume_down" || action == "volume_set" || action == "mute" || action == "unmute"; }

        private static List<SoundApp> Read()
        {
            try { return List() ?? new List<SoundApp>(); }
            catch (Exception) { return new List<SoundApp>(); }
        }

        private static string Listing(List<SoundApp> apps)
        {
            List<string> names = new List<string>();
            foreach (SoundApp app in apps) if (!names.Contains(app.Shown)) names.Add(app.Shown);
            return string.Join(", ", names.ToArray());
        }

        private static string Plain(string text)
        {
            string flat = Regex.Replace((text ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant(), @"\p{M}", "");
            return Regex.Replace(Regex.Replace(flat, @"'s\b", ""), @"[^\p{L}\p{Nd}]+", " ").Trim();
        }

        private static string Want(string wanted)
        {
            string want = Plain(wanted);
            string was;
            do
            {
                was = want;
                want = Regex.Replace(want, @"^(?:the|my|your|ur|our|that|this|on|in|of)\s+", "");
                want = Regex.Replace(want, @"\s+(?:app|application|game|volume|sound|audio|music|window|tab|client)$", "");
            }
            while (want != was);
            return PcWords.IsMatch(want) ? "" : want;
        }

        internal static string Resolve(string wanted, List<SoundApp> apps)
        {
            string want = Want(wanted);
            if (want.Length < 2 || apps == null || apps.Count == 0) return null;
            string squeezed = want.Replace(" ", "");
            int best = 0;
            List<string> top = new List<string>();
            foreach (SoundApp app in apps)
            {
                int score = Math.Max(Score(Plain(app.Program), want, squeezed), Score(Plain(app.Shown), want, squeezed));
                if (score == 0 || score < best) continue;
                if (score > best) { best = score; top.Clear(); }
                if (!top.Contains(app.Program)) top.Add(app.Program);
            }
            if (top.Count == 1) return top[0];
            if (top.Count > 1) return null;
            foreach (OpenWindow window in Hands.WindowsNamed(want, 2))
            {
                SoundApp owner = apps.Find(app => string.Equals(app.Program, window.Program, StringComparison.OrdinalIgnoreCase));
                if (owner != null) return owner.Program;
            }
            return null;
        }

        private static int Score(string name, string want, string squeezed)
        {
            if (name.Length == 0) return 0;
            string tight = name.Replace(" ", "");
            if (name == want || tight == squeezed) return 5;
            if (name.StartsWith(want + " ", StringComparison.Ordinal)) return 4;
            if ((" " + name + " ").Contains(" " + want + " ")) return 3;
            if (squeezed.Length >= 4 && tight.Contains(squeezed)) return 2;
            if (squeezed.Length >= 5 && Distance(squeezed, tight) <= Math.Max(1, squeezed.Length / 4)) return 1;
            return 0;
        }

        private static int Distance(string a, string b)
        {
            int[] row = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) row[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int corner = row[0];
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int above = row[j];
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), corner + (a[i - 1] == b[j - 1] ? 0 : 1));
                    corner = above;
                }
            }
            return row[b.Length];
        }

        private static string Named(string said, List<SoundApp> apps)
        {
            string text = " " + Plain(said) + " ";
            if (text.Trim().Length == 0) return null;
            string found = null;
            foreach (SoundApp app in apps)
            {
                string name = Plain(app.Program);
                string first = name.Split(' ')[0];
                bool named = (name.Length >= 3 && text.Contains(" " + name + " ")) || (first.Length >= 4 && text.Contains(" " + first + " "));
                if (!named) continue;
                if (found != null && found != app.Program) return null;
                found = app.Program;
            }
            return found;
        }

        private static List<SoundApp> ReadSessions()
        {
            List<SoundApp> found = new List<SoundApp>();
            Dictionary<uint, string> programs = new Dictionary<uint, string>();
            Each(delegate(uint pid, string display, ISoundSessionVolume volume)
            {
                string program = Program(pid, programs);
                if (program.Length == 0) return;
                float level;
                bool muted;
                if (volume.GetMasterVolume(out level) != 0) return;
                if (volume.GetMute(out muted) != 0) muted = false;
                found.Add(new SoundApp { Program = program, Shown = string.IsNullOrWhiteSpace(display) || display.StartsWith("@", StringComparison.Ordinal) ? program : display.Trim(), Level = level, Muted = muted });
            });
            return found;
        }

        private static bool WriteSessions(string program, float? level, bool? muted)
        {
            bool any = false, ok = true;
            Dictionary<uint, string> programs = new Dictionary<uint, string>();
            Each(delegate(uint pid, string display, ISoundSessionVolume volume)
            {
                if (!string.Equals(Program(pid, programs), program, StringComparison.OrdinalIgnoreCase)) return;
                any = true;
                if (level.HasValue) ok &= volume.SetMasterVolume(Math.Max(0f, Math.Min(1f, level.Value)), IntPtr.Zero) == 0;
                if (muted.HasValue) ok &= volume.SetMute(muted.Value, IntPtr.Zero) == 0;
            });
            return any && ok;
        }

        private static string Program(uint pid, Dictionary<uint, string> programs)
        {
            string program;
            if (programs.TryGetValue(pid, out program)) return program;
            try { using (Process process = Process.GetProcessById((int)pid)) program = process.ProcessName; }
            catch (ArgumentException) { program = ""; }
            catch (InvalidOperationException) { program = ""; }
            programs[pid] = program;
            return program;
        }

        private delegate void SessionVisit(uint pid, string display, ISoundSessionVolume volume);

        private static void Each(SessionVisit visit)
        {
            ISoundDevices devices = null;
            ISoundDeviceList list = null;
            IntPtr listed = IntPtr.Zero;
            try
            {
                devices = (ISoundDevices)new SoundDevices();
                if (devices.EnumAudioEndpoints(0, 1, out listed) != 0 || listed == IntPtr.Zero) return;
                list = (ISoundDeviceList)Marshal.GetObjectForIUnknown(listed);
                int count;
                if (list.GetCount(out count) != 0) return;
                for (int i = 0; i < count; i++)
                {
                    ISoundDevice device;
                    if (list.Item(i, out device) != 0 || device == null) continue;
                    try { Sessions(device, visit); }
                    finally { Marshal.ReleaseComObject(device); }
                }
            }
            catch (Exception) { }
            finally
            {
                if (listed != IntPtr.Zero) Marshal.Release(listed);
                if (list != null) Marshal.ReleaseComObject(list);
                if (devices != null) Marshal.ReleaseComObject(devices);
            }
        }

        private static void Sessions(ISoundDevice device, SessionVisit visit)
        {
            object found = null;
            ISoundSessionList sessions = null;
            try
            {
                Guid id = typeof(ISoundSessions).GUID;
                if (device.Activate(ref id, 23, IntPtr.Zero, out found) != 0 || found == null) return;
                if (((ISoundSessions)found).GetSessionEnumerator(out sessions) != 0 || sessions == null) return;
                int count;
                if (sessions.GetCount(out count) != 0) return;
                for (int i = 0; i < count; i++)
                {
                    object one;
                    if (sessions.GetSession(i, out one) != 0 || one == null) continue;
                    try
                    {
                        ISoundSession session = one as ISoundSession;
                        ISoundSessionVolume volume = one as ISoundSessionVolume;
                        int state;
                        uint pid;
                        if (session == null || volume == null || session.GetState(out state) != 0 || state == 2) continue;
                        if (session.IsSystemSoundsSession() == 0 || session.GetProcessId(out pid) != 0 || pid == 0) continue;
                        string display;
                        if (session.GetDisplayName(out display) != 0) display = null;
                        visit(pid, display, volume);
                    }
                    finally { Marshal.ReleaseComObject(one); }
                }
            }
            finally
            {
                if (sessions != null) Marshal.ReleaseComObject(sessions);
                if (found != null) Marshal.ReleaseComObject(found);
            }
        }
    }
}
