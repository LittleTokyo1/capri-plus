using System;
using System.Collections.Generic;
using System.Threading;

namespace CapriDesktop
{
    // Capri+ in League's draft and queue: accept the match while he is away or when she queued it for him, and with a few
    // seconds left on his pick (2 s away, 1 s at the PC, so he can still change his mind) lock in the champ on it, else down his favourites,
    // else what pros play in his lane, else any free champ he owns. A banned or cleared hover is swapped for the next free one.
    // Only the client's own API, never his mouse or keys.
    internal sealed partial class Companion
    {
        internal const long AwayMilliseconds = 15000;
        internal const long LockLeft = 2000;
        internal const long LastSecond = 1000;
        internal const long FastWindow = 5000;
        internal const double FastPoll = 0.25;
        internal static Func<long> LeagueIdle = Native.IdleMilliseconds;
        internal static Func<DateTime> LeagueClock = delegate { return DateTime.UtcNow; };
        private int lockedAction = -1;
        private int seenAction = -1;
        private DateTime seenAt;
        private bool nothingToLock;
        private volatile List<int> prosForLane;
        private bool prosAsked;
        private volatile int prosDraft;
        private int rehoverSeen = -1;
        private bool nothingToHover;
        private volatile bool leagueFast;
        private readonly List<string> whileAway = new List<string>();

        internal bool LeagueFast { get { return leagueFast; } }

        internal static bool AwayNow()
        {
            return ForkSettings.Current.AwayAlways || LeagueIdle() >= AwayMilliseconds;
        }

        // On the League worker thread, every poll in the ready check.
        internal string ReadyTurn()
        {
            if (!ForkSettings.Current.AutoAccept) return null;
            bool away = AwayNow();
            bool asked = LeagueSocial.QueuedRecently();
            bool every = ForkSettings.Current.AcceptEveryPop;
            if (!away && !asked && !every) return null;
            ReadyCheck ready = league.ReadReady();
            if (ready == null || ready.State != "InProgress" || ready.Response != "None") return null;
            bool done = league.Accept();
            string why = ForkSettings.Current.AwayAlways ? "always on" : away ? "he was away" : asked ? "he asked me to queue" : "every pop";
            ActivityLog.Write("league accept", why, done ? "accepted" : "the client refused");
            if (done) Noted("accepted the match");
            return done ? "accepted" : null;
        }

        // On the League worker thread, every poll in champ select.
        internal string LockTurn(ChampSession session)
        {
            leagueFast = false;
            ChampAction pick = League.MyPick(session);
            ChampSeat me = session == null ? null : session.Seat;
            if (!ForkSettings.Current.AutoLock || me == null) return null;
            string position = me.Position;
            if (League.Role(position) != null) ProsForLane(position);
            if (pick == null || !pick.InProgress || pick.Completed || pick.Id == lockedAction) return null;
            DateTime now = LeagueClock();
            if (pick.Id != seenAction) { seenAction = pick.Id; seenAt = now; }
            long nowEpoch = (long)(now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            long left = ChampTimer.Remaining(session.Timer, nowEpoch, (long)(now - seenAt).TotalMilliseconds);
            leagueFast = left <= FastWindow;
            bool away = AwayNow();
            if (left > (away ? LockLeft : LastSecond)) return null;
            int champion = league.LockChoice(session, ForkSettings.Current.Favorites(position), league.Pickable(), delegate { return ProsForLane(position); });
            if (champion <= 0)
            {
                if (!nothingToLock) ActivityLog.Write("league lock", position ?? "no lane", "nothing free to lock");
                nothingToLock = true;
                return null;
            }
            lockedAction = pick.Id;
            string name = league.Name(champion);
            bool done = league.Lock(pick.Id, champion);
            ActivityLog.Write("league lock", name, (done ? "locked in" : "the client refused") + " (" + Math.Max(0, left) + " ms left, " + (away ? "away" : "at the PC") + ")");
            if (!done) { lockedAction = -1; return null; }
            leagueFast = false;
            Noted("locked in " + name);
            return name;
        }

        // On the League worker thread, every poll in champ select: his hover was banned, locked by someone else or cleared.
        internal string RehoverTurn(ChampSession session)
        {
            ChampAction pick = League.MyPick(session);
            ChampSeat me = session == null ? null : session.Seat;
            if (!ForkSettings.Current.HoverFavorite || pick == null || me == null || pick.Completed) return null;
            int current = pick.Champion > 0 ? pick.Champion : me.Intent;
            if (current == rehoverSeen) return null;
            List<int> pickable = league.Pickable();
            if (current > 0 && !League.Gone(session, me, current, pickable)) return null;
            rehoverSeen = current;
            List<int> pros = League.Role(me.Position) == null ? null : ProsForLane(me.Position);
            int champion = league.Rehover(session, ForkSettings.Current.Favorites(me.Position), pickable, pros);
            if (champion <= 0)
            {
                if (!nothingToHover) ActivityLog.Write("league hover", me.Position ?? "no lane", "nothing free to hover");
                nothingToHover = true;
                return null;
            }
            string name = league.Name(champion);
            bool done = league.Hover(pick.Id, champion);
            ActivityLog.Write("league hover", name, (done ? "hovered (not locked)" : "the client refused") + (current > 0 ? ", " + league.Name(current) + " was gone" : ", his hover was empty"));
            return done ? name : null;
        }

        // Fetched once per draft off the worker thread, so the last seconds never wait on the network; the smoke test waits.
        private List<int> ProsForLane(string position)
        {
            List<int> found = prosForLane;
            if (found != null || prosAsked) return found;
            prosAsked = true;
            if (testing)
            {
                prosForLane = ProBuilds.LanePicks(position);
                return prosForLane;
            }
            int draft = prosDraft;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<int> picks;
                try { picks = ProBuilds.LanePicks(position); }
                catch (Exception error) { Brain.Log("league pros", -1, error.Message); picks = new List<int>(); }
                if (draft == prosDraft) prosForLane = picks;
            });
            return null;
        }

        // Kept for when he is back, only when he really was away (with "always" at the PC he sees it himself).
        private void Noted(string what)
        {
            if (LeagueIdle() < AwayMilliseconds) return;
            lock (whileAway) whileAway.Add(what);
        }

        // Once he is back, one short line on what she did.
        internal string BackTurn()
        {
            if (LeagueIdle() >= AwayMilliseconds) return null;
            string said;
            lock (whileAway)
            {
                if (whileAway.Count == 0) return null;
                said = "While you were away I " + string.Join(" and ", whileAway.ToArray()) + ".";
                whileAway.Clear();
            }
            if (!testing) Post(delegate { Tell("(league)", said); });
            return said;
        }

        private void EndAway()
        {
            lockedAction = -1;
            seenAction = -1;
            nothingToLock = false;
            nothingToHover = false;
            rehoverSeen = -1;
            prosDraft++;
            prosForLane = null;
            prosAsked = false;
            leagueFast = false;
            EndBan();
        }
    }
}
