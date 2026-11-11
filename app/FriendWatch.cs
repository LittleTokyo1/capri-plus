using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal static class FriendWatch
    {
        internal static readonly Regex Asked = new Regex(@"\b(?:let me know|tell me|ping me|notify me|alert me|message me|dm me|give me a (?:shout|heads[- ]?up))\b[^.?!]*?\b(?:when|if|once|as soon as)\s+(?<who>[^.?!]*?)\s+(?:gets?|comes?|goes|is|are|pops?|logs?|hops?)\s+(?:back\s+)?(?:on|online)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly HashSet<string> Anyone = new HashSet<string>(("people anyone anybody someone somebody friends everyone everybody they them boys guys squad").Split(' '));
        private static readonly object gate = new object();
        private static string who;
        private static DateTime until = DateTime.MinValue;
        private static readonly HashSet<string> seen = new HashSet<string>();

        internal static bool Active(DateTime now) { lock (gate) return now < until; }

        internal static string Who(string text)
        {
            Match found = Asked.Match(text ?? "");
            if (!found.Success) return null;
            string named = Regex.Replace(found.Groups["who"].Value, @"\b(any|some|one|of|my|the|on discord|on league)\b", " ", RegexOptions.IgnoreCase);
            named = Regex.Replace(named, @"\s+", " ").Trim();
            if (named.Length == 0 || Anyone.Contains(named.ToLowerInvariant())) return "";
            foreach (NameHit hit in Names.Find(named)) if (!hit.Self && hit.Note != null) return named;
            return null;
        }

        internal static string Start(string text, List<PresenceFriend> online, DateTime now)
        {
            string named = Who(text);
            if (named == null) return null;
            lock (gate)
            {
                who = named.Length == 0 ? null : named;
                until = now.Date.AddDays(1);
                seen.Clear();
            }
            List<string> already = new List<string>();
            foreach (PresenceFriend friend in online ?? new List<PresenceFriend>())
            {
                if (!Wanted(friend)) continue;
                lock (gate) seen.Add(friend.Id);
                already.Add(friend.Name);
            }
            string target = named.Length == 0 ? "any of your friends comes" : named + " comes";
            string onNow = already.Count == 0 ? "" : " Already on right now: " + string.Join(", ", already.ToArray()) + ".";
            return "Okay, I'll tell you when " + target + " online on Discord today." + onNow;
        }

        internal static void Stop()
        {
            lock (gate)
            {
                who = null;
                until = DateTime.MinValue;
                seen.Clear();
            }
        }

        private static bool Wanted(PresenceFriend friend)
        {
            string named;
            lock (gate) named = who;
            if (named == null) return true;
            if (Names.NameWord(named, friend.Name)) return true;
            foreach (NameHit hit in Names.Find(named)) if (!hit.Self && hit.Note != null && string.Equals(hit.Note, friend.Note, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal static List<string> Arrived(List<PresenceFriend> online, DateTime now)
        {
            return NamesOf(ArrivedFriends(online, now));
        }

        internal static List<string> NamesOf(List<PresenceFriend> friends)
        {
            List<string> names = new List<string>();
            foreach (PresenceFriend friend in friends) names.Add(friend.Name);
            return names;
        }

        internal static List<PresenceFriend> ArrivedFriends(List<PresenceFriend> online, DateTime now)
        {
            List<PresenceFriend> fresh = new List<PresenceFriend>();
            if (!Active(now)) return fresh;
            List<string> ids = new List<string>();
            foreach (PresenceFriend friend in online ?? new List<PresenceFriend>())
            {
                ids.Add(friend.Id);
                if (!Wanted(friend)) continue;
                bool added;
                lock (gate) added = seen.Add(friend.Id);
                if (added) fresh.Add(friend);
            }
            lock (gate)
            {
                seen.RemoveWhere(delegate(string id) { return !ids.Contains(id); });
                if (fresh.Count > 0 && who != null) until = DateTime.MinValue;
            }
            return fresh;
        }

        internal static string Line(List<string> names)
        {
            if (names == null || names.Count == 0) return null;
            return string.Join(" and ", names.ToArray()) + " just came online on Discord.";
        }
    }

    internal sealed partial class Companion
    {
        private readonly List<PresenceFriend> friendsArrived = new List<PresenceFriend>();
        private bool friendSending;

        private bool FriendWatchHeard(string text, bool voiceChat)
        {
            if (voiceChat || FriendWatch.Who(text) == null) return false;
            string said = !Discord.Ready ? "My Discord bot isn't set up yet, so I can't see who comes online. Say connect my Discord first." : FriendWatch.Start(text, DiscordPresence.OnlineFriends(), DateTime.Now);
            ActivityLog.Write("discord", "friend watch", said);
            Tell("(discord)", said);
            return true;
        }

        private void FriendWatchTick(DateTime now, ForkSettings settings)
        {
            if (friendsArrived.Count == 0)
                foreach (PresenceFriend friend in FriendWatch.ArrivedFriends(DiscordPresence.OnlineFriends(), now))
                {
                    if (ArrivalTold(friend.Id, now)) ActivityLog.Write("discord", "friend watch already told", friend.Name);
                    else friendsArrived.Add(friend);
                }
            if ((friendsArrived.Count == 0 && friendNews.Count == 0) || friendSending) return;
            bool away = Delivery.NotHome();
            if (!away && (Lockout.Active || settings.Paused || DiscordBusy() || talkState == TalkState.Talking || clock.Elapsed.TotalSeconds < voiceUntil)) return;
            List<PresenceFriend> arrived = new List<PresenceFriend>(friendsArrived);
            List<FriendNews> waiting = new List<FriendNews>(friendNews);
            friendsArrived.Clear();
            friendNews.Clear();
            if (away && !Delivery.Reachable()) return;
            List<FriendNews> told;
            List<string> said = FriendsGated(arrived, waiting, now, out told);
            if (arrived.Count == 0 && told.Count == 0) return;
            List<string> parts = new List<string>();
            if (arrived.Count > 0) parts.Add(FriendWatch.Line(FriendWatch.NamesOf(arrived)));
            foreach (FriendNews news in told) parts.Add(news.Text);
            string line = string.Join(" ", parts.ToArray());
            if (away)
            {
                friendSending = true;
                MarkArrivals(arrived, told, now);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool sent = false;
                    try { sent = Delivery.Send("Heads-up: " + line); }
                    catch (Exception error) { Brain.Log("friend watch", -1, error.GetType().Name); }
                    Post(delegate
                    {
                        friendSending = false;
                        if (sent) brain.Own("discord", "Sent to his phone on Discord: " + line);
                        else
                        {
                            UnmarkArrivals(arrived, told);
                            FriendsUnsaid(said);
                        }
                        ActivityLog.Write("discord", sent ? "friend watch sent to his DM" : "friend watch not sent", line);
                    });
                });
                return;
            }
            MarkArrivals(arrived, told, now);
            brain.Own("discord", line);
            ActivityLog.Write("discord", "friend watch told", line);
            heard = "(discord)";
            box.Prompt = Bubble.Sys;
            Answer(++asking, line, TalkState.Talking, chatting);
        }
    }
}
