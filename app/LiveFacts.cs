using System;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class LiveFacts
    {
        private static readonly Regex VoiceAsked = new Regex(@"\b(?:eleven ?labs|11 ?labs|chatterbox|clon(?:e|ed) voice|local voice|free voice|your voice|her voice|which voice|what voice|voice (?:engine|model|setting)s?|tts|text to speech)\b", RegexOptions.IgnoreCase);
        private static readonly Regex PlusAsked = new Regex(@"\b(?:capri ?\+|capri ?plus|capri-plus|advanced settings|your settings|her settings|fork settings)", RegexOptions.IgnoreCase);
        internal const string PlusPlace = "\nWhere your own settings are: the Capri+ key at the right end of the CAPRI band on the Mainframe's main page opens Capri+ settings, and so does right-click on you > Capri+ settings. The Mainframe's Settings page (Advanced Settings at its top shows every row) holds the original app's settings, not Capri+.";
        internal static Func<string> Voice = delegate { return null; };

        internal static string Block(string text)
        {
            string said = text ?? "";
            string block = "";
            if (VoiceAsked.IsMatch(said))
            {
                string now = null;
                try { now = Voice(); }
                catch (Exception) { }
                if (!string.IsNullOrEmpty(now)) block += "\nWhich voice you speak with right now, read live from Capri+ and the voice settings (trust this over your notes, memory or the screen): " + now + ".";
            }
            if (PlusAsked.IsMatch(said)) block += PlusPlace;
            if (SayDo.CodeBug(said)) block += BugTalk;
            block += Study.Talk(said, DateTime.Now);
            if (LeagueKnow.IsMatch(said)) block += LeagueTalk;
            else if (said.Length > 0 && !GameTalk.IsMatch(said)) block += Background;
            return block;
        }

        internal const string BugTalk = "\nHe is reporting a bug in you: agree plainly that it's a bug in your code and that Claude Code will fix it. Don't argue it isn't a bug, don't call it a lesson, and don't try to fix it with a job on his PC.";
        internal const string LeagueTalk = "\nFor a general League question (what an item does, when to buy it, into which comps, matchups, a champion's power spikes, where a jungler usually starts and paths, which camp or scuttle he goes for, a skill order, a jungle pet), answer from your own League knowledge with a clear call on what usually happens; never send him to ask anyone, least of all the enemy. The game brief only limits what you claim happened in this match. His own build this game is the exception: when the game brief names his pro build, what he should build or buy comes from that build.";
        internal const string Background = "\nTheir game, champ select, timers and screen are background here: answer what they actually said like a friend, and don't bring the game or the screen up unless they ask about it or it's urgent.";
        private static readonly Regex LeagueKnow = new Regex(@"\b(?:item|items|build|builds|buy|rush|first item|rune|runes|pet|pets|jungle companion|smite|clear|clears|clearing|path|pathing|route|scuttle|scuttles|red buff|blue buff|camps?|gank|ganks|skill order|level up|usually|matchup|counter|power ?spike|hubris|lethality|tanky|tankier|squish(?:y|ier)|comps?)\b", RegexOptions.IgnoreCase);
        private static readonly Regex GameTalk = new Regex(@"\b(?:game|match|league|champ|champion|lane|jungle|mid|top|bot|support|enemy|team|kill|kills|died|death|flash(?:ed|es)?|dragon|baron|cs|farm|gold|timer|loadout|pick|ban|lock|screen|see|look|watch|this)\b|^\(", RegexOptions.IgnoreCase);

        internal static string Describe(int engine, bool silent, bool cloned, bool heavy, bool quiet, bool server, string cloud, string fallback)
        {
            if (silent) return "none, her voice is off (Silent), so replies are text only";
            if (engine == 0 && cloned && heavy) return "her cloned voice is picked but resting while a heavy game runs, so " + fallback + " speaks for now";
            if (engine == 0 && cloned) return "her cloned voice (Chatterbox, local on the graphics card), with " + fallback + " as the fallback";
            if (engine == 0) return "her cloned voice is picked in Capri+ but isn't running, so " + fallback + " speaks";
            if (engine == 2 || quiet) return "her free voice (the small local one)";
            if (server) return "their own voice model server";
            return (string.IsNullOrEmpty(cloud) ? "ElevenLabs" : cloud) + " (cloud), not the local voice on this PC";
        }
    }

    internal sealed partial class Companion
    {
        private void SetUpLiveFacts()
        {
            LiveFacts.Voice = delegate { return LiveFacts.Describe(ForkSettings.Current.VoiceEngine, preferences.Silent, ClonedVoiceOn(), HeavyNow(), preferences.Quiet, preferences.ServerVoice, preferences.CloudVoice, FallbackName); };
        }

        internal static string KeyOwner(ForkSettings settings, int mods, int vk)
        {
            int[] keys = { settings.PauseMods, settings.PauseVk, settings.LookMods, settings.LookVk, settings.StopMods, settings.StopVk, settings.HoldMods, settings.HoldVk };
            string[] names = { "Capri+ pause", "Capri+ look", "Capri+ stop", "Capri+ hold-to-talk" };
            for (int i = 0; i < names.Length; i++)
                if (vk != 0 && keys[i * 2 + 1] == vk && (keys[i * 2] & 0xF) == (mods & 0xF)) return names[i];
            return null;
        }
    }
}
