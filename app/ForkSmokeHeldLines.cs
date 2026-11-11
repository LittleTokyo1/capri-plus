using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeHeldLines(List<string> checks)
        {
            TalkState keptState = talkState;
            double keptUntil = voiceUntil;
            var keptSpeech = speech;
            try
            {
                HoldInTests = true;
                heldLines.Clear();
                heardTick = 0;
                talkState = TalkState.Talking;
                int before = asking;
                Tell("(league)", "While you were away I accepted the match.");
                Remark("reminder", "Water the plants.");
                Tell("(league)", "While you were away I accepted the match.");
                if (HeldCount != 2 || asking != before) throw new Exception("A system line cut into her answer instead of waiting (his 2026-11-09 report: the queue line cut off what she was explaining).");
                talkState = TalkState.Off;
                voiceUntil = 0;
                HeldLinesTick();
                freeSince = Environment.TickCount - HeldGapMs - 50;
                HeldLinesTick();
                if (HeldCount != 1 || asking == before) throw new Exception("A held line was not said once she finished.");
                talkState = TalkState.Talking;
                heldLines.Clear();
                HeardNow();
                Tell("(your studies)", "Checking your drafts page first.");
                if (HeldCount != 0) throw new Exception("Her reply to what he just said was held back.");
                heardTick = 0;
                heldLines.Add(new HeldLine { Shown = "old", Line = "too late", At = DateTime.Now.AddSeconds(-HeldSeconds - 5) });
                HeldLinesTick();
                if (HeldCount != 0) throw new Exception("A line held for minutes was still waiting to be said.");
            }
            finally
            {
                HoldInTests = false;
                heldLines.Clear();
                heardTick = 0;
                freeSince = 0;
                talkState = keptState;
                voiceUntil = keptUntil;
                speech = keptSpeech;
            }
            checks.Add("PASS held lines: while she is talking or working on an answer, a system line (a League accept, a reminder, a study result) waits and is said once she has finished, the same line is never queued twice, her reply to what he just said still comes at once, and a line held for over two minutes is dropped");
        }
    }
}
