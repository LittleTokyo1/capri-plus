using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    // "Go left", "move to the middle", "come here" and "stay there" walk her along the window or floor she is on. Only a whole short command counts.
    internal sealed partial class Companion
    {
        private const string MoveVerb = @"(?:go|move|walk|step|scoot|shift|head|run|get)";
        private static readonly Regex MoveWords = new Regex(TweakLead + MoveVerb + @"(?: yourself)?(?<all> all the way)?(?: over)?(?: to)?(?: the| your)? (?<dir>left|right)(?: hand)?(?: (?<amt>a bit|a little bit|a little|a lot|a long way|far|side|edge|end|corner))?(?: of the screen)?" + TweakTail, RegexOptions.CultureInvariant);
        private static readonly Regex CornerWords = new Regex(TweakLead + MoveVerb + @"(?: yourself)?(?: to)?(?: the)? (?<vert>top|bottom|upper|lower) (?<dir>left|right)(?: hand)?(?: corner)?(?: of the screen)?" + TweakTail, RegexOptions.CultureInvariant);
        private static readonly Regex MiddleWords = new Regex(TweakLead + @"(?:go|move|walk|come|head|get)(?: yourself)?(?: over)?(?: to| into| in)?(?: the)? (?:middle|center|centre)(?: of the screen)?" + TweakTail, RegexOptions.CultureInvariant);
        private static readonly Regex HereWords = new Regex(TweakLead + @"(?:come|go|move|walk|head|get)(?: yourself)?(?: over)? (?:here|to me|to my mouse|to my cursor|next to me)" + TweakTail, RegexOptions.CultureInvariant);
        private static readonly Regex StayWords = new Regex(TweakLead + @"(?:stay (?:right )?(?:there|here)|stop (?:right )?(?:there|here))" + TweakTail, RegexOptions.CultureInvariant);

        internal static string MoveFor(string text, out double steps)
        {
            steps = 0;
            string plain = Regex.Replace(Regex.Replace((text ?? "").ToLowerInvariant().Replace("'", "").Replace("’", ""), "[^a-z0-9 ]+", " "), @"\s+", " ").Trim();
            if (plain.Length == 0 || plain.Length > 80) return null;
            if (StayWords.IsMatch(plain)) return "stay";
            Match corner = CornerWords.Match(plain);
            if (corner.Success)
            {
                bool up = corner.Groups["vert"].Value == "top" || corner.Groups["vert"].Value == "upper";
                return (up ? "top-" : "edge-") + corner.Groups["dir"].Value;
            }
            if (MiddleWords.IsMatch(plain)) return "middle";
            if (HereWords.IsMatch(plain)) return "here";
            Match found = MoveWords.Match(plain);
            if (!found.Success) return null;
            string amount = found.Groups["amt"].Value;
            if (found.Groups["all"].Success || amount == "side" || amount == "edge" || amount == "end" || amount == "corner") return "edge-" + found.Groups["dir"].Value;
            steps = amount.Length == 0 ? 1 : amount.EndsWith("bit", StringComparison.Ordinal) || amount.EndsWith("little", StringComparison.Ordinal) ? 0.5 : 3;
            return found.Groups["dir"].Value;
        }

        internal static int WalkGoal(int at, int from, int to, int by)
        {
            int goal = Math.Max(from, Math.Min(to, at + by));
            return (goal - at) * by > 0 ? goal : at;
        }

        private string Nudge(string way, double steps)
        {
            goUp = false;
            if (way == "stay")
            {
                if (!walking) return "I'm not going anywhere, choom.";
                EndWalk();
                return "Okay, staying right here.";
            }
            if (walking) EndWalk();
            Func<int, int, int, int> goal;
            string said = "Going " + way + ".";
            if (way == "left" || way == "right")
            {
                int by = (int)(steps * preferences.Size) * (way == "left" ? -1 : 1);
                goal = delegate(int at, int from, int to) { return WalkGoal(at, from, to, by); };
            }
            else if (way == "middle")
            {
                goal = delegate(int at, int from, int to) { return (from + to) / 2; };
                said = "Heading to the middle.";
            }
            else if (way == "here") return Come();
            else
            {
                bool leftSide = way.EndsWith("left", StringComparison.Ordinal);
                goal = delegate(int at, int from, int to) { return leftSide ? from : to; };
                said = way.StartsWith("top-", StringComparison.Ordinal) ? "I can't float up there, choom, so I'm taking the " + (leftSide ? "left" : "right") + " edge." : "Heading to the " + (leftSide ? "left" : "right") + " edge.";
            }
            if (!Stroll(goal)) return "Nowhere to go that way from here, choom.";
            ledTo = walkTo;
            nextWander = -1;
            return said;
        }

        internal static int BesideMouse(int mouseX, int fromX, int size)
        {
            int gap = (int)(size * 0.4);
            return fromX < mouseX ? mouseX - gap : mouseX + gap;
        }

        internal static bool CanWalkTo(bool sameScreen, bool onSeat, int seatLeft, int seatRight, int mouseX)
        {
            return sameScreen && (!onSeat || (mouseX >= seatLeft && mouseX <= seatRight));
        }

        internal static bool OnHerLine(int mouseY, int lineY, int size) { return Math.Abs(mouseY - lineY) <= size; }

        internal static Func<Point> Mouse = delegate { return Stage.Pointer(); };

        private int ledTo = int.MinValue;
        private bool goUp;

        private bool LedWalk() { return walking && walkTo == ledTo; }

        private static double WalkPace() { return ForkSettings.Current == null ? 1 : ForkSettings.Current.WalkSpeed; }

        private void Arrived()
        {
            if (!goUp) return;
            goUp = false;
            Point mouse = Mouse();
            int x = BesideMouse(mouse.X, Hip().X, preferences.Size);
            GlitchOut(delegate { Appear(mouse, x); }, false);
        }

        private void Appear(Point mouse, int x)
        {
            Rectangle area = Screen.FromPoint(mouse).WorkingArea;
            int y = Math.Max(area.Top + Headroom(), Math.Min(area.Bottom, mouse.Y + preferences.Size / 4));
            Hop(new Ledge { Left = x - 8, Right = x + 8, Y = y, Floor = true, Free = true }, x);
        }

        private string Come()
        {
            if (riding || onPanel || falling || hanging || dragging || outro != null) return "Can't come over right now, choom.";
            Point mouse = Mouse();
            Point hip = Hip();
            Screen mine = Screen.FromPoint(hip);
            Screen theirs = Screen.FromPoint(mouse);
            int stop = BesideMouse(mouse.X, hip.X, preferences.Size);
            string way;
            bool same = mine.DeviceName == theirs.DeviceName;
            Ledge air = same && seat != null && seat.Free ? seat : null;
            if (air != null) seat = null;
            bool level = OnHerLine(mouse.Y, seat != null ? seat.Y : hip.Y, preferences.Size);
            bool reach = level && CanWalkTo(same, seat != null, seat == null ? 0 : seat.Left, seat == null ? 0 : seat.Right, mouse.X);
            Func<int, int, int, int> goal = delegate(int at, int from, int to) { return Math.Max(from, Math.Min(to, seat != null ? stop : Seated(stop, 0).X)); };
            if (same && Stroll(goal))
            {
                ledTo = walkTo;
                if (!reach) goUp = true;
                way = reach ? "walk" : "walk, then up";
            }
            else if (reach) { if (air != null) seat = air; way = "none"; }
            else
            {
                if (air != null) seat = air;
                GlitchOut(delegate { Appear(mouse, stop); }, false);
                way = "glitch";
            }
            ActivityLog.Write("move", "here", way + ", mouse " + mouse.X + "," + mouse.Y + ", her " + hip.X + "," + hip.Y + (seat != null ? ", on " + seat.Left + "-" + seat.Right + " at " + seat.Y : ", free"));
            if (way == "none") return "I'm already next to you, choom.";
            nextWander = -1;
            return "Coming over.";
        }

        private bool MoveHeard(string text, bool voiceChat)
        {
            double steps;
            string way = MoveFor(text, out steps);
            if (way == null || (way == "stay" && !walking)) return false;
            string said = Nudge(way, steps);
            ActivityLog.Write("move", way, said);
            if (!voiceChat) Tell("(move)", said);
            return true;
        }
    }
}
