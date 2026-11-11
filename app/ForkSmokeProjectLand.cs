using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeProjectLand(List<string> checks)
        {
            if (!Hands.Tools.Contains("\"name\":\"" + ProjectRun.LandTool + "\"") || Lockout.Handles(ProjectRun.LandTool)) throw new Exception("Her brain isn't offered the landing tool, or the game lockout refuses it (it holds a landing itself).");
            if (Brain.NeedsTools.IndexOf("land a branch a session left ready", StringComparison.Ordinal) < 0 || Brain.NeedsTools.IndexOf("Claude Code sessions", StringComparison.Ordinal) < 0) throw new Exception("Her chat brain doesn't know landing a branch or answering a session needs her tools, so \"land it\" could stay a chat line.");
            if (Did.Line("project session", "demo", "landed feature-x in main", null) != "landed in demo: feature-x in main" || Did.Line("project session", "demo", "said: hi", null) != null) throw new Exception("A landing isn't written to her action log, or a plain session line is.");
            string root = Path.Combine(Path.GetTempPath(), "capri-land-smoke-" + Guid.NewGuid().ToString("N"));
            string work = Path.Combine(root, "work");
            string transcripts = Path.Combine(root, "transcripts");
            Func<string, string[], ProjectProcess> launch = ProjectRun.Launch;
            Func<int, long, bool> alive = ProjectRun.Alive;
            Action<int> shut = ProjectRun.Close;
            Func<bool> gaming = ProjectRun.Gaming;
            Func<string> hookExe = ProjectRun.HookExe;
            Func<string, string[], string> git = ProjectRun.Git;
            Func<bool> herOpen = ProjectHook.HerOpen;
            string statePath = ProjectRun.StatePath, transcriptRoot = ProjectRun.TranscriptRoot, hookFolder = ProjectHook.Folder;
            try
            {
                Directory.CreateDirectory(Path.Combine(work, "demo"));
                Directory.CreateDirectory(Path.Combine(work, "paro"));
                Directory.CreateDirectory(transcripts);
                ProjectRun.StatePath = Path.Combine(root, "project-runs.json");
                ProjectRun.TranscriptRoot = transcripts;
                ProjectHook.Folder = Path.Combine(root, "hooks");
                ProjectRun.HookExe = delegate { return @"C:\x\Capri.exe"; };
                ProjectHook.HerOpen = delegate { return false; };
                ProjectRun.TakeTold();
                SmokeProjectLands(work, transcripts);
            }
            finally
            {
                ProjectRun.Launch = launch;
                ProjectRun.Alive = alive;
                ProjectRun.Close = shut;
                ProjectRun.Gaming = gaming;
                ProjectRun.Git = git;
                ProjectRun.StatePath = statePath;
                ProjectRun.TranscriptRoot = transcriptRoot;
                ProjectRun.HookExe = hookExe;
                ProjectHook.HerOpen = herOpen;
                ProjectHook.Folder = hookFolder;
                ProjectRun.TakeTold();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
            checks.Add("PASS landing on his yes (097 slice 5): a | full session that leaves a new or moved branch not yet in main is told to him once as ready to land (its commits, and any unlanded branches below it); only his own words land it (never her brain alone, a read-and-plan project or the switch off), held during a game or while a session runs, then a fresh | full session lands that exact tip the way the project's CLAUDE.md says and ends LANDED or NOT LANDED; she says landed only when git shows the tip in origin's main (else it stays ready), a moved tip asks again, no drops it, and her action log gets the landing");
        }

        private static void SmokeProjectLands(string work, string transcripts)
        {
            Dictionary<string, string> heads = new Dictionary<string, string> { { "main", "m1" }, { "old-work", "o1" } };
            List<string> inMain = new List<string> { "m1" };
            List<string> below = new List<string>();
            int launched = 0, closed = 0, pid = 8000, gitCalls = 0;
            bool game = false, running = true;
            string[] last = null;
            DateTime started = new DateTime(2026, 11, 6, 2, 0, 0);
            ProjectRun.Git = delegate(string folder, string[] args) { gitCalls++; return SmokeGit(heads, inMain, below, args); };
            ProjectRun.Launch = delegate(string folder, string[] arguments) { launched++; last = arguments; pid++; running = true; return new ProjectProcess { Pid = pid, Started = started }; };
            ProjectRun.Alive = delegate(int one, long when) { return running && one == pid; };
            ProjectRun.Close = delegate(int one) { closed++; running = false; };
            ProjectRun.Gaming = delegate { return game; };
            string demo = Path.Combine(work, "demo"), paro = Path.Combine(work, "paro");
            string[] lines = { demo + " | full", paro };
            string name;

            if (ProjectRun.Start(true, lines, 6, "paro", null, started, out name) != null) throw new Exception("The read-and-plan test session didn't start.");
            heads["paro-idea"] = "p1";
            running = false;
            if (ProjectRun.Check(started.AddMinutes(5)) != "closed" || ProjectRun.Load().Ready.Count != 0 || ProjectRun.TakeTold().Count != 0) throw new Exception("A read-and-plan project's branch was offered for landing.");
            heads.Remove("paro-idea");

            DateTime at = started.AddMinutes(10);
            heads["base-y"] = "b1";
            if (ProjectRun.Start(true, lines, 6, "demo", null, at, out name) != null) throw new Exception("The full test session didn't start.");
            heads["feature-x"] = "f1";
            below.Add("base-y");
            running = false;
            if (ProjectRun.Check(at.AddMinutes(40)) != "closed") throw new Exception("The full test session didn't end.");
            List<ProjectLanding> ready = ProjectRun.Load().Ready;
            List<string> told = ProjectRun.TakeTold();
            if (ready.Count != 1 || ready[0].Branch != "feature-x" || ready[0].Tip != "f1" || ready[0].Yes || told.Count != 1) throw new Exception("A branch her session left wasn't kept as ready to land (or an untouched old branch was): " + string.Join(" | ", told.ToArray()));
            if (!told[0].Contains("feature-x") || !told[0].Contains("2 commits") || !told[0].Contains("base-y") || !told[0].Contains("land it")) throw new Exception("The ready line didn't name the branch, its commits and the branch below it: " + told[0]);

            if (!ProjectRun.Land("land", null, null, true, lines, 6, at.AddMinutes(41)).Contains("your own") || ProjectRun.Load().Ready[0].Yes) throw new Exception("A landing started without his own words.");
            if (ProjectRun.Land("land", null, "land it", false, lines, 6, at.AddMinutes(41)) != ProjectRun.Off) throw new Exception("A landing started with the switch off.");
            if (ProjectRun.Land("land", null, "don't land it yet", true, lines, 6, at.AddMinutes(41)) != ProjectRun.NotHis || ProjectRun.Load().Ready[0].Yes) throw new Exception("His no was taken as a yes to land.");
            string[] planOnly = { demo, paro };
            if (!ProjectRun.Land("land", null, "land it", true, planOnly, 6, at.AddMinutes(41)).Contains("| full") || launched != 2) throw new Exception("A project that is read-and-plan now was landed.");

            game = true;
            string held = ProjectRun.Land("land", null, "yeah land it", true, lines, 6, at.AddMinutes(42));
            if (launched != 2 || !held.Contains("after the game") || !ProjectRun.Load().Ready[0].Yes) throw new Exception("His yes during a game wasn't held: " + held);
            ProjectRun.LandWaiting(true, lines, 6, at.AddMinutes(43));
            if (launched != 2) throw new Exception("A landing started during a game.");
            game = false;
            ProjectRun.LandWaiting(true, lines, 6, at.AddMinutes(50));
            ProjectRunState landing = ProjectRun.Load();
            string task = last[last.Length - 1];
            if (launched != 3 || landing.Landing != "feature-x" || landing.LandingTip != "f1" || Array.IndexOf(last, "--dangerously-skip-permissions") < 0) throw new Exception("The held yes didn't start a | full landing session after the game.");
            if (!task.Contains("feature-x") || !task.Contains("f1") || !task.Contains(ProjectRun.LandedMark) || !task.Contains("NOT " + ProjectRun.LandedMark) || !task.Contains("CLAUDE.md")) throw new Exception("The landing session's task didn't name the branch, its tip and how to end: " + task);

            string transcript = ProjectRun.TranscriptFile(transcripts, demo, landing.Session);
            Directory.CreateDirectory(Path.GetDirectoryName(transcript));
            File.WriteAllText(transcript, "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":1,\"cache_read_input_tokens\":" + (ProjectRun.ContextTokens + 10) + "}}}\n");
            at = at.AddMinutes(60);
            if (ProjectRun.Refresh(SmokeWrapTurn(landing.Session, demo, "Smoke is running.", at), true, lines, 6, at)) throw new Exception("A landing session was swapped for a fresh checkpoint session.");
            SmokeWrapReply(landing.Session);

            at = at.AddMinutes(5);
            if (ProjectRun.Landed(SmokeWrapTurn(landing.Session, demo, "I'll merge, push, then end with LANDED: and one line.", at), at) || ProjectRun.Load().Pid == 0) throw new Exception("The landing session was closed on a sentence that only names LANDED:.");
            SmokeWrapReply(landing.Session);
            at = at.AddMinutes(5);
            if (!ProjectRun.Landed(SmokeWrapTurn(landing.Session, demo, "All green.\nLANDED: merged feature-x into main and pushed.", at), at)) throw new Exception("The landing session's LANDED line wasn't taken.");
            told = ProjectRun.TakeTold();
            ProjectRunState after = ProjectRun.Load();
            if (closed != 1 || after.Pid != 0 || after.Landing != "" || after.Ready.Count != 1 || after.Ready[0].Yes || told.Count != 1 || !told[0].Contains("doesn't show")) throw new Exception("A LANDED claim git doesn't back was told as landed: " + string.Join(" | ", told.ToArray()));

            heads["feature-x"] = "f2";
            string moved = ProjectRun.Land("land", "feature-x", "land feature-x", true, lines, 6, at.AddMinutes(1));
            if (!moved.Contains("moved") || launched != 3 || ProjectRun.Load().Ready[0].Tip != "f2" || ProjectRun.Load().Ready[0].Yes) throw new Exception("A branch that moved since he was told was landed without asking again: " + moved);
            string now = ProjectRun.Land("land", "demo", "yes land it", true, lines, 6, at.AddMinutes(2));
            if (launched != 4 || !now.Contains("Landing") || ProjectRun.Load().LandingTip != "f2") throw new Exception("His yes with the slot free didn't land at once: " + now);
            inMain.Add("f2");
            heads.Remove("feature-x");
            at = at.AddMinutes(20);
            ProjectRun.Landed(SmokeWrapTurn(ProjectRun.Load().Session, demo, "LANDED: feature-x is in main, deployed, branch deleted.", at), at);
            told = ProjectRun.TakeTold();
            if (ProjectRun.Load().Ready.Count != 0 || told.Count != 1 || !told[0].StartsWith("Landed feature-x", StringComparison.Ordinal)) throw new Exception("A landing git backs wasn't told as landed: " + string.Join(" | ", told.ToArray()));

            heads["one"] = "a1";
            heads["two"] = "a2";
            below.Clear();
            if (ProjectRun.Start(true, lines, 6, "demo", null, at.AddMinutes(1), out name) != null) throw new Exception("The third test session didn't start.");
            heads["one"] = "a3";
            heads["two"] = "a4";
            running = false;
            ProjectRun.Check(at.AddMinutes(30));
            if (ProjectRun.Load().Ready.Count != 2 || ProjectRun.TakeTold().Count != 2) throw new Exception("Two branches her session moved weren't both kept as ready.");
            string which = ProjectRun.Land("land", null, "land it", true, lines, 6, at.AddMinutes(31));
            if (!which.Contains("one") || !which.Contains("two") || launched != 5) throw new Exception("With two ready she landed one without asking which: " + which);
            if (!ProjectRun.Land("status", null, null, true, lines, 6, at.AddMinutes(31)).Contains("two")) throw new Exception("Status didn't list what is ready.");
            ProjectRun.Land("no", "two", "no, not two", true, lines, 6, at.AddMinutes(32));
            if (ProjectRun.Load().Ready.Count != 1 || ProjectRun.Load().Ready[0].Branch != "one" || !heads.ContainsKey("two")) throw new Exception("His no didn't drop that branch from the list (or touched the branch).");
            heads["low"] = "l1";
            heads["high"] = "h1";
            below.Clear();
            below.Add("low");
            if (ProjectRun.Start(true, lines, 6, "demo", null, at.AddMinutes(40), out name) != null) throw new Exception("The stack test session didn't start.");
            heads["low"] = "l2";
            heads["high"] = "h2";
            running = false;
            ProjectRun.Check(at.AddMinutes(70));
            told = ProjectRun.TakeTold();
            if (ProjectRun.Load().Ready.Count != 3 || told.Count != 1 || !told[0].Contains("branch high") || !told[0].Contains("moved in this session too")) throw new Exception("A rebased stack gave one ready line per branch instead of one for its top: " + string.Join(" | ", told.ToArray()));
            if (gitCalls == 0) throw new Exception("Git was never asked.");
        }

        private static string SmokeGit(Dictionary<string, string> heads, List<string> inMain, List<string> below, string[] args)
        {
            string line = string.Join(" ", args);
            if (line == "symbolic-ref --quiet --short refs/remotes/origin/HEAD") return "origin/main";
            if (line == "rev-parse --verify --quiet origin/main") return "m1";
            if (line.StartsWith("rev-parse --verify --quiet refs/heads/", StringComparison.Ordinal))
            {
                string branch = line.Substring(38);
                return heads.ContainsKey(branch) ? heads[branch] : null;
            }
            if (line.StartsWith("for-each-ref", StringComparison.Ordinal))
            {
                List<string> all = new List<string>();
                foreach (KeyValuePair<string, string> one in heads) all.Add(one.Key + " " + one.Value);
                return string.Join("\n", all.ToArray());
            }
            if (line.StartsWith("merge-base --is-ancestor ", StringComparison.Ordinal)) return inMain.Contains(args[2]) ? "" : null;
            if (line.StartsWith("cherry ", StringComparison.Ordinal)) return inMain.Contains(args[2]) ? "" : "+ " + args[2];
            if (line.StartsWith("rev-list --count ", StringComparison.Ordinal)) return "2";
            if (line.StartsWith("log ", StringComparison.Ordinal)) return "feat: the thing\nfix: a typo";
            if (line.StartsWith("branch --merged ", StringComparison.Ordinal)) return args[2] + (below.Count > 0 ? "\n" + string.Join("\n", below.ToArray()) : "");
            return null;
        }
    }
}
