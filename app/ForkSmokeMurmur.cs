using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeMurmur(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            Func<string, bool> keptRunning = Lockout.Running;
            Func<string, byte[]> keptFree = FreeSpeak;
            string folder = Path.Combine(Path.GetTempPath(), "capri-murmur-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                Heartbeat.Reset();
                Autonomy.Reset();
                DateTime now = DateTime.Now;

                Heartbeat.Enqueue("Your dentist is in 40 minutes.", now, null, false);
                Autonomy.Record("research", "Want me to look up the new patch notes for you?", now);
                Heartbeat.Enqueue("Want me to look up the new patch notes for you?", now, null, false);
                if (Autonomy.IsOffer("Your dentist is in 40 minutes.") || !Autonomy.IsOffer("Want me to look up the new patch notes for you?") || Autonomy.IsOffer("")) throw new Exception("An offer was not told apart from a heads-up.");
                string first = Heartbeat.Next(now, true, Autonomy.IsOffer);
                if (first == null || !first.StartsWith("Want me to look up", StringComparison.Ordinal) || Heartbeat.Next(now, true, Autonomy.IsOffer) != null) throw new Exception("In a game only an offer may be taken from the queue, once: " + first);
                string other = Heartbeat.Next(now, true);
                if (other == null || !other.StartsWith("Your dentist", StringComparison.Ordinal)) throw new Exception("A heads-up was lost while an offer was taken in a game: " + other);

                ForkSettings settings = ForkSettings.Current;
                settings.AutonomyOn = true;
                settings.Proactive = 2;
                settings.QuietFrom = 0;
                settings.QuietTo = 0;
                settings.Games = new[] { "smokegame" };
                Lockout.Running = delegate(string name) { return name == "smokegame"; };
                Lockout.Recheck();
                FreeSpeak = delegate(string words) { return new byte[] { 1 }; };
                bool game = Lockout.Active;
                bool allowed = MayMurmur(settings);
                settings.Paused = true;
                bool paused = MayMurmur(settings);
                settings.Paused = false;
                settings.Proactive = 0;
                bool quiet = MayMurmur(settings);
                settings.Proactive = 2;
                settings.AutonomyOn = false;
                bool off = MayMurmur(settings);
                settings.AutonomyOn = true;
                settings.QuietFrom = now.Hour;
                settings.QuietTo = (now.Hour + 1) % 24;
                bool night = MayMurmur(settings);
                settings.QuietFrom = 1;
                settings.QuietTo = 8;
                Lockout.Running = delegate(string name) { return false; };
                Lockout.Recheck();
                bool outside = MayMurmur(settings);
                settings.QuietFrom = 0;
                settings.QuietTo = 0;
                bool keptChatting = chatting;
                int keptLoud = loudRun;
                bool talkOff = HeartMaySpeak(settings);
                chatting = true;
                loudRun = 0;
                bool idleChat = HeartMaySpeak(settings);
                loudRun = 3;
                bool busyChat = HeartMaySpeak(settings);
                chatting = keptChatting;
                loudRun = keptLoud;
                if (!talkOff || !idleChat || busyChat) throw new Exception("A heads-up while voice chat is on must wait only while he is talking: " + talkOff + " " + idleChat + " " + busyChat + " (proactive " + settings.Proactive + ", silent " + Autonomy.Silent(settings, DateTime.Now) + ", paused " + settings.Paused + ", night " + settings.NightOn + ", game " + Lockout.Active + ", hidden " + hidden + ", dragging " + dragging + ", speech " + (speech != null) + ", talk " + talkState + ", typer " + typer.Visible + ")");
                if (!game || !allowed || paused || quiet || off || night || outside) throw new Exception("Speaking an offer in a game was allowed or refused wrongly: game " + game + " allowed " + allowed + " paused " + paused + " quiet " + quiet + " off " + off + " night " + night + " outside " + outside);
            }
            finally
            {
                Lockout.Running = keptRunning;
                Lockout.Recheck();
                FreeSpeak = keptFree;
                Heartbeat.Reset();
                Autonomy.Reset();
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS offers in a game: during a game an offer (and only an offer, never another heads-up) is taken from the queue and spoken by voice alone, with no speech bubble, only when autonomy is on, she is not paused, chattiness is not Quiet, it is not quiet hours, and a voice is available; outside a game nothing changes");
        }
    }
}
