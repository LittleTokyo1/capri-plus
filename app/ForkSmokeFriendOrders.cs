using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokePresenceHold(List<string> checks)
        {
            try
            {
                PresenceHold.Forget();
                DateTime t = new DateTime(2026, 11, 2, 20, 0, 0);
                List<PresenceFriend> on = new List<PresenceFriend> { new PresenceFriend { Id = "506", Name = "Oskar" } };
                PresenceHold.Hold(new PresenceHeads { Id = "506", Name = "Oskar", Text = "Oskar just came online on Discord.", When = t });
                List<PresenceHeads> after = PresenceHold.After(on, t.AddMinutes(35));
                if (after.Count != 1 || after[0].Text != "Oskar came online during your game and is still on Discord." || PresenceHold.Any || PresenceHold.After(on, t.AddMinutes(36)).Count != 0) throw new Exception("A friend who came online during a game was not told once after it.");
                PresenceHold.Hold(new PresenceHeads { Id = "506", Name = "Oskar", When = t });
                if (PresenceHold.After(new List<PresenceFriend>(), t.AddMinutes(35)).Count != 0) throw new Exception("A friend who went offline during the game was still told.");
                PresenceHold.Hold(new PresenceHeads { Id = "506", Name = "Oskar", When = t });
                if (PresenceHold.After(on, t.AddHours(PresenceHold.MostHours + 1)).Count != 0) throw new Exception("A heads-up held for hours was still told.");
                checks.Add("PASS friend online in a game: the heads-up is held through the game and told once after it, only if that friend is still on (never after " + PresenceHold.MostHours + " hours)");
            }
            finally { PresenceHold.Forget(); }
        }

        private static void SmokeFriendOrders(List<string> checks)
        {
            SmokePresenceHold(checks);
            string keptRoot = Vault.Root;
            Func<DiscordMember, string, string> keptSend = DiscordFriends.SendTo;
            string vault = Path.Combine(Path.GetTempPath(), "capri-friend-orders-smoke");
            try
            {
                if (Directory.Exists(vault)) Directory.Delete(vault, true);
                Directory.CreateDirectory(Path.Combine(vault, "People"));
                File.WriteAllText(Path.Combine(vault, "People", "Rudiminas.md"), "- Also called: Rudce, Domitan\n- Discord: rudce.x\n");
                File.WriteAllText(Path.Combine(vault, "People", "Pieter.md"), "- Discord: pieter\n");
                Vault.Root = vault;
                FriendOrders.Forget();
                List<DiscordMember> members = new List<DiscordMember> { new DiscordMember { Id = "501", User = "rudce.x", Global = "Rudiminas" }, new DiscordMember { Id = "502", User = "pieter", Global = "Pieter" } };
                List<PresenceFriend> mapped = DiscordPresence.Map(members, vault);
                List<PresenceFriend> pieterOnly = mapped.FindAll(delegate(PresenceFriend f) { return f.Id == "502"; });
                string asked = "hey when rudce is online message him to hop on cyberpunk";
                DateTime t = new DateTime(2026, 11, 2, 16, 0, 0);
                string sentTo = null;
                DiscordFriends.SendTo = delegate(DiscordMember one, string text) { sentTo = one.Id; return "Sent to " + one.Label + ": \"" + text + "\""; };

                string saved = FriendOrders.Tool("Rudce", "Yo Rudce, Kasper says hop on Cyberpunk!", asked, mapped, new List<PresenceFriend>(), t);
                if (mapped.Count != 2 || !saved.StartsWith("Saved: when Rudce comes online", StringComparison.Ordinal) || !FriendOrders.Pending(t.AddMinutes(1)) || sentTo != null) throw new Exception("An order for when a friend comes online was not saved: " + saved);
                if (FriendOrders.Due(pieterOnly, t.AddMinutes(5)).Count != 0) throw new Exception("An order went off for another friend.");
                List<FriendOrderDue> due = FriendOrders.Due(mapped, t.AddMinutes(10));
                if (due.Count != 1 || due[0].Friend.Id != "501" || due[0].Order.Text != "Yo Rudce, Kasper says hop on Cyberpunk!" || FriendOrders.Due(mapped, t.AddMinutes(11)).Count != 0 || FriendOrders.Pending(t.AddMinutes(11))) throw new Exception("An order did not go off once when the friend came online.");
                string result = DiscordFriends.Ordered(due[0].Friend.Id, due[0].Order.Who, due[0].Order.Text, due[0].Order.Asked);
                if (sentTo != "501" || !result.StartsWith("Sent to", StringComparison.Ordinal) || FriendOrders.Told(due[0], result) != "Rudce came online, so I messaged them: \"Yo Rudce, Kasper says hop on Cyberpunk!\"" || !FriendOrders.Told(due[0], "Rudce doesn't take DMs from me.").Contains("couldn't")) throw new Exception("A due order was not sent to the friend's own id or told right: " + result);

                FriendOrders.Tool("Rudce", "hop on?", asked, mapped, null, t);
                if (FriendOrders.Due(mapped, t.AddHours(FriendOrders.Hours).AddMinutes(1)).Count != 0) throw new Exception("An order outlived a day.");
                FriendOrders.Tool("Domitan", "cyberpunk?", asked, mapped, null, t);
                FriendOrders.Tool("Rudce", "league instead?", asked, mapped, null, t);
                due = FriendOrders.Due(mapped, t.AddMinutes(1));
                if (due.Count != 1 || due[0].Order.Text != "league instead?") throw new Exception("A newer order for the same friend did not replace the old one.");

                sentTo = null;
                string now = FriendOrders.Tool("Pieter", "get on league", "message Pieter to get on league when he's on", mapped, pieterOnly, t);
                if (sentTo != "502" || !now.Contains("online right now") || FriendOrders.Pending(t)) throw new Exception("A friend already online did not get the message at once: " + now);
                sentTo = null;
                if (!FriendOrders.Tool("Bob", "hi", asked, mapped, null, t).Contains("isn't in your People notes") || FriendOrders.Tool("Rudce", "hi", "what's the weather", mapped, null, t).StartsWith("Saved", StringComparison.Ordinal) || FriendOrders.Tool("Rudce", "Tell him to hop on", asked, mapped, null, t).StartsWith("Saved", StringComparison.Ordinal) || FriendOrders.Pending(t) || sentTo != null)
                    throw new Exception("An order for a stranger, one he never asked for, or his own instruction as the text was saved or sent.");
                if (!Understand.Prompt().Contains("when_online") || !Hands.ClaudeTools.Contains("Never for a friend coming online on Discord") || !Hands.ClaudeTools.Contains("For lobby or queue, only if they said: the monitor") || !LiveFacts.Block("Which jungle pet do you pick as that jungle?").Contains(LiveFacts.LeagueTalk) || !LiveFacts.Block("Uh where does Zack start clearing his jungle from? Does he start at red or blue, usually?").Contains(LiveFacts.LeagueTalk)) throw new Exception("A friend coming online was not steered to when_online, or a jungle pet question was not League knowledge.");
                if (DiscordFriends.Do(new ToolArguments { Action = "when_online", Name = "Rudce", Text = "hi" }).StartsWith("Use message", StringComparison.Ordinal)) throw new Exception("The discord tool did not take when_online.");
                checks.Add("PASS friend orders: the discord tool's when_online (\"when Rudce is online message him to hop on Cyberpunk\", any wording her brain understands, her own words as the text) waits up to a day and her bot DMs that friend once when they come online (at once if they already are), then she tells him; another friend, a stranger, a newer order, an ask without a message word or his instruction as the text never sends it");
            }
            finally
            {
                DiscordFriends.SendTo = keptSend;
                FriendOrders.Forget();
                Vault.Root = keptRoot;
                try { if (Directory.Exists(vault)) Directory.Delete(vault, true); }
                catch (IOException) { }
            }
        }
    }
}
