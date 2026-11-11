using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class Honest
    {
        private const int MostWords = 6;
        private const double MostSame = 0.9;
        private const int MostFailure = 140;
        private static readonly Regex Claim = new Regex(@"^\W*(?:done|finished|complete|completed|all done|sent|i (?:sent|did|finished|completed))\b", RegexOptions.IgnoreCase);
        private static readonly Regex Instruction = new Regex(@"^\W*(?:[Pp]lease )?(?:[Tt]ell|[Aa]sk|[Mm]essage|[Tt]ext|DM|[Dd]m|[Ll]et)\s+(?:(?i:him|her|them)|[A-Z]\w+)\s+(?:to|that|about)\b");
        private static readonly Regex Messaging = new Regex(@"\b(message|messages|text|dm|reply|tell|ask|chat|say|send|write to)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Quoted = new Regex("[\"“]([^\"”]+)[\"”]");
        private static readonly Regex Word = new Regex(@"[\p{L}\p{N}']+");

        private static string Result(string step)
        {
            int split = step.IndexOf(": ", System.StringComparison.Ordinal);
            string result = split >= 0 ? step.Substring(split + 2) : step;
            result = Regex.Replace(result, @"\[\d+\]\s*", "").Trim();
            return result.Length > MostFailure ? result.Substring(0, MostFailure - 3) + "..." : result;
        }

        private static string LastFailure(IList<string> steps)
        {
            if (steps == null || steps.Count == 0) return null;
            string last = Result(steps[steps.Count - 1]);
            return Computer.Failed(last) ? last : null;
        }

        internal static string Outcome(string said, IList<string> steps)
        {
            string failure = LastFailure(steps);
            if (failure == null || !Claim.IsMatch(said ?? "")) return said;
            return said.TrimEnd() + " But the last step didn't work: " + failure;
        }

        internal static string Summary(IList<string> steps)
        {
            if (steps == null || steps.Count == 0) return "I haven't done anything yet.";
            List<string> worked = new List<string>();
            foreach (string step in steps)
            {
                string result = Result(step);
                if (!Computer.Failed(result) && worked.Count < 3) worked.Add(result.TrimEnd('.'));
            }
            string failure = LastFailure(steps);
            if (worked.Count == 0) return "Couldn't do that: " + (failure ?? Result(steps[steps.Count - 1]));
            string did = string.Join(", then ", worked.ToArray());
            return failure == null ? "Done. " + did + "." : "Partly done. " + did + ". The last step didn't work: " + failure;
        }

        internal static bool Instructive(string text)
        {
            return Instruction.IsMatch(text ?? "");
        }

        internal static bool Echo(string typed, string task, bool chatBox)
        {
            if (!chatBox || string.IsNullOrWhiteSpace(typed) || string.IsNullOrWhiteSpace(task) || !Messaging.IsMatch(task)) return false;
            string plain = typed.Trim().Trim('.', '!', '?', ' ');
            foreach (Match quote in Quoted.Matches(task)) if (string.Equals(quote.Groups[1].Value.Trim().Trim('.', '!', '?', ' '), plain, System.StringComparison.OrdinalIgnoreCase)) return false;
            if (Instruction.IsMatch(typed)) return true;
            MatchCollection words = Word.Matches(typed.ToLowerInvariant());
            if (words.Count < MostWords) return false;
            string known = " " + string.Join(" ", ToWords(task)) + " ";
            int same = 0;
            foreach (Match word in words) if (known.Contains(" " + word.Value + " ")) same++;
            return same >= words.Count * MostSame;
        }

        private static string[] ToWords(string text)
        {
            List<string> words = new List<string>();
            foreach (Match word in Word.Matches(text.ToLowerInvariant())) words.Add(word.Value);
            return words.ToArray();
        }
    }
}
