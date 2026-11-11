using System;
using System.Collections.Generic;
using System.Drawing;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeMonitorWords(List<string> checks)
        {
            Func<IntPtr, Rectangle> keptDisplay = Display;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<System.Collections.Generic.List<InstalledApp>> keptCatalog = Hands.Catalog;
            Action<string> keptRun = Hands.Run;
            Func<System.Collections.Generic.List<OpenWindow>> keptWindows = Hands.Windows;
            Func<string, string, string, ICollection<IntPtr>, string> keptPlacer = Hands.Placer;
            int keptWait = Hands.PlaceWait;
            Func<string, bool> keptRunning = Lockout.Running;
            try
            {
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                Rectangle mouse = new Rectangle(0, 0, 2560, 1440), front = new Rectangle(2560, 0, 1920, 1080);
                Display = delegate(IntPtr window) { return window == IntPtr.Zero ? mouse : front; };
                ForkSettings.Current = new ForkSettings();
                if (CaptureArea(new IntPtr(-2)) != mouse) throw new Exception("She did not look at the monitor the mouse is on.");
                ForkSettings.Current.LookAtMouse = false;
                if (CaptureArea(new IntPtr(-2)) != front || CaptureArea(IntPtr.Zero) != mouse) throw new Exception("Turning the mouse look off did not bring back the front window's monitor.");

                Hands.Catalog = delegate { return new System.Collections.Generic.List<InstalledApp> { new InstalledApp("Notes Pro", "id-notes") }; };
                Hands.Run = delegate(string id) { };
                Hands.Windows = delegate { return new System.Collections.Generic.List<OpenWindow>(); };
                Hands.Forget();
                string tool;
                ToolArguments args;
                if (!Hands.Heard("pull up Notes Pro on my second monitor", out tool, out args) || tool != "open_app" || args.Name != "Notes Pro" || args.Monitor != "second") throw new Exception("Pull up on the second monitor was heard wrong: " + tool + " / " + args.Name + " / " + args.Monitor);
                if (!Hands.Heard("open Notes Pro on the main screen", out tool, out args) || args.Monitor != "main") throw new Exception("The main screen was not heard.");
                if (!Hands.Heard("open Notes Pro on my 2nd monitor", out tool, out args) || args.Monitor != "second" || !Hands.Heard("pull up Notes Pro on the 1st screen", out tool, out args) || args.Monitor != "first") throw new Exception("1st and 2nd were not heard as first and second: " + args.Monitor);
                if (!Hands.Heard("open Notes Pro", out tool, out args) || args.Monitor != null) throw new Exception("A plain open got a monitor.");
                if (!Hands.Heard("launch Notes Pro on monitor 2", out tool, out args) || args.Monitor != "2") throw new Exception("On monitor 2 was not heard: " + args.Monitor);
                if (Hands.Heard("open youtube on my second monitor", out tool, out args) && tool == "open_website") throw new Exception("A website with a monitor named dropped the monitor.");
                List<string> asked = new List<string>();
                Hands.PlaceWait = 1;
                List<bool> fresh = new List<bool>();
                Hands.Windows = delegate { return new System.Collections.Generic.List<OpenWindow> { new OpenWindow(new IntPtr(31), "Notes Pro - old", "notespro") }; };
                Hands.Placer = delegate(string name, string action, string monitor, ICollection<IntPtr> older)
                {
                    asked.Add(name + "|" + action + "|" + monitor);
                    fresh.Add(older != null && older.Contains(new IntPtr(31)));
                    return asked.Count < 3 ? name + " isn't open." : "Moved " + name + " to monitor 2.";
                };
                string done = Hands.Do("open_app", new ToolArguments { Name = "Notes Pro", Monitor = "second" });
                if (!done.Contains("Moved Notes Pro to monitor 2") || asked.Count != 3 || asked[0] != "Notes Pro|move_to_monitor|second" || !fresh[0]) throw new Exception("The app was not moved to the second monitor after opening, or the windows open before it were not passed as old: " + done + " / " + asked.Count);
                fresh.Clear();
                Hands.Placer = delegate(string name, string action, string monitor, ICollection<IntPtr> older) { fresh.Add(older != null); return name + " isn't open."; };
                string late = Hands.Do("open_app", new ToolArguments { Name = "Notes Pro", Monitor = "second" });
                if (!late.Contains("no window appeared in time") || late.Contains("isn't open") || !Hands.Failed(late)) throw new Exception("An app whose window never showed up said it was not open instead of saying the window did not appear: " + late);
                if (!fresh[0] || fresh[fresh.Count - 1]) throw new Exception("Opening on a monitor did not first wait for a new window, then fall back to one already open: " + string.Join(",", fresh.ConvertAll(b => b.ToString()).ToArray()));
                List<OpenWindow> pair = new List<OpenWindow> { new OpenWindow(new IntPtr(31), "Notes Pro - old", "notespro"), new OpenWindow(new IntPtr(32), "Notes Pro - new", "notespro") };
                if (Hands.Fresh(pair, new List<IntPtr> { new IntPtr(31) }).Count != 1 || Hands.Fresh(pair, new List<IntPtr> { new IntPtr(31) })[0].Handle != new IntPtr(32) || Hands.Fresh(pair, new List<IntPtr> { new IntPtr(31), new IntPtr(32) }).Count != 0) throw new Exception("A window open before the ask was not told apart from the new one.");
                Hands.Windows = delegate { return new System.Collections.Generic.List<OpenWindow>(); };
                Hands.Placer = delegate(string name, string action, string monitor, ICollection<IntPtr> older) { asked.Add(name + "|" + action + "|" + monitor); return "Moved " + name + " to monitor 2."; };
                asked.Clear();
                Hands.Do("open_app", new ToolArguments { Name = "Notes Pro" });
                if (asked.Count != 0) throw new Exception("An open with no monitor still moved a window.");
                Hands.Catalog = delegate { return new System.Collections.Generic.List<InstalledApp>(); };
                Hands.Forget();
                Hands.Do("open_app", new ToolArguments { Name = "Nothing Installed Here", Monitor = "second" });
                if (asked.Count != 0) throw new Exception("A failed open still tried to move a window.");
                if (!Fork.HandsRules.Contains("first or main monitor is monitor 1") || !Fork.HandsRules.Contains("not a window") || !Fork.JobRules.Contains("Monitor 1")) throw new Exception("She was not told which monitor is which, or that she is not a window.");
            }
            finally
            {
                Display = keptDisplay;
                ForkSettings.Current = keptSettings;
                Hands.Catalog = keptCatalog;
                Hands.Run = keptRun;
                Hands.Windows = keptWindows;
                Hands.Placer = keptPlacer;
                Hands.PlaceWait = keptWait;
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                Hands.Forget();
            }
            checks.Add("PASS first and second monitor: she looks at the monitor the mouse is on (the front window's monitor with the setting off), pull up or open an app on my first, second or main monitor opens it, waits for its window and moves it there, 1st and 2nd count as first and second, a plain open and a failed open move nothing, a window that never appears is reported as such and not as not open, a new window is moved before one that was already open (which is moved only when no new one comes), and her prompts say first or main is monitor 1, second is monitor 2, and that she is not a window");
            SmokeManyEyes(checks);
        }

        private static void SmokeManyEyes(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            List<Bitmap> made = new List<Bitmap>();
            try
            {
                ForkSettings.Current = new ForkSettings();
                if (!ForkSettings.Current.LookBothMonitors || !ManyEyes.Wanted(2) || ManyEyes.Wanted(1)) throw new Exception("Both monitors was not on by default for two monitors, or was on for one.");
                ForkSettings.Current.LookBothMonitors = false;
                if (ManyEyes.Wanted(2)) throw new Exception("Both monitors stayed on with the setting off.");
                ForkSettings.Current.LookBothMonitors = true;
                if (ManyEyes.Panel(new Size(2560, 1440), 1600) != new Size(1600, 900) || ManyEyes.Panel(new Size(1080, 1920), 1600) != new Size(1600, 2844) || ManyEyes.Panel(Size.Empty, 1600) != Size.Empty) throw new Exception("A monitor panel was not scaled to the picture's width.");
                DisplayInfo main = new DisplayInfo { Number = 1, Bounds = new Rectangle(0, 0, 2560, 1440), Primary = true };
                DisplayInfo second = new DisplayInfo { Number = 2, Bounds = new Rectangle(2560, 0, 1920, 1080) };
                List<DisplayInfo> both = new List<DisplayInfo> { main, second };
                List<Rectangle> grabbed = new List<Rectangle>();
                Func<Rectangle, Bitmap> grab = delegate(Rectangle area) { grabbed.Add(area); Bitmap shot = new Bitmap(area.Width / 8, area.Height / 8); using (Graphics paint = Graphics.FromImage(shot)) paint.Clear(area.X == 0 ? Color.Red : Color.Blue); made.Add(shot); return shot; };
                Rectangle every;
                using (Bitmap stacked = ManyEyes.Shoot(both, delegate { return false; }, grab, delegate { return false; }, out every))
                {
                    if (stacked == null || stacked.Width != ManyEyes.PanelWidth || stacked.Height != 2 * ManyEyes.LabelHeight + 900 + 900) throw new Exception("Two monitors did not stack into one picture: " + (stacked == null ? "none" : stacked.Width + "x" + stacked.Height));
                    if (grabbed.Count != 2 || grabbed[0] != main.Bounds || grabbed[1] != second.Bounds || every != new Rectangle(0, 0, 4480, 1440)) throw new Exception("Each monitor was not grabbed once at its own bounds.");
                    Color top = stacked.GetPixel(800, ManyEyes.LabelHeight + 450), bottom = stacked.GetPixel(800, 2 * ManyEyes.LabelHeight + 900 + 450);
                    if (top.R < 200 || top.B > 60 || bottom.B < 200 || bottom.R > 60) throw new Exception("Monitor 1 was not on top and monitor 2 under it.");
                }
                foreach (Bitmap shot in made)
                {
                    bool freed;
                    try { freed = shot.Width < 0; }
                    catch (ArgumentException) { freed = true; }
                    if (!freed) throw new Exception("A monitor grab was not freed.");
                }
                if (ManyEyes.Shoot(both, delegate(DisplayInfo display) { return display.Number == 2; }, grab, delegate { return false; }, out every) != null) throw new Exception("A monitor with a private window was put in the picture.");
                if (ManyEyes.Shoot(both, delegate { return false; }, grab, delegate(Bitmap shot) { return shot.GetPixel(0, 0).B > 200; }, out every) != null) throw new Exception("A black monitor still made a two-monitor picture.");
                if (ManyEyes.Shoot(new List<DisplayInfo> { main }, delegate { return false; }, grab, delegate { return false; }, out every) != null) throw new Exception("One monitor made a stacked picture.");
                ForkSettings.Current.LookBothMonitors = false;
                if (ManyEyes.Shoot(both, delegate { return false; }, grab, delegate { return false; }, out every) != null) throw new Exception("The setting off still stacked the monitors.");
                if (!ManyEyes.Told.Contains("one under another") || !ManyEyes.Said(2).Contains("both of his monitors")) throw new Exception("She was not told the picture holds both monitors.");
                string named = ManyEyes.Titles("Discord", new List<string> { "Minecraft 26.3 - Singleplayer", "Notes" });
                if (!Computer.ScreenNames("That Minecraft base on your other screen is huge", named) || Computer.ScreenNames("That Minecraft base is huge", "Discord") || Computer.ScreenNames("Nice Roblox run", named) || ManyEyes.Titles("Discord", null) != "Discord") throw new Exception("A glance about a game on the other monitor was dropped, or one about a game on no monitor was kept: " + named);
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                foreach (Bitmap shot in made) shot.Dispose();
            }
            SmokeWatchedMonitor(checks);
            checks.Add("PASS both monitors in one look: when he asks her to look or she glances, every monitor goes into one picture, one under another under its label (Monitor 1 on top), each grabbed at its own bounds and freed; one monitor, a private window on any monitor, a black monitor or the setting off fall back to the old single look, and she is told the picture holds both");
        }

        private static void SmokeWatchedMonitor(List<string> checks)
        {
            Func<List<OpenWindow>> windowsWas = Monitors.Windows;
            Func<IntPtr, Rectangle> whereWas = Monitors.Where;
            Func<IntPtr, bool> foldedWas = Monitors.Folded;
            Func<IntPtr, Rectangle> boxWas = Monitors.Box;
            Func<Sight> glimpseWas = Computer.Glimpse;
            try
            {
                Rectangle main = new Rectangle(0, 0, 2560, 1440), second = new Rectangle(2560, 0, 1920, 1080);
                OpenWindow game = new OpenWindow(new IntPtr(11), "League of Legends", "league");
                OpenWindow folded = new OpenWindow(new IntPtr(12), "Old notes", "notepad");
                OpenWindow steam = new OpenWindow(new IntPtr(13), "Steam", "steam");
                Monitors.Windows = delegate { return new List<OpenWindow> { game, folded, steam }; };
                Monitors.Where = delegate(IntPtr handle) { return handle == game.Handle ? main : second; };
                Monitors.Folded = delegate(IntPtr handle) { return handle == folded.Handle; };
                Monitors.Box = delegate(IntPtr handle) { return new Rectangle(2700, 100, 900, 600); };
                Computer.Glimpse = delegate { return new Sight { Window = game.Handle, Title = game.Title, Program = game.Program, Bounds = main }; };
                Sight there = Monitors.Watched(second);
                if (Monitors.Watched(null) != null || Monitors.Watched(main) != null) throw new Exception("The badge left the window in front while she looked at its own monitor.");
                if (there == null || there.Window != steam.Handle || there.Title != "Steam" || there.Bounds != new Rectangle(2700, 100, 900, 600)) throw new Exception("The badge did not follow the top window on the monitor she switched to.");
                Monitors.Windows = delegate { return new List<OpenWindow> { game }; };
                Sight empty = Monitors.Watched(second);
                if (empty == null || empty.Window != IntPtr.Zero) throw new Exception("With no window on the monitor she looks at, the badge still followed the window in front.");
            }
            finally
            {
                Monitors.Windows = windowsWas;
                Monitors.Where = whereWas;
                Monitors.Folded = foldedWas;
                Monitors.Box = boxWas;
                Computer.Glimpse = glimpseWas;
            }
            checks.Add("PASS badge follows the monitor: after switch_monitor her task badge (and where she flies) follows the top window on the monitor she is looking at, minimized windows skipped, and stays put when that monitor has no window; on the front window's own monitor nothing changes");
        }

        private static void SmokeMonitors(List<string> checks)
        {
            DisplayInfo main = new DisplayInfo { Number = 1, Bounds = new Rectangle(0, 0, 1920, 1080), Work = new Rectangle(0, 0, 1920, 1040), Primary = true };
            DisplayInfo left = new DisplayInfo { Number = 2, Bounds = new Rectangle(-1920, 0, 1920, 1080), Work = new Rectangle(-1920, 0, 1920, 1040) };
            Func<List<DisplayInfo>> keptAll = Monitors.All;
            Func<List<OpenWindow>> keptWindows = Monitors.Windows;
            Func<IntPtr, Rectangle> keptWhere = Monitors.Where;
            Func<Rectangle?, Picture> keptShoot = Computer.Shoot;
            Func<Sight> keptGlimpse = Computer.Glimpse;
            Func<Target> keptFocus = Computer.Focus;
            Func<Point, Target> keptUnder = Computer.Under;
            Func<string, ToolArguments, Picture, string> keptUse = Computer.Use;
            Func<IntPtr> keptFront = Computer.Front;
            Func<IntPtr> keptOwnFront = Computer.OwnFront;
            Func<bool> keptEscape = Computer.Escape;
            Computer.Escape = delegate { return false; };
            ForkSettings keptSettings = ForkSettings.Current;
            try
            {
                ForkSettings.Current = new ForkSettings();
                Monitors.All = delegate { return new List<DisplayInfo> { main, left }; };
                List<OpenWindow> windows = new List<OpenWindow> { new OpenWindow(new IntPtr(11), "Steam", "steam"), new OpenWindow(new IntPtr(12), "Notepad - notes", "notepad"), new OpenWindow(new IntPtr(13), "KeePass vault", "KeePass"), new OpenWindow(new IntPtr(14), "Reddit - InPrivate", "msedge") };
                Monitors.Windows = delegate { return windows; };
                Monitors.Where = delegate(IntPtr handle) { return handle == new IntPtr(12) ? main.Bounds : left.Bounds; };

                string listing = Monitors.Listing();
                if (!listing.Contains("Monitor 1 (main), 1920x1080: Notepad - notes") || !listing.Contains("Monitor 2, 1920x1080: Steam") || listing.Contains("KeePass") || listing.Contains("InPrivate")) throw new Exception("The monitor listing was wrong: " + listing);
                if (!Monitors.Hides(left) || Monitors.Hides(main)) throw new Exception("A monitor with a private window was not recognised.");
                Func<IntPtr, bool> keptFolded = Monitors.Folded;
                try
                {
                    List<OpenWindow> personal = new List<OpenWindow> { new OpenWindow(new IntPtr(21), "Inbox (3) - sam@example.com - Gmail - Opera", "opera"), new OpenWindow(new IntPtr(22), "@Rudce - Discord", "Discord"), new OpenWindow(new IntPtr(23), "Hidden Notes", "notepad"), new OpenWindow(new IntPtr(24), "Steam", "steam") };
                    Monitors.Windows = delegate { return personal; };
                    Monitors.Folded = delegate(IntPtr handle) { return handle == new IntPtr(23); };
                    string masked = Monitors.Listing();
                    if (masked.Contains("sam@example.com") || masked.Contains("Inbox") || masked.Contains("Rudce") || masked.Contains("Hidden Notes") || !masked.Contains("Gmail") || !masked.Contains("Discord") || !masked.Contains("Steam")) throw new Exception("The monitor listing showed a mail or chat title or a minimized window: " + masked);
                }
                finally { Monitors.Folded = keptFolded; Monitors.Windows = delegate { return windows; }; }
                checks.Add("PASS monitor listing privacy: mail and chat windows (Gmail, Outlook, Discord and the like) show only the app's name, never a subject, address or DM name, and minimized windows are left out");
                windows.RemoveRange(2, 2);
                Sight sight = new Sight { Window = new IntPtr(7), Title = "Desktop", Program = "explorer" };
                if (!Computer.Opening("job", sight, null, null, listing).Contains("Monitor 2, 1920x1080: Steam")) throw new Exception("The job opening did not list the monitors.");
                foreach (string said in new[] { "clean up my launchers on my second screen", "look at the 2nd monitor", "put it on the left screen", "open it on monitor 2" })
                    if (Monitors.Named(said) != left) throw new Exception("A named monitor was missed: " + said);
                if (Monitors.Named("on my main screen") != main) throw new Exception("The main screen was not picked.");
                foreach (string said in new[] { "clean up my launchers", "check the screen", "open it on monitor 3", "play it on my screen", "move it to the 2nd monitor" })
                    if (Monitors.Named(said) != null) throw new Exception("A monitor was invented from: " + said);
                if (Monitors.Choose("banana") != null) throw new Exception("A made-up monitor was accepted.");
                if (Monitors.Choose("Monitor 2") != left || Monitors.Choose("monitor 1 (main)") != main || Monitors.Choose("two") != left) throw new Exception("The names in the listing were not understood.");
                Monitors.All = delegate { return new List<DisplayInfo> { main }; };
                if (Monitors.Listing().Length != 0) throw new Exception("A single monitor still got a listing.");
                Monitors.All = delegate { return new List<DisplayInfo> { main, left }; };
                if (!Computer.Toolset.StartsWith("[{\"type\":\"computer_toolset_20260801\"},{\"name\":\"open_app\"", StringComparison.Ordinal) || !Computer.Toolset.Contains("\"name\":\"switch_monitor\"") || Computer.Tools.Contains("switch_monitor") || Computer.ClaudeTools.Contains("switch_monitor")) throw new Exception("switch_monitor is in the wrong toolsets.");

                if (!OwnForms.Is("Capri+ settings") || !OwnForms.Is("capri mainframe") || OwnForms.Is("Capri") || OwnForms.Is("Capri+ settings - Opera") || OwnForms.Is("") || OwnForms.Is(null)) throw new Exception("Her own forms were not told apart from her sprite, bubbles and other windows.");
                if (!Hands.Outside(5, 5, "Capri+ settings", OwnForms.Is) || Hands.Outside(5, 5, "Capri+ settings", null) || Hands.Outside(5, 5, "Capri", OwnForms.Is) || !Hands.Outside(6, 5, "Notepad", null)) throw new Exception("The window list did not keep her settings window while dropping her other windows.");
                List<OpenWindow> stack = new List<OpenWindow> { new OpenWindow(new IntPtr(12), "Capri+ settings", "Capri"), new OpenWindow(new IntPtr(11), "Claude Code", "WindowsTerminal") };
                if (Computer.OnTop(true, stack) != new IntPtr(12) || Computer.OnTop(false, stack) != IntPtr.Zero || Computer.OnTop(true, new List<OpenWindow>()) != IntPtr.Zero || Computer.OnTop(true, new List<OpenWindow> { stack[1], stack[0] }) != IntPtr.Zero) throw new Exception("Her settings window was taken for the front window at the wrong times.");
                Computer.OwnFront = delegate { return new IntPtr(12); };
                Sight opened = Computer.Opened();
                if (Computer.Display() != main.Bounds || opened.Window != new IntPtr(12) || opened.Program != "Capri") throw new Exception("A job started on the wrong monitor, or named the wrong window, while her settings window was in front.");
                Computer.OwnFront = delegate { return IntPtr.Zero; };

                List<Rectangle?> shots = new List<Rectangle?>();
                List<string> clicks = new List<string>();
                Computer.Shoot = delegate(Rectangle? part) { shots.Add(part); return new Picture { Jpeg = new byte[] { 1, 2, 3 }, Size = new Size(1280, 720), Area = part ?? main.Bounds }; };
                Computer.Glimpse = delegate { return new Sight { Window = new IntPtr(7), Title = "Desktop", Program = "explorer" }; };
                Computer.Focus = delegate { return new Target { Role = "box", Name = "Search" }; };
                Computer.Under = delegate(Point at) { return new Target { Role = "button", Name = "Launcher" }; };
                Computer.Front = delegate { return new IntPtr(7); };
                Computer.Use = delegate(string name, ToolArguments args, Picture seen)
                {
                    Point at = seen.ToScreen(args.Coordinate[0], args.Coordinate[1]);
                    clicks.Add(name + " " + at.X + "," + at.Y);
                    return "Done.";
                };
                string[] script =
                {
                    "{\"id\":\"m1\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"switch_monitor\",\"input\":{\"monitor\":\"2\"}}],\"stop_reason\":\"tool_use\"}",
                    "{\"id\":\"m2\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t2\",\"name\":\"left_click\",\"toolset_name\":\"computer\",\"input\":{\"coordinate\":[640,360]}}],\"stop_reason\":\"tool_use\"}",
                    "{\"id\":\"m3\",\"content\":[{\"type\":\"text\",\"text\":\"Cleaned up.\"}],\"stop_reason\":\"end_turn\"}"
                };
                int turn = 0;
                List<string> sent = new List<string>();
                Job job = new Job("clean up my launchers");
                job.Drive = delegate(string system, string toolset, string messages)
                {
                    sent.Add(messages);
                    return Brain.Parsed(script[Math.Min(turn++, script.Length - 1)]);
                };
                string said2 = job.Run();
                if (said2 != "Cleaned up." || shots.Count != 3 || shots[0] != null || shots[1] != left.Bounds || shots[2] != left.Bounds) throw new Exception("The job did not stay on the monitor it switched to: " + said2 + " / " + shots.Count + " shots");
                if (clicks.Count != 1 || clicks[0] != "left_click -960,540") throw new Exception("A click on the other monitor landed wrong: " + string.Join(",", clicks.ToArray()));
                if (!sent[0].Contains("Monitor 2, 1920x1080: Steam") || !sent[1].Contains("Monitor 2, 1920x1080. Coordinates now mean this screenshot.")) throw new Exception("The job never saw the monitor list or the switch answer.");

                shots.Clear();
                turn = 2;
                Job named = new Job("clean up my launchers on my second screen");
                named.Drive = job.Drive;
                named.Run();
                if (shots.Count == 0 || shots[0] != left.Bounds) throw new Exception("A monitor named in his words was not the first one looked at.");

                shots.Clear();
                clicks.Clear();
                sent.Clear();
                string[] together =
                {
                    "{\"id\":\"m1\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"switch_monitor\",\"input\":{\"monitor\":\"Monitor 2\"}},{\"type\":\"tool_use\",\"id\":\"t2\",\"name\":\"left_click\",\"toolset_name\":\"computer\",\"input\":{\"coordinate\":[100,100]}}],\"stop_reason\":\"tool_use\"}",
                    "{\"id\":\"m2\",\"content\":[{\"type\":\"text\",\"text\":\"Done.\"}],\"stop_reason\":\"end_turn\"}"
                };
                int step = 0;
                Job both = new Job("clean up my launchers");
                both.Drive = delegate(string system, string toolset, string messages)
                {
                    sent.Add(messages);
                    return Brain.Parsed(together[Math.Min(step++, together.Length - 1)]);
                };
                both.Run();
                if (clicks.Count != 0 || sent.Count < 2 || !sent[1].Contains("the monitor was switched")) throw new Exception("A click sent with a monitor switch ran on coordinates from the old screenshot.");

                windows.Add(new OpenWindow(new IntPtr(13), "KeePass vault", "KeePass"));
                shots.Clear();
                sent.Clear();
                step = 0;
                string[] refusing =
                {
                    "{\"id\":\"m1\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"switch_monitor\",\"input\":{\"monitor\":\"2\"}}],\"stop_reason\":\"tool_use\"}",
                    "{\"id\":\"m2\",\"content\":[{\"type\":\"text\",\"text\":\"Done.\"}],\"stop_reason\":\"end_turn\"}"
                };
                Job vault = new Job("clean up my launchers");
                vault.Drive = delegate(string system, string toolset, string messages)
                {
                    sent.Add(messages);
                    return Brain.Parsed(refusing[Math.Min(step++, refusing.Length - 1)]);
                };
                vault.Run();
                if (shots.Count == 0 || shots.Exists(shotPart => shotPart.HasValue && shotPart.Value == left.Bounds) || sent.Count < 2 || !sent[1].Contains("a private window is open")) throw new Exception("She switched to a monitor with a blocklisted window open: shots " + shots.Count + ", sent " + sent.Count + (sent.Count > 1 ? ", " + sent[1].Substring(Math.Max(0, sent[1].Length - 400)) : ""));
            }
            finally
            {
                Monitors.All = keptAll;
                Monitors.Windows = keptWindows;
                Monitors.Where = keptWhere;
                Computer.Shoot = keptShoot;
                Computer.Glimpse = keptGlimpse;
                Computer.Focus = keptFocus;
                Computer.Under = keptUnder;
                Computer.Use = keptUse;
                Computer.Front = keptFront;
                Computer.OwnFront = keptOwnFront;
                Computer.Escape = keptEscape;
                ForkSettings.Current = keptSettings;
            }
            SmokeMonitorWords(checks);
            checks.Add("PASS two monitors in jobs: the job opening lists every monitor with its windows (private apps left out), switch_monitor looks at and acts on another monitor and the job stays on it, a click on the left monitor lands at the right spot (negative origin), a click sent in the same turn as a switch never runs on the old coordinates, a monitor with a blocklisted window is never switched to and incognito titles are left out of the list, the names she sees (Monitor 2, monitor 1 (main), two) work, a monitor named in his words (second, 2nd, left, monitor 2) is looked at first, nothing is invented from a plain request, one monitor adds nothing, and the button-reading tools are unchanged; her own Capri+ settings and Mainframe windows count in the monitor list, in the monitor a job starts on and in the window the job opening names as in front when her process has the focus and her form is on top (her sprite, bubbles and task panel still do not, and a terminal on top still wins)");
        }
    }
}
