namespace CapriDesktop
{
    internal static class ForkUpstreamSettings
    {
        internal static readonly string[] Ours =
        {
            "X", "Y", "HasPosition", "Size", "OnTop", "UpdateCheckedAt", "Model", "Welcomed", "NoSitting", "NvidiaGlm", "VoiceKey", "VoiceId", "VoiceModel",
            "AgentId", "AgentVersion", "Mic", "TextKey", "TextModel", "NvidiaKey", "NvidiaModel", "PaidKey", "PaidModel", "Tier", "Tiered", "Watch", "GlanceGap",
            "Quiet", "Silent", "Wander", "FreeVoice", "NoDance", "Nickname", "Wordiness", "Recall", "Volume", "Sounds", "SleepAfter", "NoIntro", "NoDiscord",
            "Typing", "BubbleTime", "TalkMods", "TalkVk", "MuteMods", "MuteVk", "TypeMods", "TypeVk", "HideMods", "HideVk", "WanderPace", "AgentPolyglot",
            "AgentPersonality", "CheckIns", "VoiceStyle", "StartupSet", "QuickHands", "Bypass"
        };

        internal static readonly string[] Routed =
        {
            "Met", "CaughtUp", "Remembers", "Language", "Permissions", "HeyCapri", "QuietMods", "QuietVk", "StayOnModel", "Vault",
            "ClipFolder", "ClipSeconds", "ClipFps", "ClipResolution", "ClipMbps", "Hologram", "NoReactions"
        };

        internal static readonly string[] Extras =
        {
            "PopPace", "PerchFps", "MainframeSize", "NoPlayer", "PlayerX", "PlayerY", "HasPlayerSpot", "VoiceTuned", "HdVoice", "HdGameRest", "VoicePitch",
            "VoiceSpeed", "VoiceRobot", "VoiceGlitch", "HdRobot", "HdVoiceGlitch", "VoiceLayers", "LayerPitch1", "LayerDelay1", "LayerPitch2", "LayerDelay2",
            "ServerVoice", "ServerUrl", "ServerApi", "ServerModel", "ServerSpeaker", "ServerKey", "ServerRobot", "ServerGlitch", "AsideUrl", "AsideApi",
            "AsideModel", "AsideSpeaker", "AsideKey", "CloudVoice", "LocalAddress", "BrainPort", "BrainAddress", "LocalModel", "FollowUps", "WatchMemory", "NoKeepAwake",
            "HideFullScreen", "PlayerFullScreen", "ExploreAway", "AdvancedSettings", "ExploreAfter", "FreeChatTold", "ListenCapri", "NoGaming", "ListenCapriTold",
            "NoHeadPats", "MemorySize", "ReplyLength", "ClipGames", "ClipMoments", "SlowHands", "PushMods", "PushVk", "CommentPace", "GameMods", "GameVk", "GameSpots",
            "CareWater", "CareWaterMinutes", "CareStretch", "CareStretchMinutes", "CareEyes", "CareEyesMinutes", "CareLate", "CareLateHour", "CareMeal",
            "CareMealMinutes", "CareGame", "CareGameMinutes", "CareBored", "CareBoredMinutes", "CareWritten", "CareNight", "HdCpu", "VoiceRetuned"
        };

        internal static string Unrouted()
        {
            System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<string> seen = new System.Collections.Generic.List<string>();
            foreach (System.Reflection.FieldInfo field in typeof(Preferences).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                if (field.GetCustomAttributes(typeof(System.Runtime.Serialization.DataMemberAttribute), false).Length == 0) continue;
                seen.Add(field.Name);
                if (System.Array.IndexOf(Ours, field.Name) < 0 && System.Array.IndexOf(Routed, field.Name) < 0 && System.Array.IndexOf(Extras, field.Name) < 0) missing.Add(field.Name);
            }
            foreach (string[] list in new[] { Ours, Routed, Extras })
                foreach (string name in list) if (!seen.Contains(name)) missing.Add(name + " (gone upstream)");
            return missing.Count == 0 ? null : string.Join(", ", missing.ToArray());
        }
    }
}
