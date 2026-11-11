using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class SchoolGmail
    {
        private const int ListMost = 10;
        private const int TopicMost = 5;
        private const int SnippetChars = 160;
        private const int LooseDays = 30;
        private const int LooseMost = 20;
        private const long FileLimit = 2L * 1024 * 1024;
        private static readonly Regex Student = new Regex(@"\b\d{6,9}@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+\b", RegexOptions.Compiled);
        private static readonly object gate = new object();
        private static string address;
        private static bool looked;

        internal static Func<bool> Connected = delegate { return Google.Connected; };
        internal static Func<string, int, List<HeartMail>> Search = delegate(string query, int most) { string problem; return Google.Find(query, most, out problem); };
        internal static Func<string, string> Read = delegate(string id) { return Google.Read(id); };

        internal static void Forget()
        {
            lock (gate)
            {
                address = null;
                looked = false;
            }
        }

        internal static string Address()
        {
            lock (gate)
            {
                if (looked) return address;
                looked = true;
                address = Look(Study.Folder);
                return address;
            }
        }

        private static string Look(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
            try
            {
                foreach (string file in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    string kind = Path.GetExtension(file).ToLowerInvariant();
                    if (kind != ".md" && kind != ".json" && kind != ".txt") continue;
                    if (file.IndexOf(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (new FileInfo(file).Length > FileLimit) continue;
                    Match found = Student.Match(File.ReadAllText(file));
                    if (found.Success) return found.Value.ToLowerInvariant();
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        internal static bool Ready()
        {
            Func<bool> connected = Connected;
            return connected != null && connected() && Address() != null;
        }

        internal static string Query(string to, string topic)
        {
            string words = Regex.Replace(Regex.Replace(topic ?? "", @"[^\p{L}\d ]+", " "), @"\s+", " ").Trim();
            return "to:" + to + (words.Length == 0 ? " newer_than:7d" : " newer_than:60d " + words);
        }

        internal static string Answer(string topic)
        {
            Func<bool> connected = Connected;
            if (connected == null || !connected()) return null;
            string to = Address();
            if (to == null)
            {
                ActivityLog.Write("study", "school mail from Gmail", "skipped: no school address in his study files");
                return null;
            }
            string about = (topic ?? "").Trim();
            List<HeartMail> found = Search(Query(to, about), about.Length == 0 ? ListMost : TopicMost);
            bool loose = false;
            if (found != null && found.Count == 0 && about.Length > 0)
            {
                found = Search("to:" + to + " newer_than:" + LooseDays + "d", LooseMost);
                loose = true;
            }
            if (found == null || found.Count == 0)
            {
                ActivityLog.Write("study", "school mail from Gmail", found == null ? "search failed, the Outlook check runs" : "0 found" + (about.Length > 0 ? " for " + Pursuit.Shorter(about, 60) : "") + ", the Outlook check runs");
                return null;
            }
            StringBuilder text = new StringBuilder(loose ? "No school mail matched \"" + about + "\" word for word, so here are his school mails from the last " + LooseDays + " days (forwarded copies in his Gmail, newest first) " + Google.Untrusted + ". Pick the one he means and open it with the google tool, action read and its id:" : "School mail from the forwarded copies in his Gmail, newest first " + Google.Untrusted + ":");
            foreach (HeartMail mail in found)
                text.Append("\n- id: ").Append(mail.Id).Append(" | From: ").Append(mail.From ?? "?").Append(" | Subject: ").Append(mail.Subject ?? "(no subject)").Append(" | ").Append(Pursuit.Shorter(mail.Snippet ?? "", SnippetChars));
            if (about.Length > 0 && !loose)
            {
                string full = Read(found[0].Id);
                if (!string.IsNullOrEmpty(full)) text.Append("\nThe best match in full:\n").Append(full);
            }
            text.Append("\nAnswer his question from this. Only if it isn't here, say so and offer the full Outlook check.");
            ActivityLog.Write("study", "school mail from Gmail", found.Count + " found" + (about.Length > 0 ? " for " + Pursuit.Shorter(about, 60) : ""));
            return text.ToString();
        }
    }
}
