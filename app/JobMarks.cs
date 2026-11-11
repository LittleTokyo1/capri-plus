using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class JobMark
    {
        internal int Id;
        internal string Kind;
        internal DateTime Started;
        internal DateTime Ended = DateTime.MinValue;
        internal bool Ok;
        internal bool Running { get { return Ended == DateTime.MinValue; } }
    }

    internal static class JobMarks
    {
        internal const int CheckMs = 2500;
        internal const int AnswerAfterSeconds = 1;
        internal const int Most = 4;
        private static readonly object gate = new object();
        private static readonly Regex MailWords = new Regex(@"\b(?:e-?mail|mail|drafts?|register)\b", RegexOptions.IgnoreCase);
        private static List<JobMark> marks = new List<JobMark>();
        private static int next;
        private static int projectId = -1;

        internal static string KindOf(string label, string folder, string studyFolder)
        {
            if (MailWords.IsMatch(label ?? "")) return "mail";
            string here = (folder ?? "").TrimEnd('\\', '/');
            string study = (studyFolder ?? "").TrimEnd('\\', '/');
            return study.Length > 0 && string.Equals(here, study, StringComparison.OrdinalIgnoreCase) ? "study" : "project";
        }

        internal static int Begin(string kind, DateTime now)
        {
            lock (gate)
            {
                int id = ++next;
                List<JobMark> kept = Fresh(now);
                kept.Add(new JobMark { Id = id, Kind = kind, Started = now });
                marks = kept;
                return id;
            }
        }

        internal static void End(int id, bool ok, DateTime now)
        {
            lock (gate)
            {
                List<JobMark> kept = new List<JobMark>();
                foreach (JobMark one in marks)
                    kept.Add(one.Id == id && one.Running ? new JobMark { Id = one.Id, Kind = one.Kind, Started = one.Started, Ended = now, Ok = ok } : one);
                marks = kept;
            }
        }

        internal static void Project(bool running, DateTime now)
        {
            lock (gate)
            {
                if (running && projectId < 0) projectId = Begin("project", now);
                else if (!running && projectId >= 0) { End(projectId, true, now); projectId = -1; }
            }
        }

        private static List<JobMark> Fresh(DateTime now)
        {
            return marks.FindAll(delegate(JobMark one) { return one.Running || (now - one.Ended).TotalMilliseconds < CheckMs; });
        }

        internal static List<JobMark> Live(DateTime now)
        {
            lock (gate)
            {
                List<JobMark> live = Fresh(now);
                return live.Count > Most ? live.GetRange(live.Count - Most, Most) : live;
            }
        }

        internal static string Json(DateTime now)
        {
            StringBuilder json = new StringBuilder("[");
            List<JobMark> live = Live(now);
            for (int i = 0; i < live.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append("{\"kind\":\"").Append(live[i].Kind).Append("\",\"done\":").Append(live[i].Running ? "false" : "true").Append(",\"ok\":").Append(live[i].Ok || live[i].Running ? "true" : "false").Append('}');
            }
            return json.Append(']').ToString();
        }

        internal static void Reset()
        {
            lock (gate)
            {
                marks = new List<JobMark>();
                projectId = -1;
            }
        }
    }
}
