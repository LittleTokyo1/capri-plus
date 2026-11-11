using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class ReplayCase
    {
        internal string Id = "", Kind = "", Text = "", Person = "", Label = "";
        internal bool Found;
        internal DateTime At;
    }

    internal static class ReplayEval
    {
        internal const int SameLineSeconds = 120;
        internal const int RepeatHours = 24;
        internal const string Duplicate = "duplicate";
        internal static readonly string[] Labels = { "useful", "stale-done", "stale-waiting", "declined-before", Duplicate, "bad-timing", "cut-off" };
        private static readonly Regex Line = new Regex(@"^(?<at>\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)  (?<area>heartbeat|checkin|schedule|discord|autonomy)  (?<what>.+?)  ->  (?<text>.*)$");
        private static readonly Regex Spoken = new Regex(@"^(heartbeat (sent|told)|checkin sent.*|schedule (sent|told)|discord friend (online told|online sent to his DM|watch told|watch sent to his DM)|autonomy (told by voice in a game.*|offered with keys|did on her own))$");
        private static readonly Regex Arrival = new Regex(@"^(?<who>.+?)(?: and [^.]+?)? (?:just )?came online");

        internal static int Live(string set, string output)
        {
            try
            {
                if (set == "understanding") return ReplayUnderstand.Live(output);
                if (set == "memory") return ReplayMemory.Live(output);
                if (set == "memory-days") return ReplayMemory.Live(output, true);
                if (set == "identity") return ReplayIdentity.Live(output);
                if (set != "autonomy") { File.WriteAllText(output, "the sets are autonomy and understanding\n"); return 2; }
                string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri");
                OpenVault(home);
                string folder = Path.Combine(home, "replay");
                Directory.CreateDirectory(folder);
                List<ReplayCase> cases = Cases(File.ReadAllLines(Path.Combine(home, "activity.log"), Encoding.UTF8));
                Label(cases, ReadLabels(Path.Combine(folder, "autonomy-labels.txt")));
                WriteCases(Path.Combine(folder, "autonomy-cases.txt"), cases);
                File.WriteAllText(output, Score(cases), Encoding.UTF8);
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(output, "FAIL " + error.GetType().Name + "\n");
                return 1;
            }
        }

        private static void OpenVault(string home)
        {
            ForkSettings settings = ForkSettings.Read(Path.Combine(home, "fork.json"));
            string folder = settings == null || string.IsNullOrWhiteSpace(settings.VaultFolder) ? Vault.DefaultFolder() : settings.VaultFolder.Trim();
            if (Directory.Exists(folder)) Vault.Open(folder, null);
        }

        internal static List<ReplayCase> Cases(IEnumerable<string> lines)
        {
            List<ReplayCase> cases = new List<ReplayCase>();
            foreach (string raw in lines)
            {
                Match found = Line.Match(raw ?? "");
                if (!found.Success) continue;
                string kind = found.Groups["area"].Value + " " + found.Groups["what"].Value;
                if (!Spoken.IsMatch(kind)) continue;
                DateTime at = DateTime.ParseExact(found.Groups["at"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                string text = found.Groups["text"].Value.Trim();
                if (SameDelivery(cases, text, at)) continue;
                cases.Add(new ReplayCase { Id = IdOf(at, text), Kind = kind, Text = text, At = at, Person = PersonOf(kind, text) });
            }
            return cases;
        }

        private static bool SameDelivery(List<ReplayCase> cases, string text, DateTime at)
        {
            for (int i = cases.Count - 1; i >= 0 && (at - cases[i].At).TotalSeconds <= SameLineSeconds; i--)
                if (Norm(cases[i].Text) == Norm(text)) return true;
            return false;
        }

        private static string PersonOf(string kind, string text)
        {
            if (!kind.StartsWith("discord friend", StringComparison.Ordinal)) return "";
            string who = kind.StartsWith("discord friend online", StringComparison.Ordinal) ? text : Arrival.Match(text).Groups["who"].Value;
            who = who.Trim();
            if (who.Length == 0) return "";
            foreach (NameHit hit in Names.Find(who)) if (!hit.Self && hit.Note != null) return hit.Note.ToLowerInvariant();
            return who.ToLowerInvariant();
        }

        internal static void Label(List<ReplayCase> cases, Dictionary<string, string> hand)
        {
            for (int i = 0; i < cases.Count; i++)
            {
                cases[i].Found = Repeats(cases, i);
                string given;
                cases[i].Label = hand.TryGetValue(cases[i].Id, out given) ? given : cases[i].Found ? Duplicate : "";
            }
        }

        private static bool Repeats(List<ReplayCase> cases, int at)
        {
            ReplayCase now = cases[at];
            for (int i = 0; i < at; i++)
            {
                double hours = (now.At - cases[i].At).TotalHours;
                if (hours < 0 || hours > RepeatHours) continue;
                if (Norm(cases[i].Text) == Norm(now.Text)) return true;
                if (now.Person.Length > 0 && now.Person == cases[i].Person && hours * 60 < Arrivals.Minutes) return true;
            }
            return false;
        }

        internal static Dictionary<string, string> ReadLabels(string path)
        {
            Dictionary<string, string> labels = new Dictionary<string, string>();
            if (!File.Exists(path)) return labels;
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Split('#')[0].Trim();
                string[] part = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (part.Length >= 2 && Array.IndexOf(Labels, part[1]) >= 0) labels[part[0]] = part[1];
            }
            return labels;
        }

        private static void WriteCases(string path, List<ReplayCase> cases)
        {
            StringBuilder text = new StringBuilder("# id\twhen\tkind\tperson\tlabel\tline (local only, never in git)\n");
            foreach (ReplayCase one in cases)
                text.Append(one.Id).Append('\t').Append(one.At.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append('\t').Append(one.Kind).Append('\t').Append(one.Person).Append('\t').Append(one.Label).Append('\t').Append(one.Text).Append('\n');
            File.WriteAllText(path, text.ToString(), Encoding.UTF8);
        }

        internal static string Score(List<ReplayCase> cases)
        {
            StringBuilder text = new StringBuilder();
            text.Append("replay autonomy: ").Append(cases.Count).Append(" lines she started herself");
            if (cases.Count > 0) text.Append(", ").Append(cases[0].At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(" to ").Append(cases[cases.Count - 1].At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            text.Append('\n');
            int unlabeled = 0, bad = 0;
            List<string> found = new List<string>();
            foreach (ReplayCase one in cases)
            {
                if (one.Label.Length == 0) unlabeled++;
                if (one.Label.StartsWith("stale", StringComparison.Ordinal) || one.Label == Duplicate || one.Label == "cut-off") bad++;
                if (one.Found) found.Add(one.Id);
            }
            foreach (string label in Labels) text.Append(label).Append(' ').Append(cases.FindAll(delegate(ReplayCase one) { return one.Label == label; }).Count).Append(", ");
            text.Append("unlabeled ").Append(unlabeled).Append('\n');
            text.Append("stale + duplicate + cut-off: ").Append(bad).Append(" of ").Append(cases.Count).Append('\n');
            text.Append("repeats found by code: ").Append(found.Count).Append(found.Count > 0 ? " (" + string.Join(" ", found.ToArray()) + ")" : "").Append('\n');
            return text.ToString();
        }

        private static string Norm(string text)
        {
            return Regex.Replace(Regex.Replace((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{N} ]", " "), @"\s+", " ").Trim();
        }

        private static string IdOf(DateTime at, string text)
        {
            using (SHA1 hash = SHA1.Create())
            {
                byte[] sum = hash.ComputeHash(Encoding.UTF8.GetBytes(at.ToString("s", CultureInfo.InvariantCulture) + "|" + text));
                return "r" + BitConverter.ToString(sum, 0, 4).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
