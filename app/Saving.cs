using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal static class Saving
    {
        private const int PassWaitMs = 6000;
        private const int JustSeconds = 30;

        internal static Func<Cloud> Remote = delegate { return null; };

        internal static string Save(string text)
        {
            string plain = (text ?? "").Trim();
            if (plain.Length == 0 || Vault.Root == null || Brain.Personal.IsMatch(plain)) return Brain.Note(plain);
            DateTime asked = DateTime.Now;
            for (int waited = 0; PeopleNotes.Passes > 0 && waited < PassWaitMs; waited += 100) Thread.Sleep(100);
            string just = Just(plain, asked.AddSeconds(-JustSeconds));
            if (just != null) return just;
            string why;
            PeopleResult result;
            try { result = PeopleNotes.Told(Remote(), plain, out why); }
            catch (Exception error)
            {
                result = null;
                ActivityLog.Write("memory", "save", error.GetType().Name);
            }
            string told = Said(result);
            if (told != null) return told;
            if (result != null && result.Held.Count > 0) return "Couldn't save yet: " + string.Join("; ", result.Held.ToArray()) + ". Ask them which one they mean.";
            return Brain.Note(plain, DateTime.Now);
        }

        private static string Just(string text, DateTime since)
        {
            foreach (NameHit hit in Names.Find(text))
            {
                if (!hit.Exact || hit.Unsure || hit.Self || hit.Note == null || !PeopleNotes.WroteSince(hit.Note, since)) continue;
                string body;
                try { body = Path.GetFileNameWithoutExtension(hit.Note) + "\n" + File.ReadAllText(hit.Note); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                if (!Covers(body, text)) { ActivityLog.Write("memory", "save", "the memory pass kept only part of it"); continue; }
                ActivityLog.Write("memory", "save", "already written by the memory pass");
                return "Saved to People/" + Path.GetFileName(hit.Note) + " (the memory pass wrote it a moment ago).";
            }
            return null;
        }

        private const double MostlyKept = 0.75;
        private const int StemLength = 5;
        private static readonly string[] AskWords = { "remember", "please", "about", "write", "down", "there", "their", "these", "those", "would", "should", "could" };

        internal static bool Covers(string note, string text)
        {
            string kept = (note ?? "").ToLowerInvariant();
            List<string> stems = new List<string>();
            foreach (Match word in Regex.Matches((text ?? "").ToLowerInvariant(), @"\p{L}{5,}"))
            {
                if (Array.IndexOf(AskWords, word.Value) >= 0) continue;
                string stem = word.Value.Substring(0, StemLength);
                if (!stems.Contains(stem)) stems.Add(stem);
            }
            if (stems.Count == 0) return true;
            int found = stems.FindAll(delegate(string stem) { return kept.Contains(stem); }).Count;
            return found >= stems.Count * MostlyKept;
        }

        internal static string Said(PeopleResult result)
        {
            if (result == null) return null;
            List<string> parts = new List<string>();
            for (int i = 0; i < result.Saved.Count && i < result.Files.Count; i++)
            {
                string line = result.Saved[i];
                int colon = line.IndexOf(':');
                parts.Add(result.Files[i] + (colon > 0 ? line.Substring(colon) : ""));
            }
            foreach (string mine in result.Mine) parts.Add("my notes: " + mine);
            foreach (string plan in result.PlanTexts) parts.Add("Capri/plans.md: " + plan);
            if (result.Closed > 0) parts.Add("ticked off " + result.Closed + (result.Closed == 1 ? " plan" : " plans") + " in Capri/plans.md");
            if (result.Fixed > 0) parts.Add("updated lines in " + string.Join(", ", result.FixedIn.ToArray()));
            return parts.Count == 0 ? null : "Saved to " + string.Join("; ", parts.ToArray()) + ".";
        }
    }
}
