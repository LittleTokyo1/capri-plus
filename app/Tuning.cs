using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class Tunable
    {
        internal string Key;
        internal string Words;
        internal string Field;
        internal string[] Choices;
        internal int[] Steps;
        internal int Low;
        internal int High = 1;
        internal int Default;
        internal string Unit = "";
        internal bool Toggle;
        internal bool Listed;
        internal bool Upstream;
        internal string Locked;
        internal string Guard;
        internal Func<int> Get;
        internal Action<int> Set;
        internal Func<string[]> GetList;
        internal Action<string[]> SetList;
    }

    internal sealed class TuneChange
    {
        internal Tunable What;
        internal int Old;
        internal int Mask = -1;
        internal string[] OldList;
        internal DateTime At;
    }

    // Her own settings by voice or text (CAPRI-064): every safe Capri and Capri+ setting by name or meaning, relative changes,
    // undo; the in-game lockout, SECURITY, buying and spend caps stay in the settings window.
    internal static class Tuning
    {
        internal const string LockedNote = " stays in the settings window: I never change it by voice.";
        internal const string GuardNote = " changes only when you ask for it yourself, in your own words (at the PC, on your phone or in your Discord DM), never on my own. Nothing changed.";
        internal static readonly string[] SuggestNames = { "Off", "Suggest only" };
        internal const int RecentMinutes = 10;
        private const int KeptChanges = 10;

        internal static readonly string[] Internal =
        {
            "ClaudeToken", "PhonePort", "PhoneTailnet", "MixMic", "MixCable", "Month", "Spent", "Searches", "Upstream078", "VoiceChars", "PlanRuns", "PlanAccount", "Bridge", "HoldVk", "HoldMods", "PauseVk", "PauseMods", "LookVk", "LookMods",
            "StopVk", "StopMods", "Paused", "HasGameSpot", "GameSpotX", "GameSpotY", "ChatterboxLab", "VaultFolder", "GoogleId", "GoogleSecret", "GoogleRefresh",
            "GoogleAccount", "SpotifyId", "SpotifyRefresh", "SpotifyAccount", "DiscordToken", "DiscordOwner", "DiscordChannel", "DiscordLast", "DiscordPaired",
            "DiscordSeen", "GoodnightAt", "PhoneAddress", "PhoneIp", "HomeAway", "HomeProactive", "ListenCapriWas",
            "WorkSites", "NightOn", "NightQuietWas", "NightListenWas", "NightChatWas", "NightShushWas"
        };

        private static readonly string[] Stop =
        {
            "the", "a", "an", "my", "your", "her", "his", "she", "you", "me", "i", "setting", "settings", "to", "of", "on", "off", "and", "in", "it", "how", "much",
            "mode", "turn", "set", "change", "make", "be", "is", "up", "down", "percent", "for", "by", "from", "with", "that", "this", "please", "capri", "slider", "level"
        };

        internal const string Rule = "Your own settings and personality (how much you talk on your own, quiet or key moments or chatty, your humor, sass, slang, how long you talk, Japanese, your voice and its volume, sounds, walking, dancing, check-ins, quiet hours, League extras like your CS HUD and objective calls, and the rest) change only with the setting tool: never with use_computer, never by opening a settings window, and never say one changed unless the setting tool said Changed.";

        internal const string Schema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"set to change it, get to read it, undo when they say put it back, change it back, undo that or the way it was (undo takes back your last change)\",\"enum\":[\"set\",\"get\",\"undo\"]},\"name\":{\"type\":\"string\",\"description\":\"The setting in their words or from the list, like talks on her own, humor or voice volume\"},\"level\":{\"type\":\"string\",\"description\":\"Always give it. The new value in their words: on, off, a choice like key moments or quiet, a number like 30, or a change like -30%, +20 or less (30% less humor is -30%, talk less is less); for get or undo write -\"}},\"required\":[\"action\",\"level\"]}";

        private static readonly Regex Mine = new Regex(@"\b(capri\+?|mainframe|your own|her own)\b[^.!?]{0,60}\b(settings?|lockout|slider|toggle|mode)\b|\b(settings?|lockout|slider)\b[^.!?]{0,60}\b(capri\+?|mainframe)\b|\b(in-?game|game) lockout\b", RegexOptions.IgnoreCase);

        // A job about Capri's own settings (her say-do pass can write one) is never a mouse job: the setting tool answers it.
        internal static bool Own(string task) { return Mine.IsMatch(task ?? ""); }

        private static readonly Regex SwitchFirst = new Regex(@"^\W*(?:please\s+)?(?:turn|switch|put)\s+(?<state>on|off)\s+(?:the\s+|my\s+|your\s+|capris\s+)?(?<what>\S.*?)(?:\s+(?:please|now|for me))?\W*$", RegexOptions.IgnoreCase);
        private static readonly Regex SwitchLast = new Regex(@"^\W*(?:please\s+)?(?:turn|switch|put)\s+(?:the\s+|my\s+|your\s+|capris\s+)?(?<what>\S.*?)\s+(?<state>on|off)(?:\s+(?:please|now|for me))?\W*$", RegexOptions.IgnoreCase);

        internal static ToolArguments Switched(string task, string asked)
        {
            string said = (task ?? "").Replace("'", "").Replace("’", "");
            Match found = SwitchFirst.Match(said);
            if (!found.Success) found = SwitchLast.Match(said);
            if (!found.Success) return null;
            string what = Plain(found.Groups["what"].Value);
            foreach (Tunable one in All)
                if (one.Toggle && Plain(one.Key) == what) return new ToolArguments { Action = "set", Name = one.Key, Level = found.Groups["state"].Value.ToLowerInvariant(), Asked = asked ?? task };
            return null;
        }

        internal static void Settle(string task, List<KeyValuePair<string, ToolArguments>> calls)
        {
            if (!Own(task)) return;
            calls.RemoveAll(delegate(KeyValuePair<string, ToolArguments> call) { return call.Key == "use_computer" || call.Key == "shortcut"; });
            if (calls.Count == 0) calls.Add(new KeyValuePair<string, ToolArguments>("setting", new ToolArguments { Action = "set", Name = task }));
        }

        internal static readonly List<Tunable> All = Build();
        internal static readonly string Listing = Listed();

        internal static readonly string Spec = "Change or read one of your own settings, yours and Capri+'s: how much you talk on your own (quiet, key moments, chatty companion), your humor, sass, slang and talkativeness sliders (0 to 100%, 50% is your usual self), reply length, Japanese, your voice and its volume, speed and pitch, sounds, walking speed, wandering, dancing to music, check-ins, heartbeat, quiet hours, auto-reply to friends on Discord (on or off, its mode, and the friends list: add or remove a name in level, like add Alex), League extras and the rest. Use it whenever they ask you to change, turn on or off, raise, lower, reset or put back one of your settings or a part of your personality (talk less, only key moments, 30% less humor, be less sassy, louder, stop dancing to music), or ask what one is set to. Their PC's own settings (Bluetooth, brightness, Windows) are not yours: those are use_computer. Never say a setting changed unless the result starts with Changed. The in-game lockout can't be changed this way. SECURITY (its preset, Blackwall mode, Hey Capri), buying and messages on your own, spend caps, full access for big jobs, the activity log, the never-look apps, their project folders, project sessions a day and the phone app change only when they ask for that setting themselves in their own words, never on your own idea; the tool checks and says so. A project folder with | full is only ever added in Capri+. Their school sites are a list too (add or remove a name). Name the setting in their words; the tool finds it. Some of yours: talks on her own, humor, sass, slang, talkativeness, reply length, japanese, voice volume, voice speed, her voice, sound effects, walking speed, wandering, dance to music, check-ins, heartbeat, quiet hours start, cs hud (your CS per minute strip in their League game, not Counter-Strike), objective calls, accept the match, lock in my favourite, pro builds panel.";

        internal static bool Valid(ToolArguments args)
        {
            string action = string.IsNullOrEmpty(args.Action) ? "set" : args.Action;
            if (action == "undo" || action == "list") return true;
            return (action == "set" || action == "get") && !string.IsNullOrWhiteSpace(args.Name);
        }
        private static readonly List<TuneChange> changes = new List<TuneChange>();
        private static readonly object gate = new object();

        internal static Func<Action, bool> Ui = delegate(Action act) { act(); return true; };
        internal static Action<Tunable> Changed;

        private static List<Tunable> Build()
        {
            List<Tunable> all = new List<Tunable>();
            all.Add(Fork("Proactive", "talks on her own", "how much she talks on her own chatty companion quiet key moments only commentary react reactions comments speak herself proactive pop in interrupt", ForkSettingsForm.ProactiveNames));
            all.Add(Fork("Humor", "humor", "humor humour funny jokes joke joking comedy witty", null));
            all.Add(Fork("Sass", "sass", "sass sassy teasing tease smug bratty attitude cheeky snark snarky", null));
            all.Add(Fork("Slang", "slang", "slang night city words choom preem nova gonk", null));
            all.Add(Fork("Talk", "talkativeness", "talkativeness talk replies reply length long short wordy verbose words answers yap", null));
            all.Add(Fork("Japanese", "japanese", "japanese romaji exclamations cursing tokyo", ForkSettingsForm.JapaneseNames));
            all.Add(Fork("JapaneseOften", "japanese often", "japanese often every other reply", null));
            all.Add(Fork("HerChoiceName", "her choice of nicknames", "nicknames nickname call me teasing names", null));
            all.Add(Fork("FlyToWindows", "fly to windows", "fly beside window job flies", null));
            all.Add(Fork("HandsByName", "press buttons by name", "press buttons by name cursor free mouse stays real mouse ui automation hands", null));
            all.Add(Fork("HandsWindowOnly", "window-only pictures", "window only pictures screenshot just the window whole monitor hands look", null));
            all.Add(Fork("HandsListOnly", "button lists instead of pictures", "button list instead of picture no picture controls names fewer screenshots hands", null));
            all.Add(Fork("HandsRemember", "remember the way", "remember the way steps that worked jobs done before repeat hands", null));
            all.Add(Fork("Boards", "chess and board games", "chess board games mini games her board play chess checkers", null));
            all.Add(Fork("EarsHotwords", "hotwords for her name", "hotwords her name capri hearing ears hear capri better", null));
            all.Add(Fork("WalkSpeed", "walking speed", "walking speed walk faster slower pace", null, 1, 6, "x"));
            all.Add(Fork("SummarizeChats", "notes after chats", "note lasting things after a chat summarize memory pass me.md", null));
            all.Add(Fork("MeaningOn", "meaning search", "meaning search local search vectors embeddings", null));
            all.Add(Fork("HeartbeatOn", "heartbeat", "heartbeat calendar mail birthdays checks", null));
            all.Add(Fork("HeartbeatMinutes", "heartbeat minutes", "minutes between heartbeat checks", null, 0, 720, " min"));
            all.Add(Fork("HeartbeatPerDay", "heartbeat wake-ups a day", "most heartbeat wake ups a day", null, 1, 200, ""));
            all.Add(Fork("NightlyOn", "evening tidy-up", "evening tidy up nightly filing journal vault", null));
            all.Add(Fork("NightlyHour", "evening tidy-up hour", "hour of the evening tidy up nightly time", null, 0, 23, ":00"));
            all.Add(Fork("CallMode", "call mode", "call mode discord call friends talk with my friends answer them in the call", null));
            all.Add(Fork("VoiceInMic", "her voice in my mic", "voice in my mic her voice in discord mic cable vb-cable friends hear you mix", null));
            all.Add(Fork("Clones", "clones", "clones clone yourself copies of you chaos mode multiply duplicate", null));
            all.Add(Fork("UpsetChaos", "chaos when upset", "chaos when upset rude insult sets her clones off angry clones", null));
            all.Add(Fork("IconChaos", "desktop icon chaos", "desktop icon chaos icons scatter hide shuffle mess up my icons", null));
            all.Add(Fork("FakeCrash", "fake crash", "fake crash blue screen pretend crash bsod prank", null));
            all.Add(Fork("CodeMode", "code mode", "code mode husk mode claude code body watch my coding sessions codex", null));
            all.Add(Fork("ThrowHer", "throw her", "throw her fling toss yeet throwing you with the mouse bounce sulk", null));
            all.Add(Fork("PcChecks", "pc checks", "pc checks pc status internet status ping list windows check app what's open is it installed", null));
            all.Add(Fork("ClipThat", "clip that", "clip that on the spot scroll that bd save a clip right away without a game check", null));
            all.Add(Fork("OwnClock", "her own clock", "her own clock what time is it date read the pc clock never search the time", null));
            all.Add(Fork("StreamCohost", "stream co-host", "stream co-host streamer mode stream for me go live talk to my stream chat viewers", null));
            all.Add(Fork("DiscordStatus", "discord status", "discord status moving status show what you are doing on my discord profile playing capri rich presence", null));
            all.Add(Fork("Diagrams", "diagrams", "diagrams draw a diagram flowchart mind map on my screen and explain it teach me", null));
            all.Add(Fork("NameInGames", "name needed in games", "name needed in games only answer when i say your name capri during a game talking with friends ignore other voices", null));
            all.Add(Fork("Warmth", "warmer with you", "warmer with you sweeter nicer to me closer personality warm up to me", null));
            all.Add(Fork("InsideJokes", "inside jokes", "inside jokes running gags funny moments bring back jokes callbacks", null));
            all.Add(Fork("Moods", "moods", "moods her mood sleepy hyped missed you proud worried mood row", null));
            all.Add(Fork("Gestures", "gestures she picks", "gestures she picks her own animations grin glitch blink moves on her own", null));
            all.Add(Fork("HerOpinions", "her own opinions", "her own opinions favourites disagree with me takes stories made up stories her ideas", null));
            all.Add(Fork("FreeKeys","free keys", "free keys free api keys free brain medium intelligence gemini openrouter nvidia key", null));
            all.Add(Fork("MixLoud", "her loudness in my mic", "loudness volume of her voice in my mic cable discord friends hear louder quieter", null, 25, 200, "%"));
            all.Add(Fork("DiscordOn", "discord bot", "discord bot dms read", null));
            all.Add(Fork("DiscordOnline", "friend online alerts", "friend comes online discord heads up alerts plays", null));
            all.Add(Fork("DiscordFriendAnswers", "answers to friends", "friend messages her bot answer friends herself reply friends not on my auto-reply list everyone else", ForkSettingsForm.FriendAnswerNames));
            all.Add(Fork("AutoReplyOn", "auto-reply", "auto reply auto-reply answer my friends for me discord bot", null));
            all.Add(Fork("AutoReplyMode", "auto-reply mode", "how she auto replies chats back status answers only drafts asks me first", AutoReply.ModeNames));
            all.Add(Fork("FriendsMode", "friends mode", "friends mode friends call listen listener voice gpu", FriendMode.Names));
            all.Add(Fork("StudyModel", "study model", "study studies school jobs model brain opus sonnet", ClaudeJob.ModelNames));
            all.Add(Fork("AutoReplyFriends", "auto-reply friends", "who she auto replies to friends people list auto reply add remove", null));
            all.Add(Fork("VoiceOnStart", "voice chat at startup", "voice chat on at start startup launch listening", null));
            all.Add(Fork("AutonomyOn", "autonomy", "autonomy offers suggest on her own", null));
            all.Add(Fork("AwayCheckIns", "check-ins", "check ins away messages discord good morning evening note", null));
            all.Add(Fork("SchoolTriage", "inbox triage", "inbox triage school mail notifications new mail line draft replies", null));
            all.Add(Fork("ExplainDiagrams", "diagrams when explaining", "diagrams when explaining draw when you explain explain with a drawing how it works", null));
            all.Add(Fork("KeyLightsOn", "keyboard lights", "keyboard lights wooting rgb keys glow lighting wootility", null));
            all.Add(Fork("ScenesOn", "scenes", "scenes home scene work scene pentest scene i'm home sequence lets get to work", null));
            all.Add(Fork("SceneAuto", "home scene by itself", "home scene by itself automatic ask first when i come back wake up scene", null));
            all.Add(Fork("AwayWarmth", "warm notes when away", "warm notes when away warm note thinking of you good luck gg sweet message discord when i'm away", null));
            all.Add(Fork("CheckInsPerDay", "check-ins a day", "most check ins a day", null, 1, 10, ""));
            all.Add(Fork("CheckInGapHours", "hours between check-ins", "least hours between two check ins gap", null, 1, 12, " h"));
            all.Add(Fork("AutoMail", "autonomy mail", "mail gmail drafts autonomy ask first", ForkSettingsForm.AskNames));
            all.Add(Fork("AutoCalendar", "autonomy calendar", "calendar events autonomy ask first", ForkSettingsForm.AskNames));
            all.Add(Fork("AutoReminders", "autonomy reminders", "reminders nudges autonomy ask first", ForkSettingsForm.AskNames));
            all.Add(Fork("AutoResearch", "autonomy research", "research prep autonomy ask first", ForkSettingsForm.AskNames));
            all.Add(Fork("AutoPerDay", "things she does by herself a day", "most things a day do and tell by herself on her own autonomy", null, 0, 20, ""));
            all.Add(Fork("QuietFrom", "quiet hours start", "quiet hours start from night begin", null, 0, 23, ":00"));
            all.Add(Fork("QuietTo", "quiet hours end", "quiet hours end until morning stop", null, 0, 23, ":00"));
            all.Add(Fork("FreshStartOn", "daily fresh start", "daily fresh start restart memory", null));
            all.Add(Fork("FreshStartHour", "fresh start hour", "hour of the daily fresh start", null, 0, 23, ":00"));
            all.Add(Fork("FreshStartMB", "fresh start memory cap", "memory cap mb restart when she uses", null, 1024, 10240, " MB"));
            all.Add(Fork("PlanJobs", "big jobs on my plan", "big pc jobs claude plan claude code", null));
            all.Add(Fork("PlanModel", "model for big jobs", "model for big jobs opus sonnet haiku", ForkSettingsForm.PlanModels));
            all.Add(Fork("VoiceEngine", "her voice", "her voice engine cloned voice chatterbox elevenlabs free voice tts", ForkSettingsForm.VoiceEngineNames));
            all.Add(Fork("IntenseGames", "very intense games", "very intense heavy games gpu unload cloned voice", null));
            all.Add(Fork("IntenseVoice", "voice in intense games", "while a heavy game is running voice", ForkSettingsForm.IntenseVoiceNames));
            all.Add(Fork("IdleMinutes", "unload cloned voice after", "unload her cloned voice after quiet minutes gpu memory", null, 1, 120, " min"));
            all.Add(Fork("VoiceBrain", "voice chat brain", "voice chat brain elevenlabs agent claude", ForkSettingsForm.VoiceBrainNames));
            all.Add(Fork("TalkSeconds", "longest message", "longest message i can say seconds", null, 5, 60, " s"));
            all.Add(Fork("PauseTenths", "pause that ends my message", "pause ends my message tenths wait before answering cuts me off", null, 6, 30, " tenths"));
            all.Add(Fork("BargeIn", "talking over her", "talking over her interrupts barge in", null));
            all.Add(Fork("ScribeEars", "scribe ears", "elevenlabs scribe hearing ears speech to text", null));
            all.Add(Fork("MicThreshold", "mic sensitivity", "mic sensitivity microphone threshold hears quieter noise", null, 3, 100, ""));
            all.Add(Fork("GameVision", "watching games", "watching games vision look at game screen", ForkSettingsForm.VisionNames));
            all.Add(Fork("GameSeconds", "game look seconds", "n seconds how often she looks at the game", null, 2, 600, " s"));
            all.Add(Fork("LookAtMouse", "look at the mouse monitor", "two monitors looks at the one my mouse is on", null));
            all.Add(Fork("LookBothMonitors", "look at both monitors", "both monitors at once every screen look glance two screens", null));
            all.Add(Fork("DesktopVision", "watch the desktop", "watch the desktop outside games screen", null));
            all.Add(Guarded(Fork("Blocklist", "never look apps", "never look while these apps sites blocklist private", null), "the apps she never looks at"));
            all.Add(Fork("AlwaysLook", "look when I talk", "always look at my screen when i talk screenshot every message", null));
            all.Add(Fork("BackgroundLookup", "background lookups", "look things up in the background search", null));
            all.Add(Fork("KeepChats", "keep conversations going", "keep the conversation going chats after she messages someone", null));
            all.Add(Fork("ChatMinutes", "chat quiet minutes", "stop when it has been quiet minutes chat", null, 1, 240, " min"));
            all.Add(Fork("ChatSign", "message sign", "put this before her messages sign prefix", null));
            all.Add(Fork("LeagueCoach", "league coach", "league coach champ select", null));
            all.Add(Fork("CountersUnasked", "counters by herself", "counters by herself when enemies pick unasked", null));
            all.Add(Fork("LeagueResearch", "counter research", "research counters stats sites patch", null));
            all.Add(Fork("ShowBuilds", "pro builds panel", "pro builds panel probuildstats champ select", null));
            all.Add(Fork("PushButton", "push buttons", "push buttons build panel", null));
            all.Add(Fork("PushVoice", "push by asking", "push build by asking voice", null));
            all.Add(Fork("PushOnLock", "push build on lock in", "push build 1 automatically when i lock in", null));
            all.Add(Fork("PushSpells", "push summoner spells", "push the pro summoner spells", null));
            all.Add(Fork("HoverFavorite", "hover my favourite", "hover my first favourite champ lane", null));
            all.Add(Fork("AutoAccept", "accept the match", "auto accept the match queue dodge", null));
            all.Add(Fork("AutoLock", "lock in my favourite", "auto lock in favourite champ pick", null));
            all.Add(Fork("AcceptEveryPop", "accept every queue pop", "accept every queue pop even at the pc", null));
            all.Add(Fork("AutoBan", "ban a counter", "auto ban a champion strong against my pick counter", null));
            all.Add(Fork("SmarterBrain", "smarter brain", "smarter brain sonnet understanding decision point", null));
            all.Add(Fork("ListenMode", "listening mode", "overhear mode eavesdrop", null));
            all.Add(Fork("AwayAlways", "accept and lock when", "accept and lock only when away always", ForkSettingsForm.AwayNames));
            all.Add(Fork("QueueFirst", "first position", "queue first position role lane primary main", ForkSettingsForm.PositionNames));
            all.Add(Fork("QueueSecond", "second position", "queue second position role lane secondary", ForkSettingsForm.PositionNames));
            all.Add(Fork("CsHud", "cs hud", "cs per minute hud strip creep score farm", null));
            all.Add(Fork("ObjectiveCalls", "objective calls", "dragon baron voidgrubs objective calls spawn", null));
            all.Add(Fork("FavTop", "top favourites", "favourite champs top lane", null));
            all.Add(Fork("FavJungle", "jungle favourites", "favourite champs jungle", null));
            all.Add(Fork("FavMid", "mid favourites", "favourite champs mid middle lane", null));
            all.Add(Fork("FavBot", "bot favourites", "favourite champs bot adc", null));
            all.Add(Fork("FavSupport", "support favourites", "favourite champs support", null));
            all.Add(Fork("GameMode", "during games", "during games she stays still hides tiny game spot", ForkSettingsForm.GameModeNames));
            all.Add(Fork("GameSize", "size in my game spot", "tiny size in game spot", null, 140, 560, " px"));
            all.Add(Locked(Fork("Games", "games that lock her hands", "games that lock her hands in-game lockout list", null), "The in-game lockout is never changed by voice."));
            all.Add(Locked(Fork("LockLeagueClient", "lockout in champ select", "lock during league champ select and loading lockout", null), "The in-game lockout is never changed by voice."));
            all.Add(Locked(new Tunable { Key = "in-game lockout", Words = "in game lockout lock hands game lock clicking typing" }, "The in-game lockout is always on."));
            all.Add(Guarded(Fork("Cap", "monthly api cap", "monthly api cap spend budget usd money dollars", null, 0, 1000, " $"), "a spend cap"));
            all.Add(Guarded(Fork("VoiceBudget", "elevenlabs character budget", "elevenlabs characters per month budget limit", null, 0, 2000000, " characters"), "a spend cap"));
            all.Add(Guarded(Fork("AutoBuying", "buying on her own", "buying buy purchases orders auto buying", SuggestNames), "buying"));
            all.Add(Guarded(Fork("AutoMessages", "messages in my name", "messages in my name auto messages send", SuggestNames), "messages in your name"));
            all.Add(Fork("RunProjects", "run my projects", "run my projects claude projects sessions orchestrate start sessions for me", null));
            all.Add(Guarded(Fork("Projects", "project folders", "project folders my projects folders she runs claude code sessions in", null), "your project folders"));
            all.Add(Guarded(Fork("ProjectsPerDay", "project sessions a day", "most project sessions she starts a day daily cap claude code", null, 1, ProjectRun.MostPerDay, ""), "the daily project sessions"));
            all.Add(Guarded(Fork("PhoneOn", "phone app", "phone app iphone web app her room chat with her on my phone", null), "the phone app"));
            all.Add(Fork("SchoolSites", "school sites", "school sites school websites portals study sites sign in", null));
            all.Add(Guarded(Fork("PlanFullAccess","big jobs full access", "shell files registry full access big jobs", null), "full access"));
            all.Add(Guarded(Fork("LogActions", "activity log", "activity log of her hands logging", null), "the activity log"));
            all.Add(Guarded(Pick(Their("Permissions", "security preset", "security preset lockdown guarded trusted permissions toggles"), Security.Presets, null, Security.Trusted), "SECURITY"));
            all.Add(Guarded(StartsOff(Flag(Their("Bypass", "blackwall mode", "blackwall mode bypass"))), "SECURITY"));
            all.Add(Guarded(Flag(Their("HeyCapri", "hey capri", "listen for hey capri wake word")), "SECURITY"));
            all.Add(Number(Their("Volume", "voice volume", "her voice volume loud louder quieter loudness"), 0, 150, 100, "%"));
            all.Add(Number(Their("Sounds", "sound effects", "sound effects sounds sfx"), 0, 100, 60, "%"));
            all.Add(Number(Their("VoicePitch", "voice pitch", "voice pitch higher lower deeper"), -VoiceFx.MostPitch, VoiceFx.MostPitch, 0, " semitones"));
            all.Add(Number(Their("VoiceSpeed", "voice speed", "voice speed talks faster slower speaking rate"), VoiceFx.Slowest, VoiceFx.Fastest, 100, "%"));
            all.Add(Number(Their("VoiceRobot", "voice robot", "robot voice robotic effect"), 0, 100, 0, "%"));
            all.Add(Number(Their("VoiceGlitch", "voice glitch", "voice glitch effect glitchy"), 0, 100, 0, "%"));
            all.Add(Number(Their("Size", "her size", "her size bigger smaller character"), 140, 560, 240, " px"));
            all.Add(Pick(Their("Wander", "wandering", "wander wandering walk around restless moving"), new[] { "Off", "Rarely", "Sometimes", "Often", "Restless" }, null, 0));
            all.Add(Pick(Their("PopPace", "pop-ins", "how often she pops in chatty"), new[] { "Rarely", "Sometimes", "Often" }, null, 1));
            all.Add(Pick(Their("SleepAfter", "falls asleep after", "falls asleep after idle sleep"), new[] { "Never", "1 min", "2 min", "5 min", "10 min", "30 min" }, new[] { 0, 60, 120, 300, 600, 1800 }, 120));
            all.Add(Pick(Their("BubbleTime", "bubble time", "speech bubble stays time"), new[] { "Quick", "Normal", "Long" }, null, 1));
            all.Add(Pick(Their("GlanceGap", "glance gap", "how often she glances at my screen watch gap"), new[] { "10 s", "15 s", "30 s", "1 min", "1.5 min", "2 min", "3 min", "5 min", "10 min" }, new[] { 10, 15, 30, 60, 90, 120, 180, 300, 600 }, 120));
            all.Add(Pick(Their("Wordiness", "reply length", "reply length short medium long replies"), new[] { "Short", "Medium", "Long" }, null, 0));
            all.Add(Pick(Their("Recall", "chat memory", "chat memory recall how much of the chat she remembers"), new[] { "Short", "Medium", "Long" }, new[] { 8, 16, 32 }, 8));
            all.Add(Pick(Their("PerchFps", "perch fps", "perch frames a second fps smooth"), new[] { "10 fps", "15 fps", "20 fps", "30 fps", "40 fps", "50 fps", "60 fps" }, new[] { 10, 15, 20, 30, 40, 50, 60 }, 30));
            all.Add(Flag(Their("NoReactions", "live reactions", "live reactions quick lines over the game reactions feed")));
            all.Add(Flag(Their("Hologram", "hologram", "hologram mode see-through translucent click through her")));
            all.Add(Pick(Their("ClipSeconds", "clip length", "clip length default clip seconds how long clips are"), ClipChain.LengthNames, ClipSettings.Lengths, 30));
            all.Add(Pick(Their("ClipFps", "clip frame rate", "clip frame rate clip fps smoother clips"), ClipChain.RateNames, ClipSettings.Rates, 30));
            all.Add(Pick(Their("ClipResolution", "clip resolution", "clip resolution clip quality 720p 1080p 480p"), ClipChain.SizeNames, ClipSettings.Sizes, 0));
            all.Add(Pick(Their("ClipMbps", "clip bitrate", "clip bitrate mbps clip file size"), ClipChain.BitrateNames, ClipSettings.Bitrates, 0));
            all.Add(Flag(Their("NoDance", "dance to music", "dance dancing to music")));
            all.Add(Flag(Their("NoIntro", "glitch in at startup", "glitch in at startup intro")));
            all.Add(Flag(Their("Typing", "typing sound", "typing sound clicks")));
            all.Add(Flag(Their("NoPlayer", "mini player", "mini player music player")));
            all.Add(Flag(Their("OnTop", "always on top", "always on top")));
            all.Add(Flag(Their("Watch", "watch my screen", "watch my screen glances")));
            all.Add(Flag(Their("CaughtUp", "keep her caught up", "keep her caught up what she saw")));
            all.Add(Flag(Their("NoDiscord", "show on discord", "show on discord rich presence status")));
            all.Add(Flag(Their("StartupSet", "start with windows", "start with windows startup boot")));
            all.Add(Flag(Their("QuickHands", "use her cursor", "use her own cursor")));
            all.Add(Flag(Their("HdGameRest", "free voice in games", "free voice in games full screen gpu")));
            all.Add(Flag(Their("Remembers", "remember our chats", "remember our chats memory saving vault")));
            return all;
        }

        private static Tunable Fork(string field, string key, string words, string[] choices)
        {
            return Fork(field, key, words, choices, 0, 100, "%");
        }

        private static Tunable Fork(string field, string key, string words, string[] choices, int low, int high, string unit)
        {
            Tunable made = new Tunable { Key = key, Words = words, Field = field, Choices = choices };
            FieldInfo found = typeof(ForkSettings).GetField(field);
            if (found == null) return made;
            object fresh = found.GetValue(new ForkSettings());
            if (found.FieldType == typeof(string[]))
            {
                made.Listed = true;
                made.GetList = delegate { return (string[])found.GetValue(ForkSettings.Current) ?? new string[0]; };
                made.SetList = delegate(string[] value) { found.SetValue(ForkSettings.Current, value); };
                return made;
            }
            if (found.FieldType == typeof(string) && choices == null)
            {
                made.Listed = true;
                made.GetList = delegate { string text = (string)found.GetValue(ForkSettings.Current); return string.IsNullOrEmpty(text) ? new string[0] : new[] { text }; };
                made.SetList = delegate(string[] value) { found.SetValue(ForkSettings.Current, value.Length == 0 ? "" : string.Join(", ", value)); };
                return made;
            }
            if (found.FieldType == typeof(string))
            {
                string[] names = choices;
                made.High = names.Length - 1;
                made.Default = Math.Max(0, Array.IndexOf(names, (string)fresh));
                made.Get = delegate { return Math.Max(0, Array.IndexOf(names, (string)found.GetValue(ForkSettings.Current))); };
                made.Set = delegate(int value) { found.SetValue(ForkSettings.Current, names[value]); };
                return made;
            }
            if (found.FieldType == typeof(bool))
            {
                made.Toggle = choices == null;
                made.High = 1;
                made.Default = (bool)fresh ? 1 : 0;
                made.Get = delegate { return (bool)found.GetValue(ForkSettings.Current) ? 1 : 0; };
                made.Set = delegate(int value) { found.SetValue(ForkSettings.Current, value != 0); };
                return made;
            }
            made.Low = choices == null ? low : 0;
            made.High = choices == null ? high : choices.Length - 1;
            made.Unit = choices == null ? unit : "";
            if (found.FieldType == typeof(double))
            {
                made.Default = (int)(double)fresh;
                made.Get = delegate { return (int)(double)found.GetValue(ForkSettings.Current); };
                made.Set = delegate(int value) { found.SetValue(ForkSettings.Current, (double)value); };
                return made;
            }
            if (found.FieldType == typeof(long))
            {
                made.Default = (int)Math.Min(int.MaxValue, (long)fresh);
                made.Get = delegate { return (int)Math.Min(int.MaxValue, (long)found.GetValue(ForkSettings.Current)); };
                made.Set = delegate(int value) { found.SetValue(ForkSettings.Current, (long)value); };
                return made;
            }
            made.Default = (int)fresh;
            made.Get = delegate { return (int)found.GetValue(ForkSettings.Current); };
            made.Set = delegate(int value) { found.SetValue(ForkSettings.Current, value); };
            return made;
        }

        private static Tunable Their(string field, string key, string words)
        {
            return new Tunable { Key = key, Words = words, Field = "pref:" + field, Upstream = true };
        }

        private static Tunable Number(Tunable made, int low, int high, int fallback, string unit)
        {
            made.Low = low;
            made.High = high;
            made.Default = fallback;
            made.Unit = unit;
            return made;
        }

        private static Tunable Pick(Tunable made, string[] choices, int[] steps, int fallback)
        {
            made.Choices = choices;
            made.Steps = steps;
            made.High = choices.Length - 1;
            made.Default = steps == null ? fallback : Math.Max(0, Array.IndexOf(steps, fallback));
            return made;
        }

        private static Tunable Flag(Tunable made)
        {
            made.Toggle = true;
            made.Default = 1;
            return made;
        }

        private static Tunable Locked(Tunable made, string why)
        {
            made.Locked = why;
            return made;
        }

        private static Tunable Guarded(Tunable made, string what)
        {
            made.Guard = what;
            return made;
        }

        private static Tunable StartsOff(Tunable made)
        {
            made.Default = 0;
            return made;
        }

        internal static bool HisOwn(Tunable one, string asked)
        {
            if (one.Guard == null) return true;
            if (string.IsNullOrWhiteSpace(asked) || CallFriends.Friends(asked, DateTime.Now)) return false;
            List<string> said = Tokens(Regex.Replace(Plain(asked), @"^(?:hey\s+)?capri\b\W*", ""));
            int onKey = Overlap(said, one.Key);
            if (onKey == 0) return false;
            return onKey >= Math.Min(2, Tokens(one.Key).Count) || Overlap(said, one.Words) >= 2;
        }

        private static bool Secure(Tunable one)
        {
            return one != null && one.Guard == "SECURITY";
        }

        private static void Restore(int bits)
        {
            for (int i = 0; i < Security.Count; i++) Security.Set(i, (bits & (1 << i)) != 0);
        }

        internal static Tunable Named(string key)
        {
            foreach (Tunable one in All) if (one.Key == key) return one;
            return null;
        }

        internal static void Bind(string key, Func<int> get, Action<int> set)
        {
            Tunable one = Named(key);
            if (one == null) return;
            one.Get = get;
            one.Set = set;
        }

        private static string Listed()
        {
            List<string> keys = new List<string>();
            foreach (Tunable one in All) if (one.Locked == null) keys.Add(one.Key);
            return string.Join(", ", keys.ToArray());
        }

        // Every fork.json setting is changeable, locked or internal, so a new one can't slip past her setting tool.
        internal static string Unrouted()
        {
            List<string> missing = new List<string>();
            foreach (FieldInfo field in typeof(ForkSettings).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.GetCustomAttributes(typeof(System.Runtime.Serialization.DataMemberAttribute), false).Length == 0) continue;
                if (Array.IndexOf(Internal, field.Name) >= 0) continue;
                string name = field.Name;
                if (!All.Exists(delegate(Tunable one) { return one.Field == name; })) missing.Add(name);
            }
            foreach (Tunable one in All)
            {
                if (one.Field == null) continue;
                if (one.Upstream && typeof(Preferences).GetField(one.Field.Substring(5)) == null) missing.Add(one.Field + " (gone upstream)");
                if (!one.Upstream && one.Get == null && one.GetList == null) missing.Add(one.Field + " (no such field)");
            }
            return missing.Count == 0 ? null : string.Join(", ", missing.ToArray());
        }

        private static string Plain(string text)
        {
            return Regex.Replace(Regex.Replace((text ?? "").ToLowerInvariant().Replace("'", "").Replace("’", ""), @"[^\p{L}\p{Nd}%+.-]+", " "), @"\s+", " ").Trim();
        }

        private static string Stem(string word)
        {
            if (word.Length > 5 && word.EndsWith("ing", StringComparison.Ordinal)) return word.Substring(0, word.Length - 3);
            if (word.Length > 4 && word.EndsWith("s", StringComparison.Ordinal) && !word.EndsWith("ss", StringComparison.Ordinal)) return word.Substring(0, word.Length - 1);
            return word;
        }

        private static List<string> Tokens(string text)
        {
            List<string> found = new List<string>();
            foreach (string word in Plain(text).Split(' '))
            {
                string cleaned = word.Trim('%', '+', '.', '-');
                if (cleaned.Length < 2 || Array.IndexOf(Stop, cleaned) >= 0 || Regex.IsMatch(cleaned, @"^\d+$")) continue;
                string stem = Stem(cleaned);
                if (!found.Contains(stem)) found.Add(stem);
            }
            return found;
        }

        private static int Overlap(List<string> asked, string text)
        {
            List<string> has = Tokens(text);
            int count = 0;
            foreach (string word in asked) if (has.Contains(word)) count++;
            return count;
        }

        internal static Tunable Find(string name, string value, out string close)
        {
            close = null;
            string asked = Plain(name);
            if (asked.Length == 0) return null;
            foreach (Tunable one in All) if (Plain(one.Key) == asked) return one;
            List<string> words = Tokens(name);
            Tunable best = null, second = null;
            int top = 0, next = 0;
            foreach (Tunable one in All)
            {
                int score = Overlap(words, one.Key) * 3 + Overlap(words, one.Words) * 2;
                if (Plain(one.Words).IndexOf(asked, StringComparison.Ordinal) >= 0) score += 4;
                if (score > 0 && one.Choices != null && !string.IsNullOrWhiteSpace(value) && Choice(one, value) >= 0) score += 3;
                if (score > 0 && one.Toggle && !string.IsNullOrWhiteSpace(value) && (On.IsMatch(Plain(value)) || Off.IsMatch(Plain(value)))) score += 3;
                if (one.Choices != null && Choice(one, name) >= 0) score += 3;
                if (score > top) { second = best; next = top; best = one; top = score; }
                else if (score > next) { second = one; next = score; }
            }
            if (best == null || top < 2) return null;
            if (second != null && next == top && best.Locked != null && second.Locked != null) return best;
            if (second != null && next == top) { close = best.Key + " or " + second.Key; return null; }
            return best;
        }

        internal static int Choice(Tunable one, string said)
        {
            if (one.Choices == null) return -1;
            string asked = Plain(said);
            List<string> words = Tokens(said);
            int best = -1, top = 0;
            for (int i = 0; i < one.Choices.Length; i++)
            {
                string named = Plain(one.Choices[i]);
                string head = Plain(one.Choices[i].Split(':', '(')[0]);
                int score = named == asked || head == asked ? 100 : head.Length > 2 && asked.IndexOf(head, StringComparison.Ordinal) >= 0 ? 50 + head.Length : Overlap(words, one.Choices[i]) * 2;
                if (score > top) { top = score; best = i; }
            }
            return top >= 2 ? best : -1;
        }

        internal static string Shown(Tunable one, int value)
        {
            if (one.Toggle) return value != 0 ? "on" : "off";
            if (one.Choices == ForkSettingsForm.AskNames && value >= one.Choices.Length) return ForkSettingsForm.DoNames[Math.Min(ForkSettingsForm.DoNames.Length - 1, value)];
            if (one.Choices != null) return one.Choices[Math.Max(0, Math.Min(one.Choices.Length - 1, value))].Split(':', '(')[0].Trim();
            return value.ToString(CultureInfo.InvariantCulture) + one.Unit;
        }

        private static string ShownList(string[] items)
        {
            return items == null || items.Length == 0 ? "empty" : string.Join(", ", items);
        }

        internal static string Now(Tunable one)
        {
            if (one.Listed) return ShownList(one.GetList());
            return Shown(one, one.Get());
        }

        private static readonly Regex On = new Regex(@"^(on|yes|true|enable|enabled|start|turn on|switch on|1|show|allow|turn it on)$");
        private static readonly Regex Off = new Regex(@"^(off|no|false|disable|disabled|stop|turn off|switch off|0|hide|none|never|turn it off)$");
        private static readonly Regex Lower = new Regex(@"\b(less|lower|down|decrease|reduce|reduced|minus|fewer|quieter|slower|smaller|softer|tone)\b|^-");
        private static readonly Regex Higher = new Regex(@"\b(more|higher|up|increase|raise|plus|louder|faster|bigger|boost)\b|^\+");

        // The new value for what he said, or an error to say back.
        internal static string Target(Tunable one, string said, out int value)
        {
            int now = one.Get();
            value = now;
            string text = Plain(said);
            if (text.Length == 0) return "What should it be?";
            if (Regex.IsMatch(text, @"^(default|reset|usual|normal|back to normal|standard)$")) { value = one.Default; return null; }
            if (one.Toggle)
            {
                if (On.IsMatch(text)) { value = 1; return null; }
                if (Off.IsMatch(text)) { value = 0; return null; }
                if (Regex.IsMatch(text, @"^(toggle|flip|switch)$")) { value = now == 0 ? 1 : 0; return null; }
                return "Say on or off.";
            }
            Match number = Regex.Match(text, @"([+-]?)\s*(\d+(?:\.\d+)?)\s*(%|percent)?");
            bool down = Lower.IsMatch(text) || number.Groups[1].Value == "-";
            bool up = !down && (Higher.IsMatch(text) || number.Groups[1].Value == "+");
            if (one.Choices != null)
            {
                if (number.Success && one.Steps != null)
                {
                    double wanted = double.Parse(number.Groups[2].Value, CultureInfo.InvariantCulture);
                    if (Regex.IsMatch(text, @"\b(min|mins|minute|minutes)\b") && one.Steps[one.Steps.Length - 1] > 100) wanted *= 60;
                    int near = 0;
                    for (int i = 0; i < one.Steps.Length; i++) if (Math.Abs(one.Steps[i] - wanted) < Math.Abs(one.Steps[near] - wanted)) near = i;
                    value = near;
                    return null;
                }
                int picked = Choice(one, said);
                if (picked >= 0) { value = picked; return null; }
                if (Regex.IsMatch(text, @"^(max|maximum|most|highest)$")) { value = one.High; return null; }
                if (Regex.IsMatch(text, @"^(min|minimum|least|lowest)$")) { value = one.Low; return null; }
                if (down || up) { value = Math.Max(one.Low, Math.Min(one.High, now + (down ? -1 : 1))); return null; }
                return "Pick one of: " + Choices(one) + ".";
            }
            if (Regex.IsMatch(text, @"^(max|maximum|most|highest|all the way up|full)$")) { value = one.High; return null; }
            if (Regex.IsMatch(text, @"^(min|minimum|least|lowest|zero|none|all the way down|off)$")) { value = one.Low; return null; }
            if (!number.Success)
            {
                if (!down && !up) return "Give me a number from " + one.Low + " to " + one.High + one.Unit + ", or say more or less.";
                int step = Math.Max(1, (one.High - one.Low) / 10);
                value = Clamp(one, now + (down ? -step : step));
                return null;
            }
            double amount = double.Parse(number.Groups[2].Value, CultureInfo.InvariantCulture);
            bool percent = number.Groups[3].Success;
            if (!down && !up)
            {
                value = Clamp(one, (int)Math.Round(amount));
                return null;
            }
            double change = percent && one.Unit != "%" ? now * amount / 100.0 : amount;
            value = Clamp(one, (int)Math.Round(now + (down ? -change : change)));
            return null;
        }

        private static int Clamp(Tunable one, int value) { return Math.Max(one.Low, Math.Min(one.High, value)); }

        private static string Choices(Tunable one)
        {
            List<string> names = new List<string>();
            foreach (string choice in one.Choices) names.Add(choice.Split(':', '(')[0].Trim());
            return string.Join(", ", names.ToArray());
        }

        private static string[] ListTarget(string[] now, string said)
        {
            string text = (said ?? "").Trim();
            if (Regex.IsMatch(Plain(text), @"^(clear|none|empty|nothing|reset)$")) return new string[0];
            Match add = Regex.Match(text, @"^\s*(?:\+|add|put in|include)\s*(.+)$", RegexOptions.IgnoreCase);
            Match remove = Regex.Match(text, @"^\s*(?:-|remove|delete|drop|take out|take off)\s*(.+)$", RegexOptions.IgnoreCase);
            List<string> items = new List<string>(now ?? new string[0]);
            if (add.Success)
            {
                foreach (string item in Split(add.Groups[1].Value))
                {
                    string adding = item;
                    if (!items.Exists(delegate(string had) { return string.Equals(had, adding, StringComparison.OrdinalIgnoreCase); })) items.Add(adding);
                }
                return items.ToArray();
            }
            if (remove.Success)
            {
                foreach (string item in Split(remove.Groups[1].Value))
                {
                    string dropping = item;
                    items.RemoveAll(delegate(string had) { return string.Equals(had, dropping, StringComparison.OrdinalIgnoreCase); });
                }
                return items.ToArray();
            }
            return Split(text).ToArray();
        }

        private static List<string> Split(string text)
        {
            List<string> found = new List<string>();
            foreach (string part in (text ?? "").Split(',', ';', '\n')) if (part.Trim().Length > 0) found.Add(part.Trim());
            return found;
        }

        internal static string Do(ToolArguments args)
        {
            string action = string.IsNullOrEmpty(args.Action) ? "set" : args.Action;
            if (action == "undo")
            {
                Tunable undone = LastChanged();
                if (undone != null && undone.Guard != null && !(Reverts(args.Asked ?? "") || HisOwn(undone, args.Asked))) return "Putting " + undone.Key + " back" + GuardNote;
                return Undo(DateTime.Now);
            }
            if (action == "list") return "My settings: " + Listing + ".";
            string close;
            Tunable one = Find(args.Name, args.Level, out close);
            string level = args.Level;
            string reread = null;
            Tunable better = action == "set" ? Reread(one, args.Name, level, args.Asked, out reread) : null;
            if (better != null)
            {
                one = better;
                level = reread;
            }
            Tunable listed = action == "set" && (one == null || one.Toggle) ? Relist(args.Name, level) : null;
            if (listed != null) one = listed;
            if (one == null)
            {
                ActivityLog.Write("setting", "not found", Pursuit.Shorter(args.Name ?? "", 80));
                return close == null ? "None of my settings is called " + Voice.Quote(args.Name ?? "") + ". Nothing changed." : "Did you mean " + close + "? Nothing changed yet.";
            }
            if (one.Locked != null)
            {
                ActivityLog.Write("setting", "locked", one.Key);
                return Cap(one.Key) + LockedNote + " " + one.Locked + " Nothing changed.";
            }
            if (one.Listed ? one.GetList == null : one.Get == null) return Cap(one.Key) + " can't be changed until I'm fully started. Nothing changed.";
            if (action == "get") return Cap(one.Key) + " is " + Now(one) + ".";
            if (!HisOwn(one, args.Asked))
            {
                ActivityLog.Write("setting", "guarded", one.Key + ": not asked in his own words");
                return Cap(one.Key) + GuardNote;
            }
            if (one.Key == "security preset" && Choice(one, level ?? "") == Security.Custom) return "Custom is your own mix of SECURITY toggles, set in the Mainframe. Pick Lockdown, Guarded, Trusted or Blackwall. Nothing changed.";
            int named = Choice(one, level ?? "") < 0 && !Regex.IsMatch(level ?? "", @"\d") ? Named(one, args.Name) : -1;
            if (named >= 0) level = one.Choices[named].Split(':', '(')[0].Trim();
            if (string.IsNullOrWhiteSpace(level) || level.Trim() == "-") return "Nothing changed yet: say the new value in level. " + Cap(one.Key) + " is " + Now(one) + (one.Choices != null ? "; the choices are " + Choices(one) : one.Toggle ? "; on or off" : one.Listed ? "" : "; from " + one.Low + " to " + one.High + one.Unit) + ".";
            return Change(one, level, DateTime.Now);
        }

        private static readonly Regex FullAccess = new Regex(@"\|\s*full\b", RegexOptions.IgnoreCase);
        internal const string NoFullByVoice = "A project with | full (sessions that edit and commit) is added only in Capri+ > PROJECTS, never by voice. Nothing changed.";
        internal const string NoUnwatchByVoice = "A watch-only project (| watch) loses that mark only in Capri+ > PROJECTS, never by voice. Nothing changed.";
        private static readonly Regex WatchAccess = new Regex(@"\|\s*watch\b", RegexOptions.IgnoreCase);

        internal static bool AddProject(string line)
        {
            foreach (Tunable each in All)
            {
                if (each.Field != "Projects" || each.GetList == null || each.SetList == null) continue;
                Tunable one = each;
                List<string> grown = new List<string>(one.GetList());
                if (grown.Exists(delegate(string item) { return item.Split('|')[0].Trim().TrimEnd('\\', '/').Equals(line.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); })) return true;
                grown.Add(line);
                string[] wanted = grown.ToArray();
                return Apply(delegate { one.SetList(wanted); }, one);
            }
            return false;
        }

        internal static bool WatchDropped(string[] had, string[] wanted)
        {
            foreach (string old in had ?? new string[0])
            {
                if (!WatchAccess.IsMatch(old)) continue;
                string folder = old.Split('|')[0].Trim().Trim('"').TrimEnd('\\', '/');
                foreach (string item in wanted ?? new string[0])
                    if (item.Split('|')[0].Trim().Trim('"').TrimEnd('\\', '/').Equals(folder, StringComparison.OrdinalIgnoreCase) && !WatchAccess.IsMatch(item)) return true;
            }
            return false;
        }

        private static readonly Regex ListWords =new Regex(@"^\s*(?:add|put in|include|remove|delete|drop|take out|take off)\b", RegexOptions.IgnoreCase);

        internal static Tunable Relist(string name, string level)
        {
            if (string.IsNullOrWhiteSpace(level) || !ListWords.IsMatch(level)) return null;
            List<string> meant = Tokens(name);
            if (meant.Count == 0) return null;
            Tunable best = null;
            int top = 0, next = 0;
            foreach (Tunable each in All)
            {
                if (!each.Listed || each.GetList == null || each.Locked != null) continue;
                int score = Overlap(meant, each.Key) * 3 + Overlap(meant, each.Words);
                if (score > top) { next = top; top = score; best = each; }
                else if (score > next) next = score;
            }
            return best == null || top < 2 || next == top ? null : best;
        }

        internal static Tunable Reread(Tunable one, string name, string level, string asked, out string number)
        {
            number = null;
            if (string.IsNullOrWhiteSpace(asked) || (one != null && (one.Listed || one.Locked != null || one.Get == null))) return null;
            int probe;
            if (one != null && !string.IsNullOrWhiteSpace(level) && level.Trim() != "-" && Target(one, level, out probe) == null) return null;
            MatchCollection numbers = Regex.Matches(Plain(asked), @"(\bby\s+)?([+-]?\d+(?:\.\d+)?)\s*(%|percent)?");
            if (numbers.Count == 0) return null;
            Match last = numbers[numbers.Count - 1];
            List<string> words = Tokens(asked);
            List<string> meant = Tokens(name);
            Tunable best = null;
            int top = 0, next = 0;
            foreach (Tunable each in All)
            {
                if (each.Toggle || each.Listed || each.Locked != null || each.Get == null || (each.Choices != null && each.Steps == null)) continue;
                if (meant.Count > 0 && Overlap(meant, each.Key + " " + each.Words) == 0) continue;
                int score = Overlap(words, each.Key) * 3 + Overlap(words, each.Words) * 2;
                if (score > top) { next = top; top = score; best = each; }
                else if (score > next) next = score;
            }
            if (best == null || top < 3 || next == top) return null;
            string amount = last.Groups[2].Value.TrimStart('+', '-') + last.Groups[3].Value;
            if (last.Groups[1].Success) amount = (Lower.IsMatch(Plain(asked)) ? "-" : "+") + amount;
            else if (last.Groups[2].Value.StartsWith("-", StringComparison.Ordinal)) amount = "-" + amount;
            number = amount;
            return best;
        }

        private static int Named(Tunable one, string name)
        {
            if (one.Choices == null) return -1;
            string asked = Plain(name);
            for (int i = 0; i < one.Choices.Length; i++)
            {
                string head = Plain(one.Choices[i].Split(':', '(')[0]);
                if (head.Length > 2 && (" " + asked + " ").IndexOf(" " + head + " ", StringComparison.Ordinal) >= 0) return i;
            }
            return -1;
        }

        private static string Cap(string text) { return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1); }

        internal static string Change(Tunable one, string said, DateTime now)
        {
            if (one.Listed)
            {
                string[] had = one.GetList();
                string[] wanted = ListTarget(had, said);
                if (string.Join("\n", had) == string.Join("\n", wanted)) return Cap(one.Key) + " is already " + ShownList(had) + ".";
                if (one.Field == "Projects" && Array.Exists(wanted, delegate(string item) { return FullAccess.IsMatch(item) && Array.IndexOf(had, item) < 0; }))
                {
                    ActivityLog.Write("setting", "refused", one.Key + ": | full only in Capri+");
                    return NoFullByVoice;
                }
                if (one.Field == "Projects" && WatchDropped(had, wanted))
                {
                    ActivityLog.Write("setting", "refused", one.Key + ": | watch comes off only in Capri+");
                    return NoUnwatchByVoice;
                }
                if (!Apply(delegate { one.SetList(wanted); }, one)) return "I couldn't change " + one.Key + " right now. Nothing changed.";
                Remember(new TuneChange { What = one, OldList = had, At = now });
                return Done(one, ShownList(had), ShownList(wanted));
            }
            int value;
            string wrong = Target(one, said, out value);
            if (wrong != null) return wrong + " " + Cap(one.Key) + " is " + Now(one) + ", nothing changed.";
            int old = one.Get();
            if (value == old) return Cap(one.Key) + " is already " + Shown(one, old) + ".";
            int kept = value;
            int mask = Secure(one) ? Security.Mask : -1;
            if (!Apply(delegate { one.Set(kept); }, one)) return "I couldn't change " + one.Key + " right now. Nothing changed.";
            Remember(new TuneChange { What = one, Old = old, Mask = mask, At = now });
            return Done(one, Shown(one, old), Shown(one, one.Get()));
        }

        private static bool Apply(Action change, Tunable one)
        {
            return Ui(delegate
            {
                change();
                Action<Tunable> told = Changed;
                if (told != null) told(one);
                else { ForkSettings.Current.Tidy(); ForkSettings.Save(); }
            });
        }

        private static string Done(Tunable one, string from, string to)
        {
            ActivityLog.Write("setting", "changed", one.Key + "  " + from + " -> " + to);
            return "Changed: " + one.Key + " " + from + " -> " + to + ".";
        }

        private static void Remember(TuneChange change)
        {
            lock (gate)
            {
                changes.Add(change);
                if (changes.Count > KeptChanges) changes.RemoveAt(0);
            }
        }

        // A change made outside the tool (his fixed phrases like "be quiet") so "undo that" can take it back too.
        internal static void Record(Tunable one, int old, DateTime now)
        {
            if (one == null || one.Listed || one.Get == null || one.Get() == old) return;
            ActivityLog.Write("setting", "changed", one.Key + "  " + Shown(one, old) + " -> " + Now(one));
            Remember(new TuneChange { What = one, Old = old, At = now });
        }

        internal static bool HasRecent(DateTime now)
        {
            lock (gate) return changes.Count > 0 && (now - changes[changes.Count - 1].At).TotalMinutes <= RecentMinutes;
        }

        internal static void Forget() { lock (gate) changes.Clear(); }

        internal static Tunable LastChanged()
        {
            lock (gate) return changes.Count == 0 ? null : changes[changes.Count - 1].What;
        }

        internal static string Undo(DateTime now)
        {
            TuneChange last;
            lock (gate)
            {
                if (changes.Count == 0) return "There's no setting change of mine to undo.";
                last = changes[changes.Count - 1];
                changes.RemoveAt(changes.Count - 1);
            }
            Tunable one = last.What;
            string from = Now(one);
            bool done = one.Listed ? Apply(delegate { one.SetList(last.OldList); }, one) : last.Mask >= 0 ? Apply(delegate { Restore(last.Mask); }, one) : Apply(delegate { one.Set(last.Old); }, one);
            if (!done) return "I couldn't put " + one.Key + " back right now.";
            string to = Now(one);
            ActivityLog.Write("setting", "undo", one.Key + "  " + from + " -> " + to);
            return "Changed back: " + one.Key + " " + from + " -> " + to + ".";
        }

        private static readonly Regex Back = new Regex(@"^(?:(?:ok|okay|no|nah|hey|actually|wait|capri|now|so|please|just) )*(?:(?:put|change|set|turn|switch|move) (?:it|that|this) back|revert (?:it|that|this)|undo (?:that|it|this|the change|that change)|undo|take (?:it|that) back|go back to (?:how it was|what it was|before))(?: please)?(?: capri)?$");

        internal static bool Reverts(string text) { return Back.IsMatch(Plain(text)); }

        private static readonly Regex Changing = new Regex(@"\b(turn|switch|set|change|make|be|reduce|increase|lower|raise|less|more|stop|start|enable|disable|tone|dial|crank|bump|reset|louder|quieter|faster|slower|fewer|only|go back|put)\b");

        // A short message that names one of her settings and asks for a change goes to her tools first.
        internal static bool Asked(string text, DateTime now)
        {
            string plain = Plain(text);
            if (plain.Length == 0 || plain.Length > 200) return false;
            if (Reverts(plain)) return HasRecent(now);
            List<string> all = new List<string>();
            foreach (string word in plain.Split(' '))
            {
                string cleaned = word.Trim('.', '-', '+');
                if (Changing.IsMatch(cleaned) || (cleaned.Length >= 2 && Array.IndexOf(Stop, cleaned) < 0)) all.Add(cleaned);
            }
            List<string> near = new List<string>(), amounted = new List<string>(), headed = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                if (!Changing.IsMatch(all[i])) continue;
                int left = i - 1, right = i + 1;
                while (left >= 0 && Changing.IsMatch(all[left])) left--;
                while (right < all.Count && Changing.IsMatch(all[right])) right++;
                if (left >= 0) near.Add(all[left]);
                if (right < all.Count) near.Add(all[right]);
                if (Amount.IsMatch(all[i]) && left >= 0) amounted.Add(all[left]);
                if (Amount.IsMatch(all[i]) && right < all.Count) amounted.Add(all[right]);
                if (right < all.Count && !Loose.IsMatch(all[right - 1])) headed.Add(all[right]);
                if (left >= 0) headed.Add(all[left]);
            }
            if (near.Count == 0) return false;
            if (Way.IsMatch(plain)) amounted.AddRange(near);
            List<string> words = Tokens(string.Join(" ", near.ToArray())), amounts = Tokens(string.Join(" ", amounted.ToArray())), heads = Tokens(string.Join(" ", headed.ToArray())), line = Tokens(plain);
            line.RemoveAll(delegate(string word) { return Changing.IsMatch(word); });
            foreach (Tunable one in All)
                if (HeadHit(heads, one.Key) || WholeKey(line, one.Key) || Anchored(words, line, one.Words) || Overlap(amounts, one.Words) >= 1) return true;
            return false;
        }

        private static readonly Regex Way = new Regex(@"\b(up|down)\b");

        private static bool Anchored(List<string> near, List<string> line, string words)
        {
            List<string> has = Tokens(words);
            foreach (string word in near)
            {
                if (!has.Contains(word)) continue;
                foreach (string other in line) if (other != word && has.Contains(other)) return true;
            }
            return false;
        }

        private static readonly Regex Loose = new Regex(@"^(start|make)$");

        private static bool WholeKey(List<string> line, string key)
        {
            List<string> parts = Tokens(key);
            if (parts.Count < 2) return false;
            foreach (string part in parts) if (!line.Contains(part)) return false;
            return true;
        }

        private const int ShortestHead = 4;

        private static bool HeadHit(List<string> asked, string key)
        {
            List<string> parts = Tokens(key);
            if (parts.Count == 0) return false;
            string head = parts[0];
            foreach (string word in asked)
            {
                if (word == head) return true;
                string shorter = word.Length < head.Length ? word : head, longer = word.Length < head.Length ? head : word;
                if (shorter.Length >= ShortestHead && longer.Length == shorter.Length + 1 && longer.StartsWith(shorter, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static readonly Regex Amount = new Regex(@"^(set|less|more|fewer|lower|raise|reduce|increase|tone|dial|crank|bump|louder|quieter|faster|slower)$");
    }
}
