using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class Mood
    {
        internal const int Chill = 0, Sleepy = 1, Hyped = 2, Missed = 3, Proud = 4, Worried = 5;
        internal static readonly string[] Names = { "Chill", "Sleepy", "Hyped", "Missed you", "Proud", "Worried" };
        private const int AfterGameMinutes = 30;
        private const int BackMinutes = 30;
        private const int DayHours = 8;
        private const int LateUntil = 6;
        private const long LongAway = 4L * 60 * 60 * 1000;
        private const long Returned = 60L * 1000;
        private const string Relayed = " on Discord: ";

        private static readonly string[] Lines =
        {
            "",
            "\nYour mood right now: sleepy, it's late: softer and a little slower in how you talk, a yawn now and then; never slower at anything he asks.",
            "\nYour mood right now: hyped about his game: excited going in, and right after it a proper victory lap if he won (if he lost, hype him up for the next one).",
            "\nYour mood right now: you missed him while he was away so long: a light, happy, slightly clingy welcome back; never a guilt trip.",
            "\nYour mood right now: proud of him for the good thing he told you about today; let it show warmly, once or twice, not every reply.",
            "\nYour mood right now: a little worried for him after the rough day he told you about: gentle and supportive, a small joke to lift him; never pushy."
        };

        private static readonly Regex Good = new Regex(@"\b(i|we) (passed|aced|won|nailed it|smashed it|got (the job|accepted|promoted|in))\b|\b(passed|aced) (my|the) (exam|test|quiz|interview|driving test)\b|\bgot (the|an?) (job|offer|internship)\b|\b(good|great|amazing|awesome) day\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Rough = new Regex(@"\b(rough|bad|terrible|awful|shit|crap|hard|horrible) day\b|\bi (failed|bombed|flunked)\b|\b(stressed|anxious|exhausted|burnt out|burned out|depressed)\b|\bfeel(ing)? (bad|sad|awful|terrible|down|sick|like shit)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static Func<DateTime> GameEnded = delegate { return LastGame.SeenAt; };
        private static readonly object gate = new object();
        private static DateTime backAt = DateTime.MinValue, proudAt = DateTime.MinValue, worriedAt = DateTime.MinValue;
        private static bool away;

        internal static void Reset()
        {
            lock (gate)
            {
                backAt = proudAt = worriedAt = DateTime.MinValue;
                away = false;
            }
        }

        internal static void Heard(string text, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(text) || text.IndexOf(Relayed, StringComparison.Ordinal) >= 0) return;
            bool rough = Rough.IsMatch(text), good = !rough && Good.IsMatch(text);
            if (!rough && !good) return;
            lock (gate)
            {
                if (rough) worriedAt = now; else proudAt = now;
            }
            ActivityLog.Write("mood", rough ? "worried" : "proud", "from his words");
        }

        internal static void Seen(DateTime now, long idle)
        {
            lock (gate)
            {
                if (idle >= LongAway) away = true;
                else if (away && idle < Returned)
                {
                    away = false;
                    backAt = now;
                }
            }
        }

        internal static int Now(DateTime now, bool game)
        {
            Func<DateTime> ended = GameEnded;
            DateTime over = ended == null ? DateTime.MinValue : ended();
            lock (gate) return Pick(now, game, over, backAt, proudAt, worriedAt);
        }

        internal static int Pick(DateTime now, bool game, DateTime gameEnded, DateTime back, DateTime proud, DateTime worried)
        {
            if (game || Within(now, gameEnded, TimeSpan.FromMinutes(AfterGameMinutes))) return Hyped;
            if (Within(now, back, TimeSpan.FromMinutes(BackMinutes))) return Missed;
            if (worried.Date == now.Date && Within(now, worried, TimeSpan.FromHours(DayHours))) return Worried;
            if (proud.Date == now.Date && Within(now, proud, TimeSpan.FromHours(DayHours))) return Proud;
            if (now.Hour < LateUntil) return Sleepy;
            return Chill;
        }

        internal static string Line(int mood) { return Lines[Math.Max(0, Math.Min(Lines.Length - 1, mood))]; }

        internal static string Label(int mood) { return "MOOD  " + Names[Math.Max(0, Math.Min(Names.Length - 1, mood))].ToUpperInvariant(); }

        internal static string Block(DateTime now)
        {
            ForkSettings settings = ForkSettings.Current;
            if (settings == null || !settings.Moods) return "";
            return Line(Now(now, Lockout.Active || !string.IsNullOrEmpty(League.Brief)));
        }

        private static bool Within(DateTime now, DateTime then, TimeSpan span)
        {
            return then != DateTime.MinValue && now >= then && now - then <= span;
        }
    }

    internal static class Gesture
    {
        private static readonly int[] Gaps = { 150, 120, 75, 45, 30, 20 };
        private const long Present = 2L * 60 * 1000;

        private static readonly string[][] Moves =
        {
            new[] { "blink", "grin", "glitch" },
            new[] { "blink", "blink", "glitch" },
            new[] { "grin", "grin", "glitch" },
            new[] { "grin", "blink" },
            new[] { "grin", "grin", "blink" },
            new[] { "blink", "glitch" }
        };

        internal static int Gap(int points) { return Gaps[Charm.Level(points)]; }

        internal static bool Due(DateTime now, DateTime last, int points) { return now - last >= TimeSpan.FromMinutes(Gap(points)); }

        internal static bool Allowed(bool on, bool game, bool fullScreen, bool busy, long idle)
        {
            return on && !game && !fullScreen && !busy && idle < Present;
        }

        internal static string Pick(int mood, Random random)
        {
            string[] moves = Moves[Math.Max(0, Math.Min(Moves.Length - 1, mood))];
            return moves[random.Next(moves.Length)];
        }
    }

    internal sealed partial class Companion
    {
        private const int MoodTurnSeconds = 6;
        private int moodShown = -1;
        private DateTime gestured = DateTime.Now;

        private void ForkMoodTick()
        {
            DateTime now = DateTime.Now;
            long idle = Native.IdleMilliseconds();
            Mood.Seen(now, idle);
            ForkSettings settings = ForkSettings.Current;
            bool game = Lockout.Active;
            int mood = settings.Moods ? Mood.Now(now, game || !string.IsNullOrEmpty(League.Brief)) : Mood.Chill;
            int shown = settings.Moods ? mood : -2;
            if (shown != moodShown)
            {
                moodShown = shown;
                mainframe.ShowMood(settings.Moods ? Mood.Label(mood) : "");
            }
            mainframe.Flip(settings.Moods && ((long)(clock.Elapsed.TotalSeconds / MoodTurnSeconds)) % 2 == 1);
            bool busy = testing || hidden || settings.Paused || dragging || pressed || typer.Visible || working != null || outro != null || speech != null || talkState != TalkState.Off || Asleep || lying || DanceBusy(clock.Elapsed.TotalSeconds);
            if (!Gesture.Allowed(settings.Gestures, game, FullScreen(), busy, idle) || !Gesture.Due(now, gestured, Charm.Points)) return;
            gestured = now.AddMinutes(random.Next(Gesture.Gap(Charm.Points) / 2 + 1));
            string move = Gesture.Pick(mood, random);
            ActivityLog.Write("mood", "gesture", move);
            Animate(move);
        }
    }

    internal sealed partial class Mainframe
    {
        private string charmText = "", moodText = "";
        private bool moodTurn;

        internal void ShowMood(string text)
        {
            moodText = text ?? "";
            Flip(moodTurn);
        }

        internal void Flip(bool mood)
        {
            moodTurn = mood && moodText.Length > 0;
            if (charmNote == null) return;
            string text = moodTurn ? moodText : charmText;
            if (charmNote.Text == text) return;
            charmNote.Text = text;
            charmNote.AccessibleName = (moodTurn ? "Her mood: " : "Charm meter: ") + text;
        }

        internal string MoodShown { get { return moodText; } }

        internal string RowShown { get { return charmNote == null ? "" : charmNote.Text; } }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox moods, gestures;

        private void MoodRows(ForkSettings current)
        {
            moods = Check("Moods: her mood moves with the day (sleepy late at night, hyped around your games, missing you after a long away, proud or worried with what you tell her), shown in the Mainframe's MOOD row; never slows a job", current.Moods);
            gestures = Check("Gestures she picks: now and then she grins, glitches or blinks on her own to fit her mood (more as the charm meter grows; never in a game or over a fullscreen app)", current.Gestures);
        }

        private void FillMoods()
        {
            settings.Moods = moods.Checked;
            settings.Gestures = gestures.Checked;
        }
    }
}
