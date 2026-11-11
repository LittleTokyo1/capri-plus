using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // Capri+ smoke checks, run inside upstream's smoke test right after its updater checks.
    internal sealed partial class Companion
    {
        internal const string SmokeSession = "{\"localPlayerCellId\":2,\"myTeam\":[{\"cellId\":0,\"championId\":86,\"assignedPosition\":\"top\"},{\"cellId\":2,\"championId\":0,\"championPickIntent\":103,\"assignedPosition\":\"middle\"}],\"theirTeam\":[{\"cellId\":5,\"championId\":238},{\"cellId\":6,\"championId\":222},{\"cellId\":7,\"championId\":0}],\"bans\":{\"myTeamBans\":[157],\"theirTeamBans\":[]},\"timer\":{\"phase\":\"BAN_PICK\"}}";
        internal const string SmokeNames = "[{\"id\":103,\"name\":\"Ahri\",\"alias\":\"Ahri\"},{\"id\":238,\"name\":\"Zed\"},{\"id\":222,\"name\":\"Jinx\"},{\"id\":86,\"name\":\"Garen\"},{\"id\":157,\"name\":\"Yasuo\"},{\"id\":-1,\"name\":\"None\"}]";

        private void ForkSmoke(List<string> checks)
        {
            string folder = Path.Combine(Path.GetTempPath(), "capri-fork-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Study.Folder = Path.Combine(folder, "no-study");
            SmokeKept kept = SmokeKept.Take(this);
            try
            {
                Section(checks, kept, "SmokeDataKept", delegate { SmokeDataKept(checks); });
                Section(checks, kept, "SmokeShotAfter", delegate { SmokeShotAfter(checks); });
                Section(checks, kept, "SmokeRunner", delegate { SmokeRunner(checks, Path.Combine(folder, "runner")); });
                Section(checks, kept, "SmokeVoiceInMic", delegate { SmokeVoiceInMic(checks, folder); });
                Section(checks, kept, "SmokeCallMode", delegate { SmokeCallMode(checks); });
                Section(checks, kept, "SmokeCallLines", delegate { SmokeCallLines(checks); });
                Section(checks, kept, "SmokeCallPanel", delegate { SmokeCallPanel(checks, folder); });
            Section(checks, kept, "SmokeClones", delegate { SmokeClones(checks); });
            Section(checks, kept, "SmokeIcons", delegate { SmokeIcons(checks); });
            Section(checks, kept, "SmokeHusk", delegate { SmokeHusk(checks); });
            Section(checks, kept, "SmokeThrow", delegate { SmokeThrow(checks); });
            Section(checks, kept, "SmokePcTools", delegate { SmokePcTools(checks); });
            Section(checks, kept, "SmokeArrange", delegate { SmokeArrange(checks); });
            Section(checks, kept, "SmokeOwnTools", delegate { SmokeOwnTools(checks); });
            Section(checks, kept, "SmokeNameGate", delegate { SmokeNameGate(checks); });
            Section(checks, kept, "SmokeWarmth", delegate { SmokeWarmth(checks); });
            Section(checks, kept, "SmokeHerWays", delegate { SmokeHerWays(checks); });
            Section(checks, kept, "SmokeMood", delegate { SmokeMood(checks); });
            Section(checks, kept, "SmokeWarmNote", delegate { SmokeWarmNote(checks); });
            Section(checks, kept, "SmokeScreenPower", delegate { SmokeScreenPower(checks); });
            Section(checks, kept, "SmokeScene", delegate { SmokeScene(checks); });
            Section(checks, kept, "SmokeKeyLights", delegate { SmokeKeyLights(checks); });
            Section(checks, kept, "SmokeExplained", delegate { SmokeExplained(checks); });
            Section(checks, kept, "SmokeSchoolGmail", delegate { SmokeSchoolGmail(checks); });
            Section(checks, kept, "SmokeSchoolTriage", delegate { SmokeSchoolTriage(checks); });
            Section(checks, kept, "SmokeSchoolMailTool", delegate { SmokeSchoolMailTool(checks); });
            Section(checks, kept, "SmokeJobBadges", delegate { SmokeJobBadges(checks); });
            Section(checks, kept, "SmokeDigestReader", delegate { SmokeDigestReader(checks, folder); SmokeDigestMenu(checks); });
            Section(checks, kept, "SmokeDiagrams", delegate { SmokeDiagrams(checks); });
            Section(checks, kept, "SmokeStatusMoves", delegate { SmokeStatusMoves(checks); });
            Section(checks, kept, "SmokeStreamCohost", delegate { SmokeStreamCohost(checks); });
            Section(checks, kept, "SmokeOwnClock", delegate { SmokeOwnClock(checks); });
            Section(checks, kept, "SmokeClipThat", delegate { SmokeClipThat(checks); });
            Section(checks, kept, "SmokeClipChain", delegate { SmokeClipChain(checks); });
            Section(checks, kept, "SmokeStatusArt", delegate { SmokeStatusArt(checks); });
            Section(checks, kept, "SmokeHologram", delegate { SmokeHologram(checks); });
            Section(checks, kept, "SmokeLiveReactions", delegate { SmokeLiveReactions(checks); });
            Section(checks, kept, "SmokeFreeKeysSwitch", delegate { SmokeFreeKeysSwitch(checks); });
            Section(checks, kept, "SmokeFreeKeysRow", delegate { SmokeFreeKeysRow(checks); });
            Section(checks, kept, "SmokeNoDucking", delegate { SmokeNoDucking(checks); });
            Section(checks, kept, "SmokeEarsTar", delegate { SmokeEarsTar(checks); });
            Section(checks, kept, "SmokeFreezeLog", delegate { SmokeFreezeLog(checks); });
            Section(checks, kept, "SmokeAdminKeys", delegate { SmokeAdminKeys(checks); });
            Section(checks, kept, "SmokeNoBrainLines", delegate { SmokeNoBrainLines(checks); });
            Section(checks, kept, "SmokeProjectRun", delegate { SmokeProjectRun(checks); });
            Section(checks, kept, "SmokeProjectHook", delegate { SmokeProjectHook(checks); });
            Section(checks, kept, "SmokeProjectRelay", delegate { SmokeProjectRelay(checks); });
            Section(checks, kept, "SmokeClaudeToken", delegate { SmokeClaudeToken(checks); });
            Section(checks, kept, "SmokeProjectAnswer", delegate { SmokeProjectAnswer(checks); });
            Section(checks, kept, "SmokeProjectRefresh", delegate { SmokeProjectRefresh(checks); });
            Section(checks, kept, "SmokeProjectLand", delegate { SmokeProjectLand(checks); });
            Section(checks, kept, "SmokeUpstreamWatch", delegate { SmokeUpstreamWatch(checks); });
            Section(checks, kept, "SmokeIdeas", delegate { SmokeIdeas(checks); });
            Section(checks, kept, "SmokeProjectWatch", delegate { SmokeProjectWatch(checks); });
            Section(checks, kept, "SmokeProjectPropose", delegate { SmokeProjectPropose(checks); });
            Section(checks, kept, "SmokeEverySetting", delegate { SmokeEverySetting(checks); });
            Section(checks, kept, "SmokeAutoQuiet", delegate { SmokeAutoQuiet(checks); });
                Section(checks, kept, "SmokeSettings", delegate { SmokeSettings(checks, folder); });
                Section(checks, kept, "SmokeLockout", delegate { SmokeLockout(checks, folder); });
                Section(checks, kept, "SmokeSpend", delegate { SmokeSpend(checks); });
                Section(checks, kept, "SmokeFlavour", delegate { SmokeFlavour(checks); });
                Section(checks, kept, "SmokeLeague", delegate { SmokeLeague(checks); });
                Section(checks, kept, "SmokeEyesAndKeys", delegate { SmokeEyesAndKeys(checks); });
                Section(checks, kept, "SmokeBigJobs", delegate { SmokeBigJobs(checks, folder); });
                Section(checks, kept, "SmokeWindow", delegate { SmokeWindow(checks); });
                Section(checks, kept, "SmokeGuards", delegate { SmokeGuards(checks); });
                Section(checks, kept, "SmokeProBuilds", delegate { SmokeProBuilds(checks); });
                Section(checks, kept, "SmokeDraft", delegate { SmokeDraft(checks); });
                Section(checks, kept, "SmokeCounters", delegate { SmokeCounters(checks); });
                Section(checks, kept, "SmokeCsHud", delegate { SmokeCsHud(checks); });
                Section(checks, kept, "SmokeObjectives", delegate { SmokeObjectives(checks); });
                Section(checks, kept, "SmokeLeagueAway", delegate { SmokeLeagueAway(checks); });
                Section(checks, kept, "SmokeDraftWait", delegate { SmokeDraftWait(checks); });
                Section(checks, kept, "SmokeBuildAsk", delegate { SmokeBuildAsk(checks); });
                Section(checks, kept, "SmokeStudy", delegate { SmokeStudy(checks); });
                Section(checks, kept, "SmokeStudyJobs", delegate { SmokeStudyJobs(checks); });
                Section(checks, kept, "SmokeSchoolMail", delegate { SmokeSchoolMail(checks); });
                Section(checks, kept, "SmokeOpenAsks", delegate { SmokeOpenAsks(checks); });
                Section(checks, kept, "SmokePhone", delegate { SmokePhone(checks); });
                Section(checks, kept, "SmokePhoneTurns", delegate { SmokePhoneTurns(checks); });
                Section(checks, kept, "SmokePhoneLive", delegate { SmokePhoneLive(checks); });
                Section(checks, kept, "SmokePhoneHistory", delegate { SmokePhoneHistory(checks); });
                Section(checks, kept, "SmokePhoneWorker", delegate { SmokePhoneWorker(checks); });
                Section(checks, kept, "SmokePhoneFlipped", delegate { SmokePhoneFlipped(checks); });
                Section(checks, kept, "SmokePhoneView", delegate { SmokePhoneView(checks); });
                Section(checks, kept, "SmokePhoneViewWiring", delegate { SmokePhoneViewWiring(checks); });
                Section(checks, kept, "SmokePhoneButtons", delegate { SmokePhoneButtons(checks); });
                Section(checks, kept, "SmokePhoneKeys", delegate { SmokePhoneKeys(checks); });
                Section(checks, kept, "SmokeShortcutActions", delegate { SmokeShortcutActions(checks); });
                Section(checks, kept, "SmokeShortcutServer", delegate { SmokeShortcutServer(checks); });
                Section(checks, kept, "SmokeShortcutWiring", delegate { SmokeShortcutWiring(checks); });
                Section(checks, kept, "SmokePhonePage", delegate { SmokePhonePage(checks); });
                Section(checks, kept, "SmokePhoneVoiceServer", delegate { SmokePhoneVoiceServer(checks); });
                Section(checks, kept, "SmokePhoneVoiceTurn", delegate { SmokePhoneVoiceTurn(checks); });
                Section(checks, kept, "SmokeOfferTruth", delegate { SmokeOfferTruth(checks); });
                Section(checks, kept, "SmokeAfterJobs", delegate { SmokeAfterJobs(checks); });
                Section(checks, kept, "SmokeClaims", delegate { SmokeClaims(checks, folder); });
                Section(checks, kept, "SmokeNotSilent", delegate { SmokeNotSilent(checks); });
                Section(checks, kept, "SmokeAutoReply", delegate { SmokeAutoReply(checks); });
                Section(checks, kept, "SmokeUnderstandReplay", delegate { SmokeUnderstandReplay(checks); });
                Section(checks, kept, "SmokeGameSpot", delegate { SmokeGameSpot(checks); });
                Section(checks, kept, "SmokeChats", delegate { SmokeChats(checks); });
                Section(checks, kept, "SmokeScreens", delegate { SmokeScreens(checks); });
                Section(checks, kept, "SmokeLookup", delegate { SmokeLookup(checks); });
                Section(checks, kept, "SmokeWebApps", delegate { SmokeWebApps(checks); });
                Section(checks, kept, "SmokeTweaks", delegate { SmokeTweaks(checks); });
                Section(checks, kept, "SmokeVoiceTurn", delegate { SmokeVoiceTurn(checks); });
                Section(checks, kept, "SmokeThread", delegate { SmokeThread(checks); });
                Section(checks, kept, "SmokeBlackwall", delegate { SmokeBlackwall(checks); });
                Section(checks, kept, "SmokeMemoryRoute", delegate { SmokeMemoryRoute(checks); });
                Section(checks, kept, "SmokeChatterbox", delegate { SmokeChatterbox(checks); });
                Section(checks, kept, "SmokeMonitors", delegate { SmokeMonitors(checks); });
                Section(checks, kept, "SmokeMicMeter", delegate { SmokeMicMeter(checks); });
                Section(checks, kept, "SmokeHeldLines", delegate { SmokeHeldLines(checks); });
                Section(checks, kept, "SmokeBrowserRecall", delegate { SmokeBrowserRecall(checks, folder); });
                Section(checks, kept, "SmokeClaudeCli", delegate { SmokeClaudeCli(checks); });
                Section(checks, kept, "SmokeGoodnight", delegate { SmokeGoodnight(checks); });
                Section(checks, kept, "SmokeChatNotes", delegate { SmokeChatNotes(checks); });
                Section(checks, kept, "SmokeTidyFirst", delegate { SmokeTidyFirst(checks); });
                Section(checks, kept, "SmokeVault", delegate { SmokeVault(checks); });
                Section(checks, kept, "SmokeNameResolution", delegate { SmokeNameResolution(checks); });
                Section(checks, kept, "SmokeNameForms", delegate { SmokeNameForms(checks); });
                Section(checks, kept, "SmokeDayJournal", delegate { SmokeDayJournal(checks); });
                Section(checks, kept, "SmokeNow", delegate { SmokeNow(checks); });
                Section(checks, kept, "SmokeUnderstandFixes", delegate { SmokeUnderstandFixes(checks); });
                Section(checks, kept, "SmokeProactiveGate", delegate { SmokeProactiveGate(checks); });
                Section(checks, kept, "SmokeGateSources", delegate { SmokeGateSources(checks); });
                Section(checks, kept, "SmokeGenerators", delegate { SmokeGenerators(checks); });
                Section(checks, kept, "SmokeOwnChats", delegate { SmokeOwnChats(checks); });
                Section(checks, kept, "SmokeFriendMode", delegate { SmokeFriendMode(checks); });
                Section(checks, kept, "SmokeInstantMemory", delegate { SmokeInstantMemory(checks); });
                Section(checks, kept, "SmokeForgetting", delegate { SmokeForgetting(checks); });
                Section(checks, kept, "SmokeEveryPathContext", delegate { SmokeEveryPathContext(checks); });
                Section(checks, kept, "SmokeGoogle", delegate { SmokeGoogle(checks); });
                Section(checks, kept, "SmokeLeagueSocial", delegate { SmokeLeagueSocial(checks); });
                Section(checks, kept, "SmokeHeartbeat", delegate { SmokeHeartbeat(checks); });
                Section(checks, kept, "SmokeAutonomy", delegate { SmokeAutonomy(checks); });
                Section(checks, kept, "SmokeNight", delegate { SmokeNight(checks); });
                Section(checks, kept, "SmokeNightly", delegate { SmokeNightly(checks); });
                Section(checks, kept, "SmokeDiscord", delegate { SmokeDiscord(checks); });
                Section(checks, kept, "SmokeMoveByVoice", delegate { SmokeMoveByVoice(checks); });
                Section(checks, kept, "SmokeWalkSpeed", delegate { SmokeWalkSpeed(checks); });
                Section(checks, kept, "SmokeLiveGame", delegate { SmokeLiveGame(checks); });
                Section(checks, kept, "SmokeVoiceBrain", delegate { SmokeVoiceBrain(checks); });
                Section(checks, kept, "SmokeSpendBook", delegate { SmokeSpendBook(checks, folder); });
                Section(checks, kept, "SmokeBrowser", delegate { SmokeBrowser(checks, folder); });
                Section(checks, kept, "SmokeStillQuiet", delegate { SmokeStillQuiet(checks); });
                Section(checks, kept, "SmokeLogFixes", delegate { SmokeLogFixes(checks, folder); });
                Section(checks, kept, "SmokePet", delegate { SmokePet(checks, folder); });
                Section(checks, kept, "SmokeOneSender", delegate { SmokeOneSender(checks); });
                Section(checks, kept, "SmokeArrivals", delegate { SmokeArrivals(checks); });
                Section(checks, kept, "SmokeReplay", delegate { SmokeReplay(checks); });
                Section(checks, kept, "SmokeMemoryPass", delegate { SmokeMemoryPass(checks); });
                Section(checks, kept, "SmokeDid", delegate { SmokeDid(checks); });
                Section(checks, kept, "SmokePlanLife", delegate { SmokePlanLife(checks); });
            }
            finally
            {
                kept.Put(this);
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private static void SmokeSettings(List<string> checks, string folder)
        {
            ForkSettings.Load(folder);
            ForkSettings settings = ForkSettings.Current;
            if (settings.Cap != 10 || settings.Proactive != 1 || settings.Japanese != 1 || !settings.HerChoiceName || settings.GameVision != 2 || settings.PauseVk != 0x50 || settings.HoldVk != 0 || settings.Games.Length != ForkSettings.StandardGames.Length || !settings.PlanJobs || settings.PlanModel != "opus" || settings.VoiceBrain != 0 || !settings.BargeIn)
                throw new Exception("Capri+ defaults are wrong.");
            settings.Spent = 1.5;
            settings.Japanese = 2;
            settings.Games = new[] { "deadlock", "valorant" };
            ForkSettings.Save();
            ForkSettings back = ForkSettings.Read(ForkSettings.FilePath);
            if (back == null || back.Spent != 1.5 || back.Japanese != 2 || back.Games.Length != 2 || back.Games[1] != "valorant" || back.Cap != 10 || back.PauseVk != 0x50) throw new Exception("fork.json didn't round-trip.");
            back.Month = "1999-01";
            back.Tidy();
            if (back.Spent != 0 || back.Month == "1999-01") throw new Exception("A new month didn't reset the spend.");
            File.WriteAllText(ForkSettings.FilePath, "{\"Japanese\":0}");
            ForkSettings old = ForkSettings.Read(ForkSettings.FilePath);
            if (old == null || old.Japanese != 0 || old.Cap != 10 || old.LookVk != 0x4C || old.Games == null || old.Games.Length != ForkSettings.StandardGames.Length || !old.BargeIn || old.VoiceBrain != 0) throw new Exception("An older fork.json lost its defaults.");
            File.WriteAllText(ForkSettings.FilePath, "not json");
            if (ForkSettings.Read(ForkSettings.FilePath) != null) throw new Exception("A broken fork.json was read.");
            checks.Add("PASS capri+ settings: defaults, fork.json round trip, missing fields keep defaults, a broken file falls back, a new month resets spend");
        }

        private void SmokeLockout(List<string> checks, string folder)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            ActivityLog.FilePath = Path.Combine(folder, "activity.log");
            Lockout.LeaguePhase = null;
            Lockout.Running = delegate { return false; };
            Lockout.Recheck();
            if (Lockout.Active || Lockout.Refuse("open_app") != null) throw new Exception("Her hands locked with no game running.");
            Lockout.Running = delegate(string name) { return name == "deadlock"; };
            Lockout.Recheck();
            if (!Lockout.Active || Lockout.Game != "deadlock") throw new Exception("A running game wasn't seen.");
            foreach (string tool in new[] { "use_computer", "shortcut" })
                if (Lockout.Refuse(tool) != Lockout.Refusal) throw new Exception(tool + " wasn't refused mid-game.");
            foreach (string tool in new[] { "app_window", "power", "open_app", "open_website", "play_music", "look_at_screen", "remember", "media" })
                if (Lockout.Refuse(tool) != null) throw new Exception(tool + " was refused mid-game.");
            string blocked = DoTool("shortcut", new ToolArguments { Name = "capri-smoke-no-such-shortcut" }, asking);
            if (blocked != Lockout.Refusal) throw new Exception("DoTool ran a shortcut during a game: " + blocked);
            string launched = DoTool("open_app", new ToolArguments { Name = "capri-smoke-no-such-app" }, asking);
            if (launched == Lockout.Refusal || !Hands.Failed(launched)) throw new Exception("DoTool refused or mishandled open_app during a game: " + launched);
            if (!Lockout.MediaKey(0xB3) || !Lockout.MediaKey(0xAD) || Lockout.MediaKey(0x41) || Lockout.MediaKey(0x0D)) throw new Exception("Media keys were told apart wrong.");
            Lockout.Running = delegate { return false; };
            Lockout.LeaguePhase = "ChampSelect";
            Lockout.Recheck();
            if (Lockout.Game != "League of Legends") throw new Exception("League champ select didn't lock her hands.");
            ForkSettings.Current.LockLeagueClient = false;
            Lockout.Recheck();
            if (Lockout.Active) throw new Exception("Champ select locked her hands with that switched off.");
            ForkSettings.Current.LockLeagueClient = true;
            Lockout.LeaguePhase = "Lobby";
            Lockout.Recheck();
            if (Lockout.Active) throw new Exception("The League lobby locked her hands.");
            Lockout.LeaguePhase = null;
            Lockout.Recheck();
            string log = File.ReadAllText(ActivityLog.FilePath);
            if (!Regex.IsMatch(log, @"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d  blocked shortcut  capri-smoke-no-such-shortcut  ->  game running: deadlock\r?$", RegexOptions.Multiline)) throw new Exception("The activity log missed the blocked tool: " + log);
            ActivityLog.Write("open_app", ActivityLog.Describe(new ToolArguments { Name = "Spotify" }), "Opened Spotify.");
            if (!Regex.IsMatch(File.ReadAllText(ActivityLog.FilePath), @"  open_app  Spotify  ->  Opened Spotify\.\r?\n$")) throw new Exception("An action line was written wrong.");
            long size = new FileInfo(ActivityLog.FilePath).Length;
            ForkSettings.Current.LogActions = false;
            ActivityLog.Write("open_app", "Steam", "Opened Steam.");
            if (new FileInfo(ActivityLog.FilePath).Length != size) throw new Exception("The activity log kept writing after it was switched off.");
            ForkSettings.Current.LogActions = true;
            checks.Add("PASS game lockout: a running game or League champ select refuses only the keyboard and mouse tools (jobs and shortcuts) through DoTool, while opening apps and sites, music, window changes, talking, looking and media keys still work, the lobby doesn't lock, and blocks and actions land in the activity log");
        }

        private static void SmokeSpend(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            if (Spend.Price("claude-haiku-5-5")[0] != 0.10 || Spend.Price("claude-opus-5-5")[1] != 20 || Spend.Price("claude-something-new")[0] != 5) throw new Exception("Model prices are wrong.");
            double cost = Spend.Cost("claude-haiku-5-5", "{\"usage\":{\"input_tokens\":1000,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":2000,\"output_tokens\":100,\"server_tool_use\":{\"web_search_requests\":2}}}");
            if (Math.Abs(cost - 0.02017) > 1e-9) throw new Exception("A reply's cost came out at " + cost + ".");
            ForkSettings.Current.Cap = 0.0002;
            Spend.Charge("{\"model\":\"claude-haiku-5-5\"}", "{\"usage\":{\"input_tokens\":1000,\"output_tokens\":100}}");
            if (Spend.Blocked) throw new Exception("The cap blocked her too early.");
            Spend.Charge("{\"model\":\"claude-haiku-5-5\"}", "{\"usage\":{\"input_tokens\":1000,\"output_tokens\":100}}");
            if (!Spend.Blocked) throw new Exception("The cap didn't block her once spent.");
            Brain capped = new Brain();
            capped.Remote = new Cloud("sk-ant-test", "claude-haiku-5-5");
            capped.Model = "claude-haiku-5-5";
            try
            {
                capped.Ask("hi");
                throw new Exception("A paid call went out past the cap.");
            }
            catch (BrainException error) { if (error.Message != Spend.CapNote) throw new Exception("Past the cap she said: " + error.Message); }
            if (League.Research(new Cloud("sk-ant-test", "claude-haiku-5-5"), "counters?", true) != null) throw new Exception("League research ran past the cap.");
            ForkSettings.Current.Cap = 0;
            ForkSettings.Current.Spent = 0;
            if (!Spend.Blocked) throw new Exception("A $0 cap allowed paid calls.");
            if (Brain.DefaultCloudModel("anthropic", new List<string> { "claude-opus-5-5", "claude-haiku-5-5" }) != "claude-haiku-5-5") throw new Exception("A Claude key doesn't default to Haiku 5.5.");
            if (Brain.DefaultCloudModel("anthropic", new List<string> { "claude-opus-5-5" }) != "claude-opus-5-5") throw new Exception("Without Haiku 5.5 a Claude key lost its fallback.");
            checks.Add("PASS spend: prices, cache and web search costs, the monthly cap stops paid calls before they go out (chat and League research), and a Claude key starts on Haiku 5.5");
        }

        private static void SmokeFlavour(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            if (Flavour.Japanese(0, false) != "") throw new Exception("Japanese off still added Japanese.");
            string some = Flavour.Japanese(1, false), lots = Flavour.Japanese(2, true);
            if (!some.Contains("kuso") || !some.Contains("one reply in five") || some.Contains("Tokyo teen")) throw new Exception("Japanese exclamations read wrong: " + some);
            if (!lots.Contains("Tokyo teen") || !lots.Contains("every other reply") || !lots.Contains("ukeru")) throw new Exception("Japanese slang read wrong: " + lots);
            if (Flavour.Naming(true, null) == "" || Flavour.Naming(true, "Sam") != "" || Flavour.Naming(false, null) != "") throw new Exception("Her-choice nicknames read wrong.");
            string keptNick = Brain.Nickname;
            Brain.Nickname = null;
            League.Brief = "You're mid on Ahri.";
            string character = Brain.Character;
            if (!character.Contains("kuso") || !character.Contains("champ select right now. You're mid on Ahri.") || !character.Contains("Nicknames are your call")) throw new Exception("Her prompt is missing Capri+ lines.");
            ForkSettings.Current.Japanese = 0;
            ForkSettings.Current.HerChoiceName = false;
            League.Brief = null;
            character = Brain.Character;
            if (character.Contains("Japanese flavour") || character.Contains("champ select") || character.Contains("Nicknames are your call")) throw new Exception("Switched-off Capri+ lines stayed in her prompt.");
            Brain.Nickname = keptNick;
            checks.Add("PASS personality: Japanese off, exclamations and slang levels, mild Japanese curses pass the filter, her-choice nicknames, and champ select context reach her prompt");
        }

        private static void SmokeLeague(List<string> checks)
        {
            string[] found = League.ReadLockfile("LeagueClient:12345:54321:abcDEF123:https");
            if (found == null || found[0] != "54321" || found[1] != "abcDEF123") throw new Exception("The League lockfile was read wrong.");
            if (League.ReadLockfile("garbage") != null || League.ReadLockfile("a:b:0:x:https") != null || League.ReadLockfile(null) != null) throw new Exception("A bad lockfile was accepted.");
            League fake = new League();
            fake.LoadNames(SmokeNames);
            fake.Get = delegate(string path) { return path == "/lol-gameflow/v1/gameflow-phase" ? "\"ChampSelect\"" : path == "/lol-champ-select/v1/session" ? SmokeSession : null; };
            if (fake.ReadPhase() != "ChampSelect") throw new Exception("The game phase was read wrong.");
            ChampSession session = fake.ReadSession();
            string described = fake.Describe(session);
            if (described != "You're mid on Ahri. Your team: Garen (top). Enemy so far: Zed, Jinx. Bans: Yasuo.") throw new Exception("The draft was described as: " + described);
            if (League.EnemyCount(session) != 2 || League.EnemyCount(null) != 0) throw new Exception("Enemy picks were counted wrong.");
            if (League.ParseSession("not json") != null) throw new Exception("A broken session was parsed.");
            if (!League.ResearchAsk(described).Contains(described)) throw new Exception("Research doesn't say the draft.");
            string answer = League.Answer("{\"content\":[{\"type\":\"server_tool_use\",\"id\":\"x\",\"name\":\"web_search\",\"input\":{\"query\":\"ahri counters\"}},{\"type\":\"web_search_tool_result\",\"tool_use_id\":\"x\",\"content\":[]},{\"type\":\"text\",\"text\":\"Pick Galio \"},{\"type\":\"text\",\"text\":\"into Zed.\"}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":10,\"output_tokens\":5}}");
            if (answer != "Pick Galio into Zed.") throw new Exception("Research text was read as: " + answer);
            if (League.Research(new Cloud("nvapi-test", "meta/llama"), "counters?", true) != null) throw new Exception("League research tried a non-Claude brain.");
            if (League.Role("utility") != "support" || League.Role("bottom") != "ADC (bot)" || League.Role("") != null) throw new Exception("Roles were named wrong.");
            checks.Add("PASS league coach: lockfile, game phase, champ select picks, roles and bans become one line for her, web research text is read from Claude's reply, and only a Claude brain researches");
        }

        private void SmokeEyesAndKeys(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            Lockout.Running = delegate { return false; };
            Lockout.LeaguePhase = null;
            Lockout.Recheck();
            preferences.Watch = true;
            Foreground = delegate { return "chrome Swedbank - internet bank"; };
            if (ForkWatching()) throw new Exception("She looked at a blocklisted app.");
            Foreground = delegate { return "chrome YouTube"; };
            if (!ForkWatching()) throw new Exception("She stopped watching the desktop.");
            ForkSettings.Current.DesktopVision = false;
            if (ForkWatching()) throw new Exception("Desktop vision off didn't stop her looking.");
            ForkSettings.Current.DesktopVision = true;
            ForkSettings.Current.Paused = true;
            if (ForkWatching()) throw new Exception("She looked while paused.");
            ForkSettings.Current.Paused = false;
            ForkSettings.Current.Proactive = 0;
            if (ForkWatching()) throw new Exception("Quiet mode still glanced on its own.");
            ForkSettings.Current.Proactive = 1;
            if (!ForkAsk("Look.").Contains("SKIP")) throw new Exception("Key moments didn't tell her to skip small stuff.");
            string keyAsk = ForkAsk(Glances.Line);
            if (!keyAsk.Contains("quiet moment is NOT enough") || !keyAsk.Contains("overrides the lines above") || !keyAsk.Contains("Most glances are SAY: SKIP")) throw new Exception("Key moments still let a quiet scene be enough: " + keyAsk);
            keySpokeAt = double.NaN;
            if (!ForkGlanceQuiet("Nothing big has popped off yet, so SKIP.", 10)) throw new Exception("A glance line with SKIP inside it was spoken.");
            if (ForkGlanceQuiet("Penta kill, choom!", 1000) || !ForkGlanceQuiet("Baron stolen, preem!", 1060) || ForkGlanceQuiet("You won, choom!", 1000 + KeyGlanceGap)) throw new Exception("Key moments spoke glances closer than " + KeyGlanceGap + " s apart, or never again.");
            ForkSettings.Current.Proactive = 2;
            if (ForkGlanceQuiet("Nice dodge.", 2000) || ForkGlanceQuiet("Another dodge.", 2001)) throw new Exception("Chatty mode held glance lines back.");
            if (!ForkGlanceQuiet("SKIP", 2002)) throw new Exception("Chatty mode spoke a SKIP.");
            keySpokeAt = double.NaN;
            checks.Add("PASS key moments glances: no quiet scene is enough, a line with SKIP inside stays silent, at most one spoken glance every " + KeyGlanceGap + " s; chatty mode is not held back");
            if (ForkAsk("Look.") != "Look.") throw new Exception("Chatty mode changed her glance.");
            if (ForkGap(120) != 120) throw new Exception("The desktop glance gap changed.");
            Lockout.Running = delegate(string name) { return name == "deadlock"; };
            Lockout.Recheck();
            ForkSettings.Current.GameVision = 1;
            if (ForkWatching()) throw new Exception("Only-when-asked still glanced in game.");
            ForkSettings.Current.GameVision = 3;
            ForkSettings.Current.GameSeconds = 7;
            if (!ForkWatching() || ForkGap(120) != 7) throw new Exception("Every-N-seconds didn't watch the game every 7 s.");
            ForkSettings.Current.GameVision = 2;
            if (ForkGap(120) != 21) throw new Exception("Adaptive game watching wasn't 3x N.");
            Lockout.LeaguePhase = "ChampSelect";
            Lockout.Running = delegate { return false; };
            Lockout.Recheck();
            if (ForkWatching()) throw new Exception("She glanced in champ select, where the client already tells her everything.");
            Lockout.LeaguePhase = null;
            Lockout.Recheck();
            KeyIsDown = delegate(int key) { return key == 0x06 || key == 0x11; };
            if (!Pressed(0x06, 0) || !Pressed(0x06, 2) || Pressed(0x06, 1) || Pressed(0, 0) || Pressed(0x50, 2) || Pressed(0x06, 8)) throw new Exception("Hotkeys matched wrong.");
            ForkSettings.Current.VoiceBudget = 100;
            ForkSettings.Current.VoiceChars = 100;
            if (!VoiceSpent()) throw new Exception("A used-up voice budget kept ElevenLabs on.");
            ForkSettings.Current.VoiceBudget = 0;
            if (VoiceSpent()) throw new Exception("No voice budget still switched to the free voice.");
            if (!Blocked("KeePassXC Passwords", new[] { "keepass" }) || Blocked("", new[] { "x" }) || Blocked("Steam", null)) throw new Exception("The blocklist matched wrong.");
            checks.Add("PASS capri+ eyes and keys: blocklist, pause, quiet, desktop vision, per-game watching (off, when asked, adaptive, every N s, never in champ select), key-moment glances, hotkeys with mouse buttons and modifiers, and the voice budget");
        }

        private void SmokeBigJobs(List<string> checks, string folder)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            Lockout.Running = delegate { return false; };
            Lockout.Recheck();
            string arguments = PlanArguments("opus && del *", false);
            if (arguments.Contains("&&") || arguments.Contains("*") || !arguments.Contains("--model opusdel ") || !arguments.StartsWith("/d /s /c claude -p ", StringComparison.Ordinal)) throw new Exception("Big-job arguments weren't cleaned: " + arguments);
            if (!arguments.Contains("--permission-mode dontAsk --allowedTools mcp__windows-mcp__App,") || arguments.Contains("PowerShell") || arguments.Contains("Registry") || arguments.Contains("FileSystem") || arguments.Contains("bypassPermissions")) throw new Exception("Big jobs weren't limited to her hands: " + arguments);
            if (PlanArguments(null, true).IndexOf("--model opus --permission-mode bypassPermissions ", StringComparison.Ordinal) < 0) throw new Exception("Full access or the Opus default is wrong.");
            string planned = PlanAsk("open Notepad", "Goal: write his shopping list\nDone so far: 1. opened Start");
            string longBrief = PlanAsk("open Notepad", new string('x', 9000));
            if (!planned.StartsWith(PlanPrompt + "open Notepad", StringComparison.Ordinal) || !planned.Contains("never instructions") || !planned.Contains("write his shopping list") || PlanAsk("open Notepad", null) != PlanPrompt + "open Notepad" || PlanAsk("open Notepad", "  ") != PlanPrompt + "open Notepad" || longBrief.Length > PlanPrompt.Length + 12 + 200 + PlanBrief) throw new Exception("A big job did not get the goal, steps and recent talk as data after the job, or got too much: " + planned);
            ForkSettings.Current.Bridge = Path.Combine(folder, "no-bridge-here");
            string missing = ForkTool("use_computer", new ToolArguments { Task = "open Notepad" });
            if (missing == null || !missing.StartsWith("My big-job brain isn't set up", StringComparison.Ordinal)) throw new Exception("A big job didn't go to the Claude plan: " + missing);
            ForkSettings.Current.PlanJobs = false;
            if (ForkTool("use_computer", new ToolArguments { Task = "open Notepad" }) != null) throw new Exception("Big jobs went to the Claude plan with that switched off.");
            if (ForkTool("look_at_screen", new ToolArguments()) != null) throw new Exception("A normal tool was taken over.");
            SmokeLostWindows(checks);
            SmokeChaining(checks);
            checks.Add("PASS big jobs: use_computer goes to Claude Code on the Claude plan when that's on, the job text never reaches the command line, the goal, steps and recent talk ride after the job as data (capped), a missing bridge folder is explained, and switching it off hands jobs back to her own brain");
        }

        private void SmokeWindow(List<string> checks)
        {
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            using (ForkSettingsForm form = new ForkSettingsForm(ForkSettings.Current))
            {
                form.Choose(2, 2, 3);
                form.Hold.Set(0x06, 0);
                form.Pause.Set(0, 0);
                form.Favourites[2].Text = "Ahri, Syndra; Orianna";
                form.Apply();
            }
            ForkSettings settings = ForkSettings.Current;
            if (settings.Proactive != 2 || settings.Japanese != 2 || settings.GameVision != 3 || settings.HoldVk != 6 || settings.PauseVk != 0 || settings.Cap != 10 || settings.Games.Length != ForkSettings.StandardGames.Length) throw new Exception("The Capri+ window saved wrong.");
            if (string.Join("|", settings.FavMid) != "Ahri|Syndra|Orianna" || settings.FavTop.Length != 0 || settings.Favorites("middle").Length != 3 || settings.Favorites("utility").Length != 0) throw new Exception("Favourite champs saved wrong.");
            if (!settings.HoverFavorite || !settings.ShowBuilds || !settings.PushButton || !settings.PushVoice || settings.PushOnLock || settings.PushSpells || settings.PlanFullAccess || settings.GameMode != 3) throw new Exception("League and big-job defaults are wrong.");
            ApplyForkSettings();
            if (!preferences.CheckIns) throw new Exception("Chatty mode didn't turn check-ins on.");
            if (KeyBox.Describe(0x50, 3) != "Ctrl + Alt + P" || KeyBox.Describe(0x06, 0) != "Mouse 5 (forward)" || KeyBox.Describe(0, 0) != "Not set" || KeyBox.Held(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) != 6)
                throw new Exception("Hotkeys were named wrong.");
            checks.Add("PASS capri+ settings window: every option saves, hotkeys bind mouse buttons and clear, and chatty mode turns check-ins on");
        }

        private static void SmokeChaining(List<string> checks)
        {
            Func<bool> gamingWas = Chaining.Gaming;
            try
            {
                Chaining.Gaming = delegate { return false; };
                if (Chaining.Next("NEXT: click Arcane and play episode 1") != "click Arcane and play episode 1" || Chaining.Next("DONE") != null || Chaining.Next("next: open it") != "open it" || Chaining.Next("Sure! NEXT:") != null) throw new Exception("The next step was read wrong.");
                foreach (string held in new[] { "send hi to Sam on Discord", "reply to his message", "post it on twitter", "email the file to Jan", "close Chrome", "delete the old screenshots", "uninstall Steam", "buy the skin", "quit the game", "remove the folder" })
                    if (!Chaining.Held(held)) throw new Exception("A chained step that sends, closes, deletes or buys was not held: " + held);
                foreach (string free in new[] { "click Arcane and play episode 1", "open the Downloads folder", "scroll to the settings tab", "open the school email from Marsh in the list of recent mails and read its full text", "read the regrade email" })
                    if (Chaining.Held(free)) throw new Exception("A harmless next step was held: " + free);

                List<string> ran = new List<string>();
                List<string> told = new List<string>();
                int judged = 0;
                Func<string, string> act = delegate(string task) { ran.Add(task); return "Done: " + task + "."; };
                Func<string> facts = delegate { return "Steps so far: " + string.Join("; ", ran.ToArray()); };
                Action<string> tell = delegate(string line) { told.Add(line); };
                Func<string, string> always = delegate(string known) { judged++; return "NEXT: step " + judged; };
                string all = Chaining.Run("put on Arcane", "Opened Netflix.", act, facts, always, tell);
                if (ran.Count != Chaining.MostJobs - 1 || told.Count != ran.Count || !all.StartsWith("Opened Netflix.", StringComparison.Ordinal) || !all.Contains("Done: step 2.") || !all.Contains("check with you")) throw new Exception("Chaining did not stop after " + Chaining.MostJobs + " jobs in a row and say so: " + all + " | " + string.Join(" / ", ran.ToArray()));

                ran.Clear(); told.Clear();
                string finished = Chaining.Run("open Notepad", "Opened Notepad.", act, facts, delegate { return "DONE"; }, tell);
                if (finished != "Opened Notepad." || ran.Count != 0 || told.Count != 0) throw new Exception("A finished goal still chained a job.");

                judged = 0;
                foreach (string first in new[] { "That didn't work: Netflix wouldn't open.", Computer.Escaped, Lockout.Refusal, "Which profile should I pick?" })
                    if (Chaining.Run("put on Arcane", first, act, facts, always, tell) != first) throw new Exception("A failed, stopped, refused or asking job still chained: " + first);
                if (judged != 0 || ran.Count != 0) throw new Exception("A failed or asking job was even judged.");

                string heldStep;
                string asks = Chaining.Run("tell Sam I'm late", "Opened Discord.", act, facts, delegate { return "NEXT: send Sam a message saying I'm late"; }, tell, out heldStep);
                if (ran.Count != 0 || !asks.Contains("say go") || heldStep != "send Sam a message saying I'm late") throw new Exception("A chained send was not held for his go, or the held step was not handed back to wait for his answer: " + asks + " | " + heldStep);

                string looped = Chaining.Run("put on Arcane", "Opened Netflix.", act, facts, delegate { return "NEXT: open Netflix"; }, tell);
                if (ran.Count != 1) throw new Exception("The same next step ran over and over.");

                ran.Clear(); told.Clear();
                Chaining.Gaming = delegate { return true; };
                if (Chaining.Run("put on Arcane", "Opened Netflix.", act, facts, always, tell) != "Opened Netflix." || ran.Count != 0) throw new Exception("She chained a job during a game.");
                Chaining.Gaming = delegate { return false; };
                if (!Chaining.JudgeText.Contains("NEXT:") || !Chaining.JudgeText.Contains("DONE") || !Chaining.JudgeText.Contains("never")) throw new Exception("The next-step check prompt is missing its rules.");
            }
            finally
            {
                Chaining.Gaming = gamingWas;
            }
            checks.Add("PASS chaining: after a hands job, if his goal still has one clear step left she says one line and runs it herself, at most " + Chaining.MostJobs + " jobs in a row and then she checks with him; a finished goal, a failed, stopped, refused or asking job, a game, or the same step again stop her; a next step that sends, closes, deletes or buys waits for his go");
        }

        private static void SmokeLostWindows(List<string> checks)
        {
            Func<List<OpenWindow>> shownWas = LostWindows.Shown;
            Func<IntPtr, bool> foldedWas = LostWindows.Folded;
            Func<IntPtr, bool> unfoldWas = LostWindows.Unfold;
            Func<IntPtr, bool> raiseWas = LostWindows.Raise;
            Func<IntPtr, bool> aliveWas = LostWindows.Alive;
            Func<bool> gamingWas = LostWindows.Gaming;
            try
            {
                foreach (string said in new[] { "where did it go", "pull it back", "bring it back please", "capri its minimized", "can you unminimize it", "where did my window go", "restore that window" })
                    if (TweakFor(said) != "pullback") throw new Exception("\"" + said + "\" was not heard as bring the window back: " + (TweakFor(said) ?? "nothing"));
                foreach (string said in new[] { "where did it go wrong", "tell him to bring it back", "bring back the old menu in league", "why is it minimized all the time" })
                    if (TweakFor(said) == "pullback") throw new Exception("\"" + said + "\" was taken as bring the window back.");

                OpenWindow opera = new OpenWindow(new IntPtr(41), "Netflix - Opera", "opera");
                OpenWindow notes = new OpenWindow(new IntPtr(42), "Notes", "notepad");
                HashSet<IntPtr> folded = new HashSet<IntPtr>();
                List<string> done = new List<string>();
                LostWindows.Shown = delegate { List<OpenWindow> open = new List<OpenWindow>(); foreach (OpenWindow one in new[] { opera, notes }) if (!folded.Contains(one.Handle)) open.Add(one); return open; };
                LostWindows.Folded = delegate(IntPtr handle) { return folded.Contains(handle); };
                LostWindows.Unfold = delegate(IntPtr handle) { done.Add("unfold " + handle.ToInt32()); folded.Remove(handle); return true; };
                LostWindows.Raise = delegate(IntPtr handle) { done.Add("raise " + handle.ToInt32()); folded.Remove(handle); return true; };
                LostWindows.Alive = delegate { return true; };
                LostWindows.Gaming = delegate { return false; };
                LostWindows.Forget();
                DateTime now = DateTime.Now;
                if (!LostWindows.Bring(now).Contains("nothing")) throw new Exception("Bring it back with nothing lost did not say so.");

                LostWindows.Before();
                folded.Add(opera.Handle);
                string put = LostWindows.After("open netflix and pick my profile", now);
                if (!put.Contains("Opera") || !put.Contains("put") || done.Count != 1 || done[0] != "unfold 41" || folded.Contains(opera.Handle)) throw new Exception("A window her job minimized by accident was not put back and told: " + put + " | " + string.Join(" / ", done.ToArray()));

                LostWindows.Before();
                folded.Add(notes.Handle);
                string asked = LostWindows.After("minimize my notes", now.AddMinutes(1));
                if (asked.Length != 0 || done.Count != 1) throw new Exception("A window he asked her to minimize was put back: " + asked);
                string back = LostWindows.Bring(now.AddMinutes(2));
                if (!back.Contains("Notes") || done.Count != 2 || done[1] != "raise 42") throw new Exception("Where did it go did not bring back the window she minimized last: " + back);

                LostWindows.Minimized(opera, now.AddMinutes(3));
                folded.Add(opera.Handle);
                LostWindows.Gaming = delegate { return true; };
                if (LostWindows.Bring(now.AddMinutes(4)) != Lockout.Refusal || done.Count != 2) throw new Exception("Bring it back switched windows during a game.");
                LostWindows.Gaming = delegate { return false; };
                if (!LostWindows.Bring(now.AddMinutes(4)).Contains("Opera") || done[2] != "raise 41") throw new Exception("A window minimized with app_window was not brought back.");
                if (!LostWindows.Bring(now.AddMinutes(4 + LostWindows.KeepMinutes + 1)).Contains("nothing")) throw new Exception("An old minimized window was still brought back.");
            }
            finally
            {
                LostWindows.Shown = shownWas;
                LostWindows.Folded = foldedWas;
                LostWindows.Unfold = unfoldWas;
                LostWindows.Raise = raiseWas;
                LostWindows.Alive = aliveWas;
                LostWindows.Gaming = gamingWas;
                LostWindows.Forget();
            }
            checks.Add("PASS lost windows: a window her hands job minimized without being asked is put back after the job and she says so; a minimize he asked for stays; \"where did it go\", \"pull it back\", \"it's minimized\" bring back the window she minimized last (within 30 minutes), never during a game, and chat about bringing things back is not taken as the command");
        }
    }
}
