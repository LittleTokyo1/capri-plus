using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    // Capri+ smoke checks for conversations, always looking, and the message rules.
    internal sealed partial class Companion
    {
        private static ChatLine Line(ulong id, string author, string text) { return new ChatLine { Id = id, Author = author, Text = text }; }

        private static string SmokeReply(string text)
        {
            return "{\"content\":[{\"type\":\"text\",\"text\":" + Voice.Quote(text) + "}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":900,\"output_tokens\":20}}";
        }

        private void SmokeChats(List<string> checks)
        {
            ChatLine mine = ChatReader.Parse("chat-messages-911264322938875954-1565068255725490280", "sampleuser_7:22 PMWednesday, October 28, 2026 7:22 PMAI is controlling this computer:heart:Click to react:laughing:Click to react:fire:Click to reactAdd Reaction", null);
            ChatLine more = ChatReader.Parse("chat-messages-911264322938875954-1565068884443402342", "7:24 PMWednesday, October 28, 2026 7:24 PMwe yapping abuot god damn clankers", "Rudce");
            ChatLine digits = ChatReader.Parse("chat-messages-1-5", "rudce137:22 PMWednesday, October 28, 2026 7:22 PMhey (edited)", null);
            ChatLine hours = ChatReader.Parse("chat-messages-1-6", "Rudce19:22Wednesday, October 28, 2026 19:22hello", null);
            if (mine == null || mine.Id != 1565068255725490280 || mine.Author != "sampleuser_" || mine.Text != "AI is controlling this computer") throw new Exception("A Discord message was read wrong.");
            if (more == null || more.Author != "Rudce" || more.Text != "we yapping abuot god damn clankers") throw new Exception("A follow-on Discord message lost its author.");
            if (digits == null || digits.Author != "rudce13" || digits.Text != "hey" || hours == null || hours.Author != "Rudce" || hours.Text != "hello") throw new Exception("Names with digits or 24-hour times were read wrong.");
            if (!ChatApp("Discord") || !ChatApp("WhatsApp") || ChatApp("WindowsTerminal") || ChatApp("powershell") || ChatApp("claude") || ChatApp("Code") || ChatApp("explorer") || ChatApp(null) || ChatApp("steam") || ChatApp("steamwebhelper") || ChatApp("Spotify") || !ChatApp("Telegram") || ChatApp("OUTLOOK") || ChatApp("chrome") || ChatApp("chrome", "Drafts - Student - Outlook - Google Chrome") || ChatApp("chrome", "Google Search - Google Chrome") || !ChatApp("chrome", "(2) WhatsApp - Google Chrome") || !ChatApp("msedge", "Messenger | Facebook - Microsoft Edge")) throw new Exception("A terminal, a code window or Claude Code could be taken as a chat to keep going, or a chat app could not.");
            Func<bool> keptCall = InFriendsCall;
            bool keptListen = preferences.ListenCapri;
            try
            {
                preferences.ListenCapri = false;
                InFriendsCall = delegate { return true; };
                if (VoiceForHer("Tiger Claw Specialty.") || VoiceForHer("Cut a large one.") || !VoiceForHer("Capri, what time is it?") || !VoiceForHer("kapri clip that"))
                    throw new Exception("In a Discord call with friends, his talk with them was taken as said to her, or a line with her name was dropped (2026-11-07 20:03 'Cut a large one' saved a clip).");
                InFriendsCall = delegate { return false; };
                if (!VoiceForHer("Tiger Claw Specialty.")) throw new Exception("Out of a call, free voice chat stopped answering lines without her name.");
            }
            finally
            {
                InFriendsCall = keptCall;
                preferences.ListenCapri = keptListen;
            }
            if (GameSoundLines.On) throw new Exception("Her game-sound lines are back (his pick 2026-11-08: 'the speakers just blew up' in Cyberpunk with nothing happening; he had the feature removed).");
            if (ChatReader.Parse("", "Listening to Spotify", null) != null || ChatReader.Parse("chat-messages-x-y", "nothing", null) != null) throw new Exception("Something that isn't a message was read as one.");
            if (ChatReader.Conversation("@Rudce - Discord") != "Rudce" || ChatReader.Conversation("#general - Discord") != "general" || ChatReader.Conversation("WhatsApp") != "WhatsApp") throw new Exception("Chat names were read wrong.");
            if (ChatReader.InputSize != (IntPtr.Size == 8 ? 40 : 28)) throw new Exception("Typing would send malformed key presses (" + ChatReader.InputSize + " bytes).");
            if (!KeepChatting.IsMatch("keep the conversation going") || !KeepChatting.IsMatch("continue chatting with him") || !KeepChatting.IsMatch("text with Rudce for me") || KeepChatting.IsMatch("keep going") || !StopChatting.IsMatch("stop talking to him")) throw new Exception("Chat requests were heard wrong.");

            ForkSettings keptSettings = ForkSettings.Current;
            Func<string, string, Tuple<int, string>> keptPost = Quick.Post;
            Cloud keptRemote = brain.Remote;
            Func<IntPtr, List<ChatLine>> keptRead = ReadChat;
            Func<IntPtr, byte[]> keptSnap = SnapChat;
            Func<IntPtr, string, bool> keptType = TypeChat;
            Func<IntPtr, string> keptTitle = TitleOf;
            Func<IntPtr, bool> keptAlive = WindowAlive;
            Func<long> keptIdle = IdleFor;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                brain.Remote = new Cloud("sk-ant-test", "claude-haiku-5-5");
                List<ChatLine> lines = new List<ChatLine> { Line(1, "sampleuser_", "hi"), Line(2, "Rudce", "yo who is this") };
                List<string> typed = new List<string>();
                List<string> asked = new List<string>();
                string answer = "lol you wish, choom";
                long idle = 10000;
                string title = "@Rudce - Discord";
                ReadChat = delegate { return new List<ChatLine>(lines); };
                TypeChat = delegate(IntPtr window, string text) { typed.Add(text); return true; };
                TitleOf = delegate { return title; };
                WindowAlive = delegate { return true; };
                IdleFor = delegate { return idle; };
                Quick.Post = delegate(string body, string key) { asked.Add(body); return Tuple.Create(200, SmokeReply(answer)); };
                StartConvo(new IntPtr(4242), "keep chatting with Rudce", true);
                if (convo == null || convo.Name != "Rudce" || convo.LastId != 1) throw new Exception("The chat watch didn't start on Rudce's unanswered message.");
                string first = CheckConvo();
                if (first != "sent" || typed.Count != 1 || typed[0] != "[Capri] lol you wish, choom" || !asked[0].Contains("Rudce: yo who is this") || !asked[0].Contains("keep chatting with Rudce")) throw new Exception("She didn't answer Rudce in character: " + first + " / " + string.Join(" | ", typed.ToArray()));
                lines.Add(Line(3, "sampleuser_", "[Capri] lol you wish, choom"));
                if (CheckConvo() != "quiet" || typed.Count != 1) throw new Exception("She answered her own message.");
                lines.Add(Line(4, "sampleuser_", "brb"));
                if (CheckConvo() != "quiet" || typed.Count != 1) throw new Exception("She answered Kasper's own message.");
                lines.Add(Line(5, "Rudce", "ok bye"));
                answer = "SKIP";
                if (CheckConvo() != "quiet" || typed.Count != 1) throw new Exception("She replied when there was nothing to say.");
                lines.Add(Line(6, "Rudce", "wait come back"));
                answer = "fine, what";
                idle = 100;
                if (CheckConvo() != "held" || typed.Count != 1) throw new Exception("She typed while they were using the PC.");
                idle = 10000;
                if (CheckConvo() != "sent" || typed[1] != "[Capri] fine, what" || !asked[asked.Count - 1].Contains("You (Capri): [Capri] lol you wish, choom") || !asked[asked.Count - 1].Contains("Him: brb")) throw new Exception("A held reply or the transcript went wrong.");
                title = "@Someone Else - Discord";
                if (CheckConvo() != "elsewhere") throw new Exception("She typed into a different DM.");
                ConvoHeard("stop talking to him", true);
                if (convo != null) throw new Exception("Stop didn't end the chat.");

                SnapChat = delegate { return new byte[] { 1, 2, 3 }; };
                title = "WhatsApp";
                answer = "THEIRS: are you a bot?\nREPLY: maybe, maybe not";
                StartConvo(new IntPtr(4343), "keep the conversation going", false);
                if (CheckConvo() != "sent" || typed[typed.Count - 1] != "[Capri] maybe, maybe not" || !asked[asked.Count - 1].Contains("\"data\":\"AQID\"")) throw new Exception("Another chat app wasn't read from its picture.");
                if (CheckConvo() != "quiet") throw new Exception("She answered the same message twice in another app.");
                List<JobMark> marks = JobMarks.Live(DateTime.Now);
                if (!marks.Exists(delegate(JobMark one) { return one.Kind == "chat" && one.Running; })) throw new Exception("A running chat watch has no icon beside her, so he can't tell it's her (2026-11-07 21:20).");
                int sentBefore = typed.Count;
                title = "[Capri] fine - Google Search - Google Chrome";
                answer = "THEIRS: hello?\nREPLY: still here";
                if (CheckConvo() != "elsewhere" || typed.Count != sentBefore) throw new Exception("She kept typing after the window turned into something else (2026-11-07 21:16, her replies went into a Google search).");
                title = "WhatsApp";
                if (!MailJob.IsMatch("Send the Dutch draft and the feedback thank-you draft from my school Outlook in Chrome") || MailJob.IsMatch("message Casey that I'm going to the shop")) throw new Exception("A mail job could start a chat watch, or a chat message could not.");
                convo.Until = DateTime.Now.AddSeconds(-1);
                if (CheckConvo() != "ended" || convo != null) throw new Exception("A quiet chat didn't end.");
                if (JobMarks.Live(DateTime.Now).Exists(delegate(JobMark one) { return one.Kind == "chat" && one.Running; })) throw new Exception("The chat icon stayed after the chat watch ended.");

                Brain eyes = new Brain();
                eyes.Remote = new Cloud("sk-ant-test", "claude-haiku-5-5");
                eyes.Eyes = delegate { return new byte[] { 1, 2, 3 }; };
                List<ChatMessage> messages = new List<ChatMessage> { new ChatMessage("system", "s"), new ChatMessage("user", "what's this?") };
                string parts = eyes.GlimpseForTest(messages);
                if (parts == null || !parts.Contains("\"data\":\"AQID\"") || !parts.Contains("what's this?") || messages[1].Content == "what's this?") throw new Exception("Her screen didn't go with the message.");
                eyes.Remote = new Cloud("nvapi-test", "meta/llama");
                List<ChatMessage> plain = new List<ChatMessage> { new ChatMessage("system", "s"), new ChatMessage("user", "hi") };
                if (eyes.GlimpseForTest(plain) != null || plain[1].Content != "hi") throw new Exception("A brain that can't see got a picture.");
                ForkSettings.Current.AlwaysLook = false;
                if (ForkEyes() != null) throw new Exception("She looked with always-look off.");
                if (!Computer.Jobs("x").Contains("never refuse it") || !Fork.TaskRules.Contains("move my mouse and send it")) throw new Exception("The message rules are missing.");
                if (!Fork.TaskRules.Contains("You have hands: open_app, open_website, use_computer, shortcut and app_window") || !Fork.TaskRules.Contains("Never say you can't click, type or open things") || !Fork.TaskRules.Contains("even mid-game")) throw new Exception("The hands rules are missing.");            }
            finally
            {
                StopConvo("smoke done");
                ForkSettings.Current = keptSettings;
                Quick.Post = keptPost;
                brain.Remote = keptRemote;
                ReadChat = keptRead;
                SnapChat = keptSnap;
                TypeChat = keptType;
                TitleOf = keptTitle;
                WindowAlive = keptAlive;
                IdleFor = keptIdle;
            }
            checks.Add("PASS chats: Discord messages are read from accessibility (names with digits, 24-hour times, follow-ons), she answers only the other person in character with the chat in front of her, never her own or Kasper's lines, skips when there's nothing to say, waits while they're using the PC, stays in the right DM, other apps are read from a picture, only known chat apps count (Steam after a download job never does) and a browser only on a chat site, never Outlook or mail, a mail job never starts a chat watch, she stops typing when the window turns into something else, a running chat watch shows a chat icon beside her, a quiet chat ends, her screen goes with each message, and message jobs write their own words; game-sound lines stay removed (no game audio listening); in a Discord call with friends she only takes voice lines that say her name, and out of a call free voice chat answers as before");
        }
    }
}
