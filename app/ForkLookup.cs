using System;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // Capri+ lookups: a question she'd answer by opening a search is looked up on the web in the background instead, and she
    // just says the answer. Nothing opens or takes focus, so it works mid-game too.
    internal sealed partial class Companion
    {
        internal static readonly Regex WantsPage = new Regex(@"\b(open|show me|pull up|bring up|browser|new tab|in a tab|website|web ?page|youtube|watch)\b", RegexOptions.IgnoreCase);
        private static readonly Regex SearchSite = new Regex(@"^(https?://)?(www\.)?(google|bing|duckduckgo|search\.yahoo|yahoo|yandex|ecosia|brave)(\.|$)", RegexOptions.IgnoreCase);
        private static readonly Regex PageSite = new Regex(@"^(https?://)?(www\.)?(youtube|youtu\.be|twitch|netflix)(\.|$)", RegexOptions.IgnoreCase);
        internal const string LookupTool = "[{\"type\":\"web_search_20250305\",\"name\":\"web_search\",\"max_uses\":2}]";

        // A search with no page they asked to see: answer it in the background. In a game it always is (whatever the brain wrote as
        // the site, "google" or empty included), since nothing may open; only a video or streaming site still gets the lockout refusal.
        internal static bool WantsAnswer(ToolArguments args, string asked)
        {
            if (args == null || string.IsNullOrWhiteSpace(args.Search)) return false;
            string site = (args.Site ?? "").Trim();
            if (Lockout.Active) return !PageSite.IsMatch(site);
            if (site.Length > 0 && !SearchSite.IsMatch(site)) return false;
            return ForkSettings.Current.BackgroundLookup && !WantsPage.IsMatch(asked ?? "");
        }

        // Lets the smoke test open nothing for real.
        internal static Func<string, ToolArguments, string> HandsDo = Hands.Do;
        private static readonly string[,] WebApps =
        {
            { "netflix", "netflix.com" }, { "youtube", "youtube.com" }, { "twitch", "twitch.tv" }, { "disney", "disneyplus.com" }, { "disneyplus", "disneyplus.com" },
            { "primevideo", "primevideo.com" }, { "amazonprimevideo", "primevideo.com" }, { "hbomax", "max.com" }, { "hbo", "max.com" }, { "max", "max.com" },
            { "crunchyroll", "crunchyroll.com" }, { "spotify", "open.spotify.com" }, { "chatgpt", "chatgpt.com" }, { "gmail", "mail.google.com" }
        };

        // The website for an "app" that is really a web service, or null.
        internal static string WebApp(string name)
        {
            string plain = League.Plain(name);
            if (plain.Length > 6 && plain.EndsWith("app", StringComparison.Ordinal)) plain = plain.Substring(0, plain.Length - 3);
            for (int i = 0; i < WebApps.GetLength(0); i++) if (WebApps[i, 0] == plain) return WebApps[i, 1];
            return null;
        }

        // open_app for Netflix and friends: the installed app if there is one, otherwise their website.
        private string OpenWebApp(ToolArguments args, string site)
        {
            string result = HandsDo("open_app", args);
            if (result != null && result.StartsWith("No app called", StringComparison.Ordinal))
            {
                string opened = HandsDo("open_website", new ToolArguments { Site = site });
                result = opened;
                ActivityLog.Write("open_app", ActivityLog.Describe(args), "not installed, website instead: " + opened);
            }
            else ActivityLog.Write("open_app", ActivityLog.Describe(args), result);
            System.Collections.Generic.List<string> ran = ranHere;
            if (ran != null && !string.IsNullOrEmpty(result)) ran.Add(result);
            return result;
        }

        internal string Lookup(string question)
        {
            string league = League.Brief;
            string context = string.IsNullOrEmpty(league) || !Regex.IsMatch(question, @"\b(counter|build|rune|matchup|item|league|lol|champ|jungle|lane)\w*", RegexOptions.IgnoreCase) ? "" : "\nTheir League champ select right now: " + league;
            string system = "Look this up on the web and answer from what you find, for the current patch or date. Two or three short sentences of plain facts, no links, no lists." + Context.For(question);
            int status;
            string answer = Quick.Ask(brain.Remote, system, Voice.Quote(question + context), LookupTool, 700, out status);
            if (answer == null && status == 400) answer = Quick.Ask(brain.Remote, system, Voice.Quote(question + context), null, 500, out status);
            ActivityLog.Write("lookup", question, answer ?? "no answer");
            brain.Found(question, answer);
            return answer == null ? null : "Looked it up in the background, nothing opened. What the web says: " + answer;
        }
    }
}
