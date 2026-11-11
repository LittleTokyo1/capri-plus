using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeProjectWatch(List<string> checks)
        {
            SmokeWatchParts();
            string root = Path.Combine(Path.GetTempPath(), "capri-watch-smoke-" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "work", "demo");
            Func<string, string[], ProjectProcess> launch = ProjectRun.Launch;
            Func<int, long, bool> alive = ProjectRun.Alive;
            Action<int> shut = ProjectRun.Close;
            Func<bool> gaming = ProjectRun.Gaming;
            Func<string> hookExe = ProjectRun.HookExe;
            Func<bool> herOpen = ProjectHook.HerOpen;
            Func<string, string[], string> git = ProjectRun.Git;
            Func<ProjectEntry, string, DateTime, string> begin = ProjectWatch.Begin;
            string statePath = ProjectRun.StatePath, transcriptRoot = ProjectRun.TranscriptRoot, hookFolder = ProjectHook.Folder, watchPath = ProjectWatch.StatePath, vault = Vault.Root;
            try
            {
                Directory.CreateDirectory(folder);
                Directory.CreateDirectory(Path.Combine(root, "transcripts"));
                ProjectRun.StatePath = Path.Combine(root, "project-runs.json");
                ProjectRun.TranscriptRoot = Path.Combine(root, "transcripts");
                ProjectHook.Folder = Path.Combine(root, "hooks");
                ProjectWatch.StatePath = Path.Combine(root, "project-watch.json");
                Vault.Root = Path.Combine(root, "vault");
                ProjectRun.HookExe = delegate { return @"C:\x\Capri.exe"; };
                ProjectHook.HerOpen = delegate { return false; };
                ProjectRun.Close = delegate(int pid) { };
                SmokeWatchDaily();
                SmokeWatchReports(folder);
            }
            finally
            {
                ProjectRun.Launch = launch;
                ProjectRun.Alive = alive;
                ProjectRun.Close = shut;
                ProjectRun.Gaming = gaming;
                ProjectRun.HookExe = hookExe;
                ProjectHook.HerOpen = herOpen;
                ProjectRun.Git = git;
                ProjectWatch.Begin = begin;
                ProjectRun.StatePath = statePath;
                ProjectRun.TranscriptRoot = transcriptRoot;
                ProjectHook.Folder = hookFolder;
                ProjectWatch.StatePath = watchPath;
                Vault.Root = vault;
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
            checks.Add("PASS watch-only projects (097 slice 8, the Paro rule): a | watch line is never | full in any order, its sessions start, resume and refresh with only Read, Grep, Glob and AskUserQuestion as tools and no MCP servers (no edits, no commands, no web, so no server, no plan to approve), once a day with his projects switch on and never in a game, skipped when its HEAD didn't move, a refused start retries after an hour, its WATCH REPORT is told to him once and kept in Capri/projects in her vault, a session that ends without one is told too, his voice never takes the watch mark off, and the question judge has only Read, Grep and Glob as tools (his own allow list let it run commands before)");
        }

        private static void SmokeWatchParts()
        {
            List<ProjectEntry> entries = ProjectRun.Entries(new[] { @"C:\x\paroish | watch", @"C:\x\both | full | watch", @"C:\x\other | WATCH | full", @"C:\x\capri | full" });
            if (!entries[0].Watch || entries[0].Full || !entries[1].Watch || entries[1].Full || !entries[2].Watch || entries[2].Full || entries[3].Watch || !entries[3].Full) throw new Exception("A | watch line must be watch only and never | full, in any order.");
            string[] args = ProjectRun.Arguments(entries[1], ProjectWatch.Task, "s-1", "run", new DateTime(2026, 11, 5), @"C:\x\hooks.json");
            string joined = string.Join(" ", args);
            int tools = Array.IndexOf(args, "--allowedTools");
            int only = Array.IndexOf(args, "--tools");
            if (tools < 0 || Array.IndexOf(args, "dontAsk") < 0 || Array.IndexOf(args, "--strict-mcp-config") < 0 || only < 0 || args[only + 1] != "Read,Grep,Glob,AskUserQuestion" || only + 2 != tools || args[tools + ProjectWatch.Tools.Length + 1] != "--session-id") throw new Exception("A watch session must start in dontAsk with only its own tools (no MCP servers), each list ended by the next flag: " + joined);
            for (int i = 0; i < ProjectWatch.Tools.Length; i++) if (args[tools + 1 + i] != ProjectWatch.Tools[i]) throw new Exception("A watch session's allowed tools are wrong: " + joined);
            foreach (string banned in new[] { "Bash", "Edit", "Write", "NotebookEdit", "ExitPlanMode", "--dangerously-skip-permissions", "plan", "--disallowedTools" })
                if (Array.IndexOf(args, banned) >= 0) throw new Exception("A watch session may not get " + banned + ": " + joined);
            if (args[args.Length - 1] != ProjectWatch.Task || args[args.Length - 2] != @"C:\x\hooks.json" || Array.IndexOf(args, "--remote-control") < 0) throw new Exception("A watch session's task must come last, after its hooks, with Remote Control on: " + joined);
            string[] resumed = ProjectRun.ResumeArguments(entries[0], "s-1", "run", new DateTime(2026, 11, 5), @"C:\x\hooks.json", "yes");
            if (Array.IndexOf(resumed, "--resume") < 0 || Array.IndexOf(resumed, "dontAsk") < 0 || Array.IndexOf(resumed, "Bash") >= 0 || resumed[resumed.Length - 1] != ProjectRun.AnswerLead + "yes") throw new Exception("His answer must reopen a watch session still read only: " + string.Join(" ", resumed));
            if (!ProjectWatch.Task.Contains("never edit") || !ProjectWatch.Task.Contains("server") || !ProjectWatch.Task.Contains(ProjectWatch.Mark) || !ProjectRun.WrapWatch.Contains("Don't edit or run anything")) throw new Exception("The watch task and its wrap-up must forbid edits, commands and the server and ask for the report.");
            string[] judge = ProjectRelay.JudgeArguments("q", "s-2");
            int judgeTools = Array.IndexOf(judge, "--tools");
            if (judgeTools < 0 || judge[judgeTools + 1] != "Read,Grep,Glob" || Array.IndexOf(judge, "--strict-mcp-config") < 0 || Array.IndexOf(judge, "Bash") >= 0) throw new Exception("The question judge must have only Read, Grep and Glob as tools and no MCP servers: " + string.Join(" ", judge));
            string[] had = { @"C:\x\paroish | watch", @"C:\x\capri | full" };
            if (!Tuning.WatchDropped(had, new[] { @"C:\x\paroish", @"C:\x\capri | full" }) || !Tuning.WatchDropped(had, new[] { @"C:\x\paroish | watch", @"C:\x\paroish\", @"C:\x\capri | full" })) throw new Exception("His voice took the watch mark off a project.");
            if (Tuning.WatchDropped(had, new[] { @"C:\x\capri | full" }) || Tuning.WatchDropped(had, new[] { @"C:\x\paroish | watch", @"C:\x\capri | full", @"C:\x\new" })) throw new Exception("Removing a watch project or adding another one by voice was refused.");
        }

        private static void SmokeWatchDaily()
        {
            bool game = false;
            string head = "a000000000000000000000000000000000000001", refuse = null, task = null, started = null;
            int starts = 0;
            ProjectRun.Gaming = delegate { return game; };
            ProjectRun.Git = delegate(string where, string[] args) { return string.Join(" ", args).StartsWith("rev-parse", StringComparison.Ordinal) ? head : null; };
            ProjectWatch.Begin = delegate(ProjectEntry entry, string asked, DateTime when) { starts++; task = asked; started = entry.Folder; return refuse; };
            string[] lines = { @"C:\Work\capri | full", @"C:\Work\plain", @"C:\Work\rentals | watch" };
            DateTime day = new DateTime(2026, 11, 5, 10, 0, 0);
            if (ProjectWatch.Watch(false, lines, 3, day) != null || starts != 0) throw new Exception("A watch session started with his projects switch off.");
            game = true;
            if (ProjectWatch.Watch(true, lines, 3, day) != null || starts != 0) throw new Exception("A watch session started during a game.");
            game = false;
            if (ProjectWatch.Watch(true, new[] { @"C:\Work\capri | full", @"C:\Work\plain" }, 3, day) != null || starts != 0) throw new Exception("A project without | watch got a watch session.");
            string said = ProjectWatch.Watch(true, lines, 3, day);
            if (said == null || !said.Contains("read-only") || starts != 1 || started != @"C:\Work\rentals" || task != ProjectWatch.Task) throw new Exception("The daily watch didn't start one read-only session in the watch project: " + said);
            if (ProjectWatch.Watch(true, lines, 3, day.AddHours(4)) != null || starts != 1) throw new Exception("The watch ran twice in a day.");
            if (ProjectWatch.Watch(true, lines, 3, day.AddDays(1)) != null || starts != 1) throw new Exception("The watch spent a session on a project whose HEAD didn't move.");
            head = "a000000000000000000000000000000000000002";
            refuse = "a session I started is still running";
            if (ProjectWatch.Watch(true, lines, 3, day.AddDays(2)) != null || starts != 2) throw new Exception("A moved project wasn't tried the next day.");
            if (ProjectWatch.Watch(true, lines, 3, day.AddDays(2).AddMinutes(20)) != null || starts != 2) throw new Exception("A refused watch retried before an hour passed.");
            refuse = null;
            if (ProjectWatch.Watch(true, lines, 3, day.AddDays(2).AddMinutes(61)) == null || starts != 3) throw new Exception("A refused watch didn't start after an hour.");
            if (ProjectWatch.Watch(true, lines, 3, day.AddDays(2).AddMinutes(130)) != null || starts != 3) throw new Exception("A watch that started was marked as not done.");
        }

        private static void SmokeWatchReports(string folder)
        {
            List<string[]> launched = new List<string[]>();
            bool up = true;
            ProjectRun.Gaming = delegate { return false; };
            ProjectRun.Git = delegate(string where, string[] args) { return null; };
            ProjectRun.Launch = delegate(string where, string[] args) { launched.Add(args); return new ProjectProcess { Pid = 4242, Started = new DateTime(2026, 11, 5, 9, 0, 0) }; };
            ProjectRun.Alive = delegate(int pid, long since) { return up; };
            ProjectRun.TakeTold();
            string[] lines = { folder + " | watch" };
            DateTime now = new DateTime(2026, 11, 5, 10, 0, 0);
            string name;
            if (ProjectRun.Start(true, lines, 3, null, ProjectWatch.Task, now, out name) != null || launched.Count != 1 || Array.IndexOf(launched[0], "dontAsk") < 0 || !ProjectRun.Load().Watch) throw new Exception("A watch project's session didn't start read only.");
            string session = ProjectRun.Load().Session;
            ProjectRun.Reported(new HeardTurn { Folder = folder, Turn = new ProjectTurn { Kind = ProjectHook.Stop, Session = session, At = 100, Text = "Read the notes." } }, now);
            if (ProjectRun.TakeTold().Count != 0) throw new Exception("A watch turn without a report was told.");
            HeardTurn report = new HeardTurn { Folder = folder, Turn = new ProjectTurn { Kind = ProjectHook.Stop, Session = session, At = 200, Text = "Done reading.\n" + ProjectWatch.Mark + " It stands at step 3.\nNext: its tests.\nRisk: none." } };
            ProjectRun.Reported(report, now);
            List<string> told = ProjectRun.TakeTold();
            string kept = Path.Combine(Path.Combine(Path.Combine(Vault.Root, "Capri"), "projects"), "demo watch.md");
            if (told.Count != 1 || !told[0].StartsWith("Your demo watch: It stands at step 3. Next: its tests.", StringComparison.Ordinal) || !told[0].Contains("Capri/projects/demo watch.md") || !File.Exists(kept) || !File.ReadAllText(kept).Contains("## 2026-11-05 10:00") || !File.ReadAllText(kept).Contains("Risk: none.")) throw new Exception("A watch report wasn't told once and kept in her vault: " + string.Join(" / ", told.ToArray()));
            ProjectRun.Reported(report, now);
            ProjectRun.Reported(new HeardTurn { Folder = folder, Turn = new ProjectTurn { Kind = ProjectHook.Stop, Session = "someone-else", At = 300, Text = ProjectWatch.Mark + " not hers" } }, now);
            if (ProjectRun.TakeTold().Count != 0) throw new Exception("The same report was told twice, or another session's was told.");
            up = false;
            ProjectRun.Check(now.AddMinutes(5));
            if (ProjectRun.TakeTold().Count != 0 || ProjectRun.Load().Watch || ProjectRun.Load().Pid != 0) throw new Exception("A watch session that reported was told again when it closed, or kept its watch state.");
            up = true;
            if (ProjectRun.Start(true, lines, 3, null, ProjectWatch.Task, now.AddMinutes(10), out name) != null) throw new Exception("A second watch session didn't start.");
            up = false;
            ProjectRun.Check(now.AddMinutes(15));
            told = ProjectRun.TakeTold();
            if (told.Count != 1 || !told[0].Contains("demo watch session ended (closed) without its report")) throw new Exception("A watch session that closed without its report wasn't told: " + string.Join(" / ", told.ToArray()));
            ProjectRun.Start(true, new[] { folder }, 3, null, null, now.AddMinutes(20), out name);
            ProjectRun.Reported(new HeardTurn { Folder = folder, Turn = new ProjectTurn { Kind = ProjectHook.Stop, Session = ProjectRun.Load().Session, At = 400, Text = ProjectWatch.Mark + " plain" } }, now);
            ProjectRun.Check(now.AddMinutes(25));
            if (ProjectRun.TakeTold().Count != 0) throw new Exception("A plain read-and-plan session was treated as a watch.");
        }
    }
}
