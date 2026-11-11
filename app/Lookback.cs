using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class LookHit
    {
        internal string Path;
        internal string Rel;
        internal string Text;
        internal string Answer;
        internal DateTime When;
        internal bool Chat;
        internal double Score;
        internal int Matched;
        internal double Meaning;
        internal double Fused;
    }

    internal static class Lookback
    {
        private const double K1 = 1.2;
        private const double B = 0.75;
        private const double Fusion = 60;
        private const int MostLine = 400;
        private const int MostStems = 50000;
        private const long MostChatBytes = 4000000;
        private static readonly Regex Word = new Regex(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
        private static readonly Regex Said = new Regex(@"^(\d{2}):(\d{2}) (him|her): (.*)$", RegexOptions.Compiled);
        private static readonly Regex Bullet = new Regex(@"^[-*+]\s+(\[.\]\s+)?", RegexOptions.Compiled);
        private static readonly HashSet<string> Stop = new HashSet<string>(new[]
        {
            "the", "and", "but", "for", "with", "about", "from", "that", "this", "those", "these", "there", "then", "than", "what", "whats", "when", "where", "which", "who", "whom", "why", "how",
            "was", "were", "are", "is", "am", "be", "been", "being", "did", "does", "do", "done", "have", "has", "had", "will", "would", "could", "should", "can", "may", "might", "must",
            "not", "no", "yes", "you", "your", "yours", "me", "my", "mine", "we", "our", "us", "he", "him", "his", "she", "her", "they", "them", "their", "it", "its", "an", "to", "of", "in", "on",
            "at", "by", "or", "as", "so", "if", "into", "just", "also", "too", "very", "really", "some", "any", "all", "one", "mentioned", "mention", "said", "say", "says", "told", "tell",
            "remember", "remembered", "called", "know", "like", "thing", "things", "stuff", "again", "ever", "before", "last", "time", "back", "oh", "btw", "yeah", "ok", "okay", "lol",
            "hey", "capri", "choom", "im", "don", "didn", "isn", "wasn", "doesn", "got", "get", "talk", "talked", "chat", "chats"
        });
        private static readonly string[] Filler = { "talk", "say", "said", "tell", "told", "mention", "remember", "remind", "recall", "chat", "thing", "like", "know", "ask", "wonder" };
        private static readonly HashSet<string> FillerStems = new HashSet<string>(Array.ConvertAll(Filler, Porter.Stem));
        private static readonly object gate = new object();
        private static readonly Dictionary<string, Known> cache = new Dictionary<string, Known>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> stems = new Dictionary<string, string>();
        private static int pieces;

        private sealed class Piece
        {
            internal string Path;
            internal string Rel;
            internal string Text;
            internal string Raw;
            internal string Answer;
            internal string Meant;
            internal string Key;
            internal DateTime When;
            internal bool Chat;
            internal Dictionary<string, int> Counts;
            internal int Length;
        }

        private sealed class Known
        {
            internal DateTime Stamp;
            internal long Size;
            internal List<Piece> Pieces;
        }

        internal static int Pieces { get { lock (gate) return pieces; } }

        internal static void Reset()
        {
            lock (gate)
            {
                cache.Clear();
                pieces = 0;
            }
        }

        internal static List<string> Tokens(string text)
        {
            List<string> found = new List<string>();
            foreach (Match word in Word.Matches(Names.Fold(text ?? "")))
            {
                string plain = word.Value;
                if (plain.Length < 2 || Stop.Contains(plain)) continue;
                string stem = StemOf(plain);
                if (!FillerStems.Contains(stem)) found.Add(stem);
            }
            return found;
        }

        private static string StemOf(string plain)
        {
            lock (stems)
            {
                string known;
                if (stems.TryGetValue(plain, out known)) return known;
                if (stems.Count > MostStems) stems.Clear();
                string stem = Porter.Stem(plain);
                stems[plain] = stem;
                return stem;
            }
        }

        internal static List<LookHit> Find(string root, string query, DateTime? from, DateTime? to, bool notes, bool chats, int most)
        {
            List<string> asked = Asked(query);
            List<LookHit> hits = Score(root, asked, from, to, asked.Count > 0 ? Meaning.Query(query) : null);
            hits.RemoveAll(delegate(LookHit hit) { return hit.Chat ? !chats : !notes; });
            if (asked.Count == 0) hits.Sort(delegate(LookHit a, LookHit b) { return a.When.CompareTo(b.When); });
            if (hits.Count > most) hits.RemoveRange(most, hits.Count - most);
            return hits;
        }

        internal static List<string> Asked(string query)
        {
            List<string> asked = new List<string>();
            foreach (string term in Tokens(query))
                if (!asked.Contains(term)) asked.Add(term);
            return asked;
        }

        internal static List<KeyValuePair<string, string>> Meant(string root)
        {
            List<KeyValuePair<string, string>> found = new List<KeyValuePair<string, string>>();
            if (root == null) return found;
            foreach (Piece piece in Gather(root)) found.Add(new KeyValuePair<string, string>(piece.Key, piece.Meant));
            return found;
        }

        internal static List<LookHit> Score(string root, List<string> asked, DateTime? from, DateTime? to, float[] meaning)
        {
            List<LookHit> hits = new List<LookHit>();
            bool dated = from.HasValue || to.HasValue;
            if (root == null || (asked.Count == 0 && !dated)) return hits;
            List<Piece> all = Gather(root);
            if (all.Count == 0) return hits;
            double total = 0;
            foreach (Piece piece in all) total += piece.Length;
            double average = Math.Max(1, total / all.Count);
            Dictionary<string, int> spread = new Dictionary<string, int>();
            foreach (string term in asked)
            {
                int count = 0;
                foreach (Piece piece in all) if (piece.Counts.ContainsKey(term)) count++;
                spread[term] = count;
            }
            Dictionary<Piece, double> close = Close(all, meaning, from, to, dated);
            foreach (Piece piece in all)
            {
                if (Outside(piece, from, to, dated)) continue;
                double score = 0;
                int matched = 0;
                foreach (string term in asked)
                {
                    int count;
                    if (!piece.Counts.TryGetValue(term, out count)) continue;
                    matched++;
                    double rare = Math.Log(1 + (all.Count - spread[term] + 0.5) / (spread[term] + 0.5));
                    score += rare * count * (K1 + 1) / (count + K1 * (1 - B + B * piece.Length / average));
                }
                double near;
                bool meant = close.TryGetValue(piece, out near);
                if (asked.Count > 0 && matched == 0 && !meant) continue;
                if (piece.Chat && ChatArchive.InWindow(piece.Raw)) continue;
                hits.Add(new LookHit { Path = piece.Path, Rel = piece.Rel, Text = piece.Text, Answer = piece.Answer, When = piece.When, Chat = piece.Chat, Score = score, Matched = matched, Meaning = meant ? near : 0 });
            }
            hits.Sort(delegate(LookHit a, LookHit b) { return a.Score != b.Score ? b.Score.CompareTo(a.Score) : b.When.CompareTo(a.When); });
            if (close.Count > 0) Fuse(hits);
            return hits;
        }

        private static bool Outside(Piece piece, DateTime? from, DateTime? to, bool dated)
        {
            return dated && (!piece.Chat || (from.HasValue && piece.When < from.Value) || (to.HasValue && piece.When >= to.Value));
        }

        private static Dictionary<Piece, double> Close(List<Piece> all, float[] meaning, DateTime? from, DateTime? to, bool dated)
        {
            Dictionary<Piece, double> close = new Dictionary<Piece, double>();
            if (meaning == null) return close;
            Dictionary<string, float[]> known = Meaning.Snapshot();
            if (known.Count == 0) return close;
            List<KeyValuePair<double, Piece>> notes = new List<KeyValuePair<double, Piece>>();
            List<KeyValuePair<double, Piece>> chats = new List<KeyValuePair<double, Piece>>();
            foreach (Piece piece in all)
            {
                float[] vector;
                if (Outside(piece, from, to, dated) || (piece.Chat && ChatArchive.InWindow(piece.Raw)) || !known.TryGetValue(piece.Key, out vector)) continue;
                (piece.Chat ? chats : notes).Add(new KeyValuePair<double, Piece>(Meaning.Dot(meaning, vector), piece));
            }
            Standout(notes, close);
            Standout(chats, close);
            return close;
        }

        private static void Standout(List<KeyValuePair<double, Piece>> near, Dictionary<Piece, double> close)
        {
            near.Sort(delegate(KeyValuePair<double, Piece> a, KeyValuePair<double, Piece> b) { return b.Key.CompareTo(a.Key); });
            double bar = Meaning.Bar(near.ConvertAll(delegate(KeyValuePair<double, Piece> pair) { return pair.Key; }));
            for (int i = 0; i < near.Count && i < Meaning.MostMeant && near[i].Key >= bar; i++) close[near[i].Value] = near[i].Key;
        }

        private static void Fuse(List<LookHit> hits)
        {
            int rank = 0;
            foreach (LookHit hit in hits) if (hit.Matched > 0) hit.Fused += 1.0 / (Fusion + ++rank);
            List<LookHit> meant = hits.FindAll(delegate(LookHit hit) { return hit.Meaning > 0; });
            meant.Sort(delegate(LookHit a, LookHit b) { return b.Meaning.CompareTo(a.Meaning); });
            rank = 0;
            foreach (LookHit hit in meant) hit.Fused += 1.0 / (Fusion + ++rank);
            Dictionary<LookHit, int> order = new Dictionary<LookHit, int>();
            for (int i = 0; i < hits.Count; i++) order[hits[i]] = i;
            hits.Sort(delegate(LookHit a, LookHit b) { return a.Fused != b.Fused ? b.Fused.CompareTo(a.Fused) : order[a].CompareTo(order[b]); });
        }

        internal static string Line(LookHit hit)
        {
            if (!hit.Chat) return hit.Rel + ": " + hit.Text;
            return "old chat, " + hit.When.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture) + ", " + hit.Text + (hit.Answer == null ? "" : " / her: " + hit.Answer);
        }

        private static List<string> Sources(string root)
        {
            List<string> found = Vault.Files(root);
            string core = Path.Combine(root, Vault.CoreName);
            if (File.Exists(core)) found.Add(core);
            string chats = ChatArchive.FolderIn(root);
            try
            {
                if (Directory.Exists(chats))
                    foreach (string path in Directory.GetFiles(chats, "*.md"))
                        if (!Vault.Conflict(path)) found.Add(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return found;
        }

        private static List<Piece> Gather(string root)
        {
            List<string> paths = Sources(root);
            List<Piece> all = new List<Piece>();
            lock (gate)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string path in paths)
                {
                    seen.Add(path);
                    List<Piece> got = Load(root, path);
                    if (got != null) all.AddRange(got);
                }
                if (cache.Count > seen.Count)
                    foreach (string path in new List<string>(cache.Keys))
                        if (!seen.Contains(path)) cache.Remove(path);
                pieces = all.Count;
            }
            return all;
        }

        private static List<Piece> Load(string root, string path)
        {
            FileInfo info;
            try
            {
                info = new FileInfo(path);
                if (!info.Exists) return null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            Known known;
            if (cache.TryGetValue(path, out known) && known.Stamp == info.LastWriteTimeUtc && known.Size == info.Length) return known.Pieces;
            string rel = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
            List<Piece> made = rel.StartsWith("Capri/chats/", StringComparison.OrdinalIgnoreCase) ? Chat(path, rel, info.Length) : Note(path, rel);
            cache[path] = new Known { Stamp = info.LastWriteTimeUtc, Size = info.Length, Pieces = made };
            return made;
        }

        private static List<Piece> Note(string path, string rel)
        {
            List<Piece> made = new List<Piece>();
            string body = Vault.Text(path);
            if (body == null) return made;
            string name = Path.GetFileNameWithoutExtension(path);
            List<string> title = Tokens(name);
            foreach (string line in body.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal) || Brain.Personal.IsMatch(trimmed)) continue;
                string text = Bullet.Replace(trimmed, "").Trim();
                if (text.Length == 0) continue;
                List<string> words = Tokens(text);
                words.AddRange(title);
                made.Add(Make(path, rel, Clip(text), null, null, name + ": " + Clip(text), DateTime.MinValue, false, words));
            }
            return made;
        }

        private static List<Piece> Chat(string path, string rel, long size)
        {
            List<Piece> made = new List<Piece>();
            DateTime day;
            if (size > MostChatBytes || !DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return made;
            string[] lines;
            try { lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'); }
            catch (IOException) { return made; }
            catch (UnauthorizedAccessException) { return made; }
            for (int i = 0; i < lines.Length; i++)
            {
                Match said = Said.Match(lines[i].TrimEnd());
                if (!said.Success) continue;
                string raw = said.Groups[4].Value.Trim();
                if (raw.Length == 0 || raw.StartsWith("(", StringComparison.Ordinal)) continue;
                string who = said.Groups[3].Value;
                DateTime when = day.AddHours(int.Parse(said.Groups[1].Value, CultureInfo.InvariantCulture)).AddMinutes(int.Parse(said.Groups[2].Value, CultureInfo.InvariantCulture));
                string answer = null;
                if (who == "him" && i + 1 < lines.Length)
                {
                    Match next = Said.Match(lines[i + 1].TrimEnd());
                    if (next.Success && next.Groups[3].Value == "her" && !next.Groups[4].Value.StartsWith("(", StringComparison.Ordinal)) answer = Clip(next.Groups[4].Value.Trim());
                }
                made.Add(Make(path, rel, who + ": " + Clip(raw), raw, answer, who + ": " + Clip(raw), when, true, Tokens(raw)));
            }
            return made;
        }

        private static Piece Make(string path, string rel, string text, string raw, string answer, string meant, DateTime when, bool chat, List<string> words)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (string word in words)
            {
                int count;
                counts.TryGetValue(word, out count);
                counts[word] = count + 1;
            }
            return new Piece { Path = path, Rel = rel, Text = text, Raw = raw, Answer = answer, Meant = meant, Key = Meaning.Key(meant), When = when, Chat = chat, Counts = counts, Length = Math.Max(1, words.Count) };
        }

        private static string Clip(string text)
        {
            return text.Length > MostLine ? text.Substring(0, MostLine) + "..." : text;
        }
    }
}
