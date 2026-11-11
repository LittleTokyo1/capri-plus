using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class OwnReleases
    {
        internal const string Repo = "LittleTokyo1/capri-plus";
        internal const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
        internal const string DownloadPath = "/" + Repo + "/releases/download/";
        internal const int CheckHours = 24;
        internal const double FirstSeconds = 60;
        internal const int MostNotes = 300;
        internal static readonly Regex InstallAsk = new Regex(@"^(?:please\s+)?(?:install|apply|run|do)\s+(?:the\s+|that\s+|your\s+)?update(?:\s+now)?$|^update\s+(?:yourself|capri|now)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        internal static Func<string> Fetch = Download;
        internal static volatile string Notes = "";

        [DataContract]
        private sealed class ReleaseText
        {
            [DataMember(Name = "body")] public string Body;
        }

        internal static Release Latest()
        {
            string json = Fetch();
            if (string.IsNullOrEmpty(json)) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            Release found;
            using (MemoryStream body = new MemoryStream(bytes)) found = Companion.ParseRelease(body);
            ReleaseText text;
            using (MemoryStream body = new MemoryStream(bytes)) text = (ReleaseText)new DataContractJsonSerializer(typeof(ReleaseText)).ReadObject(body);
            Notes = Short(text == null ? null : text.Body);
            return found;
        }

        internal static string Short(string notes)
        {
            string plain = Regex.Replace(Regex.Replace(notes ?? "", @"[#*`>_\[\]]+", " "), @"\s+", " ").Trim();
            return plain.Length > MostNotes ? plain.Substring(0, MostNotes - 3).TrimEnd() + "..." : plain;
        }

        internal static bool Ours(string path)
        {
            return (path ?? "").StartsWith(DownloadPath, StringComparison.Ordinal);
        }

        internal static bool Due(long checkedAt, long now, double elapsed)
        {
            return elapsed >= FirstSeconds && (checkedAt <= 0 || now < checkedAt || now - checkedAt >= CheckHours * TimeSpan.TicksPerHour);
        }

        internal static bool Asked(string text)
        {
            string plain = Regex.Replace((text ?? "").Trim().TrimEnd('.', '!', '?'), @"^(hey\s+)?capri[,\s]+", "", RegexOptions.IgnoreCase).Trim();
            return InstallAsk.IsMatch(plain);
        }

        internal static string Told(string version, string notes)
        {
            string said = "Capri " + version + " is out";
            said += string.IsNullOrEmpty(notes) ? "." : ": " + notes.TrimEnd('.') + ".";
            return said + " Say \"install the update\" or use my tray menu when you're ready; it restarts me.";
        }

        private static string Download()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Api);
            request.UserAgent = "Capri-Desktop";
            request.Accept = "application/vnd.github+json";
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            try
            {
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException error)
            {
                HttpWebResponse failed = error.Response as HttpWebResponse;
                if (failed != null && failed.StatusCode == HttpStatusCode.NotFound) return null;
                throw;
            }
        }
    }

    internal sealed partial class Companion
    {
        private string toldUpdate;

        private void ForkReleaseTick(double elapsed)
        {
            if (testing) return;
            if (!updating && !installing && OwnReleases.Due(preferences.UpdateCheckedAt, DateTime.UtcNow.Ticks, elapsed)) CheckForUpdates(false);
            UpdateFile ready = updateFile;
            if (ready == null || ready.Version == toldUpdate || Lockout.Active || talkState != TalkState.Off || speech != null) return;
            toldUpdate = ready.Version;
            ActivityLog.Write("update", "told", ready.Version);
            heard = "(update)";
            box.Prompt = Bubble.Sys;
            Answer(++asking, OwnReleases.Told(ready.Version, OwnReleases.Notes), TalkState.Talking, chatting);
        }

        private bool UpdateHeard(string text, bool voiceChat)
        {
            if (voiceChat || !OwnReleases.Asked(text)) return false;
            string said;
            if (Lockout.Active) said = "After your game: installing the update restarts me.";
            else if (updateFile == null)
            {
                said = "No update is waiting, so I'm checking for one now.";
                if (!testing) CheckForUpdates(true);
            }
            else
            {
                said = "Installing Capri " + updateFile.Version + ". I'll be right back.";
                if (!testing) InstallUpdate();
            }
            ActivityLog.Write("update", "asked", said);
            Tell("(update)", said);
            return true;
        }
    }
}
