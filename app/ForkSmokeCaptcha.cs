using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeCaptcha(List<string> checks)
        {
            Sight cloudflare = new Sight { Browser = true, Title = "Just a moment... at nexusmods.com" };
            cloudflare.Words.Add("Performing security verification");
            Sight verify = new Sight { Browser = true, Title = "nexusmods.com at nexusmods.com" };
            verify.Words.Add("Verify you are human by completing the action below.");
            Sight robot = new Sight { Browser = true, Title = "Sign up at example.com" };
            robot.Words.Add("I'm not a robot");
            Sight normal = new Sight { Browser = true, Title = "Mods at nexusmods.com" };
            normal.Words.Add("Search mods");
            Sight article = new Sight { Browser = true, Title = "How captcha solving works at blog.example.com" };
            for (int i = 0; i < 30; i++) article.Targets.Add(new Target { Number = i + 1, Role = "link", Name = "Link " + i });
            article.Words.Add("Captcha systems compared");
            if (!Challenge.Seen(cloudflare) || !Challenge.Seen(verify) || !Challenge.Seen(robot)) throw new Exception("A human check page in her browser was not seen.");
            if (Challenge.Seen(normal) || Challenge.Seen(article) || Challenge.Seen(null)) throw new Exception("An ordinary page was taken for a human check.");
            if (Challenge.Host("https://www.nexusmods.com/cyberpunk2077/mods") != "nexusmods.com" || Challenge.Host("not a url") != "") throw new Exception("A page's site was read wrong.");

            DateTime now = new DateTime(2026, 11, 1, 16, 30, 0);
            Challenge.Forget();
            if (Challenge.Again("nexusmods.com", now)) throw new Exception("A first human check counted as a repeat.");
            Challenge.Handed("nexusmods.com", now);
            if (!Challenge.Again("nexusmods.com", now.AddMinutes(5)) || Challenge.Again("nexusmods.com", now.AddMinutes(11)) || Challenge.Again("example.com", now.AddMinutes(1))) throw new Exception("A repeat human check on the same site was not told apart.");
            Challenge.Forget();
            if (Challenge.Plan(true, false, false) != "later" || Challenge.Plan(false, true, false) != "back" || Challenge.Plan(false, false, true) != "theirs" || Challenge.Plan(false, false, false) != "theirs" || Challenge.Plan(true, false, true) != "later")
                throw new Exception("A human check was handed over the wrong way.");
            string research = "Search the web for League of Legends Zed build Hubris first item: when do people build Hubris";
            if (!Challenge.Skippable(research, research, "mobalytics.gg") || Challenge.Skippable("Search for Capri mod Cyberpunk 2077 on Nexus Mods", "x", "nexusmods.com") || Challenge.Skippable("sign in to my account and download the mod", "x", "example.com"))
                throw new Exception("A research job's blocked site was not skipped, or a named site or an errand was skipped.");
            string once = Challenge.WithSkip(research, "mobalytics.gg"), twice = Challenge.WithSkip(once, "op.gg");
            if (!once.StartsWith(research, StringComparison.Ordinal) || !once.Contains("mobalytics.gg") || Challenge.Skipped(twice).Count != 2 || Challenge.Skippable(research, once, "mobalytics.gg") || Challenge.Skippable(research, twice, "lolalytics.com"))
                throw new Exception("Skipped sites were not carried, or she kept skipping past two: " + twice);
            Challenge.PendingTask = "find the build";
            Challenge.PendingAt = now;
            if (!Challenge.CarryOn("carry on", now.AddMinutes(2)) || !Challenge.CarryOn("Capri, okay I did it", now.AddMinutes(2)) || Challenge.CarryOn("done", now.AddMinutes(1)) || Challenge.CarryOn("carry on with the music", now) || Challenge.CarryOn("carry on", now.AddMinutes(11)))
                throw new Exception("Carry on after his own browser was heard wrong.");
            Challenge.PendingTask = null;
            if (Challenge.CarryOn("carry on", now)) throw new Exception("Carry on with nothing waiting was taken.");
            if (!Challenge.TheirsLine("nexusmods.com").Contains("your own browser") || !Challenge.TheirsLine("nexusmods.com").Contains("carry on")) throw new Exception("The own-browser line was wrong.");

            string[] showing = { "pull up the browser so i can click it for her", "pull up your browser", "show me your browser", "Capri, bring up your browser", "put your browser on my screen", "show the browser" };
            foreach (string ask in showing) if (Challenge.BrowserAsk(ask) != "show") throw new Exception("A request to see her browser was missed: " + ask);
            foreach (string ask in new[] { "hide your browser", "put your browser away", "Capri, hide the browser" }) if (Challenge.BrowserAsk(ask) != "hide") throw new Exception("A request to hide her browser was missed: " + ask);
            foreach (string ask in new[] { "open the browser", "open chrome", "what browser do you use", "show me the browser game", "pull up youtube in the browser" }) if (Challenge.BrowserAsk(ask) != null) throw new Exception("Ordinary talk moved her browser: " + ask);

            Func<Sight> lookWas = Challenge.Look;
            try
            {
                Challenge.Look = delegate { return cloudflare; };
                HerBrowser.LastUrl = "https://www.nexusmods.com/cyberpunk2077/mods";
                Job made = new Job("search nexus mods for capri mods");
                Browsing(made);
                int thinks = 0;
                made.Think = Challenge.Guard(delegate(string system, string user, List<KeyValuePair<string, ToolArguments>> calls) { thinks++; return "done{\"text\":\"Done.\"}"; });
                string said = made.Run();
                if (said == null || !said.Contains(Challenge.Stopped) || thinks != 0 || made.Outcome != "stopped" || Challenge.Hit != "nexusmods.com") throw new Exception("A human check did not stop her browser job before her brain ran: " + said + " / thinks " + thinks + " / " + made.Outcome + " / " + Challenge.Hit);
            }
            finally
            {
                Challenge.Look = lookWas;
                Challenge.Forget();
                Computer.Arm();
                Computer.Finish();
            }
            checks.Add("PASS human checks in her browser: a Cloudflare \"Just a moment\", \"Verify you are human\" or \"I'm not a robot\" page stops her browser job before her brain spends a turn, and (his pick 2026-11-02, Cloudflare fails her browser even when he clicks) a research job skips that site and reruns on other sites (two at most), else it opens in his own browser and his carry on finishes it there with her hands (after his game or when he is back if he can't), while ordinary pages and articles about captchas don't; \"pull up / show / hide your browser\" moves her browser at once, even mid-job, and \"open the browser\" or \"show me the browser game\" don't");
        }
    }
}
