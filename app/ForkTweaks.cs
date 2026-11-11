using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    // Capri+ settings by voice or text: "stay still", "be quiet", "watch my screen", "Japanese off" and the like are changed here,
    // before her brain hears them. Only a whole short command counts, so "tell him to be quiet" or a question that mentions
    // Japanese go to her brain as usual.
    internal sealed partial class Companion
    {
        private const string TweakLead = @"^(?:(?:hey|hi|ok|okay|alright|all right|now|so|well|oh|um|uh|yo|and|then|capri) )*(?:please )?(?:(?:can|could|would|will) you )?(?:please )?(?:just )?(?:now )?";
        private const string TweakTail = @"(?: capri)?(?: for me)?(?: please)?(?: now| again)?(?: capri)?$";
        private const string ScreenWord = @"(?:screen|desktop)";
        private const string SceneLead = @"(?:(?:run|start|do|play|put on|set up|turn on|switch to|go to) (?:the |my |a )?)?";
        private static readonly string[] TweakKeys = { "still", "wander", "quiet", "moments", "chatty", "nolook", "look", "jpoff", "jpon", "goodnight", "heartbeatoff", "heartbeaton", "nightlyoff", "nightlyon", "tidynow", "discordpair", "discordoff", "discordon", "autonomyoff", "autonomyon", "checkinsoff", "checkinson", "away", "home", "undo", "pullback", "scenework", "scenerelax", "scenepentest", "sceneundo" };
        private int goodnightWait = 6000;
        private static readonly Regex[] TweakWords = Array.ConvertAll<string, Regex>(new[]
        {
            @"(?:stop (?:moving|walking|wandering)(?: around)?|stay (?:still|put)|hold still|(?:dont|do not) (?:move|walk|wander)(?: around)?)",
            @"(?:you (?:can|may) (?:move|walk|wander)(?: around)?(?: again)?|(?:start|go back to|resume) (?:moving|walking|wandering)(?: around)?|(?:move|walk|wander) around(?: again)?)",
            @"(?:be (?:quiet|silent)|stay (?:quiet|silent)|shh+|hush|quiet mode|stop (?:checking in|interrupting me|bothering me))",
            @"(?:key moments(?: only)?|only (?:the )?(?:key|big|important) (?:moments|stuff|things)|(?:react|talk|speak) only (?:to|for|on) (?:the )?(?:key|big|important) (?:moments|stuff|things))",
            @"(?:be chatty|chatty mode|be (?:more )?talkative|(?:talk|chat) (?:to me )?more)",
            @"(?:stop (?:watching|looking at) my " + ScreenWord + @"|(?:dont|do not) (?:watch|look at) my " + ScreenWord + ")",
            @"(?:start )?watch my " + ScreenWord,
            @"(?:japanese off|(?:turn|switch) (?:the )?japanese off|(?:turn|switch) off (?:the )?japanese|no more japanese|stop (?:speaking|saying|using) japanese|stop the japanese)",
            @"(?:japanese (?:back )?on|(?:turn|switch) (?:the )?japanese on|(?:turn|switch) on (?:the )?japanese|start (?:speaking|saying|using) japanese(?: again)?)",
            @"(?:good ?night|night night|nighty night|(?:i(?:m| am) )?(?:going|off|heading) (?:to|for) (?:bed|sleep)|time (?:for|to go to) (?:bed|sleep)|(?:go(?:ing)? to )?sleep mode)(?: capri)?",
            @"(?:(?:turn|switch) (?:the )?heartbeat off|(?:turn|switch) off (?:the )?heartbeat|heartbeat off|stop (?:the )?heartbeat)",
            @"(?:(?:turn|switch) (?:the )?heartbeat on|(?:turn|switch) on (?:the )?heartbeat|heartbeat on|start (?:the )?heartbeat)",
            @"(?:(?:turn|switch) (?:the )?(?:nightly|evening) (?:tidy ?up|tidying|filing) off|(?:turn|switch) off (?:the )?(?:nightly|evening) (?:tidy ?up|tidying|filing)|(?:nightly|evening) (?:tidy ?up|tidying|filing) off|stop (?:the )?(?:nightly|evening) (?:tidy ?up|tidying|filing))",
            @"(?:(?:turn|switch) (?:the )?(?:nightly|evening) (?:tidy ?up|tidying|filing) on|(?:turn|switch) on (?:the )?(?:nightly|evening) (?:tidy ?up|tidying|filing)|(?:nightly|evening) (?:tidy ?up|tidying|filing) on|start (?:the )?(?:nightly|evening) (?:tidy ?up|tidying|filing))",
            @"(?:(?:tidy|clean|sort|organi[sz]e|file)(?: up| out)? (?:(?:your |my |the )?(?:notes|vault|memory)|your inbox|(?:the |my |your )?(?:notes|vault) inbox)(?: up| out| away)?|go through (?:(?:your |my |the )?notes|your inbox)|(?:do|run|start) (?:the |your )?(?:nightly |evening )?(?:tidy ?up|tidying|filing|clean ?up))",
            @"(?:(?:connect|link|pair|set ?up)(?: with)?(?: my| the| your)? discord(?: bot)?)",
            @"(?:(?:turn|switch) (?:the )?discord(?: bot)? off|(?:turn|switch) off (?:the )?discord(?: bot)?|discord(?: bot)? off|stop (?:the )?discord(?: bot)?)",
            @"(?:(?:turn|switch) (?:the )?discord(?: bot)? on|(?:turn|switch) on (?:the )?discord(?: bot)?|discord(?: bot)? on|start (?:the )?discord(?: bot)?)",
            @"(?:(?:turn|switch) (?:the )?autonomy off|(?:turn|switch) off (?:the )?autonomy|autonomy off|stop (?:the )?autonomy|stop everything|stop (?:acting|doing things) on your own)",
            @"(?:(?:turn|switch) (?:the )?autonomy on|(?:turn|switch) on (?:the )?autonomy|autonomy on|start (?:the )?autonomy)",
            @"(?:(?:turn|switch) (?:the )?check ?ins off|(?:turn|switch) off (?:the )?check ?ins|check ?ins off|stop (?:the )?check ?ins)",
            @"(?:(?:turn|switch) (?:the )?check ?ins on|(?:turn|switch) on (?:the )?check ?ins|check ?ins on|start (?:the )?check ?ins)",
            @"(?:i(?:m| am) (?:leaving|heading out|going out|(?:off|heading|going) to (?:uni|university|work|school|class|college|the gym))|away mode|going away)",
            @"(?:i(?:m| am) (?:home|back)(?: home)?|home mode|back home|come home)",
            @"(?:undo(?: that| it| this)?|take (?:that|it) back)",
            @"(?:(?:pull|bring|get|put) (?:it|that|the window|that window|my window) back(?: up)?|where did (?:it|that|the window|that window|my window) go|(?:its|it is|the window is|thats|that is) minimi[sz]ed|unminimi[sz]e (?:it|that|the window|my window)|restore (?:it|that|the window|that window|my window))",
            SceneLead + @"(?:lets get (?:to work|working)|time to (?:work|study)|(?:work|study) (?:mode|scene)|lets study)",
            SceneLead + @"(?:(?:relax(?:ing)?|home|chill) (?:mode|scene)|lets relax|time to relax)",
            SceneLead + @"(?:pen ?test(?:ing)? (?:mode|scene)|hack(?:ing|er) (?:mode|time|scene)|lets hack|time to hack)",
            @"(?:undo the scene|scene off|bring (?:back )?my windows(?: back)?|restore my windows)"
        }, delegate(string words) { return new Regex(TweakLead + words + TweakTail, RegexOptions.CultureInvariant); });

        // Which setting a short command changes ("still", "wander", "quiet", "moments", "chatty", "nolook", "look", "jpoff", "jpon", "goodnight"), or null.
        internal static string TweakFor(string text)
        {
            string plain = Regex.Replace(Regex.Replace((text ?? "").ToLowerInvariant().Replace("'", "").Replace("’", ""), "[^a-z0-9 ]+", " "), @"\s+", " ").Trim();
            if (plain.Length == 0 || plain.Length > 80) return null;
            for (int i = 0; i < TweakWords.Length; i++) if (TweakWords[i].IsMatch(plain)) return TweakKeys[i];
            return DiscordAsk.IsMatch(plain) && !DiscordNot.IsMatch(plain) ? "discordpair" : null;
        }

        private static readonly Regex DiscordAsk = new Regex(@"\b(connect|link|pair|set ?up)\b.{0,40}\b(discord|discard|dis cord)\b|\b(discord|discard|dis cord)\b.{0,40}\b(connect|link|pair|set ?up)\b", RegexOptions.CultureInvariant);
        private static readonly Regex DiscordNot = new Regex(@"\b(not|never|dont|disconnect|unlink|tell|ask|later|why|how|what|when|if|to (spotify|steam|twitch|youtube|obsidian|google|league)|with (spotify|steam|twitch|youtube|obsidian|google|league))\b", RegexOptions.CultureInvariant);

        private void SetWander(bool wanted)
        {
            if (preferences.Wander != wanted) ToggleWander();
            wanderWas = false;
        }

        // Changes the setting, saves it and answers with the line she says back.
        private static readonly string[,] TweakTunes =
        {
            { "quiet", "talks on her own" }, { "moments", "talks on her own" }, { "chatty", "talks on her own" }, { "nolook", "watch the desktop" }, { "look", "watch the desktop" },
            { "heartbeatoff", "heartbeat" }, { "heartbeaton", "heartbeat" }, { "nightlyoff", "evening tidy-up" }, { "nightlyon", "evening tidy-up" }, { "autonomyon", "autonomy" },
            { "checkinsoff", "check-ins" }, { "checkinson", "check-ins" }, { "discordoff", "discord bot" }, { "discordon", "discord bot" }, { "jpoff", "japanese" }, { "jpon", "japanese" },
            { "still", "wandering" }, { "wander", "wandering" }
        };

        private static Tunable TweakTune(string key)
        {
            for (int i = 0; i < TweakTunes.GetLength(0); i++) if (TweakTunes[i, 0] == key) return Tuning.Named(TweakTunes[i, 1]);
            return null;
        }

        internal string ApplyTweak(string key)
        {
            Tunable tuned = TweakTune(key);
            int before = tuned == null || tuned.Get == null ? 0 : tuned.Get();
            string said = Tweaked(key);
            if (tuned != null && tuned.Get != null) Tuning.Record(tuned, before, DateTime.Now);
            return said;
        }

        private string Tweaked(string key)
        {
            ForkSettings settings = ForkSettings.Current;
            string said;
            switch (key)
            {
                case "still": SetWander(false); said = "Okay, I'll stay put."; break;
                case "wander": SetWander(true); said = "Okay, I'll wander around again."; break;
                case "quiet": settings.Proactive = 0; said = "Quiet mode. I'll only speak when you talk to me."; break;
                case "moments": settings.Proactive = 1; said = "Key moments only, got it."; break;
                case "chatty": settings.Proactive = 2; said = "Chatty mode on. You asked for it."; break;
                case "nolook": settings.DesktopVision = false; said = "I'll stop watching your screen."; break;
                case "look": settings.DesktopVision = true; said = "Watching your screen again." + (preferences.Watch ? "" : " (Watch my screen is still off in the Mainframe, though.)"); break;
                case "goodnight": said = Goodnight(); break;
                case "heartbeatoff": settings.HeartbeatOn = false; said = "Heartbeat off. I won't check your calendar and mail on my own until you say heartbeat on."; break;
                case "heartbeaton": settings.HeartbeatOn = true; said = "Heartbeat on. I'll keep an eye on your calendar and mail."; break;
                case "nightlyoff": settings.NightlyOn = false; said = "Evening tidy-up off. I won't file my notes on my own until you say evening tidy-up on."; break;
                case "nightlyon": settings.NightlyOn = true; said = "Evening tidy-up on. I'll file my notes every evening."; break;
                case "autonomyoff": settings.AutonomyOn = false; settings.AwayCheckIns = false; said = "Autonomy and check-ins off. I won't offer or message anything on my own until you say autonomy on or check-ins on."; break;
                case "checkinsoff": settings.AwayCheckIns = false; said = "Check-ins off. I won't message you on my own when you're away until you say check-ins on."; break;
                case "checkinson": settings.AwayCheckIns = true; said = "Check-ins on. When you're away I'll message you now and then, two a day at most."; break;
                case "autonomyon": settings.AutonomyOn = true; said = "Autonomy on. I'll suggest things now and then, and I won't touch mail, messages or money on my own."; break;
                case "tidynow": said = TidyNow(); break;
                case "away": said = GoAway(); break;
                case "home": said = HomeAsked(); break;
                case "scenework": said = SceneStart(Scene.Work); break;
                case "scenerelax": said = SceneStart(Scene.Relax); break;
                case "scenepentest": said = SceneStart(Scene.Pentest); break;
                case "sceneundo": said = SceneUndo(); break;
                case "undo": said = Autonomy.UndoLast(DateTime.Now); break;
                case "pullback": said = LostWindows.Bring(DateTime.Now); break;
                case "discordoff": settings.DiscordOn = false; said = "Discord off. I won't read your DMs until you say Discord on."; break;
                case "discordon": settings.DiscordOn = true; said = Discord.Ready ? "Discord on. I'm listening for your DMs." : "Discord on, but I'm not paired yet. Say \"connect my Discord\" first."; break;
                case "discordpair":
                    settings.DiscordOn = true;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        string result = Discord.Pair();
                        Action<string> tell = Discord.Told;
                        if (tell != null) tell(result);
                    });
                    said = "Setting up Discord. One moment.";
                    break;
                case "jpoff": settings.Japanese = 0; said = "No more Japanese."; break;
                default: settings.Japanese = Math.Max(1, settings.Japanese); said = "Japanese is back on."; break;
            }
            ApplyForkSettings();
            return said;
        }

        private string Goodnight()
        {
            bool game = Lockout.Active;
            ForkSettings.Current.GoodnightAt = DateTime.UtcNow.Ticks;
            if (!testing) ForkSettings.Save();
            ActivityLog.Write("night", "goodnight", null);
            int wait = goodnightWait;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(wait);
                Post(delegate { NightMute(); });
                if (!game && !Lockout.Active) Hands.Do("power", new ToolArguments { Action = "screens_off" });
            });
            return game ? Night.GameLine : Night.Line;
        }

        internal const string NothingToUndo = "Nothing of mine to undo. Undo takes back a setting of mine changed in the last 10 minutes, or a calendar event or mail draft I added on my own in the last hour.";

        private bool TweakHeard(string text, bool voiceChat)
        {
            if (MoveHeard(text, voiceChat) || ScheduleHeard(text, voiceChat) || MicHeard(text, voiceChat)) return true;
            if (!testing && Autonomy.Answered(text, DateTime.Now) == Autonomy.Yes && AskedFirst()) return true;
            if (Tuning.HasRecent(DateTime.Now) && Tuning.Reverts(text))
            {
                string back = Tuning.Undo(DateTime.Now);
                if (!voiceChat) Tell("(settings)", back);
                return true;
            }
            if (Tuning.Reverts(text) && !Autonomy.HasUndo(DateTime.Now))
            {
                ActivityLog.Write("setting", "undo", "nothing to undo");
                if (!voiceChat) Tell("(settings)", NothingToUndo);
                return true;
            }
            string key = TweakFor(text);
            if (key == null) return false;
            if (key == "undo")
            {
                if (!Autonomy.HasUndo(DateTime.Now))
                {
                    ActivityLog.Write("setting", "undo", "nothing to undo");
                    Tell("(settings)", NothingToUndo);
                    return true;
                }
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string undone = Autonomy.UndoLast(DateTime.Now);
                    ActivityLog.Write("setting", "undo", undone);
                    Post(delegate { Tell(Bubble.Outcome(undone, "(undo)"), undone); });
                });
                return true;
            }
            string said = ApplyTweak(key);
            ActivityLog.Write("setting", key, said);
            if (!voiceChat || key == "goodnight") Tell("(settings)", said);
            return true;
        }
    }
}
