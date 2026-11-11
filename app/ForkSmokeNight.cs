using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeNight(List<string> checks)
        {
            SmokeWanderInChat(checks);
            SmokeEarsLimits(checks);
            SmokeChatLog(checks);
            SmokeNotesTool(checks);
            SmokeSelfKnowledge(checks);
            SmokeTidyWords(checks);
            SmokeFoundNote(checks);
            SmokeMailRoute(checks);
            SmokeMainframePlus(checks);
            SmokeNoonFixes(checks);
            SmokeSchedule(checks);
            SmokeMicReport(checks);
            SmokeHonest(checks);
            SmokeSettingsSearch(checks);
            SmokeDiscordFriends(checks);
            SmokeDiscordInbox(checks);
            SmokeDiscordPresence(checks);
            SmokeDiscordTools(checks);
            SmokeForgetCheck(checks);
            SmokeTuning(checks);
            SmokeLongJob(checks);
            SmokeHome(checks);
            SmokeAway(checks);
            SmokeCheckIns(checks);
            SmokeParts(checks);
            SmokeAppGuess(checks);
            SmokeShotMonitors(checks);
            SmokeMemoryRetry(checks);
            SmokeMemoryPerson(checks);
            SmokeMemoryBase(checks);
            SmokeFreshStart(checks);
            SmokeMemoryEval(checks);
            SmokeSaving(checks);
            SmokeDetails(checks);
            SmokeHistory(checks);
            SmokeArchive(checks);
            SmokeLookback(checks);
            SmokeMeaning(checks);
            SmokeFetchStall(checks);
            SmokeCore(checks);
            SmokePlans(checks);
            SmokeOrders(checks);
            SmokeSpotify(checks);
            SmokeVolume(checks);
            SmokeNightReview(checks);
            SmokeUi(checks);
            SmokeNoteTidy(checks);
            SmokeMessageRoute(checks);
            SmokeAsk(checks);
            SmokeMurmur(checks);
            SmokeCharm(checks);
            SmokeFly(checks);
            SmokeHands(checks);
            SmokeUpstream078(checks);
            SmokeCaptcha(checks);
            SmokeHotwords(checks);
            SmokePains(checks);
            SmokeProbe(checks);
            SmokeShots(checks);
        }

        private void SmokeMessageRoute(List<string> checks)
        {
            if (!Fork.TaskRules.Contains("use the discord tool") || !Fork.TaskRules.Contains("from my account") || !Fork.TaskRules.Contains("WhatsApp") || !Fork.TaskRules.Contains("move my mouse and send it")) throw new Exception("Her messaging rules do not send plain messages through the discord tool and the account case through a job.");
            if (!Hands.Prompt.Contains("discord") && !Hands.Listing.Contains("discord") && !Hands.Tools.Contains("discord")) throw new Exception("The discord tool is not offered to her.");
            checks.Add("PASS message route: her rules say a plain message, text or tell goes through the discord tool from her bot, and use_computer is only for from my account, other apps and replies inside an open conversation, so the prompt no longer contradicts the tool");
        }

        private void SmokeSettingsSearch(List<string> checks)
        {
            ForkSettings picked = new ForkSettings { TalkSeconds = 33, PauseTenths = 17 };
            using (ForkSettingsForm form = new ForkSettingsForm(picked))
            {
                string[] everything = form.Filter("");
                string[] mic = form.Filter("mic sensitivity");
                string[] micWide = form.Filter("MIC");
                string[] section = form.Filter("autonomy");
                string[] nothing = form.Filter("zzzzqq");
                string[] spaced = form.Filter("  hold to talk  ");
                if (everything.Length < 50) throw new Exception("The settings window listed too few settings: " + everything.Length);
                if (mic.Length != 1 || !mic[0].StartsWith("Mic sensitivity", StringComparison.Ordinal)) throw new Exception("Searching mic sensitivity found: " + string.Join(" | ", mic));
                foreach (string found in micWide) if (found.ToLowerInvariant().IndexOf("mic", StringComparison.Ordinal) < 0) throw new Exception("A setting without the word was shown for mic: " + found);
                if (micWide.Length < 2 || section.Length < 5 || nothing.Length != 0 || spaced.Length != 1) throw new Exception("The settings search counts were wrong: " + micWide.Length + " / " + section.Length + " / " + nothing.Length + " / " + spaced.Length);
                string[] names = form.TabNames;
                form.Filter("hold to talk");
                string hotkeys = form.SelectedTabName;
                form.Filter("autonomy");
                string autonomyTab = form.SelectedTabName;
                if (string.Join(",", names) != "Brain,Spending,Voice,Personality,Autonomy,Hands,Discord,Home,Vision,Gaming,Studies,Projects,Fun,Hotkeys" || Array.IndexOf(names, "Studies") < 0 || Array.IndexOf(names, "Spending") < 0 || Array.IndexOf(names, "Voice") < 0 || Array.IndexOf(names, "Autonomy") < 0 || Array.IndexOf(names, "Home") < 0 || Array.IndexOf(names, "Brain") < 0 || Array.IndexOf(names, "Vision") < 0 || Array.IndexOf(names, "Hotkeys") < 0 || Array.IndexOf(names, "Gaming") < 0 || hotkeys != "Hotkeys" || autonomyTab != "Autonomy") throw new Exception("The settings window tabs were wrong: " + string.Join(", ", names) + " / " + hotkeys + " / " + autonomyTab);
                form.Filter("");
                form.Apply();
                if (form.Filter("").Length != everything.Length || picked.TalkSeconds != 33 || picked.PauseTenths != 17) throw new Exception("Searching changed what the settings window saves.");
            }
            checks.Add("PASS settings search: the Capri+ settings window has a Find a setting box that shows only the settings whose words match (or every setting of a section whose title matches), is not case or space sensitive, shows nothing for a miss, keeps Save and Cancel, and never changes what is saved");
        }

        private void SmokeHonest(List<string> checks)
        {
            string[] good = { "Opened Discord: Opened Discord.", "Clicked the box: Clicked." };
            string[] lastFailed = { "Opened Discord: Opened Discord.", "Typed hi: That didn't work. The box wasn't there." };
            string[] retried = { "Typed hi: That didn't work. The box wasn't there.", "Typed hi: Typed it." };
            string[] allFailed = { "Opened Discord: Couldn't find Discord.", "Opened Discord: That didn't work. Nothing opened." };
            string honest = Honest.Outcome("Done. Sent it.", lastFailed);
            if (!honest.Contains("last step didn't work") || !honest.Contains("The box wasn't there") || !honest.StartsWith("Done. Sent it.", StringComparison.Ordinal)) throw new Exception("A partly failed job still said done: " + honest);
            if (Honest.Outcome("Done. Sent it.", good) != "Done. Sent it." || Honest.Outcome("Done. Sent it.", retried) != "Done. Sent it." || Honest.Outcome("I couldn't find him.", lastFailed) != "I couldn't find him." || Honest.Outcome("Done.", null) != "Done.") throw new Exception("An honest ending was changed.");
            if (!Honest.Summary(allFailed).StartsWith("Couldn't do that", StringComparison.Ordinal) || !Honest.Summary(allFailed).Contains("Nothing opened") || !Honest.Summary(lastFailed).StartsWith("Partly done", StringComparison.Ordinal) || Honest.Summary(good) != "Done. Opened Discord, then Clicked." || !Honest.Summary(new string[0]).Contains("haven't done anything")) throw new Exception("A job summary was not honest: " + Honest.Summary(allFailed) + " | " + Honest.Summary(lastFailed) + " | " + Honest.Summary(good));
            if (!Job.Summed(allFailed).StartsWith("Couldn't do that", StringComparison.Ordinal) || Job.Summed(new[] { "Clicked add to cart: " + Computer.NoCart }) != "Done." || Job.Summed(good) != "Done. Opened Discord, then clicked.") throw new Exception("A job's own summary was not honest: " + Job.Summed(allFailed) + " | " + Job.Summed(new[] { "Clicked add to cart: " + Computer.NoCart }) + " | " + Job.Summed(good));
            string task = "In Discord, open the DM with Rudce and tell him to hop on the game";
            string[] echoed = { "Tell Rudce to hop on the game", "In Discord, open the DM with Rudce and tell him to hop on the game", "ask him about the game tonight" };
            string[] meant = { "yo Rudce hop on, we're queuing", "hop on the game", "hop on the game now Rudce", "hop on", "lol where are you" };
            foreach (string typed in echoed) if (!Honest.Echo(typed, task, true)) throw new Exception("\"" + typed + "\" was not caught as the instruction.");
            foreach (string typed in meant) if (Honest.Echo(typed, task, true)) throw new Exception("\"" + typed + "\" was caught as the instruction.");
            if (Honest.Echo(null, task, true) || Honest.Echo("tell him to hop on", null, true)) throw new Exception("An empty message or task was caught.");
            string search = "Search YouTube for lofi hip hop radio beats to relax study";
            string dictated = "In Discord message Rudce and say exactly \"hop on the game right now my friend\"";
            if (Honest.Echo("lofi hip hop radio beats to relax study", search, true) || Honest.Echo("Tell Rudce to hop on the game", task, false) || Honest.Echo("hop on the game right now my friend", dictated, true) || Honest.Echo("tell me about it", task, true) || Honest.Echo("Search YouTube for lofi hip hop radio beats to relax study", search, true)) throw new Exception("A search, a form, dictated words, a first-person line or a typed step outside a chat box was caught as the instruction.");
            checks.Add("PASS honest outcomes: a job that ends with Done after its last step failed says that step didn't work (and why), a summary with nothing that worked says it couldn't (her job summary too, unless the only miss was her own no-cart refusal), a partial one says partly done, a retry that worked and a plain success stay as they were, and a typed message that is only the job's instruction words (tell him to ..., or the task copied) is refused so she writes her own words");
        }

        private void SmokeMicReport(List<string> checks)
        {
            string[] asked = { "can you hear me", "Capri, can you hear me?", "do you hear me", "are you listening", "is my mic working", "is my microphone on", "mic check", "test my microphone", "can you hear me now" };
            foreach (string said in asked) if (!MicReport.Asked(said)) throw new Exception("\"" + said + "\" was not taken as a mic question.");
            string[] other = { "tell him to hear me out", "what is a mic check", "can you hear that song", "I can hear you", "turn the mic off", "listen to this song", "are you listening to music" };
            foreach (string said in other) if (MicReport.Asked(said)) throw new Exception("\"" + said + "\" was taken as a mic question.");
            string off = MicReport.Say(false, true, 10, 3, "F8", "Ctrl + Shift + K");
            string muted = MicReport.Say(true, true, -2, 3, "F8", "Ctrl + Shift + K");
            string quiet = MicReport.Say(true, true, -1, 3, "F8", "Ctrl + Shift + K");
            string loading = MicReport.Say(true, false, 10, 3, "F8", "Ctrl + Shift + K");
            string live = MicReport.Say(true, true, 12.4, 8, "F8", "Ctrl + Shift + K");
            string fresh = MicReport.Say(true, true, 12.4, -1, "F8", "Ctrl + Shift + K");
            string flat = MicReport.Say(true, true, 0.2, 90, "F8", "Ctrl + Shift + K");
            if (!off.Contains("voice chat is off") || !off.Contains("F8") || !muted.Contains("muted") || !muted.Contains("Ctrl + Shift + K") || !quiet.Contains("no signal") || !loading.Contains("still loading")) throw new Exception("A mic answer was wrong: " + off + " | " + muted + " | " + quiet + " | " + loading);
            if (!live.Contains("hear you") || !live.Contains("12") || !live.Contains("8 seconds ago") || !fresh.Contains("haven't picked up any words") || !flat.Contains("very quiet") || !flat.Contains("1 minute")) throw new Exception("A live mic answer was wrong: " + live + " | " + fresh + " | " + flat);
            checks.Add("PASS mic report: can you hear me, are you listening, is my mic working and mic check are answered from the real state (voice chat off, mic muted, no signal, ears still loading, level, seconds since the last words, or no words yet) instead of from the screen, and similar talk about music or other people is left to her brain");
        }

        private void SmokeMainframePlus(List<string> checks)
        {
            System.Windows.Forms.Control plus = null, support = null, updates = null;
            foreach (System.Windows.Forms.Control control in mainframe.Controls)
            {
                if (control.Text == "Capri+") plus = control;
                if (control.Text == "Support") support = control;
                if (control.Text == "Check for updates") updates = control;
            }
            if (plus == null || support == null || updates == null) throw new Exception("The Mainframe has no Capri+, Support or update key.");
            if (plus.Bounds != support.Bounds) throw new Exception("The Capri+ key is not in the Support key's place: " + plus.Bounds + " / " + support.Bounds);
            foreach (System.Windows.Forms.Control control in mainframe.Controls)
                if (control != plus && control != support && control is System.Windows.Forms.Button && control.Bounds.IntersectsWith(plus.Bounds)) throw new Exception("The Capri+ key is covered by " + control.Text + ": " + control.Bounds);
            if (!ForkHides(support, UpstreamHidden) || ForkHides(updates, UpstreamHidden) || ForkHides(plus, UpstreamHidden)) throw new Exception("The fork hides the update key or Capri+, or keeps the Ko-fi Support key.");
            checks.Add("PASS Mainframe Capri+ key (his 12:41 bug): the Capri+ key sits in the Ko-fi Support key's place at the right of the CAPRI band, the fork hides Support and keeps Check for updates (our own updater), and no other key covers Capri+");
        }

        private void SmokeMailRoute(List<string> checks)
        {
            ToolArguments gmail = new ToolArguments { Site = "mail.google.com" };
            ToolArguments calendar = new ToolArguments { Site = "google", Search = "google calendar this week" };
            ToolArguments video = new ToolArguments { Site = "youtube", Search = "lofi" };
            string steered = MailRoute.Browser(gmail, "do I have new mail", true);
            if (steered == null || !steered.Contains("google tool") || !steered.Contains("find_mail") || MailRoute.Browser(calendar, "what is on this week", true) == null) throw new Exception("Mail or calendar in the browser was not steered to the google tool: " + steered);
            if (MailRoute.Browser(gmail, "open my gmail", true) != null || MailRoute.Browser(gmail, "do I have new mail", false) != null || MailRoute.Browser(video, "play lofi", true) != null || MailRoute.Browser(null, "x", true) != null) throw new Exception("A browser page they asked for, or an unconnected Google, or another site was steered.");
            string[] viaTool = { "Open Gmail in the browser and read my latest emails", "check my calendar in Google Calendar", "In Gmail find the mail from StudyPortal and summarise it", "write a reply to the email from the bank in Gmail and send it" };
            foreach (string task in viaTool) if (!MailRoute.Job(task, true)) throw new Exception("\"" + task + "\" was not steered to the google tool.");
            string[] byHand = { "In Gmail, search for emails from GitHub and clean them out of the inbox: select them all and archive or delete them", "Open Gmail and unsubscribe from the newsletters", "label the emails from StudyPortal in Gmail", "play some music on Spotify", "open Discord and read the latest messages" };
            foreach (string task in byHand) if (MailRoute.Job(task, true)) throw new Exception("\"" + task + "\" was steered to the google tool.");
            if (MailRoute.Job(viaTool[0], false) || !MailRoute.Rule(true).Contains("google tool") || MailRoute.Rule(false).Length != 0) throw new Exception("An unconnected Google was steered, or the prompt rule was wrong.");
            string[] forms = { "Continue the New York Pizza order. Find the free upsize coupon, add the Brooklyn style pizza and fill in my email address and phone at checkout", "Sign up on the site with my email and check the box", "Find the delivery address box and type my email in the contact field, then send the order", "Register for the Dutch course: write my e-mail into the form and add my name", "Send Rudce my email on Discord: jan.test@gmail.com", "Find the booking page and send them jan.test@gmail.com as the reply address" };
            foreach (string task in forms) if (MailRoute.Job(task, true)) throw new Exception("\"" + task + "\" (a form or an order, not his mailbox) was steered to the google tool.");
            DateTime now = new DateTime(2026, 10, 30, 22, 26, 0);
            MailRoute.Reset();
            string first = MailRoute.JobSteer(viaTool[0], true, now);
            string again = MailRoute.JobSteer(viaTool[1], true, now.AddMinutes(1));
            string later = MailRoute.JobSteer(viaTool[2], true, now.AddMinutes(11));
            MailRoute.Reset();
            if (first == null || again != null || later == null || MailRoute.JobSteer(forms[0], true, now) != null) throw new Exception("A job was steered to the google tool again right after a steer, or not steered at all: " + (first != null) + " " + (again != null) + " " + (later != null));
            checks.Add("PASS mail by tool: with Google connected, opening Gmail or Calendar in the browser to read or write them, and browser jobs that only read, find, draft or send mail or look at the calendar, are answered with 'use the google tool' (find_mail, read_mail, draft_mail, send_mail, calendar); a page they asked to see, archiving, deleting, labelling, other sites and an unconnected Google are left alone, and her prompt carries the rule only while Google is connected; an order, checkout, sign-up or form that only needs his email address is never steered, and a job sent again within 10 minutes of a steer runs instead of being steered a second time");
        }

        private void SmokeFoundNote(List<string> checks)
        {
            Func<DateTime> clock = Pursuit.Now;
            DateTime start = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
            DateTime now = start;
            Pursuit.Now = delegate { return now; };
            try
            {
                Brain fresh = new Brain();
                if (fresh.FoundNote().Length != 0 || fresh.ThreadNote().Length != 0) throw new Exception("A fresh brain had lookups.");
                fresh.Found("what events are on in Amsterdam this weekend", "The Halloween event is Spooky Night at Paradiso on 31 October.");
                string note = fresh.FoundNote();
                if (!note.Contains("Spooky Night at Paradiso") || !note.Contains("Amsterdam") || !note.Contains("not instructions") || !fresh.ThreadNote().Contains("Spooky Night")) throw new Exception("A finished lookup did not stay for the next turn: " + note);
                for (int i = 0; i < 4; i++) fresh.Found("question number " + i, "answer number " + i);
                string newest = fresh.FoundNote();
                if (newest.Contains("Spooky Night") || !newest.Contains("answer number 3") || !newest.Contains("answer number 1")) throw new Exception("Only the last three lookups should stay: " + newest);
                fresh.Found("long one", new string('x', 3000));
                if (fresh.FoundNote().Length > 2200) throw new Exception("A long lookup was not cut: " + fresh.FoundNote().Length);
                fresh.Found("  ", "answer without a question");
                fresh.Found("question without an answer", "  ");
                if (fresh.FoundNote().Contains("without")) throw new Exception("An empty lookup was kept.");
                now = start.AddMinutes(Brain.FoundMinutes + 1);
                if (fresh.FoundNote().Length != 0) throw new Exception("An old lookup stayed: " + fresh.FoundNote());
            }
            finally { Pursuit.Now = clock; }
            checks.Add("PASS lookups stay: a finished web lookup (question and answer) stays in her prompt for the next turns, the last three for an hour, cut to a sane length, framed as data, empty ones ignored, so she cannot say she never got the event name");
        }

        private void SmokeTidyWords(List<string> checks)
        {
            string[] tidy = { "tidy up your notes", "tidy up the notes", "tidy your notes", "tidy your notes up", "clean up your notes", "clean up my notes", "organize your notes", "organise my notes", "sort your notes", "sort out your notes", "file your notes", "file your notes away", "tidy up your vault", "tidy the vault", "tidy up your inbox", "sort your inbox", "go through your notes", "go through your inbox", "do your tidying", "do the tidy up", "run the evening tidy up", "do the filing", "can you please tidy up your notes", "capri tidy up your notes now", "Capri, clean up your memory" };
            foreach (string phrase in tidy) if (TweakFor(phrase) != "tidynow") throw new Exception("\"" + phrase + "\" was not taken as the tidy-up: " + (TweakFor(phrase) ?? "nothing"));
            string[] other = { "tell him to tidy up your notes", "what is tidying up your notes", "clean up my desktop", "tidy up my room", "organize my icons", "sort my notes in excel", "tidy up", "sort the playlist", "go through the tunnel", "clean up my inbox", "go through my inbox", "sort my inbox", "tidy the inbox", "clean up my gmail inbox", "how do I tidy up my notes", "turn the evening tidy up off" };
            foreach (string phrase in other) if (TweakFor(phrase) == "tidynow") throw new Exception("\"" + phrase + "\" was taken as the tidy-up.");
            if (TweakFor("turn the evening tidy up off") != "nightlyoff" || TweakFor("evening tidy up on") != "nightlyon") throw new Exception("The nightly switch words changed.");
            checks.Add("PASS tidy words: the tidy-up is understood in the many ways of asking (tidy, clean, sort, organise or file your or my notes, vault or inbox, go through them, do the tidying or the filing), while a message to someone else, a question, the desktop or icons, a bare tidy up and the nightly on and off switch are not taken as it");
        }

        private void SmokeSelfKnowledge(List<string> checks)
        {
            SelfKnowledge.SetKeys("F8", "Ctrl + Shift + K", "Win + Alt + T", "Win + Alt + J");
            string block = SelfKnowledge.Block();
            string pause = KeyName(ForkSettings.Current.PauseMods, ForkSettings.Current.PauseVk);
            string[] facts = { "He built you", "Claude Code", "AI companion", "not a window", "pause key is " + pause, "F8 talks", "Ctrl + Shift + K mutes", "Win + Alt + T", "Win + Alt + J", "remember tool", "notes tool", "me.md", "People notes", "autonomy on", "remind me at 12:40", "he is leaving you go quiet", "undo that removes only what you made", "Never say you can't write to your notes" };
            foreach (string fact in facts) if (!block.Contains(fact)) throw new Exception("Her facts about herself were missing " + fact + ": " + block);
            string lower = block.ToLowerInvariant();
            if (lower.Contains("seele") || lower.Contains("lithuan") || lower.Contains("wish list") || block.Length > 1800) throw new Exception("Her facts about herself named something that does not exist or ran too long: " + block.Length);
            SelfKnowledge.SetKeys("F9", "Ctrl + M", "Win + Alt + T", "Win + Alt + J");
            if (!SelfKnowledge.Block().Contains("F9 talks") || SelfKnowledge.Block().Contains("F8 talks")) throw new Exception("A changed key was not followed.");
            if (!Brain.TurnNotes(false).Contains(SelfKnowledge.Block()) || !Brain.TurnNotes(true).Contains(SelfKnowledge.Block())) throw new Exception("The facts about herself do not reach every chat turn.");
            checks.Add("PASS self knowledge: every turn tells her who made her (Kasper, with Claude Code), that she is an AI companion and not a window, how her notes and memory pass work, which tools read and write them, her live keys (pause, talk, mute, type, hide) read from the settings and following a change, and autonomy on or off; nothing she cannot do and no code name");
        }

        private void SmokeNotesTool(List<string> checks)
        {
            string folder = Path.Combine(Path.GetTempPath(), "capri-notes-smoke-" + Guid.NewGuid().ToString("N"));
            DateTime now = new DateTime(2026, 10, 30, 9, 30, 0);
            try
            {
                Directory.CreateDirectory(Path.Combine(folder, "People"));
                Directory.CreateDirectory(Path.Combine(folder, "Areas"));
                Directory.CreateDirectory(Path.Combine(folder, "Capri"));
                Directory.CreateDirectory(Path.Combine(folder, ".obsidian"));
                File.WriteAllText(Path.Combine(folder, "me.md"), "- Runs code every day");
                File.WriteAllText(Path.Combine(folder, "People", "Sister.md"), "- Name: Freya\n- Birthday: 8 November");
                File.WriteAllText(Path.Combine(folder, "Areas", "Semester 6 plan.md"), "Block 2 electives close 2 Nov in StudyPortal.");
                File.WriteAllText(Path.Combine(folder, "Areas", "Big.md"), new string('x', 9000));
                File.WriteAllText(Path.Combine(folder, "Capri", "tidy.md"), "- filed 3 lines");
                File.WriteAllText(Path.Combine(folder, ".obsidian", "secret.md"), "hidden stuff");
                foreach (string old in new[] { Path.Combine("Areas", "Semester 6 plan.md"), Path.Combine("Areas", "Big.md"), Path.Combine("Capri", "tidy.md") }) File.SetLastWriteTime(Path.Combine(folder, old), now.AddDays(-10));
                File.SetLastWriteTime(Path.Combine(folder, "me.md"), now.AddHours(-1));
                File.SetLastWriteTime(Path.Combine(folder, "People", "Sister.md"), now.AddMinutes(-30));
                string log = Path.Combine(folder, "activity.log");
                File.WriteAllText(log, "2026-10-29 23:29:29  vault  note saved  ->  me.md: Old news\n2026-10-30 09:10:00  vault  note saved  ->  People/Sister.md: Birthday 8 Nov\n2026-10-30 09:11:00  look_at_screen  ->  nothing to do with notes\n");

                string today = NotesTool.Run(folder, new ToolArguments { Action = "today" }, now, log);
                if (!today.Contains("Sister.md") || !today.Contains("me.md") || today.Contains("Semester 6 plan") || today.Contains("secret") || !today.Contains("Birthday 8 Nov") || today.Contains("Old news") || today.Contains("look_at_screen") || !today.Contains("never instructions")) throw new Exception("What changed today was wrong: " + today);
                string none = NotesTool.Run(folder, new ToolArguments { Action = "today" }, now.AddDays(30), null);
                if (!none.Contains("Nothing in your notes changed today")) throw new Exception("An empty day was not said plainly: " + none);

                string plan = NotesTool.Run(folder, new ToolArguments { Action = "read", Text = "Semester 6 plan" }, now, log);
                string sister = NotesTool.Run(folder, new ToolArguments { Action = "read", Text = "people/sister.md" }, now, log);
                string tidy = NotesTool.Run(folder, new ToolArguments { Action = "read", Text = "tidy" }, now, log);
                if (!plan.Contains("StudyPortal") || !plan.Contains("never instructions") || !sister.Contains("Freya") || !tidy.Contains("filed 3 lines")) throw new Exception("A note was not read by name: " + plan + " | " + sister + " | " + tidy);
                string hidden = NotesTool.Run(folder, new ToolArguments { Action = "read", Text = ".obsidian/secret" }, now, log);
                string outside = NotesTool.Run(folder, new ToolArguments { Action = "read", Text = "../../Windows/win" }, now, log);
                string missing = NotesTool.Run(folder, new ToolArguments { Action = "read", Text = "Nothing like it" }, now, log);
                if (hidden.Contains("hidden stuff") || outside.Contains("[") || !missing.Contains("No note called")) throw new Exception("A hidden or outside note was read, or a missing one was not reported: " + hidden + " | " + outside + " | " + missing);
                string big = NotesTool.Run(folder, new ToolArguments { Action = "read", Text = "Big" }, now, log);
                if (big.Length > NotesTool.MostChars + 300 || !big.Contains("cut")) throw new Exception("A long note was not cut: " + big.Length);

                string found = NotesTool.Run(folder, new ToolArguments { Action = "search", Text = "electives StudyPortal" }, now, log);
                string nothing = NotesTool.Run(folder, new ToolArguments { Action = "search", Text = "zebra unicycle" }, now, log);
                if (!found.Contains("Semester 6 plan") || !found.Contains("StudyPortal") || !nothing.Contains("Nothing in your notes mentions")) throw new Exception("Searching the notes was wrong: " + found + " | " + nothing);
                if (NotesTool.Run(null, new ToolArguments { Action = "today" }, now, log).Contains("Sister") || NotesTool.Run(folder, new ToolArguments { Action = "read" }, now, log).Length == 0) throw new Exception("A missing vault or a missing note name was not handled.");

                if (!Hands.Valid("notes", new ToolArguments { Action = "today" }) || !Hands.Valid("notes", new ToolArguments { Action = "read", Text = "Sister" }) || !Hands.Valid("notes", new ToolArguments { Action = "search", Text = "birthday" }) || Hands.Valid("notes", new ToolArguments { Action = "read" }) || Hands.Valid("notes", new ToolArguments { Action = "delete", Text = "Sister" }) || Hands.Valid("notes", null)) throw new Exception("The brain's own check of the notes tool was wrong.");
                if (!Hands.ClaudeTools.Contains("\"name\":\"notes\"") || !Hands.Tools.Contains("notes")) throw new Exception("The notes tool is not offered to her brain.");
            }
            finally
            {
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS notes tool: she can list what changed in her notes today (files plus the saves in the activity log, nothing older), read a note by name or path (never a hidden or outside file, long notes cut), and search her notes by words, all read-only and framed as data; it is one of her tools and passes the brain's own check");
        }

        private void SmokeEarsLimits(List<string> checks)
        {
            string folder = Path.Combine(Path.GetTempPath(), "capri-ears-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string old = Path.Combine(folder, "old.json");
                File.WriteAllText(old, "{\"Proactive\":1,\"Cap\":10}");
                ForkSettings loaded = ForkSettings.Read(old);
                if (loaded == null || loaded.TalkSeconds != 25 || loaded.PauseTenths != 13) throw new Exception("An old fork.json did not start with 25 seconds per message and a 1.3 second pause.");
                ForkSettings wild = new ForkSettings { TalkSeconds = 999, PauseTenths = 1 };
                wild.Tidy();
                ForkSettings tiny = new ForkSettings { TalkSeconds = 0, PauseTenths = 999 };
                tiny.Tidy();
                if (wild.TalkSeconds != 60 || wild.PauseTenths != 6 || tiny.TalkSeconds != 5 || tiny.PauseTenths != 30) throw new Exception("The ears settings were not kept in range.");
                if (EndQuietChunks(loaded) != 13 || MostChunks(loaded) != 250 || EndQuietChunks(wild) != 6 || MostChunks(wild) != 600) throw new Exception("The ears settings did not turn into the right number of 0.1 second chunks.");
                ForkSettings picked = new ForkSettings { TalkSeconds = 40, PauseTenths = 20 };
                using (ForkSettingsForm form = new ForkSettingsForm(picked)) form.Apply();
                if (picked.TalkSeconds != 40 || picked.PauseTenths != 20) throw new Exception("The settings window did not save the ears settings.");
            }
            finally
            {
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS ears: a spoken message may last 25 seconds (it was 15) and ends after a 1.3 second pause (it was 0.9, which cut him off mid-thought), both settings with limits, saved by the settings window and kept from an old fork.json");
        }

        private void SmokeChatLog(List<string> checks)
        {
            string folder = Path.Combine(Path.GetTempPath(), "capri-chatlog-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                List<ChatMessage> history = new List<ChatMessage> { new ChatMessage("user", "Hello there"), new ChatMessage("assistant", "Hi, choom.") };
                DateTime now = new DateTime(2026, 10, 30, 1, 0, 0);
                ChatLog.Seen(history);
                ChatLog.Append(folder, history, now);
                if (Directory.Exists(folder) && Directory.GetFiles(folder).Length != 0) throw new Exception("History from before the launch was logged again.");
                history.Add(new ChatMessage("user", "What did you save today?"));
                history.Add(new ChatMessage("assistant", "Let me look."));
                ChatLog.Append(folder, history, now.AddMinutes(1));
                ChatLog.Append(folder, history, now.AddMinutes(2));
                string file = Path.Combine(folder, "2026-10-30.log");
                string[] lines = File.ReadAllLines(file);
                if (lines.Length != 2 || !lines[0].Contains("user") || !lines[0].Contains("What did you save today?") || !lines[1].Contains("assistant") || !lines[1].Contains("Let me look.")) throw new Exception("The chat log did not hold exactly the two new messages once: " + string.Join(" | ", lines));
                history.RemoveRange(0, 3);
                history.Add(new ChatMessage("user", "Thanks"));
                ChatLog.Append(folder, history, now.AddMinutes(3));
                lines = File.ReadAllLines(file);
                if (lines.Length != 3 || !lines[2].Contains("Thanks")) throw new Exception("A trimmed history was logged wrong: " + string.Join(" | ", lines));
                string older = Path.Combine(folder, "2026-10-20.log");
                File.WriteAllText(older, "old");
                ChatLog.Prune(folder, now);
                if (File.Exists(older) || !File.Exists(file)) throw new Exception("Pruning did not drop only the logs older than a week.");
            }
            finally
            {
                ChatLog.Seen(null);
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS chat log: every message of a chat is written once to a day file in the chatlog folder (nothing from before the launch is repeated, a trimmed history still logs only the new lines) and logs older than a week are removed");
        }

        private void SmokeWanderInChat(List<string> checks)
        {
            if (VoiceChatBlocksWander(false, TalkState.Off, false) || VoiceChatBlocksWander(false, TalkState.Talking, true)) throw new Exception("Wandering was blocked with no chat running.");
            if (VoiceChatBlocksWander(true, TalkState.Off, false)) throw new Exception("An idle voice chat still blocks her from wandering.");
            if (!VoiceChatBlocksWander(true, TalkState.Thinking, false) || !VoiceChatBlocksWander(true, TalkState.Talking, false) || !VoiceChatBlocksWander(true, TalkState.Off, true)) throw new Exception("Wandering was allowed while she is thinking, talking or has a bubble up in a chat.");
            bool keptWander = preferences.Wander, keptChatting = chatting;
            double keptNext = nextWander;
            string integration = "the gate alone";
            try
            {
                preferences.Wander = true;
                chatting = false;
                nextWander = -1;
                CheckWander(clock.Elapsed.TotalSeconds, 0);
                bool armed = nextWander >= 0;
                chatting = true;
                nextWander = -1;
                CheckWander(clock.Elapsed.TotalSeconds, 0);
                if (armed && nextWander < 0) throw new Exception("With voice chat idle she was not allowed to wander although she is without a chat.");
                if (armed) integration = "the real wander check too";
            }
            finally
            {
                preferences.Wander = keptWander;
                chatting = keptChatting;
                nextWander = keptNext;
            }
            checks.Add("PASS movement: an idle voice chat no longer stops her from wandering (voice chat now starts by itself, which kept her still), while thinking, talking or a bubble up still do (" + integration + ")");
        }
    }
}
