using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class IdentityCase
    {
        internal string Id = "", Heard = "", Expected = "", Verdict = "";
    }

    internal static class ReplayIdentity
    {
        internal const string New = "NEW", Right = "RIGHT", Held = "HELD", HeldNew = "HELD NEW", Missed = "MISSED", Merged = "WRONG PERSON";
        private static readonly string[] Verdicts = { Right, Held, HeldNew, Missed, Merged };
        private static readonly Regex CaseLine = new Regex(@"^(?<id>i\w+)\s*\|\s*(?<heard>[^|]+?)\s*\|\s*(?<e>[^|]+?)\s*$");

        internal static List<IdentityCase> Read(IEnumerable<string> lines)
        {
            List<IdentityCase> cases = new List<IdentityCase>();
            foreach (string raw in lines)
            {
                Match line = CaseLine.Match(raw.Split('#')[0].Trim());
                if (line.Success) cases.Add(new IdentityCase { Id = line.Groups["id"].Value, Heard = line.Groups["heard"].Value, Expected = line.Groups["e"].Value });
            }
            return cases;
        }

        internal static string Judge(IdentityCase one, PersonGuess guess)
        {
            bool fresh = string.Equals(one.Expected, New, StringComparison.OrdinalIgnoreCase);
            if (guess.Self || guess.Unclear) return fresh ? HeldNew : Held;
            if (guess.Notes.Count == 0) return fresh ? Right : Missed;
            string title = Path.GetFileNameWithoutExtension(guess.Notes[0]);
            return !fresh && string.Equals(title, one.Expected, StringComparison.OrdinalIgnoreCase) ? Right : Merged;
        }

        internal static string Score(List<IdentityCase> cases)
        {
            StringBuilder text = new StringBuilder("replay identity: ").Append(cases.Count).Append(" names\n");
            foreach (string verdict in Verdicts)
            {
                List<string> ids = new List<string>();
                foreach (IdentityCase one in cases) if (one.Verdict == verdict) ids.Add(one.Id);
                text.Append(verdict.ToLowerInvariant()).Append(' ').Append(ids.Count);
                if (verdict != Right && ids.Count > 0) text.Append(" (").Append(string.Join(", ", ids.ToArray())).Append(')');
                text.Append('\n');
            }
            return text.ToString();
        }

        internal static int Live(string output)
        {
            string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri");
            string folder = Path.Combine(home, "replay");
            string file = Path.Combine(folder, "identity-cases.txt");
            if (!File.Exists(file)) { File.WriteAllText(output, "no identity-cases.txt in " + folder + "\n"); return 2; }
            ForkSettings saved = ForkSettings.Read(Path.Combine(home, "fork.json"));
            string vault = saved == null || string.IsNullOrWhiteSpace(saved.VaultFolder) ? Vault.DefaultFolder() : saved.VaultFolder.Trim();
            ForkSettings.Current = new ForkSettings();
            ForkSettings.Current.Tidy();
            string work = Path.Combine(Path.GetTempPath(), "capri-identity-replay-" + Guid.NewGuid().ToString("N"));
            ActivityLog.FilePath = Path.Combine(work, "activity.log");
            Brain.LogPath = Path.Combine(work, "errors.txt");
            try
            {
                DateTime now = DateTime.Now;
                Situation.Snapshot(vault, work, now);
                string root = Path.Combine(work, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                if (!Vault.Open(root, null)) { File.WriteAllText(output, "the vault copy did not open\n"); return 2; }
                Brain.MemoryPath = Path.Combine(root, Vault.CoreName);
                Names.Reset();
                List<IdentityCase> cases = Read(File.ReadAllLines(file, Encoding.UTF8));
                StringBuilder results = new StringBuilder("# id\theard\texpected\tverdict\tfound (local only, never in git)\n");
                foreach (IdentityCase one in cases)
                {
                    PersonGuess guess = PeopleNotes.Identify(one.Heard);
                    one.Verdict = Judge(one, guess);
                    results.Append(one.Id).Append('\t').Append(one.Heard).Append('\t').Append(one.Expected).Append('\t').Append(one.Verdict).Append('\t').Append(string.Join(" / ", guess.Who.ToArray())).Append('\n');
                }
                File.WriteAllText(Path.Combine(folder, "identity-results.txt"), results.ToString(), Encoding.UTF8);
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
