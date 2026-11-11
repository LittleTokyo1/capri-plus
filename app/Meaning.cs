using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CapriDesktop
{
    internal static class Meaning
    {
        internal const string Prefix = "Represent this sentence for searching relevant passages: ";
        internal const int QuietMinutes = 10;
        internal const int Threads = 2;
        internal const int MostMeant = 3;
        internal const double Floor = 0.53;
        internal const double Gap = 0.10;
        private const int Crowd = 5;
        private const int RetryMinutes = 30;
        private const long LogEvery = 50L << 20;
        private const int Magic = 0x31564D43;
        private const string ModelUrl = "https://huggingface.co/BAAI/bge-base-en-v1.5/resolve/main/onnx/model.onnx";
        private const long ModelSize = 435811539;
        private const string ModelSha = "9bc579acdba21c253c62a9bf866891355a63ffa3442b52c8a37d75b2ccb91848";
        private const string VocabUrl = "https://huggingface.co/BAAI/bge-base-en-v1.5/resolve/main/vocab.txt";
        private const long VocabSize = 231508;
        private const string VocabSha = "07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3";
        private static readonly object gate = new object();
        private static Embed loaded;
        private static DateTime used;
        private static DateTime failedAt = DateTime.MinValue;
        private static Dictionary<string, float[]> vectors;

        internal static string Runtime;
        internal static string Model;
        internal static string Store;
        internal static Func<bool> Gaming;
        internal static bool On = true;

        internal static void Use(string data)
        {
            Runtime = LocalVoice.Folder(data);
            Model = Path.Combine(data, Path.Combine("recall", "model"));
            Store = Path.Combine(data, Path.Combine("recall", "vectors.bin"));
            Gaming = delegate { return Lockout.Game != null; };
        }

        private static string Dll { get { return Path.Combine(Runtime ?? "", "onnxruntime.dll"); } }
        private static string ModelFile { get { return Path.Combine(Model ?? "", "model.onnx"); } }
        private static string VocabFile { get { return Path.Combine(Model ?? "", "vocab.txt"); } }

        internal static string Missing()
        {
            if (!On) return "the MeaningOn setting is off";
            if (!HasRuntime) return "no onnxruntime.dll in 64-bit Windows (her free voice pack is not installed)";
            if (!Downloaded) return "the bge-base-en-v1.5 model is not downloaded";
            return null;
        }

        internal static bool HasRuntime { get { return IntPtr.Size == 8 && Runtime != null && File.Exists(Dll); } }

        internal static bool Downloaded { get { return Model != null && File.Exists(ModelFile) && File.Exists(VocabFile); } }

        internal static bool Ready { get { return Missing() == null; } }

        internal static bool Loaded { get { lock (gate) return loaded != null; } }

        internal static int Stored { get { return Snapshot().Count; } }

        private static bool Playing()
        {
            Func<bool> playing = Gaming;
            return playing != null && playing();
        }

        internal static string Key(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                StringBuilder key = new StringBuilder();
                foreach (byte value in sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""))) key.Append(value.ToString("x2"));
                return key.ToString();
            }
        }

        internal static float[] Query(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !Ready || Playing()) return null;
            try
            {
                lock (gate)
                {
                    if (!Open()) return null;
                    float norm;
                    float[] vector = loaded.Vector(Prefix + text.Trim(), out norm);
                    used = DateTime.Now;
                    return vector;
                }
            }
            catch (Exception error)
            {
                Broke(error);
                return null;
            }
        }

        internal static double Bar(List<double> sorted)
        {
            if (sorted.Count < 2) return double.MaxValue;
            return Math.Max(Floor, sorted[Math.Min(Crowd, sorted.Count) - 1] + Gap);
        }

        internal static double Dot(float[] a, float[] b)
        {
            double total = 0;
            for (int i = 0; i < a.Length && i < b.Length; i++) total += a[i] * b[i];
            return total;
        }

        internal static Dictionary<string, float[]> Snapshot()
        {
            lock (gate)
            {
                if (vectors == null) vectors = Read();
                return vectors;
            }
        }

        internal static int Fill(List<KeyValuePair<string, string>> pieces, int most, Func<bool> stop)
        {
            if (!Ready || Playing()) return 0;
            Dictionary<string, float[]> have = Snapshot();
            Dictionary<string, float[]> next = new Dictionary<string, float[]>();
            Dictionary<string, string> missing = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> piece in pieces)
            {
                float[] known;
                if (have.TryGetValue(piece.Key, out known)) next[piece.Key] = known;
                else if (!missing.ContainsKey(piece.Key)) missing[piece.Key] = piece.Value;
            }
            int made = 0;
            try
            {
                foreach (KeyValuePair<string, string> piece in missing)
                {
                    if (made >= most || (stop != null && stop()) || Playing()) break;
                    lock (gate)
                    {
                        if (!Open()) break;
                        float norm;
                        next[piece.Key] = loaded.Vector(piece.Value, out norm);
                        used = DateTime.Now;
                    }
                    made++;
                }
            }
            catch (Exception error) { Broke(error); }
            if (made == 0 && next.Count == have.Count) return 0;
            lock (gate) vectors = next;
            Save(next);
            return made;
        }

        internal static void Rest(DateTime now)
        {
            lock (gate)
                if (loaded != null && (now - used).TotalMinutes >= QuietMinutes) Unload();
        }

        internal static void Calm(DateTime now, bool game)
        {
            if (!System.Threading.Monitor.TryEnter(gate)) return;
            try
            {
                if (loaded != null && (game || (now - used).TotalMinutes >= QuietMinutes)) Unload();
            }
            finally { System.Threading.Monitor.Exit(gate); }
        }

        internal static void Unload()
        {
            lock (gate)
            {
                if (loaded == null) return;
                loaded.Dispose();
                loaded = null;
                ActivityLog.Write("meaning", "model unloaded", "quiet or a game");
            }
        }

        internal static void Reset()
        {
            lock (gate)
            {
                Unload();
                vectors = null;
                failedAt = DateTime.MinValue;
            }
        }

        internal static void Fetch(Func<bool> stop)
        {
            Directory.CreateDirectory(Model);
            string drive = Path.GetPathRoot(Path.GetFullPath(Model));
            if (new DriveInfo(drive).AvailableFreeSpace < ModelSize + (64L << 20)) throw new IOException("Storage too full for the meaning model (about 450 MB).");
            Action<string> retried = delegate(string why) { ActivityLog.Write("meaning", "download retry", why); };
            if (!File.Exists(VocabFile)) Engine.Fetch(VocabUrl, VocabFile, VocabSize, VocabSha, "the meaning word list", Told(), stop, retried);
            if (!File.Exists(ModelFile)) Engine.Fetch(ModelUrl, ModelFile, ModelSize, ModelSha, "the meaning model", Told(), stop, retried);
        }

        private static Action<string, long, long> Told()
        {
            long shown = 0;
            return delegate(string step, long done, long total)
            {
                if (done < total && done - shown < LogEvery) return;
                shown = done;
                ActivityLog.Write("meaning", "download", step + ", " + Engine.Megabytes(done) + " of " + Engine.Megabytes(total) + " MB");
            };
        }

        private static bool Open()
        {
            if (loaded != null) return true;
            if ((DateTime.Now - failedAt).TotalMinutes < RetryMinutes) return false;
            Stopwatch watch = Stopwatch.StartNew();
            loaded = Embed.Open(Dll, ModelFile, VocabFile, Threads);
            used = DateTime.Now;
            ActivityLog.Write("meaning", "model loaded", watch.ElapsedMilliseconds + " ms");
            return true;
        }

        private static void Broke(Exception error)
        {
            lock (gate)
            {
                failedAt = DateTime.Now;
                if (loaded != null)
                {
                    loaded.Dispose();
                    loaded = null;
                }
            }
            ActivityLog.Write("meaning", "failed, words only for " + RetryMinutes + " minutes", error.Message);
        }

        private static Dictionary<string, float[]> Read()
        {
            Dictionary<string, float[]> found = new Dictionary<string, float[]>();
            if (Store == null || !File.Exists(Store)) return found;
            try
            {
                using (BinaryReader reader = new BinaryReader(File.OpenRead(Store)))
                {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != Embed.Size) return found;
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string key = reader.ReadString();
                        float[] vector = new float[Embed.Size];
                        for (int n = 0; n < vector.Length; n++) vector[n] = reader.ReadSingle();
                        found[key] = vector;
                    }
                }
            }
            catch (IOException) { return new Dictionary<string, float[]>(); }
            catch (UnauthorizedAccessException) { return new Dictionary<string, float[]>(); }
            return found;
        }

        private static void Save(Dictionary<string, float[]> keep)
        {
            if (Store == null) return;
            string part = Store + ".part";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Store));
                using (BinaryWriter writer = new BinaryWriter(File.Create(part)))
                {
                    writer.Write(Magic);
                    writer.Write(Embed.Size);
                    writer.Write(keep.Count);
                    foreach (KeyValuePair<string, float[]> entry in keep)
                    {
                        writer.Write(entry.Key);
                        foreach (float value in entry.Value) writer.Write(value);
                    }
                }
                if (File.Exists(Store)) File.Delete(Store);
                File.Move(part, Store);
            }
            catch (IOException error) { ActivityLog.Write("meaning", "vectors not saved", error.Message); }
            catch (UnauthorizedAccessException error) { ActivityLog.Write("meaning", "vectors not saved", error.Message); }
        }
    }
}
