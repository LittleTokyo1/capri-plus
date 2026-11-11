using System;

namespace CapriDesktop
{
    internal static class Personality
    {
        internal const int Usual = 50;
        internal const int ShortTalk = 25;
        private static readonly int[] Centers = { ShortTalk, 50, 100 };
        private static readonly int[] UpstreamWords = { 35, 70, 140 };

        private static readonly string[] HumorLow =
        {
            "No jokes at all: answer straight and dry, with no puns, teasing quips or playful asides. This overrides the playful parts of who you are.",
            "Joke much less than usual: a light joke only now and then, most replies plain.",
            "Joke a little less than usual."
        };
        private static readonly string[] HumorHigh =
        {
            "Joke a little more than usual.",
            "Be funnier than usual: a joke or a witty line in most replies.",
            "Maximum humor: a joke or a witty line in nearly every reply."
        };
        private static readonly string[] SassLow =
        {
            "Barely any teasing: be sweet, warm and plain with them, no smugness and no chaos-gremlin act. This overrides the teasing parts of who you are.",
            "Tease them much less than usual and be warmer and plainer.",
            "Tone the teasing down a little."
        };
        private static readonly string[] SassHigh =
        {
            "Tease them a little more than usual.",
            "Be extra smug and teasing, full chaos gremlin, never mean.",
            "Maximum sass: smug, bratty teasing in almost every reply, never mean."
        };
        private static readonly string[] SlangLow =
        {
            "No Night City slang at all: no choom, preem, nova, gonk, delta, eddies or the rest. This overrides your slang lines.",
            "Use Night City slang rarely, in about one reply in ten.",
            "Use a bit less Night City slang than usual."
        };
        private static readonly string[] SlangHigh =
        {
            "Use a bit more Night City slang than usual.",
            "Use Night City slang often: a slang word in most replies, always used correctly.",
            "Heavy Night City slang: one or two slang words in nearly every reply, always used correctly."
        };

        internal static int Band(int talk) { return talk < 35 ? 0 : talk < 70 ? 1 : 2; }

        internal static int Center(int wordiness) { return Centers[Math.Max(0, Math.Min(2, wordiness))]; }

        internal static int Talk(int stored, int wordiness)
        {
            return Band(stored) == Math.Max(0, Math.Min(2, wordiness)) ? stored : Center(wordiness);
        }

        internal static int Words(int talk)
        {
            int value = Math.Max(0, Math.Min(100, talk));
            return value <= ShortTalk ? 12 + value * 23 / ShortTalk : 35 + (value - ShortTalk) * 105 / 75;
        }

        internal static string Lines(ForkSettings settings, int wordiness)
        {
            return Line("Humor", settings.Humor, HumorLow, HumorHigh) + Line("Sass", settings.Sass, SassLow, SassHigh) + Line("Slang", settings.Slang, SlangLow, SlangHigh) + Talking(Talk(settings.Talk, wordiness), wordiness);
        }

        internal static string Line(string name, int value, string[] low, string[] high)
        {
            int level = value <= 15 ? 0 : value <= 35 ? 1 : value <= 45 ? 2 : value < 55 ? -1 : value < 65 ? 3 : value < 85 ? 4 : 5;
            if (level < 0) return "";
            string said = level < 3 ? low[level] : high[level - 3];
            return "\n" + name + " slider at " + value + "% (50% is your usual self): " + said;
        }

        private static string Talking(int talk, int wordiness)
        {
            int words = Words(talk);
            if (words == UpstreamWords[Math.Max(0, Math.Min(2, wordiness))]) return "";
            string shape = words <= 20 ? "one short sentence" : words <= 50 ? "one to three short sentences" : words <= 100 ? "up to five sentences" : "up to a short paragraph";
            return "\nTalkativeness slider at " + talk + "%: answer in " + shape + ", under " + words + " words. This replaces the reply length above.";
        }
    }
}
