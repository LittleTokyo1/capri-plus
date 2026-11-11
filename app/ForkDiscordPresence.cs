using System;
using System.Collections.Generic;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int PresenceMapMinutes = 30;
        private const int PresenceSampleMinutes = 10;
        private DateTime presenceMapAt = DateTime.MinValue;
        private DateTime presenceSampleAt = DateTime.MinValue;
        private bool presenceMapping;
        private bool presenceSending;

        private void PresenceTick(DateTime now, ForkSettings settings)
        {
            if (DiscordGateway.Stepped == null) DiscordGateway.Stepped = delegate(GatewayStep step) { DiscordPresence.Took(step, DateTime.Now, ForkSettings.Current.DiscordOnline); };
            if (!Discord.Ready || !DiscordGateway.Live || DiscordGateway.PresencesOff) return;
            if (!presenceMapping && (now - presenceMapAt).TotalMinutes >= PresenceMapMinutes)
            {
                presenceMapping = true;
                presenceMapAt = now;
                string root = Vault.Root;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        string problem;
                        List<DiscordMember> members = DiscordFriends.MemberList(out problem);
                        if (members != null) DiscordPresence.Track(DiscordPresence.Map(members, root));
                    }
                    catch (Exception error) { Brain.Log("discord presence", -1, error.GetType().Name + ": " + error.Message); }
                    finally { presenceMapping = false; }
                });
            }
            if (FriendOrders.Pending(now)) FriendOrdersTick(now);
            if (FriendWatch.Active(now) || friendsArrived.Count > 0 || friendNews.Count > 0) FriendWatchTick(now, settings);
            if (!settings.DiscordOnline) return;
            if ((now - presenceSampleAt).TotalMinutes >= PresenceSampleMinutes)
            {
                presenceSampleAt = now;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        DiscordPresence.Sample(now);
                        DiscordPresence.Learn(now);
                    }
                    catch (Exception error) { Brain.Log("discord presence", -1, error.GetType().Name + ": " + error.Message); }
                });
            }
            PresenceHeadsTick(now, settings);
        }

        private void PresenceHeadsTick(DateTime now, ForkSettings settings)
        {
            if (!Lockout.Active && PresenceHold.Any) ArrivalsHeld(DiscordPresence.OnlineFriends(), now);
            if (presenceSending) return;
            PresenceHeads one = DiscordPresence.NextHeads(now);
            if (one == null) return;
            if (ArrivalTold(one.Id, now))
            {
                DiscordPresence.Told(one, now);
                ActivityLog.Write("discord", "friend online already told", one.Name);
                return;
            }
            if (Delivery.NotHome())
            {
                if (!Delivery.Reachable() || !Delivery.HeadsMay(settings, now, true)) { DiscordPresence.Drop(one); return; }
                if (FriendHeld(one, now)) return;
                presenceSending = true;
                Arrivals.Mark(one.Id, now);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool sent = false;
                    try { sent = Delivery.Send("Heads-up: " + one.Text); }
                    catch (Exception error) { Brain.Log("discord presence", -1, error.GetType().Name); }
                    Post(delegate
                    {
                        presenceSending = false;
                        if (sent)
                        {
                            DiscordPresence.Told(one, DateTime.Now);
                            brain.Own("discord", "Sent to his phone on Discord: " + one.Text);
                        }
                        else
                        {
                            DiscordPresence.Drop(one);
                            Arrivals.Unmark(one.Id);
                            ProactiveGate.Unsay(one.Text);
                        }
                        ActivityLog.Write("discord", sent ? "friend online sent to his DM" : "friend online not sent", one.Name);
                    });
                });
                return;
            }
            if (Lockout.Active) { PresenceHold.Hold(one); return; }
            if (settings.Paused || Autonomy.Quiet(now, settings.QuietFrom, settings.QuietTo)) { DiscordPresence.Drop(one); return; }
            if (!HeartMaySpeak(settings) || DiscordBusy() || clock.Elapsed.TotalSeconds < voiceUntil) return;
            if (FriendHeld(one, now)) return;
            DiscordPresence.Told(one, now);
            Arrivals.Mark(one.Id, now);
            brain.Own("discord", one.Text);
            ActivityLog.Write("discord", "friend online told", one.Name);
            heard = "(discord)";
            box.Prompt = Bubble.Sys;
            Answer(++asking, one.Text, TalkState.Talking, chatting);
        }
    }
}
