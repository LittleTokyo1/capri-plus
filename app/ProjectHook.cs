using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class ProjectTurn
    {
        [DataMember] public string Session = "";
        [DataMember] public string Kind = "";
        [DataMember] public string Text = "";
        [DataMember] public string[] Questions = new string[0];
        [DataMember] public string[] Options = new string[0];
        [DataMember] public long At;
        [DataMember] public string Transcript = "";
    }

    [DataContract]
    internal sealed class ProjectReply
    {
        [DataMember] public string Text = "";
        [DataMember] public string[] Answers = new string[0];
        [DataMember] public long For;
    }

    internal static class ProjectHook
    {
        internal const string Flag = "--project-hook";
        internal const string Stop = "stop", Ask = "ask", Plan = "plan", PlanTool = "ExitPlanMode";
        internal const int WaitSeconds = 300, HookTimeout = 600, TextChars = 4000, PollMs = 500;
        private static readonly Regex SessionId = new Regex("^[A-Za-z0-9-]{8,64}$");
        internal static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "project-hooks");
        internal static Action<int> Sleep = delegate(int ms) { Thread.Sleep(ms); };
        internal static Func<bool> HerOpen = MutexOpen;

        internal static int Main(string kind)
        {
            try
            {
                string raw;
                using (StreamReader input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false))) raw = input.ReadToEnd();
                string output = Answer(kind, raw, DateTime.Now);
                if (output == null) return 0;
                using (StreamWriter written = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))) written.Write(output);
            }
            catch (Exception error) { Note("hook failed: " + error.GetType().Name + ": " + error.Message); }
            return 0;
        }

        internal static string Answer(string kind, string raw, DateTime now)
        {
            Dictionary<string, object> input;
            try { input = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(raw ?? "") as Dictionary<string, object>; }
            catch (ArgumentException) { return null; }
            if (input == null) return null;
            string session = Field(input, "session_id");
            if (session == null || !SessionId.IsMatch(session)) return null;
            if (kind == Stop) return StopAnswer(input, session, now);
            if (kind == Ask) return AskAnswer(input, session, now);
            return null;
        }

        private static string StopAnswer(Dictionary<string, object> input, string session, DateTime now)
        {
            string said = Field(input, "last_assistant_message") ?? "";
            if (said.Length > TextChars) said = said.Substring(0, TextChars);
            WriteTurn(new ProjectTurn { Session = session, Kind = Stop, Text = said, At = now.Ticks, Transcript = Field(input, "transcript_path") ?? "" });
            ProjectReply reply = TakeReply(session, now.Ticks);
            if (reply == null || string.IsNullOrWhiteSpace(reply.Text)) return null;
            Dictionary<string, object> output = new Dictionary<string, object>();
            output["decision"] = "block";
            output["reason"] = reply.Text.Trim();
            return new JavaScriptSerializer().Serialize(output);
        }

        private static string AskAnswer(Dictionary<string, object> input, string session, DateTime now)
        {
            object tool;
            Dictionary<string, object> toolInput = input.TryGetValue("tool_input", out tool) ? tool as Dictionary<string, object> : null;
            if (Field(input, "tool_name") == PlanTool)
            {
                string plan = toolInput == null ? "" : (Field(toolInput, "plan") ?? "");
                if (plan.Length > TextChars) plan = plan.Substring(0, TextChars);
                WriteTurn(new ProjectTurn { Session = session, Kind = Plan, Text = plan, At = now.Ticks, Transcript = Field(input, "transcript_path") ?? "" });
                return null;
            }
            object listed;
            IList questions = toolInput != null && toolInput.TryGetValue("questions", out listed) ? listed as IList : null;
            if (questions == null || questions.Count == 0) return null;
            List<string> texts = new List<string>(), options = new List<string>();
            foreach (object one in questions)
            {
                Dictionary<string, object> question = one as Dictionary<string, object>;
                texts.Add(question == null ? "" : (Field(question, "question") ?? ""));
                options.Add(question == null ? "" : Labels(question));
            }
            WriteTurn(new ProjectTurn { Session = session, Kind = Ask, Questions = texts.ToArray(), Options = options.ToArray(), At = now.Ticks, Transcript = Field(input, "transcript_path") ?? "" });
            ProjectReply reply = TakeReply(session, now.Ticks);
            if (reply == null || reply.Answers == null || reply.Answers.Length != texts.Count) return null;
            Dictionary<string, object> answers = new Dictionary<string, object>();
            for (int i = 0; i < texts.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(reply.Answers[i])) return null;
                answers[texts[i]] = reply.Answers[i].Trim();
            }
            Dictionary<string, object> updated = new Dictionary<string, object>();
            updated["questions"] = questions;
            updated["answers"] = answers;
            Dictionary<string, object> specific = new Dictionary<string, object>();
            specific["hookEventName"] = "PreToolUse";
            specific["permissionDecision"] = "allow";
            specific["updatedInput"] = updated;
            Dictionary<string, object> output = new Dictionary<string, object>();
            output["hookSpecificOutput"] = specific;
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(output);
        }

        internal static string WriteSettings(string exe)
        {
            string command = "\"" + exe + "\" " + Flag + " ";
            string json = "{\"hooks\":{\"Stop\":[{\"hooks\":[" + Hook(command + Stop) + "]}],\"PreToolUse\":[{\"matcher\":\"AskUserQuestion|" + PlanTool + "\",\"hooks\":[" + Hook(command + Ask) + "]}]}}";
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "hooks-settings.json");
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }

        internal static ProjectTurn Turn(string session)
        {
            string path = TurnPath(session);
            if (path == null || !File.Exists(path)) return null;
            try { using (FileStream stream = File.OpenRead(path)) return (ProjectTurn)new DataContractJsonSerializer(typeof(ProjectTurn)).ReadObject(stream); }
            catch (Exception) { return null; }
        }

        internal static void Reply(string session, long turn, string text, string[] answers)
        {
            string path = Place(session, ".reply.json");
            if (path == null) return;
            Directory.CreateDirectory(Folder);
            Save(path + ".tmp", new ProjectReply { Text = text ?? "", Answers = answers ?? new string[0], For = turn }, typeof(ProjectReply));
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
        }

        internal static void Expect(string session, bool waiting)
        {
            string path = Place(session, ".wait");
            if (path == null) return;
            if (waiting) { Directory.CreateDirectory(Folder); File.WriteAllText(path, ""); }
            else if (File.Exists(path)) File.Delete(path);
        }

        internal static void Forget(string session)
        {
            foreach (string end in new[] { ".turn.json", ".reply.json", ".wait" })
            {
                string path = Place(session, end);
                try { if (path != null && File.Exists(path)) File.Delete(path); }
                catch (IOException) { }
            }
        }

        private static ProjectReply TakeReply(string session, long turn)
        {
            string path = Place(session, ".reply.json"), wait = Place(session, ".wait");
            DateTime until = DateTime.UtcNow.AddSeconds(File.Exists(wait) && HerOpen() ? WaitSeconds : 0);
            while (true)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        ProjectReply reply;
                        using (FileStream stream = File.OpenRead(path)) reply = (ProjectReply)new DataContractJsonSerializer(typeof(ProjectReply)).ReadObject(stream);
                        File.Delete(path);
                        if (reply != null && reply.For == turn) return reply;
                        Note("a reply for another turn was dropped");
                        continue;
                    }
                    catch (Exception error) { Note("reply unreadable: " + error.Message); return null; }
                }
                if (DateTime.UtcNow >= until || !File.Exists(wait)) return null;
                Sleep(PollMs);
            }
        }

        private static void WriteTurn(ProjectTurn turn)
        {
            string path = TurnPath(turn.Session);
            Directory.CreateDirectory(Folder);
            Save(path + ".tmp", turn, typeof(ProjectTurn));
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
        }

        private static string TurnPath(string session) { return Place(session, ".turn.json"); }

        internal static bool Current(string session, long turn)
        {
            ProjectTurn now = Turn(session);
            return now != null && now.At == turn;
        }

        private static bool MutexOpen()
        {
            Mutex open;
            if (!Mutex.TryOpenExisting("Local\\Capri.Desktop." + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, out open)) return false;
            open.Dispose();
            return true;
        }

        private static string Place(string session, string end)
        {
            return session == null || !SessionId.IsMatch(session) ? null : Path.Combine(Folder, session + end);
        }

        private static void Save(string path, object value, Type type)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                new DataContractJsonSerializer(type).WriteObject(buffer, value);
                File.WriteAllBytes(path, buffer.ToArray());
            }
        }

        private static string Hook(string command)
        {
            return "{\"type\":\"command\",\"command\":" + new JavaScriptSerializer().Serialize(command) + ",\"timeout\":" + HookTimeout + "}";
        }

        private static string Labels(Dictionary<string, object> question)
        {
            object listed;
            IList options = question.TryGetValue("options", out listed) ? listed as IList : null;
            if (options == null) return "";
            List<string> labels = new List<string>();
            foreach (object one in options)
            {
                Dictionary<string, object> option = one as Dictionary<string, object>;
                string label = option == null ? null : Field(option, "label");
                if (!string.IsNullOrEmpty(label)) labels.Add(label);
            }
            return string.Join(" / ", labels.ToArray());
        }

        private static string Field(Dictionary<string, object> from, string name)
        {
            object value;
            return from.TryGetValue(name, out value) ? value as string : null;
        }

        private static void Note(string text)
        {
            try { Directory.CreateDirectory(Folder); File.AppendAllText(Path.Combine(Folder, "hook-errors.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + "\n"); }
            catch (Exception) { }
        }
    }
}
