using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeKeyLights(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.KeyLightsOn) throw new Exception("Keyboard lights should be on by default (they do nothing without a Wooting keyboard).");
            string close;
            Tunable found = Tuning.Find("keyboard lights", "off", out close);
            if (found == null || found.Field != "KeyLightsOn") throw new Exception("Her setting tool can't find keyboard lights by voice.");
            ToolArguments offJob = Tuning.Switched("turn off the keyboard lights", "turn the keyboard lights off");
            ToolArguments onJob = Tuning.Switched("Turn keyboard lights on please.", null);
            if (offJob == null || offJob.Name != "keyboard lights" || offJob.Level != "off" || offJob.Asked != "turn the keyboard lights off" || onJob == null || onJob.Level != "on") throw new Exception("A hands job to switch her keyboard lights did not become her setting tool (his 19:13 try went to the mouse).");
            foreach (string pcJob in new[] { "turn on bluetooth", "turn off the lights in the kitchen", "turn on night light", "open the keyboard lights app", "turn off my monitor" })
                if (Tuning.Switched(pcJob, null) != null) throw new Exception("A PC job became her setting: " + pcJob);
            if (KeyLights.ZipHash.Length != 64 || KeyLights.DllHash.Length != 64 || !KeyLights.SdkUrl.StartsWith("https://github.com/WootingKb/wooting-rgb-sdk/releases/download/", StringComparison.Ordinal)) throw new Exception("The Wooting SDK download is not pinned to Wooting's own release with both hashes.");
            int size = KeyLights.Rows * KeyLights.Cols * 3;
            byte[] chill = KeyLights.Frame(KeyLights.Idle, Mood.Chill, 1, 99, 99, null);
            if (chill.Length != size) throw new Exception("A frame is not the full 6 by 21 key buffer.");
            if (chill[0] == 0 || chill[0] != chill[size - 3] || chill[0] <= chill[1]) throw new Exception("Her idle glow is not one even red across the board.");
            byte[] worried = KeyLights.Frame(KeyLights.Idle, Mood.Worried, 1, 99, 99, null);
            byte[] hyped = KeyLights.Frame(KeyLights.Idle, Mood.Hyped, 1, 99, 99, null);
            if ((worried[2] == chill[2] && worried[0] == chill[0]) || hyped[0] <= chill[0]) throw new Exception("Her mood does not tint the glow.");
            byte[] darkStart = KeyLights.Frame(KeyLights.Dark, Mood.Chill, 1, 0, 99, null);
            byte[] darkLater = KeyLights.Frame(KeyLights.Dark, Mood.Chill, 1, 10, 99, null);
            if (darkStart[0] == 0) throw new Exception("Goodnight dropped the lights at once instead of fading.");
            foreach (byte one in darkLater) if (one != 0) throw new Exception("The keyboard did not go dark after goodnight.");
            byte[] talkA = KeyLights.Frame(KeyLights.Talking, Mood.Chill, 0.0, 99, 99, null);
            byte[] talkB = KeyLights.Frame(KeyLights.Talking, Mood.Chill, 0.33, 99, 99, null);
            if (talkA[0] == talkB[0]) throw new Exception("Her talking does not pulse.");
            int brightestThink = 0;
            for (int i = 0; i < 40; i++) brightestThink = Math.Max(brightestThink, KeyLights.Frame(KeyLights.Thinking, Mood.Chill, i * 0.1, 99, 99, null)[0]);
            if (brightestThink >= Math.Max(talkA[0], talkB[0])) throw new Exception("Her thinking breathe is not softer than her talking pulse.");
            for (int feeling = 0; feeling < 6; feeling++)
                foreach (int state in new[] { KeyLights.Idle, KeyLights.Talking, KeyLights.Thinking })
                    for (int step = 0; step < 20; step++)
                    {
                        byte[] seen = KeyLights.Frame(state, feeling, step * 0.17, 99, 99, null);
                        if (Math.Max(seen[0], Math.Max(seen[1], seen[2])) < (int)(KeyLights.Readable * 255) - 1) throw new Exception("The keys went too dark to read (his 19:10 report): state " + state + ", mood " + feeling + ", brightest channel " + Math.Max(seen[0], Math.Max(seen[1], seen[2])));
                    }
            int[] gold = KeyLights.Gold;
            byte[] sweep = KeyLights.Frame(KeyLights.Idle, Mood.Chill, 1, 99, KeyLights.SweepSeconds * 5.0 / (KeyLights.Cols + 4), gold);
            int at = (2 * KeyLights.Cols + 5) * 3;
            int far = (2 * KeyLights.Cols + 18) * 3;
            if (sweep[at] != gold[0] || sweep[at + 1] != gold[1] || sweep[far] != chill[0]) throw new Exception("The level-up sweep does not run across the columns.");
            byte[] after = KeyLights.Frame(KeyLights.Idle, Mood.Chill, 1, 99, KeyLights.SweepSeconds + 1, gold);
            if (after[at] != chill[0]) throw new Exception("The sweep never ended.");
            if (!KeyLights.Big("They (Ahri) just got a PENTAKILL") || !KeyLights.Big("They just WON the game as Ahri.") || KeyLights.Big("They just lost the game as Ahri.") || KeyLights.Big("They (Ahri) just got a double kill")) throw new Exception("The wrong game moments light up the keyboard.");
            if (KeyLights.SceneColor(Scene.Work) == KeyLights.SceneColor(Scene.Pentest) || KeyLights.SceneColor(Scene.Relax) == null) throw new Exception("Each scene does not have its own colour.");
            Func<bool> keptPresent = KeyLights.Present;
            Func<string, bool> keptFetch = KeyLights.Fetch;
            bool fetched = false;
            try
            {
                KeyLights.Present = delegate { return false; };
                KeyLights.Fetch = delegate(string path) { fetched = true; return false; };
                KeyLights.Forget();
                KeyLights.Run(true);
                KeyLights.Run(true);
                if (fetched || KeyLights.Lit) throw new Exception("Without a Wooting keyboard she downloaded the SDK or started lighting.");
                KeyLights.Run(false);
                if (KeyLights.Lit) throw new Exception("The off switch did not stop the lights.");
            }
            finally
            {
                KeyLights.Present = keptPresent;
                KeyLights.Fetch = keptFetch;
                KeyLights.Forget();
            }
            checks.Add("PASS keyboard lights: on by default and found by voice, and a hands job to turn them on or off becomes her setting tool while PC jobs like bluetooth stay hands jobs;Wooting's own signed SDK is pinned to its GitHub release with both SHA-256 hashes and only fetched when a Wooting keyboard is plugged in; every frame is the full 6 by 21 board: her red kit glow tinted by her mood, a pulse while she talks, a softer breathe while she thinks, a fade to dark at goodnight, a colour sweep across the columns for a level-up, a scene, a pentakill or a win (not a loss or a double kill), and the off switch hands the lights back");
        }
    }
}
