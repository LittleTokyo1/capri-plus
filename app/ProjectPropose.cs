using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class ProposalItem
    {
        [DataMember] public int Id;
        [DataMember] public string Day = "";
        [DataMember] public string Name = "";
        [DataMember] public string Slug = "";
        [DataMember] public string Text = "";
        [DataMember] public string Status = "";
    }

    [DataContract]
    internal sealed class ProposalState
    {
        [DataMember] public List<string> Waiting = new List<string>();
        [DataMember] public long NextTry;
        [DataMember] public int Next;
        [DataMember] public string Told = "";
        [DataMember] public List<ProposalItem> Items = new List<ProposalItem>();
    }

    internal static class ProjectPropose
    {
        internal const string Tool = "project_propose";
        internal const string Mark = "PROPOSAL:";
        internal const string Open = "open", Made = "started", Dropped = "dropped";
        internal const int RetryMinutes = 60, MostWaiting = 3, LongestTopic = 300, NameChars = 40, MostListed = 10, ToldChars = 250;
        internal static readonly string[] Tools = { "WebSearch", "WebFetch", "AskUserQuestion" };
        internal const string NoVault = "I keep project proposals in my vault, and it isn't set up yet.";
        internal const string NotHis = "I think up a new project or make one only when you ask, choom.";
        internal const string Spec = "Think up new projects for them (propose only). Use action propose when they ask you to come up with a new project or a way to make money (text is their topic in their words, empty when they leave it to you): one of their read-only Claude Code sessions researches it on the web and you tell them the proposal. Use list when they ask about your proposals, start only when they themselves say yes to one (text names it or its number): that makes its folder with a plan and adds it to their projects as plan only, nothing more; no to drop one. Never say you proposed, started or made a project without this tool.";
        internal const string Schema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"propose, list, start (only on their own yes) or no\",\"enum\":[\"propose\",\"list\",\"start\",\"no\"]},\"text\":{\"type\":\"string\",\"description\":\"Their topic in their words, or words or the number naming the proposal\"}},\"required\":[\"action\"]}";
        internal const string Ending = "End with one message whose first line is " + Mark + " <a short name for the project>, then in a few short lines: what it is, who it is for, why it could work or make money, what already exists, the first three steps, the cost and the risks. Nothing gets made, bought, signed up for or published: the user decides.";
        internal const string Again = "You are a read-only research session for Capri, the user's desktop companion: only web search, web fetch and questions. Carry on thinking through the project proposal from the notes below. " + Ending;
        internal static string StatePath, FolderPath, HomeRoot;
        internal static Func<string, bool> AddProject = Tuning.AddProject;
        internal static Func<ProjectEntry, string, DateTime, string> Begin;
        private static readonly object gate = new object();

        internal static string Folder()
        {
            return FolderPath ?? Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri"), "proposals");
        }

        internal static ProjectEntry Entry() { return new ProjectEntry { Folder = Folder(), Propose = true }; }

        internal static bool Is(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return false;
            try { return Path.GetFullPath(folder).TrimEnd('\\', '/').Equals(Path.GetFullPath(Folder()).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
        }

        internal static string Task(string topic)
        {
            string plain = Plain(topic);
            return "You are a read-only research session for Capri, the user's desktop companion: you have only web search, web fetch and questions, there are no files here, and you never make, buy, sign up for or publish anything. Think through one new project the user could start" + (plain.Length == 0 ? " (your pick: something one person can build with Claude Code that could earn money or save them time)" : " about: '" + plain.Replace("\"", "'") + "'") + ". Check on the web what already exists and what it would cost. " + Ending;
        }

        internal static string Do(ToolArguments args)
        {
            return Run(args == null ? null : args.Action, args == null ? null : args.Text, args == null ? null : args.Asked, ForkSettings.Current.RunProjects, DateTime.Now);
        }

        internal static string Run(string action, string text, string asked, bool on, DateTime now)
        {
            if (string.IsNullOrEmpty(Vault.Root)) return NoVault;
            lock (gate)
            {
                ProposalState state = Load();
                if (action == "list") return Listed(state);
                if (!string.IsNullOrWhiteSpace(asked) && CallFriends.Friends(asked, now)) return "New projects come from you, not from a friend in the call. Nothing changed.";
                if (action == "propose") return Queue(state, text, asked, on);
                ProposalItem found = Matching(state, text);
                if (found == null) return "None of my proposals matches " + Voice.Quote(Plain(text)) + ". Nothing changed.";
                if (action == "no")
                {
                    if (found.Status == Made) return found.Name + " is already made; remove its folder from your projects in Capri+ if you don't want it.";
                    found.Status = Dropped;
                    Save(state);
                    WriteList(state);
                    ActivityLog.Write("proposals", "dropped", found.Name);
                    return "Dropped my proposal " + found.Name + ".";
                }
                if (action == "start")
                {
                    if (string.IsNullOrWhiteSpace(asked)) return NotHis;
                    if (found.Status == Made) return "I already made " + found.Name + ".";
                    if (ProjectRun.Gaming()) return "I'll make " + found.Name + " after your game; say start it again then.";
                    return Make(state, found, now);
                }
                return "Say propose, list, start or no. Nothing changed.";
            }
        }

        private static string Queue(ProposalState state, string text, string asked, bool on)
        {
            if (string.IsNullOrWhiteSpace(asked)) return NotHis;
            if (!on) return "Running my projects is off in Capri+, so no session can think one through. Nothing changed.";
            if (state.Waiting.Count >= MostWaiting) return "I already have " + state.Waiting.Count + " proposals waiting for a session. Nothing changed.";
            string topic = Plain(text);
            if (topic.Length > LongestTopic) topic = topic.Substring(0, LongestTopic).Trim();
            state.Waiting.Add(topic);
            Save(state);
            ActivityLog.Write("proposals", "asked", topic.Length == 0 ? "her pick" : topic);
            return "I'll have a session think through a new project" + (topic.Length == 0 ? "" : " about " + topic) + " (when no game runs and none of my other sessions is going) and tell you what it finds. Nothing gets made without your yes.";
        }

        internal static string Watch(bool on, int perDay, DateTime now)
        {
            if (!on || ProjectRun.Gaming()) return null;
            lock (gate)
            {
                ProposalState state = Load();
                if (state.Waiting.Count == 0 || now.Ticks < state.NextTry) return null;
                string topic = state.Waiting[0];
                string task = Task(topic);
                string refused;
                try
                {
                    Directory.CreateDirectory(Folder());
                    string name;
                    refused = Begin != null ? Begin(Entry(), task, now) : ProjectRun.Start(on, new string[0], perDay, Folder(), task, now, out name);
                }
                catch (IOException error) { refused = "its folder couldn't be made: " + error.Message; }
                catch (UnauthorizedAccessException error) { refused = "its folder couldn't be made: " + error.Message; }
                if (refused != null)
                {
                    state.NextTry = now.AddMinutes(RetryMinutes).Ticks;
                    bool capped = refused.StartsWith("I already started", StringComparison.Ordinal) && state.Told != topic;
                    if (capped) state.Told = topic;
                    Save(state);
                    ActivityLog.Write("proposals", "waiting to start", refused);
                    return capped ? "Your project idea" + (topic.Length == 0 ? "" : " about " + topic) + " waits: " + refused + ", so its session starts tomorrow." : null;
                }
                state.Waiting.RemoveAt(0);
                state.NextTry = 0;
                Save(state);
                ActivityLog.Write("proposals", "started", topic.Length == 0 ? "her pick" : topic);
                return null;
            }
        }

        internal static string Report(string said)
        {
            string text = said ?? "";
            int mark = text.LastIndexOf(Mark, StringComparison.Ordinal);
            return mark < 0 ? null : text.Substring(mark + Mark.Length).Trim();
        }

        internal static string Keep(string report, DateTime now)
        {
            string[] lines = (report ?? "").Replace("\r", "").Split('\n');
            string name = Regex.Replace(lines[0], @"[*#`_\[\]]+", "").Trim().Trim(':', '-', ' ');
            if (name.Length > 60) name = name.Substring(0, 60).Trim();
            if (name.Length == 0) name = "New project";
            string rest = Regex.Replace(string.Join(" ", lines, 1, lines.Length - 1), @"\s+", " ").Trim();
            lock (gate)
            {
                ProposalState state = Load();
                state.Next++;
                ProposalItem item = new ProposalItem { Id = state.Next, Day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Name = name, Slug = Slug(name), Text = report.Replace("\r", "").Trim(), Status = Open };
                state.Items.Add(item);
                Save(state);
                WriteList(state);
                WriteOne(item);
                ActivityLog.Write("proposals", "proposed", item.Id + " " + name);
                return "I have a new project proposal for you (" + item.Id + "): " + name + (rest.Length > 0 ? ": " + Pursuit.Shorter(rest, ToldChars) : ".") + " Say start it to make its folder with the plan, or no. It's all in my vault under Capri/proposals.";
            }
        }

        internal static string Slug(string name)
        {
            string slug = Regex.Replace((name ?? "").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (slug.Length > NameChars) slug = slug.Substring(0, NameChars).Trim('-');
            return slug.Length == 0 ? "new-project" : slug;
        }

        internal static string Claude(string name, string day)
        {
            return "# " + name + "\n\nA project Capri proposed and the user said yes to on " + day + ". The plan is in docs/PLAN.md: read it first, then build it step by step.\n\n## Rules\n- Conventional commits (feat:, fix:, docs:, ...).\n- No personal data, keys or secrets in tracked files.\n- Ask the user before anything that costs money, signs up for a service, publishes or deploys.\n";
        }

        private static string Make(ProposalState state, ProposalItem item, DateTime now)
        {
            string root = HomeRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub");
            string folder = Path.GetFullPath(Path.Combine(root, Slug(item.Slug)));
            if (!folder.StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return "That name doesn't make a safe folder. Nothing changed.";
            if (Directory.Exists(folder) || File.Exists(folder)) return "There's already a " + Path.GetFileName(folder) + " folder in " + root + ", so I didn't make " + item.Name + ".";
            try
            {
                Directory.CreateDirectory(Path.Combine(folder, "docs"));
                File.WriteAllText(Path.Combine(folder, "CLAUDE.md"), Claude(item.Name, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(Path.Combine(folder, "docs"), "PLAN.md"), "# " + item.Name + "\n\nProposed " + item.Day + " by Capri's read-only research session.\n\n" + item.Text + "\n", new UTF8Encoding(false));
            }
            catch (IOException error) { return "I couldn't make " + folder + ": " + error.Message; }
            catch (UnauthorizedAccessException error) { return "I couldn't make " + folder + ": " + error.Message; }
            bool git = ProjectRun.Git(folder, new[] { "init", "-q" }) != null;
            bool added = AddProject(folder);
            item.Status = Made;
            Save(state);
            WriteList(state);
            ActivityLog.Write("proposals", "made", item.Name + " in " + folder + (git ? "" : " (git init failed)") + (added ? "" : " (not added to projects)"));
            return "Made " + folder + " with its plan in docs/PLAN.md" + (git ? " and a fresh git repo" : " (git init didn't work, run it there yourself)") + (added ? ", and added it to your projects as plan only. Put | full after it in Capri+ when you want my sessions to build it." : ". Add it to your projects in Capri+ yourself.") + " No GitHub repo yet: that one's yours to make.";
        }

        private static ProposalItem Matching(ProposalState state, string text)
        {
            string plain = Plain(text).ToLowerInvariant();
            List<ProposalItem> live = state.Items.FindAll(delegate(ProposalItem one) { return one.Status != Dropped; });
            int number;
            if (int.TryParse(Regex.Replace(plain, @"[^0-9]", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && Regex.IsMatch(plain, @"^\D*\d+\D*$"))
                foreach (ProposalItem one in live) if (one.Id == number) return one;
            if (plain.Length == 0 || plain == "it" || plain == "that" || plain == "that one" || Regex.IsMatch(plain, @"\b(last|latest|newest|this one|new one)\b")) return live.Count == 0 ? null : live[live.Count - 1];
            Match place = Regex.Match(plain, @"\b(first|1st|second|2nd|third|3rd|fourth|4th|fifth|5th)\b");
            if (place.Success)
            {
                int at = Array.IndexOf(new[] { "first", "second", "third", "fourth", "fifth" }, place.Value);
                if (at < 0) at = Array.IndexOf(new[] { "1st", "2nd", "3rd", "4th", "5th" }, place.Value);
                List<ProposalItem> open = live.FindAll(delegate(ProposalItem one) { return one.Status == Open; });
                return at < open.Count ? open[at] : null;
            }
            ProposalItem best = null;
            foreach (ProposalItem one in live)
                if (plain.Contains(one.Name.ToLowerInvariant()) || one.Name.ToLowerInvariant().Contains(plain) || plain.Contains(one.Slug.Replace('-', ' '))) best = one;
            return best;
        }

        private static string Listed(ProposalState state)
        {
            List<string> parts = new List<string>();
            foreach (ProposalItem one in state.Items)
                if (one.Status != Dropped && parts.Count < MostListed) parts.Add(one.Id + ". " + one.Name + (one.Status == Made ? " (made)" : ""));
            string waiting = state.Waiting.Count == 0 ? "" : " " + state.Waiting.Count + " more waiting for a session.";
            return parts.Count == 0 ? "I have no project proposals yet." + waiting : "My project proposals: " + string.Join("; ", parts.ToArray()) + "." + waiting;
        }

        private static void WriteList(ProposalState state)
        {
            string root = Vault.Root;
            if (string.IsNullOrEmpty(root)) return;
            StringBuilder text = new StringBuilder("# Project proposals\n> New projects her read-only sessions thought through. Nothing is made without his yes; a made one gets its own folder and plan.\n\n");
            foreach (ProposalItem one in state.Items)
                if (one.Status != Dropped) text.Append("- [").Append(one.Status == Made ? "x" : " ").Append("] ").Append(one.Id).Append(". [[").Append(FileName(one)).Append('|').Append(one.Name).Append("]] (").Append(one.Day).Append(one.Status == Made ? ", made" : "").Append(")\n");
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Capri"));
                File.WriteAllText(Path.Combine(Path.Combine(root, "Capri"), "proposals.md"), text.ToString(), new UTF8Encoding(false));
            }
            catch (IOException error) { ActivityLog.Write("proposals", "list", "not written: " + error.Message); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("proposals", "list", "not written: " + error.Message); }
        }

        private static void WriteOne(ProposalItem item)
        {
            string root = Vault.Root;
            if (string.IsNullOrEmpty(root)) return;
            string folder = Path.Combine(Path.Combine(root, "Capri"), "proposals");
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, FileName(item) + ".md"), "# " + item.Name + "\n\nProposed " + item.Day + " by a read-only session. Say start it to make its folder, or no.\n\n" + item.Text + "\n", new UTF8Encoding(false));
            }
            catch (IOException error) { ActivityLog.Write("proposals", item.Name, "not kept: " + error.Message); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("proposals", item.Name, "not kept: " + error.Message); }
        }

        internal static string FileName(ProposalItem item) { return item.Day + " " + item.Slug; }

        private static string Plain(string text) { return Regex.Replace((text ?? "").Replace("\r", " ").Replace("\n", " "), @"\s+", " ").Trim(); }

        private static string FilePath()
        {
            if (StatePath != null) return StatePath;
            return ForkSettings.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ForkSettings.FilePath), "project-proposals.json");
        }

        internal static ProposalState Load()
        {
            string path = FilePath();
            if (path == null || !File.Exists(path)) return new ProposalState();
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    ProposalState state = (ProposalState)new DataContractJsonSerializer(typeof(ProposalState)).ReadObject(stream) ?? new ProposalState();
                    if (state.Waiting == null) state.Waiting = new List<string>();
                    if (state.Items == null) state.Items = new List<ProposalItem>();
                    return state;
                }
            }
            catch (Exception error)
            {
                ActivityLog.Write("proposals", "state file", "unreadable, starting fresh: " + error.Message);
                return new ProposalState();
            }
        }

        private static void Save(ProposalState state)
        {
            string path = FilePath();
            if (path == null) return;
            try
            {
                using (FileStream stream = File.Create(path)) new DataContractJsonSerializer(typeof(ProposalState)).WriteObject(stream, state);
            }
            catch (IOException error) { ActivityLog.Write("proposals", "state file", "not saved: " + error.Message); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("proposals", "state file", "not saved: " + error.Message); }
        }
    }
}
