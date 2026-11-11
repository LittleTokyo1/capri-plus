using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class WindowUse
    {
        internal int Gdi = -1, User = -1, Handles = -1;
        internal long Private = -1, Working = -1;

        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);

        internal static WindowUse Read()
        {
            WindowUse use = new WindowUse();
            try
            {
                using (Process own = Process.GetCurrentProcess())
                {
                    use.Gdi = GetGuiResources(own.Handle, 0);
                    use.User = GetGuiResources(own.Handle, 1);
                    use.Handles = own.HandleCount;
                    use.Private = own.PrivateMemorySize64;
                    use.Working = own.WorkingSet64;
                }
            }
            catch (Exception) { }
            return use;
        }

        public override string ToString()
        {
            return "GDI " + Gdi + ", USER " + User + ", handles " + Handles + ", private " + (Private < 0 ? -1 : Private >> 20) + " MB, working set " + (Working < 0 ? -1 : Working >> 20) + " MB";
        }
    }

    internal sealed class FreezeWatch
    {
        internal int StallAfter = 2000, StallSpacing = 60000, LogAfter = 10000;
        internal Func<string> Doing;
        internal Func<WindowUse> Use = WindowUse.Read;
        internal Action<string> Note = delegate(string line) { Brain.Log("frozen", -1, line); };
        private readonly Control window;
        private readonly Thread ui;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long sent, answered, sentAt;
        private volatile bool halted;

        internal FreezeWatch(Control window)
        {
            this.window = window;
            ui = Thread.CurrentThread;
        }

        internal void Start()
        {
            Thread beat = new Thread(Beat);
            beat.IsBackground = true;
            beat.Name = "Capri freeze watch";
            beat.Start();
        }

        internal void Halt() { halted = true; }

        private void Ping(long now)
        {
            long ticket = Interlocked.Increment(ref sent);
            Interlocked.Exchange(ref sentAt, now);
            try
            {
                if (window.IsDisposed || !window.IsHandleCreated) { Interlocked.Exchange(ref answered, ticket); return; }
                window.BeginInvoke((MethodInvoker)delegate { Interlocked.Exchange(ref answered, ticket); });
            }
            catch (Exception) { Interlocked.Exchange(ref answered, ticket); }
        }

        private void Beat()
        {
            long frozeAt = -1, stalledAt = -1, stallNoted = -StallSpacing;
            string stall = null;
            Ping(clock.ElapsedMilliseconds);
            while (!halted)
            {
                Thread.Sleep(250);
                if (halted) return;
                long now = clock.ElapsedMilliseconds;
                if (Interlocked.Read(ref answered) == Interlocked.Read(ref sent))
                {
                    if (frozeAt >= 0) Noted("Her window answered again after " + (now - frozeAt) / 1000 + " s.");
                    else if (stall != null) Noted("Her window didn't answer for " + ((now - stalledAt) / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s" + stall);
                    frozeAt = stalledAt = -1;
                    stall = null;
                    Ping(now);
                    continue;
                }
                long stuck = now - Interlocked.Read(ref sentAt);
                if (stuck >= LogAfter && frozeAt < 0)
                {
                    frozeAt = Interlocked.Read(ref sentAt);
                    Noted("Her window stopped answering for " + stuck / 1000 + " s" + Said() + Where(ui));
                }
                if (stuck >= StallAfter && stalledAt < 0 && frozeAt < 0)
                {
                    stalledAt = Interlocked.Read(ref sentAt);
                    if (now - stallNoted >= StallSpacing) { stallNoted = now; stall = Said() + Where(ui); }
                }
            }
        }

        private void Noted(string line)
        {
            try { Note(line); }
            catch (Exception) { }
        }

        private string Said()
        {
            Func<string> doing = Doing;
            string what = "";
            try { what = doing == null ? "" : doing() ?? ""; }
            catch (Exception) { }
            return (what.Length == 0 ? ". " : ", " + what + ". ") + Use() + ".";
        }

        internal static string Where(Thread ui)
        {
            StackTrace trace = null;
            Thread reader = new Thread(delegate()
            {
                bool paused = false;
#pragma warning disable 618
                try
                {
                    ui.Suspend();
                    paused = true;
                    trace = new StackTrace(ui, false);
                }
                catch (Exception) { }
                finally { if (paused) { try { ui.Resume(); } catch (Exception) { } } }
#pragma warning restore 618
            });
            reader.IsBackground = true;
            reader.Priority = ThreadPriority.Highest;
            reader.Start();
            if (!reader.Join(3000))
            {
#pragma warning disable 618
                try { if ((ui.ThreadState & System.Threading.ThreadState.Suspended) != 0) ui.Resume(); }
                catch (Exception) { }
#pragma warning restore 618
                return "";
            }
            if (trace == null) return "";
            List<string> frames = new List<string>();
            int ours = 0;
            for (int i = 0; i < trace.FrameCount && ours < 4; i++)
            {
                MethodBase method = trace.GetFrame(i).GetMethod();
                if (method == null) continue;
                Type type = method.DeclaringType;
                bool mine = type != null && type.Namespace == typeof(FreezeWatch).Namespace;
                if (!mine && i >= 2) continue;
                if (mine) ours++;
                frames.Add((type == null ? "" : type.Name + ".") + method.Name);
            }
            return frames.Count == 0 ? "" : " Stuck at: " + string.Join(" < ", frames.ToArray());
        }
    }

    internal sealed partial class Companion
    {
        private FreezeWatch freezeWatch;

        private void StartFreezeWatch()
        {
            if (freezeWatch != null) return;
            freezeWatch = new FreezeWatch(this);
            freezeWatch.Doing = FreezeDoing;
            freezeWatch.Start();
        }

        private string FreezeDoing()
        {
            List<string> parts = new List<string>();
            Job running = job;
            if (running != null) parts.Add(running.Playing ? "playing a game" : "on a job");
            if (chatting) parts.Add(local ? "free voice chat on" : "voice chat on");
            if (pushing) parts.Add("push to talk held");
            if (hidden) parts.Add("hidden");
            if (quitting) parts.Add("quitting");
            return string.Join(", ", parts.ToArray());
        }
    }
}
