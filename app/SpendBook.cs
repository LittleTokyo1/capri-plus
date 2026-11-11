using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class SpendLine
    {
        [DataMember] public string Name;
        [DataMember] public double Cost;
        [DataMember] public int Calls;
    }

    [DataContract]
    internal sealed class SpendMonth
    {
        [DataMember] public string Month;
        [DataMember] public double Total;
        [DataMember] public int Searches;
        [DataMember] public long VoiceChars;
        [DataMember] public int PlanRuns;
        [DataMember] public List<SpendLine> Models;
        [DataMember] public List<SpendLine> Uses;
    }

    // What her Claude calls cost, by UTC month (like the Anthropic console), split by model and by what they were for; kept in spend-history.json.
    internal static class SpendBook
    {
        internal const string Earlier = "earlier this month (before the split)";
        internal const string Chats = "chats";
        internal const string Jobs = "PC jobs (her hands)";
        internal const string Memory = "memory and notes";
        internal const string Game = "League";
        internal const string Looks = "web look-ups";
        internal const string Heart = "heartbeat, check-ins and offers";
        internal const string Checks = "checks on her own replies";
        internal const string Discord = "Discord";
        internal const string ChatWindows = "chat windows";
        internal const string Other = "other";
        private const int KeptMonths = 24;

        internal static string FilePath;
        private static readonly object gate = new object();
        private static List<SpendMonth> months;

        private static readonly Dictionary<string, string> ByClass = new Dictionary<string, string>
        {
            { "Hands", Jobs }, { "Agent", Jobs }, { "LongJob", Jobs },
            { "ChatSummary", Memory }, { "CoreNote", Memory }, { "ForgetCheck", Memory }, { "NightReview", Memory }, { "Nightly", Memory }, { "PeopleNotes", Memory },
            { "League", Game }, { "Heartbeat", Heart }, { "CheckIns", Heart }, { "Autonomy", Heart },
            { "SayDo", Checks }, { "DiscordInbox", Discord }
        };

        internal static readonly Dictionary<string, string> ByMethod = new Dictionary<string, string>
        {
            { "SummarizeChat", Memory }, { "CounterAnswer", Game }, { "React", Game }, { "Lookup", Looks }, { "Compose", ChatWindows }
        };

        private static readonly string[] Inside = { "SpendBook", "Spend", "Quick", "Brain" };

        internal static string MonthOf(DateTime utc)
        {
            return utc.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        internal static string MonthNow { get { return MonthOf(DateTime.UtcNow); } }

        // "claude-haiku-5-5" is Haiku 5.5, "claude-sonnet-4-5-20250929" is Sonnet 4.5.
        internal static string Shown(string model)
        {
            if (string.IsNullOrEmpty(model)) return "unknown model";
            Match named = Regex.Match(model.ToLowerInvariant(), @"^claude-([a-z]+)-(\d{1,2})(?:-(\d{1,2}))?(?:-|$)");
            if (!named.Success) return model;
            string family = named.Groups[1].Value;
            return char.ToUpperInvariant(family[0]) + family.Substring(1) + " " + named.Groups[2].Value + (named.Groups[3].Success ? "." + named.Groups[3].Value : "");
        }

        // What a call was for, from who made it: the first caller outside the brain code.
        internal static string Use()
        {
            StackFrame[] frames = new StackTrace(1, false).GetFrames();
            if (frames == null) return Other;
            bool driving = false;
            foreach (StackFrame frame in frames)
            {
                MethodBase method = frame.GetMethod();
                if (method == null) continue;
                Type type = method.DeclaringType;
                while (type != null && type.Name.StartsWith("<", StringComparison.Ordinal) && type.DeclaringType != null) type = type.DeclaringType;
                if (type == null) continue;
                if (type.Name == "Brain" && method.Name == "Drive") driving = true;
                if (Array.IndexOf(Inside, type.Name) >= 0) continue;
                return driving ? Jobs : UseFor(type.Name, Outer(method.Name));
            }
            return driving ? Jobs : Other;
        }

        private static string Outer(string name)
        {
            Match lambda = Regex.Match(name ?? "", @"^<(\w+)>");
            return lambda.Success ? lambda.Groups[1].Value : name;
        }

        internal static string UseFor(string type, string method)
        {
            string use;
            if (type == "Companion") return ByMethod.TryGetValue(method ?? "", out use) ? use : Chats;
            return ByClass.TryGetValue(type ?? "", out use) ? use : Other;
        }

        internal static void Add(string model, string use, double cost, int searches, DateTime utc, double earlier, int earlierSearches)
        {
            if (cost <= 0 && searches <= 0) return;
            lock (gate)
            {
                SpendMonth month = Find(MonthOf(utc), earlier, earlierSearches);
                month.Total += cost;
                month.Searches += searches;
                Count(month.Models, Shown(model), cost);
                Count(month.Uses, string.IsNullOrEmpty(use) ? Other : use, cost);
                Save();
            }
        }

        // The month that just ended keeps its ElevenLabs characters and big jobs, which only fork.json counts.
        internal static void Close(string month, double spent, int searches, long voiceChars, int planRuns)
        {
            if (string.IsNullOrEmpty(month)) return;
            lock (gate)
            {
                SpendMonth ended = Find(month, spent, searches);
                ended.VoiceChars = Math.Max(ended.VoiceChars, voiceChars);
                ended.PlanRuns = Math.Max(ended.PlanRuns, planRuns);
                Save();
            }
        }

        // Newest first, copies.
        internal static List<SpendMonth> Months()
        {
            lock (gate)
            {
                Load();
                List<SpendMonth> copy = new List<SpendMonth>();
                foreach (SpendMonth month in months) copy.Add(Copy(month));
                copy.Sort(delegate(SpendMonth a, SpendMonth b) { return string.CompareOrdinal(b.Month, a.Month); });
                return copy;
            }
        }

        internal static SpendMonth Of(string month)
        {
            foreach (SpendMonth found in Months()) if (found.Month == month) return found;
            return null;
        }

        internal static void Forget()
        {
            lock (gate) months = null;
        }

        private static SpendMonth Find(string name, double earlier, int earlierSearches)
        {
            Load();
            foreach (SpendMonth month in months) if (month.Month == name) return month;
            SpendMonth made = new SpendMonth { Month = name, Models = new List<SpendLine>(), Uses = new List<SpendLine>() };
            if (earlier > 0)
            {
                made.Total = earlier;
                made.Models.Add(new SpendLine { Name = Earlier, Cost = earlier });
                made.Uses.Add(new SpendLine { Name = Earlier, Cost = earlier });
            }
            made.Searches = Math.Max(0, earlierSearches);
            months.Add(made);
            months.Sort(delegate(SpendMonth a, SpendMonth b) { return string.CompareOrdinal(a.Month, b.Month); });
            while (months.Count > KeptMonths) months.RemoveAt(0);
            return made;
        }

        private static void Count(List<SpendLine> lines, string name, double cost)
        {
            foreach (SpendLine line in lines)
            {
                if (line.Name != name) continue;
                line.Cost += cost;
                line.Calls++;
                return;
            }
            lines.Add(new SpendLine { Name = name, Cost = cost, Calls = 1 });
        }

        private static SpendMonth Copy(SpendMonth month)
        {
            SpendMonth copy = new SpendMonth { Month = month.Month, Total = month.Total, Searches = month.Searches, VoiceChars = month.VoiceChars, PlanRuns = month.PlanRuns, Models = new List<SpendLine>(), Uses = new List<SpendLine>() };
            foreach (SpendLine line in month.Models) copy.Models.Add(new SpendLine { Name = line.Name, Cost = line.Cost, Calls = line.Calls });
            foreach (SpendLine line in month.Uses) copy.Uses.Add(new SpendLine { Name = line.Name, Cost = line.Cost, Calls = line.Calls });
            copy.Models.Sort(delegate(SpendLine a, SpendLine b) { return b.Cost.CompareTo(a.Cost); });
            copy.Uses.Sort(delegate(SpendLine a, SpendLine b) { return b.Cost.CompareTo(a.Cost); });
            return copy;
        }

        private static void Load()
        {
            if (months != null) return;
            months = new List<SpendMonth>();
            string path = FilePath;
            if (path == null || !File.Exists(path)) return;
            try
            {
                using (FileStream file = File.OpenRead(path))
                {
                    SpendMonth[] read = (SpendMonth[])new DataContractJsonSerializer(typeof(SpendMonth[])).ReadObject(file);
                    if (read == null) return;
                    foreach (SpendMonth month in read)
                    {
                        if (month == null || string.IsNullOrEmpty(month.Month)) continue;
                        if (month.Models == null) month.Models = new List<SpendLine>();
                        if (month.Uses == null) month.Uses = new List<SpendLine>();
                        months.Add(month);
                    }
                }
            }
            catch (Exception error)
            {
                ActivityLog.Write("spending", "history unreadable", error.Message);
            }
        }

        private static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                using (FileStream file = File.Create(temp)) new DataContractJsonSerializer(typeof(SpendMonth[])).WriteObject(file, months.ToArray());
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            catch (Exception error)
            {
                ActivityLog.Write("spending", "history not saved", error.Message);
            }
        }

        internal static string Money(double value)
        {
            return "$" + value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static readonly Regex AskedWords = new Regex(@"^(?:(?:hey|ok|okay|so|capri|yo)\s+)*(?:how much|what(?:'s|s| is| are| did)?|tell me|show me|give me)\b.{0,40}\b(?:spent|spend|spending|used|usage|cost me|costs? so far|bill)\b.{0,40}$", RegexOptions.IgnoreCase);
        private static readonly Regex OnSomethingElse = new Regex(@"\bon (?!claude\b|the api\b|api\b|anthropic\b|elevenlabs\b|eleven labs\b|you\b|tokens?\b)[a-z]", RegexOptions.IgnoreCase);
        private static readonly Regex AboutUs = new Regex(@"\b(?:this month|last month|so far|claude|api|anthropic|elevenlabs|eleven labs|tokens?|budget|cap|in total|altogether|on you|for you)\b", RegexOptions.IgnoreCase);

        // "How much have I spent this month?" and the like, about her own costs; never a shop's price.
        internal static bool Asked(string text)
        {
            string said = (text ?? "").Trim().TrimEnd('?', '!', '.', ' ');
            return said.Length > 0 && said.Length <= 120 && AskedWords.IsMatch(said) && AboutUs.IsMatch(said) && !OnSomethingElse.IsMatch(said);
        }

        // Her answer from the counters, no model call.
        internal static string Spoken(ForkSettings settings)
        {
            settings.Tidy();
            StringBuilder said = new StringBuilder();
            said.Append("This month so far (UTC, like the Anthropic console): Claude ").Append(Money(settings.Spent)).Append(" of your ").Append(Money(settings.Cap)).Append(" cap");
            SpendMonth now = Of(settings.Month);
            if (now != null && now.Models.Count > 0)
            {
                List<string> parts = new List<string>();
                foreach (SpendLine line in now.Models) if (line.Name != Earlier && parts.Count < 3) parts.Add(line.Name + " " + Money(line.Cost));
                if (parts.Count > 0) said.Append(" (").Append(string.Join(", ", parts.ToArray())).Append(")");
            }
            said.Append(", ").Append(settings.Searches).Append(settings.Searches == 1 ? " web search" : " web searches");
            said.Append(", and ElevenLabs ").Append(settings.VoiceChars.ToString("N0", CultureInfo.InvariantCulture)).Append(" characters.");
            SpendMonth before = null;
            foreach (SpendMonth month in Months()) if (string.CompareOrdinal(month.Month, settings.Month) < 0) { before = month; break; }
            if (before != null) said.Append(" Last month (").Append(before.Month).Append("): ").Append(Money(before.Total)).Append(".");
            return said.ToString();
        }
    }
}
