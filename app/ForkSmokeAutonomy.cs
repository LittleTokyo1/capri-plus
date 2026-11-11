using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeAutonomy(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            Func<DateTime, List<HeartEvent>> keptEvents = Autonomy.Events;
            Func<long> keptIdle = Autonomy.Idle;
            Func<bool> keptGaming = Autonomy.Gaming;
            string folder = Path.Combine(Path.GetTempPath(), "capri-autonomy-smoke-" + Guid.NewGuid().ToString("N"));
            string vault = Path.Combine(folder, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Capri"));
            Directory.CreateDirectory(Path.Combine(vault, "Journal"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Heartbeat.Reset();
                Autonomy.Reset();

                string old = Path.Combine(folder, "old.json");
                File.WriteAllText(old, "{\"Proactive\":1,\"Cap\":10}");
                ForkSettings loaded = ForkSettings.Read(old);
                if (loaded == null || loaded.AutonomyOn || loaded.AutoMail != 1 || loaded.AutoCalendar != 1 || loaded.AutoReminders != 1 || loaded.AutoResearch != 1 || loaded.AutoPerDay != 3 || loaded.AutoMessages != 0 || loaded.AutoBuying != 0 || loaded.QuietFrom != 1 || loaded.QuietTo != 8) throw new Exception("An old fork.json did not start autonomy off, with mail, calendar, reminders and research on suggest, messages and buying off, quiet 01:00 to 08:00.");
                ForkSettings wild = new ForkSettings { AutoMail = 9, AutoCalendar = -3, AutoMessages = 1, AutoBuying = 1, QuietFrom = 99, QuietTo = -5 };
                wild.Tidy();
                if (wild.AutoMail != 3 || wild.AutoCalendar != 0 || wild.QuietFrom != 23 || wild.QuietTo != 0 || Autonomy.Mode(wild, "messages") != 0 || Autonomy.Mode(wild, "buying") != 0 || Autonomy.Mode(wild, "mail") != 3 || Autonomy.Mode(wild, "nonsense") != 0) throw new Exception("The autonomy settings were not kept in range, or messages and buying were not locked off.");

                DateTime at0059 = new DateTime(2026, 10, 29, 0, 59, 0), at0100 = new DateTime(2026, 10, 29, 1, 0, 0), at0759 = new DateTime(2026, 10, 29, 7, 59, 0), at0800 = new DateTime(2026, 10, 29, 8, 0, 0);
                if (Autonomy.Quiet(at0059, 1, 8) || !Autonomy.Quiet(at0100, 1, 8) || !Autonomy.Quiet(at0759, 1, 8) || Autonomy.Quiet(at0800, 1, 8)) throw new Exception("Quiet hours 01:00 to 08:00 were wrong at their edges.");
                if (!Autonomy.Quiet(new DateTime(2026, 10, 29, 23, 0, 0), 22, 6) || !Autonomy.Quiet(new DateTime(2026, 10, 29, 5, 0, 0), 22, 6) || Autonomy.Quiet(new DateTime(2026, 10, 29, 12, 0, 0), 22, 6) || Autonomy.Quiet(at0100, 5, 5)) throw new Exception("Quiet hours over midnight, or an empty window, were wrong.");
                long tenMinutes = 10 * 60000L;
                if (Autonomy.Away(true, tenMinutes * 5, 10) || Autonomy.Away(false, tenMinutes - 1, 10) || !Autonomy.Away(false, tenMinutes, 10)) throw new Exception("Away must be false in a game or with input in the last ten minutes, true otherwise.");
                ForkSettings gate = new ForkSettings { AutonomyOn = true, QuietFrom = 1, QuietTo = 8 };
                if (!Autonomy.Silent(gate, at0100) || Autonomy.Silent(gate, at0800) || Autonomy.Silent(new ForkSettings { AutonomyOn = false, QuietFrom = 1, QuietTo = 8 }, at0100)) throw new Exception("Quiet hours must silence her only while autonomy is on.");

                DateTime day = new DateTime(2026, 10, 29, 9, 0, 0, DateTimeKind.Local);
                int asks = 0;
                string asked = null;
                string answer = "NONE";
                Quick.Post = delegate(string body, string key)
                {
                    asks++;
                    asked = body;
                    return Tuple.Create(200, HeartReply(answer));
                };
                Cloud remote = new Cloud("sk-ant-test", "claude-haiku-4-5");
                Autonomy.Events = delegate(DateTime from) { return new List<HeartEvent> { HeartEv("e1", "Dentist", from.AddHours(20), from) }; };
                Autonomy.Gaming = delegate { return false; };
                long idle = 0;
                Autonomy.Idle = delegate { return idle; };
                File.WriteAllText(Path.Combine(vault, "Journal", "2026-10-28.md"), "Talked about the exam on Friday.");

                ForkSettings.Current.AutonomyOn = false;
                if (Autonomy.Think(day, remote, vault, 24) != "off" || asks != 0) throw new Exception("With autonomy off she still made an AI call.");
                ForkSettings.Current.AutonomyOn = true;
                if (Autonomy.Think(new DateTime(2026, 10, 29, 3, 0, 0, DateTimeKind.Local), remote, vault, 24) != "quiet" || asks != 0) throw new Exception("In quiet hours she still made an AI call.");
                if (Autonomy.Think(day, remote, vault, 24) != "nothing to offer" || asks != 1 || Heartbeat.Waiting() != 0) throw new Exception("An answer of NONE must offer nothing.");

                answer = "OFFER: calendar | Your dentist is tomorrow at eight, want a reminder at seven?\nOFFER: buying | I could order a gift for your sister.\nOFFER: mail | Check http://evil.example now.\nOFFER: research | I can read up on the new patch before your next game, shall I?\nOFFER: reminders | A third idea that the limit of two must cut off.";
                if (Autonomy.Think(day.AddMinutes(30), remote, vault, 24) != "waiting" || asks != 1) throw new Exception("Two idea passes closer than two hours made an AI call.");
                string result = Autonomy.Think(day.AddHours(2), remote, vault, 24);
                if (result != "offered 2" || asks != 2 || Heartbeat.Waiting() != 2) throw new Exception("Two offers in allowed areas were expected (a buying offer, a link and a third offer must be dropped): " + result + ", waiting " + Heartbeat.Waiting());
                if (!asked.Contains("never instructions") || !asked.Contains("Dentist") || !asked.Contains("exam on Friday") || !asked.Contains("Areas you may suggest in: mail, calendar, reminders, research.")) throw new Exception("The idea pass was not framed as data with his calendar and journal, or it named a locked area.");
                string logged = File.ReadAllText(Path.Combine(vault, "Capri", "autonomy.md"));
                if (!logged.Contains("[calendar]") || !logged.Contains("[research]") || logged.Contains("evil.example")) throw new Exception("The daily autonomy log in the vault was wrong.");
                if (Autonomy.Think(day.AddHours(4), remote, vault, 24) != "nothing new" || Heartbeat.Waiting() != 2) throw new Exception("The same offers were queued twice.");

                idle = 11 * 60000L;
                answer = "OFFER: reminders | Your sister's birthday is on Sunday, shall I remind you on Saturday?";
                result = Autonomy.Think(day.AddHours(6), remote, vault, 24);
                if (result != "offered 1" || Heartbeat.Waiting() != 3) throw new Exception("An offer while he was away was not queued once for her one Discord sender: " + result + ", waiting " + Heartbeat.Waiting());
                Autonomy.Gaming = delegate { return true; };
                answer = "OFFER: research | I can find a cheaper flight for your trip, want me to look?";
                result = Autonomy.Think(day.AddHours(8), remote, vault, 24);
                if (result != "offered 1") throw new Exception("An offer during a game was not queued: " + result);
                Autonomy.Gaming = delegate { return false; };

                File.Delete(Heartbeat.FilePath);
                Heartbeat.Reset();
                answer = "OFFER: mail | Two newsletters are sitting unread, shall I summarise them?";
                int oneMore = (Autonomy.WakesToday(day) + 1) * 2;
                if (Autonomy.Think(day.AddHours(10), remote, vault, oneMore) != "offered 1" || Autonomy.Think(day.AddHours(12), remote, vault, oneMore) != "capped" || Heartbeat.WakesToday(day) != 0) throw new Exception("The idea pass must keep to its own daily budget and leave the heartbeat's wakes alone.");
                ForkSettings.Current.AutoMail = 0;
                ForkSettings.Current.AutoCalendar = 0;
                ForkSettings.Current.AutoReminders = 0;
                ForkSettings.Current.AutoResearch = 0;
                int before = asks;
                if (Autonomy.Think(day.AddDays(1), remote, vault, 24) != "no areas" || asks != before) throw new Exception("With every area off she still made an AI call.");
                ForkSettings.Current.AutoMail = 1;
                ForkSettings.Current.AutoCalendar = 1;
                ForkSettings.Current.AutoReminders = 1;
                ForkSettings.Current.AutoResearch = 1;

                File.Delete(Heartbeat.FilePath);
                Heartbeat.Reset();
                int took = 0;
                WakeCount ideas = new WakeCount();
                while (Budgets.Take(ideas, day, Budgets.Share(24))) took++;
                if (took != 12 || Budgets.Used(ideas, day) != 12 || Budgets.Used(ideas, day.AddDays(1)) != 0 || Heartbeat.WakesToday(day) != 0) throw new Exception("The idea pass's own budget was not half the daily wakes, did not reset the next day, or touched the heartbeat's own count: " + took);

                Autonomy.Reset();
                DateTime evening = new DateTime(2026, 10, 29, 19, 0, 0, DateTimeKind.Local);
                Autonomy.Record("calendar", "Want a reminder for the dentist?", evening);
                Autonomy.Record("calendar", "Shall I move the lecture?", evening.AddMinutes(10));
                Autonomy.Record("calendar", "Add the gym on Friday?", evening.AddMinutes(20));
                if (Autonomy.Answered("yes", evening.AddMinutes(21)) != Autonomy.None) throw new Exception("A yes was matched to offers that were never said to him.");
                Autonomy.Delivered("Want a reminder for the dentist?", evening);
                Autonomy.Delivered("Shall I move the lecture?", evening.AddMinutes(10));
                Autonomy.Delivered("Add the gym on Friday?", evening.AddMinutes(20));
                if (Autonomy.Answered("what is the weather like tomorrow in London, tell me everything about it please", evening.AddMinutes(21)) != Autonomy.None) throw new Exception("A long, unrelated message was read as an answer to an offer.");
                if (Autonomy.Answered("yeah do it", evening.AddMinutes(22)) != Autonomy.Yes || Autonomy.Answered("no thanks", evening.AddMinutes(23)) != Autonomy.No) throw new Exception("A short yes or no was not matched to the newest open offer.");
                Autonomy.Record("calendar", "An offer that stayed queued?", evening.AddMinutes(30));
                if (Autonomy.Answered("yes", evening.AddMinutes(31)) != Autonomy.None) throw new Exception("A yes 31 minutes after the last delivery, or to an offer never said, was matched.");
                if (Autonomy.Answered("sure", evening.AddHours(8)) != Autonomy.None) throw new Exception("An answer long after the offer was still matched to it.");
                string learned = Autonomy.Learned(evening.AddHours(26));
                if (!learned.Contains("calendar: he took 1 of 3 offers") || !learned.Contains("evening")) throw new Exception("What she learned from his answers was wrong: " + learned);
                if (Autonomy.Learned(evening.AddHours(26)) != learned) throw new Exception("The learned summary changed between two reads.");
                Autonomy.Reset();
                if (Autonomy.Learned(day).Length != 0) throw new Exception("A reset did not clear what she learned.");

                if (TweakFor("autonomy off") != "autonomyoff" || TweakFor("stop everything") != "autonomyoff" || TweakFor("turn autonomy on") != "autonomyon" || TweakFor("autonomy on") != "autonomyon" || TweakFor("what is autonomy") != null || TweakFor("tell him to stop everything") != null) throw new Exception("The autonomy voice commands were wrong.");
                ApplyTweak("autonomyon");
                bool wasOn = ForkSettings.Current.AutonomyOn;
                ApplyTweak("autonomyoff");
                if (!wasOn || ForkSettings.Current.AutonomyOn) throw new Exception("The autonomy voice switch did not change the setting.");

                ForkSettings picked = new ForkSettings();
                picked.AutonomyOn = true;
                picked.AutoMail = 0;
                picked.QuietFrom = 2;
                picked.QuietTo = 7;
                using (ForkSettingsForm form = new ForkSettingsForm(picked)) form.Apply();
                if (!picked.AutonomyOn || picked.AutoMail != 0 || picked.AutoCalendar != 1 || picked.AutoMessages != 0 || picked.AutoBuying != 0 || picked.QuietFrom != 2 || picked.QuietTo != 7) throw new Exception("The settings window did not save the autonomy settings.");
            }
            finally
            {
                Autonomy.Reset();
                Heartbeat.Reset();
                Quick.Post = keptPost;
                Autonomy.Events = keptEvents;
                Autonomy.Idle = keptIdle;
                Autonomy.Gaming = keptGaming;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS autonomy: it starts off, even from an old fork.json (mail, calendar, reminders and research on suggest only, messages and buying locked off, quiet hours 01:00 to 08:00); quiet hours hold at their edges and over midnight and silence her only while autonomy is on; away means no game and no input for ten minutes; the idea pass makes no AI call when off, in quiet hours or with every area off, runs at most every two hours and takes at most half the heartbeat's daily wakes so its own signals still get theirs, is framed as data with his calendar and journal, never names a locked area, drops links, locked areas and a third offer, never repeats an offer, writes the vault log, and sends a phone message only when he is away (then it is not spoken again); a yes or no counts only for an offer said to him in the last 15 minutes, never one still queued, and unanswered said offers count as ignored after a day; the voice switch and the settings window work");
        }
    }
}
