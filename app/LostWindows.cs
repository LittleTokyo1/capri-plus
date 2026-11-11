using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class LostWindows
    {
        internal const int KeepMinutes = 30;
        private const int MostName = 40;
        private static readonly Regex Wanted = new Regex(@"\b(minimi[sz]e\w*|hide|desktop|close|clear (?:the|my) screen|get rid of|tidy (?:up )?(?:the|my) screen)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ChatApp = new Regex(@"\b(gmail|outlook|thunderbird|discord|whatsapp|telegram|signal|slack|teams|messenger)\b", RegexOptions.IgnoreCase);
        private static readonly Regex MailWords = new Regex(@"\b(inbox|mail)\b|@", RegexOptions.IgnoreCase);
        private static readonly Regex Incognito = new Regex(@"InPrivate|Incognito|Private Browsing|Private window", RegexOptions.IgnoreCase);
        private static readonly object gate = new object();
        private static List<OpenWindow> shownBefore = new List<OpenWindow>();
        private static OpenWindow last;
        private static DateTime lastAt;

        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);

        internal static Func<List<OpenWindow>> Shown = delegate
        {
            List<OpenWindow> open = new List<OpenWindow>();
            foreach (OpenWindow window in Monitors.Windows()) if (!IsIconic(window.Handle)) open.Add(window);
            return open;
        };
        internal static Func<IntPtr, bool> Folded = IsIconic;
        internal static Func<IntPtr, bool> Unfold = delegate(IntPtr handle) { return ShowWindowAsync(handle, 4); };
        internal static Func<IntPtr, bool> Raise = Hands.Front;
        internal static Func<IntPtr, bool> Alive = IsWindow;
        internal static Func<bool> Gaming = delegate { return Lockout.Active; };

        internal static void Forget()
        {
            lock (gate)
            {
                shownBefore = new List<OpenWindow>();
                last = null;
            }
        }

        internal static void Before()
        {
            List<OpenWindow> open;
            try { open = Shown(); }
            catch (Exception) { open = new List<OpenWindow>(); }
            lock (gate) shownBefore = open;
        }

        internal static string After(string task, DateTime now)
        {
            List<OpenWindow> was;
            lock (gate)
            {
                was = shownBefore;
                shownBefore = new List<OpenWindow>();
            }
            List<OpenWindow> gone = new List<OpenWindow>();
            foreach (OpenWindow window in was)
                if (Alive(window.Handle) && Folded(window.Handle)) gone.Add(window);
            if (gone.Count == 0) return "";
            Minimized(gone[gone.Count - 1], now);
            if (Wanted.IsMatch(task ?? "")) return "";
            List<string> names = new List<string>();
            foreach (OpenWindow window in gone)
            {
                if (!Unfold(window.Handle)) continue;
                names.Add(Name(window));
                ActivityLog.Write("hands", "put back", Name(window) + " (minimized during the job)");
            }
            if (names.Count == 0) return "";
            return " I put " + string.Join(" and ", names.ToArray()) + " back: " + (names.Count == 1 ? "it" : "they") + " got minimized while I worked.";
        }

        internal static void Minimized(OpenWindow window, DateTime now)
        {
            if (window == null) return;
            lock (gate)
            {
                last = window;
                lastAt = now;
            }
        }

        internal static string Bring(DateTime now)
        {
            OpenWindow window;
            DateTime at;
            lock (gate)
            {
                window = last;
                at = lastAt;
            }
            if (window == null || (now - at).TotalMinutes > KeepMinutes || !Alive(window.Handle)) return "There's nothing I minimized lately, so there's nothing to bring back.";
            if (Gaming()) return Lockout.Refusal;
            if (!Raise(window.Handle)) return "I couldn't bring " + Name(window) + " back.";
            ActivityLog.Write("hands", "brought back", Name(window));
            return "Brought " + Name(window) + " back.";
        }

        private static string Name(OpenWindow window)
        {
            string title = Regex.Replace(window.Title ?? "", @"\s+", " ").Trim();
            if (Incognito.IsMatch(title)) return "your private window";
            Match app = ChatApp.Match((window.Program ?? "") + " " + title);
            if (app.Success) return char.ToUpperInvariant(app.Value[0]) + app.Value.Substring(1).ToLowerInvariant();
            if (MailWords.IsMatch(title)) return "your mail";
            if (title.Length == 0) title = string.IsNullOrEmpty(window.Program) ? "the window" : window.Program;
            return title.Length > MostName ? title.Substring(0, MostName).Trim() + "..." : title;
        }
    }
}
