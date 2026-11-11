using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private sealed class PressForm : Form
        {
            protected override bool ShowWithoutActivation { get { return true; } }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams made = base.CreateParams;
                    made.ExStyle |= 0x08000008;
                    return made;
                }
            }
        }

        private void SmokeHands(List<string> checks)
        {
            SmokeHandsWays();
            SmokeHandsFrames();
            SmokeHandsMeter();
            SmokeHandsSettings();
            SmokeHandsGuards();
            SmokeHandsJob();
            SmokeHandsReal();
            SmokeHandsList();
            SmokeHandsListJob();
            SmokeHandsRemember();
            checks.Add("PASS better hands: a left click on a named button, checkbox, tab or list item is pressed through UI Automation (Invoke, Toggle, Select, Expand) with her pointer showing the press and his cursor untouched, a double click opens only list items, text boxes, right clicks, held keys, greyed-out or moved controls, other windows and her own windows go to the real mouse, the same control twice in a row gets the real click, the lockout and Esc stop it first, a job's look after a step shows only the window it works in (never for a maximized window, a popup outside it, a named monitor or a screenshot call) and tells the model when the picture changes, after a step on the same window a rich control list with coordinates in the latest picture replaces the picture (at most two looks in a row, never after a failed step, for a new window, a game, a thin list or with the setting off; secrets never listed), a job that worked keeps its steps by control names (typed text and passwords never kept) and offers them to the next job like it once, a failed job drops its way, every job writes one timing line, and all four are settings that start on and are saved");
        }

        private static void SmokeHandsWays()
        {
            if (ByName.Way("Button", 1, true, false, false, false) != "Invoke" || ByName.Way("Hyperlink", 1, true, false, false, false) != "Invoke" || ByName.Way("MenuItem", 1, false, false, false, true) != "Expand" || ByName.Way("Button", 1, false, true, false, false) != "Toggle")
                throw new Exception("A button, link or menu was not pressed the right way.");
            if (ByName.Way("ListItem", 1, true, false, true, false) != "Select" || ByName.Way("TreeItem", 1, false, false, true, false) != "Select" || ByName.Way("TabItem", 1, false, false, true, false) != "Select" || ByName.Way("RadioButton", 1, false, false, true, false) != "Select")
                throw new Exception("A single click on a list item, tree item, tab or radio button did more than select it.");
            if (ByName.Way("ListItem", 1, true, false, false, false) != null) throw new Exception("A single click on a list item could open it (Explorer would run a script).");
            if (ByName.Way("ListItem", 2, true, false, true, false) != "Invoke" || ByName.Way("Button", 2, true, false, false, false) != null || ByName.Way("ListItem", 2, false, false, true, false) != null)
                throw new Exception("A double click opened something other than a list item.");
            if (ByName.Way("CheckBox", 1, false, true, false, false) != "Toggle" || ByName.Way("CheckBox", 1, true, false, false, false) != "Invoke" || ByName.Way("ComboBox", 1, false, false, false, true) != "Expand")
                throw new Exception("A checkbox or dropdown was not pressed the right way.");
            if (ByName.Way("Edit", 1, true, true, true, true) != null || ByName.Way("Document", 1, true, false, false, false) != null || ByName.Way("Text", 1, true, false, false, false) != null || ByName.Way("Image", 1, true, false, false, false) != null || ByName.Way("Button", 3, true, false, false, false) != null || ByName.Way("Button", 0, true, false, false, false) != null)
                throw new Exception("A text box, picture, label or triple click was pressed by name.");
            if (ByName.Times("left_click") != 1 || ByName.Times("double_click") != 2 || ByName.Times("right_click") != 0 || ByName.Times("triple_click") != 0 || ByName.Times("middle_click") != 0)
                throw new Exception("Click names were read wrong.");
        }

        private static void SmokeHandsFrames()
        {
            Rectangle screen = new Rectangle(0, 0, 2560, 1440);
            Rectangle window = new Rectangle(100, 100, 900, 600);
            List<Rectangle> none = new List<Rectangle>();
            if (Framing.Frame(window, screen, none) != window) throw new Exception("A normal window was not framed.");
            if (Framing.Frame(new Rectangle(0, 0, 2560, 1400), screen, none) != null) throw new Exception("A maximized window was framed.");
            if (Framing.Frame(new Rectangle(10, 10, 150, 150), screen, none) != null) throw new Exception("A tiny window was framed.");
            if (Framing.Frame(window, screen, new List<Rectangle> { new Rectangle(1200, 100, 200, 200) }) != null) throw new Exception("A popup outside the window was cut off.");
            if (Framing.Frame(window, screen, new List<Rectangle> { new Rectangle(300, 300, 100, 100) }) != window) throw new Exception("A popup inside the window stopped the frame.");
            if (Framing.Frame(new Rectangle(-50, 100, 900, 600), screen, none) != new Rectangle(0, 100, 850, 600)) throw new Exception("A window partly off its monitor was not cut to it.");
            if (Framing.Note(null, null, new Size(1280, 720), "x") != null || Framing.Note(window, window, new Size(900, 600), "x") != null) throw new Exception("An unchanged picture got a note.");
            string framed = Framing.Note(null, window, new Size(900, 600), "Notepad");
            string whole = Framing.Note(window, null, new Size(1280, 720), "Notepad");
            if (framed == null || !framed.Contains("only the \"Notepad\" window, 900x600") || !framed.Contains("screenshot call on its own now shows the whole monitor") || whole == null || !whole.Contains("whole monitor again, 1280x720"))
                throw new Exception("The picture notes were wrong: " + framed + " / " + whole);
            if (Framing.Text(null) != "" || !Framing.Text("a \"b\"").StartsWith(",{\"type\":\"text\",\"text\":\"a \\\"b\\\"\"}", StringComparison.Ordinal)) throw new Exception("A picture note was not a text block: " + Framing.Text("a \"b\""));
            ClaudeBlock look = new ClaudeBlock { Type = "tool_use", Name = "screenshot", Toolset = "computer" };
            ClaudeBlock click = new ClaudeBlock { Type = "tool_use", Name = "left_click", Toolset = "computer" };
            ClaudeBlock zoom = new ClaudeBlock { Type = "tool_use", Name = "zoom", Toolset = "computer" };
            ClaudeBlock other = new ClaudeBlock { Type = "tool_use", Name = "switch_monitor" };
            if (!Framing.Whole(new[] { look }, 0, 0) || Framing.Whole(new[] { look }, 0, 1) || Framing.Whole(new[] { click, look }, 1, 0) || !Framing.Whole(new[] { zoom, look }, 1, 0) || !Framing.Whole(new[] { other, look }, 1, 0) || Framing.Whole(new[] { click, zoom, look }, 2, 0))
                throw new Exception("A screenshot call after steps or a button list was not allowed the window, or one right after a picture was.");

            ForkSettings kept = ForkSettings.Current;
            Func<IntPtr> frontWas = Computer.Front;
            Func<Sight> glimpseWas = Computer.Glimpse;
            Func<IntPtr, List<Rectangle>> popupsWas = Framing.Popups;
            try
            {
                Rectangle stage = Stage.Main.Bounds;
                Rectangle inside = new Rectangle(stage.Left + 50, stage.Top + 50, Math.Min(900, stage.Width / 2), Math.Min(600, stage.Height / 2));
                ForkSettings on = kept.Copy();
                on.HandsWindowOnly = true;
                ForkSettings.Current = on;
                Computer.Front = delegate { return new IntPtr(7); };
                Computer.Glimpse = delegate { return new Sight { Window = new IntPtr(7), Title = "Notepad", Program = "notepad", Bounds = inside }; };
                Framing.Popups = delegate { return new List<Rectangle>(); };
                string title;
                if (Framing.Frame(new IntPtr(7), out title) != inside || title != "Notepad") throw new Exception("The window in front was not framed.");
                if (Framing.Frame(IntPtr.Zero, out title) != null || Framing.Frame(new IntPtr(8), out title) != null) throw new Exception("A new window in front was framed on its first look.");
                ForkSettings off = kept.Copy();
                off.HandsWindowOnly = false;
                ForkSettings.Current = off;
                if (Framing.Frame(new IntPtr(7), out title) != null) throw new Exception("Window-only pictures were used with the setting off.");
            }
            finally
            {
                ForkSettings.Current = kept;
                Computer.Front = frontWas;
                Computer.Glimpse = glimpseWas;
                Framing.Popups = popupsWas;
            }
        }

        private static void SmokeHandsMeter()
        {
            Dictionary<string, int> why = new Dictionary<string, int> { { "a text box", 2 }, { "right click", 1 } };
            string line = HandsMeter.Line(34210, 6, 9, 18200, 2 * 1024 * 1024, 2, 5, 3, why);
            if (line != "34.2 s, 6 turns, 9 actions, model 18.2 s, sent 2.0 MB, lists 2, by name 5, by cursor 3 (a text box x2, right click)") throw new Exception("The job timing line was wrong: " + line);
            string one = HandsMeter.Line(900, 1, 1, 800, 3000, 0, 0, 0, null);
            if (one != "0.9 s, 1 turn, 1 action, model 0.8 s, sent 2 KB, lists 0, by name 0, by cursor 0") throw new Exception("A one-turn timing line was wrong: " + one);
            HandsMeter.Start();
            HandsMeter.Turn(1200, 2048, 3);
            HandsMeter.Pressed("Invoke Button");
            HandsMeter.Cursor("a text box");
            HandsMeter.Cursor("a text box");
            HandsMeter.Cursor("right click");
            HandsMeter.Listed();
            string counted = HandsMeter.Line();
            HandsMeter.Read(1500);
            if (!HandsMeter.Line().EndsWith(", screenshot calls 0, framed screenshots 0, list reads 1.5 s", StringComparison.Ordinal)) throw new Exception("The timing line did not count list reads: " + HandsMeter.Line());
            if (!counted.Contains("1 turn, 3 actions, model 1.2 s, sent 2 KB, lists 1, by name 1, by cursor 3 (a text box x2, right click)")) throw new Exception("The job counters were wrong: " + counted);
            List<string> timed = new List<string>();
            Func<string, string, string, ClaudeReply> drive = HandsMeter.Timed(delegate(string a, string b, string c) { timed.Add(c); return Brain.Parsed("{\"id\":\"m\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"left_click\",\"input\":{}},{\"type\":\"tool_use\",\"id\":\"t2\",\"name\":\"key\",\"input\":{}}],\"stop_reason\":\"tool_use\"}"); });
            HandsMeter.Start();
            drive("s", "t", new string('x', 4096));
            if (timed.Count != 1 || !HandsMeter.Line().Contains("1 turn, 2 actions") || !HandsMeter.Line().Contains("sent 4 KB") || HandsMeter.Timed(null) != null || HandsMeter.Thought(null) != null) throw new Exception("A timed turn was not counted: " + HandsMeter.Line());
            HandsMeter.Start();
        }

        private static void SmokeHandsSettings()
        {
            string folder = Path.Combine(Path.GetTempPath(), "capri-hands-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string old = Path.Combine(folder, "old.json");
                File.WriteAllText(old, "{\"Proactive\":1,\"Cap\":10}");
                ForkSettings loaded = ForkSettings.Read(old);
                if (loaded == null || !loaded.HandsByName || !loaded.HandsWindowOnly || !loaded.HandsListOnly || !loaded.HandsRemember) throw new Exception("An old fork.json did not start with the new hands on.");
                ForkSettings picked = new ForkSettings { HandsByName = false, HandsWindowOnly = false, HandsListOnly = false, HandsRemember = false };
                using (ForkSettingsForm form = new ForkSettingsForm(picked)) form.Apply();
                if (picked.HandsByName || picked.HandsWindowOnly || picked.HandsListOnly || picked.HandsRemember) throw new Exception("The settings window did not save the new hands off.");
                ForkSettings on = new ForkSettings { HandsByName = true, HandsWindowOnly = true, HandsListOnly = true, HandsRemember = true };
                using (ForkSettingsForm form = new ForkSettingsForm(on)) form.Apply();
                if (!on.HandsByName || !on.HandsWindowOnly || !on.HandsListOnly || !on.HandsRemember) throw new Exception("The settings window did not save the new hands on.");
            }
            finally
            {
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            string unrouted = Tuning.Unrouted();
            if (unrouted != null) throw new Exception("A setting is not routed through her setting tool: " + unrouted);
        }

        private static void SmokeHandsGuards()
        {
            ForkSettings kept = ForkSettings.Current;
            Func<Point, int, string> pressWas = ByName.Press;
            Action<Point> putWas = Computer.PutCursor;
            Action<Point> tappedWas = Computer.Tapped;
            List<string> pressed = new List<string>();
            List<Point> moved = new List<Point>();
            int taps = 0;
            try
            {
                ForkSettings on = kept.Copy();
                on.HandsByName = true;
                ForkSettings.Current = on;
                ByName.Press = delegate(Point at, int times) { pressed.Add(at.X + "," + at.Y + "x" + times); return "Invoke Button"; };
                Computer.PutCursor = delegate(Point at) { moved.Add(at); };
                Computer.Tapped = delegate { taps++; };
                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                try
                {
                    if (ByName.Pressing(new Point(5, 5), "left_click") || pressed.Count != 0) throw new Exception("A press by name went through mid-game.");
                }
                finally
                {
                    Lockout.Running = delegate { return false; };
                    Lockout.Recheck();
                }
                if (ByName.Pressing(new Point(5, 5), "right_click") || pressed.Count != 0) throw new Exception("A right click was pressed by name.");
                ForkSettings off = kept.Copy();
                off.HandsByName = false;
                ForkSettings.Current = off;
                if (ByName.Pressing(new Point(5, 5), "left_click") || pressed.Count != 0) throw new Exception("A press by name went through with the setting off.");
                ForkSettings.Current = on;
                Computer.Arm();
                if (Lockout.Active || Computer.Interrupted) throw new Exception("The click by name check could not run: lockout " + Lockout.Active + ", stopped " + Computer.Interrupted);
                Rectangle stage = Stage.Main.WorkingArea;
                Rectangle area = new Rectangle(stage.Left, stage.Top, 1280, 720);
                Picture shown = new Picture { Jpeg = new byte[] { 1 }, Size = new Size(1280, 720), Area = area };
                string said = Computer.Member("left_click", new ToolArguments { Coordinate = new double[] { 200, 150 } }, shown);
                Point expect = shown.ToScreen(200, 150);
                if (said != "Clicked." || pressed.Count != 1 || pressed[0] != expect.X + "," + expect.Y + "x1" || moved.Count != 0 || taps != 1)
                    throw new Exception("A job's click by name moved his cursor or missed: " + said + " / " + string.Join(";", pressed.ToArray()) + " / moved " + moved.Count + " / taps " + taps);
            }
            finally
            {
                ForkSettings.Current = kept;
                ByName.Press = pressWas;
                Computer.PutCursor = putWas;
                Computer.Tapped = tappedWas;
                Computer.Finish();
            }
        }

        private static void SmokeHandsJob()
        {
            ForkSettings kept = ForkSettings.Current;
            Func<Rectangle?, Picture> shootWas = Computer.Shoot;
            Func<Sight> glimpseWas = Computer.Glimpse;
            Func<Target> focusWas = Computer.Focus;
            Func<Point, Target> underWas = Computer.Under;
            Func<IntPtr> frontWas = Computer.Front;
            Func<IntPtr, List<Rectangle>> popupsWas = Framing.Popups;
            Func<string, ToolArguments, Picture, string> useWas = Computer.Use;
            try
            {
                ForkSettings on = kept.Copy();
                on.HandsWindowOnly = true;
                on.HandsListOnly = false;
                ForkSettings.Current = on;
                Rectangle stage = Stage.Main.Bounds;
                Rectangle inside = new Rectangle(stage.Left + 100, stage.Top + 100, Math.Min(900, stage.Width / 2), Math.Min(600, stage.Height / 2));
                List<Rectangle?> shots = new List<Rectangle?>();
                Computer.Shoot = delegate(Rectangle? part) { shots.Add(part); Rectangle got = part ?? stage; return new Picture { Jpeg = new byte[] { 1, 2, 3 }, Size = new Size(Math.Min(1280, got.Width), Math.Min(720, got.Height)), Area = got }; };
                Computer.Glimpse = delegate { return new Sight { Window = new IntPtr(7), Title = "Notepad", Program = "notepad", Bounds = inside }; };
                Computer.Focus = delegate { return new Target { Role = "box", Name = "Text editor" }; };
                Computer.Under = delegate(Point at) { return new Target { Role = "button", Name = "Format" }; };
                Computer.Front = delegate { return new IntPtr(7); };
                Framing.Popups = delegate { return new List<Rectangle>(); };
                Computer.Use = delegate(string name, ToolArguments args, Picture seen) { return "Clicked."; };
                string[] script =
                {
                    "{\"id\":\"m1\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"left_click\",\"toolset_name\":\"computer\",\"input\":{\"coordinate\":[300,200]}}],\"stop_reason\":\"tool_use\"}",
                    "{\"id\":\"m2\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t2\",\"name\":\"screenshot\",\"toolset_name\":\"computer\",\"input\":{}}],\"stop_reason\":\"tool_use\"}",
                    "{\"id\":\"m3\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t3\",\"name\":\"left_click\",\"toolset_name\":\"computer\",\"input\":{\"coordinate\":[300,200]}},{\"type\":\"tool_use\",\"id\":\"t4\",\"name\":\"screenshot\",\"toolset_name\":\"computer\",\"input\":{}}],\"stop_reason\":\"tool_use\"}",
                    "{\"id\":\"m4\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t5\",\"name\":\"screenshot\",\"toolset_name\":\"computer\",\"input\":{}}],\"stop_reason\":\"tool_use\"}",
                    "{\"id\":\"m5\",\"content\":[{\"type\":\"text\",\"text\":\"Opened the Format menu.\"}],\"stop_reason\":\"end_turn\"}"
                };
                int turn = 0;
                List<string> sent = new List<string>();
                Job job = new Job("open the format menu in notepad");
                job.Drive = delegate(string system, string toolset, string messages) { sent.Add(messages); return Brain.Parsed(script[Math.Min(turn++, script.Length - 1)]); };
                HandsMeter.Start();
                string said = job.Run();
                string meter = HandsMeter.Line();
                HandsMeter.Start();
                if (said != "Opened the Format menu." || shots.Count != 5 || shots[0] != null || shots[1] != inside || shots[2] != null || shots[3] != inside || shots[4] != null)
                    throw new Exception("A job's looks were not whole, window after a step, whole for a lone screenshot call, window for a screenshot call after a step, whole again: " + said + " / " + shots.Count + " shots");
                if (sent.Count != 5 || sent[0].Contains("only the") || !sent[1].Contains("only the \\\"Notepad\\\" window") || !sent[2].Contains("whole monitor again") || !sent[3].Contains("only the \\\"Notepad\\\" window") || !sent[4].Contains("whole monitor again"))
                    throw new Exception("The model was not told when the picture changed to the window and back.");
                if (!meter.Contains("framed screenshots 1")) throw new Exception("The timing line did not count the framed screenshot call: " + meter);
            }
            finally
            {
                ForkSettings.Current = kept;
                Computer.Shoot = shootWas;
                Computer.Glimpse = glimpseWas;
                Computer.Focus = focusWas;
                Computer.Under = underWas;
                Computer.Front = frontWas;
                Framing.Popups = popupsWas;
                Computer.Use = useWas;
                Computer.Finish();
            }
        }

        private static Sight RichSight()
        {
            Sight rich = new Sight { Window = new IntPtr(7), Title = "Steam", Program = "steam", Bounds = new Rectangle(0, 0, 2560, 1440) };
            for (int i = 1; i <= 7; i++) rich.Targets.Add(new Target { Number = i, Role = "button", Name = "Game " + i, Box = new Rectangle(100 * i, 200, 80, 40) });
            rich.Targets.Add(new Target { Number = 8, Role = "box", Name = "Password", Line = true, Secret = true, Value = "hunter2", Box = new Rectangle(900, 900, 200, 30) });
            rich.Targets.Add(new Target { Number = 9, Role = "box", Name = "Search", Line = true, Value = "zed", Box = new Rectangle(1200, 900, 200, 30) });
            rich.Targets.Add(new Target { Number = 10, Role = "button", Name = "Off screen", Box = new Rectangle(5000, 5000, 10, 10) });
            rich.Words.Add("Library");
            return rich;
        }

        private static void SmokeHandsList()
        {
            Picture shown = new Picture { Jpeg = new byte[] { 1 }, Size = new Size(1280, 720), Area = new Rectangle(0, 0, 2560, 1440) };
            string text = Listing.Text(RichSight(), shown);
            if (text == null || !text.Contains(Listing.Opening) || !text.Contains("button \"Game 1\" at (70, 110)") || !text.Contains("holds \"zed\"") || text.Contains("hunter2") || text.Contains("Off screen") || !text.Contains("Text on it: Library"))
                throw new Exception("The control list was wrong: " + text);
            Sight thin = new Sight { Window = new IntPtr(7) };
            for (int i = 1; i <= 3; i++) thin.Targets.Add(new Target { Number = i, Role = "button", Name = "B" + i, Box = new Rectangle(100 * i, 200, 80, 40) });
            if (Listing.Text(thin, shown) != null) throw new Exception("A thin control list replaced a picture.");
            if (Listing.Read(new IntPtr(7)) != null) throw new Exception("A window that does not exist was read.");

            ForkSettings kept = ForkSettings.Current;
            Func<IntPtr> frontWas = Computer.Front;
            Func<IntPtr, Sight> readWas = Listing.Read;
            try
            {
                ForkSettings on = kept.Copy();
                on.HandsListOnly = true;
                ForkSettings.Current = on;
                Computer.Front = delegate { return new IntPtr(7); };
                Sight read = RichSight();
                int reads = 0;
                Listing.Forget();
                Listing.Read = delegate(IntPtr window) { reads++; return read; };
                if (Listing.Instead(new IntPtr(7), shown, 0) == null || Listing.Instead(new IntPtr(7), shown, 1) == null) throw new Exception("A rich window after a step still took a picture.");
                if (Listing.Instead(new IntPtr(7), shown, Listing.MostUnseen) != null) throw new Exception("More than two looks in a row went without a picture.");
                if (Listing.Instead(new IntPtr(8), shown, 0) != null || Listing.Instead(IntPtr.Zero, shown, 0) != null || Listing.Instead(new IntPtr(7), null, 0) != null) throw new Exception("A new window or a missing picture got a list.");
                read.Problem = "This window took too long to read.";
                if (Listing.Instead(new IntPtr(7), shown, 0) != null) throw new Exception("A window that could not be read got a list.");
                read.Problem = null;
                int before = reads;
                if (Listing.Instead(new IntPtr(7), shown, 0) != null || reads != before) throw new Exception("A window that came back thin or slow was read again in the same job.");
                Listing.Forget();
                if (Listing.Instead(new IntPtr(7), shown, 0) == null) throw new Exception("A new job did not try the window again.");
                read.Program = "League of Legends";
                read.Title = "League of Legends (TM) Client";
                if (Listing.Instead(new IntPtr(7), shown, 0) != null) throw new Exception("A game window got a list instead of a picture.");
                read = RichSight();
                Listing.Forget();
                ForkSettings off = kept.Copy();
                off.HandsListOnly = false;
                ForkSettings.Current = off;
                if (Listing.Instead(new IntPtr(7), shown, 0) != null) throw new Exception("A list replaced a picture with the setting off.");
            }
            finally
            {
                ForkSettings.Current = kept;
                Computer.Front = frontWas;
                Listing.Read = readWas;
            }
        }

        private static void SmokeHandsListJob()
        {
            ForkSettings kept = ForkSettings.Current;
            Func<Rectangle?, Picture> shootWas = Computer.Shoot;
            Func<Sight> glimpseWas = Computer.Glimpse;
            Func<Target> focusWas = Computer.Focus;
            Func<Point, Target> underWas = Computer.Under;
            Func<IntPtr> frontWas = Computer.Front;
            Func<IntPtr, Sight> readWas = Listing.Read;
            Func<string, ToolArguments, Picture, string> useWas = Computer.Use;
            try
            {
                ForkSettings on = kept.Copy();
                on.HandsListOnly = true;
                on.HandsWindowOnly = false;
                ForkSettings.Current = on;
                List<Rectangle?> shots = new List<Rectangle?>();
                Computer.Shoot = delegate(Rectangle? part) { shots.Add(part); return new Picture { Jpeg = new byte[] { 1, 2, 3 }, Size = new Size(1280, 720), Area = new Rectangle(0, 0, 2560, 1440) }; };
                Computer.Glimpse = delegate { return new Sight { Window = new IntPtr(7), Title = "Steam", Program = "steam" }; };
                Computer.Focus = delegate { return new Target { Role = "box", Name = "Search" }; };
                Computer.Under = delegate(Point at) { return new Target { Role = "button", Name = "Game 1" }; };
                Computer.Front = delegate { return new IntPtr(7); };
                Listing.Read = delegate(IntPtr window) { return RichSight(); };
                string result = "Clicked.";
                Computer.Use = delegate(string name, ToolArguments args, Picture seen) { return result; };
                string click = "{\"id\":\"m\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t\",\"name\":\"left_click\",\"toolset_name\":\"computer\",\"input\":{\"coordinate\":[70,110]}}],\"stop_reason\":\"tool_use\"}";
                string[] script = { click, click, click, "{\"id\":\"m4\",\"content\":[{\"type\":\"text\",\"text\":\"Clicked through the games.\"}],\"stop_reason\":\"end_turn\"}" };
                int turn = 0;
                List<string> sent = new List<string>();
                Job job = new Job("click through my steam games");
                job.Drive = delegate(string system, string toolset, string messages) { sent.Add(messages); return Brain.Parsed(script[Math.Min(turn++, script.Length - 1)]); };
                HandsMeter.Start();
                string said = job.Run();
                if (said != "Clicked through the games." || shots.Count != 2 || sent.Count != 4 || !sent[1].Contains(Listing.Opening) || !HandsMeter.Line().Contains("lists 2"))
                    throw new Exception("A job did not swap two pictures for control lists and then take a picture: " + said + " / " + shots.Count + " shots / " + HandsMeter.Line());
                shots.Clear();
                turn = 2;
                result = "That didn't work: nothing there.";
                Job failing = new Job("click a steam game");
                failing.Drive = delegate(string system, string toolset, string messages) { return Brain.Parsed(script[Math.Min(turn++, script.Length - 1)]); };
                failing.Run();
                if (shots.Count != 2) throw new Exception("A failed step got a control list instead of a picture: " + shots.Count + " shots");
                HandsMeter.Start();
            }
            finally
            {
                ForkSettings.Current = kept;
                Computer.Shoot = shootWas;
                Computer.Glimpse = glimpseWas;
                Computer.Focus = focusWas;
                Computer.Under = underWas;
                Computer.Front = frontWas;
                Listing.Read = readWas;
                Computer.Use = useWas;
                Computer.Finish();
            }
        }

        private static void SmokeHandsRemember()
        {
            string[] steps = { "left_click: Clicked Library.", "left_click: Clicked Library.", "type: Typed \"secret words\" and pressed Enter.", "left_click: That didn't work: nothing there.", "note: Noted the price.", "left_click: Clicked Installed locally.", "type: Typed " + Computer.Dots + ".", "click: Clicked [12] button \"Installed locally\"." };
            string[] kept = Ways.Kept(steps);
            if (kept.Length != 4 || kept[0] != "left_click: Clicked Library." || kept[1] != "type: Typed the text the job needs." || kept[2] != "left_click: Clicked Installed locally." || kept[3] != "click: Clicked button \"Installed locally\".")
                throw new Exception("The kept steps were wrong: " + string.Join(" | ", kept));
            if (Ways.Likeness("show only installed games in my steam library", "show only the installed games in steam library please") < Ways.Alike || Ways.Likeness("show only installed games in my steam library", "play some music") > 0 || Ways.Likeness("open steam", "open steam library show installed") > 0)
                throw new Exception("Alike jobs were not matched, or different ones were.");
            string folder = Path.Combine(Path.GetTempPath(), "capri-ways-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Func<string> pathWas = Ways.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            try
            {
                string file = Path.Combine(folder, "ways.json");
                Ways.FilePath = delegate { return file; };
                ForkSettings on = keptSettings.Copy();
                on.HandsRemember = true;
                ForkSettings.Current = on;
                string words = "show only installed games in my steam library";
                Ways.Learn(words, steps, "done");
                string saved = File.Exists(file) ? File.ReadAllText(file) : "";
                if (Ways.All().Count != 1 || saved.Contains("secret words") || saved.Contains(Computer.Dots) || !saved.Contains("Installed locally")) throw new Exception("A job that worked was not kept, or kept what was typed: " + saved);
                string offered = Ways.With("He likes Steam.", "show only the installed games in steam library please");
                if (!offered.StartsWith("He likes Steam.", StringComparison.Ordinal) || !offered.Contains(Ways.Marker) || !offered.Contains("2. type: Typed the text the job needs.") || !offered.Contains("Follow them when the screen matches"))
                    throw new Exception("The kept way was not offered to a job like it: " + offered);
                if (Ways.With(offered, words) != offered || Ways.With("x", "play some music") != "x") throw new Exception("A way was offered twice or to a different job.");
                Ways.Learn(words, steps, "asked");
                Ways.Learn(words, steps, "stopped");
                if (Ways.All().Count != 1) throw new Exception("A job that was asked or stopped changed the kept ways.");
                Ways.Learn(words, steps, "done");
                if (Ways.All().Count != 1 || Ways.All()[0].Used != 2) throw new Exception("The same job twice was kept twice.");
                Ways.Learn("show the installed games in my steam library", steps, "failed");
                if (Ways.All().Count != 0) throw new Exception("A way that failed was not dropped.");
                for (int i = 0; i < Ways.MostWays + 5; i++) Ways.Learn("job number " + i + " alpha" + i, steps, "done");
                if (Ways.All().Count != Ways.MostWays) throw new Exception("The kept ways grew past " + Ways.MostWays + ": " + Ways.All().Count);
                ForkSettings off = keptSettings.Copy();
                off.HandsRemember = false;
                ForkSettings.Current = off;
                if (Ways.With("y", "job number 3 alpha3") != "y") throw new Exception("A way was offered with the setting off.");
                Ways.Learn("brand new job here", steps, "done");
                if (Ways.Find("brand new job here") != null) throw new Exception("A way was kept with the setting off.");
            }
            finally
            {
                Ways.FilePath = pathWas;
                ForkSettings.Current = keptSettings;
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private static void SmokeHandsReal()
        {
            PressForm form = null;
            Button button = null;
            CheckBox box = null;
            IntPtr handle = IntPtr.Zero;
            Point buttonAt = Point.Empty, boxAt = Point.Empty, textAt = Point.Empty;
            int clicks = 0, mouseDowns = 0;
            ManualResetEvent ready = new ManualResetEvent(false);
            Thread ui = new Thread(delegate()
            {
                form = new PressForm { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(360, 220), Text = "Capri hands smoke", ShowInTaskbar = false };
                Rectangle work = Stage.Main.WorkingArea;
                form.Location = new Point(work.Left + 60, work.Top + 60);
                button = new Button { Text = "Capri press test", Location = new Point(20, 20), Size = new Size(160, 40) };
                box = new CheckBox { Text = "Capri toggle test", Location = new Point(20, 80), Size = new Size(200, 30) };
                TextBox text = new TextBox { Location = new Point(20, 140), Size = new Size(200, 24) };
                button.Click += delegate { Interlocked.Increment(ref clicks); };
                button.MouseDown += delegate { Interlocked.Increment(ref mouseDowns); };
                form.Controls.Add(button);
                form.Controls.Add(box);
                form.Controls.Add(text);
                form.Shown += delegate
                {
                    handle = form.Handle;
                    buttonAt = button.PointToScreen(new Point(button.Width / 2, button.Height / 2));
                    boxAt = box.PointToScreen(new Point(8, box.Height / 2));
                    textAt = text.PointToScreen(new Point(text.Width / 2, text.Height / 2));
                    ready.Set();
                };
                Application.Run(form);
            });
            ui.SetApartmentState(ApartmentState.STA);
            ui.IsBackground = true;
            ui.Start();
            Func<IntPtr> frontWas = Computer.Front;
            Func<int> ownWas = Computer.Own;
            Action<System.Windows.Automation.AutomationElement> focusWas = ByName.Focus;
            List<string> focused = new List<string>();
            try
            {
                ByName.Focus = delegate(System.Windows.Automation.AutomationElement element) { focused.Add(element.Current.Name); };
                if (!ready.WaitOne(10000)) throw new Exception("The hands test window never showed.");
                Thread.Sleep(300);
                Computer.Front = delegate { return handle; };
                Computer.Own = delegate { return -1; };
                ByName.Forget();
                Thread warm = new Thread(delegate() { try { System.Windows.Automation.AutomationElement.FromPoint(new System.Windows.Point(buttonAt.X, buttonAt.Y)); } catch (Exception) { } });
                warm.IsBackground = true;
                warm.Start();
                warm.Join(10000);
                string pressedButton = ByName.Press(buttonAt, 1);
                for (int wait = 0; wait < 100 && Thread.VolatileRead(ref clicks) == 0; wait++) Thread.Sleep(50);
                if (pressedButton == null || !pressedButton.StartsWith("Invoke Button", StringComparison.Ordinal) || clicks != 1 || mouseDowns != 0) throw new Exception("A real button was not pressed by name without the mouse: " + pressedButton + " / " + ByName.Missed + " / clicks " + clicks + " / mouse downs " + mouseDowns);
                Thread.Sleep(ByName.WaitMilliseconds);
                if (ByName.Press(buttonAt, 1) != null || !ByName.Missed.Contains("already")) throw new Exception("The same button pressed by name twice in a row did not go to the real click: " + ByName.Missed);
                string pressedBox = ByName.Press(boxAt, 1);
                bool ticked = false;
                for (int wait = 0; wait < 100 && !ticked; wait++)
                {
                    ticked = (bool)form.Invoke(new Func<bool>(delegate { return box.Checked; }));
                    if (!ticked) Thread.Sleep(50);
                }
                if (pressedBox == null || !(pressedBox.StartsWith("Toggle CheckBox", StringComparison.Ordinal) || pressedBox.StartsWith("Invoke CheckBox", StringComparison.Ordinal)) || !ticked) throw new Exception("A real checkbox was not ticked by name: " + pressedBox + " / " + ByName.Missed);
                Thread.Sleep(ByName.WaitMilliseconds);
                if (ByName.Press(textAt, 1) != null || ByName.Missed != "a text box") throw new Exception("A text box was pressed by name: " + ByName.Missed);
                if (ByName.Press(buttonAt, 2) != null) throw new Exception("A double click on a button was pressed by name.");
                Computer.Front = delegate { return new IntPtr(12345); };
                if (ByName.Press(buttonAt, 1) != null || ByName.Missed != "not on the window in front") throw new Exception("A control on a window that is not in front was pressed by name: " + ByName.Missed);
                Computer.Front = delegate { return handle; };
                Computer.Own = ownWas;
                if (ByName.Press(buttonAt, 1) != null || ByName.Missed != "her own window") throw new Exception("Her own window was pressed by name: " + ByName.Missed);
                if (clicks != 1 || mouseDowns != 0) throw new Exception("The button was pressed again: clicks " + clicks);
                if (focused.Count != 2 || focused[0] != "Capri press test" || focused[1] != "Capri toggle test") throw new Exception("Keyboard focus did not follow the presses by name: " + string.Join(" | ", focused.ToArray()));
            }
            finally
            {
                ByName.Focus = focusWas;
                Computer.Front = frontWas;
                Computer.Own = ownWas;
                ByName.Forget();
                try { if (form != null && form.IsHandleCreated) form.Invoke(new MethodInvoker(form.Close)); }
                catch (Exception) { }
                ui.Join(5000);
            }
        }
    }
}
