using System;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class FreeKeys
    {
        internal const int Row = 24;
        internal const string BlindOff = "DeepSeek can't see your screen. Turn on FREE KEYS in Capri+, then press Free key in Capri Mainframe and connect a free NVIDIA key.";
        internal const string UnservedOff = "Google doesn't serve Gemini where you are. Turn on FREE KEYS in Capri+, then press Free key for a free NVIDIA key.";
        internal const string OfflineOff = "My brain's offline. Right-click me, open Capri Mainframe and press Set up her brain, or turn on FREE KEYS in Capri+ for a free NVIDIA key.";
        internal static Func<bool> On = delegate { return ForkSettings.Current.FreeKeys; };

        internal static string Line(string on, string off)
        {
            return On() ? on : off;
        }

        internal static bool MiddleShown(bool on, int tier)
        {
            return on || tier == 1;
        }

        internal static int Gap(bool on, int tier)
        {
            return MiddleShown(on, tier) ? 0 : Row;
        }
    }

    internal sealed partial class Mainframe
    {
        private bool middleTierShown = true;

        private int TierGap { get { return FreeKeys.Gap(ForkSettings.Current.FreeKeys, tier); } }

        private void ForkMiddleTier()
        {
            middleTierShown = FreeKeys.MiddleShown(ForkSettings.Current.FreeKeys, tier);
            rungs[1].Visible = freeKey.Visible = middleTierShown;
            hint.Top = Scaled(264 - TierGap);
        }

        private bool ForkMiddleFits()
        {
            if (!middleTierShown) return rungs[2].Top >= rungs[0].Bottom;
            return rungs[1].Top >= rungs[0].Bottom && rungs[2].Top >= rungs[1].Bottom && freeKey.Left >= rungs[1].Right && freeKey.Top >= rungs[1].Top && freeKey.Bottom <= rungs[1].Bottom;
        }

        internal bool MiddleTierShown { get { return middleTierShown; } }

        internal string Crowded { get { return TextFits() ?? Overlap(); } }

        internal int MaxRungStep { get { return (int)Math.Round((rungs[2].Top - rungs[0].Top) / (double)Scaled(FreeKeys.Row)); } }
    }

    internal sealed partial class Companion
    {
        private void FreeKeysOn()
        {
            Switched("free keys", delegate { return ForkSettings.Current.FreeKeys; }, delegate
            {
                ForkSettings.Current.FreeKeys = !ForkSettings.Current.FreeKeys;
                if (!testing) ForkSettings.Save();
                ShowTiers();
            });
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox freeKeysBox;

        private void FreeKeysPage(ForkSettings current)
        {
            Section("FREE KEYS");
            freeKeysBox = Check("Free keys: show MEDIUM INTELLIGENCE in the Mainframe's BRAIN, a free brain on an NVIDIA, Google AI Studio (Gemini) or OpenRouter key (free keys can be rate limited and slow); off, only NORMAL and MAX INTELLIGENCE show", current.FreeKeys);
        }

        private void FillFreeKeys()
        {
            settings.FreeKeys = freeKeysBox.Checked;
        }
    }
}
