using System;
using System.Drawing;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int PetMissGap = 30000;
        private long petMissAt = -PetMissGap;

        private bool ForkPetFree()
        {
            return PetInChat(talkState, speech != null || speaker.Busy, transcribing);
        }

        internal static bool PetInChat(TalkState state, bool speaking, bool hearing)
        {
            return state != TalkState.Thinking && state != TalkState.Talking && !speaking && !hearing;
        }

        private void ForkPetted()
        {
            ActivityLog.Write("pet", "patted", InChat ? "voice chat on, she smiled, her line comes if he isn't talking" :"she smiled, her line comes next");
        }

        private bool ForkPetLineInChat(int ticket)
        {
            if (!InChat) return false;
            if (ticket != petTicket || hidden || typer.Visible) return true;
            bool linked = local || (agent != null && agent.Ready);
            string held = PetLineHeld(preferences.Silent || preferences.Quiet, HisTurn(), ForkPetFree() && working == null, voiceUntil > clock.Elapsed.TotalSeconds, linked);
            if (held != null) { ActivityLog.Write("pet", "line held", held); return true; }
            string line = ForkPetLine();
            heard = "pets you";
            if (!local)
            {
                dropping = repeating = false;
                stirred = Environment.TickCount;
                agent.Say("(Say this word for word: " + Voice.Quote(line) + ")");
            }
            else
            {
                box.Prompt = "> sys: ";
                Answer(++asking, line, TalkState.Talking);
            }
            ActivityLog.Write("pet", "line in voice chat", line);
            return true;
        }

        internal static string PetLineHeld(bool voiceOff, bool hisTurn, bool free, bool voiceBusy, bool linked)
        {
            if (voiceOff) return "her voice is off";
            if (!linked) return "voice chat is not connected";
            if (hisTurn) return "he is talking";
            if (!free || voiceBusy) return "she is busy";
            return null;
        }

        private void ForkPetMiss(Point cursor)
        {
            string why = PetMissReason();
            if (why == null || frame == null || !Solid(PointToClient(cursor))) return;
            long now = clock.ElapsedMilliseconds;
            if (now - petMissAt < PetMissGap) return;
            petMissAt = now;
            ActivityLog.Write("pet", "not counted", why);
        }

        private string PetMissReason()
        {
            if (pressed || dragging) return null;
            if (preferences.NoHeadPats) return "head pats are off in Mainframe";
            if (riding) return "she rides the cursor";
            if (hidden) return "she is hidden";
            if (working != null) return "a PC job is running";
            if (InChat && !ForkPetFree()) return "voice chat: she is " + (transcribing ? "hearing him" : speech != null || speaker.Busy ? "speaking" : talkState == TalkState.Thinking ? "thinking" : "talking");
            return null;
        }
    }
}
