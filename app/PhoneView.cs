using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CapriDesktop
{
    internal static class PhoneView
    {
        internal const int MostDigest = 7;
        internal const int JobChars = 160;
        internal const string Paused = "I'm paused, so I'm not looking at your screen.";
        internal const string NeverLook = "An app on your never-look list is in front, so no screenshot.";

        internal static string Refusal(bool paused, bool neverLook)
        {
            if (paused) return Paused;
            return neverLook ? NeverLook : null;
        }

        internal static string Json(bool paused, bool game, List<string> jobs, List<DigestItem> digest)
        {
            StringBuilder json = new StringBuilder("{\"paused\":").Append(paused ? "true" : "false").Append(",\"game\":").Append(game ? "true" : "false").Append(",\"jobs\":[");
            for (int i = 0; jobs != null && i < jobs.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(PhoneServer.Json(Pursuit.Shorter(jobs[i], JobChars)));
            }
            json.Append("],\"digest\":[");
            List<DigestItem> newest = digest == null ? new List<DigestItem>() : new List<DigestItem>(digest);
            newest.Sort(delegate(DigestItem a, DigestItem b) { return b.When.CompareTo(a.When); });
            for (int i = 0; i < newest.Count && i < MostDigest; i++)
            {
                if (i > 0) json.Append(',');
                json.Append("{\"source\":").Append(PhoneServer.Json(newest[i].Source)).Append(",\"text\":").Append(PhoneServer.Json(newest[i].Text)).Append(",\"at\":").Append(PhoneServer.Json(newest[i].When.ToString("HH:mm", CultureInfo.InvariantCulture))).Append('}');
            }
            return json.Append("]}").ToString();
        }
    }
}
