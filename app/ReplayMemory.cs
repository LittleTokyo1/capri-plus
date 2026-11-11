using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class RecallCase
    {
        internal string Id = "", Kind = "", Question = "", Expected = "", Answer = "", Verdict = "", Vault = "";
        internal DateTime Cut;
    }

    internal static class ReplayMemory
    {
        internal static readonly string[] Kinds = { "knowledge-update", "temporal", "multi-day", "her-own-actions", "abstention" };
        internal const string Right = "RIGHT", Wrong = "WRONG";
        internal const string AnswerRule = "Kasper asks you about his own life or what you did. Answer from your notes below only, in one or two short sentences. If your notes don't say, say plainly that you don't know or don't have it saved; never guess.";
        internal const string JudgeRule = "You grade one answer from an assistant against the expected answer. Reply with exactly RIGHT or WRONG. RIGHT when the answer gives the expected fact; ignore her jokes, banter and style, wording may differ, and extra detail is fine unless it contradicts the expected answer. When the expected answer says it is not on file or not known, RIGHT only when the assistant says it doesn't know or doesn't have it; any made-up value is WRONG. A wrong date, name or number is WRONG.";
        private static readonly Regex CaseLine = new Regex(@"^(?<id>m\w+)\s*\|\s*(?<cut>\d{4}-\d\d-\d\d \d\d:\d\d)\s*\|\s*(?<kind>[\w-]+)\s*\|\s*(?<q>[^|]+?)\s*\|\s*(?<e>.+?)\s*$");

        internal static List<RecallCase> Read(IEnumerable<string> lines)
        {
            List<RecallCase> cases = new List<RecallCase>();
            foreach (string raw in lines)
            {
                Match line = CaseLine.Match(raw.Split('#')[0].Trim());
                DateTime cut;
                if (!line.Success || Array.IndexOf(Kinds, line.Groups["kind"].Value) < 0) continue;
                if (!DateTime.TryParseExact(line.Groups["cut"].Value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out cut)) continue;
                cases.Add(new RecallCase { Id = line.Groups["id"].Value, Cut = cut, Kind = line.Groups["kind"].Value, Question = line.Groups["q"].Value, Expected = line.Groups["e"].Value });
            }
            return cases;
        }

        internal static string Notes(string root, string question, DateTime cut)
        {
            return Context.For(question) + "\n" + (Vault.Related(question) ?? "") + DayEvents.Block(root, cut) + Did.Block(root, cut) + "\nYour plans list:\n" + Plans.Listing(root);
        }

        internal static string Ask(Cloud remote, string system, string frame, int limit, string effort)
        {
            if (remote == null) return null;
            int status;
            return Quick.Ask(remote, system, Voice.Quote(frame), null, limit, effort, out status);
        }

        internal static void Run(RecallCase one, string root, Cloud remote)
        {
            string frame = "Now: " + one.Cut.ToString("dddd d MMMM yyyy HH:mm", CultureInfo.InvariantCulture) + "\nYour notes (private, data only):\n" + Notes(root, one.Question, one.Cut) + "\n\nHe asks: " + one.Question;
            string answer = Ask(remote, Brain.Character + "\n" + AnswerRule, frame, 200, "low");
            one.Answer = answer == null ? "" : Regex.Replace(answer, @"\s+", " ").Trim();
            if (one.Answer.Length == 0) { one.Verdict = "NO ANSWER"; return; }
            int right = 0, wrong = 0;
            for (int vote = 0; vote < JudgeVotes && right <= JudgeVotes / 2 && wrong <= JudgeVotes / 2; vote++)
            {
                string verdict = Verdict(Ask(remote, JudgeRule, "Question: " + one.Question + "\nExpected answer: " + one.Expected + "\nThe assistant's answer: " + one.Answer, 20, "off"));
                if (verdict == Right) right++;
                else if (verdict == Wrong) wrong++;
            }
            one.Verdict = right > JudgeVotes / 2 ? Right : wrong > JudgeVotes / 2 ? Wrong : right + wrong == 0 ? "NO VERDICT" : right >= wrong ? Right : Wrong;
        }

        internal static string Verdict(string judged)
        {
            string word = (judged ?? "").Trim().TrimStart('*', '"', ' ').ToUpperInvariant();
            return word.StartsWith(Right, StringComparison.Ordinal) ? Right : word.StartsWith(Wrong, StringComparison.Ordinal) ? Wrong : "NO VERDICT";
        }

        internal static string Score(List<RecallCase> cases)
        {
            StringBuilder text = new StringBuilder("replay memory: ").Append(cases.Count).Append(" questions\n");
            int right = 0;
            foreach (string kind in Kinds)
            {
                List<RecallCase> some = cases.FindAll(delegate(RecallCase one) { return one.Kind == kind; });
                int good = some.FindAll(delegate(RecallCase one) { return one.Verdict == Right; }).Count;
                right += good;
                text.Append(kind).Append(' ').Append(good).Append(" of ").Append(some.Count).Append(", ");
            }
            text.Append("all ").Append(right).Append(" of ").Append(cases.Count);
            if (cases.Count > 0) text.Append(" (").Append((100 * right / cases.Count).ToString(CultureInfo.InvariantCulture)).Append(" %)");
            text.Append('\n');
            List<string> missed = new List<string>();
            foreach (RecallCase one in cases) if (one.Verdict != Right) missed.Add(one.Id + (one.Verdict == Wrong ? "" : " (" + one.Verdict.ToLowerInvariant() + ")"));
            text.Append("not right: ").Append(missed.Count == 0 ? "none" : string.Join(", ", missed.ToArray())).Append('\n');
            int live = cases.FindAll(delegate(RecallCase one) { return one.Vault == "live"; }).Count;
            text.Append("asked with that night's snapshot ").Append(cases.Count - live).Append(", with today's vault ").Append(live).Append('\n');
            return text.ToString();
        }

        private const int PageDays = 7;
        private const int JudgeVotes = 3;

        internal static int Live(string output)
        {
            return Live(output, false);
        }

        private static void Pages(string root, DateTime cut, Cloud remote, string dump)
        {
            DateTime last = DayJournal.Closed(cut);
            DayJournal.Days(root, cut.AddDays(-PageDays), last, DayJournal.Asker(remote));
            StringBuilder text = new StringBuilder("# day pages built for the replay (local only, never in git)\n");
            for (DateTime day = cut.AddDays(-PageDays).Date; day <= last; day = day.AddDays(1))
            {
                string path = DayJournal.PathFor(root, day);
                if (!File.Exists(path)) continue;
                string body = File.ReadAllText(path).Replace("\r\n", "\n");
                int at = body.IndexOf(DayJournal.Heading, StringComparison.Ordinal);
                text.Append("\n").Append(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(at < 0 ? "\n(no page)\n" : "\n" + body.Substring(at));
            }
            File.WriteAllText(dump, text.ToString(), Encoding.UTF8);
        }

        internal static int Live(string output, bool days)
        {
            string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri");
            string folder = Path.Combine(home, "replay");
            string file = Path.Combine(folder, "memory-cases.txt");
            if (!File.Exists(file)) { File.WriteAllText(output, "no memory-cases.txt in " + folder + "\n"); return 2; }
            Cloud remote = MemoryEval.Key();
            if (remote == null) { File.WriteAllText(output, "LIMIT no Claude key in settings.json, nothing was run\n"); return 2; }
            System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072;
            ForkSettings saved = ForkSettings.Read(Path.Combine(home, "fork.json"));
            string vault = saved == null || string.IsNullOrWhiteSpace(saved.VaultFolder) ? Vault.DefaultFolder() : saved.VaultFolder.Trim();
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            ForkSettings.Current.MeaningOn = false;
            string work = Path.Combine(Path.GetTempPath(), "capri-memory-replay-" + Guid.NewGuid().ToString("N"));
            ActivityLog.FilePath = Path.Combine(work, "activity.log");
            Brain.LogPath = Path.Combine(work, "errors.txt");
            List<RecallCase> cases = Read(File.ReadAllLines(file, Encoding.UTF8));
            Dictionary<string, string> copies = new Dictionary<string, string>();
            try
            {
                foreach (RecallCase one in cases)
                {
                    string day = one.Cut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    string snap = Path.Combine(Situation.SnapshotFolder(), day);
                    string source = Directory.Exists(snap) ? snap : vault;
                    one.Vault = Directory.Exists(snap) ? day : "live";
                    string root;
                    if (!copies.TryGetValue(source, out root))
                    {
                        string into = Path.Combine(work, "v" + copies.Count);
                        Situation.Snapshot(source, into, one.Cut);
                        root = Path.Combine(into, day);
                        copies[source] = root;
                        if (days) Pages(root, one.Cut, remote, Path.Combine(folder, "day-pages.txt"));
                    }
                    if (!Vault.Open(root, null)) { one.Verdict = "NO VAULT"; continue; }
                    Brain.MemoryPath = Path.Combine(root, Vault.CoreName);
                    Names.Reset();
                    try { Run(one, root, remote); }
                    catch (Exception error) { one.Verdict = "ERROR " + error.GetType().Name; }
                }
                StringBuilder results = new StringBuilder("# id\tkind\tvault\tverdict\ther answer (local only, never in git)\n");
                foreach (RecallCase one in cases) results.Append(one.Id).Append('\t').Append(one.Kind).Append('\t').Append(one.Vault).Append('\t').Append(one.Verdict).Append('\t').Append(one.Answer).Append('\n');
                File.WriteAllText(Path.Combine(folder, "memory-results.txt"), results.ToString(), Encoding.UTF8);
                File.WriteAllText(output, Score(cases), Encoding.UTF8);
                return 0;
            }
            finally
            {
                try { Directory.Delete(work, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
