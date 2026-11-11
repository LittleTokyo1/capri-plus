using System;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal const string RunesKept = "You set your own runes, so I left them. Press Push if you want build 1.";
        private bool runesSeen;
        private long runesPage = -1;
        private long runesModified;

        internal static RunePage CurrentRunes(RunePage[] pages)
        {
            return pages == null ? null : Array.Find(pages, delegate(RunePage page) { return page != null && page.Current; });
        }

        internal static bool RunesChanged(long startPage, long startModified, RunePage[] pages)
        {
            RunePage now = CurrentRunes(pages);
            if (now == null || League.Hers(now.Name)) return false;
            return now.Id != startPage || now.Modified > startModified;
        }

        private void RunesAtStart()
        {
            if (runesSeen) return;
            runesSeen = true;
            RunePage start = CurrentRunes(League.Parse<RunePage[]>(league.Get("/lol-perks/v1/pages")));
            runesPage = start == null ? -1 : start.Id;
            runesModified = start == null ? 0 : start.Modified;
        }

        private bool RunesHisOwn()
        {
            if (!runesSeen) return false;
            return RunesChanged(runesPage, runesModified, League.Parse<RunePage[]>(league.Get("/lol-perks/v1/pages")));
        }

        private void EndRunes()
        {
            runesSeen = false;
            runesPage = -1;
            runesModified = 0;
        }
    }
}
