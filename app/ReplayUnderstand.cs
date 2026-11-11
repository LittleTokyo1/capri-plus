using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class HeardCase
    {
        internal string Id = "", Text = "", Reading = "", What = "", Reply = "", Situation = "", Label = "", LabelWhat = "";
        internal DateTime At;
    }

    internal static class ReplayUnderstand
    {
        internal const int ActionSeconds = 180;
        internal const int SituationSeconds = 90;
        internal static readonly DateTime TuneUntil = new DateTime(2026, 11, 4);
        internal static readonly string[] Readings = { "CHAT", "DO", "ASK" };
        private static readonly string[] Tools = { "use_computer", "hands", "media", "play_music", "open_app", "open_website", "app_window", "look_at_screen", "setting", "browser", "long job", "long_job", "google", "reminder", "schedule", "notes", "remember", "watch_for", "shortcut", "power", "nap", "draw", "animate", "move_yourself", "move", "spotify play", "app volume", "lookup", "league research", "stop", "discord", "spotify sign in", "google sign in", "league queue", "league lock", "spotify heart", "phone" };
        private static readonly Regex Spoke = new Regex(@"^(?<at>\d\d:\d\d) (?<who>him|her): (?<text>.*)$");
        private static readonly Regex Act = new Regex(@"^(?<at>\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)  (?<area>.+?)(?:  (?<what>.*?))?(?:  ->  .*)?$");
        private static readonly Regex Clarify = new Regex(@"\b(which (?:one|of|do you)|what do you mean|did you mean|do you mean|who do you mean|or do you|didn.t catch|say (?:it|that) again|spell it out)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Sit = new Regex(@"^(?<at>\d{4}-\d\d-\d\d \d\d:\d\d:\d\d) \| (?<rest>.*) \| (?<text>.*)$");

        internal static int Live(string output)
        {
            string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri");
            ForkSettings settings = ForkSettings.Read(Path.Combine(home, "fork.json"));
            string vault = settings == null || string.IsNullOrWhiteSpace(settings.VaultFolder) ? Vault.DefaultFolder() : settings.VaultFolder.Trim();
            string folder = Path.Combine(home, "replay");
            Directory.CreateDirectory(folder);
            Dictionary<string, string[]> days = new Dictionary<string, string[]>();
            string chats = Path.Combine(vault, "Capri", "chats");
            if (Directory.Exists(chats))
                foreach (string file in Directory.GetFiles(chats, "????-??-??.md")) days[Path.GetFileNameWithoutExtension(file)] = File.ReadAllLines(file, Encoding.UTF8);
            List<string> acts = new List<string>();
            foreach (string name in new[] { "activity.log.old", "activity.log" })
            {
                string path = Path.Combine(home, name);
                if (File.Exists(path)) acts.AddRange(File.ReadAllLines(path, Encoding.UTF8));
            }
            string situations = Path.Combine(home, Situation.FileName);
            List<HeardCase> cases = Cases(days, acts, File.Exists(situations) ? File.ReadAllLines(situations, Encoding.UTF8) : new string[0]);
            Label(cases, ReadLabels(Path.Combine(folder, "understanding-labels.txt")));
            WriteCases(Path.Combine(folder, "understanding-cases.txt"), cases);
            File.WriteAllText(output, Score(cases), Encoding.UTF8);
            return 0;
        }

        internal static List<HeardCase> Cases(Dictionary<string, string[]> days, IEnumerable<string> activity, IEnumerable<string> situations)
        {
            List<HeardCase> cases = new List<HeardCase>();
            List<string> names = new List<string>(days.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string day in names)
            {
                DateTime date;
                if (!DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) continue;
                HeardCase open = null;
                foreach (string raw in days[day])
                {
                    Match line = Spoke.Match(raw.Trim());
                    if (!line.Success) continue;
                    TimeSpan time;
                    if (!TimeSpan.TryParseExact(line.Groups["at"].Value, @"hh\:mm", CultureInfo.InvariantCulture, out time)) continue;
                    string text = line.Groups["text"].Value.Trim();
                    if (line.Groups["who"].Value == "him")
                    {
                        if (text.Length == 0 || text.StartsWith("(", StringComparison.Ordinal)) { open = null; continue; }
                        open = new HeardCase { At = date + time, Text = text };
                        open.Id = Id(open.At, text);
                        cases.Add(open);
                    }
                    else if (open != null && open.Reply.Length == 0) open.Reply = text;
                }
            }
            cases.Sort(delegate(HeardCase a, HeardCase b) { return a.At.CompareTo(b.At); });
            foreach (string raw in activity)
            {
                Match act = Act.Match(raw);
                if (!act.Success) continue;
                string area = act.Groups["area"].Value, what = act.Groups["what"].Value;
                if (Array.IndexOf(Tools, area) < 0) continue;
                if (area == "discord" && !(what.StartsWith("message to ", StringComparison.Ordinal) || what.StartsWith("reply to ", StringComparison.Ordinal) || what.IndexOf(" | message", StringComparison.Ordinal) >= 0)) continue;
                if (area == "setting" && what != "changed" && what != "undo") continue;
                if (area == "schedule" && what != "added") continue;
                DateTime at;
                if (!DateTime.TryParseExact(act.Groups["at"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) continue;
                HeardCase owner = Owner(cases, at);
                if (owner != null && owner.What.Length == 0) owner.What = area;
            }
            List<KeyValuePair<DateTime, string>> seen = new List<KeyValuePair<DateTime, string>>();
            foreach (string raw in situations)
            {
                Match sit = Sit.Match(raw);
                DateTime at;
                if (sit.Success && DateTime.TryParseExact(sit.Groups["at"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) seen.Add(new KeyValuePair<DateTime, string>(at, sit.Groups["rest"].Value.Trim() + "|" + sit.Groups["text"].Value.Trim()));
            }
            foreach (HeardCase one in cases)
            {
                one.Reading = one.What.Length > 0 ? "DO" : one.Reply.IndexOf('?') >= 0 && Clarify.IsMatch(one.Reply) ? "ASK" : "CHAT";
                foreach (KeyValuePair<DateTime, string> sit in seen)
                {
                    double gap = (sit.Key - one.At).TotalSeconds;
                    int bar = sit.Value.LastIndexOf('|');
                    if (gap < -5 || gap > SituationSeconds || !Same(sit.Value.Substring(bar + 1), one.Text)) continue;
                    one.Situation = sit.Value.Substring(0, bar);
                    break;
                }
            }
            return cases;
        }

        private static HeardCase Owner(List<HeardCase> cases, DateTime at)
        {
            HeardCase found = null;
            foreach (HeardCase one in cases)
            {
                if (one.At > at) break;
                found = one;
            }
            if (found == null || (at - found.At).TotalSeconds > ActionSeconds) return null;
            foreach (HeardCase one in cases)
                if (one.At == found.At && one.What.Length == 0) return one;
            return found;
        }

        private static bool Same(string a, string b)
        {
            string x = Norm(a), y = Norm(b);
            int length = Math.Min(24, Math.Min(x.Length, y.Length));
            return length > 0 && string.CompareOrdinal(x, 0, y, 0, length) == 0;
        }

        internal static void Label(List<HeardCase> cases, Dictionary<string, string> hand)
        {
            foreach (HeardCase one in cases)
            {
                string label;
                if (!hand.TryGetValue(one.Id, out label)) continue;
                string[] part = label.Split(new[] { ' ' }, 2);
                one.Label = part[0];
                one.LabelWhat = part.Length > 1 ? part[1].Trim() : "";
            }
        }

        internal static Dictionary<string, string> ReadLabels(string path)
        {
            Dictionary<string, string> labels = new Dictionary<string, string>();
            if (!File.Exists(path)) return labels;
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Split('#')[0].Trim();
                string[] part = line.Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (part.Length >= 2 && Array.IndexOf(Readings, part[1]) >= 0) labels[part[0]] = part[1] + (part.Length > 2 ? " " + part[2].Trim() : "");
            }
            return labels;
        }

        internal static bool Right(HeardCase one)
        {
            if (one.Label.Length == 0 || one.Label != one.Reading) return false;
            if (one.Label != "DO" || one.LabelWhat.Length == 0) return true;
            foreach (string tool in one.LabelWhat.Split('/')) if (tool.Trim() == one.What) return true;
            return false;
        }

        private static void WriteCases(string path, List<HeardCase> cases)
        {
            StringBuilder text = new StringBuilder("# id\twhen\tshe did\ttool\tlabel\tsituation\this line | her reply (local only, never in git)\n");
            foreach (HeardCase one in cases)
                text.Append(one.Id).Append('\t').Append(one.At.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append('\t').Append(one.Reading).Append('\t').Append(one.What).Append('\t').Append((one.Label + " " + one.LabelWhat).Trim()).Append('\t').Append(one.Situation).Append('\t').Append(one.Text).Append(" | ").Append(one.Reply.Length > 160 ? one.Reply.Substring(0, 160) : one.Reply).Append('\n');
            File.WriteAllText(path, text.ToString(), Encoding.UTF8);
        }

        internal static string Score(List<HeardCase> cases)
        {
            StringBuilder text = new StringBuilder();
            text.Append("replay understanding: ").Append(cases.Count).Append(" of his lines");
            if (cases.Count > 0) text.Append(", ").Append(cases[0].At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(" to ").Append(cases[cases.Count - 1].At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            text.Append('\n');
            foreach (string reading in Readings) text.Append("she ").Append(reading).Append(' ').Append(cases.FindAll(delegate(HeardCase one) { return one.Reading == reading; }).Count).Append(", ");
            text.Append("with a situation line ").Append(cases.FindAll(delegate(HeardCase one) { return one.Situation.Length > 0; }).Count).Append('\n');
            Half(text, "tune (to 3 Nov)", cases.FindAll(delegate(HeardCase one) { return one.At < TuneUntil; }));
            Half(text, "test (from 4 Nov)", cases.FindAll(delegate(HeardCase one) { return one.At >= TuneUntil; }));
            return text.ToString();
        }

        private static void Half(StringBuilder text, string name, List<HeardCase> cases)
        {
            List<HeardCase> labelled = cases.FindAll(delegate(HeardCase one) { return one.Label.Length > 0; });
            int right = labelled.FindAll(Right).Count;
            List<string> needless = new List<string>(), missed = new List<string>(), wrong = new List<string>();
            foreach (HeardCase one in labelled)
            {
                if (Right(one)) continue;
                if (one.Reading == "ASK") needless.Add(one.Id);
                else if (one.Label == "ASK") missed.Add(one.Id);
                else wrong.Add(one.Id);
            }
            text.Append(name).Append(": labelled ").Append(labelled.Count).Append(", right ").Append(right);
            if (labelled.Count > 0) text.Append(" (").Append((100 * right / labelled.Count).ToString(CultureInfo.InvariantCulture)).Append(" %)");
            text.Append(", asked needlessly ").Append(needless.Count).Append(", missed asks ").Append(missed.Count).Append(", wrong reading ").Append(wrong.Count);
            List<string> ids = new List<string>(needless);
            ids.AddRange(missed);
            ids.AddRange(wrong);
            if (ids.Count > 0) text.Append(" (").Append(string.Join(" ", ids.ToArray())).Append(')');
            text.Append('\n');
        }

        private static string Norm(string text)
        {
            return Regex.Replace(Regex.Replace((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{N} ]", " "), @"\s+", " ").Trim();
        }

        private static string Id(DateTime at, string text)
        {
            using (System.Security.Cryptography.SHA1 hash = System.Security.Cryptography.SHA1.Create())
            {
                byte[] sum = hash.ComputeHash(Encoding.UTF8.GetBytes(at.ToString("s", CultureInfo.InvariantCulture) + "|" + text));
                return "u" + BitConverter.ToString(sum, 0, 4).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
