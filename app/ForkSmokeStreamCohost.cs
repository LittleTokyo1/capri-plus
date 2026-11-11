using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeStreamCohost(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.StreamCohost) throw new Exception("Stream co-host should be on by default.");
            string close;
            Tunable found = Tuning.Find("stream co-host", "off", out close);
            if (found == null || found.Field != "StreamCohost") throw new Exception("Her setting tool can't find stream co-host by voice.");
            if (Computer.StreamNote.IndexOf("ko-fi", StringComparison.OrdinalIgnoreCase) >= 0 || Computer.StreamNote.IndexOf("40 seconds locked in", StringComparison.Ordinal) < 0 || Computer.StreamNote.IndexOf("with learn", StringComparison.Ordinal) < 0) throw new Exception("Her stream note is not the released one without Ko-fi.");

            ForkSettings was = ForkSettings.Current;
            bool streamingWas = Hands.Streaming;
            Func<string, string, string> workWas = Hands.Work;
            Action<string> stopWas = StreamCohost.Stop;
            Func<string, bool> runningWas = Lockout.Running;
            try
            {
                ForkSettings.Current = new ForkSettings();
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                foreach (string ask in new[] { "Hey Capri, play Minecraft with streamer mode and talk to chat", "play minecraft on my stream", "Enter Gaming Mode and use Computer Use to play Bloons TD 6. I'm also live streaming so occasionally talk to chat for me", "play terraria, I'm live", "don't stop playing and talk to chat", "can you read my tiktok chat" })
                    if (!Hands.StreamAsked(ask)) throw new Exception("A stream asked for in words was missed: " + ask);
                foreach (string not in new[] { "play minecraft", "I'm streaming tonight", "don't talk to chat", "stop talking to chat", "how does streamer mode work", "turn off streaming mode", "we're live in voice, lol", "open spotify" })
                    if (Hands.StreamAsked(not)) throw new Exception("Talk was taken as a stream ask: " + not);

                Hands.StreamMinute beat = new Hands.StreamMinute();
                if (!(!beat.Due(39) && !beat.Open(39) && beat.Due(41) && beat.Open(50) && !beat.Due(50) && !beat.Open(62) && !beat.Due(100) && beat.Due(130) && beat.Open(140) && !beat.Due(189) && beat.Due(190))) throw new Exception("Her stream minute isn't 40 seconds on the game and 20 for chat.");

                Hands.Streaming = true;
                List<string> talk = new List<string>();
                Job streamJob = new Job("play Terraria");
                streamJob.Says = talk.Add;
                string shut = streamJob.Spoken("Not my chat time yet.", 5);
                string shown = streamJob.ChatWords(41);
                streamJob.Spoken(" Hi Milo, thanks for the follow! ", 42);
                streamJob.Spoken("And right after it, another line for chat.", 43);
                if (shut != Computer.LockedIn || talk.Count != 2 || talk[0] != "Hi Milo, thanks for the follow!") throw new Exception("Her stream lines weren't held to her chat time: " + string.Join(" | ", talk.ToArray()));
                if (shown.IndexOf("out loud with say", StringComparison.Ordinal) < 0 || streamJob.ChatWords(50).Length != 0 || streamJob.Spoken("Too late for chat.", 70) != Computer.LockedIn || talk.Count != 2 || streamJob.ChatWords(101).Length == 0) throw new Exception("Her chat time wasn't shown once a minute to every brain.");
                if (streamJob.Prompted("rules").IndexOf(Computer.StreamNote, StringComparison.Ordinal) < 0) throw new Exception("Her stream note didn't reach her hands' prompt.");
                Hands.Streaming = false;
                if (streamJob.Prompted("rules").IndexOf(Computer.StreamNote, StringComparison.Ordinal) >= 0 || streamJob.ChatWords(500).Length != 0) throw new Exception("Her stream note stayed after the stream.");

                string task = null, asked = null;
                int stops = 0;
                Hands.Work = delegate(string job, string words) { task = job; asked = words; return Hands.Streaming ? "Streaming." : "Not streaming."; };
                StreamCohost.Stop = delegate { stops++; };
                if (Hands.Do("stream", new ToolArguments { Action = "start", Name = "Minecraft", Asked = "you should stream minecraft" }) != "Streaming." || task == null || task.IndexOf("Minecraft", StringComparison.Ordinal) < 0 || asked != "you should stream minecraft")
                    throw new Exception("Her stream tool didn't start a stream of the game he named: " + task);
                if (Hands.Do("stream", new ToolArguments { Action = "stop" }) != "Stopped streaming." || Hands.Streaming || stops != 1 || Hands.Do("stream", new ToolArguments { Action = "stop" }) != "I'm not streaming right now." || stops != 1)
                    throw new Exception("Her stream tool didn't stop the stream once.");
                if (!Lockout.Handles("stream")) throw new Exception("Her stream tool isn't under the game lockout.");
                Lockout.Running = delegate(string name) { return name == "League of Legends"; };
                Lockout.Recheck();
                task = null;
                if (Hands.Do("stream", new ToolArguments { Action = "start", Name = "Minecraft" }) != Lockout.Refusal || task != null) throw new Exception("Her stream started during his game.");
                Hands.Streaming = false;
                Lockout.Running = delegate { return false; };
                Lockout.Recheck();
                if (Hands.Tools.IndexOf("\"name\":\"stream\"", StringComparison.Ordinal) < 0) throw new Exception("Her brain isn't offered the stream tool.");

                ForkSettings.Current.StreamCohost = false;
                task = null;
                if (Hands.StreamAsked("Hey Capri, play Minecraft with streamer mode and talk to chat") || Hands.Do("stream", new ToolArguments { Action = "start" }) != StreamCohost.Off || task != null || Hands.Streaming) throw new Exception("The stream co-host switch off still started a stream.");
            }
            finally
            {
                ForkSettings.Current = was;
                Hands.Streaming = streamingWas;
                Hands.Work = workWas;
                StreamCohost.Stop = stopWas;
                Lockout.Running = runningWas;
                Lockout.Recheck();
            }
            checks.Add("PASS stream co-host (096-13): on by default and found by voice, a stream asked for in any words (streamer mode, talk to chat, I'm live with a game) starts it while plain talk about streams doesn't, each minute is 40 seconds on the game and 20 for chat with her lines held to her chat time and chat shown to every brain, the stream tool starts the game he named and stops once, it sits under the game lockout, the note has no Ko-fi line, and the switch off keeps streams out");
        }
    }
}
