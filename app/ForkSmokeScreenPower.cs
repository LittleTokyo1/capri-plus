using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeScreenPower(List<string> checks)
        {
            foreach (string said in new[] { "wake my screens", "wake the screens", "screens on", "turn my screens on", "turn the monitors back on", "wake up my displays" })
            {
                string tool;
                ToolArguments args;
                if (!Hands.Direct(said, out tool, out args) || tool != "power" || args.Action != "screens_on") throw new Exception("\"" + said + "\" did not go to the screens_on power action: " + tool + " " + (args == null ? "" : args.Action));
            }
            string offTool;
            ToolArguments offArgs;
            if (!Hands.Direct("turn my screens off", out offTool, out offArgs) || offArgs.Action != "screens_off") throw new Exception("Screens off broke.");
            if (!Hands.Valid("power", new ToolArguments { Action = "screens_on" })) throw new Exception("The brain's power tool does not accept screens_on.");
            if (Hands.Tools.IndexOf("screens_on", StringComparison.Ordinal) < 0) throw new Exception("The brain's power tool does not offer screens_on.");
            Func<bool> kept = ScreenPower.Wake;
            int woke = 0;
            try
            {
                ScreenPower.Wake = delegate { woke++; return true; };
                DateTime before = DateTime.UtcNow;
                string reply = Hands.Do("power", new ToolArguments { Action = "screens_on" });
                if (woke != 1 || ScreenPower.NudgedAt < before) throw new Exception("screens_on did not send the wake signal once: " + woke + ", " + reply);
                if (reply != ScreenPower.Said(true) || reply.IndexOf("wake signal", StringComparison.Ordinal) < 0) throw new Exception("screens_on claimed more than it knows: " + reply);
                ScreenPower.Wake = delegate { return false; };
                string failed = Hands.Do("power", new ToolArguments { Action = "screens_on" });
                if (failed != ScreenPower.Said(false)) throw new Exception("A failed wake was not told as a failure: " + failed);
            }
            finally { ScreenPower.Wake = kept; }
            checks.Add("PASS screens on: wake my screens, screens on and turn the monitors back on go straight to a real screens_on power action (the Windows monitor-on message, a display-required ping and a one-pixel real mouse nudge), the brain's power tool offers and accepts it, screens off still works, and her reply says only that the wake signal was sent (never that the screens are on), or that it failed");
        }
    }
}
