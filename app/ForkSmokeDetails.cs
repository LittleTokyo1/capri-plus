using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static Sight DetailsPage(string title, params string[] pairs)
        {
            Sight page = new Sight { Window = new IntPtr(7), Title = title, Program = "opera" };
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                page.Targets.Add(new Target { Number = i / 2 + 1, Role = "box", Name = pairs[i], Value = pairs[i + 1], Line = true });
            return page;
        }

        private static Sight ButtonPage(string name)
        {
            Sight page = new Sight { Window = new IntPtr(7), Title = "Checkout - New York Pizza", Program = "opera" };
            page.Targets.Add(new Target { Number = 1, Role = "button", Name = name });
            return page;
        }

        private static string FillerNotes(int count)
        {
            StringBuilder lines = new StringBuilder();
            for (int i = 0; i < count; i++) lines.Append("- Likes small thing number " + i + " in the evening\n");
            return lines.ToString();
        }

        private void SmokeDetails(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptPath = Brain.MemoryPath;
            string keptLog = ActivityLog.FilePath;
            ForkSettings keptSettings = ForkSettings.Current;
            Func<Sight> keptLook = Details.Look;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            Func<Cloud> keptRemote = Saving.Remote;
            bool keptBypass = Computer.Bypass;
            string folder = Path.Combine(Path.GetTempPath(), "capri-details-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                Names.Reset();
                MemoryQueue.Reset();
                if (!Vault.Open(Path.Combine(folder, "vault"), null)) throw new Exception("The details vault did not open.");
                string core = Path.Combine(Path.Combine(folder, "vault"), Vault.CoreName);
                Brain.MemoryPath = core;
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Saving.Remote = delegate { return null; };

                string sentence = "Remember my details... if I want you to order something for me, then you know what to type in";
                string[] asks = { "my details", "remember my details", "save my contact details", "my delivery details", "their details", "all my details", "my info", sentence, "Capri, remember my details", "can you save my contact details from the page" };
                string[] others = { "my email is jan@example.nl", "my details are 1234567", "the game details", "the details of the game", "Rudiminas's birthday is 6 May", "my birthday", "my phone is broken", "I'll give you my details later", "she took my details and left", "" };
                foreach (string ask in asks) if (!Details.Wants(ask)) throw new Exception("\"" + ask + "\" was not taken as a request to read his details off the screen.");
                foreach (string other in others) if (Details.Wants(other)) throw new Exception("\"" + other + "\" was taken as a request to read his details off the screen.");

                List<string> gaps = new List<string>();
                Sight bad = DetailsPage("Checkout", "E-mailadres", "jan.test@exam...", "Zoeken", "jan@example.com", "Straat", "Main St; rm -rf", "Postcode", "1234 NN", "Kaartnummer", "4111 1111 1111 1111");
                bad.Targets.Add(new Target { Number = 9, Role = "box", Name = "E-mail", Value = "secret@example.com", Secret = true });
                List<KeyValuePair<string, string>> none = Details.Read(bad.Targets, gaps);
                if (none.Count != 0 || !gaps.Contains("email address") || !gaps.Contains("home address")) throw new Exception("A cut value, a search box, a hostile street, a card box or a password box gave a detail: " + none.Count);

                List<string> noName = new List<string>();
                List<KeyValuePair<string, string>> courier = Details.Read(DetailsPage("Checkout", "Opmerking voor de bezorger", "Bel twee keer", "E-mailadres", "jan.test@example.com").Targets, noName);
                if (courier.Count != 1 || courier[0].Key != "Email address is") throw new Exception("A courier note box was taken as his name: " + courier.Count);
                string page = "Checkout - New York Pizza";
                string[] filled = { "Zoeken", "", "E-mailadres", "jan.test@example.com", "Telefoonnummer", "+31 6 12345678", "Voornaam", "Kasper", "Achternaam", "Testman", "Straat", "Examplestraat", "Huisnummer", "41", "Postcode", "1234 NN", "Woonplaats", "Utrecht", "Wachtwoord", "hunter22", "Kaartnummer", "4111 1111 1111 1111" };
                Sight form = DetailsPage(page, filled);
                form.Targets.Find(delegate(Target target) { return target.Name == "Wachtwoord"; }).Secret = true;
                File.WriteAllText(core, "- Plays League of Legends most evenings\n- Home address is Examplestraat 41, 1234NN (postcode)\n");
                Details.Look = delegate { return form; };
                string heardTool;
                ToolArguments heardArgs;
                if (!Hands.Heard("Remember my details", out heardTool, out heardArgs) || heardTool != "remember" || !Details.Wants(heardArgs.Text)) throw new Exception("Remember my details did not reach the remember tool as a screen request: " + heardTool);
                string first = Hands.Do(heardTool, heardArgs);
                string text = File.ReadAllText(core);
                if (!first.StartsWith("Saved to my notes from the page", StringComparison.Ordinal) || !first.Contains("jan.test@example.com") || !first.Contains("+31 6 12345678") || !first.Contains("1234 NN Utrecht") || !first.Contains("Kasper Testman")) throw new Exception("Reading the form did not say what it saved: " + first);
                if (!text.Contains("- Email address is jan.test@example.com") || !text.Contains("- Phone number is +31 6 12345678") || !text.Contains("- Name on orders is Kasper Testman") || text.Contains("hunter22") || text.Contains("4111")) throw new Exception("The details were not written, or a password or card number was: " + text);
                if (Regex.Matches(text, @"^- Home address is", RegexOptions.Multiline).Count != 1 || !text.Contains("- Home address is Examplestraat 41, 1234 NN Utrecht")) throw new Exception("The home address was added twice or not corrected: " + text);
                string again = Hands.Do(heardTool, heardArgs);
                if (!again.StartsWith("Already in my notes", StringComparison.Ordinal) || File.ReadAllText(core) != text) throw new Exception("Reading the same form again changed the notes or claimed a save: " + again);

                Sight changed = DetailsPage(page, "E-mailadres", "jan.new@example.com", "Telefoonnummer", "+31 6 12345678");
                Details.Look = delegate { return changed; };
                string newer = Hands.Do(heardTool, heardArgs);
                text = File.ReadAllText(core);
                if (!newer.StartsWith("Saved", StringComparison.Ordinal) || !newer.Contains("Not on the page: home address") || Regex.Matches(text, @"^- Email address is", RegexOptions.Multiline).Count != 1 || !text.Contains("- Email address is jan.new@example.com")) throw new Exception("A changed email was not replaced by one line, or a missing address was not said: " + newer);

                string before = File.ReadAllText(core);
                Sight slow = new Sight { Window = new IntPtr(7), Title = page, Problem = "This window took too long to read." };
                foreach (Sight unread in new Sight[] { slow, new Sight(), null })
                {
                    Sight shown = unread;
                    Details.Look = delegate { return shown; };
                    string nothing = Hands.Do(heardTool, heardArgs);
                    if (nothing.StartsWith("Saved", StringComparison.Ordinal) || !nothing.Contains("couldn't") || File.ReadAllText(core) != before) throw new Exception("An unreadable screen was answered with a save or changed the notes: " + nothing);
                }

                Func<Sight, string> keptSeen = Details.Seen;
                try
                {
                    Sight drawn = new Sight { Window = new IntPtr(7), Title = "Checkout - Thuisbezorgd", Program = "chrome" };
                    int pictured = 0;
                    Details.Seen = delegate(Sight shown) { pictured++; return "E-mailadres: jan.pic@example.com\nTelefoonnummer: +31 6 87654321\nVoornaam: Kasper\nAchternaam: Testman\nStraat: Examplestraat\nHuisnummer: 41\nPostcode: 1234 NN\nWoonplaats: Utrecht\nKaartnummer: 4111 1111 1111 1111\nWachtwoord: hunter22"; };
                    File.WriteAllText(core, "- Plays League of Legends most evenings\n");
                    Details.Look = delegate { return drawn; };
                    string fromPicture = Hands.Do(heardTool, heardArgs);
                    string kept = File.ReadAllText(core);
                    if (pictured != 1 || !fromPicture.StartsWith("Saved to my notes from the page", StringComparison.Ordinal) || !kept.Contains("- Email address is jan.pic@example.com") || !kept.Contains("- Phone number is +31 6 87654321") || kept.Contains("4111") || kept.Contains("hunter22"))
                        throw new Exception("A form her hands could not read was not read from a screenshot, or a card or password was kept: " + fromPicture);
                    Details.Seen = delegate { pictured++; return "NONE"; };
                    if (!Hands.Do(heardTool, heardArgs).Contains("couldn't")) throw new Exception("An empty screenshot read was answered with a save.");
                    pictured = 0;
                    ForkSettings.Current.Blocklist = new[] { "revolut" };
                    Sight bank = new Sight { Window = new IntPtr(7), Title = "Revolut", Program = "Revolut" };
                    Details.Look = delegate { return bank; };
                    Details.Seen = keptSeen;
                    if (Details.Glimpse(bank) != null) throw new Exception("A never-look window was sent as a screenshot to read details.");
                    if (Details.Boxes("- Email: a@b.nl\nnot a field\nPostcode: 1234 NN").Count != 2 || Details.Boxes("NONE").Count != 0) throw new Exception("The screenshot's field lines were read wrong.");
                }
                finally { Details.Seen = keptSeen; }

                PeopleNotes.Extract = delegate { return "ME | Note | Email address is jan.said@example.com"; };
                string said = Hands.Do("remember", new ToolArguments { Text = "my email is jan.said@example.com" });
                if (!said.StartsWith("Saved to my notes", StringComparison.Ordinal) || !File.ReadAllText(core).Contains("jan.said@example.com")) throw new Exception("An email he says in words did not go through the memory pass: " + said);

                File.WriteAllText(core, "- Plays League of Legends most evenings\n");
                Details.Look = delegate { return form; };
                string brainKeptOllama = Brain.Ollama;
                System.Net.Sockets.TcpListener fake = null;
                try
                {
                    List<string> asked = new List<string>();
                    Brain.Ollama = FakeServer(new[] { SmokeSay("Saved your details.") }, asked, out fake);
                    PeopleNotes.Extract = delegate { return "NONE"; };
                    List<string> tools = new List<string>();
                    Brain brain = new Brain();
                    brain.Model = "memory-test";
                    brain.Act = delegate(string tool, ToolArguments args) { tools.Add(tool + ":" + args.Text); return Hands.Do(tool, args); };
                    string spoken = brain.Ask(sentence);
                    string kept = File.ReadAllText(core);
                    if (tools.Count != 1 || tools[0] != "remember:my details" || !kept.Contains("- Email address is jan.test@example.com") || !kept.Contains("- Phone number is +31 6 12345678") || kept.Contains("- Their details") || spoken.Length == 0) throw new Exception("His own sentence did not read the screen through the remember tool: " + string.Join(",", tools.ToArray()) + " | " + kept);
                }
                finally
                {
                    if (fake != null) fake.Stop();
                    Brain.Ollama = brainKeptOllama;
                }
                File.WriteAllText(core, FillerNotes(30) +"- Email address is jan.job@example.com\n" + FillerNotes(30) + "- Phone number is +31 6 12345678\n- Prefers paying with Apple Pay first, iDEAL second\n## History\n- until 1 Oct 2026: Email address is old@example.com\n");
                string job = Computer.Jobs(Computer.Prompt);
                if (!job.Contains("jan.job@example.com") || !job.Contains("+31 6 12345678") || !job.Contains("Apple Pay first") || Details.Block().Contains("old@example.com")) throw new Exception("His saved details did not reach a long notes file's job prompt, or a history line did.");
                foreach (string rule in new[] { "Apple Pay", "iDEAL", "the bank in his saved details", "QR code", "call ask with a summary", "total price", "not secrets" }) if (!Fork.JobRules.Contains(rule)) throw new Exception("The ordering rules lack: " + rule);
                if (!Computer.Prompt.Contains("Never type passwords, PINs, card numbers, bank details or other secrets.") || !Computer.Seeing.Contains("Never type passwords, PINs, card numbers, bank details or other secrets.") || Computer.Prompt.Contains("other private details") || Computer.Seeing.Contains("other private details")) throw new Exception("The secrets rule still stops her typing his email and phone, or no longer names the real secrets.");
                checks.Add("PASS his details: 'remember my details' with no values in his words reads email, phone, name and address off the form boxes on screen (UI Automation values, or one screenshot read by the small model when the boxes can't be read, never for a never-look app; never password or card boxes, cut or hostile values, search boxes), keeps them as plain me.md lines, corrects a changed email or a worse-written address in place, says exactly what was saved and what was missing, and says plainly when the screen cannot be read; his own long sentence (remember my details... if I want you to order something) takes the same route without the model writing the save, a courier note box is not his name, an email he says in words still goes through the memory pass; the notes reach a long file's job prompt and the job rules let her type them, name Apple Pay then iDEAL with ExampleBank, the QR code and an ask with a summary");

                string[] buying = { "Bestellen", "Afrekenen", "Betalen", "Bestelling plaatsen", "Nu bestellen", "Bestel nu", "Betaal nu", "Apple Pay", "Pay with iDEAL", "Place order" };
                string[] browsing = { "Toevoegen", "Winkelwagen", "Pepperoni", "Menu", "Verder", "Bestelling" };
                foreach (string name in buying) if (Computer.Risk("click", new ToolArguments { Target = "1" }, ButtonPage(name)) != "buy") throw new Exception("\"" + name + "\" was not treated as buying.");
                foreach (string name in browsing) if (Computer.Risk("click", new ToolArguments { Target = "1" }, ButtonPage(name)) != null) throw new Exception("\"" + name + "\" was treated as risky.");
                string headline = "Pizza bestellen doe je online via New York Pizza";
                Sight resultLink = ButtonPage(headline);
                resultLink.Targets[0].Role = "link";
                Sight shortLink = ButtonPage("Bestellen");
                shortLink.Targets[0].Role = "link";
                if (Computer.Risk("click", new ToolArguments { Target = "1" }, resultLink) != null || Computer.Risk("click", new ToolArguments { Target = "1" }, shortLink) != "buy" || Computer.Risk("click", new ToolArguments { Target = "1" }, ButtonPage(headline)) != "buy") throw new Exception("A search result headline that says bestellen was treated as buying, or a short order link or a long order button was let through.");
                foreach (string payLink in new[] { "Ga verder naar betalen met iDEAL", "Betaal €17,50 nu met Apple Pay", "Proceed to checkout and pay for your order" })
                {
                    Sight paying = ButtonPage(payLink);
                    paying.Targets[0].Role = "link";
                    if (Computer.Risk("click", new ToolArguments { Target = "1" }, paying) != "buy") throw new Exception("A long pay link went past the buy gate: " + payLink);
                }
                Sight focused = ButtonPage("Bestelling plaatsen");
                focused.Targets[0].Focused = true;
                if (Computer.Risk("press", new ToolArguments { Keys = "enter" }, focused) != "buy" || Computer.Risk("press", new ToolArguments { Keys = "enter" }, ButtonPage("Bestelling plaatsen")) != null) throw new Exception("Enter on a focused order button went past the buy gate, or Enter on an unfocused one was stopped.");
                if (Computer.Risk("click", new ToolArguments { Target = "1" }, ButtonPage("Subscribe")) != "subscribe") throw new Exception("Subscribe was treated as buying, so Blackwall mode would stop it.");
                Computer.Bypass = true;
                Job order = new Job("order me a pizza from New York Pizza");
                string blackwall = Computer.Allowing(Computer.Prompt);
                bool gated = order.Needs("buy") && !order.Needs("send") && !order.Needs("delete") && !order.Needs("subscribe") &&!Computer.Needless("Place the order for €17,50?") && !Computer.Needless("Bestelling plaatsen voor 17,50?");
                Computer.Bypass = keptBypass;
                if (!gated || !blackwall.Contains("call ask before you buy") || !blackwall.Contains("except before you buy or pay") || !new Job("order me a pizza").Needs("buy")) throw new Exception("Blackwall mode let a buy click through without his yes, or hid the order summary question.");
                Sight checkout = ButtonPage("Bestelling plaatsen");
                checkout.Words.Add("Subtotaal €15,00");
                checkout.Words.Add("Totaal €17,50");
                string question = Computer.Question("click", new ToolArguments { Target = "1" }, checkout);
                string plain = Computer.Question("click", new ToolArguments { Target = "1" }, ButtonPage("Bestelling plaatsen"));
                if (!question.Contains("Bestelling plaatsen") || !question.Contains("New York Pizza") || !question.Contains("17,50") || question.Contains("15,00") || plain.Contains("total")) throw new Exception("The buy question did not name the shop and the total, or guessed one: " + question + " | " + plain);
                checks.Add("PASS orders wait for his yes: the Dutch checkout words (Bestellen, Afrekenen, Betalen, Bestelling plaatsen, Bestel nu) and Apple Pay or iDEAL buttons count as buying like Place order, browsing buttons do not, a buy click, or Enter on a focused order button, stops for his yes even in Blackwall mode (other Blackwall steps and Subscribe stay unchecked) and a yes covers that one click only, a question about paying still reaches him in Blackwall mode, and the question names the shop and the total price from the page when it shows one; a long search result link that says bestellen (a headline of six words or more) is a link to read, not a buy button, while a short order link and a long order button still ask");
            }
            finally
            {
                Computer.Bypass = keptBypass;
                Details.Look = keptLook;
                PeopleNotes.Extract = keptExtract;
                Saving.Remote = keptRemote;
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
