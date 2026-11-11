using System;
using System.Collections.Generic;
using System.Net;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeStatusArt(List<string> checks)
        {
            string artWas = Presence.Art;
            try
            {
                StatusMoves.UseOwnArt();
                if (Presence.Art != StatusMoves.OwnArt || !Presence.Art.StartsWith("https://raw.githubusercontent.com/LittleTokyo1/capri-plus/main/presence/", StringComparison.Ordinal) || Presence.Art.IndexOf("capriai", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new Exception("Her Discord art still loads from upstream's site.");
                foreach (string doing in new[] { "Plugged in", "Chatting", "Dancing to music", "Asleep", "Voice chatting", "Taking a break", "Using the computer", "Playing a game", "Streaming", "Multiplying" })
                {
                    if (Array.IndexOf(StatusMoves.ArtNames, Presence.Look(doing)) < 0) throw new Exception("Our copy has no art for " + doing);
                    string badge = Presence.Badge(doing);
                    if (badge != null && Array.IndexOf(StatusMoves.ArtNames, badge) < 0) throw new Exception("Our copy has no badge for " + doing);
                }
                try
                {
                    HttpWebRequest asking = (HttpWebRequest)WebRequest.Create(StatusMoves.OwnArt + "plugged.gif");
                    asking.Method = "HEAD";
                    asking.Timeout = 8000;
                    using (HttpWebResponse answer = (HttpWebResponse)asking.GetResponse())
                        if (answer.StatusCode != HttpStatusCode.OK || answer.ContentType.IndexOf("image/gif", StringComparison.OrdinalIgnoreCase) < 0) throw new Exception("Our art link didn't answer with a GIF: " + answer.ContentType);
                    checks.Add("PASS Discord status art (096-12): her moving art loads from our own public copy on capri-plus, never upstream's site, every look and badge she can show is in it, and the link answers with a GIF");
                }
                catch (WebException error)
                {
                    HttpWebResponse answer = error.Response as HttpWebResponse;
                    if (answer != null && answer.StatusCode == HttpStatusCode.NotFound) throw new Exception("Our art isn't published at " + StatusMoves.OwnArt);
                    checks.Add("PASS Discord status art (096-12): her moving art loads from our own public copy on capri-plus, never upstream's site, and every look and badge she can show is in it");
                    checks.Add("LIMIT Discord status art link: no internet to check the GIF answers (" + error.Status + ")");
                }
            }
            finally { Presence.Art = artWas; }
        }
    }
}
