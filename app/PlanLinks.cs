using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed class PlanLink
    {
        public string Kind, Id, Asked, Words, Key;
        public long Made;
    }

    internal static class PlanLinks
    {
        internal const string Order = "order";
        internal const string Reminder = "reminder";
        internal const int LinkMinutes = 10;
        private const int KeepHours = 48;
        private const int Most = 50;
        private static readonly string[] Common = "the and for you him her his she with after about when this that then from have once".Split(' ');
        private static readonly object gate = new object();
        private static List<PlanLink> links = new List<PlanLink>();
        private static string loadedFrom;

        internal static string FilePath
        {
            get { return ActivityLog.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ActivityLog.FilePath), "plan-links.json"); }
        }

        internal static string IdOf(params string[] parts)
        {
            return Forgetting.Squash(string.Join(" ", parts));
        }

        internal static void Reset()
        {
            lock (gate)
            {
                links = new List<PlanLink>();
                loadedFrom = null;
            }
        }

        internal static void Seen(string kind, string id, string asked, string words, DateTime now)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (gate)
            {
                List<PlanLink> live = Current(now);
                live.RemoveAll(delegate(PlanLink one) { return one.Kind == kind && one.Id == id; });
                if (live.Count >= Most) live.RemoveAt(0);
                live.Add(new PlanLink { Kind = kind, Id = id, Asked = Forgetting.Squash(asked ?? ""), Words = words ?? "", Made = now.Ticks });
                Save();
            }
        }

        private static List<string> WordsOf(string text)
        {
            List<string> words = new List<string>();
            foreach (string word in Regex.Split((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+"))
                if (word.Length >= 3 && Array.IndexOf(Common, word) < 0 && !words.Contains(word)) words.Add(word);
            return words;
        }

        internal static bool Overlaps(PlanLink link, string plan)
        {
            List<string> planned = WordsOf(plan);
            int shared = 0;
            foreach (string word in WordsOf(link.Words)) if (planned.Contains(word)) shared++;
            return link.Kind == Order ? shared >= 1 : shared >= 2;
        }

        internal static int Link(string asked, string plan, DateTime now)
        {
            string said = Forgetting.Squash(asked ?? "");
            string key = Plans.KeyOf(plan ?? "");
            if (key.Length == 0) return 0;
            int linked = 0;
            lock (gate)
            {
                foreach (PlanLink one in Current(now))
                {
                    if (one.Key != null || (now - new DateTime(one.Made)).TotalMinutes > LinkMinutes) continue;
                    if (!(said.Length > 0 && one.Asked == said) && !Overlaps(one, plan)) continue;
                    one.Key = key;
                    linked++;
                }
                if (linked > 0) Save();
            }
            return linked;
        }

        internal static string Take(string kind, string id, DateTime now)
        {
            lock (gate)
            {
                PlanLink found = Current(now).Find(delegate(PlanLink one) { return one.Kind == kind && one.Id == id && one.Key != null; });
                if (found == null) return null;
                links.Remove(found);
                Save();
                return found.Key;
            }
        }

        internal static string Done(string kind, string id, DateTime now)
        {
            string key = Take(kind, id, now);
            string root = Vault.Root;
            if (key == null || root == null) return null;
            PlanLine plan = Plans.Open(root).Find(delegate(PlanLine one) { return one.Key == key; });
            if (plan == null) return null;
            bool changed = kind == Order ? Plans.Finish(root, key, false, now) : Plans.Park(root, key);
            return changed ? plan.Text : null;
        }

        private static List<PlanLink> Current(DateTime now)
        {
            string path = FilePath;
            if (loadedFrom != path)
            {
                loadedFrom = path;
                links = Load(path);
            }
            links.RemoveAll(delegate(PlanLink one) { return (now - new DateTime(one.Made)).TotalHours > KeepHours; });
            return links;
        }

        private static List<PlanLink> Load(string path)
        {
            if (path == null || !File.Exists(path)) return new List<PlanLink>();
            try
            {
                List<PlanLink> found = new JavaScriptSerializer().Deserialize<List<PlanLink>>(File.ReadAllText(path));
                return found ?? new List<PlanLink>();
            }
            catch (Exception error)
            {
                Brain.Log("plan-links.json", -1, error.GetType().Name + ": " + error.Message);
                return new List<PlanLink>();
            }
        }

        private static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".new";
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(links));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) { Brain.Log("plan-links.json", -1, error.GetType().Name + ": " + error.Message); }
        }
    }
}
