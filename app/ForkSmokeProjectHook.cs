using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeProjectHook(List<string> checks)
        {
            string folder = ProjectHook.Folder;
            Action<int> sleep = ProjectHook.Sleep;
            Func<bool> herOpen = ProjectHook.HerOpen;
            string root = Path.Combine(Path.GetTempPath(), "capri-hook-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ProjectHook.Folder = root;
                ProjectHook.HerOpen = delegate { return true; };
                SmokeHookStop();
                SmokeHookAsk();
                SmokeHookPlan();
                SmokeHookSettings();
            }
            finally
            {
                ProjectHook.Folder = folder;
                ProjectHook.Sleep = sleep;
                ProjectHook.HerOpen = herOpen;
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
            checks.Add("PASS session hooks (097 slice 2): her sessions get a Stop hook and an AskUserQuestion hook that run Capri.exe " + ProjectHook.Flag + "; each turn is written for her (what the session said, or its questions with their options), a reply she left goes in as the session's next message or as the picked answers (echoing the questions), nothing she didn't write is answered (no reply, wrong count, a blank answer, a bad session id, a late reply meant for an older turn), a plan (ExitPlanMode) is written for her but never approved by her, and the hook only waits while she says she is answering and is running");
        }

        private static void SmokeHookStop()
        {
            string id = "0000aaaa-1111-2222-3333-444455556666";
            DateTime now = new DateTime(2026, 11, 5, 10, 0, 0);
            string stop = "{\"session_id\":\"" + id + "\",\"stop_hook_active\":false,\"last_assistant_message\":\"Plan ready. Shall I start on step 2?\"}";
            if (ProjectHook.Answer(ProjectHook.Stop, "{\"session_id\":\"..\\\\..\\\\x\",\"last_assistant_message\":\"hi\"}", now) != null || (Directory.Exists(ProjectHook.Folder) && Directory.GetFiles(ProjectHook.Folder).Length != 0)) throw new Exception("A bad session id was written or answered.");
            if (ProjectHook.Answer(ProjectHook.Stop, "not json", now) != null || ProjectHook.Answer("other", stop, now) != null) throw new Exception("A broken hook call was answered.");
            if (ProjectHook.Answer(ProjectHook.Stop, stop, now) != null) throw new Exception("A stop was answered with no reply from her.");
            ProjectTurn turn = ProjectHook.Turn(id);
            if (turn == null || turn.Kind != ProjectHook.Stop || turn.Text != "Plan ready. Shall I start on step 2?" || turn.At != now.Ticks) throw new Exception("What the session said wasn't written for her.");
            if (ProjectRun.Told(turn) != "said: Plan ready.") throw new Exception("Her log line for a turn was wrong: " + ProjectRun.Told(turn));
            ProjectHook.Reply(id, now.Ticks, "Yes, start step 2 and commit on the branch.", null);
            string output = ProjectHook.Answer(ProjectHook.Stop, stop, now);
            Dictionary<string, object> parsed = new JavaScriptSerializer().DeserializeObject(output ?? "{}") as Dictionary<string, object>;
            if (parsed == null || !"block".Equals(parsed["decision"]) || !"Yes, start step 2 and commit on the branch.".Equals(parsed["reason"])) throw new Exception("Her reply didn't go in as the next message: " + output);
            if (ProjectHook.Answer(ProjectHook.Stop, stop, now) != null) throw new Exception("One reply was used twice.");
            ProjectHook.Reply(id, now.AddMinutes(-5).Ticks, "Yes, merge it.", null);
            if (ProjectHook.Answer(ProjectHook.Stop, stop, now) != null || File.Exists(Path.Combine(ProjectHook.Folder, id + ".reply.json"))) throw new Exception("A late reply meant for an older turn went into the next one.");
            ProjectHook.Expect(id, true);
            ProjectHook.HerOpen = delegate { return false; };
            int idle = 0;
            ProjectHook.Sleep = delegate(int ms) { idle++; };
            if (ProjectHook.Answer(ProjectHook.Stop, stop, now) != null || idle != 0) throw new Exception("The hook waited for her while she isn't running.");
            ProjectHook.HerOpen = delegate { return true; };
            ProjectHook.Expect(id, false);
            if (!ProjectHook.Current(id, now.Ticks) || ProjectHook.Current(id, now.AddSeconds(1).Ticks)) throw new Exception("Which turn is current wasn't told apart.");

            int slept = 0;
            ProjectHook.Expect(id, true);
            ProjectHook.Sleep = delegate(int ms) { slept++; if (slept == 2) ProjectHook.Reply(id, now.Ticks, "Go on.", null); };
            output = ProjectHook.Answer(ProjectHook.Stop, stop, now);
            if (output == null || !output.Contains("Go on.") || slept != 2) throw new Exception("While she said she is answering, the hook didn't wait for her reply.");
            ProjectHook.Expect(id, false);
            slept = 0;
            if (ProjectHook.Answer(ProjectHook.Stop, stop, now) != null || slept != 0) throw new Exception("The hook waited although she isn't answering.");
            ProjectHook.Forget(id);
            if (ProjectHook.Turn(id) != null) throw new Exception("Forget kept the turn.");
        }

        private static void SmokeHookAsk()
        {
            string id = "0000bbbb-1111-2222-3333-444455556666";
            DateTime now = new DateTime(2026, 11, 5, 11, 0, 0);
            string ask = "{\"session_id\":\"" + id + "\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"Which branch?\",\"header\":\"Branch\",\"multiSelect\":false,\"options\":[{\"label\":\"New branch\",\"description\":\"d\"},{\"label\":\"Main\",\"description\":\"d\"}]},{\"question\":\"Run smoke?\",\"header\":\"Smoke\",\"multiSelect\":false,\"options\":[{\"label\":\"Yes\",\"description\":\"d\"},{\"label\":\"No\",\"description\":\"d\"}]}]}}";
            if (ProjectHook.Answer(ProjectHook.Ask, ask, now) != null) throw new Exception("A question was answered with no reply from her.");
            ProjectTurn turn = ProjectHook.Turn(id);
            if (turn == null || turn.Kind != ProjectHook.Ask || turn.Questions.Length != 2 || turn.Questions[1] != "Run smoke?" || turn.Options[0] != "New branch / Main") throw new Exception("The session's questions weren't written for her.");
            if (ProjectRun.Told(turn) != "asks: Which branch? (New branch / Main) | Run smoke? (Yes / No)") throw new Exception("Her log line for questions was wrong: " + ProjectRun.Told(turn));
            ProjectHook.Reply(id, now.Ticks, null, new[] { "New branch" });
            if (ProjectHook.Answer(ProjectHook.Ask, ask, now) != null) throw new Exception("Answers of the wrong count were used.");
            ProjectHook.Reply(id, now.Ticks, null, new[] { "New branch", " " });
            if (ProjectHook.Answer(ProjectHook.Ask, ask, now) != null) throw new Exception("A blank answer was used.");
            ProjectHook.Reply(id, now.Ticks, null, new[] { "New branch", "Yes" });
            string output = ProjectHook.Answer(ProjectHook.Ask, ask, now);
            Dictionary<string, object> parsed = new JavaScriptSerializer().DeserializeObject(output ?? "{}") as Dictionary<string, object>;
            Dictionary<string, object> specific = parsed == null || !parsed.ContainsKey("hookSpecificOutput") ? null : parsed["hookSpecificOutput"] as Dictionary<string, object>;
            Dictionary<string, object> updated = specific == null ? null : specific["updatedInput"] as Dictionary<string, object>;
            Dictionary<string, object> answers = updated == null ? null : updated["answers"] as Dictionary<string, object>;
            IList questions = updated == null ? null : updated["questions"] as IList;
            if (specific == null || !"PreToolUse".Equals(specific["hookEventName"]) || !"allow".Equals(specific["permissionDecision"]) || answers == null || !"New branch".Equals(answers["Which branch?"]) || !"Yes".Equals(answers["Run smoke?"]) || questions == null || questions.Count != 2) throw new Exception("Her answers didn't go in as picked options with the questions echoed: " + output);
            ProjectHook.Forget(id);
        }

        private static void SmokeHookPlan()
        {
            string id = "0000cccc-1111-2222-3333-444455556666";
            DateTime now = new DateTime(2026, 11, 5, 12, 0, 0);
            string plan = "{\"session_id\":\"" + id + "\",\"tool_name\":\"ExitPlanMode\",\"tool_input\":{\"plan\":\"# Plan\\nStep 1: read the notes.\",\"planFilePath\":\"C:\\\\x\\\\plan.md\"}}";
            ProjectHook.Reply(id, now.Ticks, "Approve it.", new[] { "Yes" });
            if (ProjectHook.Answer(ProjectHook.Ask, plan, now) != null) throw new Exception("A plan was approved by her; a read-and-plan session must only leave plan mode on his yes.");
            ProjectTurn turn = ProjectHook.Turn(id);
            if (turn == null || turn.Kind != ProjectHook.Plan || turn.Text != "# Plan\nStep 1: read the notes.") throw new Exception("The session's plan wasn't written for her.");
            if (!ProjectRun.Told(turn).StartsWith("plans (waits for your yes): ", StringComparison.Ordinal)) throw new Exception("Her log line for a plan was wrong: " + ProjectRun.Told(turn));
            ProjectHook.Forget(id);
        }

        internal static void SmokeProjectRelay(List<string> checks)
        {
            string folder = ProjectHook.Folder;
            Func<string, string, string> judge = ProjectRelay.Judge;
            string root = Path.Combine(Path.GetTempPath(), "capri-relay-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                ProjectHook.Folder = root;
                SmokeRelayParts();
                SmokeRelayHandle(root);
            }
            finally
            {
                ProjectHook.Folder = folder;
                ProjectRelay.Judge = judge;
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
            checks.Add("PASS question relay (097 slice 3): a session's question, ask or plan goes to a read-only judge of the project's own notes; a routine answer goes back with its source (exact option labels only), a real decision, a plan or anything about the lockout, updater, keys, spending, buying, security, merging, deploying, releases or a server goes to him (also when the answer itself turns risky), the hook is always released (also when the judge fails), a stale turn gets no reply, and a session waiting on him isn't closed");
        }

        private static void SmokeRelayParts()
        {
            ProjectTurn stop = new ProjectTurn { Session = "0000dddd-1111-2222-3333-444455556666", Kind = ProjectHook.Stop, Text = "I read the notes. Step 1 is done. Shall I start step 2 on the branch?", At = 1 };
            ProjectTurn quiet = new ProjectTurn { Kind = ProjectHook.Stop, Text = "Step 2 is done and pushed." };
            ProjectTurn ask = new ProjectTurn { Kind = ProjectHook.Ask, Questions = new[] { "Which branch?", "Run smoke?" }, Options = new[] { "New branch / Main", "Yes / No" } };
            ProjectTurn plan = new ProjectTurn { Kind = ProjectHook.Plan, Text = "# Plan\nStep 1" };
            if (!ProjectRelay.Needs(stop) || ProjectRelay.Needs(quiet) || !ProjectRelay.Needs(ask) || !ProjectRelay.Needs(plan)) throw new Exception("What needs an answer wasn't told apart.");
            if (ProjectRelay.Asked(stop) != "Shall I start step 2 on the branch?") throw new Exception("The session's question wasn't picked out: " + ProjectRelay.Asked(stop));
            if (!ProjectRelay.Risky("Shall I merge it into main?") || !ProjectRelay.Risky("deploy now?") || ProjectRelay.Risky("Shall I rename Foo to Bar?")) throw new Exception("Always-ask topics weren't told apart.");
            RelayVerdict said = ProjectRelay.Read("Checked the notes.\nANSWER: Yes, start step 2.\nSOURCE: docs/notes/Task.md", stop);
            if (said.Ask || said.Text != ProjectRelay.AnswerLead + "docs/notes/Task.md): Yes, start step 2." || said.Source != "docs/notes/Task.md") throw new Exception("A routine answer didn't go back with its source: " + said.Text);
            if (!ProjectRelay.Read("ANSWER: Yes", stop).Ask) throw new Exception("An answer without a source was used.");
            if (!ProjectRelay.Read("ANSWER: yes, then merge to main and push\nSOURCE: x.md", stop).Ask) throw new Exception("A risky answer was used.");
            RelayVerdict asked = ProjectRelay.Read("ASK_HIM: Start step 2 now or after the smoke?", stop);
            if (!asked.Ask || asked.Text != "Start step 2 now or after the smoke?") throw new Exception("A question for him wasn't kept: " + asked.Text);
            RelayVerdict picked = ProjectRelay.Read("**ANSWER: new branch || Yes**\nSOURCE: CLAUDE.md", ask);
            if (picked.Ask || picked.Answers.Length != 2 || picked.Answers[0] != "New branch" || picked.Answers[1] != "Yes") throw new Exception("Picked options weren't matched to their exact labels.");
            if (!ProjectRelay.Read("ANSWER: Purple || Yes\nSOURCE: CLAUDE.md", ask).Ask || !ProjectRelay.Read("ANSWER: New branch\nSOURCE: CLAUDE.md", ask).Ask) throw new Exception("A made-up option or a missing answer was used.");
            if (ProjectRelay.Read("ANSWER: New branch, Main || No\nSOURCE: CLAUDE.md", ask).Answers[0] != "New branch, Main") throw new Exception("Several labels for one question weren't kept.");
            int judged = 0;
            ProjectRelay.Judge = delegate(string where, string prompt) { judged++; return "ANSWER: Yes\nSOURCE: CLAUDE.md"; };
            if (!ProjectRelay.Decide(@"C:\x", plan).Ask || !ProjectRelay.Decide(@"C:\x", new ProjectTurn { Kind = ProjectHook.Stop, Text = "Ready. Shall I deploy it?" }).Ask || judged != 0) throw new Exception("A plan or a risky question went to the judge instead of to him.");
            if (!ProjectRelay.Prompt(ask).Contains("[New branch / Main]") || !ProjectRelay.Prompt(stop).Contains("Shall I start step 2")) throw new Exception("The judge wasn't given the question.");
        }

        private static void SmokeRelayHandle(string root)
        {
            string id = "0000eeee-1111-2222-3333-444455556666";
            DateTime now = new DateTime(2026, 11, 5, 13, 0, 0);
            string folder = Path.Combine(root, "demo");
            string stop = "{\"session_id\":\"" + id + "\",\"transcript_path\":\"C:\\\\x\\\\t.jsonl\",\"last_assistant_message\":\"Step 1 is done. Shall I start step 2?\"}";
            ProjectHook.Answer(ProjectHook.Stop, stop, now);
            ProjectTurn turn = ProjectHook.Turn(id);
            if (turn.Transcript != @"C:\x\t.jsonl") throw new Exception("The turn didn't keep its transcript for the judge.");
            ProjectRelay.Judge = delegate(string where, string prompt) { return "ANSWER: Yes, start step 2.\nSOURCE: CLAUDE.md"; };
            if (ProjectRelay.Handle(new HeardTurn { Folder = folder, Turn = turn }) != null) throw new Exception("A routine answer was sent to him.");
            string output = ProjectHook.Answer(ProjectHook.Stop, stop, now);
            if (output == null || !output.Contains("source: CLAUDE.md") || !output.Contains("Yes, start step 2.")) throw new Exception("Her routine answer didn't reach the session: " + output);

            ProjectHook.Answer(ProjectHook.Stop, stop, now.AddMinutes(1));
            ProjectRelay.Judge = delegate(string where, string prompt) { return "ASK_HIM: Start step 2 now?"; };
            string line = ProjectRelay.Handle(new HeardTurn { Folder = folder, Turn = ProjectHook.Turn(id) });
            if (line != "Your demo session asks: Start step 2 now? Answer it in its window or on your phone.") throw new Exception("A question for him wasn't told to him: " + line);
            if (!File.Exists(Path.Combine(ProjectHook.Folder, id + ".reply.json")) || ProjectHook.Answer(ProjectHook.Stop, stop, now.AddMinutes(1)) != null) throw new Exception("A question for him didn't release the hook with no answer.");

            ProjectHook.Answer(ProjectHook.Stop, stop, now.AddMinutes(2));
            ProjectRelay.Judge = delegate(string where, string prompt) { throw new InvalidOperationException("judge broke"); };
            line = ProjectRelay.Handle(new HeardTurn { Folder = folder, Turn = ProjectHook.Turn(id) });
            if (line == null || !File.Exists(Path.Combine(ProjectHook.Folder, id + ".reply.json"))) throw new Exception("A failed judge didn't release the hook and ask him.");
            ProjectHook.Answer(ProjectHook.Stop, stop, now.AddMinutes(2));

            ProjectTurn old = ProjectHook.Turn(id);
            ProjectHook.Answer(ProjectHook.Stop, stop, now.AddMinutes(3));
            ProjectRelay.Judge = delegate(string where, string prompt) { return "ANSWER: Yes\nSOURCE: CLAUDE.md"; };
            ProjectRelay.Handle(new HeardTurn { Folder = folder, Turn = old });
            if (File.Exists(Path.Combine(ProjectHook.Folder, id + ".reply.json"))) throw new Exception("A stale turn got a reply.");
            ProjectHook.Forget(id);
        }

        private static void SmokeHookSettings()
        {
            string path = ProjectHook.WriteSettings(@"C:\x y\Capri.exe");
            Dictionary<string, object> parsed = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
            Dictionary<string, object> hooks = parsed == null ? null : parsed["hooks"] as Dictionary<string, object>;
            if (hooks == null || !hooks.ContainsKey("Stop") || !hooks.ContainsKey("PreToolUse")) throw new Exception("The hooks file has no Stop or PreToolUse hook.");
            Dictionary<string, object> pre = (hooks["PreToolUse"] as IList)[0] as Dictionary<string, object>;
            Dictionary<string, object> preHook = (pre["hooks"] as IList)[0] as Dictionary<string, object>;
            Dictionary<string, object> stopHook = (((hooks["Stop"] as IList)[0] as Dictionary<string, object>)["hooks"] as IList)[0] as Dictionary<string, object>;
            if (!"AskUserQuestion|ExitPlanMode".Equals(pre["matcher"]) || !"\"C:\\x y\\Capri.exe\" --project-hook ask".Equals(preHook["command"]) || !"\"C:\\x y\\Capri.exe\" --project-hook stop".Equals(stopHook["command"]) || !ProjectHook.HookTimeout.Equals(stopHook["timeout"])) throw new Exception("The hooks file doesn't run her: " + File.ReadAllText(path));
        }
    }
}
