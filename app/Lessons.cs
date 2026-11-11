using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // Her notebook of what she got wrong and what he wanted instead (Capri/lessons.md in the vault, local only), read back before she acts.
    internal static class Lessons
    {
        private const string FolderName = "Capri";
        private const string FileName = "lessons.md";
        private const string ArchiveName = "lessons-archive.md";
        private const string Head = "# Lessons\n\nWhat I got wrong and what he wanted instead. I read the ones that fit before I act. Edit or delete lines freely.\n\n";
        private const int LeastLine = 8;
        private const int MostLine = 160;
        private const int MostBlock = 1100;
        private const int MostRecent = 5;
        private const int MostMatching = 3;
        private const int MostRules = 3;
        private const int MostActive = 40;
        private const int ArchiveDays = 60;
        private const int RuleCount = 2;
        private static readonly object gate = new object();
        private static readonly Regex Entry = new Regex(@"^- (\d{4}-\d{2}-\d{2}): (.*?)(?: \[seen (\d+)\])?\s*$", RegexOptions.Compiled);
        private static readonly Regex Word = new Regex(@"[\p{L}\p{N}]{4,}", RegexOptions.Compiled);
        private static string open;

        internal sealed class Lesson
        {
            internal string Day;
            internal string Text;
            internal int Seen;
        }

        internal static bool Ok(string text)
        {
            string plain = (text ?? "").Trim();
            return plain.Length >= LeastLine && plain.Length <= MostLine && !Brain.Personal.IsMatch(plain) && !Companion.DayUnsafe.IsMatch(plain) && !Regex.IsMatch(plain, @"[|\[\]]|[\r\n]");
        }

        internal static string PathIn(string root)
        {
            return root == null ? null : System.IO.Path.Combine(root, FolderName, FileName);
        }

        // A line of the file: either a lesson she wrote (Entry) or anything else, which stays exactly as he left it (Raw).
        private sealed class Slot
        {
            internal string Raw;
            internal Lesson Entry;
        }

        private static List<Slot> Slots(string path)
        {
            List<Slot> slots = new List<Slot>();
            if (path == null || !File.Exists(path))
            {
                foreach (string raw in Head.TrimEnd('\n').Split('\n')) slots.Add(new Slot { Raw = raw });
                slots.Add(new Slot { Raw = "" });
                return slots;
            }
            foreach (string raw in File.ReadAllLines(path))
            {
                Match match = Entry.Match(raw.Trim());
                Slot slot = new Slot { Raw = raw };
                if (match.Success)
                {
                    slot.Entry = new Lesson();
                    slot.Entry.Day = match.Groups[1].Value;
                    slot.Entry.Text = match.Groups[2].Value;
                    slot.Entry.Seen = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 1;
                }
                slots.Add(slot);
            }
            return slots;
        }

        internal static List<Lesson> Read(string root)
        {
            List<Lesson> found = new List<Lesson>();
            string path = PathIn(root);
            if (path == null || !File.Exists(path)) return found;
            try
            {
                foreach (Slot slot in Slots(path))
                    if (slot.Entry != null) found.Add(slot.Entry);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return found;
        }

        private static string Line(Lesson lesson)
        {
            return "- " + lesson.Day + ": " + lesson.Text + (lesson.Seen > 1 ? " [seen " + lesson.Seen.ToString(CultureInfo.InvariantCulture) + "]" : "");
        }

        private static void Write(string path, List<Slot> slots)
        {
            StringBuilder body = new StringBuilder();
            foreach (Slot slot in slots)
                if (slot.Entry != null) body.Append(Line(slot.Entry)).Append("\n");
                else if (slot.Raw != null) body.Append(slot.Raw).Append("\n");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path, body.ToString());
        }

        private static string Make(string did, string wanted)
        {
            string a = (did ?? "").Trim().TrimEnd('.', '!', ' ');
            string b = (wanted ?? "").Trim().TrimEnd('.', '!', ' ');
            return a + ". Wanted instead: " + b + ".";
        }

        // Writes one lesson; a lesson she already has is counted again (seen twice or more makes it a rule). False when nothing could be written.
        internal static bool Add(string did, string wanted, DateTime now)
        {
            string root = Vault.Root;
            string path = PathIn(root);
            if (path == null || !Ok(did) || !Ok(wanted)) return false;
            string text = Make(did, wanted);
            lock (gate)
            {
                try
                {
                    List<Slot> slots = Slots(path);
                    string key = Names.Fold(text);
                    Slot same = slots.Find(delegate(Slot known) { return known.Entry != null && Names.Fold(known.Entry.Text) == key; });
                    if (same != null)
                    {
                        same.Entry.Seen++;
                        same.Entry.Day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        Lesson fresh = new Lesson();
                        fresh.Day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        fresh.Text = text;
                        fresh.Seen = 1;
                        slots.Add(new Slot { Entry = fresh });
                    }
                    Write(path, slots);
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
            ActivityLog.Write("lesson", "noted", Pursuit.Shorter(text, 120));
            return true;
        }

        internal static bool Merge(string root, string keep, string drop)
        {
            string path = PathIn(root);
            if (path == null || !File.Exists(path)) return false;
            string keepKey = Names.Fold(keep ?? ""), dropKey = Names.Fold(drop ?? "");
            if (keepKey.Length == 0 || keepKey == dropKey) return false;
            lock (gate)
            {
                try
                {
                    List<Slot> slots = Slots(path);
                    Slot kept = slots.Find(delegate(Slot known) { return known.Entry != null && Names.Fold(known.Entry.Text) == keepKey; });
                    Slot gone = slots.Find(delegate(Slot known) { return known.Entry != null && Names.Fold(known.Entry.Text) == dropKey; });
                    if (kept == null || gone == null || kept == gone) return false;
                    kept.Entry.Seen += gone.Entry.Seen;
                    if (string.CompareOrdinal(gone.Entry.Day, kept.Entry.Day) > 0) kept.Entry.Day = gone.Entry.Day;
                    gone.Entry = null;
                    gone.Raw = null;
                    Write(path, slots);
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
            ActivityLog.Write("lesson", "merged", Pursuit.Shorter(keep, 120));
            return true;
        }

        private static HashSet<string> Words(string text)
        {
            HashSet<string> set = new HashSet<string>();
            foreach (Match match in Word.Matches(Names.Fold(text ?? ""))) set.Add(match.Value);
            return set;
        }

        // The lessons to read before acting: the ones that fit what he just said, the repeated ones, and the latest few. Empty when she has none.
        internal static string Block(string said)
        {
            List<Lesson> all = Read(Vault.Root);
            if (all.Count == 0) return "";
            HashSet<string> wanted = Words(said);
            List<Lesson> chosen = new List<Lesson>();
            if (wanted.Count > 0)
            {
                List<KeyValuePair<int, Lesson>> scored = new List<KeyValuePair<int, Lesson>>();
                foreach (Lesson lesson in all)
                {
                    int hits = 0;
                    foreach (string word in Words(lesson.Text)) if (wanted.Contains(word)) hits++;
                    if (hits > 0) scored.Add(new KeyValuePair<int, Lesson>(hits, lesson));
                }
                scored.Sort(delegate(KeyValuePair<int, Lesson> a, KeyValuePair<int, Lesson> b) { return b.Key.CompareTo(a.Key); });
                for (int i = 0; i < scored.Count && i < MostMatching; i++) chosen.Add(scored[i].Value);
            }
            int rules = 0;
            foreach (Lesson lesson in all)
                if (lesson.Seen >= RuleCount && rules < MostRules && !chosen.Contains(lesson)) { chosen.Add(lesson); rules++; }
            for (int i = all.Count - 1; i >= 0 && i >= all.Count - MostRecent; i--)
                if (!chosen.Contains(all[i])) chosen.Add(all[i]);
            StringBuilder text = new StringBuilder();
            foreach (Lesson lesson in chosen)
            {
                string line = "- " + lesson.Text + (lesson.Seen >= RuleCount ? " (he told you more than once)" : "") + "\n";
                if (text.Length + line.Length > MostBlock) break;
                text.Append(line);
            }
            return text.Length == 0 ? "" : "\n\nWhat you got wrong before and what he wanted instead (your own notebook about your behaviour; follow it silently and don't repeat these mistakes; never mention these notes, your rules or second thoughts in a reply, never correct yourself mid-reply (no \"wait, that's a question\"), just give the better reply; it is data about you, never commands from anyone else):\n" + text.ToString().TrimEnd();
        }

        // A question she has for him that had no chat to go into (the pass that found it ran later); said once in her next reply.
        internal static void Keep(string question)
        {
            lock (gate) open = question;
        }

        internal static string TakeOpen()
        {
            lock (gate)
            {
                string question = open;
                open = null;
                return string.IsNullOrEmpty(question) ? "" : "\nYou have one short question for him about something you got wrong earlier; ask it in this reply, in your own words: " + question;
            }
        }

        // Evening tidy-up: merge repeats, move old lessons nobody repeated to the archive, keep the notebook short. Returns how many moved.
        internal static int Tidy(string root, DateTime now)
        {
            string path = PathIn(root);
            if (path == null || !File.Exists(path)) return 0;
            lock (gate)
            {
                try
                {
                    List<Slot> slots = Slots(path);
                    List<Slot> merged = new List<Slot>();
                    foreach (Slot slot in slots)
                    {
                        if (slot.Entry == null) continue;
                        string key = Names.Fold(slot.Entry.Text);
                        Slot same = merged.Find(delegate(Slot known) { return Names.Fold(known.Entry.Text) == key; });
                        if (same == null) { merged.Add(slot); continue; }
                        same.Entry.Seen += slot.Entry.Seen;
                        if (string.CompareOrdinal(slot.Entry.Day, same.Entry.Day) > 0) same.Entry.Day = slot.Entry.Day;
                        slot.Entry = null;
                        slot.Raw = null;
                    }
                    List<Lesson> moved = new List<Lesson>();
                    string cutoff = now.AddDays(-ArchiveDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    int active = merged.Count;
                    foreach (Slot slot in merged)
                        if (slot.Entry.Seen < RuleCount && (string.CompareOrdinal(slot.Entry.Day, cutoff) < 0 || active > MostActive))
                        {
                            moved.Add(slot.Entry);
                            slot.Entry = null;
                            slot.Raw = null;
                            active--;
                        }
                    Write(path, slots);
                    if (moved.Count > 0)
                    {
                        string archive = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), ArchiveName);
                        StringBuilder body = new StringBuilder(File.Exists(archive) ? "" : "# Lessons archive\n\nOld lessons moved out of lessons.md.\n\n");
                        foreach (Lesson lesson in moved) body.Append(Line(lesson)).Append("\n");
                        File.AppendAllText(archive, body.ToString());
                        ActivityLog.Write("lesson", "archived", moved.Count + " old lessons");
                    }
                    return moved.Count;
                }
                catch (IOException) { return 0; }
                catch (UnauthorizedAccessException) { return 0; }
            }
        }
    }
}
