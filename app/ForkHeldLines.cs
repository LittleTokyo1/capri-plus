using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed class HeldLine
    {
        internal bool Remark;
        internal string Shown;
        internal string Line;
        internal bool BubbleOnly;
        internal byte[] Prepared;
        internal bool Ready;
        internal DateTime At;
    }

    internal sealed partial class Companion
    {
        internal const int HeldMost = 6;
        internal const int HeldSeconds = 120;
        internal const int ReplyWindowMs = 1500;
        internal const int HeldGapMs = 700;
        private readonly List<HeldLine> heldLines = new List<HeldLine>();
        private int heardTick;
        private int freeSince;
        private bool releasingHeld;
        internal bool HoldInTests;

        internal int HeldCount { get { return heldLines.Count; } }

        private void HeardNow() { heardTick = Environment.TickCount == 0 ? 1 : Environment.TickCount; }

        private bool ReplyingToHim { get { return heardTick != 0 && Environment.TickCount - heardTick < ReplyWindowMs; } }

        private bool HoldLine(bool remark, string shown, string line, bool bubbleOnly, byte[] prepared, bool ready)
        {
            if (testing && !HoldInTests) return false;
            if (releasingHeld || ReplyingToHim || MidJob(job) || !LineBusy || string.IsNullOrWhiteSpace(line)) return false;
            foreach (HeldLine one in heldLines) if (one.Line == line) return true;
            if (heldLines.Count >= HeldMost) heldLines.RemoveAt(0);
            heldLines.Add(new HeldLine { Remark = remark, Shown = shown, Line = line, BubbleOnly = bubbleOnly, Prepared = prepared, Ready = ready, At = DateTime.Now });
            ActivityLog.Write("line held", shown ?? "", "she was talking: " + Pursuit.Shorter(line, 60));
            return true;
        }

        private void HeldLinesTick()
        {
            if (heldLines.Count == 0) { freeSince = 0; return; }
            DateTime now = DateTime.Now;
            heldLines.RemoveAll(delegate(HeldLine one)
            {
                bool old = (now - one.At).TotalSeconds > HeldSeconds;
                if (old) ActivityLog.Write("line held", one.Shown ?? "", "dropped, too old: " + Pursuit.Shorter(one.Line, 60));
                return old;
            });
            if (heldLines.Count == 0) return;
            if (LineBusy) { freeSince = 0; return; }
            int tick = Environment.TickCount == 0 ? 1 : Environment.TickCount;
            if (freeSince == 0) { freeSince = tick; return; }
            if (tick - freeSince < HeldGapMs) return;
            HeldLine next = heldLines[0];
            heldLines.RemoveAt(0);
            freeSince = 0;
            releasingHeld = true;
            try
            {
                if (next.Remark) Remark(next.Shown, next.Line, next.BubbleOnly, next.Prepared, next.Ready);
                else Tell(next.Shown, next.Line);
            }
            finally { releasingHeld = false; }
        }
    }
}
