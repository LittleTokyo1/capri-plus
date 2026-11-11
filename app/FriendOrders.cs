using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed class FriendOrder
    {
        internal string Who = "", Note = "", Text = "", Asked = "";
        internal DateTime Until;
    }

    internal sealed class SavedOrder
    {
        public string Who, Note, Text, Asked;
        public long Until;
    }

    internal sealed class FriendOrderDue
    {
        internal PresenceFriend Friend;
        internal FriendOrder Order;
    }

    internal static class FriendOrders
    {
        internal const int Hours = 24;
        private const int Most = 10;
        private static readonly object gate = new object();
        private static readonly List<FriendOrder> orders = new List<FriendOrder>();
        private static string loadedFrom;

        internal static string FilePath
        {
            get { return ActivityLog.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ActivityLog.FilePath), "friend-orders.json"); }
        }

        internal static void Reset()
        {
            lock (gate)
            {
                orders.Clear();
                loadedFrom = null;
            }
        }

        private static void Load()
        {
            string path = FilePath;
            if (loadedFrom == path) return;
            loadedFrom = path;
            orders.Clear();
            if (path == null || !File.Exists(path)) return;
            try
            {
                List<SavedOrder> saved = new JavaScriptSerializer().Deserialize<List<SavedOrder>>(File.ReadAllText(path));
                if (saved != null) foreach (SavedOrder one in saved) orders.Add(new FriendOrder { Who = one.Who ?? "", Note = one.Note ?? "", Text = one.Text ?? "", Asked = one.Asked ?? "", Until = new DateTime(one.Until) });
            }
            catch (Exception error) { Brain.Log("friend-orders.json", -1, error.GetType().Name + ": " + error.Message); }
        }

        private static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            List<SavedOrder> saved = orders.ConvertAll(delegate(FriendOrder one) { return new SavedOrder { Who = one.Who, Note = one.Note, Text = one.Text, Asked = one.Asked, Until = one.Until.Ticks }; });
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".new";
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(saved));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) { Brain.Log("friend-orders.json", -1, error.GetType().Name + ": " + error.Message); }
        }

        internal static string Tool(string name, string text, string asked, List<PresenceFriend> tracked, List<PresenceFriend> online, DateTime now)
        {
            string who = (name ?? "").Trim();
            if (who.Length == 0) return "Which friend should I message when they come online?";
            if (string.IsNullOrWhiteSpace(text)) return "What should I tell " + who + " when they come online?";
            string note = null;
            foreach (NameHit hit in Names.Find(who)) if (!hit.Self && hit.Note != null) { note = hit.Note; break; }
            if (note == null) return who + " isn't in your People notes, so I can't see when they come online. Tell me their Discord name and I'll note it down.";
            string refused = DiscordFriends.Check(text, asked);
            if (refused != null) return refused;
            FriendOrder order = new FriendOrder { Who = who, Note = note, Text = text.Trim(), Asked = asked ?? "", Until = now.AddHours(Hours) };
            PresenceFriend friend = Find(tracked, note);
            if (friend == null) return who + "'s note has no Discord name that matches anyone in our server, so I can't see when they're on. Tell me their Discord name and I'll note it down.";
            if (Find(online, note) != null) return who + " is online right now, so I sent it now. " + DiscordFriends.Ordered(friend.Id, who, order.Text, order.Asked);
            Add(order);
            PlanLinks.Seen(PlanLinks.Order, PlanLinks.IdOf(order.Note, order.Text), asked, order.Who + " " + order.Note, now);
            return "Saved: when " + who + " comes online (until " + order.Until.ToString("ddd HH:mm", CultureInfo.InvariantCulture) + "), my bot sends them \"" + order.Text + "\" once and I tell you.";
        }

        private static PresenceFriend Find(List<PresenceFriend> friends, string note)
        {
            return (friends ?? new List<PresenceFriend>()).Find(delegate(PresenceFriend one) { return string.Equals(one.Note, note, StringComparison.OrdinalIgnoreCase); });
        }

        internal static void Add(FriendOrder order)
        {
            if (order == null) return;
            lock (gate)
            {
                Load();
                orders.RemoveAll(delegate(FriendOrder old) { return string.Equals(old.Note, order.Note, StringComparison.OrdinalIgnoreCase); });
                if (orders.Count >= Most) orders.RemoveAt(0);
                orders.Add(order);
                Save();
            }
        }

        internal static bool Pending(DateTime now)
        {
            lock (gate)
            {
                Load();
                return orders.Exists(delegate(FriendOrder one) { return now < one.Until; });
            }
        }

        internal static List<FriendOrderDue> Due(List<PresenceFriend> online, DateTime now)
        {
            List<FriendOrderDue> due = new List<FriendOrderDue>();
            lock (gate)
            {
                Load();
                int gone = orders.RemoveAll(delegate(FriendOrder one) { return now >= one.Until; });
                foreach (FriendOrder order in orders.ToArray())
                {
                    PresenceFriend friend = Find(online, order.Note);
                    if (friend == null) continue;
                    orders.Remove(order);
                    due.Add(new FriendOrderDue { Friend = friend, Order = order });
                }
                if (gone + due.Count > 0) Save();
            }
            return due;
        }

        internal static string Told(FriendOrderDue due, string result)
        {
            if (result != null && result.StartsWith("Sent to", StringComparison.Ordinal)) return due.Order.Who + " came online, so I messaged them: \"" + due.Order.Text + "\"";
            return due.Order.Who + " came online, but I couldn't message them: " + result;
        }

        internal static void Forget()
        {
            lock (gate)
            {
                Load();
                orders.Clear();
                Save();
            }
        }
    }

    internal sealed partial class Companion
    {
        private readonly List<FriendNews> friendNews = new List<FriendNews>();

        private void FriendOrdersTick(DateTime now)
        {
            foreach (FriendOrderDue due in FriendOrders.Due(DiscordPresence.OnlineFriends(), now))
            {
                FriendOrderDue one = due;
                Arrivals.Mark(one.Friend.Id, now);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string result;
                    try { result = DiscordFriends.Ordered(one.Friend.Id, one.Order.Who, one.Order.Text, one.Order.Asked); }
                    catch (Exception error) { result = error.GetType().Name; Brain.Log("friend order", -1, error.GetType().Name); }
                    string line = FriendOrders.Told(one, result);
                    if (result != null && result.StartsWith("Sent to", StringComparison.Ordinal)) PlanLinks.Done(PlanLinks.Order, PlanLinks.IdOf(one.Order.Note, one.Order.Text), DateTime.Now);
                    Post(delegate { ActivityLog.Write("discord", "friend order done", line); friendNews.Add(new FriendNews { Id = one.Friend.Id, Name = one.Friend.Name, Text = line }); });
                });
            }
        }
    }
}
