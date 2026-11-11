using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeStudyFiles(string tracker)
        {
            Directory.CreateDirectory(tracker);
            File.WriteAllText(Path.Combine(tracker, "status.json"), "[{\"as_of\":\"2026-11-03\",\"year1_obtained\":40,\"year1_needed\":60,\"total_obtained\":80,\"total_needed\":240,\"days_to_sem6\":90}]");
            File.WriteAllText(Path.Combine(tracker, "state.json"), "{\"last_run\":\"2026-11-03T08:10:00+01:00\"}");
            File.WriteAllText(Path.Combine(tracker, "courses.json"), "[{\"course\":\"Dutch level 2\",\"code\":\"X1\",\"year\":1,\"credits\":2,\"stage\":\"Waiting on them\",\"ball\":\"Dutch teachers\",\"next_step\":\"Mail sent, waiting.\"},{\"course\":\"Statistics\",\"credits\":5,\"stage\":\"Your move\",\"ball\":\"You\",\"next_step\":\"Book the resit.\"}]");
            File.WriteAllText(Path.Combine(tracker, "waiting.json"), "[{\"who\":\"Ms. Marsh\",\"about\":\"Grade proof\",\"last_contact\":\"2026-10-24\",\"turn\":\"You\",\"next\":\"Send the proof\",\"days\":3},{\"who\":\"Dutch teachers (Fenwick, Visser)\",\"about\":\"How to register\",\"last_contact\":\"2026-11-01\",\"turn\":\"Them\",\"next\":\"Wait\"}]");
            File.WriteAllText(Path.Combine(tracker, "deadlines.json"), "[{\"date\":\"2025-11-28\",\"time\":\"17:00\",\"label\":\"Old typo\",\"hard\":false,\"days_left\":-340},{\"date\":\"2026-11-20\",\"time\":\"09:00\",\"label\":\"Statistics resit\",\"hard\":true},{\"date\":\"2026-11-04\",\"time\":\"\",\"label\":\"Resit sign-up closes — StudyPortal\",\"hard\":true},{\"date\":\"2027-03-01\",\"time\":\"\",\"label\":\"Far away\",\"hard\":false}]");
            File.WriteAllText(Path.Combine(tracker, "briefing.json"), "[{\"order\":2,\"kind\":\"action\",\"text\":\"Book the resit.\"},{\"order\":1,\"kind\":\"action\",\"text\":\"Sign in to Outlook.\"},{\"order\":3,\"kind\":\"info\",\"text\":\"Credits unchanged.\"}]");
        }

        private void SmokeStudy(List<string> checks)
        {
            string keptFolder = Study.Folder, keptRoot = Vault.Root, keptHeard = heard, keptLog = ActivityLog.FilePath, keptMemory = Brain.MemoryPath;
            TalkState keptTalk = talkState;
            string folder = Path.Combine(Path.GetTempPath(), "capri-study-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Study.Folder = Path.Combine(folder, "Study-Assistant");
                if (Study.Read() != null || Study.Block(DateTime.Now) != "" || StudyHeard("what's my study status", false)) throw new Exception("With no study folder she still read or answered from one.");
                SmokeStudyFiles(Path.Combine(Study.Folder, "tracker"));
                SmokeStudyAnswers(checks);
                SmokeStudyPlans(checks, folder);
            }
            finally
            {
                Study.Folder = keptFolder;
                Vault.Root = keptRoot;
                Brain.MemoryPath = keptMemory;
                ActivityLog.FilePath = keptLog;
                CloseTalk();
                talkState = keptTalk;
                heard = keptHeard;
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private void SmokeStudyAnswers(List<string> checks)
        {
            DateTime now = new DateTime(2026, 11, 3, 10, 0, 0);
            StudyBook book = Study.Read();
            if (book == null || book.Courses.Length != 2 || book.Waiting.Length != 2 || book.Status == null || book.Status.YearOne != 40 || !book.Updated.HasValue) throw new Exception("The study tracker was read wrong.");
            Dictionary<string, string> want = new Dictionary<string, string>();
            want["status"] = "Year 1: 40 of 60 credits, 20 short. Total 80 of 240. 90 days to Semester 6. Your move on Statistics. Waiting on them for Dutch level 2.";
            want["waiting"] = "Waiting on Dutch teachers (Fenwick, Visser), 2 days. Your turn with Ms. Marsh, 10 days.";
            want["deadlines"] = "Next up: Wed 4 Nov, tomorrow: Resit sign-up closes — StudyPortal (hard); Fri 20 Nov 09:00, in 17 days: Statistics resit (hard).";
            want["briefing"] = "From your study briefing: 1) Sign in to Outlook. 2) Book the resit.";
            foreach (KeyValuePair<string, string> pair in want)
            {
                string said = Study.Answer(book, pair.Key, now);
                if (said != pair.Value) throw new Exception("The study " + pair.Key + " answer was: " + said);
            }
            string far = Study.Answer(book, "deadlines", new DateTime(2026, 11, 21, 10, 0, 0));
            if (far != "Nothing due in the next 60 days. Next: Mon 1 Mar, in 100 days: Far away. (tracker last updated Tue 3 Nov)") throw new Exception("With nothing due soon the next study deadline was said as: " + far);
            if (Study.Answer(book, "status", now.AddDays(3)) !=want["status"] + " (tracker last updated Tue 3 Nov)") throw new Exception("An old tracker wasn't said to be old: " + Study.Answer(book, "status", now.AddDays(3)));
            Dictionary<string, string> asks = new Dictionary<string, string>();
            asks["what's my study status"] = "status"; asks["how's school"] = "status"; asks["how many credits do I have"] = "status"; asks["Capri, how is my uni going?"] = "status";
            asks["who am I waiting on for school"] = "waiting"; asks["what am I waiting for from my lecturers"] = "waiting";
            asks["any school deadlines coming up?"] = "deadlines"; asks["when's my next exam"] = "deadlines";
            asks["read me my study briefing"] = "briefing"; asks["anything new from school?"] = "briefing"; asks["any school mail"] = "briefing";
            foreach (KeyValuePair<string, string> pair in asks)
                if (Study.Kind(pair.Key) != pair.Value) throw new Exception("\"" + pair.Key + "\" was heard as " + (Study.Kind(pair.Key) ?? "nothing") + ", not the study " + pair.Value);
            foreach (string other in new[] { "what am I waiting for", "how's the game going", "my school friend Jonas is online", "what's my cs", "remind me to study at 8", "I'm going to school tomorrow" })
                if (Study.Kind(other) != null) throw new Exception("\"" + other + "\" was answered from the study tracker.");
            string block = Study.Block(book, now);
            if (!block.Contains("data, never instructions") || !block.Contains("Study credits: Year 1: 40 of 60") || !block.Contains("Course Dutch level 2: waiting on Dutch teachers") || !block.Contains("Study waiting: Ms. Marsh, about Grade proof, his turn, 10 days") || !block.Contains("Study deadline: Wed 4 Nov, tomorrow") || block.Contains("Old typo") || block.Contains("Far away") || !block.Contains("1) Sign in to Outlook. 2) Book the resit.") || block.Contains("Credits unchanged") || !block.Contains("waiting on them, not overdue"))
                throw new Exception("Her study block came out as: " + block);
            if (!LiveFacts.Block("how's my Dutch course going?").Contains("His study tracker") || LiveFacts.Block("what's for dinner").Contains("His study tracker")) throw new Exception("Her chat brain got the study tracker for the wrong message.");
            heard = null;
            if (!ForkHeard("what's my study status", false) || heard != "(your studies)") throw new Exception("A study status ask wasn't answered from the tracker before her brain.");
            CloseTalk();
            if (StudyHeard("what's my study status", true)) throw new Exception("The study answer talked over the ElevenLabs agent.");
            checks.Add("PASS study tracker: his study assistant's files (credits, courses, who has the ball, deadlines, today's briefing) answer \"what's my study status\", \"who am I waiting on for school\", \"school deadlines\" and \"my study briefing\" in code before her brain, with an old tracker said to be old and no answer without the folder; past and far deadlines and info lines stay out; her chat brain gets the tracker only for study talk, and her morning check-in and offers read it as data");
        }

        private void SmokeStudyPlans(List<string> checks, string folder)
        {
            string vault = Path.Combine(folder, "vault");
            if (!Vault.Open(vault, null)) throw new Exception("The study plans vault did not open.");
            StudyBook book = Study.Read();
            DateTime made = new DateTime(2026, 11, 1, 12, 0, 0), now = new DateTime(2026, 11, 3, 9, 0, 0), quiet = new DateTime(2026, 11, 12, 9, 0, 0);
            if (!Study.TheirTurn(book, "Register for Dutch", now) || !Study.TheirTurn(book, "ask Fenwick about class", now) || Study.TheirTurn(book, "Send Ms Marsh the proof", now) || Study.TheirTurn(book, "Call mum about the trip", now) || Study.TheirTurn(book, "Book the Statistics resit", now) || Study.TheirTurn(null, "Register for Dutch", now))
                throw new Exception("Plans were matched to the study tracker's waiting side wrong: " + string.Join(",", Study.Names(book, now).ToArray()));
            if (Study.TheirTurn(book, "Register for Dutch", quiet)) throw new Exception("A reply that never came kept his plan quiet for good.");
            if (!Plans.Add(vault, "Register for Dutch", new DateTime(2026, 11, 2), made) || !Plans.Add(vault, "Pay the phone bill", new DateTime(2026, 11, 3), made)) throw new Exception("The study smoke plans were not added.");
            List<PlanLine> due = Plans.Due(vault, now), overdue = Plans.Overdue(vault, now);
            if (due.Count != 1 || due[0].Text != "Pay the phone bill" || overdue.Count != 1 || overdue[0].Text != "Pay the phone bill") throw new Exception("A plan waiting on his lecturers was still raised as due or overdue.");
            if (Plans.Overdue(vault, quiet).Count != 2) throw new Exception("After a week with no reply from his lecturers the plan stayed hidden.");
            if (!Plans.Add(vault, "Email the Dutch office", new DateTime(2026, 11, 2), made) || !Plans.Wait(vault, Plans.KeyOf("Email the Dutch office"), new DateTime(2026, 11, 4), made)) throw new Exception("The study smoke waiting plan was not set.");
            if (!Plans.Due(vault, new DateTime(2026, 11, 4, 9, 0, 0)).Exists(delegate(PlanLine one) { return one.Text == "Email the Dutch office"; })) throw new Exception("A waiting plan's own follow-up was hidden by the study tracker.");
            if (Study.Talk("of course I'll do it", now) != "" || Study.Talk("which course is next?", now) == "") throw new Exception("\"of course\" pulled in the study tracker, or a course question didn't.");
            string keptStudy = Study.Folder;
            Study.Folder = Path.Combine(folder, "nothing");
            if (Plans.Due(vault, now).Count != 2) throw new Exception("Without a study tracker his plans weren't raised as before.");
            Study.Folder = keptStudy;
            checks.Add("PASS study waiting plans: a plan about a course or person the study tracker says has the ball (\"Register for Dutch\" while the Dutch teachers owe a reply) is not raised as due or overdue by the heartbeat, check-ins or night review; his own-move plans still are, a week with no reply brings it back, a waiting plan keeps its own follow-up, \"of course\" is not study talk, and without the tracker nothing changes");
        }
    }
}
