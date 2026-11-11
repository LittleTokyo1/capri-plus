using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CapriDesktop
{
    internal sealed class CareWatch
    {
        internal const double KeepSeconds = 60;
        private readonly Dictionary<CareKind, string> held = new Dictionary<CareKind, string>();
        private readonly Dictionary<CareKind, DateTime> heldAt = new Dictionary<CareKind, DateTime>();

        internal static string Name(CareKind kind)
        {
            return Care.Names[(int)kind].ToLowerInvariant();
        }

        internal static bool Aloud(CareKind kind)
        {
            return kind == CareKind.Water || kind == CareKind.Meal;
        }

        internal static bool AtNight(CareKind kind)
        {
            return kind == CareKind.Late;
        }

        internal List<string> Seen(Care care, string line, Func<string> why, DateTime now)
        {
            List<string> told = new List<string>();
            for (int index = 0; index < Care.Names.Length; index++)
            {
                CareKind kind = (CareKind)index;
                string was;
                bool waited = held.TryGetValue(kind, out was);
                string after = waited ? " (held " + Math.Max(0, (int)(now - heldAt[kind]).TotalMinutes) + " min: " + was + ")" : "";
                if (line != null && care.Said == kind) told.Add(Tell("said", Name(kind) + " | " + line + after));
                else if (care.Dropped.Contains(kind)) told.Add(Tell("dropped", Name(kind) + (line != null ? ": another nudge went first" : ": no line for it in check-ins.md") + after));
                else if (care.Waiting(kind))
                {
                    if (waited) continue;
                    string because = why == null ? null : why();
                    held[kind] = string.IsNullOrEmpty(because) ? "not free" : because;
                    heldAt[kind] = now;
                    told.Add(Tell("held", Name(kind) + ": " + held[kind]));
                    continue;
                }
                else if (waited) told.Add(Tell("dropped", Name(kind) + ": no longer due" + after));
                held.Remove(kind);
                heldAt.Remove(kind);
            }
            care.Dropped.Clear();
            return told;
        }

        private static string Tell(string what, string detail)
        {
            ActivityLog.Write("care", what, detail);
            return what + " " + detail;
        }

        internal static string Keep(Care care, DateTime now)
        {
            return care.Since.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + care.Meal.ToString(CultureInfo.InvariantCulture) + "|" + now.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool Restore(Care care, string saved, DateTime now)
        {
            if (care == null || string.IsNullOrEmpty(saved)) return false;
            string[] parts = saved.Trim().Split('|');
            long since, meal, seen;
            if (parts.Length != 3 || !Number(parts[0], out since) || !Number(parts[1], out meal) || !Number(parts[2], out seen)) return false;
            if (seen > now.Ticks || now.Ticks - seen > Care.Away * TimeSpan.TicksPerMillisecond) return false;
            if (since <= 0 || since > now.Ticks || new DateTime(since).Date != now.Date) return false;
            if (since < care.Since.Ticks) care.Since = new DateTime(since);
            if (meal > care.Meal) care.Meal = meal;
            return true;
        }

        private static bool Number(string text, out long value)
        {
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }

    internal sealed partial class Companion
    {
        private const double CareGameSeconds = 15;
        private readonly CareWatch careWatch = new CareWatch();
        private bool careRestored, careAloud;
        private double careKeptAt = double.MinValue, careGameCheckAt = double.MinValue;
        private string careVoiceGame;
        private Action<string> careSpoken;

        private static string CareFile
        {
            get
            {
                string log = ActivityLog.FilePath;
                return string.IsNullOrEmpty(log) ? null : Path.Combine(Path.GetDirectoryName(log), "care-state.txt");
            }
        }

        private bool CareFree(double now, long quiet)
        {
            care.Allow = null;
            careAloud = false;
            bool night = CareNight();
            if (!hidden && nudgeOut < 0 && PopFree(now, quiet))
            {
                if (night) care.Allow = CareWatch.AtNight;
                return true;
            }
            if (!care.Waiting(CareKind.Water) && !care.Waiting(CareKind.Meal)) return false;
            if (night || !CareVoiceFree(now, quiet)) return false;
            care.Allow = CareWatch.Aloud;
            careAloud = true;
            return true;
        }

        private bool CareVoiceFree(double now, long quiet)
        {
            if (!chatting || hidden || nudgeOut >= 0 || quiet > Care.Away || Hushed || quitting) return false;
            if (CareInGame(now)) return false;
            return ChatIdle() && !dragging && !pressed && !typer.Visible && !menu.Visible && speech == null && talkState == TalkState.Off && outro == null
                && !onPanel && popOut < 0 && !docked && working == null && job == null && !pushing && now >= voiceUntil && !ForkSettings.Current.Paused && !ForkSettings.Current.NightOn;
        }

        private bool CareInGame(double now)
        {
            if (Lockout.Active || FullScreen()) return true;
            if (now >= careGameCheckAt)
            {
                careGameCheckAt = now + CareGameSeconds;
                careVoiceGame = Computer.GameOpen() ?? GameAhead();
            }
            return careVoiceGame != null;
        }

        private bool CareNight()
        {
            ForkSettings settings = ForkSettings.Current;
            return !testing && settings != null && Autonomy.Quiet(DateTime.Now, settings.QuietFrom, settings.QuietTo);
        }

        private string CareWhy(double now, long quiet)
        {
            if (CareNight()) return "quiet hours";
            if (hidden) return "she is hidden";
            if (nudgeOut >= 0) return "a nudge is on screen";
            if (Hushed) return "she is hushed";
            if (quitting) return "she is closing";
            if (InChat) return CareInGame(now) ? "voice chat open during a game" : "voice chat open";
            if (FullScreen()) return "a game or full-screen app is in front";
            if (quiet > 60000) return "no keys or mouse for a minute";
            if (dragging || pressed) return "he is holding her";
            if (typer.Visible) return "he is typing to her";
            if (menu.Visible || onPanel) return "a menu or her panel is open";
            if (speech != null || talkState != TalkState.Off || outro != null || speaker.Busy || now < voiceUntil) return "she is talking";
            if (popOut >= 0) return "a pop-in is showing";
            if (docked) return "she is docked in the game HUD";
            if (working != null || job != null) return "a job is running";
            if (pushing || transcribing) return "she is listening";
            return "not free";
        }

        private void CareSeen(string line, double now, long quiet)
        {
            careWatch.Seen(care, line, delegate { return CareWhy(now, quiet); }, DateTime.Now);
            if (testing) return;
            if (line != null) ProactiveGate.Record(ProactiveGate.Care, null, line, DateTime.Now);
            string path = CareFile;
            if (path == null) return;
            if (!careRestored)
            {
                careRestored = true;
                try { if (File.Exists(path)) CareWatch.Restore(care, File.ReadAllText(path), DateTime.Now); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (now - careKeptAt < CareWatch.KeepSeconds) return;
            careKeptAt = now;
            try { File.WriteAllText(path, CareWatch.Keep(care, DateTime.Now)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void CareAloudLine(string line)
        {
            if (careSpoken != null)
            {
                careSpoken(line);
                return;
            }
            heard = "(care)";
            box.Prompt = "> sys: ";
            brain.Own("care", line);
            Answer(++asking, line, TalkState.Talking, chatting);
        }
    }
}
