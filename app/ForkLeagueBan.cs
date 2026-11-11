using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal const long BanLastMs = 1000;
        internal const int BanNames = 5;
        private int bannedAction = -1;
        private int banSeen = -1;
        private DateTime banSeenAt;
        private int banFor = -1;
        private volatile List<string> banCounters;
        private volatile bool banAsking;

        internal static ChampAction MyBan(ChampSession session)
        {
            if (session == null) return null;
            foreach (ChampAction[] turn in session.Actions ?? new ChampAction[0][])
                foreach (ChampAction action in turn ?? new ChampAction[0])
                    if (action != null && action.Type == "ban" && action.Actor == session.Me && !action.Completed) return action;
            return null;
        }

        internal static List<int> Gone(ChampSession session)
        {
            List<int> gone = League.Banned(session);
            foreach (ChampSeat seat in session.Theirs ?? new ChampSeat[0]) if (seat.Champion > 0) gone.Add(seat.Champion);
            foreach (ChampSeat seat in session.Mine ?? new ChampSeat[0]) { if (seat.Champion > 0) gone.Add(seat.Champion); if (seat.Intent > 0) gone.Add(seat.Intent); }
            return gone;
        }

        internal static int BanChoice(List<string> counters, Func<string, int> idOf, List<int> gone)
        {
            foreach (string name in counters ?? new List<string>())
            {
                int id = idOf(name);
                if (id > 0 && !gone.Contains(id)) return id;
            }
            return 0;
        }

        internal static string BanAsk(string mine, string position)
        {
            string role = League.Role(position);
            return "League of Legends, current patch. I'm playing " + mine + (role == null ? "" : " " + role) + ". Search this patch's matchup data and reply with only the names of the " + BanNames +
                " champions that are strongest against " + mine + (role == null ? "" : " in that role") + " (the best bans for me), strongest first, comma separated. Names only: no reasons, no lists, no links.";
        }

        internal static List<string> Named(string answer, List<string> champions)
        {
            List<KeyValuePair<int, string>> found = new List<KeyValuePair<int, string>>();
            if (string.IsNullOrWhiteSpace(answer) || champions == null) return new List<string>();
            foreach (string champion in champions)
            {
                if (string.IsNullOrEmpty(champion)) continue;
                Match at = Regex.Match(answer, @"(?<![\w'])" + Regex.Escape(champion) + @"(?![\w'])", RegexOptions.IgnoreCase);
                if (at.Success) found.Add(new KeyValuePair<int, string>(at.Index, champion));
            }
            found.Sort((a, b) => a.Key.CompareTo(b.Key));
            List<string> names = new List<string>();
            foreach (KeyValuePair<int, string> one in found) if (names.Count < BanNames) names.Add(one.Value);
            return names;
        }

        private void BanCounters(int mine, string position)
        {
            if (mine == banFor || banAsking) return;
            Cloud remote = brain.Remote;
            if (remote == null || !remote.Claude) return;
            banFor = mine;
            banCounters = null;
            banAsking = true;
            string name = league.Name(mine);
            List<string> champions = league.Champions();
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> found = null;
                try
                {
                    int status;
                    string system = "You are a sharp League of Legends coach. Be specific and current.";
                    string answer = Quick.Ask(remote, system, Voice.Quote(BanAsk(name, position)), LookupTool, 600, "off", out status);
                    if (answer == null && status == 400) answer = Quick.Ask(remote, system, Voice.Quote(BanAsk(name, position)), null, 400, "off", out status);
                    found = Named(answer, champions);
                }
                catch (Exception error) { Brain.Log("league ban", -1, error.Message); }
                finally { banAsking = false; }
                if (banFor != mine) return;
                banCounters = found ?? new List<string>();
                ActivityLog.Write("league ban", "strong against " + name, found == null || found.Count == 0 ? "no answer" : string.Join(", ", found.ToArray()));
            });
        }

        internal string BanTurn(ChampSession session)
        {
            if (!ForkSettings.Current.AutoBan || session == null || session.Seat == null) return null;
            ChampSeat me = session.Seat;
            int mine = me.Champion > 0 ? me.Champion : me.Intent;
            ChampAction ban = MyBan(session);
            if (mine > 0 && ban != null) BanCounters(mine, me.Position);
            if (ban == null || !ban.InProgress || ban.Id == bannedAction || ban.Champion > 0) return null;
            DateTime now = LeagueClock();
            if (ban.Id != banSeen) { banSeen = ban.Id; banSeenAt = now; }
            long nowEpoch = (long)(now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            long left = ChampTimer.Remaining(session.Timer, nowEpoch, (long)(now - banSeenAt).TotalMilliseconds);
            bool away = AwayNow();
            List<string> counters = banCounters;
            if (mine <= 0 || banFor != mine) counters = null;
            if (left <= FastWindow) leagueFast = true;
            if (left > BanLastMs) return null;
            if (counters == null && left > LastSecond) { leagueFast = true; return null; }
            bannedAction = ban.Id;
            int pick = BanChoice(counters, league.IdOf, Gone(session));
            string against = mine > 0 ? league.Name(mine) : "his pick";
            if (pick <= 0)
            {
                ActivityLog.Write("league ban", against, counters == null || counters.Count == 0 ? "no counters found, banned nothing" : "every counter already banned or taken, banned nothing");
                return null;
            }
            string name = league.Name(pick);
            bool done = BanNow(ban.Id, pick);
            ActivityLog.Write("league ban", name, (done ? "banned, checked in champ select" : "the client didn't take the ban") + " (strong against " + against + ", " + Math.Max(0, left) + " ms left, " + (away ? "away" : "at the PC") + ")");
            if (!done) { bannedAction = -1; return null; }
            Noted("banned " + name);
            return name;
        }

        private bool BanNow(int action, int pick)
        {
            league.Lock(action, pick);
            if (BanTaken(League.ParseSession(league.Get("/lol-champ-select/v1/session")), action, pick)) return true;
            league.Hover(action, pick);
            league.Send("POST", "/lol-champ-select/v1/session/actions/" + action + "/complete", "");
            return BanTaken(League.ParseSession(league.Get("/lol-champ-select/v1/session")), action, pick);
        }

        internal static bool BanTaken(ChampSession session, int action, int pick)
        {
            if (session == null) return false;
            foreach (ChampAction[] turn in session.Actions ?? new ChampAction[0][])
                foreach (ChampAction one in turn ?? new ChampAction[0])
                    if (one != null && one.Id == action) return one.Completed && one.Champion == pick;
            return false;
        }

        private void EndBan()
        {
            bannedAction = -1;
            banSeen = -1;
            banFor = -1;
            banCounters = null;
        }
    }
}
