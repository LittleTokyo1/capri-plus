using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal static class PresenceHold
    {
        internal const int MostHours = 3;
        private static readonly object gate = new object();
        private static readonly List<PresenceHeads> held = new List<PresenceHeads>();

        internal static void Hold(PresenceHeads one)
        {
            if (one == null) return;
            DiscordPresence.Drop(one);
            lock (gate)
            {
                held.RemoveAll(delegate(PresenceHeads old) { return old.Id == one.Id; });
                held.Add(one);
            }
        }

        internal static bool Any { get { lock (gate) return held.Count > 0; } }

        internal static List<PresenceHeads> After(List<PresenceFriend> online, DateTime now)
        {
            List<PresenceHeads> told = new List<PresenceHeads>();
            List<PresenceHeads> all;
            lock (gate)
            {
                all = new List<PresenceHeads>(held);
                held.Clear();
            }
            foreach (PresenceHeads one in all)
            {
                if ((now - one.When).TotalHours > MostHours) continue;
                PresenceHeads id = one;
                if (!(online ?? new List<PresenceFriend>()).Exists(delegate(PresenceFriend friend) { return friend.Id == id.Id; })) continue;
                told.Add(new PresenceHeads { Id = one.Id, Name = one.Name, Text = one.Name + " came online during your game and is still on Discord.", When = now });
            }
            return told;
        }

        internal static void Forget() { lock (gate) held.Clear(); }
    }
}
