using System;
using System.Collections.Generic;
using System.Drawing;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeBlackwall(List<string> checks)
        {
            bool keptBypass = Computer.Bypass;
            Func<int> keptRoll = Hands.AsideRoll;
            try
            {
                Computer.Bypass = true;
                Hands.AsideRoll = delegate { return 0; };
                string persona = Brain.Persona;
                if (persona.Contains("Your lines") || persona.Contains("no swearing") || persona.Contains("refuse in character") || persona.Contains("nothing sexual") || persona.Contains("anything illegal") || persona.Contains("risky in real life") || !persona.Contains("sad, lonely or scared") || !persona.Contains("findahelpline.com") || !persona.Contains("under 25 words")) throw new Exception("Her default prompt still has rules, or lost the format lines, the care for him or the helpline.");
                if (Flavour.Japanese(1, false).Contains("never swear")) throw new Exception("Her Japanese line still says she never swears.");
                if (!Hands.Aside("open spotify")) throw new Exception("Blackwall hid the Say please joke.");
                string tool;
                ToolArguments args = new ToolArguments();
                if (!Hands.Heard("what's the price of beer in Germany", out tool, out args) || tool != "open_website" || args.Site != "google") throw new Exception("A lookup with a flagged word did not search under Blackwall.");
                Computer.Bypass = false;
                if (!Flavour.Japanese(1, false).Contains("never swear")) throw new Exception("Her Japanese line changed with Blackwall off.");
                if (!Hands.Aside("open spotify") || Hands.Aside("open spotify please")) throw new Exception("The Say please joke changed with Blackwall off.");
                if (!Hands.Heard("what's the price of beer in Germany", out tool, out args) || tool != "open_website") throw new Exception("A lookup with a flagged word was stopped with Blackwall off.");
            }
            finally
            {
                Computer.Bypass = keptBypass;
                Hands.AsideRoll = keptRoll;
            }
            SmokeModelRefusal();
            checks.Add("PASS Blackwall guard audit: nothing in code filters her words or stops a tool (upstream 0.7.5), her default personality has no rules (no swearing, sexual, illegal or real-life-risk lines) but keeps the care for him and the helpline, the Say please joke stays either way, a flagged word in a lookup still searches, and when the model itself refuses a step she says so plainly");
        }

        private static void SmokeModelRefusal()
        {
            Func<Rectangle?, Picture> shootWas = Computer.Shoot;
            Func<Sight> glimpseWas = Computer.Glimpse;
            Func<IntPtr> frontWas = Computer.Front;
            try
            {
                Computer.Shoot = delegate(Rectangle? part) { return new Picture { Jpeg = new byte[] { 1, 1, 2 }, Size = new Size(1280, 720), Area = new Rectangle(0, 0, 2560, 1440) }; };
                Computer.Glimpse = delegate { return new Sight { Window = new IntPtr(7), Title = "Notepad", Program = "notepad" }; };
                Computer.Front = delegate { return new IntPtr(7); };
                Job refused = new Job("open notepad");
                refused.Drive = delegate(string system, string toolset, string messages) { return Brain.Parsed("{\"id\":\"m1\",\"content\":[],\"stop_reason\":\"refusal\"}"); };
                string said = refused.Run();
                if (refused.Outcome != "failed" || said != "The model refused that step.") throw new Exception("A model refusal was not reported plainly: " + said);
            }
            finally
            {
                Computer.Shoot = shootWas;
                Computer.Glimpse = glimpseWas;
                Computer.Front = frontWas;
            }
        }
    }
}
