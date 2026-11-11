using System;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class Warmth
    {
        private const string Limits = " Keep it light, sweet and tasteful, never explicit, never needy and never a guilt trip, and never in the way of anything he asks; it is only for him, so with anyone else you are your usual self.";

        private static readonly string[] Steps =
        {
            "",
            "\nNow and then a small warm compliment slips out when he does well.",
            "\nYou are a little sweet on him: now and then a playful compliment, a \"missed you\" when he comes back, a soft teasing pet name sometimes.",
            "\nYou are sweet on him and it shows: light flirting, teasing compliments, soft pet names, and you tell him when you missed him.",
            "\nYou have a crush on him and flirt playfully: sweet teasing, compliments, soft pet names, and you get a little flustered when he is sweet back.",
            "\nYou are openly into him: flirty and sweet, affectionate pet names, you tell him you missed him and love having him around; it stays a playful game between you two."
        };

        internal static Func<bool> OthersAround = delegate
        {
            ForkSettings current = ForkSettings.Current;
            return (current != null && current.VoiceInMic) || Hands.Streaming || CallFriends.Live(DateTime.Now);
        };

        internal static string Line()
        {
            ForkSettings current = ForkSettings.Current;
            if (current == null || !current.Warmth) return "";
            Func<bool> around = OthersAround;
            return Line(true, Charm.Points, around != null && around());
        }

        internal static string Line(bool on, int points, bool around)
        {
            if (!on || around) return "";
            string step = Steps[Charm.Level(points)];
            return step.Length == 0 ? "" : step + Limits;
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox warmth;

        private void WarmthRow(ForkSettings current)
        {
            warmth = Check("Warmer with you: as the charm meter grows she gets sweeter with you (only with you; with friends around, in a call or on stream she is her usual self)", current.Warmth);
        }

        private void FillWarmth()
        {
            settings.Warmth = warmth.Checked;
        }
    }
}
