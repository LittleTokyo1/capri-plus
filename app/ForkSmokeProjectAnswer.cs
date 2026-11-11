using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeProjectAnswer(List<string> checks)
        {
            if (!Hands.Tools.Contains("\"name\":\"" + ProjectRun.AnswerTool + "\"") || Lockout.Handles(ProjectRun.AnswerTool)) throw new Exception("Her brain isn't offered the project answer tool, or the game lockout refuses it (it holds answers itself).");
            string root = Path.Combine(Path.GetTempPath(), "capri-answer-smoke-" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "work", "demo");
            string transcripts = Path.Combine(root, "transcripts");
            Func<string, string[], ProjectProcess> launch = ProjectRun.Launch;
            Func<int, long, bool> alive = ProjectRun.Alive;
            Action<int> shut = ProjectRun.Close;
            Func<bool> gaming = ProjectRun.Gaming;
            Func<string> hookExe = ProjectRun.HookExe;
            Func<bool> herOpen = ProjectHook.HerOpen;
            string statePath = ProjectRun.StatePath, transcriptRoot = ProjectRun.TranscriptRoot, hookFolder = ProjectHook.Folder;
            try
            {
                Directory.CreateDirectory(folder);
                Directory.CreateDirectory(transcripts);
                ProjectRun.StatePath = Path.Combine(root, "project-runs.json");
                ProjectRun.TranscriptRoot = transcripts;
                ProjectHook.Folder = Path.Combine(root, "hooks");
                ProjectRun.HookExe = delegate { return @"C:\x\Capri.exe"; };
                ProjectHook.HerOpen = delegate { return false; };
                SmokeProjectAnswers(folder, transcripts);
            }
            finally
            {
                ProjectRun.Launch = launch;
                ProjectRun.Alive = alive;
                ProjectRun.Close = shut;
                ProjectRun.Gaming = gaming;
                ProjectRun.StatePath = statePath;
                ProjectRun.TranscriptRoot = transcriptRoot;
                ProjectRun.HookExe = hookExe;
                ProjectHook.HerOpen = herOpen;
                ProjectHook.Folder = hookFolder;
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
            checks.Add("PASS his answer back (097 slice 3b): an answer to a question she brought him goes into that session (its idle window closed, then --resume with the same session id, her hooks, Remote Control and its permission again, his words as the next message), held during a game and passed on after it, never when nothing waits (a plan stays his to approve in its window), with the switch off, or when the session already went on (a newer turn, or he answered in its window or on the phone)");
        }

        private static void SmokeProjectAnswers(string folder, string transcripts)
        {
            int launched = 0, closed = 0, pid = 5000;
            bool game = false;
            string[] last = null;
            DateTime started = new DateTime(2026, 11, 5, 9, 0, 0);
            ProjectRun.Launch = delegate(string where, string[] arguments) { launched++; last = arguments; pid++; return new ProjectProcess { Pid = pid, Started = started }; };
            ProjectRun.Alive = delegate(int one, long when) { return one == pid && when == started.Ticks; };
            ProjectRun.Close = delegate(int one) { closed++; };
            ProjectRun.Gaming = delegate { return game; };
            string[] lines = { folder };
            DateTime day = started;
            string name;
            if (ProjectRun.Start(true, lines, 5, null, null, day, out name) != null || launched != 1) throw new Exception("The test session didn't start.");
            string session = ProjectRun.Load().Session;
            string transcript = ProjectRun.TranscriptFile(transcripts, folder, session);
            Directory.CreateDirectory(Path.GetDirectoryName(transcript));

            if (ProjectRun.Answer("Use Nova.", true, lines, day.AddMinutes(1)) != ProjectRun.NoneWaiting || launched != 1) throw new Exception("An answer went in while nothing waited on him.");
            if (ProjectRun.Answer("  ", true, lines, day.AddMinutes(1)) == null || launched != 1) throw new Exception("An empty answer went in.");

            DateTime asked = day.AddMinutes(10);
            SmokeAsk(session, transcript, "Should I call it Starlight or Nova?", asked);
            if (ProjectRun.Answer("Use Nova.", false, lines, asked.AddMinutes(1)) != ProjectRun.Off || launched != 1) throw new Exception("An answer went in with the switch off.");
            if (!ProjectRun.Waiting().Contains("Starlight or Nova")) throw new Exception("What the session waits on wasn't said: " + ProjectRun.Waiting());

            game = true;
            string held = ProjectRun.Answer("Use Nova.", true, lines, asked.AddMinutes(2));
            if (held == null || !held.Contains("after the game") || launched != 1 || closed != 0 || ProjectRun.Load().Pending != "Use Nova.") throw new Exception("An answer during a game wasn't held: " + held);
            if (ProjectRun.DeliverPending(true, lines, asked.AddMinutes(3)) != null || launched != 1) throw new Exception("A held answer went in while the game still ran.");
            game = false;
            string passed = ProjectRun.DeliverPending(true, lines, asked.AddMinutes(30));
            int resumeAt = Array.IndexOf(last ?? new string[0], "--resume");
            if (passed == null || launched != 2 || closed != 1 || resumeAt < 0 || last[resumeAt + 1] != session || Array.IndexOf(last, "--session-id") >= 0) throw new Exception("The held answer didn't reopen the same session after the game: " + passed);
            string joined = string.Join(" ", last);
            if (!joined.Contains("--remote-control capri_run_demo") || !joined.Contains("--permission-mode plan") || Array.IndexOf(last, "--settings") < 0 || last[last.Length - 1] != ProjectRun.AnswerLead + "Use Nova.") throw new Exception("The reopened session lost Remote Control, its permission, her hooks or his words: " + joined);
            ProjectRunState after = ProjectRun.Load();
            if (after.Asked != 0 || after.Pending != "" || after.Pid != pid || after.Session != session || ProjectRun.DeliverPending(true, lines, asked.AddMinutes(31)) != null) throw new Exception("After his answer the session still counted as waiting, or went in twice.");

            DateTime again = asked.AddMinutes(40);
            SmokeAsk(session, transcript, "Shall I add tests too?", again);
            File.SetLastWriteTime(transcript, again.AddMinutes(3));
            string moved = ProjectRun.Answer("Yes.", true, lines, again.AddMinutes(5));
            if (moved == null || !moved.Contains("already went on") || launched != 2 || ProjectRun.Load().Asked != 0) throw new Exception("An answer went into a session he had already answered in its window: " + moved);

            DateTime third = again.AddMinutes(20);
            SmokeAsk(session, transcript, "Which colour?", third);
            ProjectHook.Answer(ProjectHook.Stop, "{\"session_id\":\"" + session + "\",\"last_assistant_message\":\"Done, I picked red.\"}", third.AddMinutes(1));
            ProjectRun.NoteAsked(session, third.Ticks);
            if (ProjectRun.Answer("Blue.", true, lines, third.AddMinutes(2)).IndexOf("already went on", StringComparison.Ordinal) < 0 || launched != 2) throw new Exception("An answer went in after the session had a newer turn.");
        }

        private static void SmokeAsk(string session, string transcript, string question, DateTime at)
        {
            ProjectHook.Answer(ProjectHook.Stop, "{\"session_id\":\"" + session + "\",\"last_assistant_message\":\"" + question + "\"}", at);
            File.WriteAllText(transcript, SmokeAssistant("end_turn") + "\n");
            File.SetLastWriteTime(transcript, at);
            ProjectRun.NoteAsked(session, at.Ticks);
        }
    }
}
