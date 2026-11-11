using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // A day-by-day log of everything said in her chats, kept on this PC for a week so bugs can be traced from what he really said.
    internal static class ChatLog
    {
        internal const int KeepDays = 7;
        private static readonly object gate = new object();
        private static ChatMessage last;

        internal static string FolderFor(string chatPath)
        {
            return chatPath == null ? null : Path.Combine(Path.GetDirectoryName(chatPath), "chatlog");
        }

        internal static void Seen(IList<ChatMessage> history)
        {
            lock (gate) last = history == null || history.Count == 0 ? null : history[history.Count - 1];
            ChatArchive.Keep(history);
        }

        internal static void Append(string folder, IList<ChatMessage> history, DateTime now)
        {
            if (folder == null || history == null) return;
            ChatArchive.Keep(history);
            lock (gate)
            {
                int from = 0;
                if (last != null)
                {
                    int found = -1;
                    for (int i = history.Count - 1; i >= 0; i--) if (ReferenceEquals(history[i], last)) { found = i; break; }
                    if (found >= 0) from = found + 1;
                }
                if (from >= history.Count) return;
                string stamp = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                List<string> lines = new List<string>();
                List<ChatMessage> fresh = new List<ChatMessage>();
                for (int i = from; i < history.Count; i++)
                {
                    ChatMessage message = history[i];
                    if (message == null || string.IsNullOrEmpty(message.Content)) continue;
                    lines.Add(stamp + " " + message.Role + ": " + Regex.Replace(message.Content, @"\s+", " ").Trim());
                    fresh.Add(message);
                }
                last = history[history.Count - 1];
                if (lines.Count == 0) return;
                ChatArchive.Add(fresh, now);
                try
                {
                    Directory.CreateDirectory(folder);
                    File.AppendAllText(Path.Combine(folder, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log"), string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static void Prune(string folder, DateTime now)
        {
            if (folder == null || !Directory.Exists(folder)) return;
            try
            {
                string keep = now.AddDays(-KeepDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                foreach (string file in Directory.GetFiles(folder, "*.log"))
                    if (string.CompareOrdinal(Path.GetFileNameWithoutExtension(file), keep) < 0) File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
