using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeCallLines(List<string> checks)
        {
            DateTime now = new DateTime(2026, 11, 4, 21, 0, 0);
            ForkSettings settings = new ForkSettings();
            settings.Tidy();
            long idle = 40L * 60000L;
            try
            {
                CallFriends.Reset();
                if (!Delivery.NotHome(settings, false, idle)) throw new Exception("40 minutes idle without call mode wasn't away.");
                settings.CallMode = true;
                if (!Delivery.NotHome(settings, false, idle, now)) throw new Exception("Call mode with no call going counted him at the PC.");
                CallFriends.Heard(ListenBook.Friend, "yo capri", now.AddMinutes(-3));
                if (Delivery.NotHome(settings, false, idle, now)) throw new Exception("In a live call her lines still went to Discord as if he was away.");
                if (!Delivery.NotHome(settings, false, idle, now.AddMinutes(CallFriends.LiveMinutes + 1))) throw new Exception("A call quiet for over 10 minutes still kept him at the PC.");
                settings.HomeAway = true;
                if (!Delivery.NotHome(settings, false, idle, now)) throw new Exception("His I'm leaving was ignored in a call.");
                settings.HomeAway = false;
                settings.CallMode = false;
                if (!Delivery.NotHome(settings, false, idle, now)) throw new Exception("A live call kept him home with call mode off.");
                settings.CallMode = true;
                CallFriends.Said("Your mail check is done, two new mails.", settings, now);
                CallFriends.Said("Your mail check is done, two new mails.", settings, now.AddSeconds(1));
                string call = CallFriends.Transcript();
                int first = call.IndexOf("CAPRI: Your mail check is done", StringComparison.Ordinal);
                if (first < 0 || call.IndexOf("CAPRI: Your mail check is done", first + 1, StringComparison.Ordinal) >= 0) throw new Exception("Her own line didn't join the call once: " + call);
                CallFriends.Reset();
                CallFriends.Said("Not in a call.", settings, now);
                if (CallFriends.Transcript().Length != 0) throw new Exception("A line with no call going joined the call.");
            }
            finally { CallFriends.Reset(); }
            checks.Add("PASS call lines (096-04): in a live call (a line in the last 10 minutes) she counts him at the PC so check-ins and her own lines are said into the call instead of Discord, his I'm leaving still wins, call mode off or a quiet call changes nothing, every line she says joins the call's context once");
        }
    }
}
