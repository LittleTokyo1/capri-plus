using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class NoteLine
    {
        internal string Id;
        internal string Path;
        internal string Label;
        internal string Text;
        internal DateTime? Said;
    }

    internal static class NoteEdit
    {
        private const int MostCandidates = 40;
        private const int MostLine = 200;
        private const int MostRemoved = 3;
        private const int MostPastRemoved = 30;
        private const int MostFile = 60000;
        private static readonly object gate = new object();
        private static readonly Regex Skip = new Regex(@"\[\[|^(also called|related|usernames?|accounts?|real name)\s*:|^name\s*:\s*not known yet", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Heading = new Regex(@"^\s*(#|>|---)", RegexOptions.Compiled);
        private static readonly Regex SaidDay = new Regex(@"\(\[\[Journal/(?<day>\d{4}-\d\d-\d\d)(?:\|[^\]\r\n]*)?\]\]\)\s*$", RegexOptions.Compiled);
        private static readonly Regex Tail = new Regex(@"\s*\(\[\[[^\]\r\n]*\]\]\)\s*$", RegexOptions.Compiled);
        private static readonly Regex HistoryHead = new Regex(@"^##\s+History\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HistoryAny = new Regex(@"^##\s+History\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex CoreHead = new Regex(@"^##\s+Core\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SourcesLine =new Regex(@"^sources\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FieldText = new Regex(@"^\p{L}[\p{L} ]{0,20}:\s", RegexOptions.Compiled);
        private static readonly Regex Word = new Regex(@"[\p{L}\p{N}]{3,}", RegexOptions.Compiled);
        private static readonly string[] Plain = { "the", "and", "that", "this", "with", "have", "for", "are", "was", "not", "but", "you", "your", "they", "them", "their", "anymore", "longer", "just", "now", "like", "really", "about", "from", "what", "when", "where", "don", "didn", "isn", "aren", "his", "her", "she", "him", "has", "had", "can", "will" };

        internal static List<string> Files()
        {
            List<string> found = new List<string>();
            string core = Brain.MemoryPath;
            if (core != null && File.Exists(core)) found.Add(core);
            if (Vault.Root != null)
                foreach (string path in Vault.Files(Vault.Root))
                    if (!found.Exists(delegate(string known) { return string.Equals(known, path, StringComparison.OrdinalIgnoreCase); })) found.Add(path);
            return found;
        }

        internal static string LabelOf(string path)
        {
            string root = Vault.Root;
            if (root != null && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
            return System.IO.Path.GetFileName(path);
        }

        internal static string Bare(string line)
        {
            return Tail.Replace(line ?? "", "");
        }

        internal static bool IsHistory(string line)
        {
            return HistoryHead.IsMatch((line ?? "").Trim());
        }

        internal static string Current(string body)
        {
            if (body == null) return null;
            if (body.IndexOf("[[", StringComparison.Ordinal) < 0 && !HistoryAny.IsMatch(body)) return body;
            List<string> kept = new List<string>();
            bool past = false;
            foreach (string line in body.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.Trim();
                if (HistoryHead.IsMatch(trimmed)) { past = true; continue; }
                if (past && trimmed.StartsWith("#", StringComparison.Ordinal)) past = false;
                if (past || SourcesLine.IsMatch(trimmed)) continue;
                kept.Add(Bare(line));
            }
            return string.Join("\n", kept.ToArray());
        }

        internal static bool IsCoreHead(string line)
        {
            return CoreHead.IsMatch((line ?? "").Trim());
        }

        internal static void CoreSpan(List<string> lines, out int start, out int end)
        {
            start = lines.FindIndex(delegate(string line) { return CoreHead.IsMatch(line.Trim()); });
            end = start;
            if (start < 0) return;
            end = start + 1;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith("#", StringComparison.Ordinal)) end++;
        }

        internal static List<string> CoreLines(string body)
        {
            List<string> found = new List<string>();
            List<string> lines = new List<string>((body ?? "").Replace("\r\n", "\n").Split('\n'));
            int start, end;
            CoreSpan(lines, out start, out end);
            for (int i = start + 1; start >= 0 && i < end; i++)
                if (lines[i].Trim().Length > 0) found.Add(lines[i].Trim());
            return found;
        }

        internal static string OutsideCore(string body)
        {
            if (body == null) return null;
            List<string> lines = new List<string>(body.Replace("\r\n", "\n").Split('\n'));
            int start, end;
            CoreSpan(lines, out start, out end);
            if (start < 0) return body;
            lines.RemoveRange(start, end - start);
            return string.Join("\n", lines.ToArray());
        }

        internal static List<string> Past(string body)
        {
            List<string> found = new List<string>();
            bool past = false;
            foreach (string line in (body ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.Trim();
                if (HistoryHead.IsMatch(trimmed)) { past = true; continue; }
                if (past && trimmed.StartsWith("#", StringComparison.Ordinal)) past = false;
                if (past && (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))) found.Add(Bare(trimmed.Substring(2).Trim()));
            }
            return found;
        }

        internal static bool IsField(string text)
        {
            return FieldText.IsMatch((text ?? "").Trim());
        }

        internal static string Source(DateTime day)
        {
            string root = Vault.Root;
            if (root == null) return "";
            string stamp = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            try
            {
                string folder = System.IO.Path.Combine(root, "Journal");
                string path = System.IO.Path.Combine(folder, stamp + ".md");
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(path, "# " + stamp + "\n\n");
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return " (" + Link(day) + ")";
        }

        private static string Link(DateTime day)
        {
            return "[[Journal/" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + day.ToString("d MMM", CultureInfo.InvariantCulture) + "]]";
        }

        internal static void Stamp(List<string> lines, ICollection<string> before, DateTime day)
        {
            bool field = false;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (before.Contains(line)) continue;
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)) continue;
                bool bullet = trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal);
                if (bullet && !IsField(trimmed.Substring(2))) { if (Bare(line) == line) lines[i] = line.TrimEnd() + Source(day); }
                else field = true;
            }
            if (field) Sourced(lines, day);
        }

        internal static void Sourced(List<string> lines, DateTime day)
        {
            if (Source(day).Length == 0) return;
            string link = Link(day);
            int at = lines.FindIndex(delegate(string line) { return SourcesLine.IsMatch(line.Trim()); });
            if (at >= 0)
            {
                if (lines[at].IndexOf(link, StringComparison.Ordinal) < 0) lines[at] = lines[at].TrimEnd() + ", " + link;
                return;
            }
            int history = lines.FindIndex(delegate(string line) { return HistoryHead.IsMatch(line.Trim()); });
            int end = history >= 0 ? history : lines.Count;
            while (end > 0 && lines[end - 1].Trim().Length == 0) end--;
            lines.Insert(end, "Sources: " + link);
        }

        internal static int Ending(List<string> lines)
        {
            int stop = lines.FindIndex(delegate(string line) { string trimmed = line.Trim(); return SourcesLine.IsMatch(trimmed) || HistoryHead.IsMatch(trimmed); });
            if (stop < 0) return -1;
            while (stop > 0 && lines[stop - 1].Trim().Length == 0) stop--;
            return stop;
        }

        internal static string Append(string body, string line)
        {
            string newline = body.Contains("\r\n") ? "\r\n" : "\n";
            List<string> lines = new List<string>(body.Replace("\r\n", "\n").Split('\n'));
            int stop = Ending(lines);
            if (stop < 0)
            {
                string text = body.Length > 0 && !body.EndsWith("\n", StringComparison.Ordinal) ? body + newline : body;
                return text + line + newline;
            }
            lines.Insert(stop, line);
            return string.Join(newline, lines.ToArray());
        }

        internal static bool SetLine(string path, string prefix, string line)
        {
            lock (gate)
            {
                try
                {
                    string body = Read(path);
                    if (body == null) return false;
                    string newline = body.Contains("\r\n") ? "\r\n" : "\n";
                    List<string> lines = new List<string>(body.Replace("\r\n", "\n").Split('\n'));
                    int history = lines.FindIndex(delegate(string known) { return HistoryHead.IsMatch(known.Trim()); });
                    int end = history < 0 ? lines.Count : history;
                    for (int i = 0; i < end; i++)
                    {
                        string text;
                        if (!Bullet(lines[i], out text) || !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        if (text == line) return false;
                        lines[i] = "- " + line;
                        File.WriteAllText(path, string.Join(newline, lines.ToArray()));
                        return true;
                    }
                    File.WriteAllText(path, Append(body, "- " + line));
                    return true;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        private static void Archive(List<string> lines, string old, DateTime day)
        {
            string entry = "- until " + day.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + ": " + old;
            int head = lines.FindIndex(delegate(string line) { return HistoryHead.IsMatch(line.Trim()); });
            if (head < 0)
            {
                bool ending = lines.Count > 0 && lines[lines.Count - 1].Length == 0;
                while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
                lines.Add("");
                lines.Add("## History");
                lines.Add(entry);
                if (ending) lines.Add("");
                return;
            }
            int end = head + 1;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith("#", StringComparison.Ordinal)) end++;
            while (end > head + 1 && lines[end - 1].Trim().Length == 0) end--;
            lines.Insert(end, entry);
        }

        internal static List<NoteLine> Candidates(string said)
        {
            List<NoteLine> found = new List<NoteLine>();
            List<string> words = Words(said);
            string core = Brain.MemoryPath;
            foreach (string path in Files())
            {
                bool own = string.Equals(path, core, StringComparison.OrdinalIgnoreCase);
                string raw = Read(path);
                Dictionary<string, DateTime> days = SaidDays(raw);
                string body = OutsideCore(Current(raw));
                if (body == null) continue;
                foreach (string line in body.Split('\n'))
                {
                    string text;
                    if (!Bullet(line, out text) || (!own && !Shares(text, words))) continue;
                    if (found.Count >= MostCandidates) return Number(found);
                    NoteLine note = new NoteLine();
                    note.Path = path;
                    note.Label = LabelOf(path);
                    note.Text = text;
                    DateTime day;
                    if (days.TryGetValue(line.Trim(), out day)) note.Said = day;
                    found.Add(note);
                }
            }
            return Number(found);
        }

        internal static string Format(List<NoteLine> lines)
        {
            StringBuilder text = new StringBuilder();
            foreach (NoteLine line in lines) text.Append(line.Id + " [" + line.Label + "] " + line.Text + (line.Said == null ? "" : " (said " + line.Said.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + ")") + "\n");
            return text.ToString().TrimEnd('\n');
        }

        private static Dictionary<string, DateTime> SaidDays(string raw)
        {
            Dictionary<string, DateTime> days = new Dictionary<string, DateTime>();
            if (raw == null) return days;
            foreach (string line in raw.Replace("\r\n", "\n").Split('\n'))
            {
                Match found = SaidDay.Match(line);
                DateTime day;
                if (!found.Success || !DateTime.TryParseExact(found.Groups["day"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) continue;
                days[Bare(line).Trim()] = day;
            }
            return days;
        }

        internal static bool Replace(NoteLine line, string replacement)
        {
            return Replace(line, replacement, DateTime.Now);
        }

        internal static bool Replace(NoteLine line, string replacement, DateTime day)
        {
            return Edit(line, replacement, day, true);
        }

        internal static bool Drop(NoteLine line)
        {
            return Edit(line, "", DateTime.Now, false);
        }

        private static bool Edit(NoteLine line, string replacement, DateTime day, bool keep)
        {
            lock (gate)
            {
                try
                {
                    string body = Read(line.Path);
                    if (body == null) return false;
                    string newline = body.Contains("\r\n") ? "\r\n" : "\n";
                    List<string> lines = new List<string>(body.Replace("\r\n", "\n").Split('\n'));
                    int history = lines.FindIndex(delegate(string known) { return HistoryHead.IsMatch(known.Trim()); });
                    int coreStart, coreEnd;
                    CoreSpan(lines, out coreStart, out coreEnd);
                    string wanted = Forgetting.Squash(line.Text);
                    int at = -1;
                    List<int> copies = new List<int>();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        string text;
                        if (!Bullet(lines[i], out text) || Forgetting.Squash(text) != wanted) continue;
                        if (coreStart >= 0 && i > coreStart && i < coreEnd) copies.Add(i);
                        else if (at < 0) at = i;
                    }
                    if (at < 0 || (history >= 0 && at > history)) return false;
                    string trimmed = lines[at].TrimStart();
                    string lead = lines[at].Substring(0, lines[at].Length - trimmed.Length) + trimmed.Substring(0, 2);
                    string old = trimmed.Substring(2).Trim();
                    for (int i = copies.Count - 1; i >= 0; i--)
                    {
                        string copy = lines[copies[i]].TrimStart();
                        if (replacement.Length == 0) lines.RemoveAt(copies[i]);
                        else lines[copies[i]] = lines[copies[i]].Substring(0, lines[copies[i]].Length - copy.Length) + copy.Substring(0, 2) + replacement;
                    }
                    if (replacement.Length == 0) at -= copies.FindAll(delegate(int copy) { return copy < at; }).Count;
                    if (replacement.Length == 0) lines.RemoveAt(at);
                    else lines[at] = lead + replacement + (IsField(replacement) ? "" : Source(day));
                    if (keep) Archive(lines, old, day);
                    if (replacement.Length > 0 && IsField(replacement)) Sourced(lines, day);
                    Write(line.Path, string.Join(newline, lines.ToArray()));
                    Undo(line.Label, line.Text, replacement);
                    return true;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        internal static int Remove(List<string> topics)
        {
            if (topics == null || topics.Count == 0) return 0;
            List<string> flat = topics.ConvertAll(delegate(string topic) { return Forgetting.Squash(topic); });
            lock (gate)
            {
                try
                {
                    Dictionary<string, List<string>> hits = new Dictionary<string, List<string>>();
                    int total = 0, old = 0;
                    foreach (string path in Files())
                    {
                        string body = Read(path);
                        if (body == null) continue;
                        bool past = false, copy = false;
                        foreach (string line in body.Replace("\r\n", "\n").Split('\n'))
                        {
                            string trimmed = line.Trim();
                            if (HistoryHead.IsMatch(trimmed)) { past = true; continue; }
                            if (CoreHead.IsMatch(trimmed)) { copy = true; continue; }
                            if (trimmed.StartsWith("#", StringComparison.Ordinal)) { past = false; copy = false; }
                            if (trimmed.Length == 0 || Heading.IsMatch(trimmed) || trimmed.StartsWith("Related:", StringComparison.OrdinalIgnoreCase) || Bare(trimmed).IndexOf("[[", StringComparison.Ordinal) >= 0) continue;
                            string squashed = Forgetting.Squash(trimmed);
                            if (!flat.Exists(delegate(string topic) { return squashed.Contains(topic); })) continue;
                            if (!hits.ContainsKey(path)) hits[path] = new List<string>();
                            hits[path].Add(line);
                            if (past || copy) old++; else total++;
                        }
                    }
                    if (total + old == 0 || total > MostRemoved || old > MostPastRemoved) return 0;
                    foreach (KeyValuePair<string, List<string>> file in hits)
                    {
                        string body = Read(file.Key);
                        string newline = body.Contains("\r\n") ? "\r\n" : "\n";
                        List<string> kept = new List<string>();
                        foreach (string line in body.Replace("\r\n", "\n").Split('\n'))
                            if (file.Value.Contains(line)) Undo(LabelOf(file.Key), line.Trim(), ""); else kept.Add(line);
                        Write(file.Key, string.Join(newline, kept.ToArray()));
                        ActivityLog.Write("memory", "forgot", LabelOf(file.Key));
                    }
                    return total + old;
                }
                catch (IOException) { return 0; }
                catch (UnauthorizedAccessException) { return 0; }
            }
        }

        private static List<NoteLine> Number(List<NoteLine> lines)
        {
            for (int i = 0; i < lines.Count; i++) lines[i].Id = "L" + (i + 1);
            return lines;
        }

        private static bool Bullet(string line, out string text)
        {
            string trimmed = line.Trim();
            text = null;
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal) && !trimmed.StartsWith("* ", StringComparison.Ordinal)) return false;
            text = Bare(trimmed.Substring(2).Trim());
            return text.Length >= 4 && text.Length <= MostLine && !Skip.IsMatch(text);
        }

        private static List<string> Words(string said)
        {
            List<string> words = new List<string>();
            foreach (Match match in Word.Matches((said ?? "").ToLowerInvariant()))
                if (Array.IndexOf(Plain, match.Value) < 0 && !words.Contains(match.Value)) words.Add(match.Value);
            return words;
        }

        private static bool Shares(string text, List<string> words)
        {
            string lower = text.ToLowerInvariant();
            return words.Exists(delegate(string word) { return lower.Contains(word); });
        }

        private static string Read(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                return !info.Exists || info.Length > MostFile ? null : File.ReadAllText(path);
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static void Write(string path, string body)
        {
            if (string.Equals(path, Brain.MemoryPath, StringComparison.OrdinalIgnoreCase)) Brain.Locked(delegate { File.WriteAllText(path, body); });
            else File.WriteAllText(path, body);
        }

        private static void Undo(string label, string old, string replacement)
        {
            try
            {
                string folder = System.IO.Path.GetDirectoryName(ActivityLog.FilePath ?? "");
                if (string.IsNullOrEmpty(folder)) return;
                File.AppendAllText(System.IO.Path.Combine(folder, "corrections.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + label + "  -  " + old + (replacement.Length == 0 ? "" : "  =>  " + replacement) + "\r\n");
                ActivityLog.Write("memory", replacement.Length == 0 ? "removed a line" : "rewrote a line", label);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
