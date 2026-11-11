using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeProjectRun(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.RunProjects || fresh.Projects == null || fresh.Projects.Length != 0 || fresh.ProjectsPerDay != ProjectRun.StandardPerDay) throw new Exception("Running his projects should be off with no projects until he sets them.");
            string close;
            Tunable found = Tuning.Find("run my projects", "on", out close);
            if (found == null || found.Field != "RunProjects" || found.Locked != null || !found.Toggle) throw new Exception("Running his projects must be switched on and off by voice (his rule: every setting by voice).");
            Tunable folders = Tuning.Find("project folders", "add C:\\Work\\demo", out close);
            if (Array.IndexOf(Tuning.Internal, "Projects") >= 0 || folders == null || folders.Field != "Projects" || folders.Guard == null) throw new Exception("His project folders must change by voice, and only on his own words (his rule: every setting by voice).");
            SmokeProjectParts();

            string root = Path.Combine(Path.GetTempPath(), "capri-projects-smoke-" + Guid.NewGuid().ToString("N"));
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
                SmokeProjectRuns(folder, transcripts);
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
            checks.Add("PASS run my projects (097 slice 1): off with no projects by default and switched by voice too, reads the projects list (read and plan unless | full), the Max-month model, Remote Control and its own session id, starts one minimised session at a time and never with the switch off, an unlisted project or a game, keeps a daily cap, closes a session that's done and quiet " + ProjectRun.QuietMinutes + " minutes but not one that's waiting on him, frees the slot when its window is closed, reads the real end of a turn, and on the real plan limit error closes the session and starts none until the next day (not when a file only mentions it, and an old limit line never marks the next day)");
        }

        private static void SmokeProjectParts()
        {
            List<ProjectEntry> entries = ProjectRun.Entries(new[] { "  ", "# mine", @"C:\x\capri | full", "\"C:\\x\\other\"" });
            if (entries.Count != 2 || !entries[0].Full || entries[1].Full || entries[0].Name != "capri" || entries[1].Folder != @"C:\x\other") throw new Exception("The projects list wasn't read right.");
            ProjectEntry named = ProjectRun.Find(new[] { @"C:\x\capri", @"C:\x\other" }, "OTHER");
            if (named == null || named.Folder != @"C:\x\other" || ProjectRun.Find(new[] { @"C:\x\capri" }, "nope") != null || ProjectRun.Find(new[] { @"C:\x\capri" }, null).Name != "capri") throw new Exception("A project wasn't found by its name.");
            if (ProjectRun.ModelFor(new DateTime(2026, 11, 30, 23, 0, 0)) != ClaudeJob.Model || ProjectRun.ModelFor(new DateTime(2026, 12, 1)) != "opusplan") throw new Exception("The session model didn't follow the start script's Max month.");
            string[] plan = ProjectRun.Arguments(entries[1], null, "s-1", "run-1", new DateTime(2026, 11, 5), @"C:\h\hooks.json");
            string[] full = ProjectRun.Arguments(entries[0], "Do the next step.", "s-2", "run-2", new DateTime(2026, 11, 5), null);
            string planned = string.Join(" ", plan), freed = string.Join(" ", full);
            if (planned.Contains("--dangerously-skip-permissions") || !planned.Contains("--permission-mode plan")) throw new Exception("A project without | full must only read and plan: " + planned);
            if (!freed.Contains("--dangerously-skip-permissions") || freed.Contains("--permission-mode")) throw new Exception("A | full project didn't get edit rights: " + freed);
            if (!planned.Contains("--effort high") || !planned.Contains("--session-id s-1") || !planned.Contains("--remote-control run-1") || !planned.Contains("--name run-1") || !planned.Contains(@"--settings C:\h\hooks.json") || freed.Contains("--settings") || plan[plan.Length - 1] != ProjectRun.Task || full[full.Length - 1] != "Do the next step.") throw new Exception("The session arguments were wrong: " + planned);
            if (ProjectRun.TranscriptFile(@"C:\r", @"C:\x\my.repo", "abc") != @"C:\r\C--x-my-repo\abc.jsonl") throw new Exception("The session's transcript file wasn't found: " + ProjectRun.TranscriptFile(@"C:\r", @"C:\x\my.repo", "abc"));
            if (!ProjectRun.LimitHit("{\"content\":[{\"type\":\"text\",\"text\":\"You've hit your 5-hour limit · resets 12:40am (Europe/Amsterdam)\"}],\"apiError\":\"usage_limit_reached\",\"apiErrorParams\":{}}")) throw new Exception("The real plan limit line wasn't seen.");
            if (ProjectRun.LimitHit("{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"content\":\"notes: You've hit your 5-hour limit; \\\"apiError\\\":\\\"usage_limit_reached\\\"\"}]}}") || ProjectRun.LimitHit("{\"text\":\"Smoke limit is 900 seconds.\"}")) throw new Exception("A file that only mentions the plan limit stopped sessions for the day.");
            DateTime now = new DateTime(2026, 11, 5, 12, 0, 0);
            string ended = SmokeAssistant("end_turn") + "\n";
            if (!ProjectRun.Finished(ended, now.AddMinutes(-25), now) || ProjectRun.Finished(ended, now.AddMinutes(-5), now)) throw new Exception("A done session wasn't closed after the quiet time, or was closed too soon.");
            if (ProjectRun.Finished(ended + "{\"type\":\"user\",\"message\":{\"content\":\"one more thing\"}}\n", now.AddMinutes(-25), now)) throw new Exception("A session he wrote to again was closed.");
            if (ProjectRun.Finished(SmokeAssistant("tool_use") + "\n", now.AddMinutes(-25), now)) throw new Exception("A session waiting on him was closed.");
            if (!ProjectRun.Finished(ended + "{\"type\":\"assistant\",\"isSidechain\":true,\"message\":{\"stop_reason\":\"tool_use\"}}\n{\"type\":\"system\"}\n", now.AddMinutes(-25), now)) throw new Exception("A helper's line kept a done session open.");
            string real = ended + "{\"type\":\"attachment\",\"isSidechain\":false}\n{\"type\":\"system\",\"subtype\":\"stop_hook_summary\",\"isSidechain\":false}\n{\"type\":\"system\",\"subtype\":\"turn_duration\",\"isMeta\":false,\"isSidechain\":false}\n{\"type\":\"cost-state\"}\n{\"type\":\"last-prompt\"}\n";
            if (!ProjectRun.Finished(real, now.AddMinutes(-25), now)) throw new Exception("The real end of a turn (hook summary, turn time and cost lines after it) wasn't read as done.");
        }

        private static void SmokeProjectRuns(string folder, string transcripts)
        {
            int launched = 0, closed = 0;
            bool running = true, game = false;
            string[] last = null;
            ProjectRun.Launch = delegate(string where, string[] arguments) { launched++; last = arguments; return new ProjectProcess { Pid = 4242, Started = new DateTime(2026, 11, 5, 9, 0, 0) }; };
            ProjectRun.Alive = delegate(int pid, long started) { return running && pid == 4242 && started == new DateTime(2026, 11, 5, 9, 0, 0).Ticks; };
            ProjectRun.Close = delegate(int pid) { closed++; running = false; };
            ProjectRun.Gaming = delegate { return game; };
            string[] lines = { folder };
            DateTime day = new DateTime(2026, 11, 5, 9, 0, 0);
            string name;

            if (ProjectRun.Start(false, lines, 2, null, null, day, out name) != ProjectRun.Off || launched != 0) throw new Exception("A session started with the switch off.");
            if (ProjectRun.Start(true, lines, 2, "elsewhere", null, day, out name) == null || launched != 0) throw new Exception("A session started in a project that isn't listed.");
            game = true;
            if (ProjectRun.Start(true, lines, 2, null, null, day, out name) != ProjectRun.GameOn || launched != 0) throw new Exception("A session started during a game.");
            game = false;
            if (ProjectRun.Start(true, lines, 2, null, null, day, out name) != null || launched != 1 || name != "demo") throw new Exception("A session didn't start when everything allowed it.");
            if (ProjectRun.Start(true, lines, 2, null, null, day, out name) != ProjectRun.Busy || launched != 1) throw new Exception("A second session started while one runs.");

            string transcript = ProjectRun.TranscriptFile(transcripts, folder, ProjectRun.Load().Session);
            if (Array.IndexOf(last, ProjectRun.Load().Session) < 0) throw new Exception("The session didn't get the id she follows.");
            int hooksAt = Array.IndexOf(last, "--settings");
            if (hooksAt < 0 || !File.Exists(last[hooksAt + 1]) || !File.ReadAllText(last[hooksAt + 1]).Contains("--project-hook")) throw new Exception("Her session didn't get her hooks.");
            string first = ProjectRun.Load().Session;
            ProjectHook.Answer(ProjectHook.Stop, "{\"session_id\":\"" + first + "\",\"last_assistant_message\":\"Plan ready. Shall I start?\"}", day.AddMinutes(10));
            ProjectRun.Check(day.AddMinutes(11));
            long heard = ProjectRun.Load().Heard;
            ProjectRun.Check(day.AddMinutes(12));
            if (heard == 0 || ProjectRun.Load().Heard != heard || ProjectHook.Turn(first) == null) throw new Exception("What her session said wasn't heard once.");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript));
            File.WriteAllText(transcript, SmokeAssistant("end_turn") + "\n");
            File.SetLastWriteTime(transcript, day.AddMinutes(30));
            if (ProjectRun.Check(day.AddMinutes(35)) != null || closed != 0) throw new Exception("A session that just finished its turn was closed before the quiet time.");
            ProjectRun.NoteAsked(first, day.AddMinutes(10).Ticks);
            if (ProjectRun.Check(day.AddMinutes(55)) != null || closed != 0) throw new Exception("A session waiting on his answer was closed after the quiet time.");
            ProjectHook.Answer(ProjectHook.Stop, "{\"session_id\":\"" + first + "\",\"last_assistant_message\":\"Done with step 2.\"}", day.AddMinutes(20));
            File.SetLastWriteTime(transcript, day.AddMinutes(30));
            if (ProjectRun.Check(day.AddMinutes(56)) != "finished" || closed != 1 || ProjectRun.Load().Pid != 0) throw new Exception("A done, quiet session wasn't closed once it went past his question.");
            if (ProjectHook.Turn(first) != null || ProjectRun.Load().Heard != 0) throw new Exception("A closed session's hook files were kept.");

            running = true;
            if (ProjectRun.Start(true, lines, 2, null, null, day.AddHours(1), out name) != null || launched != 2) throw new Exception("The next session didn't start after the first was done.");
            running = false;
            if (ProjectRun.Check(day.AddHours(1).AddMinutes(1)) != "closed" || ProjectRun.Load().Pid != 0) throw new Exception("A window he closed kept the slot.");
            running = true;
            string capped = ProjectRun.Start(true, lines, 2, null, null, day.AddHours(2), out name);
            if (capped == null || !capped.Contains("2 sessions today") || launched != 2) throw new Exception("The daily cap didn't hold: " + capped);

            DateTime next = day.AddDays(1);
            if (ProjectRun.Start(true, lines, 2, null, null, next, out name) != null || launched != 3) throw new Exception("A new day didn't reset the cap.");
            transcript = ProjectRun.TranscriptFile(transcripts, folder, ProjectRun.Load().Session);
            Directory.CreateDirectory(Path.GetDirectoryName(transcript));
            File.WriteAllText(transcript, "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"stop_sequence\",\"content\":[{\"type\":\"text\",\"text\":\"You've hit your 5-hour limit\"}]},\"apiError\":\"usage_limit_reached\"}\n");
            int shutBefore = closed;
            if (ProjectRun.Check(next.AddMinutes(5)) != "limit" || ProjectRun.Load().StoppedDay != "2026-11-06" || ProjectRun.Load().Pid != 0 || closed != shutBefore + 1) throw new Exception("The plan limit with its window still open didn't close it, free the slot and stop new sessions for the day.");
            running = true;
            if (ProjectRun.Start(true, lines, 5, null, null, next.AddHours(3), out name) != ProjectRun.LimitToday || launched != 3) throw new Exception("A session started after the plan limit was hit today.");
            if (ProjectRun.Start(true, lines, 5, null, null, next.AddDays(1), out name) != null || launched != 4) throw new Exception("The day after the limit, sessions didn't start again.");
            SmokeProjectOwnAndCap(folder, next.AddDays(1));
            if (ProjectRun.Check(next.AddDays(1).AddMinutes(1)) != null || ProjectRun.Load().StoppedDay != "2026-11-06") throw new Exception("An old limit line marked the next day too.");
        }

        private static void SmokeProjectOwnAndCap(string folder, DateTime day)
        {
            string current = ProjectRun.Load().Session;
            string judged = Guid.NewGuid().ToString();
            ProjectRelay.NoteJudge(judged, DateTime.Now);
            if (!ProjectRun.Hers(current) || !ProjectRun.Hers(judged) || ProjectRun.Hers(Guid.NewGuid().ToString()) || ProjectRun.Hers(null)) throw new Exception("Code mode can't tell her own sessions and judge runs from his.");
            Func<string, string, string> judge = ProjectRelay.Judge;
            try
            {
                ProjectRelay.Judge = delegate(string where, string prompt) { return "ANSWER: Yes, go on.\nSOURCE: CLAUDE.md"; };
                for (int i = 0; i <= ProjectRun.MostAutoAnswers; i++)
                {
                    ProjectHook.Answer(ProjectHook.Stop, "{\"session_id\":\"" + current + "\",\"last_assistant_message\":\"Step " + i + " is done. Shall I go on with step " + (i + 1) + "?\"}", day.AddMinutes(i + 1));
                    string line = ProjectRelay.Handle(new HeardTurn { Folder = folder, Turn = ProjectHook.Turn(current) });
                    if (i < ProjectRun.MostAutoAnswers && line != null) throw new Exception("Routine answer " + (i + 1) + " in a row went to him.");
                    if (i == ProjectRun.MostAutoAnswers && line == null) throw new Exception("After " + ProjectRun.MostAutoAnswers + " answers in a row she kept answering on her own.");
                }
                string same = "{\"session_id\":\"" + current + "\",\"last_assistant_message\":\"Shall I go on with the next step?\"}";
                ProjectHook.Answer(ProjectHook.Stop, same, day.AddMinutes(30));
                if (ProjectRelay.Handle(new HeardTurn { Folder = folder, Turn = ProjectHook.Turn(current) }) != null) throw new Exception("After his turn the count didn't start again.");
                ProjectHook.Answer(ProjectHook.Stop, same, day.AddMinutes(31));
                if (ProjectRelay.Handle(new HeardTurn { Folder = folder, Turn = ProjectHook.Turn(current) }) == null) throw new Exception("The same question answered twice in a row didn't go to him.");
            }
            finally { ProjectRelay.Judge = judge; }
        }

        private static string SmokeAssistant(string stop)
        {
            return "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"" + stop + "\",\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}}";
        }
    }
}
