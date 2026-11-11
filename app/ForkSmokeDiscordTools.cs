using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeDiscordTools(List<string> checks)
        {
            if (!Brain.WantsTools("TOOLS") || !Brain.WantsTools(" TOOLS. ") || !Brain.WantsTools("I'll ask my tools to look. TOOLS") || Brain.WantsTools("I can't use my tools") || Brain.WantsTools("TOOLS are cool"))
                throw new Exception("Her ask for her tools was misread.");
            if (Brain.Untagged("Dunno, choom. TOOLS") != "Dunno, choom." || Brain.Untagged("TOOLS") != Brain.NoTool || Brain.Untagged("Nice play.") != "Nice play.")
                throw new Exception("A leftover TOOLS would be said out loud.");
            if (!Brain.LeftoverTools(false, false, "TOOLS") || Brain.LeftoverTools(true, false, "TOOLS") || Brain.LeftoverTools(false, true, "TOOLS") || Brain.LeftoverTools(false, false, "Done, choom."))
                throw new Exception("A bare TOOLS with no tool step was not thought again.");
            string sentRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "capri-sent-smoke-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(sentRoot, "Capri"));
            DateTime sentNow = new DateTime(2026, 11, 3, 17, 44, 0);
            System.IO.File.WriteAllText(System.IO.Path.Combine(sentRoot, "Capri", "sent-messages.md"), "- 2026-11-03 17:41 to Rudce: you're nothing but a chromed-out gonk\n- 2026-11-03 17:43 to vaidass_: want to play?\n");
            string sentWho;
            bool sentRight = SentAsk.Wants("What exactly did you say to him?", out sentWho) && sentWho == null && SentAsk.Answer(sentRoot, "Rudce", sentNow) == "I sent Rudce: \"you're nothing but a chromed-out gonk\"" && SentAsk.Answer(sentRoot, null, sentNow) == "I sent vaidass_: \"want to play?\"" && SentAsk.Answer(sentRoot, null, sentNow.AddHours(5)) == null && !SentAsk.Wants("what did you say about the weather", out sentWho);
            System.IO.Directory.Delete(sentRoot, true);
            if (!sentRight) throw new Exception("What she sent was not answered word for word from her sent messages, or an old or other line was.");
            if (Glances.Reply("Nope, that's Claude in a chat window, not a game, so nothing to hype here, choom.") != "" || Glances.Reply("Quiet screen, so I'll stay quiet.") != "" || Glances.Reply("Nice dodge, choom!") == "")
                throw new Exception("A glance line about whether to react was said, or a real one was dropped.");
            if (Brain.NeedsTools.IndexOf("Discord", StringComparison.Ordinal) < 0 || Brain.NeedsTools.IndexOf(Brain.ToolsWord, StringComparison.Ordinal) < 0)
                throw new Exception("The no-tools prompt doesn't tell her she can ask for her tools.");
            string carried = Brain.CarryTask("Ask Rudce whether they continue Cyberpunk today and say he misses it.", "ask rudce if were gonna continue playing cyberpunk today? say that u miss it");
            if (carried.IndexOf("say that u miss it", StringComparison.Ordinal) < 0 || Brain.CarryTask("text rudce gg", "text rudce gg") != "text rudce gg")
                throw new Exception("A carried job lost his own words: " + carried);
            if (Hands.Tools.IndexOf("u miss it means I miss it", StringComparison.Ordinal) < 0)
                throw new Exception("The discord tool doesn't say whose words a message speaks.");
            List<DiscordCase> cases = DiscordEval.Cases();
            DiscordCase online = cases.Find(delegate(DiscordCase one) { return one.Id == "o1"; });
            DiscordCase night = cases.Find(delegate(DiscordCase one) { return one.Id == "m1"; });
            DiscordCase miss = cases.Find(delegate(DiscordCase one) { return one.Id == "m2"; });
            List<DiscordCall> none = new List<DiscordCall>();
            List<DiscordCall> asked = new List<DiscordCall> { new DiscordCall { Tool = "discord", Args = new ToolArguments { Action = "online" } } };
            List<DiscordCall> wrongNight = new List<DiscordCall> { new DiscordCall { Tool = "discord", Args = new ToolArguments { Action = "message", Name = "Pieter", Text = "Goodnight Pieter! Kasper just left and went to sleep, so he's offline for the night." } } };
            List<DiscordCall> rightNight = new List<DiscordCall> { new DiscordCall { Tool = "discord", Args = new ToolArguments { Action = "message", Name = "Pieter", Text = "Goodnight Pieter, sleep well!" } } };
            List<DiscordCall> wrongMiss = new List<DiscordCall> { new DiscordCall { Tool = "discord", Args = new ToolArguments { Action = "message", Name = "Rudce", Text = "Yo Rudce, Kasper wanted to know if you're still gonna keep playing Cyberpunk today. He says he misses it, choom." } } };
            if (cases.Count < 20 || online.Check(none, "I can't see who's online from here.") == null || online.Check(asked, "") != null
                || night.Check(wrongNight, "") == null || night.Check(rightNight, "") != null || miss.Check(wrongMiss, "") == null)
                throw new Exception("The Discord eval's checks don't catch today's real misses.");
            checks.Add("PASS Discord tools (CAPRI-059): with no tools she can answer only TOOLS and her tools run at once (who's online, what friends sent her bot, messages), a carried job keeps his own words, a Discord message speaks as Capri to the friend (u means her, he means the friend, nothing about him he didn't say), and the live set ci/discord-eval.ps1 (" + cases.Count + " cases from his real lines, 3 runs each, nothing sent) catches 13:15's 'I can't see who's online', 20:59's 'Kasper left and went to sleep' and 21:41's 'he says he misses it'");
            checks.Add("PASS what she sent and leftover TOOLS: \"what exactly did you say to him\" is answered word for word from her sent messages (the last line in 3 hours, or the last one to the friend he names), a bare TOOLS with no tool step makes her think again instead of saying it, and a screen glance that only says there is nothing to react to stays silent");
            checks.Add("LIMIT Discord tools: how her real brain answers is checked only by the live set ci/discord-eval.ps1, not here");
        }
    }
}
