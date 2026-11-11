using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static string SmokeBanSession(int hovered, bool completed, long left)
        {
            return "{\"localPlayerCellId\":2,\"myTeam\":[{\"cellId\":2,\"championId\":0,\"championPickIntent\":238,\"assignedPosition\":\"middle\"}],\"theirTeam\":[],\"bans\":{\"myTeamBans\":[],\"theirTeamBans\":[]}," +
                "\"actions\":[[{\"id\":4,\"actorCellId\":2,\"championId\":" + hovered + ",\"type\":\"ban\",\"completed\":" + (completed ? "true" : "false") + ",\"isInProgress\":true}]]," +
                "\"timer\":{\"adjustedTimeLeftInPhase\":" + left + ",\"phase\":\"BAN_PICK\"}}";
        }

        private void SmokeDraftWait(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            League keptLeague = league;
            Func<long> keptIdle = LeagueIdle;
            Func<DateTime> keptClock = LeagueClock;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.AutoBan = true;
                DateTime clock = new DateTime(2026, 11, 4, 12, 21, 0, DateTimeKind.Utc);
                LeagueClock = delegate { return clock; };
                LeagueIdle = delegate { return 60000; };
                List<string> sent = new List<string>();
                Dictionary<string, string> replies = new Dictionary<string, string>();
                league = SmokeLeagueClient(sent, replies);
                EndBan();
                banFor = 238;
                banCounters = new List<string> { "Garen" };
                if (BanTurn(League.ParseSession(SmokeBanSession(0, false, 13000))) != null || sent.Count != 0) throw new Exception("She banned with 13 s left while he only sat still; he may have a better ban in mind.");
                if (BanTurn(League.ParseSession(SmokeBanSession(0, false, 6000))) != null || sent.Count != 0) throw new Exception("She banned with 6 s left.");
                leagueFast = false;
                if (BanTurn(League.ParseSession(SmokeBanSession(0, false, 3000))) != null || sent.Count != 0) throw new Exception("She banned with 3 s left; he wants the ban in the last second.");
                if (!leagueFast) throw new Exception("In the last 5 s of his ban she did not watch the client every 250 ms, so the last second could slip by.");
                if (BanTurn(League.ParseSession(SmokeBanSession(0, false, 1500))) != null || sent.Count != 0) throw new Exception("She banned with 1.5 s left.");
                replies["/lol-champ-select/v1/session"] = SmokeBanSession(86, true, 500);
                string banned = BanTurn(League.ParseSession(SmokeBanSession(0, false, 900)));
                if (banned != "Garen" || sent.Count == 0 || sent[0] != "PATCH /lol-champ-select/v1/session/actions/4 {\"championId\":86,\"completed\":true}") throw new Exception("With 1 s left and no ban of his she did not ban: " + banned + " / " + string.Join(" || ", sent.ToArray()));
                EndBan();
                sent.Clear();
                banFor = 238;
                banCounters = new List<string> { "Garen" };
                if (BanTurn(League.ParseSession(SmokeBanSession(134, false, 900))) != null || sent.Count != 0) throw new Exception("She replaced the ban he hovered himself.");
                RunePage[] start = { new RunePage { Id = 10, Name = "My Zed", Current = true, Modified = 1000 }, new RunePage { Id = 11, Name = "Lethality" } };
                RunePage[] picked = { new RunePage { Id = 10, Name = "My Zed", Modified = 1000 }, new RunePage { Id = 11, Name = "Lethality", Current = true } };
                RunePage[] edited = { new RunePage { Id = 10, Name = "My Zed", Current = true, Modified = 5000 } };
                RunePage[] hers = { new RunePage { Id = 12, Name = "Capri: Zed 1", Current = true, Modified = 9000 } };
                if (RunesChanged(10, 1000, start) || !RunesChanged(10, 1000, picked) || !RunesChanged(10, 1000, edited) || RunesChanged(10, 1000, hers) || RunesChanged(10, 1000, null))
                    throw new Exception("A rune page he picked or edited in champ select was not seen as his, or an untouched one was.");
                checks.Add("PASS champ select waits for him: a ban goes in only in the last second (his pick 12:46), away or not, with the client watched every 250 ms in its last 5 s, never over a ban he hovered; an away lock-in only with 2 s left; auto-push on lock-in never pushes over a rune page he picked or edited in that champ select (it says so and Push still works)");
            }
            finally
            {
                ForkSettings.Current = keptSettings;
                league = keptLeague;
                LeagueIdle = keptIdle;
                LeagueClock = keptClock;
                EndBan();
                EndRunes();
            }
        }
    }
}
