using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    // Capri+ League side: polling the client, hovering a favourite, the pro build panel, pushing builds, and her game spot.
    internal sealed partial class Companion
    {
        internal static readonly Regex PushWords = new Regex(@"\b(push|import|load|put in|set up|use)\b.*\b(build|runes?|page|item set)\b", RegexOptions.IgnoreCase);
        private League league;
        private double nextLeague;
        private volatile bool leagueBusy;
        private volatile bool researching;
        private int researchedFor = -1;
        private bool hovered, autoPushed, drafting;
        private int buildsFor = -1;
        private volatile List<ProBuild> builds;
        private volatile ChampSession lastSession;
        private LeaguePanel panel;
        private bool shrunk;
        private int sizeBefore;
        private Point spotBefore;

        private void PollLeague(double now)
        {
            ForkSettings settings = ForkSettings.Current;
            if (!(settings.LeagueCoach || settings.LockLeagueClient) || leagueBusy || now < nextLeague) return;
            nextLeague = now + (leagueFast ? FastPoll : 2);
            leagueBusy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { CheckLeague(); }
                catch (Exception error) { Brain.Log("league", -1, error.GetType().Name + ": " + error.Message); }
                finally { leagueBusy = false; }
            });
        }

        // On a worker thread. Anything touching her or a window goes through Post.
        private void CheckLeague()
        {
            if (!League.Open())
            {
                league.Disconnect();
                Lockout.LeaguePhase = null;
                if (drafting) Post(EndDraft);
                return;
            }
            if (!league.Connected && !league.Connect()) return;
            string phase = league.ReadPhase();
            // A failed read keeps the last phase, so a hiccup mid-draft doesn't unlock her hands.
            if (phase == null) return;
            Lockout.LeaguePhase = phase;
            if (phase == "InProgress" || ((phase == "Lobby" || phase == "None") && DateTime.UtcNow - LeagueSocial.Queued > TimeSpan.FromSeconds(15))) LeagueSocial.Queued = DateTime.MinValue;
            if (phase != "ChampSelect") leagueFast = false;
            if (phase == "ReadyCheck") ReadyTurn();
            BackTurn();
            if (phase != "ChampSelect")
            {
                if (drafting) Post(EndDraft);
                if (phase == "InProgress") LiveTurn(false); else EndLive();
                return;
            }
            ForkSettings settings = ForkSettings.Current;
            if (!settings.LeagueCoach) return;
            ChampSession session = league.ReadSession();
            if (session == null) return;
            drafting = true;
            lastSession = session;
            ChampSeat me = session.Seat;
            bool firstHover = settings.HoverFavorite && !hovered;
            if (firstHover) HoverFavourite(session, me);
            LockTurn(session);
            BanTurn(session);
            if (!firstHover && !leagueFast) RehoverTurn(session);
            int mine = DraftChampion(League.MyPick(session), me);
            if (settings.ShowBuilds && mine > 0 && mine != buildsFor) LoadBuilds(mine, me.Position);
            AutoPushTurn(session);
            string picks = league.Describe(session);
            league.Picks = picks;
            League.Brief = Briefing(picks);
            int enemies = League.EnemyCount(session);
            CounterTurn(session, false);
            if (ResearchWanted(enemies)) Research(picks, enemies);
        }

        private string Briefing(string picks)
        {
            string said = picks + league.Locked(lastSession);
            List<ProBuild> ready = builds;
            if (ready != null && ready.Count > 0)
            {
                List<string> lines = ready.ConvertAll(build => ProBuilds.Line(build, league));
                said += " Pro builds for their champ from probuildstats.com (they can say \"push build 1\", 2 or 3 to load one): " + string.Join(" ", lines.ToArray());
            }
            if (league.Researched.Length > 0) said += " Your research for this draft, from current-patch stats sites: " + league.Researched;
            return said;
        }

        // Hovers once per draft (locking in is LockTurn's, only while they are away): their first favourite for the lane that's free and owned.
        private void HoverFavourite(ChampSession session, ChampSeat me)
        {
            ChampAction pick = League.MyPick(session);
            if (pick == null || me == null) return;
            hovered = true;
            string[] favourites = ForkSettings.Current.Favorites(me.Position);
            if (favourites.Length == 0) return;
            int champion = league.Favourite(session, favourites, league.Pickable());
            if (champion <= 0) return;
            bool done = league.Hover(pick.Id, champion);
            ActivityLog.Write("league hover", league.Name(champion), done ? "hovered (not locked)" : "the client refused");
        }

        private void LoadBuilds(int champion, string position)
        {
            buildsFor = champion;
            string name = league.Name(champion);
            List<ProBuild> found = FindBuilds(champion, name, position);
            builds = found;
            if (found.Count == 0) return;
            List<string> lines = found.ConvertAll(build => ProBuilds.Line(build, league));
            Post(delegate { ShowBuildPanel(name, lines); });
        }

        private void ShowBuildPanel(string champion, List<string> lines)
        {
            if (Lockout.LeaguePhase != "ChampSelect") return;
            if (panel == null || panel.IsDisposed)
            {
                panel = new LeaguePanel();
                panel.PushAsked += delegate(int number) { PushBuild(number, false); };
            }
            panel.Fill(champion, lines, ForkSettings.Current.PushButton);
            panel.Beside(new Rectangle(anchor, Size));
            if (!testing && !panel.Visible) panel.Show();
        }

        private void EndDraft()
        {
            drafting = false;
            hovered = autoPushed = false;
            EndRunes();
            buildsFor = researchedFor = -1;
            EndCounters();
            EndAway();
            KeepDraftBuilds();
            builds = null;
            lastSession = null;
            League.Brief = null;
            league.Picks = "";
            league.Researched = "";
            if (panel != null && !panel.IsDisposed) panel.Hide();
        }

        private void Research(string picks, int enemies)
        {
            Cloud remote = brain.Remote;
            if (remote == null || !remote.Claude) return;
            researching = true;
            researchedFor = enemies;
            ActivityLog.Write("league research", picks, null);
            ChampSession session = lastSession;
            List<string> champions = league.Champions(), taken = session == null ? new List<string>() : Taken(session);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string found = null;
                try { found = Three(League.Research(remote, League.ResearchAsk(picks), true), champions, taken); }
                catch (Exception error) { Brain.Log("league research", -1, error.Message); }
                finally { researching = false; }
                if (found == null || !drafting) return;
                found = "Picks into their team: " + found + ".";
                league.Researched = found;
                League.Brief = Briefing(league.Picks);
                if (ForkSettings.Current.CountersUnasked) Post(delegate { if (Lockout.LeaguePhase != "ChampSelect") return; Tell("(champ select)", found); brain.Own("champ select research", found); });
            });
        }

        internal static int PushNumber(string text)
        {
            Match digit = Regex.Match(text ?? "", @"\b([1-3])\b");
            if (digit.Success) return digit.Groups[1].Value[0] - '0';
            if (Regex.IsMatch(text ?? "", @"\b(second|two|2nd)\b", RegexOptions.IgnoreCase)) return 2;
            if (Regex.IsMatch(text ?? "", @"\b(third|three|3rd|last)\b", RegexOptions.IgnoreCase)) return 3;
            return 1;
        }

        // "push build 2", "import the second build", "load the runes": handled here instead of by her brain.
        // In ElevenLabs voice chat her agent still answers out loud, so the result goes to the panel instead of her bubble.
        private bool ForkHeard(string text, bool voiceChat)
        {
            SituationSeen(text, voiceChat);
            if (CallAskHeard(text, voiceChat) || SchoolSendHeard(text, voiceChat) || AutoReplyAnswered(text, voiceChat) || SendWaitingHeard(text, voiceChat) || (!voiceChat && (AfterJobAnswered(text) || StudyAnswered(text))) || DoneHeard(text, voiceChat) || RepeatHeard(text, voiceChat) || UpdateHeard(text, voiceChat) || FriendWatchHeard(text, voiceChat) || LongHeard(text) || ConvoHeard(text, voiceChat) || TweakHeard(text, voiceChat) || CsHeard(text, voiceChat) || BuildHeard(text, voiceChat) || SchoolMailHeard(text, voiceChat) || DigestAsked(text, voiceChat) || StudyHeard(text, voiceChat) || SentHeard(text, voiceChat) || ShotHeard(text, voiceChat) || PresenceHeard(text, voiceChat) || HerMicHeard(text, voiceChat) || SpendHeard(text, voiceChat) || CountersHeard(text, voiceChat) || BrowserHeard(text, voiceChat)) return true;
            if (!ForkSettings.Current.PushVoice || !PushWords.IsMatch(text ?? "")) return false;
            if (!drafting) return false;
            PushBuild(PushNumber(text), !voiceChat);
            return true;
        }

        private readonly Dictionary<int, DateTime> recentPushes = new Dictionary<int, DateTime>();
        private const double PushRepeatSeconds = 3;

        internal static bool PushAgain(Dictionary<int, DateTime> pushes, int number, DateTime now)
        {
            lock (pushes)
            {
                DateTime last;
                if (pushes.TryGetValue(number, out last) && (now - last).TotalSeconds < PushRepeatSeconds) return true;
                pushes[number] = now;
                return false;
            }
        }

        private void PushBuild(int number, bool say)
        {
            List<ProBuild> ready = builds;
            ChampSession session = lastSession;
            if (ready == null || ready.Count == 0)
            {
                string none = "No pro builds loaded yet. Hover or pick your champ first.";
                if (say) Tell("(push build)", none);
                else if (panel != null && !panel.IsDisposed) panel.Status(none);
                return;
            }
            ProBuild build = ready[Math.Max(0, Math.Min(ready.Count - 1, number - 1))];
            if (PushAgain(recentPushes, build.Number, DateTime.Now)) return;
            if (panel != null && !panel.IsDisposed) panel.Status("Pushing build " + build.Number + "...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try { result = league.Push(build, session); }
                catch (Exception error) { result = "That push failed: " + error.Message; }
                KeepBuild(build);
                ActivityLog.Write("league push", "build " + build.Number + " (" + build.Match.Pro + ")", result);
                Post(delegate
                {
                    if (panel != null && !panel.IsDisposed) panel.Status(result);
                    if (say) Tell("(push build " + build.Number + ")", result);
                });
            });
        }

        // Tiny in her game spot for the match: wherever they left her last game, top-left the first time.
        private void ShrinkForGame()
        {
            ForkSettings settings = ForkSettings.Current;
            sizeBefore = preferences.Size;
            spotBefore = anchor;
            shrunk = true;
            if (seat != null) StandUp();
            SetCharacterSize(settings.GameSize);
            Rectangle work = Stage.Main.WorkingArea;
            Point spot = settings.HasGameSpot ? new Point(settings.GameSpotX, settings.GameSpotY) : new Point(work.Left + 16, work.Top + 16);
            anchor = KeepOnScreen(spot);
            Location = anchor;
        }

        private void GrowAfterGame()
        {
            if (!shrunk) return;
            shrunk = false;
            ForkSettings settings = ForkSettings.Current;
            settings.GameSpotX = anchor.X;
            settings.GameSpotY = anchor.Y;
            settings.HasGameSpot = true;
            if (!testing) ForkSettings.Save();
            if (seat != null) StandUp();
            SetCharacterSize(sizeBefore);
            anchor = KeepOnScreen(spotBefore);
            Location = anchor;
            if (!testing) SavePreferences();
        }
    }
}
