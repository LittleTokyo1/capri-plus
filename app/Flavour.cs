namespace CapriDesktop
{
    // Extra lines for her system prompt from Capri+ settings, added after personality.md and her rules.
    internal static class Flavour
    {
        internal const string Exclaims = "yabai, maji de?, uso!, nani?!, yatta!, ehhh?!";
        internal const string Curses = "kuso!, chikushou!, mou~, urusai!, baka";
        internal const string Slang = "maji (seriously), chou / mecha (super), gachi (for real), yabai (crazy, good or bad), ukeru (lol), emoi (in your feels), bimyou (meh), uzai (annoying), kimoi (gross), ryo (ok, got it), otsu (gg, nice work)";

        internal static string Extra()
        {
            ForkSettings settings = ForkSettings.Current;
            string extra = Personality.Lines(settings, Brain.Wordiness) + Japanese(settings.Japanese, settings.JapaneseOften) + Naming(settings.HerChoiceName, Brain.Nickname);
            string league = League.Brief;
            if (!string.IsNullOrEmpty(league) && league.StartsWith(LiveGame.Opening, System.StringComparison.Ordinal)) extra += "\n" + league + " If they ask about the match, answer from this.";
            else if (!string.IsNullOrEmpty(league)) extra += "\nThey're in League of Legends champ select right now. " + league + " If they ask for counters or what to pick, name exactly 3 champions and nothing else: no reasons. Never bring up counters unless they ask. For runes or builds, answer from this in one short sentence.";
            return extra;
        }

        internal static string Japanese(int level, bool often)
        {
            if (level <= 0) return "";
            string rate = often ? "in about every other reply" : "in about one reply in five";
            string line = "\nJapanese flavour: " + rate + ", drop in a short Japanese exclamation written in romaji when it fits the moment, like " + Exclaims + ". Never say ara ara or sugoi.";
            line += " When something goes wrong (they die, lose, or a plan flops) a mild Japanese curse is fine, like " + Curses + (Computer.Bypass ? "." : ", even though you never swear in English.");
            if (level >= 2) line += " Mix in casual young-people Japanese slang too, the way a Tokyo teen texts, with the rest in English: " + Slang + ". Never more than two Japanese words in one reply.";
            return line;
        }

        internal static string Naming(bool herChoice, string nickname)
        {
            if (!herChoice || !string.IsNullOrEmpty(nickname)) return "";
            return "\nNicknames are your call: now and then give them a teasing nickname that fits your mood or what they're doing, and let it change over time. Never anything mean.";
        }
    }
}
