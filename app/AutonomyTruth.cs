using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static partial class Autonomy
    {
        internal const int MuteDays = 14;
        internal const int SharedWords = 2;
        internal const int IgnoredInRow = 2;
        internal const int SpokeSlackSeconds = 3;
        private static readonly string[] Plain = { "your", "with", "that", "this", "want", "would", "could", "should", "shall", "maybe", "about", "there", "them", "they", "have", "from", "some", "what", "when", "will", "just", "like", "look", "make", "help", "time", "today", "tonight", "tomorrow", "choom" };
        private static readonly object spokeGate = new object();
        private static string spokeKey;
        private static long spokeAt;

        internal static void Spoke(string said, DateTime now)
        {
            string key = Key(said);
            if (key.Length == 0) return;
            lock (spokeGate)
            {
                spokeKey = key;
                spokeAt = Heartbeat.Utc(now).Ticks;
            }
        }

        internal static void ForgetSpoke()
        {
            lock (spokeGate)
            {
                spokeKey = null;
                spokeAt = 0;
            }
        }

        private static bool AboutOffer(AutoOffer offer, DateTime now)
        {
            string key;
            long at;
            lock (spokeGate)
            {
                key = spokeKey;
                at = spokeAt;
            }
            if (key == null || at <= offer.Told + SpokeSlackSeconds * TimeSpan.TicksPerSecond || at > Heartbeat.Utc(now).Ticks) return true;
            string offered = Key(offer.Text);
            return offered.Length > 0 && key.Contains(offered);
        }

        internal static List<string> Words(string text)
        {
            List<string> words = new List<string>();
            foreach (Match word in Regex.Matches((text ?? "").ToLowerInvariant(), @"[a-z][a-z']{3,}"))
            {
                string one = word.Value.Replace("'", "");
                if (one.Length >= 4 && Array.IndexOf(Plain, one) < 0 && !words.Contains(one)) words.Add(one);
            }
            return words;
        }

        internal static bool Alike(string a, string b)
        {
            if (Key(a) == Key(b)) return true;
            List<string> one = Words(a), two = Words(b);
            int shared = 0;
            foreach (string word in one) if (two.Contains(word)) shared++;
            return shared >= SharedWords;
        }

        private static List<AutoOffer> Declined(AutoState live, DateTime now)
        {
            long since = Heartbeat.Utc(now).Ticks - MuteDays * TimeSpan.TicksPerDay;
            return live.Offers.FindAll(delegate(AutoOffer offer) { return offer.Outcome == No && Math.Max(offer.Told, offer.Made) >= since; });
        }

        internal static bool Muted(string area, string text, DateTime now)
        {
            lock (gate)
            {
                foreach (AutoOffer offer in Declined(Current(), now))
                    if (offer.Area == area && Alike(offer.Text, text)) return true;
            }
            return false;
        }

        internal static string Avoid(DateTime now)
        {
            StringBuilder text = new StringBuilder();
            lock (gate)
            {
                AutoState live = Current();
                foreach (AutoOffer offer in Declined(live, now)) text.Append("\nHe said no to this lately; never offer it or anything like it again: ").Append(offer.Text);
                foreach (string area in Areas)
                {
                    List<AutoOffer> told = live.Offers.FindAll(delegate(AutoOffer offer) { return offer.Area == area && offer.Told > 0 && offer.Outcome != Open; });
                    told.Sort(delegate(AutoOffer a, AutoOffer b) { return a.Told.CompareTo(b.Told); });
                    if (told.Count < IgnoredInRow) continue;
                    bool ignored = true;
                    for (int i = told.Count - IgnoredInRow; i < told.Count; i++) if (told[i].Outcome != Ignored) ignored = false;
                    if (ignored) text.Append("\nHe ignored your last ").Append(IgnoredInRow).Append(" ").Append(area).Append(" offers: in ").Append(area).Append(" offer only what is clearly worth his time, or nothing.");
                }
            }
            return text.ToString();
        }
    }
}
