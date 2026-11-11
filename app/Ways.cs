using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class Way
    {
        [DataMember] public string Words;
        [DataMember] public string[] Steps;
        [DataMember] public string When;
        [DataMember] public int Used;
    }

    internal static class Ways
    {
        internal const int MostWays = 60;
        internal const int MostSteps = 25;
        internal const double Alike = 0.6;
        internal const int MinShared = 3;
        internal const string Marker = "\nLast time a job like this worked";

        internal static Func<string> FilePath = delegate
        {
            string settings = ForkSettings.FilePath;
            return settings == null ? null : Path.Combine(Path.GetDirectoryName(settings), "ways.json");
        };

        private static readonly object gate = new object();
        private static readonly HashSet<string> Small = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "the", "and", "for", "with", "please", "pls", "can", "you", "could", "would", "then", "that", "this", "into", "from", "your", "mine", "just", "now", "again", "capri", "hey", "okay", "yeah" };
        private static readonly Regex Typing = new Regex(@"^(type|key|hold_key|hold_and_press):", RegexOptions.IgnoreCase);
        private static readonly Regex Plain = new Regex(@"^(left_click|double_click|right_click|middle_click|triple_click|scroll|click|open_app|open_website|press|wait|wait_for|switch_monitor):", RegexOptions.IgnoreCase);

        internal static HashSet<string> Keys(string words)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match word in Regex.Matches((words ?? "").ToLowerInvariant(), @"[\p{L}\p{N}]+"))
                if (word.Value.Length >= 3 && !Small.Contains(word.Value)) keys.Add(word.Value);
            return keys;
        }

        internal static double Likeness(string one, string two)
        {
            HashSet<string> a = Keys(one), b = Keys(two);
            if (a.Count == 0 || b.Count == 0) return 0;
            int both = 0;
            foreach (string key in a) if (b.Contains(key)) both++;
            if (both < MinShared) return 0;
            return (double)both / (a.Count + b.Count - both);
        }

        internal static string[] Kept(IList<string> steps)
        {
            List<string> kept = new List<string>();
            if (steps == null) return kept.ToArray();
            foreach (string step in steps)
            {
                if (string.IsNullOrWhiteSpace(step) || step.IndexOf(Computer.Dots, StringComparison.Ordinal) >= 0) continue;
                string line = step.Trim();
                int split = line.IndexOf(": ", StringComparison.Ordinal);
                if (split < 0 || Computer.Failed(line.Substring(split + 2))) continue;
                if (Typing.IsMatch(line)) line = line.Substring(0, split) + ": " + (line.StartsWith("type", StringComparison.OrdinalIgnoreCase) ? "Typed the text the job needs." : "Pressed keys.");
                else if (!Plain.IsMatch(line)) continue;
                line = Regex.Replace(line, @"\s*\[\d+\]\s*", " ").Trim();
                if (kept.Count > 0 && kept[kept.Count - 1] == line) continue;
                kept.Add(line);
                if (kept.Count >= MostSteps) break;
            }
            return kept.ToArray();
        }

        internal static List<Way> All()
        {
            string path = FilePath();
            if (path == null || !File.Exists(path)) return new List<Way>();
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    Way[] read = (Way[])new DataContractJsonSerializer(typeof(Way[])).ReadObject(stream);
                    return read == null ? new List<Way>() : new List<Way>(read);
                }
            }
            catch (Exception error)
            {
                ActivityLog.Write("hands", "ways not read", error.GetType().Name);
                return new List<Way>();
            }
        }

        private static void Save(List<Way> ways)
        {
            string path = FilePath();
            if (path == null) return;
            string temporary = path + ".tmp";
            try
            {
                using (FileStream stream = File.Create(temporary)) new DataContractJsonSerializer(typeof(Way[])).WriteObject(stream, ways.ToArray());
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) { ActivityLog.Write("hands", "ways not saved", error.GetType().Name); }
        }

        internal static Way Find(string words)
        {
            lock (gate) return Best(All(), words);
        }

        private static Way Best(List<Way> ways, string words)
        {
            Way best = null;
            double score = Alike;
            foreach (Way way in ways)
            {
                double alike = Likeness(way.Words, words);
                if (alike >= score) { best = way; score = alike; }
            }
            return best;
        }

        internal static string Offer(Way way)
        {
            if (way == null || way.Steps == null || way.Steps.Length == 0) return "";
            List<string> lines = new List<string>();
            for (int i = 0; i < way.Steps.Length; i++) lines.Add((i + 1) + ". " + way.Steps[i]);
            return Marker + " (" + Voice.Quote(way.Words) + "), these steps did it, by the names of the controls: " + string.Join(" ", lines.ToArray()) + " Follow them when the screen matches; when it doesn't, work it out yourself.";
        }

        internal static string With(string background, string words)
        {
            if (!ForkSettings.Current.HandsRemember || (background ?? "").IndexOf(Marker, StringComparison.Ordinal) >= 0) return background;
            string offer = Offer(Find(words));
            if (offer.Length == 0) return background;
            ActivityLog.Write("hands", "remembered way", words);
            return (background ?? "").TrimEnd() + offer;
        }

        internal static void Learn(string words, IList<string> steps, string outcome)
        {
            if (!ForkSettings.Current.HandsRemember || string.IsNullOrWhiteSpace(words) || outcome == "waiting" || outcome == "asked") return;
            lock (gate)
            {
                List<Way> ways = All();
                Way known = Best(ways, words);
                if (outcome != "done")
                {
                    if (known == null || outcome != "failed") return;
                    ways.Remove(known);
                    Save(ways);
                    ActivityLog.Write("hands", "way dropped", known.Words);
                    return;
                }
                string[] kept = Kept(steps);
                if (kept.Length == 0) return;
                if (known != null) ways.Remove(known);
                ways.Insert(0, new Way { Words = words.Trim(), Steps = kept, When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Used = known == null ? 1 : known.Used + 1 });
                while (ways.Count > MostWays) ways.RemoveAt(ways.Count - 1);
                Save(ways);
            }
        }
    }
}
