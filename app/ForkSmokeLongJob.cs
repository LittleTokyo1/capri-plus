using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    // CAPRI-055 smoke: long jobs (run in parts, repeat, watch, the DM window), his words about them, real versus injected input.
    internal sealed partial class Companion
    {
        private void SmokeLongJob(List<string> checks)
        {
            string folder = Path.Combine(Path.GetTempPath(), "capri-smoke-long-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string keptPath = LongJob.Path;
            LongJob.Path = Path.Combine(folder, "long-jobs.json");
            LongJob.Forget();
            DateTime now = new DateTime(2026, 10, 31, 22, 0, 0);
            try
            {
                SmokeLongRun(now);
                SmokeLongModes(now);
                SmokeLongWords();
                SmokeLongCompanion();
            }
            finally
            {
                LongJob.End();
                LongJob.Stop(true);
                LongJob.Path = keptPath;
                LongJob.Forget();
                LongJob.Strict = false;
                longBusy = false;
                longJob = longHeld = null;
                longStopWhy = null;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
            checks.Add("LIMIT long jobs: the real input hooks, a real job running for hours, Discord delivery to his phone and her brain picking long_job for his words are not run here; Kasper must verify");
            checks.Add("PASS long jobs (CAPRI-055): a long_job call starts one job at a time with an 8 hour limit (his hours or end time, at most 24), kept on disk across a restart; each part carries the last part's progress, a timed wrap-up (SO FAR) goes on, a part that runs 5 minutes past it is ended and handed over, and FINISHED or a plain done ends it with a report, three failures in a row stop it with a report, her question or a held step waits for his answer (a line more than 3 hours later is not taken as one); watch ends on YES and checks again on NO, repeat waits for its interval; time up, stop and Esc end it, a game and his return pause it and carry on goes on; stop, carry on, yes, no, an answer and how's it going are read in code; her own injected input never counts as him and Esc stays her stop key; unasked sends and deletes are held during a long job; another mouse job is told at once; the DM window answers his friends until its end, in his quiet hours too, without touching his setting");
        }

        private static void SmokeLongRun(DateTime now)
        {
            if (Hands.Valid("long_job", new ToolArguments { Action = "start" }) || !Hands.Valid("long_job", new ToolArguments { Action = "start", Task = "install the game" }) || !Hands.Valid("long_job", new ToolArguments { Action = "stop" }) || Hands.Valid("long_job", new ToolArguments { Action = "start", Task = "x", Mode = "forever" }))
                throw new Exception("The long_job tool took a bad call or refused a good one.");
            bool listed = false;
            for (int i = 0; i < Hands.Count; i++) if (Hands.Name(i) == "long_job") listed = true;
            if (!listed) throw new Exception("The long_job tool isn't offered to her brain.");
            List<KeyValuePair<string, ToolArguments>> looked = new List<KeyValuePair<string, ToolArguments>> { new KeyValuePair<string, ToolArguments>("long_job", new ToolArguments { Action = "status" }) };
            List<KeyValuePair<string, ToolArguments>> ended = new List<KeyValuePair<string, ToolArguments>> { new KeyValuePair<string, ToolArguments>("long_job", new ToolArguments { Action = "stop" }) };
            List<KeyValuePair<string, ToolArguments>> begun = new List<KeyValuePair<string, ToolArguments>> { new KeyValuePair<string, ToolArguments>("long_job", new ToolArguments { Action = "start", Task = "watch my downloads", Mode = "watch" }) };
            List<KeyValuePair<string, ToolArguments>> mixed = new List<KeyValuePair<string, ToolArguments>> { looked[0], new KeyValuePair<string, ToolArguments>("use_computer", new ToolArguments { Task = "open downloads" }) };
            if (!LongJob.Missed(looked) || !LongJob.Missed(ended) || LongJob.Missed(begun) || LongJob.Missed(mixed) || LongJob.Missed(new List<KeyValuePair<string, ToolArguments>>()) || LongJob.StartNote.IndexOf("action start", StringComparison.Ordinal) < 0)
                throw new Exception("A long job he asked to start, answered with only a status or stop call while none runs, isn't asked again to start.");
            string started = Hands.Do("long_job", new ToolArguments { Action = "start", Task = "install the game from my downloads" });
            if (LongJob.Missed(looked)) throw new Exception("A status call while a long job runs was taken as a missed start.");
            LongWork work = LongJob.Current;
            if (work == null || work.State != LongJob.Running || !started.Contains("Say stop") || (new DateTime(work.Deadline) - DateTime.Now).TotalHours < 7.9 || (new DateTime(work.Deadline) - DateTime.Now).TotalHours > 8.1) throw new Exception("A long job did not start with the 8 hour limit: " + started);
            if (!LongJob.Do(new ToolArguments { Action = "start", Task = "something else" }, now).StartsWith("I'm already on a long job", StringComparison.Ordinal)) throw new Exception("A second long job started over the first.");
            if (LongJob.Block(now).Length == 0) throw new Exception("Her brain isn't told a long job is going.");
            LongJob.Forget();
            if (LongJob.Current == null || LongJob.Current.Task != "install the game from my downloads") throw new Exception("The long job was lost on a restart.");
            LongJob.End();
            if ((LongJob.Until(new ToolArguments { Hours = "3" }, now) - now).TotalHours != 3 || (LongJob.Until(new ToolArguments { Hours = "99" }, now) - now).TotalHours != LongJob.MostHours || LongJob.Until(new ToolArguments { End = "07:00" }, now) != new DateTime(2026, 11, 1, 7, 0, 0) || LongJob.Until(new ToolArguments { End = "2026-11-01T06:30" }, now) != new DateTime(2026, 11, 1, 6, 30, 0))
                throw new Exception("A long job's end time was worked out wrong.");
            LongWork run = new LongWork { Task = "sort my photos", Mode = "run", State = LongJob.Running, Deadline = now.AddHours(8).Ticks };
            List<string> steps = new List<string> { "opened Photos", "made a 2024 folder" };
            if (LongJob.After(run, "done", "SO FAR: moved January to March, April on is left", steps, true, now) != null || run.State != LongJob.Running || !run.Progress.StartsWith("moved January", StringComparison.Ordinal) || run.NextRun != now.Ticks || run.Chunks != 1)
                throw new Exception("A wrapped-up part did not carry on: " + run.Progress);
            if (!LongJob.Chunk(run).Contains("moved January") || !LongJob.Chunk(run).Contains("don't redo")) throw new Exception("The next part doesn't get the progress so far.");
            string fail1 = LongJob.After(run, "failed", "the window closed", steps, false, now);
            string fail2 = LongJob.After(run, "failed", "the window closed", steps, false, now);
            if (fail1 != null || fail2 != null || run.State != LongJob.Running || run.NextRun != now.AddMinutes(LongJob.RetryMinutes).Ticks) throw new Exception("A failed part was not retried.");
            string stuck = LongJob.After(run, "failed", "the window closed", steps, false, now);
            if (stuck == null || run.State != "finished" || !stuck.Contains("stuck")) throw new Exception("Three failures in a row did not stop it with a report.");
            LongWork done = new LongWork { Task = "sort my photos", Mode = "run", State = LongJob.Running };
            string finished = LongJob.After(done, "done", "FINISHED: all photos are in year folders", null, true, now);
            if (finished != "Done: all photos are in year folders" || done.State != "finished") throw new Exception("A finished long job was reported as: " + finished);
            LongWork asking = new LongWork { Task = "sign me up", Mode = "run", State = LongJob.Running };
            string asked = LongJob.After(asking, "asked", "Which email should I use?", null, false, now);
            if (asking.State != LongJob.Asked || asking.Question != "Which email should I use?" || !asked.Contains("Which email") || !asked.Contains("stop")) throw new Exception("Her question in a long job didn't wait for him.");
            LongWork holding = new LongWork { Task = "post it", Mode = "run", State = LongJob.Running };
            LongJob.After(holding, "waiting", "Should I press Post?", null, false, now);
            if (holding.State != LongJob.Waiting) throw new Exception("A held step in a long job didn't wait for his yes.");
            if (!LongJob.Over(new LongWork { Deadline = now.Ticks }, now) || !LongJob.TimeUpReport(run).StartsWith("Time's up", StringComparison.Ordinal)) throw new Exception("Time up was not seen.");
        }

        private static void SmokeLongModes(DateTime now)
        {
            if (LongJob.Minutes("1 hour", 60) != 60 || LongJob.Minutes("15", 10) != 15 || LongJob.Minutes("1", 10) != LongJob.LeastEvery || LongJob.Minutes(null, 10) != 10 || LongJob.Minutes("2h", 10) != 120) throw new Exception("How often was read wrong.");
            LongWork watch = new LongWork { Task = "tell me when the download is done", Mode = "watch", Watch = "the qBittorrent download is finished", Every = 10, State = LongJob.Running };
            if (!LongJob.Chunk(watch).Contains("change nothing") || !LongJob.Chunk(watch).Contains("YES")) throw new Exception("A watch check may change things.");
            if (LongJob.After(watch, "done", "NO, it's at 64%", null, false, now) != null || watch.NextRun != now.AddMinutes(10).Ticks || watch.Runs != 1) throw new Exception("A watch did not check again later.");
            string happened = LongJob.After(watch, "done", "YES, it says Completed", null, false, now);
            if (happened == null || !happened.StartsWith("It happened", StringComparison.Ordinal) || watch.State != "finished") throw new Exception("A watch did not end when it happened: " + happened);
            LongWork repeat = new LongWork { Task = "tidy my downloads", Mode = "repeat", Every = 60, State = LongJob.Running };
            if (LongJob.After(repeat, "done", "moved 3 files", null, false, now) != null || repeat.Runs != 1 || repeat.NextRun != now.AddMinutes(60).Ticks || !repeat.Progress.Contains("moved 3 files")) throw new Exception("A repeating job did not wait for its next run.");
            string answering = LongJob.Do(new ToolArguments { Action = "start", Mode = "answer", Hours = "2" }, DateTime.Now);
            if (!answering.Contains("answer your friends") || LongJob.AnswerMode(0, DateTime.Now) != 2 || LongJob.AnswerMode(0, DateTime.Now.AddHours(3)) != 0) throw new Exception("The DM window did not answer his friends: " + answering);
            if (LongJob.AnswerRoute("wait", DateTime.Now) != "digest" || LongJob.AnswerRoute("say", DateTime.Now) != "say") throw new Exception("A friend's message waited through his quiet hours while the DM window was open.");
            if (LongJob.Status(DateTime.Now).IndexOf("answering your friends", StringComparison.Ordinal) < 0) throw new Exception("Status doesn't say she answers his DMs.");
            LongJob.Stop(true);
            if (LongJob.AnswerMode(1, DateTime.Now) != 1) throw new Exception("Stopping the DM window did not give back his setting.");
        }

        private static void SmokeLongWords()
        {
            if (LongJob.Heard("stop", DateTime.Now) != null || LongJob.Heard("stop answering my DMs", DateTime.Now) != "stop answering") throw new Exception("Long job words were read with no long job going.");
            LongJob.Do(new ToolArguments { Action = "start", Task = "install the game" }, DateTime.Now);
            if (LongJob.Heard("stop", DateTime.Now) != "stop" || LongJob.Heard("cancel the long job", DateTime.Now) != "stop" || LongJob.Heard("how's the job going?", DateTime.Now) != "status" || LongJob.Heard("stop talking about cats", DateTime.Now) != null || LongJob.Heard("carry on", DateTime.Now) != null || LongJob.Heard("what's the weather", DateTime.Now) != null)
                throw new Exception("His words about a running long job were read wrong.");
            LongJob.Change(delegate(LongWork w) { w.State = LongJob.Paused; });
            if (LongJob.Heard("ok carry on", DateTime.Now) != "carry on" || LongJob.Heard("continue", DateTime.Now) != "carry on") throw new Exception("Carry on wasn't heard.");
            LongJob.Change(delegate(LongWork w) { w.State = LongJob.Waiting; });
            if (LongJob.Heard("yes", DateTime.Now) != "yes" || LongJob.Heard("nah", DateTime.Now) != "no") throw new Exception("Yes or no to a held step wasn't heard.");
            LongJob.Change(delegate(LongWork w) { w.State = LongJob.Asked; w.AskedAt = DateTime.Now.AddHours(-LongJob.AnswerHours - 1).Ticks; });
            if (LongJob.Heard("use my gmail", DateTime.Now) != null) throw new Exception("A line hours after her question was taken as the answer.");
            LongJob.Change(delegate(LongWork w) { w.AskedAt = DateTime.Now.Ticks; });
            if (LongJob.Heard("use my gmail", DateTime.Now) != "answer") throw new Exception("His answer to her question wasn't taken.");
            LongJob.End();
            if (LongJob.Real(0x01, false, 0) || !LongJob.Real(0, false, 0) || LongJob.Real(0x10, true, 0x41) || !LongJob.Real(0, true, 0x41) || LongJob.Real(0, true, 0x1B)) throw new Exception("Her own injected input counted as him, or his Esc paused instead of stopping.");
            if (!LongJob.CameBack(0, LongJob.AwayMilliseconds) || LongJob.CameBack(1000, 60000)) throw new Exception("Coming back was judged wrong.");
            LongJob.Strict = true;
            try
            {
                if (!new Job("open notepad").Needs("send") || !new Job("open notepad").Needs("delete")) throw new Exception("An unasked send or delete was not held during a long job.");
            }
            finally { LongJob.Strict = false; }
        }

        private void SmokeLongCompanion()
        {
            LongJob.Do(new ToolArguments { Action = "start", Task = "install the game" }, DateTime.Now);
            Job part = new Job("install the game");
            part.Steps.Add("opened Steam");
            longStopWhy = LongJob.Back;
            string back = LongAfter(part, "stopped", LongJob.Back);
            if (LongJob.Current.State != LongJob.Paused || back == null || !back.StartsWith("Welcome back", StringComparison.Ordinal) || !back.Contains("opened Steam")) throw new Exception("His return did not pause the long job: " + back);
            if (!LongHeard("carry on") || LongJob.Current.State != LongJob.Running || !longHeard.StartsWith("Carrying on", StringComparison.Ordinal)) throw new Exception("Carry on didn't resume the long job.");
            longStopWhy = LongJob.PartEnd;
            Job longPart = new Job("install the game");
            longPart.Steps.Add("ran the installer");
            LongAfter(longPart, "stopped", LongJob.PartEnd);
            if (LongJob.Current == null || LongJob.Current.State != LongJob.Running || LongJob.Current.Chunks != 1 || !LongJob.Current.Progress.Contains("ran the installer")) throw new Exception("A part that ran past its wrap-up did not hand over to the next part.");
            longStopWhy = "game";
            LongAfter(new Job("install the game"), "stopped", "game");
            if (LongJob.Current.State != LongJob.Paused) throw new Exception("A game didn't pause the long job.");
            longGamePaused = false;
            LongJob.Change(delegate(LongWork w) { w.State = LongJob.Running; });
            string esc = LongAfter(new Job("install the game"), "stopped", Computer.Escaped);
            if (LongJob.Current != null || esc == null || !esc.Contains("Esc")) throw new Exception("Esc did not stop the long job.");
            LongJob.Do(new ToolArguments { Action = "start", Task = "install the game" }, DateTime.Now);
            longBusy = true;
            if (LongBusy("use_computer", new ToolArguments { Task = "x" }) == null || LongBusy("media", new ToolArguments { Action = "pause" }) != null) throw new Exception("Another mouse job wasn't told about the long job.");
            longBusy = false;
            LongJob.Change(delegate(LongWork w) { w.Deadline = DateTime.Now.AddMinutes(-1).Ticks; });
            string over = LongStep(LongJob.Current, DateTime.Now);
            if (LongJob.Current != null || over == null || !over.StartsWith("Time's up", StringComparison.Ordinal)) throw new Exception("Time up did not end the long job: " + over);
            LongJob.Do(new ToolArguments { Action = "start", Task = "install the game" }, DateTime.Now);
            if (!LongHeard("stop") || LongJob.Current != null || !longHeard.StartsWith("Stopped the long job", StringComparison.Ordinal)) throw new Exception("Stop didn't end the long job.");
        }
    }
}
