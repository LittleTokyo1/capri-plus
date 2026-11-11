using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // A plain list of everything her hands did, so he can check on her. Lines look like:
    // 2026-10-28 18:04:12  open_app  Spotify  ->  Opened Spotify.
    internal static class ActivityLog
    {
        internal static string FilePath;
        private const long Biggest = 1024 * 1024;
        private static readonly object writing = new object();

        internal static void Write(string what, string detail, string result)
        {
            detail = ClaudeToken.Hide(detail);
            result = ClaudeToken.Hide(result);
            Did.Seen(what, detail, result, DateTime.Now);
            if (!ForkSettings.Current.LogActions) return;
            string path = FilePath;
            if (path == null) return;
            StringBuilder line = new StringBuilder();
            line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
            line.Append("  ").Append(Short(what, 40));
            if (!string.IsNullOrEmpty(detail)) line.Append("  ").Append(Short(Computer.Mask(detail), 160));
            if (!string.IsNullOrEmpty(result)) line.Append("  ->  ").Append(Short(Computer.Mask(result), 200));
            lock (writing)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    FileInfo file = new FileInfo(path);
                    if (file.Exists && file.Length > Biggest)
                    {
                        string old = path + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    File.AppendAllText(path, line.ToString() + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception) { }
            }
        }

        internal static string Describe(ToolArguments args)
        {
            if (args == null) return "";
            StringBuilder said = new StringBuilder();
            Add(said, args.Name);
            Add(said, args.Site);
            Add(said, args.Search);
            Add(said, args.Song);
            Add(said, args.App);
            Add(said, args.Action);
            Add(said, args.Task);
            Add(said, args.Target == null ? null : "#" + args.Target);
            Add(said, args.Text == null ? null : args.Text.Length + " characters of text");
            Add(said, args.Focus);
            return Computer.Mask(said.ToString());
        }

        private static void Add(StringBuilder said, string part)
        {
            if (string.IsNullOrWhiteSpace(part)) return;
            if (said.Length > 0) said.Append(" | ");
            said.Append(part.Trim());
        }

        private static string Short(string text, int most)
        {
            text = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return text.Length <= most ? text : text.Substring(0, most - 3) + "...";
        }
    }
}
