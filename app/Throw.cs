using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        // While she's thrown her fall is a flight, which Tumble hands to Fly.
        private bool Flying { get { return falling && thrown; } }

        // A gap in their cursor's track with no move in it means it sat still until just before the next one.
        private const double TrailGap = 0.03;

        // How fast their cursor went over the last span seconds up to now, in pixels a second: from where it was then to where
        // it is, going straight between the spots it was seen at. Before the first spot it was there.
        internal static PointF TrailSpeed(System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<double, Point>> track, double now, double span)
        {
            if (track.Count == 0 || span <= 0) return PointF.Empty;
            PointF to = TrailAt(track, now), from = TrailAt(track, now - span);
            return new PointF((float)((to.X - from.X) / span), (float)((to.Y - from.Y) / span));
        }

        private static PointF TrailAt(System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<double, Point>> track, double at)
        {
            if (at <= track[0].Key) return track[0].Value;
            for (int i = 1; i < track.Count; i++)
            {
                if (at > track[i].Key) continue;
                double began = Math.Max(track[i - 1].Key, track[i].Key - TrailGap), length = track[i].Key - began;
                double part = at <= began ? 0 : length <= 0 ? 1 : (at - began) / length;
                Point a = track[i - 1].Value, b = track[i].Value;
                return new PointF((float)(a.X + (b.X - a.X) * part), (float)(a.Y + (b.Y - a.Y) * part));
            }
            return track[track.Count - 1].Value;
        }

        // Let go while their cursor's still going fast, she's thrown at its speed (Zr4, F63: "grab her and throw her by mouse
        // speed"). Slower, she drops like before, and mid-job dragging her stays as it was.
        private bool Throw(double now)
        {
            if (!ThrowOn(ForkSettings.Current) || onPanel || hidden || docked || riding || working != null || job != null || trail.Count == 0) return false;
            PointF speed = TrailSpeed(trail, now, ThrowSpan);
            double scale = TalkScale(), vx = speed.X, vy = speed.Y, fast = Math.Sqrt(vx * vx + vy * vy);
            if (fast < ThrowFrom * scale) return false;
            double most = ThrowMost * scale;
            if (fast > most) { vx *= most / fast; vy *= most / fast; fast = most; }
            Launch(vx, vy, now);
            Grudge(fast / scale, now);
            return true;
        }

        // She leaves their hand at vx, vy pixels a second, kept to the screen she's on.
        private void Launch(double vx, double vy, double now)
        {
            SitUp(false);
            seat = null;
            hanging = false;
            walking = false;
            dropAfter = false;
            fallTo = null;
            falling = true;
            thrown = true;
            fallSpeed = 0;
            fallStep = now;
            flyX = anchor.X;
            flyY = anchor.Y;
            flyAt = anchor;
            flyVX = vx;
            flyVY = vy;
            flyArea = Screen.FromPoint(new Point(anchor.X + Width / 2, anchor.Y + Height / 2)).WorkingArea;
            flyBegan = now;
            flyHardest = 0;
            flyBumped = -1;
            flyBounces = flyFloors = 0;
            flyLedges = null;
            dizzyUntil = -1;
            leanAngle = 0;
            spin = 0;
            // She tumbles the way she's thrown, faster the harder it is.
            spinRate = preferences.Motion ? Math.Max(-1080, Math.Min(1080, vx * 0.28 + (vx < 0 ? -1 : 1) * Math.Abs(vy) * 0.1)) : 0;
            Play(dangle);
            frames.Start();
        }

        // One frame of her flight: gravity and air drag, bounces off her screen's sides, top and taskbar that lose some speed each
        // time, and a ledge she comes down onto catches her like a fall. A soft bounce off the taskbar lands her on it.
        private void Fly(double now)
        {
            double step = Math.Max(0, Math.Min(0.05, now - fallStep));
            fallStep = now;
            // Moved by something else mid-flight, like a screen going away, she carries on from there.
            if (anchor != flyAt) { flyX = anchor.X; flyY = anchor.Y; }
            if (Screen.FromRectangle(flyArea).WorkingArea != flyArea) flyArea = Screen.FromPoint(new Point(anchor.X + Width / 2, anchor.Y + Height / 2)).WorkingArea;
            if (clip == idle) Play(dangle);
            int size = preferences.Size, hipDown = PaddingSize + (int)(size * SitY), hipAcross = PaddingSize + (int)(size * SitX);
            bool sits = !preferences.NoSitting;
            RectangleF body = Body;
            double left = flyArea.Left - PaddingSize - (int)(size * body.Left), right = flyArea.Right - PaddingSize - (int)Math.Ceiling(size * body.Right);
            double top = flyArea.Top - PaddingSize - (int)(size * body.Top);
            // She bounces where she'd sit on the taskbar, her hip on its edge, but never with her feet past the bottom of the screen.
            double feet = flyArea.Bottom - PaddingSize - (int)Math.Ceiling(size * body.Bottom);
            double floor = sits ? Math.Min(flyArea.Bottom - hipDown, Screen.FromRectangle(flyArea).Bounds.Bottom - PaddingSize - (int)Math.Ceiling(size * body.Bottom)) : feet;
            if (sits && (flyLedges == null || now - flyLedgesAt >= 0.25)) { flyLedges = Around(); flyLedgesAt = now; }
            double gravity = size * 11, settle = size * 2.2, calm = 900 * TalkScale();
            int steps = Math.Max(1, (int)Math.Ceiling(step / 0.004));
            double dt = step / steps;
            for (int i = 0; i < steps; i++)
            {
                double hipWas = flyY + hipDown;
                flyVY += gravity * dt;
                double keep = Math.Exp(-ThrowDrag * dt);
                flyVX *= keep;
                flyVY *= keep;
                flyX += flyVX * dt;
                flyY += flyVY * dt;
                Spin(dt, calm);
                if (flyX < left) { Hit(Math.Abs(flyVX), now); flyX = Math.Min(right, left + (left - flyX) * WallBounce); flyVX = Math.Abs(flyVX) * WallBounce; spinRate = -spinRate * 0.8; }
                else if (flyX > right) { Hit(Math.Abs(flyVX), now); flyX = Math.Max(left, right - (flyX - right) * WallBounce); flyVX = -Math.Abs(flyVX) * WallBounce; spinRate = -spinRate * 0.8; }
                if (flyY < top) { Hit(Math.Abs(flyVY), now); flyY = Math.Min(floor, top + (top - flyY) * WallBounce); flyVY = Math.Abs(flyVY) * WallBounce; }
                if (sits && flyVY > 0)
                {
                    double hipNow = flyY + hipDown;
                    int hipX = (int)Math.Round(flyX) + hipAcross;
                    Ledge onto = null;
                    foreach (Ledge ledge in flyLedges)
                    {
                        if (ledge.Floor || ledge.Y < hipWas || ledge.Y > hipNow || ledge.Right - ledge.Left < 24 || hipX < ledge.Left || hipX > ledge.Right) continue;
                        if (ledge.Y - Headroom() < Screen.FromPoint(new Point(hipX, ledge.Y)).Bounds.Top) continue;
                        if (onto == null || ledge.Y < onto.Y) onto = ledge;
                    }
                    if (onto != null)
                    {
                        double impact = flyVY;
                        anchor = new Point((int)Math.Round(flyX), onto.Y - hipDown);
                        Land(onto);
                        if (seat == onto) { Landed(impact, now); return; }
                        // Its window's gone or can't seat her any more: she flies on past it.
                        flyLedges.Remove(onto);
                    }
                }
                if (flyY >= floor)
                {
                    double impact = flyVY;
                    flyY = floor;
                    if (impact > settle && flyFloors < 6) { Hit(impact, now); flyVY = -impact * FloorBounce; flyVX *= FloorGrip; spinRate *= 0.6; flyFloors++; }
                    else { Settle(impact, now, sits); return; }
                }
            }
            anchor = new Point((int)Math.Round(flyX), (int)Math.Round(flyY));
            flyAt = anchor;
            // A flight that somehow never settles carries on as a plain fall.
            if (now - flyBegan > FlightLongest) { thrown = false; if (!sits) { falling = false; SavePreferences(); } }
        }

        // Her tumble slows in the air, and once she's slowed down or bounced off the taskbar she rights herself before she lands.
        // None with reduced motion.
        private void Spin(double dt, double calm)
        {
            if (!preferences.Motion) { spin = spinRate = 0; return; }
            spin += spinRate * dt;
            spinRate *= Math.Exp(-0.6 * dt);
            if (flyFloors == 0 && flyVX * flyVX + flyVY * flyVY >= calm * calm) return;
            double off = spin - 360 * Math.Round(spin / 360);
            spinRate += (-off * 60 - spinRate * 14) * dt;
        }

        // How far she's turned in her tumble, for drawing.
        private float Turn() { return Flying && preferences.Motion ? (float)spin : 0; }

        // She hits a side, the top or the taskbar at impact pixels a second.
        private void Hit(double impact, double now)
        {
            flyBounces++;
            flyHardest = Math.Max(flyHardest, impact);
            if (impact / TalkScale() < BumpFrom || now - flyBumped < 0.12) return;
            flyBumped = now;
            anchor = new Point((int)Math.Round(flyX), (int)Math.Round(flyY));
            Bump();
        }

        // An impact she feels: a thud, and with motion on a short screen tear where she hit.
        private void Bump()
        {
            bumps++;
            if (instant || hidden || !IsHandleCreated) return;
            Sfx.Play(Cue.Bonk);
            Static(false, null);
        }

        // Slow at the taskbar, she lands on it like any fall. With Sit on windows off she just stops there.
        private void Settle(double impact, double now, bool sits)
        {
            anchor = new Point((int)Math.Round(flyX), (int)Math.Round(flyY));
            flyAt = anchor;
            if (sits) Land(new Ledge { Floor = true, Left = flyArea.Left, Right = flyArea.Right, Y = flyArea.Bottom });
            else
            {
                falling = false;
                thrown = false;
                SavePreferences();
            }
            Landed(impact, now);
        }

        // Down after a flight: a hard one thuds, leaves her dizzy for a beat and gets a word out of her, and the third throw in
        // two minutes gets her sulk.
        private void Landed(double impact, double now)
        {
            thrown = false;
            spin = spinRate = 0;
            double scale = TalkScale(), hardest = Math.Max(flyHardest, impact) / scale;
            if (impact / scale >= BumpFrom) Bump();
            bool dizzy = hardest >= DizzyFrom;
            if (dizzy) { dizzyFrom = now; dizzyUntil = now + DizzyFor; dizzyGlitchAt = now + 0.9; }
            if (sulkLine) { sulkLine = false; Yelp("throws you", Lines.Pick(SulkLines), true); }
            else if (dizzy && throwsAt.Count == 1) Yelp("throws you", Lines.Pick(DizzyLines), true);
        }

        private bool Dizzy(double now) { return now >= dizzyFrom && now < dizzyUntil; }

        // Dizzy, her head swims around her hip and settles. Not with reduced motion, or while she's held, riding or falling.
        private float Sway(double now)
        {
            if (!preferences.Motion || !Dizzy(now) || dragging || falling || riding || onPanel) return 0;
            double fade = Math.Min(1, (dizzyUntil - now) / (DizzyFor * 0.6));
            return (float)(DizzySway * Math.Sin((now - dizzyFrom) * Math.PI * 2 * 1.3) * fade);
        }

        // Every throw counts toward her grudge: the second in two minutes gets a warning and the third starts her sulk. A hard
        // first one gets a yelp and a glitch as she flies.
        private void Grudge(double fast, double now)
        {
            throwsAt.RemoveAll(at => now - at > SulkWindow || at > now);
            throwsAt.Add(now);
            if (fast >= ThrowHard) Static(false, Cue.Hop);
            if (throwsAt.Count >= SulkThrows) { throwsAt.Clear(); Sulk(now); sulkLine = true; return; }
            string line = throwsAt.Count == 2 ? Lines.Pick(WarnLines) : fast >= ThrowHard ? Lines.Pick(ThrowLines) : null;
            if (line != null) Yelp("throws you", line, false);
        }

        // Her short line about being thrown or touched: in her bubble straight away, or said in her voice like her pat lines.
        // Never over a chat, a reply on its way, her typing box or a job, though one of these lines still up gives way to the
        // next. shown is what her bubble says they did, or null for a line she says on her own.
        private void Yelp(string shown, string line, bool voiced)
        {
            if (hidden || docked || InChat || typer.Visible || working != null || job != null) return;
            if ((speech != null || talkState != TalkState.Off) && asking != yelpTicket) return;
            heard = shown ?? "sulk";
            box.Prompt = shown == null ? "" : "> sys: ";
            yelpTicket = ++asking;
            if (shown == null) remarkTicket = yelpTicket;
            if (voiced) Answer(yelpTicket, line, TalkState.Talking);
            else Begin(yelpTicket, line, TalkState.Talking, null);
        }

        private bool Sulking { get { return sulkSince >= 0; } }

        private void Sulk(double now)
        {
            sulkSince = now;
            sulkBothered = now;
            sulkDodges = 0;
            dodgeAt = dodgeSaidAt = -10;
            // A cursor that's already close when she starts sulking hasn't come at her.
            shyInside = true;
            shyAt = -1;
            frames.Start();
            Agent told = agent;
            if (told != null) told.Tell("They just threw Capri around three times in two minutes, so she's sulking: she won't let them pick her up until they say sorry, give her a head pat or leave her alone for five minutes. She still answers and helps them as usual.");
        }

        // She makes up with them and forgets the throws: they said sorry, gave her a head pat, or left her be long enough.
        private void Forgive()
        {
            if (!Sulking) return;
            sulkSince = -1;
            sulkLine = false;
            throwsAt.Clear();
            shyAt = -1;
            Agent told = agent;
            if (told != null) told.Tell("Capri forgave them for throwing her around and isn't sulking any more.");
        }

        // Left alone five minutes, she gets over it by herself.
        private void CheckSulk(double now)
        {
            if (!Sulking || now - sulkBothered < SulkAlone) return;
            Forgive();
            if (!Hushed) Yelp(null, Lines.Pick(AloneLines), true);
        }

        // Sulking, she keeps an eye on their cursor: one that comes at her fast gets a hop away, at most every DodgeGap seconds.
        // One that comes slowly can reach her, so a head pat can make up with her.
        private void Shy(double now)
        {
            Point cursor = MouseAt();
            int margin = Math.Max(16, (int)(preferences.Size * 0.18f));
            bool inside = Rectangle.Inflate(BodyOnScreen(anchor, seat != null ? PerchedBody : Body), margin, margin).Contains(cursor);
            double gap = now - shyAt, dx = cursor.X - shyCursor.X, dy = cursor.Y - shyCursor.Y;
            bool fast = shyAt >= 0 && gap > 0 && gap < 0.3 && Math.Sqrt(dx * dx + dy * dy) / gap >= DodgeSpeed * TalkScale();
            bool came = inside && !shyInside && fast;
            shyInside = inside;
            shyCursor = cursor;
            shyAt = now;
            if (came && now - dodgeAt >= DodgeGap && !hidden && !docked && !pressed && !dragging && !falling && !riding && !onPanel && outro == null && working == null && job == null && !Asleep) Dodge(cursor, now);
        }

        // Sulking, she glitches a short hop away from their cursor: along her ledge if it has room one way or the other, or else
        // across the air, kept on her screen, falling onto what's below if she'd been sitting (Zr4, F44).
        private void Dodge(Point cursor, double now)
        {
            dodgeAt = now;
            sulkBothered = now;
            dodges++;
            sulkDodges++;
            Point hip = Hip();
            Rectangle area = Screen.FromPoint(hip).WorkingArea;
            int size = preferences.Size, hop = (int)(size * (0.85 + random.NextDouble() * 0.4));
            int away = cursor.X > hip.X ? -1 : 1;
            if (seat != null && !seat.Free && seat.Icon.IsEmpty)
            {
                int low = Math.Max(seat.Left, area.Left) + size / 3, high = Math.Min(seat.Right, area.Right) - size / 3;
                if (high > low)
                {
                    int there = Math.Max(low, Math.Min(high, hip.X + away * hop)), back = Math.Max(low, Math.Min(high, hip.X - away * hop));
                    int x = Math.Abs(there - cursor.X) >= Math.Abs(back - cursor.X) ? there : back;
                    if (Math.Abs(x - cursor.X) >= size / 2 && Math.Abs(x - hip.X) >= size / 4)
                    {
                        Ledge spot = seat;
                        GlitchOut(delegate { Hop(spot, x); }, false);
                        Fuss(now);
                        return;
                    }
                }
            }
            RectangleF shape = seat != null ? PerchedBody : Body;
            Point to = Within(new Point(anchor.X + away * hop, anchor.Y), area, shape);
            if (Math.Abs(to.X - anchor.X) < size / 3) to = Within(new Point(anchor.X - away * hop, anchor.Y), area, shape);
            bool free = seat != null && seat.Free, drop = seat != null && !free && !preferences.NoSitting;
            int freeY = free ? seat.Y : 0, shift = to.X - anchor.X;
            GlitchOut(delegate
            {
                if (free) { int freeX = Hip().X + shift; Hop(new Ledge { Left = freeX - 8, Right = freeX + 8, Y = freeY, Floor = true, Free = true }, freeX); return; }
                SitUp(false);
                seat = null;
                hanging = false;
                walking = false;
                anchor = to;
                Location = anchor;
                Play(instant || !preferences.Motion ? idle : arrive);
                Static(false, null);
                frames.Start();
                if (!drop) return;
                if (clip == arrive) dropAfter = spawnFall = true;
                else Fall();
            }, false);
            Fuss(now);
        }

        // What she says as she hops away: always the first time in a sulk, then now and then.
        private void Fuss(double now)
        {
            if (sulkDodges > 1 && now - dodgeSaidAt < DodgeSaid) return;
            dodgeSaidAt = now;
            Yelp("reaches for you", sulkDodges == 1 ? FirstDodgeLine : Lines.Pick(DodgeLines), true);
        }

        // An apology to her, typed or said: "sorry", "I'm sorry Capri", "my bad", "forgive me", and sorry in a few other languages.
        private static readonly Regex SorrySaid = new Regex(@"\b(?:sorry|sry|soz|sowwy|apologi[sz]e|my bad|forgive me|lo siento|perd[oó]n(?:ame)?|d[eé]sol[eé]e?|entschuldigung|tut mir leid|scusa(?:mi)?|mi dispiace|desculpa|przepraszam|прости|извини)\b|ごめん|すみません|미안|죄송|对不起|對不起|抱歉", RegexOptions.IgnoreCase);
        // The words around a plain apology: her name, "I'm so", "for throwing you" and the like.
        private static readonly Regex SorryWords = new Regex(@"^(?:sorry|sry|soz|sowwy|i|i'm|im|i’m|am|so|soo+|really|truly|very|super|capri|choom|babe|bestie|hey|oh|ok|okay|please|pls|plz|forgive|me|my|bad|apologi[sz]e|for|about|throwing|threw|throw|tossing|tossed|yeeting|yeeted|yeet|you|that|it|earlier|again|the|thing|won't|wont|won’t|do|will|never|i'll|ill|i’ll|lol|haha|a|lot|lo|siento|perd[oó]n|perdona|perd[oó]name|d[eé]sol[eé]e?|entschuldigung|tut|mir|leid|scusa|scusami|mi|dispiace|desculpa|przepraszam|прости|извини)$", RegexOptions.IgnoreCase);

        internal static bool Apology(string text) { return text != null && SorrySaid.IsMatch(text); }

        // Nothing but an apology, so her forgiving line is the whole answer. Anything more goes on to her brain as usual.
        internal static bool OnlyApology(string text)
        {
            if (!Apology(text)) return false;
            foreach (string word in Regex.Split(text.Trim(), @"[^\p{L}\p{N}'’]+"))
                if (word.Length > 0 && !SorryWords.IsMatch(word)) return false;
            return true;
        }
    }
}
