using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeBrowserRecall(List<string> checks, string folder)
        {
            string keptFile = BrowserRecall.FilePath;
            try
            {
                BrowserRecall.FilePath = Path.Combine(folder, "browser-recall.txt");
                if (File.Exists(BrowserRecall.FilePath)) File.Delete(BrowserRecall.FilePath);
                DateTime found = new DateTime(2026, 11, 10, 14, 13, 0);
                DateTime asked = new DateTime(2026, 11, 10, 17, 46, 0);
                string task = "Research cheap boom arms (mic arms) on Amazon that fit the Blue Yeti X microphone. Find several affordable options.";
                string result = "I searched Amazon.com and did not buy anything. Best cheap picks: (1) InnoGear Boom Arm Microphone Mount, about 19 EUR, 4.4 stars; (2) Neewer boom arm, about 25 EUR.";
                BrowserRecall.Keep(task, result, "done", "Done. Searched amazon.com for boom arm.", found);
                string again = "Search Amazon for cheap boom arms (mic arms) that fit the Blue Yeti X microphone. Read the results and list the best options you find with product name, price and rating.";
                string said = BrowserRecall.Earlier("show m e the amazon options that you foiund for yheti x boom arm", again, asked);
                if (said == null || !said.Contains("InnoGear") || !said.Contains("14:13")) throw new Exception("Asking for the Amazon options she found started a new search instead of repeating her 14:13 result (his 2026-11-10 report): " + (said ?? "null"));
                if (BrowserRecall.Earlier(null, "Search Amazon for boom arms that fit the Yeti X and show me the options you found", asked) == null) throw new Exception("The understanding step's own 'options you found' was not read as a recall when his line was missing.");
                if (BrowserRecall.Earlier("find me more boom arms on amazon, cheaper ones this time", again, asked) != null) throw new Exception("Asking for more or cheaper ones repeated the old result instead of searching again.");
                if (BrowserRecall.Earlier("search amazon again for the boom arms you found", again, asked) != null) throw new Exception("'search again' repeated the old result instead of searching.");
                if (BrowserRecall.Earlier("show me the steam deals you found", "Look up the Steam deals on games this week", asked) != null) throw new Exception("A recall about another topic returned the boom arm result.");
                if (BrowserRecall.Earlier("search amazon for boom arms for the yeti x", again, asked) != null) throw new Exception("A plain new search was answered from the old result.");
                if (BrowserRecall.Earlier("show me the amazon options you found for the boom arm", again, found.AddHours(BrowserRecall.KeepHours + 1)) != null) throw new Exception("A result older than the keep window was still repeated.");
                string next = BrowserRecall.Earlier("what were the boom arm options you found on amazon", again, found.AddDays(1));
                if (next == null || !next.Contains("yesterday")) throw new Exception("A result from the day before was not told as yesterday's: " + (next ?? "null"));
                BrowserRecall.Keep(again, "Done. Searched google.com for boom arm mic stand for Blue Yeti X in your browser, then opened amazon.com in your browser. I didn't add anything to your cart.", "done", "Done. Searched google.com for boom arm mic stand for Blue Yeti X in your browser, then opened amazon.com in your browser.", asked);
                BrowserRecall.Keep(again, "Done. I didn't add anything to your cart.", "done", "Done.", asked);
                BrowserRecall.Keep(again, "I couldn't load Amazon.", "failed", "", asked);
                said = BrowserRecall.Earlier("show me the amazon boom arm options you found", again, asked.AddMinutes(1));
                if (said == null || !said.Contains("InnoGear")) throw new Exception("A step summary or a failed job replaced her real finding: " + (said ?? "null"));
                BrowserRecall.Heard = "show m e the amazon options that you foiund for yheti x boom arm";
                said = BrowserTool(new ToolArguments { Task = again });
                if (said == null || !said.Contains("InnoGear")) throw new Exception("The browser tool started a job instead of repeating what she found: " + (said ?? "null"));
                if (BrowserRecall.Heard != null) throw new Exception("His line stayed behind for the next browser job.");
            }
            finally
            {
                BrowserRecall.Heard = null;
                BrowserRecall.FilePath = keptFile;
            }
            checks.Add("PASS browser recall: \"show me the Amazon options you found\" (typos too) repeats her last matching browser finding with its time instead of starting a new search; more, cheaper, again or a plain search still searches, another topic or a result past the keep window is never repeated, and a step summary or a failed job never replaces a real finding");
        }
    }
}
