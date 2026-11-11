using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class NightEval
    {
        private static int Count(string body, string pattern)
        {
            return Regex.Matches(NoteEdit.Current(body) ?? "", pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline).Count;
        }

        private static int Check(StringBuilder lines, string id, string what, string wrong)
        {
            lines.Append(wrong == null ? "PASS night review live (" + id + ") " + what : "FAIL night review live (" + id + ") " + what + ": " + wrong).Append("\n");
            return wrong == null ? 0 : 1;
        }

        internal static int Live(string folder, Cloud remote, StringBuilder lines)
        {
            string root = Path.Combine(folder, "night-review");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (!Vault.Open(root, null)) return Check(lines, "setup", "the vault opens", "it did not");
            string core = Path.Combine(root, Vault.CoreName);
            Brain.MemoryPath = core;
            Names.Reset();
            DateTime now = DateTime.Now.Date.AddHours(22).AddMinutes(5);
            string stamp = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            File.WriteAllText(core, Brain.NotesHead + "- Plays drums on Sundays\n- Plays football on Sundays\n- Their sister lives in Kaunas\n- Their mum lives in Kaunas\n- Has a cat called Mochi\n- Owns a cat named Mochi\n");
            string dom = Path.Combine(root, "People", "Rudiminas.md");
            File.WriteAllText(dom, "# Rudiminas\n- Is: friend\n- Rents a flat in Kaunas ([[Journal/2026-09-01|1 Sep]])\n- Rents a flat in Vilnius now ([[Journal/2026-10-20|20 Oct]])\n");
            File.WriteAllText(Lessons.PathIn(root), "# Lessons\n\n- 2026-10-28: Said a reminder was set when none was. Wanted instead: Only say it is set after the tool ran.\n- 2026-10-29: Claimed the reminder existed without setting it. Wanted instead: Set it first, then confirm.\n- 2026-10-29: Opened the wrong monitor. Wanted instead: Use the monitor in front.\n");
            string lina = Path.Combine(root, "People", "Lina.md");
            File.WriteAllText(lina, "# Lina\n- Is: friend\n- Plays guitar in a band on Fridays ([[Journal/2026-09-01|1 Sep]])\n- Plays football with friends on Fridays ([[Journal/2026-10-20|20 Oct]])\n");
            Directory.CreateDirectory(ChatArchive.FolderIn(root));
            File.WriteAllText(Path.Combine(ChatArchive.FolderIn(root), stamp + ".md"), "# " + stamp + "\n\n18:00 him: oh and i play the drums every sunday btw\n18:00 her: Nice, how long have you played?\n18:02 him: I started learning Spanish on Duolingo this week\n18:02 her: Que bien! Good luck with it.\n");
            Func<bool> keptStop = NightReview.Stop;
            NightReview.Stop = delegate { return false; };
            string summary;
            try { summary = NightReview.Run(now, remote, root, core); }
            finally { NightReview.Stop = keptStop; }
            string me = File.ReadAllText(core);
            string domBody = File.ReadAllText(dom);
            int failed = 0;
            failed += Check(lines, "a", "a real contradiction moves the older line to History", Count(domBody, "Kaunas") == 0 && Count(domBody, "Vilnius") == 1 && domBody.Contains("## History") ? null : "Rudiminas.md is now: " + Flat(domBody));
            string linaBody = File.ReadAllText(lina);
            failed += Check(lines, "f", "two dated true lines about different things are both kept (guitar and football on Fridays)", Count(linaBody, "guitar") == 1 && Count(linaBody, "football") == 1 ? null : "Lina.md is now: " + Flat(linaBody));
            failed += Check(lines, "b", "two true lines that share words are both kept (drums and football, sister and mum)", Count(me, @"^- Plays drums") == 1 && Count(me, @"^- Plays football") == 1 && Count(me, @"^- Their sister lives") == 1 && Count(me, @"^- Their mum lives") == 1 ? null : "me.md is now: " + Flat(me));
            failed += Check(lines, "c", "a duplicate leaves one line", Count(me, @"^- .*Mochi") == 1 ? null : "me.md is now: " + Flat(me));
            failed += Check(lines, "d", "a missed fact is saved and a known one is not saved twice", Count(me, "Spanish") == 1 && Count(me, "drum") == 1 ? null : "me.md is now: " + Flat(me));
            List<Lessons.Lesson> kept = Lessons.Read(root);
            failed += Check(lines, "e", "two lessons about the same mistake merge, a different one stays", kept.Count == 2 && kept.Exists(delegate(Lessons.Lesson lesson) { return lesson.Text.Contains("monitor"); }) ? null : kept.Count + " lessons left");
            lines.Append("night review live result: " + summary + "\n");
            string changes = Path.Combine(root, "Capri", "memory-changes.md");
            if (File.Exists(changes))
                foreach (string line in File.ReadAllLines(changes))
                    if (line.StartsWith("- ", StringComparison.Ordinal)) lines.Append("  change ").Append(line).Append("\n");
            return failed;
        }

        private static string Flat(string body)
        {
            string flat = body.Replace("\r", "").Replace("\n", " / ");
            return flat.Length > 700 ? flat.Substring(0, 700) + "..." : flat;
        }
    }
}
