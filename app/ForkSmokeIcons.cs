using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeIcons(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.IconChaos || !fresh.FakeCrash) throw new Exception("Icon chaos and the fake crash should be on by default.");
            if (IconHook.IconsRefusal(fresh, false, false, "mess") != null || IconHook.CrashRefusal(fresh, false, false, "show") != null) throw new Exception("Icon chaos or the fake crash was refused with nothing in the way.");
            if (IconHook.IconsRefusal(fresh, true, false, "mess") != CloneHook.InGame || IconHook.IconsRefusal(fresh, false, true, "hide") != CloneHook.InGame || IconHook.CrashRefusal(fresh, false, true, "show") != CloneHook.InGame) throw new Exception("Icon chaos or the fake crash was allowed in a game.");
            if (IconHook.IconsRefusal(fresh, true, true, "back") != null || IconHook.CrashRefusal(fresh, true, true, "stop") != null) throw new Exception("Putting icons back or closing the fake crash was refused.");
            fresh.IconChaos = false;
            fresh.FakeCrash = false;
            if (IconHook.IconsRefusal(fresh, false, false, "mess") != IconHook.IconsOff || IconHook.CrashRefusal(fresh, false, false, "show") != IconHook.CrashOff) throw new Exception("Icon chaos or the fake crash ran with its switch off.");
            if (IconHook.IconsRefusal(fresh, false, false, "back") != null) throw new Exception("Putting icons back was refused with the switch off.");
            foreach (string tool in new[] { "desktop_icons", "fake_crash", "clone" }) if (!Lockout.Handles(tool)) throw new Exception("The lockout doesn't cover " + tool + ".");

            Func<IntPtr> targetWas = DesktopIcons.Target;
            string markerWas = IconMischief.Marker, marker = Path.Combine(Path.GetTempPath(), "capri-icons-smoke-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                DesktopIcons.Target = delegate { return IntPtr.Zero; };
                IconMischief.Marker = marker;
                IconMischief none = new IconMischief(DesktopIcons.Target, 3);
                if (none.Start(0, false) || none.Vanish(0) || none.Active) throw new Exception("An icon prank started with no desktop to reach.");
                if (File.Exists(marker)) throw new Exception("An icon prank that never started left a file.");
                File.WriteAllLines(marker, new[] { "shuffle", IconMischief.MarkLine("Recycle Bin", new Point(20, 20), new Point(600, 400), false) });
                string kept = File.ReadAllText(marker);
                if (IconMischief.Recover() >= 0 || File.ReadAllText(marker) != kept) throw new Exception("With Explorer gone the icons' way back was lost.");
                File.Delete(marker);
                if (IconMischief.Recover() != 0) throw new Exception("With nothing scattered she still put icons back.");
            }
            finally { DesktopIcons.Target = targetWas; IconMischief.Marker = markerWas; if (File.Exists(marker)) File.Delete(marker); }

            string close;
            Tunable icons = Tuning.Find("desktop icon chaos", "off", out close);
            Tunable crash = Tuning.Find("fake crash", "off", out close);
            if (icons == null || icons.Field != "IconChaos" || crash == null || crash.Field != "FakeCrash") throw new Exception("Her setting tool can't find icon chaos or the fake crash by voice.");
            if (!Hands.Tools.Contains("\"name\":\"desktop_icons\"") || !Hands.Tools.Contains("\"name\":\"fake_crash\"") || !Hands.Tools.Contains("\"name\":\"clone\"")) throw new Exception("Her brain isn't offered the clone, icon or fake crash tools.");
            checks.Add("PASS desktop icon chaos and fake crash (096-10): both on by default and found by voice, refused with their switch off or in a game (the lockout covers both tools), putting icons back and closing the crash always allowed, no prank starts without a desktop to reach, with Explorer gone the icons' way back is kept for later, her brain is offered clone, desktop_icons and fake_crash");
        }
    }
}