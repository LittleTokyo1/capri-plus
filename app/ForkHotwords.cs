using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CapriDesktop
{
    internal static class Hotwords
    {
        internal const int ModelingUnitAt = 136;
        internal const int BpeVocabAt = 144;
        internal const int DecodingAt = 528;
        internal const int PathsAt = 536;
        internal const int FileAt = 544;
        internal const int ScoreAt = 552;
        internal const int Paths = 4;
        internal const float Score = 1.5f;
        internal const string Folder = "capri-hotwords";
        internal static readonly string[] Words = { "Capri", "capri", "Hey Capri" };
        internal static readonly string[] School = { };

        internal static Func<bool> On = delegate { return ForkSettings.Current.EarsHotwords; };

        internal static string Vocab(IList<string> tokenLines)
        {
            StringBuilder vocab = new StringBuilder();
            foreach (string line in tokenLines)
            {
                string trimmed = (line ?? "").TrimEnd('\r');
                int split = trimmed.LastIndexOf(' ');
                if (split <= 0) continue;
                string piece = trimmed.Substring(0, split);
                int id;
                if (!int.TryParse(trimmed.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) continue;
                vocab.Append(piece).Append(' ').Append((-Math.Log(id + 2)).ToString("0.0000", CultureInfo.InvariantCulture)).Append('\n');
            }
            return vocab.ToString();
        }

        internal const int MostPeople = 60;

        internal static Func<List<string>> People = delegate { return Names.Spoken(MostPeople); };

        internal static string List()
        {
            List<string> all = new List<string>(Words);
            all.AddRange(School); all.AddRange(SchoolSites.Names());
            List<string> people;
            try { people = People() ?? new List<string>(); }
            catch (Exception) { people = new List<string>(); }
            foreach (string name in people)
                if (name != null && System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Z][A-Za-z ]{1,28}[A-Za-z]$") && !System.Text.RegularExpressions.Regex.IsMatch(name, @"^My\b|\band\b") && !all.Contains(name)) all.Add(name);
            return string.Join("\n", all.ToArray()) + "\n";
        }

        internal static bool Prepare(string model, out string vocab, out string list)
        {
            vocab = list = null;
            try
            {
                string tokens = Path.Combine(model, "tokens.txt");
                if (!File.Exists(tokens)) return false;
                string folder = Path.Combine(model, Folder);
                Directory.CreateDirectory(folder);
                vocab = Path.Combine(folder, "bpe.vocab");
                list = Path.Combine(folder, "hotwords.txt");
                if (!File.Exists(vocab) || File.GetLastWriteTimeUtc(vocab) < File.GetLastWriteTimeUtc(tokens))
                    File.WriteAllText(vocab, Vocab(File.ReadAllLines(tokens, Encoding.UTF8)), new UTF8Encoding(false));
                File.WriteAllText(list, List(), new UTF8Encoding(false));
                return new FileInfo(vocab).Length > 0;
            }
            catch (Exception error)
            {
                ActivityLog.Write("ears", "hotwords not ready", error.GetType().Name);
                vocab = list = null;
                return false;
            }
        }

        internal static bool Tune(IntPtr config, string model, Func<string, IntPtr> keep)
        {
            if (!On()) return false;
            string vocab, list;
            if (!Prepare(model, out vocab, out list)) return false;
            Marshal.WriteIntPtr(config, ModelingUnitAt, keep("bpe"));
            Marshal.WriteIntPtr(config, BpeVocabAt, keep(vocab));
            Marshal.WriteIntPtr(config, DecodingAt, keep("modified_beam_search"));
            Marshal.WriteInt32(config, PathsAt, Paths);
            Marshal.WriteIntPtr(config, FileAt, keep(list));
            Marshal.WriteInt32(config, ScoreAt, BitConverter.ToInt32(BitConverter.GetBytes(Score), 0));
            return true;
        }
    }

    internal static class EarsEval
    {
        internal static int Live(string clips, string output, string data)
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
                StringBuilder found = new StringBuilder();
                Func<bool> onWas = Hotwords.On;
                foreach (bool boosted in new[] { false, true })
                {
                    Hotwords.On = delegate { return boosted; };
                    using (LocalEars ears = new LocalEars(data))
                    {
                        foreach (string clip in Directory.GetFiles(clips, "*.wav"))
                        {
                            Stopwatch took = Stopwatch.StartNew();
                            string heard = ears.Hear(Samples(clip));
                            found.Append(boosted ? "hot" : "plain").Append('\t').Append(Path.GetFileName(clip)).Append('\t').Append(took.ElapsedMilliseconds).Append('\t').Append(heard).Append('\n');
                        }
                    }
                }
                Hotwords.On = onWas;
                File.WriteAllText(output, found.ToString(), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(output, "failed: " + error.GetType().Name + ": " + error.Message + "\n", new UTF8Encoding(false));
                return 1;
            }
        }

        internal static short[] Samples(string wav)
        {
            byte[] bytes = File.ReadAllBytes(wav);
            int at = 12;
            while (at + 8 <= bytes.Length)
            {
                string id = Encoding.ASCII.GetString(bytes, at, 4);
                int size = BitConverter.ToInt32(bytes, at + 4);
                if (id == "data")
                {
                    int count = Math.Min(size, bytes.Length - at - 8) / 2;
                    short[] samples = new short[count];
                    Buffer.BlockCopy(bytes, at + 8, samples, 0, count * 2);
                    return samples;
                }
                at += 8 + size + (size & 1);
            }
            return new short[0];
        }
    }
}
