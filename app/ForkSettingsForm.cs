using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Tone = CapriDesktop.Look;

namespace CapriDesktop
{
    internal sealed partial class ForkSettingsForm : Form
    {
        internal static readonly string[] AutonomyNames = { "Off", "Suggest only" };
        internal static readonly string[] AskNames = { "Off", "Suggest only", "Ask first" };
        internal static readonly string[] DoNames = { "Off", "Suggest only", "Ask first", "Do and tell" };
        internal static readonly string[] ProactiveNames = { "Quiet: only when I talk to her", "Key moments: big plays, wins, errors", "Chatty companion" };
        internal static readonly string[] JapaneseNames = { "Off", "Exclamations and mild cursing", "Slang mix (young Tokyo style)" };
        internal static readonly string[] VisionNames = { "Off", "Only when I ask", "Adaptive (about every 3x N seconds)", "Every N seconds" };
        internal static readonly string[] GameModeNames = { "Stay as normal", "Stay still, no wandering", "Hide until I hold talk", "Go tiny in my game spot (drag her once in game, she remembers)" };
        internal static readonly string[] VoiceBrainNames = { "Capri's brain (Claude, one memory)", "ElevenLabs agent (separate brain)" };
        internal static readonly string[] VoiceEngineNames = { "Her cloned voice (Chatterbox, uses the GPU)", "ElevenLabs", "Free voice" };
        internal static readonly string[] IntenseVoiceNames = { "Keep her cloned voice", "Switch to her fallback voice (no GPU)", "Text only, no voice" };
        internal static readonly string[] FallbackNames = { "ElevenLabs (her free voice if it fails)", "Her free voice" };
        internal static readonly string[] AwayNames = { "Only when I'm away (no mouse or keys for 15 seconds)", "Always" };
        internal static readonly string[] PositionNames = { "Top", "Jungle", "Mid", "Bot", "Support", "Fill" };
        internal static readonly string[] Lanes ={ "Top", "Jungle", "Mid", "Bot (ADC)", "Support" };
        internal static readonly string[] PlanModels = { "opus", "sonnet", "haiku" };
        private const int ShortWidth = 360;
        internal event Action Saved;
        private ForkSettings settings;
        private readonly ForkSettings opened;
        private string[] edits = new string[0];
        private readonly KitSlider humor, sass, slang, talk;
        private KitFlow column;
        private readonly KitTable root = new KitTable();
        private readonly CheckBox planJobs, planFull, often, herName, desktop, logging, coach, popup, research, lockClient, hover, showBuilds, pushButton, pushVoice, pushOnLock, pushSpells, alwaysLook, keepChats, lookups, bargeIn, scribeEars, summarize, lookMouse, lookBoth, heartbeat, nightly, discord;
        private readonly KitNumber chatMinutes;
        private readonly KitSlider micThreshold;
        private readonly TextBox chatSign;
        private readonly KitPick planModel, proactive, japanese, vision, gameMode, voiceBrain, voiceEngine, intenseVoice, fallbackVoice;
        private int openedEngine, openedFallback;
        private readonly TextBox account, bridge, blocklist, games, intenseGames, voiceLab, phone, spotifyId, autoReplyFriends;
        private readonly KitNumber idleMinutes, heartbeatMinutes, heartbeatWakes, nightlyHour, checkInsPerDay, checkInGap, walkSpeed;
        private readonly CheckBox autonomy, checkIns, flyWindows, discordOnline, autoReply, handsByName, handsWindow, handsList, handsRemember, boards, earsHotwords;
        private readonly CheckBox autoAccept, autoLock, csHud, objectiveCalls, everyPop, smarterBrain, autoBan, listenMode;
        private readonly KitPick awayWhen, queueFirst, queueSecond;
        private readonly KitPick autoMail, autoCalendar, autoReminders, autoResearch, friendAnswers, autoReplyMode, studyModel, friendsMode;
        internal static readonly string[] FriendAnswerNames = { "Never, she only forwards them to me", "When I'm away, one \"he's away, I'll pass it on\"", "Simple answers about my status (away, in a game), then tells me" };
        private readonly KitNumber quietFrom, quietTo, talkSeconds, pauseTenths, autoPerDay;
        private readonly KitMeter meter;
        private readonly KitLabel meterText;
        private readonly Timer meterTimer;
        internal Func<double> MicLevel;
        internal readonly TextBox[] Favourites = new TextBox[5];
        private readonly KitNumber cap, voiceBudget, seconds;
        internal readonly KeyBox Hold, Pause, Look, Stop;

        internal ForkSettingsForm(ForkSettings current)
        {
            settings = current;
            opened = current.Copy();
            Text = "Capri+ settings";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Stage.Centre(this);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            Font = Kit.Type(9.5f);
            BackColor = Neon.Ground;
            ForeColor = Neon.White;
            DoubleBuffered = true;
            ShowInTaskbar = true;
            ClientSize = new Size(600, 760);
            MinimumSize = new Size(520, 400);
            Padding = new Padding(Band, TitleRow, Band, Band);
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));
            Controls.Add(root);
            Frame();
            StyleTabs();
            root.Controls.Add(strip, 0, 1);
            root.Controls.Add(host, 0, 2);
            SearchBox();

            Section("BRAIN");
            planJobs = Check("Big PC jobs run on my Claude plan (Claude Code + windows-mcp)", current.PlanJobs);
            planFull = Check("Let big jobs use the shell, files and registry too (off = mouse, keyboard, apps and screen only)", current.PlanFullAccess);
            planModel = Pick("Model for big jobs", PlanModels, Math.Max(0, Array.IndexOf(PlanModels, current.PlanModel)));
            account = Field("Separate plan account folder (empty = your normal Claude login)", current.PlanAccount, false);
            bridge = Field("Bridge folder", current.BridgeFolder, false);
            smarterBrain = Check("Smarter brain: Sonnet 5.5 first reads an unclear ask with the talk and what's on your PC (about a second slower on those)", current.SmarterBrain);
            Tab("Brain");
            FreeKeysPage(current);
            Tab(null);

            Section("SPENDING  (months are UTC, like the Anthropic console: a new month starts at 01:00 your time, 02:00 in summer)");
            cap = Number("Monthly API cap in USD (0 = no paid API calls)", (decimal)current.Cap, 0, 1000, 2);
            SpendClaude(current);
            voiceBudget = Number("ElevenLabs characters per month before the free voice takes over (0 = no limit)", current.VoiceBudget, 0, 10000000, 0);
            SpendVoice(current);
            SpendHistory(current);

            Section("VOICE");
            voiceEngine = Pick("Her voice (the same switch as the Mainframe's Voice row)", VoiceEngineNames, current.VoiceEngine);
            fallbackVoice = Pick("Fallback when her cloned voice can't speak (loading, broken, or a heavy game)", FallbackNames, Math.Max(0, Math.Min(1, current.Fallback)));
            openedEngine = voiceEngine.SelectedIndex;
            openedFallback = fallbackVoice.SelectedIndex;
            voiceLab = Field("Folder with her cloned voice (cb\\Scripts\\pythonw.exe and refv2.wav inside; empty = Capri's own voice folder)", current.ChatterboxLab, false);
            intenseGames = Field("Very intense games (process names, one per line)", string.Join(Environment.NewLine, current.IntenseGames ?? new string[0]), true);
            GameKeys(intenseGames);
            intenseVoice = Pick("While one of those is running", IntenseVoiceNames, current.IntenseVoice);
            idleMinutes = Number("Unload her cloned voice after this many quiet minutes (frees about 3.5 GB of GPU memory)", current.IdleMinutes, 1, 120, 0);
            voiceBrain = Pick("Voice chat brain", VoiceBrainNames, current.VoiceBrain);
            talkSeconds = Number("Longest message I can say in one go, in seconds (5-60; a longer one is dropped)", current.TalkSeconds, 5, 60, 0);
            pauseTenths = Number("How long a pause ends my message, in tenths of a second (6-30; higher = she waits longer before answering and cuts me off less)", current.PauseTenths, 6, 30, 0);
            bargeIn = Check("Talking over her interrupts her (best with headphones)", current.BargeIn);
            scribeEars = Check("Hear me with ElevenLabs Scribe instead of her offline ears (uses ElevenLabs credits)", current.ScribeEars);
            micThreshold = Slider("Mic sensitivity: how loud you must be before she hears you (left = hears quieter speech, right = ignores more noise)", current.MicThreshold, 3, 60);
            Caption("Mic level right now (talk while voice chat is on; she hears you when the bar passes the yellow mark)");
            meter = new KitMeter { Width = ShortWidth, Height = 18, Margin = new Padding(0, 4, 0, 2), Threshold = micThreshold.Value };
            column.Controls.Add(meter);
            micThreshold.Changed += delegate { meter.Threshold = micThreshold.Value; Metered(); };
            meterText = new KitLabel { Upper = true, MaximumSize = new Size(Wide, 0), ForeColor = Neon.Faint, Margin = new Padding(0, 2, 0, 4) };
            column.Controls.Add(meterText);
            meterTimer = new Timer { Interval = 250 };
            meterTimer.Tick += delegate { Metered(); };
            meterTimer.Start();
            earsHotwords = Check("Hotwords: her ears listen hard for her name, Capri and Hey Capri (the Mainframe's Listen for Capri decides whether she listens at all)", current.EarsHotwords);
            listenMode = Check("Listening mode: she stays quiet unless you say Capri (unlike the Mainframe's Say her name anywhere, which only starts a chat), and learns about you and your friends from your mic and Discord voices, Lithuanian too (keeps facts, never recordings; first use downloads 490 MB of ears)", current.ListenMode);
            friendsMode = Pick("Friends mode: her cloned voice unloads (frees the graphics card) and she listens to you and your friends like listening mode; automatic = in a Discord call with friends while her mic is muted, never switched during a League game", FriendMode.Names, Math.Max(0, Math.Min(2, current.FriendsMode)));
            Tab("Voice");
            NameGatePage(current);
            VoiceInMicPage(current);
            CallModePage(current);
            Tab(null);

            Section("PERSONALITY");
            humor = Percent("Humor: how often she jokes (50% is her usual self; you can also tell her \"30% less humor\")", current.Humor);
            sass = Percent("Sass: how smug and teasing she is (50% is her usual self)", current.Sass);
            slang = Percent("Slang: how much Night City slang she uses (50% is her usual self)", current.Slang);
            talk = Percent("Talkativeness: how long her replies are (25% short, 50% medium, 100% long; moves the Mainframe's reply length too)", Personality.Talk(current.Talk, Brain.Wordiness));
            proactive = Pick("How much she talks on her own", ProactiveNames, current.Proactive);
            japanese = Pick("Japanese", JapaneseNames, current.Japanese);
            often = Check("Japanese often (about every other reply)", current.JapaneseOften);
            herName = Check("Her choice of nicknames (when no nickname is set in the Mainframe)", current.HerChoiceName);
            WarmthRow(current);
            WaysRows(current);
            MoodRows(current);
            SceneRows(current);
            KeyLightsRow(current);
            TriageRow(current);
            walkSpeed = Number("Walking speed (1 = her old pace, 2 = twice as fast)", current.WalkSpeed, 1, 6, 0);
            walkSpeed.Unit = "x";

            Section("AUTONOMY");
            summarize = Check("Memory notes: after a chat she writes lasting things about me into me.md (a very small Claude call; she keeps passwords and addresses out; the Mainframe's Remember our chats keeps the chats themselves)", current.SummarizeChats);
            heartbeat = Check("Heartbeat: every so often she checks my calendar, mail and birthdays for things worth telling me (a tiny Claude call, only when something changed)", current.HeartbeatOn);
            heartbeatMinutes = Number("Minutes between heartbeat checks (0 = never)", current.HeartbeatMinutes, 0, 720, 0);
            heartbeatWakes = Number("Most heartbeat wake-ups a day", current.HeartbeatPerDay, 1, 200, 0);
            nightly = Check("Evening tidy-up: once a day she files her notes and your Inbox into the right vault notes, writes the day's journal and tells me what she did (a small Claude call, only when there is something to file)", current.NightlyOn);
            nightlyHour = Number("Hour of the evening tidy-up (0-23)", current.NightlyHour, 0, 23, 0);
            autonomy = Check("Autonomy: now and then she asks herself how she can help me and offers it (say \"stop everything\" or \"autonomy off\" any time; each area below sets how far she goes; she never sends, buys or deletes anything of mine)", current.AutonomyOn);
            checkIns = Check("Away check-ins: when I'm away she sometimes messages me on Discord (a good morning or evening note, how an event went, a follow-up on something I told her, after a long silence), plus one around the end of my workday (say \"check-ins off\" any time; the Mainframe's Check in on me is her popping over to say hi at the PC)", current.AwayCheckIns);
            WarmNoteRow(current);
            checkInsPerDay = Number("Most check-ins a day (the workday-end message does not count)", Math.Max(1, current.CheckInsPerDay), 1, 10, 0);
            checkInGap = Number("Least hours between two check-ins", Math.Max(1, current.CheckInGapHours), 1, 12, 0);
            checkInGap.Unit = " h";
            autoMail = Pick("Mail (she drafts a reply in Gmail, and never sends it; ask first = after your yes, do and tell = by herself, then she tells you)", DoNames, Math.Max(0, Math.Min(3, current.AutoMail)));
            autoCalendar = Pick("Calendar (she adds an event; say undo that to remove it)", DoNames, Math.Max(0, Math.Min(3, current.AutoCalendar)));
            autoReminders = Pick("Reminders and nudges (she sets a reminder for you)", DoNames, Math.Max(0, Math.Min(3, current.AutoReminders)));
            autoResearch = Pick("Research and prep (she looks it up and writes a note in her vault)", DoNames, Math.Max(0, Math.Min(3, current.AutoResearch)));
            autoPerDay = Number("Most things she does by herself a day (do and tell; over this she asks first; 0 = never by herself)", current.AutoPerDay, 0, 20, 0);
            Note("Messages in my name and buying stay off in this version; she never deletes anything of mine. Quiet hours: no voice or pop-ups from her offers (the Discord message to my phone still goes when I'm away).");
            quietFrom = Number("Quiet hours start (0-23)", current.QuietFrom, 0, 23, 0);
            quietTo = Number("Quiet hours end (0-23)", current.QuietTo, 0, 23, 0);

            Section("HANDS AND PC  (how she works on my PC)");
            flyWindows = Check("When a job starts using a window, she flies beside it first (only the first step on each window)", current.FlyToWindows);
            handsByName = Check("Jobs press buttons by name, so my cursor stays free (my real mouse only when a button has no name)", current.HandsByName);
            handsWindow = Check("Jobs look at just the window they work in, not the whole monitor (faster, sharper)", current.HandsWindowOnly);
            handsList = Check("After a step, jobs read the window's buttons instead of taking a new picture when that is enough (a picture at least every third look)", current.HandsListOnly);
            handsRemember = Check("Jobs remember the steps that worked (by button names, never what was typed) and use them next time", current.HandsRemember);
            Tab("Hands");
            PcChecksPage(current);
            CodeModePage(current);
            DiagramsPage(current);
            Tab(null);

            Section("DISCORD AND CHATS");
            discord = Check("Discord bot: I read your DMs to the bot like typed lines and answer there (say \"connect my Discord\" to pair it)", current.DiscordOn);
            discordOnline = Check("Tell me when a friend comes online on Discord, so I can invite them (anyone in my People notes with a Discord name; once a day each, never in a game or quiet hours) and learn when friends are usually on", current.DiscordOnline);
            keepChats = Check("After she messages someone, keep the conversation going (Discord, WhatsApp, Messenger, any chat app)", current.KeepChats);
            chatMinutes = Number("Stop when it's been quiet this many minutes", current.ChatMinutes, 1, 240, 0);
            chatSign = Field("Put this before her messages, so they know it's her (empty = nothing)", current.ChatSign, false);
            Note("Say \"keep the conversation going\" to start, \"stop talking to him\" to stop.");
            Note("Screenshots are never saved to disk.");
            Tab("Discord");
            Section("FRIENDS MESSAGING HER BOT  (friends on your auto-reply list get auto-reply; everyone else gets the last row)");
            autoReply = Check("Auto-reply: she answers the friends listed below on Discord herself (switch it off when I chat with them from my phone)", current.AutoReplyOn);
            autoReplyMode = Pick("How she auto-replies to them", AutoReply.ModeNames, Math.Max(0, Math.Min(2, current.AutoReplyMode)));
            autoReplyFriends = Field("Friends she auto-replies to (names as on Discord or in my People notes, one per line; or say \"auto-reply to Alex\")", string.Join(Environment.NewLine, current.AutoReplyFriends ?? new string[0]), true);
            friendAnswers = Pick("Friends not on your auto-reply list: when they message her bot (she always tells me: aloud at the PC, on Discord when I'm away), may she answer them herself", FriendAnswerNames, Math.Max(0, Math.Min(2, current.DiscordFriendAnswers)));
            DiscordStatusPage(current);
            Tab(null);

            Section("HOME AND AWAY");
            phone = Field("Your phone's Wi-Fi address, so she can tell when you're out (a MAC like aa-bb-cc-dd-ee-ff, or its IP; empty = no automatic home and away)", current.PhoneAddress, false);
            Note("Say \"I'm leaving\" and she goes quiet and dark; press the pause key or say \"I'm home\" and she is back as you had her. With an address set she also notices on her own after about ten minutes. On the iPhone set Private Wi-Fi Address to Fixed for your home network.");
            Tab("Home");
            PhonePage(current);
            Tab(null);

            Section("VISION AND PRIVACY");
            vision = Pick("Watching games", VisionNames, current.GameVision);
            seconds = Number("N seconds", current.GameSeconds, 2, 600, 0);
            lookMouse = Check("With two monitors she looks at the one my mouse is on (off = the one with the window in front)", current.LookAtMouse);
            lookBoth = Check("When I ask her to look and when she glances, she sees every monitor at once", current.LookBothMonitors);
            desktop = Check("Watch the desktop too, outside games (uses the Mainframe's Watch my screen)", current.DesktopVision);
            blocklist = Field("Never look while these apps or sites are in front (one per line)", string.Join(Environment.NewLine, current.Blocklist ?? new string[0]), true);
            logging = Check("Keep an activity log of everything her hands do", current.LogActions);
            alwaysLook = Check("Always look at my screen when I talk to her (about $0.0001 a message)", current.AlwaysLook);
            lookups = Check("Look things up in the background and just tell me (no browser unless I say open)", current.BackgroundLookup);

            Section("GAMING");
            coach = Check("League coach in champ select", current.LeagueCoach);
            popup = Check("Tell me counters by herself when enemies pick (off: only when I ask)", current.CountersUnasked);
            research = Check("Research counters on current-patch stats sites (about $0.02 per draft)", current.LeagueResearch);
            showBuilds = Check("Show the top 3 pro builds (probuildstats.com) for my champ in champ select", current.ShowBuilds);
            pushButton = Check("Push buttons on the build panel", current.PushButton);
            pushVoice = Check("Push by asking her (\"push build 2\")", current.PushVoice);
            pushOnLock = Check("Push build 1 automatically when I lock in", current.PushOnLock);
            pushSpells = Check("Also push the pro's summoner spells", current.PushSpells);
            hover = Check("Hover my first available favourite for my lane", current.HoverFavorite);
            autoAccept = Check("Accept the match for me (also after a dodge)", current.AutoAccept);
            autoBan = Check("Ban a champion that's strong against my pick (away at once, at the PC with 5 seconds left; nothing if they're all banned)", current.AutoBan);
            everyPop = Check("Accept every queue pop, even when I'm at the PC or queued myself", current.AcceptEveryPop);
            autoLock = Check("Lock in my favourite for me (down the list if banned or taken, else what pros play in my lane; at the PC only with 1 second left, so I never dodge)", current.AutoLock);
            awayWhen = Pick("Accept and lock", AwayNames, current.AwayAlways ? 1 : 0);
            queueFirst = Pick("When she queues me: first position", PositionNames, current.QueueFirst);
            queueSecond = Pick("Second position", PositionNames, current.QueueSecond);
            csHud = Check("Show my CS per minute at the top of the game", current.CsHud);
            objectiveCalls = Check("Call dragon, Baron and Voidgrubs a minute before they spawn", current.ObjectiveCalls);
            Caption("Favourite champs per lane, in order, separated by commas (e.g. Ahri, Syndra, Orianna)");
            string[][] lists = { current.FavTop, current.FavJungle, current.FavMid, current.FavBot, current.FavSupport };
            for (int lane = 0; lane < Lanes.Length; lane++) Favourites[lane] = Field(Lanes[lane], string.Join(", ", lists[lane] ?? new string[0]), false);
            gameMode = Pick("During games she", GameModeNames, current.GameMode);
            games = Field("Games that lock her hands (process names, one per line)", string.Join(Environment.NewLine, current.Games ?? new string[0]), true);
            lockClient = Check("Also lock during League champ select and loading", current.LockLeagueClient);
            column.Controls.Add(new KitLock { Text = "In-game lockout: she never clicks, types or presses keys while a game runs.", Width = Wide, Height = 42, Margin = new Padding(0, 10, 0, 4) });
            Tab("Gaming");
            ClipThatPage(current);
            ClipChainPage();
            LiveReactionsPage();
            StreamCohostPage(current);
            Tab(null);

            Section("STUDIES");
            studyModel = Pick("Brain for her study jobs (school briefing, mail drafts, approvals); they run as background Claude Code sessions on my plan", ClaudeJob.ModelNames, Math.Max(0, Math.Min(1, current.StudyModel)));
            Note("Her own chat brain only passes study work on; this picks the model that does it. My own Windows study tasks keep their own setting.");
            Tab("Studies");
            SchoolSitesPage(current);
            Tab(null);

            ProjectsPage(current);

            Section("FUN  (her extras)");
            boards = Check("Chess and board games: she opens her own board when I ask for one", current.Boards);
            spotifyId = Field("Spotify app Client ID, so the mini player heart knows my liked songs (make a free app at developer.spotify.com with the redirect URI " + SpotifyLibrary.Redirect + ", paste its Client ID here, then say \"connect my Spotify\")", Voice.Unprotect(current.SpotifyId) ?? "", false);
            Tab("Fun");
            ClonesPage(current);
            IconsPage(current);
            ThrowPage(current);
            HologramPage();
            OwnClockPage(current);
            Tab(null);

            Section("HOTKEYS  (click a box, then press a key, combo or mouse button. Backspace clears.)");
            Hold = Key("Hold to talk", current.HoldVk, current.HoldMods);
            Pause = Key("Pause: blind and deaf", current.PauseVk, current.PauseMods);
            Look = Key("Look at my screen now", current.LookVk, current.LookMods);
            Stop = Key("Stop her current job", current.StopVk, current.StopMods);
            Clashes();


            Footer();
            Grouped();
            Watch(this);
            ShowTab(pages[0]);
        }

        internal bool EngineChanged { get { return voiceEngine.SelectedIndex != openedEngine; } }

        internal bool FallbackChanged { get { return fallbackVoice.SelectedIndex != openedFallback; } }

        internal void ShowVoice(int engine, int fallback)
        {
            if (voiceEngine.SelectedIndex == openedEngine && engine != openedEngine) { voiceEngine.SelectedIndex = engine; openedEngine = engine; }
            if (fallbackVoice.SelectedIndex == openedFallback && fallback != openedFallback) { fallbackVoice.SelectedIndex = fallback; openedFallback = fallback; }
        }

        internal void ChooseVoice(int engine, int fallback)
        {
            voiceEngine.SelectedIndex = engine;
            fallbackVoice.SelectedIndex = fallback;
        }

        internal void Voiced()
        {
            openedEngine = voiceEngine.SelectedIndex;
            openedFallback = fallbackVoice.SelectedIndex;
        }

        internal string VoiceShown { get { return VoiceEngineNames[Math.Max(0, voiceEngine.SelectedIndex)] + " | " + FallbackNames[Math.Max(0, fallbackVoice.SelectedIndex)]; } }

        // Only what he changed in this window is saved, so a setting she changed by voice while it was open stays.
        internal void Apply()
        {
            ForkSettings target = settings;
            ForkSettings edited = opened.Copy();
            settings = edited;
            try { Fill(); }
            finally { settings = target; }
            System.Collections.Generic.List<string> changed = new System.Collections.Generic.List<string>();
            foreach (System.Reflection.FieldInfo field in typeof(ForkSettings).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                object now = field.GetValue(edited);
                if (Same(now, field.GetValue(opened))) continue;
                field.SetValue(target, now);
                changed.Add(field.Name);
            }
            edits = changed.ToArray();
            target.Tidy();
        }

        internal bool Edited(string field) { return Array.IndexOf(edits, field) >= 0; }

        private static bool Same(object one, object other)
        {
            string[] these = one as string[], those = other as string[];
            if (these != null || those != null) return these != null && those != null && string.Join("\n", these) == string.Join("\n", those);
            return Equals(one, other);
        }

        private void Fill()
        {
            FillPhone();
            FillVoiceInMic();
            FillCallMode();
            FillClones();
            FillIcons();
            FillCodeMode();
            FillThrow();
            FillPcChecks();
            FillNameGate();
            FillWarmth();
            FillWays();
            FillMoods();
            FillWarmNote();
            FillScenes();
            FillKeyLights();
            FillTriage();
            FillDiagrams();
            FillDiscordStatus();
            FillStreamCohost();
            FillOwnClock();
            FillClipThat();
            FillClipChain();
            FillHologram();
            FillSchoolSites();
            FillLiveReactions();
            FillFreeKeys();
            FillProjects();
            settings.Humor = humor.Value;
            settings.Sass = sass.Value;
            settings.Slang = slang.Value;
            settings.Talk = talk.Value;
            settings.PlanJobs = planJobs.Checked;
            settings.PlanModel = PlanModels[Math.Max(0, planModel.SelectedIndex)];
            settings.PlanAccount = account.Text.Trim().Length == 0 ? null : account.Text.Trim();
            settings.Bridge = bridge.Text.Trim().Length == 0 ? null : bridge.Text.Trim();
            settings.Cap = (double)cap.Value;
            settings.VoiceBudget = (long)voiceBudget.Value;
            if (EngineChanged) settings.VoiceEngine = Math.Max(0, voiceEngine.SelectedIndex);
            settings.Fallback = Math.Max(0, fallbackVoice.SelectedIndex);
            settings.ChatterboxLab = voiceLab.Text.Trim().Length == 0 ? null : voiceLab.Text.Trim();
            settings.IntenseGames = Lines(intenseGames.Text);
            settings.IntenseVoice = Math.Max(0, intenseVoice.SelectedIndex);
            settings.IdleMinutes = (int)idleMinutes.Value;
            settings.VoiceBrain = Math.Max(0, voiceBrain.SelectedIndex);
            settings.BargeIn = bargeIn.Checked;
            settings.TalkSeconds = (int)talkSeconds.Value;
            settings.PauseTenths = (int)pauseTenths.Value;
            settings.ScribeEars = scribeEars.Checked;
            settings.MicThreshold = micThreshold.Value;
            settings.Proactive = Math.Max(0, proactive.SelectedIndex);
            settings.Japanese = Math.Max(0, japanese.SelectedIndex);
            settings.JapaneseOften = often.Checked;
            settings.HerChoiceName = herName.Checked;
            settings.SummarizeChats = summarize.Checked;
            settings.HeartbeatOn = heartbeat.Checked;
            settings.HeartbeatMinutes = (int)heartbeatMinutes.Value;
            settings.HeartbeatPerDay = (int)heartbeatWakes.Value;
            settings.NightlyOn = nightly.Checked;
            settings.NightlyHour = (int)nightlyHour.Value;
            settings.DiscordOn = discord.Checked;
            settings.DiscordFriendAnswers = Math.Max(0, friendAnswers.SelectedIndex);
            settings.AutoReplyOn = autoReply.Checked;
            settings.AutoReplyMode = Math.Max(0, autoReplyMode.SelectedIndex);
            settings.StudyModel = Math.Max(0, studyModel.SelectedIndex);
            settings.AutoReplyFriends = AutoReply.Tidy(Lines(autoReplyFriends.Text));
            settings.DiscordOnline = discordOnline.Checked;
            settings.FlyToWindows = flyWindows.Checked;
            settings.HandsByName = handsByName.Checked;
            settings.HandsWindowOnly = handsWindow.Checked;
            settings.HandsListOnly = handsList.Checked;
            settings.HandsRemember = handsRemember.Checked;
            settings.Boards = boards.Checked;
            settings.EarsHotwords = earsHotwords.Checked;
            settings.SmarterBrain = smarterBrain.Checked;
            settings.ListenMode = listenMode.Checked;
            settings.FriendsMode = Math.Max(0, friendsMode.SelectedIndex);
            settings.WalkSpeed = (int)walkSpeed.Value;
            string phoneBefore = settings.PhoneAddress;
            settings.PhoneAddress = Home.Address(phone.Text);
            if (settings.PhoneAddress != phoneBefore) settings.PhoneIp = null;
            settings.AutonomyOn = autonomy.Checked;
            settings.AwayCheckIns = checkIns.Checked;
            settings.CheckInsPerDay = (int)checkInsPerDay.Value;
            settings.CheckInGapHours = (int)checkInGap.Value;
            settings.AutoMail = Math.Max(0, autoMail.SelectedIndex);
            settings.AutoCalendar = Math.Max(0, autoCalendar.SelectedIndex);
            settings.AutoReminders = Math.Max(0, autoReminders.SelectedIndex);
            settings.AutoResearch = Math.Max(0, autoResearch.SelectedIndex);
            settings.AutoPerDay = (int)autoPerDay.Value;
            settings.QuietFrom = (int)quietFrom.Value;
            settings.QuietTo = (int)quietTo.Value;
            settings.GameVision = Math.Max(0, vision.SelectedIndex);
            settings.GameSeconds = (int)seconds.Value;
            settings.DesktopVision = desktop.Checked;
            settings.LookAtMouse = lookMouse.Checked;
            settings.LookBothMonitors = lookBoth.Checked;
            settings.Blocklist = Lines(blocklist.Text);
            settings.LogActions = logging.Checked;
            settings.HoldVk = Hold.Vk;
            settings.HoldMods = Hold.Mods;
            settings.PauseVk = Pause.Vk;
            settings.PauseMods = Pause.Mods;
            settings.LookVk = Look.Vk;
            settings.LookMods = Look.Mods;
            settings.StopVk = Stop.Vk;
            settings.StopMods = Stop.Mods;
            settings.LeagueCoach = coach.Checked;
            settings.CountersUnasked = popup.Checked;
            settings.LeagueResearch = research.Checked;
            settings.GameMode = Math.Max(0, gameMode.SelectedIndex);
            settings.Games = Lines(games.Text);
            settings.LockLeagueClient = lockClient.Checked;
            settings.PlanFullAccess = planFull.Checked;
            settings.HoverFavorite = hover.Checked;
            settings.AutoAccept = autoAccept.Checked;
            settings.AcceptEveryPop = everyPop.Checked;
            settings.AutoBan = autoBan.Checked;
            settings.AutoLock = autoLock.Checked;
            settings.AwayAlways = awayWhen.SelectedIndex == 1;
            settings.QueueFirst = Math.Max(0, queueFirst.SelectedIndex);
            settings.QueueSecond = Math.Max(0, queueSecond.SelectedIndex);
            settings.CsHud = csHud.Checked;
            settings.ObjectiveCalls = objectiveCalls.Checked;
            settings.ShowBuilds = showBuilds.Checked;
            settings.PushButton = pushButton.Checked;
            settings.PushVoice = pushVoice.Checked;
            settings.PushOnLock = pushOnLock.Checked;
            settings.PushSpells = pushSpells.Checked;
            settings.AlwaysLook = alwaysLook.Checked;
            settings.BackgroundLookup = lookups.Checked;
            settings.KeepChats = keepChats.Checked;
            settings.ChatMinutes = (int)chatMinutes.Value;
            settings.ChatSign = chatSign.Text;
            string spotify = spotifyId.Text.Trim();
            if (spotify != (Voice.Unprotect(settings.SpotifyId) ?? ""))
            {
                settings.SpotifyId = spotify.Length == 0 ? null : Voice.Protect(spotify);
                settings.SpotifyRefresh = null;
                settings.SpotifyAccount = null;
                SpotifyLibrary.Forget();
            }
            settings.FavTop = Champions(Favourites[0].Text);
            settings.FavJungle = Champions(Favourites[1].Text);
            settings.FavMid = Champions(Favourites[2].Text);
            settings.FavBot = Champions(Favourites[3].Text);
            settings.FavSupport = Champions(Favourites[4].Text);
            settings.Tidy();
        }

        internal int MeterValue { get { return meter.Level; } }

        internal string MeterText { get { return meterText.Text; } }

        internal void Metered()
        {
            Func<double> read = MicLevel;
            double level = read == null ? -1 : read();
            if (level < 0)
            {
                meter.Level = 0;
                meterText.ForeColor = level == -2 ? Neon.Hot : Neon.Faint;
                meterText.Text = level == -2 ? "The mic is muted (hold-to-talk key up, or paused), so she isn't listening." : "Start voice chat and talk to see your level here.";
                return;
            }
            int shown = (int)Math.Max(0, Math.Min(meter.Maximum, level));
            meter.Level = shown;
            bool heard = shown >= micThreshold.Value;
            meterText.ForeColor = heard ? Neon.Cyan : Neon.Faint;
            meterText.Text = "Level " + shown + ", she hears you above " + micThreshold.Value + (heard ? " (she hears this)" : " (too quiet for her)");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && meterTimer != null)
            {
                meterTimer.Stop();
                meterTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        internal void Choose(int proactiveIndex, int japaneseIndex, int visionIndex) { proactive.SelectedIndex = proactiveIndex; japanese.SelectedIndex = japaneseIndex; vision.SelectedIndex = visionIndex; }

        internal static string[] Champions(string text)
        {
            System.Collections.Generic.List<string> found = new System.Collections.Generic.List<string>();
            foreach (string name in (text ?? "").Split(',', ';', '\n')) if (name.Trim().Length > 0 && found.Count < 5) found.Add(name.Trim());
            return found.ToArray();
        }

        private static string[] Lines(string text)
        {
            System.Collections.Generic.List<string> found = new System.Collections.Generic.List<string>();
            foreach (string line in (text ?? "").Split('\n')) if (line.Trim().Length > 0) found.Add(line.Trim());
            return found.ToArray();
        }

        private int Wide { get { return Math.Max(420, ClientSize.Width - 90); } }

        private string joinTab;

        // The sections after this go into that tab under their own heading, until Tab(null).
        private void Tab(string name) { joinTab = name; }

        private void Section(string title)
        {
            int open = title.IndexOf("  (", StringComparison.Ordinal);
            string heading = open < 0 ? title : title.Substring(0, open);
            Control joined = null;
            if (joinTab != null) foreach (Control each in pages) if (each.Text == joinTab) joined = each;
            if (joined != null)
            {
                column = (KitFlow)joined.Controls[0];
                Label under = Kit.Heading(heading);
                under.Margin = new Padding(0, 22, 0, 6);
                under.Size = new Size(Wide, 26);
                column.Controls.Add(under);
                if (open < 0) return;
                string aside = title.Substring(open + 3).TrimEnd(')', ' ');
                Note(char.ToUpperInvariant(aside[0]) + aside.Substring(1));
                return;
            }
            KitPanel page = new KitPanel { Text = TabName(title), Dock = DockStyle.Fill, Visible = false };
            column = new KitFlow { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(22, 6, 22, 12) };
            KitFlow made = column;
            made.HandleCreated += delegate { Kit.Dark(made); };
            page.Controls.Add(column);
            pages.Add(page);
            host.Controls.Add(page);
            AddTab(page);
            Label head = Kit.Heading(heading);
            head.Margin = new Padding(0, 8, 0, 6);
            head.Size = new Size(Wide, 26);
            column.Controls.Add(head);
            if (open < 0) return;
            string note = title.Substring(open + 3).TrimEnd(')', ' ');
            Note(char.ToUpperInvariant(note[0]) + note.Substring(1));
        }

        private void Note(string text)
        {
            column.Controls.Add(new KitLabel { Text = text, MaximumSize = new Size(Wide, 0), ForeColor = Neon.Faint, Font = Kit.Type(8.5f), Margin = new Padding(0, 2, 0, 6) });
        }

        private void Hint(string text, int indent)
        {
            if (string.IsNullOrEmpty(text)) return;
            string first = text.Split(' ')[0];
            string shown = first.IndexOfAny(new[] { '\\', '/', '.' }) >= 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
            column.Controls.Add(new KitLabel { Text = shown, MaximumSize = new Size(Wide - indent, 0), ForeColor = Neon.Faint, Font = Kit.Type(8.5f), Margin = new Padding(indent, 0, 0, 4) });
        }

        private void Caption(string text)
        {
            string[] parts = Kit.Split(text);
            column.Controls.Add(new KitLabel { Text = parts[0], Tag = Kit.Caption, MaximumSize = new Size(Wide, 0), ForeColor = Neon.Rose, Margin = new Padding(0, 10, 0, 2) });
            Hint(parts[1], 0);
        }

        private CheckBox Check(string text, bool value)
        {
            KitToggle box = new KitToggle { Text = text, Checked = value, AutoSize = false, Size = new Size(Wide, 22), Margin = new Padding(0, 8, 0, 0), AccessibleName = Kit.Split(text)[0] };
            column.Controls.Add(box);
            Hint(Kit.Split(text)[1], 0);
            return box;
        }

        private KitPick Pick(string text, string[] choices, int value)
        {
            Caption(text);
            KitPick box = new KitPick(choices, value) { Width = Math.Min(ShortWidth, Wide), Height = 28, Margin = new Padding(0, 2, 0, 4), AccessibleName = Kit.Split(text)[0] };
            column.Controls.Add(box);
            return box;
        }

        private KitSlider Slider(string text, int value, int low, int high)
        {
            Caption(text);
            KitSlider bar = new KitSlider(low, high, value) { Width = Math.Min(ShortWidth, Wide), Height = 28, Margin = new Padding(0, 2, 0, 4), AccessibleName = Kit.Split(text)[0] };
            column.Controls.Add(bar);
            return bar;
        }

        private KitSlider Percent(string text, int value)
        {
            KitSlider bar = Slider(text, value, 0, 100);
            KitLabel shown = new KitLabel { Text = bar.Value + "%", AutoSize = true, ForeColor = Neon.Cyan, Margin = new Padding(0, 0, 0, 4) };
            bar.Changed += delegate { shown.Text = bar.Value + "%"; };
            column.Controls.Add(shown);
            return bar;
        }

        internal int TalkShown { get { return talk.Value; } }

        internal void SlidePersonality(int humorValue, int talkValue) { humor.Value = humorValue; talk.Value = talkValue; }

        private TextBox Field(string text, string value, bool lines)
        {
            Caption(text);
            KitEntry entry = new KitEntry(lines) { Width = Wide, Height = lines ? 84 : 28, Margin = new Padding(0, 2, 0, 4) };
            entry.Box.Text = value ?? "";
            entry.Box.AccessibleName = Kit.Split(text)[0];
            column.Controls.Add(entry);
            return entry.Box;
        }

        private KitNumber Number(string text, decimal value, decimal low, decimal high, int places)
        {
            Caption(text);
            KitNumber box = new KitNumber(low, high, places, value) { Height = 28, Margin = new Padding(0, 2, 0, 4), AccessibleName = Kit.Split(text)[0] };
            box.Width = box.Small ? ShortWidth : 180;
            if (places == 2) box.Prefix = "$";
            column.Controls.Add(box);
            return box;
        }

        private void Stat(string big, string rest, double share, string below)
        {
            column.Controls.Add(new KitStat { Big = big, Rest = rest, Share = Math.Max(0, Math.Min(1, share)), Below = below, Text = big + rest + ". " + below, Width = Wide, Height = 64, Margin = new Padding(0, 10, 0, 4) });
        }

        private KeyBox Key(string text, int vk, int mods)
        {
            Caption(text);
            KeyBox box = new KeyBox(vk, mods) { Width = 260, Height = 28, Margin = new Padding(0, 2, 0, 4), AccessibleName = text };
            column.Controls.Add(box);
            return box;
        }

        private void Clashes()
        {
            KeyBox[] all = { Hold, Pause, Look, Stop };
            foreach (KeyBox box in all) box.Bound += delegate { Clashed(all); };
            Clashed(all);
        }

        private static void Clashed(KeyBox[] all)
        {
            foreach (KeyBox box in all)
            {
                bool clash = false;
                foreach (KeyBox other in all) if (other != box && box.Vk != 0 && other.Vk == box.Vk && other.Mods == box.Mods) clash = true;
                if (clash == box.Clash) continue;
                box.Clash = clash;
                box.Invalidate();
            }
        }
    }

    internal sealed class KeyBox : Button
    {
        internal int Vk;
        internal int Mods;
        internal bool Clash;
        internal event Action Bound;
        private bool listening;
        private bool hot;

        internal KeyBox(int vk, int mods)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Neon.Ground;
            Cursor = Cursors.Hand;
            Vk = vk;
            Mods = mods;
            Text = Describe(vk, mods);
            Click += delegate { if (!listening) Listen(); };
            MouseDown += Clicked;
            LostFocus += delegate { if (listening) { listening = false; Text = Describe(Vk, Mods); } };
        }

        internal bool Listening { get { return listening; } }

        private void Listen()
        {
            listening = true;
            Text = "Press a key or mouse button...";
        }

        private void Clicked(object sender, MouseEventArgs e)
        {
            if (!listening) return;
            int button = e.Button == MouseButtons.Middle ? 0x04 : e.Button == MouseButtons.XButton1 ? 0x05 : e.Button == MouseButtons.XButton2 ? 0x06 : 0;
            if (button == 0) return;
            Set(button, Held(ModifierKeys));
        }

        protected override bool IsInputKey(Keys key) { return listening || base.IsInputKey(key); }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (!listening) return base.ProcessCmdKey(ref message, keyData);
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin) return true;
            if (key == Keys.Escape) { listening = false; Text = Describe(Vk, Mods); return true; }
            if (key == Keys.Back) { Set(0, 0); return true; }
            Set((int)key, Held(keyData));
            return true;
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = Kit.Unit(this);
            Graphics graphics = e.Graphics;
            Kit.Backdrop(graphics, this, s);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color edge = listening ? Tone.Yellow : Clash ? Neon.Hot : hot || (Focused && ShowFocusCues) ? Neon.Cyan : Neon.WellEdge;
            float line = Math.Max(1f, s);
            using (GraphicsPath plate = Kit.Key(new RectangleF(line / 2, line / 2, Width - line, Height - line), s))
            {
                using (Brush fill = new SolidBrush(hot ? Kit.Hover : Neon.Well)) graphics.FillPath(fill, plate);
                using (Pen pen = new Pen(edge, line)) graphics.DrawPath(pen, plate);
            }
            string shown = listening ? "PRESS A KEY..." : Vk <= 0 ? "NOT SET" : Text;
            Color ink = listening ? Tone.Yellow : Vk <= 0 ? Neon.Faint : Neon.White;
            Rectangle area = new Rectangle((int)(12 * s), 0, Width - (int)(24 * s), Height);
            TextRenderer.DrawText(graphics, shown, Font, area, ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (!listening) return;
            Size size = TextRenderer.MeasureText(shown, Font);
            using (Brush caret = new SolidBrush(Tone.Yellow)) graphics.FillRectangle(caret, area.Left + size.Width - 2 * s, (Height - 14 * s) / 2, 7 * s, 14 * s);
        }

        internal void Set(int vk, int mods)
        {
            Vk = vk;
            Mods = vk == 0 ? 0 : mods;
            listening = false;
            Text = Describe(Vk, Mods);
            if (Bound != null) Bound();
        }

        internal static int Held(Keys keys)
        {
            int mods = 0;
            if ((keys & Keys.Alt) != 0) mods |= 1;
            if ((keys & Keys.Control) != 0) mods |= 2;
            if ((keys & Keys.Shift) != 0) mods |= 4;
            return mods;
        }

        internal static string Describe(int vk, int mods)
        {
            if (vk <= 0) return "Not set";
            string name = vk == 0x04 ? "Middle mouse" : vk == 0x05 ? "Mouse 4 (back)" : vk == 0x06 ? "Mouse 5 (forward)" : ((Keys)vk).ToString();
            string prefix = ((mods & 8) != 0 ? "Win + " : "") + ((mods & 2) != 0 ? "Ctrl + " : "") + ((mods & 1) != 0 ? "Alt + " : "") + ((mods & 4) != 0 ? "Shift + " : "");
            return prefix + name;
        }
    }
}
