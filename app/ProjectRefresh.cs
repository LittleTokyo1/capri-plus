using System;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static partial class ProjectRun
    {
        internal const int ContextTokens = 250000, HandoffChars = 1500;
        internal const string HandoffMark = "HANDOFF:";
        internal const string HandoffLead = " Notes from the last session, which ran out of context: ";
        internal const string WrapFull = "Your context is getting full, so Capri will carry this work on in a fresh session. Finish the step you are on, update this project's checkpoint and notes the way its CLAUDE.md says, commit, then end with one message that starts with " + HandoffMark + " and says in a few lines what is done and the exact next action.";
        internal const string WrapPlan = "Your context is getting full, so Capri will carry this on in a fresh session. Don't edit anything. End with one message that starts with " + HandoffMark + " and says in a few lines what you found, what you planned and the exact next action.";
        internal const string WrapWatch = "Your context is getting full, so Capri will carry this on in a fresh session. Don't edit or run anything. End with one message that starts with " + HandoffMark + " and says in a few lines what you read, what you found and what is left to read.";
        private static readonly Regex InputTokens = new Regex("\"(input_tokens|cache_read_input_tokens|cache_creation_input_tokens)\":(\\d+)");

        internal static int ContextUsed(string tail)
        {
            string[] lines = (tail ?? "").Replace("\r", "").Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                if (line.IndexOf("\"type\":\"assistant\"", StringComparison.Ordinal) < 0 || line.IndexOf("\"isSidechain\":true", StringComparison.Ordinal) >= 0 || line.IndexOf("\"usage\"", StringComparison.Ordinal) < 0) continue;
                long total = 0;
                foreach (Match part in InputTokens.Matches(line)) total += long.Parse(part.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                return (int)Math.Min(int.MaxValue, total);
            }
            return 0;
        }

        internal static bool Refresh(HeardTurn one, bool on, string[] lines, int perDay, DateTime now)
        {
            if (one == null || one.Turn == null || one.Turn.Kind != ProjectHook.Stop) return false;
            ProjectTurn turn = one.Turn;
            lock (gate)
            {
                ProjectRunState state = Load();
                if (state.Pid == 0 || state.Session != turn.Session || !string.IsNullOrEmpty(state.Landing)) return false;
                ProjectEntry entry = Find(lines, state.Folder);
                if (state.Wrapping != 0) return Swap(state, entry, turn, on, lines, perDay, now);
                if (entry == null || ProjectRelay.Needs(turn)) return false;
                int used = ContextUsed(Tail(TranscriptFile(TranscriptRoot ?? HuskWatch.DefaultRoot(), state.Folder, state.Session)));
                if (used < ContextTokens) return false;
                ProjectRunState probe = new ProjectRunState { Day = state.Day, Started = state.Started, StoppedDay = state.StoppedDay };
                if (Refusal(on, entry, probe, Gaming(), perDay, now) != null || !ProjectHook.Current(turn.Session, turn.At)) return false;
                ProjectHook.Reply(turn.Session, turn.At, entry.Full ? WrapFull : entry.Watch || entry.Propose ? WrapWatch : WrapPlan, null);
                state.Wrapping = turn.At;
                Save(state);
                ActivityLog.Write("project session", entry.Name, "context at " + used + " tokens: asked it to wrap up for a fresh session");
                return true;
            }
        }

        private static bool Swap(ProjectRunState state, ProjectEntry entry, ProjectTurn turn, bool on, string[] lines, int perDay, DateTime now)
        {
            string said = turn.Text ?? "";
            int mark = said.IndexOf(HandoffMark, StringComparison.Ordinal);
            state.Wrapping = 0;
            Save(state);
            if (mark < 0 || entry == null)
            {
                ActivityLog.Write("project session", ProjectName(state), "its wrap-up ended without a handoff; it goes on as it is");
                return false;
            }
            string handoff = Regex.Replace(said.Substring(mark + HandoffMark.Length), @"\s+", " ").Trim();
            if (handoff.Length > HandoffChars) handoff = handoff.Substring(0, HandoffChars);
            if (ProjectHook.Current(turn.Session, turn.At)) ProjectHook.Reply(turn.Session, turn.At, "", null);
            Shut(state);
            End(state, FreshEnd, now);
            string name;
            string refused = Start(on, lines, perDay, entry.Folder, (entry.Propose ? ProjectPropose.Again : entry.Watch ? ProjectWatch.Task : Task) + HandoffLead + handoff, now, out name);
            if (refused == null) return true;
            ActivityLog.Write("project session", entry.Name, "no fresh session: " + refused + "; its handoff: " + handoff);
            ProjectRunState left = Load();
            FindReady(left, entry.Folder, left.Tips, entry.Full);
            left.Tips = "";
            left.Full = false;
            Save(left);
            return true;
        }
    }
}
