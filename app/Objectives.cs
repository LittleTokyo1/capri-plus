using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    // Spoken objective calls on Summoner's Rift: dragon (Elder after a soul), Baron and Voidgrubs, a minute before they spawn.
    // Timers from the League wiki for patch 26.x (checked 2026-10-31): drake 5:00, again 5:00 after a kill; Elder 6:00 after a
    // team's fourth drake and 6:00 after an Elder; Baron 20:00, again 6:00 after a kill; Voidgrubs once at 8:00.
    internal sealed class Objectives
    {
        internal const double Lead = 60;
        internal const double Window = 15;
        internal const double DragonFirst = 300, DragonAgain = 300, ElderAgain = 360;
        internal const double BaronFirst = 1200, BaronAgain = 360;
        internal const double GrubsFirst = 480;
        internal const int Soul = 4;
        private readonly List<string> called = new List<string>();

        internal void Reset() { lock (called) called.Clear(); }

        internal static bool Rift(LiveData data)
        {
            return data != null && data.Clock != null && data.Clock.Mode == "CLASSIC" && (data.Clock.Map == 11 || data.Clock.Map == 0);
        }

        // When the next dragon spawns and whether it is the Elder, from every dragon kill since the start.
        internal static double NextDragon(LiveData data, out bool elder)
        {
            elder = false;
            double next = DragonFirst;
            Dictionary<string, int> drakes = new Dictionary<string, int>();
            foreach (LiveEvent happened in Events(data, "DragonKill"))
            {
                if (happened.Dragon == "Elder") { elder = true; next = happened.Time + ElderAgain; continue; }
                LivePlayer killer = LiveGame.Find(data, happened.Killer);
                string team = killer != null && !string.IsNullOrEmpty(killer.Team) ? killer.Team : "?";
                drakes[team] = (drakes.ContainsKey(team) ? drakes[team] : 0) + 1;
                if (team != "?" && drakes[team] >= Soul) elder = true;
                next = happened.Time + (elder ? ElderAgain : DragonAgain);
            }
            return next;
        }

        internal static double NextBaron(LiveData data)
        {
            double next = BaronFirst;
            foreach (LiveEvent happened in Events(data, "BaronKill")) next = happened.Time + BaronAgain;
            return next;
        }

        private static List<LiveEvent> Events(LiveData data, string name)
        {
            List<LiveEvent> found = new List<LiveEvent>();
            if (data != null && data.Events != null && data.Events.List != null)
                foreach (LiveEvent happened in data.Events.List) if (happened != null && happened.Name == name) found.Add(happened);
            found.Sort(delegate(LiveEvent a, LiveEvent b) { return a.Time.CompareTo(b.Time); });
            return found;
        }

        // The call due now (once per spawn, only in the minute before it), or null.
        internal string Turn(LiveData data)
        {
            if (!Rift(data)) return null;
            double now = data.Clock.Time;
            bool elder;
            double dragon = NextDragon(data, out elder);
            List<string> said = new List<string>();
            Due(said, now, dragon, (elder ? "elder@" : "dragon@") + (int)dragon, elder ? "Elder Dragon spawns in one minute." : "Dragon spawns in one minute.");
            double baron = NextBaron(data);
            Due(said, now, baron, "baron@" + (int)baron, "Baron spawns in one minute.");
            Due(said, now, GrubsFirst, "grubs", "Voidgrubs spawn in one minute.");
            return said.Count == 0 ? null : string.Join(" ", said.ToArray());
        }

        private void Due(List<string> said, double now, double spawn, string key, string line)
        {
            double from = spawn - Lead;
            if (now < from || now >= from + Window) return;
            lock (called)
            {
                if (called.Contains(key)) return;
                called.Add(key);
            }
            said.Add(line);
        }
    }
}
