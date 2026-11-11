using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal const string SmokeBuildItems = "[{\"id\":6655,\"name\":\"Luden's Companion\"},{\"id\":3020,\"name\":\"Sorcerer's Shoes\"},{\"id\":4645,\"name\":\"Shadowflame\"},{\"id\":3089,\"name\":\"Rabadon's Deathcap\"},{\"id\":3157,\"name\":\"Zhonya's Hourglass\"},{\"id\":3135,\"name\":\"Void Staff\"},{\"id\":3118,\"name\":\"Malignance\"},{\"id\":2003,\"name\":\"Health Potion\"}]";
        internal const string SmokeFakerItems = "6655,3020,4645,3089,3157,3135";
        internal const string SmokeFullBuild = "Pro build from Faker: Luden's Companion, Sorcerer's Shoes, Shadowflame, Rabadon's Deathcap, Zhonya's Hourglass, Void Staff.";
        internal const string SmokeTwoItems = "{\"displayName\":\"Luden's Companion\",\"itemID\":6655},{\"displayName\":\"Sorcerer's Shoes\",\"itemID\":3020},{\"displayName\":\"Health Potion\",\"itemID\":2003}";

        private static string SmokeBuildMatch(int champion, string pro, long perk0, long when, string completed)
        {
            return "{\"calculatedRole\":\"mid\",\"championId\":" + champion + ",\"completedItems\":[" + completed + "],\"finalBuild\":[" + completed + "],\"itemPath\":[],\"matchId\":" + when + ",\"matchTimestamp\":" + when + "," +
                "\"normalizedName\":\"" + pro.ToLowerInvariant() + "\",\"proInfo\":{\"officialName\":\"" + pro + "\",\"currentTeam\":\"T1\"},\"opponentChampionId\":238," +
                "\"runes\":{\"perk0\":" + perk0 + ",\"perk1\":8139,\"perk2\":8140,\"perk3\":8106,\"perk4\":8226,\"perk5\":8210,\"primaryStyle\":8100,\"subStyle\":8200},\"statShards\":[5008,5008,5011],\"summonerSpells\":[4,14],\"totalKills\":9,\"totalDeaths\":2,\"totalAssists\":7,\"win\":true}";
        }

        private static string SmokeBuildApi(params string[] matches)
        {
            return "{\"data\":{\"getProChampionMatchList\":{\"matchList\":[" + string.Join(",", matches) + "]}}}";
        }

        private static string SmokeBuildLive(string champion, string items, int time)
        {
            return "{\"activePlayer\":{\"riotId\":\"sampleuser#EUW\",\"riotIdGameName\":\"sampleuser\",\"summonerName\":\"sampleuser#EUW\",\"currentGold\":500,\"level\":11,\"championStats\":{\"currentHealth\":800,\"maxHealth\":1000.0}}," +
                "\"allPlayers\":[{\"championName\":\"" + champion + "\",\"riotId\":\"sampleuser#EUW\",\"riotIdGameName\":\"sampleuser\",\"summonerName\":\"sampleuser#EUW\",\"team\":\"ORDER\",\"level\":11,\"position\":\"MIDDLE\",\"scores\":{\"kills\":4,\"deaths\":1,\"assists\":3,\"creepScore\":120},\"items\":[" + items + "]}," +
                "{\"championName\":\"Annie\",\"riotId\":\"bob#EUW\",\"riotIdGameName\":\"bob\",\"summonerName\":\"bob#EUW\",\"team\":\"CHAOS\",\"level\":10,\"scores\":{\"kills\":1,\"deaths\":3,\"assists\":0,\"creepScore\":100},\"items\":[{\"displayName\":\"Shadowflame\",\"itemID\":4645}]}]," +
                "\"events\":{\"Events\":[{\"EventID\":0,\"EventName\":\"GameStart\",\"EventTime\":0.05}]},\"gameData\":{\"gameMode\":\"CLASSIC\",\"mapNumber\":11,\"gameTime\":" + time + "}}";
        }

        private static string SmokeLockSession(int seat, int locked, bool completed)
        {
            return "{\"localPlayerCellId\":2,\"myTeam\":[{\"cellId\":2,\"championId\":" + seat + ",\"championPickIntent\":0,\"assignedPosition\":\"middle\",\"spell1Id\":4,\"spell2Id\":14}],\"theirTeam\":[{\"cellId\":5,\"championId\":238}]," +
                "\"actions\":[[{\"id\":7,\"actorCellId\":2,\"championId\":" + locked + ",\"type\":\"pick\",\"completed\":" + (completed ? "true" : "false") + ",\"isInProgress\":false}]]}";
        }

        private void SmokeBuildAsk(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            League keptLeague = league;
            Func<string, string> keptDownload = ProBuilds.Download;
            Func<string> keptFetch = LiveGame.Fetch;
            List<ProBuild> keptReady = builds;
            bool keptAuto = autoPushed, keptDrafting = drafting;
            string keptBrief = League.Brief, keptPhase = Lockout.LeaguePhase, keptHeard = heard;
            TalkState keptTalk = talkState;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.Proactive = 0;
                ForkSettings.Current.ObjectiveCalls = false;
                List<string> sent = new List<string>();
                Dictionary<string, string> replies = new Dictionary<string, string>();
                replies["/lol-perks/v1/pages"] = "[]";
                replies["/lol-summoner/v1/current-summoner"] = "{\"summonerId\":555}";
                replies["/lol-item-sets/v1/item-sets/555/sets"] = "{\"accountId\":1,\"itemSets\":[],\"timestamp\":1}";
                league = SmokeLeagueClient(sent, replies);
                league.LoadGameData(SmokePerks, SmokeStyles, SmokeBuildItems, SmokeSpells);
                ProBuilds.Download = delegate { throw new InvalidOperationException("The smoke reached the pro build site."); };
                List<ProBuild> ahri = ProBuilds.Top(ProBuilds.Parse(SmokeBuildApi(SmokeBuildMatch(103, "Faker", 8112, 2000, SmokeFakerItems), SmokeBuildMatch(103, "Chovy", 8229, 1000, "3118,3020,3089"))), "middle", 3);
                List<ProBuild> syndra = ProBuilds.Top(ProBuilds.Parse(SmokeBuildApi(SmokeBuildMatch(134, "Bdd", 8112, 3000, "3118,3020,3157"))), "middle", 3);
                if (ahri.Count != 2 || ahri[0].Match.Pro != "Faker" || ahri[1].Match.Pro != "Chovy" || syndra.Count != 1) throw new Exception("The smoke pro builds were read wrong.");
                EndGameBuild();
                SmokeBuildRace(checks, sent, ahri, syndra);
                SmokeBuildKept(checks, ahri, syndra);
                SmokeBuildAnswer(checks);
                SmokeBuildBrief(checks, ahri, syndra);
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                league = keptLeague;
                ProBuilds.Download = keptDownload;
                LiveGame.Fetch = keptFetch;
                EndLive();
                live = false;
                lastLive = null;
                ObjectiveCalled = null;
                EndGameBuild();
                builds = keptReady;
                autoPushed = keptAuto;
                drafting = keptDrafting;
                League.Brief = keptBrief;
                Lockout.LeaguePhase = keptPhase;
                CloseTalk();
                talkState = keptTalk;
                heard = keptHeard;
            }
        }

        private static void SmokeBuildFill(List<string> checks)
        {
            List<ProBuild> zed = ProBuilds.Top(ProBuilds.Parse(SmokeBuildApi(
                SmokeBuildMatch(238, "Bzeuw", 8112, 5000, "3158,6699,3158,2520"),
                SmokeBuildMatch(238, "MathisV", 8112, 4000, "6699,3158,6694,3814"),
                SmokeBuildMatch(238, "Mikyx", 8112, 3000, "6699,3158,6696,3036,3814"),
                SmokeBuildMatch(238, "Tobias", 8112, 2000, "6699,3111,6696,6695"),
                SmokeBuildMatch(238, "Kangin", 8229, 1000, "6697,3158,6699,2520,6695"))), "middle", 1);
            if (zed.Count != 1 || zed[0].Match.Pro != "Bzeuw") throw new Exception("The smoke Zed builds were read wrong.");
            string full = string.Join(",", Array.ConvertAll(zed[0].Full(), id => id.ToString()));
            if (full != "3158,6699,2520,6696,3814,6694") throw new Exception("A pro game that ended with three items gave the full build " + full);
            string options = string.Join(",", Array.ConvertAll(zed[0].Options(), id => id.ToString()));
            if (options != "6695,6697,3036") throw new Exception("The other items pros build most came out as " + options);
            ProBuild five = ProBuilds.Top(ProBuilds.Parse(SmokeBuildApi(SmokeBuildMatch(238, "Mikyx", 8112, 3000, "6699,3158,6696,3036,3814"))), "middle", 1)[0];
            if (string.Join(",", Array.ConvertAll(five.Full(), id => id.ToString())) != "6699,3158,6696,3036,3814" || Filled(five) || !Filled(zed[0])) throw new Exception("A lone pro game's own items weren't kept, or the fill was told wrong.");
            string merged = League.MergeItemSet("{\"accountId\":1,\"itemSets\":[],\"timestamp\":1}", zed[0], 238, "Capri");
            if (!merged.Contains("\"type\":\"Full build: first items from Bzeuw, then what pros build most\",\"items\":[{\"id\":\"3158\",\"count\":1},{\"id\":\"6699\",\"count\":1},{\"id\":\"2520\",\"count\":1},{\"id\":\"6696\",\"count\":1},{\"id\":\"3814\",\"count\":1},{\"id\":\"6694\",\"count\":1}]")
                || !merged.Contains("\"type\":\"More options, most built by pros first\",\"items\":[{\"id\":\"6695\",\"count\":1},{\"id\":\"6697\",\"count\":1},{\"id\":\"3036\",\"count\":1}]"))
                throw new Exception("The pushed item set doesn't hold the six-item build and the options: " + merged);
            checks.Add("PASS six-item pro build: a pro game that ended with three items is pushed and read back as its first three items, then what the pros with the same runes (then the whole lane) build most, six items with one pair of boots, plus a block of more options most built first; a game with five items keeps its own");
        }

        private void SmokeBuildRace(List<string> checks, List<string> sent, List<ProBuild> ahri, List<ProBuild> syndra)
        {
            ChampSession lagging = League.ParseSession(SmokeLockSession(134, 103, true)), hovering = League.ParseSession(SmokeLockSession(134, 134, false)), locked = League.ParseSession(SmokeLockSession(103, 103, true));
            if (DraftChampion(League.MyPick(lagging), lagging.Seat) != 103 || DraftChampion(League.MyPick(hovering), hovering.Seat) != 134 || DraftChampion(null, null) != 0) throw new Exception("Pro builds don't load for the champion he locked while his seat still shows the hover.");
            ForkSettings.Current.PushOnLock = true;
            autoPushed = false;
            builds = syndra;
            string early = AutoPushTurn(locked);
            if (early != null || sent.Count != 0 || autoPushed) throw new Exception("Ahri was locked while Syndra's pro builds were still loaded and a build went out: " + early + " / " + string.Join(" || ", sent.ToArray()));
            builds = ahri;
            if (AutoPushTurn(lagging) != null || sent.Count != 0 || autoPushed) throw new Exception("A build was pushed while his seat still showed the hovered champion, so the item set would go to the wrong champion.");
            if (AutoPushTurn(League.ParseSession(SmokeLockSession(103, 103, false))) != null || sent.Count != 0) throw new Exception("A build was pushed before he locked in.");
            string pushed = AutoPushTurn(locked);
            if (pushed == null || !pushed.StartsWith("Build 1 is in: Faker's runes and items", StringComparison.Ordinal) || !autoPushed || !sent.Exists(line => line.StartsWith("POST /lol-perks/v1/pages ", StringComparison.Ordinal))
                || !sent.Exists(line => line.StartsWith("PUT /lol-item-sets/v1/item-sets/555/sets ", StringComparison.Ordinal) && line.Contains("\"associatedChampions\":[103]") && line.Contains("\"id\":\"6655\"")))
                throw new Exception("The lock-in push of Ahri's build 1 went out as: " + pushed + " / " + string.Join(" || ", sent.ToArray()));
            int count = sent.Count;
            if (AutoPushTurn(locked) != null || sent.Count != count) throw new Exception("The lock-in push went out twice.");
            if (pushedBuild != ahri[0]) throw new Exception("The lock-in push wasn't remembered as this game's build.");
            autoPushed = false;
            ForkSettings.Current.PushOnLock = false;
            if (AutoPushTurn(locked) != null || sent.Count != count) throw new Exception("A build was pushed on lock-in with that switched off.");
            sent.Clear();
            checks.Add("PASS lock-in push waits for his champ: with auto-push on, locking Ahri while Syndra's pro builds are still loaded, or while his seat still shows the hover, pushes nothing and waits; builds load for the locked champion, then build 1 (runes and the item set for Ahri) goes in once, never another champion's, never before the lock or with it switched off, and it is remembered for the game");
        }

        private void SmokeBuildKept(List<string> checks, List<ProBuild> ahri, List<ProBuild> syndra)
        {
            EndGameBuild();
            lastSession = null;
            builds = ahri;
            PushBuild(2, false);
            for (int i = 0; i < 100 && pushedBuild == null; i++) Thread.Sleep(30);
            if (pushedBuild != ahri[1]) throw new Exception("\"push build 2\" (and the panel's second button) wasn't remembered as this game's build.");
            LiveData playing = League.Parse<LiveData>(SmokeBuildLive("Ahri", "", 300));
            string said = BuildAnswer(playing, false);
            if (said != "Pro build from Chovy and what pros build most: Malignance, Sorcerer's Shoes, Rabadon's Deathcap, Luden's Companion, Shadowflame, Zhonya's Hourglass.") throw new Exception("The build he pushed wasn't the one she read back: " + said);
            Lockout.LeaguePhase = "GameStart";
            builds = ahri;
            EndDraft();
            if (builds != null || BuildAnswer(playing, false) != said) throw new Exception("The pushed build was lost when champ select ended.");
            pushedBuild = null;
            if (BuildAnswer(playing, false) != SmokeFullBuild) throw new Exception("With nothing pushed, the draft's pro build 1 wasn't used: " + BuildAnswer(playing, false));
            pushedBuild = syndra[0];
            keptBuilds = syndra;
            if (BuildAnswer(playing, false) != null) throw new Exception("Syndra's build was read back while he plays Ahri.");
            KeepBuild(ahri[0]);
            keptBuilds = ahri;
            Lockout.LeaguePhase = "Lobby";
            EndDraft();
            if (pushedBuild != null || keptBuilds != null || BuildAnswer(playing, false) != null) throw new Exception("A dodged draft's build stayed for the next game.");
            checks.Add("PASS this game's build: a build pushed on lock-in, by \"push build N\" or by the panel's buttons is remembered through the end of champ select; with nothing pushed the draft's pro build 1 is used; another champion's build is never used, and a dodged draft forgets it");
        }

        private void SmokeBuildAnswer(List<string> checks)
        {
            foreach (string asked in new[] { "Jo Caprizo va what do I build give me the full build please.", "what do I build", "give me the full build", "what's my build", "whats my build?", "what should I buy next", "what's next item", "what's my next item", "what items?", "what items should I get", "Capri, what should I build now?", "remind me of the build", "what should I build against Zed", "what should I build in this game", "what's my item order" })
                if (!BuildAsked.IsMatch(asked)) throw new Exception("\"" + asked + "\" wasn't heard as a build ask.");
            foreach (string other in new[] { "push build 2", "build a house in minecraft", "what did you build today", "what's my cs", "what items does Zed have", "what's the best build", "what should we buy for dinner", "what's the build for Zed", "load the runes", "I need to buy groceries" })
                if (BuildAsked.IsMatch(other)) throw new Exception("\"" + other + "\" was heard as a build ask.");
            if (!PushWords.IsMatch("push build 2")) throw new Exception("\"push build 2\" stopped being the push command.");

            EndGameBuild();
            builds = null;
            List<string> downloads = new List<string>();
            string answer = SmokeBuildApi(SmokeBuildMatch(103, "Faker", 8112, 2000, SmokeFakerItems));
            ProBuilds.Download = delegate(string body) { lock (downloads) downloads.Add(body); return answer; };
            LiveData fresh = League.Parse<LiveData>(SmokeBuildLive("Ahri", "", 300));
            if (BuildAnswer(fresh, false) != null || downloads.Count != 0) throw new Exception("A build was given before any pro build was loaded.");
            string loaded = BuildAnswer(fresh, true);
            if (loaded != SmokeFullBuild || downloads.Count == 0 || !downloads[0].Contains("\"championId\":103") || !downloads[0].Contains("\"role\":\"mid\"")) throw new Exception("In a game with nothing loaded, Ahri's pro builds weren't fetched for his lane: " + loaded + " / " + string.Join(" || ", downloads.ToArray()));
            int fetched = downloads.Count;
            if (BuildAnswer(fresh, true) != SmokeFullBuild || downloads.Count != fetched) throw new Exception("The game's pro builds were fetched again for every ask.");
            LiveData two = League.Parse<LiveData>(SmokeBuildLive("Ahri", SmokeTwoItems, 900));
            string next = BuildAnswer(two, false);
            if (next != "You've got Luden's Companion and Sorcerer's Shoes from the pro build. Next: Shadowflame, then Rabadon's Deathcap, Zhonya's Hourglass, Void Staff.") throw new Exception("What he has and what comes next was said as: " + next);
            string named = BuildAnswer(League.Parse<LiveData>(SmokeBuildLive("Ahri", "{\"displayName\":\"Luden's Companion\"}", 600)), false);
            if (named != "You've got Luden's Companion from the pro build. Next: Sorcerer's Shoes, then Shadowflame, Rabadon's Deathcap, Zhonya's Hourglass, Void Staff.") throw new Exception("An item the game gave no id for wasn't matched by its name: " + named);
            string all = "{\"itemID\":6655},{\"itemID\":3020},{\"itemID\":4645},{\"itemID\":3089},{\"itemID\":3157},{\"itemID\":3135}";
            string done = BuildAnswer(League.Parse<LiveData>(SmokeBuildLive("Ahri", all, 2000)), false);
            if (done != "You've got the whole pro build already.") throw new Exception("A finished build was said as: " + done);
            answer = SmokeBuildApi();
            string none = BuildAnswer(League.Parse<LiveData>(SmokeBuildLive("Zed", "", 300)), true);
            if (none != "I couldn't find a pro build for Zed.") throw new Exception("With no pro build for his champ she said: " + none);
            foreach (string line in new[] { loaded, next, named, done, none })
                if (Regex.IsMatch(line, @"\b(they|them|their)\b", RegexOptions.IgnoreCase) || Regex.Matches(line, @"[.!?](\s|$)").Count > 2) throw new Exception("A build answer wasn't one or two short sentences to him as you: " + line);

            HudTurn(two);
            heard = null;
            if (!BuildHeard("Jo Caprizo va what do I build give me the full build please.", false) || heard != "(your build)") throw new Exception("His build ask in a game went to the model.");
            CloseTalk();
            if (BuildHeard("what do I build", true)) throw new Exception("The build answer talked over the ElevenLabs agent.");
            foreach (string other in new[] { "push build 2", "build a house in minecraft", "what did you build today" })
                if (BuildHeard(other, false)) throw new Exception("\"" + other + "\" was answered with his build.");
            heard = null;
            if (!ForkHeard("what should I buy next", false) || heard != "(your build)") throw new Exception("The build ask isn't answered before her brain.");
            CloseTalk();
            lastLiveAt = DateTime.Now.AddSeconds(-FreshLive - 5);
            if (BuildHeard("what do I build", false)) throw new Exception("An old game answered his build ask.");
            lastLive = null;
            if (BuildHeard("what do I build", false)) throw new Exception("A build ask outside a game was answered in code instead of from the champ select brief.");
            checks.Add("PASS build ask in game: \"what do I build\", \"give me the full build\", \"what should I buy next\", \"what's next item\" and the like (speech-to-text noise included) are answered in code before her brain with the pro build's items by name in buy order, what he already has and what comes next, loaded for his live champion when nothing was pushed (fetched once), and an honest line when no pro build exists; \"push build 2\", \"build a house in minecraft\" and \"what did you build today\" are left alone, never over the ElevenLabs agent or from an old game");
        }

        private void SmokeBuildBrief(List<string> checks, List<ProBuild> ahri, List<ProBuild> syndra)
        {
            EndLive();
            EndGameBuild();
            builds = null;
            List<string> downloads = new List<string>();
            ProBuilds.Download = delegate(string body) { lock (downloads) downloads.Add(body); return SmokeBuildApi(); };
            Lockout.LeaguePhase = "InProgress";
            LiveGame.Fetch = delegate { return SmokeBuildLive("Ahri", SmokeTwoItems, 900); };
            LiveTurn(true);
            if (League.Brief == null || !League.Brief.StartsWith(LiveGame.Opening, StringComparison.Ordinal) || League.Brief.Contains("Their build this game") || downloads.Count != 0) throw new Exception("With no build known the game brief named one, or the smoke fetched pro builds from the poll: " + League.Brief);
            KeepBuild(ahri[0]);
            LiveTurn(true);
            string brief = League.Brief ?? "";
            if (!brief.StartsWith(LiveGame.Opening, StringComparison.Ordinal) || !brief.Contains("Their build this game is Faker's pro Ahri build from a recent pro game, in buy order: Luden's Companion, Sorcerer's Shoes, Shadowflame, Rabadon's Deathcap, Zhonya's Hourglass, Void Staff.")
                || !brief.Contains("They have Luden's Companion and Sorcerer's Shoes; next is Shadowflame.") || !brief.Contains("never from general League knowledge or a search"))
                throw new Exception("Her game brief doesn't carry the pro build: " + brief);
            if (!Flavour.Extra().Contains("in buy order: Luden's Companion") || !LiveFacts.Block("what do I build next?").Contains("comes from that build")) throw new Exception("Her model prompt in a game doesn't carry the pro build, or still sends build questions to general knowledge.");
            KeepBuild(syndra[0]);
            LiveTurn(true);
            if ((League.Brief ?? "").Contains("Their build this game") || (League.Brief ?? "").Contains("Bdd")) throw new Exception("Another champion's build reached her game brief.");
            KeepBuild(ahri[0]);
            LiveTurn(true);
            EndLive();
            if (pushedBuild != null || keptBuilds != null) throw new Exception("The finished game kept its build for the next one.");
            checks.Add("PASS build in her game brief: in a game her model sees the same pro build (pro, buy order by name, what he has and what's next) and is told to answer build questions from it, never general knowledge or a search; nothing for another champion, no fetching from the smoke's game polls, and a finished game forgets its build");
        }
    }
}
