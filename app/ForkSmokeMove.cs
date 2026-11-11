using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeWalkSpeed(List<string> checks)
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "capri-walk-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            int kept = ForkSettings.Current.WalkSpeed;
            System.Drawing.Point keptAnchor = anchor;
            Ledge keptSeat = seat;
            try
            {
                string old = System.IO.Path.Combine(folder, "old.json");
                System.IO.File.WriteAllText(old, "{\"Proactive\":1}");
                if (ForkSettings.Read(old).WalkSpeed != 2 || new ForkSettings().WalkSpeed != 2) throw new Exception("An old or new fork.json did not start at twice her old walking speed.");
                ForkSettings tidy = new ForkSettings();
                tidy.WalkSpeed = 50;
                tidy.Tidy();
                if (tidy.WalkSpeed != 6) throw new Exception("A walking speed above 6 was not capped.");
                int[] covered = new int[3];
                for (int speed = 1; speed <= 2; speed++)
                {
                    ForkSettings.Current.WalkSpeed = speed;
                    if (walking) EndWalk();
                    Halt();
                    System.Drawing.Rectangle area = Stage.Main.WorkingArea;
                    Ledge floor = new Ledge { Floor = true, Left = area.Left, Right = area.Right, Y = area.Bottom };
                    Land(floor);
                    Hop(floor, area.Left + preferences.Size);
                    int start = Hip().X;
                    Nudge("right", 3);
                    if (!walking) throw new Exception("She did not start walking for the speed check.");
                    double tick = clock.Elapsed.TotalSeconds;
                    for (int i = 0; i < 20; i++) { tick += 0.05; Walk(tick); }
                    covered[speed] = Hip().X - start;
                    EndWalk();
                }
                if (covered[1] <= 0 || Math.Abs(covered[2] - 2 * covered[1]) > 3) throw new Exception("Walking speed 2 did not walk twice as far in one second as speed 1: " + covered[1] + " and " + covered[2] + " px.");
            }
            finally
            {
                ForkSettings.Current.WalkSpeed = kept;
                if (walking) EndWalk();
                anchor = keptAnchor;
                seat = keptSeat;
                try { System.IO.Directory.Delete(folder, true); } catch { }
            }
            checks.Add("PASS walking speed: she walks twice her old pace by default, even from an old fork.json, the setting (1 to 6) scales every walk and is capped, and the settings window keeps it");
        }

        private void WalkOut()
        {
            double tick = clock.Elapsed.TotalSeconds;
            for (int i = 0; i < 20000 && walking; i++) { tick += 0.05; Walk(tick); }
        }

        private void LandHop(string what, int wantedX, int wantedY)
        {
            if (outro != null) { Action next = outro; outro = null; next(); }
            else if (!instant && preferences.Motion) throw new Exception(what + " did not glitch out to hop, or her reply cancelled the hop.");
            if (Math.Abs(Hip().X - wantedX) > preferences.Size / 4 || seat == null || seat.Y != wantedY) throw new Exception(what + " did not land beside the mouse: hip " + Hip().X + " on " + (seat == null ? "nothing" : seat.Y.ToString()) + ", wanted " + wantedX + " on " + wantedY);
        }

        private void SmokeMoveByVoice(List<string> checks)
        {
            string[,] heard =
            {
                { "go left", "left", "1" }, { "Capri, move right a bit", "right", "0.5" }, { "move to the left a lot", "left", "3" },
                { "hey capri please go right for me", "right", "1" }, { "walk left a little bit", "left", "0.5" }, { "scoot over to the right", "right", "1" },
                { "stay there", "stay", "0" }, { "stop right there", "stay", "0" }, { "stay here please", "stay", "0" },
                { "go all the way left", "edge-left", "-" }, { "move to the right edge", "edge-right", "-" }, { "walk to the left side of the screen", "edge-left", "-" },
                { "go to the middle", "middle", "-" }, { "move yourself to the center of the screen", "middle", "-" }, { "come here", "here", "-" }, { "capri come over here please", "here", "-" }, { "come to me", "here", "-" },
                { "now come here", "here", "-" }, { "ok come here", "here", "-" }, { "hey come here", "here", "-" }, { "okay now come here", "here", "-" }, { "um, come here", "here", "-" }, { "come here capri", "here", "-" }, { "Capri, now come here.", "here", "-" }, { "alright then go left", "left", "1" }, { "so come over here", "here", "-" },
                { "move to the top left corner", "top-left", "-" }, { "go to the bottom right", "edge-right", "-" }, { "move yourself to the top right of the screen", "top-right", "-" }
            };
            for (int i = 0; i < heard.GetLength(0); i++)
            {
                double steps;
                string way = MoveFor(heard[i, 0], out steps);
                if (way != heard[i, 1] || (heard[i, 2] != "-" && Math.Abs(steps - double.Parse(heard[i, 2], System.Globalization.CultureInfo.InvariantCulture)) > 0.001)) throw new Exception("\"" + heard[i, 0] + "\" was heard as " + (way ?? "nothing") + " " + steps + ", not " + heard[i, 1] + " " + heard[i, 2] + ".");
            }
            foreach (string other in new[] { "go left in the next game", "turn left at the corner", "move the window left", "what's left", "move left click", "stay still", "stop", "go to the left lane", "", "tell him to go right", "go to the middle lane", "come here and watch my screen", "move the mouse to the middle", "come to me in the game", "now come here and watch my screen", "so whats left", "then tell him to go right" })
            {
                double ignored;
                if (MoveFor(other, out ignored) != null) throw new Exception("\"" + other + "\" was taken as a move command.");
            }
            if (WalkGoal(100, 0, 500, 200) != 300 || WalkGoal(400, 0, 500, 200) != 500 || WalkGoal(50, 0, 500, -200) != 0 || WalkGoal(500, 0, 500, 100) != 500) throw new Exception("The walk target was not clamped to the room she has.");
            if (WalkGoal(600, 0, 500, 200) != 600 || WalkGoal(-100, 0, 500, -200) != -100) throw new Exception("She would walk the wrong way when she starts outside her room.");
            if (BesideMouse(1000, 400, 100) != 960 || BesideMouse(1000, 1500, 100) != 1040 || BesideMouse(1000, 1000, 100) != 1040) throw new Exception("Come here did not stop on the side she comes from, a body-width gap from the mouse.");
            if (!CanWalkTo(true, false, 0, 0, 5000) || !CanWalkTo(true, true, 200, 900, 500) || CanWalkTo(true, true, 200, 900, 1500) || CanWalkTo(false, false, 0, 0, 500) || CanWalkTo(false, true, 200, 900, 500)) throw new Exception("Come here walked when the mouse was out of her reach (another window or another monitor) instead of hopping, or hopped when she could just walk.");
            if (TweakFor("stay there") != null || TweakFor("go left") != null) throw new Exception("A move command was taken as a setting.");
            bool keptWalking = walking;
            walking = false;
            if (MoveHeard("stay there", true)) throw new Exception("Stay there was taken while she was not walking, so a bare stop could be swallowed.");
            string said = Nudge("right", 0.5);
            if (said.StartsWith("Going", StringComparison.Ordinal) && !walking) throw new Exception("She said she was going but never started walking.");
            if (walking && !MoveHeard("stay there", true)) throw new Exception("Stay there did not stop a walk in progress.");
            if (walking) throw new Exception("She kept walking after stay there.");
            walking = keptWalking;
            if (!OnHerLine(1400, 1400, 100) || !OnHerLine(1330, 1400, 100) || OnHerLine(600, 1400, 100)) throw new Exception("Her line was read wrong for come here.");
            Func<System.Drawing.Point> keptMouse = Mouse;
            System.Drawing.Point keptAnchor = anchor;
            Ledge keptSeat = seat;
            try
            {
                if (walking) EndWalk();
                Halt();
                System.Drawing.Rectangle area = Stage.Main.WorkingArea;
                Land(new Ledge { Floor = true, Left = area.Left, Right = area.Right, Y = area.Bottom });
                int startHip = Hip().X;
                int far = startHip < area.Left + area.Width / 2 ? startHip + preferences.Size * 5 / 2 : startHip - preferences.Size * 5 / 2;
                Mouse = delegate { return new System.Drawing.Point(far, area.Bottom - 10); };
                string came = Nudge("here", 0);
                Say(came);
                if (!walking) throw new Exception("Her own reply to come here stopped the walk she had just started.");
                double tick = clock.Elapsed.TotalSeconds;
                for (int i = 0; i < 20000 && walking; i++) { tick += 0.05; Walk(tick); }
                int wanted = BesideMouse(far, startHip, preferences.Size);
                if (came != "Coming over." || walking || Math.Abs(Hip().X - wanted) > preferences.Size / 4) throw new Exception("Come here did not walk her to the mouse on her own line: " + came + " / from " + startHip + " to " + Hip().X + ", wanted " + wanted);
                Say(null);
                Ledge floor = new Ledge { Floor = true, Left = area.Left, Right = area.Right, Y = area.Bottom };
                Land(floor);
                Hop(floor, area.Left + preferences.Size);
                int farHip = Hip().X;
                int farAway = area.Right - preferences.Size / 2;
                Mouse = delegate { return new System.Drawing.Point(farAway, area.Bottom - 10); };
                string farCame = Nudge("here", 0);
                Say(farCame);
                if (farCame != "Coming over." || !walking) throw new Exception("Come here with the mouse far along her own line did not walk: " + farCame);
                WalkOut();
                if (outro != null || seat != floor || Math.Abs(Hip().X - BesideMouse(farAway, farHip, preferences.Size)) > preferences.Size / 4) throw new Exception("Come here with the mouse far along her own line did not walk the whole way to it: hip " + Hip().X + ", glitching " + (outro != null));
                Say(null);
                Ledge shelf = new Ledge { Floor = true, Left = area.Left + area.Width / 8, Right = area.Right - area.Width / 8, Y = area.Top + area.Height / 2 };
                Land(shelf);
                Hop(shelf, shelf.Left + preferences.Size);
                int shelfHip = Hip().X;
                int shelfMouse = shelf.Right - preferences.Size / 2;
                Mouse = delegate { return new System.Drawing.Point(shelfMouse, shelf.Y + 20); };
                Nudge("here", 0);
                if (!walking) throw new Exception("Come here far along the window she sits on, with the mouse just under its top edge, did not walk along it.");
                WalkOut();
                if (outro != null || seat != shelf || Math.Abs(Hip().X - BesideMouse(shelfMouse, shelfHip, preferences.Size)) > preferences.Size / 4) throw new Exception("Come here along the window she sits on did not end beside the mouse on that window: hip " + Hip().X + ", glitching " + (outro != null));
                Land(floor);
                Hop(floor, area.Left + preferences.Size);
                int upHip = Hip().X;
                System.Drawing.Point up = new System.Drawing.Point(area.Left + area.Width / 2, area.Top + 5);
                Mouse = delegate { return up; };
                string upCame = Nudge("here", 0);
                Say(upCame);
                if (upCame != "Coming over." || !walking) throw new Exception("Come here with the mouse high above her did not first walk along her line to under it: " + upCame);
                WalkOut();
                LandHop("Come here with the mouse high above her", BesideMouse(up.X, upHip, preferences.Size), Math.Max(area.Top + Headroom(), Math.Min(area.Bottom, up.Y + preferences.Size / 4)));
                if (!seat.Free) throw new Exception("Come here with the mouse high above her did not sit in the air beside it.");
                Say(null);
                int airHip = Hip().X;
                int airY = seat.Y;
                System.Drawing.Point level = new System.Drawing.Point(airHip < area.Left + area.Width / 2 ? area.Right - preferences.Size : area.Left + preferences.Size, airY);
                Mouse = delegate { return level; };
                string airCame = Nudge("here", 0);
                if (airCame != "Coming over." || !walking || outro != null) throw new Exception("Come here while she sat in the air glitched instead of walking to a mouse level with her: " + airCame);
                WalkOut();
                if (outro != null || Math.Abs(Hip().X - BesideMouse(level.X, airHip, preferences.Size)) > preferences.Size / 4 || Math.Abs(Hip().Y - airY) > preferences.Size / 4) throw new Exception("Come here from the air did not walk her to the mouse at her height: hip " + Hip());
                Land(floor);
                Hop(floor, area.Left + preferences.Size);
                Nudge("here", 0);
                Nudge("stay", 0);
                Nudge("right", 1);
                WalkOut();
                if (outro != null) throw new Exception("A go right after stay there still glitched up to the mouse of an earlier come here.");
                Land(new Ledge { Floor = true, Left = area.Left, Right = area.Right, Y = area.Bottom });
                string going =Nudge(Hip().X < area.Left + area.Width / 2 ? "right" : "left", 1);
                Say(going);
                if (!walking) throw new Exception("Her own reply to a go left or right stopped the walk she had just started: " + going);
                EndWalk();
                Say(null);
                if (Stroll() && !walking) throw new Exception("A wander did not start.");
                if (walking) { Say("Hi."); if (walking) throw new Exception("Talking no longer stops a wander of her own."); }
                Say(null);
                if (walking) EndWalk();
                seat = null;
                anchor = new System.Drawing.Point(area.Left + area.Width / 4, area.Bottom - preferences.Size * 2);
                int freeStart = Hip().X;
                int freeFar = freeStart + preferences.Size * 3;
                int freeY = Hip().Y;
                Mouse = delegate { return new System.Drawing.Point(freeFar, freeY); };
                string freeCame = Nudge("here", 0);
                tick = clock.Elapsed.TotalSeconds;
                for (int i = 0; i < 20000 && walking; i++) { tick += 0.05; Walk(tick); }
                int freeWanted = BesideMouse(freeFar, freeStart, preferences.Size);
                if (freeCame != "Coming over." || walking || Math.Abs(Hip().X - freeWanted) > preferences.Size / 4) throw new Exception("Come here did not walk her to the mouse while she stood free on the floor: from " + freeStart + " to " + Hip().X + ", wanted " + freeWanted);
                Halt();
            }
            finally
            {
                Mouse = keptMouse;
                Say(null);
                Halt();
                if (walking) EndWalk();
                anchor = keptAnchor;
                seat = keptSeat;
            }
            checks.Add("PASS move by voice: go, move, walk or scoot left or right (a bit, a little, a lot), and stay there or stop right there, are heard as whole short commands only, also after now, ok, hey, so, um or her name (now come here, come here capri) (go left in the next game, move the window left, stay still and a bare stop are not), the walk target stays inside the room she has, she says what she does, and stay there stops a walk in progress; come here stops a body-width from the mouse on the side she comes from, walks the whole way when the mouse is on her window or floor however far, walks to under it and then glitches up to sit in the air beside it when it is higher, and stay there drops that glitch (her own spoken reply no longer stops a walk he asked for, while talking still stops a wander of her own); on another monitor she glitches straight to beside the mouse");
        }
    }
}
