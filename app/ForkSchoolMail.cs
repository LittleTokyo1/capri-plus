using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private volatile StudyDraft schoolSend;
        private string schoolSendPrint;
        private DateTime schoolSendAt = DateTime.MinValue, schoolSendUntil = DateTime.MinValue;
        private SchoolMailItem schoolLastMail;
        private DateTime schoolLastMailAt = DateTime.MinValue;
        private DateTime schoolTickAt = DateTime.MinValue;
        private Action schoolQueued;
        private string schoolQueuedLabel;
        private volatile bool schoolCheckNow;
        private volatile string schoolTopic;
        private volatile string schoolCheckWhy = "he signed in";
        internal const string SignInAsk = "I couldn't check your school mail: Outlook wants you to sign in once in Chrome. Tell me when you've done it and I'll check right away.";
        internal static Func<DateTime, bool> SchoolTasksBusy = TasksNear;

        private static bool TasksNear(DateTime now)
        {
            foreach (string routine in new[] { StudyRoutines.Briefing, StudyRoutines.Drafts })
            {
                string xml = StudyRoutines.TaskXml(StudyRoutines.TaskName(routine));
                if (xml == null || SchoolMail.NearTask(xml, now)) return true;
            }
            return false;
        }

        private string SchoolSender()
        {
            SchoolMailItem last = schoolLastMail;
            return last != null && (DateTime.Now - schoolLastMailAt).TotalHours < SchoolMail.ReplyHours ? last.From : null;
        }

        private bool SchoolSendHeard(string text, bool voiceChat)
        {
            StudyDraft draft = schoolSend;
            if (draft == null || voiceChat) return false;
            DateTime now = DateTime.Now;
            if (now > schoolSendUntil) { schoolSend = null; SchoolQueueDrop(); return false; }
            if (AutoReply.Waiting(now) && AutoReply.DraftAt > schoolSendAt) return false;
            string said = (text ?? "").Trim();
            if (!SchoolMail.SendIt.IsMatch(said) && SchoolMail.Edit.IsMatch(said))
            {
                schoolSend = null;
                string editPage = StudyDrafts.PageLink();
                if (editPage == null) { SayStudy("school mail edit  " + draft.Id, "I can't find your drafts page link in the study routine, so I can't change it."); return true; }
                SayStudy("school mail edit  " + draft.Id, "Changing it now. I'll read it to you again.");
                SchoolJob("change school mail " + draft.Id, SchoolMail.EditPrompt(editPage, draft, said, now), SchoolMail.DraftTools, SchoolMail.ComposeMinutes, SchoolDrafted, true);
                return true;
            }
            if (SchoolMail.Leave.IsMatch(said))
            {
                schoolSend = null;
                SayStudy("school mail left as a draft  " + said, "Okay, it stays a draft on your drafts page. Say approve the " + Pursuit.Shorter(draft.To ?? "", 40) + " draft to put it in your Outlook Drafts." + SchoolNext());
                return true;
            }
            if (!SchoolMail.SendIt.IsMatch(said))
            {
                if (!SchoolMail.BareYes.IsMatch(said)) return false;
                SayStudy("school mail yes  " + said, "Say send it to send it now, or leave it as a draft.");
                return true;
            }
            schoolSend = null;
            string why = SchoolMail.SendBlock(draft, schoolSendPrint, StudyDrafts.Cached(), Lockout.Active, studyJobRunning, StudyDrafts.PageLink());
            if (why != null) { SayStudy("school mail not sent  " + draft.Id, why + SchoolRestStay()); return true; }
            string page = StudyDrafts.PageLink();
            SayStudy("school mail send  " + draft.Id, "Sending it now from your Outlook. I'll tell you when it's in your Sent items.");
            SchoolJob("send school mail " + draft.Id, SchoolMail.SendPrompt(page, draft, now), SchoolMail.DraftTools, SchoolMail.SendMinutes, delegate(ClaudeJobResult result) { return SchoolAfterSend(result, draft); }, false);
            return true;
        }

        private const int SchoolTalkLines = 8;

        private static string SchoolTalk(DateTime now)
        {
            List<PhoneChatLine> lines = PhoneHistory.Lines(Lately.FolderNow, now, SchoolTalkLines);
            List<string> said = new List<string>();
            foreach (PhoneChatLine line in lines) said.Add((line.Mine ? "him: " : "her: ") + (line.Text ?? "").Replace("\n", " "));
            return string.Join("\n", said.ToArray());
        }

        private bool SchoolMailHeard(string text, bool voiceChat)
        {
            if (voiceChat || Study.Safe() == null) return false;
            string what;
            string kind = SchoolMail.Kind(text, SchoolSender(), out what);
            if (kind == null) return SchoolCheckAsked(text);
            DateTime now = DateTime.Now;
            if (kind == "register")
            {
                SayStudy("school register  " + text, Lockout.Active ? "I'll get the registration ready after your game; it needs your Chrome." : "Getting the registration ready for " + what + ". I'll fill it in and stop before the final button.");
                string asked = what;
                SchoolJob("register " + what, SchoolMail.RegisterPrompt(what, now), SchoolMail.WatchTools, SchoolMail.RegisterMinutes, delegate(ClaudeJobResult result) { return SchoolMail.ReadyLine(result, asked); }, true);
                return true;
            }
            string page = StudyDrafts.PageLink();
            if (page == null) { SayStudy("school mail  " + text, "I can't find your drafts page link in the study routine, so I can't write it there."); return true; }
            SchoolMailItem answering = kind == "reply" ? schoolLastMail : null;
            SchoolQueueDrop();
            SayStudy("school mail compose  " + text, Lockout.Active ? "I'll write it after your game; it needs your Chrome." : "Writing it now. I'll read it to you in a minute or two.");
            SchoolJob("write school mail", SchoolMail.ComposePrompt(page, text, answering, answering == null ? SchoolTalk(now) : null, now), SchoolMail.DraftTools, SchoolMail.ComposeMinutes, SchoolDrafted, true);
            return true;
        }

        // "check my school mail" runs the read-only check now; "... about my grades" searches the last week for that topic.
        private bool SchoolCheckAsked(string text)
        {
            Match check = SchoolMail.CheckAsk.Match((text ?? "").Trim());
            if (!check.Success || SchoolGmail.Ready()) return false;
            string topic = check.Groups["topic"].Success ? check.Groups["topic"].Value.Trim().TrimEnd('.', '?', '!') : "";
            schoolTopic = topic.Length == 0 ? null : topic;
            schoolCheckWhy = "he asked";
            schoolCheckNow = true;
            schoolTickAt = DateTime.MinValue;
            string say = Lockout.Active ? "I'll check your school mail after your game; it needs your Chrome." : topic.Length == 0 ? "Checking your school mail now. I'll tell you what's new in a minute or two." : "Looking through your school mail from the last week for " + topic + ". I'll tell you what I find.";
            SayStudy("school mail check asked  " + text, say);
            return true;
        }

        private string SchoolDrafted(ClaudeJobResult result)
        {
            string id = SchoolMail.Drafted(result);
            if (id == null) return "I couldn't write that email: " + (result == null ? "the job didn't start" : result.Problem ?? Pursuit.Shorter(result.Last, StudyDrafts.SaidChars)) + ".";
            StudyDraft draft = SchoolMail.Find(StudyDrafts.Cached(), id);
            if (draft == null) return "I wrote it on your drafts page, but I can't read it back yet. Ask me to read the draft in a minute.";
            string hole = StudyDrafts.Unfilled(draft);
            if (hole != null) return "I wrote it, but it still has " + hole + " to fill in on your drafts page. " + StudyDrafts.Reading(draft);
            schoolSendPrint = SchoolMail.Fingerprint(draft);
            schoolSendAt = DateTime.Now;
            schoolSendUntil = schoolSendAt.AddSeconds(SchoolMail.SendSeconds);
            schoolSend = draft;
            SchoolTold(draft.Id);
            return StudyDrafts.Reading(draft) + (schoolSendQueue.Count > 0 ? " Say send it to send this one, or leave it and I'll read the next." : " Say send it to send it now, or leave it as a draft.");
        }

        private void SchoolJob(string label, string prompt, string[] tools, int minutes, Func<ClaudeJobResult, string> said, bool mayWait)
        {
            if (studyJobRunning || Lockout.Active)
            {
                if (!mayWait) { SayStudy(label, "Not now; it stays a draft."); return; }
                schoolQueued = delegate { SchoolJob(label, prompt, tools, minutes, said, false); };
                schoolQueuedLabel = label;
                ActivityLog.Write("study", "school job queued", label);
                return;
            }
            studyJobRunning = true;
            string folder = Study.Folder;
            if (testing)
            {
                try { SayStudy(label + " (done)", said(ClaudeJob.Run(label, folder, prompt, tools, true, minutes))); }
                finally { studyJobRunning = false; }
                return;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                string line;
                try { line = said(ClaudeJob.Run(label, folder, prompt, tools, true, minutes)); }
                catch (Exception error) { line = "That school job failed: " + error.Message; }
                finally { studyJobRunning = false; }
                Post(delegate { SayStudy(label + " (done)", line); });
            });
        }

        internal bool SchoolMailTurn(DateTime now)
        {
            SchoolMailTool.Start = SchoolToolStart;
            if (Math.Abs((now - schoolTickAt).TotalSeconds) < StudyTickSeconds) return false;
            schoolTickAt = now;
            if (studyJobRunning || Lockout.Active || Study.Safe() == null) return false;
            Action queued = schoolQueued;
            if (queued != null)
            {
                schoolQueued = null;
                ActivityLog.Write("study", "school job from the queue", schoolQueuedLabel);
                queued();
                return false;
            }
            DateTime last;
            List<string> told;
            SchoolMail.Load(out last, out told);
            bool asked = schoolCheckNow;
            bool own = asked && schoolCheckWhy == "he asked";
            if (!own && SchoolTasksBusy(now)) return false;
            if (!asked &&(!SchoolMail.Due(now, last) || OpenAsks.AskedToday(OpenAsks.SignIn, now))) return false;
            schoolCheckNow = false;
            string topic = asked ? schoolTopic : null;
            schoolTopic = null;
            if (asked) ActivityLog.Write("study", "school mail check", "now, " + schoolCheckWhy + (topic == null ? "" : ": " + topic));
            schoolCheckWhy = "he signed in";
            DateTime since = topic != null ? now.AddDays(-SchoolMail.SearchDays) : last == DateTime.MinValue ? now.AddHours(-SchoolMail.WatchHours) : last;
            SchoolMail.Save(now, told);
            studyJobRunning = true;
            string folder = Study.Folder, prompt = SchoolMail.WatchPrompt(since, now, topic);
            Action work = delegate
            {
                ClaudeJobResult result = null;
                try
                {
                    if (own) SchoolWaitRoutine(folder);
                    result = ClaudeJob.Run("school mail check", folder, prompt, SchoolMail.WatchTools, true, SchoolMail.WatchMinutes);
                }
                catch (Exception error) { Brain.Log("school mail", -1, error.Message); }
                finally { studyJobRunning = false; }
                bool everyMatch = topic != null;
                Action show = delegate { SchoolMailChecked(result, folder, DateTime.Now, now, everyMatch, own); };
                if (testing) show(); else Post(show);
            };
            if (testing) work();
            else ThreadPool.QueueUserWorkItem(delegate { work(); });
            return true;
        }

        private const int RoutineWaitMinutes = 25;

        private void SchoolWaitRoutine(string folder)
        {
            if (testing || !ClaudeJob.RoutineWorking(folder)) return;
            ActivityLog.Write("study", "school mail check", "waiting, his study routine is running");
            Post(delegate { SchoolTell("Your study routine is using Chrome right now, so I'll check your mail the moment it's done.", DateTime.Now); });
            DateTime until = DateTime.Now.AddMinutes(RoutineWaitMinutes);
            while (DateTime.Now < until && ClaudeJob.RoutineWorking(folder)) Thread.Sleep(ClaudeJob.PollSeconds * 1000);
        }

        private void SchoolMailChecked(ClaudeJobResult result, string folder, DateTime now, DateTime turn, bool everyMatch = false, bool asked = false)
        {
            if (SchoolMail.SignIn(result))
            {
                ActivityLog.Write("study", "school mail check", "Outlook wants him to sign in");
                bool toldToday = OpenAsks.AskedToday(OpenAsks.SignIn, turn);
                OpenAsks.Add(OpenAsks.SignIn, OpenAsks.SchoolSignIn, turn);
                if (SchoolMail.TellSignIn(toldToday, asked)) SchoolTell(SignInAsk, now);
                return;
            }
            if (result == null || !result.Ok)
            {
                ActivityLog.Write("study", "school mail check failed", result == null ? null : result.Problem);
                return;
            }
            DateTime last;
            List<string> told;
            SchoolMail.Load(out last, out told);
            List<SchoolMailItem> fresh = everyMatch ? SchoolMail.Mail(folder) : SchoolMail.Untold(SchoolMail.Mail(folder), told);
            ActivityLog.Write("study", "school mail check", fresh.Count + (everyMatch ? " found" : " new"));
            int shown = 0;
            foreach (SchoolMailItem mail in fresh)
            {
                if (!told.Contains(mail.Key)) told.Add(mail.Key);
                if (shown >= SchoolMail.MostTold) continue;
                shown++;
                if (mail.NeedsReply) { schoolLastMail = mail; schoolLastMailAt = now; }
                SchoolTell(SchoolMail.Telling(mail), now);
            }
            if (fresh.Count > shown) SchoolTell("And " + (fresh.Count - shown) + " more school mails; ask me for your study briefing for the rest.", now);
            SchoolMail.Save(last, told);
        }

        private void SchoolTell(string line, DateTime now)
        {
            if (testing) { SayStudy("school mail", line); return; }
            bool notHome = Delivery.NotHome();
            if (notHome && Delivery.HeadsMay(ForkSettings.Current, now, true))
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool sent = false;
                    try { sent = Delivery.Send(line); }
                    catch (Exception error) { Brain.Log("school mail", -1, error.GetType().Name); }
                    Post(delegate
                    {
                        if (sent) brain.Own("study", "Sent to his phone on Discord: " + line);
                        else Digest.Add("study", line, DateTime.Now);
                    });
                });
                return;
            }
            if (!notHome && ScheduleMaySpeak()) { SayStudy("school mail", line); return; }
            ActivityLog.Write("study", "school mail held for later", line);
            Digest.Add("study", line, now);
        }
    }
}
