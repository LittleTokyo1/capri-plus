using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeCharm(List<string> checks)
        {
            string keptPath = ForkSettings.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-charm-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Charm.Reset();
                DateTime day = new DateTime(2026, 11, 2, 18, 0, 0);
                if (Charm.Points != Charm.Start || Charm.Name(Charm.Points) != "Fond") throw new Exception("A fresh charm meter did not start at Fond: " + Charm.Points);
                string label = Charm.Label();
                if (!label.StartsWith("CHARM", StringComparison.Ordinal) || !label.Contains("12%") || !label.Contains("FOND")) throw new Exception("The charm label is wrong: " + label);

                Charm.Heard("thanks choom, you're the best", day);
                if (Charm.Points != Charm.Start + 4) throw new Exception("A kind line did not give a talk point and kind words: " + Charm.Points);
                Charm.Heard("this game is stupid", day);
                if (Charm.Points != Charm.Start + 5) throw new Exception("A rude word about a game counted as rude to her: " + Charm.Points);
                Charm.Heard("shut up, you're useless", day);
                if (Charm.Points != Charm.Start + 5 - Charm.RudeCost) throw new Exception("A rude line to her did not drop the meter, or still gave a talk point: " + Charm.Points);
                int fair = Charm.Points;
                Charm.Heard("shut up, you're useless", day, true, false);
                Charm.Heard("you're so dumb, capri", day, false, true);
                if (Charm.Points != fair + 2) throw new Exception("A rude line in voice chat that does not name her, or one in a game, dropped the meter: " + Charm.Points);
                Charm.Heard("shut up capri", day, true, false);
                if (Charm.Points != fair + 2 - Charm.RudeCost) throw new Exception("A rude line in voice chat that names her did not drop the meter: " + Charm.Points);
                for (int i = 0; i < 40; i++) Charm.Heard("what time is it", day);
                int talked = Charm.Points;
                if (talked != Charm.Start + 7 - 2 * Charm.RudeCost + (Charm.TalkCap - 4)) throw new Exception("Talking points are not capped for the day: " + talked);
                for (int i = 0; i < 20; i++) Charm.Patted(day);
                if (Charm.Points != talked + Charm.PatCap) throw new Exception("Head pats are not capped for the day: " + Charm.Points);
                int patted = Charm.Points;
                for (int i = 0; i < 25; i++) Charm.Together(day.AddMinutes(i), true);
                Charm.Together(day.AddMinutes(24), true);
                for (int i = 30; i < 60; i++) Charm.Together(day.AddMinutes(i), false);
                if (Charm.Points != patted + 2) throw new Exception("Time together did not give a point every ten minutes he is at the PC, or counted time away: " + Charm.Points);

                DateTime next = day.AddDays(1);
                int before = Charm.Points;
                Charm.Heard("hey", next);
                if (Charm.Points != before + 1) throw new Exception("The daily caps did not reset the next day: " + Charm.Points);
                for (int i = 0; i < 50; i++) Charm.Heard("fuck you, you're so dumb", next);
                if (Charm.Points != before + 1 - Charm.RudeCap) throw new Exception("Rude drops are not capped for the day: " + Charm.Points);

                Charm.Reset();
                if (Charm.Points != before + 1 - Charm.RudeCap) throw new Exception("The charm meter was not kept on disk: " + Charm.Points);
                if (Charm.TakeNews() != null) throw new Exception("A level-up line came without a level up.");

                Charm.Set(Charm.Steps[3] - 1);
                Charm.Heard("good girl, nice work", next.AddDays(1));
                string news = Charm.TakeNews();
                if (news == null || !news.Contains("Close") || Charm.TakeNews() != null) throw new Exception("Reaching Close did not give one level-up line: " + news);
                Charm.Set(Charm.Steps[3] - 1);
                Charm.Heard("thanks", next.AddDays(2));
                if (Charm.Name(Charm.Points) != "Close" || Charm.TakeNews() != null) throw new Exception("Falling and climbing back to a level she reached before said the level-up line again.");

                string block = Charm.Block();
                if (!block.Contains(Charm.Percent(Charm.Points) + "%") || !block.Contains("Close") || !block.Contains("how much you like them")) throw new Exception("Her prompt line lacks the percent, the level or the ask rule: " + block);
                if (!Context.Memory("hi").Contains("charm meter")) throw new Exception("The charm line does not reach her chat prompt.");

                Random seeded = new Random(7);
                int fresh = 0;
                for (int i = 0; i < 60; i++) if (Charm.PetLine(seeded) != null) fresh++;
                Charm.Set(Charm.Start);
                int none = 0;
                for (int i = 0; i < 60; i++) if (Charm.PetLine(seeded) != null) none++;
                if (fresh == 0 || fresh == 60 || none != 0) throw new Exception("New pat lines do not unlock at Close or show below it: " + fresh + " / " + none);

                Charm.Set(Charm.Steps[5] + 500);
                if (Charm.Percent(Charm.Points) != 100 || Charm.Name(Charm.Points) != "Devoted") throw new Exception("The meter does not top out at Devoted 100%.");
                Charm.Set(-50);
                if (Charm.Points != 0) throw new Exception("The meter went below zero.");

                Charm.Set(Charm.Steps[4]);
                ForkCharmShow();
                if (mainframe.CharmShown != Charm.Label() || !mainframe.CharmShown.Contains("SMITTEN")) throw new Exception("The Mainframe does not show the charm meter: " + mainframe.CharmShown);
                if (mainframe.CharmOverlaps) throw new Exception("The Mainframe charm row overlaps a key or sticks out of the window.");
                string shots = Environment.GetEnvironmentVariable("CAPRI_SHOTS");
                if (!string.IsNullOrEmpty(shots) && mainframe.Width > 0 && mainframe.Height > 0)
                {
                    Directory.CreateDirectory(shots);
                    using (System.Drawing.Bitmap picture = new System.Drawing.Bitmap(mainframe.Width, mainframe.Height))
                    {
                        mainframe.DrawToBitmap(picture, new System.Drawing.Rectangle(0, 0, mainframe.Width, mainframe.Height));
                        picture.Save(Path.Combine(shots, "charm-mainframe.png"));
                    }
                }
            }
            finally
            {
                ForkSettings.FilePath = keptPath;
                Charm.Reset();
                try { ForkCharmShow(); } catch (Exception) { }
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS charm meter: starts at Fond, grows by time together (a point every ten minutes at the PC), talking, head pats and kind words with daily caps, drops only for rude words aimed at her (typed, or naming her in voice chat, never in a game; capped, never below zero), is kept on disk, says one level-up line the first time a level is reached, reaches her prompt with the percent and level, unlocks new pat lines from Close and shows in the Mainframe's CAPRI band");
        }
    }
}
