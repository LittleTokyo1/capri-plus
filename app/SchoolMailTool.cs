using System;

namespace CapriDesktop
{
    internal static class SchoolMailTool
    {
        internal const string Tool = "school_mail";
        internal const string Spec = "His school (university) email in Outlook on the web. check reads what is new, or searches the last week for a topic (search, like Dutch level or grades); write drafts a new school email; reply drafts a reply to a school mail he means. For write and reply, text is who it goes to and what it should say, in his words (\"the right coordinator\", \"the lecturer who answered\" is fine, the job finds the thread). The draft is read back to him and is only sent when he says send it. send is for drafts already written (send both, send the Dutch one, send it): it reads each one back from his drafts page and each goes out only on his own send it; text is which ones, in his words. Use it whenever he asks what school, lecturer or coordinator mails said, to check or search his school mail, or to draft, write or reply to a school email, in any words or in the middle of a longer message. Never for Discord, texts or his other mail. Never say a draft is written or a mail is checked unless this tool started it.";
        internal const string Schema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"check reads or searches, write drafts a new email, reply drafts a reply, send reads waiting drafts back for his send it\",\"enum\":[\"check\",\"write\",\"reply\",\"send\"]},\"text\":{\"type\":\"string\",\"description\":\"For write and reply: who it goes to and what it says, in his words. For send: which drafts, in his words\"},\"search\":{\"type\":\"string\",\"description\":\"For check: the topic to search for, only if he named one\"}},\"required\":[\"action\"]}";
        internal const string NotSet = "Your school mail isn't set up on this PC yet, so I can't reach it.";
        internal const string Unknown = "I can check, write, reply to or send your school mail; say which.";

        internal static Func<string, string, string, string> Start;
        private static readonly System.Text.RegularExpressions.Regex Replying = new System.Text.RegularExpressions.Regex(@"\b(?:reply|replies|answer|respond)\b(?!\s+from\b)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        private static readonly System.Text.RegularExpressions.Regex Writing = new System.Text.RegularExpressions.Regex(@"\b(?:draft|write|compose|send)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        private static readonly System.Text.RegularExpressions.Regex About = new System.Text.RegularExpressions.Regex(@"\b(?:about|regarding)\s+(?:the\s+|my\s+)?(?<topic>[\p{L}\d][\p{L}\d'& -]{1,40}?)(?=\s*(?:[,.;!?]|\s(?:and|especially|from|so|that|which|please|or)\b|$))", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        internal static ToolArguments FromTask(string task)
        {
            string said = (task ?? "").Trim();
            if (SchoolSendQueue.Sends(said)) return new ToolArguments { Action = "send", Text = said, Asked = said };
            bool writing = Writing.IsMatch(said) && !Checking.IsMatch(said);
            string action = writing && Replying.IsMatch(said) ? "reply" : writing ? "write" : "check";
            return new ToolArguments { Action = action, Text = said, Asked = said, Search = action == "check" ? Topic(said) : null };
        }

        private static readonly System.Text.RegularExpressions.Regex Checking = new System.Text.RegularExpressions.Regex(@"^\s*(?:please\s+)?(?:check|search|look\s+(?:through|for|at|up)|find|read|go\s+through|see\s+(?:if|what))\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        private static readonly System.Text.RegularExpressions.Regex Quoted = new System.Text.RegularExpressions.Regex("\"(?<topic>[^\"]{2,60})\"");
        private static readonly System.Text.RegularExpressions.Regex MailFor = new System.Text.RegularExpressions.Regex(@"\b(?:mail|e-?mail|inbox|outlook|messages?)\s+(?:for|on)\s+(?:the\s+|my\s+|an?\s+|any\s+)?(?<topic>[^,.;!?""]{2,60}?)(?=\s+(?:that|which|who|so)\b|\s+and\s+(?:tell|let|show|read|open|see|find|check|draft|reply|give|say|work|look|send|write)\b|\s+to\s+(?:me|see)\b|\s+e-?mails?\b|\s*[,.;!?]|\s*$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static string Topic(string said)
        {
            foreach (System.Text.RegularExpressions.Regex one in new[] { About, Quoted, MailFor })
            {
                System.Text.RegularExpressions.Match found = one.Match(said);
                if (found.Success) return found.Groups["topic"].Value.Trim();
            }
            return null;
        }

        internal static string Do(string tool, ToolArguments args)
        {
            if (tool != Tool) return null;
            Func<string, string, string, string> start = Start;
            if (start == null) return NotSet;
            string action = args == null || string.IsNullOrEmpty(args.Action) ? "check" : args.Action.Trim().ToLowerInvariant();
            if (action != "check" && action != "write" && action != "reply" && action != "send") return Unknown;
            if (action == "write" && args != null && SchoolSendQueue.Sends(args.Asked)) action = "send";
            if (action == "check") return start("check", "", args == null || string.IsNullOrWhiteSpace(args.Search) ? "" : args.Search.Trim());
            string own = args == null ? "" : (args.Asked ?? "").Trim();
            string text = args == null ? "" : (args.Text ?? "").Trim();
            string asked = own.Length == 0 ? text : text.Length == 0 || own.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ? own : own + " (" + text + ")";
            return start(action, asked, "");
        }
    }

    internal sealed partial class Companion
    {
        private string SchoolToolStart(string action, string asked, string topic)
        {
            if (Study.Safe() == null) return SchoolMailTool.NotSet;
            DateTime now = DateTime.Now;
            if (action == "check")
            {
                string quick = SchoolGmail.Answer(topic);
                if (quick != null) return quick;
                Post(delegate
                {
                    schoolTopic = topic.Length == 0 ? null : topic;
                    schoolCheckWhy = "he asked";
                    schoolCheckNow = true;
                    schoolTickAt = DateTime.MinValue;
                    ActivityLog.Write("study", "school mail check by tool", topic);
                });
                return Lockout.Active ? "I'll check your school mail after your game; it needs your Chrome." : topic.Length == 0 ? "Checking your school mail now. I'll tell you what's new in a minute or two." : "Looking through your school mail from the last week for " + topic + ". I'll tell you what I find in a minute or two.";
            }
            if (action == "send") return SchoolToolSend(asked);
            string page = StudyDrafts.PageLink();
            if (page == null) return "I can't find your drafts page link in the study routine, so I can't write it there.";
            string his = (Brain.Asking ?? "").Trim();
            if (his.Length > 0 && (asked ?? "").IndexOf(his, StringComparison.OrdinalIgnoreCase) < 0) asked = (asked ?? "") + " (his own words: \"" + his + "\")";
            Post(delegate
            {
                SchoolMailItem answering = action == "reply" ? schoolLastMail : null;
                SchoolQueueDrop();
                ActivityLog.Write("study", "school mail " + action + " by tool", Pursuit.Shorter(asked, 120));
                SchoolJob("write school mail", SchoolMail.ComposePrompt(page, asked, answering, answering == null ? SchoolTalk(now) : null, now), SchoolMail.DraftTools, SchoolMail.ComposeMinutes, SchoolDrafted, true);
            });
            return Lockout.Active ? "I'll write it after your game; it needs your Chrome." : "Writing it now. I'll read it to you in a minute or two, and it only goes out when you say send it.";
        }
    }
}
