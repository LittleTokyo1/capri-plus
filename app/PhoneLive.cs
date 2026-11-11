using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace CapriDesktop
{
    internal sealed class PhoneEvent
    {
        internal int Id;
        internal string Text;
        internal DateTime At;
        internal bool Acked;
    }

    internal static class PhoneLive
    {
        internal const int AckSeconds = 5;
        internal const int OpenSeconds = 35;
        internal const int MostKept = 30;
        internal const int MostWaiting = 4;
        internal static int PollMs = 25000;
        private static readonly object gate = new object();
        private static readonly List<PhoneEvent> events = new List<PhoneEvent>();
        private static int lastId;
        private static int waiting;
        private static long polledTicks;

        internal static void Reset()
        {
            lock (gate)
            {
                events.Clear();
                lastId = 0;
                waiting = 0;
                polledTicks = 0;
                Monitor.PulseAll(gate);
            }
        }

        internal static bool Open()
        {
            long polled = Interlocked.Read(ref polledTicks);
            return polled != 0 && (DateTime.UtcNow.Ticks - polled) / TimeSpan.TicksPerSecond < OpenSeconds;
        }

        private static void Polled() { Interlocked.Exchange(ref polledTicks, DateTime.UtcNow.Ticks); }

        internal static int Push(string text)
        {
            lock (gate)
            {
                PhoneEvent made = new PhoneEvent { Id = ++lastId, Text = text ?? "", At = DateTime.UtcNow };
                events.Add(made);
                if (events.Count > MostKept) events.RemoveRange(0, events.Count - MostKept);
                Monitor.PulseAll(gate);
                return made.Id;
            }
        }

        internal static bool Offer(string text, int ackMs)
        {
            if (string.IsNullOrWhiteSpace(text) || !Open()) return false;
            int id = Push(text);
            DateTime until = DateTime.UtcNow.AddMilliseconds(ackMs);
            lock (gate)
            {
                while (true)
                {
                    PhoneEvent shown = events.Find(delegate(PhoneEvent one) { return one.Id == id; });
                    if (shown != null && shown.Acked) { ActivityLog.Write("phone", "own line shown", "acked"); return true; }
                    int left = (int)(until - DateTime.UtcNow).TotalMilliseconds;
                    if (shown == null || left <= 0) break;
                    Monitor.Wait(gate, left);
                }
            }
            ActivityLog.Write("phone", "own line not acked", "in " + AckSeconds + " s, it goes by her usual way");
            return false;
        }

        internal static bool Ack(int id)
        {
            lock (gate)
            {
                PhoneEvent shown = events.Find(delegate(PhoneEvent one) { return one.Id == id; });
                if (shown == null) return false;
                shown.Acked = true;
                Monitor.PulseAll(gate);
                return true;
            }
        }

        internal static List<PhoneEvent> Wait(int after, int ms)
        {
            Polled();
            lock (gate)
            {
                if (after < 0 || after > lastId) return new List<PhoneEvent>();
                if (waiting >= MostWaiting) return null;
                waiting++;
                try
                {
                    DateTime until = DateTime.UtcNow.AddMilliseconds(ms);
                    while (true)
                    {
                        List<PhoneEvent> newer = events.FindAll(delegate(PhoneEvent one) { return one.Id > after; });
                        int left = (int)(until - DateTime.UtcNow).TotalMilliseconds;
                        if (newer.Count > 0 || left <= 0) return newer;
                        Monitor.Wait(gate, left);
                        Polled();
                    }
                }
                finally { waiting--; Polled(); }
            }
        }

        internal static int Last { get { lock (gate) return lastId; } }

        internal static string Json(List<PhoneEvent> list)
        {
            StringBuilder json = new StringBuilder("{\"events\":[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append("{\"id\":").Append(list[i].Id.ToString(CultureInfo.InvariantCulture)).Append(",\"text\":").Append(PhoneServer.Json(list[i].Text)).Append(",\"at\":").Append(PhoneServer.Json(list[i].At.ToString("o", CultureInfo.InvariantCulture))).Append('}');
            }
            return json.Append("],\"last\":").Append(Last.ToString(CultureInfo.InvariantCulture)).Append('}').ToString();
        }
    }
}
