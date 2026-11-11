using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CapriDesktop
{
    internal sealed class MemoryCase
    {
        internal string Id;
        internal string What;
        internal int Step;
        internal string Said;
        internal string LastReply;
        internal string Recent;
        internal string Ask;
        internal string Fake;
        internal string Forget;
        internal Dictionary<string, string> Seed = new Dictionary<string, string>();
        internal Func<string, string, string> Check;
    }

    internal static class MemoryEval
    {
        internal const int StepsDone = 8;
        internal static readonly DateTime Day = new DateTime(2026, 10, 30, 14, 35, 0);
        private const string Rudiminas = "# Rudiminas\n- Discord: rudce\n- Is: friend\n";
        private const string Core = "# About them\n- Goes to the gym on Mondays and Thursdays\n- Plays League of Legends most evenings\n";

        internal static List<MemoryCase> Cases()
        {
            List<MemoryCase> cases = new List<MemoryCase>();
            MemoryCase a = Make("a", "a birthday said as 'today' is kept as 30 October", 0);
            a.Said = "oh btw it's rudiminas birthday today!";
            a.Fake = "Rudiminas | Note | Birthday is 30 October";
            a.Check = delegate(string root, string output) { return Has(root, "People/Rudiminas.md", "30 October") ?? Lacks(root, "Capri/plans.md", "- [ ]"); };
            cases.Add(a);
            MemoryCase b = Make("b", "his bare 'today' answering her 'when is it?' is kept", 0);
            b.Said = "today";
            b.LastReply = "Nice! When is it?";
            b.Recent = "USER: rudiminas has his birthday coming up\nASSISTANT: Nice! When is it?";
            b.Fake = "Rudiminas | Note | Birthday is 30 October";
            b.Check = a.Check;
            cases.Add(b);
            MemoryCase c = Make("c", "'I don't go to the gym anymore' keeps the old line under History", 3);
            c.Said = "I don't go to the gym anymore";
            c.Fake = "REPLACE | L1 | NONE";
            c.Check = delegate(string root, string output) { return Has(root, Vault.CoreName, "## History") ?? Has(root, Vault.CoreName, "until 30 Oct 2026"); };
            cases.Add(c);
            MemoryCase d = Make("d", "the sister's birthday goes to People/Sister.md", 0);
            d.Said = "my sister's birthday is on 6 November, don't let me forget it";
            d.Fake = "Sister | Note | Birthday is 6 November";
            d.Check = delegate(string root, string output) { return Has(root, "People/Sister.md", "6 November") ?? Lacks(root, "Capri/plans.md", "- [ ]"); };
            cases.Add(d);
            MemoryCase e = Make("e", "'you didn't note down X' becomes a lesson", 0);
            e.Said = "hmm you didn't note down rudiminas birthday, I told you it was today";
            e.LastReply = "Happy birthday to Rudiminas, choom!";
            e.Fake = "Rudiminas | Note | Birthday is 30 October. LESSON | Said happy birthday to Rudiminas but did not note that his birthday is today. | When the user says someone's birthday is today, save it as that person's yearly birthday date.";
            e.Check = delegate(string root, string output) { return Has(root, "Capri/lessons.md", "Wanted instead") ?? Has(root, "People/Rudiminas.md", "30 October"); };
            cases.Add(e);
            MemoryCase f = Make("f", "'I'm thinking about quitting my job' is not saved as a fact but kept as a maybe in Capri/plans.md", 8);
            f.Said = "I'm thinking about quitting my job";
            f.Fake = "PLAN | maybe: quit their job | none";
            f.Check = delegate(string root, string output) { return Lacks(root, Vault.CoreName, "quit") ?? Has(root, "Capri/plans.md", "- [ ] maybe:"); };
            cases.Add(f);
            MemoryCase k = Make("k", "'I'll call mum about the trip on 5 November' becomes a plan due that day", 8);
            k.Said = "I'll call mum about the trip on 5 November, remind me if I forget";
            k.Fake = "PLAN | Call mum about the trip | 2026-11-05";
            k.Check = delegate(string root, string output) { return Has(root, "Capri/plans.md", "\U0001F4C5 2026-11-05"); };
            cases.Add(k);
            MemoryCase l = Make("l", "'I finally called mum about the trip' ticks off the open plan", 8);
            l.Said = "I finally called mum about the trip, she's in";
            l.Seed["Capri/plans.md"] = "# Plans and promises\n\n- [ ] Call mum about the trip \U0001F4C5 2026-11-05\n";
            l.Fake = "DONE | P1 | done";
            l.Check = delegate(string root, string output) { return Has(root, "Capri/plans.md", "- [x] Call mum about the trip"); };
            cases.Add(l);
            MemoryCase m = Make("m", "'remind me at 17:00 to call mum' stays a reminder, not a plan", 8);
            m.Said = "remind me at 17:00 to call mum";
            m.Fake = "NONE";
            m.Check = delegate(string root, string output) { return Lacks(root, "Capri/plans.md", "mum"); };
            cases.Add(m);
            MemoryCase n = Make("n", "his email and phone, told so she can fill in an order, are kept", 0);
            n.Said = "my email is jan.test@example.com and my phone number is +31 6 12345678, you will need them when you order food for me";
            n.LastReply = "I can't fill those in because your email address and phone number aren't in my notes.";
            n.Fake = "ME | Note | Email address is jan.test@example.com\nME | Note | Phone number is +31 6 12345678";
            n.Check = delegate(string root, string output) { return Has(root, Vault.CoreName, "jan.test@example.com") ?? Has(root, Vault.CoreName, "+31 6 12345678"); };
            cases.Add(n);
            MemoryCase o = Make("o", "'I pay with Apple Pay, iDEAL second' is kept as a payment preference", 0);
            o.Said = "I like to pay with Apple Pay, and iDEAL as my second choice";
            o.Fake = "ME | Note | Prefers paying with Apple Pay first, iDEAL second";
            o.Check = delegate(string root, string output) { return Has(root, Vault.CoreName, "Apple Pay") ?? Has(root, Vault.CoreName, "iDEAL"); };
            cases.Add(o);
            MemoryCase p = Make("p", "what and when he usually orders is kept", 0);
            p.Said = "I usually order a pepperoni pizza from New York Pizza on Friday evenings";
            p.Fake = "ME | Note | Usually orders a pepperoni pizza from New York Pizza on Friday evenings";
            p.Check = delegate(string root, string output) { return Has(root, Vault.CoreName, "pepperoni") ?? Has(root, Vault.CoreName, "Friday"); };
            cases.Add(p);
            MemoryCase q = Make("q", "his home address, sent by the model under its own field name, is kept", 0);
            q.Said = "my home address is Kerkstraat 12 in Utrecht";
            q.Fake = "ME | Address | Kerkstraat 12, Utrecht";
            q.Check = delegate(string root, string output) { return Has(root, Vault.CoreName, "Kerkstraat 12"); };
            cases.Add(q);
            MemoryCase r = Make("r", "a PIN is never kept, whatever field the model puts it under", 0);
            r.Said = "my bank card pin is 4821";
            r.Fake = "ME | Pin | 4821";
            r.Check = delegate(string root, string output) { return Lacks(root, Vault.CoreName, "4821"); };
            cases.Add(r);
            MemoryCase s = Make("s", "'I bought Rengar after the game' becomes a dated event in the day's journal", 0);
            s.Said = "just bought rengar after the game";
            s.Fake = "EVENT | Bought Rengar after the game | 2026-10-30";
            s.Check = delegate(string root, string output) { return Has(root, "Journal/2026-10-30.md", "- event: ") ?? Has(root, "Journal/2026-10-30.md", "Rengar"); };
            cases.Add(s);
            MemoryCase t = Make("t", "'oh I already ordered the pizza' becomes an event", 0);
            t.Said = "oh i already ordered the pizza btw";
            t.LastReply = "Want me to order that pizza for you?";
            t.Fake = "EVENT | Ordered the pizza | 2026-10-30";
            t.Check = delegate(string root, string output) { return Has(root, "Journal/2026-10-30.md", "- event: ") ?? Has(root, "Journal/2026-10-30.md", "pizza"); };
            cases.Add(t);
            MemoryCase u = Make("u", "'I might buy Rengar later' is no event", 0);
            u.Said = "i might buy rengar later";
            u.Fake = "PLAN | maybe: buy Rengar | none";
            u.Check = delegate(string root, string output) { return Lacks(root, "Journal/2026-10-30.md", "- event: "); };
            cases.Add(u);
            MemoryCase v = Make("v", "'got my BMC grade back, 5.1' keeps the grade as a fact, not only as an event", 0);
            v.Said = "got my BMC grade back today, 5.1, so I failed it";
            v.Fake = "ME | Note | Got 5.1 on the BMC report, a fail\nEVENT | Got the BMC grade back, 5.1 | 2026-10-30";
            v.Check = delegate(string root, string output) { return Has(root, Vault.CoreName, "5.1"); };
            cases.Add(v);
            MemoryCase w = Make("w", "'I think my sister is turning 13' keeps the doubt", 0);
            w.Said = "i think my sister is turning 13 this year";
            w.Fake = "Sister | Note | Thinks she is turning 13 this year";
            w.Check = delegate(string root, string output) { string body = Read(root, "People/Sister.md") ?? Read(root, Vault.CoreName) ?? ""; return body.Contains("13") && System.Text.RegularExpressions.Regex.IsMatch(body, @"think|probably|maybe|believe|not sure", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? null : "the age was not kept with the doubt"; };
            cases.Add(w);
            MemoryCase g =Make("g", "after 40 other turns, 'when is Rudiminas's birthday' finds the line", 5);
            g.Ask = "when is rudiminas birthday";
            g.Seed["People/Rudiminas.md"] = Rudiminas + "- Note: Birthday is 30 October\n";
            g.Check = delegate(string root, string output) { return output.Contains("30 October") ? null : "the birthday line was not found"; };
            cases.Add(g);
            MemoryCase h = Make("h", "'what was that hotel I mentioned' finds a chat from 10 days ago", 5);
            h.Ask = "what was that hotel I mentioned";
            h.Seed["Capri/chats/2026-10-20.md"] = "# 2026-10-20\n18:02 him: booked the Hotel Sakura in Kyoto for the trip\n18:02 her: Nice pick, choom.\n";
            h.Check = delegate(string root, string output) { return output.Contains("Sakura") ? null : "the old chat was not found"; };
            cases.Add(h);
            MemoryCase i = Make("i", "after 'forget about the band' the band is found nowhere", 4);
            i.Ask = "what band did I like";
            i.Forget = "Ghost Choir";
            i.Seed[Vault.CoreName] = Core + "- Loves the band Ghost Choir\n";
            i.Seed["Capri/chats/2026-10-28.md"] = "# 2026-10-28\n21:10 him: ghost choir is my favourite band\n21:10 her: Solid taste, choom.\n";
            i.Check = delegate(string root, string output) { return output.IndexOf("Ghost Choir", StringComparison.OrdinalIgnoreCase) >= 0 ? "the forgotten band was still found" : Anywhere(root, "ghost choir"); };
            cases.Add(i);
            MemoryCase j = Make("j", "a thing he never mentioned finds nothing", 0);
            j.Ask = "what is my hamster called";
            j.Check = delegate(string root, string output) { return output.IndexOf("hamster", StringComparison.OrdinalIgnoreCase) < 0 ? null : "something about a hamster was found"; };
            cases.Add(j);
            return cases;
        }

        private static MemoryCase Make(string id, string what, int step)
        {
            MemoryCase made = new MemoryCase();
            made.Id = id;
            made.What = what;
            made.Step = step;
            made.Seed[Vault.CoreName] = Core;
            made.Seed["People/Rudiminas.md"] = Rudiminas;
            return made;
        }

        private static string Read(string root, string relative)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static string Has(string root, string relative, string text)
        {
            string body = Read(root, relative);
            if (body == null) return relative + " was not written";
            return body.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ? null : relative + " does not say " + text;
        }

        private static string Anywhere(string root, string text)
        {
            foreach (string path in Directory.GetFiles(root, "*.md", SearchOption.AllDirectories))
                if (File.ReadAllText(path).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return path.Substring(root.Length).TrimStart('\\') + " still says " + text;
            return null;
        }

        private static string Lacks(string root, string relative, string text)
        {
            string body = Read(root, relative) ?? "";
            return body.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0 ? null : relative + " now says " + text;
        }

        internal static string Run(MemoryCase test, string folder, Cloud remote, bool fake)
        {
            if (test.Step > StepsDone) return "LIMIT";
            string root = Path.Combine(folder, "case-" + test.Id);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (!Vault.Open(root, null)) return "FAIL: the vault did not open";
            foreach (KeyValuePair<string, string> seed in test.Seed)
            {
                string path = Path.Combine(root, seed.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, seed.Value);
            }
            Brain.MemoryPath = Path.Combine(root, Vault.CoreName);
            Names.Reset();
            MemoryQueue.Reset();
            ChatSummary.Reset();
            if (test.Forget != null)
            {
                List<string> topics = new List<string> { test.Forget };
                NoteEdit.Remove(topics);
                ChatArchive.Forget(topics);
                ChatSummary.Forget(topics);
                MemoryQueue.Forget(topics);
            }
            string output;
            string heard = null;
            if (test.Ask != null)
            {
                Meaning.Fill(Lookback.Meant(root), int.MaxValue, null);
                output = Vault.Related(test.Ask) ?? "";
            }
            else
            {
                Func<string, string> kept = PeopleNotes.Extract;
                Action<string> keptHeard = PeopleNotes.Heard;
                if (fake) PeopleNotes.Extract = delegate { return test.Fake; };
                else PeopleNotes.Extract = null;
                PeopleNotes.Heard = delegate(string reply) { heard = reply; };
                try
                {
                    PeopleResult result = PeopleNotes.Learn(remote, test.Said, null, test.LastReply, 1, test.Recent, Day);
                    output = result == null ? "" : string.Join("; ", result.Saved.ToArray());
                }
                finally
                {
                    PeopleNotes.Extract = kept;
                    PeopleNotes.Heard = keptHeard;
                }
            }
            string wrong = test.Check(root, output);
            if (wrong == null) return "PASS";
            return "FAIL: " + wrong + (heard == null ? "" : " (the model said: " + Flat(heard) + ")");
        }

        private static string Flat(string reply)
        {
            string flat = reply.Trim().Replace("\r", "").Replace("\n", " / ");
            return flat.Length > 600 ? flat.Substring(0, 600) + "..." : flat;
        }

        internal static int Live(string folder)
        {
            string target = Path.GetFullPath(folder);
            Directory.CreateDirectory(target);
            string report = Path.Combine(target, "memory-eval.txt");
            StringBuilder lines = new StringBuilder();
            Cloud remote = Key();
            if (remote == null)
            {
                File.WriteAllText(report, "LIMIT no Claude key in settings.json, nothing was run\n");
                return 2;
            }
            string work = Path.Combine(target, "vaults");
            System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072;
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            ActivityLog.FilePath = Path.Combine(target, "activity.log");
            Brain.LogPath = Path.Combine(target, "errors.txt");
            Meaning.Use(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri"));
            Meaning.Store = Path.Combine(target, "vectors.bin");
            lines.Append(Meaning.Ready ? "Meaning search on (bge-base-en-v1.5).\n" : "Meaning search off: " + Meaning.Missing() + ".\n");
            int failed = 0;
            foreach (MemoryCase test in Cases())
            {
                string verdict;
                try { verdict = Run(test, work, remote, false); }
                catch (Exception error) { verdict = "FAIL: " + error.GetType().Name + ": " + error.Message; }
                if (verdict.StartsWith("FAIL", StringComparison.Ordinal)) failed++;
                lines.Append(Line(test, verdict)).Append("\n");
            }
            try { failed += NightEval.Live(work, remote, lines); }
            catch (Exception error) { failed++; lines.Append("FAIL night review live: " + error.GetType().Name + ": " + error.Message + "\n"); }
            lines.Append(failed == 0 ? "All cases that can run passed against the real model.\n" : failed + " case(s) failed against the real model; the vaults are in " + work + "\n");
            File.WriteAllText(report, lines.ToString());
            return failed == 0 ? 0 : 1;
        }

        internal static string Line(MemoryCase test, string verdict)
        {
            string name = "memory eval (" + test.Id + ") " + test.What;
            if (verdict == "LIMIT") return "LIMIT " + name + " (needs step " + test.Step + ")";
            if (verdict == "PASS") return "PASS " + name;
            return "FAIL " + name + ": " + verdict.Substring("FAIL: ".Length);
        }

        internal static Cloud Key()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "settings.json");
            try
            {
                Preferences saved;
                using (FileStream stream = File.OpenRead(path)) saved = (Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(stream);
                if (saved == null || saved.Tier != 2) return null;
                string key = Voice.Unprotect(saved.PaidKey);
                if (key == null) return null;
                Cloud remote = new Cloud(key, saved.PaidModel);
                return remote.Claude ? remote : null;
            }
            catch (Exception) { return null; }
        }
    }
}
