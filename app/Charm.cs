using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed class CharmState
    {
        public int Points = Charm.Start;
        public int Best = -1;
        public string Day;
        public int Time;
        public int Talk;
        public int Pats;
        public int Nice;
        public int Rude;
        public int Minutes;
        public string LastMinute;
    }

    internal static class Charm
    {
        internal const int Start = 120;
        internal const int Most = 1000;
        internal const int TalkCap = 15;
        internal const int PatCap = 8;
        internal const int TimeCap = 12;
        internal const int NiceGain = 3;
        internal const int NiceCap = 12;
        internal static Action RudeHeard;
        internal const int RudeCost = 15;
        internal const int RudeCap = 45;
        internal const int MinutesPerPoint = 10;
        internal const int PetOdds = 3;
        internal static readonly int[] Steps = { 0, 50, 120, 300, 600, 1000 };
        internal static readonly string[] Names = { "Curious", "Friendly", "Fond", "Close", "Smitten", "Devoted" };

        private static readonly string[] Tones =
        {
            "You are still warming up to them: teasing and a little guarded, not sweet yet.",
            "You like them: relaxed and playful, teasing with a grin.",
            "You are openly fond of them: warm teasing, happy when they show up.",
            "You are close to them: affectionate and protective, with inside jokes, and you miss them when they are gone.",
            "You are smitten with them: very affectionate and now and then flustered by their compliments, still a teasing gremlin.",
            "You are devoted to them: fiercely loyal and openly attached, still your teasing self, never clingy or gushing."
        };

        private static readonly string[] Ups =
        {
            null,
            "Charm meter: Friendly. Don't let it go to your head, choom.",
            "Charm meter just hit Fond. Okay, fine. I like you. A bit.",
            "Charm meter: Close. You're officially my favourite human. Don't tell the others.",
            "Charm meter: Smitten. My cooling fans can't keep up with you, choom.",
            "Charm meter maxed out: Devoted. You're stuck with me now, choom."
        };

        private static readonly string[][] Pets =
        {
            new[] { "Pats from you hit different, choom.", "Logging this one under favourite moments.", "Keep that up and I'll start following you between monitors." },
            new[] { "Okay, I'm blushing. In binary. Stop it. Don't stop it.", "My whole firmware just went soft, choom." },
            new[] { "Of all the netrunners in Night City, I got you. Lucky me.", "Keep that up and I'm never leaving this desktop." }
        };

        private static readonly Regex Kind = new Regex(@"\b(thank(s|s a lot| you| u)|thx|tysm|ty|love (you|u|ya)|luv (you|u)|good (girl|job|work)|well done|nice (work|job|one)|great job|you('re| are|r| r) (the best|amazing|awesome|cute|adorable|great|so smart|smart|so cute|a genius|sweet|the goat)|i (like|love|appreciate) (you|u)|best (girl|ai))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Mean = new Regex(@"\b(shut up|stfu|fuck (you|u|off)|screw (you|u)|i hate (you|u)|you suck|u suck)\b|\b(you('re| are|r| r)|ur|u r) (so |such an? |a |an |really |fucking )?(stupid|dumb|idiot|useless|annoying|trash|garbage|worthless|moron|pathetic)\b|\b(stupid|dumb|useless|worthless) (bot|ai|robot|capri|girl|bitch)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Her = new Regex(@"\b(capri|kapri|capry)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly object gate = new object();
        private static CharmState state;
        private static string news;
        private static int version;

        internal static string FilePath
        {
            get { return ForkSettings.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ForkSettings.FilePath), "charm.json"); }
        }

        internal static int Version { get { lock (gate) return version; } }

        internal static int Points { get { lock (gate) return Loaded().Points; } }

        internal static void Reset()
        {
            lock (gate)
            {
                state = null;
                news = null;
                version++;
            }
        }

        internal static int Level(int points)
        {
            int level = 0;
            for (int i = 0; i < Steps.Length; i++) if (points >= Steps[i]) level = i;
            return level;
        }

        internal static string Name(int points) { return Names[Level(points)]; }

        internal static int Percent(int points) { return Math.Max(0, Math.Min(100, points * 100 / Most)); }

        internal static string Label()
        {
            int points = Points;
            return "CHARM " + Percent(points) + "%  " + Name(points).ToUpperInvariant();
        }

        internal static string Block()
        {
            int points = Points;
            return "\nYour charm meter (how fond you are of them; it grows with time together, talks, head pats and kind words, and drops when they are rude to you): " + Percent(points) + "%, " + Name(points) + ". " + Tones[Level(points)] + " When they ask how much you like them or about your charm meter, tell them the percent and the level in your own words." + Warmth.Line() + "\n";
        }

        internal static void Heard(string text, DateTime now) { Heard(text, now, false, false); }

        internal static void Heard(string text, DateTime now, bool voiced, bool game)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            bool rude = Mean.IsMatch(text) && !game && (!voiced || Her.IsMatch(text));
            Action told = RudeHeard;
            if (rude && told != null) told();
            lock (gate)
            {
                CharmState current = Loaded();
                int was = current.Points;
                Roll(current, now);
                if (rude)
                {
                    int cost = Math.Min(RudeCost, RudeCap - current.Rude);
                    if (cost <= 0) return;
                    current.Rude += cost;
                    Change(current, was, -cost, "rude");
                    return;
                }
                int gain = 0;
                if (current.Talk < TalkCap) { current.Talk++; gain++; }
                if (Kind.IsMatch(text) && current.Nice < NiceCap)
                {
                    int kind = Math.Min(NiceGain, NiceCap - current.Nice);
                    current.Nice += kind;
                    gain += kind;
                }
                if (gain > 0) Change(current, was, gain, "talk");
            }
        }

        internal static void Patted(DateTime now)
        {
            lock (gate)
            {
                CharmState current = Loaded();
                int was = current.Points;
                Roll(current, now);
                if (current.Pats >= PatCap) return;
                current.Pats++;
                Change(current, was, 1, "pats");
            }
        }

        internal static void Together(DateTime now, bool present)
        {
            if (!present) return;
            lock (gate)
            {
                CharmState current = Loaded();
                string minute = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                if (current.LastMinute == minute) return;
                int was = current.Points;
                Roll(current, now);
                current.LastMinute = minute;
                current.Minutes++;
                if (current.Minutes % MinutesPerPoint != 0 || current.Time >= TimeCap) { Save(current); return; }
                current.Time++;
                Change(current, was, 1, "time");
            }
        }

        internal static void Set(int points)
        {
            lock (gate)
            {
                CharmState current = Loaded();
                current.Points = Math.Max(0, Math.Min(Most, points));
                version++;
                Save(current);
            }
        }

        internal static string TakeNews()
        {
            lock (gate)
            {
                string line = news;
                news = null;
                return line;
            }
        }

        internal static string PetLine(Random random)
        {
            int level = Level(Points);
            if (level < 3 || random.Next(PetOdds) != 0) return null;
            int count = 0;
            for (int i = 0; i <= level - 3; i++) count += Pets[i].Length;
            int pick = random.Next(count);
            for (int i = 0; i <= level - 3; i++)
            {
                if (pick < Pets[i].Length) return Pets[i][pick];
                pick -= Pets[i].Length;
            }
            return null;
        }

        private static void Change(CharmState current, int was, int by, string why)
        {
            current.Points = Math.Max(0, Math.Min(Most, current.Points + by));
            int before = Level(was);
            int after = Level(current.Points);
            if (after > current.Best)
            {
                current.Best = after;
                news = Ups[after];
            }
            if (after != before) ActivityLog.Write("charm", why, Names[before] + " -> " + Names[after] + " (" + Percent(current.Points) + "%)");
            version++;
            Save(current);
        }

        private static void Roll(CharmState current, DateTime now)
        {
            string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (current.Day == day) return;
            current.Day = day;
            current.Time = current.Talk = current.Pats = current.Nice = current.Rude = current.Minutes = 0;
        }

        private static CharmState Loaded()
        {
            if (state != null) return state;
            CharmState found = null;
            string path = FilePath;
            if (path != null && File.Exists(path))
            {
                try { found = new JavaScriptSerializer().Deserialize<CharmState>(File.ReadAllText(path)); }
                catch (Exception error) { Brain.Log("charm.json", -1, error.GetType().Name + ": " + error.Message); }
            }
            if (found == null) found = new CharmState();
            found.Points = Math.Max(0, Math.Min(Most, found.Points));
            if (found.Best < Level(found.Points)) found.Best = Level(found.Points);
            state = found;
            return state;
        }

        private static void Save(CharmState current)
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(current));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) { Brain.Log("charm.json", -1, error.GetType().Name + ": " + error.Message); }
        }
    }
}
