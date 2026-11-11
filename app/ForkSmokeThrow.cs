using System;
using System.Collections.Generic;
using System.Drawing;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeThrow(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!ThrowOn(fresh)) throw new Exception("Throwing her should be on by default.");
            fresh.ThrowHer = false;
            if (ThrowOn(fresh)) throw new Exception("Throwing her stayed on with its switch off.");
            string close;
            Tunable found = Tuning.Find("throw her", "off", out close);
            if (found == null || found.Field != "ThrowHer") throw new Exception("Her setting tool can't find throw her by voice.");
            List<KeyValuePair<double, Point>> track = new List<KeyValuePair<double, Point>>();
            track.Add(new KeyValuePair<double, Point>(1.00, new Point(100, 500)));
            track.Add(new KeyValuePair<double, Point>(1.04, new Point(180, 480)));
            track.Add(new KeyValuePair<double, Point>(1.08, new Point(260, 460)));
            PointF fling = TrailSpeed(track, 1.08, ThrowSpan);
            if (Math.Abs(fling.X - 2000) > 1 || Math.Abs(fling.Y + 500) > 1) throw new Exception("A flick's speed was read wrong: " + fling);
            List<KeyValuePair<double, Point>> still = new List<KeyValuePair<double, Point>>();
            still.Add(new KeyValuePair<double, Point>(1.00, new Point(100, 500)));
            still.Add(new KeyValuePair<double, Point>(1.50, new Point(140, 500)));
            PointF slow = TrailSpeed(still, 1.60, ThrowSpan);
            if (Math.Abs(slow.X) > 1 || Math.Abs(slow.Y) > 1) throw new Exception("A cursor that stopped before letting go still threw her: " + slow);
            if (Math.Sqrt(fling.X * fling.X + fling.Y * fling.Y) < ThrowFrom || ThrowFrom >= ThrowHard || ThrowHard >= ThrowMost) throw new Exception("The throw thresholds are out of order.");
            if (!Apology("sorry capri") || !Apology("I'm so sorry for throwing you") || !Apology("my bad") || Apology("what's the score")) throw new Exception("Sorry wasn't heard right.");
            if (!OnlyApology("sorry capri") || !OnlyApology("I'm sorry for throwing you") || OnlyApology("sorry, what time is it")) throw new Exception("A plain sorry wasn't told apart from a sorry with a question.");
            checks.Add("PASS throw her (096-08): on by default and found by voice, off means a plain drop, a flick's speed read from the last 80 ms of the cursor and a cursor that stopped first throws nothing, thresholds in order, sorry heard and a plain sorry told apart from a sorry with a question");
        }
    }
}