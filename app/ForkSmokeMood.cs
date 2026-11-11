using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeMood(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.Moods || !fresh.Gestures) throw new Exception("Moods and her own gestures should be on by default.");
            string close;
            Tunable moods = Tuning.Find("moods", "off", out close);
            Tunable moves = Tuning.Find("gestures she picks", "off", out close);
            if (moods == null || moods.Field != "Moods" || moves == null || moves.Field != "Gestures") throw new Exception("Her setting tool can't find moods or her gestures by voice.");
            DateTime day = new DateTime(2026, 11, 6, 15, 0, 0);
            DateTime never = DateTime.MinValue;
            if (Mood.Pick(day, false, never, never, never, never) != Mood.Chill) throw new Exception("An ordinary afternoon was not chill.");
            if (Mood.Pick(day.Date.AddHours(2), false, never, never, never, never) != Mood.Sleepy) throw new Exception("Two at night was not sleepy.");
            if (Mood.Pick(day, true, never, never, never, never) != Mood.Hyped) throw new Exception("A game was not hyped.");
            if (Mood.Pick(day, false, day.AddMinutes(-10), never, never, never) != Mood.Hyped) throw new Exception("Right after a game was not hyped.");
            if (Mood.Pick(day, false, day.AddMinutes(-50), never, never, never) != Mood.Chill) throw new Exception("The after-game hype never wore off.");
            if (Mood.Pick(day, false, never, day.AddMinutes(-5), never, never) != Mood.Missed) throw new Exception("Coming back after a long away did not miss him.");
            if (Mood.Pick(day, false, never, never, day.AddHours(-1), day.AddHours(-2)) != Mood.Worried) throw new Exception("A rough day did not worry her over a good one.");
            if (Mood.Pick(day, false, never, never, day.AddHours(-1), never) != Mood.Proud) throw new Exception("A good day did not make her proud.");
            if (Mood.Pick(day.AddDays(1), false, never, never, day.AddHours(-1), never) != Mood.Chill) throw new Exception("Yesterday's pride carried into the next day.");
            Mood.Reset();
            Mood.Heard("I passed my exam today!", day);
            if (Mood.Now(day.AddMinutes(1), false) != Mood.Proud) throw new Exception("Telling her a win did not make her proud.");
            Mood.Heard("ugh, rough day honestly", day.AddMinutes(2));
            if (Mood.Now(day.AddMinutes(3), false) != Mood.Worried) throw new Exception("Telling her a bad day did not worry her.");
            Mood.Reset();
            Mood.Heard("Mallory on Discord: I failed my exam", day);
            if (Mood.Now(day.AddMinutes(1), false) != Mood.Chill) throw new Exception("A friend's bad day read out from Discord moved her mood about him.");
            Mood.Reset();
            Mood.Seen(day, 60L * 60 * 1000);
            Mood.Seen(day.AddMinutes(1), 1000);
            if (Mood.Now(day.AddMinutes(2), false) == Mood.Missed) throw new Exception("An hour away counted as a long away.");
            Mood.Seen(day.AddHours(5), 5L * 60 * 60 * 1000);
            Mood.Seen(day.AddHours(5).AddMinutes(1), 2000);
            if (Mood.Now(day.AddHours(5).AddMinutes(2), false) != Mood.Missed) throw new Exception("Coming back after five hours away did not miss him.");
            Mood.Reset();
            if (Mood.Line(Mood.Sleepy).IndexOf("never slower", StringComparison.Ordinal) < 0 || Mood.Line(Mood.Missed).IndexOf("never a guilt", StringComparison.Ordinal) < 0) throw new Exception("A mood line lost its limits.");
            if (Mood.Line(Mood.Chill) != "") throw new Exception("Chill should add nothing to her prompt.");
            if (Mood.Label(Mood.Sleepy) != "MOOD  SLEEPY" || Mood.Label(Mood.Missed) != "MOOD  MISSED YOU") throw new Exception("The MOOD row text is wrong: " + Mood.Label(Mood.Missed));
            if (Gesture.Gap(Charm.Steps[0]) < 120 || Gesture.Gap(Charm.Steps[5]) > 20 || Gesture.Gap(Charm.Steps[2]) >= Gesture.Gap(Charm.Steps[0])) throw new Exception("Her own gestures do not grow from rare at Curious to a few an hour at Devoted.");
            if (Gesture.Due(day, day.AddMinutes(-10), Charm.Steps[5]) || !Gesture.Due(day, day.AddMinutes(-25), Charm.Steps[5])) throw new Exception("Gesture timing is wrong.");
            if (!Gesture.Allowed(true, false, false, false, 1000)) throw new Exception("A free moment did not allow a gesture.");
            if (Gesture.Allowed(false, false, false, false, 1000) || Gesture.Allowed(true, true, false, false, 1000) || Gesture.Allowed(true, false, true, false, 1000) || Gesture.Allowed(true, false, false, true, 1000) || Gesture.Allowed(true, false, false, false, 10L * 60 * 1000)) throw new Exception("A gesture was allowed with the switch off, in a game, over a fullscreen app, while she was busy or with him away.");
            Random seeded = new Random(3);
            for (int mood = 0; mood < Mood.Names.Length; mood++)
            {
                string move = Gesture.Pick(mood, seeded);
                if (move != "grin" && move != "glitch" && move != "blink") throw new Exception("Her gesture is not one of her own moves: " + move);
            }
            ForkSettings keptSettings = ForkSettings.Current;
            try
            {
                ForkSettings on = new ForkSettings();
                on.Tidy();
                ForkSettings.Current = on;
                if (Mood.Block(day.Date.AddHours(2)).IndexOf("sleepy", StringComparison.Ordinal) < 0) throw new Exception("Her mood does not reach her chat prompt.");
                on.Moods = false;
                if (Mood.Block(day.Date.AddHours(2)) != "") throw new Exception("Her mood reached her prompt with the switch off.");
                on.Moods = true;
                ForkCharmShow();
                mainframe.ShowMood(Mood.Label(Mood.Hyped));
                mainframe.Flip(true);
                if (mainframe.MoodShown != "MOOD  HYPED" || mainframe.RowShown != "MOOD  HYPED") throw new Exception("The Mainframe does not show her mood on its turn: " + mainframe.RowShown);
                if (mainframe.CharmOverlaps) throw new Exception("The Mainframe MOOD turn overlaps a key or sticks out of the window.");
                mainframe.Flip(false);
                if (mainframe.RowShown != mainframe.CharmShown || mainframe.RowShown.IndexOf("CHARM", StringComparison.Ordinal) < 0) throw new Exception("The CHARM row did not come back after the mood's turn: " + mainframe.RowShown);
                mainframe.ShowMood("");
                mainframe.Flip(true);
                if (mainframe.RowShown != mainframe.CharmShown) throw new Exception("With moods off the row still took a mood turn.");
                string shots = Environment.GetEnvironmentVariable("CAPRI_SHOTS");
                if (!string.IsNullOrEmpty(shots) && mainframe.Width > 0 && mainframe.Height > 0)
                {
                    Directory.CreateDirectory(shots);
                    using (System.Drawing.Bitmap picture = new System.Drawing.Bitmap(mainframe.Width, mainframe.Height))
                    {
                        mainframe.DrawToBitmap(picture, new System.Drawing.Rectangle(0, 0, mainframe.Width, mainframe.Height));
                        picture.Save(Path.Combine(shots, "mood-mainframe.png"));
                    }
                }
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                Mood.Reset();
            }
            checks.Add("PASS moods: moods and her own gestures are on by default and found by voice; she is sleepy late at night, hyped in and right after a game, misses him after a long away (not after an hour), proud or worried from what he told her today (worried wins, gone the next day, a friend's news read out from Discord never counts), each mood line keeps its limits and reaches her prompt only with the switch on; the Mainframe's CHARM row takes turns with a MOOD line (none with moods off, no overlap); her gestures (grin, glitch, blink) come rarely at Curious and a few an hour at Devoted, never in a game, over a fullscreen app, while she is busy or while he is away");
        }
    }
}
