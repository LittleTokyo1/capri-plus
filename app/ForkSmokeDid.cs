using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeDid(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptLog = ActivityLog.FilePath;
            string keptChats = Lately.Folder;
            string folder = Path.Combine(Path.GetTempPath(), "capri-did-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                string vault = Path.Combine(folder, "vault");
                Directory.CreateDirectory(Path.Combine(vault, "Capri"));
                Vault.Root = vault;
                DateTime now = DateTime.Now;
                DidLines();
                DidWritten(vault, now);
                DidRead(vault, now);
                DidLately(folder, now);
                DidKept(folder, now);
                checks.Add("PASS her action log: what she did (sent, told, set, saved, changed, PC and browser jobs with how they ended) goes into Capri/did/<day>.md with a gist of at most " + Did.GistChars + " characters and no private text, never game moments, never with Remember our chats off and never in the note search; the offer frame reads the last " + Did.ReadDays + " days; game reactions no longer push his talk out of her 20 hours; friend orders and the away box survive a restart");
            }
            finally
            {
                Vault.Root = keptRoot;
                ActivityLog.FilePath = keptLog;
                Lately.Folder = keptChats;
                FriendOrders.Reset();
                Digest.Reset();
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private static void DidLines()
        {
            if (Did.Line("discord", "message to Rudce", null, null) != "messaged Rudce on Discord" || Did.Line("game moment", "They (Zed) just got a double kill!", "x", null) != null || Did.Line("objective call", "Dragon spawns in one minute.", null, null) != null || Did.Line("memory", "pass", "kept 1", null) != null) throw new Exception("Her action log took the wrong lines.");
            if (Did.Line("remember", "169 characters of text", "Saved to my notes: they start the new job on Monday", null) != "remembered: they start the new job on Monday" || Did.Line("remember", "121 characters of text", "Saved to People/Rudiminas.md (the memory pass wrote it a moment ago).", null) != null) throw new Exception("A remember save was missing from her action log, or a memory pass save was counted twice.");
            string longLine = Did.Line("heartbeat", "sent", new string('a', 200), null);
            if (longLine == null || longLine.Length > "sent him a heads-up on Discord: ".Length + Did.GistChars || Did.Gist("my bank pin is 4821") != Did.Private) throw new Exception("An action log gist was too long or kept private text.");
        }

        private static void DidWritten(string vault, DateTime now)
        {
            ActivityLog.Write("use_computer", "open Calculator and switch it to Scientific mode", null);
            ActivityLog.Write("hands", "job done", "31.1 s, 6 turns");
            ActivityLog.Write("discord", "message to Rudce", null);
            ActivityLog.Write("use_computer", "open the Bluetooth page in Settings", null);
            ActivityLog.Write("hands", "job stopped", "4.0 s, 1 turn");
            ActivityLog.Write("game moment", "They (Zed) just got a double kill!", "Yezzir");
            string file = Path.Combine(Did.FolderIn(vault), now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md");
            string today = File.Exists(file) ? File.ReadAllText(file) : "";
            if (!today.Contains(" she: ended a PC job: open Calculator and switch it to Scientific mode") || !today.Contains(" she: tried a PC job (stopped): open the Bluetooth page in Settings") || !today.Contains(" she: messaged Rudce on Discord") || today.Contains("double kill")) throw new Exception("Her actions were not written to her action log through the activity log: " + today);
            if (Vault.Files(vault).Exists(delegate(string path) { return path.Replace('\\', '/').Contains("/Capri/did/"); })) throw new Exception("Her action log reached the note search.");
            bool keeping = Brain.Keeping;
            Brain.Keeping = false;
            ActivityLog.Write("discord", "message to Sam", null);
            Brain.Keeping = keeping;
            if (File.ReadAllText(file).Contains("messaged Sam")) throw new Exception("Her action log was written with Remember our chats off.");
        }

        private static void DidRead(string vault, DateTime now)
        {
            File.WriteAllText(Path.Combine(Did.FolderIn(vault), now.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md"), "- 10:00 she: bought the old thing\n");
            string block = Did.Block(now);
            string frame = Autonomy.Frame(now, new List<string> { "research" }, vault);
            if (!block.Contains("messaged Rudce on Discord") || block.Contains("bought the old thing") || !frame.Contains("messaged Rudce on Discord")) throw new Exception("The offer frame did not read her last " + Did.ReadDays + " days of actions, or read older ones.");
        }

        private static void DidLately(string folder, DateTime now)
        {
            string chats = Path.Combine(folder, "chatlog");
            Directory.CreateDirectory(chats);
            Lately.Folder = chats;
            DateTime at = now.AddMinutes(-5);
            string stamp = at.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            File.WriteAllText(Path.Combine(chats, at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log"),
                stamp + " user: Send me the shopping list on Discord\n" + stamp + " assistant: Sent it, choom.\n" +
                stamp + " user: (game: They (Zed) just got a double kill!)\n" + stamp + " assistant: Yezzir, Zed doubled up!\n" +
                stamp + " user: (heartbeat)\n" + stamp + " assistant: Freya's birthday is in four days.\n");
            string talk = string.Join("\n", Lately.Talk(chats, now).ToArray());
            if (!talk.Contains("shopping list") || !talk.Contains("Sent it") || !talk.Contains("Freya's birthday") || talk.Contains("Yezzir")) throw new Exception("Game reactions still fill her recent talk, or her told lines were dropped: " + talk);
        }

        private static void DidKept(string folder, DateTime now)
        {
            ActivityLog.FilePath = Path.Combine(folder, "activity.log");
            FriendOrders.Reset();
            FriendOrders.Add(new FriendOrder { Who = "Sam", Note = "Sam", Text = "Hop on?", Until = now.AddHours(5) });
            FriendOrders.Reset();
            bool orderKept = FriendOrders.Pending(now) && !FriendOrders.Pending(now.AddHours(6));
            Digest.Reset();
            Digest.Add("heads", "Starts in 40 minutes: Dentist", now);
            Digest.Reset();
            bool digestKept = Digest.Count == 1 && Digest.Items()[0].Text == "Starts in 40 minutes: Dentist";
            FriendOrders.Forget();
            Digest.Clear();
            if (!orderKept || !digestKept) throw new Exception("A friend order or the away box did not survive a restart: order " + orderKept + ", box " + digestKept);
        }
    }
}
