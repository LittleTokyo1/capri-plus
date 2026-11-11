using System;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const long CharmAway = 120000;
        private int charmShown = -1;

        private void ForkCharmTick()
        {
            Charm.Together(DateTime.Now, !hidden && Native.IdleMilliseconds() < CharmAway);
            if (Charm.Version != charmShown) ForkCharmShow();
            ForkCharmNews();
            ForkMoodTick();
        }

        private void ForkCharmShow()
        {
            charmShown = Charm.Version;
            mainframe.ShowCharm(Charm.Label());
        }

        private void ForkCharmNews()
        {
            if (hidden || Lockout.Active || dragging || pressed || typer.Visible || working != null || outro != null) return;
            if (DanceBusy(clock.Elapsed.TotalSeconds)) return;
            string line = Charm.TakeNews();
            if (line == null) return;
            ActivityLog.Write("charm", "level up", line);
            KeyLights.Sweep(KeyLights.Rose);
            Remark("charm", line);
        }

        private string ForkPetLine()
        {
            string mine = Charm.PetLine(random);
            return mine ?? NextPetLine();
        }
    }
}
