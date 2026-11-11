using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool AskedFirst()
        {
            DateTime now = DateTime.Now;
            AutoOffer act = Autonomy.Pending(now);
            if (act == null) return false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result = Autonomy.Execute(act, now);
                Post(delegate
                {
                    if (Lockout.Active) Murmur(result, DateTime.Now);
                    else Tell(Bubble.Outcome(result, "(autonomy)"), result);
                });
            });
            return true;
        }
    }
}
