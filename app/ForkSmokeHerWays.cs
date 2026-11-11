using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeHerWays(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.InsideJokes || !fresh.HerOpinions) throw new Exception("Inside jokes and her own opinions should be on by default.");
            string close;
            Tunable jokes = Tuning.Find("inside jokes", "off", out close);
            Tunable views = Tuning.Find("her own opinions", "off", out close);
            if (jokes == null || jokes.Field != "InsideJokes" || views == null || views.Field != "HerOpinions") throw new Exception("Her setting tool can't find inside jokes or her own opinions by voice.");
            string curious = HerWays.Lines(Charm.Steps[0], true, true, false);
            string close3 = HerWays.Lines(Charm.Steps[3], true, true, false);
            string devoted = HerWays.Lines(Charm.Steps[5], true, true, false);
            if (curious.IndexOf("heckle", StringComparison.Ordinal) < 0 || curious.IndexOf("corpo gig", StringComparison.Ordinal) >= 0) throw new Exception("Curious does not start with heckling his plays, or already has the top level.");
            if (close3.IndexOf("Saturday", StringComparison.Ordinal) < 0 || devoted.IndexOf("corpo gig", StringComparison.Ordinal) < 0) throw new Exception("Her own ideas do not unlock with charm.");
            if (devoted.IndexOf("Pet names", StringComparison.Ordinal) < 0 || HerWays.Lines(Charm.Steps[5], false, true, false).IndexOf("Pet names", StringComparison.Ordinal) >= 0) throw new Exception("Pet names do not follow her choice of nicknames.");
            if (HerWays.Lines(Charm.Steps[5], true, false, false).IndexOf("opinions", StringComparison.Ordinal) >= 0) throw new Exception("Her own opinions stayed with the switch off.");
            if (HerWays.Lines(Charm.Steps[5], true, true, true).IndexOf(HerWays.Names[5], StringComparison.Ordinal) >= 0) throw new Exception("Her warmest pet names showed with other people around.");
            if (curious == devoted) throw new Exception("Her ways do not grow with charm.");
            Func<bool> keptAround = Warmth.OthersAround;
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-herways-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Charm.Reset();
                Jokes.Reset();
                ForkSettings on = new ForkSettings();
                on.Tidy();
                ForkSettings.Current = on;
                Warmth.OthersAround = delegate { return false; };
                Charm.Set(Charm.Steps[3]);
                DateTime start = new DateTime(2026, 11, 6, 15, 0, 0);
                string hers = "I'd rather date a toaster than lose that lane again, choom.";
                if (!Jokes.Heard("lmao the toaster", hers, start, false)) throw new Exception("A laugh at her line did not save an inside joke.");
                if (Jokes.Heard("lmao the toaster", hers, start, false)) throw new Exception("The same moment was saved twice.");
                if (Jokes.Heard("what time is it", "Five past three.", start, false)) throw new Exception("A plain line was saved as an inside joke.");
                if (Jokes.Heard("haha nice", "Gank incoming top.", start, true)) throw new Exception("A joke was saved during a game.");
                if (Jokes.Heard("haha", "", start, false)) throw new Exception("A laugh with nothing before it was saved.");
                if (Jokes.Heard("Mallory on Discord: lmao", "My fans just did a backflip.", start, false)) throw new Exception("A friend's laugh read out from Discord was saved as his.");
                string keptNick = Brain.Nickname;
                Brain.Nickname = "Stand-in";
                bool named = HerWays.Block("hey", start).IndexOf("Pet names", StringComparison.Ordinal) >= 0;
                Brain.Nickname = keptNick;
                if (named) throw new Exception("Pet names fought the nickname set in the Mainframe.");
                if (!File.Exists(Jokes.FilePath)) throw new Exception("Inside jokes are not kept in jokes.json next to her settings.");
                if (Jokes.Bring("that toaster again", start.AddMinutes(10)) != "") throw new Exception("A joke came back in the same chat it was made.");
                if (Jokes.Bring("what's the weather", start.AddHours(2)) != "") throw new Exception("A joke came back when the talk did not touch it.");
                string back = Jokes.Bring("should I buy a toaster", start.AddHours(2));
                if (back.IndexOf("toaster", StringComparison.Ordinal) < 0) throw new Exception("A joke did not come back when the talk touched it.");
                if (Jokes.Bring("toaster shopping", start.AddHours(2)).IndexOf("toaster", StringComparison.Ordinal) < 0) throw new Exception("A rebuilt prompt in the same minute lost the inside joke.");
                if (Jokes.Bring("the toaster lol", start.AddHours(3)) != "") throw new Exception("A joke came back before resting a few days.");
                if (Jokes.Bring("the toaster lol", start.AddDays(4)) == "") throw new Exception("A rested joke never came back.");
                if (HerWays.Block("toaster time", start.AddDays(9)).IndexOf("toaster", StringComparison.Ordinal) < 0) throw new Exception("The inside joke does not reach her chat prompt.");
                Warmth.OthersAround = delegate { return true; };
                if (HerWays.Block("toaster time", start.AddDays(20)).IndexOf("toaster", StringComparison.Ordinal) >= 0) throw new Exception("An inside joke came back with other people around.");
                Warmth.OthersAround = delegate { return false; };
                on.InsideJokes = false;
                if (HerWays.Block("toaster time", start.AddDays(30)).IndexOf("toaster", StringComparison.Ordinal) >= 0) throw new Exception("An inside joke came back with the switch off.");
                if (Jokes.Heard("lol", "My fans just did a backflip.", start.AddDays(30), false)) throw new Exception("A joke was saved with the switch off.");
                on.InsideJokes = true;
                Jokes.Reset();
                try { File.Delete(Jokes.FilePath); } catch (Exception) { }
                Charm.Set(Charm.Steps[0]);
                for (int i = 0; i < 6; i++) Jokes.Heard("haha", "Line number " + i + " about a gremlin.", start.AddMinutes(i), false);
                if (Jokes.Count != Jokes.Most(Charm.Steps[0])) throw new Exception("Curious kept " + Jokes.Count + " jokes, not " + Jokes.Most(Charm.Steps[0]) + ".");
                if (Jokes.Most(Charm.Steps[5]) <= Jokes.Most(Charm.Steps[0])) throw new Exception("More inside jokes do not unlock with charm.");
            }
            finally
            {
                Warmth.OthersAround = keptAround;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                Charm.Reset();
                Jokes.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
            checks.Add("PASS her ways: inside jokes and her own opinions are on by default and found by voice; pet names (her choice of nicknames), her opinions, small stories and her own ideas per level (heckling his plays at Curious, Saturday chaos at Close, the corpo gig line at Devoted) grow with charm, the warmest names stay out with others around; a laugh at her line saves an inside joke in jokes.json (never in a game, no doubles, more kept at higher charm), and it comes back only when the talk touches it, not in the same chat, rested a few days, never with others around or the switch off");
        }
    }
}
