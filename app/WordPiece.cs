using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    internal sealed class WordPiece
    {
        internal const int Most = 256;
        private const int LongestWord = 100;
        private readonly Dictionary<string, int> vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly int unknown, first, last;

        internal WordPiece(IEnumerable<string> words)
        {
            int id = 0;
            foreach (string word in words)
            {
                string plain = (word ?? "").TrimEnd('\r');
                if (!vocab.ContainsKey(plain)) vocab[plain] = id;
                id++;
            }
            unknown = Find("[UNK]");
            first = Find("[CLS]");
            last = Find("[SEP]");
        }

        internal static WordPiece Load(string path)
        {
            return new WordPiece(File.ReadAllLines(path, Encoding.UTF8));
        }

        private int Find(string word)
        {
            int id;
            if (!vocab.TryGetValue(word, out id)) throw new InvalidDataException("The word list has no " + word + ".");
            return id;
        }

        internal long[] Ids(string text)
        {
            List<long> ids = new List<long> { first };
            foreach (string piece in Pieces(text))
            {
                if (ids.Count >= Most - 1) break;
                int id;
                ids.Add(vocab.TryGetValue(piece, out id) ? id : unknown);
            }
            ids.Add(last);
            return ids.ToArray();
        }

        internal List<string> Pieces(string text)
        {
            List<string> found = new List<string>();
            foreach (string word in Words(text)) Split(word, found);
            return found;
        }

        private void Split(string word, List<string> found)
        {
            if (word.Length > LongestWord) { found.Add("[UNK]"); return; }
            List<string> parts = new List<string>();
            int start = 0;
            while (start < word.Length)
            {
                string match = null;
                for (int end = word.Length; end > start; end--)
                {
                    string part = (start > 0 ? "##" : "") + word.Substring(start, end - start);
                    if (!vocab.ContainsKey(part)) continue;
                    match = part;
                    start = end;
                    break;
                }
                if (match == null) { found.Add("[UNK]"); return; }
                parts.Add(match);
            }
            found.AddRange(parts);
        }

        private static List<string> Words(string text)
        {
            StringBuilder clean = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (c == 0 || c == '�' || Control(c)) continue;
                if (Space(c)) clean.Append(' ');
                else if (Chinese(c)) clean.Append(' ').Append(c).Append(' ');
                else clean.Append(c);
            }
            List<string> words = new List<string>();
            foreach (string token in clean.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                StringBuilder plain = new StringBuilder();
                foreach (char c in token.ToLowerInvariant().Normalize(NormalizationForm.FormD))
                    if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) plain.Append(c);
                StringBuilder word = new StringBuilder();
                foreach (char c in plain.ToString())
                {
                    if (!Punctuation(c)) { word.Append(c); continue; }
                    if (word.Length > 0) { words.Add(word.ToString()); word.Length = 0; }
                    words.Add(c.ToString());
                }
                if (word.Length > 0) words.Add(word.ToString());
            }
            return words;
        }

        private static bool Space(char c)
        {
            return c == ' ' || c == '\t' || c == '\n' || c == '\r' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;
        }

        private static bool Control(char c)
        {
            if (c == '\t' || c == '\n' || c == '\r') return false;
            UnicodeCategory kind = CharUnicodeInfo.GetUnicodeCategory(c);
            return kind == UnicodeCategory.Control || kind == UnicodeCategory.Format;
        }

        private static bool Punctuation(char c)
        {
            if ((c >= 33 && c <= 47) || (c >= 58 && c <= 64) || (c >= 91 && c <= 96) || (c >= 123 && c <= 126)) return true;
            switch (CharUnicodeInfo.GetUnicodeCategory(c))
            {
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.OtherPunctuation:
                    return true;
            }
            return false;
        }

        private static bool Chinese(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0xF900 && c <= 0xFAFF);
        }
    }
}
