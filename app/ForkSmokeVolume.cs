using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeVolume(List<string> checks)
        {
            Func<List<SoundApp>> keptList = Mixer.List;
            Func<string, float?, bool?, bool> keptChange = Mixer.Change;
            Func<string, bool> keptRunning = Lockout.Running;
            string keptAsking = Brain.Asking;
            List<SoundApp> apps = new List<SoundApp>
            {
                new SoundApp { Program = "Spotify", Shown = "Spotify", Level = 0.8f },
                new SoundApp { Program = "Discord", Shown = "Discord", Level = 0.5f },
                new SoundApp { Program = "chrome", Shown = "chrome", Level = 0.6f },
                new SoundApp { Program = "chrome", Shown = "chrome", Level = 0.6f },
                new SoundApp { Program = "League of Legends", Shown = "League of Legends", Level = 1f }
            };
            List<string> changes = new List<string>();
            try
            {
                Mixer.List = delegate { return apps; };
                Mixer.Change = delegate(string program, float? level, bool? muted)
                {
                    changes.Add(program + "|" + (level.HasValue ? ((int)Math.Round(level.Value * 100)).ToString() : "-") + "|" + (muted.HasValue ? muted.Value.ToString() : "-"));
                    foreach (SoundApp app in apps)
                    {
                        if (app.Program != program) continue;
                        if (level.HasValue) app.Level = level.Value;
                        if (muted.HasValue) app.Muted = muted.Value;
                    }
                    return true;
                };
                string[,] heard =
                {
                    { "lower discord", "volume_down", "Discord", "" },
                    { "turn discord down", "volume_down", "Discord", "" },
                    { "turn down spotify a bit", "volume_down", "Spotify", "5" },
                    { "turn spotify down a lot", "volume_down", "Spotify", "25" },
                    { "make chrome quieter", "volume_down", "chrome", "" },
                    { "raise the volume of discord", "volume_up", "Discord", "" },
                    { "turn up league", "volume_up", "League of Legends", "" },
                    { "set spotify to 30%", "volume_set", "Spotify", "30" },
                    { "spotify volume to 30 percent", "volume_set", "Spotify", "30" },
                    { "put discord at 70", "volume_set", "Discord", "70" },
                    { "mute chrome", "mute", "chrome", "" },
                    { "unmute chrome", "unmute", "chrome", "" },
                    { "lower discort", "volume_down", "Discord", "" },
                    { "lower spotify's volume", "volume_down", "Spotify", "" },
                    { "capri, mute discord's sound please", "mute", "Discord", "" },
                    { "unmute discord audio", "unmute", "Discord", "" }
                };
                for (int i = 0; i < heard.GetLength(0); i++)
                {
                    string tool;
                    ToolArguments args;
                    if (!Hands.Heard(heard[i, 0], out tool, out args) || tool != "media" || args.Action != heard[i, 1] || Mixer.Resolve(args.App) != heard[i, 2] || (args.Level ?? "") != heard[i, 3])
                        throw new Exception("An app volume ask was heard wrong: " + heard[i, 0] + " -> " + tool + " " + args.Action + " " + args.App + " " + args.Level);
                }
                foreach (string pc in new[] { "turn the volume down", "lower the volume", "mute", "turn it up", "set the volume to 40", "make it quieter" })
                {
                    string tool;
                    ToolArguments args;
                    if (Hands.Heard(pc, out tool, out args) && tool == "media" && args.App != null) throw new Exception("A PC volume ask was aimed at one app: " + pc + " -> " + args.App);
                }
                foreach (string kept in new[] { "mute discord", "unmute discord", "mute my mic", "mute me on discord", "deafen me", "bring up discord", "put spotify on my main screen", "switch to discord", "open spotify", "play lofi on spotify", "is rudce online on discord", "turn up the volume", "turn the volume up" })
                {
                    string tool;
                    ToolArguments args;
                    if (Hands.Heard(kept, out tool, out args) && tool == "media" && args.App != null) throw new Exception("An ask that was never about an app's volume went to the mixer: " + kept + " -> " + args.Action + " " + args.App);
                }
                if (!Mixer.Do(new ToolArguments { Action = "mute", App = "Discord" }, "mute discord").StartsWith("Not done: for Discord, mute means his mic", StringComparison.Ordinal)) throw new Exception("Her brain could mute Discord's sound when he meant his mic.");
                foreach (string chat in new[] { "lower your expectations", "turn down for what", "set the table to 4", "mute the haters", "raise the bar" })
                {
                    string tool;
                    ToolArguments args;
                    if (Hands.Heard(chat, out tool, out args) && tool == "media" && args.App != null) throw new Exception("Chat was taken as an app volume ask: " + chat);
                }

                Action<byte> keptPress = Hands.Press;
                List<byte> pressed = new List<byte>();
                string down;
                try
                {
                    Hands.Press = delegate(byte key) { pressed.Add(key); };
                    Brain.Asking = "turn discord down";
                    down = DoTool("media", new ToolArguments { Action = "volume_down", App = "Discord" });
                }
                finally { Hands.Press = keptPress; Brain.Asking = keptAsking; }
                if (down != "Turned Discord down to 40%." || pressed.Count != 0 || apps[1].Level < 0.39f || apps[1].Level > 0.41f) throw new Exception("Discord was not turned down by 10 through her tool call, or the PC's volume keys were pressed: " + down + " " + apps[1].Level + " keys " + pressed.Count);
                string set = Mixer.Do(new ToolArguments { Action = "volume_set", App = "Spotify", Level = "30" }, null);
                if (set != "Set Spotify to 30%." || Math.Abs(apps[0].Level - 0.3f) > 0.01f) throw new Exception("Spotify was not set to 30%: " + set);
                string muteSaid = Mixer.Do(new ToolArguments { Action = "mute", App = "chrome" }, null);
                if (muteSaid != "Muted chrome." || !apps[2].Muted || !apps[3].Muted) throw new Exception("Both chrome sessions were not muted: " + muteSaid);
                if (Mixer.Do(new ToolArguments { Action = "mute", App = "chrome" }, null) != "chrome is already muted.") throw new Exception("A second mute did not say it was already muted.");
                string louder = Mixer.Do(new ToolArguments { Action = "volume_up", App = "chrome", Level = "20" }, null);
                if (louder != "Turned chrome up to 80%." || apps[2].Muted) throw new Exception("Turning a muted app up did not unmute it: " + louder);
                apps[1].Level = 0.03f;
                if (Mixer.Do(new ToolArguments { Action = "volume_down", App = "discord" }, null) != "Turned Discord down to 0%." || apps[1].Level != 0) throw new Exception("Turning down past zero did not stop at 0%.");
                if (Mixer.Do(new ToolArguments { Action = "volume_set", App = "discord", Level = "250" }, null) != "Couldn't set Discord's volume: it goes from 0 to 100.") throw new Exception("A level over 100 was accepted.");

                string missing = Mixer.Do(new ToolArguments { Action = "volume_down", App = "teams" }, null);
                if (missing == null || !missing.Contains("teams") || !missing.Contains("Spotify") || !missing.Contains("Discord") || !missing.Contains("chrome") || !missing.Contains("League of Legends")) throw new Exception("An app with no sound did not list what is playing: " + missing);
                if (Mixer.Do(new ToolArguments { Action = "volume_down", App = "the volume" }, null) != null || Mixer.Do(new ToolArguments { Action = "volume_down", App = "pc" }, null) != null) throw new Exception("A PC volume word was taken as an app.");
                if (Mixer.Do(new ToolArguments { Action = "volume_down" }, "turn it down") != null) throw new Exception("A plain volume ask with no app went to an app.");
                string aimed = Mixer.Do(new ToolArguments { Action = "volume_down" }, "discord is way too loud, turn it down");
                if (aimed == null || !aimed.StartsWith("Turned Discord", StringComparison.Ordinal)) throw new Exception("A model call with no app was not aimed at the app he named: " + aimed);
                if (Mixer.Do(new ToolArguments { Action = "next", App = "spotify" }, null) != null || Mixer.Do(new ToolArguments { Action = "now_playing", App = "spotify" }, null) != null) throw new Exception("A media action that isn't volume was taken by the mixer.");

                if (!Mixer.Takes(new ToolArguments { Action = "volume_down", App = "spotify" }) || CursorJob("media", new ToolArguments { Action = "volume_down", App = "spotify" }) != null) throw new Exception("Spotify's volume with Spotify playing still drags its slider instead of using its own volume.");
                apps.RemoveAll(app => app.Program == "Spotify");
                if (Mixer.Takes(new ToolArguments { Action = "volume_down", App = "spotify" }) || Mixer.Do(new ToolArguments { Action = "volume_down", App = "spotify" }, null) != null) throw new Exception("Spotify with no sound did not fall back to the upstream path.");

                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                bool inGame = Lockout.Refuse("media") == null && Mixer.Do(new ToolArguments { Action = "volume_down", App = "discord" }, null) != null;
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                if (!inGame) throw new Exception("An app's volume was refused during a game, though it presses no keys.");
                if (typeof(ISoundSessionVolume).GUID != new Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8") || typeof(ISoundSessions).GUID != new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F") || typeof(ISoundSession).GUID != new Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d")) throw new Exception("A Windows mixer interface has the wrong id, so no app's volume could be read.");
                if (!Hands.ClaudeTools.Contains("\"app\":{\"type\":\"string\",\"description\":\"Only when they named an app")) throw new Exception("The media tool does not offer an app to her brain.");
                checks.Add("PASS app volume: \"lower Discord\", \"Spotify to 30%\", \"mute Chrome\" and sound-alikes change only that app's own volume in Windows' mixer (every session of it), a plain volume ask still changes the PC, an app with no sound lists what is playing, Spotify's volume no longer drags its slider while it plays, it works in a game, and her brain's media tool can name an app");
            }
            finally
            {
                Mixer.List = keptList;
                Mixer.Change = keptChange;
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                Brain.Asking = keptAsking;
            }
        }
    }
}
