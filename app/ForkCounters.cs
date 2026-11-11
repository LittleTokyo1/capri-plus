using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    // Capri+ lane counters: in champ select, each new enemy pick (up to three) gets a quick background web look-up of the best
    // counterpicks for their own role, as long as their own pick isn't locked in yet. Three names, said only when he asks.
    internal sealed partial class Companion
    {
        private const int MaxCounters = 3;
        private readonly List<int> countered = new List<int>();
        private volatile bool countering;

        private int CounterCount { get { lock (countered) return countered.Count; } }

        // Counters replace the three-enemies research for a draft once any have run; the research still covers a draft they locked in early.
        internal bool ResearchWanted(int enemies)
        {
            return ForkSettings.Current.LeagueResearch && !researching && researchedFor < 0 && enemies >= 3 && CounterCount == 0;
        }

        // The next enemy champion that hasn't had a counter look-up, or 0. Nothing once their pick is locked, with no role, or while one runs.
        internal int NextCounter(ChampSession session, ChampSeat me, ChampAction pick)
        {
            if (!ForkSettings.Current.LeagueResearch || countering || session == null || me == null || League.Role(me.Position) == null) return 0;
            if (pick == null || pick.Completed) return 0;
            lock (countered)
            {
                if (countered.Count >= MaxCounters) return 0;
                foreach (ChampSeat seat in session.Theirs ?? new ChampSeat[0])
                    if (seat.Champion > 0 && !countered.Contains(seat.Champion)) return seat.Champion;
            }
            return 0;
        }

        internal static string CounterAsk(string enemy, string position, string[] favourites)
        {
            string ask = "League of Legends, current patch. I'm playing " + League.Role(position) + " and the enemy picked " + enemy + ". " +
                "Search for this patch's matchup data, then reply with only the names of the 3 best counterpicks for my role into " + enemy + ", comma separated.";
            if (favourites != null && favourites.Length > 0) ask += " Put my favourites for this role first if any of them fit: " + string.Join(", ", favourites) + ".";
            return ask + " Only champions nobody has picked or banned. Three names, nothing else: no reasons, no lists, no links.";
        }

        // On the League worker thread. Starts one look-up when there is a new enemy to counter; answers the shown text when told to wait (the smoke test).
        internal string CounterTurn(ChampSession session, bool wait)
        {
            ChampSeat me = session == null ? null : session.Seat;
            int enemy = NextCounter(session, me, League.MyPick(session));
            Cloud remote = brain.Remote;
            if (enemy <= 0 || remote == null || !remote.Claude) return null;
            lock (countered) countered.Add(enemy);
            countering = true;
            string name = league.Name(enemy), position = me.Position;
            List<string> champions = league.Champions(), taken = Taken(session);
            ActivityLog.Write("league counter", name, null);
            if (wait) return Counter(remote, name, position, champions, taken);
            ThreadPool.QueueUserWorkItem(delegate { Counter(remote, name, position, champions, taken); });
            return null;
        }

        private string Counter(Cloud remote, string enemy, string position, List<string> champions, List<string> taken)
        {
            string found = null;
            try { found = Three(CounterAnswer(remote, enemy, position), champions, taken); }
            catch (Exception error) { Brain.Log("league counter", -1, error.Message); }
            finally { countering = false; }
            ActivityLog.Write("league counter", enemy, found ?? "no answer");
            if (found == null || !drafting) return null;
            string text = "Into " + enemy + ": " + found + ".";
            league.Researched = league.Researched.Length > 0 ? league.Researched + " " + text : text;
            League.Brief = Briefing(league.Picks);
            if (!testing && ForkSettings.Current.CountersUnasked)
                Post(delegate
                {
                    if (Lockout.LeaguePhase != "ChampSelect") return;
                    Tell("(champ select)", text);
                    brain.Own("champ select counters into " + enemy, text);
                });
            return text;
        }

        // Every champion already picked, hovered by a teammate or banned, so none of them is named as a pick. Their own hover stays free.
        private List<string> Taken(ChampSession session)
        {
            List<string> taken = new List<string>();
            List<int> ids = League.Banned(session);
            foreach (ChampSeat seat in session.Theirs ?? new ChampSeat[0]) ids.Add(seat.Champion);
            foreach (ChampSeat seat in session.Mine ?? new ChampSeat[0]) if (seat != session.Seat) { ids.Add(seat.Champion); ids.Add(seat.Intent); }
            foreach (int id in ids)
            {
                string name = id > 0 ? league.Name(id) : null;
                if (name != null && !taken.Contains(name)) taken.Add(name);
            }
            return taken;
        }

        // The model's answer cut to the first three free champions it names, in its order: "Katarina, Diana, Viktor", or null.
        internal static string Three(string answer, List<string> champions, List<string> taken)
        {
            if (string.IsNullOrWhiteSpace(answer) || champions == null) return null;
            List<KeyValuePair<int, string>> named = new List<KeyValuePair<int, string>>();
            foreach (string champion in champions)
            {
                if (string.IsNullOrEmpty(champion) || taken.Exists(gone => string.Equals(gone, champion, StringComparison.OrdinalIgnoreCase))) continue;
                Match at = Regex.Match(answer, @"(?<![\w'])" + Regex.Escape(champion) + @"(?![\w'])", RegexOptions.IgnoreCase);
                if (at.Success) named.Add(new KeyValuePair<int, string>(at.Index, champion));
            }
            if (named.Count == 0) return null;
            named.Sort((a, b) => a.Key.CompareTo(b.Key));
            List<string> three = new List<string>();
            foreach (KeyValuePair<int, string> one in named) if (three.Count < 3) three.Add(one.Value);
            return string.Join(", ", three.ToArray());
        }

        private static readonly Regex CounterWords = new Regex(@"\b(counter\w*|what (should|do|can) i (pick|play|go)|who (should|do|can) i (pick|play)|best picks?|what('s| is) good (into|against))\b", RegexOptions.IgnoreCase);
        private static readonly Regex CounterEntry = new Regex(@"(?:Into ([^:.]+)|Picks into their team): ([^.]+)\.");

        // "counters?" in champ select: the newest look-up's names, or the one for the enemy they named. Null when it isn't that or nothing is ready.
        internal static string CountersSaid(string text, string researched)
        {
            if (string.IsNullOrWhiteSpace(text) || !CounterWords.IsMatch(text) || string.IsNullOrEmpty(researched)) return null;
            string newest = null;
            foreach (Match entry in CounterEntry.Matches(researched))
            {
                string enemy = entry.Groups[1].Value.Trim();
                string names = entry.Groups[2].Value.Trim() + ".";
                if (enemy.Length > 0 && Regex.IsMatch(text, @"\b" + Regex.Escape(enemy) + @"\b", RegexOptions.IgnoreCase)) return names;
                newest = names;
            }
            return newest;
        }

        private bool CountersHeard(string text, bool voiceChat)
        {
            if (voiceChat || !drafting) return false;
            string said = CountersSaid(text, league.Researched);
            if (said == null) return false;
            ActivityLog.Write("league counter", "asked", said);
            Tell("(counters)", said);
            return true;
        }

        private static string CounterAnswer(Cloud remote, string enemy, string position)
        {
            string system = "You are a sharp League of Legends coach. Be specific and current." + Context.Core();
            string ask = Voice.Quote(CounterAsk(enemy, position, ForkSettings.Current.Favorites(position)));
            int status;
            string answer = Quick.Ask(remote, system, ask, LookupTool, 600, "off", out status);
            if (answer == null && status == 400) answer = Quick.Ask(remote, system, ask, null, 400, "off", out status);
            return answer;
        }

        private void EndCounters()
        {
            lock (countered) countered.Clear();
        }
    }
}
