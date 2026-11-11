using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class StreamCohost
    {
        internal const string Spec = "Start or stop your stream. On stream you play a game yourself, live for their stream, and talk to their chat between plays, until they stop you. Start it whenever they ask, suggest or hint that you stream, go live, play for their chat or viewers, or entertain their chat, in any words, like stream for me, you should stream, wanna go live, or chat wants to see you play. name is the game to play if they named one; leave it out to play the game that's open. stop ends your stream. Never during their own game.";
        internal const string Schema = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"start or stop\",\"enum\":[\"start\",\"stop\"]},\"name\":{\"type\":\"string\",\"description\":\"The game to play, if they named one\"}},\"required\":[\"action\"]}";
        internal const string Off = "Stream co-host is off in Capri+.";
        internal static System.Action<string> Stop = Computer.Interrupt;

        internal static bool On()
        {
            ForkSettings current = ForkSettings.Current;
            return current == null || current.StreamCohost;
        }

        internal static bool Valid(ToolArguments args)
        {
            return args == null || args.Action == null || args.Action == "start" || args.Action == "stop";
        }

        internal static string Do(ToolArguments args)
        {
            if (args == null) args = new ToolArguments();
            if (!Valid(args)) return "Couldn't find that stream action.";
            if (args.Action == "stop")
            {
                bool was = Hands.Streaming;
                Hands.Streaming = Computer.StreamingAlways;
                if (was) Stop("You asked me to stop streaming.");
                return was ? "Stopped streaming." : "I'm not streaming right now.";
            }
            if (!On()) return Off;
            string game = string.IsNullOrWhiteSpace(args.Name) ? null : args.Name.Trim();
            Hands.Streaming = true;
            return Hands.Work(game == null ? "Stream the game that's open: play it yourself, live for their stream, and talk to their chat."
                : "Stream " + game + ": open it if it isn't open, then play it yourself, live for their stream, and talk to their chat.", args.Asked);
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox streamCohost;

        private void StreamCohostPage(ForkSettings current)
        {
            Section("STREAM CO-HOST");
            streamCohost = Check("Stream co-host: when I ask her to stream or say I'm live with a game, she plays it herself and talks to the stream's chat (TikTok LIVE Studio's chat panel), 40 seconds of each minute on the game and 20 for chat; Esc or \"stop streaming\" ends it, never in my own game", current.StreamCohost);
        }

        private void FillStreamCohost()
        {
            settings.StreamCohost = streamCohost.Checked;
        }
    }
}
