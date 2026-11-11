using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeNotSilent(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath, keptLog = ActivityLog.FilePath;
            bool checkInsWas = preferences.CheckIns;
            string folder = Path.Combine(Path.GetTempPath(), "capri-silent-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.LogActions = true;
                ForkSettings.Current.Proactive = keptSettings.Proactive;
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Heartbeat.Reset();
                Autonomy.Reset();
                AwayCheckIns.Reset();
                SilenceLog.Reset();
                SmokeOwnBudgets(checks);
                SmokeSilenceLogged(checks, folder);
                SmokeCareLogged(checks);
                SmokeCareAfterRestart(checks);
                SmokeCareInVoiceChat(checks);
                SmokeNumbersByVoice(checks);
            }
            finally
            {
                Heartbeat.Reset();
                Autonomy.Reset();
                AwayCheckIns.Reset();
                SilenceLog.Reset();
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                ActivityLog.FilePath = keptLog;
                ApplyForkSettings();
                if (preferences.CheckIns != checkInsWas) ToggleCheckIns();
                Heartbeat.Reset();
                Autonomy.Reset();
                AwayCheckIns.Reset();
                try { Directory.Delete(folder, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void SmokeOwnBudgets(List<string> checks)
        {
            DateTime day = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Local);
            if (Budgets.Share(48) != 24 || Budgets.Share(24) != 12 || Budgets.Share(1) != 1 || Budgets.Share(0) != 1) throw new Exception("A budget share was not half the daily wakes, at least one.");
            WakeCount offers = new WakeCount(), checkIns = new WakeCount();
            int took = 0;
            while (Budgets.Take(offers, day, Budgets.Share(24))) took++;
            if (took != 12 || Budgets.Take(offers, day.AddHours(5), Budgets.Share(24))) throw new Exception("The offer budget did not stop at its own share: " + took);
            if (!Budgets.Take(checkIns, day, Budgets.Share(24)) || Budgets.Used(checkIns, day) != 1) throw new Exception("Used-up offers starved the check-ins.");
            if (Heartbeat.WakesToday(day) != 0 || Autonomy.WakesToday(day) != 0 || AwayCheckIns.WakesToday(day) != 0) throw new Exception("A budget count leaked into another one.");
            if (!Budgets.Take(offers, day.AddDays(1), Budgets.Share(24)) || Budgets.Used(offers, day.AddDays(1)) != 1) throw new Exception("The offer budget did not start fresh the next day.");
            if (Budgets.Take(null, day, 5) || Budgets.Used(null, day) != 0) throw new Exception("A missing budget count was not refused.");
            ForkSettings.Current.QuietFrom = 1;
            ForkSettings.Current.QuietTo = 5;
            if (AwayCheckIns.CountDay(new DateTime(2026, 11, 3, 2, 29, 0, DateTimeKind.Local)) != "2026-11-02" || AwayCheckIns.CountDay(new DateTime(2026, 11, 3, 8, 1, 0, DateTimeKind.Local)) != "2026-11-03") throw new Exception("A check-in in the night counted towards the next day.");
            ForkSettings.Current.QuietTo = 1;
            if (AwayCheckIns.CountDay(new DateTime(2026, 11, 3, 0, 30, 0, DateTimeKind.Local)) != "2026-11-03") throw new Exception("With no quiet hours the check-in day did not start at midnight.");
            ForkSettings.Current.QuietFrom = 1;
            ForkSettings.Current.QuietTo = 8;
            checks.Add("PASS own budgets: heartbeat signals, offers and check-ins each keep their own daily wake count (offers and check-ins half the daily wakes each), so one can't starve the others; a check-in sent in the night counts towards the day before, not the next one");
        }

        private static void SmokeSilenceLogged(List<string> checks, string folder)
        {
            DateTime day = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Local);
            string log = Path.Combine(folder, "activity.log");
            if (File.Exists(log)) File.Delete(log);
            if (!SilenceLog.Once("heartbeat", "capped", "all 24 used", day) || SilenceLog.Once("heartbeat", "capped", "all 24 used", day.AddMinutes(30))) throw new Exception("A cap was not logged once a day.");
            if (!SilenceLog.Once("autonomy", "capped", "all 12 used", day) || !SilenceLog.Once("heartbeat", "capped", "all 24 used", day.AddDays(1))) throw new Exception("Another area or the next day did not log its own cap.");
            if (!SilenceLog.Once("checkin", "idle: cap", CheckInWhy("idle: cap", day, 24), day)) throw new Exception("The check-in daily limit was not logged.");
            string text = File.Exists(log) ? File.ReadAllText(log) : "";
            if (Regex.Matches(text, @"heartbeat  capped").Count != 2 || text.IndexOf("autonomy  capped", StringComparison.Ordinal) < 0 || text.IndexOf("checkin  idle: cap", StringComparison.Ordinal) < 0 || text.IndexOf("check-ins sent today", StringComparison.Ordinal) < 0) throw new Exception("The silence lines did not reach activity.log: " + text);
            if (CheckInWhy("capped", day, 48).IndexOf("24", StringComparison.Ordinal) < 0) throw new Exception("The check-in wake cap did not say its number.");
            checks.Add("PASS silence shows: a used-up heartbeat, offer or check-in budget and every reason a check-in waits are logged once a day in activity.log with the numbers, so a quiet day can be read back");
        }

        private static Preferences CareOnly(params CareKind[] kinds)
        {
            Preferences settings = new Preferences();
            for (int index = 0; index < Care.Names.Length; index++) settings.SetCare((CareKind)index, false, settings.CareTime((CareKind)index));
            foreach (CareKind kind in kinds) settings.SetCare(kind, true, settings.CareTime(kind));
            return settings;
        }

        private static void SmokeCareLogged(List<string> checks)
        {
            double clock = 0;
            DateTime start = new DateTime(2026, 11, 3, 14, 0, 0, DateTimeKind.Local);
            Care care = new Care(delegate { return clock; }, delegate { return start.AddSeconds(clock); }, delegate(CareKind kind) { return kind.ToString(); });
            CareWatch watch = new CareWatch();
            Preferences settings = CareOnly(CareKind.Water, CareKind.Stretch);
            int water = settings.CareTime(CareKind.Water) * 60, stretch = settings.CareTime(CareKind.Stretch) * 60;
            int both = Math.Max(water, stretch);
            care.Check(settings, 0, true, null, -1, 1);
            clock = both;
            if (care.Check(settings, 0, false, null, -1, 1) != null || !care.Waiting(CareKind.Water) || !care.Waiting(CareKind.Stretch)) throw new Exception("Care: due nudges were not held while she was busy.");
            List<string> told = watch.Seen(care, null, delegate { return "voice chat open"; }, start.AddSeconds(clock));
            if (!told.Contains("held water: voice chat open") || !told.Contains("held stretch: voice chat open")) throw new Exception("Care: a held nudge was not logged with why: " + string.Join(" / ", told.ToArray()));
            if (watch.Seen(care, null, delegate { return "voice chat open"; }, start.AddSeconds(clock + 5)).Count != 0) throw new Exception("Care: a held nudge was logged every tick.");
            care.Allow = CareWatch.Aloud;
            clock += 600;
            string said = care.Check(settings, 0, true, null, -1, 1);
            if (said != "Water" || !care.Waiting(CareKind.Stretch) || care.Dropped.Count != 0) throw new Exception("Care: in voice chat water was not said alone, or stretch was lost: " + said);
            told = watch.Seen(care, said, null, start.AddSeconds(clock));
            if (told.Count != 1 || !told[0].StartsWith("said water | Water (held 10 min: voice chat open)", StringComparison.Ordinal)) throw new Exception("Care: the said nudge was not logged with how long it waited: " + string.Join(" / ", told.ToArray()));
            care.Allow = null;
            said = care.Check(settings, 0, true, null, -1, 1);
            told = watch.Seen(care, said, null, start.AddSeconds(clock));
            if (said != "Stretch" || told.Count != 1 || !told[0].StartsWith("said stretch | Stretch", StringComparison.Ordinal)) throw new Exception("Care: the held stretch was not said once she was free: " + said);
            clock += both * 2;
            care.Check(settings, 0, false, null, -1, 1);
            watch.Seen(care, null, delegate { return "she is talking"; }, start.AddSeconds(clock));
            said = care.Check(settings, 0, true, null, -1, 1);
            told = watch.Seen(care, said, null, start.AddSeconds(clock));
            if (said == null || told.Count != 2 || !told.Exists(delegate(string one) { return one.StartsWith("dropped ", StringComparison.Ordinal) && one.IndexOf("another nudge went first", StringComparison.Ordinal) >= 0; })) throw new Exception("Care: a nudge skipped for another was not logged as dropped: " + string.Join(" / ", told.ToArray()));
            clock += both;
            care.Check(settings, 0, false, null, -1, 1);
            watch.Seen(care, null, delegate { return "a job is running"; }, start.AddSeconds(clock));
            care.Check(settings, Care.Away + 1, false, null, -1, 1);
            told = watch.Seen(care, null, null, start.AddSeconds(clock + 60));
            if (told.Count != 2 || !told.TrueForAll(delegate(string one) { return one.StartsWith("dropped ", StringComparison.Ordinal) && one.IndexOf("no longer due", StringComparison.Ordinal) >= 0; })) throw new Exception("Care: nudges lost to him being away were not logged: " + string.Join(" / ", told.ToArray()));
            checks.Add("PASS care nudges logged: every water, food, stretch or break nudge is logged when it is held (once, with why), when it is said (with how long it waited) and when it is dropped; in voice chat only water and food may go, the rest stay held");
        }

        private static void SmokeCareAfterRestart(List<string> checks)
        {
            double clock = 0;
            DateTime start = new DateTime(2026, 11, 3, 11, 0, 0, DateTimeKind.Local);
            Func<double> seconds = delegate { return clock; };
            Func<DateTime> local = delegate { return start.AddSeconds(clock); };
            Func<CareKind, string> words = delegate(CareKind kind) { return kind.ToString(); };
            Preferences settings = CareOnly(CareKind.Meal);
            Care before = new Care(seconds, local, words);
            before.Check(settings, 0, true, null, -1, 1);
            clock = 3600;
            if (before.Check(settings, 0, true, null, -1, 1) != "Meal") throw new Exception("Care: lunch was missed before any restart.");
            DateTime lunchAt = start.Date.AddHours(12);
            DateTime dinner = lunchAt.AddMinutes(settings.CareTime(CareKind.Meal));
            if (dinner.Hour >= 21 || dinner.AddHours(-1) <= start.AddSeconds(3600)) throw new Exception("Care: the meal gap leaves no dinner to test: " + dinner);
            clock = (dinner.AddHours(-1) - start).TotalSeconds;
            before.Check(settings, 0, true, null, -1, 1);
            string saved = CareWatch.Keep(before, local());
            clock += 60;
            Care after = new Care(seconds, local, words);
            after.Check(settings, 0, true, null, -1, 1);
            if (!CareWatch.Restore(after, saved, local()) || after.Since != start) throw new Exception("Care: a restart after lunch lost that he has been here since before lunch.");
            Care plain = new Care(seconds, local, words);
            plain.Check(settings, 0, true, null, -1, 1);
            clock = (dinner - start).TotalSeconds;
            if (after.Check(settings, 0, true, null, -1, 1) != "Meal") throw new Exception("Care: the dinner nudge did not come after a restart in the afternoon.");
            if (plain.Check(settings, 0, true, null, -1, 1) != null) throw new Exception("Care: a fresh start in the afternoon counted as being here since the morning.");
            string fed = CareWatch.Keep(after, local());
            clock += 60;
            Care again = new Care(seconds, local, words);
            again.Check(settings, 0, true, null, -1, 1);
            if (!CareWatch.Restore(again, fed, local()) || again.Check(settings, 0, true, null, -1, 1) != null) throw new Exception("Care: a restart repeated the dinner nudge.");
            Care late = new Care(seconds, local, words);
            late.Check(settings, 0, true, null, -1, 1);
            DateTime savedAt = dinner.AddHours(-1);
            if (CareWatch.Restore(late, saved, savedAt.AddMilliseconds(Care.Away + 60000)) || CareWatch.Restore(late, saved, savedAt.AddDays(1)) || CareWatch.Restore(late, "garbage", local()) || CareWatch.Restore(late, null, local())) throw new Exception("Care: an old, next-day or broken saved state was restored.");
            checks.Add("PASS meal after restart: a restart or the daily fresh start keeps that he has been at the PC since before lunch (care-state.txt beside activity.log, only within ten minutes and the same day), so lunch and dinner still come and are never said twice");
        }

        private void SmokeCareInVoiceChat(List<string> checks)
        {
            Preferences preferencesWas = preferences;
            Care careWas = care;
            Func<bool> screenWas = FullScreen;
            double nudgeWas = nudgeOut, voiceWas = voiceUntil, popWas = popOut, gameAtWas = careGameCheckAt;
            bool chatWas = chatting, hiddenWas = hidden, shushWas = shushed;
            string speechWas = speech, workWas = working, gameWas = careVoiceGame;
            Job jobWas = job;
            TalkState talkWas = talkState;
            List<string> spoken = new List<string>();
            double clock = 0;
            DateTime start = new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Local);
            Func<string, bool> runningWas = Lockout.Running;
            string phaseWas = Lockout.LeaguePhase;
            try
            {
                Lockout.Running = delegate { return false; };
                Lockout.LeaguePhase = null;
                Lockout.Recheck();
                if (Lockout.Active) throw new Exception("Care: the lockout stayed on with no game for the voice chat check.");
                preferences = CareOnly(CareKind.Water, CareKind.Stretch);
                care = new Care(delegate { return clock; }, delegate { return start.AddSeconds(clock); }, delegate(CareKind kind) { return kind.ToString(); });
                careSpoken = delegate(string line) { spoken.Add(line); };
                FullScreen = delegate { return false; };
                careGameCheckAt = double.MaxValue;
                careVoiceGame = null;
                shushed = hidden = false;
                speech = working = null;
                job = null;
                talkState = TalkState.Off;
                voiceUntil = 0;
                popOut = nudgeOut = -1;
                chatting = true;
                CheckCare(0, 0);
                clock = preferences.CareTime(CareKind.Water) * 60;
                CheckCare(clock, 0);
                clock += 1;
                CheckCare(clock, 0);
                if (spoken.Count != 1 || spoken[0] != "Water" || speech != null) throw new Exception("Care: in voice chat the water nudge was not said aloud: " + string.Join(",", spoken.ToArray()));
                clock = preferences.CareTime(CareKind.Stretch) * 60 + 1;
                CheckCare(clock, 0);
                CheckCare(clock + 1, 0);
                if (spoken.Count != 1 || !care.Waiting(CareKind.Stretch)) throw new Exception("Care: a stretch nudge broke into voice chat.");
                clock = preferences.CareTime(CareKind.Water) * 120 + 2;
                FullScreen = delegate { return true; };
                CheckCare(clock, 0);
                CheckCare(clock + 1, 0);
                if (spoken.Count != 1 || speech != null) throw new Exception("Care: a water nudge was said in voice chat during a game.");
                FullScreen = delegate { return false; };
                careVoiceGame = "Cyberpunk2077";
                CheckCare(clock + 2, 0);
                if (spoken.Count != 1) throw new Exception("Care: a water nudge was said in voice chat while a game was open.");
                careVoiceGame = null;
                CheckCare(clock + 3, 0);
                if (spoken.Count != 2) throw new Exception("Care: the held water nudge did not come after the game.");
                checks.Add("PASS care in voice chat: water and food nudges are said aloud in voice chat when he is not talking and no game or full-screen app is open; stretch and other nudges wait for her to be free");
            }
            finally
            {
                Lockout.Running = runningWas;
                Lockout.LeaguePhase = phaseWas;
                Lockout.Recheck();
                preferences = preferencesWas;
                care = careWas;
                careSpoken = null;
                FullScreen = screenWas;
                nudgeOut = nudgeWas;
                voiceUntil = voiceWas;
                popOut = popWas;
                careGameCheckAt = gameAtWas;
                careVoiceGame = gameWas;
                chatting = chatWas;
                hidden = hiddenWas;
                shushed = shushWas;
                speech = speechWas;
                working = workWas;
                job = jobWas;
                talkState = talkWas;
            }
        }

        private static string SetByVoice(string name, string level, string asked)
        {
            return Hands.Do("setting", new ToolArguments { Action = "set", Name = name, Level = level, Asked = asked });
        }

        private static List<string> SettingWords(string key)
        {
            List<string> found = new List<string>();
            string[] filler = { "that", "this", "with", "from", "mode", "your", "setting", "settings", "much", "level", "slider", "please", "capri", "change", "make", "turn" };
            foreach (string word in Regex.Split((key ?? "").ToLowerInvariant(), "[^a-z]+")) if (word.Length >= 4 && Array.IndexOf(filler, word) < 0 && !found.Contains(word)) found.Add(word);
            return found;
        }

        private void SmokeNumbersByVoice(List<string> checks)
        {
            ForkSettings settings = ForkSettings.Current;
            Tuning.Forget();
            settings.CheckInsPerDay = 2;
            settings.AwayCheckIns = true;
            string said = SetByVoice("check-ins", "set", "set most check-ins a day to 5");
            if (settings.CheckInsPerDay != 5 || !settings.AwayCheckIns || !said.StartsWith("Changed", StringComparison.Ordinal)) throw new Exception("'Set most check-ins a day to 5' did not reach the number: " + said);
            said = SetByVoice("check ins", "4", "set check ins a day to 4");
            if (settings.CheckInsPerDay != 4 || !settings.AwayCheckIns) throw new Exception("'Check ins a day to 4' landed on the on/off switch: " + said);
            said = SetByVoice("check-ins a day", "set", "raise check-ins a day by 2");
            if (settings.CheckInsPerDay != 6) throw new Exception("'Raise check-ins a day by 2' did not add two: " + said);
            said = SetByVoice("check-ins", "off", "turn check-ins off till 5");
            if (settings.AwayCheckIns || settings.CheckInsPerDay != 6) throw new Exception("'Turn check-ins off till 5' did not just switch them off: " + said);
            settings.AwayCheckIns = true;
            settings.HeartbeatOn = true;
            settings.HeartbeatPerDay = 24;
            said = SetByVoice("heartbeat", "set", "set heartbeat wake-ups a day to 48");
            if (settings.HeartbeatPerDay != 48 || !settings.HeartbeatOn) throw new Exception("'Set heartbeat wake-ups a day to 48' did not reach the number: " + said);
            said = SetByVoice("heartbeat", "40", "set heartbeat wake ups a day to 40");
            if (settings.HeartbeatPerDay != 40 || !settings.HeartbeatOn) throw new Exception("'Heartbeat wake ups a day to 40' landed on the on/off switch: " + said);
            settings.AutonomyOn = true;
            settings.AutoPerDay = 3;
            said = SetByVoice("autonomy", "6", "let her do 6 things a day by herself");
            if (settings.AutoPerDay != 6 || !settings.AutonomyOn) throw new Exception("'6 things a day by herself' landed on the autonomy switch: " + said);
            said = SetByVoice("humor", "set", "set humor to 30");
            if (settings.Humor != 30) throw new Exception("'Set humor to 30' with no number in the tool's value did not change it: " + said);
            settings.Humor = 50;
            List<string> wrong = new List<string>();
            foreach (Tunable number in Tuning.All)
            {
                if (number.Toggle || number.Listed || number.Upstream || number.Locked != null || number.Choices != null || number.Get == null || number.Set == null) continue;
                List<string> words = SettingWords(number.Key);
                foreach (Tunable toggle in Tuning.All)
                {
                    if (!toggle.Toggle || toggle.Upstream || toggle.Locked != null || toggle.Get == null || !SettingWords(toggle.Key).Exists(delegate(string word) { return words.Contains(word); })) continue;
                    int old = number.Get(), oldSwitch = toggle.Get();
                    int wanted = number.Low + (number.High - number.Low) / 2;
                    if (wanted == old) wanted++;
                    if (wanted > number.High) continue;
                    string answer = SetByVoice(toggle.Key, "set", "set " + number.Key + " to " + wanted);
                    if (number.Get() != wanted || toggle.Get() != oldSwitch) wrong.Add(toggle.Key + " -> " + number.Key + " (" + answer + ")");
                    number.Set(old);
                    toggle.Set(oldSwitch);
                }
            }
            if (wrong.Count > 0) throw new Exception("Number settings said by voice landed on an on/off switch: " + string.Join("; ", wrong.ToArray()));
            settings.CheckInsPerDay = 2;
            settings.HeartbeatPerDay = 24;
            settings.AutoPerDay = 3;
            Tuning.Forget();
            checks.Add("PASS numbers by voice: when the tool keeps only a setting's name and drops the number (set most check-ins a day to 5, heartbeat wake-ups a day to 48, 6 things a day), she reads the number and the setting from his whole line and changes the number, never the on/off switch with a similar name; checked over every number setting that shares words with a switch");
        }
    }
}
