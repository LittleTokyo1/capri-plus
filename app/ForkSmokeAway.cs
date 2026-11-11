using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeAway(List<string> checks)
        {
            const long Minute = 60000L;
            ForkSettings home = new ForkSettings();
            ForkSettings left = new ForkSettings { HomeAway = true };
            if (Delivery.NotHome(home, false, 0) || Delivery.NotHome(home, false, 9 * Minute) || Delivery.NotHome(home, true, 30 * Minute)) throw new Exception("At home counted as not home.");
            if (!Delivery.NotHome(left, false, 0) || !Delivery.NotHome(left, true, 0) || !Delivery.NotHome(home, false, 10 * Minute) || !Delivery.NotHome(home, false, 60 * Minute)) throw new Exception("Leaving, or ten idle minutes, did not count as not home.");

            DateTime spoke = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
            Delivery.Reset(spoke);
            if (Delivery.AtPcMilliseconds(spoke) != long.MaxValue) throw new Exception("Before he spoke at the PC, she thought he was there.");
            Delivery.Heard("> you: ", spoke);
            Delivery.Heard("> sys: ", spoke.AddMinutes(3));
            if (Delivery.AtPcMilliseconds(spoke.AddMinutes(5)) != 5 * Minute || Delivery.ContactMilliseconds(spoke.AddMinutes(5)) != 5 * Minute) throw new Exception("Speaking at the PC was not counted, or a system line was.");
            Delivery.Heard("> discord: ", spoke.AddMinutes(20));
            if (Delivery.AtPcMilliseconds(spoke.AddMinutes(25)) != 25 * Minute || Delivery.ContactMilliseconds(spoke.AddMinutes(25)) != 5 * Minute) throw new Exception("A Discord line counted as him being at the PC, or did not count as contact.");
            if (Delivery.NotHome(home, false, 11 * Minute, 2 * Minute) || !Delivery.NotHome(home, false, 11 * Minute, 11 * Minute) || !Delivery.NotHome(left, false, 0, 0) || Delivery.NotHome(home, true, 30 * Minute, 30 * Minute)) throw new Exception("Talking to her at the PC did not keep him at home, or leaving stopped working.");
            if (Delivery.DiscordMilliseconds(spoke.AddMinutes(23)) != 3 * Minute) throw new Exception("The time since his last Discord line was wrong.");
            if (!Delivery.NotHome(home, false, 2 * Minute, long.MaxValue, 3 * Minute) || Delivery.NotHome(home, false, 2 * Minute, 1 * Minute, 3 * Minute) || Delivery.NotHome(home, false, 2 * Minute, long.MaxValue, 30 * Minute) || Delivery.NotHome(home, true, 2 * Minute, long.MaxValue, 3 * Minute)) throw new Exception("Writing to her on Discord while the PC shows input (remote desktop) did not count as away, or typing at the PC after it did not bring him home, or a game was ignored.");
            Delivery.Reset(DateTime.UtcNow);

            DateTime noon = new DateTime(2026, 10, 30, 12, 0, 0);
            DateTime night = noon.AddHours(15);
            ForkSettings quiet = new ForkSettings { AutonomyOn = true, QuietFrom = 1, QuietTo = 8, DiscordOn = true, Proactive = 2 };
            if (Delivery.HeadsMay(quiet, noon, false) || !Delivery.HeadsMay(quiet, noon, true) || Delivery.HeadsMay(quiet, night, true)) throw new Exception("A heads-up went to Discord at home, or did not go when away, or went in the quiet hours.");
            ForkSettings plain = new ForkSettings { DiscordOn = true, Proactive = 2 };
            if (plain.AutonomyOn || Delivery.HeadsMay(plain, night, true) || !Delivery.HeadsMay(plain, noon, true)) throw new Exception("With autonomy off a heads-up still went to his phone at night, or none went by day.");
            ForkSettings hush =new ForkSettings { DiscordOn = true, Proactive = 0 };
            ForkSettings noDiscord = new ForkSettings { DiscordOn = false, Proactive = 0 };
            if (!Delivery.HeadsMay(hush, noon, false) || Delivery.HeadsMay(noDiscord, noon, true)) throw new Exception("Quiet mode lost its Discord heads-ups, or a heads-up was sent with Discord off.");

            ForkSettings keptSettings = ForkSettings.Current;
            Func<long> keptIdle = Autonomy.Idle;
            Func<bool> keptGaming = Autonomy.Gaming;
            string folder = Path.Combine(Path.GetTempPath(), "capri-away-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "schedule.txt");
            int askedBefore = asking;
            try
            {
                Autonomy.Gaming = delegate { return false; };
                Autonomy.Idle = delegate { return 0L; };
                ForkSettings.Current = new ForkSettings { HomeAway = true };
                if (!Delivery.NotHome()) throw new Exception("The live check missed that he said he is leaving.");
                ForkSettings.Current = new ForkSettings();
                if (Delivery.NotHome()) throw new Exception("The live check sent him away while he is at the PC.");
                Autonomy.Idle = delegate { return 11 * Minute; };
                if (!Delivery.NotHome()) throw new Exception("The live check missed ten idle minutes.");

                Scheduler.Add(path, new ScheduleItem { Due = noon.AddMinutes(-1), Repeat = "", Text = "stand up" });
                Scheduler.Add(path, new ScheduleItem { Due = noon.AddMinutes(-2), Repeat = "daily", Text = "pills" });
                Scheduler.Add(path, new ScheduleItem { Due = noon.AddMinutes(30), Repeat = "", Text = "later" });
                Scheduler.Add(path, new ScheduleItem { Due = noon.AddHours(-8), Repeat = "", Text = "old" });
                List<ScheduleItem> got = ScheduleDue(path, noon);
                string line = ScheduleTexts(got);
                if (got.Count != 2 || line != "pills; stand up") throw new Exception("The due reminders were wrong: " + line);
                if (Scheduler.Load(path).Count != 2) throw new Exception("Taking the due reminders left the wrong ones: " + Scheduler.Load(path).Count);

                ScheduleSent(path, got, line, false, noon);
                if (scheduleRetryAt <= noon || Scheduler.Load(path).Count != 4) throw new Exception("A reminder that could not be sent was not kept for a retry.");
                List<ScheduleItem> again = ScheduleDue(path, noon.AddMinutes(1));
                if (again.Count != 2 || ScheduleTexts(again) != line) throw new Exception("The retry did not bring the same reminders back: " + ScheduleTexts(again));
                ScheduleSent(path, again, line, true, noon.AddMinutes(1));
                List<ScheduleItem> remaining = Scheduler.Load(path);
                bool laterKept = false;
                foreach (ScheduleItem item in remaining) if (item.Text == "later") laterKept = true;
                if (remaining.Count != 2 || !laterKept) throw new Exception("A sent reminder was kept, or the later one was lost: " + remaining.Count);
                if (asking != askedBefore) throw new Exception("Sending a reminder to Discord touched the reply counter, which would cancel a reply he is waiting for.");
            }
            finally
            {
                Delivery.Reset(DateTime.UtcNow);
                ForkSettings.Current = keptSettings;
                Autonomy.Idle = keptIdle;
                Autonomy.Gaming = keptGaming;
                scheduleRetryAt = DateTime.MinValue;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            checks.Add("PASS away delivery: not home means he said he is leaving, or the PC was idle ten minutes outside a game; then reminders go to Discord (a failed send is kept and retried, a sent one is gone, the reply counter is untouched), and heads-ups go there too except in the quiet hours or with Discord off");
        }
    }
}
