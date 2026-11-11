using System;
using System.Collections.Generic;
using System.Globalization;

namespace CapriDesktop
{
    internal sealed partial class ForkSettingsForm
    {
        internal const string NoPastMonths = "No past months kept yet; each month is kept here once it ends.";

        private void SpendClaude(ForkSettings current)
        {
            Stat(SpendBook.Money(current.Spent), " of " + SpendBook.Money(current.Cap) + " on Claude this month", current.Cap <= 0 ? (current.Spent > 0 ? 1 : 0) : current.Spent / current.Cap, current.Searches + " web searches, " + current.PlanRuns + " big jobs on your plan (those are on your plan, not the API).");
            SpendMonth now = SpendBook.Of(current.Month);
            if (now == null || now.Models.Count == 0) return;
            Caption("This month by model");
            foreach (SpendLine line in now.Models) Note(Line(line));
            Caption("This month by use");
            foreach (SpendLine line in now.Uses) Note(Line(line));
        }

        private void SpendVoice(ForkSettings current)
        {
            Stat(current.VoiceChars.ToString("N0", CultureInfo.InvariantCulture), current.VoiceBudget > 0 ? " of " + current.VoiceBudget.ToString("N0", CultureInfo.InvariantCulture) + " ElevenLabs characters this month" : " ElevenLabs characters used this month", current.VoiceBudget > 0 ? (double)current.VoiceChars / current.VoiceBudget : 0, "Counted from what she sends to ElevenLabs. The free voice takes over at the limit.");
        }

        private void SpendHistory(ForkSettings current)
        {
            Caption("Past months");
            List<string> past = PastMonths(current.Month);
            if (past.Count == 0) Note(NoPastMonths);
            foreach (string line in past) Note(line);
        }

        internal static List<string> PastMonths(string month)
        {
            List<string> lines = new List<string>();
            foreach (SpendMonth past in SpendBook.Months())
            {
                if (string.CompareOrdinal(past.Month, month ?? "") >= 0) continue;
                lines.Add(past.Month + ": Claude " + SpendBook.Money(past.Total) + ", " + past.Searches + " web searches, " + past.VoiceChars.ToString("N0", CultureInfo.InvariantCulture) + " ElevenLabs characters, " + past.PlanRuns + " big jobs");
            }
            return lines;
        }

        private static string Line(SpendLine line)
        {
            return line.Name + ": " + SpendBook.Money(line.Cost) + (line.Calls > 0 ? " (" + line.Calls + (line.Calls == 1 ? " call)" : " calls)") : "");
        }
    }
}
