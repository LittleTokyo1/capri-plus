using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeSpendBook(List<string> checks, string folder)
        {
            string keptPath = SpendBook.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            TalkState keptTalk = talkState;
            try
            {
                SpendBook.FilePath = Path.Combine(folder, "spend-history.json");
                SpendBook.Forget();
                if (SpendBook.Shown("claude-haiku-5-5") != "Haiku 5.5" || SpendBook.Shown("claude-sonnet-4-5-20250929") != "Sonnet 4.5" || SpendBook.Shown("claude-opus-5-5") != "Opus 5.5" || SpendBook.Shown(null) != "unknown model")
                    throw new Exception("A model's name in the spending tab is wrong: " + SpendBook.Shown("claude-sonnet-4-5-20250929"));
                if (SpendBook.MonthOf(new DateTime(2026, 10, 31, 23, 30, 0, DateTimeKind.Utc)) != "2026-10" || SpendBook.MonthNow != SpendBook.MonthOf(DateTime.UtcNow))
                    throw new Exception("The spending month is not the UTC month.");
                if (SpendBook.UseFor("Companion", "SummarizeChat") != SpendBook.Memory || SpendBook.UseFor("Companion", "Respond") != SpendBook.Chats || SpendBook.UseFor("Hands", "Do") != SpendBook.Jobs
                    || SpendBook.UseFor("League", "Research") != SpendBook.Game || SpendBook.UseFor("Heartbeat", "Next") != SpendBook.Heart || SpendBook.UseFor("Mystery", "Thing") != SpendBook.Other)
                    throw new Exception("A call was put under the wrong use.");
                foreach (string name in SpendBook.ByMethod.Keys)
                    if (typeof(Companion).GetMember(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Length == 0)
                        throw new Exception("A spending use names a method that no longer exists: " + name);

                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Quick.Post = delegate { return Tuple.Create(200, "{\"content\":[{\"type\":\"text\",\"text\":\"Malphite, Lissandra, Galio\"}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":1000,\"output_tokens\":100,\"server_tool_use\":{\"web_search_requests\":1}}}"); };
                League.Research(new Cloud("sk-ant-test", "claude-haiku-5-5"), "counters?", true);
                SpendMonth live = SpendBook.Of(SpendBook.MonthNow);
                if (live == null || live.Uses.Count != 1 || live.Uses[0].Name != SpendBook.Game || live.Models[0].Name != "Haiku 5.5" || live.Searches != 1 || Math.Abs(live.Total - ForkSettings.Current.Spent) > 1e-9)
                    throw new Exception("A real League look-up was not booked under League and Haiku 5.5, or the book and the cap counter disagree.");

                DateTime january = new DateTime(2030, 1, 15, 12, 0, 0, DateTimeKind.Utc);
                SpendBook.Add("claude-haiku-5-5", SpendBook.Chats, 0.10, 0, january, 0.05, 2);
                SpendBook.Add("claude-sonnet-5-5", SpendBook.Game, 0.20, 3, january, 0.05, 2);
                SpendBook.Add("claude-haiku-5-5", SpendBook.Chats, 0.01, 0, january, 0.05, 2);
                SpendBook.Close("2030-01", 0, 0, 1234, 2);
                SpendBook.Add("claude-opus-5-5", SpendBook.Jobs, 0.50, 0, january.AddMonths(1), 0, 0);
                SpendBook.Forget();
                SpendMonth one = SpendBook.Of("2030-01");
                double models = 0, uses = 0;
                if (one != null) { foreach (SpendLine line in one.Models) models += line.Cost; foreach (SpendLine line in one.Uses) uses += line.Cost; }
                if (one == null || Math.Abs(one.Total - 0.36) > 1e-9 || Math.Abs(models - 0.36) > 1e-9 || Math.Abs(uses - 0.36) > 1e-9 || one.Searches != 5 || one.VoiceChars != 1234 || one.PlanRuns != 2)
                    throw new Exception("A month's total, its models, its uses or its counts disagree after reading the file back: " + (one == null ? "missing" : one.Total + " / " + models + " / " + uses + " / " + one.Searches));
                if (one.Models[0].Name != "Sonnet 5.5" || one.Uses.Find(delegate(SpendLine line) { return line.Name == SpendBook.Earlier; }) == null || one.Models.Find(delegate(SpendLine line) { return line.Name == "Haiku 5.5"; }).Calls != 2)
                    throw new Exception("The month's lines are not sorted by cost, lost what was spent before the split, or miscounted calls.");
                List<SpendMonth> all = SpendBook.Months();
                if (all.Count < 2 || all[0].Month != "2030-02" || all[1].Month != "2030-01") throw new Exception("Past months are not newest first.");

                ForkSettings ended = new ForkSettings();
                ended.Month = "2029-12";
                ended.Spent = 1.25;
                ended.Searches = 4;
                ended.VoiceChars = 77;
                ended.Tidy();
                SpendMonth december = SpendBook.Of("2029-12");
                if (december == null || Math.Abs(december.Total - 1.25) > 1e-9 || december.VoiceChars != 77 || december.Searches != 4 || ended.Spent != 0 || ended.Month != SpendBook.MonthNow)
                    throw new Exception("A new month wiped the old month instead of keeping it in the history.");
                List<string> past = ForkSettingsForm.PastMonths("2030-02");
                if (past.Count < 2 || !past[0].StartsWith("2030-01: Claude $0.36, 5 web searches, 1,234 ElevenLabs characters", StringComparison.Ordinal))
                    throw new Exception("The spending tab's past months read wrong: " + (past.Count > 0 ? past[0] : "none"));

                string[] asked = { "how much have I spent this month", "what's my claude spending so far?", "how much elevenlabs have I used this month", "capri how much did you cost me this month", "how much have I spent on claude" };
                string[] notAsked = { "how much does this pizza cost", "how much is the shipping", "what did you spend your day doing", "how much time have I spent in league", "how much have I spent on league skins this month", "set your voice volume to 80" };
                foreach (string line in asked) if (!SpendBook.Asked(line)) throw new Exception("A spending question was not heard: " + line);
                foreach (string line in notAsked) if (SpendBook.Asked(line)) throw new Exception("A line that is not about her spending was taken as one: " + line);
                if (Tuning.Asked("how much have I spent this month", DateTime.Now)) throw new Exception("A spending question went to her setting tool.");

                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.Spent = 0.42;
                ForkSettings.Current.Cap = 20;
                ForkSettings.Current.Searches = 6;
                ForkSettings.Current.VoiceChars = 543;
                string said = SpendBook.Spoken(ForkSettings.Current);
                if (said.IndexOf("Claude $0.42 of your $20.00 cap", StringComparison.Ordinal) < 0 || said.IndexOf("6 web searches", StringComparison.Ordinal) < 0 || said.IndexOf("ElevenLabs 543 characters", StringComparison.Ordinal) < 0 || said.IndexOf("Haiku 5.5", StringComparison.Ordinal) < 0)
                    throw new Exception("Her spending answer is wrong: " + said);
                if (!SpendHeard("how much have I spent this month?", false) || SpendHeard("how much have I spent this month?", true) || SpendHeard("push build 2", false))
                    throw new Exception("Her spending answer was not given from code, or talked over the ElevenLabs agent, or took another line.");
                checks.Add("PASS spending (CAPRI-073): every Claude call is booked by UTC month (like the Anthropic console) under its model (Haiku 5.5, Sonnet 4.5 ...) and its use (chats, memory, League, PC jobs, heartbeat ...; a real League look-up lands under League), the month's total, models and uses agree with the cap counter and survive a restart, a new month keeps the old one in spend-history.json with its ElevenLabs characters instead of wiping it, the SPENDING tab lists past months newest first, and \"how much have I spent this month\" is answered from the counters with no model call (never a shop's price, never the setting tool)");
            }
            finally
            {
                Quick.Post = keptPost;
                talkState = keptTalk;
                ForkSettings.Current = keptSettings;
                SpendBook.FilePath = keptPath;
                SpendBook.Forget();
            }
        }
    }
}
