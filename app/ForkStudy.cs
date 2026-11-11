using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal const int StudyAskSeconds = 120;
        private static readonly Regex StudyRefresh = new Regex(@"\b(?:check|refresh|update|reload|look\s+at)\b", RegexOptions.IgnoreCase);
        private volatile StudyDraft studyPending;
        private DateTime studyPendingUntil;
        private volatile bool studyJobRunning;
        internal const int StudyTickSeconds = 60;
        private volatile string studyHeld;
        private DateTime studyTickAt = DateTime.MinValue;
        private DateTime studySignInDay = DateTime.MinValue;

        private bool StudyHeard(string text, bool voiceChat)
        {
            if (voiceChat) return false;
            if (StudyAnswered(text)) return true;
            string routine = StudyRoutines.Asked(text);
            if (routine != null && Study.Safe() != null)
            {
                StudyRoutine(routine, true, DateTime.Now);
                return true;
            }
            string drafts = StudyDrafts.Kind(text);
            if (drafts != null && Study.Safe() != null) return DraftsHeard(drafts, text);
            string kind = Study.Kind(text);
            if (kind == null) return false;
            StudyBook book = Study.Safe();
            if (book == null) return false;
            SayStudy(kind + "  " + text, Study.Answer(book, kind, DateTime.Now));
            return true;
        }

        private void SayStudy(string what, string said)
        {
            ActivityLog.Write("study", what, said);
            brain.Own("he asked about his studies", said);
            Tell("(your studies)", said);
        }

        private bool StudyAnswered(string text)
        {
            StudyDraft draft = studyPending;
            if (draft == null) return false;
            studyPending = null;
            if (DateTime.Now > studyPendingUntil) return false;
            if (StudyDrafts.No.IsMatch(text ?? ""))
            {
                SayStudy("draft not approved  " + text, "Okay, I left the draft " + StudyDrafts.Short(draft) + " as it is.");
                return true;
            }
            if (!StudyDrafts.Yes.IsMatch(text ?? "")) return false;
            string page = StudyDrafts.PageLink();
            if (page == null)
            {
                SayStudy("draft approve  " + text, "I can't find your drafts page link in the study routine, so I can't approve it.");
                return true;
            }
            SayStudy("draft approve  " + text, "Approving it now. I'll tell you when it's done.");
            string shown = StudyDrafts.Short(draft);
            StudyJob("approve draft " + draft.Id, StudyDrafts.ApprovePrompt(page, draft), delegate(ClaudeJobResult result)
            {
                if (StudyDrafts.Approved(result)) return "Approved: the draft " + shown + " goes into your Outlook Drafts at the next drafts run. You press Send.";
                return "I couldn't approve the draft " + shown + ": " + (result.Problem ?? Pursuit.Shorter(result.Last, StudyDrafts.SaidChars));
            });
            return true;
        }

        private bool DraftsHeard(string kind, string text)
        {
            List<StudyDraft> drafts = StudyDrafts.Cached();
            if (drafts == null || (kind == "list" && StudyRefresh.IsMatch(text ?? "")))
            {
                string page = StudyDrafts.PageLink();
                if (page == null) { SayStudy("drafts  " + text, "I can't find your drafts page link in the study routine."); return true; }
                SayStudy("drafts  " + text, drafts == null ? "I don't have your drafts yet. Checking your drafts page now, about a minute." : "Checking your drafts page now, about a minute.");
                StudyJob("refresh drafts", StudyDrafts.RefreshPrompt(page), delegate(ClaudeJobResult result)
                {
                    List<StudyDraft> fresh = StudyDrafts.Cached();
                    if (!result.Ok || fresh == null) return "I couldn't read your drafts page: " + (result.Problem ?? Pursuit.Shorter(result.Last, StudyDrafts.SaidChars));
                    return StudyDrafts.Listing(fresh);
                });
                return true;
            }
            if (kind == "list") { SayStudy("drafts  " + text, StudyDrafts.Listing(drafts)); return true; }
            List<StudyDraft> named = StudyDrafts.Named(drafts, text, kind == "approve");
            if (named.Count != 1)
            {
                string which = named.Count == 0 ? "I can't tell which draft you mean. " + StudyDrafts.Listing(drafts) : "Which one: " + string.Join("; ", named.ConvertAll(StudyDrafts.Short).ToArray()) + "?";
                SayStudy("drafts  " + text, which);
                return true;
            }
            StudyDraft draft = named[0];
            if (kind == "read") { SayStudy("draft read  " + text, SchoolReadArm(draft)); return true; }
            schoolSend = null;
            string hole = StudyDrafts.Unfilled(draft);
            if (hole != null)
            {
                SayStudy("draft approve  " + text, "The draft " + StudyDrafts.Short(draft) + " still has " + hole + " to fill in. Fill the brackets on the drafts page first, then tell me.");
                return true;
            }
            studyPending = draft;
            studyPendingUntil = DateTime.Now.AddSeconds(StudyAskSeconds);
            SayStudy("draft approve asked  " + text, "Approve the draft " + StudyDrafts.Short(draft) + "? It goes into your Outlook Drafts, not sent; you still press Send. Yes or no?");
            return true;
        }

        private void StudyTick()
        {
            if (testing) return;
            StudyTurn(DateTime.Now);
            SchoolMailTurn(DateTime.Now);
        }

        internal void StudyTurn(DateTime now)
        {
            if (Math.Abs((now - studyTickAt).TotalSeconds) < StudyTickSeconds) return;
            studyTickAt = now;
            if (studyJobRunning || Lockout.Active || Study.Safe() == null) return;
            string held = studyHeld;
            if (held != null)
            {
                studyHeld = null;
                StudyRoutine(held, true, now);
                return;
            }
            Dictionary<string, DateTime> last = StudyRoutines.Load();
            foreach (string routine in new[] { StudyRoutines.Briefing, StudyRoutines.Drafts })
                if (StudyRoutines.Due(routine, now, last) && !StudyRoutines.WindowsTaskOn(StudyRoutines.TaskName(routine)))
                {
                    StudyRoutine(routine, false, now);
                    return;
                }
        }

        private void StudyRoutine(string routine, bool asked, DateTime now)
        {
            string label = StudyRoutines.Label(routine);
            if (Lockout.Active)
            {
                studyHeld = routine;
                if (asked) SayStudy("routine held  " + routine, "I'll run your " + label + " after your game; it needs your Chrome.");
                return;
            }
            if (studyJobRunning)
            {
                if (asked) SayStudy("routine  " + routine, "A study job is still running; ask me again in a few minutes.");
                return;
            }
            StudyRoutines.Mark(routine, now);
            studyJobRunning = true;
            string folder = Study.Folder, prompt = StudyRoutines.Prompt(routine, now);
            if (asked) SayStudy("routine  " + routine, "Running your " + label + " now in Chrome. It takes a few minutes; I'll tell you when it's done.");
            Action work = delegate
            {
                ClaudeJobResult result = null;
                string line;
                try
                {
                    result = ClaudeJob.Run(label, folder, prompt, StudyRoutines.Tools, true, StudyRoutines.JobMinutes);
                    line = StudyRoutines.Said(routine, result, Study.Safe());
                }
                catch (Exception error) { line = "Your " + label + " failed: " + error.Message; }
                finally { studyJobRunning = false; }
                bool paused = result != null && result.Problem == ClaudeJob.GameStarted;
                if (paused)
                {
                    studyHeld = routine;
                    line = "Stopped your " + label + " because a game started; it runs again after the game.";
                }
                bool signIn = !paused && StudyRoutines.SignIn(result);
                bool tell = !paused && (asked || (result != null && !result.Ok) || (signIn && studySignInDay != now.Date));
                if (signIn) { studySignInDay = now.Date; OpenAsks.Add(OpenAsks.SignIn, OpenAsks.StudySignIn, now); line += " Tell me when you've done it."; }
                Action show = delegate
                {
                    if (!tell) { ActivityLog.Write("study", label + " (done, on schedule)", line); return; }
                    if (asked || ScheduleMaySpeak()) { SayStudy(label + " (done)", line); return; }
                    ActivityLog.Write("study", label + " (done, held for later)", line);
                    Digest.Add("study", line, DateTime.Now);
                };
                if (testing) show(); else Post(show);
            };
            if (testing) work();
            else ThreadPool.QueueUserWorkItem(delegate { work(); });
        }

        private void StudyJob(string label, string prompt, Func<ClaudeJobResult, string> said)
        {
            if (studyJobRunning) { SayStudy(label, "A study job is still running; I'll take this after it."); return; }
            studyJobRunning = true;
            string folder = Study.Folder;
            if (testing)
            {
                try { SayStudy(label + " (done)", said(ClaudeJob.Run(label, folder, prompt, StudyDrafts.Tools, false, StudyDrafts.JobMinutes))); }
                finally { studyJobRunning = false; }
                return;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                string line;
                try { line = said(ClaudeJob.Run(label, folder, prompt, StudyDrafts.Tools, false, StudyDrafts.JobMinutes)); }
                catch (Exception error) { line = "That study job failed: " + error.Message; }
                finally { studyJobRunning = false; }
                Post(delegate { SayStudy(label + " (done)", line); });
            });
        }
    }
}
