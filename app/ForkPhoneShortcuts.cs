using System;
using System.Collections.Generic;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool PhoneTurnNow()
        {
            lock (phoneGate) return phoneLine != null && phoneAnswer == null && (phoneTicket == -1 || phoneTicket == asking);
        }

        private void PhoneShortcutsWire()
        {
            PhoneServer.Happened = PhoneHappened;
            PhoneServer.Unbacked = PhoneUnbacked;
        }

        private string PhoneHappened(string kind, string value, string name)
        {
            PhoneFacts.Note(kind, value, name, DateTime.Now);
            if (kind == "home") { PhoneDo(PhoneCameHome); return "Welcome home."; }
            if (kind == "left") { PhoneDo(PhoneLeftHome); return "See you later."; }
            return "Noted.";
        }

        private void PhoneDo(Action act)
        {
            if (testing) act();
            else Post(act);
        }

        private void PhoneCameHome()
        {
            if (IsDisposed || !ForkSettings.Current.HomeAway) return;
            ActivityLog.Write("home", "back by the phone", null);
            Tell("(home)", ComeHome());
        }

        private void PhoneLeftHome()
        {
            if (IsDisposed || ForkSettings.Current.HomeAway) return;
            ActivityLog.Write("home", "left by the phone", null);
            GoAway();
        }

        private static void PhoneUnbacked(List<string> events)
        {
            List<string> ids = new List<string>(events);
            ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (string id in ids)
                {
                    string problem;
                    try { problem = PhoneActions.Uncalendar(id); }
                    catch (Exception error) { problem = error.GetType().Name; }
                    ActivityLog.Write("phone", problem == null ? "calendar backup removed" : "calendar backup kept", problem);
                }
            });
        }
    }
}
