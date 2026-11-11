using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class ProMatch
    {
        [DataMember(Name = "calculatedRole")] public string Role;
        [DataMember(Name = "championId")] public int Champion;
        [DataMember(Name = "completedItems")] public int[] Completed;
        [DataMember(Name = "finalBuild")] public int[] Final;
        [DataMember(Name = "itemPath")] public ProItemEvent[] Path;
        [DataMember(Name = "normalizedName")] public string Handle;
        [DataMember(Name = "proInfo")] public ProInfo Info;
        [DataMember(Name = "opponentChampionId")] public int Opponent;
        [DataMember(Name = "runes")] public ProRunes Runes;
        [DataMember(Name = "statShards")] public int[] Shards;
        [DataMember(Name = "summonerSpells")] public long[] Spells;
        [DataMember(Name = "skillOrders")] public string Skills;
        [DataMember(Name = "totalKills")] public int Kills;
        [DataMember(Name = "totalDeaths")] public int Deaths;
        [DataMember(Name = "totalAssists")] public int Assists;
        [DataMember(Name = "win")] public bool Won;
        [DataMember(Name = "matchTimestamp")] public long When;
        [DataMember(Name = "matchId")] public long Id;

        internal string Pro { get { return Info != null && !string.IsNullOrEmpty(Info.Name) ? Info.Name : Handle ?? "a pro"; } }
    }

    [DataContract]
    internal sealed class ProItemEvent
    {
        [DataMember(Name = "itemId")] public int Item;
        [DataMember(Name = "timestamp")] public long At;
        [DataMember(Name = "type")] public int Type;
    }

    [DataContract]
    internal sealed class ProInfo
    {
        [DataMember(Name = "officialName")] public string Name;
        [DataMember(Name = "currentTeam")] public string Team;
    }

    [DataContract]
    internal sealed class ProRunes
    {
        [DataMember(Name = "perk0")] public long Perk0;
        [DataMember(Name = "perk1")] public long Perk1;
        [DataMember(Name = "perk2")] public long Perk2;
        [DataMember(Name = "perk3")] public long Perk3;
        [DataMember(Name = "perk4")] public long Perk4;
        [DataMember(Name = "perk5")] public long Perk5;
        [DataMember(Name = "primaryStyle")] public long Primary;
        [DataMember(Name = "subStyle")] public long Sub;
    }

    internal sealed class ProBuild
    {
        internal int Number;
        internal int Count;
        internal int Games;
        internal ProMatch Match;
        internal List<ProMatch> Setup;
        internal List<ProMatch> Lane;
        internal const int FullItems = 6;
        internal const int CoreItems = 3;
        internal const int OptionItems = 6;
        private static readonly int[] Boots = { 1001, 2422, 3005, 3006, 3009, 3010, 3013, 3020, 3047, 3111, 3117, 3158, 3170, 3171, 3172, 3173, 3174, 3175, 3176 };

        internal static bool IsBoots(int id) { return Array.IndexOf(Boots, id) >= 0; }

        internal int[] Full()
        {
            List<int> full = new List<int>();
            foreach (int id in Match.Completed ?? new int[0]) if (full.Count < CoreItems && Fits(full, id)) full.Add(id);
            foreach (List<ProMatch> games in new[] { Setup, Lane })
                foreach (int id in MostBuilt(games)) if (Fits(full, id)) full.Add(id);
            return full.ToArray();
        }

        internal int[] Options()
        {
            List<int> taken = new List<int>(Full());
            List<int> options = new List<int>();
            foreach (int id in MostBuilt(Lane))
                if (options.Count < OptionItems && !taken.Contains(id) && !(IsBoots(id) && taken.Exists(IsBoots))) options.Add(id);
            return options.ToArray();
        }

        private static bool Fits(List<int> full, int id)
        {
            if (id <= 0 || full.Count >= FullItems || full.Contains(id)) return false;
            return !IsBoots(id) || !full.Exists(IsBoots);
        }

        internal static List<int> MostBuilt(List<ProMatch> games)
        {
            Dictionary<int, int> count = new Dictionary<int, int>();
            Dictionary<int, int> place = new Dictionary<int, int>();
            List<int> seen = new List<int>();
            foreach (ProMatch game in games ?? new List<ProMatch>())
            {
                if (game == null) continue;
                List<int> once = new List<int>();
                foreach (int id in game.Completed ?? new int[0])
                {
                    if (id <= 0 || once.Contains(id)) continue;
                    once.Add(id);
                    if (!count.ContainsKey(id)) { count[id] = 0; place[id] = 0; seen.Add(id); }
                    count[id]++;
                    place[id] += once.Count - 1;
                }
            }
            List<int> ranked = new List<int>(seen);
            ranked.Sort(delegate(int a, int b)
            {
                int byCount = count[b].CompareTo(count[a]);
                if (byCount != 0) return byCount;
                int byPlace = ((double)place[a] / count[a]).CompareTo((double)place[b] / count[b]);
                return byPlace != 0 ? byPlace : seen.IndexOf(a).CompareTo(seen.IndexOf(b));
            });
            return ranked;
        }

        // What they bought in the first 90 seconds, in order.
        internal int[] Starting()
        {
            List<ProItemEvent> bought = new List<ProItemEvent>();
            foreach (ProItemEvent step in Match.Path ?? new ProItemEvent[0]) if (step.Type == 1 && step.At < 90000 && step.Item > 0) bought.Add(step);
            bought.Sort(delegate(ProItemEvent a, ProItemEvent b) { return a.At.CompareTo(b.At); });
            return bought.ConvertAll(step => step.Item).ToArray();
        }
    }

    // Pro builds from probuildstats.com (U.GG's pro site). Its pages load the recent pro games from u.gg's own API, with exact
    // rune, shard, spell and item ids, so she asks that API the same question and nothing is guessed or retyped by a model.
    internal static class ProBuilds
    {
        internal const string Api = "https://u.gg/api";
        internal const int PerPage = 20;
        private const string Fields = "calculatedRole championId completedItems finalBuild itemPath { itemId timestamp type } matchId matchTimestamp normalizedName proInfo { officialName currentTeam } opponentChampionId runes { perk0 perk1 perk2 perk3 perk4 perk5 primaryStyle subStyle } statShards summonerSpells skillOrders totalAssists totalDeaths totalKills win";
        private const string Query = "query ChampionMatchList($championId: Int, $role: String, $isOtp: Boolean, $pageNumber: Int) { getProChampionMatchList(championId: $championId, role: $role, isOtp: $isOtp, pageNumber: $pageNumber) { matchList { " + Fields + " } } }";
        internal static Func<string, string> Download = Fetch;

        // What the last answer looked like when it held no pro games, for the activity log ("" when it was fine).
        [ThreadStatic] private static string shape;
        internal static string Shape { get { return shape ?? ""; } }

        internal static string Slug(string champion) { return League.Plain(champion); }

        private static string Fetch(string body)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Api);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130 Safari/537.36 Capri-Desktop";
            request.Referer = "https://probuildstats.com/";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            byte[] payload = Encoding.UTF8.GetBytes(body);
            try
            {
                request.ContentLength = payload.Length;
                using (Stream outgoing = request.GetRequestStream()) outgoing.Write(payload, 0, payload.Length);
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException error)
            {
                HttpWebResponse failed = error.Response as HttpWebResponse;
                Brain.Log(Api, -1, "pro builds: " + (failed != null ? "HTTP " + (int)failed.StatusCode : error.Status.ToString()));
                return null;
            }
        }

        // The question probuildstats' champion page asks: a champion (0 = any), a lane (null = any) and a page of 20 games.
        internal static string Body(int champion, string role, int page)
        {
            StringBuilder variables = new StringBuilder("{");
            if (champion > 0) variables.Append("\"championId\":").Append(champion).Append(',');
            if (role != null) variables.Append("\"role\":").Append(Voice.Quote(role)).Append(',');
            variables.Append("\"isOtp\":false,\"pageNumber\":").Append(page).Append('}');
            return "{\"operationName\":\"ChampionMatchList\",\"variables\":" + variables + ",\"query\":" + Voice.Quote(Query) + "}";
        }

        // Their lane's games first (two pages), then any lane when that gave fewer than three.
        internal static List<ProMatch> Matches(int champion, string position)
        {
            shape = null;
            List<ProMatch> found = new List<ProMatch>();
            string role = RoleOf(position);
            if (role != null) for (int page = 1; page <= 2; page++) if (!Add(found, Ask(Body(champion, role, page)))) break;
            if (found.Count < 3) Add(found, Ask(Body(champion, null, 1)));
            if (found.Count > 0) shape = null;
            return found;
        }

        // The champions pros played most in a lane lately, most played first.
        internal static List<int> LanePicks(string position)
        {
            shape = null;
            string role = RoleOf(position);
            List<int> order = new List<int>();
            if (role == null) return order;
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int page = 1; page <= 2; page++)
            {
                string text = Download(Body(0, role, page));
                ProMatch[] matches = League.Parse<ProMatch[]>(List(text));
                if (matches == null) { Note(text); break; }
                foreach (ProMatch match in matches)
                {
                    if (match == null || match.Champion <= 0) continue;
                    if (!counts.ContainsKey(match.Champion)) { counts[match.Champion] = 0; order.Add(match.Champion); }
                    counts[match.Champion]++;
                }
                if (matches.Length < PerPage) break;
            }
            List<int> ranked = new List<int>(order);
            ranked.Sort(delegate(int a, int b) { int byCount = counts[b].CompareTo(counts[a]); return byCount != 0 ? byCount : order.IndexOf(a).CompareTo(order.IndexOf(b)); });
            return ranked;
        }

        private static List<ProMatch> Ask(string body)
        {
            string text = Download(body);
            List<ProMatch> found = Parse(text);
            if (found.Count == 0) Note(text);
            return found;
        }

        private static void Note(string text)
        {
            string flat = (text ?? "no answer").Replace("\r", " ").Replace("\n", " ");
            if (flat.IndexOf("\"matchList\":[]", StringComparison.Ordinal) >= 0) flat = "no games";
            shape = flat.Length > 120 ? flat.Substring(0, 120) : flat;
        }

        // Adds the games not already in the list; false when the page had fewer than a full page (so there is no next one).
        private static bool Add(List<ProMatch> found, List<ProMatch> more)
        {
            foreach (ProMatch match in more)
                if (match.Id == 0 || !found.Exists(had => had.Id == match.Id)) found.Add(match);
            return more.Count >= PerPage;
        }

        private static string List(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int query = text.IndexOf("getProChampionMatchList", StringComparison.Ordinal);
            if (query < 0) return null;
            int list = text.IndexOf("\"matchList\":", query, StringComparison.Ordinal);
            if (list < 0) return null;
            int open = text.IndexOf('[', list);
            int close = Closing(text, open);
            return open < 0 || close < 0 ? null : text.Substring(open, close - open + 1);
        }

        // The "matchList": [...] array inside getProChampionMatchList: u.gg's answer, or the page's Apollo state as it used to be.
        internal static List<ProMatch> Parse(string html)
        {
            List<ProMatch> found = new List<ProMatch>();
            string list = List(html);
            if (list == null) return found;
            ProMatch[] matches = League.Parse<ProMatch[]>(list);
            if (matches != null) foreach (ProMatch match in matches) if (match != null && match.Runes != null) found.Add(match);
            return found;
        }

        private static int Closing(string text, int open)
        {
            if (open < 0) return -1;
            int depth = 0;
            bool quoted = false;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '\\') i++;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '[' || c == '{') depth++;
                else if (c == ']' || c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        internal static string RoleOf(string position)
        {
            switch ((position ?? "").ToLowerInvariant())
            {
                case "top": return "top";
                case "jungle": return "jungle";
                case "middle": return "mid";
                case "bottom": return "adc";
                case "utility": return "supp";
                default: return null;
            }
        }

        // The three most played keystone + secondary tree setups among pros in their lane. Each is shown and pushed as the
        // newest pro game with that setup, a win when there is one, so the page, spells and items all come from one real game.
        internal static List<ProBuild> Top(List<ProMatch> matches, string position, int wanted)
        {
            List<ProBuild> builds = new List<ProBuild>();
            if (matches == null || matches.Count == 0) return builds;
            string role = RoleOf(position);
            List<ProMatch> pool = role == null ? matches : matches.FindAll(match => match.Role == role);
            if (pool.Count < wanted) pool = matches;
            Dictionary<string, List<ProMatch>> setups = new Dictionary<string, List<ProMatch>>();
            List<string> order = new List<string>();
            foreach (ProMatch match in pool)
            {
                string key = match.Runes.Perk0 + "/" + match.Runes.Sub;
                if (!setups.ContainsKey(key)) { setups[key] = new List<ProMatch>(); order.Add(key); }
                setups[key].Add(match);
            }
            order.Sort(delegate(string a, string b)
            {
                int byCount = setups[b].Count.CompareTo(setups[a].Count);
                return byCount != 0 ? byCount : Newest(setups[b]).When.CompareTo(Newest(setups[a]).When);
            });
            foreach (string key in order)
            {
                if (builds.Count >= wanted) break;
                List<ProMatch> games = setups[key];
                List<ProMatch> wins = games.FindAll(match => match.Won);
                builds.Add(new ProBuild { Number = builds.Count + 1, Count = games.Count, Games = pool.Count, Match = Newest(wins.Count > 0 ? wins : games), Setup = games, Lane = pool });
            }
            return builds;
        }

        private static ProMatch Newest(List<ProMatch> games)
        {
            ProMatch best = games[0];
            foreach (ProMatch match in games) if (match.When > best.When) best = match;
            return best;
        }

        // "1) Electrocute + Sorcery: Malignance > Lich Bane > Blackfire Torch, Flash + Teleport. 12 of 40 pro games, like Mireu 11/1/8 (win)."
        internal static string Line(ProBuild build, League names)
        {
            ProMatch match = build.Match;
            StringBuilder said = new StringBuilder();
            said.Append(build.Number).Append(") ").Append(names.Perk(match.Runes.Perk0)).Append(" + ").Append(names.Style(match.Runes.Sub)).Append(": ");
            List<string> core = new List<string>();
            foreach (int id in match.Completed ?? new int[0]) if (core.Count < 3) core.Add(names.Item(id));
            said.Append(core.Count > 0 ? string.Join(" > ", core.ToArray()) : "no finished items");
            if (match.Spells != null && match.Spells.Length == 2) said.Append(", ").Append(names.Spell(match.Spells[0])).Append(" + ").Append(names.Spell(match.Spells[1]));
            said.Append(". ").Append(build.Count).Append(" of ").Append(build.Games).Append(" pro games, like ").Append(match.Pro).Append(' ')
                .Append(match.Kills).Append('/').Append(match.Deaths).Append('/').Append(match.Assists).Append(match.Won ? " (win)." : " (loss).");
            return said.ToString();
        }
    }
}
