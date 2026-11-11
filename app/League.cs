using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class ChampSession
    {
        [DataMember(Name = "localPlayerCellId")] public int Me = -1;
        [DataMember(Name = "myTeam")] public ChampSeat[] Mine;
        [DataMember(Name = "theirTeam")] public ChampSeat[] Theirs;
        [DataMember(Name = "bans")] public ChampBans Bans;
        [DataMember(Name = "actions")] public ChampAction[][] Actions;
        [DataMember(Name = "timer")] public ChampTimer Timer;

        internal ChampSeat Seat
        {
            get
            {
                foreach (ChampSeat seat in Mine ?? new ChampSeat[0]) if (seat.Cell == Me) return seat;
                return null;
            }
        }
    }

    [DataContract]
    internal sealed class ChampSeat
    {
        [DataMember(Name = "cellId")] public int Cell = -1;
        [DataMember(Name = "championId")] public int Champion;
        [DataMember(Name = "championPickIntent")] public int Intent;
        [DataMember(Name = "assignedPosition")] public string Position;
        [DataMember(Name = "spell1Id")] public long Spell1;
        [DataMember(Name = "spell2Id")] public long Spell2;
    }

    [DataContract]
    internal sealed class ChampBans
    {
        [DataMember(Name = "myTeamBans")] public int[] Mine;
        [DataMember(Name = "theirTeamBans")] public int[] Theirs;
    }

    [DataContract]
    internal sealed class ChampAction
    {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "actorCellId")] public int Actor = -1;
        [DataMember(Name = "championId")] public int Champion;
        [DataMember(Name = "type")] public string Type;
        [DataMember(Name = "completed")] public bool Completed;
        [DataMember(Name = "isInProgress")] public bool InProgress;
    }

    [DataContract]
    internal sealed class ChampTimer
    {
        [DataMember(Name = "adjustedTimeLeftInPhase")] public long? Left;
        [DataMember(Name = "internalNowInEpochMs")] public long? Now;
        [DataMember(Name = "totalTimeInPhase")] public long? Total;
        [DataMember(Name = "phase")] public string Phase;

        internal const long Turn = 30000;

        // The client's numbers are a snapshot from its last change (League Akari counts them down the same way), so the time
        // left is the snapshot minus what passed since; also the turn's length minus how long she has seen it run. The sooner wins.
        internal static long Remaining(ChampTimer timer, long nowEpoch, long seenFor)
        {
            long total = timer != null && timer.Total.HasValue && timer.Total.Value > 0 ? timer.Total.Value : Turn;
            long left = total - seenFor;
            if (timer != null && timer.Left.HasValue)
            {
                long snapshot = timer.Left.Value;
                if (timer.Now.HasValue && timer.Now.Value > 0) snapshot -= Math.Max(0, nowEpoch - timer.Now.Value);
                left = Math.Min(left, snapshot);
            }
            return left;
        }
    }

    [DataContract]
    internal sealed class ReadyCheck
    {
        [DataMember(Name = "state")] public string State;
        [DataMember(Name = "playerResponse")] public string Response;
    }

    [DataContract]
    internal sealed class ChampName
    {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "name")] public string Name;
    }

    [DataContract]
    internal sealed class PerkStyles
    {
        [DataMember(Name = "styles")] public ChampName[] Styles;
    }

    [DataContract]
    internal sealed class RunePage
    {
        [DataMember(Name = "id")] public long Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "isDeletable")] public bool Deletable;
        [DataMember(Name = "isEditable")] public bool Editable;
        [DataMember(Name = "current")] public bool Current;
        [DataMember(Name = "lastModified")] public long Modified;
    }

    // The League client's own local API (the one Porofessor, Blitz and U.GG's app use), found through its lockfile.
    // She reads the draft, hovers a favourite, accepts the match and locks a pick in only while they are away (their settings),
    // and pushes rune pages, summoner spells and item sets.
    internal sealed class League
    {
        internal static volatile string Brief;
        internal const string ResearchSites = "[\"u.gg\",\"op.gg\",\"lolalytics.com\",\"mobalytics.gg\",\"leagueofgraphs.com\",\"blitz.gg\"]";
        internal const string Tag = "Capri";
        internal static readonly string[] Lockfiles =
        {
            @"C:\Riot Games\League of Legends\lockfile",
            @"D:\Riot Games\League of Legends\lockfile",
            @"C:\Program Files\Riot Games\League of Legends\lockfile"
        };
        internal Func<string, string> Get;
        internal Func<string, string, string, string> Send;
        private Dictionary<int, string> names;
        private Dictionary<int, string> perks, styles, items, spells;
        private string port;
        private string password;
        internal string Phase;
        internal string Picks = "";
        internal string Researched = "";

        internal League()
        {
            Get = delegate(string path) { return Call("GET", path, null); };
            Send = Call;
        }

        internal static bool Open()
        {
            Process[] found = Process.GetProcessesByName("LeagueClientUx");
            foreach (Process process in found) process.Dispose();
            return found.Length > 0;
        }

        // "LeagueClient:pid:port:password:https"
        internal static string[] ReadLockfile(string text)
        {
            string[] parts = (text ?? "").Trim().Split(':');
            if (parts.Length < 5) return null;
            int number;
            if (!int.TryParse(parts[2], out number) || number <= 0 || number > 65535 || parts[3].Length == 0) return null;
            return new[] { parts[2], parts[3] };
        }

        internal bool Connected { get { return port != null; } }

        // The client closed (an account switch restarts it with a new port and password): read the lockfile again next time.
        internal void Disconnect() { port = null; }

        internal bool Connect()
        {
            foreach (string path in Candidates())
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    string text;
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader reader = new StreamReader(stream)) text = reader.ReadToEnd();
                    string[] found = ReadLockfile(text);
                    if (found == null) continue;
                    port = found[0];
                    password = found[1];
                    return true;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            port = null;
            return false;
        }

        private static List<string> Candidates()
        {
            List<string> paths = new List<string>();
            foreach (Process process in Process.GetProcessesByName("LeagueClientUx"))
            {
                try { paths.Add(Path.Combine(Path.GetDirectoryName(process.MainModule.FileName), "lockfile")); }
                catch (Exception) { }
                finally { process.Dispose(); }
            }
            paths.AddRange(Lockfiles);
            return paths;
        }

        // GET answers with the body or null. Writes answer with the body, "" for an empty success, or null on failure.
        private string Call(string method, string path, string body)
        {
            if (port == null) return null;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://127.0.0.1:" + port + path);
            request.Method = method;
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:" + password));
            // The client signs its local API with Riot's own certificate, so only this loopback request skips the usual check.
            request.ServerCertificateValidationCallback = delegate { return true; };
            try
            {
                if (body != null)
                {
                    byte[] payload = Encoding.UTF8.GetBytes(body);
                    request.ContentType = "application/json";
                    request.ContentLength = payload.Length;
                    using (Stream outgoing = request.GetRequestStream()) outgoing.Write(payload, 0, payload.Length);
                }
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException error)
            {
                HttpWebResponse failed = error.Response as HttpWebResponse;
                if (failed != null)
                {
                    if (method != "GET") Brain.Log("league " + method + " " + path, -1, "HTTP " + (int)failed.StatusCode);
                    if (failed.StatusCode == HttpStatusCode.Unauthorized) port = null;
                    return null;
                }
                port = null;
                return null;
            }
        }

        internal string ReadPhase()
        {
            string text = Get("/lol-gameflow/v1/gameflow-phase");
            if (text == null) return null;
            return text.Trim().Trim('"');
        }

        internal ChampSession ReadSession()
        {
            string text = Get("/lol-champ-select/v1/session");
            return text == null ? null : ParseSession(text);
        }

        internal static ChampSession ParseSession(string json)
        {
            return Parse<ChampSession>(json);
        }

        internal static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                using (MemoryStream data = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(data);
            }
            catch (SerializationException) { return null; }
        }

        private static Dictionary<int, string> Named(ChampName[] list)
        {
            Dictionary<int, string> found = new Dictionary<int, string>();
            foreach (ChampName entry in list ?? new ChampName[0]) if (entry.Id > 0 && !string.IsNullOrEmpty(entry.Name)) found[entry.Id] = entry.Name;
            return found;
        }

        internal void LoadNames(string json)
        {
            Dictionary<int, string> found = Named(Parse<ChampName[]>(json ?? "[]"));
            if (found.Count > 0) names = found;
        }

        internal void LoadGameData(string perkJson, string styleJson, string itemJson, string spellJson)
        {
            perks = Named(Parse<ChampName[]>(perkJson));
            PerkStyles found = Parse<PerkStyles>(styleJson);
            styles = Named(found == null ? null : found.Styles);
            items = Named(Parse<ChampName[]>(itemJson));
            spells = Named(Parse<ChampName[]>(spellJson));
        }

        private void GameData()
        {
            if (perks != null && perks.Count > 0) return;
            LoadGameData(Get("/lol-game-data/assets/v1/perks.json"), Get("/lol-game-data/assets/v1/perkstyles.json"), Get("/lol-game-data/assets/v1/items.json"), Get("/lol-game-data/assets/v1/summoner-spells.json"));
        }

        internal string Name(int id)
        {
            if (id <= 0) return null;
            if (names == null) LoadNames(Get("/lol-game-data/assets/v1/champion-summary.json"));
            string name;
            return names != null && names.TryGetValue(id, out name) ? name : "champion " + id;
        }

        internal List<string> Champions()
        {
            if (names == null) LoadNames(Get("/lol-game-data/assets/v1/champion-summary.json"));
            return names == null ? new List<string>() : new List<string>(names.Values);
        }

        internal static string Plain(string name) { return Regex.Replace((name ?? "").ToLowerInvariant(), "[^a-z0-9]", ""); }

        internal int IdOf(string champion)
        {
            if (names == null) LoadNames(Get("/lol-game-data/assets/v1/champion-summary.json"));
            if (names == null) return 0;
            string wanted = Plain(champion);
            if (wanted.Length == 0) return 0;
            foreach (KeyValuePair<int, string> entry in names) if (Plain(entry.Value) == wanted) return entry.Key;
            foreach (KeyValuePair<int, string> entry in names) if (Plain(entry.Value).StartsWith(wanted, StringComparison.Ordinal)) return entry.Key;
            return 0;
        }

        private static string Look(Dictionary<int, string> map, long id)
        {
            string name;
            return map != null && map.TryGetValue((int)id, out name) ? name : "#" + id;
        }

        internal string Perk(long id) { GameData(); return Look(perks, id); }
        internal string Style(long id) { GameData(); return Look(styles, id); }
        internal string Item(long id) { GameData(); return Look(items, id); }
        internal string Spell(long id) { GameData(); return Look(spells, id); }

        internal static string Role(string position)
        {
            switch ((position ?? "").ToLowerInvariant())
            {
                case "top": return "top";
                case "jungle": return "jungle";
                case "middle": return "mid";
                case "bottom": return "ADC (bot)";
                case "utility": return "support";
                default: return null;
            }
        }

        // One line about the draft, e.g. "You're mid on Ahri. Your team: Garen (top). Enemy so far: Zed, Jinx. Bans: Yasuo."
        internal string Describe(ChampSession session)
        {
            if (session == null) return "";
            ChampSeat me = session.Seat;
            List<string> team = new List<string>(), enemy = new List<string>(), bans = new List<string>();
            foreach (ChampSeat seat in session.Mine ?? new ChampSeat[0])
            {
                if (seat == me) continue;
                string name = Name(seat.Champion > 0 ? seat.Champion : seat.Intent);
                if (name != null) team.Add(name + (Role(seat.Position) != null ? " (" + Role(seat.Position) + ")" : ""));
            }
            foreach (ChampSeat seat in session.Theirs ?? new ChampSeat[0])
            {
                string name = Name(seat.Champion);
                if (name != null) enemy.Add(name);
            }
            foreach (int id in Banned(session))
            {
                string name = Name(id);
                if (name != null && !bans.Contains(name)) bans.Add(name);
            }
            StringBuilder said = new StringBuilder();
            string role = me == null ? null : Role(me.Position);
            string mine = me == null ? null : Name(me.Champion > 0 ? me.Champion : me.Intent);
            said.Append("You're ").Append(role ?? "in a blind or flex role");
            said.Append(mine != null ? " on " + mine + "." : ", nothing picked yet.");
            if (team.Count > 0) said.Append(" Your team: ").Append(string.Join(", ", team.ToArray())).Append('.');
            said.Append(enemy.Count > 0 ? " Enemy so far: " + string.Join(", ", enemy.ToArray()) + "." : " No enemy picks yet.");
            if (bans.Count > 0) said.Append(" Bans: ").Append(string.Join(", ", bans.ToArray())).Append('.');
            return said.ToString();
        }

        internal string Locked(ChampSession session)
        {
            if (session == null || session.Actions == null) return "";
            List<string> done = new List<string>();
            foreach (ChampAction[] turn in session.Actions)
                foreach (ChampAction action in turn ?? new ChampAction[0])
                {
                    if (action == null || action.Type != "pick" || !action.Completed || action.Champion <= 0) continue;
                    string name = Name(action.Champion);
                    if (name != null && !done.Contains(name)) done.Add(name);
                }
            if (done.Count == 0) return " Nobody has locked in yet; every pick above is only a hover.";
            return " Locked in for good (nobody can take these any more): " + string.Join(", ", done.ToArray()) + ". Any other pick above is only a hover.";
        }

        internal static List<int> Banned(ChampSession session)
        {
            List<int> found = new List<int>();
            if (session == null) return found;
            if (session.Bans != null)
                foreach (int[] side in new[] { session.Bans.Mine, session.Bans.Theirs })
                    foreach (int id in side ?? new int[0]) if (id > 0 && !found.Contains(id)) found.Add(id);
            foreach (ChampAction[] turn in session.Actions ?? new ChampAction[0][])
                foreach (ChampAction action in turn ?? new ChampAction[0])
                    if (action.Type == "ban" && action.Completed && action.Champion > 0 && !found.Contains(action.Champion)) found.Add(action.Champion);
            return found;
        }

        internal static int EnemyCount(ChampSession session)
        {
            int count = 0;
            if (session != null) foreach (ChampSeat seat in session.Theirs ?? new ChampSeat[0]) if (seat.Champion > 0) count++;
            return count;
        }

        internal static ChampAction MyPick(ChampSession session)
        {
            if (session == null) return null;
            foreach (ChampAction[] turn in session.Actions ?? new ChampAction[0][])
                foreach (ChampAction action in turn ?? new ChampAction[0])
                    if (action.Type == "pick" && action.Actor == session.Me) return action;
            return null;
        }

        // The champion she hovers: their first favourite for this lane that they own and nobody has banned, picked or claimed.
        // Nothing when they've already hovered or locked something themselves.
        internal int Favourite(ChampSession session, string[] favourites, List<int> pickable)
        {
            ChampAction pick = MyPick(session);
            ChampSeat me = session == null ? null : session.Seat;
            if (pick == null || pick.Completed || pick.Champion > 0 || me == null || me.Champion > 0 || me.Intent > 0) return 0;
            return FirstFree(Taken(session, me), Ids(favourites), pickable);
        }

        // Banned, picked by anyone else, or claimed by a teammate's hover.
        internal static List<int> Taken(ChampSession session, ChampSeat me)
        {
            List<int> taken = Banned(session);
            foreach (ChampSeat[] side in new[] { session.Mine, session.Theirs })
                foreach (ChampSeat seat in side ?? new ChampSeat[0])
                {
                    if (seat == me) continue;
                    if (seat.Champion > 0) taken.Add(seat.Champion);
                    if (seat.Intent > 0) taken.Add(seat.Intent);
                }
            return taken;
        }

        private List<int> Ids(string[] favourites)
        {
            List<int> ids = new List<int>();
            foreach (string favourite in favourites ?? new string[0]) ids.Add(IdOf(favourite));
            return ids;
        }

        private static int FirstFree(List<int> taken, List<int> wanted, List<int> pickable)
        {
            foreach (int id in wanted)
                if (id > 0 && !taken.Contains(id) && (pickable == null || pickable.Contains(id))) return id;
            return 0;
        }

        // What she locks for them: the champ on their pick if it is still free, else down their favourites, else the pros' picks for the lane.
        internal int LockChoice(ChampSession session, string[] favourites, List<int> pickable, Func<List<int>> pros)
        {
            ChampAction pick = MyPick(session);
            ChampSeat me = session == null ? null : session.Seat;
            if (pick == null || pick.Completed || me == null) return 0;
            List<int> taken = Taken(session, me);
            int own = FirstFree(taken, new List<int> { pick.Champion, me.Intent }, pickable);
            if (own > 0) return own;
            int favourite = FirstFree(taken, Ids(favourites), pickable);
            if (favourite > 0 || pros == null) return favourite;
            int pro = FirstFree(taken, pros() ?? new List<int>(), pickable);
            if (pro > 0 || pickable == null) return pro;
            return FirstFree(taken, pickable, pickable);
        }

        // Banned, locked by someone else, or not his to pick: a hover on it can never be locked.
        internal static bool Gone(ChampSession session, ChampSeat me, int champion, List<int> pickable)
        {
            if (champion <= 0) return true;
            if (Banned(session).Contains(champion)) return true;
            if (pickable != null && !pickable.Contains(champion)) return true;
            foreach (ChampSeat[] side in new[] { session.Mine, session.Theirs })
                foreach (ChampSeat seat in side ?? new ChampSeat[0])
                    if (seat != me && seat.Champion == champion) return true;
            return false;
        }

        // A new hover when his is gone: down his favourites, else the pros' picks for the lane (never one a teammate claimed).
        internal int Rehover(ChampSession session, string[] favourites, List<int> pickable, List<int> pros)
        {
            ChampSeat me = session == null ? null : session.Seat;
            if (me == null) return 0;
            List<int> taken = Taken(session, me);
            int favourite = FirstFree(taken, Ids(favourites), pickable);
            return favourite > 0 || pros == null ? favourite : FirstFree(taken, pros, pickable);
        }

        internal ReadyCheck ReadReady() { return Parse<ReadyCheck>(Get("/lol-matchmaking/v1/ready-check")); }

        internal bool Accept() { return Send("POST", "/lol-matchmaking/v1/ready-check/accept", "") != null; }

        internal bool Lock(int action, int champion)
        {
            return Send("PATCH", "/lol-champ-select/v1/session/actions/" + action, "{\"championId\":" + champion + ",\"completed\":true}") != null;
        }

        internal List<int> Pickable()
        {
            int[] found = Parse<int[]>(Get("/lol-champ-select/v1/pickable-champion-ids"));
            return found == null ? null : new List<int>(found);
        }

        internal bool Hover(int action, int champion)
        {
            return Send("PATCH", "/lol-champ-select/v1/session/actions/" + action, "{\"championId\":" + champion + "}") != null;
        }

        internal static string RuneBody(string name, ProBuild build)
        {
            ProRunes runes = build.Match.Runes;
            int[] shards = build.Match.Shards != null && build.Match.Shards.Length == 3 ? build.Match.Shards : new[] { 5008, 5008, 5011 };
            long[] chosen = { runes.Perk0, runes.Perk1, runes.Perk2, runes.Perk3, runes.Perk4, runes.Perk5, shards[0], shards[1], shards[2] };
            string[] parts = new string[chosen.Length];
            for (int i = 0; i < chosen.Length; i++) parts[i] = chosen[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "{\"name\":" + Voice.Quote(name) + ",\"primaryStyleId\":" + runes.Primary + ",\"subStyleId\":" + runes.Sub + ",\"selectedPerkIds\":[" + string.Join(",", parts) + "],\"current\":true}";
        }

        // Her pages and item sets: "Capri", or "Capri: ..." from before they were all just called Capri.
        internal static bool Hers(string name) { return name != null && (name == Tag || name.StartsWith(Tag + ":", StringComparison.Ordinal)); }

        // Her page is called "Capri" and replaces her previous one, so pressing Push again never piles up pages.
        // When every slot is taken, it overwrites the page they have selected, like the build apps do.
        internal string PushRunes(ProBuild build, string champion)
        {
            if (build.Match.Runes == null) return "That build has no runes.";
            RunePage[] pages = Parse<RunePage[]>(Get("/lol-perks/v1/pages"));
            if (pages == null) return "Couldn't read your rune pages.";
            foreach (RunePage page in pages)
                if (page.Deletable && Hers(page.Name)) Send("DELETE", "/lol-perks/v1/pages/" + page.Id, null);
            if (Send("POST", "/lol-perks/v1/pages", RuneBody(Tag, build)) != null) return null;
            RunePage replace = Array.Find(pages, page => page.Current && page.Editable) ?? Array.Find(pages, page => page.Editable && page.Deletable);
            if (replace == null) return "Couldn't fit a rune page: none of yours can be edited.";
            if (Send("DELETE", "/lol-perks/v1/pages/" + replace.Id, null) != null && Send("POST", "/lol-perks/v1/pages", RuneBody(Tag, build)) != null) return null;
            return "Couldn't save the rune page.";
        }

        // Flash stays on the key they keep it on.
        internal static long[] SpellOrder(long[] wanted, long first, long second)
        {
            if (wanted == null || wanted.Length < 2) return null;
            long a = wanted[0], b = wanted[1];
            if ((a == 4 && second == 4 && first != 4) || (b == 4 && first == 4)) { long swap = a; a = b; b = swap; }
            return new[] { a, b };
        }

        internal string PushSpells(ProBuild build, ChampSession session)
        {
            ChampSeat me = session == null ? null : session.Seat;
            long[] order = SpellOrder(build.Match.Spells, me == null ? 0 : me.Spell1, me == null ? 0 : me.Spell2);
            if (order == null) return null;
            return Send("PATCH", "/lol-champ-select/v1/session/my-selection", "{\"spell1Id\":" + order[0] + ",\"spell2Id\":" + order[1] + "}") == null ? "Couldn't set your summoner spells." : null;
        }

        // Their item sets go back untouched; only her own old set for this champion is swapped for the new one.
        internal static string MergeItemSet(string current, ProBuild build, int champion, string title)
        {
            JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            Dictionary<string, object> root = json.DeserializeObject(current) as Dictionary<string, object>;
            if (root == null) return null;
            List<object> sets = new List<object>();
            object had;
            if (root.TryGetValue("itemSets", out had) && had is object[])
                foreach (object set in (object[])had)
                {
                    Dictionary<string, object> fields = set as Dictionary<string, object>;
                    object name;
                    if (fields != null && fields.TryGetValue("title", out name) && Hers(name as string) && Champions(fields).Contains(champion)) continue;
                    sets.Add(set);
                }
            Dictionary<string, object> mine = new Dictionary<string, object>();
            mine["title"] = title;
            mine["associatedChampions"] = new object[] { champion };
            mine["associatedMaps"] = new object[] { 11 };
            mine["map"] = "any";
            mine["mode"] = "any";
            mine["preferredItemSlots"] = new object[0];
            mine["sortrank"] = 0;
            mine["startedFrom"] = "blank";
            mine["type"] = "custom";
            mine["uid"] = Guid.NewGuid().ToString();
            List<object> blocks = new List<object>();
            Block(blocks, "Start (" + build.Match.Pro + ")", build.Starting());
            Block(blocks, "Full build: first items from " + build.Match.Pro + ", then what pros build most", build.Full());
            Block(blocks, "More options, most built by pros first", build.Options());
            mine["blocks"] = blocks.ToArray();
            sets.Add(mine);
            root["itemSets"] = sets.ToArray();
            root["timestamp"] = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            return json.Serialize(root);
        }

        private static List<int> Champions(Dictionary<string, object> set)
        {
            List<int> found = new List<int>();
            object list;
            if (set.TryGetValue("associatedChampions", out list) && list is object[])
                foreach (object id in (object[])list) if (id is int) found.Add((int)id);
            return found;
        }

        private static void Block(List<object> blocks, string type, int[] ids)
        {
            if (ids == null) return;
            List<object> items = new List<object>();
            Dictionary<int, int> counts = new Dictionary<int, int>();
            List<int> order = new List<int>();
            foreach (int id in ids)
            {
                if (id <= 0) continue;
                if (!counts.ContainsKey(id)) { counts[id] = 0; order.Add(id); }
                counts[id]++;
            }
            foreach (int id in order)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                item["count"] = counts[id];
                items.Add(item);
            }
            if (items.Count == 0) return;
            Dictionary<string, object> block = new Dictionary<string, object>();
            block["type"] = type;
            block["items"] = items.ToArray();
            blocks.Add(block);
        }

        internal string PushItems(ProBuild build, int champion, string championName)
        {
            string me = Get("/lol-summoner/v1/current-summoner");
            Match id = Regex.Match(me ?? "", "\"summonerId\"\\s*:\\s*([0-9]+)");
            if (!id.Success) return "Couldn't find your summoner.";
            string path = "/lol-item-sets/v1/item-sets/" + id.Groups[1].Value + "/sets";
            string current = Get(path);
            if (current == null) return "Couldn't read your item sets.";
            string merged = MergeItemSet(current, build, champion, Tag);
            if (merged == null) return "Couldn't read your item sets.";
            return Send("PUT", path, merged) == null ? "Couldn't save the item set." : null;
        }

        // Runes, then spells, then the item set. Answers with what worked, in her words.
        internal string Push(ProBuild build, ChampSession session)
        {
            ChampSeat me = session == null ? null : session.Seat;
            int champion = me == null ? 0 : me.Champion > 0 ? me.Champion : me.Intent;
            if (champion <= 0) champion = build.Match.Champion;
            string name = Name(champion) ?? "champ";
            List<string> problems = new List<string>();
            string runes = PushRunes(build, name);
            if (runes != null) problems.Add(runes);
            bool withSpells = ForkSettings.Current.PushSpells;
            string spells = withSpells ? PushSpells(build, session) : null;
            if (spells != null) problems.Add(spells);
            string sets = PushItems(build, champion, name);
            if (sets != null) problems.Add(sets);
            if (problems.Count == 0) return "Build " + build.Number + " is in: " + build.Match.Pro + "'s " + (withSpells ? "runes, spells and items" : "runes and items") + ". Go cook.";
            return string.Join(" ", problems.ToArray());
        }

        internal static string ResearchAsk(string picks)
        {
            return "League of Legends champ select, current patch. " + picks + "\n" +
                "Look up current-patch counter and matchup data, then reply with only the names of the 3 best picks for my role into their team, comma separated. " +
                "Only champions nobody has picked or banned. Three names, nothing else: no reasons, no lists, no links.";
        }

        // Asks Claude with web search, limited to the big stats sites, so the advice is this patch's and not training memory.
        internal static string Research(Cloud remote, string ask, bool search)
        {
            string tools = search ? "[{\"type\":\"web_search_20250305\",\"name\":\"web_search\",\"max_uses\":2,\"allowed_domains\":" + ResearchSites + "}]" : null;
            int status;
            string answer = Quick.Ask(remote, "You are a sharp League of Legends coach. Be specific and current." + Context.Core(), Voice.Quote(ask), tools, 1024, "off", out status);
            if (answer == null && search && status == 400) return Research(remote, ask, false);
            return answer;
        }

        internal static string Answer(string reply)
        {
            return Answer(reply, false);
        }

        internal static string Answer(string reply, bool lines)
        {
            ClaudeReply parsed;
            try { parsed = Brain.Parsed(reply); }
            catch (Exception) { return null; }
            StringBuilder said = new StringBuilder();
            if (parsed.Content != null) foreach (ClaudeBlock block in parsed.Content) if (block.Type == "text" && block.Text != null) said.Append(block.Text);
            string answer = lines ? Regex.Replace(Regex.Replace(said.ToString(), @"[^\S\n]+", " "), @" ?\n\s*", "\n").Trim() : Regex.Replace(said.ToString(), @"\s+", " ").Trim();
            return answer.Length == 0 ? null : answer;
        }
    }
}
