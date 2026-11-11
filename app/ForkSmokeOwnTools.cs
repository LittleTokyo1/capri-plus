using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeOwnTools(List<string> checks)
        {
            Func<ToolArguments, string> chatWas = OwnTools.VoiceChat, jobWas = OwnTools.JobControl, quitWas = OwnTools.Quit, updatesWas = OwnTools.Updates;
            Func<string> speechWas = OwnTools.StopSpeech;
            Func<string, string> frameWas = OwnTools.Mainframe;
            try
            {
                List<string> ran = new List<string>();
                OwnTools.VoiceChat = delegate(ToolArguments args) { ran.Add("voice_chat " + args.Action); return "ok"; };
                OwnTools.StopSpeech = delegate { ran.Add("speech stop"); return "ok"; };
                OwnTools.JobControl = delegate(ToolArguments args) { ran.Add("job_control " + args.Action + (args.Task == null ? "" : " " + args.Task)); return "ok"; };
                OwnTools.Quit = delegate { ran.Add("quit"); return "ok"; };
                OwnTools.Mainframe = delegate(string page) { ran.Add("mainframe " + (page ?? "")); return "ok"; };
                OwnTools.Updates = delegate(ToolArguments args) { ran.Add("updates " + args.Action); return "ok"; };
                Hands.Do("voice_chat", new ToolArguments { Action = "off" });
                Hands.Do("speech", new ToolArguments { Action = "stop" });
                Hands.Do("job_control", new ToolArguments { Action = "change", Task = "use the second tab" });
                Hands.Do("quit", new ToolArguments { Action = "quit" });
                Hands.Do("mainframe", new ToolArguments { Name = "capri_plus" });
                Hands.Do("updates", new ToolArguments { Action = "status" });
                string said = string.Join("|", ran.ToArray());
                if (said != "voice_chat off|speech stop|job_control change use the second tab|quit|mainframe capri_plus|updates status") throw new Exception("Her own tools didn't reach her: " + said);
                if (Hands.Do("voice_chat", new ToolArguments { Action = "maybe" }) != "Couldn't find that voice chat action." || ran.Count != 6) throw new Exception("A voice chat action that isn't one ran.");
                if (Hands.Do("job_control", new ToolArguments { Action = "change" }) != "Couldn't find that job instruction.") throw new Exception("A job change with no new instruction ran.");

                foreach (string tool in OwnTools.Names)
                {
                    if (!Hands.Tools.Contains("\"name\":\"" + tool + "\"")) throw new Exception("Her brain isn't offered " + tool + ".");
                    if (Lockout.Handles(tool)) throw new Exception(tool + " isn't keys or mouse, yet the input lockout took it.");
                }
                if (!Hands.Valid("voice_chat", new ToolArguments { Action = "status" }) || !Hands.Valid("mainframe", new ToolArguments()) || !Hands.Valid("updates", new ToolArguments { Action = "install" }) || Hands.Valid("mainframe", new ToolArguments { Name = "keys" }) || Hands.Valid("quit", new ToolArguments { Action = "later" })) throw new Exception("The tool check read her own tools wrong.");

                if (OwnTools.QuitRefusal(true) == null || OwnTools.QuitRefusal(false) != null) throw new Exception("Quitting in a game wasn't refused, or quitting outside one was.");
                foreach (string install in new[] { "install the update", "update yourself", "yes install it", "please update capri now", "apply the update" })
                    if (!OwnTools.InstallAsked(install)) throw new Exception("His clear ask to install wasn't taken: " + install);
                foreach (string not in new[] { "is there an update?", "any updates", "check for updates", "did you update", "what's new in the update", null })
                    if (OwnTools.InstallAsked(not)) throw new Exception("A question or a hint was taken as installing: " + not);
                if (OwnTools.InstallRefusal(true, "install the update") == null || OwnTools.InstallRefusal(false, "is there an update?") == null || OwnTools.InstallRefusal(false, "install the update") != null) throw new Exception("Installing wasn't held for a game or a hint, or his clear ask outside a game was held.");

                if (OwnTools.Due(10, 10.5, false) || !OwnTools.Due(10, 11.2, false) || OwnTools.Due(10, 30, true) || !OwnTools.Due(10, 71, true)) throw new Exception("After this reply ran too soon, waited on a reply that never ends, or never ran.");
            }
            finally
            {
                OwnTools.VoiceChat = chatWas;
                OwnTools.StopSpeech = speechWas;
                OwnTools.JobControl = jobWas;
                OwnTools.Quit = quitWas;
                OwnTools.Mainframe = frameWas;
                OwnTools.Updates = updatesWas;
            }
            checks.Add("PASS everything is a tool, her own actions (096-07): voice_chat, speech, job_control, quit, mainframe and updates reach her and are offered to her brain, an action that isn't one or a job change with no instruction never runs, none of them is taken by the input lockout, quitting waits for the game to end, installing needs his clear ask (a question or hint only checks) and never runs in a game, and what waits for her reply runs once it's said (or after a minute at most)");
        }
    }
}
