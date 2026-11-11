using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeEverySetting(List<string> checks)
        {
            string[] guarded = { "never look apps", "monthly api cap", "elevenlabs character budget", "buying on her own", "messages in my name", "big jobs full access", "activity log", "security preset", "blackwall mode", "hey capri" };
            foreach (string key in guarded)
            {
                Tunable one = Tuning.Named(key);
                if (one == null || one.Locked != null || one.Guard == null) throw new Exception("A setting he wants by voice is still locked, or has no guard for his own words: " + key);
            }
            foreach (string key in new[] { "games that lock her hands", "lockout in champ select", "in-game lockout" })
                if (Tuning.Named(key) == null || Tuning.Named(key).Locked == null) throw new Exception("The in-game lockout became changeable by voice: " + key);
            if (Tuning.Spec.IndexOf("own words", StringComparison.Ordinal) < 0 || Tuning.Spec.IndexOf("SECURITY, buying and spend caps can't", StringComparison.Ordinal) >= 0) throw new Exception("Her setting tool still says SECURITY, buying and caps can't change, or doesn't say they need his own words.");
            string close;
            foreach (string[] said in new[] { new[] { "blackwall mode", "on", "blackwall mode" }, new[] { "security preset", "trusted", "security preset" }, new[] { "hey capri", "off", "hey capri" }, new[] { "buying", "suggest only", "buying on her own" } })
            {
                Tunable found = Tuning.Find(said[0], said[1], out close);
                if (found == null || found.Key != said[2]) throw new Exception("\"" + said[0] + "\" didn't find " + said[2] + ": " + (found == null ? close : found.Key));
            }
            string[][] his = { new[] { "never look apps", "put Steam on the never look apps list" }, new[] { "monthly api cap", "raise my spend cap to 40" }, new[] { "elevenlabs character budget", "set the elevenlabs budget to 50000" }, new[] { "buying on her own", "turn on buying on her own" }, new[] { "messages in my name", "let her send messages in my name" }, new[] { "big jobs full access", "give big jobs full access" }, new[] { "activity log", "turn off the activity log" }, new[] { "security preset", "set security to lockdown" }, new[] { "blackwall mode", "turn on blackwall" }, new[] { "hey capri", "turn off hey capri" } };
            foreach (string[] one in his) if (!Tuning.HisOwn(Tuning.Named(one[0]), one[1])) throw new Exception("His own words didn't count for " + one[0] + ": " + one[1]);
            string[][] near = { new[] { "hey capri", "hey capri what's up" }, new[] { "hey capri", "hey capri turn off the lights" }, new[] { "buying on her own", "order the usual" }, new[] { "messages in my name", "send Alex the link" }, new[] { "never look apps", "look at my screen" }, new[] { "big jobs full access", "open my files" }, new[] { "activity log", "what did your hands do today" } };
            foreach (string[] one in near) if (Tuning.HisOwn(Tuning.Named(one[0]), one[1])) throw new Exception("An everyday line counted as his ask for " + one[0] + ": " + one[1]);
            SmokeSecurityByVoice();
            SmokeCallFriendNoGuard();
            SmokeRoutedSwitches(checks);
            ForkSettings settings = ForkSettings.Current;
            int buying = settings.AutoBuying;
            double cap = settings.Cap;
            string[] blocklist = settings.Blocklist;
            Action<Tunable> changed = Tuning.Changed;
            try
            {
                Tuning.Changed = delegate(Tunable one) { };
                Tuning.Forget();
                settings.AutoBuying = 0;
                string refused = Hands.Do("setting", new ToolArguments { Action = "set", Name = "buying on her own", Level = "suggest only" });
                if (refused.IndexOf(Tuning.GuardNote, StringComparison.Ordinal) < 0 || settings.AutoBuying != 0) throw new Exception("A guarded setting changed without his own words: " + refused);
                refused = Hands.Do("setting", new ToolArguments { Action = "set", Name = "buying on her own", Level = "suggest only", Asked = "what's up, how was your day" });
                if (refused.IndexOf(Tuning.GuardNote, StringComparison.Ordinal) < 0 || settings.AutoBuying != 0) throw new Exception("A guarded setting changed on words of his that never named it: " + refused);
                string done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "buying on her own", Level = "suggest only", Asked = "let her suggest buying things on her own" });
                if (!done.StartsWith("Changed", StringComparison.Ordinal) || settings.AutoBuying != 1) throw new Exception("His own words didn't change buying on her own: " + done);
                if (!Tuning.Undo(DateTime.Now).StartsWith("Changed back", StringComparison.Ordinal) || settings.AutoBuying != 0) throw new Exception("Undo didn't put buying on her own back.");
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "monthly api cap", Level = "40", Asked = "set my monthly api cap to 40 dollars" });
                if (!done.StartsWith("Changed", StringComparison.Ordinal) || (int)settings.Cap != 40) throw new Exception("His own words didn't change the monthly API cap: " + done);
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "never look apps", Level = "add Steam", Asked = "add Steam to the never look apps" });
                if (!done.StartsWith("Changed", StringComparison.Ordinal) || Array.IndexOf(settings.Blocklist, "Steam") < 0) throw new Exception("His own words didn't add to the never-look list: " + done);
                string locked = Hands.Do("setting", new ToolArguments { Action = "set", Name = "lockout in champ select", Level = "off", Asked = "turn the lockout in champ select off" });
                if (locked.IndexOf(Tuning.LockedNote, StringComparison.Ordinal) < 0) throw new Exception("The in-game lockout changed by voice, even on his own words: " + locked);
                string read = Hands.Do("setting", new ToolArguments { Action = "get", Name = "monthly api cap", Level = "-" });
                if (!read.Contains("40")) throw new Exception("Reading a guarded setting needed his words or failed: " + read);
            }
            finally
            {
                settings.AutoBuying = buying;
                settings.Cap = cap;
                settings.Blocklist = blocklist;
                Tuning.Forget();
                Tuning.Changed = changed;
            }
            checks.Add("PASS every setting by voice (his rule 2026-11-04): SECURITY presets, Blackwall, Hey Capri, buying and messages on her own, both spend caps, full access for big jobs, the activity log and the never-look list now change by voice or message, but only when his own words clearly name them (never her brain alone, never an everyday line that shares one word, never a friend's call ask he said yes to); undo puts them back exactly (SECURITY's whole toggle mix) and needs his words too; Custom is never a voice target; the in-game lockout stays locked even on his words");
        }

        private static void SmokeSecurityByVoice()
        {
            Tunable preset = Tuning.Named("security preset"), blackwall = Tuning.Named("blackwall mode");
            Func<int> presetGet = preset.Get, wallGet = blackwall.Get;
            Action<int> presetSet = preset.Set, wallSet = blackwall.Set;
            Action<Tunable> changed = Tuning.Changed;
            Action saved = Security.Changed;
            int was = Security.Mask;
            try
            {
                Tuning.Changed = delegate(Tunable one) { };
                Security.Changed = null;
                Tuning.Bind("security preset", delegate { return Security.Preset; }, delegate(int value) { if (value != Security.Custom) Security.Pick(value); });
                Tuning.Bind("blackwall mode", delegate { return Security.Preset == Security.Blackwall ? 1 : 0; }, delegate(int value) { Security.Pick(value != 0 ? Security.Blackwall : Security.Trusted); });
                Tuning.Forget();
                Security.Pick(Security.Guarded);
                string on = Hands.Do("setting", new ToolArguments { Name = "blackwall mode", Level = "on", Asked = "turn on blackwall mode" });
                if (!on.StartsWith("Changed", StringComparison.Ordinal) || Security.Preset != Security.Blackwall) throw new Exception("His own words didn't turn Blackwall on: " + on);
                if (Hands.Do("setting", new ToolArguments { Action = "undo", Level = "-" }).IndexOf(Tuning.GuardNote, StringComparison.Ordinal) < 0 || Security.Preset != Security.Blackwall) throw new Exception("A SECURITY change was undone without his words.");
                Hands.Do("setting", new ToolArguments { Action = "undo", Level = "-", Asked = "undo that" });
                if (Security.Preset != Security.Guarded) throw new Exception("Undo after Blackwall didn't go back to Guarded: " + Security.PresetName);
                Security.Pick(Security.Trusted);
                Security.Set(Security.Power, false);
                int custom = Security.Mask;
                Hands.Do("setting", new ToolArguments { Name = "security preset", Level = "lockdown", Asked = "set my security preset to lockdown" });
                if (Security.Preset != Security.Lockdown) throw new Exception("His own words didn't pick Lockdown.");
                Hands.Do("setting", new ToolArguments { Action = "undo", Level = "-", Asked = "put it back" });
                if (Security.Mask != custom) throw new Exception("Undo didn't bring back his own mix of SECURITY toggles.");
                string mix = Hands.Do("setting", new ToolArguments { Name = "security preset", Level = "custom", Asked = "set my security preset to custom" });
                if (mix.StartsWith("Changed", StringComparison.Ordinal) || Security.Mask != custom) throw new Exception("Custom was taken as a voice target: " + mix);
            }
            finally
            {
                Tuning.Forget();
                for (int i = 0; i < Security.Count; i++) Security.Set(i, (was & (1 << i)) != 0);
                preset.Get = presetGet;
                preset.Set = presetSet;
                blackwall.Get = wallGet;
                blackwall.Set = wallSet;
                Security.Changed = saved;
                Tuning.Changed = changed;
            }
        }

        private static void SmokeCallFriendNoGuard()
        {
            Func<string, string, string> asking = CallFriends.Asking;
            try
            {
                CallFriends.Reset();
                DateTime now = DateTime.Now;
                CallFriends.Asking = delegate(string system, string frame) { return "I'll ask him.\n" + CallFriends.Marker + " turn on blackwall mode"; };
                CallFriends.Answer("capri turn on blackwall mode", null, now);
                string run;
                if (CallFriends.Answered("yeah sure", now.AddSeconds(2), out run) == null || run == null) throw new Exception("The call test didn't hand back the friend's ask.");
                if (Tuning.HisOwn(Tuning.Named("blackwall mode"), run)) throw new Exception("A friend's call ask he said yes to counted as his own words for a SECURITY setting.");
                if (!CallFriends.Friends(run, now.AddSeconds(3)) || CallFriends.Friends("turn on blackwall mode please", now.AddSeconds(3))) throw new Exception("A friend's call ask wasn't told apart from his own line.");
            }
            finally
            {
                CallFriends.Asking = asking;
                CallFriends.Reset();
            }
        }
    }
}
