using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CapriDesktop
{
    internal sealed class PhoneDevice
    {
        internal string Id;
        internal string Hash;
        internal DateTime Paired;
        internal string Name;
    }

    internal static class PhoneAuth
    {
        internal const int CodeLength = 6;
        internal const int MostWrong = 5;
        internal const int LockMinutes = 10;
        internal const int CodeMinutes = 5;
        private const int TokenBytes = 32;
        private const int MostName = 40;
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        internal static string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "phones.dat");
        private static readonly object gate = new object();
        private static string code;
        private static DateTime codeUntil = DateTime.MinValue, lockedUntil = DateTime.MinValue;
        private static int wrong;

        internal static string NewCode(DateTime now)
        {
            lock (gate)
            {
                if (now < lockedUntil) return null;
                byte[] random = Random(CodeLength);
                StringBuilder made = new StringBuilder();
                foreach (byte one in random) made.Append(Alphabet[one % Alphabet.Length]);
                code = made.ToString();
                codeUntil = now.AddMinutes(CodeMinutes);
                wrong = 0;
                return code;
            }
        }

        internal static bool Locked(DateTime now)
        {
            lock (gate) return now < lockedUntil;
        }

        internal static void Reset()
        {
            lock (gate) { code = null; wrong = 0; codeUntil = DateTime.MinValue; lockedUntil = DateTime.MinValue; }
        }

        internal static string Pair(string tried, string device, DateTime now)
        {
            lock (gate)
            {
                if (now < lockedUntil || code == null || now > codeUntil) return null;
                if (!Same(Normal(tried), code))
                {
                    wrong++;
                    if (wrong >= MostWrong) { lockedUntil = now.AddMinutes(LockMinutes); code = null; wrong = 0; }
                    return null;
                }
                code = null;
                wrong = 0;
                string token = Convert.ToBase64String(Random(TokenBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                List<PhoneDevice> devices = Load();
                string name = (device ?? "phone").Replace("|", " ").Replace("\r", " ").Replace("\n", " ").Trim();
                devices.Add(new PhoneDevice { Id = Hex(Random(4)), Hash = HashOf(token), Paired = now, Name = name.Length > MostName ? name.Substring(0, MostName) : name });
                Save(devices);
                return token;
            }
        }

        internal static string Mint(string device, DateTime now)
        {
            lock (gate)
            {
                string token = Convert.ToBase64String(Random(TokenBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                List<PhoneDevice> devices = Load();
                devices.RemoveAll(delegate(PhoneDevice one) { return one.Name == device; });
                devices.Add(new PhoneDevice { Id = Hex(Random(4)), Hash = HashOf(token), Paired = now, Name = device });
                Save(devices);
                return token;
            }
        }

        internal static string Device(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            string hash = HashOf(token);
            lock (gate)
                foreach (PhoneDevice one in Load())
                    if (Same(one.Hash, hash)) return one.Id;
            return null;
        }

        internal static bool Revoke(string id)
        {
            lock (gate)
            {
                List<PhoneDevice> devices = Load();
                int removed = devices.RemoveAll(delegate(PhoneDevice one) { return one.Id == id; });
                if (removed > 0) Save(devices);
                return removed > 0;
            }
        }

        internal static List<PhoneDevice> Devices()
        {
            lock (gate) return Load();
        }

        private static string Normal(string tried)
        {
            return (tried ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
        }

        private static bool Same(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static byte[] Random(int count)
        {
            byte[] bytes = new byte[count];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return bytes;
        }

        private static string Hex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        internal static string HashOf(string token)
        {
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? "")));
        }

        private static List<PhoneDevice> Load()
        {
            List<PhoneDevice> devices = new List<PhoneDevice>();
            try
            {
                if (!File.Exists(FilePath)) return devices;
                string text = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser));
                foreach (string line in text.Split('\n'))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 4) continue;
                    DateTime paired;
                    DateTime.TryParseExact(parts[2], "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out paired);
                    devices.Add(new PhoneDevice { Id = parts[0], Hash = parts[1], Paired = paired, Name = parts[3] });
                }
            }
            catch (CryptographicException error) { Brain.Log("phone", -1, "pairing file unreadable: " + error.GetType().Name); }
            catch (IOException error) { Brain.Log("phone", -1, error.Message); }
            return devices;
        }

        private static void Save(List<PhoneDevice> devices)
        {
            StringBuilder text = new StringBuilder();
            foreach (PhoneDevice one in devices) text.Append(one.Id).Append('|').Append(one.Hash).Append('|').Append(one.Paired.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)).Append('|').Append(one.Name).Append('\n');
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllBytes(FilePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(text.ToString()), null, DataProtectionScope.CurrentUser));
            }
            catch (IOException error) { Brain.Log("phone", -1, error.Message); }
        }
    }
}
