using System;
using System.IO;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class IconHook
    {
        internal const string IconsOff = "Messing with your desktop icons is switched off in Capri+ (CLONES AND CHAOS), choom.";
        internal const string CrashOff = "My fake blue screen is switched off in Capri+ (CLONES AND CHAOS), choom.";
        internal static Func<string, string> Icons = delegate { return "Couldn't do that just now."; };
        internal static Func<string, string> Crash = delegate { return "Couldn't show my fake blue screen."; };

        internal static string Refusal(bool on, string off, bool locked, bool game, string action, string harmless)
        {
            if (action == harmless) return null;
            if (!on) return off;
            if (locked || game) return CloneHook.InGame;
            return null;
        }

        internal static string IconsRefusal(ForkSettings settings, bool locked, bool game, string action)
        {
            return Refusal(settings != null && settings.IconChaos, IconsOff, locked, game, action, "back");
        }

        internal static string CrashRefusal(ForkSettings settings, bool locked, bool game, string action)
        {
            return Refusal(settings != null && settings.FakeCrash, CrashOff, locked, game, action, "stop");
        }
    }

    internal sealed partial class Companion
    {
        private void IconsStart(string data)
        {
            IconHook.Icons = delegate(string action) { return IconHook.IconsRefusal(ForkSettings.Current, Lockout.Active, Computer.GameOpen() != null, action) ?? IconTool(action); };
            IconHook.Crash = delegate(string action) { return IconHook.CrashRefusal(ForkSettings.Current, Lockout.Active, Computer.GameOpen() != null, action) ?? CrashTool(action); };
            iconRecoveryTimer.Tick += IconRecoveryTick;
            iconTimer.Interval = 120;
            iconTimer.Tick += IconTick;
            if (testing)
            {
                DesktopIcons.Target = delegate { return IntPtr.Zero; };
                return;
            }
            IconMischief.Marker = Path.Combine(data, "scattered-icons.txt");
            BeginIconRecovery();
        }

        private void IconsGuardTick()
        {
            if (iconChaos == null && chaosScreen == null) return;
            bool game = Lockout.Active || Computer.GameOpen() != null;
            if (iconChaos != null && (game || !ForkSettings.Current.IconChaos))
            {
                ClearIcons();
                ActivityLog.Write("icons", "put back", game ? "a game started" : "switched off");
            }
            if (chaosScreen != null && (game || !ForkSettings.Current.FakeCrash))
            {
                ChaosOff();
                ActivityLog.Write("fake crash", "closed", game ? "a game started" : "switched off");
            }
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox iconChaos, fakeCrash;

        private void IconsPage(ForkSettings current)
        {
            iconChaos = Check("Desktop icon chaos: with clones out (or when I ask) she scatters or hides my desktop icons, then puts every one back, also after a crash", current.IconChaos);
            fakeCrash = Check("Fake crash: one pretend blue screen per chaos session (or when I ask); Esc or a click closes it, never over full screen, a stream or a game", current.FakeCrash);
        }

        private void FillIcons()
        {
            settings.IconChaos = iconChaos.Checked;
            settings.FakeCrash = fakeCrash.Checked;
        }
    }
}
