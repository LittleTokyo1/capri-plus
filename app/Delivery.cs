using System;
using System.Threading;

namespace CapriDesktop
{
    internal static class Delivery
    {
        internal static Func<bool> Reachable = delegate { return PhoneLive.Open() || ForkSettings.Current.DiscordOn && Discord.Ready; };
        internal static Func<string, bool> Send = delegate(string text) { return Route(text, delegate(string line) { return ForkSettings.Current.DiscordOn && Discord.Ready && Discord.Send(line); }); };

        internal static bool Route(string text, Func<string, bool> discord)
        {
            if (PhoneLive.Offer(text, PhoneLive.AckSeconds * 1000)) return true;
            return discord(text);
        }

        private const int PhoneMinutes = 10;
        private static long discordTicks;
        private static long phoneTicks;
        private static long atPcTicks;
        private static long contactTicks = DateTime.UtcNow.Ticks;

        internal static void Heard(string prompt)
        {
            Heard(prompt, DateTime.UtcNow);
        }

        internal static void Heard(string prompt, DateTime utc)
        {
            if (prompt == "> you: ")
            {
                Interlocked.Exchange(ref atPcTicks, utc.Ticks);
                Interlocked.Exchange(ref contactTicks, utc.Ticks);
            }
            else if (prompt == "> discord: ")
            {
                Interlocked.Exchange(ref discordTicks, utc.Ticks);
                Interlocked.Exchange(ref contactTicks, utc.Ticks);
            }
            else if (prompt == "> phone: ")
            {
                Interlocked.Exchange(ref phoneTicks, utc.Ticks);
                Interlocked.Exchange(ref contactTicks, utc.Ticks);
            }
        }

        internal static long PhoneMilliseconds(DateTime utc)
        {
            return Since(Interlocked.Read(ref phoneTicks), utc);
        }

        internal static long RemoteMilliseconds(DateTime utc)
        {
            return Math.Min(DiscordMilliseconds(utc), PhoneMilliseconds(utc));
        }

        internal static void Reset(DateTime utc)
        {
            Interlocked.Exchange(ref atPcTicks, 0);
            Interlocked.Exchange(ref discordTicks, 0);
            Interlocked.Exchange(ref phoneTicks, 0);
            Interlocked.Exchange(ref contactTicks, utc.Ticks);
        }

        internal static long DiscordMilliseconds(DateTime utc)
        {
            return Since(Interlocked.Read(ref discordTicks), utc);
        }

        internal static bool NotHome(ForkSettings settings, bool game, long idleMilliseconds, long atPcMilliseconds, long discordMilliseconds)
        {
            bool onPhone = !game && discordMilliseconds < PhoneMinutes * 60000L && discordMilliseconds < atPcMilliseconds;
            return onPhone || NotHome(settings, game, idleMilliseconds, atPcMilliseconds);
        }

        private static long Since(long ticks, DateTime utc)
        {
            return ticks == 0 ? long.MaxValue : Math.Max(0, (utc.Ticks - ticks) / TimeSpan.TicksPerMillisecond);
        }

        internal static long AtPcMilliseconds(DateTime utc)
        {
            return Since(Interlocked.Read(ref atPcTicks), utc);
        }

        internal static long ContactMilliseconds(DateTime utc)
        {
            return Since(Interlocked.Read(ref contactTicks), utc);
        }

        internal static bool NotHome(ForkSettings settings, bool game, long idleMilliseconds, long atPcMilliseconds)
        {
            return NotHome(settings, game, Math.Min(idleMilliseconds, atPcMilliseconds));
        }

        internal static bool NotHome(ForkSettings settings, bool game, long idleMilliseconds)
        {
            return NotHome(settings, game, idleMilliseconds, DateTime.Now);
        }

        internal static bool NotHome(ForkSettings settings, bool game, long idleMilliseconds, DateTime now)
        {
            return settings.HomeAway || !(settings.CallMode && CallFriends.Live(now)) && Autonomy.Away(game, idleMilliseconds, Autonomy.AwayMinutes);
        }

        internal static bool NotHome()
        {
            DateTime now = DateTime.UtcNow;
            return NotHome(ForkSettings.Current, Autonomy.Gaming(), Autonomy.Idle(), AtPcMilliseconds(now), RemoteMilliseconds(now));
        }

        internal static bool HeadsMay(ForkSettings settings, DateTime now, bool notHome)
        {
            return (settings.DiscordOn || PhoneLive.Open()) && (settings.Proactive == 0 || notHome) && !Autonomy.Quiet(now, settings.QuietFrom, settings.QuietTo);
        }
    }
}
