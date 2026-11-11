using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int JobChars = 100;

        private NowState NowFor(string text)
        {
            NowState state = Now.For(DateTime.Now, "");
            bool phone = typedLine && discordLine != null;
            state.Channel = PhoneTurnNow() ? "the Capri app on his iPhone" : phone ? "Discord, from his phone" : typedLine ? "typing at the PC" : "his voice";
            state.Phone.AddRange(PhoneFacts.Lines(DateTime.Now));
            state.Spoken = !typedLine;
            state.Game = Lockout.Game ?? careVoiceGame ?? "";
            ForkSettings settings = ForkSettings.Current;
            List<string> call = DiscordPresence.InCallWithHim(settings.DiscordOwner);
            if (call != null) state.Call.AddRange(call);
            state.Listen = settings.ListenMode || FriendMode.Active;
            state.Away = Delivery.NotHome();
            Job running = job;
            if (running != null && !string.IsNullOrEmpty(running.Words)) state.Job = Pursuit.Shorter(running.Words, JobChars);
            if (!string.IsNullOrEmpty(text)) state.Hints = Names.Hints(Names.Find(text));
            return state;
        }
    }
}
