using System;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool DoneHeard(string text, bool voiceChat)
        {
            string kind = OpenAsks.Heard(text);
            if (kind == null) return false;
            DateTime now = DateTime.Now;
            string root = Vault.Root;
            OpenAsk newest = OpenAsks.Peek(kind == OpenAsks.SignIn ? OpenAsks.SignIn : null, now);
            bool signIn = kind == OpenAsks.SignIn || newest != null && newest.Kind == OpenAsks.SignIn;
            PlanLine plan = signIn ? OpenAsks.Plan(Plans.Open(root)) : null;
            if (newest == null && plan == null) return false;
            if (newest != null) OpenAsks.Take(newest.Kind, now);
            string what = plan != null ? plan.Text : newest.Text;
            if (plan != null) Plans.Finish(root, plan.Key, false, now);
            ActivityLog.Write("asks", "done", what + "  <-  " + text);
            string line = signIn ? SignedInNow(what, now) : "Nice, ticked off: " + what + ".";
            if (!voiceChat) { SayStudy("he did it  " + text, line); return true; }
            ActivityLog.Write("study", "he did it (voice chat)  " + text, line);
            brain.Own("he said he did what she asked; she did this in code", line);
            if (agent != null) agent.Tell("Capri just did this in code, tell him in your own words: " + line);
            return true;
        }

        private string SignedInNow(string what, DateTime now)
        {
            studySignInDay = DateTime.MinValue;
            if (Study.Safe() == null) return "Nice, ticked off: " + what + ".";
            schoolCheckNow = true;
            if (Lockout.Active) return "Nice, ticked off: " + what + ". I'll check your school mail right after your game.";
            schoolTickAt = DateTime.MinValue;
            if (SchoolMailTurn(now)) return "Nice, ticked off: " + what + ". Checking your school mail now.";
            return "Nice, ticked off: " + what + ". I'll check your school mail as soon as your other study job is done.";
        }
    }
}
