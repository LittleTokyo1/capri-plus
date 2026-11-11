using System;
using System.Collections.Generic;
using System.Drawing;

namespace CapriDesktop
{
    // Capri+ smoke checks for her working memory: the thread, the guard that never swaps her answer for a refusal, and cut-off voice lines.
    internal sealed partial class Companion
    {
        private const string SmokeGoal = "go to Netflix and pick my profile";

        private static string SmokeCall(string task) { return "{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"function\":{\"name\":\"use_computer\",\"arguments\":{\"task\":\"" + task + "\"}}}]},\"done_reason\":\"stop\"}"; }

        private static string SmokeSay(string text) { return "{\"message\":{\"role\":\"assistant\",\"content\":\"" + text + "\"},\"done_reason\":\"stop\"}"; }

        private void SmokeThread(List<string> checks)
        {
            SmokeThreadState(checks);
            SmokeThreadTurns(checks);
            SmokeThreadGuard(checks);
            SmokeThreadSafety(checks);
            SmokeThreadJobs(checks);
            SmokeCutoff(checks);
        }

        private static void SmokeThreadState(List<string> checks)
        {
            DateTime clock = new DateTime(2026, 10, 29, 1, 0, 0, DateTimeKind.Utc);
            Func<DateTime> keptNow = Pursuit.Now;
            Pursuit.Now = delegate { return clock; };
            try
            {
                Pursuit work = new Pursuit(SmokeGoal);
                work.Step("job open Netflix: Five profiles are showing. Which one is yours?");
                string note = work.Note();
                if (!note.Contains(SmokeGoal) || !note.Contains("Five profiles are showing")) throw new Exception("The thread note lost the goal or a step: " + note);
                if (work.Continues("open Spotify and play some lofi") || !work.Continues("press the X") || !work.Continues("pick it again") || !work.Continues("pick the profile on Netflix for me")) throw new Exception("A line was matched to the wrong goal.");
                work.Open = "Which one is yours?";
                if (!work.Continues("mine starts with a T")) throw new Exception("An answer to her question did not continue the thread.");
                if (!Pursuit.Dropping("never mind") || !Pursuit.Dropping("forget it") || Pursuit.Dropping("never mind the lag, press the X")) throw new Exception("Dropping the goal was read wrong.");
                for (int i = 0; i < 30; i++) work.Step("step " + i);
                if (work.Steps.Count != Pursuit.MostSteps || work.Steps[work.Steps.Count - 1] != "step 29") throw new Exception("The thread kept too many steps or dropped the newest.");
                if (work.Stale) throw new Exception("A fresh thread was stale.");
                clock = clock.AddMinutes(Pursuit.IdleMinutes + 1);
                if (!work.Stale) throw new Exception("An idle thread never expired.");
            }
            finally { Pursuit.Now = keptNow; }
            checks.Add("PASS the thread: it keeps her goal in his words and the steps done, carries his short lines and answers to her question, lets a clearly new request replace it, drops on \"never mind\", keeps the newest twelve steps and expires after ten idle minutes");
        }

        private static void SmokeThreadTurns(List<string> checks)
        {
            string keptOllama = Brain.Ollama;
            List<string> asked = new List<string>();
            System.Net.Sockets.TcpListener fake;
            Brain.Ollama = FakeServer(new[]
            {
                SmokeCall("open Netflix and pick the profile"), SmokeSay("Which profile is yours?"),
                SmokeCall("pick the profile that starts with T"), SmokeSay("T profile's in."),
                SmokeSay("chat"), SmokeSay("On it, pressing the X now."), SmokeSay("X is closed."),
                SmokeSay("Okay, dropped it.")
            }, asked, out fake);
            try
            {
                List<string> jobs = new List<string>();
                List<string> backgrounds = new List<string>();
                List<bool> carryFlags = new List<bool>();
                Brain brain = new Brain();
                brain.Model = "thread-test";
                brain.Act = delegate(string tool, ToolArguments args)
                {
                    jobs.Add(tool + "|" + args.Task + "|" + args.Asked);
                    backgrounds.Add(Brain.Briefing ?? "");
                    carryFlags.Add(Brain.Carrying);
                    return jobs.Count == 1 ? "Five profiles are showing. Which one is yours?" : jobs.Count == 2 ? "Picked the T profile." : "Pressed the X.";
                };
                string first = brain.Ask(SmokeGoal);
                string second = brain.Ask("mine starts with a T");
                string third = brain.Ask("press the X");
                if (first != "Which profile is yours?" || second != "T profile's in." || third != "X is closed.") throw new Exception("The thread turns were answered wrong: " + first + " / " + second + " / " + third + " / " + string.Join(" ; ", jobs.ToArray()));
                if (jobs.Count != 3) throw new Exception("The thread ran the wrong number of jobs: " + string.Join(" ; ", jobs.ToArray()));
                if (!backgrounds[1].Contains(SmokeGoal) || !backgrounds[1].Contains("Which one is yours?") || !backgrounds[1].Contains("Five profiles are showing")) throw new Exception("The second job did not get the goal, her question and the first result: " + backgrounds[1]);
                if (!backgrounds[2].Contains(SmokeGoal) || !jobs[2].Contains("press the X") || jobs[2].Contains(SmokeGoal) || jobs[2].Contains("haven't done that yet")) throw new Exception("The promise without a tool did not become a job that knows the goal without carrying it in its task: " + jobs[2]);
                if (carryFlags[0] || !carryFlags[1] || !carryFlags[2]) throw new Exception("Carried turns were not told apart from fresh ones: " + string.Join(",", carryFlags.ToArray()));
                if (brain.ThreadNote().Contains("waiting for the answer")) throw new Exception("The thread kept waiting for an answer after a turn with no question.");
                string[] sent = asked.ToArray();
                if (sent.Length != 7 || !sent[2].Contains("Which one is yours?") || !sent[2].Contains("Work in progress") || !sent[2].Contains(SmokeGoal)) throw new Exception("The answer to her question reached her brain without the thread: " + (sent.Length > 2 ? sent[2] : "(missing)") + " / requests " + sent.Length);
                if (!brain.ThreadNote().Contains("Picked the T profile")) throw new Exception("A job result was not written back to the thread.");
                string dropped = brain.Ask("never mind");
                if (dropped != "Okay, dropped it." || brain.ThreadNote().Length != 0) throw new Exception("Never mind did not close the thread: " + dropped);
            }
            finally
            {
                fake.Stop();
                Brain.Ollama = keptOllama;
            }
            checks.Add("PASS working memory: his answer to her question reaches her brain and the job with the goal, her question, the earlier result and the recent talk, every job result is written back, a promise with no tool becomes a job that knows the goal, and \"never mind\" closes the thread");
        }

        private static void SmokeThreadGuard(List<string> checks)
        {
            string keptOllama = Brain.Ollama;
            List<string> asked = new List<string>();
            System.Net.Sockets.TcpListener fake;
            Brain.Ollama = FakeServer(new[] { SmokeSay("chat"), SmokeSay("On it, pressing the X now."), SmokeSay("chat"), SmokeSay("I'll go find the cover one next."), SmokeSay("chat"), SmokeSay("chat"), SmokeSay("I'll go find the cover one next."), SmokeSay(Lockout.Refusal) }, asked, out fake);
            Func<string, bool> keptRunning = Lockout.Running;
            try
            {
                Brain failing = new Brain();
                failing.Model = "guard-test";
                failing.Act = delegate { return "Couldn't do that: the window in front changed."; };
                string failed = failing.Ask("press the X");
                if (failed != "Couldn't do that: the window in front changed.") throw new Exception("A failed escalated job did not say its own failure: " + failed);

                List<string> ran = new List<string>();
                Brain finding = new Brain();
                finding.Model = "guard-test";
                finding.Act = delegate(string tool, ToolArguments args) { ran.Add(tool + "|" + args.Task); return "Did it."; };
                if (!Hands.Promised("I'll go find a made-for-you one with the solid-color cover next.")) throw new Exception("A promise to go find something slipped through.");
                finding.Ask("press the X");
                if (ran.Count != 1 || !ran[0].StartsWith("use_computer|", StringComparison.Ordinal)) throw new Exception("A promise with no tool did not start a job.");

                Lockout.Running = delegate(string name) { return name == "deadlock"; };
                Lockout.Recheck();
                List<string> results = new List<string>();
                Brain locked = new Brain();
                locked.Model = "guard-test";
                locked.Act = delegate(string tool, ToolArguments args) { string result = Hands.Do(tool, args); results.Add(result); return result; };
                string lockedSaid = locked.Ask("press the X");
                if (results.Count != 1 || results[0] != Lockout.Refusal || lockedSaid.Contains("haven't done that yet")) throw new Exception("The escalated job got past the game lockout or was swapped for a refusal: " + lockedSaid);
            }
            finally
            {
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                fake.Stop();
                Brain.Ollama = keptOllama;
            }
            checks.Add("PASS no canned refusal: a promise with no tool starts a job instead of \"I haven't done that yet\", a failed job says its own reason, promises like \"I'll go find ... next\" count, and the escalated job still stops at the in-game input lockout");
        }

        private static void SmokeThreadSafety(List<string> checks)
        {
            Job wide = new Job("send hi in general on discord", "press the X");
            if (wide.Needs("send")) throw new Exception("The test job did not start with send approved, so it proves nothing.");
            wide.Only();
            if (!wide.Needs("send")) throw new Exception("A carried job kept approvals that came from earlier talk.");
            foreach (string talk in new[] { "I'll pick you up at six.", "let me find out.", "I'll press on.", "I'll choose you every time.", "I'll find a way." })
                if (Hands.Promised(talk)) throw new Exception("Plain chat counted as a promise to act: " + talk);
            string keptOllama = Brain.Ollama;
            List<string> asked = new List<string>();
            System.Net.Sockets.TcpListener fake;
            Brain.Ollama = FakeServer(new[] { SmokeSay("I'll pick burgers for you."), SmokeSay("chat") }, asked, out fake);
            try
            {
                List<string> ran = new List<string>();
                Brain chat = new Brain();
                chat.Model = "chat-test";
                chat.Act = delegate { ran.Add("job"); return "Did it."; };
                string said = chat.Ask("what should we eat tonight?");
                if (ran.Count != 0 || said != "I'll pick burgers for you.") throw new Exception("A promise in plain chat started a job or was swapped: " + said + " / " + ran.Count);
            }
            finally
            {
                fake.Stop();
                Brain.Ollama = keptOllama;
            }
            checks.Add("PASS thread safety: a job carried from earlier talk is only approved by his current words (a send in the old goal is never approved again), chat like \"I'll pick you up\" or \"let me find out\" is not a promise, and a promise in plain chat with no thread starts no job");
        }

        private static void SmokeThreadJobs(List<string> checks)
        {
            Job plain = new Job("open spotify");
            plain.Background = "Work in progress for them: send hi in general on discord.";
            if (!plain.Needs("send")) throw new Exception("The background widened what a job may do on its own.");
            Sight sight = new Sight { Window = new IntPtr(7), Title = "Netflix", Program = "opera" };
            string brief = Computer.Brief("pick the profile", new List<string>(), sight, "Work in progress for them: " + SmokeGoal);
            string opening = Computer.Opening("pick the profile", sight, null, "Work in progress for them: " + SmokeGoal);
            if (!brief.Contains(SmokeGoal) || !opening.Contains(SmokeGoal) || !opening.Contains("never to type") || Computer.Opening("job", sight, null, null).Contains("never to type")) throw new Exception("The prompt builders did not carry the background.");
            Func<Rectangle?, Picture> shootWas = Computer.Shoot;
            Func<Sight> glimpseWas = Computer.Glimpse;
            Func<IntPtr> frontWas = Computer.Front;
            try
            {
                Computer.Shoot = delegate(Rectangle? part) { return new Picture { Jpeg = new byte[] { 1, 1, 2 }, Size = new Size(1280, 720), Area = new Rectangle(0, 0, 2560, 1440) }; };
                Computer.Glimpse = delegate { return new Sight { Window = new IntPtr(7), Title = "Netflix", Program = "opera" }; };
                Computer.Front = delegate { return new IntPtr(7); };
                List<string> driven = new List<string>();
                Job drive = new Job("pick the profile");
                drive.Background = "Work in progress for them: " + SmokeGoal + "\nSteps so far:\n- job: Five profiles are showing.";
                drive.Drive = delegate(string system, string toolset, string messages)
                {
                    driven.Add(messages);
                    return Brain.Parsed("{\"id\":\"m1\",\"content\":[{\"type\":\"text\",\"text\":\"Picked it.\"}],\"stop_reason\":\"end_turn\"}");
                };
                string said = drive.Run();
                if (said != "Picked it." || driven.Count != 1 || !driven[0].Contains(SmokeGoal) || !driven[0].Contains("Five profiles are showing")) throw new Exception("The computer-tools job never saw the goal: " + said + " / " + (driven.Count > 0 ? driven[0] : "(no call)"));
            }
            finally
            {
                Computer.Shoot = shootWas;
                Computer.Glimpse = glimpseWas;
                Computer.Front = frontWas;
            }
            checks.Add("PASS jobs know the goal: the goal, steps and recent talk reach both job prompts (screenshot and button paths) as background to understand and never type, and they never add to what the job may do without asking");
        }

        private static void SmokeCutoff(List<string> checks)
        {
            foreach (string cut in new[] { "Could you open up, um,", "Can you select my account? It's", "open it and", "I want to, uh", "pick the one with..." })
                if (!Cutoff.Dangling(cut)) throw new Exception("A cut-off line was taken as finished: " + cut);
            foreach (string whole in new[] { "Open Netflix.", "Can you open Netflix?", "Mine starts with a T", "yes", "that's it", "what's that for", "who are you with" })
                if (Cutoff.Dangling(whole)) throw new Exception("A finished line was held: " + whole);
            Cutoff held = new Cutoff();
            if (held.Take("Could you open up, um,") != null || !held.Holding) throw new Exception("A cut-off line was not held.");
            string merged = held.Take("Netflix please");
            if (merged != "Could you open up, um, Netflix please" || held.Holding) throw new Exception("The next line was not joined to the held one: " + merged);
            if (held.Take("and") != null) throw new Exception("A second cut-off line was not held.");
            string released = held.Release();
            if (released != "and" || held.Holding) throw new Exception("A held line was lost after the wait: " + released);
            if (held.Release() != null) throw new Exception("Releasing with nothing held returned text.");
            checks.Add("PASS cut-off lines: a line ending in um, uh, a comma, a trailing it's or and is held a few seconds and joined to the next one, released alone if nothing follows, and phrases that only sound unfinished (\"what's that for\") are not held");
        }
    }
}
