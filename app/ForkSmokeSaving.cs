using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeSaving(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            Func<Cloud> keptRemote = Saving.Remote;
            string folder = Path.Combine(Path.GetTempPath(), "capri-saving-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Names.Reset();
                MemoryQueue.Reset();
                string vault = Path.Combine(folder, "vault");
                if (!Vault.Open(vault, null)) throw new Exception("The saving vault did not open.");
                string core = Path.Combine(vault, Vault.CoreName);
                Brain.MemoryPath = core;
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                string note = Path.Combine(vault, "People", "Rudiminas.md");
                Directory.CreateDirectory(Path.GetDirectoryName(note));
                File.WriteAllText(note, "# Rudiminas\n- Discord: rudce\n");
                Names.Reset();
                Saving.Remote = delegate { return null; };
                List<string> frames = new List<string>();
                string reply = "Rudiminas | Note | Birthday is 30 October";
                PeopleNotes.Extract = delegate(string frame) { frames.Add(frame); return reply; };
                string saved = Hands.Do("remember", new ToolArguments { Text = "Rudiminas's birthday is 30 October" });
                if (!saved.StartsWith("Saved to People/Rudiminas.md", StringComparison.Ordinal) || !File.ReadAllText(note).Contains("Birthday is 30 October") || File.ReadAllText(core).Contains("30 October")) throw new Exception("A remember about a person did not land in the People note: " + saved);
                if (frames.Count != 1 || !frames[0].Contains("Today is ")) throw new Exception("The remember tool did not use the memory pass frame.");
                reply = null;
                string fallback = Hands.Do("remember", new ToolArguments { Text = "they like pineapple on pizza" });
                if (!fallback.StartsWith("Saved to my notes", StringComparison.Ordinal) || !File.ReadAllText(core).Contains("pineapple")) throw new Exception("A remember the model did not answer was lost instead of going to me.md: " + fallback);
                reply = "NONE";
                string plain = Hands.Do("remember", new ToolArguments { Text = "they keep their bike in the hallway" });
                if (!plain.StartsWith("Saved to my notes", StringComparison.Ordinal) || !File.ReadAllText(core).Contains("bike in the hallway")) throw new Exception("A remember the model judged as nothing was dropped: " + plain);
                frames.Clear();
                string secret = Hands.Do("remember", new ToolArguments { Text = "my card number is 4111 1111 1111 1111" });
                if (frames.Count != 0 || secret.StartsWith("Saved", StringComparison.Ordinal) || File.ReadAllText(core).Contains("4111")) throw new Exception("A card number was sent to the model or saved: " + secret);
                File.WriteAllText(note, "# Rudiminas\n- Discord: rudce\n");
                Names.Reset();
                reply = "Rudiminas | Note | Birthday is 6 May";
                PeopleNotes.Learn(null, "btw rudiminas has his birthday on 6 May every year", null, null);
                frames.Clear();
                reply = "Rudiminas | Note | Birthday: 6 May";
                string twice = Hands.Do("remember", new ToolArguments { Text = "Rudiminas's birthday is 6 May" });
                int lines = 0;
                foreach (string line in File.ReadAllLines(note))
                    if (line.Contains("6 May")) lines++;
                if (!twice.StartsWith("Saved to People/Rudiminas.md", StringComparison.Ordinal) || frames.Count != 0 || lines != 1) throw new Exception("The side pass and the remember tool both wrote the same fact: " + lines + " lines, " + twice);
                File.AppendAllText(core, new string('x', Brain.NotesLimit + 10) + "\n");
                reply = null;
                string full = Hands.Do("remember", new ToolArguments { Text = "they start the new job on Monday" });
                if (full.StartsWith("Saved", StringComparison.Ordinal) || !full.StartsWith("My notes are full", StringComparison.Ordinal) || PeopleNotes.Pending != 0) throw new Exception("A remember that could not be written did not say so plainly: " + full);
                if (!Saving.Covers("Rudiminas\n- Also called: Rudce\n- Plays: Cyberpunk 2077\n", "remember Rudce plays Cyberpunk") || Saving.Covers("- Also called: Rudce, Domitan, Dario\n- Discord: Rudce\n- Friend of Kasper", "remember this: Dario is also Rudce, my friend Rudiminas. With Rudce you don't need any filter, if he roasts you, roast him back"))
                    throw new Exception("A friend's note that kept only part of what he said counted as saved, or a full save did not.");
                checks.Add("PASS a part save is not called saved: the remember tool trusts the memory pass only when the friend's note holds most of what he said, else it saves it itself");
                checks.Add("PASS one save path: the remember tool runs the same memory pass as the side pass (a fact about Rudiminas lands in People/Rudiminas.md), a fact the side pass just wrote is not written twice, a remember the model does not answer or judges as nothing still goes to me.md, a card number never reaches the model, and when nothing can be written the result says so plainly; only a result starting with Saved is a save");
            }
            finally
            {
                Saving.Remote = keptRemote;
                PeopleNotes.Extract = keptExtract;
                ActivityLog.FilePath = keptLog;
                ForkSettings.Current = keptSettings;
                MemoryQueue.Reset();
                Names.Reset();
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }
    }
}
