using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeProjectPropose(List<string> checks)
        {
            string root = Path.Combine(Path.GetTempPath(), "capri-propose-smoke-" + Guid.NewGuid().ToString("N"));
            Func<string, string[], ProjectProcess> launch = ProjectRun.Launch;
            Func<int, long, bool> alive = ProjectRun.Alive;
            Action<int> shut = ProjectRun.Close;
            Func<bool> gaming = ProjectRun.Gaming;
            Func<string> hookExe = ProjectRun.HookExe;
            Func<bool> herOpen = ProjectHook.HerOpen;
            Func<string, string[], string> git = ProjectRun.Git;
            Func<ProjectEntry, string, DateTime, string> begin = ProjectPropose.Begin;
            Func<string, bool> add = ProjectPropose.AddProject;
            string statePath = ProjectRun.StatePath, transcriptRoot = ProjectRun.TranscriptRoot, hookFolder = ProjectHook.Folder, vault = Vault.Root;
            string proposals = ProjectPropose.StatePath, folderPath = ProjectPropose.FolderPath, homeRoot = ProjectPropose.HomeRoot;
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "transcripts"));
                Directory.CreateDirectory(Path.Combine(root, "home"));
                ProjectRun.StatePath = Path.Combine(root, "project-runs.json");
                ProjectRun.TranscriptRoot = Path.Combine(root, "transcripts");
                ProjectHook.Folder = Path.Combine(root, "hooks");
                ProjectPropose.StatePath = Path.Combine(root, "project-proposals.json");
                ProjectPropose.FolderPath = Path.Combine(root, "proposals");
                ProjectPropose.HomeRoot = Path.Combine(root, "home");
                ProjectRun.HookExe = delegate { return @"C:\x\Capri.exe"; };
                ProjectHook.HerOpen = delegate { return false; };
                ProjectRun.Close = delegate(int pid) { };
                SmokeProposeParts();
                Vault.Root = null;
                if (ProjectPropose.Run("list", null, null, true, DateTime.Now) != ProjectPropose.NoVault) throw new Exception("Proposals without a vault didn't say so.");
                Vault.Root = Path.Combine(root, "vault");
                SmokeProposeQueue();
                SmokeProposeReports(root);
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
                ProjectPropose.Begin = begin;
                ProjectPropose.AddProject = add;
                ProjectRun.StatePath = statePath;
                ProjectRun.TranscriptRoot = transcriptRoot;
                ProjectHook.Folder = hookFolder;
                Vault.Root = vault;
                ProjectPropose.StatePath = proposals;
                ProjectPropose.FolderPath = folderPath;
                ProjectPropose.HomeRoot = homeRoot;
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
            checks.Add("PASS new project proposals (097 slice 9, propose only): only on his own ask and with his projects switch on, at most " + ProjectPropose.MostWaiting + " waiting; one session at a time researches it with only web search, web fetch and questions (no file tools, no commands, no MCP), never in a game, a refused start retries after an hour; its PROPOSAL is told once and kept in Capri/proposals in her vault; start it on his own yes (never in a game) makes only a folder with a fixed CLAUDE.md, the plan in docs/PLAN.md, git init and a plain (never | full) project line; a taken folder or an unsafe name makes nothing; no drops one; a session that ends without a proposal is told");
        }

        private static void SmokeProposeParts()
        {
            string[] args = ProjectRun.Arguments(ProjectPropose.Entry(), ProjectPropose.Task("x"), "s-1", "run", new DateTime(2026, 11, 5), @"C:\x\hooks.json");
            string joined = string.Join(" ", args);
            int only = Array.IndexOf(args, "--tools"), allowed = Array.IndexOf(args, "--allowedTools");
            if (only < 0 || args[only + 1] != "WebSearch,WebFetch,AskUserQuestion" || allowed != only + 2 || args[allowed + ProjectPropose.Tools.Length + 1] != "--session-id" || Array.IndexOf(args, "dontAsk") < 0 || Array.IndexOf(args, "--strict-mcp-config") < 0) throw new Exception("A proposal session must have only web search, web fetch and questions, no MCP servers, each list ended by the next flag: " + joined);
            foreach (string banned in new[] { "Read", "Grep", "Glob", "Bash", "Edit", "Write", "--dangerously-skip-permissions", "plan" })
                if (Array.IndexOf(args, banned) >= 0) throw new Exception("A proposal session may not get " + banned + ": " + joined);
            if (args[args.Length - 1] != ProjectPropose.Task("x")) throw new Exception("A proposal session's task must come last: " + joined);
            ProjectEntry found = ProjectRun.Find(new string[0], ProjectPropose.Folder().ToUpperInvariant() + "\\");
            if (found == null || !found.Propose || found.Full || found.Watch) throw new Exception("Her proposals folder must be found as a proposal project (for his answers and fresh sessions).");
            if (ProjectRun.Find(new[] { @"C:\Work\demo" }, @"C:\Work\demo").Propose) throw new Exception("A listed project was taken as a proposal.");
            string task = ProjectPropose.Task("a \"lawn\" app");
            if (!task.Contains("'lawn'") || !task.Contains(ProjectPropose.Mark) || !task.Contains("never make, buy, sign up for or publish") || !ProjectPropose.Task("").Contains("your pick") || !ProjectPropose.Again.Contains(ProjectPropose.Mark)) throw new Exception("The proposal task lacks his topic, the mark or the never-make rule: " + task);
            if (ProjectPropose.Slug(@"..\..\Evil Name!!") != "evil-name" || ProjectPropose.Slug(new string('a', 60)).Length > ProjectPropose.NameChars || ProjectPropose.Slug("") != "new-project") throw new Exception("A proposal's folder name isn't made safe in code.");
        }

        private static void SmokeProposeQueue()
        {
            bool game = false;
            int starts = 0;
            string task = null, refuse = "a session I started is still running";
            ProjectEntry entry = null;
            ProjectRun.Gaming = delegate { return game; };
            ProjectPropose.Begin = delegate(ProjectEntry one, string asked, DateTime when) { starts++; entry = one; task = asked; return refuse; };
            DateTime now = new DateTime(2026, 11, 5, 10, 0, 0);
            if (ProjectPropose.Run("propose", "a lawn app", null, true, now) != ProjectPropose.NotHis) throw new Exception("A proposal started without his own words.");
            if (!ProjectPropose.Run("propose", "a lawn app", "think up a lawn app project", false, now).Contains("off in Capri+")) throw new Exception("A proposal was queued with his projects switch off.");
            string said = ProjectPropose.Run("propose", "a lawn app", "think up a lawn app project", true, now);
            if (!said.Contains("about a lawn app") || !said.Contains("Nothing gets made without your yes") || ProjectPropose.Load().Waiting.Count != 1) throw new Exception("His ask wasn't queued: " + said);
            ProjectPropose.Run("propose", "", "come up with something to make money", true, now);
            ProjectPropose.Run("propose", "a game", "a game project idea please", true, now);
            if (!ProjectPropose.Run("propose", "more", "and another one", true, now).Contains("already have 3") || ProjectPropose.Load().Waiting.Count != 3) throw new Exception("More than " + ProjectPropose.MostWaiting + " proposals were queued.");
            ProjectPropose.Watch(false, 3, now);
            game = true;
            ProjectPropose.Watch(true, 3, now);
            game = false;
            if (starts != 0) throw new Exception("A proposal session started with the switch off or in a game.");
            if (ProjectPropose.Watch(true, 3, now) != null) throw new Exception("A busy slot was told as a used-up day.");
            ProjectPropose.Watch(true, 3, now.AddMinutes(20));
            if (starts != 1 || ProjectPropose.Load().Waiting.Count != 3) throw new Exception("A refused proposal start was dropped or retried before an hour passed.");
            refuse = "I already started 3 sessions today";
            string capped = ProjectPropose.Watch(true, 3, now.AddMinutes(61));
            if (capped == null || !capped.Contains("starts tomorrow") || ProjectPropose.Watch(true, 3, now.AddMinutes(122)) != null || starts != 3) throw new Exception("A proposal waiting on the used-up day wasn't told once: " + capped);
            now = now.AddMinutes(122);
            refuse = null;
            ProjectPropose.Watch(true, 3, now.AddMinutes(61));
            if (starts != 4 || entry == null || !entry.Propose || !task.Contains("'a lawn app'") || ProjectPropose.Load().Waiting.Count != 2 || !Directory.Exists(ProjectPropose.Folder())) throw new Exception("The first waiting proposal didn't start in her proposals folder.");
            ProjectPropose.Begin = null;
        }

        private static void SmokeProposeReports(string root)
        {
            List<string[]> launched = new List<string[]>();
            List<string> gits = new List<string>(), added = new List<string>();
            bool up = true, game = false;
            ProjectRun.Gaming = delegate { return game; };
            ProjectRun.Git = delegate(string where, string[] args) { gits.Add(where + " " + string.Join(" ", args)); return ""; };
            ProjectPropose.AddProject = delegate(string line) { added.Add(line); return true; };
            ProjectRun.Launch = delegate(string where, string[] args) { launched.Add(args); return new ProjectProcess { Pid = 4343, Started = new DateTime(2026, 11, 5, 9, 0, 0) }; };
            ProjectRun.Alive = delegate(int pid, long since) { return up; };
            ProjectRun.TakeTold();
            DateTime now = new DateTime(2026, 11, 5, 11, 0, 0);
            string name;
            if (ProjectRun.Start(true, new string[0], 3, ProjectPropose.Folder(), ProjectPropose.Task(""), now, out name) != null || Array.IndexOf(launched[0], "WebSearch,WebFetch,AskUserQuestion") < 0 || !ProjectRun.Load().Propose) throw new Exception("A proposal session didn't start with only web tools.");
            string session = ProjectRun.Load().Session;
            ProjectRun.Reported(new HeardTurn { Folder = ProjectPropose.Folder(), Turn = new ProjectTurn { Kind = ProjectHook.Stop, Session = session, At = 100, Text = "WATCH REPORT: not a proposal" } }, now);
            if (ProjectRun.TakeTold().Count != 0) throw new Exception("A proposal session's turn without PROPOSAL was told.");
            HeardTurn turn = new HeardTurn { Folder = ProjectPropose.Folder(), Turn = new ProjectTurn { Kind = ProjectHook.Stop, Session = session, At = 200, Text = "Looked around.\n" + ProjectPropose.Mark + " **Lawn Bot**\nA bot that books lawn jobs for neighbours.\nFirst steps: a form, a calendar, a price list." } };
            ProjectRun.Reported(turn, now);
            ProjectRun.Reported(turn, now);
            List<string> told = ProjectRun.TakeTold();
            string list = Path.Combine(Path.Combine(Vault.Root, "Capri"), "proposals.md");
            string one = Path.Combine(Path.Combine(Path.Combine(Vault.Root, "Capri"), "proposals"), "2026-11-05 lawn-bot.md");
            if (told.Count != 1 || !told[0].StartsWith("I have a new project proposal for you (1): Lawn Bot: A bot that books lawn jobs", StringComparison.Ordinal) || !File.Exists(list) || !File.ReadAllText(list).Contains("[[2026-11-05 lawn-bot|Lawn Bot]]") || !File.Exists(one)) throw new Exception("A proposal wasn't told once and kept in her vault: " + string.Join(" / ", told.ToArray()));
            up = false;
            ProjectRun.Check(now.AddMinutes(5));
            if (ProjectRun.TakeTold().Count != 0 || ProjectRun.Load().Propose) throw new Exception("A proposal session that proposed was told again when it closed.");
            up = true;
            ProjectRun.Start(true, new string[0], 3, ProjectPropose.Folder(), ProjectPropose.Task("x"), now.AddMinutes(10), out name);
            up = false;
            ProjectRun.Check(now.AddMinutes(15));
            told = ProjectRun.TakeTold();
            if (told.Count != 1 || !told[0].Contains("proposal session ended (closed) without a proposal")) throw new Exception("A proposal session that closed without a proposal wasn't told: " + string.Join(" / ", told.ToArray()));
            if (!ProjectPropose.Run("list", null, null, true, now).Contains("1. Lawn Bot")) throw new Exception("Her proposals list doesn't name it.");
            if (ProjectPropose.Run("start", "lawn bot", null, true, now) != ProjectPropose.NotHis) throw new Exception("A proposal was made without his own yes.");
            game = true;
            if (!ProjectPropose.Run("start", "lawn bot", "yes start the lawn bot", true, now).Contains("after your game")) throw new Exception("A proposal was made during a game.");
            game = false;
            string folder = Path.Combine(Path.Combine(root, "home"), "lawn-bot");
            if (!ProjectPropose.Run("start", "the second one", "start the second one", true, now).StartsWith("None of my proposals", StringComparison.Ordinal)) throw new Exception("A proposal that isn't there was picked by its place.");
            string made = ProjectPropose.Run("start", "the first one", "yes start the first one", true, now);
            string claude = File.Exists(Path.Combine(folder, "CLAUDE.md")) ? File.ReadAllText(Path.Combine(folder, "CLAUDE.md")) : "";
            string plan = File.Exists(Path.Combine(Path.Combine(folder, "docs"), "PLAN.md")) ? File.ReadAllText(Path.Combine(Path.Combine(folder, "docs"), "PLAN.md")) : "";
            if (!made.Contains("plan only") || !made.Contains("No GitHub repo") || claude.Length == 0 || claude.Contains("neighbours") || !plan.Contains("books lawn jobs for neighbours") || gits.Count != 1 || gits[0] != folder + " init -q" || added.Count != 1 || added[0] != folder) throw new Exception("Start it didn't make only the folder, a fixed CLAUDE.md, the plan, git init and a plain project line: " + made + " / " + string.Join(" | ", gits.ToArray()) + " / " + string.Join(" | ", added.ToArray()));
            if (!ProjectPropose.Run("start", "1", "yes start 1", true, now).Contains("already made") || !ProjectPropose.Run("no", "1", "no not that one", true, now).Contains("already made")) throw new Exception("A made proposal was made again or dropped.");
            ProjectPropose.Keep("Lawn Bot\nThe same idea again.", now);
            if (!ProjectPropose.Run("start", "the latest one", "yes start the latest one", true, now).Contains("already a lawn-bot folder") || gits.Count != 1 || added.Count != 1) throw new Exception("A proposal whose folder is taken still made something, or the latest one wasn't picked.");
            if (!ProjectPropose.Run("start", "the first one", "start the first one", true, now).Contains("already a lawn-bot folder")) throw new Exception("The first open proposal wasn't picked by its place (a made one doesn't count).");
            if (!ProjectPropose.Run("no", "2", "no drop number 2", true, now).StartsWith("Dropped", StringComparison.Ordinal) || ProjectPropose.Run("list", null, null, true, now).Contains("2.")) throw new Exception("No didn't drop a proposal.");
        }
    }
}
