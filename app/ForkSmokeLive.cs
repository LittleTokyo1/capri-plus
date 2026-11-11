using System;
using System.Collections.Generic;
using System.Globalization;

namespace CapriDesktop
{
    // Capri+ smoke checks for live League matches.
    internal sealed partial class Companion
    {
        private static string SmokeLive(string events, double time = 905.3, double health = 500, bool dead = false, int farm = 80, string position = "MIDDLE")
        {
            return "{\"activePlayer\":{\"riotId\":\"sampleuser#EUW\",\"riotIdGameName\":\"sampleuser\",\"summonerName\":\"sampleuser#EUW\",\"currentGold\":1234.5,\"level\":9,\"championStats\":{\"currentHealth\":" + health.ToString(CultureInfo.InvariantCulture) + ",\"maxHealth\":1000.0}}," +
                "\"allPlayers\":[{\"championName\":\"Zed\",\"riotId\":\"sampleuser#EUW\",\"riotIdGameName\":\"sampleuser\",\"summonerName\":\"sampleuser#EUW\",\"team\":\"ORDER\",\"level\":9,\"position\":\"" + position + "\",\"isDead\":" + (dead ? "true" : "false") + ",\"scores\":{\"kills\":3,\"deaths\":1,\"assists\":2,\"creepScore\":" + farm + "},\"items\":[{\"displayName\":\"Youmuu's Ghostblade\"},{\"displayName\":\"Serrated Dirk\"}]}," +
                "{\"championName\":\"Annie\",\"riotId\":\"bob#EUW\",\"riotIdGameName\":\"bob\",\"summonerName\":\"bob#EUW\",\"team\":\"CHAOS\",\"level\":8,\"scores\":{\"kills\":1,\"deaths\":3,\"assists\":0,\"creepScore\":70},\"items\":[{\"displayName\":\"Luden's Companion\"}]}," +
                "{\"championName\":\"Ekko\",\"riotId\":\"amy#EUW\",\"riotIdGameName\":\"amy\",\"summonerName\":\"amy#EUW\",\"team\":\"ORDER\",\"level\":8,\"scores\":{\"kills\":2,\"deaths\":0,\"assists\":3,\"creepScore\":50},\"items\":[]}]," +
                "\"events\":{\"Events\":[" + events + "]},\"gameData\":{\"gameMode\":\"CLASSIC\",\"gameTime\":" + time.ToString(CultureInfo.InvariantCulture) + "}}";
        }

        private static string SmokeEvent(int id, string name, double time, string fields)
        {
            return "{\"EventID\":" + id + ",\"EventName\":\"" + name + "\",\"EventTime\":" + time.ToString(CultureInfo.InvariantCulture) + (fields.Length > 0 ? "," + fields : "") + "}";
        }

        private static void SmokeLiveFacts(List<string> checks)
        {
            string events = string.Join(",", new[]
            {
                SmokeEvent(0, "GameStart", 0.05, ""),
                SmokeEvent(1, "MinionsSpawning", 65, ""),
                SmokeEvent(2, "ChampionKill", 751.4, "\"KillerName\":\"sampleuser\",\"VictimName\":\"bob\",\"Assisters\":[\"amy\"]"),
                SmokeEvent(3, "DragonKill", 842.7, "\"KillerName\":\"bob\",\"DragonType\":\"Fire\",\"Stolen\":\"False\""),
                SmokeEvent(4, "TurretKilled", 880, "\"KillerName\":\"Minion_T200L0S1N11\",\"TurretKilled\":\"Turret_T2_L_03_A\""),
                SmokeEvent(5, "ChampionKill", 890, "\"KillerName\":\"bob\",\"VictimName\":\"amy\",\"Assisters\":[]"),
                SmokeEvent(6, "InhibKilled", 895, "\"KillerName\":\"Turret_T1_C_01_A\",\"InhibKilled\":\"Barracks_T1_L1\"")
            });
            LiveData data = League.Parse<LiveData>(SmokeLive(events));
            string brief = LiveGame.Describe(data);
            string wanted = "Recent events (game clock): 12:31 Zed killed Annie (assists: Ekko); 14:02 the enemy team took the Infernal dragon; 14:40 minions destroyed an enemy turret; 14:50 Annie killed Ekko; 14:55 a turret destroyed their team's inhibitor.";
            if (!brief.Contains(wanted) || brief.Contains("MinionsSpawning") || !brief.Contains("Only state match facts that are in this data; if something isn't here, say you don't know.") || !brief.EndsWith(LiveGame.FarmRule, StringComparison.Ordinal))
                throw new Exception("The recent events were written as: " + brief);
            if (!brief.Contains("80 cs (5.3 cs/min), 1234 gold")) throw new Exception("Their farm rate was written wrong: " + brief);
            List<string> many = new List<string>();
            for (int i = 0; i < 15; i++) many.Add(SmokeEvent(i, "ChampionKill", 100 + i * 10, "\"KillerName\":\"bob\",\"VictimName\":\"amy\""));
            string capped = LiveGame.Describe(League.Parse<LiveData>(SmokeLive(string.Join(",", many.ToArray()))));
            if (capped.Split(new[] { "killed" }, StringSplitOptions.None).Length - 1 != 12 || capped.Contains("1:40 ") || !capped.Contains("4:00 Annie killed Ekko")) throw new Exception("The recent events weren't the last twelve: " + capped);
            if (LiveGame.Describe(League.Parse<LiveData>(SmokeLive(""))).Contains("Recent events")) throw new Exception("An empty feed still listed events.");
            if (LiveGame.Describe(null) != "") throw new Exception("No game still gave a description.");

            LiveData farmed = League.Parse<LiveData>(SmokeLive("", 600, 500, false, 40));
            string nudge = LiveGame.Farm(farmed, 10);
            if (nudge == null || !nudge.Contains("4.0 cs/min") || !nudge.Contains("10 minutes") || !nudge.Contains("gently")) throw new Exception("The farm nudge was: " + nudge);
            if (LiveGame.Farm(League.Parse<LiveData>(SmokeLive("", 600, 500, false, 70)), 10) != null) throw new Exception("A good farm pace was nagged about.");
            foreach (string role in new[] { "JUNGLE", "UTILITY", "" })
                if (LiveGame.Farm(League.Parse<LiveData>(SmokeLive("", 600, 500, false, 10, role)), 10) != null) throw new Exception("Farm was checked for " + (role.Length == 0 ? "no role" : role) + ".");

            LiveGame.Scare scare = new LiveGame.Scare();
            Func<double, double, bool, string> watch = delegate(double time, double health, bool dead) { return scare.Watch(League.Parse<LiveData>(SmokeLive("", time, health, dead))); };
            if (watch(1000, 50, false) != null || watch(1003, 40, false) != null) throw new Exception("A near-death was reported before five seconds passed.");
            string clutch = watch(1006, 150, false);
            if (clutch == null || !clutch.Contains("survived") || !clutch.Contains("40 HP") || clutch.Contains("%") || clutch.Contains("10 percent") || watch(1008, 150, false) != null || watch(1010, 400, false) != null) throw new Exception("The near-death survival was reported wrong: " + clutch);
            if (watch(1020, 50, false) != null || watch(1021, 0, true) != null || watch(1030, 50, false) != null || watch(1033, 50, false) != null) throw new Exception("A death or a long gap confused the survival check.");
            if (watch(1036, 50, false) == null) throw new Exception("A second near-death wasn't reported.");
            if (new LiveGame.Scare().Watch(League.Parse<LiveData>(SmokeLive("").Replace("\"maxHealth\":1000.0", "\"maxHealth\":0"))) != null) throw new Exception("A missing max health counted as a scare.");
            Dictionary<string, DateTime> seenMoments = new Dictionary<string, DateTime>();
            DateTime momentAt = new DateTime(2026, 11, 3, 23, 12, 48);
            seenMoments["Their own team just aced the enemy team."] = momentAt;
            List<string> momentsLeft = Unrepeated(new List<string> { "They (Zed) just got a double kill!", "Their own team just aced the enemy team." }, seenMoments, momentAt.AddSeconds(28));
            if (momentsLeft.Count != 1 || momentsLeft[0] != "They (Zed) just got a double kill!" || Unrepeated(new List<string> { "Their own team just aced the enemy team." }, seenMoments, momentAt.AddMinutes(4)).Count != 1) throw new Exception("A game moment she reacted to a moment ago got a second reaction, or an old one was held.");
            checks.Add("PASS game moments not repeated: a moment she reacted to less than 3 minutes ago (the second ace at 23:13) is left out of the next reaction, the new part (a double kill) still gets one");
            checks.Add("PASS live facts: the last 12 notable events are listed with game-clock times and champion names (turrets and minions named, minion spawns skipped), cs/min is shown, the brief tells her to state only facts it contains, the 10/15/20 minute farm nudge skips junglers and supports, and a near-death survived five seconds is spotted once per scare and told as how low they got in HP, never as a percentage");
        }

        private void SmokeLiveGame(List<string> checks)
        {
            LiveData data = League.Parse<LiveData>(SmokeLive("{\"EventID\":0,\"EventName\":\"GameStart\",\"EventTime\":0.05}"));
            string brief = LiveGame.Describe(data);
            if (LiveGame.Mine(data) == null || LiveGame.Mine(data).Champion != "Zed" || !brief.Contains("They're Zed this game (") || !brief.Contains("level 9, 3/1/2, 80 cs (5.3 cs/min), 1234 gold, items: Youmuu's Ghostblade, Serrated Dirk") || !brief.Contains("Enemies: Annie (1/3/0, 70 cs; Luden's Companion)") || !brief.Contains("15 min in"))
                throw new Exception("The live game was described as: " + brief);
            SmokeLiveFacts(checks);
            LiveEvent kill = new LiveEvent { Name = "ChampionKill", Killer = "sampleuser", Victim = "bob" }, death = new LiveEvent { Name = "ChampionKill", Killer = "bob", Victim = "sampleuser" };
            LiveEvent assist = new LiveEvent { Name = "ChampionKill", Killer = "amy", Victim = "bob", Assisters = new[] { "sampleuser" } };
            LiveEvent penta = new LiveEvent { Name = "Multikill", Killer = "sampleuser", Streak = 5 }, dragon = new LiveEvent { Name = "DragonKill", Killer = "amy", Dragon = "Fire" };
            if (LiveGame.Moment(kill, data, 2) != "They (Zed) just killed Annie." || LiveGame.Moment(death, data, 2) != "They (Zed) just died to Annie." || LiveGame.Moment(assist, data, 2) != "They (Zed) assisted a kill on Annie." || !LiveGame.Moment(penta, data, 1).Contains("PENTAKILL"))
                throw new Exception("Kills, deaths, assists or multikills were read wrong.");
            if (LiveGame.Moment(kill, data, 1) != null || LiveGame.Moment(death, data, 1) != null || LiveGame.Moment(assist, data, 1) != null) throw new Exception("Plain kills, deaths or assists reached key moments.");
            if (!LiveGame.Moment(new LiveEvent { Name = "Multikill", Killer = "sampleuser", Streak = 2 }, data, 1).Contains("double kill") || LiveGame.Moment(new LiveEvent { Name = "Multikill", Killer = "bob", Streak = 2 }, data, 1) != null)
                throw new Exception("Double kills were read wrong.");
            if (LiveGame.Moment(dragon, data, 1) != null || LiveGame.Moment(dragon, data, 2) != "Their own team took the Fire dragon." || LiveGame.Moment(kill, data, 0) != null)
                throw new Exception("Chattiness didn't decide which moments she reacts to.");
            LiveEvent stolenByUs = new LiveEvent { Name = "DragonKill", Killer = "amy", Dragon = "Fire", Stolen = "True" }, stolenByThem = new LiveEvent { Name = "DragonKill", Killer = "bob", Dragon = "Fire", Stolen = "True" };
            if (LiveGame.Moment(stolenByUs, data, 1) != "Their own team took the Fire dragon, stolen!" || LiveGame.Moment(stolenByThem, data, 1) != null || LiveGame.Moment(stolenByThem, data, 2) == null)
                throw new Exception("Dragon steals were read wrong.");
            LiveEvent baronUs = new LiveEvent { Name = "BaronKill", Killer = "amy" }, baronThem = new LiveEvent { Name = "BaronKill", Killer = "bob" }, heraldUs = new LiveEvent { Name = "HeraldKill", Killer = "amy" };
            if (LiveGame.Moment(baronUs, data, 1) != "Their own team took Baron." || LiveGame.Moment(baronThem, data, 1) != null || LiveGame.Moment(baronThem, data, 2) != "The enemy team took Baron.")
                throw new Exception("Baron was read wrong.");
            if (LiveGame.Moment(heraldUs, data, 1) != null || LiveGame.Moment(heraldUs, data, 2) != "Their own team took the Rift Herald." || LiveGame.Moment(new LiveEvent { Name = "HeraldKill", Killer = "amy", Stolen = "True" }, data, 1) == null)
                throw new Exception("Herald was read wrong.");
            if (LiveGame.Moment(new LiveEvent { Name = "FirstBlood", Recipient = "sampleuser" }, data, 1) == null || LiveGame.Moment(new LiveEvent { Name = "FirstBlood", Recipient = "bob" }, data, 1) != null)
                throw new Exception("First blood was read wrong.");
            if (LiveGame.Moment(new LiveEvent { Name = "GameEnd", Result = "Win" }, data, 1) != "They just WON the game as Zed." || LiveGame.Moment(new LiveEvent { Name = "Ace", AcingTeam = "ORDER" }, data, 1) != "Their own team just aced the enemy team." || LiveGame.Moment(new LiveEvent { Name = "Ace", AcingTeam = "CHAOS" }, data, 1) != null)
                throw new Exception("Wins or aces were read wrong.");

            ForkSettings keptSettings = ForkSettings.Current;
            Func<string> keptFetch = LiveGame.Fetch;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            Cloud keptRemote = brain.Remote;
            string keptBrief = League.Brief;
            try
            {
                ForkSettings.Current = new ForkSettings();
                brain.Forget();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.Proactive = 2;
                brain.Remote = new Cloud("sk-ant-test", "claude-haiku-5-5");
                List<string> asked = new List<string>();
                Quick.Post = delegate(string body, string key) { asked.Add(body); return Tuple.Create(200, SmokeReply("Kuso, that was clean!")); };
                string feed = "{\"EventID\":0,\"EventName\":\"GameStart\"},{\"EventID\":1,\"EventName\":\"ChampionKill\",\"KillerName\":\"sampleuser\",\"VictimName\":\"bob\"}";
                LiveGame.Fetch = delegate { return SmokeLive(feed); };
                lastEvent = -1;
                lastReaction = DateTime.MinValue;
                if (LiveTurn(true) != null || asked.Count != 0 || !League.Brief.Contains("They're Zed")) throw new Exception("Starting mid-game replayed old kills or didn't learn the game.");
                feed += ",{\"EventID\":2,\"EventName\":\"ChampionKill\",\"KillerName\":\"sampleuser\",\"VictimName\":\"bob\"}";
                string line = LiveTurn(true);
                if (line != "Kuso, that was clean!" || !asked[0].Contains("They (Zed) just killed Annie.")) throw new Exception("She didn't react to their kill in chatty mode: " + line);
                if (!asked[0].Contains("\"thinking\":{\"type\":\"disabled\"}")) throw new Exception("A game reaction let the small model think, so its short budget can go to thinking and give no line.");
                string[] said = brain.Recent();
                if (said.Length != 2 || said[0] != "(game: They (Zed) just killed Annie.)" || said[1] != "Kuso, that was clean!") throw new Exception("Her game reaction didn't reach her chat history: " + string.Join(" / ", said));
                feed += ",{\"EventID\":3,\"EventName\":\"ChampionKill\",\"KillerName\":\"sampleuser\",\"VictimName\":\"bob\"}";
                if (LiveTurn(true) != null || asked.Count != 1) throw new Exception("She reacted to every kill within seconds.");
                feed += ",{\"EventID\":4,\"EventName\":\"GameEnd\",\"Result\":\"Win\"}";
                if (LiveTurn(true) == null || !asked[1].Contains("WON the game")) throw new Exception("She skipped the win.");
                EndLive();
                if (lastEvent != -1 || League.Brief != null) throw new Exception("The finished game stayed in her head.");
                LiveGame.Fetch = delegate { return null; };
                if (LiveTurn(true) != null) throw new Exception("No game running still gave a reaction.");

                ForkSettings.Current.Proactive = 1;
                asked.Clear();
                double time = 905.3, health = 500;
                int farm = 80;
                string position = "MIDDLE";
                feed = "{\"EventID\":0,\"EventName\":\"GameStart\"},{\"EventID\":1,\"EventName\":\"ChampionKill\",\"KillerName\":\"sampleuser\",\"VictimName\":\"bob\"}";
                LiveGame.Fetch = delegate { return SmokeLive(feed, time, health, false, farm, position); };
                lastReaction = DateTime.MinValue;
                LiveTurn(true);
                feed += ",{\"EventID\":2,\"EventName\":\"ChampionKill\",\"KillerName\":\"sampleuser\",\"VictimName\":\"bob\"},{\"EventID\":3,\"EventName\":\"ChampionKill\",\"KillerName\":\"bob\",\"VictimName\":\"sampleuser\"}";
                if (LiveTurn(true) != null || asked.Count != 0) throw new Exception("Key moments reacted to a plain kill or death.");
                feed += ",{\"EventID\":4,\"EventName\":\"Multikill\",\"KillerName\":\"sampleuser\",\"KillStreak\":2}";
                if (LiveTurn(true) == null || !asked[0].Contains("double kill")) throw new Exception("Key moments skipped their double kill.");

                lastReaction = DateTime.MinValue;
                time = 1000;
                health = 50;
                if (LiveTurn(true) != null || asked.Count != 1) throw new Exception("A near-death was reported right away.");
                time = 1006;
                if (LiveTurn(true) == null || !asked[1].Contains("barely survived") || asked[1].Contains("%")) throw new Exception("Surviving a near-death for five seconds wasn't noticed.");
                time = 1010;
                if (LiveTurn(true) != null || asked.Count != 2) throw new Exception("The same near-death was reported twice.");

                health = 800;
                lastReaction = DateTime.MinValue;
                EndLive();
                farm = 40;
                time = 599;
                feed = "{\"EventID\":0,\"EventName\":\"GameStart\"}";
                LiveTurn(true);
                time = 601;
                if (LiveTurn(true) == null || !asked[2].Contains("4.0 cs/min") || !asked[2].Contains("10 minutes")) throw new Exception("The 10 minute farm nudge didn't come.");
                lastReaction = DateTime.MinValue;
                time = 605;
                if (LiveTurn(true) != null || asked.Count != 3) throw new Exception("The 10 minute farm nudge came twice.");
                position = "JUNGLE";
                lastReaction = DateTime.MinValue;
                time = 901;
                if (LiveTurn(true) != null || asked.Count != 3) throw new Exception("A jungler was nudged about farm.");
                EndLive();
                position = "MIDDLE";
                time = 1000;
                feed = "{\"EventID\":0,\"EventName\":\"GameStart\"}";
                if (LiveTurn(true) != null || farmMarks.Count != 2 || asked.Count != 3) throw new Exception("Joining mid-game nudged about farm marks already passed.");
                time = 1201;
                farm = 60;
                lastReaction = DateTime.MinValue;
                if (LiveTurn(true) == null || !asked[3].Contains("20 minutes")) throw new Exception("The 20 minute farm nudge didn't come.");

                ForkSettings.Current.Proactive = 0;
                EndLive();
                feed += ",{\"EventID\":5,\"EventName\":\"Multikill\",\"KillerName\":\"sampleuser\",\"KillStreak\":3}";
                lastReaction = DateTime.MinValue;
                time = 905.3;
                LiveTurn(true);
                feed += ",{\"EventID\":6,\"EventName\":\"Multikill\",\"KillerName\":\"sampleuser\",\"KillStreak\":3}";
                if (LiveTurn(true) != null || asked.Count != 4) throw new Exception("Quiet mode reacted to a multikill.");
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                LiveGame.Fetch = keptFetch;
                Quick.Post = keptPost;
                brain.Remote = keptRemote;
                brain.Forget();
                League.Brief = keptBrief;
                lastEvent = -1;
                scare.Reset();
                farmMarks.Clear();
                live = false;
            }
            checks.Add("PASS live game: League's live data gives her the champ, level, KDA, cs/min, gold and items for them and every enemy, key moments are only their multikills, first blood, aces, steals, Baron, the result and a near-death survived (plain kills, deaths and assists are chatty mode only), a farm nudge comes at 10/15/20 minutes, starting mid-game doesn't replay old events, reactions are spaced out, and a reaction is asked for with thinking off so the short budget gives a line");
        }
    }
}
