using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed class HeldJob
    {
        internal string Tool;
        internal string Said;
        internal DateTime At;
    }

    internal static class AfterJobs
    {
        internal const int KeepHours = 3;
        internal const int Most = 3;
        internal const int SaidChars = 140;
        private static readonly List<HeldJob> held = new List<HeldJob>();
        private static readonly object gate = new object();

        internal static string Describe(string tool, ToolArguments args)
        {
            ToolArguments a = args ?? new ToolArguments();
            if (!string.IsNullOrWhiteSpace(a.Task)) return Pursuit.Shorter(a.Task.Trim(), SaidChars);
            if (tool == "open_app" && !string.IsNullOrWhiteSpace(a.Name)) return "open " + a.Name.Trim();
            return Pursuit.Shorter((tool ?? "a job").Replace('_', ' ') + " " + ActivityLog.Describe(a), SaidChars).Trim();
        }

        internal static void Hold(string tool, ToolArguments args, DateTime now)
        {
            string said = Describe(tool, args);
            if (said.Length == 0) return;
            lock (gate)
            {
                held.RemoveAll(delegate(HeldJob one) { return (now - one.At).TotalHours > KeepHours || one.Said == said; });
                held.Add(new HeldJob { Tool = tool, Said = said, At = now });
                if (held.Count > Most) held.RemoveRange(0, held.Count - Most);
            }
            ActivityLog.Write("after game", "job held", said);
        }

        internal static List<HeldJob> Take(DateTime now)
        {
            lock (gate)
            {
                List<HeldJob> fresh = held.FindAll(delegate(HeldJob one) { return (now - one.At).TotalHours <= KeepHours; });
                held.Clear();
                return fresh;
            }
        }

        internal static bool Waiting(DateTime now)
        {
            lock (gate) return held.Exists(delegate(HeldJob one) { return (now - one.At).TotalHours <= KeepHours; });
        }

        internal static void Reset() { lock (gate) held.Clear(); }

        internal static string Joined(List<HeldJob> jobs)
        {
            return string.Join("; ", jobs.ConvertAll(delegate(HeldJob one) { return one.Said; }).ToArray());
        }
    }
}
