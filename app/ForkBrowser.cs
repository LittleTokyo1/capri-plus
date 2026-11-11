using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        // CAPRI-071: a browser job runs her usual job loop on her own hidden Chrome, so his mouse and keyboard stay his and every safety stop still holds.
        private string BrowserTool(ToolArguments args)
        {
            string heard = BrowserRecall.Take();
            string action = args == null || string.IsNullOrEmpty(args.Action) ? "job" : args.Action;
            if (action == "show" || action == "hide") return HerBrowser.Show(action == "show");
            if (args == null || string.IsNullOrWhiteSpace(args.Task)) return "Which job, choom?";
            string earlier = BrowserRecall.Earlier(heard, args.Task, DateTime.Now);
            if (earlier != null) return earlier;
            Job made = new Job(args.Task.Trim(), args.Asked);
            made.Background = Brain.Briefing;
            if (Brain.Carrying) made.Only();
            Browsing(made);
            return BrowserRun(made);
        }

        // One job at a time, like her hands, so a browser job never shares her job state with another.
        private string BrowserRun(Job made)
        {
            lock (mouseGate) return Handed(made, BrowserWork(made));
        }

        internal const string ShownForYou = "My browser is on your screen now, so you can do that part there.";
        internal const string ShowLater = "After your game, say show me your browser and I'll put it on your screen for that part.";
        internal const string ShowWhenBack = "When you're back at the PC, say show me your browser and I'll put it on your screen for that part.";

        // A sign-in, card or payment step is his to do, in her browser, so he gets to see it: now, or after his game.
        internal static bool Handoff(Job made, string result)
        {
            string said = result ?? "";
            return said.Contains(Computer.PayYourself) || said.Contains(Computer.CardYourself) || said.Contains(Computer.SignInYourself);
        }

        private string Handed(Job made, string result)
        {
            if ((result ?? "").Contains(Challenge.Stopped))
            {
                string host = Challenge.Hit ?? "";
                if (!Challenge.Skippable(made.Words, made.Task, host)) return ChallengeHanded(result, made.Task);
                ActivityLog.Write("browser", "human check", host + " -> skipped, other sites");
                Challenge.Hit = null;
                Job again = new Job(Challenge.WithSkip(made.Task, host));
                again.Background = made.Background;
                Browsing(again);
                return Handed(again, BrowserWork(again));
            }
            if (!Handoff(made, result))
            {
                if (Challenge.Showing && HerBrowser.Shown) { Challenge.Showing = false; HerBrowser.Show(false); }
                return result;
            }
            if (Lockout.Active) return (result ?? "").TrimEnd() + " " + ShowLater;
            if (Delivery.NotHome()) return (result ?? "").TrimEnd() + " " + ShowWhenBack;
            string shown = HerBrowser.Show(true);
            return (result ?? "").TrimEnd() + " " + (shown.StartsWith("My browser is on", StringComparison.Ordinal) ? ShownForYou : shown);
        }

        internal static void Browsing(Job made)
        {
            made.Sense = Challenge.Sensed;
            made.Hand = HerBrowser.Act;
            made.Rules = HerBrowser.Rules;
        }

        // No task panel and no pointer: nothing of hers moves on his screens. In a game his Esc is for the game, not her browser.
        private string BrowserWork(Job running)
        {
            job = running;
            running.Think = delegate(string system, string user, List<KeyValuePair<string, ToolArguments>> calls) { return brain.Step(system, user, Computer.Tools, Computer.ClaudeTools, Computer.Listing, calls); };
            running.Think = Challenge.Guard(running.Think);
            running.Progress = delegate(string status) { Post(delegate { working = status + " (in my browser)"; UpdateTalk(clock.Elapsed.TotalSeconds); }); };
            Func<bool> escape = Computer.Escape;
            Computer.Escape = delegate { return !Lockout.Active && escape(); };
            ActivityLog.Write("browser", "job", running.Words);
            string result = null;
            try
            {
                result = running.Run();
                return result;
            }
            finally
            {
                Computer.Escape = escape;
                if (job == running && !running.Asking) job = null;
                ActivityLog.Write("browser", "job " + (running.Outcome ?? "failed"), result);
                BrowserRecall.Keep(running.Task, result, running.Outcome, Job.Summed(running.Steps), DateTime.Now);
                Post(delegate { if (job == null) working = null; UpdateTalk(clock.Elapsed.TotalSeconds); });
            }
        }
    }
}