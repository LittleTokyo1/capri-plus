using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeFriendMode(List<string> checks)
        {
            DateTime at = new DateTime(2026, 11, 4, 21, 0, 0);
            try
            {
                FriendMode.Reset();
                if (FriendMode.Step(FriendMode.Auto, true, true, false, at) != null || FriendMode.Active) throw new Exception("Friends mode switched on at once, before the call had lasted a minute.");
                if (FriendMode.Step(FriendMode.Auto, true, true, false, at.AddSeconds(FriendMode.OnSeconds)) == null || !FriendMode.Active) throw new Exception("Friends mode did not switch on in a Discord call with friends while her mic was muted.");
                if (FriendMode.Step(FriendMode.Auto, true, false, false, at.AddSeconds(FriendMode.OnSeconds + 30)) != null || !FriendMode.Active) throw new Exception("Friends mode flipped off when the listener unmuted her mic during the call.");
                if (FriendMode.Step(FriendMode.Auto, false, false, true, at.AddMinutes(30)) != null || !FriendMode.Active) throw new Exception("Friends mode switched during a League game.");
                if (FriendMode.Step(FriendMode.Auto, false, false, false, at.AddMinutes(31)) != null || !FriendMode.Active) throw new Exception("Friends mode switched off the moment the call dropped, before the wait.");
                if (FriendMode.Step(FriendMode.Auto, false, false, false, at.AddMinutes(31).AddSeconds(FriendMode.OffSeconds)) == null || FriendMode.Active) throw new Exception("Friends mode stayed on after the call ended.");
                FriendMode.Reset();
                if (FriendMode.Step(FriendMode.Auto, true, false, false, at) != null || FriendMode.Step(FriendMode.Auto, true, false, false, at.AddMinutes(5)) != null || FriendMode.Active) throw new Exception("Friends mode switched on in a call while her mic was on.");
                if (FriendMode.Step(FriendMode.On, false, false, false, at) != "switched on" || !FriendMode.Active || FriendMode.Step(FriendMode.Off, true, true, false, at) != "switched off" || FriendMode.Active) throw new Exception("The friends mode switch by hand did not turn it on and off.");
                if (new ForkSettings().FriendsMode != FriendMode.Auto) throw new Exception("Friends mode does not start on automatic.");
            }
            finally { FriendMode.Reset(); }
            checks.Add("PASS friends mode: automatic switches on after a minute in a Discord call with friends while her mic is muted, stays on while the call lasts (also when the listener unmutes her), never switches during a League game, and goes off two minutes after the call; by hand it is plain on and off");
        }
    }
}
