using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeProjectRefresh(List<string> checks)
        {
            string big = "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":12,\"cache_read_input_tokens\":" + ProjectRun.ContextTokens + ",\"cache_creation_input_tokens\":3000,\"output_tokens\":900}}}";
            string helper = "{\"type\":\"assistant\",\"isSidechain\":true,\"message\":{\"usage\":{\"input_tokens\":5,\"cache_read_input_tokens\":999999}}}";
            if (ProjectRun.ContextUsed(big + "\n" + helper + "\n{\"type\":\"system\"}\n") != ProjectRun.ContextTokens + 3012 || ProjectRun.ContextUsed("{\"type\":\"user\"}") != 0 || ProjectRun.ContextUsed(null) != 0) throw new Exception("The session's context size wasn't read from its last own reply.");
            string root = Path.Combine(Path.GetTempPath(), "capri-fresh-smoke-" + Guid.NewGuid().ToString("N"));
            string work = Path.Combine(root, "work");
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
                Directory.CreateDirectory(Path.Combine(work, "demo"));
                Directory.CreateDirectory(Path.Combine(work, "paro"));
                Directory.CreateDirectory(transcripts);
                ProjectRun.StatePath = Path.Combine(root, "project-runs.json");
                ProjectRun.TranscriptRoot = transcripts;
                ProjectHook.Folder = Path.Combine(root, "hooks");
                ProjectRun.HookExe = delegate { return @"C:\x\Capri.exe"; };
                ProjectHook.HerOpen = delegate { return false; };
                SmokeProjectRefreshes(work, transcripts, big);
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
            checks.Add("PASS fresh sessions (097 slice 4): when her session's context passes " + ProjectRun.ContextTokens + " tokens at the end of a turn she asks it to wrap up (a | full one updates its checkpoint and commits, a read-and-plan one edits nothing) and end with HANDOFF:, then closes it and starts a fresh session in the same project from its checkpoint plus that handoff; never for a question (the relay first), a small context, a game, the daily cap or the switch off, and a wrap-up without a handoff leaves the session as it is");
        }

        private static void SmokeProjectRefreshes(string work, string transcripts, string big)
        {
            int launched = 0, closed = 0, pid = 7000;
            bool game = false;
            string[] last = null;
            string where = null;
            DateTime started = new DateTime(2026, 11, 5, 9, 0, 0);
            ProjectRun.Launch = delegate(string folder, string[] arguments) { launched++; last = arguments; where = folder; pid++; return new ProjectProcess { Pid = pid, Started = started }; };
            ProjectRun.Alive = delegate(int one, long when) { return one == pid; };
            ProjectRun.Close = delegate(int one) { closed++; };
            ProjectRun.Gaming = delegate { return game; };
            string demo = Path.Combine(work, "demo"), paro = Path.Combine(work, "paro");
            string[] lines = { demo + " | full", paro };
            string name;
            if (ProjectRun.Start(true, lines, 3, "demo", null, started, out name) != null || launched != 1) throw new Exception("The test session didn't start.");
            string session = ProjectRun.Load().Session;
            string transcript = ProjectRun.TranscriptFile(transcripts, demo, session);
            Directory.CreateDirectory(Path.GetDirectoryName(transcript));

            DateTime at = started.AddMinutes(30);
            File.WriteAllText(transcript, SmokeAssistant("end_turn") + "\n");
            if (ProjectRun.Refresh(SmokeWrapTurn(session, demo, "Step 3 is done.", at), true, lines, 3, at) || SmokeWrapReply(session) != null) throw new Exception("A session with a small context was asked to wrap up.");
            File.WriteAllText(transcript, big + "\n");
            at = at.AddMinutes(1);
            if (ProjectRun.Refresh(SmokeWrapTurn(session, demo, "Shall I go on with step 4?", at), true, lines, 3, at)) throw new Exception("A question was taken for a wrap-up before the relay saw it.");
            at = at.AddMinutes(1);
            game = true;
            if (ProjectRun.Refresh(SmokeWrapTurn(session, demo, "Step 4 is done.", at), true, lines, 3, at)) throw new Exception("A wrap-up was asked during a game.");
            game = false;
            if (ProjectRun.Refresh(SmokeWrapTurn(session, demo, "Step 4 is done.", at), false, lines, 3, at)) throw new Exception("A wrap-up was asked with the switch off.");
            if (ProjectRun.Refresh(SmokeWrapTurn(session, demo, "Step 4 is done.", at), true, lines, 1, at)) throw new Exception("A wrap-up was asked when the daily cap allows no fresh session.");

            at = at.AddMinutes(1);
            if (!ProjectRun.Refresh(SmokeWrapTurn(session, demo, "Step 4 is done.", at), true, lines, 3, at)) throw new Exception("A full context at the end of a turn wasn't wrapped up.");
            string asked = SmokeWrapReply(session);
            if (asked == null || !asked.Contains(ProjectRun.HandoffMark) || !asked.Contains("commit") || ProjectRun.Load().Wrapping != at.Ticks || closed != 0) throw new Exception("The wrap-up ask wasn't sent to a | full session: " + asked);
            at = at.AddMinutes(5);
            if (!ProjectRun.Refresh(SmokeWrapTurn(session, demo, "Checkpoint updated and committed.\nHANDOFF: steps 1 to 4 done; next: step 5, the settings page.", at), true, lines, 3, at)) throw new Exception("The handoff wasn't taken.");
            ProjectRunState fresh = ProjectRun.Load();
            string task = last[last.Length - 1];
            if (closed != 1 || launched != 2 || where != demo || fresh.Session == session || fresh.Wrapping != 0 || fresh.Started != 2 || Array.IndexOf(last, fresh.Session) < 0 || Array.IndexOf(last, "--resume") >= 0) throw new Exception("The old session wasn't closed and a fresh one started in the same project.");
            if (!task.StartsWith(ProjectRun.Task, StringComparison.Ordinal) || !task.Contains("step 5, the settings page") || task.Contains("Checkpoint updated") || !string.Join(" ", last).Contains("--dangerously-skip-permissions")) throw new Exception("The fresh session didn't get the checkpoint task plus the handoff, or lost its rights: " + task);

            ProjectRun.Close(pid);
            pid++;
            string second;
            if (ProjectRun.Check(at.AddMinutes(1)) != "closed" || ProjectRun.Start(true, lines, 5, "paro", null, at.AddMinutes(2), out second) != null) throw new Exception("The read-and-plan test session didn't start.");
            string plan = ProjectRun.Load().Session;
            string planTranscript = ProjectRun.TranscriptFile(transcripts, paro, plan);
            Directory.CreateDirectory(Path.GetDirectoryName(planTranscript));
            File.WriteAllText(planTranscript, big + "\n");
            at = at.AddMinutes(10);
            if (!ProjectRun.Refresh(SmokeWrapTurn(plan, paro, "Read the notes.", at), true, lines, 5, at)) throw new Exception("A read-and-plan session with a full context wasn't wrapped up.");
            string planAsk = SmokeWrapReply(plan);
            if (planAsk == null || planAsk.Contains("commit") || !planAsk.Contains("Don't edit")) throw new Exception("A read-and-plan session was asked to edit or commit: " + planAsk);
            at = at.AddMinutes(1);
            int before = launched;
            if (ProjectRun.Refresh(SmokeWrapTurn(plan, paro, "I read more notes instead.", at), true, lines, 5, at) || ProjectRun.Load().Wrapping != 0 || launched != before || ProjectRun.Load().Session != plan) throw new Exception("A wrap-up answered without a handoff still swapped the session.");
        }

        private static HeardTurn SmokeWrapTurn(string session, string folder, string said, DateTime at)
        {
            ProjectHook.Answer(ProjectHook.Stop, "{\"session_id\":\"" + session + "\",\"last_assistant_message\":" + new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(said) + "}", at);
            return new HeardTurn { Folder = folder, Turn = ProjectHook.Turn(session) };
        }

        private static string SmokeWrapReply(string session)
        {
            string path = Path.Combine(ProjectHook.Folder, session + ".reply.json");
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path);
            File.Delete(path);
            return text;
        }
    }
}
