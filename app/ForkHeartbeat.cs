using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int HeartLineGapSeconds = 90;
        private bool beating;
        private bool heartSending;
        private DateTime lastHeartLine = DateTime.MinValue;

        private void ForkHeartbeatTick()
        {
            if (testing) return;
            ForkSettings settings = ForkSettings.Current;
            if (!settings.HeartbeatOn || settings.HeartbeatMinutes <= 0) return;
            DateTime now = DateTime.Now;
            GameOffers(now);
            if (Lockout.Active && (now - lastHeartLine).TotalSeconds >= HeartLineGapSeconds && MayMurmur(settings))
            {
                string offer = Heartbeat.Next(now, true, delegate(string text) { return ProactiveGate.InGame(text, League.Brief); }, ProactiveGate.Game);
                if (offer != null)
                {
                    lastHeartLine = now;
                    Murmur(offer, now);
                }
            }
            if (!HeartAway(settings, now) && HeartMaySpeak(settings) && (now - lastHeartLine).TotalSeconds >= HeartLineGapSeconds)
            {
                string line = Heartbeat.Next(now, true);
                if (line != null)
                {
                    lastHeartLine = now;
                    bool offer = Autonomy.IsOffer(line);
                    Autonomy.Delivered(line, now);
                    if (offer) OfferLine(line, now);
                    else HeartLine(line);
                }
            }
            if (beating || !Heartbeat.Due(now, settings.HeartbeatMinutes)) return;
            beating = true;
            Cloud remote = brain.Remote;
            int minutes = settings.HeartbeatMinutes, perDay = settings.HeartbeatPerDay;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (Heartbeat.Poll(now, remote, minutes, perDay, Vault.Root) == "capped") SilenceLog.Once("heartbeat", "capped", "all " + perDay + " heartbeat wake-ups used today, the rest waits until tomorrow", now);
                }
                catch (Exception error)
                {
                    Brain.Log("heartbeat", -1, error.GetType().Name + ": " + error.Message);
                    Heartbeat.Hold(now, minutes);
                }
                try
                {
                    if (Autonomy.Think(now, remote, Vault.Root, perDay) == "capped") SilenceLog.Once("autonomy", "capped", "all " + Budgets.Share(perDay) + " idea passes used today, no more offers until tomorrow", now);
                }
                catch (Exception error) { Brain.Log("autonomy", -1, error.GetType().Name + ": " + error.Message); }
                finally { beating = false; }
            });
        }

        private DateTime gameSince = DateTime.MinValue;

        private void GameOffers(DateTime now)
        {
            if (Lockout.Active)
            {
                if (gameSince == DateTime.MinValue) gameSince = now;
                return;
            }
            if (gameSince == DateTime.MinValue) return;
            int dropped = Heartbeat.DropSince(gameSince, Autonomy.IsOffer);
            if (dropped > 0) ActivityLog.Write("proactive gate", "game over", dropped + " offer(s) made during the game dropped, they were about that moment");
            gameSince = DateTime.MinValue;
        }

        private bool HeartMaySpeak(ForkSettings settings)
        {
            return settings.Proactive >= 1 && !Autonomy.Silent(settings, DateTime.Now) && !settings.Paused && !settings.NightOn && !Lockout.Active && (!chatting || ChatIdle()) && !hidden && !dragging && speech == null && talkState == TalkState.Off && !typer.Visible;
        }

        private bool HeartAway(ForkSettings settings, DateTime now)
        {
            bool notHome = Delivery.NotHome();
            if (!Delivery.Reachable() || !Delivery.HeadsMay(settings, now, notHome)) return false;
            HeartDm(now, notHome);
            return true;
        }

        private void HeartDm(DateTime now, bool notHome)
        {
            if (heartSending || (now - lastHeartLine).TotalSeconds < HeartLineGapSeconds) return;
            string line = notHome ? Heartbeat.Next(now, true) : Heartbeat.NextRelayed(now);
            if (line == null) return;
            lastHeartLine = now;
            heartSending = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool sent = false;
                try { sent = Delivery.Send("Heads-up: " + line); }
                catch (Exception error) { Brain.Log("heartbeat", -1, error.GetType().Name + ": " + error.Message); }
                Post(delegate
                {
                    heartSending = false;
                    if (sent)
                    {
                        Autonomy.Delivered(line, DateTime.Now);
                        brain.Own("heartbeat", "Sent to his phone on Discord: " + line);
                        Digest.Add(Autonomy.IsOffer(line) ? "offer" : "heads", line, DateTime.Now);
                    }
                    ActivityLog.Write("heartbeat", sent ? "sent" : Heartbeat.Unclaim(line) ? "not sent, kept for later" : "not sent", line);
                });
            });
        }

        private void HeartLine(string line)
        {
            heard = "(heartbeat)";
            box.Prompt = "> sys: ";
            brain.Own("heartbeat", line);
            ActivityLog.Write("heartbeat", "told", line);
            Answer(++asking, line, TalkState.Talking, chatting);
        }
    }
}
