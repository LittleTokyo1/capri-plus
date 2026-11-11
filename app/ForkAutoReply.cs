using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static readonly string[] AutoModeWords = { "chat", "status", "ask first" };

        private bool AutoReplyTake(FriendLine line, string route, bool notHome, bool game, DateTime now)
        {
            ForkSettings settings = ForkSettings.Current;
            if (!AutoReply.Covers(line, settings)) return false;
            if (route == "wait")
            {
                if (!notHome) return false;
                route = "digest";
            }
            DiscordInbox.Done(line);
            int mode = Math.Max(0, Math.Min(2, settings.AutoReplyMode));
            string status = notHome ? "away from his PC" : game ? "in a game" : route == "say" ? "at his PC" : "busy and can't look at messages right now";
            string shownRoute = route;
            Cloud remote = brain.Remote;
            string root = Vault.Root;
            ActivityLog.Write("discord", "auto-reply " + AutoModeWords[mode] + " " + route, line.Name);
            inboxSending = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                AutoPlan plan = null;
                bool forwarded = false;
                try
                {
                    plan = AutoReply.Handle(line, mode, shownRoute, status, notHome, remote, now, root);
                    if (plan.Dm != null) forwarded = Delivery.Send(plan.Dm);
                }
                catch (Exception error) { Brain.Log("auto-reply", -1, error.GetType().Name + ": " + error.Message); }
                Post(delegate
                {
                    inboxSending = false;
                    if (plan == null) return;
                    Digest.Add("discord", plan.Digest, line.When);
                    if (plan.Reply != null) brain.Own("discord", "Answered " + line.Name + " on Discord by myself (auto-reply): " + plan.Reply);
                    if (plan.Draft != null && !plan.Held) ActivityLog.Write("discord", "auto-reply draft kept for the digest", line.Name);
                    if (plan.Dm != null) ActivityLog.Write("discord", forwarded ? "forwarded to his DM" : "forward to his DM failed", line.Name);
                    if (plan.Spoken == null) return;
                    heard = "(from " + line.Name + ")";
                    box.Prompt = Bubble.Discord;
                    Answer(++asking, plan.Spoken, TalkState.Talking, chatting);
                });
            });
            return true;
        }

        private bool AutoReplyAnswered(string text, bool voiceChat)
        {
            FriendLine line;
            string draft;
            bool fromPhone = typedLine && discordLine != null;
            string verdict = AutoReply.Answered(text, DateTime.Now, voiceChat, fromPhone, out line, out draft);
            if (verdict == null) return false;
            if (fromPhone) discordLine = null;
            if (verdict == "drop" || line == null || draft == null)
            {
                ActivityLog.Write("discord", "auto-reply draft dropped", line == null ? null : line.Name);
                AutoReplyTold("Okay, I won't send it.", voiceChat, fromPhone);
                return true;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool sent = false;
                try { sent = AutoReply.Sender(line, draft); }
                catch (Exception error) { Brain.Log("auto-reply", -1, error.GetType().Name + ": " + error.Message); }
                Post(delegate
                {
                    ActivityLog.Write("discord", sent ? "auto-reply draft sent" : "auto-reply draft not sent", line.Name);
                    if (sent) brain.Own("discord", "Answered " + line.Name + " on Discord after his yes: " + draft);
                    AutoReplyTold(sent ? "Sent to " + line.Name + "." : "I couldn't send it to " + line.Name + ".", voiceChat, fromPhone);
                });
            });
            return true;
        }

        private void AutoReplyTold(string said, bool voiceChat, bool fromPhone)
        {
            if (fromPhone)
            {
                if (!testing) ThreadPool.QueueUserWorkItem(delegate { Delivery.Send(said); });
                return;
            }
            if (!voiceChat && !testing) Tell("(discord)", said);
        }
    }
}
