using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal static class Challenge
    {
        internal const int RepeatMinutes = 10;
        internal const int FewTargets = 15;
        internal const int LinesRead = 8;
        internal const string Stopped = "A human check (a captcha) came up on that page, and it's yours to click.";
        internal const string Shown = "My browser is on your screen now: click the check, then ask me again.";
        internal const string Theirs = "That site keeps checking my browser, so I opened it in your own browser instead.";

        internal static Func<Sight> Look = HerBrowser.Look;
        internal static Action<string> OpenTheirs = delegate(string url) { if (!string.IsNullOrEmpty(url)) Process.Start(url); };
        internal static volatile string Hit;
        internal static volatile bool Showing;

        private static readonly object gate = new object();
        private static readonly Dictionary<string, DateTime> handed = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex TitleWords = new Regex(@"^(?:just a moment|attention required|security check|captcha)\b|\b(?:verify you are (?:a )?human|are you a robot)\b", RegexOptions.IgnoreCase);
        private static readonly Regex LineWords = new Regex(@"\b(?:verify you are (?:a )?human|i'?m not a robot|performing security verification|checking (?:if the site connection is secure|your browser)|complete the security check|press and hold|hcaptcha|recaptcha|captcha)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Calling = new Regex(@"^\W*(?:hey\s+)?(?:capri|kapri)\W+", RegexOptions.IgnoreCase);
        private static readonly Regex ShowAsk = new Regex(@"^(?:can you |could you |please )?(?:pull up|bring up|show(?: me)?|put(?: up)?) (?:the|your|her) browser(?: (?:up|back|again|on my screen|for me|so i can .+))?\W*$", RegexOptions.IgnoreCase);
        private static readonly Regex HideAsk = new Regex(@"^(?:can you |could you |please )?(?:hide|close) (?:the|your|her) browser\W*$|^put (?:the|your|her) browser away\W*$", RegexOptions.IgnoreCase);

        internal static bool Seen(Sight sight)
        {
            if (sight == null) return false;
            if (TitleWords.IsMatch(sight.Title ?? "")) return true;
            if (sight.Targets.Count > FewTargets) return false;
            for (int i = 0; i < sight.Words.Count && i < LinesRead; i++) if (LineWords.IsMatch(sight.Words[i] ?? "")) return true;
            return false;
        }

        internal static string Host(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url ?? "", UriKind.Absolute, out uri)) return "";
            return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host.Substring(4) : uri.Host;
        }

        internal static bool Again(string host, DateTime now)
        {
            DateTime before;
            lock (gate) if (!handed.TryGetValue(host ?? "", out before)) return false;
            return now >= before && now - before < TimeSpan.FromMinutes(RepeatMinutes);
        }

        internal static void Handed(string host, DateTime now)
        {
            lock (gate) handed[host ?? ""] = now;
        }

        internal static void Forget()
        {
            lock (gate) handed.Clear();
            Hit = null;
            Showing = false;
        }

        internal static string Plan(bool lockout, bool away, bool again)
        {
            if (lockout) return "later";
            if (away) return "back";
            return "theirs";
        }

        internal const int MostSkips = 2;
        internal const string SkipMark = "\nSites to skip in this job (they block your browser with a human check, so get the same facts from other sites): ";
        private static readonly Regex Errand = new Regex(@"\b(?:sign (?:in|up)|log ?in|buy|order|book|checkout|check out|pay|post|send|download|install|subscribe|register|apply|upload|reply|comment|message)\b", RegexOptions.IgnoreCase);
        private static readonly Regex CarryOnWords = new Regex(@"^(?:ok(?:ay)?[, ]+)?(?:carry on|go on|go ahead|continue|i did it|i passed it|passed(?: it)?|finish it)\W*$", RegexOptions.IgnoreCase);
        internal static volatile string PendingTask;
        internal static volatile string PendingHost;
        internal static DateTime PendingAt;

        internal static string Core(string host)
        {
            string plain = (host ?? "").ToLowerInvariant();
            int dot = plain.IndexOf('.');
            return dot > 0 ? plain.Substring(0, dot) : plain;
        }

        internal static List<string> Skipped(string task)
        {
            List<string> hosts = new List<string>();
            int at = (task ?? "").IndexOf(SkipMark, StringComparison.Ordinal);
            if (at < 0) return hosts;
            foreach (string host in task.Substring(at + SkipMark.Length).Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)) hosts.Add(host.Trim());
            return hosts;
        }

        internal static bool Skippable(string words, string task, string host)
        {
            string core = Core(host);
            if (core.Length < 3 || Skipped(task).Count >= MostSkips || Skipped(task).Contains(host)) return false;
            string said = Regex.Replace((words ?? "").ToLowerInvariant(), @"[^a-z0-9]", "");
            if (said.Contains(core)) return false;
            return !Errand.IsMatch(words ?? "");
        }

        internal static string WithSkip(string task, string host)
        {
            List<string> hosts = Skipped(task);
            hosts.Add(host);
            int at = (task ?? "").IndexOf(SkipMark, StringComparison.Ordinal);
            string bare = at < 0 ? task ?? "" : task.Substring(0, at);
            return bare + SkipMark + string.Join(", ", hosts.ToArray());
        }

        internal static bool CarryOn(string text, DateTime now)
        {
            return PendingTask != null && now - PendingAt < TimeSpan.FromMinutes(RepeatMinutes) && CarryOnWords.IsMatch(Calling.Replace((text ?? "").Trim(), ""));
        }

        internal static string TheirsLine(string host)
        {
            return (string.IsNullOrEmpty(host) ? "That site" : host) + " blocks my browser with a human check, so I opened it in your own browser. Pass the check there, then say carry on and I'll finish it in that window.";
        }

        internal static string BrowserAsk(string text)
        {
            string said = Calling.Replace((text ?? "").Trim(), "");
            if (ShowAsk.IsMatch(said)) return "show";
            if (HideAsk.IsMatch(said)) return "hide";
            return null;
        }

        internal static Sight Sensed()
        {
            Sight sight = Look();
            if (!Seen(sight)) return sight;
            Hit = Host(HerBrowser.LastUrl);
            Computer.Interrupt(Stopped);
            return sight;
        }

        internal static Func<string, string, List<KeyValuePair<string, ToolArguments>>, string> Guard(Func<string, string, List<KeyValuePair<string, ToolArguments>>, string> think)
        {
            return delegate(string system, string user, List<KeyValuePair<string, ToolArguments>> calls)
            {
                if (Computer.Interrupted && Computer.Why == Stopped) return "";
                return think(system, user, calls);
            };
        }
    }

    internal sealed partial class Companion
    {
        private bool BrowserHeard(string text, bool voiceChat)
        {
            if (Challenge.CarryOn(text, DateTime.Now)) { CarryOnTheirs(text, voiceChat); return true; }
            string want = Challenge.BrowserAsk(text);
            if (want == null) return false;
            string said = want == "show" && Lockout.Active ? ShowLater : HerBrowser.Show(want == "show");
            if (want == "hide") Challenge.Showing = false;
            Tell(voiceChat ? "(voice: browser)" : "(browser)", said);
            return true;
        }

        private string ChallengeHanded(string result, string task)
        {
            string host = Challenge.Hit ?? "";
            string way = Challenge.Plan(Lockout.Active, Delivery.NotHome(), host.Length > 0 && Challenge.Again(host, DateTime.Now));
            ActivityLog.Write("browser", "human check", (host.Length > 0 ? host : "a page") + " -> " + way);
            string told = (result ?? "").TrimEnd();
            if (way == "later") return told + " " + ShowLater;
            if (way == "back") return told + " " + ShowWhenBack;
            if (way == "theirs")
            {
                try { Challenge.OpenTheirs(HerBrowser.LastUrl); }
                catch (Exception error) { ActivityLog.Write("browser", "own browser not opened", error.GetType().Name); return told + " " + ShowLater; }
                Challenge.Handed(host, DateTime.Now);
                Challenge.PendingTask = task;
                Challenge.PendingHost = host;
                Challenge.PendingAt = DateTime.Now;
                return Challenge.TheirsLine(host);
            }
            return told;
        }

        private void CarryOnTheirs(string text, bool voiceChat)
        {
            string task = Challenge.PendingTask, host = Challenge.PendingHost;
            Challenge.PendingTask = null;
            if (task == null) return;
            if (Lockout.Active) { Tell(voiceChat ? "(voice: browser)" : "(browser)", ShowLater); return; }
            Job made = new Job("In their own browser window on " + (string.IsNullOrEmpty(host) ? "the page I opened for them" : host) + " (they already passed its human check there, so use that window with the mouse and keyboard): " + task, text);
            ActivityLog.Write("browser", "carry on in his browser", host);
            Tell(voiceChat ? "(voice: browser)" : "(browser)", "On it, in your browser.");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try { result = Work(made); }
                catch (Exception error) { result = "That didn't work: " + error.Message; }
                Post(delegate { Tell("(browser)", result); });
            });
        }
    }
}
