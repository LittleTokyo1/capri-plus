using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class ChatArchive
    {
        private const int MostLine = 2000;
        private const int MostForgotten = 200;
        private const string Private = "(left out: private)";
        private const int KeyChars = 120;
        private static readonly object gate = new object();
        private static HashSet<string> window = new HashSet<string>();

        internal static void Keep(IList<ChatMessage> history)
        {
            HashSet<string> kept = new HashSet<string>();
            if (history != null)
                foreach (ChatMessage message in history)
                    if (message != null && !string.IsNullOrEmpty(message.Content)) kept.Add(Key(message.Content));
            window = kept;
        }

        internal static bool InWindow(string text)
        {
            return text != null && window.Contains(Key(text));
        }

        private static string Key(string text)
        {
            string flat = Regex.Replace(text, @"\s+", " ").Trim();
            if (Brain.Personal.IsMatch(flat)) flat = Private;
            flat = flat.ToLowerInvariant();
            return flat.Length > KeyChars ? flat.Substring(0, KeyChars) : flat;
        }

        internal static string FolderIn(string root)
        {
            return Path.Combine(root, "Capri", "chats");
        }

        internal static void Seen(string saw, string said, DateTime now)
        {
            string root = Vault.Root;
            string text = Regex.Replace(saw ?? "", @"\s+", " ").Trim();
            if (!Fork.MemoryOn || root == null || ActivityLog.FilePath == null || text.Length == 0) return;
            if (Brain.Personal.IsMatch(text)) text = Private;
            if (text.Length > MostLine) text = text.Substring(0, MostLine);
            string line = now.ToString("HH:mm", CultureInfo.InvariantCulture) + " she saw: " + text + (string.IsNullOrEmpty(said) ? "" : " She said: " + Regex.Replace(said, @"\s+", " ").Trim()) + "\n";
            string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            lock (gate)
            {
                try
                {
                    string folder = FolderIn(root);
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, day + ".md");
                    if (!File.Exists(path)) File.WriteAllText(path, "# " + day + "\n\n");
                    File.AppendAllText(path, line);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private const int SourceMinutes = 5;
        private static string source;
        private static DateTime sourceAt = DateTime.MinValue;

        // His next line came from his phone or his Discord: it is kept with [phone] or [Discord] in front, in the same day's log.
        internal static void From(string where)
        {
            lock (gate) { source = where; sourceAt = DateTime.Now; }
        }

        internal static string Untagged(string text)
        {
            return Regex.Replace(text ?? "", @"^\[(phone|Discord)\]\s*", "");
        }

        internal static void Add(IList<ChatMessage> messages, DateTime now)
        {
            string root = Vault.Root;
            if (!Fork.MemoryOn || root == null || ActivityLog.FilePath == null || messages == null || messages.Count == 0) return;
            string stamp = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            StringBuilder lines = new StringBuilder();
            foreach (ChatMessage message in messages)
            {
                string text = Regex.Replace(message.Content ?? "", @"\s+", " ").Trim();
                if (text.Length == 0 || text.StartsWith("(", StringComparison.Ordinal)) continue;
                if (Brain.Personal.IsMatch(text)) text = Private;
                if (text.Length > MostLine) text = text.Substring(0, MostLine);
                string tag = "";
                if (message.Role == "user")
                    lock (gate)
                    {
                        if (source != null && (now - sourceAt).TotalMinutes < SourceMinutes) tag = "[" + source + "] ";
                        source = null;
                    }
                lines.Append(stamp).Append(message.Role == "user" ? " him: " : " her: ").Append(tag).Append(text).Append("\n");
            }
            if (lines.Length == 0) return;
            string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            lock (gate)
            {
                try
                {
                    string folder = FolderIn(root);
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, day + ".md");
                    if (!File.Exists(path)) File.WriteAllText(path, "# " + day + "\n\n");
                    File.AppendAllText(path, lines.ToString());
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static int Forget(List<string> topics)
        {
            string root = Vault.Root;
            if (root == null || topics == null || topics.Count == 0) return 0;
            List<string> flat = topics.ConvertAll(delegate(string topic) { return Forgetting.Squash(topic); });
            int removed = 0;
            lock (gate)
            {
                foreach (string path in Days(root))
                {
                    try
                    {
                        List<string> kept = new List<string>();
                        int here = 0;
                        foreach (string line in File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'))
                        {
                            string squashed = Forgetting.Squash(line);
                            if (!line.StartsWith("#", StringComparison.Ordinal) && removed + here < MostForgotten && flat.Exists(delegate(string topic) { return topic.Length > 0 && squashed.Contains(topic); })) here++;
                            else kept.Add(line);
                        }
                        if (here == 0) continue;
                        File.WriteAllText(path, string.Join("\n", kept.ToArray()));
                        removed += here;
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            if (removed > 0) ActivityLog.Write("memory", "forgot", removed + " chat archive lines");
            return removed;
        }

        internal static bool Clear(DateTime now)
        {
            string root = Vault.Root;
            if (root == null) return true;
            lock (gate)
            {
                List<string> days = Days(root);
                if (days.Count == 0) return true;
                try
                {
                    string backup = Path.Combine(root, "Capri", "backups", "chats-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(backup);
                    foreach (string path in days) File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), true);
                    foreach (string path in days) File.Delete(path);
                    ActivityLog.Write("memory", "forgot", "the chat archive (backed up)");
                    return true;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        private static List<string> Days(string root)
        {
            string folder = FolderIn(root);
            try { return Directory.Exists(folder) ? new List<string>(Directory.GetFiles(folder, "*.md")) : new List<string>(); }
            catch (IOException) { return new List<string>(); }
            catch (UnauthorizedAccessException) { return new List<string>(); }
        }
    }
}
