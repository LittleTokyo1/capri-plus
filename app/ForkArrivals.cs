using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed class FriendNews
    {
        internal string Id = "", Name = "", Text = "";
        internal bool Own;
    }

    internal static class Arrivals
    {
        internal const int Minutes = PresenceHold.MostHours * 60;
        private static readonly object gate = new object();
        private static readonly Dictionary<string, DateTime> told = new Dictionary<string, DateTime>();

        internal static bool Fresh(string id, DateTime now)
        {
            if (string.IsNullOrEmpty(id)) return true;
            lock (gate)
            {
                DateTime at;
                return !told.TryGetValue(id, out at) || now < at || (now - at).TotalMinutes >= Minutes;
            }
        }

        internal static void Mark(string id, DateTime now)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (gate) told[id] = now;
        }

        internal static void Unmark(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (gate) told.Remove(id);
        }

        internal static void Forget()
        {
            lock (gate) told.Clear();
        }
    }

    internal sealed partial class Companion
    {
        private bool ArrivalTold(string id, DateTime now)
        {
            if (!Arrivals.Fresh(id, now)) return true;
            if (string.IsNullOrEmpty(id)) return false;
            foreach (FriendNews news in friendNews) if (news.Id == id) return true;
            return false;
        }

        private void ArrivalsHeld(List<PresenceFriend> online, DateTime now)
        {
            foreach (PresenceHeads back in PresenceHold.After(online, now))
            {
                DiscordPresence.Told(back, now);
                if (ArrivalTold(back.Id, now)) { ActivityLog.Write("discord", "friend online already told", back.Name); continue; }
                ActivityLog.Write("discord", "friend online held through a game", back.Name);
                friendNews.Add(new FriendNews { Id = back.Id, Name = back.Name, Text = back.Text, Own = true });
            }
        }

        private static bool FriendHeld(PresenceHeads one, DateTime now)
        {
            if (ProactiveGate.Check(ProactiveGate.Friend, one.Name, one.Text, now) == null) return false;
            DiscordPresence.Told(one, now);
            Arrivals.Mark(one.Id, now);
            return true;
        }

        private static List<string> FriendsGated(List<PresenceFriend> arrived, List<FriendNews> news, DateTime now, out List<FriendNews> passed)
        {
            List<string> said = new List<string>();
            passed = new List<FriendNews>();
            foreach (PresenceFriend friend in arrived)
            {
                string text = FriendWatch.Line(new List<string> { friend.Name });
                ProactiveGate.Record(ProactiveGate.Friend, friend.Name, text, now);
                said.Add(text);
            }
            foreach (FriendNews one in news)
            {
                if (!one.Own) ProactiveGate.Record(ProactiveGate.Friend, one.Name, one.Text, now);
                else if (ProactiveGate.Check(ProactiveGate.Friend, one.Name, one.Text, now) != null)
                {
                    Arrivals.Mark(one.Id, now);
                    continue;
                }
                said.Add(one.Text);
                passed.Add(one);
            }
            return said;
        }

        private static void FriendsUnsaid(List<string> said)
        {
            foreach (string text in said) ProactiveGate.Unsay(text);
        }

        private static void MarkArrivals(List<PresenceFriend> arrived, List<FriendNews> news, DateTime now)
        {
            foreach (PresenceFriend friend in arrived) Arrivals.Mark(friend.Id, now);
            foreach (FriendNews one in news) Arrivals.Mark(one.Id, now);
        }

        private static void UnmarkArrivals(List<PresenceFriend> arrived, List<FriendNews> news)
        {
            foreach (PresenceFriend friend in arrived) Arrivals.Unmark(friend.Id);
            foreach (FriendNews one in news) Arrivals.Unmark(one.Id);
        }
    }
}
