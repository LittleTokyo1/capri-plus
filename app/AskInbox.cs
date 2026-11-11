using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    // "Capri.exe --ask <text>" hands a line to the running Capri as if he typed it. Local only: a file in her own data folder.
    internal static class AskInbox
    {
        internal const string Flag = "--ask";
        private const int MostLines = 5;

        internal static string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "ask.txt");

        internal static int Drop(string text)
        {
            string line = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (line.Length == 0) return 1;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.AppendAllText(FilePath, line + "\n", new UTF8Encoding(false));
                return 0;
            }
            catch (IOException) { return 1; }
            catch (UnauthorizedAccessException) { return 1; }
        }

        private const int ReplySeconds = 120;
        private static DateTime takenAt = DateTime.MinValue;

        internal static string Replies { get { return Path.Combine(Path.GetDirectoryName(FilePath), "ask-replies.txt"); } }

        internal static void Shown(string tag, string line)
        {
            if ((DateTime.Now - takenAt).TotalSeconds > ReplySeconds) return;
            try { File.AppendAllText(Replies, DateTime.Now.ToString("HH:mm:ss") + " " + tag + " " + (line ?? "").Replace("\n", " / ") + "\n", new UTF8Encoding(false)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static void Reset() { takenAt = DateTime.MinValue; }

        internal static string Take()
        {
            string taken = TakeLine();
            if (taken != null) takenAt = DateTime.Now;
            return taken;
        }

        private static string TakeLine()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                List<string> lines = new List<string>();
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8)) if (raw.Trim().Length > 0 && lines.Count < MostLines) lines.Add(raw.Trim());
                if (lines.Count <= 1) File.Delete(FilePath);
                else File.WriteAllLines(FilePath, lines.GetRange(1, lines.Count - 1).ToArray(), new UTF8Encoding(false));
                if (lines.Count == 0) return null;
                return lines[0].Length > Fork.MessageLimit ? lines[0].Substring(0, Fork.MessageLimit) : lines[0];
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
