using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal static partial class Discord
    {
        internal static string Get(string path, out int status) { return Api("GET", path, null, out status); }

        internal static string PostJson(string path, string json, out int status) { return Api("POST", path, json, out status); }

        internal static string Why(int status) { return Problem(status); }
    }

    internal sealed class DiscordMember
    {
        internal string Id, User, Global, Nick;

        internal string Label { get { return !string.IsNullOrEmpty(Nick) ? Nick : !string.IsNullOrEmpty(Global) ? Global : User; } }
    }

    internal static class DiscordFriends
    {
        internal const int MostChars = 1500;
        internal const int MostPerHour = 8;
        private const int CacheMinutes = 5;
        private const int MostGuilds = 3;
        private static readonly Regex Asking = new Regex(@"\b(message|messages|text|dm|tell|ask|ping|write|send|let|say|contact|discord|invite)\b", RegexOptions.IgnoreCase);
        private static readonly Regex Replying = new Regex(@"\b(reply|answer|respond)\b", RegexOptions.IgnoreCase);
        private static readonly object gate = new object();
        private static readonly List<DateTime> sentAt = new List<DateTime>();
        private static List<DiscordMember> cached;
        private static DateTime cachedAt = DateTime.MinValue;

        internal static void Forget()
        {
            lock (gate)
            {
                cached = null;
                cachedAt = DateTime.MinValue;
                sentAt.Clear();
            }
        }

        internal static Func<string, string> ShotToHim = delegate { return "I can't take a screenshot right now."; };

        internal static string Do(ToolArguments args)
        {
            if (args != null && args.Action == "online") return DiscordPresence.Answer(args.Name, DateTime.Now);
            if (args != null && args.Action == "received") return DiscordInbox.Recent(Vault.Root);
            if (args != null && args.Action == "screenshot") return ShotToHim(args.Name);
            if (args != null && args.Action == "when_online") return FriendOrders.Tool(args.Name, args.Text, Brain.Asking, DiscordPresence.Friends, DiscordPresence.OnlineFriends(), DateTime.Now);
            if (args == null || args.Action != "message") return "Use message with a name and the text.";
            string[] parts = Regex.Split((args.Text ?? "").Trim(), @"\s*\[next\]\s*", RegexOptions.IgnoreCase);
            if (parts.Length > 1)
            {
                List<string> results = new List<string>();
                for (int i = 0; i < parts.Length && i < 4; i++)
                    if (parts[i].Length > 0) results.Add(Do(new ToolArguments { Action = "message", Name = args.Name, Text = parts[i] }));
                return string.Join(" ", results.ToArray());
            }
            if (Himself(args.Name)) return ToHim(args.Text);
            FriendLine wrote = DiscordInbox.Pick(args.Name, DateTime.Now);
            if (wrote != null) return To(wrote, args.Text);
            List<string> accounts = new List<string>();
            bool noteFound = false;
            foreach (NameHit hit in Names.Find(args.Name ?? ""))
            {
                string body = hit.Self ? null : Vault.Text(hit.Note);
                if (body == null) continue;
                noteFound = true;
                accounts.AddRange(FromNote(body));
            }
            string result = Message(args.Name, args.Text, accounts);
            return result.StartsWith("I couldn't find", StringComparison.Ordinal) ? result + Hint(noteFound, accounts) : result;
        }

        internal static bool Himself(string name)
        {
            string plain = Regex.Replace((name ?? "").Trim().ToLowerInvariant(), @"[^a-z0-9_ ]", "").Trim();
            if (Regex.IsMatch(plain, @"^(me|myself|my dm|my dms|my discord|my account|my own account)$")) return true;
            foreach (NameHit hit in Names.Find(name ?? "")) if (hit.Self) return true;
            string handle = Names.Fold((name ?? "").Trim()).TrimStart('@').TrimEnd('_');
            if (handle.Length == 0) return false;
            string notes = Notes();
            foreach (string mine in FromNote(notes)) if (Names.Fold(mine).TrimEnd('_') == handle) return true;
            return OwnName(notes, name);
        }

        private static readonly Regex NameSaid = new Regex(@"(?:^[-*\s]*(?:full |real )?name\s*:\s*|\b(?:his|her|their|my) (?:full |real )?name is\s+)([\p{L}][\p{L}'\-]*(?: [\p{L}][\p{L}'\-]*){0,3})", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private const int ShortestName = 3;

        internal static bool OwnName(string notes, string name)
        {
            string wanted = Names.Fold((name ?? "").Trim());
            if (wanted.Length < ShortestName) return false;
            foreach (Match said in NameSaid.Matches(notes ?? ""))
            {
                string full = Names.Fold(said.Groups[1].Value.Trim());
                if (full == wanted) return true;
                foreach (string part in full.Split(' ')) if (part.Length >= ShortestName && part == wanted) return true;
            }
            return false;
        }

        private static string Notes()
        {
            try { return Brain.MemoryPath != null && File.Exists(Brain.MemoryPath) ? File.ReadAllText(Brain.MemoryPath) : ""; }
            catch (IOException) { return ""; }
        }

        internal static Func<string, bool> SendHim = Discord.Send;

        internal static string ToHim(string text)
        {
            if (!Discord.Ready) return "My Discord bot isn't set up yet. Say connect my Discord first.";
            string clean;
            string refused = Refused(text, false, out clean);
            if (refused != null) return refused;
            if (!SendHim(clean)) return "I couldn't send that to your Discord DM.";
            ActivityLog.Write("discord", "message to him", null);
            return "Sent to you on Discord: \"" + (clean.Length > 160 ? clean.Substring(0, 157) + "..." : clean) + "\"";
        }

        internal static string Hint(bool noteFound, List<string> accounts)
        {
            if (accounts != null && accounts.Count > 0) return " His note says Discord: " + string.Join(", ", accounts.ToArray()) + ", but nobody with that name is in our server.";
            if (noteFound) return " I found him in your notes, but there is no Discord line in his note. Tell me his Discord name and I'll note it down.";
            return " He isn't in your notes yet. Tell me his Discord name and I'll note it down.";
        }

        internal static List<string> FromNote(string body)
        {
            List<string> accounts = new List<string>();
            foreach (string raw in (body ?? "").Split('\n'))
            {
                Match line = Regex.Match(raw.Trim().TrimStart('-', '*', ' '), @"^discord\s*:\s*(.+)$", RegexOptions.IgnoreCase);
                if (!line.Success) continue;
                foreach (string part in Regex.Split(Regex.Replace(line.Groups[1].Value, @"\s*\(\[\[.*$", ""), @"\s*(?:,|;|&|\band\b)\s*", RegexOptions.IgnoreCase))
                {
                    string name = Account(part);
                    if (name.Length > 0 && !accounts.Contains(name)) accounts.Add(name);
                }
            }
            return accounts;
        }

        internal static List<DiscordMember> MemberList(out string problem)
        {
            return Members(out problem);
        }

        internal static string Account(string text)
        {
            string name = Names.Fold((text ?? "").Trim()).TrimStart('@');
            int hash = name.IndexOf('#');
            return (hash >= 0 ? name.Substring(0, hash) : name).Trim();
        }

        internal static List<DiscordMember> Match(List<DiscordMember> members, string who, List<string> accounts)
        {
            List<DiscordMember> found = new List<DiscordMember>();
            if (accounts != null)
            {
                foreach (string account in accounts)
                    foreach (DiscordMember member in members)
                        if (Account(member.User) == Account(account) && !found.Contains(member)) found.Add(member);
                if (found.Count == 0)
                    foreach (string account in accounts)
                        foreach (DiscordMember member in members)
                            if (Account(account).Length > 0 && (Account(member.Nick) == Account(account) || Account(member.Global) == Account(account)) && !found.Contains(member)) found.Add(member);
            }
            if (found.Count > 0) return found;
            string wanted = Account(who);
            found = members.FindAll(delegate(DiscordMember m) { return Account(m.Nick) == wanted || Account(m.Global) == wanted || Account(m.User) == wanted; });
            if (found.Count > 0 || wanted.Length < 3) return found;
            return members.FindAll(delegate(DiscordMember m) { return Names.NameWord(m.Nick, who) || Names.NameWord(m.Global, who) || Names.NameStarts(m.Nick, who) || Names.NameStarts(m.Global, who); });
        }

        internal static string Check(string text, string asked)
        {
            string clean;
            return Refused(text, false, asked, out clean);
        }

        private static string Refused(string text, bool replying, out string clean)
        {
            return Refused(text, replying, Brain.Asking, out clean);
        }

        private static string Refused(string text, bool replying, string asked, out string clean)
        {
            clean = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            if (clean.Length == 0) return "What should I say?";
            if (string.IsNullOrEmpty(asked) || !(Asking.IsMatch(asked) || (replying && Replying.IsMatch(asked)))) return "I only message people when you ask me to, in your own words. Say message, text or tell, then the name, and I will.";
            if (Honest.Instructive(clean)) return "That reads like your instruction, not a message. I'll write the words to send myself.";
            if (clean.Length > MostChars) clean = clean.Substring(0, MostChars - 3) + "...";
            return null;
        }

        internal static string To(FriendLine line, string text)
        {
            if (!Discord.Ready) return "My Discord bot isn't set up yet. Say connect my Discord first.";
            string clean;
            string refused = Refused(text, true, out clean);
            if (refused != null) return refused;
            if (!Allowed()) return "I've sent " + MostPerHour + " messages in the last hour, so I'm holding this one until you say so.";
            int status;
            Discord.PostJson("/channels/" + line.Channel + "/messages", "{\"content\":" + Voice.Quote(clean) + ",\"allowed_mentions\":{\"parse\":[]}}", out status);
            if (status == 403) return line.Name + " doesn't take DMs from me.";
            if (status < 200 || status >= 300) return Discord.Why(status);
            lock (gate) sentAt.Add(DateTime.Now);
            ActivityLog.Write("discord", "reply to " + line.Name, null);
            SentLog(line.Name, clean);
            return "Sent to " + line.Name + ": \"" + (clean.Length > 160 ? clean.Substring(0, 157) + "..." : clean) + "\"";
        }

        internal static string Message(string who, string text, List<string> accounts)
        {
            if (!Discord.Ready) return "My Discord bot isn't set up yet. Say connect my Discord first.";
            if (string.IsNullOrWhiteSpace(who)) return "Who should I message, choom?";
            string clean;
            string refused = Refused(text, false, out clean);
            if (refused != null) return refused;
            string problem;
            List<DiscordMember> members = Members(out problem);
            if (members == null) return problem;
            string name = who.Trim();
            List<DiscordMember> found = Match(members, name, accounts);
            if (found.Count == 0) return "I couldn't find " + name + " in the server I share with them.";
            if (found.Count > 1)
            {
                List<string> labels = new List<string>();
                foreach (DiscordMember m in found) labels.Add(m.Label + " (" + m.User + ")");
                return "Which " + name + ", choom? " + string.Join(" or ", labels.ToArray()) + ".";
            }
            if (!Allowed()) return "I've sent " + MostPerHour + " messages in the last hour, so I'm holding this one until you say so.";
            return SendTo(found[0], clean);
        }

        internal static string Ordered(string id, string label, string text, string asked)
        {
            string clean;
            string refused = Refused(text, false, asked, out clean);
            if (refused != null) return refused;
            if (!Allowed()) return "I've sent " + MostPerHour + " messages in the last hour, so I'm holding this one until you say so.";
            return SendTo(new DiscordMember { Id = id, User = label, Global = label }, clean);
        }

        internal static Func<DiscordMember, string, string> SendTo = Send;

        private static string Send(DiscordMember one, string clean)
        {
            if (!Discord.Ready) return "My Discord bot isn't set up yet. Say connect my Discord first.";
            int status;
            string channel = Discord.PostJson("/users/@me/channels", "{\"recipient_id\":" + Voice.Quote(one.Id) + "}", out status);
            string id = Field(channel, "id");
            if (status == 403 || (status >= 200 && status < 300 && id == null)) return one.Label + " doesn't take DMs from me.";
            if (status < 200 || status >= 300) return Discord.Why(status);
            Discord.PostJson("/channels/" + id + "/messages", "{\"content\":" + Voice.Quote(clean) + ",\"allowed_mentions\":{\"parse\":[]}}", out status);
            if (status == 403) return one.Label + " doesn't take DMs from me.";
            if (status < 200 || status >= 300) return Discord.Why(status);
            lock (gate) sentAt.Add(DateTime.Now);
            ActivityLog.Write("discord", "message to " + one.Label, null);
            SentLog(one.Label, clean);
            return "Sent to " + one.Label + ": \"" + (clean.Length > 160 ? clean.Substring(0, 157) + "..." : clean) + "\"";
        }

        internal static void SentLog(string who, string text)
        {
            string root = Vault.Root;
            if (string.IsNullOrEmpty(root)) return;
            try
            {
                string folder = Path.Combine(root, "Capri");
                if (Directory.Exists(folder)) File.AppendAllText(Path.Combine(folder, "sent-messages.md"), "- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " to " + who + ": " + text + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static bool Allowed()
        {
            lock (gate)
            {
                sentAt.RemoveAll(delegate(DateTime when) { return (DateTime.Now - when).TotalMinutes > 60; });
                return sentAt.Count < MostPerHour;
            }
        }

        private static List<DiscordMember> Members(out string problem)
        {
            problem = null;
            lock (gate)
                if (cached != null && (DateTime.Now - cachedAt).TotalMinutes < CacheMinutes) return cached;
            int status;
            string guilds = Discord.Get("/users/@me/guilds", out status);
            if (status < 200 || status >= 300) { problem = Discord.Why(status); return null; }
            string owner = ForkSettings.Current.DiscordOwner;
            List<DiscordMember> list = new List<DiscordMember>();
            int count = 0;
            foreach (Dictionary<string, object> guild in Dicts(guilds))
            {
                if (++count > MostGuilds) break;
                string json = Discord.Get("/guilds/" + Text(guild, "id") + "/members?limit=1000", out status);
                if (status == 403) { problem = "I can't see the server's member list. The Server Members intent must be switched on for my bot in the Discord Developer Portal."; return null; }
                if (status < 200 || status >= 300) { problem = Discord.Why(status); return null; }
                foreach (Dictionary<string, object> entry in Dicts(json))
                {
                    Dictionary<string, object> user = entry.ContainsKey("user") ? entry["user"] as Dictionary<string, object> : null;
                    if (user == null || (user.ContainsKey("bot") && user["bot"] is bool && (bool)user["bot"])) continue;
                    string id = Text(user, "id");
                    if (id.Length == 0 || id == owner || list.Exists(delegate(DiscordMember m) { return m.Id == id; })) continue;
                    list.Add(new DiscordMember { Id = id, User = Text(user, "username"), Global = Text(user, "global_name"), Nick = Text(entry, "nick") });
                }
            }
            lock (gate)
            {
                cached = list;
                cachedAt = DateTime.Now;
            }
            return list;
        }

        private static List<Dictionary<string, object>> Dicts(string json)
        {
            List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
            try
            {
                IEnumerable all = new JavaScriptSerializer().DeserializeObject(json) as IEnumerable;
                if (all == null) return items;
                foreach (object item in all)
                {
                    Dictionary<string, object> entry = item as Dictionary<string, object>;
                    if (entry != null) items.Add(entry);
                }
            }
            catch (Exception) { }
            return items;
        }

        private static string Text(Dictionary<string, object> data, string key)
        {
            return data != null && data.ContainsKey(key) && data[key] != null ? Convert.ToString(data[key]) : "";
        }

        private static string Field(string json, string key)
        {
            try
            {
                Dictionary<string, object> data = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                string value = Text(data, key);
                return value.Length == 0 ? null : value;
            }
            catch (Exception) { return null; }
        }
    }
}
