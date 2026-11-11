using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    // SEELE 4: his DMs to the bot are typed lines to her normal brain; her answer goes back to the DM, not out loud.
    internal sealed partial class Companion
    {
        private const int DiscordWindowMinutes = 15;
        private const int DiscordPictureSize = 1600;
        private const int DiscordLineLimit = Fork.MessageLimit;
        private const int DiscordShotFollowMinutes = 10;
        private DateTime lastShotAsk = DateTime.MinValue;
        private string discordLine;
        private DateTime discordSince = DateTime.MinValue;
        private int discordTicket = -1;
        private bool discordPolling;

        // His next DM waits on Discord while she is still on the last one, so split messages never cancel each other.
        private bool DiscordBusy()
        {
            if (discordLine == null) return false;
            bool superseded = discordTicket != -1 && discordTicket != asking;
            if (superseded || (DateTime.Now - discordSince).TotalMinutes > DiscordWindowMinutes) discordLine = null;
            return discordLine != null;
        }

        private void ForkDiscordTick()
        {
            ForkInboxTick();
            if (testing || discordPolling || DiscordBusy() || !Discord.Due(DateTime.Now)) return;
            discordPolling = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Discord.Poll(delegate(string line) { Post(delegate { DiscordHeard(line); }); }); }
                catch (Exception error) { Brain.Log("discord", -1, error.GetType().Name + ": " + error.Message); }
                finally { discordPolling = false; }
            });
        }

        private void DiscordHeard(string line)
        {
            bool afterShot = (DateTime.Now - lastShotAsk).TotalMinutes < DiscordShotFollowMinutes;
            if (Discord.WantsShot(line)) lastShotAsk = DateTime.Now;
            if (Discord.OnlyShot(line, afterShot))
            {
                lastShotAsk = DateTime.Now;
                if (!testing) Delivery.Heard("> discord: ");
                ActivityLog.Write("discord", "screenshot", Screens.Wording(line) ?? "screen");
                byte[] picture = DiscordShot(line, "");
                string note = picture == null ? "I couldn't grab the screen just now." : "Here you go.";
                ThreadPool.QueueUserWorkItem(delegate { Discord.Reply(note, picture, null); });
                return;
            }
            discordLine = line;
            discordSince = DateTime.Now;
            discordTicket = -1;
            ActivityLog.Write("discord", "message", null);
            if (line.Length > DiscordLineLimit) ThreadPool.QueueUserWorkItem(delegate { Discord.Send("I only read the first " + DiscordLineLimit + " characters of that. Send the rest again if it matters."); });
            typedLine = true;
            ChatArchive.From("Discord");
            try { Hear(line, line, "> discord: "); }
            finally { typedLine = false; }
            if (discordLine != null) discordTicket = asking;
        }

        // Her answer to a line that came from Discord goes to Discord; on the PC it only shows in her bubble.
        private bool ForkDiscordAnswer(int ticket, ref string said, TalkState state)
        {
            said = Musing.Strip(said);
            Autonomy.Spoke(said, DateTime.Now);
            string spoken = Discord.Plain(said);
            string reply = said;
            said = spoken;
            string line = discordLine;
            if (line == null) return false;
            if ((DateTime.Now - discordSince).TotalMinutes > DiscordWindowMinutes) { discordLine = null; return false; }
            if (ticket != asking || (discordTicket != -1 && ticket != discordTicket)) return false;
            discordLine = null;
            byte[] shot = Discord.WantsShot(line) || Discord.MarkedShot(reply) ? DiscordShot(line, reply) : null;
            string file = Discord.FileIn(line);
            Begin(ticket, spoken, state, null);
            ThreadPool.QueueUserWorkItem(delegate { Discord.Reply(reply, shot, file); });
            return true;
        }

        private static readonly Regex ShotToPhone = new Regex(@"\b(on|to|via|over|through|in)\s+(my\s+)?(discord|phone|dms?)\b", RegexOptions.IgnoreCase);

        // At the PC, "send me a screenshot of my main monitor on discord" goes to his DM from code (her discord tool sends only text).
        internal static bool ShotToDiscord(string line)
        {
            return Discord.WantsShot(line) && ShotToPhone.IsMatch(line ?? "") && ShotOnly(line);
        }

        internal static string ShotPlace(string wording)
        {
            if (string.IsNullOrWhiteSpace(wording)) return "your screen";
            string word = wording.Trim();
            if (Regex.IsMatch(word, @"^\d+$")) return "monitor " + word;
            return Regex.IsMatch(word, @"\b(monitor|screen|display)\b", RegexOptions.IgnoreCase) ? "your " + word : "your " + word + " monitor";
        }

        private bool ShotHeard(string text, bool voiceChat)
        {
            if (!ShotToDiscord(text)) return false;
            string where = ShotPlace(Screens.Wording(text));
            if (Blocklisted())
            {
                ActivityLog.Write("discord", "screenshot refused", "a never-look app is in front");
                if (!voiceChat) Tell("(discord)", "Not sending that: an app on your never-look list is in front.");
                return true;
            }
            if (string.IsNullOrEmpty(ForkSettings.Current.DiscordChannel))
            {
                if (!voiceChat) Tell("(discord)", "My Discord isn't connected yet, so I can't send it there. Say connect my Discord.");
                return true;
            }
            if (!voiceChat) Tell("(discord)", "Sending a screenshot of " + where + " to your Discord.");
            byte[] picture = DiscordShot(text, "");
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool sent = false;
                try { sent = picture != null && Discord.SendFile(picture, "screen.jpg", "Here's " + where + "."); }
                catch (Exception error) { Brain.Log("discord", -1, error.GetType().Name + ": " + error.Message); }
                ActivityLog.Write("discord", "screenshot to him", where + (sent ? " sent" : " not sent"));
                string said = sent ? "Sent a screenshot of " + where + " to your Discord." : "I couldn't send the screenshot to your Discord just now.";
                Post(delegate
                {
                    brain.Remember(text, said);
                    if (!voiceChat && !sent) Tell("(discord)", said);
                });
            });
            return true;
        }
        private bool PresenceHeard(string text, bool voiceChat)
        {
            string tool;
            ToolArguments args;
            if (voiceChat || !Hands.Heard(text, out tool, out args) || tool != "discord" || (args.Action != "online" && args.Action != "received")) return false;
            string said = DiscordFriends.Do(args);
            ActivityLog.Write("discord", args.Action + " asked", args.Name);
            Tell("(discord)", said);
            return true;
        }

        private byte[] DiscordShot(string line, string reply)
        {
            try
            {
                Point cursor = Stage.Pointer();
                Rectangle? named = Screens.Area(line, Screens.All(), cursor) ?? Screens.Area(reply, Screens.All(), cursor);
                using (Bitmap screen = named.HasValue ? CaptureScreen(named.Value) : CaptureScreen()) return screen == null ? null : Jpeg(screen, DiscordPictureSize);
            }
            catch (Exception) { return null; }
        }
    }
}
