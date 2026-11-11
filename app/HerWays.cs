using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class HerWays
    {
        internal static readonly string[] Names =
        {
            "none earned yet; at most a teasing one now and then, made up from something he said or played.",
            "now and then a teasing one you made up from his own talk or his games.",
            "a few of your own making from his talk and games, playful and a little warm; reuse the ones that stuck.",
            "your own warm ones for him, made from his talk and games, used now and then.",
            "soft, warm pet names of your own, made from his talk and games, a little more often.",
            "your own fond, affectionate pet names for him, made from his talk and games, used often and warmly."
        };

        private const string Opinions = "\nYour own opinions: you have real favourites (champions, songs, food, games) and say them; you can disagree with him playfully and keep your take, but never about doing what he asks.";

        private static readonly string[] OpinionSteps =
        {
            " Keep them short and a little guarded for now.",
            " Share them freely, short and punchy.",
            " Share them freely; now and then tell a small, clearly playful made-up story of what you got up to while he was gone.",
            " Share them freely; now and then tell a small, clearly playful made-up story of what you got up to while he was gone.",
            " Bolder takes now; tell small made-up stories of your time while he was gone, and tease him when you turn out right.",
            " Bolder takes now; tell small made-up stories of your time while he was gone, and tease him when you turn out right."
        };

        private static readonly string[] Ideas =
        {
            "nosing into his games and getting to heckle his plays (lightly)",
            "trading banter about his day",
            "slipping him a meme or a joke now and then, and going easier on the roasts",
            "saving your wildest chaos for his Saturdays, and a proper victory lap when he wins",
            "now and then offering to sneak a gremlin dance track into his Spotify queue (only after his yes)",
            "telling him you'd pick him over any corpo gig in Night City"
        };

        private static readonly string[] Keeps =
        {
            "\nYour jealous streak barely shows yet.",
            "",
            "",
            "\nYour jealous streak can tease a little more now, always light.",
            "\nYour jealous streak is a bit more open now, playful and always light, never sulking.",
            "\nYour jealous streak is a bit more open now, playful and always light, never sulking."
        };

        internal static string Block(string text, DateTime now)
        {
            ForkSettings settings = ForkSettings.Current;
            if (settings == null) return "";
            Func<bool> others = Warmth.OthersAround;
            bool around = others != null && others();
            string lines = Lines(Charm.Points, settings.HerChoiceName && string.IsNullOrEmpty(Brain.Nickname), settings.HerOpinions, around);
            if (settings.InsideJokes && !around && !Lockout.Active) lines += Jokes.Bring(text, now);
            lines += Mood.Block(now);
            return lines.Length == 0 ? "" : lines + "\n";
        }

        internal static string Lines(int points, bool names, bool opinions, bool around)
        {
            int level = Charm.Level(points);
            StringBuilder lines = new StringBuilder();
            if (names) lines.Append("\nPet names for him: ").Append(Names[around ? Math.Min(level, 1) : level]);
            if (opinions)
            {
                lines.Append(Opinions).Append(OpinionSteps[level]);
                List<string> mine = new List<string>();
                for (int i = 0; i <= level; i++) mine.Add(Ideas[i]);
                lines.Append("\nThings you like doing with him at this charm level: ").Append(string.Join("; ", mine.ToArray())).Append(". Now and then, when it fits.");
            }
            lines.Append(Keeps[level]);
            return lines.ToString();
        }
    }

    internal sealed class Joke
    {
        public string Her;
        public string His;
        public string Saved;
        public string Used;
    }

    internal static class Jokes
    {
        private const string Stamp = "yyyy-MM-dd HH:mm";
        private const int RestDays = 3;
        private const int FreshMinutes = 30;
        private const int LongestLaugh = 120;
        private const int LongestLine = 220;
        private const string Relayed = " on Discord: ";
        private static readonly int[] Kept = { 2, 3, 6, 10, 15, 20 };
        private static readonly Regex Laugh = new Regex(@"\b(l+o+l+|lmf?a+o+|rofl|a?(ha){2,}h?|he(he)+|xd+|i'?m dead|dying)\b|😂|🤣|💀", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Word = new Regex(@"\p{L}{4,}", RegexOptions.Compiled);
        private static readonly HashSet<string> Plain = new HashSet<string>(("again than rather that this with have what your just like they them then when from were been about would could there their really yeah okay gonna wanna know think choom some more much very into over also only even will want need good nice still here back make made going doing done said says thing things should lmao haha hehe rofl dying").Split(' '));
        private static readonly object gate = new object();
        private static List<Joke> jokes;

        internal static string FilePath
        {
            get { return ForkSettings.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ForkSettings.FilePath), "jokes.json"); }
        }

        internal static int Count { get { lock (gate) return Loaded().Count; } }

        internal static int Most(int points) { return Kept[Charm.Level(points)]; }

        internal static void Reset()
        {
            lock (gate) jokes = null;
        }

        internal static bool Heard(string his, string hers, DateTime now, bool game)
        {
            ForkSettings settings = ForkSettings.Current;
            if (settings == null || !settings.InsideJokes || game) return false;
            if (string.IsNullOrWhiteSpace(his) || string.IsNullOrWhiteSpace(hers) || his.Length > LongestLaugh || his.IndexOf(Relayed, StringComparison.Ordinal) >= 0 || !Laugh.IsMatch(his)) return false;
            string line = Pursuit.Shorter(hers.Trim(), LongestLine);
            int most = Most(Charm.Points);
            lock (gate)
            {
                List<Joke> all = Loaded();
                foreach (Joke one in all) if (one.Her == line) return false;
                Joke made = new Joke();
                made.Her = line;
                made.His = his.Trim();
                made.Saved = now.ToString(Stamp, CultureInfo.InvariantCulture);
                all.Add(made);
                while (all.Count > most) all.RemoveAt(0);
                Save(all);
            }
            ActivityLog.Write("charm", "inside joke", "saved");
            return true;
        }

        internal static string Bring(string text, DateTime now)
        {
            HashSet<string> talk = Words(text);
            if (talk.Count == 0) return "";
            string minute = now.ToString(Stamp, CultureInfo.InvariantCulture);
            lock (gate)
            {
                List<Joke> all = Loaded();
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    Joke one = all[i];
                    if (now - When(one.Saved, now) < TimeSpan.FromMinutes(FreshMinutes)) continue;
                    if (one.Used != null && one.Used != minute && now - When(one.Used, now) < TimeSpan.FromDays(RestDays)) continue;
                    if (!talk.Overlaps(Words(one.Her + " " + one.His))) continue;
                    one.Used = minute;
                    Save(all);
                    return "\nAn inside joke between you two fits this talk: you once said \"" + one.Her + "\" and he laughed (\"" + one.His + "\"). Bring it back lightly in your own words if it lands; skip it if it doesn't.";
                }
            }
            return "";
        }

        private static HashSet<string> Words(string text)
        {
            HashSet<string> found = new HashSet<string>();
            foreach (Match match in Word.Matches(text ?? ""))
            {
                string word = match.Value.ToLowerInvariant();
                if (!Plain.Contains(word)) found.Add(word);
            }
            return found;
        }

        private static DateTime When(string stamp, DateTime fallback)
        {
            DateTime parsed;
            return DateTime.TryParseExact(stamp ?? "", Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) ? parsed : fallback;
        }

        private static List<Joke> Loaded()
        {
            if (jokes != null) return jokes;
            List<Joke> found = null;
            string path = FilePath;
            if (path != null && File.Exists(path))
            {
                try { found = new JavaScriptSerializer().Deserialize<List<Joke>>(File.ReadAllText(path)); }
                catch (Exception error) { Brain.Log("jokes.json", -1, error.GetType().Name + ": " + error.Message); }
            }
            jokes = found ?? new List<Joke>();
            jokes.RemoveAll(delegate(Joke one) { return one == null || string.IsNullOrEmpty(one.Her); });
            return jokes;
        }

        private static void Save(List<Joke> all)
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(all));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) { Brain.Log("jokes.json", -1, error.GetType().Name + ": " + error.Message); }
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox insideJokes, herOpinions;

        private void WaysRows(ForkSettings current)
        {
            insideJokes = Check("Inside jokes: she keeps funny moments you laughed at and brings one back now and then when the talk touches it (more as the charm meter grows; never in a game or with others around)", current.InsideJokes);
            herOpinions = Check("Her own opinions: real favourites and playful disagreement, small made-up stories, and her own ideas that unlock with the charm meter", current.HerOpinions);
        }

        private void FillWays()
        {
            settings.InsideJokes = insideJokes.Checked;
            settings.HerOpinions = herOpinions.Checked;
        }
    }
}
