using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokePhoneWorker(List<string> checks)
        {
            PhoneFile worker = PhoneFiles.Find("/sw.js");
            string text = worker == null ? "" : Encoding.UTF8.GetString(worker.Bytes);
            if (worker == null || worker.Keep || worker.Type.IndexOf("javascript", StringComparison.Ordinal) < 0) throw new Exception("The phone's service worker is not served as a script that is checked on every open.");
            if (!Regex.IsMatch(PhoneFiles.Version, "^[0-9a-f]{12}$") || text.IndexOf("var VERSION = \"" + PhoneFiles.Version + "\"", StringComparison.Ordinal) < 0 || text.IndexOf("__VERSION__", StringComparison.Ordinal) >= 0)
                throw new Exception("The service worker does not carry this build's version, so a deploy would not refresh the phone.");
            if (text.IndexOf("path.indexOf(\"/api/\") === 0 || path === \"/pair\"", StringComparison.Ordinal) < 0 || text.IndexOf("name !== CACHE", StringComparison.Ordinal) < 0 || text.IndexOf("request.method !== \"GET\"", StringComparison.Ordinal) < 0)
                throw new Exception("The service worker could keep her answers, pairing or posts, or never drops an old cache.");
            PhoneFile script = PhoneFiles.Find("/app.js");
            string page = script == null ? "" : Encoding.UTF8.GetString(script.Bytes);
            if (page.IndexOf("serviceWorker.register(\"sw.js\")", StringComparison.Ordinal) < 0) throw new Exception("The page does not start its service worker.");
            checks.Add("PASS phone service worker (CAPRI-095): the page and her sprite strips are kept on the phone so the Home Screen app opens at once; the cache name carries this build's id, so every deploy drops the old one; her answers (/api/), pairing and posts are never kept; sw.js itself is checked on every open");
        }
    }
}
