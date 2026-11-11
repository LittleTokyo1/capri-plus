using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal enum HuskKind { Doing, Asking, Answered, Helpers }

    internal sealed class HuskEvent
    {
        internal HuskKind Kind;
        internal string Session;
        internal string Project;
        internal string Text;
        internal int Count;
    }

    internal sealed class HuskWatch
    {
        internal const double ActiveSeconds = 120, HelperSeconds = 25;
        private const int MostRead = 1 << 20;
        private static readonly string[] AskTools = { "AskUserQuestion", "ExitPlanMode" };
        private readonly string root;
        private readonly Dictionary<string, long> offsets = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Turn> turns = new Dictionary<string, Turn>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> helpers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private sealed class Turn
        {
            internal string Project;
            internal string Asking;
        }

        internal HuskWatch(string folder) { root = folder; }

        internal static string DefaultRoot()
        {
            string folder = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            return Path.Combine(folder, "projects");
        }

        internal static string ProjectName(string folder)
        {
            string name = Path.GetFileName((folder ?? "").TrimEnd('\\', '/'));
            int cut = name.LastIndexOf('-');
            return cut >= 0 && cut < name.Length - 1 ? name.Substring(cut + 1) : name;
        }

        internal List<HuskEvent> Scan(DateTime now)
        {
            List<HuskEvent> found = new List<HuskEvent>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return found;
            foreach (string project in SafeDirectories(root))
            {
                string name = ProjectName(project);
                foreach (string file in SafeFiles(project))
                {
                    DateTime written = File.GetLastWriteTime(file);
                    string session = Path.GetFileNameWithoutExtension(file);
                    if ((now - written).TotalSeconds > ActiveSeconds)
                    {
                        if (!offsets.ContainsKey(file)) Mark(file);
                        int had;
                        if (helpers.TryGetValue(session, out had) && had > 0) Helpers(Path.Combine(project, session, "subagents"), session, name, now, found);
                        continue;
                    }
                    Read(file, session, name, now, found);
                    Helpers(Path.Combine(project, session, "subagents"), session, name, now, found);
                }
            }
            return found;
        }

        private void Mark(string file)
        {
            try { offsets[file] = new FileInfo(file).Length; }
            catch (IOException) { }
        }

        private void Read(string file, string session, string project, DateTime now, List<HuskEvent> found)
        {
            long length;
            try { length = new FileInfo(file).Length; }
            catch (IOException) { return; }
            long from;
            if (!offsets.TryGetValue(file, out from)) { offsets[file] = length; return; }
            if (length < from) { offsets[file] = length; return; }
            if (length == from) return;
            byte[] bytes;
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(from, length - MostRead);
                    stream.Seek(start, SeekOrigin.Begin);
                    bytes = new byte[length - start];
                    int got = 0;
                    while (got < bytes.Length)
                    {
                        int read = stream.Read(bytes, got, bytes.Length - got);
                        if (read <= 0) break;
                        got += read;
                    }
                    if (got < bytes.Length) Array.Resize(ref bytes, got);
                    from = start;
                }
            }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
            int last = Array.LastIndexOf(bytes, (byte)'\n');
            if (last < 0) return;
            offsets[file] = from + last + 1;
            foreach (string line in Encoding.UTF8.GetString(bytes, 0, last).Split('\n')) Line(line.Trim(), session, project, now, found);
        }

        internal void Line(string line, string session, string project, DateTime now, List<HuskEvent> found)
        {
            if (line.Length == 0 || line[0] != '{') return;
            Dictionary<string, object> entry;
            try { entry = json.DeserializeObject(line) as Dictionary<string, object>; }
            catch (ArgumentException) { return; }
            catch (InvalidOperationException) { return; }
            if (entry == null) return;
            string type = Text(entry, "type");
            if (type != "assistant" && type != "user") return;
            if (entry.ContainsKey("isSidechain") && entry["isSidechain"] is bool && (bool)entry["isSidechain"]) return;
            Dictionary<string, object> message = entry.ContainsKey("message") ? entry["message"] as Dictionary<string, object> : null;
            object[] blocks = message != null && message.ContainsKey("content") ? message["content"] as object[] : null;
            if (blocks == null) return;
            Turn turn;
            if (!turns.TryGetValue(session, out turn)) { turn = new Turn(); turns[session] = turn; }
            turn.Project = project;
            foreach (object item in blocks)
            {
                Dictionary<string, object> block = item as Dictionary<string, object>;
                if (block == null) continue;
                string kind = Text(block, "type");
                if (type == "user")
                {
                    if (kind == "tool_result") turn.Asking = null;
                    continue;
                }
                if (kind == "text")
                {
                    string said = Text(block, "text").Trim();
                    if (said.Length > 0 && Text(message, "stop_reason") == "end_turn") found.Add(new HuskEvent { Kind = HuskKind.Answered, Session = session, Project = project, Text = said });
                }
                else if (kind == "tool_use")
                {
                    string tool = Text(block, "name");
                    Dictionary<string, object> input = block.ContainsKey("input") ? block["input"] as Dictionary<string, object> : null;
                    if (Array.IndexOf(AskTools, tool) >= 0)
                    {
                        string id = Text(block, "id");
                        if (turn.Asking == id && id.Length > 0) continue;
                        turn.Asking = id;
                        found.Add(new HuskEvent { Kind = HuskKind.Asking, Session = session, Project = project, Text = tool == "ExitPlanMode" ? "wants your OK on its plan" : "is asking you something" });
                        continue;
                    }
                    found.Add(new HuskEvent { Kind = HuskKind.Doing, Session = session, Project = project, Text = Doing(tool, input) });
                }
            }
        }

        private void Helpers(string folder, string session, string project, DateTime now, List<HuskEvent> found)
        {
            int busy = 0;
            if (Directory.Exists(folder))
                foreach (string file in SafeFiles(folder))
                    if ((now - File.GetLastWriteTime(file)).TotalSeconds <= HelperSeconds) busy++;
            int was;
            helpers.TryGetValue(session, out was);
            if (busy == was) return;
            helpers[session] = busy;
            found.Add(new HuskEvent { Kind = HuskKind.Helpers, Session = session, Project = project, Count = busy });
        }

        internal int HelperTotal()
        {
            int total = 0;
            foreach (int count in helpers.Values) total += count;
            return total;
        }

        internal static string Doing(string tool, Dictionary<string, object> input)
        {
            string file = input == null ? "" : Text(input, "file_path");
            if (file.Length == 0 && input != null) file = Text(input, "notebook_path");
            string shortFile = file.Length == 0 ? "" : " " + Path.GetFileName(file);
            string note = input == null ? "" : Text(input, "description");
            if (note.Length > 60) note = note.Substring(0, 57) + "...";
            switch (tool)
            {
                case "Read": return "reading" + shortFile;
                case "Edit": case "MultiEdit": case "NotebookEdit": return "editing" + shortFile;
                case "Write": return "writing" + shortFile;
                case "Bash": case "PowerShell": return note.Length > 0 ? "running: " + note : "running a command";
                case "Grep": case "Glob": return "searching the code";
                case "Agent": case "Task": return note.Length > 0 ? "sending a helper: " + note : "sending a helper";
                case "WebSearch": case "WebFetch": return "looking things up online";
                case "TodoWrite": return "updating its to-do list";
                default: return "using " + (tool.StartsWith("mcp__", StringComparison.Ordinal) ? "a connected tool" : tool);
            }
        }

        internal static string FirstLine(string text, int most)
        {
            string flat = (text ?? "").Replace("\r", " ").Replace("\n", " ").Replace("**", "").Replace("`", "").Trim();
            while (flat.Contains("  ")) flat = flat.Replace("  ", " ");
            int stop = -1;
            foreach (string end in new[] { ". ", "! ", "? " })
            {
                int at = flat.IndexOf(end, StringComparison.Ordinal);
                if (at >= 0 && (stop < 0 || at < stop)) stop = at + 1;
            }
            if (stop > 0) flat = flat.Substring(0, stop);
            return flat.Length <= most ? flat : flat.Substring(0, most - 3).TrimEnd() + "...";
        }

        private static string Text(Dictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value is string ? (string)value : "";
        }

        private static string[] SafeDirectories(string folder)
        {
            try { return Directory.GetDirectories(folder); }
            catch (IOException) { return new string[0]; }
            catch (UnauthorizedAccessException) { return new string[0]; }
        }

        private static string[] SafeFiles(string folder)
        {
            try { return Directory.GetFiles(folder, "*.jsonl"); }
            catch (IOException) { return new string[0]; }
            catch (UnauthorizedAccessException) { return new string[0]; }
        }
    }
}
