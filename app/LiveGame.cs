using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class LiveData
    {
        [DataMember(Name = "activePlayer")] public LiveMe Me;
        [DataMember(Name = "allPlayers")] public LivePlayer[] Players;
        [DataMember(Name = "events")] public LiveEvents Events;
        [DataMember(Name = "gameData")] public LiveClock Clock;
    }

    [DataContract]
    internal sealed class LiveMe
    {
        [DataMember(Name = "riotId")] public string RiotId;
        [DataMember(Name = "riotIdGameName")] public string GameName;
        [DataMember(Name = "summonerName")] public string Summoner;
        [DataMember(Name = "currentGold")] public double Gold;
        [DataMember(Name = "level")] public int Level;
        [DataMember(Name = "championStats")] public LiveStats Stats;
    }

    [DataContract]
    internal sealed class LiveStats
    {
        [DataMember(Name = "currentHealth")] public double Health;
        [DataMember(Name = "maxHealth")] public double MaxHealth;
    }

    [DataContract]
    internal sealed class LivePlayer
    {
        [DataMember(Name = "championName")] public string Champion;
        [DataMember(Name = "riotId")] public string RiotId;
        [DataMember(Name = "riotIdGameName")] public string GameName;
        [DataMember(Name = "summonerName")] public string Summoner;
        [DataMember(Name = "team")] public string Team;
        [DataMember(Name = "level")] public int Level;
        [DataMember(Name = "position")] public string Position;
        [DataMember(Name = "isDead")] public bool Dead;
        [DataMember(Name = "scores")] public LiveScores Scores;
        [DataMember(Name = "items")] public LiveItem[] Items;
    }

    [DataContract]
    internal sealed class LiveScores
    {
        [DataMember(Name = "kills")] public int Kills;
        [DataMember(Name = "deaths")] public int Deaths;
        [DataMember(Name = "assists")] public int Assists;
        [DataMember(Name = "creepScore")] public int Farm;
    }

    [DataContract]
    internal sealed class LiveItem
    {
        [DataMember(Name = "displayName")] public string Name;
        [DataMember(Name = "itemID")] public int Id;
    }

    [DataContract]
    internal sealed class LiveEvents
    {
        [DataMember(Name = "Events")] public LiveEvent[] List;
    }

    [DataContract]
    internal sealed class LiveEvent
    {
        [DataMember(Name = "EventID")] public int Id;
        [DataMember(Name = "EventName")] public string Name;
        [DataMember(Name = "EventTime")] public double Time;
        [DataMember(Name = "TurretKilled")] public string Turret;
        [DataMember(Name = "InhibKilled")] public string Inhibitor;
        [DataMember(Name = "KillerName")] public string Killer;
        [DataMember(Name = "VictimName")] public string Victim;
        [DataMember(Name = "Recipient")] public string Recipient;
        [DataMember(Name = "Acer")] public string Acer;
        [DataMember(Name = "AcingTeam")] public string AcingTeam;
        [DataMember(Name = "KillStreak")] public int Streak;
        [DataMember(Name = "DragonType")] public string Dragon;
        [DataMember(Name = "Stolen")] public string Stolen;
        [DataMember(Name = "Result")] public string Result;
        [DataMember(Name = "Assisters")] public string[] Assisters;
    }

    [DataContract]
    internal sealed class LiveClock
    {
        [DataMember(Name = "gameTime")] public double Time;
        [DataMember(Name = "gameMode")] public string Mode;
        [DataMember(Name = "mapNumber")] public int Map;
    }

    // League's official Live Client Data API (port 2999, only while a match runs): read-only game state and events,
    // the same data overlays like Porofessor use. She never sends anything to the game.
    internal static class LiveGame
    {
        internal const string Opening = "They're in a League of Legends match, ";
        internal const string Honest = " Only state match facts that are in this data; if something isn't here, say you don't know.";
        internal const string FarmRule = " Their cs/min above is read from the game (about 5 to 10 is normal, never a fraction of one): quote it as it is, never work out your own.";
        internal static readonly System.Text.RegularExpressions.Regex CsAsked = new System.Text.RegularExpressions.Regex(@"\b(my|our)\s+(cs|creep ?score|farm(ing)?)\b|\bcs\s*(/|per|a|each)\s*min|\bcs\s+per\s+minute\b|\bhow many cs\b|\bcreep ?score\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        private const int RecentEvents = 12;
        internal const double LowHealth = 0.10, HealthyAgain = 0.30;
        internal const double ClutchSeconds = 5;
        internal static readonly int[] FarmMarks = { 10, 15, 20 };
        internal const double FarmPace = 6.0;
        internal const double NoFarmSeconds = 300;
        internal const string Address = "https://127.0.0.1:2999/liveclientdata/allgamedata";
        internal static Func<string> Fetch = Download;

        private static string Download()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Address);
            request.Timeout = 2000;
            request.ReadWriteTimeout = 2000;
            // The game signs this loopback API with Riot's own certificate, so only this request skips the usual check.
            request.ServerCertificateValidationCallback = delegate { return true; };
            try
            {
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException) { return null; }
        }

        internal static LiveData Read() { return League.Parse<LiveData>(Fetch()); }

        private static bool Named(string name, LivePlayer player)
        {
            if (string.IsNullOrEmpty(name) || player == null) return false;
            return name == player.GameName || name == player.Summoner || name == player.RiotId || (player.RiotId != null && player.RiotId.StartsWith(name + "#", StringComparison.Ordinal));
        }

        internal static LivePlayer Mine(LiveData data)
        {
            if (data == null || data.Me == null || data.Players == null) return null;
            foreach (LivePlayer player in data.Players)
                if (Named(data.Me.GameName, player) || Named(data.Me.Summoner, player) || Named(data.Me.RiotId, player)) return player;
            return null;
        }

        internal static LivePlayer Find(LiveData data, string name)
        {
            if (data == null || data.Players == null) return null;
            foreach (LivePlayer player in data.Players) if (Named(name, player)) return player;
            return null;
        }

        private static string Items(LivePlayer player)
        {
            List<string> names = new List<string>();
            foreach (LiveItem item in player.Items ?? new LiveItem[0]) if (!string.IsNullOrEmpty(item.Name)) names.Add(item.Name);
            return names.Count == 0 ? "no items" : string.Join(", ", names.ToArray());
        }

        private static string Score(LivePlayer player)
        {
            LiveScores score = player.Scores ?? new LiveScores();
            return score.Kills + "/" + score.Deaths + "/" + score.Assists + ", " + score.Farm + " cs";
        }

        // Their game in a few lines, for her prompt: "In game, 15 min in. You're Zed, level 9, 3/1/2, 80 cs, 1234 gold, items: ..."
        internal static string Describe(LiveData data)
        {
            LivePlayer me = Mine(data);
            if (me == null) return "";
            StringBuilder said = new StringBuilder();
            int minutes = data.Clock == null ? 0 : (int)(data.Clock.Time / 60);
            said.Append(Opening).Append(minutes).Append(" min in. They're ").Append(me.Champion).Append(" this game (any other champion named earlier in the talk was another game or a question), level ").Append(me.Level)
                .Append(", ").Append(Score(me)).Append(" (").Append(FarmRate(me, data)).Append(" cs/min), ").Append((int)data.Me.Gold).Append(" gold, items: ").Append(Items(me)).Append('.');
            List<string> enemies = new List<string>(), allies = new List<string>();
            foreach (LivePlayer player in data.Players)
            {
                if (player == me) continue;
                string line = player.Champion + " (" + Score(player) + "; " + Items(player) + ")";
                if (player.Team == me.Team) allies.Add(line); else enemies.Add(line);
            }
            if (enemies.Count > 0) said.Append(" Enemies: ").Append(string.Join("; ", enemies.ToArray())).Append('.');
            if (allies.Count > 0) said.Append(" Allies: ").Append(string.Join("; ", allies.ToArray())).Append('.');
            string recent = Recent(data, me);
            if (recent.Length > 0) said.Append(" Recent events (game clock): ").Append(recent).Append('.');
            return said.Append(Honest).Append(FarmRule).ToString();
        }

        // His own words for "what's my CS", answered from the game's number with no model in between, or null.
        internal static string CsAnswer(LiveData data)
        {
            LivePlayer me = Mine(data);
            if (me == null || me.Scores == null || data.Clock == null || data.Clock.Time < 60) return null;
            return "About " + FarmRate(me, data) + " CS per minute: " + me.Scores.Farm + " CS at " + Clock(data.Clock.Time) + ", straight from the game.";
        }

        internal static double FarmPerMinute(LivePlayer player, LiveData data)
        {
            double minutes = data == null || data.Clock == null ? 0 : data.Clock.Time / 60;
            return minutes <= 0 || player == null || player.Scores == null ? 0 : player.Scores.Farm / minutes;
        }

        private static string FarmRate(LivePlayer player, LiveData data) { return FarmPerMinute(player, data).ToString("0.0", CultureInfo.InvariantCulture); }

        private static string Clock(double seconds)
        {
            int whole = Math.Max(0, (int)seconds);
            return (whole / 60) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        // Who did it, for the event list: a champion, or what the game calls a turret or a minion wave.
        private static string Doer(LiveData data, string name)
        {
            LivePlayer player = Find(data, name);
            if (player != null) return player.Champion;
            if (!string.IsNullOrEmpty(name) && name.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0) return "a turret";
            return "minions";
        }

        // Turret_T1_... and Barracks_T1_... belong to ORDER, T2 to CHAOS.
        private static string Owner(string structure)
        {
            if (string.IsNullOrEmpty(structure)) return null;
            if (structure.IndexOf("_T1_", StringComparison.Ordinal) >= 0 || structure.EndsWith("_T1", StringComparison.Ordinal)) return "ORDER";
            if (structure.IndexOf("_T2_", StringComparison.Ordinal) >= 0 || structure.EndsWith("_T2", StringComparison.Ordinal)) return "CHAOS";
            return null;
        }

        private static string Side(LivePlayer me, string team) { return team == null ? "a" : team == me.Team ? "their team's" : "an enemy"; }

        private static string Drake(string type)
        {
            switch (type)
            {
                case "Fire": return "Infernal";
                case "Earth": return "Mountain";
                case "Water": return "Ocean";
                case "Air": return "Cloud";
                default: return string.IsNullOrEmpty(type) ? "a" : type;
            }
        }

        private static string Taker(LiveData data, LivePlayer me, LiveEvent happened)
        {
            LivePlayer killer = Find(data, happened.Killer);
            if (killer == null) return "someone";
            return killer.Team == me.Team ? "their own team" : "the enemy team";
        }

        private static string Fact(LiveData data, LivePlayer me, LiveEvent happened)
        {
            string stolen = happened.Stolen == "True" ? " (stolen)" : "";
            switch (happened.Name)
            {
                case "ChampionKill":
                    List<string> helpers = new List<string>();
                    foreach (string name in happened.Assisters ?? new string[0]) helpers.Add(Doer(data, name));
                    return Doer(data, happened.Killer) + " killed " + Doer(data, happened.Victim) + (helpers.Count > 0 ? " (assists: " + string.Join(", ", helpers.ToArray()) + ")" : "");
                case "Multikill":
                    return Doer(data, happened.Killer) + " got a " + (happened.Streak >= 5 ? "pentakill" : happened.Streak == 4 ? "quadra kill" : happened.Streak == 3 ? "triple kill" : "double kill");
                case "FirstBlood":
                    return Doer(data, happened.Recipient) + " drew first blood";
                case "DragonKill":
                    return Taker(data, me, happened) + " took the " + Drake(happened.Dragon) + " dragon" + stolen;
                case "BaronKill":
                    return Taker(data, me, happened) + " took Baron" + stolen;
                case "HeraldKill":
                    return Taker(data, me, happened) + " took the Rift Herald" + stolen;
                case "TurretKilled":
                    return Doer(data, happened.Killer) + " destroyed " + Side(me, Owner(happened.Turret)) + " turret";
                case "InhibKilled":
                    return Doer(data, happened.Killer) + " destroyed " + Side(me, Owner(happened.Inhibitor)) + " inhibitor";
                case "Ace":
                    return (happened.AcingTeam == me.Team ? "their own team" : "the enemy team") + " got an ace";
                default:
                    return null;
            }
        }

        // The last few notable events with their game-clock time, so she reads the match instead of guessing it.
        internal static string Recent(LiveData data, LivePlayer me)
        {
            if (data == null || me == null || data.Events == null || data.Events.List == null) return "";
            List<string> facts = new List<string>();
            foreach (LiveEvent happened in data.Events.List)
            {
                string fact = happened == null ? null : Fact(data, me, happened);
                if (fact != null) facts.Add(Clock(happened.Time) + " " + fact);
            }
            if (facts.Count > RecentEvents) facts.RemoveRange(0, facts.Count - RecentEvents);
            return string.Join("; ", facts.ToArray());
        }

        // Health as a share of the maximum, or -1 when they're dead or the game gave no number.
        internal static double HealthShare(LiveData data)
        {
            LivePlayer me = Mine(data);
            if (me == null || me.Dead || data.Me == null || data.Me.Stats == null || data.Me.Stats.MaxHealth <= 0 || data.Me.Stats.Health <= 0) return -1;
            return data.Me.Stats.Health / data.Me.Stats.MaxHealth;
        }

        // A gentle nudge when they're behind on farm at 10, 15 and 20 minutes (not for junglers and supports), else null.
        internal static string Farm(LiveData data, int mark)
        {
            LivePlayer me = Mine(data);
            if (me == null || string.IsNullOrEmpty(me.Position)) return null;
            string role = me.Position.ToUpperInvariant();
            if (role == "JUNGLE" || role == "UTILITY" || !Objectives.Rift(data)) return null;
            if (me.Scores == null || (me.Scores.Farm == 0 && data.Clock.Time >= NoFarmSeconds)) return null;
            double pace = FarmPerMinute(me, data);
            if (Math.Round(pace, 1) >= FarmPace) return null;
            return "Their farm is behind at " + mark + " minutes: " + pace.ToString("0.0", CultureInfo.InvariantCulture) + " cs/min as " + me.Champion + " (about 6 is a good pace). Mention it gently, no nagging.";
        }

        // The "still alive" check for a near-death: they dropped under 10% health and five game seconds later they're still up.
        // Fires once per scare and re-arms after they heal past 30% or die.
        internal sealed class Scare
        {
            private double since = -1;
            private double lowest = -1;
            private bool told;

            internal void Reset() { since = -1; lowest = -1; told = false; }

            internal string Watch(LiveData data)
            {
                LivePlayer me = Mine(data);
                double share = HealthShare(data);
                if (me == null || data.Clock == null || share < 0) { Reset(); return null; }
                double now = data.Clock.Time;
                if (share >= HealthyAgain) { Reset(); return null; }
                if (told) return null;
                if (share < LowHealth && since < 0) since = now;
                if (since >= 0 && (lowest < 0 || data.Me.Stats.Health < lowest)) lowest = data.Me.Stats.Health;
                if (since < 0 || now - since < ClutchSeconds) return null;
                told = true;
                return "They just barely survived a fight as " + me.Champion + ", down to " + Math.Max(1, (int)Math.Ceiling(lowest)).ToString(CultureInfo.InvariantCulture) + " HP at the lowest. React like a friend watching would, in your own words: no percentages, and don't open with their champion's name.";
            }
        }

        // What a fresh event means to them, or null when it isn't worth a word at this chattiness (1 key moments, 2 chatty).
        // Key moments: their multikills, first blood, aces, steals and Baron by their team, and the result. Plain kills, deaths and assists are chatty only.
        internal static string Moment(LiveEvent happened, LiveData data, int chattiness)
        {
            LivePlayer me = Mine(data);
            if (happened == null || me == null || chattiness <= 0) return null;
            string mine = me.Champion;
            LivePlayer killer = Find(data, happened.Killer), victim = Find(data, happened.Victim);
            bool byMe = killer == me, onMe = victim == me;
            bool assisted = happened.Assisters != null && Array.Exists(happened.Assisters, name => Find(data, name) == me);
            bool theirs = killer != null && killer.Team == me.Team, stolen = happened.Stolen == "True";
            switch (happened.Name)
            {
                case "ChampionKill":
                    if (chattiness < 2) return null;
                    if (byMe) return "They (" + mine + ") just killed " + (victim != null ? victim.Champion : "an enemy") + ".";
                    if (onMe) return "They (" + mine + ") just died to " + (killer != null ? killer.Champion : "a turret or minions") + ".";
                    if (assisted) return "They (" + mine + ") assisted a kill on " + (victim != null ? victim.Champion : "an enemy") + ".";
                    return null;
                case "Multikill":
                    return byMe ? "They (" + mine + ") just got a " + (happened.Streak >= 5 ? "PENTAKILL" : happened.Streak == 4 ? "quadra kill" : happened.Streak == 3 ? "triple kill" : "double kill") + "!" : null;
                case "FirstBlood":
                    return Find(data, happened.Recipient) == me ? "They (" + mine + ") got first blood." : null;
                case "Ace":
                    return happened.AcingTeam == me.Team ? "Their own team just aced the enemy team." : chattiness >= 2 ? "Their own team just got aced." : null;
                case "BaronKill":
                    if (killer == null || (!theirs && chattiness < 2)) return null;
                    return (theirs ? "Their own team took Baron" : "The enemy team took Baron") + (stolen ? ", stolen!" : ".");
                case "DragonKill":
                case "HeraldKill":
                    if (killer == null || (chattiness < 2 && !(theirs && stolen))) return null;
                    string prize = happened.Name == "HeraldKill" ? "Rift Herald" : (happened.Dragon ?? "") + " dragon";
                    return (theirs ? "Their own team took the " : "The enemy team took the ") + prize + (stolen ? ", stolen!" : ".");
                case "GameEnd":
                    return happened.Result == "Win" ? "They just WON the game as " + mine + "." : "They just lost the game as " + mine + ".";
                default:
                    return null;
            }
        }
    }
}
