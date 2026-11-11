using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const string CoreMiddle = "- Allergic to peanuts since childhood";

        private static string CoreBody(int count)
        {
            StringBuilder text = new StringBuilder(Brain.NotesHead);
            for (int i = 0; i < count; i++) text.Append(i == 20 ? CoreMiddle : "- Daily routine detail number " + i + " about mornings and errands").Append("\n");
            return text.ToString();
        }

        private void SmokeCore(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            bool keptOn = Meaning.On;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            string folder = Path.Combine(Path.GetTempPath(), "capri-core-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Meaning.On = false;
                Names.Reset();
                Lookback.Reset();
                string vault = Path.Combine(folder, "vault");
                if (!Vault.Open(vault, null)) throw new Exception("The core vault did not open.");
                string core = Path.Combine(vault, Vault.CoreName);
                Brain.MemoryPath = core;
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                CoreReading(core);
                CoreWriting(core);
                CoreEdits(vault, core);
                CoreKeeping(vault, core);
                CoreListing(vault);
            }
            finally
            {
                Quick.Post = keptPost;
                Meaning.On = keptOn;
                ActivityLog.FilePath = keptLog;
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptPath;
                Names.Reset();
                Lookback.Reset();
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS small core (step 7): a me.md fact in the middle of 60 lines, which the old head-and-tail pick drops, reaches her when he asks about it (search fetches it); a ## Core section is sent first and never passes 1500 characters, the rest fills what is left; the evening tidy-up writes the Core with one model call only when me.md is over budget or a Core exists (backup first, invented or secret lines refused, every other line kept); filing, corrections and forgetting never treat a Core copy as the fact, a correction updates an identical Core copy too, and forgetting is not blocked by it; the vault map keeps every folder when it is too long, newest notes first with a count of the rest");
        }

        private static void CoreReading(string core)
        {
            File.WriteAllText(core, CoreBody(60));
            Lookback.Reset();
            if (Context.Core().Contains("peanuts")) throw new Exception("The test fact was not in the middle that the old pick drops.");
            string asked = Context.For("am I allergic to peanuts");
            if (!asked.Contains("Allergic to peanuts")) throw new Exception("A middle me.md fact did not reach her when he asked about it: " + asked.Length);
            string other = Context.For("what should I cook tonight");
            if (other.Contains("peanuts")) throw new Exception("A middle fact was fetched for a question about something else.");
            string small = Brain.NotesHead + "- Lives in Eindhoven\n- Studies software\n";
            File.WriteAllText(core, small);
            Lookback.Reset();
            string plain = Context.For("where do I live");
            if (!plain.Contains("Lives in Eindhoven") || plain.IndexOf("Lives in Eindhoven", StringComparison.Ordinal) != plain.LastIndexOf("Lives in Eindhoven", StringComparison.Ordinal)) throw new Exception("A small me.md was not sent once, whole: " + plain);
        }

        private static void CoreWriting(string core)
        {
            string body = CoreBody(60);
            List<string> lines = new List<string> { "Kasper has a daily routine of mornings and errands", "Allergic to peanuts since childhood" };
            string put = CoreNote.Put(body, lines);
            if (!put.StartsWith(Brain.NotesHead + "## Core\n- Kasper has a daily routine", StringComparison.Ordinal) || !put.Contains("\n## Notes\n- Daily routine detail number 0") || !put.Contains("detail number 59")) throw new Exception("The Core section was not put under the title with the notes kept below: " + put.Substring(0, Math.Min(300, put.Length)));
            string again = CoreNote.Put(put, new List<string> { "Allergic to peanuts since childhood" });
            if (again.Split(new[] { "## Core" }, StringSplitOptions.None).Length != 2 || again.Split(new[] { "## Notes" }, StringSplitOptions.None).Length != 2 || again.Contains("Kasper has a daily routine") || !again.Contains("detail number 59")) throw new Exception("Writing the Core again did not replace it in place.");
            File.WriteAllText(core, put);
            Lookback.Reset();
            string block = Context.Core();
            if (!block.Contains("Kasper has a daily routine") || block.IndexOf("Kasper has a daily routine", StringComparison.Ordinal) > block.IndexOf("detail number 0", StringComparison.Ordinal)) throw new Exception("The Core was not sent first: " + block.Substring(0, Math.Min(400, block.Length)));
            if (!Context.For("am I allergic to peanuts").Contains("Allergic to peanuts")) throw new Exception("With a Core the rest of me.md was not fetched by search.");
            List<string> many = new List<string>();
            for (int i = 0; i < 30; i++) many.Add("Long core fact " + i + " " + new string('x', 140));
            string kept = Context.Kept(CoreNote.Put(Brain.NotesHead, many));
            if (kept.Length > CoreNote.MostChars || !kept.Contains("Long core fact 0")) throw new Exception("The Core block passed its cap: " + kept.Length);

            string source = CoreBody(60) + "- Plays League of Legends with Rudiminas\n";
            List<string> parsed = CoreNote.Parse("Here is the core:\n- Has a daily routine of mornings and errands\n- Plays League of Legends with Rudiminas\n- Owns a pet dragon named Zorg\n- Card number 4111 1111 1111 1111\n- " + new string('y', 260), source);
            if (parsed == null || parsed.Count != 2 || parsed.Exists(delegate(string line) { return line.Contains("dragon") || line.Contains("4111") || line.Contains("yyy"); })) throw new Exception("An invented, secret or overlong Core line was kept: " + (parsed == null ? "null" : string.Join(" | ", parsed.ToArray())));
            if (CoreNote.Parse("I can't do that.", source) != null || CoreNote.Parse("- Owns a pet dragon named Zorg", source) != null) throw new Exception("An unusable Core answer was accepted.");
            List<string> fields = CoreNote.Parse("- Discord: rudiminas\n- Plays League of Legends with Rudiminas", source + "- Discord: rudiminas\n");
            if (fields == null || fields.Count != 1 || fields[0].StartsWith("Discord", StringComparison.Ordinal)) throw new Exception("A field line was copied into the Core.");
            foreach (string tail in new[] { "", "\n## History\n- until 1 Oct 2026: Old fact\n" })
            {
                File.WriteAllText(core, CoreNote.Put(Brain.NotesHead + tail, new List<string> { "Has a daily routine of mornings and errands" }));
                Brain.Note("Likes ramen on Fridays", new DateTime(2026, 10, 30, 12, 0, 0));
                string noted = File.ReadAllText(core);
                if (NoteEdit.CoreLines(noted).Exists(delegate(string line) { return line.Contains("ramen"); }) || !NoteEdit.OutsideCore(noted).Contains("ramen")) throw new Exception("A new note line landed inside the Core: " + noted);
            }
        }

        private static void CoreEdits(string vault, string core)
        {
            string body = CoreNote.Put(Brain.NotesHead + "- Goes to the gym on Mondays\n- Gym bag lives in the car\n- Gym membership at Basic-Fit\n- Studies software\n", new List<string> { "Goes to the gym on Mondays", "Studies software at Fontys" });
            File.WriteAllText(core, body);
            NightBatch batch = Nightly.Gather(vault, core);
            if (batch.Lines.Exists(delegate(NightLine line) { return line.Text.Contains("Fontys") || line.Text.StartsWith("#", StringComparison.Ordinal); }) || batch.Lines.Count != 4) throw new Exception("The evening filing offered a Core line or a heading: " + batch.Lines.Count);
            Brain.Unfile(new List<string> { "- Goes to the gym on Mondays" });
            string unfiled = File.ReadAllText(core);
            if (!NoteEdit.CoreLines(unfiled).Contains("- Goes to the gym on Mondays") || NoteEdit.OutsideCore(unfiled).Contains("Goes to the gym on Mondays")) throw new Exception("Filing a line away took the Core copy instead of the line: " + unfiled);
            File.WriteAllText(core, body);
            List<NoteLine> candidates = NoteEdit.Candidates("I don't go to the gym anymore");
            if (candidates.Exists(delegate(NoteLine line) { return line.Text.Contains("Fontys"); }) || candidates.FindAll(delegate(NoteLine line) { return line.Text == "Goes to the gym on Mondays"; }).Count != 1) throw new Exception("A Core copy was offered as a line to correct.");
            NoteLine gym = candidates.Find(delegate(NoteLine line) { return line.Text == "Goes to the gym on Mondays"; });
            if (!NoteEdit.Replace(gym, "Stopped going to the gym", new DateTime(2026, 10, 30, 12, 0, 0))) throw new Exception("The gym correction was not applied.");
            string after = File.ReadAllText(core);
            List<string> coreLines = NoteEdit.CoreLines(after);
            if (!coreLines.Contains("- Stopped going to the gym") || coreLines.Contains("- Goes to the gym on Mondays") || after.Split(new[] { "until 30 Oct 2026: Goes to the gym on Mondays" }, StringSplitOptions.None).Length != 2) throw new Exception("A correction did not update the identical Core copy once: " + after);
            File.WriteAllText(core, body);
            int removed = NoteEdit.Remove(new List<string> { "gym" });
            string forgotten = File.ReadAllText(core);
            if (removed == 0 || forgotten.IndexOf("gym", StringComparison.OrdinalIgnoreCase) >= 0 || !forgotten.Contains("Studies software at Fontys")) throw new Exception("Forgetting was blocked by a Core copy or left it: " + removed + " / " + forgotten);
        }

        private static void CoreKeeping(string vault, string core)
        {
            List<string> asked = new List<string>();
            string answer = "- Has a daily routine of mornings and errands\n- Allergic to peanuts since childhood";
            Quick.Post = delegate(string body, string key)
            {
                asked.Add(body);
                return Tuple.Create(200, NightReply(answer));
            };
            Cloud remote = new Cloud("sk-ant-test", "claude-haiku-4-5");
            DateTime night = new DateTime(2026, 10, 30, 22, 0, 0);
            File.WriteAllText(core, Brain.NotesHead + "- Lives in Eindhoven\n- Studies software\n");
            if (CoreNote.Keep(remote, vault, core, night) != "not needed" || asked.Count != 0) throw new Exception("A small me.md got a Core.");
            string big = CoreBody(60);
            File.WriteAllText(core, big);
            string result = CoreNote.Keep(remote, vault, core, night);
            string written = File.ReadAllText(core);
            if (!result.StartsWith("written", StringComparison.Ordinal) || asked.Count != 1 || !written.Contains("## Core\n- Has a daily routine of mornings and errands") || !written.Contains("detail number 59") || !written.Contains(CoreMiddle)) throw new Exception("The Core was not written from a big me.md with every line kept: " + result);
            if (Directory.GetFiles(Path.Combine(vault, "Capri", "backups"), "me-core-*.md").Length != 1) throw new Exception("No backup was made before the Core was written.");
            answer = "- Allergic to peanuts since childhood";
            answer += "\n- Has a daily routine of mornings and errands";
            CoreNote.Keep(remote, vault, core, night.AddDays(1));
            string twice = File.ReadAllText(core);
            if (asked.Count != 2 || twice.Split(new[] { "## Core" }, StringSplitOptions.None).Length != 2 || twice.IndexOf("- Allergic to peanuts since childhood", StringComparison.Ordinal) > twice.IndexOf("## Notes", StringComparison.Ordinal)) throw new Exception("An existing Core was not refreshed in place.");
            answer = "Sorry, I can't help with that.";
            if (CoreNote.Keep(remote, vault, core, night.AddDays(2)) == "written" || File.ReadAllText(core) != twice) throw new Exception("An unusable answer changed me.md.");
        }

        private static void CoreListing(string vault)
        {
            string people = Path.Combine(vault, "People");
            DateTime old = new DateTime(2026, 1, 1, 12, 0, 0);
            for (int i = 0; i < 160; i++)
            {
                string path = Path.Combine(people, "Classmate with a long name " + i.ToString("000") + ".md");
                File.WriteAllText(path, "- Met at school");
                File.SetLastWriteTime(path, old.AddHours(i));
            }
            Directory.CreateDirectory(Path.Combine(vault, "Areas"));
            File.WriteAllText(Path.Combine(vault, "Areas", "Gardening.md"), "- Tomatoes");
            File.WriteAllText(Path.Combine(vault, "Areas", "Piano.md"), "- Scales");
            string map = Vault.Core();
            if (!map.Contains("Areas: Gardening, Piano") || !map.Contains("Classmate with a long name 159") || map.Contains("Classmate with a long name 000,") || !map.Contains("more")) throw new Exception("A long vault map cut a folder or kept old names over new ones: " + map.Length);
        }
    }
}
