using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeHologram(List<string> checks)
        {
            System.Reflection.FieldInfo field = typeof(Preferences).GetField("Hologram");
            if (field == null || field.GetCustomAttributes(typeof(System.Runtime.Serialization.DataMemberAttribute), false).Length == 0) throw new Exception("Hologram isn't saved with her settings.");
            if (new Preferences().Hologram) throw new Exception("Hologram should be off by default.");
            if (Hands.Tools.IndexOf("\"hologram\"", StringComparison.Ordinal) < 0) throw new Exception("Her brains can't call the hologram tool.");
            string tool;
            ToolArguments args;
            foreach (string on in new[] { "hologram on", "hologram mode on", "turn on hologram mode", "Capri, turn on hologram mode" })
                if (!Hands.Heard(on, out tool, out args) || tool != "hologram" || args.Action != "on") throw new Exception("Hologram on wasn't heard: " + on);
            foreach (string off in new[] { "hologram off", "hologram mode off", "turn off hologram mode", "exit hologram mode" })
                if (!Hands.Heard(off, out tool, out args) || tool != "hologram" || args.Action != "off") throw new Exception("Hologram off wasn't heard: " + off);
            foreach (string plain in new[] { "what's a hologram", "holograms are cool", "the hologram in that movie was sick" })
                if (Hands.Heard(plain, out tool, out args) && tool == "hologram") throw new Exception("Plain talk about holograms flipped hologram mode: " + plain);
            string close;
            Tunable found = Tuning.Find("hologram", "off", out close);
            if (found == null || found.Field != "pref:Hologram" || !found.Toggle) throw new Exception("Her setting tool can't find hologram by voice.");
            if (Array.IndexOf(ForkUpstreamSettings.Routed, "Hologram") < 0) throw new Exception("Hologram isn't routed into our build.");
            checks.Add("PASS hologram (096-16): saved and off by default, her brains can call it, hologram on and off are heard in plain words while talk about holograms isn't, her setting tool finds it by voice, and it is routed into our build with a Capri+ switch to leave it");
        }
    }
}
