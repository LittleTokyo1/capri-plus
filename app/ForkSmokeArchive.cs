using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeArchive(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            string folder = Path.Combine(Path.GetTempPath(), "capri-archive-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                string vault = Path.Combine(folder, "vault");
                if (!Vault.Open(vault, null)) throw new Exception("The archive vault did not open.");
                string logs = Path.Combine(folder, "chatlog");
                DateTime now = new DateTime(2026, 10, 30, 14, 35, 10);
                List<ChatMessage> history = new List<ChatMessage>();
                history.Add(new ChatMessage("user", "I booked the Hotel Sakura in Kyoto for the trip"));
                history.Add(new ChatMessage("assistant", "Nice pick,\n choom."));
                history.Add(new ChatMessage("user", "(screen glance) a browser"));
                history.Add(new ChatMessage("user", "my password is hunter2"));
                ActivityLog.FilePath = null;
                ChatLog.Seen(null);
                ChatLog.Append(logs, history, now);
                string day = Path.Combine(ChatArchive.FolderIn(vault), "2026-10-30.md");
                if (File.Exists(day)) throw new Exception("A smoke run without a log wrote to the vault chat archive.");
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                ChatLog.Seen(null);
                ChatLog.Append(logs, history, now);
                string kept = File.ReadAllText(day);
                if (!kept.StartsWith("# 2026-10-30", StringComparison.Ordinal) || !kept.Contains("14:35 him: I booked the Hotel Sakura in Kyoto for the trip") || !kept.Contains("14:35 her: Nice pick, choom.") || kept.Contains("screen glance") || kept.Contains("hunter2")) throw new Exception("The chat did not land in the vault archive as dated him and her lines, or kept a private or system line: " + kept);
                history.Add(new ChatMessage("assistant", "Want me to add the dates to your calendar?"));
                ChatLog.Append(logs, history, now.AddMinutes(2));
                kept = File.ReadAllText(day);
                if (kept.Split(new[] { "Sakura" }, StringSplitOptions.None).Length != 2 || !kept.Contains("14:37 her: Want me to add the dates")) throw new Exception("A second turn repeated old lines or was not added: " + kept);
                if (ChatArchive.Forget(new List<string> { "Sakura" }) != 1 || File.ReadAllText(day).Contains("Sakura") || !File.ReadAllText(day).Contains("Nice pick")) throw new Exception("Forgetting a topic did not remove only its archive lines.");
                if (!ChatArchive.Clear(now) || File.Exists(day)) throw new Exception("Forgetting everything did not clear the archive.");
                string[] backups = Directory.GetDirectories(Path.Combine(vault, "Capri", "backups"), "chats-*");
                if (backups.Length != 1 || Directory.GetFiles(backups[0], "*.md").Length != 1) throw new Exception("The archive was cleared without a backup.");
                checks.Add("PASS chat archive: every turn also goes to Capri/chats/<date>.md in her vault as 'HH:mm him:' and 'HH:mm her:' lines under a '# date' heading (kept for good, private lines and bracketed system lines left out, never written by a smoke run), a later turn adds only its new lines, 'forget about X' removes the matching archive lines and 'forget everything' backs the archive up to Capri/backups and clears it");
            }
            finally
            {
                ChatLog.Seen(null);
                ActivityLog.FilePath = keptLog;
                ForkSettings.Current = keptSettings;
                Vault.Root = keptRoot;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }
    }
}
