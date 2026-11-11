using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeGoodnight(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, int, bool> keptPower = Hands.Power;
            Func<string, bool> keptRunning = Lockout.Running;
            int keptWait = goodnightWait;
            Func<bool> keptWake = ScreenPower.Wake;
            bool keptQuiet = preferences.Quiet, keptListen = preferences.ListenCapri;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                foreach (string said in new[] { "goodnight", "good night capri", "night night", "hey capri goodnight please", "I'm going to bed", "time for bed", "go to sleep mode", "going to sleep mode", "im off to sleep" })
                    if (TweakFor(said) != "goodnight") throw new Exception("\"" + said + "\" was not heard as goodnight: " + (TweakFor(said) ?? "nothing"));
                foreach (string other in new[] { "goodnight moon is a great book", "tell him goodnight", "what time is bedtime", "is it time for bed yet", "sleep", "bedtime", "nini" })
                    if (TweakFor(other) == "goodnight") throw new Exception("\"" + other + "\" was taken as goodnight.");

                List<string> power = new List<string>();
                Hands.Power = delegate(string action, int seconds) { lock (power) power.Add(action + " " + seconds); return true; };
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                string tool;
                ToolArguments args;
                foreach (string said in new[] { "screens off", "turn my monitors off", "turn off my screens please", "switch the displays off" })
                    if (!Hands.Heard(said, out tool, out args) || tool != "power" || args.Action != "screens_off") throw new Exception("\"" + said + "\" did not become screens_off.");
                if (!Hands.Heard("turn off my pc", out tool, out args) || args.Action != "shut_down") throw new Exception("Turning the PC off no longer shuts it down.");
                if (!Hands.Valid("power", new ToolArguments { Action = "screens_off" })) throw new Exception("screens_off is not a valid power action.");
                power.Clear();
                string done = Hands.Do("power", new ToolArguments { Action = "screens_off" });
                if (!done.Contains("Screens off") || power.Count != 1 || power[0] != "screens_off 0") throw new Exception("The screens_off action did the wrong thing: " + done + " / " + string.Join(",", power.ToArray()));

                power.Clear();
                string timed = Hands.Do("power", new ToolArguments { Action = "screens_off", Minutes = "10" });
                if (!timed.Contains("right now") || power.Count != 0) throw new Exception("A timed screens off ran now: " + timed);
                if (!Hands.Valid("power", new ToolArguments { Action = "screens_off" }) || !Hands.Tools.Contains("screens_off")) throw new Exception("The brain's power tool does not offer screens_off.");
                goodnightWait = 0;
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                ForkSettings.Current.Paused = false;
                ScreenPower.Wake = delegate { return true; };
                string line = ApplyTweak("goodnight");
                PumpUntil(delegate { lock (power) return power.Count > 0 && ForkSettings.Current.NightOn; }, 3000);
                lock (power) if (power.Count != 1 || power[0] != "screens_off 0") throw new Exception("Goodnight did not turn the screens off, or did more: " + string.Join(",", power.ToArray()));
                if (ForkSettings.Current.Paused || !ForkSettings.Current.NightOn || !preferences.Quiet || preferences.ListenCapri || !line.Contains("Goodnight") || !line.Contains("pause key") || !line.Contains("I'm home")) throw new Exception("Goodnight did not go to night mode (voice and ears off, still working) or tell him how to wake her: " + line);
                foreach (string action in power) if (action.StartsWith("shut_down") || action.StartsWith("restart") || action.StartsWith("sleep") || action.StartsWith("lock") || action.StartsWith("sign_out")) throw new Exception("Goodnight started a shutdown, sleep or lock.");
                NightWake("smoke");

                power.Clear();
                ForkSettings.Current.Paused = false;
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                string game = ApplyTweak("goodnight");
                PumpUntil(delegate { return ForkSettings.Current.NightOn; }, 3000);
                System.Threading.Thread.Sleep(600);
                lock (power) if (power.Count != 0) throw new Exception("Goodnight turned the screens off during a game.");
                if (!game.Contains("A game's running") || !ForkSettings.Current.NightOn || ForkSettings.Current.Paused) throw new Exception("Goodnight during a game was wrong: " + game);
                NightWake("smoke");
            }
            finally
            {
                if (ForkSettings.Current.NightOn) NightWake("smoke");
                ScreenPower.Wake = keptWake;
                preferences.Quiet = keptQuiet;
                preferences.ListenCapri = keptListen;
                ForkSettings.Current = keptSettings;
                Hands.Power = keptPower;
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                goodnightWait = keptWait;
            }
            checks.Add("PASS goodnight mode: goodnight, night night, going to bed, time for bed and go to sleep mode (whole short commands only) make her say goodnight and go to night mode (voice and ears off, her jobs, Discord and the phone keep running, never paused) until his touch, the pause key or I'm home, and turn the screens off with the new screens_off power action after her line, never a shutdown, sleep, lock or sign out; screens off by itself works in plain words and shutting the PC down still does what it did; with a game running she leaves the screens on and says so");
        }
    }
}
