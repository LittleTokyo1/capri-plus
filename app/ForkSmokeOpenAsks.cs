using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const string SmokeBlocked = "[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"blocked\"}]";
        private const string SmokeJobDone = "[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]";
        private const string SmokeSignInPlan = "Sign in to Outlook, MySchool and CoursePortal in Chrome";

        private void SmokeOpenAsks(List<string> checks)
        {
            string keptFolder = Study.Folder, keptLog = ActivityLog.FilePath, keptFile = SchoolMail.FilePath, keptAsks = OpenAsks.FilePath, keptRoot = Vault.Root, keptHeard = heard;
            Func<string, string[], string> keptCli = ClaudeJob.Cli;
            Action<int> keptWait = ClaudeJob.Wait;
            Func<DateTime, bool> keptBusy = SchoolTasksBusy;
            TalkState keptTalk = talkState;
            string folder = Path.Combine(Path.GetTempPath(), "capri-open-asks-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                SchoolMail.FilePath = Path.Combine(folder, "school-mail.txt");
                OpenAsks.FilePath = Path.Combine(folder, "open-asks.txt");
                Vault.Root = Path.Combine(folder, "vault");
                Directory.CreateDirectory(Vault.Root);
                ClaudeJob.Wait = delegate { };
                SchoolTasksBusy = delegate { return false; };
                Study.Folder = Path.Combine(folder, "Study-Assistant");
                SmokeStudyFiles(Path.Combine(Study.Folder, "tracker"));
                File.WriteAllText(Path.Combine(Study.Folder, "tracker", SchoolMail.MailName), "[]");
                SmokeOpenAskWords();
                SmokeSignInWatch();
                SmokeDoneWord();
                checks.Add("PASS sign-in asks: a school mail job that stops at a sign-in page is told as a sign-in once a day (not as a stuck permission prompt) and the watch waits until he says he did it or the next day; \"I signed in\" (also misheard as \"output\"), \"done\" or \"I did it\" ticks off her last open ask or sign-in plan, even hours old, and runs the school mail check at once; a done-word with no open ask stays chat; his school sites are in her hotwords");
            }
            finally
            {
                Study.Folder = keptFolder;
                ActivityLog.FilePath = keptLog;
                SchoolMail.FilePath = keptFile;
                OpenAsks.FilePath = keptAsks;
                Vault.Root = keptRoot;
                ClaudeJob.Cli = keptCli;
                ClaudeJob.Wait = keptWait;
                SchoolTasksBusy = keptBusy;
                schoolCheckNow = false;
                schoolQueued = null;
                studyJobRunning = false;
                CloseTalk();
                talkState = keptTalk;
                heard = keptHeard;
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private static void SmokeOpenAskWords()
        {
            string[] signIn = { "I signed in", "He signed in to output by the way. The sign in.", "okay I logged into Outlook", "signed in", "I've signed in to CoursePortal now", "just logged in", "I'm signed in", "I am logged in now", "logged onto CoursePortal" };
            string[] bare = { "done", "I did it", "all done", "okay, done", "I've done it" };
            string[] chat = { "I need to sign in to Outlook", "did you sign in?", "sign me in to Discord", "I signed in to Discord", "I'm done with this game", "I'm signed in to Discord", "how do I sign in", "I signed in but it didn't work", "I logged into Steam and it crashed", "done with dinner? no", "what did you do today" };
            foreach (string line in signIn) if (OpenAsks.Heard(line) != OpenAsks.SignIn) throw new Exception("He said he signed in and it was not heard as that: " + line);
            foreach (string line in bare) if (OpenAsks.Heard(line) != OpenAsks.Bare) throw new Exception("A done-word was not heard: " + line);
            foreach (string line in chat) if (OpenAsks.Heard(line) != null) throw new Exception("A chat line was taken as him having done her ask: " + line);
            if (!OpenAsks.SignInAtEnd("Outlook shows the Microsoft account picker.\nSIGN IN\n────") || !OpenAsks.SignInAtEnd("opened outlook\nSIGN IN required"))
                throw new Exception("A job ending with SIGN IN was not read as a sign-in.");
            if (OpenAsks.SignInAtEnd("Reply with one line: MAIL <count>, or\nSIGN IN when Outlook wants him to sign in.\nopened the inbox\nMAIL 0") || OpenAsks.SignInAtEnd("working...\nAPPROVED"))
                throw new Exception("The prompt's own words or a normal end were read as a sign-in.");
        }

        private void SmokeSignInWatch()
        {
            SmokeCli cli = new SmokeCli();
            ClaudeJob.Cli = cli.Run;
            SchoolMail.Save(new DateTime(2026, 11, 4, 8, 0, 0), new List<string>());
            cli.Log = "Outlook shows the Microsoft account picker.\nSIGN IN";
            cli.States.Enqueue(SmokeBlocked);
            schoolTickAt = DateTime.MinValue;
            SchoolMailTurn(new DateTime(2026, 11, 4, 10, 30, 0));
            string log = File.ReadAllText(ActivityLog.FilePath);
            if (log.IndexOf("ended: " + ClaudeJob.SignInPage, StringComparison.Ordinal) < 0 || log.IndexOf("Outlook wants him to sign in", StringComparison.Ordinal) < 0 || log.IndexOf(SignInAsk, StringComparison.Ordinal) < 0 || log.IndexOf("stuck on a permission prompt", StringComparison.Ordinal) >= 0)
                throw new Exception("A school mail job that stopped at a sign-in page was not told as a sign-in: " + Pursuit.Shorter(log, 400));
            if (OpenAsks.Peek(OpenAsks.SignIn, new DateTime(2026, 11, 4, 10, 31, 0)) == null) throw new Exception("Her sign-in ask was not kept as open.");
            int calls = cli.Calls.Count;
            SchoolMail.Save(new DateTime(2026, 11, 4, 8, 0, 0), new List<string>());
            schoolTickAt = DateTime.MinValue;
            SchoolMailTurn(new DateTime(2026, 11, 4, 12, 40, 0));
            if (cli.Calls.Count != calls) throw new Exception("The mail watch ran again the same day while her sign-in ask was open.");
            cli.Log = "Opened login.microsoftonline.com: Pick an account.";
            cli.States.Enqueue(SmokeBlocked);
            schoolCheckNow = true;
            schoolTickAt = DateTime.MinValue;
            SchoolMailTurn(new DateTime(2026, 11, 4, 13, 0, 0));
            log = File.ReadAllText(ActivityLog.FilePath);
            if (log.IndexOf(SignInAsk, StringComparison.Ordinal) != log.LastIndexOf(SignInAsk, StringComparison.Ordinal)) throw new Exception("The sign-in ask was told twice in one day.");
            if (log.IndexOf("ended: " + ClaudeJob.SignInPage, StringComparison.Ordinal) == log.LastIndexOf("ended: " + ClaudeJob.SignInPage, StringComparison.Ordinal)) throw new Exception("A job blocked on the Microsoft account picker with no SIGN IN line was not read as a sign-in.");
            if (OpenAsks.AskedToday(OpenAsks.SignIn, new DateTime(2026, 11, 5, 10, 30, 0))) throw new Exception("Yesterday's sign-in ask still held the watch today.");
        }

        private void SmokeDoneWord()
        {
            SmokeCli cli = new SmokeCli();
            ClaudeJob.Cli = cli.Run;
            DateTime now = DateTime.Now;
            File.Delete(OpenAsks.FilePath);
            if (DoneHeard("done", false) || DoneHeard("I signed in", false) || DoneHeard("I did it", true)) throw new Exception("A done-word with no open ask was not left to chat.");
            OpenAsks.Add(OpenAsks.SignIn, OpenAsks.SchoolSignIn, now.AddHours(-2));
            Plans.Add(Vault.Root, SmokeSignInPlan, null, now.AddHours(-2));
            Plans.Add(Vault.Root, "Call mum about the trip", null, now.AddHours(-1));
            cli.Log = "opened the inbox\nMAIL 0";
            int calls = cli.Calls.Count;
            SchoolTasksBusy = delegate { return true; };
            if (!ForkHeard("He signed in to output by the way. The sign in.", false)) throw new Exception("He said he signed in and she did not take it.");
            string plans = File.ReadAllText(Plans.PathIn(Vault.Root));
            if (plans.IndexOf("- [x] " + SmokeSignInPlan, StringComparison.Ordinal) < 0 || plans.IndexOf("- [ ] Call mum about the trip", StringComparison.Ordinal) < 0)
                throw new Exception("The sign-in plan was not ticked off, or another plan was: " + plans);
            if (OpenAsks.Peek(OpenAsks.SignIn, DateTime.Now) != null) throw new Exception("Her sign-in ask stayed open after he did it.");
            string log = File.ReadAllText(ActivityLog.FilePath);
            if (cli.Calls.Count != calls || log.IndexOf("Checking your school mail now", StringComparison.Ordinal) >= 0 || log.IndexOf("as soon as your other study job is done", StringComparison.Ordinal) < 0)
                throw new Exception("She said she was checking his school mail while his own study task held the check back.");
            SchoolTasksBusy = delegate { return false; };
            cli.States.Enqueue(SmokeJobDone);
            schoolTickAt = DateTime.MinValue;
            SchoolMailTurn(DateTime.Now);
            string watched = cli.Calls.GetRange(calls, cli.Calls.Count - calls).Find(delegate(string call) { return call.StartsWith("--bg", StringComparison.Ordinal); });
            log = File.ReadAllText(ActivityLog.FilePath);
            if (watched == null || watched.IndexOf("A read-only check of his school mail", StringComparison.Ordinal) < 0 || log.IndexOf("school mail check  ->  now, he signed in", StringComparison.Ordinal) < 0)
                throw new Exception("The school mail check that waited on the sign-in did not run once his study task was out of the way.");
            OpenAsks.Add(OpenAsks.SignIn, OpenAsks.StudySignIn, DateTime.Now.AddHours(-5));
            cli.States.Enqueue(SmokeJobDone);
            calls = cli.Calls.Count;
            if (!DoneHeard("I did it", true) || OpenAsks.Peek(null, DateTime.Now) != null || cli.Calls.Count == calls) throw new Exception("A done in voice chat after her old sign-in ask did not close it and run the check.");
            log = File.ReadAllText(ActivityLog.FilePath);
            if (log.IndexOf("he did it (voice chat) I did it  ->  Nice, ticked off: " + OpenAsks.StudySignIn + ". Checking your school mail now.", StringComparison.Ordinal) < 0 || log.IndexOf("study  he did it  I did it", StringComparison.Ordinal) >= 0)
                throw new Exception("In voice chat she spoke over her own voice reply, or the check was not told to her voice brain: " + log.Substring(Math.Max(0, log.Length - 700)));
            if (DoneHeard("done", false)) throw new Exception("A second done with nothing open was taken.");
        }
    }
}
