using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal const int PhoneWaitSeconds = 60;
        private const int PhoneCheckSeconds = 5;
        internal const string PhoneSlow = "That's taking me a while. Ask me again in a minute.";
        private PhoneServer phoneServer;
        private int phonePort, phoneFailedPort;
        private DateTime phoneCheckAt = DateTime.MinValue;
        private readonly object phoneGate = new object();
        private string phoneLine;
        private DateTime phoneSince = DateTime.MinValue;
        private int phoneTicket = -1;
        private string phoneAnswer;
        private readonly ManualResetEvent phoneDone = new ManualResetEvent(false);
        private const int PhoneVoiceWaitSeconds = 45;
        internal Func<string, byte[]> PhoneVoicePick;
        private int phoneTurn;
        private bool phoneWantsVoice;
        private byte[] phoneWav;
        private string phoneSaid;
        private readonly ManualResetEvent phoneWavDone = new ManualResetEvent(true);

        private void PhoneTick()
        {
            if (testing) return;
            DateTime now = DateTime.Now;
            if (Math.Abs((now - phoneCheckAt).TotalSeconds) < PhoneCheckSeconds) return;
            phoneCheckAt = now;
            PhoneSwitch(ForkSettings.Current);
        }

        internal void PhoneSwitch(ForkSettings settings)
        {
            bool wanted = settings != null && settings.PhoneOn;
            int port = settings == null ? 0 : settings.PhonePort;
            if (phoneServer != null && (!wanted || port != phonePort))
            {
                phoneServer.Stop();
                phoneServer = null;
            }
            if (!wanted) { phoneFailedPort = 0; return; }
            if (phoneServer != null || port == phoneFailedPort) return;
            PhoneServer.TailnetName = delegate { ForkSettings current = ForkSettings.Current; return current == null ? "" : current.PhoneTailnet ?? ""; };
            PhoneServer.Said = PhoneAsk;
            PhoneServer.Listened = PhoneListen;
            PhoneServer.Turn = delegate { lock (phoneGate) return phoneTurn; };
            PhoneServer.Audio = PhoneAudio;
            PhoneServer.State = PhoneState;
            PhoneServer.Pc = PhoneStatus;
            PhoneServer.ShotRefusal = PhoneShotRefusal;
            PhoneServer.Shot = PhoneShot;
            PhoneServer.Key = PhoneKey;
            PhoneServer.Settings = delegate { return PhoneButtons.Json(Tuning.All); };
            PhoneServer.Setting = delegate(string key, string value) { return PhoneButtons.Change(key, value, DateTime.Now); };
            PhoneServer.History = delegate { return PhoneHistory.Json(PhoneHistory.Lines(Lately.FolderNow, DateTime.Now, PhoneHistory.MostLines)); };
            PhoneShortcutsWire();
            PhoneServer made = new PhoneServer(port);
            string problem = made.Start();
            if (problem != null)
            {
                phoneFailedPort = port;
                ActivityLog.Write("phone", "server not started", problem);
                return;
            }
            phoneServer = made;
            phonePort = port;
        }

        private void PhoneAsked(string text, bool voice)
        {
            lock (phoneGate)
            {
                phoneAnswer = null;
                phoneDone.Reset();
                phoneLine = text;
                phoneSince = DateTime.Now;
                phoneTicket = -1;
                phoneTurn++;
                phoneWantsVoice = voice;
                phoneWav = null;
                phoneSaid = null;
                phoneWavDone.Reset();
            }
        }

        private string PhoneAsk(string text, bool voice)
        {
            lock (phoneGate)
            {
                if (phoneLine != null && (DateTime.Now - phoneSince).TotalSeconds < PhoneWaitSeconds) return null;
                PhoneAsked(text, voice);
            }
            if (testing) PhoneHeard(text);
            else Post(delegate { PhoneHeard(text); });
            bool answered = phoneDone.WaitOne(PhoneWaitSeconds * 1000);
            lock (phoneGate)
            {
                string answer = phoneAnswer;
                phoneLine = null;
                if (!answered || answer == null) phoneWavDone.Set();
                return answered && answer != null ? answer : PhoneSlow;
            }
        }

        private void PhoneHeard(string text)
        {
            ActivityLog.Write("phone", "message", null);
            ChatArchive.From("phone");
            typedLine = true;
            try { Hear(text, text, "> phone: "); }
            finally { typedLine = false; }
            lock (phoneGate) if (phoneLine != null && phoneAnswer == null) phoneTicket = asking;
        }

        private bool ForkPhoneAnswer(int ticket, ref string said, TalkState state)
        {
            bool wantsVoice;
            int turn;
            lock (phoneGate)
            {
                if (phoneLine == null || phoneAnswer != null) return false;
                if (ticket != asking || (phoneTicket != -1 && ticket != phoneTicket)) return false;
                phoneAnswer = said ?? "";
                wantsVoice = phoneWantsVoice;
                turn = phoneTurn;
            }
            Begin(ticket, said, state, null);
            if (wantsVoice) PhoneSpeak(said, turn);
            else phoneWavDone.Set();
            phoneDone.Set();
            return true;
        }

        private void PhoneSpeak(string said, int turn)
        {
            string words = Voice.Spoken((said ?? "").Replace(Computer.Dots, "the password"));
            Func<string, byte[]> free = PhoneVoicePick;
            Func<string, byte[]> speak = free ?? PickVoice(false, out free);
            if (speak != null && speak != free && VoiceSpent() && !ClonedVoiceOn()) speak = free;
            if (speak == null || words.Length == 0) { if (speak == null) ActivityLog.Write("phone", "no voice", preferences.Silent ? "silent" : "no voice picked"); phoneWavDone.Set(); return; }
            lock (phoneGate) phoneSaid = said;
            ThreadPool.QueueUserWorkItem(delegate
            {
                PhoneVoiceAwake();
                string problem;
                byte[] wav = Voiced(speak, free, words, out problem);
                if (problem != null) ActivityLog.Write("phone", "voice failed", problem);
                lock (phoneGate)
                {
                    if (turn != phoneTurn) return;
                    phoneWav = wav;
                }
                phoneWavDone.Set();
            });
        }

        private byte[] PhoneAudio(int turn)
        {
            lock (phoneGate) if (turn != phoneTurn) return null;
            phoneWavDone.WaitOne(PhoneVoiceWaitSeconds * 1000);
            byte[] wav;
            string said;
            lock (phoneGate)
            {
                if (turn != phoneTurn) return null;
                wav = phoneWav;
                said = phoneSaid;
            }
            if (wav != null && !testing) Post(delegate { PhoneMouth(said, wav); });
            return wav;
        }

        private void PhoneMouth(string said, byte[] wav)
        {
            if (IsDisposed || wav == null || string.IsNullOrEmpty(said)) return;
            Say(said);
            mouths = Voice.Mouths(wav, MouthRate);
        }

        private string PhoneState()
        {
            Clip shown = drawnClip ?? idle;
            int cell = Math.Max(0, drawnCell);
            return "{\"clip\":" + PhoneServer.Json(shown.Name) + ",\"cell\":" + cell + ",\"left\":" + (facingLeft ? "true" : "false") + ",\"mouth\":" + drawnMouth + ",\"jobs\":" + JobMarks.Json(DateTime.Now) + "}";
        }

        private string PhoneListen(short[] samples)
        {
            try
            {
                LocalEars chat = Hearing.Own;
                string which = PhoneMicTrace.Ears(chat != null && chat.Ready, parakeet.Ready);
                ActivityLog.Write("phone", "ears", which);
                if (which == PhoneMicTrace.ChatEars) return Hearing.Clean(chat.Hear(samples));
                if (which == PhoneMicTrace.WindowsEars) return Hearing.Text(samples);
                try { return Hearing.Clean(parakeet.Hear(samples)); }
                finally { if (Hearing.Own == null) parakeet.Dispose(); }
            }
            catch (Exception error)
            {
                ActivityLog.Write("phone", "ears failed", error.GetType().Name);
                return null;
            }
        }
    }
}
