using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private volatile List<StudyDraft> schoolSendQueue = new List<StudyDraft>();
        private volatile string schoolToldId;
        private DateTime schoolToldAt = DateTime.MinValue;

        private bool SchoolToldLately()
        {
            return schoolToldId != null && (DateTime.Now - schoolToldAt).TotalMinutes < SchoolSendQueue.RecentMinutes;
        }

        private void SchoolQueueDrop() { schoolSendQueue = new List<StudyDraft>(); }

        private bool SendWaitingHeard(string text, bool voiceChat)
        {
            if (voiceChat || Study.Safe() == null) return false;
            string kind = SchoolSendQueue.Wanted(text, SchoolToldLately(), StudyDrafts.Waiting(StudyDrafts.Cached()));
            if (kind == null) return false;
            string blocked = SendWaitingBlock();
            if (blocked != null) { SayStudy("school send waiting  " + text, blocked); return true; }
            SayStudy("school send waiting  " + text, SendWaitingSaid);
            SendWaitingBegin(text, kind);
            return true;
        }

        private const string SendWaitingSaid = "Checking your drafts page first, then I'll read each one to you. Nothing goes out until you say send it.";

        private string SendWaitingBlock()
        {
            if (StudyDrafts.PageLink() == null) return "I can't find your drafts page link in the study routine, so I can't send anything from it.";
            if (Lockout.Active) return "Not now: a game is running, and sending needs your Chrome. Say it again after the game.";
            if (studyJobRunning) return "A study job is still running, so I can't check your drafts yet. Say it again in a few minutes.";
            return null;
        }

        private void SendWaitingBegin(string text, string kind)
        {
            SchoolQueueDrop();
            schoolSend = null;
            string lastId = schoolToldId;
            ActivityLog.Write("study", "school send asked", kind + ": " + Pursuit.Shorter(text, 120));
            StudyJob("refresh drafts to send", StudyDrafts.RefreshPrompt(StudyDrafts.PageLink()), delegate(ClaudeJobResult result) { return SendWaitingPicked(result, kind, text, lastId); });
        }

        private string SendWaitingPicked(ClaudeJobResult result, string kind, string text, string lastId)
        {
            List<StudyDraft> fresh = StudyDrafts.Cached();
            if (result == null || !result.Ok || fresh == null) return "I couldn't read your drafts page, so I sent nothing: " + (result == null ? "the job didn't start" : result.Problem ?? Pursuit.Shorter(result.Last, StudyDrafts.SaidChars)) + ".";
            string ask;
            List<StudyDraft> older;
            List<StudyDraft> picked = SchoolSendQueue.Pick(fresh, kind, text, lastId, out older, out ask);
            if (ask != null) return ask;
            string flag = SchoolSendQueue.Flag(older, picked);
            schoolSendQueue = picked.GetRange(1, picked.Count - 1);
            return ((flag.Length == 0 ? "" : flag + " ") + SchoolSendQueue.Intro(picked.Count) + SchoolArm(picked[0])).Trim();
        }

        private string SchoolArm(StudyDraft draft)
        {
            string hole = StudyDrafts.Unfilled(draft);
            if (hole != null) return "The draft " + StudyDrafts.Short(draft) + " still has " + hole + " to fill in, so I skipped it." + SchoolNext();
            schoolSendPrint = SchoolMail.Fingerprint(draft);
            schoolSendAt = DateTime.Now;
            schoolSendUntil = schoolSendAt.AddSeconds(SchoolMail.SendSeconds);
            schoolSend = draft;
            SchoolTold(draft.Id);
            bool more = schoolSendQueue.Count > 0;
            return StudyDrafts.Reading(draft) + (more ? " Say send it to send this one, or leave it and I'll read the next." : " Say send it to send it now, or leave it as a draft.");
        }

        private void SchoolTold(string id)
        {
            schoolToldId = id;
            schoolToldAt = DateTime.Now;
        }

        private string SchoolNext()
        {
            List<StudyDraft> queue = schoolSendQueue;
            if (queue.Count == 0) return "";
            schoolSendQueue = queue.GetRange(1, queue.Count - 1);
            return " Next: " + SchoolArm(queue[0]);
        }

        private string SchoolRestStay()
        {
            int left = schoolSendQueue.Count;
            if (left == 0) return "";
            SchoolQueueDrop();
            return left == 1 ? " The other draft stays on your drafts page too." : " The other drafts stay on your drafts page too.";
        }

        private string SchoolAfterSend(ClaudeJobResult result, StudyDraft draft)
        {
            string line = SchoolMail.SentLine(result, draft);
            if (schoolSendQueue.Count == 0) return line;
            if (!SchoolSendQueue.Clear(result)) return line + " I stopped there." + SchoolRestStay();
            return line + SchoolNext();
        }

        private string SchoolReadArm(StudyDraft draft)
        {
            if (draft.Status != "proposed" || StudyDrafts.Unfilled(draft) != null) return StudyDrafts.Reading(draft);
            SchoolQueueDrop();
            List<StudyDraft> older;
            SchoolSendQueue.Newest(StudyDrafts.Waiting(StudyDrafts.Cached()), out older);
            string flag = SchoolSendQueue.Flag(older.FindAll(delegate(StudyDraft one) { return one.Id != draft.Id; }), new List<StudyDraft> { draft });
            return SchoolArm(draft) + (flag.Length == 0 ? "" : " " + flag);
        }

        private string SchoolToolSend(string asked)
        {
            string his = (Brain.Asking ?? "").Trim();
            string words = SchoolSendQueue.Kind(his) != null ? his : asked ?? "";
            string kind = SchoolSendQueue.Kind(words) ?? (SchoolToldLately() ? "last" : "all");
            string blocked = SendWaitingBlock();
            if (blocked != null) return blocked;
            ActivityLog.Write("study", "school mail send by tool", Pursuit.Shorter(words, 120));
            if (testing) SendWaitingBegin(words, kind);
            else Post(delegate { SendWaitingBegin(words, kind); });
            return SendWaitingSaid;
        }
    }
}
