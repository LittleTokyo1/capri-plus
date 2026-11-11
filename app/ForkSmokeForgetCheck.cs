using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeForgetCheck(List<string> checks)
        {
            Func<Cloud, string, string> kept = ForgetCheck.Decide;
            Cloud remote = new Cloud("sk-ant-smoke", "claude-haiku-5-5");
            List<string> asked = new List<string>();
            try
            {
                if (ForgetCheck.Parse("YES") != true || ForgetCheck.Parse(" yes.") != true || ForgetCheck.Parse("NO") != false || ForgetCheck.Parse("No, he asked her to message a friend.") != false || ForgetCheck.Parse("maybe") != null || ForgetCheck.Parse(null) != null)
                    throw new Exception("The forget check's answer was misread.");
                ForgetCheck.Decide = delegate(Cloud cloud, string frame) { asked.Add(frame); return frame.IndexOf("forget the gym line", StringComparison.Ordinal) >= 0 || frame.IndexOf("vergiss", StringComparison.Ordinal) >= 0 ? "YES" : "NO"; };
                if (ForgetCheck.Meant(remote, "ask rudce if were gonna continue playing cyberpunk today? say that u miss it", null))
                    throw new Exception("A forget marker on a message to a friend would delete notes.");
                if (ForgetCheck.Meant(remote, "message pieter goodnight", "Done, sent it."))
                    throw new Exception("A forget marker on a plain request would delete notes.");
                if (!ForgetCheck.Meant(remote, "forget the gym line", null) || !ForgetCheck.Meant(remote, "vergiss das bitte", "I noted your gym days."))
                    throw new Exception("A real ask to forget, in any language, was refused.");
                if (asked.Count != 4 || asked[3].IndexOf("I noted your gym days.", StringComparison.Ordinal) < 0)
                    throw new Exception("The forget check didn't see his words and her line before.");
                if (!ForgetCheck.Blocks("remember", new ToolArguments { Action = "forget", Text = "rudce" }, delegate { return false; }) || ForgetCheck.Blocks("remember", new ToolArguments { Action = "forget", Text = "gym" }, delegate { return true; }) || ForgetCheck.Blocks("remember", new ToolArguments { Text = "pieter plays tarkov" }, delegate { return false; }) || ForgetCheck.Blocks("discord", new ToolArguments { Action = "message" }, delegate { return false; }))
                    throw new Exception("The remember tool's forget ran without the forget check, or the check held a save.");
                ForgetCheck.Decide = delegate { return null; };
                if (ForgetCheck.Meant(remote, "ask rudce if were gonna play", null) || !ForgetCheck.Meant(remote, "forget my old gym days", null) || !ForgetCheck.Meant(remote, "delete that note about the gym", null))
                    throw new Exception("With no answer from the check, the plain fallback deleted on a request or refused a clear forget.");
                if (ForgetCheck.Plain("don't forget my birthday") || ForgetCheck.Plain("i forgot my keys lol") || !ForgetCheck.Plain("wipe everything you know about me"))
                    throw new Exception("The plain forget fallback misread his words.");
            }
            finally { ForgetCheck.Decide = kept; }
            checks.Add("PASS stray forget marker (CAPRI-072): a [forget] marker, or the remember tool's forget action, only deletes when a small check of his own words (and her line before, any language) says he asked her to forget, remove or wipe something; a message to a friend or a plain request deletes nothing; with no answer from the check only a clear English forget, delete or wipe in his words counts, never 'don't forget' or 'I forgot'");
        }
    }
}
