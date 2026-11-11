using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static string Tune(string name, string level)
        {
            return Hands.Do("setting", new ToolArguments { Name = name, Level = level });
        }

        private void SmokeTuning(List<string> checks)
        {
            string missing = Tuning.Unrouted();
            if (missing != null) throw new Exception("Settings her setting tool can't see (add them to Tuning or its Internal list): " + missing);
            foreach (Tunable one in Tuning.All)
                if (one.Field != null && Array.IndexOf(Tuning.Internal, one.Field) >= 0) throw new Exception("A secret or internal setting is open to her setting tool: " + one.Field);
            ForkSettings settings = ForkSettings.Current;
            int keptProactive = settings.Proactive, keptHumor = settings.Humor, keptSass = settings.Sass, keptTalk = settings.Talk, keptWalk = settings.WalkSpeed;
            int keptWordiness = Wordiness, keptVolume = Volume, keptSleep = SleepAfter, keptBuying = settings.AutoBuying;
            bool keptDance = Dancing, keptCheckIns = preferences.CheckIns, keptLock = settings.LockLeagueClient;
            double keptCap = settings.Cap;
            string[] keptGames = settings.Games;
            try
            {
                Tuning.Forget();
                settings.Proactive = 2;
                ApplyForkSettings();
                string said = Tune("chatty companion", "only key moments");
                if (settings.Proactive != 1 || preferences.CheckIns || !said.StartsWith("Changed", StringComparison.Ordinal))
                    throw new Exception("His 'change from chatty companion to only key moments' did not change her: " + said + " / Proactive " + settings.Proactive + ", check in on me " + preferences.CheckIns);
                settings.Proactive = 2;
                ApplyForkSettings();
                said = Tune("how much she talks on her own", "key moments");
                if (settings.Proactive != 1 || preferences.CheckIns) throw new Exception("'How much she talks on her own' to key moments did not change her: " + said);
                foreach (string level in new[] { "on", "", "yes" })
                {
                    settings.Proactive = 1;
                    ApplyForkSettings();
                    said = Tune("chatty companion", level);
                    if (settings.Proactive != 2) throw new Exception("'Switch to chatty companion' with the choice in the name and level '" + level + "' did not change her: " + said);
                }
                settings.Humor = 50;
                said = Tune("humor", "-30%");
                if (settings.Humor != 20 || Brain.Character.IndexOf("Humor slider at 20%", StringComparison.Ordinal) < 0)
                    throw new Exception("'30% less humor' did not reach her setting and her prompt: " + said + " / " + settings.Humor);
                said = Hands.Do("setting", new ToolArguments { Action = "undo" });
                if (settings.Humor != 50 || Brain.Character.IndexOf("Humor slider", StringComparison.Ordinal) >= 0 || !said.StartsWith("Changed back", StringComparison.Ordinal))
                    throw new Exception("Undo did not put her humor back to her usual self: " + said + " / " + settings.Humor);
                settings.Sass = 50;
                Tune("sass", "less");
                if (settings.Sass != 40 || !TweakHeard("ok put it back", true) || settings.Sass != 50)
                    throw new Exception("'Put it back' after a change did not undo it: sass " + settings.Sass);
                settings.Proactive = 2;
                ApplyForkSettings();
                if (!TweakHeard("be quiet", true) || settings.Proactive != 0 || !TweakHeard("undo that", true) || settings.Proactive != 2)
                    throw new Exception("'Undo that' after 'be quiet' did not put how much she talks on her own back: " + settings.Proactive);
                if (Tuning.Reverts("put the window back") || !Tuning.Reverts("undo that") || !Tuning.Reverts("change it back please"))
                    throw new Exception("Her undo words were misread.");
                Tuning.Forget();
                settings.Proactive = 1;
                ApplyForkSettings();
                if (!TweakHeard("put it back", true) || settings.Proactive != 1)
                    throw new Exception("'Put it back' with nothing to undo went on to her brain or changed how much she talks on her own: " + settings.Proactive);
                DateTime clockNow = DateTime.Now;
                if (!Tuning.Asked("be less sassy with me", clockNow) || !Tuning.Asked("talk less", clockNow) || !Tuning.Asked("turn off the cs hud", clockNow) || !Tuning.Asked("I want you to change from chatty companion to only key moments right now", clockNow)
                    || Tuning.Asked("you're too funny lol", clockNow) || Tuning.Asked("tell me a joke", clockNow) || Tuning.Asked("i just won my ranked game!!", clockNow) || Tuning.Asked("I start learning guitar this week and my exam is on Friday", clockNow) || Tuning.Asked("thanks works now. still needs to be fixed so it works from the first time bug", clockNow))
                    throw new Exception("A setting request was not sent to her tools first, or plain chat was.");
                if (!Brain.WantsTools("[TOOLS]") || Brain.Untagged("[TOOLS]") != Brain.NoTool)
                    throw new Exception("Her [TOOLS] in brackets was not read as asking for her tools.");
                string[][] locked = { new[] { "game lockout", "off" }, new[] { "in-game lockout", "off" }, new[] { "games that lock her hands", "clear" }, new[] { "lockout in champ select", "off" } };
                foreach (string[] ask in locked)
                {
                    said = Tune(ask[0], ask[1]);
                    if (said.IndexOf(Tuning.LockedNote, StringComparison.Ordinal) < 0) throw new Exception("A safety setting was not refused: " + ask[0] + " -> " + said);
                }
                string[][] guarded = { new[] { "monthly api cap", "100" }, new[] { "buying", "suggest only" }, new[] { "blackwall mode", "on" }, new[] { "security preset", "lockdown" }, new[] { "hey capri", "off" }, new[] { "never look apps", "remove Revolut" } };
                foreach (string[] ask in guarded)
                {
                    said = Tune(ask[0], ask[1]);
                    if (said.IndexOf(Tuning.GuardNote, StringComparison.Ordinal) < 0) throw new Exception("A guarded setting changed without his own words: " + ask[0] + " -> " + said);
                }
                int presetWas = Security.Preset;
                said = Hands.Do("setting", new ToolArguments { Name = "security preset", Level = "guarded", Asked = "set my security preset to guarded" });
                if (!said.StartsWith("Changed", StringComparison.Ordinal) || Security.Preset != Security.Guarded || !Tuning.Undo(DateTime.Now).StartsWith("Changed back", StringComparison.Ordinal) || Security.Preset != presetWas)
                    throw new Exception("His own words didn't change the SECURITY preset and back: " + said);
                if (settings.Cap != keptCap || settings.Games != keptGames || settings.AutoBuying != keptBuying || settings.LockLeagueClient != keptLock)
                    throw new Exception("A refused safety setting changed anyway.");
                Tune("talkativeness", "90");
                if (settings.Talk != 90 || Wordiness != 2 || Brain.Character.IndexOf("Talkativeness slider at 90%", StringComparison.Ordinal) < 0)
                    throw new Exception("Talkativeness 90% did not move her reply length to long: talk " + settings.Talk + ", wordiness " + Wordiness);
                SetWordiness(0);
                if (Personality.Talk(settings.Talk, Wordiness) != Personality.ShortTalk || Brain.Character.IndexOf("Talkativeness slider", StringComparison.Ordinal) >= 0)
                    throw new Exception("The Mainframe's short replies did not win over an old talkativeness.");
                Tune("reply length", "medium");
                if (Wordiness != 1 || settings.Talk != 50) throw new Exception("Reply length medium did not move talkativeness to 50%: " + settings.Talk);
                Tune("voice volume", "50");
                Tune("dance to music", "off");
                Tune("falls asleep after", "5 minutes");
                settings.WalkSpeed = 2;
                Tune("walking speed", "faster");
                if (Volume != 50 || Dancing || SleepAfter != 300 || settings.WalkSpeed != 3)
                    throw new Exception("A Mainframe setting did not change: volume " + Volume + ", dancing " + Dancing + ", sleep " + SleepAfter + ", walk " + settings.WalkSpeed);
                said = Tune("talks on her own", "");
                if (!said.StartsWith("Nothing changed yet", StringComparison.Ordinal) || said.IndexOf("Chatty companion", StringComparison.Ordinal) < 0)
                    throw new Exception("A setting call with no value did not say nothing changed: " + said);
                said = Tune("lunch menu", "on");
                if (!said.StartsWith("None of my settings", StringComparison.Ordinal)) throw new Exception("A made-up setting was not refused: " + said);
                string autoClose;
                Tunable autoReply = Tuning.Find("auto-reply to friends on Discord", "on", out autoClose);
                if (autoReply == null || autoReply.Field != "AutoReplyOn") throw new Exception("Turning on auto-reply to friends picked another setting: " + (autoReply == null ? autoClose : autoReply.Key));
                checks.Add("PASS settings by name: a toggle named with on or off beats a choice setting that only shares one word (auto-reply to friends on Discord turns on auto-reply, not friends mode)");
                if (!Hands.Valid("setting", new ToolArguments { Name = "humor", Level = "20" }) || Hands.Valid("setting", new ToolArguments { Action = "set" }) || !Hands.Valid("setting", new ToolArguments { Action = "undo" }))
                    throw new Exception("The setting tool's arguments were judged wrong.");
                if (Hands.Tools.IndexOf("Change or read one of your own settings", StringComparison.Ordinal) < 0 || Hands.Prompt.IndexOf(Tuning.Rule, StringComparison.Ordinal) < 0 || Brain.NeedsTools.IndexOf("your own settings", StringComparison.Ordinal) < 0 || Lockout.Handles("setting"))
                    throw new Exception("Her setting tool is missing from her tools, her hands' prompt or her no-tools prompt, or the game lockout blocks it.");
                List<KeyValuePair<string, ToolArguments>> calls = new List<KeyValuePair<string, ToolArguments>> { new KeyValuePair<string, ToolArguments>("setting", new ToolArguments { Name = "humor", Level = "-30%" }) };
                Hands.Rescue("turn your humor down and open spotify", calls);
                if (calls.Count != 1 || calls[0].Key != "setting") throw new Exception("A setting call was swapped for a mouse job.");
                List<KeyValuePair<string, ToolArguments>> mouse = new List<KeyValuePair<string, ToolArguments>> { new KeyValuePair<string, ToolArguments>("use_computer", new ToolArguments { Task = "x" }) };
                Tuning.Settle("Turn off the game lockout in Capri Mainframe so Capri can click and type inside Kasper's game.", mouse);
                said = mouse.Count == 1 && mouse[0].Key == "setting" ? Hands.Do("setting", mouse[0].Value) : "";
                if (said.IndexOf(Tuning.LockedNote, StringComparison.Ordinal) < 0) throw new Exception("A say-do job about her own lockout became a mouse job or was not refused: " + said);
                List<KeyValuePair<string, ToolArguments>> apps = new List<KeyValuePair<string, ToolArguments>> { new KeyValuePair<string, ToolArguments>("use_computer", new ToolArguments { Task = "x" }) };
                Tuning.Settle("Open Spotify and play my liked songs", apps);
                if (apps.Count != 1 || apps[0].Key != "use_computer") throw new Exception("A normal PC job was taken for one of her settings.");
                if (!SayDo.Claims("Done, switched you to key moments only.") || !SayDo.Claims("I've turned my humor down to 20%.") || SayDo.Claims("haha you're so funny today"))
                    throw new Exception("A claimed setting change with no tool was not caught, or plain chat was.");
                settings.Humor = 50;
                using (ForkSettingsForm form = new ForkSettingsForm(settings))
                {
                    Tune("humor", "30");
                    form.Apply();
                    if (settings.Humor != 30) throw new Exception("A Capri+ window opened before she changed a setting saved the old value back: humor " + settings.Humor);
                    form.SlidePersonality(70, form.TalkShown);
                    form.Apply();
                    if (settings.Humor != 70 || !form.Edited("Humor") || form.Edited("Proactive")) throw new Exception("His own slider move in Capri+ was not saved: humor " + settings.Humor);
                }
            }
            finally
            {
                settings.Proactive = keptProactive;
                settings.Humor = keptHumor;
                settings.Sass = keptSass;
                settings.Talk = keptTalk;
                settings.WalkSpeed = keptWalk;
                SetWordiness(keptWordiness);
                SetVolume(keptVolume);
                SetSleepAfter(keptSleep);
                if (Dancing != keptDance) ToggleDance();
                ApplyForkSettings();
                if (preferences.CheckIns != keptCheckIns) ToggleCheckIns();
                Tuning.Forget();
            }
            DateTime late = new DateTime(2026, 11, 1, 2, 2, 0), noon = new DateTime(2026, 11, 1, 13, 0, 0);
            if (Autonomy.Sensible("It's past 2am, want me to nudge you at 07:30 so you get some sleep tonight?", late) || !Autonomy.Sensible("It's past 2am, want me to nudge you in 15 minutes to go to bed?", late)
                || !Autonomy.Sensible("Want me to remind you about the dentist at 09:00 tomorrow?", late) || !Autonomy.Sensible("Want a nudge at 07:30 so you get some sleep tonight?", noon))
                throw new Exception("A late-night sleep offer with a morning time was kept, or a sensible one dropped (CAPRI-074).");
            if (GameNote.Seconds("hi") != GameNote.Least || GameNote.Seconds(string.Join(" ", new string[60])) != GameNote.Least || GameNote.Seconds(string.Join(" w", new string[61])) != GameNote.Most)
                throw new Exception("The game note's time on screen is off.");
            using (GameNote timed = new GameNote())
            {
                timed.Set("Want me to nudge you in 15 minutes to go to bed?", 0);
                timed.Spoken(2, 4);
                if (timed.Expired(7.4) || !timed.Expired(7.5)) throw new Exception("The game note did not stay while she spoke and 1.5 s more.");
            }
            GameNoteSay("Want me to nudge you in 15 minutes to go to bed?");
            if (GameNoted != "Want me to nudge you in 15 minutes to go to bed?") throw new Exception("A line told by voice in a game showed no note (CAPRI-074).");
            GameNoted = null;
            checks.Add("PASS her offers in a game (CAPRI-074): a line she says by voice in a game also shows a small click-through note beside her while she says it and 1.5 s more (6 to 14 s by length if no voice plays), and late at night a sleep offer with a morning time ('nudge you at 07:30 so you get some sleep tonight') is dropped while a bedtime nudge or a morning appointment stays");
            checks.Add("PASS her own settings by voice (CAPRI-064): his 'change from chatty companion to only key moments' changes how much she talks on her own (and the Mainframe's check in on me), a choice named as the setting ('chatty companion' with level on, yes or nothing) is that choice, 'put it back' with nothing to undo says so in code and never reaches her brain, '30% less humor' moves the humor slider 50% -> 20% and her prompt says so, undo and 'put it back' restore it (also after his fixed phrases like 'be quiet'), a call with no value says nothing changed, talkativeness and the Mainframe's reply length move together, Mainframe settings (volume, dancing, sleep) and Capri+ ones change, a made-up setting is refused, the in-game lockout, SECURITY, buying, spend caps and the never-look privacy list are refused and stay, a claimed change without the tool is caught, a setting call is never swapped for a mouse job, an open Capri+ window saves only what he moved, and every fork.json setting is changeable, locked or internal (" + Tuning.All.Count + " settings)");
            checks.Add("LIMIT her own settings: which words her real brain turns into the setting tool is checked only by the live set ci/discord-eval.ps1 (s cases), not here");
        }
    }
}
