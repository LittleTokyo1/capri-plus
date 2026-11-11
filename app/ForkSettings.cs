using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CapriDesktop
{
    // Settings only this fork has. They live in fork.json beside settings.json, so upstream's file never changes shape.
    [DataContract]
    internal sealed class ForkSettings
    {
        // Process names without .exe. Online games with anti-cheat; single-player games stay unlocked.
        internal static readonly string[] StandardGames = { "League of Legends", "deadlock", "VALORANT-Win64-Shipping", "cs2", "FortniteClient-Win64-Shipping", "r5apex", "RainbowSix", "EscapeFromTarkov", "Overwatch", "cod" };
        internal static readonly string[] StandardBlocklist = { "KeePass", "1Password", "Bitwarden", "Revolut", "Swedbank", "SEB", "Luminor", "Paysera" };
        internal static ForkSettings Current = new ForkSettings();
        internal static string FilePath;
        private static readonly object saving = new object();

        [DataMember] public string[] Games = StandardGames;
        [DataMember] public bool LockLeagueClient = true;
        [DataMember] public bool LogActions = true;
        [DataMember] public double Cap = 10;
        [DataMember] public string Month;
        [DataMember] public double Spent;
        [DataMember] public int Searches;
        [DataMember] public long VoiceChars;
        [DataMember] public long VoiceBudget;
        [DataMember] public bool PlanJobs = true;
        [DataMember] public string PlanModel = "opus";
        [DataMember] public string PlanAccount;
        [DataMember] public string Bridge;
        [DataMember] public int PlanRuns;
        [DataMember] public int Proactive = 1;
        [DataMember] public int Japanese = 1;
        [DataMember] public bool JapaneseOften;
        [DataMember] public bool HerChoiceName = true;
        [DataMember] public int GameVision = 2;
        [DataMember] public int GameSeconds = 5;
        [DataMember] public bool DesktopVision = true;
        [DataMember] public string[] Blocklist = StandardBlocklist;
        [DataMember] public int HoldVk;
        [DataMember] public int HoldMods;
        [DataMember] public int PauseVk = 0x50;
        [DataMember] public int PauseMods = 3;
        [DataMember] public int LookVk = 0x4C;
        [DataMember] public int LookMods = 3;
        [DataMember] public int StopVk = 0x58;
        [DataMember] public int StopMods = 3;
        [DataMember] public bool Paused;
        [DataMember] public bool LeagueCoach = true;
        [DataMember] public bool CountersUnasked;
        [DataMember] public bool LeagueResearch = true;
        [DataMember] public int GameMode = 3;
        [DataMember] public bool PlanFullAccess;
        [DataMember] public string[] FavTop = new string[0];
        [DataMember] public string[] FavJungle = new string[0];
        [DataMember] public string[] FavMid = new string[0];
        [DataMember] public string[] FavBot = new string[0];
        [DataMember] public string[] FavSupport = new string[0];
        [DataMember] public bool HoverFavorite = true;
        [DataMember] public bool ShowBuilds = true;
        [DataMember] public bool PushButton = true;
        [DataMember] public bool PushVoice = true;
        [DataMember] public bool PushOnLock;
        [DataMember] public bool PushSpells;
        [DataMember] public bool HasGameSpot;
        [DataMember] public int GameSpotX;
        [DataMember] public int GameSpotY;
        [DataMember] public int GameSize = 140;
        [DataMember] public bool AlwaysLook = true;
        [DataMember] public bool BackgroundLookup = true;
        [DataMember] public bool KeepChats = true;
        [DataMember] public int ChatMinutes = 10;
        [DataMember] public string ChatSign = "[Capri] ";
        [DataMember] public int VoiceBrain;
        [DataMember] public bool BargeIn = true;
        [DataMember] public bool ScribeEars;
        [DataMember] public int MicThreshold = 20;
        [DataMember] public int VoiceEngine;
        public int Fallback;
        [DataMember] public string[] IntenseGames = new string[0];
        [DataMember] public int IntenseVoice = 1;
        [DataMember] public int IdleMinutes = 10;
        [DataMember] public string ChatterboxLab;
        [DataMember] public string VaultFolder;
        [DataMember] public bool SummarizeChats = true;
        [DataMember] public bool LookAtMouse = true;
        [DataMember] public bool LookBothMonitors = true;
        [DataMember] public string GoogleId;
        [DataMember] public string GoogleSecret;
        [DataMember] public string GoogleRefresh;
        [DataMember] public string GoogleAccount;
        [DataMember] public string SpotifyId;
        [DataMember] public string SpotifyRefresh;
        [DataMember] public string SpotifyAccount;
        [DataMember] public bool HeartbeatOn = true;
        [DataMember] public int HeartbeatMinutes = 30;
        [DataMember] public int HeartbeatPerDay = 24;
        [DataMember] public bool NightlyOn = true;
        [DataMember] public int NightlyHour = 22;
        [DataMember] public bool DiscordOn = true;
        [DataMember] public string DiscordToken;
        [DataMember] public string DiscordOwner;
        [DataMember] public string DiscordChannel;
        [DataMember] public string DiscordLast;
        [DataMember] public bool DiscordPaired;
        [DataMember] public int DiscordFriendAnswers;
        [DataMember] public bool AutoReplyOn = true;
        [DataMember] public int AutoReplyMode;
        [DataMember] public string[] AutoReplyFriends = new string[0];
        [DataMember] public string[] SchoolSites = new string[0];
        [DataMember] public int StudyModel;
        [DataMember] public bool PhoneOn;
        [DataMember] public int PhonePort = 47123;
        [DataMember] public string PhoneTailnet = "";
        [DataMember] public bool VoiceInMic;
        [DataMember] public string MixMic = "";
        [DataMember] public string MixCable = "";
        [DataMember] public int MixLoud = 100;
        [DataMember] public bool CallMode;
        [DataMember] public bool Clones = true;
        [DataMember] public bool UpsetChaos;
        [DataMember] public bool IconChaos = true;
        [DataMember] public bool FakeCrash = true;
        [DataMember] public bool CodeMode;
        [DataMember] public bool ThrowHer = true;
        [DataMember] public bool PcChecks = true;
        [DataMember] public bool NameInGames;
        [DataMember] public bool Warmth;
        [DataMember] public bool InsideJokes = true;
        [DataMember] public bool HerOpinions = true;
        [DataMember] public bool Moods = true;
        [DataMember] public bool Gestures = true;
        [DataMember] public bool AwayWarmth = true;
        [DataMember] public bool KeyLightsOn = true;
        [DataMember] public bool ExplainDiagrams = true;
        [DataMember] public bool SchoolTriage = true;
        [DataMember] public bool ScenesOn = true;
        [DataMember] public bool SceneAuto = true;
        [DataMember] public string[] WorkSites = new string[0];
        [DataMember] public bool NightOn;
        [DataMember] public bool NightQuietWas;
        [DataMember] public bool NightListenWas;
        [DataMember] public bool NightChatWas;
        [DataMember] public bool NightShushWas;
        [DataMember] public bool FreeKeys;
        [DataMember] public bool RunProjects;
        [DataMember] public string[] Projects = new string[0];
        [DataMember] public int ProjectsPerDay = 3;
        [DataMember] public string ClaudeToken;
        [DataMember] public bool Diagrams = true;
        [DataMember] public bool DiscordStatus;
        [DataMember] public bool StreamCohost = true;
        [DataMember] public bool OwnClock = true;
        [DataMember] public bool ClipThat = true;
        [DataMember] public int FriendsMode = 2;
        [DataMember] public string DiscordSeen;
        [DataMember] public bool DiscordOnline = true;
        [DataMember] public bool VoiceOnStart = true;
        [DataMember] public bool AutonomyOn;
        [DataMember] public bool AwayCheckIns = true;
        [DataMember] public long GoodnightAt;
        [DataMember] public int CheckInsPerDay = 2;
        [DataMember] public int CheckInGapHours = 3;
        [DataMember] public int AutoMail = 1;
        [DataMember] public int AutoCalendar = 1;
        [DataMember] public int AutoMessages;
        [DataMember] public int AutoReminders = 1;
        [DataMember] public int AutoBuying;
        [DataMember] public int AutoResearch = 1;
        [DataMember] public int AutoPerDay = 3;
        [DataMember] public int QuietFrom = 1;
        [DataMember] public int QuietTo = 8;
        [DataMember] public int TalkSeconds = 25;
        [DataMember] public int PauseTenths = 13;
        [DataMember] public string PhoneAddress;
        [DataMember] public string PhoneIp;
        [DataMember] public bool HomeAway;
        [DataMember] public int HomeProactive = 2;
        [DataMember] public bool FlyToWindows = true;
        [DataMember] public bool HandsByName = true;
        [DataMember] public bool HandsWindowOnly = true;
        [DataMember] public bool HandsListOnly = true;
        [DataMember] public bool HandsRemember = true;
        [DataMember] public bool Boards;
        [DataMember] public bool Upstream078;
        [DataMember] public bool EarsHotwords = true;
        [DataMember] public bool FreshStartOn = true;
        [DataMember] public int FreshStartHour = 5;
        [DataMember] public int FreshStartMB = 4096;
        [DataMember] public bool MeaningOn = true;
        [DataMember] public int WalkSpeed = 2;
        [DataMember] public bool CsHud = true;
        [DataMember] public bool ObjectiveCalls = true;
        [DataMember] public bool AutoAccept = true;
        [DataMember] public bool AutoLock = true;
        [DataMember] public bool AwayAlways;
        [DataMember] public bool AcceptEveryPop = true;
        [DataMember] public bool SmarterBrain;
        [DataMember] public bool ListenMode;
        [DataMember] public int ListenCapriWas;
        [DataMember] public bool AutoBan = true;
        [DataMember] public int QueueFirst = 2;
        [DataMember] public int QueueSecond = 1;
        [DataMember] public int Humor = Personality.Usual;
        [DataMember] public int Sass = Personality.Usual;
        [DataMember] public int Slang = Personality.Usual;
        [DataMember] public int Talk = Personality.ShortTalk;

        internal ForkSettings Copy() { return (ForkSettings)MemberwiseClone(); }

        internal string[] Favorites(string role)
        {
            switch (role)
            {
                case "top": return FavTop ?? new string[0];
                case "jungle": return FavJungle ?? new string[0];
                case "middle": return FavMid ?? new string[0];
                case "bottom": return FavBot ?? new string[0];
                case "utility": return FavSupport ?? new string[0];
                default: return new string[0];
            }
        }

        [OnDeserializing]
        private void Defaults(StreamingContext context)
        {
            Games = StandardGames;
            LockLeagueClient = true;
            LogActions = true;
            Cap = 10;
            PlanJobs = true;
            PlanModel = "opus";
            Proactive = 1;
            Japanese = 1;
            HerChoiceName = true;
            GameVision = 2;
            GameSeconds = 5;
            DesktopVision = true;
            Blocklist = StandardBlocklist;
            PauseVk = 0x50;
            PauseMods = 3;
            LookVk = 0x4C;
            LookMods = 3;
            StopVk = 0x58;
            StopMods = 3;
            LeagueCoach = true;
            CountersUnasked = false;
            LeagueResearch = true;
            GameMode = 3;
            FavTop = FavJungle = FavMid = FavBot = FavSupport = new string[0];
            HoverFavorite = true;
            ShowBuilds = true;
            PushButton = true;
            PushVoice = true;
            GameSize = 140;
            AlwaysLook = true;
            BackgroundLookup = true;
            KeepChats = true;
            ChatMinutes = 10;
            ChatSign = "[Capri] ";
            VoiceBrain = 0;
            BargeIn = true;
            ScribeEars = false;
            MicThreshold = 20;
            VoiceEngine = 0;
            IntenseGames = new string[0];
            IntenseVoice = 1;
            IdleMinutes = 10;
            SummarizeChats = true;
            LookAtMouse = true;
            LookBothMonitors = true;
            HeartbeatOn = true;
            HeartbeatMinutes = 30;
            HeartbeatPerDay = 24;
            NightlyOn = true;
            NightlyHour = 22;
            DiscordOn = true;
            DiscordOnline = true;
            AutoReplyOn = true;
            AutoReplyMode = 0;
            AutoReplyFriends = new string[0];
            StudyModel = 0;
            PhoneOn = false;
            PhonePort = 47123;
            PhoneTailnet = "";
            VoiceInMic = false;
            MixMic = "";
            MixCable = "";
            MixLoud = 100;
            CallMode = false;
            Clones = true;
            UpsetChaos = false;
            IconChaos = true;
            FakeCrash = true;
            CodeMode = false;
            ThrowHer = true;
            PcChecks = true;
            NameInGames = false;
            Warmth = false;
            InsideJokes = true;
            HerOpinions = true;
            Moods = true;
            Gestures = true;
            AwayWarmth = true;
            KeyLightsOn = true;
            ExplainDiagrams = true;
            SchoolTriage = true;
            ScenesOn = true;
            SceneAuto = true;
            WorkSites = new string[0];
            NightOn = NightQuietWas = NightListenWas = NightChatWas = NightShushWas = false;
            FreeKeys = false;
            RunProjects = false;
            Projects = new string[0];
            ProjectsPerDay = ProjectRun.StandardPerDay;
            Diagrams = true;
            DiscordStatus = false;
            StreamCohost = true;
            OwnClock = true;
            ClipThat = true;
            FriendsMode = 2;
            VoiceOnStart = true;
            AutonomyOn = false;
            AwayCheckIns = true;
            CheckInsPerDay = 2;
            CheckInGapHours = 3;
            AutoMail = AutoCalendar = AutoReminders = AutoResearch = 1;
            AutoPerDay = 3;
            AutoMessages = AutoBuying = 0;
            QuietFrom = 1;
            QuietTo = 8;
            TalkSeconds = 25;
            PauseTenths = 13;
            PhoneAddress = null;
            PhoneIp = null;
            HomeAway = false;
            HomeProactive = 2;
            FlyToWindows = true;
            EarsHotwords = true;
            HandsByName = HandsWindowOnly = HandsListOnly = HandsRemember = true;
            Boards = false;
            FreshStartOn = true;
            FreshStartHour = 5;
            FreshStartMB = 4096;
            MeaningOn = true;
            WalkSpeed = 2;
            CsHud = ObjectiveCalls = AutoAccept = AutoLock = AcceptEveryPop = AutoBan = true;
            AwayAlways = false;
            QueueFirst = 2;
            QueueSecond = 1;
            Humor = Sass = Slang = Personality.Usual;
            Talk = Personality.ShortTalk;
        }

        internal string BridgeFolder
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Bridge)) return Bridge;
                return Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub"), "capri"), "bridge");
            }
        }

        internal static void Load(string data)
        {
            FilePath = data == null ? null : Path.Combine(data, "fork.json");
            Current = Read(FilePath) ?? new ForkSettings();
            Current.Tidy();
        }

        internal static ForkSettings Read(string path)
        {
            if (path == null || !File.Exists(path)) return null;
            try
            {
                using (FileStream stream = File.OpenRead(path)) return (ForkSettings)new DataContractJsonSerializer(typeof(ForkSettings)).ReadObject(stream);
            }
            catch (Exception error)
            {
                Brain.Log("fork.json", -1, error.GetType().Name + ": " + error.Message);
                return null;
            }
        }

        internal static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            lock (saving)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string temporary = path + ".new";
                    using (FileStream stream = File.Create(temporary)) new DataContractJsonSerializer(typeof(ForkSettings)).WriteObject(stream, Current);
                    if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                }
                catch (Exception error) { Brain.Log("fork.json", -1, error.GetType().Name + ": " + error.Message); }
            }
        }

        internal void Tidy()
        {
            if (Games == null) Games = StandardGames;
            if (Blocklist == null) Blocklist = StandardBlocklist;
            if (Cap < 0) Cap = 0;
            if (Projects == null) Projects = new string[0];
            ProjectsPerDay = Math.Max(1, Math.Min(ProjectRun.MostPerDay, ProjectsPerDay));
            Proactive = Math.Max(0, Math.Min(2, Proactive));
            Japanese = Math.Max(0, Math.Min(2, Japanese));
            GameVision = Math.Max(0, Math.Min(3, GameVision));
            GameSeconds = Math.Max(2, Math.Min(600, GameSeconds));
            GameMode = Math.Max(0, Math.Min(3, GameMode));
            QueueFirst = Math.Max(0, Math.Min(ForkSettingsForm.PositionNames.Length - 1, QueueFirst));
            QueueSecond = Math.Max(0, Math.Min(ForkSettingsForm.PositionNames.Length - 1, QueueSecond));
            GameSize = Math.Max(140, Math.Min(560, GameSize));
            WalkSpeed = Math.Max(1, Math.Min(6, WalkSpeed));
            ChatMinutes = Math.Max(1, Math.Min(240, ChatMinutes));
            VoiceBrain = Math.Max(0, Math.Min(1, VoiceBrain));
            MicThreshold = Math.Max(3, Math.Min(100, MicThreshold));
            VoiceEngine = Math.Max(0, Math.Min(2, VoiceEngine));
            IntenseVoice = Math.Max(0, Math.Min(2, IntenseVoice));
            IdleMinutes = Math.Max(1, Math.Min(120, IdleMinutes));
            if (HeartbeatMinutes != 0) HeartbeatMinutes = Math.Max(5, Math.Min(720, HeartbeatMinutes));
            HeartbeatPerDay = Math.Max(1, Math.Min(200, HeartbeatPerDay));
            NightlyHour = Math.Max(0, Math.Min(23, NightlyHour));
            AutoMail = Math.Max(0, Math.Min(3, AutoMail));
            AutoCalendar = Math.Max(0, Math.Min(3, AutoCalendar));
            AutoPerDay = Math.Max(0, Math.Min(20, AutoPerDay));
            AutoMessages = Math.Max(0, Math.Min(1, AutoMessages));
            DiscordFriendAnswers = Math.Max(0, Math.Min(2, DiscordFriendAnswers));
            AutoReplyMode = Math.Max(0, Math.Min(2, AutoReplyMode));
            StudyModel = Math.Max(0, Math.Min(1, StudyModel));
            if (PhonePort < 1024 || PhonePort > 65535) PhonePort = 47123;
            if (PhoneTailnet == null) PhoneTailnet = "";
            if (MixMic == null) MixMic = "";
            if (MixCable == null) MixCable = "";
            MixLoud = Math.Max(25, Math.Min(200, MixLoud));
            FriendsMode = Math.Max(0, Math.Min(2, FriendsMode));
            AutoReplyFriends = AutoReply.Tidy(AutoReplyFriends);
            AutoReminders = Math.Max(0, Math.Min(3, AutoReminders));
            AutoBuying = Math.Max(0, Math.Min(1, AutoBuying));
            AutoResearch = Math.Max(0, Math.Min(3, AutoResearch));
            QuietFrom = Math.Max(0, Math.Min(23, QuietFrom));
            QuietTo = Math.Max(0, Math.Min(23, QuietTo));
            TalkSeconds = Math.Max(5, Math.Min(60, TalkSeconds));
            PauseTenths = Math.Max(6, Math.Min(30, PauseTenths));
            Humor = Math.Max(0, Math.Min(100, Humor));
            Sass = Math.Max(0, Math.Min(100, Sass));
            Slang = Math.Max(0, Math.Min(100, Slang));
            Talk = Math.Max(0, Math.Min(100, Talk));
            if (IntenseGames == null) IntenseGames = new string[0];
            if (ChatSign == null) ChatSign = "";
            if (FavTop == null) FavTop = new string[0];
            if (FavJungle == null) FavJungle = new string[0];
            if (FavMid == null) FavMid = new string[0];
            if (FavBot == null) FavBot = new string[0];
            if (FavSupport == null) FavSupport = new string[0];
            FreshStartHour = Math.Max(0, Math.Min(23, FreshStartHour));
            FreshStartMB = Math.Max(1024, Math.Min(10240, FreshStartMB));
            if (string.IsNullOrWhiteSpace(PlanModel)) PlanModel = "opus";
            string month = SpendBook.MonthNow;
            if (Month != month)
            {
                SpendBook.Close(Month, Spent, Searches, VoiceChars, PlanRuns);
                Month = month;
                Spent = 0;
                Searches = 0;
                VoiceChars = 0;
                PlanRuns = 0;
            }
        }
    }
}
