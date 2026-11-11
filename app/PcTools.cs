using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class PcTools
    {
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);

        internal static Func<PcUse> Use = Usage.Read;
        internal static Func<string, NetUse> Net = Network.Read;
        internal static Func<IntPtr, bool> Minimized = IsIconic;
        internal static Func<string, bool> Running = ProcessRunning;
        internal static Func<string> Monitors = Arrange.Monitors;

        internal const string ListSpec = "See what's open on their PC right now: each open window's title, trimmed, and its program, which one is in front and which are minimized. Give name to see only one app's windows. Use it before you switch to, move or close something you aren't sure is open. It only reads; it changes nothing.";
        internal const string ListSchema = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"Only this app's windows, if they named one\"}}}";
        internal const string CheckSpec = "Check whether an app or game is installed on their PC, by the name open_app would open, and whether it's running: with a window open, in front or minimized, or in the background with no window, like Discord in the tray. Use it when they ask whether something is installed or running, and before opening something that might already be open. It only reads; it changes nothing.";
        internal const string CheckSchema = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"The app's name\"}},\"required\":[\"name\"]}";
        internal const string PcSpec = "See how their PC is doing right now, read over half a second: how busy its processor is, how much memory is in use, the apps using the most of either, each drive's free space, and the battery on a laptop. Use it when they say their PC or a game is slow, laggy or stuttering, or ask what's using their CPU, memory or disk space, before guessing why. Give name to see one app's use. It only reads; it changes nothing.";
        internal const string PcSchema = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"One app to look at, if they named one\"}}}";
        internal const string NetSpec = "Check their internet right now: whether they're on Ethernet or Wi-Fi, how four pings to a public address went, how long they took and how many were lost, and whether web addresses look up. Use it when they ask if their internet is down, why a site won't load, or what their ping is, and with pc_status when a game lags, to tell their PC from their connection. Give address to ping a site or server they named. It only reads; it changes nothing.";
        internal const string NetSchema = "{\"type\":\"object\",\"properties\":{\"address\":{\"type\":\"string\",\"description\":\"A site or server to ping instead, only if they named one, like google.com\"}}}";

        internal const string Off = "My PC and internet checks are switched off in Capri+ (PC CHECKS), choom.";

        internal static string Do(string tool, ToolArguments args)
        {
            if (Array.IndexOf(new[] { "pc_status", "internet_status", "list_windows", "check_app" }, tool) < 0) return null;
            if (!ForkSettings.Current.PcChecks) return Off;
            if (tool == "pc_status") return PcStatus(args.Name);
            if (tool == "internet_status") return InternetStatus(string.IsNullOrWhiteSpace(args.Address) ? args.Site : args.Address);
            if (tool == "list_windows") return ListWindows(args.Name);
            if (tool == "check_app") return CheckApp(args.Name);
            return null;
        }

        private static string Plain(string text) { return Regex.Replace((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim(); }

        internal static string InternetStatus(string address)
        {
            NetUse net;
            try { net = Net(address); }
            catch (Exception) { net = null; }
            if (net == null) return "Couldn't check their internet just now.";
            if (net.Links.Count == 0) return "Their PC isn't connected to any network: no Ethernet or Wi-Fi is up.";
            string over = "over " + string.Join(" and ", net.Links.ToArray());
            if (net.Sent == 0) return "Their PC is connected " + over + ", but couldn't ping anything just now.";
            if (net.Lost == net.Sent) return "Their PC is connected " + over + ", but nothing answered: all " + net.Sent + " pings to " + net.Pinged + " were lost" + (net.Names == true ? ", though web addresses still look up." : net.Names == false ? ", and web addresses don't look up either, so the internet looks down." : ".");
            string said = (net.Lost == 0 ? "Their internet is working, " + over + ": " : "Their internet is dropping out, " + over + ": " + net.Lost + " of " + net.Sent + " pings to " + net.Pinged + " were lost, and the rest ")
                + (net.Lost == 0 ? "pings to " + net.Pinged + " took " : "took ") + net.Average + " ms on average, " + net.Fastest + " ms at best";
            if (net.Names == false) return said + ", but web addresses don't look up, so sites won't load: their DNS is the problem.";
            return said + (net.Names == true ? ", and web addresses look up fine." : ".");
        }

        internal static string PcStatus(string name)
        {
            PcUse use;
            try { use = Use(); }
            catch (Exception) { use = null; }
            if (use == null) return "Couldn't read how their PC is doing just now.";
            string said = "Their PC right now: the processor is " + (int)Math.Round(use.Cpu) + "% busy" + (use.MemoryTotal > 0 ? ", and " + Usage.Size(use.MemoryUsed) + " of its " + Usage.Size(use.MemoryTotal) + " of memory is in use (" + (int)Math.Round(100.0 * use.MemoryUsed / use.MemoryTotal) + "%)." : ".");
            if (!string.IsNullOrWhiteSpace(name))
            {
                string want = Plain(Regex.Replace(name.Trim(), @"\.exe$", "", RegexOptions.IgnoreCase)).Replace(" ", "");
                List<AppUse> asked = use.Apps.FindAll(delegate(AppUse app)
                {
                    string program = Plain(app.App).Replace(" ", "");
                    return want.Length > 0 && (program == want || want.Length >= 4 && program.StartsWith(want, StringComparison.Ordinal));
                });
                if (asked.Count == 0) return "Nothing called " + name.Trim() + " is running. " + said;
                List<string> each = new List<string>();
                foreach (AppUse app in asked) each.Add(app.App + " is using " + (int)Math.Round(app.Cpu) + "% of the processor and " + Usage.Size(app.Memory) + " of memory");
                return string.Join("; ", each.ToArray()) + ". " + said;
            }
            List<AppUse> busiest = use.Apps.FindAll(delegate(AppUse app) { return app.Cpu >= 1; });
            busiest.Sort(delegate(AppUse a, AppUse b) { return b.Cpu.CompareTo(a.Cpu); });
            if (busiest.Count > 5) busiest.RemoveRange(5, busiest.Count - 5);
            List<AppUse> heaviest = new List<AppUse>(use.Apps);
            heaviest.Sort(delegate(AppUse a, AppUse b) { return b.Memory.CompareTo(a.Memory); });
            List<string> named = new List<string>();
            foreach (AppUse app in busiest) named.Add(app.App + " " + (int)Math.Round(app.Cpu) + "% of the processor and " + Usage.Size(app.Memory));
            int added = 0;
            foreach (AppUse app in heaviest)
            {
                if (added == 3) break;
                if (busiest.Contains(app)) continue;
                named.Add(app.App + " " + Usage.Size(app.Memory) + " of memory");
                added++;
            }
            if (named.Count > 0) said += " Using the most: " + string.Join("; ", named.ToArray()) + ".";
            if (use.Drives.Count > 0) said += " Drives: " + string.Join("; ", use.Drives.ToArray()) + ".";
            if (use.Battery != null) said += " Battery " + use.Battery + ".";
            return said;
        }

        internal static string ListWindows(string named)
        {
            List<OpenWindow> open;
            IntPtr front;
            try { open = Hands.Windows(); front = Hands.Foreground(); }
            catch (Exception error) { return "Couldn't read their windows just now: " + error.Message; }
            if (open == null || open.Count == 0) return "No app windows are open on their PC.";
            int top = open.FindIndex(delegate(OpenWindow window) { return window.Handle == front; });
            if (top < 0) top = 0;
            List<OpenWindow> order = new List<OpenWindow> { open[top] };
            for (int i = 0; i < open.Count; i++) if (i != top) order.Add(open[i]);
            string want = Regex.Replace(Plain(named), @"^(?:the|my|your|our) |( app| application| game| window| windows)$", "");
            if (want.Length > 0)
            {
                order = order.FindAll(delegate(OpenWindow window) { return Plain(window.Program).Contains(want) || Plain(window.Title).Contains(want); });
                if (order.Count == 0) return "No window of " + named.Trim() + " is open.";
                return "Windows of " + named.Trim() + ", nearest the front first: " + string.Join("; ", Described(order, open[top]).ToArray()) + ".";
            }
            List<string> all = Described(order, open[top]);
            return "In front: " + all[0] + "." + (all.Count == 1 ? " Nothing else is open." : "\nAlso open, nearest the front first: " + string.Join("; ", all.GetRange(1, all.Count - 1).ToArray()) + ".") + Monitored();
        }

        private static string Monitored()
        {
            try { return Monitors() ?? ""; }
            catch (Exception) { return ""; }
        }

        private static List<string> Described(List<OpenWindow> windows, OpenWindow front)
        {
            List<string> said = new List<string>(), seen = new List<string>();
            int more = 0;
            foreach (OpenWindow window in windows)
            {
                string title = (window.Title ?? "").Trim();
                if (title.Length > 60) title = title.Substring(0, 57) + "...";
                string named = (title.Length == 0 ? window.Program : title) + " (" + window.Program + ")";
                if (seen.Contains(named)) continue;
                seen.Add(named);
                if (said.Count >= 16) { more++; continue; }
                bool minimized;
                try { minimized = Minimized(window.Handle); }
                catch (Exception) { minimized = false; }
                said.Add(named + (minimized ? ", minimized" : "") + (window == front ? ", in front" : ""));
            }
            if (more > 0) said.Add("and " + more + " more");
            return said;
        }

        internal static string CheckApp(string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted)) return "Which app, choom?";
            wanted = wanted.Trim();
            List<InstalledApp> found;
            try
            {
                found = Hands.Match(Hands.Apps(), wanted);
                string nicknamed = found.Count == 0 ? Hands.Nickname(wanted) : null;
                if (nicknamed != null) found = Hands.Match(Hands.Apps(), nicknamed);
            }
            catch (Exception error) { return "Couldn't read their apps just now: " + error.Message; }
            InstalledApp chosen = null;
            foreach (InstalledApp app in found) if (chosen == null || Hands.AppName(app).Length < Hands.AppName(chosen).Length) chosen = app;
            string name = chosen == null ? wanted : chosen.Name;
            List<OpenWindow> open;
            IntPtr front;
            try { open = Hands.Windows() ?? new List<OpenWindow>(); front = Hands.Foreground(); }
            catch (Exception) { open = new List<OpenWindow>(); front = IntPtr.Zero; }
            OpenWindow shown = Hands.OpenedWindow(open, name) ?? (name == wanted ? null : Hands.OpenedWindow(open, wanted));
            string where = chosen == null ? "No app called " + wanted + " is installed on this PC" : name + " is installed";
            if (shown != null)
            {
                bool minimized;
                try { minimized = Minimized(shown.Handle); }
                catch (Exception) { minimized = false; }
                return where + (chosen == null ? ", but a window of it is open: " : " and open: ") + (shown.Title ?? shown.Program) + (shown.Handle == front ? " is in front." : minimized ? " is minimized." : " is open behind other windows.");
            }
            bool running;
            try { running = Running(name); }
            catch (Exception) { running = false; }
            if (running) return where + (chosen == null ? ", but something by that name is running with no window open." : " and running in the background with no window open, maybe in the tray.");
            return where + (chosen == null ? "." : " and isn't running.");
        }

        private static bool ProcessRunning(string name)
        {
            string plain = Plain(name).Replace(" ", "");
            if (plain.Length < 3) return false;
            Process[] all = Process.GetProcesses();
            try
            {
                foreach (Process process in all)
                {
                    string running;
                    try { running = Plain(process.ProcessName).Replace(" ", ""); }
                    catch (Exception) { continue; }
                    if (running == plain || (plain.Length >= 4 && running.StartsWith(plain, StringComparison.Ordinal))) return true;
                }
            }
            finally { foreach (Process process in all) process.Dispose(); }
            return false;
        }
    }
}