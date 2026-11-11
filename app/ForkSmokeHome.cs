using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeHome(List<string> checks)
        {
            string[] leaving = { "i'm leaving", "im leaving", "I am leaving", "i'm heading out", "i'm going out", "away mode", "capri i'm leaving now", "i'm off to uni", "i'm heading to uni", "i'm going to work", "im heading to the gym", "i'm going to school" };
            string[] back = { "i'm home", "im home", "I am back", "i'm back", "i'm back home", "home mode", "capri i'm home" };
            foreach (string said in leaving) if (TweakFor(said) != "away") throw new Exception("\"" + said + "\" was not heard as leaving: " + (TweakFor(said) ?? "nothing"));
            foreach (string said in back) if (TweakFor(said) != "home") throw new Exception("\"" + said + "\" was not heard as home: " + (TweakFor(said) ?? "nothing"));
            string[] other = { "i'm leaving for the game in ten minutes", "tell him i'm home", "is he home", "what does home mode do", "i'm leaving you a note", "away mode for the heartbeat please explain", "when i'm home play music" };
            foreach (string said in other) if (TweakFor(said) == "away" || TweakFor(said) == "home") throw new Exception("\"" + said + "\" was taken as leaving or home.");

            ForkSettings s = new ForkSettings { Proactive = 2 };
            Home.Away(s);
            if (!s.HomeAway || s.Proactive != 0 || s.HomeProactive != 2) throw new Exception("Away did not go quiet and keep the old setting.");
            s.Proactive = 0;
            Home.Away(s);
            if (s.HomeProactive != 2) throw new Exception("Going away twice lost the stored setting.");
            Home.Back(s);
            if (s.HomeAway || s.Proactive != 2) throw new Exception("Coming home did not restore what he had set: " + s.Proactive);
            Home.Back(s);
            if (s.Proactive != 2) throw new Exception("Coming home twice changed the setting.");
            ForkSettings changed = new ForkSettings { Proactive = 1 };
            Home.Away(changed);
            changed.Proactive = 2;
            Home.Back(changed);
            if (changed.Proactive != 2 || changed.HomeAway) throw new Exception("A change he made while away was overwritten.");
            ForkSettings quiet = new ForkSettings { Proactive = 0 };
            Home.Away(quiet);
            Home.Back(quiet);
            if (quiet.Proactive != 0) throw new Exception("A quiet setting was turned chatty on coming home.");

            string arp = "\r\nInterface: 192.168.1.10 --- 0x7\r\n  Internet Address      Physical Address      Type\r\n  192.168.1.1           10-20-30-40-50-60     dynamic   \r\n  192.168.1.23          aa-bb-cc-dd-ee-ff     dynamic   \r\n  192.168.1.255         ff-ff-ff-ff-ff-ff     static    \r\n";
            if (!Home.Seen(arp, "AA:BB:CC:DD:EE:FF") || !Home.Seen(arp, "aa-bb-cc-dd-ee-ff") || !Home.Seen(arp, "192.168.1.23") || Home.Seen(arp, "192.168.1.2") || Home.Seen(arp, "aa-bb-cc-dd-ee-00") || Home.Seen(arp, "") || Home.Seen(null, "aa-bb-cc-dd-ee-ff") || Home.Seen(arp, "ff-ff-ff-ff-ff-ff")) throw new Exception("The ARP table was read wrongly.");
            if (Home.Address("AA:BB:CC:DD:EE:FF") != "aa-bb-cc-dd-ee-ff" || Home.Address(" 192.168.1.23 ") != "192.168.1.23" || Home.Address("not an address") != null || Home.Address(null) != null) throw new Exception("A phone address was read wrongly.");

            DateTime t = new DateTime(2026, 10, 30, 12, 0, 0);
            HomeWatch watch = new HomeWatch(10);
            if (watch.Update(t, true) != HomeChange.None || watch.Update(t.AddMinutes(5), false) != HomeChange.None || watch.Update(t.AddMinutes(9), false) != HomeChange.None) throw new Exception("A short gap counted as leaving.");
            if (watch.Update(t.AddMinutes(11), false) != HomeChange.Left || watch.Update(t.AddMinutes(12), false) != HomeChange.None) throw new Exception("Leaving was not reported once.");
            if (watch.Update(t.AddMinutes(30), true) != HomeChange.Came || watch.Update(t.AddMinutes(31), true) != HomeChange.None) throw new Exception("Coming home was not reported once.");
            if (!watch.Update(t.AddMinutes(40), false).Equals(HomeChange.None) || watch.Gone) throw new Exception("A phone that is back was still counted as gone.");
            watch.Update(t.AddMinutes(52), false);
            if (!watch.Gone || !Home.Leaves(watch.Gone, false, true) || Home.Leaves(watch.Gone, false, false) || Home.Leaves(watch.Gone, true, true) || Home.Leaves(false, false, true)) throw new Exception("Going away on her own did not wait for him to be away from the PC (no input for ten minutes, no game).");
            HomeWatch flap = new HomeWatch(10);
            flap.Update(t, true);
            if (flap.Update(t.AddMinutes(3), false) != HomeChange.None || flap.Update(t.AddMinutes(4), true) != HomeChange.None || flap.Update(t.AddMinutes(8), false) != HomeChange.None || flap.Update(t.AddMinutes(13), false) != HomeChange.None) throw new Exception("A phone that sleeps for a few minutes toggled her.");
            HomeWatch restarted = new HomeWatch(10, true);
            if (restarted.Update(t, false) != HomeChange.None || restarted.Update(t.AddMinutes(3), true) != HomeChange.Came || restarted.Update(t.AddMinutes(4), true) != HomeChange.None) throw new Exception("After a restart while he was away, his phone coming back did not bring her home.");
            string awayTable = "Interface: 192.168.1.20 --- 0x7\n  Internet Address      Physical Address      Type\n  192.168.1.1           a4-91-b1-00-00-01     dynamic\n  192.168.1.107         ba-39-00-00-00-02     dynamic\n  192.168.1.255         ff-ff-ff-ff-ff-ff     static\n";
            string backTable = awayTable + "  192.168.1.36          5a-a7-46-03-f1-bd     dynamic\n";
            List<HomeDevice> awayDevices = Home.Devices(awayTable), backDevices = Home.Devices(backTable);
            if (awayDevices.Count != 1 || awayDevices[0].Mac != "ba-39-00-00-00-02" || backDevices.Count != 2 || !Home.Private("5a-a7-46-03-f1-bd") || Home.Private("a4-91-b1-00-00-01")) throw new Exception("The Wi-Fi table was not read into private-address devices: " + awayDevices.Count + " / " + backDevices.Count);
            if (!Home.SeenAny(backTable, "12-19-59-97-c6-37", "192.168.1.36") || Home.SeenAny(awayTable, "12-19-59-97-c6-37", "192.168.1.36") || !Home.SeenAny(backTable, "5a-a7-46-03-f1-bd", null) || Home.SeenAny(backTable, "12-19-59-97-c6-37", null)) throw new Exception("His phone was not seen by its saved IP when its Wi-Fi address changed, or was seen when it was gone.");
            HomeDevice learned = Home.Relearn(awayDevices, backDevices, "192.168.1.234");
            if (learned == null || learned.Mac != "5a-a7-46-03-f1-bd" || learned.Ip != "192.168.1.36") throw new Exception("A phone with a new Wi-Fi address was not relearned from the device that appeared while he was out.");
            List<HomeDevice> two = Home.Devices(backTable + "  192.168.1.147         9a-17-00-00-00-03     dynamic\n");
            HomeDevice byIp = Home.Relearn(awayDevices, two, "192.168.1.147");
            if (Home.Relearn(awayDevices, two, "192.168.1.234") != null || byIp == null || byIp.Mac != "9a-17-00-00-00-03" || Home.Relearn(backDevices, backDevices, null) != null) throw new Exception("Two new devices were guessed between instead of left alone (or the saved IP did not pick one).");
            checks.Add("PASS phone relearn: his phone counts as home by its saved Wi-Fi address or its saved IP; when both changed, his return (pause key or I'm home) saves the one private-address device that appeared on the Wi-Fi since he went away (the saved IP picks between several, otherwise none is guessed); a restart while he is away still lets his phone coming back bring her home");
            HomeWatch unknown = new HomeWatch(10);
            if (unknown.Update(t, false) != HomeChange.None || unknown.Update(t.AddMinutes(9), false) != HomeChange.None || unknown.Update(t.AddMinutes(10), false) != HomeChange.Left) throw new Exception("An unknown presence did not turn quiet after the wait.");

            ForkSettings keptSettings = ForkSettings.Current;
            try
            {
                ForkSettings.Current = new ForkSettings { Proactive = 2 };
                string awaySaid = ApplyTweak("away");
                if (!ForkSettings.Current.Paused || !ForkSettings.Current.HomeAway || ForkSettings.Current.Proactive != 0 || !awaySaid.Contains("pause key")) throw new Exception("Leaving did not pause her and go quiet: " + awaySaid);
                HomeBack();
                if (ForkSettings.Current.HomeAway || ForkSettings.Current.Proactive != 2) throw new Exception("The pause key did not bring her back to what he had set.");
                ForkSettings.Current.Paused = true;
                string homeSaid = ApplyTweak("home");
                if (ForkSettings.Current.Paused || ForkSettings.Current.HomeAway || !homeSaid.Contains("Welcome back")) throw new Exception("Saying home did not wake her: " + homeSaid);
            }
            finally
            {
                ForkSettings.Current = keptSettings;
            }
            checks.Add("PASS home and away: I'm leaving and I'm home (and the short ways of saying them, not longer sentences or talk about someone else) switch her to quiet and paused and back to exactly what he had set, a change he made while away is kept, the pause key also brings her home, the phone is found in the ARP table by Wi-Fi address or IP, a few minutes of Wi-Fi sleep do not toggle her, and an unknown presence turns quiet after ten minutes");
        }
    }
}
