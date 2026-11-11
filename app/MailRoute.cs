using System;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class MailRoute
    {
        internal const string Steer = "Google is connected, so use the google tool for this (calendar, add_event, change_event, find_mail, read_mail, draft_mail or send_mail) instead of opening the browser. Use the browser only for what the tool can't do, like archiving or deleting mail.";
        private static readonly Regex Page = new Regex(@"mail\.google\.com|\bgmail\b|\bgoogle mail\b|calendar\.google\.com|\bgoogle calendar\b", RegexOptions.IgnoreCase);
        private static readonly Regex Subject = new Regex(@"\b(gmail|g-?mail|e-?mails?|inbox|google calendar|my calendar)\b", RegexOptions.IgnoreCase);
        private static readonly Regex ToolVerb = new Regex(@"\b(read|check|find|search|look|summari[sz]e|draft|write|reply|send|show|list|what|when|add|schedule|create|change)\b", RegexOptions.IgnoreCase);
        private static readonly Regex ByHand = new Regex(@"\b(archive|delete|trash|label|unsubscribe|spam|clean|star|mark|filter|organi[sz]e|empty|sort|forward|attach|download|upload|export|import|settings|rules?)\w*", RegexOptions.IgnoreCase);
        private static readonly Regex Elsewhere = new Regex(@"\b(order|checkout|check out|basket|cart|coupon|pay|sign ?up|register|log ?in|form|field|box|contact|delivery|fill|account|booking|discord|whatsapp|telegram|signal|steam)\w*", RegexOptions.IgnoreCase);
        private static readonly Regex Address = new Regex(@"\be-?mail\s+address(es)?\b|[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.IgnoreCase);
        internal const string SchoolSteer = "That is his school mail in Outlook, not Gmail: use the school_mail tool (check with a search topic, write or reply) instead of use_computer or the google tool.";
        internal const string SchoolRule = "\nHis school mail (Outlook: lecturers, coordinators, courses) is the school_mail tool, never Gmail, the google tool or use_computer.";
        private static readonly Regex School = new Regex(@"\b(school|uni|university|student|outlook|lecturers?|teachers?|professors?|coordinators?|course|tutors?)\b", RegexOptions.IgnoreCase);
        private static readonly TimeSpan Again = TimeSpan.FromMinutes(10);
        private static DateTime steeredAt = DateTime.MinValue;

        internal static string Browser(ToolArguments args, string asked, bool connected)
        {
            if (!connected || args == null) return null;
            if (!Page.IsMatch((args.Site ?? "") + " " + (args.Search ?? "") + " " + (args.Name ?? ""))) return null;
            return Companion.WantsPage.IsMatch(asked ?? "") ? null : Steer;
        }

        internal static bool Job(string task, bool connected)
        {
            return connected && !string.IsNullOrWhiteSpace(task) && !School.IsMatch(task) && Subject.IsMatch(Address.Replace(task, "")) && ToolVerb.IsMatch(task) && !ByHand.IsMatch(task) && !Elsewhere.IsMatch(task);
        }

        internal static bool SchoolJob(string task)
        {
            string said = task ?? "";
            return School.IsMatch(said) && (Subject.IsMatch(said) || Regex.IsMatch(said, @"\bmails?\b", RegexOptions.IgnoreCase)) && ToolVerb.IsMatch(said) && !ByHand.IsMatch(said);
        }

        internal static string JobSteer(string task, bool connected, DateTime now)
        {
            if (SchoolJob(task)) return SchoolSteer;
            if (!Job(task, connected) || now - steeredAt < Again) return null;
            steeredAt = now;
            return Steer;
        }

        internal static void Reset()
        {
            steeredAt = DateTime.MinValue;
        }

        internal static string Rule(bool connected)
        {
            return (connected ? SchoolRule + "\nGoogle is connected, so for his calendar and other mail use the google tool (calendar, add_event, change_event, find_mail, read_mail, draft_mail, send_mail) and never open Gmail or Google Calendar in the browser or in a use_computer job for those. The browser is only for what the tool cannot do, such as archiving, deleting or labelling mail." : "");
        }
    }
}
