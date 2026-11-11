using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool MurmurVoice(out Func<string, byte[]> speak, out Func<string, byte[]> free)
        {
            free = preferences.Quiet && !FreeChatOn ? null : FreeSpeak ?? (!testing && kokoro.Installed ? new Func<string, byte[]>(kokoro.Speak) : null);
            bool spent = VoiceSpent();
            speak = preferences.Quiet || voiceBroke || spent ? null : Speak;
            if (speak == null && !voiceBroke && !spent && !testing && voice.Ready && !preferences.Quiet) speak = voice.Speak;
            if (speak == null && (voice.Key == null || voiceBroke || spent || preferences.Quiet)) speak = free;
            ForkVoicePick(ref speak, ref free);
            return speak != null && !preferences.Silent;
        }

        private GameNote gameNote;
        internal string GameNoted;

        private void GameNoteSay(string line)
        {
            GameNoted = line;
            if (testing) return;
            if (gameNote == null) gameNote = new GameNote();
            gameNote.Set(line, clock.Elapsed.TotalSeconds);
            GameNoteDraw();
        }

        private void GameNoteTick()
        {
            if (gameNote == null || !gameNote.Live) return;
            if (gameNote.Expired(clock.Elapsed.TotalSeconds) || ForkSettings.Current.Paused) { gameNote.Stop(); return; }
            GameNoteDraw();
        }

        private void GameNoteDraw()
        {
            if (gameNote == null || !gameNote.Live || IsDisposed) return;
            Point head = Head();
            gameNote.Update(TalkScale(), head, Screen.FromPoint(head).WorkingArea);
        }

        private bool ChatIdle()
        {
            return !transcribing && !speaker.Busy && loudRun == 0;
        }

        private bool MayMurmur(ForkSettings settings)
        {
            Func<string, byte[]> speak, free;
            return Lockout.Active && settings.AutonomyOn && settings.Proactive >= 1 && !settings.Paused && !Autonomy.Silent(settings, DateTime.Now) && !typer.Visible && !dragging && speech == null && ChatIdle() && (talkState == TalkState.Off || talkState == TalkState.Online) && MurmurVoice(out speak, out free);
        }

        private void Murmur(string line, DateTime now, bool game = true)
        {
            Func<string, byte[]> speak, free;
            if (!MurmurVoice(out speak, out free)) return;
            string words = Voice.Spoken(line);
            if (words.Length == 0) return;
            if (game) brain.Own("heartbeat", line);
            if (game) GameNoteSay(line);
            ActivityLog.Write("autonomy", game ? "told by voice in a game, with a note" : "said aloud", line);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string problem;
                byte[] wav = Voiced(speak, free, words, out problem);
                Post(delegate
                {
                    if (wav == null || IsDisposed || testing) return;
                    if (game) Autonomy.Delivered(line, now);
                    if (game && gameNote != null && gameNote.Text == line) gameNote.Spoken(clock.Elapsed.TotalSeconds, Voice.Seconds(wav));
                    voiceUntil = clock.Elapsed.TotalSeconds + Voice.Seconds(wav) + 0.6;
                    CheckEars(clock.Elapsed.TotalSeconds);
                    voice.Play(wav);
                });
            });
        }
    }
}
