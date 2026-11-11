using System;
using System.Collections.Generic;
using System.Drawing;

namespace CapriDesktop
{
    // Capri+ smoke checks for moving windows between monitors and pushing runes into full page slots.
    internal sealed partial class Companion
    {
        private static void SmokeScreens(List<string> checks)
        {
            List<DisplayInfo> two = new List<DisplayInfo>
            {
                new DisplayInfo { Number = 1, Bounds = new Rectangle(0, 0, 2560, 1440), Work = new Rectangle(0, 0, 2560, 1400), Primary = true },
                new DisplayInfo { Number = 2, Bounds = new Rectangle(-1920, 0, 1920, 1080), Work = new Rectangle(-1920, 0, 1920, 1040) }
            };
            Rectangle onSecond = new Rectangle(-1800, 100, 1200, 800);
            if (Screens.Pick(two, "monitor two", onSecond, Point.Empty).Number != 2 || Screens.Pick(two, "1", onSecond, Point.Empty).Number != 1 || Screens.Pick(two, "my main screen", onSecond, Point.Empty).Number != 1)
                throw new Exception("Monitor numbers or main were read wrong.");
            if (Screens.Pick(two, "left", onSecond, Point.Empty).Number != 2 || Screens.Pick(two, "right", onSecond, Point.Empty).Number != 1 || Screens.Pick(two, "other", onSecond, Point.Empty).Number != 1 || Screens.Pick(two, null, onSecond, Point.Empty).Number != 1)
                throw new Exception("Left, right or the other monitor were read wrong.");
            if (Screens.Pick(two, "here", onSecond, new Point(-500, 500)).Number != 2 || Screens.Pick(two, "monitor 7", onSecond, Point.Empty) != null) throw new Exception("Here or a missing monitor were read wrong.");
            Rectangle placed = Screens.Place(onSecond, two[1].Work, two[0].Work);
            if (!two[0].Work.Contains(placed) || placed.Width != 1200 || placed.Height != 800 || placed.X != 160) throw new Exception("The window landed at " + placed + ".");
            Rectangle shrunk = Screens.Place(new Rectangle(0, 0, 2400, 1300), two[0].Work, two[1].Work);
            if (!two[1].Work.Contains(shrunk) || shrunk.Width != 1920) throw new Exception("A big window didn't fit the smaller monitor: " + shrunk);

            List<string> sent = new List<string>();
            bool full = true;
            League client = new League();
            client.LoadNames(SmokeChampions);
            client.Get = delegate(string path) { return path == "/lol-perks/v1/pages" ? "[{\"id\":21,\"name\":\"Mine\",\"isDeletable\":true,\"isEditable\":true,\"current\":false},{\"id\":22,\"name\":\"Picked\",\"isDeletable\":true,\"isEditable\":true,\"current\":true},{\"id\":23,\"name\":\"Capricorn\",\"isDeletable\":true,\"isEditable\":true}]" : null; };
            client.Send = delegate(string method, string path, string body)
            {
                sent.Add(method + " " + path);
                if (method == "POST" && full) { full = false; return null; }
                return "";
            };
            ProBuild build = ProBuilds.Top(ProBuilds.Parse(SmokeHtml()), "middle", 1)[0];
            string problem = client.PushRunes(build, "Ahri");
            if (problem != null || string.Join("|", sent.ToArray()) != "POST /lol-perks/v1/pages|DELETE /lol-perks/v1/pages/22|POST /lol-perks/v1/pages") throw new Exception("Full rune pages went: " + string.Join("|", sent.ToArray()) + " => " + problem);
            if (!League.Hers("Capri") || !League.Hers("Capri: Ahri 1") || League.Hers("Capricorn") || League.Hers(null)) throw new Exception("Her pages were told apart from theirs wrong.");
            checks.Add("PASS monitors and rune slots: windows move to monitor 1/2, main, left, right, here or the other one, keep their spot and fit a smaller screen, and with every rune slot full her Capri page replaces the selected page (never a Capricorn)");
        }

        private void SmokeLookup(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            Cloud keptRemote = brain.Remote;
            string keptAsking = Brain.Asking;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                ToolArguments search = new ToolArguments { Site = "google.com", Search = "annie counters" };
                if (!WantsAnswer(search, "who counters annie?") || WantsAnswer(search, "open google and search annie counters") || WantsAnswer(new ToolArguments { Site = "youtube.com", Search = "annie" }, "annie montage") || WantsAnswer(new ToolArguments { Site = "u.gg" }, "x"))
                    throw new Exception("Background lookups were chosen wrong.");
                ForkSettings.Current.BackgroundLookup = false;
                if (WantsAnswer(search, "who counters annie?")) throw new Exception("Background lookups ran with the setting off.");
                ForkSettings.Current.BackgroundLookup = true;
                List<string> asked = new List<string>();
                brain.Remote = new Cloud("sk-ant-test", "claude-haiku-5-5");
                Quick.Post = delegate(string body, string key) { asked.Add(body); return Tuple.Create(200, SmokeReply("Malphite, Galio and Kassadin are strong into Annie this patch.")); };
                Brain.Asking = "annie counters?";
                string told = ForkTool("open_website", search);
                if (told == null || !told.Contains("nothing opened") || !told.Contains("Malphite") || !asked[0].Contains("web_search") || !asked[0].Contains("annie counters")) throw new Exception("A question opened a page instead of being answered: " + told);
                Brain.Asking = "open google and search annie counters";
                if (ForkTool("open_website", search) != null) throw new Exception("An open request was answered instead of opened.");
                ToolArguments bare = new ToolArguments { Site = "google", Search = "annie counters" };
                Brain.Asking = "who counters annie?";
                told = ForkTool("open_website", bare);
                if (told == null || !told.Contains("Malphite")) throw new Exception("A bare \"google\" site opened a page instead of being answered: " + told);
                foreach (string engine in new[] { "", "bing", "duckduckgo", "yahoo", "ecosia", "brave", "www.google.com", "https://search.yahoo.com" })
                    if (!WantsAnswer(new ToolArguments { Site = engine, Search = "x" }, "who is x?")) throw new Exception("The search site \"" + engine + "\" wasn't answered in the background.");
                if (WantsAnswer(new ToolArguments { Site = "googleplay", Search = "x" }, "who is x?")) throw new Exception("A site that only starts with google was taken for a search engine.");
                Brain.Asking = "open google and search annie counters";
                if (WantsAnswer(bare, Brain.Asking)) throw new Exception("An open request with a bare site was answered instead of opened.");
                Lockout.Running = delegate(string name) { return name == "League of Legends"; };
                Lockout.Recheck();
                told = ForkTool("open_website", search);
                if (told == null || !told.Contains("Malphite")) throw new Exception("Mid-game lookups were refused: " + told);
                Brain.Asking = "who counters annie?";
                told = ForkTool("open_website", bare);
                if (told == null || !told.Contains("Malphite")) throw new Exception("A bare \"google\" site fell through to the lockout mid-game: " + told);
                told = ForkTool("open_website", new ToolArguments { Site = "", Search = "annie counters" });
                if (told == null || !told.Contains("Malphite")) throw new Exception("An empty site fell through to the lockout mid-game: " + told);
                if (ForkTool("open_website", new ToolArguments { Site = "youtube", Search = "annie montage" }) != null) throw new Exception("A video site was answered or refused mid-game instead of opened.");
            }
            finally
            {
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                ForkSettings.Current = keptSettings;
                Quick.Post = keptPost;
                brain.Remote = keptRemote;
                Brain.Asking = keptAsking;
            }
            checks.Add("PASS lookups: questions are answered from a background web search without opening anything, \"open ...\" still opens the browser, and mid-game lookups work instead of hitting the anti-cheat lock");
        }
    }
}
