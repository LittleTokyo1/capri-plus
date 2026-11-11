using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeForgetting(List<string> checks)
        {
            string keptPath = Brain.MemoryPath;
            string keptOllama = Brain.Ollama;
            string keptLog = ActivityLog.FilePath;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            string folder = Path.Combine(Path.GetTempPath(), "capri-forget-smoke-" + Guid.NewGuid().ToString("N"));
            System.Net.Sockets.TcpListener fake = null;
            try
            {
                Directory.CreateDirectory(folder);
                string core = Path.Combine(folder, "me.md");
                Brain.MemoryPath = core;
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                ForgettingParsing();
                ForgettingDropping(core);
                fake = ForgettingTurns(folder, core);
            }
            finally
            {
                if (fake != null) fake.Stop();
                PeopleNotes.Extract = keptExtract;
                Brain.Ollama = keptOllama;
                ActivityLog.FilePath = keptLog;
                Brain.MemoryPath = keptPath;
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS forgetting: her own model decides what a request to forget means, in any words (the reply starts with a [forget: topic] marker that is stripped from what she says); the topic is removed from her notes and from the recent chat whatever the spelling (baby metal or Babymetal), her answer says what was removed and never claims she can't, a topic with nothing behind it says so, a topic that matches too many notes removes none, and \"everything\" backs up the notes file before wiping the notes and the chat");
        }

        private static void ForgettingParsing()
        {
            string raw = "[forget: baby metal | babymetal] Sure thing.";
            string topics = Forgetting.Take(ref raw);
            if (topics != "baby metal | babymetal" || raw != "Sure thing.") throw new Exception("The forget marker was read wrong: " + topics + " / " + raw);
            raw = "Sure thing. [FORGET: Cheesecake]";
            if (Forgetting.Take(ref raw) != "Cheesecake" || raw != "Sure thing.") throw new Exception("A marker at the end or in capitals was read wrong: " + raw);
            raw = "I can forget things, choom.";
            if (Forgetting.Take(ref raw) != null || raw != "I can forget things, choom.") throw new Exception("Ordinary words took for a marker.");
            List<string> list = Forgetting.Topics("baby metal | babymetal | ab | \"cheesecake\" | a fourth one");
            if (list.Count != 3 || list[0] != "baby metal" || list[2] != "cheesecake") throw new Exception("The topics were split wrong: " + string.Join(",", list.ToArray()));
            if (Forgetting.Plain("the line in Rudiminas's note that says \"Roasts Kasper") != "Roasts Kasper" || Forgetting.Plain("the line \"Roasts Kasper\" in Rudiminas's note") != "Roasts Kasper" || Forgetting.Plain("green tea") != "green tea" || Forgetting.Plain("the note in my Freya note that says: likes cats.") != "likes cats") throw new Exception("A forget that names the line by its note was not cut down to the line itself.");
            if (!Forgetting.IsAll(Forgetting.Topics("everything")) || !Forgetting.IsAll(Forgetting.Topics("all of it")) || Forgetting.IsAll(Forgetting.Topics("baby metal"))) throw new Exception("Everything was read wrong.");
        }

        private static void ForgettingDropping(string core)
        {
            File.WriteAllText(core, "# What Capri remembers about you\n> She reads this every time you chat.\n\n- They really like cheesecake\n- They like Babymetal\n- They live in Utrecht\n");
            if (Brain.Drop(Forgetting.Topics("baby metal")) != 1) throw new Exception("A note with another spelling was not found.");
            string left = File.ReadAllText(core);
            if (left.Contains("Babymetal") || !left.Contains("cheesecake") || !left.Contains("Utrecht") || !left.Contains("# What Capri remembers")) throw new Exception("Dropping a note took other lines with it: " + left);
            File.WriteAllText(core, "# What Capri remembers about you\n\n- They like tea\n- They like rice\n- They like jazz\n- They like film\n");
            string before = File.ReadAllText(core);
            if (Brain.Drop(Forgetting.Topics("they like")) != 0 || File.ReadAllText(core) != before) throw new Exception("A topic matching most of the notes removed some.");
            if (Brain.Drop(Forgetting.Topics("dragon pit")) != 0) throw new Exception("A missing topic removed something.");
            string keptRoot = Vault.Root;
            try
            {
                string vault = Path.Combine(Path.GetDirectoryName(core), "vault");
                Directory.CreateDirectory(Path.Combine(vault, "People"));
                Vault.Root = vault;
                string vaidas = Path.Combine(vault, "People", "Vaidas.md");
                string interests = Path.Combine(vault, "Interests.md");
                File.WriteAllText(core, "# What Capri remembers about you\n\n- They like Babymetal\n- They live in Utrecht\n");
                File.WriteAllText(vaidas, "# Vaidas\n- Plays: Valorant\n- Likes babymetal\nRelated: [[Kasper]]\n");
                File.WriteAllText(interests, "# Interests\n- Listens to babymetal on the train\n- Cooking\n");
                if (Brain.Drop(Forgetting.Topics("baby metal | babymetal")) != 3) throw new Exception("A topic was not removed from every note that held it.");
                string person = File.ReadAllText(vaidas);
                string topic = File.ReadAllText(interests);
                if (File.ReadAllText(core).Contains("Babymetal") || person.ToLowerInvariant().Contains("babymetal") || topic.ToLowerInvariant().Contains("babymetal")) throw new Exception("A forgotten line stayed in a note.");
                if (person != "# Vaidas\n- Plays: Valorant\nRelated: [[Kasper]]\n" || topic != "# Interests\n- Cooking\n" || !File.ReadAllText(core).Contains("Utrecht")) throw new Exception("Forgetting in a vault note touched more than the line: " + person + " / " + topic);
                if (!File.ReadAllText(Path.Combine(Path.GetDirectoryName(ActivityLog.FilePath), "corrections.log")).Contains("Likes babymetal")) throw new Exception("A forgotten line was not kept in the undo file.");
                File.WriteAllText(core, "# What\n\n- a babymetal one\n- b babymetal two\n- c babymetal three\n- d babymetal four\n");
                string crowded = File.ReadAllText(core);
                if (Brain.Drop(Forgetting.Topics("babymetal")) != 0 || File.ReadAllText(core) != crowded) throw new Exception("A topic matching too many lines removed some.");
            }
            finally { Vault.Root = keptRoot; }
        }

        private static System.Net.Sockets.TcpListener ForgettingTurns(string folder, string core)
        {
            File.WriteAllText(core, "# What Capri remembers about you\n> She reads this every time you chat.\n\n- They really like cheesecake\n- They like Babymetal\n- They live in Utrecht\n");
            List<string> asked = new List<string>();
            System.Net.Sockets.TcpListener fake;
            Brain.Ollama = FakeServer(new[] { SmokeSay("Babymetal, right? Japanese idols with exclamation marks."), SmokeSay("[forget: baby metal | babymetal] Sure, I'll drop that."), SmokeSay("Fine, what is the band?"), SmokeSay("[forget: dragon pit] Okay."), SmokeSay("[forget: metalhead] Okay."), SmokeSay("Done."), SmokeSay("Nope, nothing else."), SmokeSay("[forget: everything] Okay.") }, asked, out fake);
            PeopleNotes.Extract = delegate { return "NONE"; };
            List<string> tools = new List<string>();
            Brain brain = new Brain();
            brain.Model = "memory-test";
            brain.Act = delegate(string tool, ToolArguments args) { tools.Add(tool); return tool == "remember" ? Brain.Unlearn(args.Text) : "ok"; };
            brain.Ask("What is my favourite band? It has two exclamation marks in the name.");
            string gone = brain.Ask("No, I don't listen to baby metal. Remove that from your memory.");
            if (gone != "Gone, choom. Removed 1 note from my notes and 1 line of our chat.") throw new Exception("A request to forget was not carried out or not reported: " + gone);
            string notes = File.ReadAllText(core);
            if (notes.Contains("Babymetal") || !notes.Contains("cheesecake") || !notes.Contains("Utrecht")) throw new Exception("The forgotten note was not the only one removed: " + notes);
            string next = brain.Ask("Hey there, how are you doing today?");
            string body = asked[asked.Count - 1].ToLowerInvariant();
            if (next != "Fine, what is the band?" || body.Contains("babymetal") || body.Contains("baby metal") || !body.Contains("exclamation marks")) throw new Exception("The forgotten topic stayed in the chat, or other chat was lost: " + next);
            string nothing = brain.Ask("Please erase the dragon pit from your head.");
            if (nothing != Forgetting.NothingFound) throw new Exception("A topic with nothing behind it was reported wrong: " + nothing);
            for (int i = 0; i < 4; i++) brain.Remember("metalhead chat number " + i, "metalhead answer number " + i);
            string broad = brain.Ask("Delete the metalhead thing from your notes.");
            if (broad != Forgetting.NothingFound) throw new Exception("A topic matching too much of the chat removed some: " + broad);
            brain.Remember("tell me about japanese idols", "Idols sing on a stage.");
            File.AppendAllText(core, "- They watch japanese idols\n");
            string spelled = brain.Ask("forget japanese idols");
            if (spelled != "Gone, choom. Removed 1 note from my notes and 1 line of our chat." || File.ReadAllText(core).Contains("idols")) throw new Exception("An explicit forget did not use the same clean-up: " + spelled);
            string again = brain.Ask("Anything else you want to ask me?");
            string tail = asked[asked.Count - 1].ToLowerInvariant();
            if (again != "Nope, nothing else." || tail.Contains("japanese idols") || !tail.Contains("metalhead")) throw new Exception("The explicit forget left the topic in the chat or lost other chat: " + again);
            if (tools.Count != 1 || tools[0] != "remember") throw new Exception("A forget request turned into another tool job: " + string.Join(",", tools.ToArray()));
            string wiped = brain.Ask("Wipe your whole memory.");
            string[] backups = Directory.GetFiles(folder, "me.md.backup-*");
            if (!wiped.StartsWith("Wiped, choom.", StringComparison.Ordinal) || backups.Length != 1 || !File.ReadAllText(backups[0]).Contains("Utrecht") || File.ReadAllText(core).Contains("Utrecht")) throw new Exception("Wiping everything left notes or kept no backup: " + wiped);
            return fake;
        }
    }
}
