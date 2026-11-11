using System;
using System.Collections.Generic;
using System.Globalization;

namespace CapriDesktop
{
    internal sealed class WakeCount
    {
        public string Day = "";
        public int Wakes;
    }

    internal static class Budgets
    {
        internal static int Share(int perDay)
        {
            return Math.Max(1, perDay / 2);
        }

        internal static bool Take(WakeCount count, DateTime now, int most)
        {
            if (count == null) return false;
            Roll(count, now);
            if (count.Wakes >= most) return false;
            count.Wakes++;
            return true;
        }

        internal static int Used(WakeCount count, DateTime now)
        {
            return count == null || count.Day != Day(now) ? 0 : count.Wakes;
        }

        private static void Roll(WakeCount count, DateTime now)
        {
            string day = Day(now);
            if (count.Day == day) return;
            count.Day = day;
            count.Wakes = 0;
        }

        private static string Day(DateTime now)
        {
            return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    internal static class SilenceLog
    {
        private static readonly Dictionary<string, string> told = new Dictionary<string, string>();
        private static readonly object gate = new object();

        internal static bool Once(string area, string why, string detail, DateTime now)
        {
            string key = area + "|" + why;
            string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            lock (gate)
            {
                string had;
                if (told.TryGetValue(key, out had) && had == day) return false;
                told[key] = day;
            }
            ActivityLog.Write(area, why, detail);
            return true;
        }

        internal static void Reset()
        {
            lock (gate) told.Clear();
        }
    }
}
