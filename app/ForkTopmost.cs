using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool HerTopmost { get { return IsHandleCreated && (Native.GetWindowLong(Handle, -20) & 0x8) != 0; } }

        // Windows can leave her without the topmost bit after Always on top went off and on again: raising her to the top
        // then reports success and changes nothing. Dropping her out of the topmost band first puts her back in it.
        private void KeepTopmost()
        {
            if (!preferences.OnTop || HerTopmost) return;
            Native.SetWindowPos(Handle, new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            foreach (Form above in new Form[] { pill, box, typer }) if (above.IsHandleCreated) Native.SetWindowPos(above.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }

        private void SmokeTopmost(List<string> checks)
        {
            if (!preferences.OnTop || !IsHandleCreated || hidden) { checks.Add("LIMIT topmost repair: she was hidden or Always on top was off at this point of the smoke"); return; }
            preferences.OnTop = false;
            Layer();
            preferences.OnTop = true;
            Layer();
            lastForeground = new IntPtr(-1);
            StayOnTop();
            if (!HerTopmost) throw new Exception("After Always on top went off and on, she stayed out of the topmost band.");
            foreach (Form above in new Form[] { pill, box, typer }) if (above.IsHandleCreated && (Native.GetWindowLong(above.Handle, -20) & 0x8) == 0) throw new Exception("After her topmost repair " + above.GetType().Name + " was left below the topmost band.");
            checks.Add("PASS Always on top off and on again leaves her topmost (Windows can keep the bit off while saying it raised her, so she drops out of the band and back in), with her pill, bubble and typing box still on top");
        }
    }
}
