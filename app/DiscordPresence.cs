using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed class PresenceFriend
    {
        internal string Id = "", Name = "", Note = "";
        internal bool Plays;
    }

    internal sealed class PresenceLive
    {
        internal string Status = "offline";
        internal bool Desktop, DeskSeen, Mobile, Known;
        internal DateTime Since = DateTime.MinValue, DeskOff = DateTime.MinValue;
    }

    internal sealed class PresenceHeads
    {
        internal string Id = "", Name = "", Text = "";
        internal DateTime When;
    }

    internal sealed class PresenceState
    {
        public List<string> Watched = new List<string>();
        public List<string> Online = new List<string>();
        public Dictionary<string, string> Told = new Dictionary<string, string>();
        public Dictionary<string, string> Written = new Dictionary<string, string>();
        public string Learned;
    }

    internal static class DiscordPresence
    {
        internal const string Lead = "Usually on Discord: ";
        internal const int KeepDays = 28;
        internal const int OfflineMinutes = 30;
        internal const int FreshMinutes = 10;
        internal const double UsualShare = 0.5;
        private const string Day = "yyyy-MM-dd";
        private static readonly Regex PlaysLine = new Regex(@"^\s*[-*]?\s*plays\s*:", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly HashSet<string> Everyone = new HashSet<string>(("anyone anybody someone somebody who everyone everybody friends my friends any of my friends").Split(' '));
        private static readonly object gate = new object();
        private static readonly Dictionary<string, PresenceLive> live = new Dictionary<string, PresenceLive>();
        private static readonly Dictionary<string, string> voice = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> rooms = new Dictionary<string, string>();
        private static readonly List<PresenceHeads> heads = new List<PresenceHeads>();
        private static List<PresenceFriend> friends = new List<PresenceFriend>();
        private static PresenceState state;
        private static string loadedFrom;
        private static bool snapshotSeen;
        internal static string Stored;

        internal static string FilePath
        {
            get { return Stored ?? (ActivityLog.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ActivityLog.FilePath), "discord-presence.json")); }
        }

        internal static List<PresenceFriend> Friends { get { lock (gate) return new List<PresenceFriend>(friends); } }

        internal static List<string> InCallWithHim(string owner)
        {
            List<string> names = new List<string>();
            lock (gate)
            {
                string his = null;
                if (!string.IsNullOrEmpty(owner)) voice.TryGetValue(owner, out his);
                foreach (PresenceFriend friend in friends)
                {
                    string seat;
                    if (!voice.TryGetValue(friend.Id, out seat)) continue;
                    if (his != null && seat != his) continue;
                    if (!names.Contains(friend.Name)) names.Add(friend.Name);
                }
            }
            return names;
        }

        internal static bool HeInCall(string owner)
        {
            if (string.IsNullOrEmpty(owner)) return false;
            lock (gate)
            {
                string his;
                if (!voice.TryGetValue(owner, out his) || his == null) return false;
                foreach (PresenceFriend friend in friends)
                {
                    string seat;
                    if (voice.TryGetValue(friend.Id, out seat) && seat == his) return true;
                }
            }
            return false;
        }

        internal static void Forget()
        {
            lock (gate)
            {
                live.Clear();
                voice.Clear();
                rooms.Clear();
                heads.Clear();
                friends = new List<PresenceFriend>();
                state = null;
                loadedFrom = null;
                snapshotSeen = false;
            }
        }

        private static PresenceState Current()
        {
            string path = FilePath;
            if (state != null && loadedFrom == path) return state;
            loadedFrom = path;
            state = null;
            if (path != null && File.Exists(path))
            {
                try { state = new JavaScriptSerializer { MaxJsonLength = 8000000 }.Deserialize<PresenceState>(File.ReadAllText(path)); }
                catch (Exception error) { Brain.Log("discord-presence.json", -1, error.GetType().Name + ": " + error.Message); }
            }
            if (state == null) state = new PresenceState();
            if (state.Watched == null) state.Watched = new List<string>();
            if (state.Online == null) state.Online = new List<string>();
            if (state.Told == null) state.Told = new Dictionary<string, string>();
            if (state.Written == null) state.Written = new Dictionary<string, string>();
            return state;
        }

        private static void Save()
        {
            string path = FilePath;
            if (path == null || state == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".new";
                File.WriteAllText(temporary, new JavaScriptSerializer { MaxJsonLength = 8000000 }.Serialize(state));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) { Brain.Log("discord-presence.json", -1, error.GetType().Name + ": " + error.Message); }
        }

        internal static List<PresenceFriend> Map(List<DiscordMember> members, string root)
        {
            List<PresenceFriend> found = new List<PresenceFriend>();
            if (members == null || string.IsNullOrEmpty(root)) return found;
            string folder = Path.Combine(root, "People");
            string[] notes;
            try { notes = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.md") : new string[0]; }
            catch (IOException) { return found; }
            catch (UnauthorizedAccessException) { return found; }
            foreach (string note in notes)
            {
                if (Vault.Conflict(note)) continue;
                string body = Vault.Text(note);
                if (body == null) continue;
                string title = Path.GetFileNameWithoutExtension(note);
                List<string> accounts = DiscordFriends.FromNote(body);
                List<DiscordMember> hits = new List<DiscordMember>();
                foreach (DiscordMember member in members)
                {
                    bool exact = accounts.Count > 0
                        ? accounts.Exists(delegate(string account) { string want = DiscordFriends.Account(account); return want.Length > 0 && (DiscordFriends.Account(member.User) == want || DiscordFriends.Account(member.Nick) == want || DiscordFriends.Account(member.Global) == want); })
                        : Names.Fold(title).Length > 0 && (Names.Fold(member.Nick) == Names.Fold(title) || Names.Fold(member.Global) == Names.Fold(title) || Names.Fold(member.User) == Names.Fold(title));
                    if (exact && !hits.Contains(member)) hits.Add(member);
                }
                if (hits.Count != 1 || found.Exists(delegate(PresenceFriend known) { return known.Id == hits[0].Id; })) continue;
                found.Add(new PresenceFriend { Id = hits[0].Id, Name = title, Note = note, Plays = PlaysLine.IsMatch(body) });
            }
            return found;
        }

        internal static void Track(List<PresenceFriend> mapped)
        {
            Track(mapped, DateTime.Now);
        }

        internal static void Track(List<PresenceFriend> mapped, DateTime now)
        {
            lock (gate)
            {
                friends = mapped ?? new List<PresenceFriend>();
                if (snapshotSeen)
                    foreach (PresenceFriend friend in friends)
                    {
                        PresenceLive one;
                        if (!live.TryGetValue(friend.Id, out one) || !one.Known) live[friend.Id] = new PresenceLive { Known = true, Since = now, DeskOff = now };
                    }
                PresenceState kept = Current();
                int before = kept.Online.Count;
                kept.Online.RemoveAll(delegate(string entry) { string id = entry.Split('|')[0]; return !friends.Exists(delegate(PresenceFriend friend) { return friend.Id == id; }); });
                if (kept.Online.Count != before) Save();
            }
        }

        internal static void Took(GatewayStep step, DateTime now, bool headsOn)
        {
            if (step == null) return;
            lock (gate)
            {
                if (step.Opened)
                {
                    snapshotSeen = false;
                    foreach (PresenceLive one in live.Values) one.Known = false;
                    voice.Clear();
                }
                foreach (KeyValuePair<string, string> room in step.Channels) rooms[room.Key] = room.Value;
                foreach (GatewayVoice seat in step.Voices)
                {
                    if (seat.Channel.Length == 0) voice.Remove(seat.User);
                    else voice[seat.User] = seat.Channel;
                }
                foreach (GatewayPresence presence in step.Presences) Update(presence, step.Snapshot || step.Opened, now, headsOn);
                if (!step.Snapshot) return;
                snapshotSeen = true;
                foreach (PresenceFriend friend in friends)
                {
                    PresenceLive one;
                    if (live.TryGetValue(friend.Id, out one) && one.Known) continue;
                    live[friend.Id] = new PresenceLive { Known = true, Since = now, DeskOff = now };
                }
            }
        }

        private static bool Ready(string status)
        {
            return status == "online" || status == "dnd";
        }

        private static void Update(GatewayPresence presence, bool snapshot, DateTime now, bool headsOn)
        {
            PresenceLive before;
            live.TryGetValue(presence.User, out before);
            bool desktop = Ready(presence.Desktop);
            bool seen = presence.Desktop.Length > 0 && presence.Desktop != "offline";
            PresenceLive after = new PresenceLive
            {
                Status = presence.Status.Length == 0 ? "offline" : presence.Status,
                Desktop = desktop,
                DeskSeen = seen,
                Mobile = presence.Mobile.Length > 0 && presence.Mobile != "offline",
                Known = true
            };
            after.Since = before != null && before.Known && before.Status == after.Status ? before.Since : now;
            after.DeskOff = seen ? DateTime.MinValue : before != null && before.Known && !before.DeskSeen ? before.DeskOff : now;
            live[presence.User] = after;
            if (snapshot || !headsOn || !desktop || before == null || !before.Known || before.DeskSeen || (now - before.DeskOff).TotalMinutes < OfflineMinutes) return;
            PresenceFriend friend = friends.Find(delegate(PresenceFriend known) { return known.Id == presence.User; });
            if (friend == null) return;
            string told;
            if (Current().Told.TryGetValue(friend.Id, out told) && told == now.ToString(Day, CultureInfo.InvariantCulture)) return;
            if (heads.Exists(delegate(PresenceHeads known) { return known.Id == friend.Id; })) return;
            heads.Add(new PresenceHeads { Id = friend.Id, Name = friend.Name, Text = friend.Name + " just came online on Discord.", When = now });
        }

        internal static PresenceHeads NextHeads(DateTime now)
        {
            lock (gate)
            {
                heads.RemoveAll(delegate(PresenceHeads one) { return (now - one.When).TotalMinutes > FreshMinutes; });
                return heads.Count == 0 ? null : heads[0];
            }
        }

        internal static void Drop(PresenceHeads one)
        {
            lock (gate) heads.Remove(one);
        }

        internal static List<PresenceFriend> OnlineFriends()
        {
            lock (gate)
                return friends.FindAll(delegate(PresenceFriend friend)
                {
                    PresenceLive one;
                    return live.TryGetValue(friend.Id, out one) && one.Known && one.Status != "offline";
                });
        }

        internal static void Told(PresenceHeads one, DateTime now)
        {
            lock (gate)
            {
                heads.Remove(one);
                Current().Told[one.Id] = now.ToString(Day, CultureInfo.InvariantCulture);
                Save();
            }
        }

        internal static void Sample(DateTime now)
        {
            if (!DiscordGateway.Live || DiscordGateway.PresencesOff) return;
            lock (gate)
            {
                if (friends.Count == 0) return;
                PresenceState kept = Current();
                string day = now.ToString(Day, CultureInfo.InvariantCulture);
                string hour = now.Hour.ToString("00", CultureInfo.InvariantCulture);
                string watched = day + "|" + hour;
                if (!kept.Watched.Contains(watched)) kept.Watched.Add(watched);
                foreach (PresenceFriend friend in friends)
                {
                    PresenceLive one;
                    bool on = live.TryGetValue(friend.Id, out one) && one.Known && Ready(one.Status);
                    if (!on && !voice.ContainsKey(friend.Id)) continue;
                    string entry = friend.Id + "|" + watched;
                    if (!kept.Online.Contains(entry)) kept.Online.Add(entry);
                }
                string oldest = now.AddDays(-KeepDays).ToString(Day, CultureInfo.InvariantCulture);
                kept.Watched.RemoveAll(delegate(string entry) { return string.CompareOrdinal(entry.Split('|')[0], oldest) < 0; });
                kept.Online.RemoveAll(delegate(string entry) { string[] parts = entry.Split('|'); return parts.Length < 3 || string.CompareOrdinal(parts[1], oldest) < 0; });
                Save();
            }
        }

        internal static string Usual(string id)
        {
            lock (gate) return Usual(Current(), id);
        }

        internal static string Usual(PresenceState kept, string id)
        {
            List<string> parts = new List<string>();
            foreach (bool weekend in new[] { false, true })
            {
                Dictionary<int, HashSet<string>> watched = new Dictionary<int, HashSet<string>>();
                Dictionary<int, HashSet<string>> online = new Dictionary<int, HashSet<string>>();
                HashSet<string> days = new HashSet<string>();
                foreach (string entry in kept.Watched) Count(entry, null, weekend, watched, days);
                foreach (string entry in kept.Online) Count(entry, id, weekend, online, null);
                if (days.Count < (weekend ? 2 : 3)) continue;
                List<int> hours = new List<int>();
                for (int h = 0; h < 24; h++)
                {
                    HashSet<string> seen, on;
                    if (!watched.TryGetValue(h, out seen) || seen.Count < 2 || !online.TryGetValue(h, out on)) continue;
                    if (on.Count >= seen.Count * UsualShare) hours.Add(h);
                }
                string ranges = Ranges(hours);
                if (ranges != null) parts.Add((weekend ? "weekends " : "weekdays ") + ranges);
            }
            return parts.Count == 0 ? null : Lead + string.Join(", ", parts.ToArray());
        }

        private static void Count(string entry, string id, bool weekend, Dictionary<int, HashSet<string>> into, HashSet<string> days)
        {
            string[] parts = entry.Split('|');
            int at = id == null ? 0 : 1;
            if (parts.Length < at + 2 || (id != null && parts[0] != id)) return;
            DateTime day;
            int hour;
            if (!DateTime.TryParseExact(parts[at], Day, CultureInfo.InvariantCulture, DateTimeStyles.None, out day) || !int.TryParse(parts[at + 1], out hour)) return;
            bool isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
            if (isWeekend != weekend) return;
            HashSet<string> set;
            if (!into.TryGetValue(hour, out set)) into[hour] = set = new HashSet<string>();
            set.Add(parts[at]);
            if (days != null) days.Add(parts[at]);
        }

        internal static string Ranges(List<int> hours)
        {
            if (hours.Count == 0) return null;
            if (hours.Count >= 24) return "all day";
            bool[] on = new bool[24];
            foreach (int h in hours) on[h] = true;
            int start = 0;
            while (on[start]) start++;
            List<string> found = new List<string>();
            int step = 1;
            while (step <= 24)
            {
                int h = (start + step) % 24;
                if (!on[h]) { step++; continue; }
                int length = 0;
                while (on[(h + length) % 24]) length++;
                found.Add(h.ToString("00", CultureInfo.InvariantCulture) + "-" + ((h + length) % 24).ToString("00", CultureInfo.InvariantCulture));
                step += length;
            }
            return string.Join(" and ", found.ToArray());
        }

        internal static int Learn(DateTime now)
        {
            List<PresenceFriend> mapped;
            PresenceState kept;
            lock (gate)
            {
                kept = Current();
                string today = now.ToString(Day, CultureInfo.InvariantCulture);
                if (kept.Learned == today || friends.Count == 0) return 0;
                kept.Learned = today;
                mapped = new List<PresenceFriend>(friends);
                Save();
            }
            int written = 0;
            foreach (PresenceFriend friend in mapped)
            {
                string line;
                string had;
                lock (gate)
                {
                    line = Usual(kept, friend.Id);
                    kept.Written.TryGetValue(friend.Id, out had);
                }
                if (line == null || line == had) continue;
                bool changed;
                lock (Names.Gate) changed = NoteEdit.SetLine(friend.Note, Lead.TrimEnd(), line);
                lock (gate)
                {
                    kept.Written[friend.Id] = line;
                    Save();
                }
                if (!changed) continue;
                written++;
                ActivityLog.Write("discord", "usual online times noted", friend.Name);
            }
            return written;
        }

        internal static Func<string, bool> Friend = Tracked;

        private static bool Tracked(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            try
            {
                foreach (NameHit hit in Names.Find(name))
                {
                    if (hit.Self || hit.Note == null) continue;
                    lock (gate)
                        if (friends.Exists(delegate(PresenceFriend known) { return string.Equals(known.Note, hit.Note, StringComparison.OrdinalIgnoreCase); })) return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        internal static string Answer(string name, DateTime now)
        {
            if (!Discord.Ready) return "My Discord bot isn't set up yet. Say connect my Discord first.";
            if (DiscordGateway.PresencesOff) return "I can't see who's online on Discord: the Presence intent is off for my bot in the Discord Developer Portal.";
            if (!DiscordGateway.Live) return "I can't see Discord status right now; my live connection to Discord isn't up.";
            string wanted = Names.Fold((name ?? "").Trim());
            lock (gate)
            {
                if (wanted.Length == 0 || Everyone.Contains(wanted))
                {
                    if (friends.Count == 0) return "None of your People notes match anyone in our Discord server yet, so I can't tell who's on.";
                    List<string> on = new List<string>();
                    foreach (PresenceFriend friend in friends)
                    {
                        PresenceLive one;
                        string room = Room(friend.Id);
                        bool known = live.TryGetValue(friend.Id, out one) && one.Known;
                        if (known && one.Status != "offline") on.Add(friend.Name + " (" + Describe(one, now) + Seated(room) + ")");
                        else if (room != null) on.Add(friend.Name + " (" + Hidden(room) + ")");
                    }
                    return on.Count == 0 ? "None of your friends with a People note are on Discord right now." : "On Discord now: " + string.Join(", ", on.ToArray()) + ".";
                }
                PresenceFriend match = null;
                foreach (NameHit hit in Names.Find(name))
                {
                    if (hit.Self || hit.Note == null) continue;
                    PresenceFriend found = friends.Find(delegate(PresenceFriend known) { return string.Equals(known.Note, hit.Note, StringComparison.OrdinalIgnoreCase); });
                    if (found != null) { match = found; break; }
                }
                if (match == null) return "I only see Discord status for friends whose People note matches their Discord account, and I can't match " + name.Trim() + " to anyone in our server.";
                string usual = Usual(Current(), match.Id);
                string tail = usual == null ? "" : " They're usually on " + usual.Substring(Lead.Length) + ".";
                PresenceLive status;
                string seat = Room(match.Id);
                bool seen = live.TryGetValue(match.Id, out status) && status.Known;
                if (seat != null && (!seen || status.Status == "offline")) return match.Name + " shows " + Hidden(seat) + "." + tail;
                if (!seen) return "I haven't seen " + match.Name + "'s Discord status since I connected." + tail;
                return match.Name + " is " + Describe(status, now) + Seated(seat) + " on Discord." + tail;
            }
        }

        private static string Room(string id)
        {
            string channel;
            if (!voice.TryGetValue(id, out channel)) return null;
            string name;
            return rooms.TryGetValue(channel, out name) && !string.IsNullOrWhiteSpace(name) ? "the voice channel \"" + name + "\"" : "a voice channel";
        }

        private static string Seated(string room)
        {
            return room == null ? "" : ", in " + room;
        }

        private static string Hidden(string room)
        {
            return "offline, but in " + room + " right now (they may be invisible)";
        }

        private static string Describe(PresenceLive one, DateTime now)
        {
            string since = one.Since > DateTime.MinValue && one.Since.Date == now.Date ? " since " + one.Since.ToString("HH:mm", CultureInfo.InvariantCulture) : "";
            if (one.Status == "offline") return "offline" + since;
            string word = one.Status == "dnd" ? "on do not disturb" : one.Status;
            return word + (!one.Desktop && one.Mobile ? ", on their phone" : "") + since;
        }
    }
}
