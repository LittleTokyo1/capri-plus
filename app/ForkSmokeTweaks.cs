using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    // Capri+ smoke checks for settings by voice or text, apps that are really websites, and voice turns.
    internal sealed partial class Companion
    {
        private void SmokeTweaks(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            bool keptWander = preferences.Wander, keptWatch = preferences.Watch, keptCheckIns = preferences.CheckIns;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                string[,] heard =
                {
                    { "stay still", "still" }, { "Capri, stop moving.", "still" }, { "don't move", "still" }, { "hey capri, please stop wandering around", "still" },
                    { "you can move around again", "wander" }, { "can you walk around again please", "wander" },
                    { "be quiet", "quiet" }, { "Key moments only", "moments" }, { "be chatty", "chatty" },
                    { "stop watching my screen", "nolook" }, { "watch my screen", "look" }, { "Japanese off", "jpoff" }, { "turn Japanese on", "jpon" }
                };
                for (int i = 0; i < heard.GetLength(0); i++)
                    if (TweakFor(heard[i, 0]) != heard[i, 1]) throw new Exception("\"" + heard[i, 0] + "\" was heard as " + (TweakFor(heard[i, 0]) ?? "nothing") + ", not " + heard[i, 1] + ".");
                foreach (string other in new[] { "is this anime in Japanese on Netflix?", "tell him to be quiet", "can you watch my screen and tell me what you see", "stop talking to him", "what's the best build for Zed", "", "push build 2" })
                    if (TweakFor(other) != null) throw new Exception("\"" + other + "\" was taken as a settings command: " + TweakFor(other));
                if (TweakHeard("what's the weather", true) || ForkSettings.Current.Proactive != 1) throw new Exception("An ordinary sentence changed a setting.");
                if (!TweakHeard("be quiet", true) || ForkSettings.Current.Proactive != 0 || preferences.CheckIns) throw new Exception("Quiet mode wasn't set.");
                if (!TweakHeard("be chatty", true) || ForkSettings.Current.Proactive != 2 || !preferences.CheckIns) throw new Exception("Chatty mode wasn't set or didn't turn check-ins on.");
                if (!ForkHeard("key moments only", true) || ForkSettings.Current.Proactive != 1 || preferences.CheckIns) throw new Exception("Key moments weren't set through ForkHeard.");
                TweakHeard("stop watching my screen", true);
                if (ForkSettings.Current.DesktopVision) throw new Exception("Watching the screen wasn't switched off.");
                preferences.Watch = false;
                if (!ApplyTweak("look").Contains("Mainframe") || !ForkSettings.Current.DesktopVision) throw new Exception("Watching the screen wasn't switched on, or the Mainframe switch wasn't mentioned.");
                preferences.Watch = true;
                if (ApplyTweak("look").Contains("Mainframe")) throw new Exception("A needless Mainframe warning.");
                TweakHeard("Japanese off", true);
                if (ForkSettings.Current.Japanese != 0) throw new Exception("Japanese wasn't switched off.");
                TweakHeard("Japanese on", true);
                if (ForkSettings.Current.Japanese != 1) throw new Exception("Japanese wasn't switched on.");
                ForkSettings.Current.Japanese = 2;
                TweakHeard("Japanese on", true);
                if (ForkSettings.Current.Japanese != 2) throw new Exception("Japanese on lowered the level.");
                if (preferences.Wander) ToggleWander();
                TweakHeard("you can move around again", true);
                if (!preferences.Wander) throw new Exception("She wasn't allowed to wander.");
                TweakHeard("stay still", true);
                if (preferences.Wander) throw new Exception("She didn't stay still.");
            }
            finally
            {
                if (preferences.Wander != keptWander) ToggleWander();
                wanderWas = false;
                preferences.Watch = keptWatch;
                preferences.CheckIns = keptCheckIns;
                ForkSettings.Current = keptSettings;
            }
            checks.Add("PASS settings by voice or text: stay still / you can move, be quiet / key moments / be chatty, stop watching / watch my screen and Japanese off / on change the setting before her brain hears it (check-ins follow chattiness, Japanese on never lowers a level), while longer sentences that only mention them are left alone");
        }

        private void SmokeWebApps(List<string> checks)
        {
            Func<string, ToolArguments, string> keptDo = HandsDo;
            Func<string, bool> keptRunning = Lockout.Running;
            ForkSettings keptSettings = ForkSettings.Current;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                string[,] sites = { { "Netflix", "netflix.com" }, { "Disney+", "disneyplus.com" }, { "Prime Video", "primevideo.com" }, { "HBO Max", "max.com" }, { "Spotify app", "open.spotify.com" }, { "ChatGPT", "chatgpt.com" }, { "Gmail", "mail.google.com" }, { "Crunchyroll", "crunchyroll.com" }, { "Twitch", "twitch.tv" } };
                for (int i = 0; i < sites.GetLength(0); i++)
                    if (WebApp(sites[i, 0]) != sites[i, 1]) throw new Exception(sites[i, 0] + " mapped to " + WebApp(sites[i, 0]));
                if (WebApp("Discord") != null || WebApp("Zorblax") != null || WebApp(null) != null || WebApp("") != null) throw new Exception("An ordinary app was taken for a website.");
                List<string> calls = new List<string>();
                bool installed = false;
                HandsDo = delegate(string tool, ToolArguments args)
                {
                    calls.Add(tool + ":" + (tool == "open_website" ? args.Site : args.Name));
                    if (tool == "open_website") return "Opened " + args.Site + ".";
                    return installed ? "Opened " + args.Name + "." : "No app called " + args.Name + " is installed on this PC.";
                };
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                string opened = ForkTool("open_app", new ToolArguments { Name = "Netflix" });
                if (opened != "Opened netflix.com." || string.Join("|", calls.ToArray()) != "open_app:Netflix|open_website:netflix.com") throw new Exception("An uninstalled Netflix wasn't opened as a website: " + opened + " / " + string.Join("|", calls.ToArray()));
                calls.Clear();
                installed = true;
                if (ForkTool("open_app", new ToolArguments { Name = "Netflix" }) != "Opened Netflix." || calls.Count != 1) throw new Exception("An installed Netflix was opened as a website.");
                calls.Clear();
                if (ForkTool("open_app", new ToolArguments { Name = "Zorblax" }) != null || calls.Count != 0) throw new Exception("An ordinary app was intercepted.");
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                string midgame = ForkTool("open_app", new ToolArguments { Name = "Netflix" });
                if (midgame == Lockout.Refusal || calls.Count == 0) throw new Exception("A web app was refused mid-game: " + midgame);
            }
            finally
            {
                HandsDo = keptDo;
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                ForkSettings.Current = keptSettings;
            }
            checks.Add("PASS apps that are websites: Netflix, YouTube, Twitch, Disney+, Prime Video, Max, Crunchyroll, Spotify, ChatGPT and Gmail open their website when no app is installed (the installed app wins, other apps are untouched, and they still open mid-game)");
        }

        private void SmokeVoiceTurn(List<string> checks)
        {
            string keptOllama = Brain.Ollama;
            List<string> asked = new List<string>();
            System.Net.Sockets.TcpListener fake;
            string reply = "{\"message\":{\"role\":\"assistant\",\"content\":\"Loud and clear.\"},\"done_reason\":\"stop\"}";
            Brain.Ollama = FakeServer(new[] { reply, reply }, asked, out fake);
            try
            {
                Brain.Voiced = false;
                if (Brain.TurnNotes(true).Contains(Fork.VoiceTurn) || !Brain.TurnNotes(true).Contains("really work on their PC") || Brain.TurnNotes(false).Contains("really work")) throw new Exception("The turn notes were put together wrong.");
                Brain heard = new Brain();
                heard.Model = "smoke-model";
                Brain.Voiced = true;
                string voiced = heard.Ask("can you hear me?");
                Brain.Voiced = false;
                string typed = heard.Ask("and now?");
                if (voiced != "Loud and clear." || asked.Count != 2) throw new Exception("The voice turn wasn't answered: " + voiced);
                if (!asked[0].Contains("You hear him through the mic") || !asked[0].Contains("never claim you can only read the screen") || asked[1].Contains("You hear him through the mic")) throw new Exception("A voice turn didn't carry the voice note, or a typed turn did.");
                foreach (string line in heard.Recent()) if (line.Contains("through the mic")) throw new Exception("The voice note leaked into her history.");
                if (typed != "Loud and clear.") throw new Exception("The typed turn wasn't answered.");
            }
            finally
            {
                Brain.Voiced = false;
                fake.Stop();
                Brain.Ollama = keptOllama;
            }
            checks.Add("PASS voice turns: a turn that came in by microphone tells her brain she hears him through the mic (typed turns don't), the hands and hearing lines reach her chat prompt, and nothing of it is kept in her history");
        }
    }
}
