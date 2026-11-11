using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class PlanLine
    {
        internal string Id;
        internal string Text;
        internal string Key;
        internal DateTime? Due;
        internal DateTime? Made;
        internal DateTime? FollowUp;
        internal bool Maybe;
        internal bool Waiting;
        internal bool Stale;
    }

    internal static class Plans
    {
        private const string Head = "# Plans and promises\n> Capri keeps this list herself: what you or she said will happen, and maybes. She ticks things off when they are done. The Obsidian Tasks plugin can show it.\n\n";
        private const string Calendar = "\U0001F4C5";
        private const string Ticked = "✅";
        private const string Dropped = "❌";
        private const string FollowMark = "\u23F3";
        private const string StaleTag = "#stale";
        private const int LeastText = 8;
        private const int MostText = 200;
        private const int MostListed = 15;
        private const int MostBytes = 200000;
        private static readonly Regex Task = new Regex(@"^\s*- \[( |/|x|X|-)\] (.+?)(?: \(\[\[Journal/[^\]\r\n]*\]\]\))?(?: " + Calendar + @" (\d{4}-\d{2}-\d{2}))?(?: " + FollowMark + @" (\d{4}-\d{2}-\d{2}))?( " + StaleTag + @")?(?: (?:✅|❌) \d{4}-\d{2}-\d{2})?\s*$", RegexOptions.Compiled);
        private static readonly Regex Unsafe = new Regex(@"https?://|www\.|[`<>{}$\\|\[\]]|\.(exe|ps1|bat|cmd)\b|\b(ignore|disregard|instructions?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Finished = new Regex(@"^\s*- \[(?:x|X|-)\] .*(?:✅|❌) (\d{4}-\d{2}-\d{2})\s*$", RegexOptions.Compiled);
        private static readonly Regex Said = new Regex(@"\[\[Journal/(\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);
        private static readonly object gate = new object();

        internal static string PathIn(string root)
        {
            return Path.Combine(Path.Combine(root, "Capri"), "plans.md");
        }

        private static string Clean(string text)
        {
            return Regex.Replace(text ?? "", @"\s+", " ").Trim().TrimEnd('.', ' ');
        }

        internal static bool Ok(string text)
        {
            string clean = Clean(text);
            return clean.Length >= LeastText && clean.Length <= MostText && !Brain.Personal.IsMatch(clean) && !Unsafe.IsMatch(clean);
        }

        private static List<string> Read(string root)
        {
            string path = PathIn(root);
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length > MostBytes) return new List<string>();
                return new List<string>(File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'));
            }
            catch (IOException) { return new List<string>(); }
            catch (UnauthorizedAccessException) { return new List<string>(); }
        }

        private static List<KeyValuePair<int, PlanLine>> Scan(List<string> lines)
        {
            List<KeyValuePair<int, PlanLine>> found = new List<KeyValuePair<int, PlanLine>>();
            for (int i = 0; i < lines.Count; i++)
            {
                Match task = Task.Match(lines[i]);
                if (!task.Success || (task.Groups[1].Value != " " && task.Groups[1].Value != "/")) continue;
                PlanLine plan = new PlanLine();
                plan.Text = task.Groups[2].Value.Trim();
                plan.Key = Forgetting.Squash(plan.Text);
                plan.Maybe = plan.Text.StartsWith("maybe", StringComparison.OrdinalIgnoreCase);
                DateTime due;
                if (task.Groups[3].Success && DateTime.TryParseExact(task.Groups[3].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out due)) plan.Due = due;
                DateTime follow;
                if (task.Groups[4].Success && DateTime.TryParseExact(task.Groups[4].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out follow)) plan.FollowUp = follow;
                plan.Waiting = task.Groups[1].Value == "/";
                plan.Stale = task.Groups[5].Success;
                Match said = Said.Match(lines[i]);
                DateTime made;
                if (said.Success && DateTime.TryParseExact(said.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out made)) plan.Made = made;
                plan.Id = "P" + (found.Count + 1);
                found.Add(new KeyValuePair<int, PlanLine>(i, plan));
            }
            return found;
        }

        internal static List<PlanLine> Open(string root)
        {
            List<PlanLine> found = new List<PlanLine>();
            if (root == null) return found;
            lock (gate)
                foreach (KeyValuePair<int, PlanLine> pair in Scan(Read(root))) found.Add(pair.Value);
            return found;
        }

        internal static string Listing(string root)
        {
            List<PlanLine> open = Open(root);
            if (open.Count == 0) return "";
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < open.Count && i < MostListed; i++)
                text.Append("\n").Append(open[i].Id).Append(" ").Append(open[i].Text).Append(open[i].Due.HasValue ? " (due " + open[i].Due.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + ")" : "").Append(State(open[i]));
            return text.ToString();
        }

        private static string State(PlanLine plan)
        {
            if (plan.Stale) return " (stale, no longer raised)";
            if (!plan.Waiting) return "";
            return " (waiting on someone else" + (plan.FollowUp.HasValue ? ", follow up " + plan.FollowUp.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "") + ")";
        }

        internal static string Current(string root, string key, DateTime now)
        {
            if (root == null || string.IsNullOrEmpty(key)) return null;
            List<string> lines;
            lock (gate) lines = Read(root);
            if (lines.Count == 0) return null;
            foreach (string line in lines)
            {
                Match task = Task.Match(line);
                if (!task.Success || Forgetting.Squash(task.Groups[2].Value.Trim()) != key) continue;
                string box = task.Groups[1].Value;
                if (box == "-") return "plan dropped";
                if (box != " " && box != "/") return "plan done";
                if (task.Groups[5].Success) return "plan stale";
                DateTime follow;
                if (box == "/" && task.Groups[4].Success && DateTime.TryParseExact(task.Groups[4].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out follow) && follow.Date > now.Date)
                    return "waiting until " + follow.ToString("d MMM", CultureInfo.InvariantCulture);
                return null;
            }
            return "plan gone";
        }

        internal static string KeyOf(string text)
        {
            return Forgetting.Squash(Clean(text));
        }

        private static bool Raised(DateTime? when, DateTime now)
        {
            return when.HasValue && when.Value.Date <= now.Date && when.Value.Date >= now.Date.AddDays(-1);
        }

        internal static List<PlanLine> Overdue(string root, DateTime now)
        {
            StudyBook study = Study.Safe();
            return Open(root).FindAll(delegate(PlanLine plan) { return !plan.Maybe && !plan.Waiting && !plan.Stale && plan.Due.HasValue && plan.Due.Value.Date <= now.Date && !Study.TheirTurn(study, plan.Text, now); });
        }

        internal static List<PlanLine> Due(string root, DateTime now)
        {
            StudyBook study = Study.Safe();
            return Open(root).FindAll(delegate(PlanLine plan) { return !plan.Maybe && !plan.Stale && Raised(plan.Waiting ? plan.FollowUp : plan.Due, now) && (plan.Waiting || !Study.TheirTurn(study, plan.Text, now)); });
        }

        internal static string Describe(PlanLine plan, DateTime now)
        {
            if (plan.Waiting) return "Waiting plan, he did his part and waits on someone else; follow-up " + (plan.FollowUp.HasValue && plan.FollowUp.Value.Date < now.Date ? "was " + plan.FollowUp.Value.ToString("d MMM", CultureInfo.InvariantCulture) : "today") + ", ask once whether it came through: " + plan.Text;
            if (!plan.Due.HasValue || plan.Due.Value.Date >= now.Date) return "Plan due today: " + plan.Text;
            return "Plan overdue (it was due " + plan.Due.Value.ToString("d MMM", CultureInfo.InvariantCulture) + "): " + plan.Text;
        }

        internal static bool Add(string root, string text, DateTime? due, DateTime day)
        {
            if (root == null || !Ok(text)) return false;
            string clean = Clean(text);
            string key = Forgetting.Squash(clean);
            lock (gate)
            {
                try
                {
                    List<string> lines = Read(root);
                    foreach (KeyValuePair<int, PlanLine> pair in Scan(lines))
                        if (pair.Value.Key == key) return false;
                    string line = "- [ ] " + clean + NoteEdit.Source(day) + (due.HasValue ? " " + Calendar + " " + due.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "");
                    string path = PathIn(root);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string body = File.Exists(path) ? File.ReadAllText(path) : Head;
                    if (body.Length > 0 && !body.EndsWith("\n", StringComparison.Ordinal)) body += "\n";
                    File.WriteAllText(path, body + line + "\n");
                    ActivityLog.Write("plans", "added", clean + (due.HasValue ? ", due " + due.Value.ToString("d MMM", CultureInfo.InvariantCulture) : ""));
                    return true;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        internal static List<string> Archive(string root, DateTime now, int closedDays, int maybeDays, Action<string> before)
        {
            List<string> moved = new List<string>();
            if (root == null) return moved;
            lock (gate)
            {
                try
                {
                    List<string> lines = Read(root);
                    HashSet<int> maybes = new HashSet<int>();
                    foreach (KeyValuePair<int, PlanLine> pair in Scan(lines))
                        if (pair.Value.Maybe) maybes.Add(pair.Key);
                    List<string> kept = new List<string>();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        Match closed = Finished.Match(lines[i]);
                        Match said = Said.Match(lines[i]);
                        DateTime day;
                        bool old = closed.Success ? DateTime.TryParseExact(closed.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day) && day < now.Date.AddDays(-closedDays)
                            : maybes.Contains(i) && said.Success && DateTime.TryParseExact(said.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day) && day < now.Date.AddDays(-maybeDays);
                        if (old) moved.Add(lines[i].Trim()); else kept.Add(lines[i]);
                    }
                    if (moved.Count == 0) return moved;
                    string path = PathIn(root);
                    if (before != null) before(path);
                    string archive = Path.Combine(Path.GetDirectoryName(path), "plans-archive.md");
                    string head = File.Exists(archive) ? "" : "# Plans archive\n> Old finished plans and old maybes, moved out of plans.md.\n\n";
                    File.AppendAllText(archive, head + string.Join("\n", moved.ToArray()) + "\n");
                    File.WriteAllText(path, string.Join("\n", kept.ToArray()));
                    ActivityLog.Write("plans", "archived", moved.Count + (moved.Count == 1 ? " old plan" : " old plans"));
                    return moved;
                }
                catch (IOException) { return new List<string>(); }
                catch (UnauthorizedAccessException) { return new List<string>(); }
            }
        }

        internal static bool Wait(string root, string key, DateTime? follow, DateTime day)
        {
            if (root == null || key == null) return false;
            lock (gate)
            {
                try
                {
                    List<string> lines = Read(root);
                    foreach (KeyValuePair<int, PlanLine> pair in Scan(lines))
                    {
                        if (pair.Value.Key != key) continue;
                        string line = Regex.Replace(lines[pair.Key], " " + FollowMark + @" \d{4}-\d{2}-\d{2}| " + StaleTag, "").TrimEnd();
                        int box = line.IndexOf("- [ ] ", StringComparison.Ordinal);
                        if (box < 0) box = line.IndexOf("- [/] ", StringComparison.Ordinal);
                        line = line.Substring(0, box) + "- [/] " + line.Substring(box + 6);
                        if (follow.HasValue) line += " " + FollowMark + " " + follow.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        lines[pair.Key] = line;
                        File.WriteAllText(PathIn(root), string.Join("\n", lines.ToArray()));
                        ActivityLog.Write("plans", "waiting", pair.Value.Text + (follow.HasValue ? ", follow up " + follow.Value.ToString("d MMM", CultureInfo.InvariantCulture) : ""));
                        return true;
                    }
                    return false;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        internal static bool Park(string root, string key)
        {
            if (root == null || key == null) return false;
            lock (gate)
            {
                try
                {
                    List<string> lines = Read(root);
                    foreach (KeyValuePair<int, PlanLine> pair in Scan(lines))
                    {
                        if (pair.Value.Key != key) continue;
                        if (pair.Value.Stale) return true;
                        lines[pair.Key] = lines[pair.Key].TrimEnd() + " " + StaleTag;
                        File.WriteAllText(PathIn(root), string.Join("\n", lines.ToArray()));
                        ActivityLog.Write("plans", "parked", pair.Value.Text);
                        return true;
                    }
                    return false;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        internal static List<string> Expire(string root, DateTime now)
        {
            List<string> gone = new List<string>();
            if (root == null) return gone;
            lock (gate)
            {
                try
                {
                    List<string> lines = Read(root);
                    foreach (KeyValuePair<int, PlanLine> pair in Scan(lines))
                    {
                        PlanLine plan = pair.Value;
                        DateTime? when = plan.Waiting ? plan.FollowUp : plan.Due;
                        if (plan.Stale || plan.Maybe || !when.HasValue || when.Value.Date >= now.Date.AddDays(-1)) continue;
                        lines[pair.Key] = lines[pair.Key].TrimEnd() + " " + StaleTag;
                        gone.Add(plan.Text);
                    }
                    if (gone.Count == 0) return gone;
                    File.WriteAllText(PathIn(root), string.Join("\n", lines.ToArray()));
                    foreach (string text in gone) ActivityLog.Write("plans", "stale", text);
                    return gone;
                }
                catch (IOException) { return gone; }
                catch (UnauthorizedAccessException) { return gone; }
            }
        }

        internal static bool Close(string root, string id, bool dropped, DateTime day)
        {
            return id != null && Mark(root, delegate(PlanLine plan) { return string.Equals(plan.Id, id, StringComparison.OrdinalIgnoreCase); }, dropped, day);
        }

        internal static bool Finish(string root, string key, bool dropped, DateTime day)
        {
            return key != null && Mark(root, delegate(PlanLine plan) { return plan.Key == key; }, dropped, day);
        }

        private static bool Mark(string root, Predicate<PlanLine> wanted, bool dropped, DateTime day)
        {
            if (root == null) return false;
            lock (gate)
            {
                try
                {
                    List<string> lines = Read(root);
                    foreach (KeyValuePair<int, PlanLine> pair in Scan(lines))
                    {
                        if (!wanted(pair.Value)) continue;
                        string line = lines[pair.Key];
                        int box = line.IndexOf("- [ ] ", StringComparison.Ordinal);
                        if (box < 0) box = line.IndexOf("- [/] ", StringComparison.Ordinal);
                        lines[pair.Key] = line.Substring(0, box) + (dropped ? "- [-] " : "- [x] ") + line.Substring(box + 6).TrimEnd() + " " + (dropped ? Dropped : Ticked) + " " + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        File.WriteAllText(PathIn(root), string.Join("\n", lines.ToArray()));
                        ActivityLog.Write("plans", dropped ? "dropped" : "done", pair.Value.Text);
                        return true;
                    }
                    return false;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }
    }
}
