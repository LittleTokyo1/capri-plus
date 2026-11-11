using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeScene(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.ScenesOn || !fresh.SceneAuto || fresh.NightOn || fresh.WorkSites == null) throw new Exception("Scenes should be on and automatic by default, out of night mode, with no work sites.");
            string close;
            Tunable scenesTune = Tuning.Find("scenes", "off", out close);
            Tunable autoTune = Tuning.Find("home scene by itself", "off", out close);
            if (scenesTune == null || scenesTune.Field != "ScenesOn" || autoTune == null || autoTune.Field != "SceneAuto") throw new Exception("Her setting tool can't find scenes or the home scene by itself by voice.");
            string[][] heard =
            {
                new[] { "let's get to work", "scenework" }, new[] { "hey capri lets get to work", "scenework" }, new[] { "work mode", "scenework" },
                new[] { "relax mode", "scenerelax" }, new[] { "pentest mode", "scenepentest" }, new[] { "hacking time", "scenepentest" },
                new[] { "undo the scene", "sceneundo" }, new[] { "i'm home", "home" }, new[] { "I'm back", "home" },
                new[] { "Come home.", "home" }, new[] { "Run the home scene.", "scenerelax" }, new[] { "Run the relax scene", "scenerelax" }, new[] { "start the work scene", "scenework" }, new[] { "put on relaxing mode", "scenerelax" }
            };
            if (Night.SceneNamed("home scene") != Scene.Relax || Night.SceneNamed("relax") != Scene.Relax || Night.SceneNamed("work mode") != Scene.Work || Night.SceneNamed("pentest") != Scene.Pentest || Night.SceneNamed("text my mum when I get home from work") != null)
                throw new Exception("A routine call naming a scene (18:30 \"Couldn't find that routine\") is not turned into that scene, or a real routine phrase is.");
            foreach (string[] one in heard) if (TweakFor(one[0]) != one[1]) throw new Exception("\"" + one[0] + "\" was not heard as " + one[1] + ": " + (TweakFor(one[0]) ?? "nothing"));
            foreach (string other in new[] { "work is hard today", "my pentest report is due friday", "is it time to work yet" })
            {
                string key = TweakFor(other);
                if (key != null && key.StartsWith("scene", StringComparison.Ordinal)) throw new Exception("\"" + other + "\" started a scene.");
            }

            SceneSpec relax = Scene.Find(Scene.Relax), work = Scene.Find(Scene.Work), pentest = Scene.Find(Scene.Pentest);
            string relaxPlan = string.Join(",", Scene.Plan(relax, null, null).ToArray());
            if (relaxPlan != "minimize,open:Spotify,open:Discord,open:Stremio,open:Opera GX,open:League of Legends,monitor1:Opera GX,music") throw new Exception("The home scene plan is wrong: " + relaxPlan);
            string workPlan = string.Join(",", Scene.Plan(work, "x.ps1", new[] { "https://example.org" }).ToArray());
            if (workPlan != "minimize,open:Spotify,open:Google Chrome,site:https://example.org,code,monitor1:Google Chrome,monitor2:" + Scene.CodeWindow + ",music") throw new Exception("The work scene plan is wrong: " + workPlan);
            if (string.Join(",", Scene.Plan(work, null, null).ToArray()).Contains("code")) throw new Exception("A Claude Code step came without the script.");
            string hackPlan = string.Join(",", Scene.Plan(pentest, "x.ps1", null).ToArray());
            if (hackPlan != "minimize,open:Spotify,open:Ollama,vm,model,code,monitor1:VirtualBox,monitor2:" + Scene.CodeWindow + ",music") throw new Exception("The pentest scene plan is wrong: " + hackPlan);
            string[] school = { "CoursePortal", "MySchool" };
            if (Scene.Sites(null, school) != school || Scene.Sites(new string[0], school) != school || Scene.Sites(new[] { "https://example.org" }, school)[0] != "https://example.org" || Scene.Sites(null, null).Length != 0) throw new Exception("The work scene does not fall back to his school sites, the same pages her study jobs use.");
            if (work.Volume != 30 || relax.Volume != 50 || pentest.Volume != 35) throw new Exception("Scene volumes are not 30, 50 and 35.");
            string[] lists = { "Gym Mix", "Lofi Study", "Phonk Nights" };
            if (Scene.Playlist(work, lists) != "Lofi Study" || Scene.Playlist(pentest, lists) != "Phonk Nights" || Scene.Playlist(relax, lists) != "Gym Mix" || Scene.Playlist(relax, new string[0]) != null) throw new Exception("Her playlist pick per scene is wrong.");

            Func<string, ToolArguments, string> keptDo = Scene.Do;
            Func<string> keptVm = Scene.StartVm, keptModel = Scene.WarmModel, keptScript = Scene.CodeScript;
            Func<string, string> keptCode = Scene.StartCode;
            Func<SceneSpec, string> keptMusic = Scene.Music;
            Action<int> keptSettle = Scene.Settle;
            Func<List<IntPtr>> keptWindows = Scene.Windows;
            Func<IntPtr, bool> keptMinimized = Scene.Minimized;
            Func<IntPtr, int, bool> keptShow = Scene.Show;
            Func<bool> keptWake = ScreenPower.Wake;
            Func<string, bool> keptRunning = Lockout.Running;
            ForkSettings keptSettings = ForkSettings.Current;
            bool keptQuiet = preferences.Quiet, keptListen = preferences.ListenCapri, keptShush = shushed;
            List<string> calls = new List<string>();
            HashSet<IntPtr> iconic = new HashSet<IntPtr>();
            try
            {
                Scene.Do = delegate(string tool, ToolArguments args) { calls.Add(tool + ":" + args.Name + (args.Monitor == null ? "" : "@" + args.Monitor)); return "ok"; };
                Scene.StartVm = delegate { calls.Add("vm"); return "no Kali VM"; };
                Scene.WarmModel = delegate { calls.Add("model"); return "kali loaded"; };
                Scene.StartCode = delegate(string script) { calls.Add("code"); return "Claude Code session started"; };
                Scene.CodeScript = delegate { return "x.ps1"; };
                Scene.Music = delegate(SceneSpec spec) { calls.Add("music " + spec.Volume); return "Playing"; };
                Scene.Settle = delegate { };
                Scene.Windows = delegate { calls.Add("windows"); return new List<IntPtr> { new IntPtr(1), new IntPtr(2), new IntPtr(3) }; };
                Scene.Minimized = delegate(IntPtr window) { return iconic.Contains(window); };
                Scene.Show = delegate(IntPtr window, int how) { calls.Add("show " + window + " " + how); if (how == 7) iconic.Add(window); else iconic.Remove(window); return true; };

                List<string> refused = Scene.Run(pentest, true, null);
                if (calls.Count != 0 || refused.Count != 1 || !refused[0].StartsWith("refused", StringComparison.Ordinal)) throw new Exception("A scene ran during a game.");
                iconic.Add(new IntPtr(2));
                List<string> done = Scene.Run(pentest, false, null);
                string order = string.Join(",", calls.ToArray());
                if (order != "windows,show 1 7,show 3 7,open_app:Spotify,open_app:Ollama,vm,model,code,app_window:VirtualBox@1,app_window:" + Scene.CodeWindow + "@2,music 35") throw new Exception("The pentest scene ran in the wrong order or minimized an already minimized window: " + order);
                string missed = Night.Misses(done);
                if (missed.IndexOf("no Kali VM", StringComparison.Ordinal) < 0 || missed.IndexOf("Ollama", StringComparison.Ordinal) >= 0) throw new Exception("What did not work is not picked out for her greeting: " + missed);
                calls.Clear();
                iconic.Remove(new IntPtr(1));
                if (Scene.Restore() != 1 || string.Join(",", calls.ToArray()) != "show 3 4") throw new Exception("Undo the scene touched a window it did not minimize or one he already brought back: " + string.Join(",", calls.ToArray()));
                if (Scene.Restore() != 0) throw new Exception("Undo the scene ran twice.");

                if (!Night.ShouldWake(true, 500, 30, 120, false)) throw new Exception("His touch after goodnight did not wake her.");
                if (Night.ShouldWake(true, 5000, 30, 120, false) || Night.ShouldWake(true, 500, 5, 120, false) || Night.ShouldWake(true, 500, 30, 30, false) || Night.ShouldWake(true, 500, 30, 120, true) || Night.ShouldWake(false, 500, 30, 120, false)) throw new Exception("She woke with no fresh input, from her own mouse nudge, right after goodnight, while busy or outside night mode.");
                if (!Night.ShouldScene(500, 30, false, false) || Night.ShouldScene(500, 30, true, false) || Night.ShouldScene(500, 5, false, false)) throw new Exception("The primed home scene rule is wrong.");
                if (!Night.Primes(6L * 60 * 1000) || Night.Primes(60L * 1000)) throw new Exception("His phone coming home primed the scene while he sat at the PC, or not after he was away.");

                ForkSettings on = new ForkSettings();
                on.Tidy();
                ForkSettings.Current = on;
                int woke = 0;
                ScreenPower.Wake = delegate { woke++; return true; };
                preferences.Quiet = false;
                preferences.ListenCapri = true;
                shushed = false;
                NightMute();
                if (!on.NightOn || !preferences.Quiet || preferences.ListenCapri || !shushed || on.Paused) throw new Exception("Night mode did not turn her voice and ears off, or paused her work.");
                if (!NightWake("smoke") || on.NightOn || preferences.Quiet || !preferences.ListenCapri || shushed || woke != 1) throw new Exception("Waking did not bring her voice, ears and screens back as they were.");
                if (NightWake("smoke")) throw new Exception("She woke twice.");
                on.VoiceOnStart = true;
                on.NightOn = true;
                if (StartVoiceMay(on, false, true)) throw new Exception("After a restart in night mode her voice chat started by itself.");
                on.NightOn = false;
                if (!StartVoiceMay(on, false, true)) throw new Exception("Voice chat at startup broke outside night mode.");
                on.ScenesOn = false;
                if (SceneStart(Scene.Relax).IndexOf("Scenes are off", StringComparison.Ordinal) < 0) throw new Exception("A scene ran with scenes off.");
                on.ScenesOn = true;
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                if (SceneStart(Scene.Work).IndexOf("Not during a game", StringComparison.Ordinal) < 0) throw new Exception("A scene started during a game.");
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                calls.Clear();
                string asked = SceneStart(Scene.Relax);
                System.Threading.Thread.Sleep(300);
                if (calls.Count != 0 || sceneRunning || !asked.StartsWith("Setting up", StringComparison.Ordinal)) throw new Exception("A scene ran its real steps inside a smoke test: " + string.Join(",", calls.ToArray()));
                if (!HomeAsked().StartsWith("Welcome back", StringComparison.Ordinal)) throw new Exception("I'm home lost its welcome back.");
            }
            finally
            {
                Scene.Do = keptDo;
                Scene.StartVm = keptVm;
                Scene.WarmModel = keptModel;
                Scene.CodeScript = keptScript;
                Scene.StartCode = keptCode;
                Scene.Music = keptMusic;
                Scene.Settle = keptSettle;
                Scene.Windows = keptWindows;
                Scene.Minimized = keptMinimized;
                Scene.Show = keptShow;
                ScreenPower.Wake = keptWake;
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                ForkSettings.Current = keptSettings;
                preferences.Quiet = keptQuiet;
                preferences.ListenCapri = keptListen;
                shushed = keptShush;
            }
            checks.Add("PASS scenes: on and automatic by default, found by voice; let's get to work, relax mode, pentest mode, I'm home and undo the scene are heard as whole short commands; each scene first minimizes only windows that were open, then opens its apps (home: Spotify, Discord, Stremio, Opera GX, League; work: Spotify, Chrome and his sites, a Claude Code session; pentest: Spotify, Ollama with his Kali model, the Kali VM, a Claude Code session), puts the browser or VM on monitor 1 and Claude Code on monitor 2, then her playlist pick at 30, 50 or 35%; undo puts back only what it minimized; never in a game or with scenes off; night mode keeps her working but turns voice and ears off, and his fresh touch (not her own nudge, not right after goodnight) or the pause key brings them back as they were with the screens on");
        }
    }
}
