using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokePcTools(List<string> checks)
        {
            Func<PcUse> useWas = PcTools.Use;
            Func<string, NetUse> netWas = PcTools.Net;
            Func<IntPtr, bool> minWas = PcTools.Minimized;
            Func<string, bool> runWas = PcTools.Running;
            Func<List<OpenWindow>> windowsWas = Hands.Windows;
            Func<IntPtr> frontWas = Hands.Foreground;
            try
            {
                ForkSettings fresh = new ForkSettings();
                fresh.Tidy();
                if (!fresh.PcChecks) throw new Exception("PC checks should be on by default.");
                bool checksWas = ForkSettings.Current.PcChecks;
                ForkSettings.Current.PcChecks = false;
                string off = PcTools.Do("pc_status", new ToolArguments());
                ForkSettings.Current.PcChecks = checksWas;
                if (off != PcTools.Off || PcTools.Do("open_app", new ToolArguments()) != null) throw new Exception("PC checks ran with their switch off, or took another tool.");
                PcUse busy = new PcUse { Cpu = 87.4, MemoryUsed = 12L << 30, MemoryTotal = 32L << 30, Battery = null };
                busy.Apps.Add(new AppUse { App = "League of Legends", Cpu = 41, Memory = 3L << 30 });
                busy.Apps.Add(new AppUse { App = "chrome", Cpu = 22, Memory = 4L << 30 });
                busy.Apps.Add(new AppUse { App = "Discord", Cpu = 0.2, Memory = 1L << 30 });
                busy.Drives.Add("C: 120 GB free of 1 TB");
                PcTools.Use = delegate { return busy; };
                string pc = PcTools.PcStatus(null);
                if (!pc.StartsWith("Their PC right now: the processor is 87% busy", StringComparison.Ordinal) || pc.IndexOf("League of Legends 41%", StringComparison.Ordinal) < 0 || pc.IndexOf("Discord", StringComparison.Ordinal) < 0 || pc.IndexOf("C: 120 GB free", StringComparison.Ordinal) < 0) throw new Exception("pc_status read wrong: " + pc);
                if (PcTools.PcStatus("chrome").IndexOf("chrome is using 22% of the processor", StringComparison.Ordinal) != 0) throw new Exception("pc_status for one app read wrong.");
                if (!PcTools.PcStatus("valorant").StartsWith("Nothing called valorant is running.", StringComparison.Ordinal)) throw new Exception("pc_status made up an app that isn't running.");
                PcTools.Use = delegate { throw new InvalidOperationException("no counters"); };
                if (PcTools.PcStatus(null) != "Couldn't read how their PC is doing just now.") throw new Exception("A failed PC read wasn't said honestly.");

                NetUse down = new NetUse { Pinged = "1.1.1.1", Sent = 4, Lost = 4, Names = false };
                down.Links.Add("Wi-Fi");
                NetUse good = new NetUse { Pinged = "1.1.1.1", Sent = 4, Lost = 0, Average = 18, Fastest = 15, Names = true };
                good.Links.Add("Ethernet");
                NetUse none = new NetUse();
                string pinged = null;
                PcTools.Net = delegate(string address) { pinged = address; return down; };
                if (PcTools.InternetStatus(null).IndexOf("internet looks down", StringComparison.Ordinal) < 0) throw new Exception("A dead connection wasn't called down.");
                PcTools.Net = delegate(string address) { pinged = address; return good; };
                ToolArguments asked = new ToolArguments { Site = "euw.leagueoflegends.com" };
                if (PcTools.Do("internet_status", asked) != "Their internet is working, over Ethernet: pings to 1.1.1.1 took 18 ms on average, 15 ms at best, and web addresses look up fine." || pinged != "euw.leagueoflegends.com") throw new Exception("A good connection or the named server was read wrong.");
                PcTools.Net = delegate { return none; };
                if (PcTools.InternetStatus(null) != "Their PC isn't connected to any network: no Ethernet or Wi-Fi is up.") throw new Exception("No network wasn't said.");

                OpenWindow game = new OpenWindow(new IntPtr(11), "League of Legends (TM) Client", "League of Legends");
                OpenWindow spotify = new OpenWindow(new IntPtr(12), "Spotify Premium", "Spotify");
                OpenWindow discord = new OpenWindow(new IntPtr(13), "#general - Discord", "Discord");
                Hands.Windows = delegate { return new List<OpenWindow> { spotify, game, discord }; };
                Hands.Foreground = delegate { return new IntPtr(11); };
                PcTools.Minimized = delegate(IntPtr handle) { return handle == new IntPtr(13); };
                string listed = PcTools.ListWindows(null);
                if (!listed.StartsWith("In front: League of Legends (TM) Client (League of Legends), in front.", StringComparison.Ordinal) || listed.IndexOf("#general - Discord (Discord), minimized", StringComparison.Ordinal) < 0) throw new Exception("list_windows read wrong: " + listed);
                if (PcTools.ListWindows("spotify").IndexOf("Spotify Premium (Spotify)", StringComparison.Ordinal) < 0 || PcTools.ListWindows("steam") != "No window of steam is open.") throw new Exception("list_windows for one app read wrong.");
                PcTools.Running = delegate { return false; };
                Hands.Windows = delegate { return new List<OpenWindow>(); };
                if (PcTools.CheckApp("zzqq not an app") != "No app called zzqq not an app is installed on this PC.") throw new Exception("check_app made up an app.");
                foreach (string tool in new[] { "pc_status", "internet_status", "list_windows", "check_app" })
                {
                    if (!Hands.Tools.Contains("\"name\":\"" + tool + "\"")) throw new Exception("Her brain isn't offered " + tool + ".");
                    if (Lockout.Handles(tool)) throw new Exception(tool + " only reads, yet the game lockout refuses it.");
                }
            }
            finally
            {
                PcTools.Use = useWas;
                PcTools.Net = netWas;
                PcTools.Minimized = minWas;
                PcTools.Running = runWas;
                Hands.Windows = windowsWas;
                Hands.Foreground = frontWas;
            }
            checks.Add("PASS everything is a tool, first slice (096-07): a PC checks switch (on), pc_status names CPU, memory, the busiest apps and drives (one app too, a missing app or a failed read said honestly), internet_status tells down, working and no network and pings a server he named, list_windows puts the front window first with minimized ones marked and filters by app, check_app never makes up an app, all four offered to her brain and allowed in a game (they only read)");
        }
    }
}