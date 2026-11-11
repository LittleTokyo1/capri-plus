using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class Launchable
    {
        private const int MostFiles = 20000;
        private const int DeepestFolder = 2;
        private const int SearchMilliseconds = 2500;
        private static readonly string[] Kinds = { ".ps1", ".bat", ".cmd", ".lnk" };
        private static readonly Regex Installer = new Regex(@"setup|install|uninst|unins\d", RegexOptions.IgnoreCase);
        private static readonly string[] Skipped = { "appdata", "node_modules", ".git", "windows", "program files", "program files (x86)", "$recycle.bin", "onedrive" };
        private static readonly Regex Filler = new Regex(@"\A(start|run|open|launch|the|my|a|an|file|script|shortcut|please|for|me|that|this|with|powershell|and|then)\z", RegexOptions.IgnoreCase);

        private static readonly Regex Asked = new Regex(@"^(?:please\s+)?(?:start|run|launch|execute)\s+(?:the\s+|my\s+)?(.+?)[\s.!]*$", RegexOptions.IgnoreCase);
        private static readonly Regex Script = new Regex(@"\b(?:ps1|bat|cmd|script)\b", RegexOptions.IgnoreCase);

        private static readonly Regex ScriptPath = new Regex(@"[A-Za-z]:\\[^\r\n""<>|*?,]+?\.(?:ps1|bat|cmd)\b", RegexOptions.IgnoreCase);
        private static readonly Regex FolderPath = new Regex(@"[A-Za-z]:\\[^\r\n""<>|*?,]+", RegexOptions.IgnoreCase);
        private static readonly Regex ScriptName = new Regex(@"(?<![\\\w.-])[\w.-]+\.(?:ps1|bat|cmd)\b", RegexOptions.IgnoreCase);
        private static readonly Regex RunWord = new Regex(@"\b(?:run|start|launch|execute)\b", RegexOptions.IgnoreCase);

        internal static string PathAsked(string text)
        {
            string words = ScriptName.Replace(Regex.Replace(text ?? "", @"[A-Za-z]:\\\S+", " "), " ");
            if (!RunWord.IsMatch(words)) return null;
            try
            {
                Match path = ScriptPath.Match(text);
                if (path.Success && File.Exists(path.Value)) return path.Value;
                Match folder = FolderPath.Match(text), name = ScriptName.Match(text);
                if (!folder.Success || !name.Success) return null;
                string place = folder.Value.Trim().TrimEnd('\\', '.');
                while (!Directory.Exists(place) && place.IndexOf(' ') > 0) place = place.Substring(0, place.LastIndexOf(' ')).TrimEnd('\\', '.', ' ');
                string joined = Path.Combine(place, name.Value);
                return Directory.Exists(place) && File.Exists(joined) ? joined : null;
            }
            catch (ArgumentException) { return null; }
        }

        internal static string Wants(string text)
        {
            string path = PathAsked(text);
            if (path != null) return path;
            Match asked = Asked.Match((text ?? "").Trim());
            return asked.Success && Script.IsMatch(asked.Groups[1].Value) ? asked.Groups[1].Value : null;
        }

        internal static bool Scripted(string wanted)
        {
            return Script.IsMatch(wanted ?? "");
        }

        internal static Func<List<string>> Roots = Folders;
        internal static Action<string> Start = Begin;

        private static List<string> Folders()
        {
            List<string> roots = new List<string>();
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.Programs));
            return roots;
        }

        private static void Begin(string path)
        {
            string folder = Path.GetDirectoryName(path);
            ProcessStartInfo info = path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                ? new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + path + "\"")
                : new ProcessStartInfo(path);
            info.UseShellExecute = true;
            info.WorkingDirectory = folder;
            Process.Start(info);
        }

        internal static List<string> Words(string text)
        {
            List<string> words = new List<string>();
            foreach (Match word in Regex.Matches(Regex.Replace((text ?? "").Normalize(System.Text.NormalizationForm.FormD), @"\p{M}", "").ToLowerInvariant(), @"[a-z0-9]+")) words.Add(word.Value);
            return words;
        }

        private static List<string> Wanted(string spoken)
        {
            List<string> wanted = new List<string>();
            foreach (string word in Words(spoken)) if (!Filler.IsMatch(word)) wanted.Add(word);
            return wanted;
        }

        internal static bool Fits(string file, List<string> wanted)
        {
            if (wanted.Count == 0 || Installer.IsMatch(Path.GetFileNameWithoutExtension(file))) return false;
            List<string> have = Words(Path.GetFileNameWithoutExtension(file) + " " + Path.GetExtension(file));
            foreach (string word in wanted) if (!have.Contains(word)) return false;
            return true;
        }

        internal static List<string> Find(string spoken)
        {
            List<string> wanted = Wanted(spoken);
            List<string> found = new List<string>();
            if (wanted.Count == 0) return found;
            Stopwatch clock = Stopwatch.StartNew();
            int seen = 0;
            foreach (string root in Roots())
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                Walk(root, 0, wanted, found, clock, ref seen);
            }
            return found;
        }

        private static void Walk(string folder, int depth, List<string> wanted, List<string> found, Stopwatch clock, ref int seen)
        {
            if (seen > MostFiles || clock.ElapsedMilliseconds > SearchMilliseconds) return;
            try
            {
                foreach (string file in Directory.GetFiles(folder))
                {
                    seen++;
                    if (Array.IndexOf(Kinds, Path.GetExtension(file).ToLowerInvariant()) >= 0 && Fits(file, wanted) && !found.Contains(file)) found.Add(file);
                }
                if (depth >= DeepestFolder) return;
                foreach (string sub in Directory.GetDirectories(folder))
                {
                    string name = Path.GetFileName(sub).ToLowerInvariant();
                    if (Array.IndexOf(Skipped, name) >= 0 || name.StartsWith(".", StringComparison.Ordinal)) continue;
                    Walk(sub, depth + 1, wanted, found, clock, ref seen);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        // The one file that fits what he called it, run; null when none fits or several fit equally.
        internal static string Run(string spoken)
        {
            string exact = PathAsked("run " + (spoken ?? ""));
            if (exact != null && exact.Length == (spoken ?? "").Trim().Length)
            {
                try { Start(exact); }
                catch (Exception error) { return "I found " + Path.GetFileName(exact) + " but it didn't start: " + error.Message; }
                return "Ran " + Path.GetFileName(exact) + " from " + Path.GetDirectoryName(exact) + ".";
            }
            List<string> found = Find(spoken);
            if (found.Count == 0) return null;
            string chosen = null;
            int fewest = int.MaxValue;
            bool tie = false;
            foreach (string file in found)
            {
                int extra = Words(Path.GetFileNameWithoutExtension(file)).Count;
                if (extra < fewest) { fewest = extra; chosen = file; tie = false; }
                else if (extra == fewest && !SameName(file, chosen)) tie = true;
            }
            if (tie || chosen == null) return null;
            try { Start(chosen); }
            catch (Exception error) { return "I found " + Path.GetFileName(chosen) + " but it didn't start: " + error.Message; }
            return "Ran " + Path.GetFileName(chosen) + " from " + Path.GetDirectoryName(chosen) + ".";
        }

        private static bool SameName(string one, string other)
        {
            return string.Equals(Path.GetFileNameWithoutExtension(one), Path.GetFileNameWithoutExtension(other), StringComparison.OrdinalIgnoreCase);
        }
    }
}
