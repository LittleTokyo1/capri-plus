using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const string SmokeSchoolDraft = "[{\"id\":\"m9\",\"to\":\"Ms. Hollis\",\"cc\":\"\",\"subject\":\"Dutch level 2\",\"body\":\"Dear Ms. Hollis, how do I register for Dutch level 2?\",\"status\":\"proposed\",\"note\":\"\"}]";
        private const string SmokeTaskXml = "<Task><Settings><Enabled>true</Enabled></Settings><Triggers><TimeTrigger><Repetition><Interval>PT2H</Interval></Repetition><StartBoundary>2026-10-27T08:00:00</StartBoundary></TimeTrigger></Triggers></Task>";

        private void SmokeSchoolMail(List<string> checks)
        {
            string keptFolder = Study.Folder, keptLog = ActivityLog.FilePath, keptFile = SchoolMail.FilePath, keptAsks = OpenAsks.FilePath, keptHeard = heard;
            Func<string, string[], string> keptCli = ClaudeJob.Cli;
            Action<int> keptWait = ClaudeJob.Wait;
            Func<DateTime, bool> keptBusy = SchoolTasksBusy;
            TalkState keptTalk = talkState;
            string folder = Path.Combine(Path.GetTempPath(), "capri-school-mail-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                SchoolMail.FilePath = Path.Combine(folder, "school-mail.txt");
                OpenAsks.FilePath = Path.Combine(folder, "open-asks.txt");
                ClaudeJob.Wait = delegate { };
                Study.Folder = Path.Combine(folder, "Study-Assistant");
                SmokeStudyFiles(Path.Combine(Study.Folder, "tracker"));
                Directory.CreateDirectory(Path.Combine(Study.Folder, "routine"));
                File.WriteAllText(Path.Combine(Study.Folder, "routine", "process-drafts.md"), "# Drafts\nDrafts page (database): " + SmokeDraftsPage + " - collection `drafts`.");
                SmokeSchoolWords();
                SmokeSchoolSend();
                SchoolMailTool.Start = SchoolToolStart;
                SmokeSchoolSendQueue(checks);
                SmokeSchoolWatch();
                checks.Add("PASS school mail by voice: \"email X about Y\" and a reply to the lecturer who wrote are written by an Opus job onto his drafts page and read back; only \"send it\" within 3 minutes sends that one draft (a bare yes, a changed draft, another draft, a game or an expired ask never send), an unclear result is never retried; \"register me for X\" fills the school page and stops before the final button; Discord and drafts asks stay out");
                checks.Add("PASS school mail watch: every 2 hours from 08:00 to 22:00, out of his own study tasks' way, a read-only Opus job lists new school mail (no send tool), each mail is told once, one that needs an answer can be answered with \"reply to her\"");
            }
            finally
            {
                Study.Folder = keptFolder;
                ActivityLog.FilePath = keptLog;
                SchoolMail.FilePath = keptFile;
                OpenAsks.FilePath = keptAsks;
                ClaudeJob.Cli = keptCli;
                ClaudeJob.Wait = keptWait;
                SchoolTasksBusy = keptBusy;
                schoolSend = null;
                SchoolQueueDrop();
                schoolToldId = null;
                schoolQueued = null;
                schoolLastMail = null;
                studyJobRunning = false;
                CloseTalk();
                talkState = keptTalk;
                heard = keptHeard;
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private static void SmokeSchoolWords()
        {
            System.Text.RegularExpressions.Match grades = SchoolMail.CheckAsk.Match("check my school emails about some grades I got");
            if (!grades.Success || grades.Groups["topic"].Value != "some grades I got" || !SchoolMail.CheckAsk.IsMatch("check my school mail") || !SchoolMail.CheckAsk.IsMatch("can you look through my uni inbox for the exam results") || SchoolMail.CheckAsk.IsMatch("check my mail") || SchoolMail.CheckAsk.IsMatch("check my gmail"))
                throw new Exception("Checking his school mail on his ask (with a topic) was read wrong.");
            string search = SchoolMail.WatchPrompt(new DateTime(2026, 10, 29), new DateTime(2026, 11, 5), "grades");
            if (search.IndexOf("Search the mailbox", StringComparison.Ordinal) < 0 || search.IndexOf("grades", StringComparison.Ordinal) < 0 || SchoolMail.WatchPrompt(new DateTime(2026, 11, 5), new DateTime(2026, 11, 5), null).IndexOf("Search the mailbox", StringComparison.Ordinal) >= 0)
                throw new Exception("A school mail search for a topic did not ask Outlook for it.");
            if (search.IndexOf("never open a second Outlook tab", StringComparison.Ordinal) < 0 || SchoolMail.RegisterPrompt("Dutch level 2", new DateTime(2026, 11, 5)).IndexOf("leave it open", StringComparison.OrdinalIgnoreCase) < 0 && SchoolMail.RegisterPrompt("Dutch level 2", new DateTime(2026, 11, 5)).IndexOf("Leave that tab open", StringComparison.Ordinal) < 0)
                throw new Exception("A school mail job could open another Outlook tab every time and leave it open (his Chrome had ten).");
            string what;
            if (SchoolMail.Kind("email the Dutch lecturers asking how to register", null, out what) != "compose" || what != "the Dutch lecturers asking how to register"
                || SchoolMail.Kind("can you write an email to Ms Hollis about the Dutch course", null, out what) != "compose"
                || SchoolMail.Kind("send a message to Rudce on Discord", null, out what) != null
                || SchoolMail.Kind("reply to Rudce he's a gonk", null, out what) != null
                || SchoolMail.Kind("reply to her saying thanks", "Ms. Hollis", out what) != "reply"
                || SchoolMail.Kind("reply to Hollis, say I'll register tomorrow", "Ms. Hollis", out what) != "reply"
                || SchoolMail.Kind("reply to the lecturer saying thanks", null, out what) != "reply"
                || SchoolMail.Kind("register me for Dutch level 2", null, out what) != "register" || what != "Dutch level 2"
                || SchoolMail.Kind("sign me in to Discord", null, out what) != null
                || SchoolMail.Kind("any school mail?", null, out what) != null
                || SchoolMail.Kind("read me the email drafts", null, out what) != null
                || SchoolMail.Kind("email drafts waiting?", null, out what) != null
                || SchoolMail.Kind("okay, can you draft an email reply? Read it out to me and then I will confirm it that everything is all right and then you can send it after my confirmation.", null, out what) != "reply"
                || SchoolMail.Kind("okay can you write an email to Mr Quill about the Dutch course", null, out what) != "compose"
                || SchoolMail.Kind("draft a reply to her", "Ms. Quill", out what) != "reply"
                || SchoolMail.Kind("draft a reply to Casey on Discord", null, out what) != null
                || SchoolMail.Kind("draft a reply to Casey", null, out what) != null
                || SchoolMail.Kind("what drafts do I have", null, out what) != null)
                throw new Exception("A school mail ask was read wrong, or a Discord or drafts ask was taken as one.");
            if (SchoolMail.ComposePrompt("https://example.invalid/drafts", "draft an email reply", null, "him: what did she answer\nher: She copied the coordinators", new DateTime(2026, 11, 5)).IndexOf("She copied the coordinators", StringComparison.Ordinal) < 0)
                throw new Exception("A reply drafted with no mail she told about does not get the talk just before.");
            if (!SchoolMail.SendIt.IsMatch("send it")|| !SchoolMail.SendIt.IsMatch("okay, send it now") || SchoolMail.SendIt.IsMatch("yes") || SchoolMail.SendIt.IsMatch("send it to Rudce") || !SchoolMail.BareYes.IsMatch("yes") || !SchoolMail.Leave.IsMatch("leave it as a draft"))
                throw new Exception("Send it, a bare yes or leave it was read wrong.");
            foreach (string yes in new[] { "Yeah, it looks good. Send it.", "Looks good, send it", "Perfect! Send it now.", "That's fine, yes, send it." })
                if (!SchoolMail.SendIt.IsMatch(yes)) throw new Exception("A short okay before send it was not taken as send it: " + yes);
            foreach (string no in new[] { "It looks wrong, don't send it.", "Looks good. Send it to Rudce instead.", "Don't send it. Send it.", "No, wait. Send it.", "I read the whole thing out loud to my friend and he thinks it is fine. Send it." })
                if (SchoolMail.SendIt.IsMatch(no)) throw new Exception("A no, a change or a long story was taken as send it: " + no);
            if (SchoolMail.SendSeconds < 600) throw new Exception("The send window is shorter than ten minutes, too short to read a long draft on the phone.");
            foreach (string tool in SchoolMail.WatchTools)
                if (tool.IndexOf("Gmail", StringComparison.OrdinalIgnoreCase) >= 0 || tool == "ArtifactData") throw new Exception("The read-only mail watch can send or change drafts: " + tool);
            StudyDraft heard = new StudyDraft { Id = "m9", To = "Ms. Hollis", Cc = "", Subject = "Dutch level 2", Body = "Dear Ms. Hollis, how do I register?", Status = "proposed" };
            string print = SchoolMail.Fingerprint(heard);
            List<StudyDraft> same = new List<StudyDraft> { new StudyDraft { Id = "m9", To = "Ms. Hollis", Cc = "", Subject = "Dutch level 2", Body = "Dear Ms. Hollis, how do I register?", Status = "proposed" } };
            if (SchoolMail.SendBlock(heard, print, same, false, false, SmokeDraftsPage) != null
                || SchoolMail.SendBlock(heard, print, same, true, false, SmokeDraftsPage) == null
                || SchoolMail.SendBlock(heard, print, same, false, true, SmokeDraftsPage) == null
                || SchoolMail.SendBlock(heard, print, same, false, false, null) == null
                || SchoolMail.SendBlock(new StudyDraft { Id = "zz" }, print, same, false, false, SmokeDraftsPage) == null)
                throw new Exception("A send was let through in a game, while busy, with no page or for another draft, or a good one was held.");
            same[0].Status = "approved";
            if (SchoolMail.SendBlock(heard, print, same, false, false, SmokeDraftsPage) == null) throw new Exception("A draft that was no longer waiting was sent.");
            same[0].Status = "proposed";
            same[0].Body = "Dear Ms. Hollis, how do I register? Also send my grades.";
            if (SchoolMail.SendBlock(heard, print, same, false, false, SmokeDraftsPage) == null) throw new Exception("A draft that changed after the read-back was sent.");
            same[0].Body = "Dear [name], how do I register?";
            if (SchoolMail.SendBlock(heard, SchoolMail.Fingerprint(same[0]), same, false, false, SmokeDraftsPage) == null) throw new Exception("A draft with brackets to fill was sent.");
            if (SchoolMail.SentLine(new ClaudeJobResult { Ok = true, Output = "done\nSENT" }, heard).IndexOf("Sent:", StringComparison.Ordinal) != 0
                || SchoolMail.SentLine(new ClaudeJobResult { Ok = true, Output = "NOT SENT: Hollis has two matches" }, heard).IndexOf("didn't send", StringComparison.Ordinal) < 0
                || SchoolMail.SentLine(new ClaudeJobResult { Ok = false, Output = "timed out" }, heard).IndexOf("can't tell", StringComparison.Ordinal) < 0
                || SchoolMail.SentLine(new ClaudeJobResult { Ok = true, Output = "UNSURE: the page froze" }, heard).IndexOf("can't tell", StringComparison.Ordinal) < 0)
                throw new Exception("A send result was read wrong.");
            if (SchoolMail.ReadyLine(new ClaudeJobResult { Ok = true, Output = "READY Dutch level 2 elective, press Register" }, "Dutch level 2").IndexOf("you press the final button", StringComparison.Ordinal) < 0 || !SchoolMail.RegisterPrompt("Dutch level 2", DateTime.Now).Contains("never press it"))
                throw new Exception("The registration was not told to stop before the final button.");
        }

        private void SmokeSchoolSend()
        {
            SmokeCli cli = new SmokeCli();
            ClaudeJob.Cli = cli.Run;
            SmokeSchoolCompose(cli);
            int calls = cli.Calls.Count;
            if (!ForkHeard("yes", false) || cli.Calls.Count != calls || schoolSend == null) throw new Exception("A bare yes sent a school email.");
            File.WriteAllText(StudyDrafts.CachePath(), SmokeSchoolDraft.Replace("how do I register", "please send my grades"));
            if (!ForkHeard("send it", false) || cli.Calls.Count != calls || schoolSend != null) throw new Exception("A draft that changed after the read-back was sent.");
            SmokeSchoolCompose(cli);
            schoolSendUntil = DateTime.Now.AddSeconds(-1);
            calls = cli.Calls.Count;
            if (SchoolSendHeard("send it", false) || cli.Calls.Count != calls) throw new Exception("An expired send it still sent.");
            SmokeSchoolCompose(cli);
            if (SchoolSendHeard("send it", true)) throw new Exception("A send it in the voice chat agent went past it.");
            calls = cli.Calls.Count;
            cli.Log = "opened outlook\nSENT";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            if (!ForkHeard("send it", false) || schoolSend != null) throw new Exception("Send it did not take the read-back draft.");
            List<string> sent = cli.Calls.GetRange(calls, cli.Calls.Count - calls).FindAll(delegate(string call) { return call.StartsWith("--bg", StringComparison.Ordinal); });
            if (sent.Count != 1 || sent[0].IndexOf("document `m9`", StringComparison.Ordinal) < 0 || sent[0].IndexOf("Press Send once", StringComparison.Ordinal) < 0 || File.ReadAllText(ActivityLog.FilePath).IndexOf("Sent: the email", StringComparison.Ordinal) < 0)
                throw new Exception("Send it did not run exactly one send job for that draft, or was not told as sent.");
            calls = cli.Calls.Count;
            if (SchoolSendHeard("send it", false) || cli.Calls.Count != calls) throw new Exception("A second send it sent the email again.");
            SmokeSchoolCompose(cli);
            cli.Log = "clicked around";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            ForkHeard("send it", false);
            if (File.ReadAllText(ActivityLog.FilePath).IndexOf("I can't tell whether the email", StringComparison.Ordinal) < 0) throw new Exception("An unclear send was not told as unclear.");
            SmokeSchoolCompose(cli);
            string firstPrint = schoolSendPrint;
            cli.WriteCache = SmokeSchoolDraft.Replace("how do I register for Dutch level 2?", "how do I register for Dutch level 2? I am free on Friday.");
            cli.Log = "changed\nDRAFTED m9";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            calls = cli.Calls.Count;
            if (!ForkHeard("no, change it to say I'm free on Friday", false) || schoolSend == null || schoolSendPrint == firstPrint) throw new Exception("A change asked by voice was not made and read back, or the old read-back could still be sent.");
            string edited = null;
            for (int i = calls; i < cli.Calls.Count; i++) if (cli.Calls[i].StartsWith("--bg", StringComparison.Ordinal)) { edited = cli.Calls[i]; break; }
            if (edited == null || edited.IndexOf("Never change its status", StringComparison.Ordinal) < 0 || edited.IndexOf("document `m9`", StringComparison.Ordinal) < 0) throw new Exception("The change job could touch the status or another draft.");
            cli.WriteCache = null;
            if (!ForkHeard("leave it as a draft", false) || schoolSend != null) throw new Exception("Leave it did not keep it a draft.");
        }

        private void SmokeSchoolCompose(SmokeCli cli)
        {
            cli.WriteCache = SmokeSchoolDraft;
            cli.Log = "drafting...\nDRAFTED m9";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            int calls = cli.Calls.Count;
            if (!ForkHeard("email Ms Hollis asking how to register for Dutch level 2", false) || schoolSend == null || schoolSend.Id != "m9") throw new Exception("An email asked by voice was not written and read back.");
            string asked = null;
            for (int i = calls; i < cli.Calls.Count; i++) if (cli.Calls[i].StartsWith("--bg", StringComparison.Ordinal)) { asked = cli.Calls[i]; break; }
            if (asked == null || asked.IndexOf("CAPRI-HANDOFF.md", StringComparison.Ordinal) < 0 || asked.IndexOf("\"status\\\": \\\"proposed\\\"", StringComparison.Ordinal) < 0 && asked.IndexOf("\"status\": \"proposed\"", StringComparison.Ordinal) < 0 || asked.IndexOf("--chrome", StringComparison.Ordinal) < 0)
                throw new Exception("The writing job did not read his study situation, keep the draft proposed or use his Chrome: " + (asked == null ? "no job" : Pursuit.Shorter(asked, 200)));
            cli.WriteCache = null;
        }

        private void SmokeSchoolWatch()
        {
            if (!SchoolMail.NearTask(SmokeTaskXml, new DateTime(2026, 11, 4, 10, 10, 0)) || SchoolMail.NearTask(SmokeTaskXml, new DateTime(2026, 11, 4, 11, 0, 0)) || !SchoolMail.NearTask(SmokeTaskXml, new DateTime(2026, 11, 4, 9, 57, 0)) || SchoolMail.NearTask(SmokeTaskXml.Replace("true", "false"), new DateTime(2026, 11, 4, 10, 10, 0)))
                throw new Exception("His own study task times were read wrong.");
            if (SchoolMail.Due(new DateTime(2026, 11, 4, 23, 30, 0), DateTime.MinValue) || SchoolMail.Due(new DateTime(2026, 11, 4, 10, 0, 0), new DateTime(2026, 11, 4, 9, 0, 0)) || !SchoolMail.Due(new DateTime(2026, 11, 4, 11, 0, 0), new DateTime(2026, 11, 4, 9, 0, 0)))
                throw new Exception("The school mail watch was due at the wrong time.");
            SmokeCli cli = new SmokeCli();
            ClaudeJob.Cli = cli.Run;
            SchoolTasksBusy = delegate { return false; };
            File.WriteAllText(Path.Combine(Study.Folder, "tracker", SchoolMail.MailName), "[{\"key\":\"2026-11-04T09:12|Ms. Hollis|Dutch level 2\",\"from\":\"Ms. Hollis\",\"subject\":\"Dutch level 2\",\"received\":\"2026-11-04T09:12\",\"summary\":\"You can register through the form by Friday\",\"needs_reply\":true},{\"key\":\"2026-11-04T09:40|CoursePortal|Weekly digest\",\"from\":\"CoursePortal\",\"subject\":\"Weekly digest\",\"received\":\"2026-11-04T09:40\",\"summary\":\"New announcements in two courses\",\"needs_reply\":false}]");
            SchoolMail.Save(new DateTime(2026, 11, 4, 8, 0, 0), new List<string>());
            cli.Log = "MAIL 2";
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            schoolTickAt = DateTime.MinValue;
            SchoolMailTurn(new DateTime(2026, 11, 4, 10, 30, 0));
            string log = File.ReadAllText(ActivityLog.FilePath);
            if (log.IndexOf("school mail check  ->  2 new", StringComparison.Ordinal) < 0 || log.IndexOf("School mail from Ms. Hollis", StringComparison.Ordinal) < 0 || log.IndexOf("Want me to answer it?", StringComparison.Ordinal) < 0 || schoolLastMail == null || schoolLastMail.From != "Ms. Hollis")
                throw new Exception("New school mail was not told, or the one to answer was not kept: " + log.Substring(Math.Max(0, log.Length - 700)));
            string watched = cli.Calls.Find(delegate(string call) { return call.StartsWith("--bg", StringComparison.Ordinal); });
            if (watched == null || watched.IndexOf("Never send, reply, draft, move, delete, flag or mark anything", StringComparison.Ordinal) < 0 || watched.IndexOf("Gmail", StringComparison.Ordinal) >= 0) throw new Exception("The mail watch job was not read-only.");
            cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
            schoolTickAt = DateTime.MinValue;
            SchoolMailTurn(new DateTime(2026, 11, 4, 12, 40, 0));
            if (File.ReadAllText(ActivityLog.FilePath).IndexOf("school mail check  ->  0 new", StringComparison.Ordinal) < 0) throw new Exception("A school mail was told twice.");
            string what;
            if (SchoolMail.Kind("reply to her, say thanks and I'll do it tomorrow", SchoolSender(), out what) != "reply") throw new Exception("A reply to the lecturer she just told about was not taken.");
            SchoolTasksBusy = delegate { return true; };
            int calls = cli.Calls.Count;
            SchoolMail.Save(new DateTime(2026, 11, 4, 8, 0, 0), new List<string>());
            schoolTickAt = DateTime.MinValue;
            SchoolMailTurn(new DateTime(2026, 11, 4, 14, 5, 0));
            if (cli.Calls.Count != calls) throw new Exception("The mail watch ran while his own study task was running.");
        }
    }
}
