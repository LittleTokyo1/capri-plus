using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const string SmokeGreenLight = "okay green light on both emails, for school, go ahead and send both";

        private static string SmokeSendDrafts(string quill, string coord, string thanks)
        {
            return "[{\"id\":\"2026-10-20-quill-grades\",\"to\":\"Mr. Quill\",\"cc\":\"\",\"subject\":\"Re: Grade list\",\"body\":\"Dear Mr. Quill, thank you for the list.\",\"status\":\"" + quill + "\",\"note\":\"\"}," +
                "{\"id\":\"2026-10-26-marsh-resit\",\"to\":\"Ms. Marsh\",\"cc\":\"\",\"subject\":\"Re: Feedback on the resit\",\"body\":\"Dear Ms. Marsh, could I get feedback?\",\"status\":\"sent\",\"note\":\"\"}," +
                "{\"id\":\"2026-11-05-hollis-dutch\",\"to\":\"Coordinators; Pat; Sam\",\"cc\":\"\",\"subject\":\"RE: Dutch level 2 sign-up\",\"body\":\"Dear all, following up on Dutch level 2.\",\"status\":\"proposed\",\"note\":\"\"}," +
                "{\"id\":\"2026-11-06-coord-dutch\",\"to\":\"Coordinators\",\"cc\":\"Ms. Hollis\",\"subject\":\"RE: Dutch level 2 sign-up\",\"body\":\"Dear coordinators, could you register me for Dutch level 2?\",\"status\":\"" + coord + "\",\"note\":\"Written by Capri by voice. Recipients: the coordinators register students.\"}," +
                "{\"id\":\"2026-11-06-marsh-thanks\",\"to\":\"Ms. Marsh\",\"cc\":\"\",\"subject\":\"RE: Feedback on the resit\",\"body\":\"Dear Ms. Marsh, thank you for the feedback.\",\"status\":\"" + thanks + "\",\"note\":\"\"}]";
        }

        private static List<StudyDraft> SmokeSendParsed(string json)
        {
            return new List<StudyDraft>(League.Parse<StudyDraft[]>(json));
        }

        private static void SmokeSendWords()
        {
            if (!TerminalGuard.Holds("Yeah, it looks good. Send it.", "WindowsTerminal") || !TerminalGuard.Holds("yes", "claude") || TerminalGuard.Holds("Yeah, it looks good. Send it.", "Discord") || TerminalGuard.Holds("answer Claude Code: yes", "WindowsTerminal") || TerminalGuard.Holds("open spotify and play Blinding Lights", "WindowsTerminal") || TerminalGuard.Holds("send it to Casey on Discord", "WindowsTerminal"))
                throw new Exception("A bare yes was typed into his Claude Code terminal (14:57), or a real job was held because a terminal was in front.");
            List<StudyDraft> all = SmokeSendParsed(SmokeSendDrafts("proposed", "proposed", "proposed"));
            List<StudyDraft> waiting = StudyDrafts.Waiting(all);
            if (SchoolSendQueue.Wanted(SmokeGreenLight, false, waiting) != "both" || SchoolSendQueue.Wanted("send both", false, waiting) != null || SchoolSendQueue.Wanted("send both", true, waiting) != "both")
                throw new Exception("\"Send both\" for his school drafts was read wrong (15:57 went to the write tool).");
            if (SchoolSendQueue.Wanted("send them both to Casey on Discord", true, waiting) != null || SchoolSendQueue.Wanted("don't send both emails yet", true, waiting) != null || SchoolSendQueue.Wanted("send the funny one to Casey", false, waiting) != null)
                throw new Exception("A Discord send, a no or another send was taken as sending school drafts.");
            if (SchoolSendQueue.Wanted("send the Dutch one", false, waiting) != "named" || SchoolSendQueue.Wanted("Yeah, it looks good. Send it.", true, waiting) != "last" || SchoolSendQueue.Wanted("Yeah, it looks good. Send it.", false, waiting) != null)
                throw new Exception("Send the Dutch one, or send it right after a read-back, was not taken.");
            List<StudyDraft> older;
            string ask;
            List<StudyDraft> picked = SchoolSendQueue.Pick(all, "both", SmokeGreenLight, null, out older, out ask);
            if (picked.Count != 0 || ask == null || ask.IndexOf("3 drafts are waiting", StringComparison.Ordinal) < 0 || ask.IndexOf("Pat", StringComparison.Ordinal) >= 0)
                throw new Exception("With three drafts waiting, both was guessed, or the older reply-all draft was offered: " + ask);
            picked = SchoolSendQueue.Pick(all, "named", "send the Dutch one and the thanks one", null, out older, out ask);
            string flag = SchoolSendQueue.Flag(older, picked);
            if (ask != null || picked.Count != 2 || picked[0].Id != "2026-11-06-coord-dutch" || picked[1].Id != "2026-11-06-marsh-thanks" || flag.IndexOf("older draft on the same thread", StringComparison.Ordinal) < 0 || flag.IndexOf("Pat", StringComparison.Ordinal) < 0)
                throw new Exception("Two named drafts were not picked, or the older one on the same thread was not left out and told: " + ask + " / " + flag);
            const string hisWords = "send registration dutch level 2 email and send reauest for feedback email";
            picked = SchoolSendQueue.Pick(all, SchoolSendQueue.Wanted(hisWords, false, waiting) ?? "none", hisWords, null, out older, out ask);
            if (SchoolSendQueue.Wanted(hisWords, false, waiting) != "named" || ask != null || picked.Count != 2 || picked[0].Id != "2026-11-06-coord-dutch" || picked[1].Id != "2026-11-06-marsh-thanks" || !SchoolMailTool.FromTask(hisWords).Action.Equals("send"))
                throw new Exception("His 18:15 answer naming both drafts without \"the\" (and a typo) was not taken as those two: " + ask);
            if (SchoolSendQueue.Wanted("send him an email", false, waiting) != null || SchoolSendQueue.Wanted("send mum a mail about dinner", false, waiting) != null || SchoolSendQueue.Sends("send Ms Hollis an email about the grades") && SchoolSendQueue.Kind("send Ms Hollis an email about the grades") != null)
                throw new Exception("A new email ask was taken as sending a waiting draft.");
            picked = SchoolSendQueue.Pick(SmokeSendParsed(SmokeSendDrafts("sent", "proposed", "proposed")), "both", SmokeGreenLight, null, out older, out ask);
            if (ask != null || picked.Count != 2 || picked.Exists(delegate(StudyDraft one) { return one.Id == "2026-11-05-hollis-dutch"; }))
                throw new Exception("Both did not take the two newest drafts, or took the older reply-all one.");
            SchoolSendQueue.Pick(SmokeSendParsed(SmokeSendDrafts("sent", "sent", "sent").Replace("\"proposed\"", "\"sent\"")), "both", SmokeGreenLight, null, out older, out ask);
            if (ask == null || ask.IndexOf("No drafts are waiting", StringComparison.Ordinal) < 0) throw new Exception("With no drafts waiting she did not say so: " + ask);
            if (SchoolMailTool.FromTask("In Outlook on the web (school mail), open the Drafts folder and send these two drafts, as they are written, without changing them").Action != "send"
                || SchoolMailTool.FromTask("write an email in Outlook to the IB coordinators about Dutch level 2").Action != "write"
                || SchoolMailTool.FromTask("Draft an email to Ms Hollis and send it after I confirm").Action == "send")
                throw new Exception("A send of his waiting drafts became a write job, or a write ask became a send.");
            foreach (string tonight in new[] { "Send the Dutch level 2 registration draft to the IB coordinators with Ms. Hollis in CC, and send the feedback thank-you draft", "Send the Dutch draft and the feedback thank-you draft from my school Outlook in Chrome", "send dutch draft on outlook" })
                if (!SchoolSendQueue.HandsSend(tonight, false)) throw new Exception("A send of his waiting drafts went to her hands and opened Gmail (2026-11-07 21:13): " + tonight);
            foreach (string writing in new[] { "Draft an email to Ms Hollis and send it after I confirm", "write a reply to the coordinators and send it", "can you draft a mail to Ms Marsh and send it" })
                if (SchoolSendQueue.HandsSend(writing, false)) throw new Exception("A write-and-send ask was taken as sending a waiting draft: " + writing);
            if (!Hands.Valid(SchoolMailTool.Tool, new ToolArguments { Action = "send" })) throw new Exception("Her brain's own check refuses the school_mail send action.");
            Func<string, string, string, string> kept = SchoolMailTool.Start;
            try
            {
                List<string> started = new List<string>();
                SchoolMailTool.Start = delegate(string action, string text, string topic) { started.Add(action); return "ok"; };
                SchoolMailTool.Do(SchoolMailTool.Tool, new ToolArguments { Action = "write", Text = "Send both school emails from the drafts", Asked = SmokeGreenLight });
                if (started.Count != 1 || started[0] != "send") throw new Exception("Her brain's write call for his \"send both\" still starts a write job.");
            }
            finally { SchoolMailTool.Start = kept; }
        }

        private static int SmokeSendJobs(SmokeCli cli, int from)
        {
            int count = 0;
            for (int i = from; i < cli.Calls.Count; i++) if (cli.Calls[i].StartsWith("--bg", StringComparison.Ordinal) && cli.Calls[i].IndexOf("Press Send once", StringComparison.Ordinal) >= 0) count++;
            return count;
        }

        private static string SmokeLastSend(SmokeCli cli)
        {
            return cli.Calls.FindLast(delegate(string call) { return call.StartsWith("--bg", StringComparison.Ordinal) && call.IndexOf("Press Send once", StringComparison.Ordinal) >= 0; }) ?? "";
        }

        private void SmokeSendArmBoth(SmokeCli cli)
        {
            string both = SmokeSendDrafts("sent", "proposed", "proposed");
            File.WriteAllText(StudyDrafts.CachePath(), both);
            cli.WriteCache = both;
            cli.Log = "DRAFTS 5";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            int calls = cli.Calls.Count;
            if (!ForkHeard(SmokeGreenLight, false) || schoolSend == null || schoolSend.Id != "2026-11-06-coord-dutch" || schoolSendQueue.Count != 1 || SmokeSendJobs(cli, calls) != 0)
                throw new Exception("\"Send both\" did not read the first draft back and wait, or sent something on its own.");
            string refresh = null;
            for (int i = calls; i < cli.Calls.Count; i++) if (cli.Calls[i].StartsWith("--bg", StringComparison.Ordinal) && cli.Calls[i].IndexOf("change no draft", StringComparison.Ordinal) >= 0) refresh = cli.Calls[i];
            if (refresh == null) throw new Exception("\"Send both\" did not read his drafts page first.");
        }

        private void SmokeSchoolSendQueue(List<string> checks)
        {
            SmokeSendWords();
            SmokeCli cli = new SmokeCli();
            ClaudeJob.Cli = cli.Run;
            schoolSend = null;
            schoolToldId = null;
            SmokeSendArmBoth(cli);
            string log = File.ReadAllText(ActivityLog.FilePath);
            if (log.IndexOf("Two drafts to send, one at a time", StringComparison.Ordinal) < 0 || log.IndexOf("older draft on the same thread", StringComparison.Ordinal) < 0)
                throw new Exception("The two drafts were not told one at a time, or the older draft on the same thread was not told.");
            int calls = cli.Calls.Count;
            cli.WriteCache = SmokeSendDrafts("sent", "sent", "proposed");
            cli.Log = "opened outlook\nSENT";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            if (!ForkHeard("send it", false) || SmokeSendJobs(cli, calls) != 1 || schoolSend == null || schoolSend.Id != "2026-11-06-marsh-thanks" || schoolSendQueue.Count != 0)
                throw new Exception("Send it did not send exactly the first draft and then read the second back to wait for his word.");
            if (SmokeLastSend(cli).IndexOf("document `2026-11-06-coord-dutch`", StringComparison.Ordinal) < 0) throw new Exception("The first send job was for another draft.");
            if (File.ReadAllText(ActivityLog.FilePath).IndexOf("Next: Draft to Ms. Marsh", StringComparison.Ordinal) < 0) throw new Exception("The second draft was not read back after the first went out.");
            calls = cli.Calls.Count;
            cli.WriteCache = SmokeSendDrafts("sent", "sent", "sent");
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            if (!ForkHeard("send it", false) || SmokeSendJobs(cli, calls) != 1 || schoolSend != null || SmokeLastSend(cli).IndexOf("document `2026-11-06-marsh-thanks`", StringComparison.Ordinal) < 0)
                throw new Exception("His second send it did not send the second draft once.");
            cli.WriteCache = null;
            SmokeSendArmBoth(cli);
            calls = cli.Calls.Count;
            if (!ForkHeard("leave it", false) || schoolSend == null || schoolSend.Id != "2026-11-06-marsh-thanks" || !ForkHeard("leave it", false) || schoolSend != null || SmokeSendJobs(cli, calls) != 0)
                throw new Exception("Leave it did not keep that draft and go on to the next one, or something was sent.");
            SmokeSendArmBoth(cli);
            cli.WriteCache = null;
            cli.Log = "clicked around";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            ForkHeard("send it", false);
            if (schoolSend != null || schoolSendQueue.Count != 0 || File.ReadAllText(ActivityLog.FilePath).IndexOf("I stopped there", StringComparison.Ordinal) < 0)
                throw new Exception("After a send she couldn't confirm, the next draft was still offered.");
            File.WriteAllText(StudyDrafts.CachePath(), SmokeSendDrafts("sent", "proposed", "proposed"));
            SchoolTold("2026-11-06-coord-dutch");
            cli.WriteCache = SmokeSendDrafts("sent", "proposed", "proposed");
            cli.Log = "DRAFTS 5";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            calls = cli.Calls.Count;
            string steered = ForkTool("use_computer", new ToolArguments { Task = "Yeah, it looks good. Send it", Asked = "Yeah, it looks good. Send it." });
            if (steered == null || steered.IndexOf("drafts page", StringComparison.Ordinal) < 0 || SmokeSendJobs(cli, calls) != 0 || schoolSend == null || schoolSend.Id != "2026-11-06-coord-dutch" || File.ReadAllText(ActivityLog.FilePath).IndexOf("school drafts go out only through the school_mail tool", StringComparison.Ordinal) < 0)
                throw new Exception("A send it given to her hands was not turned into the school send read-back (14:57 typed it into a terminal and told it as sent): " + steered);
            schoolSend = null;
            SchoolQueueDrop();
            schoolToldId = null;
            cli.WriteCache = null;
            checks.Add("PASS school drafts sent by voice: \"send both\", \"send the Dutch one\" or a send it handed to her hands reads his drafts page first, leaves out an older draft on the same thread and says so, asks which when more are waiting than he named, then reads each draft back and sends each one only on his own send it, one send job at a time; leave it goes to the next, an unclear send stops the rest, and Discord or a no never send; a bare yes or send it is never typed into his Claude Code terminal in front (14:57), while real jobs and a reply that names Claude Code still run");
        }
    }
}
