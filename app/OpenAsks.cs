using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed class OpenAsk
    {
        internal DateTime At;
        internal string Kind;
        internal string Text;
    }

    internal static class OpenAsks
    {
        internal const string SignIn = "signin";
        internal const string Bare = "done";
        internal const int KeepHours = 36;
        internal const int MostWords = 14;
        internal const int TailLines = 12;
        internal const string SchoolSignIn = "Sign in to Outlook once in Chrome";
        internal static string StudySignIn { get { return "Sign in to Outlook and " + SchoolSites.Listed(", ") + " once in Chrome"; } }
        internal static string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "open-asks.txt");
        private static readonly object gate = new object();

        private const string Lead = @"^\W*(?:(?:ok(?:ay)?|so|yeah|yep|yes|alright|right|hey\s+capri|capri)[,\s]+)*";
        private static readonly Regex SignedIn = new Regex(Lead + @"(?:(?:i(?:'ve|\s+have)?|i'?m|i\s+am|he|he'?s)\s+)?(?:just\s+|already\s+|now\s+|finally\s+)?(?:signed|logged)\s+(?:in(?:to)?|onto|on)\b(?<rest>[^?]*)$", RegexOptions.IgnoreCase);
        private static readonly Regex BareDone = new Regex(Lead + @"(?:(?:i(?:'ve|\s+have)?\s+)?(?:done|did)\s+(?:it|that)|all\s+done|it'?s\s+done|done|finished|did\s+it)(?:\s+(?:now|already))?\W*$", RegexOptions.IgnoreCase);
        private static readonly Regex NotDone = new Regex(@"\b(?:need|needs|have\s+to|has\s+to|should|will|gonna|going\s+to|can'?t|cannot|couldn'?t|how|why|when|tried|trying|didn'?t|not)\b|\?", RegexOptions.IgnoreCase);
        private static readonly Regex SchoolSite = new Regex(@"\b(?:out\s?look|out\s?put|outlet|school|uni|university|e-?mail|mail|microsoft)\b", RegexOptions.IgnoreCase);
        private static readonly Regex OtherSite = new Regex(@"\b(?:discord|steam|spotify|riot|league|netflix|youtube|twitch|instagram|facebook|reddit|epic|battle\.?net|ubisoft|google|gmail|github|claude|tailscale)\b", RegexOptions.IgnoreCase);
        private static readonly Regex SignInPlan = new Regex(@"\b(?:sign|log)(?:ged|ed)?[ -]?(?:in|into|on)\b", RegexOptions.IgnoreCase);
        private static readonly Regex SignInLine = new Regex(@"^\W*SIGN IN\b");
        private static readonly Regex LoginPage = new Regex(@"login\.microsoftonline\.com|login\.live\.com|\bpick an account\b|\baccount picker\b|\bsign in to your account\b", RegexOptions.IgnoreCase);
        internal const int TailChars = 2000;

        internal static bool LoginSeen(string output)
        {
            string text = output ?? "";
            return LoginPage.IsMatch(text.Length > TailChars ? text.Substring(text.Length - TailChars) : text);
        }

        internal static string Heard(string text)
        {
            string said = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            if (said.Length == 0 || said.Split(' ').Length > MostWords) return null;
            string first = Regex.Split(said, @"(?<=[.!])\s+")[0].TrimEnd('.', '!', ' ');
            Match signed = SignedIn.Match(first);
            if (signed.Success)
            {
                if (NotDone.IsMatch(first)) return null;
                string rest = signed.Groups["rest"].Value;
                if (OtherSite.IsMatch(rest) && !SchoolSite.IsMatch(rest) && !SchoolSites.Named(rest)) return null;
                return SignIn;
            }
            if (BareDone.IsMatch(said) && !NotDone.IsMatch(said)) return Bare;
            return null;
        }

        internal static bool SignInAtEnd(string output)
        {
            string[] lines = (output ?? "").Replace("\r", "").Split('\n');
            int seen = 0;
            for (int i = lines.Length - 1; i >= 0 && seen < TailLines; i--)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || Regex.IsMatch(line, @"^[\W_]+$")) continue;
                seen++;
                if (SignInLine.IsMatch(line) && line.IndexOf("when Outlook wants", StringComparison.OrdinalIgnoreCase) < 0) return true;
            }
            return false;
        }

        internal static PlanLine Plan(List<PlanLine> open)
        {
            if (open == null) return null;
            for (int i = open.Count - 1; i >= 0; i--)
                if (!open[i].Waiting && SignInPlan.IsMatch(open[i].Text ?? "")) return open[i];
            return null;
        }

        private static List<OpenAsk> Read()
        {
            List<OpenAsk> asks = new List<OpenAsk>();
            try
            {
                if (!File.Exists(FilePath)) return asks;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] parts = line.Split(new[] { '|' }, 3);
                    DateTime at;
                    if (parts.Length < 3 || !DateTime.TryParseExact(parts[0], "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) continue;
                    asks.Add(new OpenAsk { At = at, Kind = parts[1], Text = parts[2] });
                }
            }
            catch (IOException error) { Brain.Log("open asks", -1, error.Message); }
            catch (UnauthorizedAccessException error) { Brain.Log("open asks", -1, error.Message); }
            return asks;
        }

        private static void Write(List<OpenAsk> asks)
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (OpenAsk ask in asks) lines.Add(ask.At.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) + "|" + ask.Kind + "|" + Regex.Replace(ask.Text ?? "", @"[\r\n|]+", " "));
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (IOException error) { Brain.Log("open asks", -1, error.Message); }
            catch (UnauthorizedAccessException error) { Brain.Log("open asks", -1, error.Message); }
        }

        private static List<OpenAsk> Fresh(DateTime now)
        {
            return Read().FindAll(delegate(OpenAsk ask) { return (now - ask.At).TotalHours < KeepHours && ask.At <= now.AddMinutes(1); });
        }

        internal static void Add(string kind, string text, DateTime now)
        {
            lock (gate)
            {
                List<OpenAsk> asks = Fresh(now).FindAll(delegate(OpenAsk ask) { return ask.Kind != kind; });
                asks.Add(new OpenAsk { At = now, Kind = kind, Text = text });
                Write(asks);
            }
            ActivityLog.Write("asks", "open", text);
        }

        internal static OpenAsk Peek(string kind, DateTime now)
        {
            lock (gate)
            {
                List<OpenAsk> asks = Fresh(now);
                for (int i = asks.Count - 1; i >= 0; i--) if (kind == null || asks[i].Kind == kind) return asks[i];
                return null;
            }
        }

        internal static OpenAsk Take(string kind, DateTime now)
        {
            lock (gate)
            {
                List<OpenAsk> asks = Fresh(now);
                for (int i = asks.Count - 1; i >= 0; i--)
                {
                    if (kind != null && asks[i].Kind != kind) continue;
                    OpenAsk taken = asks[i];
                    asks.RemoveAt(i);
                    Write(asks);
                    return taken;
                }
                return null;
            }
        }

        internal static bool AskedToday(string kind, DateTime now)
        {
            OpenAsk ask = Peek(kind, now);
            return ask != null && ask.At.Date == now.Date;
        }
    }
}
