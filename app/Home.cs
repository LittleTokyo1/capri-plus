using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CapriDesktop
{
    internal enum HomeChange { None, Left, Came }

    internal sealed class HomeDevice
    {
        internal string Ip;
        internal string Mac;
    }

    internal sealed class HomeWatch
    {
        private readonly double minutes;
        private DateTime anchor;
        private bool started;
        private bool known;
        private bool home;

        internal HomeWatch(int minutes) : this(minutes, false) { }

        internal HomeWatch(int minutes, bool away)
        {
            this.minutes = minutes;
            if (!away) return;
            known = true;
            home = false;
        }

        internal bool Gone { get { return known && !home; } }

        internal HomeChange Update(DateTime now, bool seen)
        {
            if (!started)
            {
                started = true;
                anchor = now;
            }
            if (seen)
            {
                anchor = now;
                bool cameBack = known && !home;
                home = true;
                known = true;
                return cameBack ? HomeChange.Came : HomeChange.None;
            }
            if ((now - anchor).TotalMinutes >= minutes && (home || !known))
            {
                home = false;
                known = true;
                return HomeChange.Left;
            }
            return HomeChange.None;
        }
    }

    internal static class Home
    {
        internal const int AwayMinutes = 10;
        internal const int CheckSeconds = 60;
        private const int SweepMilliseconds = 300;
        private const int SweepWait = 3000;
        private const string Broadcast = "ff-ff-ff-ff-ff-ff";
        private static readonly Regex Mac = new Regex(@"^([0-9a-f]{2})[:-]([0-9a-f]{2})[:-]([0-9a-f]{2})[:-]([0-9a-f]{2})[:-]([0-9a-f]{2})[:-]([0-9a-f]{2})$", RegexOptions.IgnoreCase);
        private static readonly Regex Ip = new Regex(@"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$");

        internal static void Away(ForkSettings settings)
        {
            if (settings.HomeAway) return;
            settings.HomeAway = true;
            settings.HomeProactive = settings.Proactive;
            settings.Proactive = 0;
        }

        internal static void Back(ForkSettings settings)
        {
            if (!settings.HomeAway) return;
            settings.HomeAway = false;
            if (settings.Proactive == 0) settings.Proactive = Math.Max(0, Math.Min(2, settings.HomeProactive));
        }

        internal static bool Leaves(bool gone, bool alreadyAway, bool awayFromPc)
        {
            return gone && !alreadyAway && awayFromPc;
        }

        internal static string Address(string text)
        {
            string clean = (text ?? "").Trim();
            Match mac = Mac.Match(clean);
            if (mac.Success) return string.Join("-", new[] { mac.Groups[1].Value, mac.Groups[2].Value, mac.Groups[3].Value, mac.Groups[4].Value, mac.Groups[5].Value, mac.Groups[6].Value }).ToLowerInvariant();
            Match ip = Ip.Match(clean);
            if (!ip.Success) return null;
            for (int i = 1; i <= 4; i++) if (int.Parse(ip.Groups[i].Value) > 255) return null;
            return clean;
        }

        internal static bool Seen(string arp, string wanted)
        {
            string address = Address(wanted);
            if (address == null || address == Broadcast || string.IsNullOrEmpty(arp)) return false;
            foreach (string line in arp.Split('\n'))
            {
                string[] part = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (part.Length < 3 || Address(part[0]) == null || Address(part[1]) == null || Address(part[1]) == Broadcast) continue;
                if (part[0] == address || Address(part[1]) == address) return true;
            }
            return false;
        }

        internal static string Scan(string address)
        {
            try
            {
                if (Ip.IsMatch(address)) Run("ping", "-n 1 -w 400 " + address);
                else Sweep();
                return Run("arp", "-a");
            }
            catch (Exception) { return null; }
        }

        internal static bool Private(string mac)
        {
            string address = Address(mac);
            return address != null && Ip.IsMatch(address) == false && "26ae".IndexOf(address[1]) >= 0;
        }

        internal static List<HomeDevice> Devices(string arp)
        {
            List<HomeDevice> found = new List<HomeDevice>();
            foreach (string line in (arp ?? "").Split('\n'))
            {
                string[] part = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (part.Length < 3 || !Ip.IsMatch(part[0])) continue;
                string mac = Address(part[1]);
                if (mac == null || mac == Broadcast || !Private(mac)) continue;
                found.Add(new HomeDevice { Ip = part[0], Mac = mac });
            }
            return found;
        }

        internal static bool SeenAny(string arp, string phone, string ip)
        {
            if (Seen(arp, phone)) return true;
            return !string.IsNullOrEmpty(ip) && Devices(arp).Exists(device => device.Ip == ip);
        }

        internal static HomeDevice Relearn(List<HomeDevice> before, List<HomeDevice> now, string savedIp)
        {
            List<HomeDevice> fresh = now.FindAll(device => !before.Exists(old => old.Mac == device.Mac));
            if (fresh.Count == 1) return fresh[0];
            return string.IsNullOrEmpty(savedIp) ? null : fresh.Find(device => device.Ip == savedIp);
        }

        private static void Sweep()
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                IPInterfaceProperties properties = adapter.GetIPProperties();
                if (properties.GatewayAddresses.Count == 0) continue;
                foreach (UnicastIPAddressInformation info in properties.UnicastAddresses)
                {
                    if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] mine = info.Address.GetAddressBytes();
                    List<Task> pings = new List<Task>();
                    for (int host = 1; host < 255; host++)
                    {
                        if (host == mine[3]) continue;
                        pings.Add(new Ping().SendPingAsync(new IPAddress(new[] { mine[0], mine[1], mine[2], (byte)host }), SweepMilliseconds));
                    }
                    try { Task.WaitAll(pings.ToArray(), SweepWait); }
                    catch (AggregateException) { }
                }
            }
        }

        private static string Run(string file, string arguments)
        {
            ProcessStartInfo start = new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using (Process process = Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(5000)) { process.Kill(); return null; }
                return output;
            }
        }
    }
}
