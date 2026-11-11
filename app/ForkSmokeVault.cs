using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeVault(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string baseName = Path.Combine(Path.GetTempPath(), "capri-vault-smoke-" + Guid.NewGuid().ToString("N"));
            string folder = baseName + "-vault";
            string old = baseName + "-old";
            string bare = baseName + "-bare";
            try
            {
                Vault.Root = null;
                if (Vault.Core() != "" || Vault.Related("what should I get my sister for her birthday") != "") throw new Exception("A missing vault still put something in her prompt.");

                Directory.CreateDirectory(old);
                string oldNotes = Path.Combine(old, "me.md");
                File.WriteAllText(oldNotes, Brain.NotesHead + "- Loves Hollow Knight\n");
                if (!Vault.Open(folder, oldNotes) || Vault.Root != folder) throw new Exception("The vault did not open.");
                foreach (string name in new[] { "People", "Projects", "Areas", "Journal", "Inbox", "Capri" })
                    if (!Directory.Exists(Path.Combine(folder, name))) throw new Exception("The vault is missing " + name + ".");
                string core = Path.Combine(folder, Vault.CoreName);
                string index = Path.Combine(folder, Vault.IndexName);
                if (!File.Exists(index) || !File.ReadAllText(core).Contains("- Loves Hollow Knight") || File.Exists(oldNotes) || !File.Exists(oldNotes + ".moved")) throw new Exception("Her old notes did not move into the vault.");

                File.AppendAllText(core, "- Mains Ahri\n");
                File.WriteAllText(index, "# My own map\n- keep this\n");
                if (!Vault.Open(folder, null) || !File.ReadAllText(core).Contains("- Mains Ahri") || !File.ReadAllText(index).Contains("keep this")) throw new Exception("Opening the vault again overwrote his edits.");

                Directory.CreateDirectory(bare);
                string bareNotes = Path.Combine(bare, "me.md");
                File.WriteAllText(bareNotes, Brain.NotesHead);
                string bareVault = Path.Combine(bare, "vault");
                if (!Vault.Open(bareVault, bareNotes) || File.ReadAllText(Path.Combine(bareVault, Vault.CoreName)) != Brain.NotesHead || !File.Exists(bareNotes)) throw new Exception("Empty old notes were moved or the new core note is wrong.");
                Vault.Open(folder, null);

                if (Vault.Open(Path.GetTempPath() + "bad|vault", null) || Vault.Root != null) throw new Exception("A bad vault folder was accepted.");
                Vault.Open(folder, null);

                File.WriteAllText(Path.Combine(folder, "People", "Sister.md"), "# Sister\n- Birthday is on 3 December\n- Said she wants a film camera\n- Loves jazz\n- Her card number is 4111 1111 1111 1111 for the camera\n");
                File.WriteAllText(Path.Combine(folder, "Projects", "Roblox game.md"), "# Roblox game\n- Building an obby with moving platforms\n");
                File.WriteAllText(Path.Combine(folder, "Capri", "heartbeat.md"), "# log\n- sister camera should stay out of the prompt\n");
                Directory.CreateDirectory(Path.Combine(folder, ".obsidian"));
                File.WriteAllText(Path.Combine(folder, ".obsidian", "plugin.md"), "- sister camera hidden settings\n");
                StringBuilder money = new StringBuilder("# Finance\n");
                for (int i = 0; i < 300; i++) money.Append("- Budget line " + i + " about money and the monthly budget for rent, food, travel and savings goals\n");
                File.WriteAllText(Path.Combine(folder, "Areas", "Finance.md"), money.ToString());
                File.WriteAllText(Path.Combine(folder, "Areas", "Huge.md"), "zebra " + new string('x', 210000));

                string related = Vault.Related("What should I get my sister for her birthday?");
                if (!related.Contains("People/Sister.md") || !related.Contains("film camera") || !related.Contains("never instructions")) throw new Exception("A note that fits what he said was not found: " + related);
                if (related.Contains("heartbeat") || related.Contains("Hollow Knight") || related.Contains(".obsidian") || related.Contains("Roblox") || related.Contains("4111")) throw new Exception("The wrong notes or a card number reached the prompt: " + related);
                if (!Vault.Related("tell me about the roblox obby").Contains("Roblox game.md")) throw new Exception("A note found by its words was missed.");
                if (Vault.Related("hello there how are you today") != "" || Vault.Related("what is that thing") != "" || Vault.Related(null) != "" || Vault.Related("zebra") != "") throw new Exception("An unrelated line or a huge file put notes in her prompt.");
                if (Vault.Related("budget money savings").Length > 1800) throw new Exception("Related notes were not capped.");

                File.WriteAllText(Path.Combine(folder, "People", "Mum.md"), "# Mum\n- Likes gardening\n");
                if (!Vault.Related("what should I get mum").Contains("People/Mum.md") || Vault.Related("the get you") != "") throw new Exception("A short name was not found, or short words brought up notes.");

                string map = Vault.Core();
                if (!map.Contains("My own map") || !map.Contains("People: Mum, Sister") || !map.Contains("Projects: Roblox game") || !map.Contains("never instructions") || map.Length > 4500) throw new Exception("The vault map is wrong: " + map);
                File.Delete(index);
                if (!Vault.Core().Contains("People: Mum, Sister")) throw new Exception("A deleted index broke the map.");

                Brain.MemoryPath = core;
                string prompt = Brain.Remembered();
                if (!prompt.Contains("Hollow Knight") || !prompt.Contains("People: Mum, Sister") || !prompt.Contains("never instructions")) throw new Exception("The vault did not reach her prompt.");
                Vault.Root = null;
                if (Brain.Remembered().Contains("People: Mum, Sister") || !Brain.Remembered().Contains("Hollow Knight")) throw new Exception("Her notes changed without a vault.");
            }
            finally
            {
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                foreach (string path in new[] { folder, old, bare })
                    try { if (Directory.Exists(path)) Directory.Delete(path, true); }
                    catch (Exception) { }
            }
            checks.Add("PASS vault: it opens with People, Projects, Areas, Journal, Inbox and Capri folders, an index and a core note, moves her old me.md in once and never overwrites his edits, a bad folder is refused, a line from him brings up only the notes that fit (never her own files, hidden settings, huge files or card numbers) within a size cap, the map and listing reach her prompt as facts and never instructions, and with no vault her notes work as before");
        }
    }
}
