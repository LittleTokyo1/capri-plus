using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CapriDesktop
{
    // While a game runs, her hands are off the keyboard and the mouse: no clicks, keys or typing, whatever Blackwall says.
    // Anti-cheat can't tell her input from a bot's, so this is never a setting. Everything else (opening apps and sites, music,
    // window moves, talking, looking, advice) stays allowed.
    internal static class Lockout
    {
        internal const string Refusal = "Hands off mid-match, choom. Anti-cheat doesn't know I'm friendly. I'll do it after your game.";
        private static readonly string[] Handsy = { "use_computer", "stream", "shortcut", "draw", "mini_game", "gaming_mode", "clone", "desktop_icons", "fake_crash" };
        private static readonly string[] LeagueLocked = { "ChampSelect", "GameStart", "InProgress", "Reconnect" };
        internal static Func<string, bool> Running = delegate(string name)
        {
            Process[] found = Process.GetProcessesByName(name);
            foreach (Process process in found) process.Dispose();
            return found.Length > 0;
        };
        internal static volatile string LeaguePhase;
        private static readonly object gate = new object();
        private static DateTime checkedAt = DateTime.MinValue;
        private static string game;

        internal static string Game
        {
            get
            {
                lock (gate)
                {
                    if ((DateTime.UtcNow - checkedAt).TotalMilliseconds < 1500) return game;
                    checkedAt = DateTime.UtcNow;
                    game = Find();
                    return game;
                }
            }
        }

        internal static bool Active { get { return Game != null; } }

        internal static void Recheck() { lock (gate) checkedAt = DateTime.MinValue; }

        private static string Find()
        {
            ForkSettings settings = ForkSettings.Current;
            string phase = LeaguePhase;
            if (settings.LockLeagueClient && phase != null && Array.IndexOf(LeagueLocked, phase) >= 0) return "League of Legends";
            foreach (string name in Watched(settings.Games))
            {
                try { if (Running(name)) return name; }
                catch (Exception) { }
            }
            return null;
        }

        // The standard games are always watched, whatever the list says, and "game.exe" is read as "game".
        internal static List<string> Watched(string[] extra)
        {
            List<string> names = new List<string>();
            foreach (string[] list in new[] { ForkSettings.StandardGames, extra ?? new string[0] })
                foreach (string entry in list)
                {
                    string name = (entry ?? "").Trim();
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4).Trim();
                    if (name.Length > 0 && !names.Exists(delegate(string had) { return string.Equals(had, name, StringComparison.OrdinalIgnoreCase); })) names.Add(name);
                }
            return names;
        }

        // A tool that would press a key, click or type.
        internal static bool Handles(string tool) { return Array.IndexOf(Handsy, tool) >= 0; }

        internal static string Refuse(string tool)
        {
            if (!Handles(tool) || !Active) return null;
            return Refusal;
        }

        // Media keys reach Windows, not the game, so volume and play/pause still work mid-match.
        internal static bool MediaKey(int key) { return key >= 0xAD && key <= 0xB3; }
    }
}
