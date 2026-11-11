using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    internal static class Situation
    {
        internal const string FileName = "situation.log";
        internal const long Biggest = 2 * 1024 * 1024;
        internal const int MostChars = 200;
        internal const int KeepSnapshots = 21;
        private static readonly object writing = new object();

        internal static string Line(DateTime now, string source, string game, List<string> call, bool listen, bool away, string text)
        {
            string said = Computer.Mask(text ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();
            if (said.Length > MostChars) said = said.Substring(0, MostChars);
            string with = call == null || call.Count == 0 ? "none" : string.Join(", ", call.ToArray()).Replace("|", "/");
            return now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " | " + source + " | game: " + (string.IsNullOrEmpty(game) ? "none" : game.Replace("|", "/")) + " | call: " + with + " | listen: " + (listen ? "on" : "off") + " | " + (away ? "away" : "home") + " | " + said;
        }

        internal static void Write(string line)
        {
            string log = ActivityLog.FilePath;
            if (string.IsNullOrEmpty(log) || string.IsNullOrEmpty(line) || !ForkSettings.Current.LogActions || !Brain.Keeping) return;
            string path = Path.Combine(Path.GetDirectoryName(log), FileName);
            lock (writing)
            {
                try
                {
                    FileInfo file = new FileInfo(path);
                    if (file.Exists && file.Length > Biggest)
                    {
                        string old = path + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static string Snapshot(string vault, string folder, DateTime now)
        {
            if (string.IsNullOrEmpty(vault) || string.IsNullOrEmpty(folder) || !Directory.Exists(vault)) return null;
            string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string target = Path.Combine(folder, day);
            if (Directory.Exists(target)) return null;
            string full = Path.GetFullPath(vault).TrimEnd('\\', '/');
            int copied = 0;
            foreach (string file in Directory.GetFiles(full, "*.md", SearchOption.AllDirectories))
            {
                string relative = file.Substring(full.Length + 1);
                string[] parts = relative.Split('\\', '/');
                if (Array.Exists(parts, delegate(string part) { return part.StartsWith(".", StringComparison.Ordinal); })) continue;
                if (relative.Replace('/', '\\').StartsWith("Capri\\backups\\", StringComparison.OrdinalIgnoreCase) || Vault.Conflict(file)) continue;
                string to = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(file, to, false);
                copied++;
            }
            Prune(folder);
            return day + " (" + copied + " notes)";
        }

        internal static void Prune(string folder)
        {
            if (!Directory.Exists(folder)) return;
            List<string> days = new List<string>();
            foreach (string dir in Directory.GetDirectories(folder))
            {
                DateTime parsed;
                if (DateTime.TryParseExact(Path.GetFileName(dir), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) days.Add(dir);
            }
            days.Sort(StringComparer.Ordinal);
            for (int index = 0; index < days.Count - KeepSnapshots; index++)
            {
                try { Directory.Delete(days[index], true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static string SnapshotFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "replay", "vault");
        }
    }

    internal sealed partial class Companion
    {
        private void SituationSeen(string text, bool voiceChat)
        {
            if (testing || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                string source = voiceChat ? "voice chat" : typedLine && discordLine != null ? "discord" : typedLine ? "typed" : "voice";
                string game = Lockout.Game ?? careVoiceGame;
                ForkSettings settings = ForkSettings.Current;
                Situation.Write(Situation.Line(DateTime.Now, source, game, DiscordPresence.InCallWithHim(settings.DiscordOwner), settings.ListenMode, Delivery.NotHome(), text));
            }
            catch (Exception error) { Brain.Log("situation", -1, error.GetType().Name + ": " + error.Message); }
        }
    }
}
