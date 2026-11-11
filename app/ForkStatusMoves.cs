using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class StatusMoves
    {
        internal const string OwnArt = "https://raw.githubusercontent.com/LittleTokyo1/capri-plus/main/presence/";
        internal static readonly string[] ArtNames = { "asleep", "break", "chatting", "clones", "computer", "controller", "cursor", "dancing", "game", "live", "plugged", "voice" };
        internal static volatile bool Buttoned = true;

        internal static void UseOwnArt() { Presence.Art = OwnArt; }
        internal static Func<string> FetchApp = Discord.AppId;
        internal static Func<bool> BotReady = delegate { return Discord.Connected; };
        private const int RetryMinutes = 10;
        private static volatile string botApp;
        private static int asking;
        private static DateTime nextTry = DateTime.MinValue;

        internal static bool Showing()
        {
            ForkSettings current = ForkSettings.Current;
            if (current == null || !current.DiscordStatus) return false;
            string app = botApp;
            if (app == null) { Ask(); return false; }
            if (Presence.AppId != app) { Presence.AppId = app; Buttoned = false; }
            return true;
        }

        internal static bool Learn(string id)
        {
            if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, @"^\d{15,21}$")) return false;
            botApp = id;
            return true;
        }

        internal static void Forget()
        {
            botApp = null;
            nextTry = DateTime.MinValue;
        }

        private static void Ask()
        {
            if (DateTime.Now < nextTry) return;
            nextTry = DateTime.Now.AddMinutes(RetryMinutes);
            if (!BotReady()) return;
            if (Interlocked.Exchange(ref asking, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (Learn(FetchApp())) ActivityLog.Write("discord", "status", "shown through her bot's app");
                    else ActivityLog.Write("discord", "status", "her bot's app id not read, tried again in " + RetryMinutes + " minutes");
                }
                catch (Exception error) { ActivityLog.Write("discord", "status", "app id failed: " + error.GetType().Name); }
                finally { Interlocked.Exchange(ref asking, 0); }
            });
        }
    }

    internal static partial class Discord
    {
        internal static string AppId()
        {
            int status;
            Dictionary<string, object> app = Parse(Api("GET", "/oauth2/applications/@me", null, out status));
            return app == null ? null : Text(app, "id");
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox discordStatus;

        private void DiscordStatusPage(ForkSettings current)
        {
            Section("DISCORD STATUS  (my profile)");
            discordStatus = Check("Discord status: my Discord profile shows her moving (her animations, using the computer, playing a game, how many clones) through her own bot's app, with no links to other sites", current.DiscordStatus);
        }

        private void FillDiscordStatus()
        {
            settings.DiscordStatus = discordStatus.Checked;
        }
    }
}
