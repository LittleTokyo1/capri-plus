using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class Explained
    {
        private const int GapMinutes = 10;
        private const int SameSeconds = 60;
        private const long Present = 2L * 60 * 1000;
        private static readonly Regex Asks = new Regex(@"^(?:(?:hey|ok|okay|so|capri|um|yo)[,\s]+)*(?:(?:can|could|would) you |please )?(?:(?:explain|teach me|walk me through|break down|go over)(?: to me)?(?: how| what| why)? (?<topic>.{3,120}?)|how (?:does|do|did) (?<topic>.{2,100}?) (?:work|works|happen)|what(?:'s| is) the difference between (?<topic>.{3,120}?))\s*[?.!]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Personal = new Regex(@"\b(?:you|your|yours|yourself|capri|me|my|mine|i|im|i'm|this pc|setting|settings|mode|tool)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly object gate = new object();
        private static DateTime lastAt = DateTime.MinValue;
        private static string lastText;

        internal static Func<bool> AtPc = delegate { return Native.IdleMilliseconds() < Present && !Lockout.Active && !Companion.FullScreen(); };

        internal static void Reset()
        {
            lock (gate)
            {
                lastAt = DateTime.MinValue;
                lastText = null;
            }
        }

        internal static ToolArguments Request(string text, DateTime now)
        {
            if (!Wants(text, now)) return null;
            return new ToolArguments { Action = "replace", Description = text.Trim() + " (a diagram that explains it)" };
        }

        private static bool Wants(string text, DateTime now)
        {
            ForkSettings settings = ForkSettings.Current;
            if (settings == null || !settings.ExplainDiagrams || !ForkDiagrams.On()) return false;
            string said = (text ?? "").Trim();
            Match asked = Asks.Match(said);
            if (!asked.Success || Personal.IsMatch(asked.Groups["topic"].Value) || SelfKnow.Matching(said) != null) return false;
            lock (gate)
            {
                if (said == lastText && now >= lastAt && now - lastAt < TimeSpan.FromSeconds(SameSeconds)) return true;
                if (lastAt != DateTime.MinValue && now >= lastAt && now - lastAt < TimeSpan.FromMinutes(GapMinutes)) return false;
            }
            Func<bool> here = AtPc;
            if (here == null || !here()) return false;
            lock (gate)
            {
                lastAt = now;
                lastText = said;
            }
            ActivityLog.Write("draw", "explaining", "a diagram for his question");
            return true;
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox explainDiagrams;

        private void ExplainRow(ForkSettings current)
        {
            explainDiagrams = Check("Diagrams when explaining: when you ask how something works or the difference between two things at the PC, she draws a diagram and talks you through it (at most once every 10 minutes, never in a game)", current.ExplainDiagrams);
        }

        private void FillExplain()
        {
            settings.ExplainDiagrams = explainDiagrams.Checked;
        }
    }
}
