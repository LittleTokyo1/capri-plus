using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CapriDesktop
{
    // Capri+ smoke checks for the review fixes, pro builds, the draft and her game spot.
    internal sealed partial class Companion
    {
        internal const string SmokePerks = "[{\"id\":8112,\"name\":\"Electrocute\"},{\"id\":8229,\"name\":\"Arcane Comet\"},{\"id\":8010,\"name\":\"Conqueror\"}]";
        internal const string SmokeStyles = "{\"styles\":[{\"id\":8000,\"name\":\"Precision\"},{\"id\":8100,\"name\":\"Domination\"},{\"id\":8200,\"name\":\"Sorcery\"},{\"id\":8300,\"name\":\"Inspiration\"},{\"id\":8400,\"name\":\"Resolve\"}]}";
        internal const string SmokeItems = "[{\"id\":3118,\"name\":\"Malignance\"},{\"id\":3100,\"name\":\"Lich Bane\"},{\"id\":3041,\"name\":\"Mejai's Soulstealer\"},{\"id\":1056,\"name\":\"Doran's Ring\"},{\"id\":2003,\"name\":\"Health Potion\"},{\"id\":3340,\"name\":\"Stealth Ward\"}]";
        internal const string SmokeSpells = "[{\"id\":4,\"name\":\"Flash\"},{\"id\":12,\"name\":\"Teleport\"},{\"id\":14,\"name\":\"Ignite\"}]";
        internal const string SmokeChampions = "[{\"id\":103,\"name\":\"Ahri\"},{\"id\":238,\"name\":\"Zed\"},{\"id\":134,\"name\":\"Syndra\"},{\"id\":7,\"name\":\"LeBlanc\"},{\"id\":61,\"name\":\"Orianna\"},{\"id\":86,\"name\":\"Garen\"}]";

        private static string SmokeMatch(string role, bool won, long perk0, long sub, long when, string pro, string completed, string path)
        {
            return "{\"__typename\":\"ProMatchSummary\",\"calculatedRole\":\"" + role + "\",\"championId\":103,\"completedItems\":[" + completed + "],\"finalBuild\":[3118,3100,0]," +
                "\"itemPath\":[" + path + "],\"normalizedName\":\"" + pro.ToLowerInvariant() + "\",\"proInfo\":{\"__typename\":\"ProInfo\",\"officialName\":\"" + pro + "\",\"currentTeam\":\"T1\"}," +
                "\"opponentChampionId\":238,\"runes\":{\"__typename\":\"ProMatchRunes\",\"perk0\":" + perk0 + ",\"perk1\":8139,\"perk2\":8140,\"perk3\":8106,\"perk4\":8226,\"perk5\":8210,\"primaryStyle\":8100,\"subStyle\":" + sub + "}," +
                "\"statShards\":[5008,5008,5011],\"summonerSpells\":[4,12],\"skillOrders\":\"QWEQQRQ\",\"totalKills\":11,\"totalDeaths\":1,\"totalAssists\":8,\"win\":" + (won ? "true" : "false") + ",\"isWorlds\":\"false\",\"matchTimestamp\":" + when + "}";
        }

        internal static string SmokeHtml()
        {
            string start = "{\"itemId\":1056,\"timestamp\":17000,\"type\":1},{\"itemId\":2003,\"timestamp\":17001,\"type\":1},{\"itemId\":2003,\"timestamp\":17002,\"type\":1},{\"itemId\":3340,\"timestamp\":18000,\"type\":1},{\"itemId\":1052,\"timestamp\":300000,\"type\":1},{\"itemId\":2003,\"timestamp\":40000,\"type\":2}";
            string list = string.Join(",", new[]
            {
                SmokeMatch("mid", true, 8112, 8200, 3000, "Mireu", "3118,3100,3041", start),
                SmokeMatch("mid", false, 8112, 8200, 4000, "Carnage", "3118", ""),
                SmokeMatch("mid", true, 8112, 8400, 2000, "Faker", "3100", ""),
                SmokeMatch("top", true, 8010, 8400, 5000, "Zeus", "3118", ""),
                SmokeMatch("mid", true, 8229, 8300, 1000, "Te]st \\\" Chovy", "3041", "")
            });
            return "<html><script>window.__REACTN_PRELOADED_STATE__ = {\"x\":[1,2]};</script><script>window.__APOLLO_STATE__ = {\"ROOT_QUERY\":{\"getProChampionMatchList({\\\"championId\\\":103,\\\"isOtp\\\":false})\":{\"__typename\":\"ProChampionMatchPage\",\"matchList\":[" + list + "]}}};</script></html>";
        }

        internal static string SmokeApi()
        {
            string real = "{\"calculatedRole\":\"top\",\"championId\":238,\"completedItems\":[3158,6699,2520,3036],\"finalBuild\":[3158,6699,2520,3036,1120,3133,3363],\"itemPath\":[{\"itemId\":2003,\"timestamp\":15627,\"type\":1},{\"itemId\":3340,\"timestamp\":14926,\"type\":1},{\"itemId\":1120,\"timestamp\":14592,\"type\":1}]," +
                "\"matchTimestamp\":1793391216173,\"normalizedName\":\"twtvbzeuw\",\"opponentChampionId\":23,\"proInfo\":{\"currentTeam\":\"One Trick Pony\",\"officialName\":\"TwTv BZ EUW\"},\"runes\":{\"perk0\":8369,\"perk1\":8321,\"perk2\":8313,\"perk3\":8316,\"perk4\":8473,\"perk5\":8451,\"primaryStyle\":8300,\"subStyle\":8400}," +
                "\"skillOrders\":\"QEWQQRQEQEEREWWWR\",\"statShards\":[5008,5008,5001],\"summonerSpells\":[4,12],\"totalAssists\":5,\"totalDeaths\":6,\"totalKills\":4,\"win\":true}";
            return "{\"data\":{\"getProChampionMatchList\":{\"matchList\":[" + real + "," + SmokeMatch("top", false, 8010, 8400, 5000, "Zeus", "3118", "") + "]}}}";
        }

        private static League SmokeLeagueClient(List<string> sent, Dictionary<string, string> replies)
        {
            League fake = new League();
            fake.LoadNames(SmokeChampions);
            fake.LoadGameData(SmokePerks, SmokeStyles, SmokeItems, SmokeSpells);
            fake.Get = delegate(string path) { string answer; return replies.TryGetValue(path, out answer) ? answer : null; };
            fake.Send = delegate(string method, string path, string body) { sent.Add(method + " " + path + (body == null ? "" : " " + body)); return ""; };
            return fake;
        }

        private static void SmokeGuards(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            List<string> watched = Lockout.Watched(new[] { "League of Legends.exe", " Valorant ", "deadlock", "" });
            if (string.Join("|", watched.ToArray()) != string.Join("|", ForkSettings.StandardGames) + "|Valorant") throw new Exception("Game names were read as: " + string.Join("|", watched.ToArray()));
            if (Lockout.Watched(new string[0]).Count != ForkSettings.StandardGames.Length || Lockout.Watched(null).Count != ForkSettings.StandardGames.Length) throw new Exception("An empty game list dropped the standard games.");
            foreach (string anticheat in new[] { "League of Legends", "deadlock", "VALORANT-Win64-Shipping", "cs2", "FortniteClient-Win64-Shipping", "r5apex", "RainbowSix", "EscapeFromTarkov", "Overwatch", "cod" })
                if (!watched.Contains(anticheat)) throw new Exception(anticheat + " isn't a standard locked game.");
            if (watched.Contains("Celeste") || watched.Contains("Stardew Valley")) throw new Exception("A single-player game got locked.");
            Lockout.Running = delegate(string name) { return name == "deadlock"; };
            Lockout.Recheck();
            try
            {
                if (Hands.Do("shortcut", new ToolArguments { Name = "copy" }) != Lockout.Refusal) throw new Exception("Her hands pressed a shortcut mid-game.");
                foreach (string drawn in new[] { "draw", "mini_game", "gaming_mode" }) if (Lockout.Refuse(drawn) != Lockout.Refusal) throw new Exception("Upstream's " + drawn + " was not locked mid-game.");
                if (Hands.Do("open_app", new ToolArguments { Name = "capri-smoke-no-such-app" }) == Lockout.Refusal || Hands.Do("app_window", new ToolArguments { Name = "capri-smoke-no-such-window", Action = "minimize" }) == Lockout.Refusal || Screens.Move(IntPtr.Zero, "capri-smoke", null, false) == Lockout.Refusal) throw new Exception("Opening an app or moving a window was refused mid-game.");
                if (Hands.Do("use_computer", new ToolArguments { Task = "click something" }) != Lockout.Refusal) throw new Exception("A job started mid-game.");
                if (Computer.Do("click", new ToolArguments { Target = "1" }, null) != Lockout.Refusal) throw new Exception("A job step clicked mid-game.");
                if (Computer.Member("left_click", new ToolArguments { Coordinate = new double[] { 10, 10 } }, null) != Lockout.Refusal) throw new Exception("Her cursor clicked mid-game.");
                if (!Computer.Escape()) throw new Exception("A running job wasn't stopped when a game started.");
            }
            finally
            {
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
            }
            string described = ActivityLog.Describe(new ToolArguments { Text = "hunter2", Target = "4" });
            if (described.Contains("hunter2") || !described.Contains("7 characters of text")) throw new Exception("Typed text reached the activity log: " + described);
            checks.Add("PASS lockout guards: game names with .exe and an emptied list still lock League and Deadlock, her shortcuts, job steps and cursor refuse mid-game while opening apps and moving windows still work, jobs stop when a game starts, and typed text never reaches the log");
        }

        private static void SmokeProBuilds(List<string> checks)
        {
            List<ProMatch> matches = ProBuilds.Parse(SmokeHtml());
            if (matches.Count != 5 || matches[4].Pro != "Te]st \" Chovy" || matches[0].Runes.Perk0 != 8112 || matches[0].Shards[2] != 5011 || matches[0].Spells[1] != 12) throw new Exception("Pro matches were read wrong: " + matches.Count);
            if (ProBuilds.Parse("<html>nothing</html>").Count != 0 || ProBuilds.Parse(null).Count != 0) throw new Exception("A page without pro games gave builds.");
            List<ProMatch> answered = ProBuilds.Parse(SmokeApi());
            if (answered.Count != 2 || answered[0].Pro != "TwTv BZ EUW" || answered[0].Runes.Perk0 != 8369 || answered[0].Runes.Sub != 8400 || answered[0].Shards[2] != 5001 || answered[0].Spells[1] != 12 || answered[0].Completed[0] != 3158 || !answered[0].Won)
                throw new Exception("The u.gg answer was read wrong: " + answered.Count);
            if (ProBuilds.Parse("{\"data\":{\"getProChampionMatchList\":{\"matchList\":[]}}}").Count != 0) throw new Exception("An empty u.gg answer gave builds.");
            string body = ProBuilds.Body(238, "mid", 2);
            if (!body.Contains("\"championId\":238") || !body.Contains("\"role\":\"mid\"") || !body.Contains("\"pageNumber\":2") || !body.Contains("\"isOtp\":false") || !body.Contains("getProChampionMatchList(") || !body.Contains("perk0") || !body.Contains("summonerSpells"))
                throw new Exception("The u.gg question was built as: " + body);
            string lane = ProBuilds.Body(0, "supp", 1);
            if (lane.Contains("championId\":") || !lane.Contains("\"role\":\"supp\"")) throw new Exception("The lane question was built as: " + lane);
            List<string> bodies = new List<string>();
            Func<string, string> keptDownload = ProBuilds.Download;
            try
            {
                ProBuilds.Download = delegate(string sent) { bodies.Add(sent); return sent.Contains("\"pageNumber\":1") && sent.Contains("\"role\":\"top\"") ? SmokeApi() : "{\"data\":{\"getProChampionMatchList\":{\"matchList\":[]}}}"; };
                List<ProMatch> fetched = ProBuilds.Matches(238, "top");
                if (fetched.Count != 2 || bodies.Count != 2 || !bodies[0].Contains("\"role\":\"top\"") || bodies[1].Contains("\"role\"")) throw new Exception("The u.gg pages were asked as: " + string.Join(" || ", bodies.ToArray()));
                if (ProBuilds.Shape.Length != 0) throw new Exception("A good answer left a shape note: " + ProBuilds.Shape);
                ProBuilds.Download = delegate { return "<html>changed again</html>"; };
                if (ProBuilds.Matches(238, "top").Count != 0 || !ProBuilds.Shape.Contains("changed again")) throw new Exception("A changed answer left no shape note: " + ProBuilds.Shape);
                ProBuilds.Download = delegate { return "{\"data\":{\"getProChampionMatchList\":{\"matchList\":[{\"championId\":8,\"calculatedRole\":\"mid\"},{\"championId\":134,\"calculatedRole\":\"mid\"},{\"championId\":8,\"calculatedRole\":\"mid\"},{\"championId\":0,\"calculatedRole\":\"mid\"}]}}}"; };
                List<int> picks = ProBuilds.LanePicks("middle");
                if (picks.Count != 2 || picks[0] != 8 || picks[1] != 134) throw new Exception("The pros' lane picks came out as: " + string.Join(",", picks.ConvertAll(id => id.ToString()).ToArray()));
            }
            finally { ProBuilds.Download = keptDownload; }
            if (ProBuilds.Slug("Nunu & Willump") != "nunuwillump" || ProBuilds.Slug("Kai'Sa") != "kaisa" || ProBuilds.Slug("Dr. Mundo") != "drmundo") throw new Exception("Champion page names are wrong.");
            List<ProBuild> top = ProBuilds.Top(matches, "middle", 3);
            if (top.Count != 3 || top[0].Match.Pro != "Mireu" || top[0].Count != 2 || top[0].Games != 4 || top[1].Match.Pro != "Faker" || top[2].Match.Runes.Perk0 != 8229) throw new Exception("Top builds were picked wrong.");
            if (string.Join(",", Array.ConvertAll(top[0].Starting(), id => id.ToString())) != "1056,2003,2003,3340") throw new Exception("Starting items were read wrong.");
            League names = SmokeLeagueClient(new List<string>(), new Dictionary<string, string>());
            string line = ProBuilds.Line(top[0], names);
            if (line != "1) Electrocute + Sorcery: Malignance > Lich Bane > Mejai's Soulstealer, Flash + Teleport. 2 of 4 pro games, like Mireu 11/1/8 (win).") throw new Exception("A build was described as: " + line);
            string page = League.RuneBody("Capri: Ahri 1", top[0]);
            if (page != "{\"name\":\"Capri: Ahri 1\",\"primaryStyleId\":8100,\"subStyleId\":8200,\"selectedPerkIds\":[8112,8139,8140,8106,8226,8210,5008,5008,5011],\"current\":true}") throw new Exception("The rune page was built as: " + page);
            long[] flashOnF = League.SpellOrder(new long[] { 4, 12 }, 14, 4), flashOnD = League.SpellOrder(new long[] { 12, 4 }, 4, 14), none = League.SpellOrder(new long[] { 4, 12 }, 14, 12);
            if (flashOnF[0] != 12 || flashOnF[1] != 4 || flashOnD[0] != 4 || flashOnD[1] != 12 || none[0] != 4) throw new Exception("Flash moved off their key.");
            string mine = "{\"accountId\":1,\"itemSets\":[{\"title\":\"My Ahri\",\"associatedChampions\":[103],\"blocks\":[],\"uid\":\"u1\",\"preferredItemSlots\":[{\"id\":\"3020\",\"preferredItemSlot\":1}]},{\"title\":\"Capri: Ahri (Old)\",\"associatedChampions\":[103],\"uid\":\"u2\"},{\"title\":\"Capri: Zed (X)\",\"associatedChampions\":[238],\"uid\":\"u3\"}],\"timestamp\":1}";
            string merged = League.MergeItemSet(mine, top[0], 103, "Capri: Ahri (Mireu)");
            Dictionary<string, object> root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(merged);
            object[] sets = (object[])root["itemSets"];
            List<string> titles = new List<string>();
            foreach (object set in sets) titles.Add((string)((Dictionary<string, object>)set)["title"]);
            if (string.Join("|", titles.ToArray()) != "My Ahri|Capri: Zed (X)|Capri: Ahri (Mireu)") throw new Exception("Item sets merged as: " + string.Join("|", titles.ToArray()));
            if (!merged.Contains("\"preferredItemSlot\":1") || !merged.Contains("{\"id\":\"2003\",\"count\":2}") || !merged.Contains("Full build: first items from Mireu, then what pros build most")) throw new Exception("Their own item set or her blocks came out wrong: " + merged);
            checks.Add("PASS pro builds: pro games come from u.gg's own API (his lane first, two pages, then any lane; an answer of a new shape is logged), the pros' most played champions per lane are counted, the old page data still reads (quotes and brackets in names included), the 3 most played rune setups for their lane win, each pushed as one real pro game, and their own item sets survive while her old one is replaced");
        }

        private void SmokeDraft(List<string> checks)
        {
            string session = "{\"localPlayerCellId\":2,\"myTeam\":[{\"cellId\":1,\"championId\":0,\"championPickIntent\":7,\"assignedPosition\":\"top\"},{\"cellId\":2,\"championId\":0,\"championPickIntent\":0,\"assignedPosition\":\"middle\",\"spell1Id\":4,\"spell2Id\":14}]," +
                "\"theirTeam\":[{\"cellId\":5,\"championId\":238}],\"bans\":{\"myTeamBans\":[],\"theirTeamBans\":[]}," +
                "\"actions\":[[{\"id\":1,\"actorCellId\":2,\"championId\":0,\"type\":\"ban\",\"completed\":true,\"isInProgress\":false},{\"id\":2,\"actorCellId\":5,\"championId\":103,\"type\":\"ban\",\"completed\":true,\"isInProgress\":false}],[{\"id\":7,\"actorCellId\":2,\"championId\":0,\"type\":\"pick\",\"completed\":false,\"isInProgress\":false}]]}";
            List<string> sent = new List<string>();
            Dictionary<string, string> replies = new Dictionary<string, string>();
            replies["/lol-champ-select/v1/session"] = session;
            replies["/lol-champ-select/v1/pickable-champion-ids"] = "[61,103,7]";
            replies["/lol-perks/v1/pages"] = "[{\"id\":11,\"name\":\"Capri: Ahri 1\",\"isDeletable\":true},{\"id\":12,\"name\":\"Mine\",\"isDeletable\":true}]";
            replies["/lol-summoner/v1/current-summoner"] = "{\"summonerId\":555,\"displayName\":\"x\"}";
            replies["/lol-item-sets/v1/item-sets/555/sets"] = "{\"accountId\":1,\"itemSets\":[],\"timestamp\":1}";
            League fake = SmokeLeagueClient(sent, replies);
            ChampSession draft = fake.ReadSession();
            if (League.MyPick(draft) == null || League.MyPick(draft).Id != 7 || !League.Banned(draft).Contains(103)) throw new Exception("The draft's actions were read wrong.");
            int favourite = fake.Favourite(draft, new[] { "Ahri", "leblanc", "Syndra", "Orianna" }, fake.Pickable());
            if (favourite != 61) throw new Exception("She'd hover champion " + favourite + " instead of Orianna (Ahri banned, LeBlanc claimed, Syndra not owned).");
            draft.Seat.Intent = 134;
            if (fake.Favourite(draft, new[] { "Orianna" }, null) != 0) throw new Exception("She overrode their own hover.");
            draft.Seat.Intent = 0;
            if (!fake.Hover(7, 61) || sent[0] != "PATCH /lol-champ-select/v1/session/actions/7 {\"championId\":61}") throw new Exception("The hover went out as: " + (sent.Count > 0 ? sent[0] : "nothing"));
            foreach (string line in sent) if (line.Contains("/complete") || line.Contains("\"completed\"")) throw new Exception("She tried to lock in.");
            sent.Clear();
            List<ProBuild> top = ProBuilds.Top(ProBuilds.Parse(SmokeHtml()), "middle", 3);
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            string pushed = fake.Push(top[0], draft);
            if (!pushed.StartsWith("Build 1 is in: Mireu", StringComparison.Ordinal) || !pushed.Contains("runes and items") || sent.Count != 3 || sent[0] != "DELETE /lol-perks/v1/pages/11" || !sent[1].StartsWith("POST /lol-perks/v1/pages {\"name\":\"Capri\"", StringComparison.Ordinal)
                || !sent[2].StartsWith("PUT /lol-item-sets/v1/item-sets/555/sets ", StringComparison.Ordinal))
                throw new Exception("The push (spells off) went out as: " + string.Join(" || ", sent.ToArray()) + " => " + pushed);
            sent.Clear();
            ForkSettings.Current.PushSpells = true;
            pushed = fake.Push(top[0], draft);
            if (!pushed.Contains("runes, spells and items") || sent.Count != 4 || sent[0] != "DELETE /lol-perks/v1/pages/11" || !sent[1].StartsWith("POST /lol-perks/v1/pages {\"name\":\"Capri\"", StringComparison.Ordinal)
                || sent[2] != "PATCH /lol-champ-select/v1/session/my-selection {\"spell1Id\":4,\"spell2Id\":12}" || !sent[3].StartsWith("PUT /lol-item-sets/v1/item-sets/555/sets ", StringComparison.Ordinal))
                throw new Exception("The push (spells on) went out as: " + string.Join(" || ", sent.ToArray()) + " => " + pushed);
            ForkSettings.Current.PushSpells = false;
            if (PushNumber("push build 2") != 2 || PushNumber("load the third build") != 3 || PushNumber("push the best build") != 1 || PushNumber("import the second runes") != 2) throw new Exception("Build numbers were heard wrong.");
            if (!PushWords.IsMatch("push build 2") || !PushWords.IsMatch("Capri, import the runes") || PushWords.IsMatch("what's the best build?")) throw new Exception("Push requests were heard wrong.");
            bool wasDrafting = drafting;
            drafting = false;
            if (ForkHeard("push build 2", false)) throw new Exception("A push was taken outside champ select.");
            drafting = wasDrafting;
            int asked = 0;
            using (LeaguePanel shown = new LeaguePanel())
            {
                shown.PushAsked += delegate(int number) { asked = number; };
                shown.Fill("Ahri", new List<string> { "1) a", "2) b", "3) c" }, true);
                typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shown.Buttons[1], new object[] { EventArgs.Empty });
                if (shown.Buttons.Count != 3 || asked != 2) throw new Exception("The panel's Push buttons are wired wrong.");
                shown.Fill("Ahri", new List<string> { "1) a" }, false);
                if (shown.Buttons.Count != 0) throw new Exception("Push buttons showed with them switched off.");
            }
            checks.Add("PASS draft: her pick and bans are read, she hovers the first owned free favourite and the hover alone never locks in or overrides their hover, a push swaps her old rune page and sends runes and the item set (summoner spells only when switched on, Flash kept on its key), \"push build 2\" is heard, and the panel's buttons push the right build");
        }

        private void SmokeCounters(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            Cloud keptRemote = brain.Remote;
            League keptLeague = league;
            bool keptDrafting = drafting;
            string keptBrief = League.Brief;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.FavMid = new[] { "Ahri", "Syndra" };
                league = SmokeLeagueClient(new List<string>(), new Dictionary<string, string>());
                brain.Remote = new Cloud("sk-ant-test", "claude-haiku-5-5");
                drafting = true;
                List<string> asked = new List<string>();
                Quick.Post = delegate(string body, string key)
                {
                    asked.Add(body);
                    if (!body.Contains("\"thinking\":{\"type\":\"disabled\"}")) return Tuple.Create(200, "{\"content\":[{\"type\":\"thinking\",\"thinking\":\"Let me think about the matchup at length...\"}],\"stop_reason\":\"max_tokens\",\"usage\":{\"output_tokens\":600}}");
                    return Tuple.Create(200, SmokeReply("1. **Ahri**: strong into his burst. 2. **Syndra** outranges him. 3. LeBlanc works too, and Orianna is fine."));
                };
                if (ForkSettings.Current.CountersUnasked) throw new Exception("Counters pop up by themselves by default; he wants them only when he asks.");
                string ask = CounterAsk("Zed", "middle", new[] { "Ahri" });
                if (!ask.Contains("only the names of the 3 best counterpicks") || !ask.Contains("nothing else") || ask.Contains("why") || !League.ResearchAsk("You're mid.").Contains("only the names of the 3 best picks") || League.ResearchAsk("You're mid.").Contains("why")) throw new Exception("A counter or research ask still asked for reasons: " + ask);
                List<string> pool = new List<string> { "Katarina", "Malphite", "Diana", "Vi", "Zed", "Viktor" };
                string three = Three("I'll check the data. 1. **Katarina**: strong via her reset. 2. **Malphite** is banned. 3. **Diana** and Viktor too. Zed is theirs.", pool, new List<string> { "Malphite", "Zed" });
                if (three != "Katarina, Diana, Viktor" || Three("no names here", pool, new List<string>()) != null) throw new Exception("The answer was cut to three names as: " + three);
                string stored = "Into Zed: Ahri, LeBlanc. Into Garen: Darius, Vayne, Quinn.";
                if (CountersSaid("counters?", stored) != "Darius, Vayne, Quinn." || CountersSaid("what counters zed", stored) != "Ahri, LeBlanc." || CountersSaid("who should I pick", stored) != "Darius, Vayne, Quinn." || CountersSaid("what's the best build", stored) != null || CountersSaid("counters?", "") != null
                    || CountersSaid("best picks?", "Picks into their team: Ahri, Syndra, LeBlanc.") != "Ahri, Syndra, LeBlanc.")
                    throw new Exception("Asked counters were answered as: " + CountersSaid("counters?", stored) + " / " + CountersSaid("what counters zed", stored));
                League.Brief = "You're mid.";
                if (!Flavour.Extra().Contains("name exactly 3 champions and nothing else")) throw new Exception("Her champ select brief doesn't say to name only 3 champions.");
                string json = SmokeSession.Replace("\"championId\":222", "\"championId\":134").Replace("{\"cellId\":7,\"championId\":0}", "{\"cellId\":7,\"championId\":86},{\"cellId\":8,\"championId\":61}")
                    .Replace("\"timer\":", "\"actions\":[[{\"id\":7,\"actorCellId\":2,\"championId\":0,\"type\":\"pick\",\"completed\":false,\"isInProgress\":true}]],\"timer\":");
                ChampSession session = League.ParseSession(json);
                if (ResearchWanted(3) == false) throw new Exception("The three-enemies research was off before any counter ran.");
                string first = CounterTurn(session, true);
                if (first != "Into Zed: Ahri, LeBlanc." || asked.Count != 1 || !asked[0].Contains("web_search") || !asked[0].Contains("playing mid") || !asked[0].Contains("enemy picked Zed") || !asked[0].Contains("Ahri, Syndra"))
                    throw new Exception("The first counter look-up was: " + first + " / " + string.Join(" || ", asked.ToArray()));
                if (ResearchWanted(3)) throw new Exception("The three-enemies research still fired after a counter ran.");
                string second = CounterTurn(session, true);
                if (second == null || !second.StartsWith("Into Syndra:", StringComparison.Ordinal) || !asked[1].Contains("enemy picked Syndra")) throw new Exception("The second enemy wasn't countered: " + second);
                string third = CounterTurn(session, true);
                if (third == null || !third.StartsWith("Into Garen:", StringComparison.Ordinal)) throw new Exception("The third enemy wasn't countered: " + third);
                if (CounterTurn(session, true) != null || asked.Count != 3) throw new Exception("A fourth counter ran past the limit of three.");
                if (!league.Researched.Contains("Into Zed:") || !league.Researched.Contains("Into Garen:")) throw new Exception("The counters didn't reach her briefing.");
                EndCounters();
                if (!ResearchWanted(3)) throw new Exception("A new draft didn't bring the research back.");
                ChampSession locked = League.ParseSession(json.Replace("\"completed\":false", "\"completed\":true"));
                if (CounterTurn(locked, true) != null || asked.Count != 3) throw new Exception("A counter ran after their own pick was locked.");
                ForkSettings.Current.LeagueResearch = false;
                if (CounterTurn(session, true) != null || asked.Count != 3) throw new Exception("A counter ran with research switched off.");
                ForkSettings.Current.LeagueResearch = true;
                if (CounterTurn(League.ParseSession(json.Replace("\"assignedPosition\":\"middle\"", "\"assignedPosition\":\"\"")), true) != null || asked.Count != 3) throw new Exception("A counter ran with no assigned role.");
                if (CounterAsk("Zed", "middle", new string[0]).Contains("favourites")) throw new Exception("The ask mentioned favourites when there are none.");
                asked.Clear();
                string researched = League.Research(brain.Remote, League.ResearchAsk("You're mid on Ahri."), true);
                if (researched == null || asked.Count != 1) throw new Exception("The draft research spent its answer on thinking: " + string.Join(" || ", asked.ToArray()));
            }
            finally
            {
                EndCounters();
                countering = false;
                ForkSettings.Current = keptSettings;
                Quick.Post = keptPost;
                brain.Remote = keptRemote;
                league = keptLeague;
                drafting = keptDrafting;
                League.Brief = keptBrief;
            }
            checks.Add("PASS lane counters: in champ select each new enemy pick (up to three) gets a background web look-up for the best counterpicks into it for their role (thinking off, so the answer is never spent on thinking), favourites preferred, nothing after they lock in or with research off, and the old three-enemies research steps aside once counters ran; the answer is cut to 3 free champion names with no reasons, kept quiet until he asks (\"counters?\" answers the newest, \"counters zed\" that one)");
        }

        private void SmokeGameSpot(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            int sizeWas = preferences.Size;
            Point anchorWas = anchor;
            Rectangle work = Stage.Main.WorkingArea;
            ForkSettings.Current.HasGameSpot = true;
            ForkSettings.Current.GameSpotX = work.Left + 40;
            ForkSettings.Current.GameSpotY = work.Top + 40;
            ShrinkForGame();
            if (preferences.Size != 140 || !shrunk || Math.Abs(anchor.X - (work.Left + 40)) > 200) throw new Exception("She didn't go tiny in her game spot.");
            anchor = new Point(work.Left + 120, work.Top + 90);
            Location = anchor;
            GrowAfterGame();
            if (preferences.Size != sizeWas || shrunk || ForkSettings.Current.GameSpotX != work.Left + 120 || ForkSettings.Current.GameSpotY != work.Top + 90) throw new Exception("She didn't remember her game spot or come back to size.");
            anchor = anchorWas;
            Location = anchor;
            checks.Add("PASS game spot: in a match she goes tiny in her game spot, and afterwards remembers where they left her and comes back to her size");
        }
    }
}
