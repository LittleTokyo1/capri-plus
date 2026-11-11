using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokePet(List<string> checks, string folder)
        {
            if (!PetInChat(TalkState.Off, false, false) || !PetInChat(TalkState.Online, false, false) || !PetInChat(TalkState.Dormant, false, false) || PetInChat(TalkState.Thinking, false, false) || PetInChat(TalkState.Talking, false, false) || PetInChat(TalkState.Online, true, false) || PetInChat(TalkState.Online, false, true))
                throw new Exception("Petting in voice chat was read wrong: a reply left on screen should not block it, her speaking, thinking or hearing him should.");
            if (PetLineHeld(false, false, true, false, true) != null || PetLineHeld(false, true, true, false, true) != "he is talking" || PetLineHeld(false, false, false, false, true) != "she is busy" || PetLineHeld(false, false, true, true, true) != "she is busy" || PetLineHeld(true, false, true, false, true) != "her voice is off" || PetLineHeld(false, false, true, false, false) != "voice chat is not connected")
                throw new Exception("The pat line in voice chat was held at the wrong time.");
            Point spot = PetSpot();
            if (spot.IsEmpty) throw new Exception("Couldn't find her body to pet in voice chat.");
            Point left = PointToScreen(new Point(spot.X - 20, spot.Y));
            Point right = PointToScreen(new Point(spot.X + 20, spot.Y));
            string keptLog = ActivityLog.FilePath;
            string log = Path.Combine(folder, "pet-activity.log");
            bool chatWas = chatting, localWas = local;
            TalkState stateWas = talkState;
            bool listenWas = preferences.ListenCapri;
            int micWas = micTalked;
            bool heldOverHim = false;
            for (int wait = 0; wait < 300 && speaker.Busy; wait++) { Application.DoEvents(); Thread.Sleep(10); }
            int before = petTicket;
            bool afterReply, whileTalking;
            try
            {
                ActivityLog.FilePath = log;
                chatting = true;
                local = false;
                speech = null;
                transcribing = false;
                talkState = TalkState.Online;
                PetRub(left, right);
                afterReply = petTicket == before + 1;
                talkState = TalkState.Talking;
                PetRub(left, right);
                whileTalking = petTicket == before + 1;
                local = true;
                preferences.ListenCapri = false;
                talkState = TalkState.Online;
                micTalked = Environment.TickCount;
                int askedBefore = asking;
                heldOverHim = ForkPetLineInChat(petTicket) && asking == askedBefore;
            }
            finally
            {
                ActivityLog.FilePath = keptLog;
                chatting = chatWas;
                local = localWas;
                talkState = stateWas;
                preferences.ListenCapri = listenWas;
                micTalked = micWas;
                petTicket++;
                petAt = -60000;
                petMissAt = -PetMissGap;
                petTurns.Clear();
                petWay = 0;
                Play(seat != null ? perch : idle);
            }
            string lines = File.Exists(log) ? File.ReadAllText(log) : "";
            if (!afterReply || !whileTalking) throw new Exception("Petting in voice chat went wrong: with her reply still on screen " + afterReply + ", held off while she talks " + whileTalking + ".");
            if (!lines.Contains("  pet  patted  ->  voice chat on, she smiled") || !lines.Contains("  pet  not counted  ->  voice chat: she is ")) throw new Exception("Petting did not write its activity lines: " + lines);
            if (!heldOverHim || !lines.Contains("  pet  line held  ->  he is talking")) throw new Exception("Her pat line in voice chat was not held while he talked: " + heldOverHim + ", " + lines);
            checks.Add("PASS petting in voice chat: a rub over her counts while her last reply is still on screen, not while she speaks, thinks or hears him, and each pat or refused rub writes a pet line to the activity log");
            checks.Add("PASS pat line in voice chat: after a pat she says her line in voice chat when he isn't talking and she isn't busy, held (and logged) while he talks, she is busy, her voice is off or the link is down");
        }

        private void PetRub(Point left, Point right)
        {
            petAt = -60000;
            petMissAt = -PetMissGap;
            petTurns.Clear();
            petWay = 0;
            for (int i = 0; i < 10; i++) Stroke(i % 2 == 0 ? right : left);
        }

        private Point PetSpot()
        {
            if (frame == null) return Point.Empty;
            for (int y = frame.Height / 4; y < frame.Height * 3 / 4; y += 3)
                for (int x = 30; x < frame.Width - 30; x += 3)
                    if (Solid(new Point(x - 20, y)) && Solid(new Point(x + 20, y))) return new Point(x, y);
            return Point.Empty;
        }
    }
}
