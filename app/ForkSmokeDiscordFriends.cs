using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeDiscordFriends(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath, keptBase = Discord.Base;
            string folder = Path.Combine(Path.GetTempPath(), "capri-discord-friends-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            DiscordFake fake = new DiscordFake();
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Discord.Base = fake.Base;
                Discord.Reset();
                DiscordFriends.Forget();
                ForkSettings settings = ForkSettings.Current;
                settings.DiscordToken = Voice.Protect(DiscordFake.Token);
                settings.DiscordPaired = true;
                settings.DiscordChannel = "chan1";
                settings.DiscordOwner = "900";
                fake.Members = "[{\"user\":{\"id\":\"900\",\"username\":\"kasper\"}},{\"user\":{\"id\":\"501\",\"username\":\"rudce.x\",\"global_name\":\"Rudiminas\"},\"nick\":\"Rudce\"},{\"user\":{\"id\":\"502\",\"username\":\"samuel_k\",\"global_name\":\"Sam\"}},{\"user\":{\"id\":\"503\",\"username\":\"sam.b\",\"global_name\":\"Sam\"}},{\"user\":{\"id\":\"504\",\"username\":\"helperbot\",\"bot\":true}},{\"user\":{\"id\":\"505\",\"username\":\"closed.dm\"},\"nick\":\"Closed\"},{\"user\":{\"id\":\"506\",\"username\":\"rowan.q\",\"global_name\":\"Rowan\"}}]";

                Brain.Asking = null;
                string noAsk = DiscordFriends.Message("Rudce", "hop on the game", null);
                Brain.Asking = "what's the weather in amsterdam";
                string wrongAsk = DiscordFriends.Message("Rudce", "hop on the game", null);
                Brain.Asking = "message Rudce on Discord that we play at 9";
                string instructive = DiscordFriends.Message("Rudce", "tell him to hop on", null);
                if (!noAsk.Contains("only message") || !wrongAsk.Contains("only message") || !instructive.Contains("reads like") || fake.Friends.Count != 0 || Honest.Instructive("hop on the game") || !Honest.Instructive("Tell Rudce to hop on the game")) throw new Exception("A message went out without his ask, or his instruction went out as the text: " + noAsk + " | " + wrongAsk + " | " + instructive);

                string sent = DiscordFriends.Message("Rudce", "hop on the game @everyone", null);
                if (!sent.StartsWith("Sent to Rudce", StringComparison.Ordinal) || !sent.Contains("hop on the game") || fake.Friends.Count != 1 || !fake.Friends[0].StartsWith("chan501 | ", StringComparison.Ordinal) || !fake.Friends[0].Contains("\"allowed_mentions\":{\"parse\":[]}") || !fake.Friends[0].Contains("hop on the game")) throw new Exception("A message to a friend was not sent as the bot with no mentions: " + sent + " | " + string.Join(" / ", fake.Friends.ToArray()));
                if (fake.Count("chan1/messages") != 0) throw new Exception("A friend's message went to his own DM.");

                string byAccount = DiscordFriends.Message("Sam", "hi", new List<string> { "sam.b" });
                string byGlobal = DiscordFriends.Message("rudiminas", "yo", null);
                if (!byAccount.StartsWith("Sent to", StringComparison.Ordinal) || !fake.Friends[1].StartsWith("chan503", StringComparison.Ordinal) || !byGlobal.StartsWith("Sent to", StringComparison.Ordinal) || !fake.Friends[2].StartsWith("chan501", StringComparison.Ordinal)) throw new Exception("The Discord line or the display name did not find the right member: " + byAccount + " | " + byGlobal);

                string byDisplay = DiscordFriends.Message("Zed", "yo again", new List<string> { "rudce" });
                if (!byDisplay.StartsWith("Sent to", StringComparison.Ordinal) || fake.Friends.Count != 4 || !fake.Friends[3].StartsWith("chan501", StringComparison.Ordinal)) throw new Exception("A Discord line that holds his display name, not his username, did not find the member: " + byDisplay);

                if (!DiscordFriends.Hint(true, new List<string>()).Contains("no Discord line") || !DiscordFriends.Hint(false, null).Contains("isn't in your notes") || !DiscordFriends.Hint(true, new List<string> { "rudce" }).Contains("Discord: rudce")) throw new Exception("The reason a friend was not found was not explained from his note.");

                string keptRoot = Vault.Root;
                string sentVault = Path.Combine(folder, "vault");
                Directory.CreateDirectory(Path.Combine(sentVault, "Capri"));
                Vault.Root = sentVault;
                string logged;
                string sentLog;
                try
                {
                    logged = DiscordFriends.Message("Zed", "logged once", new List<string> { "rudce" });
                    sentLog = File.ReadAllText(Path.Combine(sentVault, "Capri", "sent-messages.md"));
                }
                finally { Vault.Root = keptRoot; }
                if (!logged.StartsWith("Sent to", StringComparison.Ordinal) || !sentLog.Contains(" to Rudce: logged once")) throw new Exception("A message to a friend was not recorded in the vault's sent-messages note: " + sentLog);

                int before = fake.Friends.Count;
                string which = DiscordFriends.Message("Sam", "hi", null);
                string nobody = DiscordFriends.Message("Zed", "hi", null);
                string himself = DiscordFriends.Message("kasper", "hi", null);
                string bot = DiscordFriends.Message("helperbot", "hi", null);
                string empty = DiscordFriends.Message("Rudce", "   ", null);
                string noName = DiscordFriends.Message("  ", "hi", null);
                if (!which.Contains("Which Sam") || !which.Contains("samuel_k") || !which.Contains("sam.b") || !nobody.Contains("couldn't find") || !himself.Contains("couldn't find") || !bot.Contains("couldn't find") || !empty.Contains("What should I say") || !noName.Contains("Who") || fake.Friends.Count != before) throw new Exception("An unclear, missing, own, bot or empty message was sent or not explained: " + which + " | " + nobody + " | " + himself + " | " + bot + " | " + empty + " | " + noName);

                fake.ClosedFriend = "505";
                string closed = DiscordFriends.Message("Closed", "hi", null);
                fake.ClosedFriend = "";
                if (closed.StartsWith("Sent", StringComparison.Ordinal) || !closed.Contains("doesn't take") || fake.Friends.Count != before) throw new Exception("A refused DM was reported as sent: " + closed);

                string cut = DiscordFriends.Message("Rowan", new string('x', 3000), null);
                if (!cut.StartsWith("Sent to", StringComparison.Ordinal) || fake.Friends[fake.Friends.Count - 1].Length > DiscordFriends.MostChars + 200) throw new Exception("A very long message was not cut.");

                List<string> noted = DiscordFriends.FromNote("- Real name: Rudiminas\n- Discord: rudce.x, @Domas#0\n* league: Domas#EUW");
                if (noted.Count != 2 || noted[0] != "rudce.x" || noted[1] != "domas") throw new Exception("The Discord line of a People note was read wrongly: " + string.Join("|", noted.ToArray()));

                DiscordFriends.Forget();
                int baseline = fake.Friends.Count;
                string last = null;
                for (int i = 0; i <= DiscordFriends.MostPerHour; i++) last = DiscordFriends.Message("Rudce", "n" + i, null);
                if (!last.Contains("holding") || fake.Friends.Count != baseline + DiscordFriends.MostPerHour) throw new Exception("The hourly limit did not hold: " + last + " / " + fake.Friends.Count);

                DiscordFriends.Forget();
                int toTool = fake.Friends.Count;
                ToolArguments call = new ToolArguments { Action = "message", Name = "Rudce", Text = "via the tool" };
                if (!Hands.Valid("discord", call) || Hands.Valid("discord", new ToolArguments { Action = "message", Name = "Rudce" }) || Hands.Valid("discord", new ToolArguments { Action = "message", Text = "hi" }) || Hands.Valid("discord", new ToolArguments { Action = "delete", Name = "Rudce", Text = "hi" }) || Hands.Valid("discord", null)) throw new Exception("The brain's own check of the discord tool was wrong.");
                string viaTool = Hands.Do("discord", call);
                if (!viaTool.StartsWith("Sent to Rudce", StringComparison.Ordinal) || fake.Friends.Count != toTool + 1 || !Hands.ClaudeTools.Contains("\"name\":\"discord\"")) throw new Exception("The discord tool did not send or is not offered: " + viaTool);

                settings.DiscordPaired = false;
                if (!DiscordFriends.Message("Rudce", "hi", null).Contains("isn't set up")) throw new Exception("An unpaired Discord still sent.");
                checks.Add("PASS Discord friends: a message goes from her bot account to a friend found through the shared server (display name, nickname, global name or the Discord line of their People note), never to his own DM, bots or himself, with no mentions and long texts cut; two matches ask which, nobody matches says so, a closed DM is reported as not sent, an hourly limit holds, an unpaired bot says it isn't set up, and the tool passes the brain's own check and is offered to the brain");
            }
            finally
            {
                Brain.Asking = null;
                fake.Close();
                Discord.Base = keptBase;
                Discord.Reset();
                DiscordFriends.Forget();
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }
    }
}
