using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeShotAfter(List<string> checks)
        {
            if (ShotToDiscord("open capri+ settings first then send me a screenshot on discord") || ShotToDiscord("open spotify and send me a screenshot to my phone"))
                throw new Exception("A screenshot asked after another job was sent at once, before the job.");
            if (!ShotToDiscord("send me a screenshot of my main monitor on discord") || !ShotToDiscord("can you send me a ss of my second monitor in discord"))
                throw new Exception("A plain screenshot ask for his Discord stopped going straight there.");
            string shot;
            string first = ShotAfterFirst("open capri+ settings first then send me a screenshot on discord", out shot);
            if (first != "open capri+ settings" || shot == null || !Discord.WantsShot(shot)) throw new Exception("\"Open Capri+ settings first then send me a screenshot\" was not split into the job and the screenshot: " + first + " / " + shot);
            first = ShotAfterFirst("First open spotify, and then send me a screenshot of my second monitor to my phone", out shot);
            if (first != "open spotify" || ShotPlace(Screens.Wording(shot)) != "your second monitor") throw new Exception("A job, then a screenshot of a named monitor, was split wrong: " + first + " / " + shot);
            foreach (string whole in new[] { "send me a screenshot on discord", "message pieter on discord and tell him hi", "take a screenshot", "send me a screenshot of discord and then open spotify", "open spotify then send me a screenshot" })
                if (ShotAfterFirst(whole, out shot) != null) throw new Exception("A line that is not a job then a screenshot for his Discord was split: " + whole);
            string text = "open capri+ settings first then send me a screenshot on discord";
            string keptWhere = shotAfterWhere;
            try
            {
                ShotAfterSplit(ref text);
                if (text != "open capri+ settings" || shotAfterWhere != "your screen") throw new Exception("Her brain did not get only the job, or the screenshot was not kept for after it: " + text + " / " + shotAfterWhere);
            }
            finally { shotAfterWhere = keptWhere; }
            if (!Hands.Valid("discord", new ToolArguments { Action = "screenshot" }) || Hands.Tools.IndexOf("\"screenshot\"", StringComparison.Ordinal) < 0) throw new Exception("Her brain has no discord screenshot action.");
            Func<string, string> shotWas = DiscordFriends.ShotToHim;
            ForkSettings settings = ForkSettings.Current;
            bool pausedWas = settings.Paused, awayWas = settings.HomeAway;
            string channelWas = settings.DiscordChannel;
            try
            {
                DiscordFriends.ShotToHim = delegate(string monitor) { return "shot of " + monitor; };
                if (DiscordFriends.Do(new ToolArguments { Action = "screenshot", Name = "second" }) != "shot of second") throw new Exception("The discord screenshot action did not reach her screenshot.");
                settings.DiscordChannel = "123";
                settings.Paused = true;
                settings.HomeAway = false;
                if (!DiscordShotTool(null).StartsWith("I'm paused", StringComparison.Ordinal)) throw new Exception("A screenshot was taken while he had paused her himself.");
            }
            finally
            {
                DiscordFriends.ShotToHim = shotWas;
                settings.Paused = pausedWas;
                settings.HomeAway = awayWas;
                settings.DiscordChannel = channelWas;
            }
            if (ShotRefused(true, true) != null || ShotRefused(true, false) == null || ShotRefused(false, false) != null) throw new Exception("A screenshot he asked for was refused while she slept for him being away, or allowed through his own pause.");
            string rootWas = Vault.Root, logWas = ActivityLog.FilePath;
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "capri-archive-tag-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (!Vault.Open(folder, null)) throw new Exception("The archive tag vault did not open.");
                ActivityLog.FilePath = System.IO.Path.Combine(folder, "activity.log");
                DateTime at = new DateTime(2026, 11, 5, 11, 0, 0);
                ChatArchive.From("phone");
                ChatArchive.Add(new[] { new ChatMessage("user", "open capri+ settings"), new ChatMessage("assistant", "Done.") }, DateTime.Now);
                ChatArchive.Add(new[] { new ChatMessage("user", "and at the PC") }, DateTime.Now);
                string day = System.IO.File.ReadAllText(System.IO.Path.Combine(ChatArchive.FolderIn(folder), DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".md"));
                if (!day.Contains(" him: [phone] open capri+ settings") || !day.Contains(" her: Done.") || !day.Contains(" him: and at the PC") || day.Contains("[phone] and at the PC") || ChatArchive.Untagged("[Discord] hi") != "hi")
                    throw new Exception("A line from his phone was not marked in the day's chat log, or a PC line was: " + day);
            }
            finally
            {
                Vault.Root = rootWas;
                ActivityLog.FilePath = logWas;
                try { System.IO.Directory.Delete(folder, true); } catch (Exception) { }
            }
            checks.Add("PASS asleep for him being away (his pick, 5 Nov): only the screenshots he asks for get through, his pause key keeps her eyes off; his phone and Discord lines go into the same day's chat log with [phone] or [Discord] in front, and day pages read them without the mark");
            checks.Add("PASS her discord tool takes a screenshot itself (his phone, 5 Nov): action screenshot sends his screen (the monitor he names) to his DM after the step before it, never while he paused her himself; when she paused only because he is away, his own ask still gets it");
            checks.Add("PASS a job then a screenshot (his phone, 5 Nov): \"open Capri+ settings first then send me a screenshot on Discord\" is split, her brain gets only the job and the screenshot goes to his Discord once her answer lands (after any job she started), while a line that only asks for a screenshot still goes straight from code and answers in the same turn, so the phone never waits for it");
        }
    }
}
