using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static readonly string[] MeaningTexts = { "Booked a ryokan near Tokyo station for the December trip", "Rudiminas's birthday is 30 October", "Naïve café, isn't it? Hello—world 42!" };
        private static readonly long[][] MeaningIds =
        {
            new long[] { 101, 17414, 1037, 29431, 12352, 2078, 2379, 5522, 2276, 2005, 1996, 2285, 4440, 102 },
            new long[] { 101, 21766, 22172, 15227, 1005, 1055, 5798, 2003, 2382, 2255, 102 },
            new long[] { 101, 15743, 7668, 1010, 3475, 1005, 1056, 2009, 1029, 7592, 1517, 2088, 4413, 999, 102 }
        };
        private static readonly double[] MeaningNorms = { 15.767471, 15.833245, 15.984415 };
        private static readonly double[][] MeaningFirst =
        {
            new[] { 0.003857, -0.011035, 0.057107, 0.015857, 0.024921, 0.015162, 0.068019, 0.00035 },
            new[] { -0.070248, -0.02959, -0.010896, -0.009654, 0.017129, 0.005885, 0.056469, 0.017976 },
            new[] { 0.024777, 0.008757, -0.003794, 0.028835, 0.046822, 0.019714, 0.049382, 0.009689 }
        };

        private static string IdText(long[] ids)
        {
            return string.Join(",", Array.ConvertAll(ids, delegate(long id) { return id.ToString(); }));
        }

        private void SmokeMeaning(List<string> checks)
        {
            WordPiece mini = new WordPiece(new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]", "naive", "cafe", ",", "isn", "'", "t", "it", "?", "hello", "—", "world", "4", "##2", "!", "un", "##aff", "##able", "中" });
            string pieces = string.Join(" ", mini.Pieces("Naïve  CAFÉ, isn't it? Hello—world 42!\tunaffable xyz 中文\u0007").ToArray());
            if (pieces != "naive cafe , isn ' t it ? hello — world 4 ##2 ! un ##aff ##able [UNK] 中 [UNK]") throw new Exception("The WordPiece split was wrong: " + pieces);
            long[] small = mini.Ids("hello world");
            if (IdText(small) != "2,12,14,3") throw new Exception("The WordPiece ids were wrong: " + IdText(small));
            long[] full = mini.Ids(string.Join(" hello ", new string[600]));
            if (full.Length != WordPiece.Most || full[0] != 2 || full[full.Length - 1] != 3) throw new Exception("A long text was not cut to " + WordPiece.Most + " tokens with [CLS] and [SEP]: " + full.Length);

            string keptRuntime = Meaning.Runtime, keptModel = Meaning.Model, keptStore = Meaning.Store;
            Func<bool> keptGaming = Meaning.Gaming;
            bool keptOn = Meaning.On;
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-meaning-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri");
                Meaning.Reset();
                Meaning.Runtime = Path.Combine(folder, "no-engine");
                Meaning.Model = Path.Combine(data, Path.Combine("recall", "model"));
                Meaning.Store = Path.Combine(folder, "vectors.bin");
                Meaning.On = true;
                Meaning.Gaming = null;
                if (Meaning.Ready || Meaning.Query("Japan hotel") != null || Meaning.Loaded) throw new Exception("Meaning search ran with no onnxruntime.dll.");
                Meaning.Runtime = LocalVoice.Folder(data);
                if (!Meaning.Ready)
                {
                    checks.Add("LIMIT meaning search (step 6) not run: " + Meaning.Missing() + "; search stays BM25 only, the tokenizer passed");
                    return;
                }
                string dll = Path.Combine(Meaning.Runtime, "onnxruntime.dll");
                string probe = Embed.Probe(dll);
                if (probe != null) throw new Exception("The onnxruntime function table did not answer as expected: " + probe);
                Stopwatch load = Stopwatch.StartNew();
                using (Embed embed = Embed.Open(dll, Path.Combine(Meaning.Model, "model.onnx"), Path.Combine(Meaning.Model, "vocab.txt"), Meaning.Threads))
                {
                    load.Stop();
                    for (int i = 0; i < MeaningTexts.Length; i++)
                    {
                        long[] ids = embed.Words.Ids(MeaningTexts[i]);
                        if (IdText(ids) != IdText(MeaningIds[i])) throw new Exception("The tokenizer gave other ids than the reference for \"" + MeaningTexts[i] + "\": " + IdText(ids));
                        float norm;
                        float[] vector = embed.Vector(MeaningTexts[i], out norm);
                        if (vector.Length != Embed.Size || Math.Abs(norm - MeaningNorms[i]) > MeaningNorms[i] * 0.001) throw new Exception("The vector for \"" + MeaningTexts[i] + "\" has " + vector.Length + " numbers and length " + norm + ", not " + Embed.Size + " and " + MeaningNorms[i]);
                        for (int n = 0; n < MeaningFirst[i].Length; n++)
                            if (Math.Abs(vector[n] - MeaningFirst[i][n]) > 0.002) throw new Exception("Number " + n + " of the vector for \"" + MeaningTexts[i] + "\" is " + vector[n] + ", the reference is " + MeaningFirst[i][n]);
                    }
                }

                string root = Path.Combine(folder, "vault");
                if (!Vault.Open(root, null)) throw new Exception("The meaning vault did not open.");
                Brain.MemoryPath = Path.Combine(root, Vault.CoreName);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Names.Reset();
                Lookback.Reset();
                System.Text.StringBuilder trip = new System.Text.StringBuilder("# Trip\n");
                for (int line = 0; line < 14; line++) trip.Append("- Packing list item ").Append(line).Append(": socks and chargers\n");
                trip.Append("- Booked a ryokan near Tokyo station for the December trip\n- Flights leave on 12 December\n");
                LookWrite(root, "Areas/Trip.md", trip.ToString());
                LookWrite(root, "Areas/Life.md", "- Has a cat named Mochi\n- Works nine to five at the warehouse\n- Likes spicy ramen\n- Uses a WHOOP band for sleep\n- Goes to the gym on Mondays\n- Allergic to peanuts\n- Prefers tea over coffee\n- Saving up for a new GPU\n");
                LookWrite(root, "People/Greta.md", "# Greta\n- Is: friend\n- Watches Arcane with him\n");
                LookWrite(root, "Capri/chats/2026-10-20.md", "# 2026-10-20\n\n18:02 him: lost three ranked games in a row\n18:02 her: Rough night.\n");
                System.Text.StringBuilder echo = new System.Text.StringBuilder("# 2026-10-25\n\n");
                string[] stays = { "where are we sleeping in Tokyo again", "is the Tokyo place near the station", "did I book the inn in Tokyo", "how much was the Tokyo room", "what's the name of the Tokyo inn" };
                for (int line = 0; line < stays.Length; line++) echo.Append("1").Append(line).Append(":00 him: ").Append(stays[line]).Append("\n1").Append(line).Append(":00 her: The ryokan by Tokyo station, choom.\n");
                LookWrite(root, "Capri/chats/2026-10-25.md", echo.ToString());
                int made = Meaning.Fill(Lookback.Meant(root), 1000, null);
                if (made < 20 || Meaning.Stored != made) throw new Exception("Filling the vectors made " + made + " and keeps " + Meaning.Stored + ".");

                string japan = Vault.Related("Japan hotel");
                if (japan.IndexOf("Areas/Trip.md", StringComparison.Ordinal) < 0 || japan.IndexOf("Booked a ryokan", StringComparison.Ordinal) < 0) throw new Exception("Japan hotel did not find the Tokyo ryokan note line (no shared word) through its meaning, or ten old chat lines about it pushed the note out: " + japan);
                string found = NotesTool.Run(root, new ToolArguments { Action = "search", Text = "Japan hotel" }, new DateTime(2026, 10, 30, 12, 0, 0), null);
                if (found.IndexOf("ryokan", StringComparison.OrdinalIgnoreCase) < 0) throw new Exception("The notes tool did not find the ryokan for Japan hotel: " + found);
                string hamster = Vault.Related("what is my hamster called");
                string never = NotesTool.Run(root, new ToolArguments { Action = "search", Text = "hamster" }, DateTime.Now, null);
                if (hamster.IndexOf("Mochi", StringComparison.Ordinal) >= 0 || hamster.IndexOf("hamster", StringComparison.OrdinalIgnoreCase) >= 0 || !never.Contains("Nothing in your notes mentions")) throw new Exception("A thing he never said pulled up a note through its meaning: " + hamster + " | " + never);
                foreach (string chat in new[] { "hey", "ok thanks", "you there?" })
                {
                    string talk = Vault.Related(chat);
                    if (talk.Length > 0) throw new Exception("\"" + chat + "\" pulled up notes: " + talk);
                }
                Stopwatch ask = Stopwatch.StartNew();
                Meaning.Query("when do I fly out");
                ask.Stop();

                Meaning.Reset();
                if (Meaning.Fill(Lookback.Meant(root), 1000, null) != 0 || Meaning.Stored != made) throw new Exception("The vectors were made again instead of read from the cache file.");
                LookWrite(root, "Areas/Life.md", "- Has a cat named Mochi\n- Works nine to five at the warehouse\n");
                if (Meaning.Fill(Lookback.Meant(root), 1000, null) != 0 || Meaning.Stored != made - 6) throw new Exception("Removed lines kept their vectors: " + Meaning.Stored + " of " + made);

                Meaning.Gaming = delegate { return true; };
                Meaning.Unload();
                if (Meaning.Query("Japan hotel") != null || Meaning.Loaded) throw new Exception("Meaning search loaded the model during a game.");
                if (Vault.Related("Japan hotel").IndexOf("ryokan", StringComparison.OrdinalIgnoreCase) >= 0) throw new Exception("A game did not keep search to words only.");
                Meaning.Gaming = null;
                if (Meaning.Query("Japan hotel") == null || !Meaning.Loaded) throw new Exception("Meaning search did not load again after the game.");
                Meaning.Rest(DateTime.Now.AddMinutes(Meaning.QuietMinutes - 1));
                if (!Meaning.Loaded) throw new Exception("The model was unloaded before " + Meaning.QuietMinutes + " quiet minutes.");
                Meaning.Rest(DateTime.Now.AddMinutes(Meaning.QuietMinutes + 1));
                if (Meaning.Loaded) throw new Exception("The model stayed loaded after " + Meaning.QuietMinutes + " quiet minutes.");
                Meaning.On = false;
                if (Meaning.Query("Japan hotel") != null || Meaning.Loaded) throw new Exception("Meaning search ran with the setting off.");
                checks.Add("PASS meaning search (step 6): the WordPiece tokenizer and bge-base-en-v1.5 through the shipped onnxruntime.dll give the reference ids and vectors (Python onnxruntime, same model); Japan hotel finds the Tokyo ryokan line with no shared word (vault and notes tool, a long note shows the line, ten old chat lines about it do not push the note out), a never-said hamster and small talk find nothing; vectors are cached on disk, read back, pruned with removed lines; words only during a game; unloaded after " + Meaning.QuietMinutes + " quiet minutes; off by setting; load " + load.ElapsedMilliseconds + " ms, one question " + ask.ElapsedMilliseconds + " ms");
            }
            finally
            {
                Meaning.Reset();
                Meaning.Runtime = keptRuntime;
                Meaning.Model = keptModel;
                Meaning.Store = keptStore;
                Meaning.Gaming = keptGaming;
                Meaning.On = keptOn;
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
    }
}
