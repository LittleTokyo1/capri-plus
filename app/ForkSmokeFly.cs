using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeFly(List<string> checks)
        {
            IntPtr a = new IntPtr(1001), b = new IntPtr(1002), none = IntPtr.Zero;
            IntPtr last = none;
            bool first = NewWindow(ref last, a), again = NewWindow(ref last, a), other = NewWindow(ref last, b), back = NewWindow(ref last, a), empty = NewWindow(ref last, none);
            if (!first || again || !other || !back || empty) throw new Exception("A job flew to a window more or less than once per arrival: " + first + again + other + back + empty);

            List<Ledge> spots = new List<Ledge>
            {
                new Ledge { Window = b, Left = 900, Right = 1400, Y = 200 },
                new Ledge { Window = a, Left = 100, Right = 600, Y = 150, Floor = true },
                new Ledge { Window = a, Left = 100, Right = 600, Y = 150 }
            };
            Ledge perch = Perch(spots, a);
            if (perch == null || perch.Window != a || perch.Floor || Perch(spots, new IntPtr(7)) != null || Perch(null, a) != null || Perch(spots, none) != null) throw new Exception("The window's own top edge was not picked, or a missing window found one.");
            int margin = 20;
            if (LandX(perch, 50, margin) != 120 || LandX(perch, 800, margin) != 580 || LandX(perch, 300, margin) != 300) throw new Exception("She would land off the window's edge: " + LandX(perch, 50, margin) + " / " + LandX(perch, 800, margin));
            if (LandX(new Ledge { Left = 100, Right = 110, Y = 1 }, 500, margin) != 105) throw new Exception("A tiny window did not get the middle.");

            string folder = Path.Combine(Path.GetTempPath(), "capri-fly-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string old = Path.Combine(folder, "old.json");
                File.WriteAllText(old, "{\"Proactive\":1,\"Cap\":10}");
                ForkSettings loaded = ForkSettings.Read(old);
                if (loaded == null || !loaded.FlyToWindows) throw new Exception("An old fork.json did not start with flying to windows on.");
                ForkSettings picked = new ForkSettings { FlyToWindows = false };
                using (ForkSettingsForm form = new ForkSettingsForm(picked)) form.Apply();
                if (picked.FlyToWindows) throw new Exception("The settings window did not save flying to windows off.");
                ForkSettings on = new ForkSettings { FlyToWindows = true };
                using (ForkSettingsForm form = new ForkSettingsForm(on)) form.Apply();
                if (!on.FlyToWindows) throw new Exception("The settings window did not save flying to windows on.");
            }
            finally
            {
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS fly to the window: a job flies beside a window only on the first step it takes there (again only after it was on another one), onto that window's own top edge on the side nearest her and never off its ends, never for a window without such an edge, and it is a setting that starts on and is saved");
        }
    }
}
