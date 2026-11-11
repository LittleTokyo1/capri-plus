using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed class QueuedPass
    {
        public string Text;
        public string Before;
        public string Reply;
        public string Recent;
        public long Said;
        public long Due;
        public int Attempt;
        public string Failed;
        public int Nights;
        public long Lost;
        public bool Spoken;
    }

    internal sealed class QueueState
    {
        public List<QueuedPass> Items = new List<QueuedPass>();
        public List<QueuedPass> Gone = new List<QueuedPass>();
        public string FailDay = "";
        public int Failures;
        public string DrainDay = "";
    }

    internal static class MemoryQueue
    {
        internal const int LiveTries = 3;
        internal const int MostNights = 3;
        internal const int NightMost = 5;
        internal const int MostWaiting = 50;
        internal const int MostWaitMinutes = 30;
        private const int GiveUpDays = 7;
        private const int ShowLostDays = 3;
        private const int MostLost = 20;
        private static readonly object gate = new object();
        private static QueueState state = new QueueState();
        private static string loadedFrom;

        internal static string FilePath
        {
            get { return ActivityLog.FilePath == null ? null : Path.Combine(Path.GetDirectoryName(ActivityLog.FilePath), "memory-queue.json"); }
        }

        internal static void Reset()
        {
            lock (gate)
            {
                state = new QueueState();
                loadedFrom = null;
            }
        }

        internal static int Count
        {
            get { lock (gate) return Current().Items.Count; }
        }

        internal static int WaitMinutes(int attempt)
        {
            int tries = Math.Max(1, attempt);
            return Math.Min(2 * tries * tries, MostWaitMinutes);
        }

        internal static bool AtNight(QueuedPass item)
        {
            return item.Attempt >= LiveTries;
        }

        private static QueueState Current()
        {
            string path = FilePath;
            if (loadedFrom != path)
            {
                state = Load(path);
                loadedFrom = path;
            }
            return state;
        }

        private static QueueState Load(string path)
        {
            QueueState found = null;
            if (path != null && File.Exists(path))
            {
                try { found = new JavaScriptSerializer { MaxJsonLength = 8000000 }.Deserialize<QueueState>(File.ReadAllText(path)); }
                catch (Exception error) { Brain.Log("memory-queue.json", -1, error.GetType().Name + ": " + error.Message); }
            }
            if (found == null) found = new QueueState();
            if (found.Items == null) found.Items = new List<QueuedPass>();
            if (found.Gone == null) found.Gone = new List<QueuedPass>();
            if (found.FailDay == null) found.FailDay = "";
            if (found.DrainDay == null) found.DrainDay = "";
            return found;
        }

        private static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".new";
                File.WriteAllText(temporary, new JavaScriptSerializer { MaxJsonLength = 8000000 }.Serialize(state));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) { Brain.Log("memory-queue.json", -1, error.GetType().Name + ": " + error.Message); }
        }

        internal static void Settle()
        {
            lock (gate) { Current(); Save(); }
        }

        internal static void Add(QueuedPass pass)
        {
            lock (gate)
            {
                QueueState live = Current();
                if (live.Items.Count >= MostWaiting)
                {
                    Lose(live, live.Items[0], "the queue was full", DateTime.Now);
                    live.Items.RemoveAt(0);
                }
                live.Items.Add(pass);
                Save();
            }
        }

        internal static QueuedPass Take(DateTime now)
        {
            lock (gate)
            {
                QueueState live = Current();
                bool dropped = false;
                for (int i = live.Items.Count - 1; i >= 0; i--)
                    if ((now - new DateTime(live.Items[i].Said)).TotalDays > GiveUpDays)
                    {
                        Lose(live, live.Items[i], "older than " + GiveUpDays + " days", now);
                        live.Items.RemoveAt(i);
                        dropped = true;
                    }
                if (dropped) Save();
                QueuedPass next = live.Items.Find(delegate(QueuedPass item) { return !AtNight(item) && new DateTime(item.Due) <= now; });
                if (next != null) live.Items.Remove(next);
                return next;
            }
        }

        internal static List<QueuedPass> TakeNight(int most)
        {
            lock (gate)
            {
                QueueState live = Current();
                List<QueuedPass> night = live.Items.FindAll(AtNight);
                if (night.Count > most) night.RemoveRange(most, night.Count - most);
                foreach (QueuedPass item in night) live.Items.Remove(item);
                return night;
            }
        }

        internal static void Lose(QueuedPass item, string why, DateTime now)
        {
            lock (gate)
            {
                Lose(Current(), item, why, now);
                Save();
            }
        }

        private static void Lose(QueueState live, QueuedPass item, string why, DateTime now)
        {
            item.Lost = now.Ticks;
            live.Gone.Add(item);
            if (live.Gone.Count > MostLost) live.Gone.RemoveRange(0, live.Gone.Count - MostLost);
            ActivityLog.Write("memory", "pass", "lost (" + why + "): " + Clip(item.Text, 60));
        }

        internal static string Failed(DateTime now)
        {
            lock (gate)
            {
                QueueState live = Current();
                string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (live.FailDay != day) { live.FailDay = day; live.Failures = 0; }
                live.Failures++;
                Save();
                return Health(live, now);
            }
        }

        internal static bool DrainedOn(DateTime now)
        {
            lock (gate) return Current().DrainDay == now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        internal static void Drained(DateTime now)
        {
            lock (gate)
            {
                Current().DrainDay = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Save();
            }
        }

        private static string Health(QueueState live, DateTime now)
        {
            return "memory pass failed " + live.Failures + " today, " + live.Items.Count + " waiting, " + LostSince(live, now).Count + " lost";
        }

        private static List<QueuedPass> LostSince(QueueState live, DateTime now)
        {
            return live.Gone.FindAll(delegate(QueuedPass item) { return (now - new DateTime(item.Lost)).TotalDays <= ShowLostDays; });
        }

        internal static int Forget(List<string> topics)
        {
            List<string> flat = new List<string>();
            foreach (string topic in topics) if (Forgetting.Squash(topic).Length > 0) flat.Add(Forgetting.Squash(topic));
            if (flat.Count == 0) return 0;
            Predicate<QueuedPass> about = delegate(QueuedPass item)
            {
                string all = Forgetting.Squash(item.Text + item.Before + item.Reply + item.Recent);
                return flat.Exists(delegate(string topic) { return all.Contains(topic); });
            };
            lock (gate)
            {
                QueueState live = Current();
                int gone = live.Items.RemoveAll(about);
                int lost = live.Gone.RemoveAll(about);
                if (gone + lost > 0) Save();
                return gone;
            }
        }

        internal static void Clear()
        {
            lock (gate)
            {
                QueueState live = Current();
                live.Items.Clear();
                live.Gone.Clear();
                Save();
            }
        }

        internal static List<QueuedPass> Waiting()
        {
            lock (gate) return new List<QueuedPass>(Current().Items);
        }

        internal static List<string> Lines()
        {
            List<string> lines = new List<string>();
            lock (gate)
                foreach (QueuedPass item in Current().Items) if (Shown(item)) lines.Add(Line(item));
            return lines;
        }

        internal static List<string> LostLines(DateTime now)
        {
            List<string> lines = new List<string>();
            lock (gate)
                foreach (QueuedPass item in LostSince(Current(), now)) if (Shown(item)) lines.Add(Line(item));
            return lines;
        }

        private static bool Shown(QueuedPass item)
        {
            return !Brain.Personal.IsMatch(item.Text ?? "");
        }

        private static string Line(QueuedPass item)
        {
            return new DateTime(item.Said).ToString("d MMMM HH:mm", CultureInfo.InvariantCulture) + ": " + Clip(item.Text, 60);
        }

        private static string Clip(string text, int most)
        {
            string plain = (text ?? "").Trim();
            return plain.Length > most ? plain.Substring(0, most) : plain;
        }
    }
}
