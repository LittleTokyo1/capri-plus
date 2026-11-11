using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class OwnTools
    {
        internal static readonly string[] Names = { "voice_chat", "speech", "job_control", "quit", "mainframe", "updates" };

        internal static Func<ToolArguments, string> VoiceChat = delegate { return "Couldn't change voice chat."; };
        internal static Func<string> StopSpeech = delegate { return "Stopped talking."; };
        internal static Func<ToolArguments, string> JobControl = delegate { return "I'm not on a job right now."; };
        internal static Func<ToolArguments, string> Quit = delegate { return "Couldn't quit Capri."; };
        internal static Func<string, string> Mainframe = delegate { return "Couldn't open Mainframe."; };
        internal static Func<ToolArguments, string> Updates = delegate { return "Couldn't check for an update."; };

        internal const string VoiceSpec = "Turn your voice chat on or off, or read its status, when they ask, suggest or hint at talking with you by voice or stopping it. The switch waits until your reply is said.";
        internal const string VoiceSchema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"on, off or status\",\"enum\":[\"on\",\"off\",\"status\"]}},\"required\":[\"action\"]}";
        internal const string SpeechSpec = "Stop your current spoken reply when they ask, suggest or hint that you stop talking right now. Your voice stays on for later replies.";
        internal const string SpeechSchema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"stop talking now\",\"enum\":[\"stop\"]}},\"required\":[\"action\"]}";
        internal const string JobSpec = "Stop the running mouse and keyboard job, read how it's going, or change what it does, when they ask, suggest or hint at it. For change, task is their new instruction in their words.";
        internal const string JobSchema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"stop, status or change\",\"enum\":[\"stop\",\"status\",\"change\"]},\"task\":{\"type\":\"string\",\"description\":\"For change, the new instruction in their words\"}},\"required\":[\"action\"]}";
        internal const string QuitSpec = "Quit Capri (close yourself) after your reply is said, when they ask you to quit, close or shut yourself down. This leaves their PC running, and Capri asks them to confirm. Never mid-game.";
        internal const string QuitSchema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"quit Capri\",\"enum\":[\"quit\"]}},\"required\":[\"action\"]}";
        internal const string FrameSpec = "Open one of your settings windows when they ask, suggest or hint at seeing your controls: Capri Mainframe (your brain, voice, keys and upstream settings) or Capri+ (capri_plus: your fork's switches, Discord, the phone, studies, PC checks). This only opens it; keys, brains, voice services, SECURITY and memories are changed there by them.";
        internal const string FrameSchema = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"Which window; leave it out for Mainframe\",\"enum\":[\"mainframe\",\"capri_plus\"]}}}";
        internal const string UpdateSpec = "Check for a Capri update or read its status when they ask, suggest or hint at it. Install only when they clearly ask to install or update Capri; a status question or a hint never installs. Installing restarts you and never happens mid-game.";
        internal const string UpdateSchema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"check, status or install; install needs their clear ask\",\"enum\":[\"check\",\"status\",\"install\"]}},\"required\":[\"action\"]}";

        private static readonly Regex Install = new Regex(@"\b(?:install|apply)\b|\bupdate\s+(?:yourself|capri|now)\b|^\W*update\W*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Question = new Regex(@"\?|^\W*(?:did|is|are|was|were|have|has|what|when|why|how|do|does|any)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static string Do(string tool, ToolArguments args)
        {
            if (Array.IndexOf(Names, tool) < 0) return null;
            if (!Valid(tool, args))
            {
                if (tool == "voice_chat") return "Couldn't find that voice chat action.";
                if (tool == "speech") return "Couldn't find that speech action.";
                if (tool == "job_control") return "Couldn't find that job instruction.";
                if (tool == "quit") return "Couldn't find that quit action.";
                if (tool == "mainframe") return "Couldn't find that settings window.";
                return "Couldn't find that update action.";
            }
            if (tool == "voice_chat") return VoiceChat(args);
            if (tool == "speech") return StopSpeech();
            if (tool == "job_control") return JobControl(args);
            if (tool == "quit") return Quit(args);
            if (tool == "mainframe") return Mainframe(string.IsNullOrWhiteSpace(args.Name) ? "mainframe" : args.Name);
            return Updates(args);
        }

        internal static bool Valid(string tool, ToolArguments args)
        {
            if (args == null) return false;
            string action = args.Action ?? "";
            if (tool == "voice_chat") return action == "on" || action == "off" || action == "status";
            if (tool == "speech") return action == "stop";
            if (tool == "job_control") return action == "stop" || action == "status" || action == "change" && !string.IsNullOrWhiteSpace(args.Task);
            if (tool == "quit") return action == "quit";
            if (tool == "mainframe") return string.IsNullOrWhiteSpace(args.Name) || args.Name == "mainframe" || args.Name == "capri_plus";
            if (tool == "updates") return action == "check" || action == "status" || action == "install";
            return false;
        }

        internal static string QuitRefusal(bool game)
        {
            return game ? "Not mid-match, choom. I'll quit after your game if you still want me gone." : null;
        }

        internal static bool InstallAsked(string asked)
        {
            if (string.IsNullOrWhiteSpace(asked)) return false;
            return Install.IsMatch(asked) && !Question.IsMatch(asked);
        }

        internal static string InstallRefusal(bool game, string asked)
        {
            if (game) return "After your game: installing the update restarts me.";
            if (!InstallAsked(asked)) return "Say install the update and I'll install it. Asking about it only checks.";
            return null;
        }

        internal static bool Due(double queuedAt, double now, bool busy)
        {
            return now - queuedAt >= 60 || !busy && now - queuedAt >= 1;
        }
    }

    internal sealed partial class Companion
    {
        private readonly List<KeyValuePair<double, Action>> ownAfter = new List<KeyValuePair<double, Action>>();

        private void OwnToolsStart()
        {
            OwnTools.VoiceChat = VoiceChatTool;
            OwnTools.StopSpeech = SpeechTool;
            OwnTools.JobControl = JobTool;
            OwnTools.Quit = QuitTool;
            OwnTools.Mainframe = MainframeTool;
            OwnTools.Updates = UpdateTool;
        }

        private void AfterReply(Action action)
        {
            ownAfter.Add(new KeyValuePair<double, Action>(clock.Elapsed.TotalSeconds, action));
        }

        private void OwnToolsTick(double now)
        {
            if (ownAfter.Count == 0) return;
            bool busy = talkState == TalkState.Thinking || talkState == TalkState.Talking || speech != null;
            List<KeyValuePair<double, Action>> due = ownAfter.FindAll(delegate(KeyValuePair<double, Action> waiting) { return OwnTools.Due(waiting.Key, now, busy); });
            foreach (KeyValuePair<double, Action> waiting in due)
            {
                ownAfter.Remove(waiting);
                try { waiting.Value(); }
                catch (Exception error) { ActivityLog.Write("tool", "after reply failed", error.Message); }
            }
        }

        private string VoiceChatTool(ToolArguments args)
        {
            if (InvokeRequired) return (string)Invoke(new Func<ToolArguments, string>(VoiceChatTool), args);
            if (args.Action == "status") return chatting ? "Voice chat is on." : "Voice chat is off.";
            bool on = args.Action == "on";
            if (chatting == on) return on ? "Voice chat is already on." : "Voice chat is already off.";
            AfterReply(delegate { if (chatting != on) ToggleChat(); });
            return on ? "I'll turn voice chat on after this reply." : "I'll turn voice chat off after this reply.";
        }

        private string SpeechTool()
        {
            if (InvokeRequired) return (string)Invoke(new Func<string>(SpeechTool));
            BargeIn();
            return "Stopped talking.";
        }

        private string JobTool(ToolArguments args)
        {
            Job running = job;
            if (!MidJob(running)) return "I'm not on a job right now.";
            if (args.Action == "status") return Asides.Status(running.Report(), "status");
            if (args.Action == "stop")
            {
                StopJob("You asked me to stop.");
                return "Stopped my job.";
            }
            running.Change(args.Task);
            return "Changed what my job should do.";
        }

        private string QuitTool(ToolArguments args)
        {
            if (InvokeRequired) return (string)Invoke(new Func<ToolArguments, string>(QuitTool), args);
            string refused = OwnTools.QuitRefusal(Lockout.Active);
            if (refused != null) return refused;
            AfterReply(delegate { if (!Lockout.Active) Quit(); });
            return "I'll quit after this reply, once you confirm.";
        }

        private string MainframeTool(string page)
        {
            if (InvokeRequired) return (string)Invoke(new Func<string, string>(MainframeTool), page);
            if (page == "capri_plus")
            {
                OpenForkSettings();
                return "Opened Capri+.";
            }
            mainframe.Restore();
            return "Opened Capri Mainframe.";
        }

        private string UpdateTool(ToolArguments args)
        {
            if (InvokeRequired) return (string)Invoke(new Func<ToolArguments, string>(UpdateTool), args);
            UpdateFile ready = updateFile;
            if (args.Action == "status")
            {
                if (installing) return "The update is installing.";
                if (updating) return "I'm checking for an update.";
                if (ready != null) return "Capri " + ready.Version + " is downloaded and ready to install.";
                return string.IsNullOrEmpty(mainframe.UpdateStatus) ? "I haven't checked for updates yet." : mainframe.UpdateStatus;
            }
            if (installing) return "The update is already installing.";
            if (args.Action == "check")
            {
                CheckForUpdates(true);
                return "I'm checking for an update.";
            }
            string refused = OwnTools.InstallRefusal(Lockout.Active, args.Asked);
            if (refused != null) return refused;
            if (ready == null)
            {
                CheckForUpdates(true);
                return "No update is waiting, so I'm checking for one now. I'll tell you when it's ready.";
            }
            ActivityLog.Write("update", "asked by tool", ready.Version);
            AfterReply(delegate { if (!Lockout.Active && !installing && updateFile != null) InstallUpdate(); });
            return "Installing Capri " + ready.Version + " after this reply. I'll be right back.";
        }
    }
}
