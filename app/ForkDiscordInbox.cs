using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool inboxWired;
        private bool inboxSending;
        private DateTime inboxAt = DateTime.MinValue;

        private void ForkInboxTick()
        {
            if (testing) return;
            DateTime now = DateTime.Now;
            if ((now - inboxAt).TotalSeconds < 1) return;
            inboxAt = now;
            ForkSettings settings = ForkSettings.Current;
            if (!inboxWired)
            {
                inboxWired = true;
                DiscordGateway.Heard = delegate(GatewayMessage message) { DiscordInbox.Take(message, DateTime.Now); };
                DiscordGateway.Opened = DiscordInbox.CatchUp;
            }
            DiscordGateway.Keep(settings.DiscordOn && Discord.Ready && Discord.Failed == null, Voice.Unprotect(settings.DiscordToken));
            PresenceTick(now, settings);
            if (inboxSending) return;
            FriendLine line = DiscordInbox.Next(now);
            if (line == null) return;
            bool notHome = Delivery.NotHome();
            bool game = Lockout.Active;
            bool free = !hidden && !dragging && speech == null && talkState == TalkState.Off && !typer.Visible && !DiscordBusy() && (!chatting || ChatIdle()) && clock.Elapsed.TotalSeconds >= voiceUntil;
            string route = DiscordInbox.Route(notHome, Delivery.Reachable(), Autonomy.Quiet(now, settings.QuietFrom, settings.QuietTo), game, settings.Paused, free, (now - line.First).TotalMinutes);
            route = LongJob.AnswerRoute(route, now);
            if (AutoReplyTake(line, route, notHome, game, now)) return;
            if (route == "wait") return;
            DiscordInbox.Done(line);
            if (route != "digest" && !DiscordInbox.Allow(line.Author, now)) route = "digest";
            ActivityLog.Write("discord", "friend message " + route, line.Name);
            if (route == "say")
            {
                FriendSay(line);
                return;
            }
            int mode = LongJob.AnswerMode(settings.DiscordFriendAnswers, now);
            bool answer = DiscordInbox.AnswerMay(mode, notHome, line.Author, now);
            string status = notHome ? "away from his PC" : game ? "in a game" : "busy and can't look at messages right now";
            bool phone = route == "phone";
            Cloud remote = brain.Remote;
            inboxSending = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string reply = null;
                bool sent = false;
                try
                {
                    if (answer)
                    {
                        string text = DiscordInbox.Reply(mode, line, status, remote, now);
                        if (text != null && DiscordInbox.Send(line, text)) reply = text;
                    }
                    if (phone) sent = Delivery.Send(DiscordInbox.Forward(line, reply));
                }
                catch (Exception error) { Brain.Log("discord inbox", -1, error.GetType().Name); }
                Post(delegate
                {
                    inboxSending = false;
                    Digest.Add("discord", DiscordInbox.Digested(line) + (reply == null ? "" : " (I answered: " + reply + ")"), line.When);
                    if (phone) ActivityLog.Write("discord", sent ? "forwarded to his DM" : "forward to his DM failed", line.Name);
                });
            });
        }

        private void FriendSay(FriendLine line)
        {
            heard = "(from " + line.Name + ")";
            box.Prompt = Bubble.Discord;
            Answer(++asking, DiscordInbox.Spoken(line), TalkState.Talking, chatting);
        }
    }
}
