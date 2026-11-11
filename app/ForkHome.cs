using System;
using System.Collections.Generic;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private HomeWatch homeWatch;
        private List<HomeDevice> homeBefore;
        private DateTime lastHomeCheck = DateTime.MinValue;
        private bool homeChecking;

        private string GoAway()
        {
            ForkSettings settings = ForkSettings.Current;
            Home.Away(settings);
            settings.Paused = true;
            if (!testing) ForkSettings.Save();
            if (chatting) StopChat();
            ActivityLog.Write("paused", "away", null);
            return "Okay, going dark and quiet. Press the pause key when you're back.";
        }

        private string ComeHome()
        {
            HomeBack();
            ForkSettings.Current.Paused = false;
            if (!testing) ForkSettings.Save();
            ActivityLog.Write("unpaused", "home", null);
            return "Welcome back. Eyes and ears are on again.";
        }

        private void PhoneIpKeep(string table, string phone)
        {
            HomeDevice at = Home.Devices(table).Find(device => device.Mac == phone);
            ForkSettings settings = ForkSettings.Current;
            if (at == null || at.Ip == settings.PhoneIp) return;
            settings.PhoneIp = at.Ip;
            if (!testing) ForkSettings.Save();
            ActivityLog.Write("home", "phone ip noted", at.Ip);
        }

        private void PhoneRelearn()
        {
            List<HomeDevice> before = homeBefore;
            homeBefore = null;
            string phone = Home.Address(ForkSettings.Current.PhoneAddress);
            if (testing || before == null || phone == null) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string table = Home.Scan(phone);
                Post(delegate
                {
                    ForkSettings settings = ForkSettings.Current;
                    if (table == null || Home.SeenAny(table, phone, settings.PhoneIp)) return;
                    List<HomeDevice> now = Home.Devices(table);
                    HomeDevice found = Home.Relearn(before, now, settings.PhoneIp);
                    if (found == null)
                    {
                        ActivityLog.Write("home", "phone not relearned", now.Count + " private devices, " + before.Count + " before");
                        return;
                    }
                    settings.PhoneAddress = found.Mac;
                    settings.PhoneIp = found.Ip;
                    ForkSettings.Save();
                    homeWatch = null;
                    ActivityLog.Write("home", "phone relearned", found.Ip);
                });
            });
        }

        private void HomeBack()
        {
            ForkSettings settings = ForkSettings.Current;
            if (!settings.HomeAway) return;
            PhoneRelearn();
            Home.Back(settings);
            if (!testing) ForkSettings.Save();
            ActivityLog.Write("home", "back", "chattiness " + settings.Proactive);
        }

        private void ForkHomeTick()
        {
            if (testing) return;
            string phone = Home.Address(ForkSettings.Current.PhoneAddress);
            if (phone == null)
            {
                homeWatch = null;
                return;
            }
            DateTime now = DateTime.Now;
            if (homeChecking || (now - lastHomeCheck).TotalSeconds < Home.CheckSeconds) return;
            lastHomeCheck = now;
            homeChecking = true;
            if (homeWatch == null) homeWatch = new HomeWatch(Home.AwayMinutes, ForkSettings.Current.HomeAway);
            HomeWatch watch = homeWatch;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string table = Home.Scan(phone);
                Post(delegate
                {
                    homeChecking = false;
                    if (table == null || watch != homeWatch) return;
                    ForkSettings settings = ForkSettings.Current;
                    bool seen = Home.SeenAny(table, phone, settings.PhoneIp);
                    if (seen) PhoneIpKeep(table, phone);
                    if (!settings.HomeAway) homeBefore = null;
                    else if (!seen && homeBefore == null) homeBefore = Home.Devices(table);
                    HomeChange change = watch.Update(DateTime.Now, seen);
                    if (change == HomeChange.Came && ForkSettings.Current.HomeAway)
                    {
                        Tell("(home)", ComeHome());
                        ScenePrime();
                    }
                    else if (Home.Leaves(watch.Gone, ForkSettings.Current.HomeAway, Autonomy.Away(Lockout.Active, Autonomy.Idle(), Home.AwayMinutes))) GoAway();
                });
            });
        }
    }
}
