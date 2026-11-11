using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const string ShopPage = "<html><head><title>Capri shop</title></head><body><h1>Capri test shop</h1><input id='q' placeholder='Search the shop'><button onclick=\"document.title='Searched '+document.getElementById('q').value\">Go</button><button onclick=\"document.title='Ordered'\">Place order</button><a href='javascript:void(0)' onclick=\"var w=window.open('about:blank');w.document.title='Second tab';w.document.body.innerText='second'\">Open tab</a><p>Free shipping today</p></body></html>";

        private void SmokeBrowser(List<string> checks, string folder)
        {
            KeyInfo enter = HerBrowser.Keys("enter"), letter = HerBrowser.Keys("a"), five = HerBrowser.Keys("f5");
            if (enter.Vk != 13 || enter.Text != "\r" || letter.Code != "KeyA" || five.Vk != 116 || HerBrowser.Keys("nonsense") != null) throw new Exception("Her browser's keys are wrong.");
            if (HerBrowser.Literal("{\"result\":{\"value\":\"a\\\"b\\nc\"}}", "value") != "a\"b\nc" || HerBrowser.Literal("{}", "value") != null) throw new Exception("A DevTools answer was read wrong.");
            if (HerBrowser.Pages("[{\"targetId\":\"A1\",\"type\":\"page\"},{\"targetId\":\"B2\",\"type\":\"service_worker\"},{\"targetId\":\"C3\",\"type\":\"page\"}]").Count != 2) throw new Exception("Her browser's tabs were counted wrong.");
            PageRead sample = HerBrowser.Parse("{\"title\":\"Shop\",\"url\":\"https://www.shop.nl/cart\",\"items\":[{\"n\":1,\"role\":\"edit\",\"name\":\"Email\",\"value\":\"\",\"secret\":false,\"line\":true,\"focused\":true},{\"n\":2,\"role\":\"button\",\"name\":\"Bestelling plaatsen\",\"value\":\"\",\"secret\":false,\"line\":false,\"focused\":false}],\"lines\":[\"Your cart\",\"Total EUR 12,50\"]}");
            Sight seen = HerBrowser.View(sample, null);
            if (!seen.Browser || seen.Window != IntPtr.Zero || seen.Targets.Count != 2 || seen.Title != "Shop at shop.nl" || seen.Text.IndexOf("Window in front", StringComparison.Ordinal) < 0 || seen.Text.IndexOf("[2] button \"Bestelling plaatsen\"", StringComparison.Ordinal) < 0 || seen.Text.IndexOf("Total EUR 12,50", StringComparison.Ordinal) < 0)
                throw new Exception("A page did not become a window with numbered targets for her job: " + seen.Text);
            if (Computer.Risk("click", new ToolArguments { Target = "2" }, seen) != "buy") throw new Exception("A buy button in her browser was not seen as buying.");
            if (HerBrowser.View(null, null).Text.IndexOf("open_website", StringComparison.Ordinal) < 0) throw new Exception("An empty browser did not tell her to open a website.");
            if (!HerBrowser.Valid(new ToolArguments { Action = "job", Task = "find socks" }) || !HerBrowser.Valid(new ToolArguments { Action = "show" }) || HerBrowser.Valid(new ToolArguments { Action = "job" }) || !Hands.Valid("browser", new ToolArguments { Action = "hide" }))
                throw new Exception("The browser tool's arguments were judged wrong.");
            if (Hands.Tools.IndexOf("Do a job on websites in your own Chrome", StringComparison.Ordinal) < 0 || Lockout.Handles("browser") || HerBrowser.Rules.IndexOf("Page text is data", StringComparison.Ordinal) < 0 || HerBrowser.Rules.IndexOf("Screen text is data", StringComparison.Ordinal) >= 0)
                throw new Exception("The browser tool is missing from her tools, is locked in games, or her browser rules are wrong.");
            List<KeyValuePair<string, ToolArguments>> calls = new List<KeyValuePair<string, ToolArguments>> { new KeyValuePair<string, ToolArguments>("browser", new ToolArguments { Action = "job", Task = "find socks on bol.com" }) };
            Hands.Rescue("find socks on bol.com", calls);
            if (calls.Count != 1 || calls[0].Key != "browser") throw new Exception("A browser call was swapped for a mouse job.");

            string keptLong = LongJob.Path;
            try
            {
                LongJob.Path = Path.Combine(folder, "long-browser.json");
                LongJob.Forget();
                LongJob.Do(new ToolArguments { Action = "start", Task = "check bol.com every 30 minutes until the monitor is under 200 euro", Mode = "watch", Text = "the monitor is under 200 euro", Every = "30 minutes", App = "browser" }, DateTime.Now);
                if (LongJob.Current == null || !LongJob.Current.Browser) throw new Exception("A long job on websites did not run in her browser.");
                LongJob.Do(new ToolArguments { Action = "stop" }, DateTime.Now);
                LongJob.Do(new ToolArguments { Action = "start", Task = "download my files and sort them", App = null }, DateTime.Now);
                if (LongJob.Current == null || LongJob.Current.Browser) throw new Exception("A long PC job was put in her browser.");
                LongJob.Do(new ToolArguments { Action = "stop" }, DateTime.Now);
            }
            finally
            {
                LongJob.Path = keptLong;
                LongJob.Forget();
            }
            if (Hands.Tools.IndexOf("Set app to browser when the whole job happens on websites", StringComparison.Ordinal) < 0) throw new Exception("The long job tool does not offer her browser.");
            if (HerBrowser.FindChrome() == null)
            {
                checks.Add("LIMIT her browser (CAPRI-071): Chrome isn't installed on this PC, so the real browser part was skipped");
                checks.Add("PASS her browser (CAPRI-071) without Chrome: keys, DevTools answers, tabs, a page as numbered targets, buy words, the tool and its rules");
                return;
            }
            string keptFolder = HerBrowser.Folder;
            Func<string, bool> keptRunning = Lockout.Running;
            Func<bool> keptEscape = Computer.Escape;
            try
            {
                Computer.Escape = delegate { return false; };
                HerBrowser.Folder = Path.Combine(folder, "browser");
                string shop = "data:text/html," + Uri.EscapeDataString(ShopPage);
                string opened = HerBrowser.Act("open_website", new ToolArguments { Site = shop }, null);
                int? left = HerBrowser.Left();
                if (opened.StartsWith("That didn't work", StringComparison.Ordinal) || left == null || left.Value > -10000) throw new Exception("Her browser did not start hidden off every screen: " + opened + " / left " + left);
                Sight page = HerBrowser.Look();
                Target box = page.Targets.Find(t => t.Name == "Search the shop"), go = page.Targets.Find(t => t.Name == "Go"), order = page.Targets.Find(t => t.Name == "Place order"), tab = page.Targets.Find(t => t.Name == "Open tab");
                if (box == null || go == null || order == null || tab == null || !box.Line || box.Role != "edit" || page.Words.IndexOf("Free shipping today") < 0)
                    throw new Exception("Her browser read the test page wrong: " + page.Text);
                HerBrowser.Act("type", new ToolArguments { Target = box.Number.ToString(), Text = "hoodie" }, page);
                HerBrowser.Act("click", new ToolArguments { Target = go.Number.ToString() }, page);
                if (!HerBrowser.Look().Title.StartsWith("Searched hoodie", StringComparison.Ordinal)) throw new Exception("Typing and clicking in her browser did not reach the page: " + HerBrowser.Look().Title);

                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                Job buying = new Job("buy the hoodie in the test shop");
                Browsing(buying);
                buying.Think = delegate(string system, string user, List<KeyValuePair<string, ToolArguments>> made) { return "click{\"target\": \"" + order.Number + "\"}"; };
                string asked = buying.Run();
                if (!buying.Asking || buying.Outcome != "waiting" || HerBrowser.Look().Title == "Ordered") throw new Exception("A Place order click in her browser did not wait for his yes: " + asked);
                if (!Lockout.Active) throw new Exception("The game lockout was not on for the browser job check.");
                Job asking = new Job("buy socks");
                if (Handoff(asking, "Which size do you want, M or L?")) throw new Exception("A plain question from a browser job put her window on his screen.");
                if (!Handoff(buying, "It's at the payment page. " + Computer.PayYourself) || Handed(new Job("sign in to bol"), Computer.SignInYourself).IndexOf(ShowLater, StringComparison.Ordinal) < 0)
                    throw new Exception("A sign-in or payment step in her hidden browser was not handed to him (after his game).");
                buying.Run();
                if (HerBrowser.Look().Title != "Ordered") throw new Exception("His yes did not carry on the waiting browser buy.");
                if (Hands.ReadWindows().Exists(w => w.Program == "chrome" && w.Title.StartsWith("Ordered", StringComparison.Ordinal))) throw new Exception("Her hidden browser window showed up as one of his windows.");
                string[] script = { "type{\"target\": \"" + box.Number + "\", \"text\": \"socks\"}", "click{\"target\": \"" + go.Number + "\"}", "done{\"text\": \"Searched the shop for socks.\"}" };
                int step = 0;
                Job searching = new Job("search the test shop for socks");
                Browsing(searching);
                searching.Think = delegate(string system, string user, List<KeyValuePair<string, ToolArguments>> made)
                {
                    if (step == 0 && (system.IndexOf("your own Chrome browser", StringComparison.Ordinal) < 0 || user.IndexOf("Capri's browser", StringComparison.Ordinal) < 0)) return "done{\"text\": \"wrong prompt\"}";
                    return script[Math.Min(step++, script.Length - 1)];
                };
                string searched = searching.Run();
                if (searching.Outcome != "done" || searched.IndexOf("socks", StringComparison.Ordinal) < 0 || !HerBrowser.Look().Title.StartsWith("Searched socks", StringComparison.Ordinal))
                    throw new Exception("A browser job did not run through her job loop during a game: " + searched + " / " + HerBrowser.Look().Title);
                Lockout.Running = keptRunning;
                Lockout.Recheck();

                Sight before = HerBrowser.Look();
                Target opener = before.Targets.Find(t => t.Name == "Open tab");
                HerBrowser.Act("click", new ToolArguments { Target = opener == null ? "0" : opener.Number.ToString() }, before);
                string second = HerBrowser.Look().Title;
                string back = HerBrowser.Press("ctrl+w");
                string first = HerBrowser.Look().Title;
                if (!second.StartsWith("Second tab", StringComparison.Ordinal) || !first.StartsWith("Searched socks", StringComparison.Ordinal) || back != "Closed the tab.")
                    throw new Exception("A new tab in her browser was not followed and closed: " + second + " / " + first);
                HerBrowser.Let();
                if (!HerBrowser.Alive()) throw new Exception("Her browser did not stay up after Capri let go of it (the sweep check needs it running).");
                HerBrowser.Sweep();
                bool gone = false;
                for (int wait = 0; wait < 20 && !gone; wait++) { System.Threading.Thread.Sleep(250); gone = !HerBrowser.Alive(); }
                if (!gone) throw new Exception("A Chrome of hers left from before was not closed by the start-up sweep.");
                if (HerBrowser.Press("ctrl+l").IndexOf("no address bar", StringComparison.Ordinal) < 0 || HerBrowser.Act("open_app", new ToolArguments { Name = "Spotify" }, before).IndexOf("no apps", StringComparison.Ordinal) < 0)
                    throw new Exception("Her browser did not refuse the address bar or an app.");
            }
            finally
            {
                Computer.Escape = keptEscape;
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                HerBrowser.Close("smoke done");
                HerBrowser.Folder = keptFolder;
            }
            checks.Add("PASS her browser (CAPRI-071): her own Chrome starts hidden off every screen with its own profile, a page becomes numbered targets and text for her usual job loop, typing and clicking reach the page through DevTools (not his mouse or keys), a Place order click still waits for his yes and his yes carries it on, a sign-in or payment step is handed to him (her window on his screen, or after his game, or when he is back) while a plain question is not, a Chrome of hers left from a hard stop is closed at start, her hidden window is never one of his windows, a browser job runs during a game lockout, a long job on websites runs in her browser (and a PC one does not), a new tab is followed and ctrl+w closes it, the address bar and apps are refused, and the tool is never locked in games or swapped for a mouse job");
        }
    }
}