using System;
using System.Collections.Generic;
using System.Text;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokePhoneFlipped(List<string> checks)
        {
            Clip perch = null, plain = null;
            foreach (Clip clip in Clip.Listed())
            {
                if (perch == null && clip.HasFlipped) perch = clip;
                if (plain == null && !clip.HasFlipped) plain = clip;
            }
            if (perch == null || plain == null) throw new Exception("Her clip table has no clip with her artist's left-facing art, or none without.");
            string list = PhoneFiles.Clips();
            if (list.IndexOf("{\"name\":" + PhoneServer.Json(perch.Name), StringComparison.Ordinal) < 0 || list.IndexOf("\"flipped\":true", StringComparison.Ordinal) < 0 || list.IndexOf("\"flipped\":false", StringComparison.Ordinal) < 0)
                throw new Exception("The phone's clip list does not say which clips have her left-facing art.");
            PhoneFile left = PhoneFiles.Find("/sprite/" + perch.Name + PhoneFiles.FlippedEnd + ".png"), right = PhoneFiles.Find("/sprite/" + perch.Name + ".png");
            if (left == null || right == null || left.Type != "image/png" || !left.Keep || BitConverter.ToString(left.Bytes) == BitConverter.ToString(right.Bytes))
                throw new Exception("Her left-facing " + perch.Name + " strip was not served, or is the same as the right-facing one.");
            if (PhoneFiles.Find("/sprite/" + plain.Name + PhoneFiles.FlippedEnd + ".png") != null || PhoneFiles.Find("/sprite/nobody" + PhoneFiles.FlippedEnd + ".png") != null)
                throw new Exception("A left-facing strip was served for a clip that has none.");
            PhoneFile script = PhoneFiles.Find("/app.js");
            string page = script == null ? "" : Encoding.UTF8.GetString(script.Bytes);
            if (page.IndexOf("clips[name].flipped ? name + \"_flipped\"", StringComparison.Ordinal) < 0) throw new Exception("The phone page does not use her left-facing art.");
            checks.Add("PASS phone left-facing art (CAPRI-095): facing left, the phone draws her artist's flipped strip for the clips that have one (perch, laydown), like her PC body, and a plain mirror only for the rest; a flipped strip is never served for a clip without one");
        }
    }
}
