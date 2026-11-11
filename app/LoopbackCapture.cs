using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapriDesktop
{
    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ILoopActivated
    {
        void ActivateCompleted(ILoopActivation operation);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ILoopActivation
    {
        void GetActivateResult(out int result, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
    }

    [ComImport, Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ILoopAgile
    {
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ILoopClient
    {
        [PreserveSig] int Initialize(int shareMode, uint flags, long bufferDuration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long normal, out long least);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService([In] ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ILoopCapture
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong counter);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    internal sealed class LoopActivated : ILoopActivated, ILoopAgile
    {
        internal readonly ManualResetEvent Done = new ManualResetEvent(false);
        internal int Result = -1;
        internal object Client;

        public void ActivateCompleted(ILoopActivation operation)
        {
            try
            {
                int result;
                object activated;
                operation.GetActivateResult(out result, out activated);
                Result = result;
                Client = activated;
            }
            catch (Exception) { Result = -1; }
            Done.Set();
        }
    }

    internal sealed class LoopbackCapture : IDisposable
    {
        private const string LoopbackDevice = "VAD\\Process_Loopback";
        private const int ProcessLoopback = 1;
        private const int IncludeTree = 0;
        private const short PropBlob = 65;
        private const uint Flags = 0x00020000 | 0x00040000 | 0x80000000 | 0x08000000;
        private const long BufferTicks = 2000000;
        private const uint Silent = 0x2;
        private static readonly Guid ClientId = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static readonly Guid CaptureId = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
        private static readonly string[] Apps = { "Discord", "DiscordPTB", "DiscordCanary" };

        private readonly int process;
        private readonly Action<short[]> heard;
        private volatile bool running;
        private Thread thread;
        internal volatile string Problem;

        [DllImport("Mmdevapi.dll", ExactSpelling = true)]
        private static extern int ActivateAudioInterfaceAsync([MarshalAs(UnmanagedType.LPWStr)] string device, [In] ref Guid id, IntPtr parameters, ILoopActivated done, out ILoopActivation operation);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            internal uint Size;
            internal uint Usage;
            internal uint Id;
            internal IntPtr Heap;
            internal uint Module;
            internal uint Threads;
            internal uint Parent;
            internal int Priority;
            internal uint Kind;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Exe;
        }

        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        internal LoopbackCapture(int process, Action<short[]> heard)
        {
            this.process = process;
            this.heard = heard;
        }

        internal int Process { get { return process; } }

        internal bool Running { get { return running; } }

        internal static int DiscordRoot()
        {
            Dictionary<uint, KeyValuePair<uint, string>> all = new Dictionary<uint, KeyValuePair<uint, string>>();
            IntPtr snapshot = CreateToolhelp32Snapshot(0x2, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return 0;
            try
            {
                ProcessEntry entry = new ProcessEntry();
                entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry));
                if (!Process32FirstW(snapshot, ref entry)) return 0;
                do all[entry.Id] = new KeyValuePair<uint, string>(entry.Parent, entry.Exe ?? "");
                while (Process32NextW(snapshot, ref entry));
            }
            finally { CloseHandle(snapshot); }
            return Root(all);
        }

        internal static int Root(Dictionary<uint, KeyValuePair<uint, string>> all)
        {
            foreach (KeyValuePair<uint, KeyValuePair<uint, string>> one in all)
            {
                if (!IsDiscord(one.Value.Value)) continue;
                KeyValuePair<uint, string> parent;
                if (all.TryGetValue(one.Value.Key, out parent) && IsDiscord(parent.Value)) continue;
                return (int)one.Key;
            }
            return 0;
        }

        private static bool IsDiscord(string exe)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(exe ?? "");
            foreach (string app in Apps) if (string.Equals(name, app, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal void Start()
        {
            if (running) return;
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "Capri listening loopback" };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        private void Run()
        {
            ILoopClient client = null;
            ILoopCapture capture = null;
            IntPtr parameters = IntPtr.Zero, variant = IntPtr.Zero, format = IntPtr.Zero;
            AutoResetEvent ready = new AutoResetEvent(false);
            try
            {
                parameters = Marshal.AllocHGlobal(12);
                Marshal.WriteInt32(parameters, 0, ProcessLoopback);
                Marshal.WriteInt32(parameters, 4, process);
                Marshal.WriteInt32(parameters, 8, IncludeTree);
                variant = Marshal.AllocHGlobal(24);
                for (int at = 0; at < 24; at += 8) Marshal.WriteInt64(variant, at, 0);
                Marshal.WriteInt16(variant, 0, PropBlob);
                Marshal.WriteInt32(variant, 8, 12);
                Marshal.WriteIntPtr(variant, 16, parameters);
                LoopActivated done = new LoopActivated();
                Guid id = ClientId;
                ILoopActivation operation;
                int started = ActivateAudioInterfaceAsync(LoopbackDevice, ref id, variant, done, out operation);
                if (started != 0) throw new InvalidOperationException("Windows would not open Discord's sound (0x" + started.ToString("X8") + ").");
                if (!done.Done.WaitOne(5000)) throw new InvalidOperationException("Windows took too long to open Discord's sound.");
                if (done.Result != 0 || done.Client == null) throw new InvalidOperationException("Windows refused Discord's sound (0x" + done.Result.ToString("X8") + ").");
                client = (ILoopClient)done.Client;
                format = Marshal.AllocHGlobal(18);
                Marshal.WriteInt16(format, 0, 1);
                Marshal.WriteInt16(format, 2, 1);
                Marshal.WriteInt32(format, 4, Ears.Rate);
                Marshal.WriteInt32(format, 8, Ears.Rate * 2);
                Marshal.WriteInt16(format, 12, 2);
                Marshal.WriteInt16(format, 14, 16);
                Marshal.WriteInt16(format, 16, 0);
                Check(client.Initialize(0, Flags, BufferTicks, 0, format, IntPtr.Zero), "set up");
                Mixer.NoDucking(client);
                Check(client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle()), "wait on");
                Guid captureId = CaptureId;
                object service;
                Check(client.GetService(ref captureId, out service), "read");
                capture = (ILoopCapture)service;
                Check(client.Start(), "start");
                while (running)
                {
                    ready.WaitOne(200);
                    uint packet;
                    while (running && capture.GetNextPacketSize(out packet) == 0 && packet > 0)
                    {
                        IntPtr data;
                        uint frames, flags;
                        ulong position, counter;
                        if (capture.GetBuffer(out data, out frames, out flags, out position, out counter) != 0) break;
                        short[] samples = new short[frames];
                        if ((flags & Silent) == 0 && frames > 0) Marshal.Copy(data, samples, 0, (int)frames);
                        capture.ReleaseBuffer(frames);
                        if (frames > 0) heard(samples);
                    }
                }
                client.Stop();
            }
            catch (Exception error) { Problem = error.Message; }
            finally
            {
                running = false;
                if (capture != null) Marshal.ReleaseComObject(capture);
                if (client != null) Marshal.ReleaseComObject(client);
                if (format != IntPtr.Zero) Marshal.FreeHGlobal(format);
                if (variant != IntPtr.Zero) Marshal.FreeHGlobal(variant);
                if (parameters != IntPtr.Zero) Marshal.FreeHGlobal(parameters);
                ready.Close();
            }
        }

        private static void Check(int result, string what)
        {
            if (result != 0) throw new InvalidOperationException("Couldn't " + what + " Discord's sound (0x" + result.ToString("X8") + ").");
        }

        public void Dispose()
        {
            running = false;
            Thread was = thread;
            if (was != null && was != Thread.CurrentThread) was.Join(1000);
        }
    }
}
