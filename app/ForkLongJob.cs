using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapriDesktop
{
    // CAPRI-055: his real mouse and keys (not her own injected input), seen through low-level hooks on their own thread while
    // a long job is running, so it pauses when he comes back and her reports go to Discord while he is away. Her SetCursorPos
    // never reaches a low-level hook (probed 2026-10-31) and her SendInput carries the injected flag.
    internal static class RealInput
    {
        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetMessage(out Message message, IntPtr window, uint first, uint last);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);

        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        {
            public IntPtr Window;
            public uint Id;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int X;
            public int Y;
        }

        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static HookProc mouseProc, keyProc;
        private static IntPtr mouseHook, keyHook;
        private static Thread pump;
        private static uint pumpId;
        private static long last, back;

        internal static long Now { get { return clock.ElapsedMilliseconds; } }
        internal static bool On { get { return pump != null; } }
        internal static long Idle { get { return Now - Interlocked.Read(ref last); } }

        internal static void Start(long idleMilliseconds)
        {
            if (pump != null) return;
            Interlocked.Exchange(ref last, Now - Math.Max(0, idleMilliseconds));
            Interlocked.Exchange(ref back, 0);
            mouseProc = Mouse;
            keyProc = Key;
            ManualResetEvent ready = new ManualResetEvent(false);
            pump = new Thread(delegate()
            {
                pumpId = GetCurrentThreadId();
                IntPtr module = GetModuleHandle(null);
                mouseHook = SetWindowsHookEx(14, mouseProc, module, 0);
                keyHook = SetWindowsHookEx(13, keyProc, module, 0);
                ready.Set();
                Message message;
                while (GetMessage(out message, IntPtr.Zero, 0, 0) > 0) { }
                if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
                if (keyHook != IntPtr.Zero) UnhookWindowsHookEx(keyHook);
                mouseHook = keyHook = IntPtr.Zero;
            });
            pump.IsBackground = true;
            pump.Name = "Capri input watch";
            pump.Start();
            ready.WaitOne(2000);
            ActivityLog.Write("long job", "input watch", mouseHook != IntPtr.Zero && keyHook != IntPtr.Zero ? "on" : "could not start");
        }

        internal static void Stop()
        {
            if (pump == null) return;
            PostThreadMessage(pumpId, 0x0012, IntPtr.Zero, IntPtr.Zero);
            pump = null;
        }

        private static IntPtr Mouse(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0) Seen(Marshal.ReadInt32(lParam, 12), false, 0);
            return CallNextHookEx(mouseHook, code, wParam, lParam);
        }

        private static IntPtr Key(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0) Seen(Marshal.ReadInt32(lParam, 8), true, Marshal.ReadInt32(lParam, 0));
            return CallNextHookEx(keyHook, code, wParam, lParam);
        }

        internal static void Seen(int flags, bool key, int vk)
        {
            if (!LongJob.Real(flags, key, vk)) return;
            long now = Now;
            if (LongJob.CameBack(Interlocked.Read(ref last), now)) Interlocked.Exchange(ref back, now);
            Interlocked.Exchange(ref last, now);
        }

        internal static bool TakeBack() { return Interlocked.Exchange(ref back, 0) != 0; }

        internal static void Settle() { Interlocked.Exchange(ref last, Now); Interlocked.Exchange(ref back, 0); }
    }

    internal sealed partial class Companion
    {
        private const int LongCheckSeconds = 2;
        private const long LongAwayMilliseconds = 600000;
        private DateTime longChecked = DateTime.MinValue;
        private DateTime longSince;
        private volatile bool longBusy;
        private volatile string longStopWhy;
        private bool longWrapped;
        private DateTime longWrappedAt;
        private bool longGamePaused;
        private Job longJob;
        private Job longHeld;
        private string longHeard;

        private void ForkLongTick()
        {
            if (testing) return;
            DateTime now = DateTime.Now;
            if ((now - longChecked).TotalSeconds < LongCheckSeconds) return;
            longChecked = now;
            LongWork work = LongJob.Current;
            bool watching = work != null && !work.Browser && work.State == LongJob.Running && !Lockout.Active;
            if (watching && !RealInput.On) RealInput.Start(Autonomy.Idle());
            if (!watching && RealInput.On && !longBusy) RealInput.Stop();
            if (work == null) return;
            if (longBusy) { LongWatch(work, now); return; }
            LongStep(work, now);
        }

        internal string LongStep(LongWork work, DateTime now)
        {
            if (LongJob.Over(work, now)) return LongFinish(LongJob.TimeUpReport(work));
            if (work.State == LongJob.Running && Lockout.Active && !work.Browser)
            {
                LongJob.Change(delegate(LongWork w) { w.State = LongJob.Paused; });
                longGamePaused = true;
                ActivityLog.Write("long job", "paused", "game: " + Lockout.Game);
                return "paused";
            }
            if (longGamePaused && !Lockout.Active)
            {
                longGamePaused = false;
                return LongSay("Your game's over. Your long job " + Voice.Quote(work.Task) + " is paused. Say carry on or stop.", false);
            }
            if (work.State == LongJob.Running && !work.Browser && RealInput.On && RealInput.TakeBack()) return LongPause(work, work.Progress ?? "");
            if (!LongJob.Due(work, now) || (Lockout.Active && !work.Browser)) return null;
            LongStart(work);
            return "started";
        }

        private string LongPause(LongWork work, string progress)
        {
            LongJob.Change(delegate(LongWork w) { w.State = LongJob.Paused; w.Progress = progress; });
            RealInput.Settle();
            ActivityLog.Write("long job", "paused", "he came back");
            return LongSay("Welcome back. I paused your long job " + Voice.Quote(work.Task) + "." + (progress.Length > 0 ? " So far: " + progress : "") + " Say carry on or stop.", false);
        }

        private void LongWatch(LongWork work, DateTime now)
        {
            Job running = longJob;
            if (running == null) return;
            string why = longStopWhy;
            if (why == null && Lockout.Active && !work.Browser) why = "game";
            if (why == null && !work.Browser && RealInput.TakeBack()) why = LongJob.Back;
            if (why == null && LongJob.Over(work, now)) why = LongJob.TimeUp;
            if (why == null && longWrapped && (now - longWrappedAt).TotalMinutes >= LongJob.WrapGraceMinutes) why = LongJob.PartEnd;
            if (why != null)
            {
                longStopWhy = why;
                if (!Computer.Interrupted) Computer.Interrupt(why);
                return;
            }
            if (!longWrapped && (now - longSince).TotalMinutes >= LongJob.ChunkMinutes)
            {
                longWrapped = true;
                longWrappedAt = now;
                running.Tell(LongJob.Wrap);
            }
        }

        private void LongStart(LongWork work)
        {
            Job made = longHeld ?? new Job(LongJob.Chunk(work), work.Task);
            if (longHeld == null) made.Background = Brain.Briefing;
            if (longHeld == null && work.Browser) Browsing(made);
            longHeld = null;
            longJob = made;
            longBusy = true;
            longWrapped = false;
            longStopWhy = null;
            longSince = DateTime.Now;
            LongJob.Strict = true;
            RealInput.TakeBack();
            ActivityLog.Write("long job", work.Mode + " part " + (work.Mode == "run" ? work.Chunks + 1 : work.Runs + 1), work.Task);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result = null;
                string outcome = "failed";
                try
                {
                    result = work.Browser ? BrowserRun(made) : Work(made);
                    outcome = made.Outcome ?? "failed";
                }
                catch (Exception error)
                {
                    result = "That didn't work: " + error.Message;
                    Brain.Log("long job", -1, error.GetType().Name + ": " + error.Message);
                }
                Post(delegate { LongAfter(made, outcome, result); });
            });
        }

        internal string LongAfter(Job made, string outcome, string result)
        {
            longBusy = false;
            longJob = null;
            LongJob.Strict = false;
            string why = longStopWhy;
            longStopWhy = null;
            if (job == made) job = null;
            LongWork work = LongJob.Current;
            ActivityLog.Write("long job", "part " + outcome, Pursuit.Shorter(result ?? "", 160));
            if (work == null) return null;
            if (outcome == "stopped")
            {
                string progress = LongJob.Trail(work.Progress, made.Steps);
                if (why == LongJob.TimeUp)
                {
                    LongJob.Change(delegate(LongWork w) { w.Progress = progress; });
                    return LongFinish(LongJob.TimeUpReport(LongJob.Current));
                }
                if (why == "game")
                {
                    LongJob.Change(delegate(LongWork w) { w.State = LongJob.Paused; w.Progress = progress; });
                    longGamePaused = true;
                    return "paused";
                }
                if (why == LongJob.Back) return LongPause(work, progress);
                if (why == LongJob.PartEnd) outcome = work.Mode == "run" ? "done" : "failed";
                else return LongFinish((result == Computer.Escaped ? "You pressed Esc, so I stopped the long job " : "I stopped the long job ") + Voice.Quote(work.Task) + "." + (progress.Length > 0 ? " Where I got to: " + progress : ""));
                result = work.Mode == "run" ? "SO FAR " + progress : LongJob.PartEnd;
            }
            if (outcome == "waiting") longHeld = made;
            string report = null;
            bool wrapped = longWrapped || why == LongJob.PartEnd;
            string ended = outcome, said = result;
            LongJob.Change(delegate(LongWork w) { report = LongJob.After(w, ended, said, made.Steps, wrapped, DateTime.Now); });
            LongWork after = LongJob.Current;
            if (after != null && after.State == "finished") return LongFinish(report);
            return report == null ? null : LongSay(report, true);
        }

        private string LongFinish(string report)
        {
            LongJob.End();
            longHeld = null;
            ActivityLog.Write("long job", "finished", Pursuit.Shorter(report ?? "", 160));
            return LongSay(report, true);
        }

        // At the PC a bubble; away (he said he's leaving, or no real input for ten minutes) a Discord DM, and the bubble if it can't be sent.
        private string LongSay(string text, bool phone)
        {
            if (string.IsNullOrEmpty(text)) return text;
            brain.Own("long job", text);
            if (testing) return text;
            bool away = ForkSettings.Current.HomeAway || (RealInput.On ? RealInput.Idle >= LongAwayMilliseconds : Delivery.NotHome());
            if (!phone || !away || !Delivery.Reachable())
            {
                Tell("(long job)", text);
                return text;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool sent = false;
                try { sent = Delivery.Send(text); }
                catch (Exception error) { Brain.Log("long job", -1, error.GetType().Name + ": " + error.Message); }
                Post(delegate
                {
                    ActivityLog.Write("long job", sent ? "sent to his DM" : "DM failed, told at the PC", Pursuit.Shorter(text, 160));
                    if (sent) Digest.Add("job", text, DateTime.Now);
                    else Tell("(long job)", text);
                });
            });
            return text;
        }

        // His words about the long job, before her brain: stop, carry on, yes or no to a held step, an answer, how it's going.
        private bool LongHeard(string text)
        {
            DateTime now = DateTime.Now;
            string act = LongJob.Heard(text, now);
            if (act == null) return false;
            LongWork work = LongJob.Current;
            string question = work == null ? "" : work.Question ?? "";
            string said;
            if (act == "stop answering") said = LongJob.Stop(true);
            else if (act == "status") said = LongJob.Status(now);
            else if (act == "stop")
            {
                bool busy = longBusy;
                said = LongJob.Stop(false);
                longHeld = null;
                if (busy) { longStopWhy = LongJob.StoppedByHim; Computer.Interrupt(LongJob.StoppedByHim); }
            }
            else
            {
                string answer = act == "carry on" ? null
                    : act == "yes" ? (longHeld != null ? null : "They said yes to your question " + Voice.Quote(question) + ", so do that now.")
                    : act == "no" ? "They said no to " + Voice.Quote(question) + ": don't do that. Do the rest of the job without it, or call done and say what is left."
                    : "You asked them " + Voice.Quote(question) + " and they answered " + Voice.Quote(text.Trim()) + ".";
                if (act == "no") longHeld = null;
                LongJob.Change(delegate(LongWork w) { w.State = LongJob.Running; w.NextRun = now.Ticks; w.Question = null; w.Answer = answer; });
                RealInput.Settle();
                said = act == "no" ? "Okay, I won't. Carrying on with the rest." : act == "answer" ? "Got it, carrying on." : "Carrying on with " + Voice.Quote(work.Task) + ".";
            }
            ActivityLog.Write("long job", "heard " + act, null);
            longHeard = said;
            if (testing) return true;
            bool discord = discordLine != null && (now - discordSince).TotalSeconds < 15;
            if (discord)
            {
                discordLine = null;
                ThreadPool.QueueUserWorkItem(delegate { Discord.Send(said); });
            }
            else Tell("(long job)", said);
            return true;
        }

        // Another mouse job (or a quick tool that becomes one) would wait behind a long job's part; it is told at once instead.
        private string LongBusy(string tool, ToolArguments args)
        {
            if (!longBusy || (tool != "use_computer" && (args == null || CursorJob(tool, args) == null))) return null;
            LongWork work = LongJob.Current;
            return "I'm in the middle of your long job" + (work == null ? "" : " " + Voice.Quote(work.Task)) + ". Say stop to end it, or ask me again after.";
        }
    }
}
