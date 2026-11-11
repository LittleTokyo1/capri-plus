using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeLookback(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-lookback-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                string[,] stems = { { "caresses", "caress" }, { "ponies", "poni" }, { "meetings", "meet" }, { "booking", "book" }, { "booked", "book" }, { "hotels", "hotel" }, { "generalizations", "gener" }, { "hopping", "hop" }, { "agreed", "agre" } };
                for (int i = 0; i < stems.GetLength(0); i++)
                    if (Porter.Stem(stems[i, 0]) != stems[i, 1]) throw new Exception("The stemmer made " + Porter.Stem(stems[i, 0]) + " of " + stems[i, 0] + ", not " + stems[i, 1]);
                string root = Path.Combine(folder, "vault");
                if (!Vault.Open(root, null)) throw new Exception("The lookback vault did not open.");
                Brain.MemoryPath = Path.Combine(root, Vault.CoreName);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Names.Reset();
                LookWrite(root, "People/Rudiminas.md", "# Rudiminas\n- Is: friend\n- Note: Birthday is 30 October\n");
                LookWrite(root, "People/Rudiminas.sync-conflict-20261030-101010-ABCDEFG.md", "# Rudiminas\n- Note: hotel conflict copy\n");
                LookWrite(root, "Areas/Trip.md", "- Flights to Japan on 12 December\n- Booked a ryokan near Kyoto station\n");
                LookWrite(root, "Capri/sent-messages.md", "- to Rudce: the hotel in Kyoto is the Sakura\n");
                LookWrite(root, "Capri/heartbeat.md", "- mail: hotel booking confirmed at the Sakura\n");
                LookWrite(root, "Capri/chats/2026-10-20.md", "# 2026-10-20\n\n18:02 him: booked the Hotel Sakura in Kyoto for the trip\n18:02 her: Nice pick, choom.\n");
                LookWrite(root, "Capri/chats/2026-10-21.md", "# 2026-10-21\n\n19:00 him: join the call when you can\n19:00 her: On my way.\n");
                LookWrite(root, "Capri/chats/2026-10-30.md", "# 2026-10-30\n\n09:00 him: the hotel wifi is awful today\n09:00 her: Ugh, sorry.\n");
                ChatArchive.Keep(new List<ChatMessage> { new ChatMessage("user", "the hotel wifi is awful today"), new ChatMessage("assistant", "Ugh, sorry.") });

                string related = Vault.Related("what was that hotel I mentioned");
                if (!related.Contains("Sakura") || !related.Contains("20 Oct 2026") || !related.Contains("old chat") || !related.Contains("Nice pick")) throw new Exception("A chat from ten days ago was not found with its date and her answer: " + related);
                if (related.Contains("wifi") || related.Contains("to Rudce") || related.Contains("booking confirmed") || related.Contains("conflict copy")) throw new Exception("A line still in her chat window, a friend's message, a mail subject or a sync conflict copy was found: " + related);
                string stemmed = Vault.Related("booking hotels");
                if (!stemmed.Contains("Sakura")) throw new Exception("Booking hotels did not find booked the Hotel: " + stemmed);
                foreach (string asked in new[] { "what was that hotel I was talking about", "remind me what I said about the hotel", "what did I say about hotels" })
                    if (!Vault.Related(asked).Contains("Sakura")) throw new Exception("\"" + asked + "\" did not find the hotel: the asking words were searched for.");
                System.Text.StringBuilder journal = new System.Text.StringBuilder("# 2026-10-29\n");
                for (int line = 0; line < 25; line++) journal.Append("- Talked with Rudiminas about his birthday plans, part ").Append(line).Append("\n");
                LookWrite(root, "Journal/2026-10-29.md", journal.ToString());
                string person = Vault.Related("when is rudiminas birthday");
                int own = person.IndexOf("People/Rudiminas.md", StringComparison.Ordinal), journalAt = person.IndexOf("Journal/2026-10-29.md", StringComparison.Ordinal);
                if (own < 0 || !person.Contains("30 October") || (journalAt >= 0 && journalAt < own)) throw new Exception("A long Journal day pushed his friend's own note down or out: " + person);
                string hamster = Vault.Related("what is my hamster called");
                if (hamster.IndexOf("hamster", StringComparison.OrdinalIgnoreCase) >= 0 || hamster.Contains("join the call")) throw new Exception("Something was found for a thing he never said, or one common word pulled up a chat: " + hamster);

                string found = NotesTool.Run(root, new ToolArguments { Action = "search", Text = "hotel Kyoto" }, new DateTime(2026, 10, 30, 12, 0, 0), null);
                if (!found.Contains("Sakura") || !found.Contains("20 Oct 2026") || !found.Contains("old chat") || !found.Contains("Trip.md")) throw new Exception("The notes tool did not search notes and old chats together: " + found);
                if (found.Contains("to Rudce") || found.Contains("booking confirmed") || found.Contains("conflict copy") || found.Contains("wifi")) throw new Exception("The notes tool search returned a friend's message, a mail subject, a conflict copy or a line still in her window: " + found);
                string day = NotesTool.Run(root, new ToolArguments { Action = "search", Start = "2026-10-20" }, DateTime.Now, null);
                string gap = NotesTool.Run(root, new ToolArguments { Action = "search", Text = "hotel", Start = "2026-10-25", End = "2026-10-29" }, DateTime.Now, null);
                string never = NotesTool.Run(root, new ToolArguments { Action = "search", Text = "hamster" }, DateTime.Now, null);
                if (!day.Contains("Sakura") || day.Contains("join the call") || !gap.Contains("Nothing") || gap.Contains("Sakura") || !never.Contains("Nothing in your notes mentions")) throw new Exception("Looking back by day was wrong: " + day + " | " + gap + " | " + never);
                string sent = NotesTool.Run(root, new ToolArguments { Action = "read", Text = "Capri/sent-messages" }, DateTime.Now, null);
                string beat = NotesTool.Run(root, new ToolArguments { Action = "read", Text = "heartbeat" }, DateTime.Now, null);
                string friend = NotesTool.Run(root, new ToolArguments { Action = "read", Text = "Rudiminas" }, DateTime.Now, null);
                if (sent.Contains("to Rudce") || beat.Contains("booking confirmed") || !friend.Contains("30 October") || friend.Contains("conflict copy") || friend.Contains("More than one")) throw new Exception("The notes tool read a friend's message, a mail subject or a conflict copy: " + sent + " | " + beat + " | " + friend);
                if (Vault.Core().Contains("sync-conflict") || string.Join(", ", Names.Roster().ToArray()).Contains("sync-conflict")) throw new Exception("A sync conflict copy was listed as a note or a person.");
                if (!Hands.Valid("notes", new ToolArguments { Action = "search", Start = "2026-10-20" }) || Hands.Valid("notes", new ToolArguments { Action = "search", Start = "soon" }) || !Hands.ClaudeTools.Contains("old chats") || !SelfKnowledge.Block().Contains("old chats")) throw new Exception("The notes tool does not offer looking back through old chats, or the brain's own check refuses it.");

                for (int file = 0; file < 200; file++)
                {
                    System.Text.StringBuilder body = new System.Text.StringBuilder("# Bulk " + file + "\n");
                    for (int line = 0; line < 10; line++) body.Append("- line ").Append(line).Append(" about topic ").Append(file % 17).Append(" with words alpha beta gamma\n");
                    LookWrite(root, "Areas/Bulk/Bulk " + file + ".md", body.ToString());
                }
                Lookback.Find(root, "alpha topic", null, null, true, true, 5);
                long best = long.MaxValue;
                for (int run = 0; run < 3; run++)
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    List<LookHit> hits = Lookback.Find(root, "gamma topic 7", null, null, true, true, 5);
                    watch.Stop();
                    if (hits.Count == 0) throw new Exception("The big index found nothing.");
                    best = Math.Min(best, watch.ElapsedMilliseconds);
                }
                if (Lookback.Pieces < 2000 || best >= 50) throw new Exception("Searching " + Lookback.Pieces + " pieces took " + best + " ms (must be under 50 with the file listing).");
                checks.Add("PASS lookback: one ranked search (BM25 with Porter stems) over her notes, me.md and the chat archive Capri/chats finds a chat from ten days ago with its date and her answer (booking hotels finds booked the Hotel), never a line still in her chat window, a friend's message (sent-messages.md), a mail subject (heartbeat.md), a sync conflict copy or one common word; the notes tool searches notes and old chats together and by day; " + Lookback.Pieces + " pieces answer in " + best + " ms with the file listing");
            }
            finally
            {
                ChatArchive.Keep(null);
                ActivityLog.FilePath = keptLog;
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                Names.Reset();
                Lookback.Reset();
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private static void LookWrite(string root, string relative, string text)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
    }
}
