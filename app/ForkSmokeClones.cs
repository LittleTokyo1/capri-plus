using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeClones(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.Clones || fresh.UpsetChaos) throw new Exception("Clones should be on and chaos when upset off by default.");
            if (CloneHook.Refusal(fresh, false, false) != null) throw new Exception("Clones were refused with the switch on and no game.");
            if (CloneHook.Refusal(fresh, true, false) != CloneHook.InGame || CloneHook.Refusal(fresh, false, true) != CloneHook.InGame) throw new Exception("Clones were allowed in a game or under the lockout.");
            fresh.Clones = false;
            if (CloneHook.Refusal(fresh, false, false) != CloneHook.Off) throw new Exception("Clones ran with their switch off.");
            if (!Lockout.Handles("clone")) throw new Exception("The lockout doesn't cover the clone tool.");
            Func<string, bool> runningWas = Lockout.Running;
            try
            {
                Lockout.Running = delegate { return true; };
                Lockout.Recheck();
                if (Lockout.Refuse("clone") != Lockout.Refusal) throw new Exception("The clone tool wasn't refused mid-match.");
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                if (Lockout.Refuse("clone") != null) throw new Exception("The clone tool was refused with no game.");
            }
            finally { Lockout.Running = runningWas; Lockout.Recheck(); }

            Swarm locked = new Swarm(1920, 1080, 7, 0);
            locked.Pending = 6;
            locked.Windows = delegate { return new List<CloneHaul> { new CloneHaul { Window = new IntPtr(1234), X = 300, Y = 200, Width = 800, Height = 600 } }; };
            Point cursor = new Point(900, 500);
            for (double now = 0; now < 90; now += 0.066)
            {
                if (locked.Tick(now, 0.066, true, false, cursor, false) != null) throw new Exception("A clone grabbed the cursor while her hands weren't free (a game or the lockout).");
                if (locked.Hauls.Count > 0) throw new Exception("A clone went for a window while hauling wasn't allowed.");
            }
            if (locked.Bodies.Count != 6) throw new Exception("Six asked clones didn't come out: " + locked.Bodies.Count);

            Swarm free = new Swarm(1920, 1080, 11, 0);
            free.Pending = 20;
            free.Windows = delegate { return new List<CloneHaul> { new CloneHaul { Window = new IntPtr(4321), X = 300, Y = 200, Width = 800, Height = 600 } }; };
            bool nabbed = false, hauled = false;
            double at = 0;
            for (; at < 120 && !(nabbed && hauled); at += 0.066)
            {
                if (free.Tick(at, 0.066, true, true, cursor, true) != null) nabbed = true;
                if (free.Hauls.Count > 0) hauled = true;
            }
            if (!nabbed || !hauled) throw new Exception("With free hands the clones never grabbed the cursor or a window: " + nabbed + " " + hauled);
            free.Tick(at, 0.066, true, false, cursor, false);
            if (free.Hauls.Count != 0 || free.Nabber != null) throw new Exception("A game starting didn't make the clones drop the cursor and the window.");

            List<KeyValuePair<int, string>> script = CloneScript("1: i'm the real one\n2) no you're not\ncapri 3 - we vote\nnot a line");
            if (script.Count != 3 || script[1].Key != 2 || script[2].Value != "we vote") throw new Exception("The clones' lines weren't read right.");

            string markerWas = CloneWindow.Marker, marker = Path.Combine(Path.GetTempPath(), "capri-clones-smoke-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                CloneWindow.Marker = marker;
                File.WriteAllText(marker, CloneWindow.MarkLine(new IntPtr(0x7FFFFFF0), 1, new Rectangle(10, 10, 400, 300), new Rectangle(10, 10, 1, 1), new Point(500, 400)) + Environment.NewLine + "not a line" + Environment.NewLine);
                if (CloneWindow.Recover() != 0 || File.Exists(marker)) throw new Exception("A moved-windows file with a gone window wasn't tidied away.");
                if (CloneWindow.Recover() != 0) throw new Exception("No moved-windows file still put something back.");
            }
            finally { CloneWindow.Marker = markerWas; if (File.Exists(marker)) File.Delete(marker); }

            Action rudeWas = Charm.RudeHeard;
            int rude = 0;
            try
            {
                Charm.RudeHeard = delegate { rude++; };
                Charm.Heard("you're so annoying capri", new DateTime(2026, 11, 4, 15, 0, 0), true, false);
                Charm.Heard("you're so annoying capri", new DateTime(2026, 11, 4, 15, 1, 0), true, true);
                Charm.Heard("thanks capri, you're the best", new DateTime(2026, 11, 4, 15, 2, 0), true, false);
            }
            finally { Charm.RudeHeard = rudeWas; Charm.Reset(); }
            if (rude != 1) throw new Exception("Chaos when upset heard " + rude + " insults, not just the one outside a game.");

            string close;
            Tunable chaos = Tuning.Find("chaos when upset", "on", out close);
            Tunable clones = Tuning.Find("clones", "off", out close);
            if (chaos == null || chaos.Field != "UpsetChaos" || clones == null || clones.Field != "Clones") throw new Exception("Her setting tool can't find the clone switches by voice.");
            checks.Add("PASS clones and chaos (096-09): clones on and chaos when upset off by default, both found by voice, the clone tool refused with its switch off, in a game or under the lockout, asked clones come out, with her hands not free (a game) no clone grabs the cursor or a window and a game starting makes them drop both, their lines read, a moved-windows file with gone windows tidied, only an insult outside a game sets chaos off");
        }
    }
}
