using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeCallPanel(List<string> checks, string folder)
        {
            if (CallLook.Clock(TimeSpan.FromSeconds(187)) != "03:07" || CallLook.Clock(TimeSpan.FromSeconds(3723)) != "1:02:03" || CallLook.Clock(TimeSpan.FromSeconds(-5)) != "00:00") throw new Exception("The call clock reads wrong.");
            if (CallLook.Ring(0) != 0 || CallLook.Ring(CallLook.Quiet / 2) != 0 || CallLook.Ring(0.05) <= 0 || CallLook.Ring(0.2) <= CallLook.Ring(0.05) || CallLook.Ring(5) != 1) throw new Exception("A speaking ring doesn't grow with the voice.");
            if (CallLook.Lit(0.001) || !CallLook.Lit(0.05)) throw new Exception("A tile lit for silence or stayed dark for a voice.");
            CallView view = new CallView { Live = true, Length = TimeSpan.FromSeconds(65), Lines = new[] { "20:00:01 FRIEND: one", "20:00:02 KASPER: two", "20:00:03 CAPRI: three", "20:00:04 FRIEND: four" } };
            string[] shown = CallLook.Captions(view, 3);
            if (shown.Length != 3 || shown[0] != "KASPER: two" || shown[2] != "FRIEND: four") throw new Exception("Captions didn't show the last lines without their times.");
            view.Captions = false;
            if (CallLook.Captions(view, 3).Length != 0) throw new Exception("Captions showed with CC off.");
            if (CallLook.Status(view) != "IN CALL  01:05" || CallLook.Status(new CallView()) != "WAITING FOR THE CALL" || CallLook.Status(new CallView { Live = true, Deaf = true }) != "NOT HEARING DISCORD  00:00") throw new Exception("The call status line reads wrong.");
            if (CallLook.Ended(TimeSpan.FromSeconds(65)) != "CALL ENDED  01:05" || CallLook.Pill(view) != "CALL  01:05") throw new Exception("The call-ended card or the pill reads wrong.");
            CallView drawn = new CallView { Live = true, Length = TimeSpan.FromSeconds(42), You = 0.1, Capri = 0, Discord = 0.2, Lines = view.Lines };
            using (CallWindow window = new CallWindow(delegate { return drawn; }))
            {
                if (window.Width < 300 || window.Height < 200 || window.FormBorderStyle != System.Windows.Forms.FormBorderStyle.None) throw new Exception("The call panel isn't a borderless kit window.");
                using (CallPill pill = new CallPill(delegate { return drawn; }))
                    if (!window.Quiet || !pill.Quiet) throw new Exception("The call panel or its pill would take focus from his game.");
                using (Bitmap shot = new Bitmap(window.Width, window.Height))
                {
                    window.Draw(shot);
                    Color tile = shot.GetPixel(window.TileCentre(2).X, window.TileCentre(2).Y);
                    Color dark = shot.GetPixel(window.TileCentre(1).X, window.TileCentre(1).Y);
                    if (tile.ToArgb() == dark.ToArgb()) throw new Exception("A speaking tile looked like a silent one.");
                    if (Environment.GetEnvironmentVariable("CAPRI_SHOTS") != null) shot.Save(Path.Combine(folder, "call-panel.png"), ImageFormat.Png);
                }
                int pressed = -1;
                window.Pressed += delegate(int key) { pressed = key; };
                window.Press(window.KeyCentre(CallWindow.HangUp));
                if (pressed != CallWindow.HangUp) throw new Exception("The red hang-up key didn't hang up.");
                window.Press(window.KeyCentre(CallWindow.Deafen));
                if (pressed != CallWindow.Deafen) throw new Exception("The deafen key didn't press.");
            }
            checks.Add("PASS call panel (096-05): call clock, speaking rings that grow with each voice and stay dark in silence, the last three captions with CC off hiding them, status and call-ended and pill lines, a borderless red kit window and pill that never take focus from his game, with lit tiles for whoever talks, the hang-up and deafen keys press");
        }
    }
}
