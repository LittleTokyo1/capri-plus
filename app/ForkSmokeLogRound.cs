using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeLogRound(List<string> checks, string folder)
        {
            string clock = ToolClock.Line(new DateTime(2026, 11, 3, 0, 18, 0));
            if (!clock.Contains("Tuesday 3 November 2026, 00:18") || !clock.Contains("2026-11-03T00:18")) throw new Exception("Her tool step's clock line was wrong: " + clock);
            string leaked = "The tool result says I stopped mid-match and won't touch the game until it's over, so I need to answer that in one short sentence. Krillion is a game the user wants me to play by typing answers, and the result shows I held off because anti-cheat might flag it. I should say that plainly, in character, with no praise for them. Anti-cheat would flag me, so I'm sitting this one out until your game's done, choom.";
            if (Musing.Strip(leaked) != "Anti-cheat would flag me, so I'm sitting this one out until your game's done, choom.") throw new Exception("Her reasoning leaked into the reply: " + Musing.Strip(leaked));
            if (Brain.Clean(leaked).Contains("tool result")) throw new Exception("Her reply cleaner let her reasoning through.");
            foreach (string plain in new[] { "Set it for 8:41, so you've got seven minutes to get that bread in the oven.", "Double kill, Zed! Two of them down and the enemy's looking nervous.", "Sent it to your Discord, choom.", "Got it. I will keep it in mind for your next game.", "Rudce's online, I will tell him you're coming.", "Checked it for you. The result says it opens at 9." })
                if (Musing.Strip(plain) != plain) throw new Exception("A normal reply was cut as if it were reasoning: " + plain);
            string watch = "Rudeminas to come online on Discord, then message him to hop on Cyberpunk";
            if (!WatchNames.Close(watch, "Rudiminas to come online") || WatchNames.Close(watch, "Pieter to come online") || WatchNames.Close(watch, "") || WatchNames.Close("a red boss bar shows", "the timer"))
                throw new Exception("Stopping a screen watch by a close spelling of a name was wrong.");
            if (!ForkLiveSide()) throw new Exception("Her game reactions are not told the event's They means him and their team is his.");
            SmokeLately(folder);
            SmokeTidyNote();
            checks.Add("PASS log round 2026-11-03: her tool step always gets the 24-hour time and a start-time form (\"in like 30min\" at 00:18 no longer \"already passed\"), her own reasoning is cut from a reply, \"stop watching for Rudiminas\" stops the Dominicas watch, game reactions say your team, ideas read the last 20 hours of talk so nothing done or bought is offered again, and she can say what she filed at her last tidy");
        }

        private static bool ForkLiveSide()
        {
            return GameSide.Contains("your team") && GameSide.Contains("never they");
        }

        private static void SmokeLately(string folder)
        {
            string logs = Path.Combine(folder, "lately-chatlog");
            if (Directory.Exists(logs)) Directory.Delete(logs, true);
            Directory.CreateDirectory(logs);
            File.WriteAllLines(Path.Combine(logs, "2026-11-02.log"), new[] { "05:00:00 user: too old to count", "16:04:08 user: Send me my shopping list on Discord: baguette, ham, cucumber.", "16:04:08 assistant: Shopping list's in your Discord now.", "17:02:46 user: (game: Their own team took the Chemtech dragon, stolen!)" });
            File.WriteAllLines(Path.Combine(logs, "2026-11-03.log"), new[] { "02:13:35 user: No, thank you. I already have all of them." });
            DateTime now = new DateTime(2026, 11, 3, 2, 14, 0);
            List<string> talk = Lately.Talk(logs, now);
            if (talk.Count != 3 || talk[0] != "Mon 16:04 Him: Send me my shopping list on Discord: baguette, ham, cucumber." || talk[1] != "Mon 16:04 Capri: Shopping list's in your Discord now." || talk[2] != "Tue 02:13 Him: No, thank you. I already have all of them.")
                throw new Exception("The recent talk for her ideas was read wrong: " + string.Join(" / ", talk.ToArray()));
            string keptFolder = Lately.Folder;
            try
            {
                Lately.Folder = logs;
                string frame = Autonomy.Frame(now, new List<string> { "reminders" }, null);
                if (!frame.Contains("What he and you said lately") || !frame.Contains("I already have all of them") || !Autonomy.JudgeText.Contains("already done, bought, sent or turned down"))
                    throw new Exception("Her idea prompt does not see the recent talk, so it can offer what is already done.");
            }
            finally { Lately.Folder = keptFolder; }
            Directory.Delete(logs, true);
        }

        private static void SmokeTidyNote()
        {
            string[] tidy = { "- 2026-11-02 22:00 filed 2 (Areas/My PC and games 2), kept 11, left 0; journal lines: 2", "  - [[My PC and games]]: Has a second monitor ([[Journal/2026-11-02|2 Nov]])", "  - [[Interests and hobbies]]: Often jungles as Graves ([[Journal/2026-11-02|2 Nov]])" };
            string block = TidyNote.Block(tidy, new DateTime(2026, 11, 2, 22, 0, 25));
            if (!block.Contains("- My PC and games: Has a second monitor\n- Interests and hobbies: Often jungles as Graves") || block.Contains("[[")) throw new Exception("What she filed at her tidy was not given to her: " + block);
            if (TidyNote.Block(tidy, new DateTime(2026, 11, 3, 13, 0, 0)) != "") throw new Exception("An old tidy was still given to her.");
        }
    }
}
