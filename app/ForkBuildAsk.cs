using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const string BuildAskOff = @"(?!\s+(?:for|in|at|from)\s+(?!(?:this|me|us|now|next|my|our)\b|the\s+(?:game|match|next)\b))";
        internal static readonly Regex BuildAsked = new Regex(
            @"\bwhat\s+(?:do|should|shall|can|must|would)\s+(?:i|we)\s+(?:need\s+to\s+|have\s+to\s+)?(?:build|buy|rush)\b" + BuildAskOff +
            @"|\bwhat\s+(?:do|should|shall|can)\s+(?:i|we)\s+(?:get|go)\s+next\b" +
            @"|\bwhat\s+(?:to|i\s+should|should\s+i)\s+(?:build|buy)\b" + BuildAskOff +
            @"|\b(?:full|whole|complete|entire)\s+build\b" +
            @"|\bwhat(?:[^\w\s]?s|\s+is|\s+was)\s+(?:my|our|the|that)\s+(?:pro\s+|item\s+)?build\b(?!\s+(?:for|on|of|against|in)\s+(?!(?:me|us|this|my)\b))" +
            @"|\b(?:tell|remind|give|read|show)\s+me\s+(?:of\s+)?(?:the|my|that|this)\s+(?:pro\s+|item\s+)?build\b" +
            @"|\b(?:my|the)\s+next\s+items?\b|\bwhat(?:[^\w\s]?s|\s+is)?\s+next\s+item\b" +
            @"|\bwhat\s+items?\s+(?:do|should|can|shall)\s+(?:i|we)\b|\bwhat\s+items?\s+(?:next|now|to\s+(?:build|buy|get))\b|\bwhat\s+items?\W*$" +
            @"|\b(?:my|the|our)\s+(?:item|build)\s+order\b",
            RegexOptions.IgnoreCase);
        internal const int BuildItems = 6;
        private volatile ProBuild pushedBuild;
        private volatile List<ProBuild> keptBuilds;
        private volatile bool buildLoading;
        private volatile int buildTried = -1;
        private readonly object buildGate = new object();

        internal static int DraftChampion(ChampAction pick, ChampSeat me)
        {
            if (me == null) return 0;
            if (pick != null && pick.Completed && pick.Champion > 0) return pick.Champion;
            return me.Champion > 0 ? me.Champion : me.Intent;
        }

        internal static bool BuildsOf(List<ProBuild> ready, int champion)
        {
            return champion > 0 && ready != null && ready.Count > 0 && ready[0] != null && ready[0].Match != null && ready[0].Match.Champion == champion;
        }

        internal static bool PushReady(ChampAction pick, ChampSeat me, List<ProBuild> ready)
        {
            if (pick == null || me == null || !pick.Completed || pick.Champion <= 0) return false;
            int seat = me.Champion > 0 ? me.Champion : me.Intent;
            return seat == pick.Champion && BuildsOf(ready, pick.Champion);
        }

        internal string AutoPushTurn(ChampSession session)
        {
            List<ProBuild> ready = builds;
            ChampAction pick = League.MyPick(session);
            if (!ForkSettings.Current.PushOnLock || autoPushed) return null;
            RunesAtStart();
            if (!PushReady(pick, session == null ? null : session.Seat, ready)) return null;
            autoPushed = true;
            if (RunesHisOwn())
            {
                ActivityLog.Write("league push", "kept his runes (" + league.Name(pick.Champion) + ")", "he picked or changed a rune page in champ select, nothing pushed");
                Post(delegate { if (panel != null && panel.Visible) panel.Status(RunesKept); });
                return RunesKept;
            }
            ProBuild build = ready[0];
            string result = league.Push(build, session);
            KeepBuild(build);
            ActivityLog.Write("league push", "build 1 on lock-in (" + league.Name(pick.Champion) + ")", result);
            Post(delegate { if (panel != null && panel.Visible) panel.Status(result); });
            return result;
        }

        private List<ProBuild> FindBuilds(int champion, string name, string position)
        {
            List<ProBuild> found = ProBuilds.Top(ProBuilds.Matches(champion, position), position, 3);
            ActivityLog.Write("pro builds", name, found.Count > 0 ? found.Count + " builds" : "none found" + (ProBuilds.Shape.Length > 0 ? " (answer: " + ProBuilds.Shape + ")" : ""));
            return found;
        }

        private void KeepBuild(ProBuild build)
        {
            if (build != null && build.Match != null) pushedBuild = build;
        }

        private void KeepDraftBuilds()
        {
            string phase = Lockout.LeaguePhase;
            if (phase != "GameStart" && phase != "InProgress")
            {
                pushedBuild = null;
                keptBuilds = null;
                return;
            }
            List<ProBuild> ready = builds;
            if (ready != null && ready.Count > 0) keptBuilds = ready;
        }

        private void EndGameBuild()
        {
            pushedBuild = null;
            keptBuilds = null;
            buildTried = -1;
        }

        private ProBuild KnownBuild(int champion)
        {
            if (champion <= 0) return null;
            ProBuild pushed = pushedBuild;
            if (pushed != null && pushed.Match != null && pushed.Match.Champion == champion) return pushed;
            foreach (List<ProBuild> ready in new[] { keptBuilds, builds })
                if (BuildsOf(ready, champion)) return ready[0];
            return null;
        }

        private ProBuild LoadGameBuild(LivePlayer me, int champion)
        {
            lock (buildGate)
            {
                ProBuild known = KnownBuild(champion);
                if (known != null) return known;
                buildTried = champion;
                List<ProBuild> found = FindBuilds(champion, me.Champion + " (in game)", me.Position);
                if (found.Count > 0) keptBuilds = found;
                return KnownBuild(champion);
            }
        }

        private void WantGameBuild(LivePlayer me, int champion)
        {
            if (testing || champion <= 0 || buildLoading || buildTried == champion) return;
            buildLoading = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { LoadGameBuild(me, champion); }
                catch (Exception error) { Brain.Log("pro builds", -1, error.Message); }
                finally { buildLoading = false; }
            });
        }

        private string BuildBrief(LiveData data)
        {
            LivePlayer me = LiveGame.Mine(data);
            if (me == null) return "";
            int champion = league.IdOf(me.Champion);
            ProBuild build = KnownBuild(champion);
            if (build == null)
            {
                WantGameBuild(me, champion);
                return "";
            }
            return BuildNote(build, league, me);
        }

        internal string BuildAnswer(LiveData data, bool load)
        {
            LivePlayer me = LiveGame.Mine(data);
            if (me == null) return null;
            int champion = league.IdOf(me.Champion);
            ProBuild build = KnownBuild(champion);
            if (build == null && load && champion > 0) build = LoadGameBuild(me, champion);
            if (build != null) return BuildSaid(build, league, me);
            return load ? "I couldn't find a pro build for " + me.Champion + "." : null;
        }

        private bool BuildHeard(string text, bool voiceChat)
        {
            if (voiceChat || !BuildAsked.IsMatch(text ?? "")) return false;
            LiveData data = lastLive;
            if (data == null || (DateTime.Now - lastLiveAt).TotalSeconds > FreshLive || LiveGame.Mine(data) == null) return false;
            string said = BuildAnswer(data, false);
            if (said != null)
            {
                SayBuild(text, said);
                return true;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                string found = null;
                try { found = BuildAnswer(data, true); }
                catch (Exception error) { Brain.Log("build ask", -1, error.Message); }
                string line = found ?? "I couldn't reach the pro builds right now.";
                Post(delegate { SayBuild(text, line); });
            });
            return true;
        }

        private void SayBuild(string text, string said)
        {
            ActivityLog.Write("build ask", text, said);
            brain.Own("he asked for his build in game", said);
            Tell("(your build)", said);
        }

        private static bool Owns(LivePlayer me, int id, string name)
        {
            foreach (LiveItem item in me == null || me.Items == null ? new LiveItem[0] : me.Items)
                if (item != null && (item.Id == id || (item.Id == 0 && League.Plain(item.Name) == League.Plain(name)))) return true;
            return false;
        }

        private static void Split(ProBuild build, League names, LivePlayer me, List<string> order, List<string> had, List<string> next)
        {
            List<int> seen = new List<int>();
            foreach (int id in build.Full())
            {
                if (id <= 0 || seen.Contains(id) || seen.Count >= BuildItems) continue;
                seen.Add(id);
                string name = names.Item(id);
                if (string.IsNullOrEmpty(name) || name.StartsWith("#", StringComparison.Ordinal)) continue;
                order.Add(name);
                if (Owns(me, id, name)) had.Add(name); else next.Add(name);
            }
        }

        private static string Both(List<string> names)
        {
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + " and " + names[names.Count - 1];
        }

        internal static string BuildSaid(ProBuild build, League names, LivePlayer me)
        {
            List<string> order = new List<string>(), had = new List<string>(), next = new List<string>();
            Split(build, names, me, order, had, next);
            string pro = build.Match.Pro;
            if (order.Count == 0) return "I couldn't read the items of " + pro + "'s build.";
            if (had.Count == 0) return "Pro build from " + pro + (Filled(build) ? " and what pros build most" : "") + ": " + string.Join(", ", order.ToArray()) + ".";
            if (next.Count == 0) return "You've got the whole pro build already.";
            string rest = next.Count > 1 ? ", then " + string.Join(", ", next.GetRange(1, next.Count - 1).ToArray()) : "";
            return "You've got " + Both(had) + " from the pro build. Next: " + next[0] + rest + ".";
        }

        internal static bool Filled(ProBuild build)
        {
            List<int> own = new List<int>(build.Match.Completed ?? new int[0]);
            foreach (int id in build.Full()) if (!own.Contains(id)) return true;
            return false;
        }

        internal static string BuildNote(ProBuild build, League names, LivePlayer me)
        {
            List<string> order = new List<string>(), had = new List<string>(), next = new List<string>();
            Split(build, names, me, order, had, next);
            if (order.Count == 0) return "";
            string note = " Their build this game is " + build.Match.Pro + "'s pro " + me.Champion + " build from a recent pro game" + (Filled(build) ? ", filled up with what pros build most" : "") + ", in buy order: " + string.Join(", ", order.ToArray()) + ".";
            if (had.Count > 0) note += next.Count == 0 ? " They have all of it." : " They have " + Both(had) + "; next is " + next[0] + ".";
            return note + " When they ask what to build or buy, answer from this pro build, never from general League knowledge or a search.";
        }
    }
}
