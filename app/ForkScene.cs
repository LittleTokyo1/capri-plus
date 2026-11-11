using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class Night
    {
        internal const long FreshInput = 2000;
        internal const int NudgeSeconds = 10;
        internal const int SettleSeconds = 60;
        internal const double PrimeHours = 2;
        internal const long AwayForPrime = 5L * 60 * 1000;
        internal const string Line = "Goodnight, choom. Screens off, ears and voice off; I'll keep working quietly. Touch the mouse, press the pause key or say I'm home to wake me.";
        internal const string GameLine = "Goodnight, choom. A game's running, so I left your screens on. Ears and voice off; touch the mouse or press the pause key to wake me.";
        internal const string AskLine = "Welcome back, choom. Want the home scene? Say I'm home.";
        private static readonly Regex Missed = new Regex(@"\b(couldn'?t|can'?t|not found|isn'?t (installed|open)|no (kali|virtualbox|claude code)|failed|did not|didn'?t|not answering|not set)\b", RegexOptions.IgnoreCase);

        internal static bool ShouldWake(bool night, long idle, double sinceNudge, double sinceMuted, bool busy)
        {
            return night && idle < FreshInput && sinceNudge > NudgeSeconds && sinceMuted > SettleSeconds && !busy;
        }

        internal static bool ShouldScene(long idle, double sinceNudge, bool game, bool busy)
        {
            return idle < FreshInput && sinceNudge > NudgeSeconds && !game && !busy;
        }

        internal static bool Primes(long idle) { return idle >= AwayForPrime; }

        private static readonly Regex SceneWords = new Regex(@"\b(?:(?<relax>relax(?:ing)?|home|chill)|(?<work>work|study)|(?<pentest>pen ?test(?:ing)?|hack(?:ing|er)?))\b", RegexOptions.IgnoreCase);

        internal static string SceneNamed(string phrase)
        {
            string said = phrase ?? "";
            if (!Regex.IsMatch(said, @"\b(?:scene|mode)\b", RegexOptions.IgnoreCase) && said.Trim().Split(' ').Length > 2) return null;
            Match found = SceneWords.Match(said);
            if (!found.Success) return null;
            return found.Groups["relax"].Success ? Scene.Relax : found.Groups["work"].Success ? Scene.Work : Scene.Pentest;
        }

        internal static string Misses(List<string> done)
        {
            List<string> missed = new List<string>();
            if (done != null) foreach (string step in done) if (Missed.IsMatch(step)) missed.Add(step);
            return string.Join("; ", missed.ToArray());
        }
    }

    internal sealed partial class Companion
    {
        private DateTime nightMutedAt = DateTime.MinValue;
        private DateTime primedAt = DateTime.MinValue;
        private bool sceneRunning;

        private void NightMute()
        {
            ForkSettings settings = ForkSettings.Current;
            if (settings.NightOn) return;
            settings.NightOn = true;
            settings.NightQuietWas = preferences.Quiet;
            settings.NightListenWas = preferences.ListenCapri;
            settings.NightChatWas = chatting;
            settings.NightShushWas = shushed;
            preferences.Quiet = true;
            preferences.ListenCapri = false;
            shushed = true;
            voice.Stop();
            if (chatting) StopChat();
            if (!testing)
            {
                SavePreferences();
                ForkSettings.Save();
            }
            mainframe.Sync();
            nightMutedAt = DateTime.UtcNow;
            ActivityLog.Write("night", "ears and voice off", "jobs, Discord and the phone keep running");
        }

        internal bool NightWake(string why)
        {
            ForkSettings settings = ForkSettings.Current;
            if (!settings.NightOn) return false;
            settings.NightOn = false;
            preferences.Quiet = settings.NightQuietWas;
            preferences.ListenCapri = settings.NightListenWas;
            shushed = settings.NightShushWas;
            if (!testing)
            {
                SavePreferences();
                ForkSettings.Save();
            }
            mainframe.Sync();
            ScreenPower.On();
            if (settings.NightChatWas && !chatting && !testing) ToggleChat();
            ActivityLog.Write("night", "awake", why);
            return true;
        }

        private void ForkSceneTick()
        {
            if (testing) return;
            ForkSettings settings = ForkSettings.Current;
            long idle = Native.IdleMilliseconds();
            double sinceNudge = (DateTime.UtcNow - ScreenPower.NudgedAt).TotalSeconds;
            bool busy = working != null || sceneRunning;
            if (settings.NightOn)
            {
                if (!shushed) shushed = true;
                if (!Night.ShouldWake(true, idle, sinceNudge, (DateTime.UtcNow - nightMutedAt).TotalSeconds, busy)) return;
                NightWake("input at the PC");
                SceneBack();
                return;
            }
            if (primedAt == DateTime.MinValue) return;
            if ((DateTime.UtcNow - primedAt).TotalHours > Night.PrimeHours)
            {
                primedAt = DateTime.MinValue;
                return;
            }
            if (!Night.ShouldScene(idle, sinceNudge, Lockout.Active, busy)) return;
            primedAt = DateTime.MinValue;
            SceneBack();
        }

        private void ScenePrime()
        {
            if (!Night.Primes(Native.IdleMilliseconds())) return;
            primedAt = DateTime.UtcNow;
            ActivityLog.Write("scene", "primed", "his phone is home; the home scene runs at his first touch");
        }

        private void SceneBack()
        {
            ForkSettings settings = ForkSettings.Current;
            if (!settings.ScenesOn || Lockout.Active) return;
            if (!settings.SceneAuto)
            {
                Tell("(home)", Night.AskLine);
                return;
            }
            Tell("(home)", SceneStart(Scene.Relax));
        }

        private string HomeAsked()
        {
            string back = ComeHome();
            NightWake("he said he is home");
            primedAt = DateTime.MinValue;
            string scene = SceneStart(Scene.Relax);
            return scene.StartsWith("Setting up", StringComparison.Ordinal) ? "Welcome back. " + scene : back;
        }

        internal string SceneStart(string name)
        {
            SceneSpec spec = Scene.Find(name);
            ForkSettings settings = ForkSettings.Current;
            if (spec == null) return "I don't have that scene.";
            if (!settings.ScenesOn) return "Scenes are off in Capri+. Say scenes on and I'll set them up.";
            if (Lockout.Active) return "Not during a game, choom. Say it again after the match.";
            if (sceneRunning) return "Already setting a scene up.";
            if (testing) return "Setting up the " + spec.Called + ".";
            sceneRunning = true;
            KeyLights.Sweep(KeyLights.SceneColor(name));
            string[] sites = Scene.Sites(settings.WorkSites, SchoolSites.Names());
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> done = null;
                try { done = Scene.Run(spec, Lockout.Active, sites); }
                catch (Exception error) { Brain.Log("scene", -1, error.GetType().Name + ": " + error.Message); }
                Post(delegate
                {
                    sceneRunning = false;
                    SceneGreet(spec, done);
                });
            });
            return "Setting up the " + spec.Called + ".";
        }

        private string SceneUndo()
        {
            int back = Scene.Restore();
            return back == 0 ? "Nothing of mine to put back." : "Put back " + back + (back == 1 ? " window." : " windows.");
        }

        private void SceneGreet(SceneSpec spec, List<string> done)
        {
            if (testing || hidden || Lockout.Active) return;
            string move = Gesture.Pick(Mood.Now(DateTime.Now, false), random);
            Animate(move);
            string missed = Night.Misses(done);
            heard = "(scene)";
            Answer(++asking, "(The " + spec.Called + " just finished setting up his PC. Greet him in one short line of your own that fits this scene, the hour and his day." + (missed.Length > 0 ? " Briefly tell him what didn't work: " + missed + "." : "") + " Do nothing else.)", TalkState.Talking, chatting);
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox scenes, sceneAuto;

        private void SceneRows(ForkSettings current)
        {
            scenes = Check("Scenes: \"let's get to work\", \"I'm home\" and \"pentest mode\" tidy the open windows away, open that scene's apps on your monitors and start fitting music (never in a game; \"undo the scene\" brings the windows back)", current.ScenesOn);
            sceneAuto = Check("Home scene by itself: after goodnight, or when your phone comes home, your first touch at the PC wakes her and runs the home scene (off = she asks first)", current.SceneAuto);
        }

        private void FillScenes()
        {
            settings.ScenesOn = scenes.Checked;
            settings.SceneAuto = sceneAuto.Checked;
        }
    }
}
