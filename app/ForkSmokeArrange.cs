using System;
using System.Collections.Generic;
using System.Drawing;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeArrange(List<string> checks)
        {
            Func<List<OpenWindow>> windowsWas = Hands.Windows;
            Func<List<Arrange.Display>> displaysWas = Arrange.Displays;
            Func<IntPtr, Arrange.Spot> readWas = Arrange.Read;
            Func<IntPtr, string, Rectangle, bool> shapeWas = Arrange.Shape;
            Func<IntPtr, bool> raisedWas = Arrange.Raised, hungWas = Arrange.Hung, pinnedWas = Arrange.Pinned;
            Func<IntPtr, bool, bool> pinWas = Arrange.Pin;
            Func<Point> mouseWas = Arrange.Mouse;
            Func<string> monitorsWas = PcTools.Monitors;
            int patienceWas = Arrange.Patience;
            try
            {
                Arrange.Display main = new Arrange.Display { Number = 1, Primary = true, Bounds = new Rectangle(0, 0, 1920, 1080), Work = new Rectangle(0, 0, 1920, 1040) };
                Arrange.Display side = new Arrange.Display { Number = 2, Bounds = new Rectangle(1920, 0, 2560, 1440), Work = new Rectangle(1920, 0, 2560, 1400) };
                List<Arrange.Display> desk = new List<Arrange.Display> { main, side };
                Arrange.Named(desk);
                Dictionary<IntPtr, Arrange.Spot> spots = new Dictionary<IntPtr, Arrange.Spot>();
                Dictionary<IntPtr, bool> pinned = new Dictionary<IntPtr, bool>();
                IntPtr discord = new IntPtr(21), spotify = new IntPtr(22);
                spots[discord] = new Arrange.Spot { Edges = new Rectangle(100, 100, 1000, 700) };
                spots[spotify] = new Arrange.Spot { Edges = new Rectangle(300, 200, 1200, 800) };
                pinned[discord] = false;
                pinned[spotify] = false;
                Arrange.Displays = delegate { return desk; };
                Arrange.Read = delegate(IntPtr handle) { Arrange.Spot spot; return spots.TryGetValue(handle, out spot) ? new Arrange.Spot { Edges = spot.Edges, Minimized = spot.Minimized, Maximized = spot.Maximized } : new Arrange.Spot { Gone = true }; };
                Arrange.Shape = delegate(IntPtr handle, string how, Rectangle wanted)
                {
                    Arrange.Spot spot = spots[handle];
                    if (how == "restore") spots[handle] = new Arrange.Spot { Edges = spot.Edges, Maximized = spot.Minimized && spot.Maximized };
                    else if (how == "maximize") spots[handle] = new Arrange.Spot { Edges = main.Work, Maximized = true };
                    else spots[handle] = new Arrange.Spot { Edges = wanted };
                    return true;
                };
                Arrange.Raised = delegate { return false; };
                Arrange.Hung = delegate { return false; };
                Arrange.Pinned = delegate(IntPtr handle) { return pinned[handle]; };
                Arrange.Pin = delegate(IntPtr handle, bool on) { pinned[handle] = on; return true; };
                Arrange.Mouse = delegate { return new Point(50, 50); };
                Arrange.Patience = 1;
                Hands.Windows = delegate { return new List<OpenWindow> { new OpenWindow(discord, "#general - Discord", "Discord"), new OpenWindow(spotify, "Spotify Premium", "Spotify") }; };

                string snapped = Hands.Do("app_window", new ToolArguments { Name = "Discord", Action = "snap_left" });
                if (snapped != "Snapped Discord to the left half of the main screen." || spots[discord].Edges != Arrange.Half(main.Work, "left")) throw new Exception("Snap left read wrong: " + snapped);
                string moved = Hands.Do("app_window", new ToolArguments { Name = "Spotify", Action = "move", Position = "center", Monitor = "2" });
                if (!moved.StartsWith("Moved Spotify to the middle of screen 2", StringComparison.Ordinal) || !side.Work.Contains(spots[spotify].Edges)) throw new Exception("Moving to the middle of monitor 2 (her monitor word) read wrong: " + moved);
                string resized = Hands.Do("app_window", new ToolArguments { Name = "Spotify", Action = "resize", Width = "50%", Height = "50%" });
                if (resized != "Resized Spotify to 1280 x 700." || spots[spotify].Edges.Size != new Size(1280, 700)) throw new Exception("Resizing to half the monitor read wrong: " + resized);
                string unread = Hands.Do("app_window", new ToolArguments { Name = "Spotify", Action = "resize", Width = "huge", Height = "50%" });
                if (!unread.StartsWith("Couldn't resize Spotify: huge isn't a size.", StringComparison.Ordinal) || !Hands.Failed(unread)) throw new Exception("A size that isn't one wasn't turned down: " + unread);
                if (Hands.Do("app_window", new ToolArguments { Name = "Discord", Action = "pin_on_top" }) != "Kept Discord on top of your other windows. Say unpin Discord to let it go." || !pinned[discord]) throw new Exception("Pin on top didn't keep Discord on top.");
                if (Hands.Do("app_window", new ToolArguments { Name = "Discord", Action = "pin_on_top" }) != "Discord is already kept on top of your other windows.") throw new Exception("A second pin wasn't told as already on top.");
                if (Hands.Do("app_window", new ToolArguments { Name = "Discord", Action = "unpin" }) != "Discord isn't kept on top any more." || pinned[discord]) throw new Exception("Unpin didn't let Discord go.");
                spots[discord] = new Arrange.Spot { Edges = spots[discord].Edges, Minimized = true };
                if (Hands.Do("app_window", new ToolArguments { Name = "Discord", Action = "restore" }) != "Brought Discord back up." || spots[discord].Minimized) throw new Exception("Restore didn't bring a minimized Discord back up.");
                string nowhere = Hands.Do("app_window", new ToolArguments { Name = "Discord", Action = "move", Position = "center", Monitor = "5" });
                if (!nowhere.StartsWith("There's no screen 5.", StringComparison.Ordinal)) throw new Exception("A monitor that isn't there wasn't turned down: " + nowhere);

                if (!Hands.Valid("app_window", new ToolArguments { Name = "Discord", Action = "snap_right" }) || !Hands.Valid("app_window", new ToolArguments { Name = "Discord", Action = "pin_on_top" })) throw new Exception("New window actions were refused by the tool check.");
                if (Hands.Valid("app_window", new ToolArguments { Name = "Discord", Action = "resize" }) || Hands.Valid("app_window", new ToolArguments { Name = "Discord", Action = "move" }) || Hands.Valid("app_window", new ToolArguments { Name = "Discord", Action = "move", Position = "upside" })) throw new Exception("A move or resize with nothing to go by passed the tool check.");
                if (!Hands.Valid("app_window", new ToolArguments { Name = "League of Legends", Action = "move_to_monitor", Monitor = "2" }) || !Hands.Valid("app_window", new ToolArguments { Name = "Discord", Action = "maximize" })) throw new Exception("The old window actions stopped passing.");
                if (Security.Asking("app_window", new ToolArguments { Name = "Discord", Action = "snap_left" }) != "snap Discord to the left half of its screen" || Security.Asking("app_window", new ToolArguments { Name = "Discord", Action = "pin_on_top" }) != "keep Discord on top of your other windows") throw new Exception("Her ask before a window move named the wrong move.");
                foreach (string word in new[] { "snap_left", "snap_bottom", "restore", "resize", "pin_on_top", "unpin", "\"position\"", "\"width\"" })
                    if (Hands.Tools.IndexOf(word, StringComparison.Ordinal) < 0) throw new Exception("Her brain isn't offered " + word + " in app_window.");
                if (Lockout.Handles("app_window")) throw new Exception("Window moves aren't input, yet the game lockout took them.");

                PcTools.Monitors = Arrange.Monitors;
                string listed = PcTools.ListWindows(null);
                if (listed.IndexOf("Monitors, left to right: main screen 1920 x 1080; screen 2 2560 x 1440.", StringComparison.Ordinal) < 0) throw new Exception("list_windows didn't name the monitors: " + listed);
            }
            finally
            {
                Hands.Windows = windowsWas;
                Arrange.Displays = displaysWas;
                Arrange.Read = readWas;
                Arrange.Shape = shapeWas;
                Arrange.Raised = raisedWas;
                Arrange.Hung = hungWas;
                Arrange.Pinned = pinnedWas;
                Arrange.Pin = pinWas;
                Arrange.Mouse = mouseWas;
                Arrange.Patience = patienceWas;
                PcTools.Monitors = monitorsWas;
            }
            checks.Add("PASS everything is a tool, window arranging (096-07): app_window snaps a window to half its monitor, moves it to a spot on the monitor she names, resizes it by percent, keeps it on top until unpin, brings a minimized one back up, turns down a size that isn't one and a monitor that isn't there, the old switch, minimize, close, maximize and move_to_monitor still pass, her ask names the move, her brain is offered the new moves, and list_windows names the monitors");
        }
    }
}
