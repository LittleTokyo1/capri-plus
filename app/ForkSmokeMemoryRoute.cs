using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeMemoryRoute(List<string> checks)
        {
            string unrouted = ForkUpstreamSettings.Unrouted();
            if (unrouted != null) throw new Exception("New upstream settings are not routed into our build yet: " + unrouted + ". Decide how each one reaches our memory and tools, then list it in ForkUpstreamSettings.");
            string transcriptsWas = Brain.TranscriptPath, memoriesWas = Brain.MemoriesPath, rootWas = Vault.Root, notesWas = Brain.MemoryPath, logWas = ActivityLog.FilePath;
            bool keepingWas = Brain.Keeping, remembersWas = preferences.Remembers;
            string folder = Path.Combine(Path.GetTempPath(), "capri-route-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                Brain.TranscriptPath = Path.Combine(folder, "transcripts");
                Brain.MemoriesPath = Path.Combine(folder, "memories.md");
                ForkVaultOnly();
                Brain.Keeping = true;
                Brain.Transcribe("You", "remember that I love Hollow Knight");
                bool nothingElse = Brain.TranscriptPath == null && Brain.MemoriesPath == null && Brain.Memories() == "" && Directory.GetFileSystemEntries(folder).Length == 0;
                Vault.Root = folder;
                memoriesOpened = null;
                OpenMemories();
                bool opensVault = memoriesOpened == "obsidian://open?path=" + Uri.EscapeDataString(folder) && Directory.GetFileSystemEntries(folder).Length == 0;
                string notes = Path.Combine(folder, "me.md");
                File.WriteAllText(notes, "# Me\n\n- Plays League of Legends most evenings\n");
                Brain.MemoryPath = notes;
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                string firstJot = Brain.Jot("Pronouns", "he/him");
                string secondJot = Brain.Jot("Pronouns", "they/them");
                string body = File.ReadAllText(notes);
                bool metRouted = firstJot == null && secondJot == null && body.Contains("- Pronouns: they/them") && body.Contains("he/him") && body.IndexOf("## History", StringComparison.Ordinal) > body.IndexOf("- Pronouns: they/them", StringComparison.Ordinal);
                DateTime now = new DateTime(2026, 10, 31, 16, 5, 0);
                ChatArchive.Seen("a chess board, white to move", null, now);
                string chats = Path.Combine(ChatArchive.FolderIn(folder), "2026-10-31.md");
                bool seenRouted = File.Exists(chats) && File.ReadAllText(chats).Contains("16:05 she saw: a chess board, white to move");
                preferences.Remembers = true;
                Brain.Keeping = true;
                ToggleRemembering();
                bool off = !Fork.MemoryOn && !preferences.Remembers;
                ChatArchive.Seen("a dungeon map", null, now);
                bool offKept = !File.ReadAllText(chats).Contains("a dungeon map");
                ToggleRemembering();
                bool on = Fork.MemoryOn && preferences.Remembers;
                if (!nothingElse || !opensVault || !metRouted || !seenRouted || !off || !offKept || !on) throw new Exception("Her memory went somewhere other than the vault: nothing else " + nothingElse + ", opens vault " + opensVault + " (" + memoriesOpened + "), get to know me " + metRouted + " (" + firstJot + " / " + secondJot + " / " + body.Replace("\n", " | ") + "), glances " + seenRouted + ", off " + off + " " + offKept + ", on " + on);
            }
            finally
            {
                Brain.TranscriptPath = transcriptsWas;
                Brain.MemoriesPath = memoriesWas;
                Vault.Root = rootWas;
                Brain.MemoryPath = notesWas;
                ActivityLog.FilePath = logWas;
                Brain.Keeping = keepingWas;
                preferences.Remembers = remembersWas;
                try { Directory.Delete(folder, true); }
                catch (IOException) { }
            }
            checks.Add("PASS one memory: every upstream setting is listed as ours, routed or a voice/look extra (a new one fails this check); upstream's transcripts and memories.md have no folder; Get to know me answers go into me.md through our line editor (a changed answer moves the old one to History); glances go into her vault chat archive; Open her memories opens the Obsidian vault; Remember our chats turns her vault saving off and on");
        }
    }
}
