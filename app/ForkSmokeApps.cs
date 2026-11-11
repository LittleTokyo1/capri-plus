using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeShotMonitors(List<string> checks)
        {
            string[,] said =
            {
                { "check my second monitor and send me a screenshot", "second" }, { "monitor 2", "2" }, { "take a screenshot of screen two", "two" }, { "look at the other screen", "other" },
                { "my main screen", "main" }, { "2nd monitor please", "second" }, { "the left display", "left" }, { "both monitors", "all" }, { "what is on all of my screens", "all" },
                { "take a screenshot", "" }, { "what's on my screen", "" }, { "the second thing I asked", "" }
            };
            for (int i = 0; i < said.GetLength(0); i++)
            {
                string wording = Screens.Wording(said[i, 0]);
                if (wording != (said[i, 1].Length == 0 ? null : said[i, 1])) throw new Exception("\"" + said[i, 0] + "\" named the monitor " + (wording ?? "nothing") + " instead of " + (said[i, 1].Length == 0 ? "nothing" : said[i, 1]) + ".");
            }
            List<DisplayInfo> displays = new List<DisplayInfo> { new DisplayInfo { Number = 1, Bounds = new System.Drawing.Rectangle(0, 0, 2560, 1440), Primary = true }, new DisplayInfo { Number = 2, Bounds = new System.Drawing.Rectangle(2560, 0, 1920, 1080) } };
            System.Drawing.Point onMain = new System.Drawing.Point(100, 100), onSecond = new System.Drawing.Point(3000, 100);
            if (Screens.Area("take a screenshot of my second monitor", displays, onMain) != displays[1].Bounds || Screens.Area("monitor 1", displays, onSecond) != displays[0].Bounds) throw new Exception("A named monitor was not the one captured.");
            if (Screens.Area("the other screen", displays, onMain) != displays[1].Bounds || Screens.Area("the other screen", displays, onSecond) != displays[0].Bounds) throw new Exception("The other screen was not the one the mouse is not on.");
            if (Screens.Area("both monitors", displays, onMain) != new System.Drawing.Rectangle(0, 0, 4480, 1440) || Screens.Area("screenshot please", displays, onMain) != null || Screens.Area("the third monitor", displays, onMain) != null) throw new Exception("Both monitors, no monitor named, or a monitor that does not exist were wrong.");
            if (!Discord.WantsShot("send me sc") || !Discord.WantsShot("take a screenshot of my second monitor") || Discord.WantsShot("what is a sc") || Discord.WantsShot("give me a hint")) throw new Exception("A short ask for a screenshot was not understood, or a plain line was taken for one.");
            string[] onlyShots = { "okay, can you now send me a screenshot of my second monitor", "send me sc", "take a screenshot of my main monitor now", "send me a screenshot of monitor 1", "screenshot of both monitors please" };
            foreach (string asked in onlyShots) if (!Discord.OnlyShot(asked, false)) throw new Exception("A plain screenshot ask was not answered at once: " + asked);
            string[] more = { "check my second monitor and send me a screenshot of it what are u on", "open obsidian and send me a screenshot", "what's on my second monitor", "open spotify on my second monitor", "send me a screenshot and tell me what the progress is" };
            foreach (string asked in more) if (Discord.OnlyShot(asked, true)) throw new Exception("A line that wants more than a picture was answered with only a picture: " + asked);
            if (!Discord.OnlyShot("now of the second monitor", true) || Discord.OnlyShot("now of the second monitor", false)) throw new Exception("A bare follow-up naming a monitor must count right after a screenshot ask and not otherwise.");
            if (!Fork.HandsRules.Contains("never say you can't capture his second monitor")) throw new Exception("Her rules do not say she can capture any monitor.");
            Func<string, string> keptLook = Hands.Look;
            string looked = null;
            try
            {
                Hands.Look = delegate(string focus) { looked = focus; return "ok"; };
                Hands.Do("look_at_screen", new ToolArguments { Focus = "the download progress", Monitor = "2" });
            }
            finally { Hands.Look = keptLook; }
            if (looked == null || Screens.Wording(looked) != "2") throw new Exception("The monitor the model asked for did not reach the look: " + looked);
            checks.Add("PASS monitor words: second monitor, monitor 2, the other screen, left and both monitors pick the screen she looks at and the one she sends to Discord (one monitor, or all stitched), a monitor passed to the look tool reaches it, and send me sc counts as a screenshot request");
        }

        private void SmokeAppGuess(List<string> checks)
        {
            Func<List<OpenWindow>> keptWindows = Hands.Windows;
            Func<OpenWindow, string, bool> keptSteer = Hands.Steer;
            List<string> steered = new List<string>();
            try
            {
                Hands.Steer = delegate(OpenWindow window, string action) { steered.Add(action + " " + window.Handle); return true; };
                Hands.Windows = delegate { return new List<OpenWindow> { new OpenWindow(new IntPtr(1), "qBittorrent v4.6", "qbittorrent"), new OpenWindow(new IntPtr(2), "Spotify", "spotify"), new OpenWindow(new IntPtr(3), "Best torrent sites - Google Chrome", "chrome") }; };
                string guessed = Hands.Do("app_window", new ToolArguments { Name = "uTorrent", Action = "switch_to" });
                if (steered.Count != 1 || steered[0] != "switch_to 1" || !guessed.Contains("qbittorrent") || !guessed.Contains("uTorrent")) throw new Exception("uTorrent was not understood as the open qBittorrent: " + guessed + " / " + string.Join(", ", steered.ToArray()));
                steered.Clear();
                string plain = Hands.Do("app_window", new ToolArguments { Name = "torrent", Action = "minimize" });
                if (steered.Count != 1 || steered[0] != "minimize 1" || Hands.Failed(plain)) throw new Exception("The word torrent did not find qBittorrent: " + plain);
                steered.Clear();
                string close = Hands.Do("app_window", new ToolArguments { Name = "uTorrent", Action = "close" });
                if (steered.Count != 0 || !close.StartsWith("There's no window called", StringComparison.Ordinal) || !close.Contains("qbittorrent") || !Hands.Failed(close)) throw new Exception("A guessed name was closed, or the suggestion was not given: " + close);
                string none = Hands.Do("app_window", new ToolArguments { Name = "Photoshop", Action = "switch_to" });
                if (steered.Count != 0 || none != "Photoshop isn't open.") throw new Exception("An unrelated name was guessed: " + none);
                Hands.Windows = delegate { return new List<OpenWindow> { new OpenWindow(new IntPtr(1), "qBittorrent", "qbittorrent"), new OpenWindow(new IntPtr(4), "BitTorrent", "bittorrent") }; };
                string two = Hands.Do("app_window", new ToolArguments { Name = "uTorrent", Action = "switch_to" });
                if (steered.Count != 0 || two != "uTorrent isn't open.") throw new Exception("Two equally close apps were guessed between: " + two);
            }
            finally
            {
                Hands.Windows = keptWindows;
                Hands.Steer = keptSteer;
            }
            if (!Fork.HandsRules.Contains("never invent another product name") || !Fork.HandsRules.Contains("[screenshot]") || !Fork.HandsRules.Contains("open_app only starts an app")) throw new Exception("Her rules do not tell her to use his word for an app, to look before claiming an app is on screen, or to attach a screenshot.");
            checks.Add("PASS app names: a name that matches no open window resolves to the one open app sharing six letters with it (uTorrent to qBittorrent) for switch, minimize and move, never for close and never when two apps are equally close, and her rules say to use his word, to look before claiming an app is on screen, and to use [screenshot]");
        }
    }
}
