using System;
using System.IO;

namespace CapriDesktop
{
    internal static partial class ProjectRun
    {
        internal const string AnswerTool = "project_answer";
        internal const string AnswerLead = "The user answers you (through Capri): ";
        internal const string NoneWaiting = "None of my Claude sessions is waiting on your answer right now.";
        internal const int MovedSeconds = 60;
        internal const string AnswerSpec = "Pass their answer to one of your Claude Code sessions that asked them something. Use it when you told them one of their project sessions asks something (Your <project> session asks: ...) and they answer it, in any words or language, at the PC, on Discord or on the phone: text is their answer in their own words. Use action status when they ask what a session is waiting on. A plan is approved only in the session's window or on their phone, never through this. Never say you passed an answer on without calling this.";
        internal const string AnswerSchema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"answer (the default) passes their answer on, status says what is waiting\",\"enum\":[\"answer\",\"status\"]},\"text\":{\"type\":\"string\",\"description\":\"Their answer, in their words\"}}}";

        internal static string Do(string tool, ToolArguments args)
        {
            if (tool == LandTool) return LandDo(args);
            if (tool == Ideas.Tool) return Ideas.Do(args);
            if (tool == ProjectPropose.Tool) return ProjectPropose.Do(args);
            if (tool != AnswerTool) return null;
            if (args != null && args.Action == "status") return Waiting();
            ForkSettings settings = ForkSettings.Current;
            return Answer(args == null ? null : args.Text, settings.RunProjects, settings.Projects, DateTime.Now);
        }

        internal static string Waiting()
        {
            lock (gate)
            {
                ProjectRunState state = Load();
                ProjectTurn turn = state.Pid == 0 || state.Asked == 0 ? null : ProjectHook.Turn(state.Session);
                if (turn == null) return NoneWaiting;
                return "Your " + ProjectName(state) + " session waits on your answer to: " + ProjectRelay.Asked(turn) + (state.Pending.Length > 0 ? " (your answer \"" + state.Pending + "\" goes in after the game)" : "");
            }
        }

        internal static string Answer(string text, bool on, string[] lines, DateTime now)
        {
            string said = (text ?? "").Trim();
            if (said.Length == 0) return "What should I tell your session, choom?";
            if (!on) return Off;
            lock (gate)
            {
                ProjectRunState state = Load();
                if (state.Pid == 0 || state.Asked == 0) return NoneWaiting;
                string moved = Moved(state, now);
                if (moved != null) return moved;
                if (Gaming())
                {
                    state.Pending = said;
                    Save(state);
                    ActivityLog.Write("project session", ProjectName(state), "held his answer until the game ends");
                    return "I'll pass that to your " + ProjectName(state) + " session after the game.";
                }
                return Deliver(state, said, lines, now);
            }
        }

        internal static string DeliverPending(bool on, string[] lines, DateTime now)
        {
            lock (gate)
            {
                ProjectRunState state = Load();
                if (string.IsNullOrEmpty(state.Pending)) return null;
                if (!on || state.Pid == 0 || state.Asked == 0)
                {
                    state.Pending = "";
                    Save(state);
                    return null;
                }
                if (Gaming()) return null;
                string moved = Moved(state, now);
                if (moved != null) return moved;
                string said = state.Pending;
                return Deliver(state, said, lines, now);
            }
        }

        internal static string[] ResumeArguments(ProjectEntry entry, string session, string run, DateTime day, string hooks, string said)
        {
            string[] arguments = Arguments(entry, AnswerLead + said, session, run, day, hooks);
            arguments[Array.IndexOf(arguments, "--session-id")] = "--resume";
            return arguments;
        }

        private static string Moved(ProjectRunState state, DateTime now)
        {
            ProjectTurn turn = ProjectHook.Turn(state.Session);
            string file = TranscriptFile(TranscriptRoot ?? HuskWatch.DefaultRoot(), state.Folder, state.Session);
            bool newer = turn == null || turn.At != state.Asked;
            bool written = File.Exists(file) && File.GetLastWriteTime(file) > new DateTime(state.Asked).AddSeconds(MovedSeconds);
            if (!newer && !written) return null;
            string project = ProjectName(state);
            state.Asked = 0;
            state.Pending = "";
            Save(state);
            ActivityLog.Write("project session", project, "his answer not passed in: the session already went on");
            return "Your " + project + " session already went on (you answered it there?), so I didn't pass that in.";
        }

        private static string Deliver(ProjectRunState state, string said, string[] lines, DateTime now)
        {
            string project = ProjectName(state);
            ProjectEntry entry = Find(lines, state.Folder);
            if (entry == null) return "Your " + project + " session's folder isn't in my Capri+ PROJECTS list anymore, so answer it in its window.";
            string run = string.IsNullOrEmpty(state.Run) ? RunName(entry, now) : state.Run;
            Shut(state);
            ProjectProcess started = null;
            try { started = Launch(entry.Folder, ResumeArguments(entry, state.Session, run, now, ProjectHook.WriteSettings(HookExe()), said)); }
            catch (Exception error) { ActivityLog.Write("project session", project, "didn't reopen: " + error.GetType().Name + ": " + error.Message); }
            if (started == null)
            {
                End(state, "closed", now);
                return "I couldn't reopen your " + project + " session, so your answer didn't go in. Answer it in Claude Code with claude --resume.";
            }
            state.Pid = started.Pid;
            state.PidStart = started.Started.Ticks;
            state.Asked = 0;
            state.Pending = "";
            state.Run = run;
            Save(state);
            ProjectHook.Expect(state.Session, true);
            ActivityLog.Write("project session", project, "passed his answer in and reopened it: " + said);
            return "Passed it to your " + project + " session; it goes on now, minimised, with Remote Control on.";
        }

        private static string ProjectName(ProjectRunState state)
        {
            return Path.GetFileName((state.Folder ?? "").TrimEnd('\\', '/'));
        }
    }
}
