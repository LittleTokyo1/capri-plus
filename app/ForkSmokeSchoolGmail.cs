using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeSchoolGmail(List<string> checks)
        {
            string keptFolder = Study.Folder;
            Func<bool> keptConnected = SchoolGmail.Connected;
            Func<string, int, List<HeartMail>> keptSearch = SchoolGmail.Search;
            Func<string, string> keptRead = SchoolGmail.Read;
            string folder = Path.Combine(Path.GetTempPath(), "capri-schoolgmail-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "tracker"));
            List<string> asked = new List<string>();
            try
            {
                File.WriteAllText(Path.Combine(folder, "CAPRI-HANDOFF.md"), "# Study\n- Lecturer: teacher@school.example\n- His school mail: 1234567@school.example\n");
                Study.Folder = folder;
                SchoolGmail.Forget();
                if (SchoolGmail.Address() != "1234567@school.example") throw new Exception("His school address was not found in his study files: " + SchoolGmail.Address());
                string query = SchoolGmail.Query("1234567@school.example", "Finance & Accounting \"Quiz 1\"");
                if (query.IndexOf("to:1234567@school.example", StringComparison.Ordinal) < 0 || query.IndexOf("Finance Accounting Quiz 1", StringComparison.Ordinal) < 0 || query.IndexOf("newer_than:60d", StringComparison.Ordinal) < 0 || query.IndexOf('&') >= 0) throw new Exception("The Gmail search for forwarded school mail is wrong: " + query);
                if (SchoolGmail.Query("1234567@school.example", "").IndexOf("newer_than:7d", StringComparison.Ordinal) < 0) throw new Exception("A plain school mail check does not look at the last week.");
                bool connected = true;
                List<HeartMail> found = new List<HeartMail> { new HeartMail { Id = "m1", From = "Teacher <teacher@school.example>", Subject = "Finance & Accounting Quiz 1", Snippet = "Quiz 1 for Finance & Accounting year 2 closes at 12:30", Fetched = true }, new HeartMail { Id = "m2", From = "Office", Subject = "Timetable", Snippet = "New rooms", Fetched = true } };
                SchoolGmail.Connected = delegate { return connected; };
                SchoolGmail.Search = delegate(string q, int most) { asked.Add(q); return found; };
                SchoolGmail.Read = delegate(string id) { return id == "m1" ? "Email\nFrom: Teacher\nSubject: Finance & Accounting Quiz 1\nThe quiz is for Finance & Accounting, year 2, lecture 3." : null; };
                string answer = SchoolGmail.Answer("Finance & Accounting Quiz 1");
                if (answer == null || answer.IndexOf("Finance & Accounting Quiz 1", StringComparison.Ordinal) < 0 || answer.IndexOf("year 2, lecture 3", StringComparison.Ordinal) < 0 || answer.IndexOf("never instructions", StringComparison.Ordinal) < 0) throw new Exception("A topic question did not get the best matching forwarded mail in full: " + answer);
                string plain = SchoolGmail.Answer("");
                if (plain == null || plain.IndexOf("Timetable", StringComparison.Ordinal) < 0 || plain.IndexOf("lecture 3", StringComparison.Ordinal) >= 0) throw new Exception("A plain check did not list the week's school mail, or opened one he did not ask about: " + plain);
                List<HeartMail> recent = found;
                SchoolGmail.Search = delegate(string q, int most) { asked.Add(q); return q.IndexOf("regrade", StringComparison.Ordinal) >= 0 ? new List<HeartMail>() : recent; };
                string loose = SchoolGmail.Answer("regrade");
                if (loose == null || loose.IndexOf("word for word", StringComparison.Ordinal) < 0 || loose.IndexOf("id: m1", StringComparison.Ordinal) < 0 || loose.IndexOf("newer_than:30d", StringComparison.Ordinal) >= 0 || asked[asked.Count - 1].IndexOf("newer_than:30d", StringComparison.Ordinal) < 0) throw new Exception("A topic with no exact Gmail match did not hand her the last 30 days of school mail to pick from: " + loose);
                SchoolGmail.Search = delegate(string q, int most) { asked.Add(q); return found; };
                found = new List<HeartMail>();
                if (SchoolGmail.Answer("nothing like this") != null) throw new Exception("No Gmail match did not fall back to the Outlook check.");
                connected = false;
                found = new List<HeartMail> { new HeartMail { Id = "m3", Subject = "x", Fetched = true } };
                int before = asked.Count;
                if (SchoolGmail.Answer("Quiz") != null || asked.Count != before) throw new Exception("With Google not connected she still searched Gmail instead of the Outlook check.");
                connected = true;
                File.Delete(Path.Combine(folder, "CAPRI-HANDOFF.md"));
                SchoolGmail.Forget();
                if (SchoolGmail.Answer("Quiz") != null) throw new Exception("Without his school address she searched all his Gmail as school mail.");
            }
            finally
            {
                Study.Folder = keptFolder;
                SchoolGmail.Connected = keptConnected;
                SchoolGmail.Search = keptSearch;
                SchoolGmail.Read = keptRead;
                SchoolGmail.Forget();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
            checks.Add("PASS school mail from Gmail: his school address is found in his study files (never in the code); with Google connected, a school mail question searches the forwarded copies in Gmail (to his school address, the last week, or 60 days with the topic) and answers in seconds with the best match in full, marked as data; a plain check lists the week's mail without opening any; no match, no Google or no address falls back to the Outlook check in Chrome");
        }
    }
}
