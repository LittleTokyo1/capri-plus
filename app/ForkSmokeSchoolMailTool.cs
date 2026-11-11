using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeSchoolMailTool(List<string> checks)
        {
            if (Understand.Prompt().IndexOf(SchoolMailTool.Tool, StringComparison.Ordinal) < 0) throw new Exception("Her brain and understanding step have no school mail tool, so a long or mid-sentence ask to draft a school email gets no tool.");
            if (StudyDrafts.Kind("There were some emails about Dutch levels, right? They said I messaged the wrong person again. Can you draft an email to the correct person and show me the draft") != null || StudyDrafts.Kind("can you draft up an email to the coordinator") != null)
                throw new Exception("An ask to write a school email was taken as a look at his drafts list.");
            if (StudyDrafts.Kind("what drafts are waiting") != "list" || StudyDrafts.Kind("read me the Quill draft") != "read") throw new Exception("A drafts list or read ask stopped working.");
            if (!Hands.Valid(SchoolMailTool.Tool, new ToolArguments { Action = "write" }) || !Hands.Valid(SchoolMailTool.Tool, new ToolArguments { Action = "check" }) || Hands.Valid(SchoolMailTool.Tool, new ToolArguments { Action = "delete" })) throw new Exception("Her brain's own check refuses a real school mail call or takes an unknown one.");
            string echoed = "A read-only check of his school mail. If Outlook or a school site shows a sign-in page, type nothing and say so. Reply with one line: MAIL <count>, or SIGN IN when Outlook wants him to sign in.";
            if (SchoolMail.SignIn(new ClaudeJobResult { Ok = true, Output = ClaudeJob.Unnamed("Casey — Outlook is signed in; I've run the first search.\nAlex — MAIL 2") }) || StudyRoutines.SignIn(new ClaudeJobResult { Ok = true, Output = "Casey — MAIL 2" }))
                throw new Exception("A mail check that read his mail (MAIL 2) was taken as a sign-in page (00:16).");
            if (!SchoolMail.SignIn(new ClaudeJobResult { Ok = true, Output = ClaudeJob.Unnamed("Casey — SIGN IN") })) throw new Exception("A real sign-in answer was missed.");
            string projects = Path.Combine(Path.GetTempPath(), "capri-answers-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(projects, "C--Study"));
                File.WriteAllText(Path.Combine(projects, "C--Study", "ab12cd34-0000.jsonl"), "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":" + new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(echoed) + "}}\n{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"thinking\",\"thinking\":\"\"},{\"type\":\"text\",\"text\":\"Outlook is signed in.\"}]}}\n{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"MAIL 2\"}]}}\n", new System.Text.UTF8Encoding(false));
                string answers = ClaudeJob.Answers(projects, "ab12cd34");
                if (answers != "Outlook is signed in.\nMAIL 2" || ClaudeJob.Answers(projects, "ffffffff") != null || ClaudeJob.Answers(projects, "ab12cd34", DateTime.Now.AddMinutes(5)) != null) throw new Exception("Her job's own answers are not read from its transcript (the echoed instructions made every check a sign-in): " + answers);
            }
            finally { try { Directory.Delete(projects, true); } catch (Exception) { } }
            if (!ClaudeJob.Idle("/idle") || !ClaudeJob.Idle("idle/") || ClaudeJob.Idle("/working") || ClaudeJob.Idle("/done") || ClaudeJob.Idle(null)) throw new Exception("A background job that finished but stays idle is not seen (the 14:33 mail check waited 20 minutes).");
            if (ClaudeJob.Unnamed("Casey, MAIL 0") != "MAIL 0" || ClaudeJob.Unnamed("x\nAlex, NOT DRAFTED: no thread") != "x\nNOT DRAFTED: no thread" || ClaudeJob.Unnamed("Sure, here it is") != "Sure, here it is") throw new Exception("A job answer that starts with his name before its marker is not read.");
            if (SchoolMail.WatchPrompt(new DateTime(2026, 10, 29), new DateTime(2026, 11, 5), "Dutch levels lecturer").IndexOf("fewer or other key words", StringComparison.Ordinal) < 0) throw new Exception("A school mail search with too many words gives up after one search.");
            if (Study.Kind("what did the school mails about the Dutch level say? check my school mail please") != null || Study.Kind("so about the dutch levels, what does the lectureres email say?") != null || Study.Kind("can you read me my school emails from this week") != null)
                throw new Exception("An ask to check or read school mail got this morning's briefing instead of a real check (15:33).");
            if (Study.Kind("any school mail?") != "briefing" || Study.Kind("my study briefing") != "briefing") throw new Exception("A plain ask for the study briefing stopped working.");
            string agents = "[{\"id\":\"aa\",\"cwd\":\"C:\\\\Study\",\"state\":\"working\"},{\"id\":\"bb\",\"cwd\":\"C:\\\\Study\",\"state\":\"blocked\"},{\"id\":\"cc\",\"cwd\":\"C:\\\\Other\",\"state\":\"working\"}]";
            if (!ClaudeJob.Working(agents, "C:\\Study") || ClaudeJob.Working(agents.Replace("\"working\"},{\"id\":\"bb\"", "\"done\"},{\"id\":\"bb\""), "C:\\Study") || ClaudeJob.Working(null, "C:\\Study"))
                throw new Exception("An asked mail check can't tell whether his study routine is really running (15:59 waited 26 minutes for a routine window).");
            if (!SchoolMail.TellSignIn(true, true) || SchoolMail.TellSignIn(true, false) || !SchoolMail.TellSignIn(false, false)) throw new Exception("A sign-in answer to his own ask was kept quiet because he was told once today (16:29).");
            if (Study.Kind("Use the School Mail tool.") != null) throw new Exception("Use the school mail tool got the morning briefing (17:17).");
            ToolArguments fromCheck = SchoolMailTool.FromTask("check my school mail for emails about Dutch, especially a recent reply from my lecturer about signing up for Dutch classes, and tell me what it says");
            ToolArguments fromDraft = SchoolMailTool.FromTask("Draft an email reply in my school mail to the Dutch level 2 lecturer about the Dutch level 2 sign-up");
            ToolArguments fromWrite = SchoolMailTool.FromTask("write an email in Outlook to the IB coordinators about Dutch level 2");
            if (fromCheck.Action != "check" || fromCheck.Search != "Dutch" || fromDraft.Action != "reply" || fromWrite.Action != "write" || fromWrite.Text.IndexOf("IB coordinators", StringComparison.Ordinal) < 0)
                throw new Exception("A school mail job her hands got is not turned into the right school_mail call: " + fromCheck.Action + " " + fromCheck.Search + " / " + fromDraft.Action + " / " + fromWrite.Action);
            string[][] topics =
            {
                new[] { "check my school mail for the Finance & Accounting Quiz 1 that closes at 12:30 today and tell me which class it is for", "Finance & Accounting Quiz 1" },
                new[] { "Check my school mail for the Finance and Accounting quiz email and tell me which class and which year's lecture it's for", "Finance and Accounting quiz" },
                new[] { "search your school mail for \"Finance & Accounting Quiz 1\" and open the matching email", "Finance & Accounting Quiz 1" },
                new[] { "check my school mail for the Dutch class registration email and tell me what it says", "Dutch class registration" }
            };
            foreach (string[] one in topics)
            {
                ToolArguments found = SchoolMailTool.FromTask(one[0]);
                if (found.Action != "check" || found.Search != one[1]) throw new Exception("A school mail question lost its topic, so the check only looks for new mail: \"" + one[0] + "\" gave \"" + found.Search + "\"");
            }
            string compose = SchoolMail.ComposePrompt("https://claude.ai/artifact/stand-in", "draft a reply to the lecturer about the Dutch sign-up", null, null, new DateTime(2026, 11, 6, 12, 0, 0));
            if (compose.IndexOf("only a hint, never a fact", StringComparison.Ordinal) < 0 || compose.IndexOf("school websites", StringComparison.Ordinal) < 0 || compose.IndexOf("drop the rest of a reply-all list", StringComparison.Ordinal) < 0 || compose.IndexOf("Recipients: <one short reason>", StringComparison.Ordinal) < 0) throw new Exception("The draft job is not told to work out the recipients itself, drop reply-all leftovers and give a reason.");
            string read = StudyDrafts.Reading(new StudyDraft { Id = "d1", To = "IB-coordinators", Cc = "Lecturer", Subject = "Dutch level 2", Body = "Dear IB coordinators, ...", Status = "proposed", Note = "Written by Capri by voice. Recipients: the IB coordinators handle registrations; the lecturer in cc." });
            if (read.IndexOf("the IB coordinators handle registrations", StringComparison.Ordinal) >= 0 || read.IndexOf("IB-coordinators", StringComparison.Ordinal) < 0 || read.IndexOf("cc Lecturer", StringComparison.Ordinal) < 0) throw new Exception("The read-back must name who it goes to and the cc, without the job's reasoning note (his pick 2026-11-11: it read 'which he used in 2024' aloud): " + read);
            ToolArguments ampersand = SchoolMailTool.FromTask("check my school mail for Marsh's email about the R&D report regrade and tell me what it says in full");
            if (ampersand.Search != "R&D report regrade") throw new Exception("A topic with an & in it was cut short: " + ampersand.Search);
            ToolArguments sendBack = SchoolMailTool.FromTask("check my school mail for the regrade and send me on Discord what it says");
            if (sendBack.Action != "check" || sendBack.Search != "regrade") throw new Exception("A check that asks for the answer on Discord was taken as writing an email: " + sendBack.Action + " " + sendBack.Search);
            if (SchoolMailTool.FromTask("Draft an email reply in my school mail to the lecturer").Action == "check") throw new Exception("A real draft ask became a check.");
            if (SchoolMailTool.FromTask("check my school mail").Search != null || SchoolMailTool.FromTask("Check my school mail and my school boxes, work out everything I need to reply to").Search != null) throw new Exception("A plain school mail check got a made-up topic.");
            MailRoute.Reset();
            if (MailRoute.JobSteer("Search my school mail for the lecturer's email about the Dutch levels and tell me what it says", true, new DateTime(2026, 11, 5, 14, 52, 0)) != MailRoute.SchoolSteer || MailRoute.Job("check my school mail inbox", true) || MailRoute.Rule(true).IndexOf(SchoolMailTool.Tool, StringComparison.Ordinal) < 0)
                throw new Exception("A school mail job was steered to Gmail and the google tool (14:52, 14:54).");
            if (MailRoute.JobSteer("read my latest emails in gmail", true, new DateTime(2026, 11, 5, 15, 30, 0)) != MailRoute.Steer) throw new Exception("A Gmail job is no longer steered to the google tool.");
            MailRoute.Reset();
            Func<string, string, string, string> kept = SchoolMailTool.Start;
            try
            {
                List<string> started = new List<string>();
                SchoolMailTool.Start = delegate(string action, string text, string topic) { started.Add(action + "|" + text + "|" + topic); return "Writing it now."; };
                string wrote = SchoolMailTool.Do(SchoolMailTool.Tool, new ToolArguments { Action = "write", Text = "to the right coordinator about Dutch level 2", Asked = "They said I messaged the wrong person. Can you draft an email to the correct person?" });
                string replied = SchoolMailTool.Do(SchoolMailTool.Tool, new ToolArguments { Action = "reply", Text = "thanks, I'll register tomorrow" });
                string checkedMail = SchoolMailTool.Do(SchoolMailTool.Tool, new ToolArguments { Action = "check", Search = "Dutch level" });
                string odd = SchoolMailTool.Do(SchoolMailTool.Tool, new ToolArguments { Action = "delete" });
                if (wrote != "Writing it now." || replied != "Writing it now." || checkedMail != "Writing it now." || started.Count != 3) throw new Exception("The school mail tool did not start its jobs: " + string.Join(" / ", started.ToArray()));
                if (started[0].IndexOf("write|", StringComparison.Ordinal) != 0 || started[0].IndexOf("They said I messaged the wrong person", StringComparison.Ordinal) < 0 || started[0].IndexOf("right coordinator", StringComparison.Ordinal) < 0) throw new Exception("A school mail write lost his own words: " + started[0]);
                if (started[1].IndexOf("reply|", StringComparison.Ordinal) != 0 || started[2] != "check||Dutch level") throw new Exception("A reply or a topic check was passed wrong: " + started[1] + " / " + started[2]);
                if (odd == null || odd.IndexOf("check, write, reply to or send", StringComparison.Ordinal) < 0 || started.Count != 3) throw new Exception("An unknown school mail action ran something.");
                if (SchoolMailTool.Do("discord", new ToolArguments { Action = "write" }) != null) throw new Exception("The school mail tool answered another tool's call.");
                SchoolMailTool.Start = null;
                if ((SchoolMailTool.Do(SchoolMailTool.Tool, new ToolArguments { Action = "write" }) ?? "").Length == 0) throw new Exception("With no school mail set up the tool said nothing.");
            }
            finally { SchoolMailTool.Start = kept; }
            checks.Add("PASS school mail tool: her brain and understanding step have a school_mail tool (check with a topic, write, reply), so any wording or a mid-sentence ask reaches the same draft and check jobs with his own words; an unknown action runs nothing; drafts still wait for his send it");
        }
    }
}
