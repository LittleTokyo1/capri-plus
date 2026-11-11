using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;

namespace CapriDesktop
{
    internal static class PhoneMicTrace
    {
        private const int MostHeldMs = 60000;
        private const int MostPlayedMs = 600000;

        internal const string ChatEars = "voice chat ears";
        internal const string OwnEars = "her ears";
        internal const string WindowsEars = "windows speech";

        internal static string Ears(bool chatEars, bool ownEars)
        {
            if (chatEars) return ChatEars;
            return ownEars ? OwnEars : WindowsEars;
        }

        internal static string LastClip =Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Capri", "phone-last.wav");

        internal static string Extra(NameValueCollection query)
        {
            string extra = "";
            int held = Number(query["held"], MostHeldMs);
            if (held > 0) extra += ", held " + held.ToString(CultureInfo.InvariantCulture) + " ms";
            string how = query["how"];
            if (how == "rec") extra += ", recorder";
            else if (how == "tap") extra += ", tap";
            string app = query["app"];
            if (app == "1") extra += ", home screen app";
            else if (app == "0") extra += ", safari";
            int played = Number(query["played"], MostPlayedMs);
            if (played > 0) extra += ", her voice " + played.ToString(CultureInfo.InvariantCulture) + " ms before";
            return extra;
        }

        internal static void Keep(short[] samples)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LastClip));
                File.WriteAllBytes(LastClip, Voice.Wave(samples, 16000));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static int Number(string asked, int most)
        {
            int value;
            return int.TryParse(asked, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= most ? value : 0;
        }
    }
}
