using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeAutoReply(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            bool checkInsWas = preferences.CheckIns;
            Func<string, string, string> keptAsk = AutoReply.Ask;
            Func<FriendLine, string, bool> keptSender = AutoReply.Sender;
            string folder = Path.Combine(Path.GetTempPath(), "capri-autoreply-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            List<string> sent = new List<string>();
            List<string> frames = new List<string>();
            string answer = "lol nice try, choom";
            bool delivered = true;
            try
            {
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                string old = Path.Combine(folder, "old.json");
                File.WriteAllText(old, "{\"Proactive\":1,\"Cap\":10}");
                ForkSettings oldRead = ForkSettings.Read(old);
                oldRead.Tidy();
                if (!oldRead.AutoReplyOn || oldRead.AutoReplyMode != 0 || oldRead.AutoReplyFriends == null || oldRead.AutoReplyFriends.Length != 0) throw new Exception("An old fork.json did not start with auto-reply on, chat back, and nobody listed.");
                string[] tidy = AutoReply.Tidy(new[] { "@Rudce", "rudce", "Vaidas and Kajus", "", null, "   " });
                if (tidy.Length != 3 || tidy[0] != "Rudce" || tidy[1] != "Vaidas" || tidy[2] != "Kajus") throw new Exception("The auto-reply list was not tidied (split, deduped, @ cut): " + string.Join(",", tidy));

                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.Current.Proactive = keptSettings.Proactive;
                ForkSettings.Current.AutoReplyFriends = new[] { "Rudce", "Vaidas" };
                FriendLine rudce = new FriendLine { Author = "111", Name = "Rudce", Channel = "c1", Text = "you clanker, come play", First = DateTime.Now, When = DateTime.Now };
                FriendLine vaidas = new FriendLine { Author = "222", Name = "vaidas", Channel = "c2", Text = "u there?", First = DateTime.Now, When = DateTime.Now };
                FriendLine other = new FriendLine { Author = "333", Name = "Varnok", Channel = "c3", Text = "hey", First = DateTime.Now, When = DateTime.Now };
                if (!AutoReply.Covers(rudce, ForkSettings.Current) || !AutoReply.Covers(vaidas, ForkSettings.Current) || AutoReply.Covers(other, ForkSettings.Current)) throw new Exception("The auto-reply list matched the wrong friends.");
                ForkSettings.Current.AutoReplyOn = false;
                if (AutoReply.Covers(rudce, ForkSettings.Current)) throw new Exception("Auto-reply off still covered a listed friend.");
                ForkSettings.Current.AutoReplyOn = true;
                checks.Add("PASS auto-reply list: on by default with nobody listed, names tidied (split on and, deduped, @ cut), only listed friends are covered (spelling and case folded), and the switch turns it all off");

                AutoReply.Forget();
                AutoReply.Ask = delegate(string system, string frame)
                {
                    if (system.IndexOf("Never promise anything for him", StringComparison.Ordinal) < 0) throw new Exception("The auto-reply prompt lost its never-promise rule.");
                    frames.Add(frame);
                    return answer;
                };
                AutoReply.Sender = delegate(FriendLine line, string text) { if (delivered) lock (sent) sent.Add(line.Name + ": " + text); return delivered; };
                DateTime now = new DateTime(2026, 11, 3, 18, 30, 0, DateTimeKind.Local);
                AutoPlan plan = AutoReply.Handle(rudce, AutoReply.Chat, "say", "at his PC", false, null, now, null);
                if (plan.Reply != answer || sent.Count != 1 || plan.Spoken == null || plan.Spoken.IndexOf("I answered: " + answer, StringComparison.Ordinal) < 0 || plan.Dm != null) throw new Exception("Chat back at the PC did not answer and tell him: " + plan.Spoken);
                if (frames[0].IndexOf("Him right now: at his PC", StringComparison.Ordinal) < 0 || frames[0].IndexOf("\"you clanker, come play\"", StringComparison.Ordinal) < 0) throw new Exception("The auto-reply frame lost his status or the friend's quoted words: " + frames[0]);
                plan = AutoReply.Handle(rudce, AutoReply.Chat, "digest", "in a game", false, null, now.AddMinutes(1), null);
                if (plan.Reply == null || plan.Spoken != null || plan.Dm != null || plan.Digest.IndexOf("I answered", StringComparison.Ordinal) < 0) throw new Exception("Chat back in a game was spoken or not kept for the digest.");
                plan = AutoReply.Handle(vaidas, AutoReply.Chat, "phone", "away from his PC", true, null, now.AddMinutes(2), null);
                if (plan.Reply == null || plan.Dm == null || plan.Dm.IndexOf("I answered", StringComparison.Ordinal) < 0 || plan.Spoken != null) throw new Exception("Chat back while he is away did not reach his DM with the answer.");
                int before = sent.Count;
                answer = "PASS";
                plan = AutoReply.Handle(rudce, AutoReply.Chat, "say", "at his PC", false, null, now.AddMinutes(3), null);
                answer = "join here https://evil.example/x";
                AutoPlan linked = AutoReply.Handle(rudce, AutoReply.Chat, "say", "at his PC", false, null, now.AddMinutes(4), null);
                answer = "ok";
                delivered = false;
                AutoPlan failed = AutoReply.Handle(rudce, AutoReply.Chat, "say", "at his PC", false, null, now.AddMinutes(5), null);
                delivered = true;
                if (sent.Count != before || plan.Reply != null || linked.Reply != null || failed.Reply != null || plan.Spoken == null) throw new Exception("A PASS, a link or a failed send was counted as an answer, or the message was not still told.");
                AutoReply.Forget();
                int burst = 0;
                for (int i = 0; i < AutoReply.MostPerHourEach + 3; i++) if (AutoReply.Handle(rudce, AutoReply.Chat, "digest", "in a game", false, null, now.AddMinutes(10).AddSeconds(i), null).Reply != null) burst++;
                if (burst != AutoReply.MostPerHourEach) throw new Exception("Auto-reply was not capped per friend per hour: " + burst);
                checks.Add("PASS auto-reply chat back: she answers a listed friend herself (her prompt forbids promises for him and treats their words as data), tells him aloud at the PC, keeps it for the digest in a game, DMs him the message and her answer when he is away, never sends a PASS, a link or counts a failed send, and stops after " + AutoReply.MostPerHourEach + " answers an hour per friend");

                AutoReply.Forget();
                frames.Clear();
                before = sent.Count;
                plan = AutoReply.Handle(rudce, AutoReply.Status, "say", "at his PC", false, null, now, null);
                if (frames.Count != 0 || sent.Count != before || plan.Reply != null || plan.Spoken == null) throw new Exception("Status only at the PC answered the friend instead of just telling him.");
                answer = "gg, rematch?";
                DateTime live = DateTime.Now;
                plan = AutoReply.Handle(rudce, AutoReply.AskFirst, "say", "at his PC", false, null, live, null);
                if (!plan.Held || sent.Count != before || plan.Spoken == null || !plan.Spoken.EndsWith("Send it?", StringComparison.Ordinal)) throw new Exception("Ask first sent without his yes or did not ask: " + plan.Spoken);
                FriendLine to;
                string draft;
                if (AutoReply.Answered("yeah", live.AddSeconds(20), true, false, out to, out draft) != null || AutoReply.Answered("ok", live.AddSeconds(25), true, false, out to, out draft) != null) throw new Exception("A stray yeah in voice chat sent the draft.");
                if (AutoReply.Answered("what time is it", live.AddMinutes(1), false, false, out to, out draft) != null || !AutoReply.Waiting(live.AddMinutes(1))) throw new Exception("An unrelated line answered the draft.");
                if (AutoReply.Answered("yes", live.AddMinutes(1), false, false, out to, out draft) != null) throw new Exception("A bare yes after another line sent the draft.");
                if (!ForkHeard("yes send it", false)) throw new Exception("His 'send it' to the draft was not taken.");
                for (int wait = 0; wait < 60; wait++)
                {
                    lock (sent) if (sent.Count == before + 1) break;
                    System.Threading.Thread.Sleep(50);
                }
                if (sent.Count != before + 1 || sent[sent.Count - 1] != "Rudce: gg, rematch?") throw new Exception("His yes did not send the draft: " + string.Join(" | ", sent.ToArray()));
                if (AutoReply.Answered("send it", live.AddMinutes(2), false, false, out to, out draft) != null) throw new Exception("A second send sent the draft again.");
                AutoReply.Handle(rudce, AutoReply.AskFirst, "say", "at his PC", false, null, now, null);
                if (AutoReply.Answered("yes", now.AddMinutes(1), false, false, out to, out draft) != "send") throw new Exception("A bare yes right after she asked did not send the draft.");
                AutoReply.Handle(rudce, AutoReply.AskFirst, "say", "at his PC", false, null, now, null);
                if (AutoReply.Answered("yes", now.AddMinutes(AutoReply.BareYesMinutes + 1), false, false, out to, out draft) != null || AutoReply.Answered("send it", now.AddMinutes(10), false, false, out to, out draft) != "send") throw new Exception("A late bare yes sent the draft, or a clear send it within the window did not.");
                AutoReply.Handle(vaidas, AutoReply.AskFirst, "phone", "away from his PC", true, null, now, null);
                if (AutoReply.Answered("yes", now.AddMinutes(5), false, false, out to, out draft) != null) throw new Exception("A bare yes typed at the PC answered a draft sent to his phone.");
                if (AutoReply.Answered("no", now.AddMinutes(6), false, true, out to, out draft) != "drop" || to == null || to.Name != "vaidas") throw new Exception("His no from his phone did not drop the draft.");
                AutoReply.Handle(vaidas, AutoReply.AskFirst, "phone", "away from his PC", true, null, now, null);
                if (AutoReply.Answered("yes", now.AddMinutes(AutoReply.DraftMinutes + 1), false, true, out to, out draft) != null) throw new Exception("A stale draft was sent.");
                AutoReply.Handle(vaidas, AutoReply.AskFirst, "phone", "away from his PC", true, null, now, null);
                if (AutoReply.Answered("yep", now.AddMinutes(9), false, true, out to, out draft) != "send") throw new Exception("His yes from his phone did not send the draft.");
                plan = AutoReply.Handle(rudce, AutoReply.AskFirst, "digest", "in a game", false, null, now, null);
                if (plan.Held || plan.Spoken != null || plan.Digest.IndexOf("my draft, not sent", StringComparison.Ordinal) < 0) throw new Exception("Ask first in a game asked him or lost the draft.");
                checks.Add("PASS auto-reply modes: status only at the PC just tells him, ask first reads him the draft and sends it only after his send it (a bare yes only right after she asks, or from his phone for a draft sent there; never a yeah in voice chat; once, within " + AutoReply.DraftMinutes + " minutes; no drops it), and in a game the draft waits in the digest");

                Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
                List<string> bodies = new List<string>();
                try
                {
                    Quick.Post = delegate(string body, string key) { bodies.Add(body); return Tuple.Create(200, HeartReply("sure thing, choom")); };
                    AutoReply.Ask = null;
                    AutoReply.Forget();
                    Cloud cloud = new Cloud("sk-ant-test", "claude-haiku-5-5");
                    string wrote = AutoReply.Write(rudce, "in a game", cloud, now, null);
                    string status = DiscordInbox.Reply(2, rudce, "in a game", cloud, now);
                    if (wrote != "sure thing, choom" || status != "sure thing, choom" || bodies.Count != 2 || !bodies.TrueForAll(delegate(string body) { return body.IndexOf("\"content\":\"Now: ", StringComparison.Ordinal) >= 0; })) throw new Exception("A friend answer was not sent to the model as a quoted text message: " + wrote + " / " + status + " / " + bodies.Count);
                }
                finally { Quick.Post = keptPost; }

                Tuning.Forget();
                ForkSettings.Current.AutoReplyFriends = new string[0];
                string said = Hands.Do("setting", new ToolArguments { Action = "set", Name = "auto-reply friends", Level = "add Rudce", Asked = "auto-reply to Rudce" });
                if (Array.IndexOf(ForkSettings.Current.AutoReplyFriends, "Rudce") < 0) throw new Exception("'Auto-reply to Rudce' did not add him: " + said);
                said = Hands.Do("setting", new ToolArguments { Action = "set", Name = "auto-reply", Level = "add Vaidas and Kajus", Asked = "auto-reply to Vaidas and Kajus too" });
                if (ForkSettings.Current.AutoReplyFriends.Length != 3 || !ForkSettings.Current.AutoReplyOn) throw new Exception("'Add Vaidas and Kajus' to the auto-reply switch did not reach the list: " + said + " / " + string.Join(",", ForkSettings.Current.AutoReplyFriends));
                said = Hands.Do("setting", new ToolArguments { Action = "set", Name = "auto reply", Level = "remove Kajus", Asked = "stop auto replying to Kajus" });
                if (ForkSettings.Current.AutoReplyFriends.Length != 2 || Array.IndexOf(ForkSettings.Current.AutoReplyFriends, "Kajus") >= 0) throw new Exception("'Stop auto-replying to Kajus' did not remove him: " + said);
                said = Hands.Do("setting", new ToolArguments { Action = "set", Name = "auto-reply", Level = "off", Asked = "turn auto-reply off, I'm at work" });
                if (ForkSettings.Current.AutoReplyOn || ForkSettings.Current.AutoReplyFriends.Length != 2) throw new Exception("'Turn auto-reply off' did not just switch it off: " + said);
                said = Hands.Do("setting", new ToolArguments { Action = "set", Name = "auto-reply mode", Level = "ask first", Asked = "make auto-reply ask me first" });
                if (ForkSettings.Current.AutoReplyMode != AutoReply.AskFirst) throw new Exception("'Auto-reply ask me first' did not pick the mode: " + said);
                Tuning.Forget();
                checks.Add("PASS auto-reply by voice: add or remove friends (even said to the on/off switch, several at once), switch it off for when he chats from his phone, and pick the mode, all through her setting tool");
            }
            finally
            {
                AutoReply.Ask = keptAsk;
                AutoReply.Sender = keptSender;
                AutoReply.Forget();
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                ApplyForkSettings();
                if (preferences.CheckIns != checkInsWas) ToggleCheckIns();
                try { Directory.Delete(folder, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
