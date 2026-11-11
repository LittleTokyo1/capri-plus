using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeStatusMoves(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.DiscordStatus) throw new Exception("Discord status should be off by default (his 28 Oct pick: no status that points at other sites).");
            string close;
            Tunable found = Tuning.Find("discord status", "on", out close);
            if (found == null || found.Field != "DiscordStatus") throw new Exception("Her setting tool can't find Discord status by voice.");
            if (Presence.Look("Plugged in") != "plugged" || Presence.Look("Playing a game") != "game" || Presence.Look("Streaming") != "live" || Presence.Look("Using the computer") != "computer" || Presence.Look("Multiplying") != "clones" || Presence.Look("Voice chatting") != "voice" || Presence.Look("Dancing to music") != "dancing"
                || Presence.Badge("Using the computer") != "cursor" || Presence.Badge("Playing a game") != "controller" || Presence.Badge("Plugged in") != null || Presence.Header("Voice chatting") != 2 || Presence.Header("Plugged in") != 0)
                throw new Exception("Her Discord looks were picked wrong.");

            ForkSettings was = ForkSettings.Current;
            string appWas = Presence.AppId, pipeWas = Presence.PipeName;
            Func<bool> artWas = Presence.ArtLive, readyWas = StatusMoves.BotReady;
            Func<string> fetchWas = StatusMoves.FetchApp;
            bool buttonedWas = StatusMoves.Buttoned;
            string moving = null, fellBack = null, hello = null;
            try
            {
                StatusMoves.Forget();
                ForkSettings.Current = new ForkSettings();
                int fetched = 0;
                StatusMoves.BotReady = delegate { return true; };
                StatusMoves.FetchApp = delegate { fetched++; return "1555000000000000777"; };
                if (StatusMoves.Showing() || fetched != 0) throw new Exception("Her Discord status showed, or asked Discord, with the switch off.");
                ForkSettings.Current.DiscordStatus = true;
                StatusMoves.BotReady = delegate { return false; };
                if (StatusMoves.Showing()) throw new Exception("Her Discord status showed before her bot's app was known.");
                if (StatusMoves.Learn("not an id") || StatusMoves.Learn(null)) throw new Exception("A bad app id was taken.");
                if (!StatusMoves.Learn("1555000000000000777") || !StatusMoves.Showing() || Presence.AppId != "1555000000000000777" || StatusMoves.Buttoned) throw new Exception("Her Discord status didn't move to her bot's app without the other site's buttons.");

                Presence.PipeName = "capri-smoke-moves-" + Process.GetCurrentProcess().Id + "-ipc-";
                Presence.ArtLive = delegate { return true; };
                Presence status = new Presence("9.9.9");
                try
                {
                    using (NamedPipeServerStream pipe = new NamedPipeServerStream(Presence.PipeName + "0", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        IAsyncResult joined = pipe.BeginWaitForConnection(null, null);
                        status.Crowd = 14;
                        status.Show("Playing a game");
                        if (!joined.AsyncWaitHandle.WaitOne(8000)) throw new Exception("Her moving Discord status never called the pipe.");
                        pipe.EndWaitForConnection(joined);
                        byte[] head = Presence.Take(pipe, 8, 5000);
                        hello = Encoding.UTF8.GetString(Presence.Take(pipe, BitConverter.ToInt32(head, 4), 5000));
                        Presence.Send(pipe, 1, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"v\":1}}");
                        head = Presence.Take(pipe, 8, 5000);
                        moving = Encoding.UTF8.GetString(Presence.Take(pipe, BitConverter.ToInt32(head, 4), 5000));
                        Presence.Send(pipe, 1, "{\"cmd\":\"SET_ACTIVITY\",\"evt\":\"ERROR\",\"data\":{\"code\":4000,\"message\":\"No thanks\"}}");
                        head = Presence.Take(pipe, 8, 6000);
                        fellBack = Encoding.UTF8.GetString(Presence.Take(pipe, BitConverter.ToInt32(head, 4), 5000));
                        status.Show(null);
                    }
                }
                finally { status.Dispose(); }
            }
            finally
            {
                ForkSettings.Current = was;
                Presence.AppId = appWas;
                Presence.PipeName = pipeWas;
                Presence.ArtLive = artWas;
                StatusMoves.BotReady = readyWas;
                StatusMoves.FetchApp = fetchWas;
                StatusMoves.Buttoned = buttonedWas;
                StatusMoves.Forget();
            }
            if (hello == null || hello.IndexOf("\"client_id\":\"1555000000000000777\"", StringComparison.Ordinal) < 0) throw new Exception("Her moving status didn't say hello as her bot's app: " + hello);
            if (moving == null || moving.IndexOf("capri-plus\\/main\\/presence\\/game.gif", StringComparison.Ordinal) < 0 || moving.IndexOf("capri-plus\\/main\\/presence\\/controller.gif", StringComparison.Ordinal) < 0 || moving.IndexOf("capriai", StringComparison.Ordinal) >= 0 || moving.IndexOf("\"party\"", StringComparison.Ordinal) < 0 || moving.IndexOf("14", StringComparison.Ordinal) < 0 || moving.IndexOf("\"buttons\"", StringComparison.Ordinal) >= 0 || moving.IndexOf("\"details\":\"Playing a game\"", StringComparison.Ordinal) < 0) throw new Exception("Her moving Discord status line was wrong: " + moving);
            if (fellBack == null || fellBack.IndexOf("\"party\"", StringComparison.Ordinal) >= 0 || fellBack.IndexOf("\"details\":\"Playing a game\"", StringComparison.Ordinal) < 0 || fellBack.IndexOf("\"buttons\"", StringComparison.Ordinal) >= 0) throw new Exception("Her status didn't fall back to the plain line after Discord said no: " + fellBack);
            checks.Add("PASS Discord status moves (096-12): off by default and found by voice, nothing shown or asked with it off or before her bot's app is known, then shown through her own bot's app with no buttons to other sites, her artist's moving art for what she does (game with the controller badge, a clone count), and the plain line again when Discord turns the extras down");
        }
    }
}
