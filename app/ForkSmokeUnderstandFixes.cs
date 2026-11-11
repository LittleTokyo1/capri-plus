using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeUnderstandFixes(List<string> checks)
        {
            if (!Brain.Persona.Contains(Fork.YouRule) || !Hands.Prompt.Contains(Fork.YouRule)) throw new Exception("Her persona or her tool step lost the line that says they means him and he is you.");
            foreach (string head in new[] { Context.KnownHead, Context.PastHead })
                if (head.Contains("about them") || head.Contains("their notes") || head.Contains("unless they asked") || head.Contains("they live") || !head.Contains("about him")) throw new Exception("A notes header still calls him they: " + head);

            LiveData data = new LiveData();
            data.Me = new LiveMe { GameName = "sampleuser" };
            data.Players = new[]
            {
                new LivePlayer { Champion = "Zed", GameName = "sampleuser", Team = "ORDER" },
                new LivePlayer { Champion = "Lee Sin", GameName = "ally", Team = "ORDER" },
                new LivePlayer { Champion = "Annie", GameName = "bob", Team = "CHAOS" },
                new LivePlayer { Champion = "Kai'Sa", GameName = "carl", Team = "CHAOS" },
                new LivePlayer { Champion = "Dr. Mundo", GameName = "dan", Team = "CHAOS" }
            };
            string moment = "They (Zed) just killed Annie.";
            if (GameClaims.Unfounded("Annie got deleted, clean!", moment, data) != null || GameClaims.Unfounded("Zed diff, Annie's crying!", moment, data) != null || GameClaims.Unfounded("Yezzir, that was clean!", moment, data) != null)
                throw new Exception("A game line about the moment itself was dropped.");
            if (GameClaims.Unfounded("Annie down, Kaisa's next!", moment, data) != "Kai'Sa" || GameClaims.Unfounded("Dr. Mundo is running scared!", moment, data) != "Dr. Mundo" || GameClaims.Unfounded("Lee Sin was ganking up there!", moment, data) != "Lee Sin")
                throw new Exception("A game line that names a champion who is not in the moment was kept.");
            if (GameClaims.Unfounded("anything", moment, null) != null || GameClaims.Unfounded(null, moment, data) != null) throw new Exception("The game claim check broke on no data.");

            DateTime now = new DateTime(2026, 11, 3, 23, 0, 0);
            foreach (string asked in new[] { "be less sassy with me", "talk less", "turn off the cs hud", "be more funny", "lower your humor", "turn the talkativeness down", "turn off answers to friends", "stop dancing", "turn on listening mode", "switch to chatty companion", "turn the volume down", "set most check-ins a day to 5", "set heartbeat wake-ups a day to 48", "I want you to change from chatty companion to only key moments right now" })
                if (!Tuning.Asked(asked, now)) throw new Exception("A setting request was not sent to her setting tool: " + asked);
            foreach (string talk in new[] { "start listening to my new playlist", "I start learning Lithuanian songs", "make plans with friends tonight", "make a sandwich with cheese", "tell me a joke", "you're too funny lol" })
                if (Tuning.Asked(talk, now)) throw new Exception("Plain talk was taken as a settings change on one loose word: " + talk);
            checks.Add("PASS understanding fixes: her persona and her tool step say they and the user mean Kasper and he is you, her notes headers call him Kasper and he; a game reaction that names a champion who is not in the moment (an ally or enemy, Kai'Sa as Kaisa, Dr. Mundo) is dropped and logged, his own champion and the moment's champions are fine; a settings request needs the head word of the setting's name (not right after start or make), its whole name, a word of it next to the verb plus another of its words in the line, or one of its words next to an amount word (set, more, less, lower, up, down), so less sassy, more funny, talk less, stop dancing, listening mode, chatty companion, the volume down, the cs hud and set check-ins or wake-ups a day still work, and start listening to a playlist or make plans with friends does not");
        }
    }
}
