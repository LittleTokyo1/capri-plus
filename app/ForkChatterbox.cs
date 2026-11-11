using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private Chatterbox chatterbox = new Chatterbox();

        private int intenseEvery = 1000;
        private int intenseAt;
        private bool intenseWas;
        private bool smokeVoice;

        private bool IntenseGame()
        {
            int now = Environment.TickCount;
            if (intenseAt != 0 && now - intenseAt < intenseEvery) return intenseWas;
            bool found = false;
            string[] names = ForkSettings.Current.IntenseGames;
            if (names != null)
                foreach (string name in names)
                    if (name.Length > 0 && Lockout.Running(name)) found = true;
            intenseAt = now == 0 ? 1 : now;
            intenseWas = found;
            return found;
        }

        private bool ClonedVoiceOn()
        {
            if (testing && !smokeVoice) return false;
            return ForkSettings.Current.VoiceEngine == 0 && !preferences.Silent && !(preferences.Quiet && !FreeChatOn) && Chatterbox.Present() && !FriendMode.Active;
        }

        private void ForkVoicePick(ref Func<string, byte[]> speak, ref Func<string, byte[]> free)
        {
            if (ForkSettings.Current.VoiceEngine == 2 && free != null && !preferences.Silent) speak = free;
            if (!ClonedVoiceOn()) return;
            ForkSettings settings = ForkSettings.Current;
            Func<string, byte[]> paid = speak, light = free;
            Chatterbox mine = chatterbox;
            Func<string, byte[]> cloned = delegate(string words)
            {
                if (HeavyNow())
                {
                    mine.Stop();
                    return settings.IntenseVoice == 2 ? null : Backup(paid ?? light, light, words);
                }
                if (mine.State == Chatterbox.Phase.Ready)
                {
                    try { return mine.Speak(words); }
                    catch (VoiceException error)
                    {
                        if (error.Message == Chatterbox.CutOff) return null;
                        Brain.Log("her cloned voice", -1, Voice.Short(error.Message));
                        if (HeavyGames.Starved(error.Message)) LearnHeavy(mine);
                    }
                }
                else mine.Warm();
                return Backup(paid ?? light, light, words);
            };
            speak = cloned;
            free = cloned;
        }

        private byte[] Backup(Func<string, byte[]> first, Func<string, byte[]> then, string words)
        {
            if (first == null) return null;
            try { return first(words); }
            catch (VoiceException error)
            {
                if (!Broke(error.Message) || then == null || then == first) throw;
                voiceBroke = true;
                return then(words);
            }
        }

        private void ForkWarmVoice()
        {
            if (testing || !ClonedVoiceOn() || HeavyNow()) return;
            ThreadPool.QueueUserWorkItem(delegate { chatterbox.Warm(); });
        }

        private bool HeavyNow() { return ForkSettings.Current.IntenseVoice != 0 && IntenseGame(); }

        private void LearnHeavy(Chatterbox mine)
        {
            ForkSettings settings = ForkSettings.Current;
            if (settings.IntenseVoice == 0) return;
            string heavy = HeavyGames.Learn(HeavyGames.Front(), settings.IntenseGames, DateTime.Now);
            if (heavy == null) return;
            settings.IntenseGames = HeavyGames.With(settings.IntenseGames, heavy);
            if (!testing) ForkSettings.Save();
            intenseAt = 0;
            mine.Stop();
            Brain.Log("her cloned voice", -1, "learned " + heavy + " is a heavy game: her cloned voice rests while it runs (Capri+ Very intense games)");
        }

        private void ForkVoiceTick()
        {
            if (testing) return;
            string why = VoiceRest();
            if (why != null) Brain.Log("her cloned voice", -1, "unloaded, " + why);
        }

        private string VoiceRest()
        {
            if (!chatterbox.Running) return null;
            if (FriendMode.Active) { chatterbox.Stop(); return "friends mode is on"; }
            if (!ClonedVoiceOn()) { chatterbox.Stop(); return "another voice is picked or her voice is off"; }
            if (HeavyNow()) { chatterbox.Stop(); return "a heavy game is running"; }
            chatterbox.Tick();
            return chatterbox.State == Chatterbox.Phase.Off ? "quiet for " + ForkSettings.Current.IdleMinutes + " minutes" : null;
        }

        internal const string ClonedVoiceName = "Her cloned voice (GPU)";

        internal bool ClonedPicked { get { return ForkSettings.Current.VoiceEngine == 0; } }

        internal bool ClonedReady { get { return (!testing || smokeVoice) && Chatterbox.Present(); } }

        internal bool ClonedShown { get { return ClonedPicked && ClonedReady && !preferences.Silent; } }

        internal bool FallbackEleven { get { return !preferences.Quiet && !preferences.ServerVoice; } }

        internal string FallbackName { get { return preferences.ServerVoice ? "your voice model" : preferences.Quiet ? "her free voice" : voice.Key != null ? ElevenLabs : ElevenLabs + " (no key yet)"; } }

        internal void PickClonedVoice()
        {
            if (!ClonedReady) return;
            if (preferences.Silent)
            {
                preferences.Silent = false;
                SavePreferences();
            }
            ForkVoiceChosen(0);
            mainframe.Sync();
            ShowVoice(false);
            if (!testing) Brain.Log("her cloned voice", -1, "picked in the Mainframe");
        }

        internal void ChooseFallback(bool eleven)
        {
            if (eleven) preferences.CloudVoice = ElevenLabs;
            preferences.Quiet = !eleven;
            preferences.ServerVoice = false;
            SavePreferences();
            mainframe.Sync();
            ShowVoice(false);
            if (!testing) Brain.Log("her cloned voice", -1, "fallback set to " + FallbackName);
        }

        private ForkSettingsForm forkForm;

        private void ForkFormVoice()
        {
            ForkSettingsForm form = forkForm;
            if (form != null && !form.IsDisposed) form.ShowVoice(ForkSettings.Current.VoiceEngine, FallbackEleven ? 0 : 1);
        }

        private void ForkVoiceSaved(ForkSettingsForm form)
        {
            if (form.EngineChanged || form.FallbackChanged) ForkVoiceFromSettings(form.EngineChanged, form.FallbackChanged);
            form.Voiced();
        }

        private void ForkVoiceFromSettings(bool engineChanged, bool fallbackChanged)
        {
            ForkSettings settings = ForkSettings.Current;
            if (engineChanged) preferences.Silent = false;
            if (engineChanged) mainframe.WantEleven(settings.VoiceEngine == 1);
            if (engineChanged && settings.VoiceEngine == 1) { preferences.CloudVoice = ElevenLabs; preferences.Quiet = false; preferences.ServerVoice = false; }
            else if (engineChanged && settings.VoiceEngine == 2) { preferences.Quiet = true; preferences.ServerVoice = false; }
            else if (fallbackChanged && settings.VoiceEngine == 0)
            {
                if (settings.Fallback == 0) preferences.CloudVoice = ElevenLabs;
                preferences.Quiet = settings.Fallback != 0;
                preferences.ServerVoice = false;
            }
            SavePreferences();
            mainframe.Sync();
            ShowVoice(false);
            if (chatterbox.Running && !ClonedVoiceOn())
            {
                chatterbox.Stop();
                if (!testing) Brain.Log("her cloned voice", -1, "unloaded, " + ForkSettingsForm.VoiceEngineNames[settings.VoiceEngine] + " picked in Capri+");
            }
            if (!testing) Brain.Log("voice", -1, "Capri+ set " + ForkSettingsForm.VoiceEngineNames[settings.VoiceEngine] + ", fallback " + FallbackName);
        }

        private void ForkVoiceChosen(int engine)
        {
            if (testing && !smokeVoice) return;
            ForkSettings settings = ForkSettings.Current;
            if (settings.VoiceEngine != engine)
            {
                settings.VoiceEngine = engine;
                if (!testing) ForkSettings.Save();
            }
            if (!chatterbox.Running || ClonedVoiceOn()) return;
            chatterbox.Stop();
            if (!testing) Brain.Log("her cloned voice", -1, "unloaded, " + ForkSettingsForm.VoiceEngineNames[engine] + " picked in the Mainframe");
        }
    }
}
