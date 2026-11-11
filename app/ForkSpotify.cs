using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapriDesktop
{
    internal static class SpotifyHeart
    {
        internal static Func<string> Title = PlayingTitle;
        internal static Func<bool?> Peek = PeekSpotify;
        private const int PeekMs = 600;
        private static Func<bool?> stock;
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);

        internal static void Install()
        {
            Func<bool?> read = Tunes.Hearted;
            Func<Tune, string, bool> send = Tunes.Send;
            stock = read;
            Tunes.Hearted = delegate { return Read(read); };
            Tunes.Send = delegate(Tune tune, string what) { return Send(read, send, tune, what); };
        }

        private static bool? Read(Func<bool?> read)
        {
            if (SpotifyLibrary.Connected)
            {
                bool? has = SpotifyLibrary.Liked(Title());
                if (has.HasValue) return has;
            }
            return read();
        }

        private static bool Send(Func<bool?> read, Func<Tune, string, bool> send, Tune tune, string what)
        {
            if (what != "like" && what != "unlike") return send(tune, what);
            bool want = what == "like";
            if (SpotifyLibrary.Connected)
            {
                bool? done = SpotifyLibrary.Set(tune != null && !string.IsNullOrEmpty(tune.Title) ? tune.Title : Title(), want);
                if (done == true) return true;
            }
            bool? has = read();
            if (!has.HasValue)
            {
                if (Lockout.Active)
                {
                    ActivityLog.Write("spotify heart", what, "a game is running, so Spotify stays where it is and nothing was pressed");
                    return false;
                }
                has = Peek();
            }
            if (!has.HasValue)
            {
                ActivityLog.Write("spotify heart", what, "could not tell if the song is liked, so nothing was pressed");
                return false;
            }
            if (has.Value == want) return true;
            return send(tune, what);
        }

        private static string PlayingTitle()
        {
            Tune tune = Tunes.Read();
            return tune != null && tune.Spotify ? tune.Title : null;
        }

        private static bool? PeekSpotify()
        {
            Func<bool?> read = stock;
            if (read == null) return null;
            IntPtr window = IntPtr.Zero;
            foreach (OpenWindow open in Hands.Windows())
                if (string.Equals(open.Program, "Spotify", StringComparison.OrdinalIgnoreCase) && open.Handle != IntPtr.Zero) { window = open.Handle; break; }
            if (window == IntPtr.Zero) return null;
            IntPtr was = Native.GetForegroundWindow();
            bool tucked = IsIconic(window);
            if (!Tunes.Bring(window)) return null;
            Thread.Sleep(PeekMs);
            bool? has = null;
            try { has = read(); }
            catch (Exception) { }
            if (tucked) ShowWindow(window, 7);
            if (was != IntPtr.Zero && was != window) Tunes.Bring(was);
            ActivityLog.Write("spotify heart", "looked at Spotify's own heart", has.HasValue ? (has.Value ? "liked" : "not liked") : "still could not tell");
            return has;
        }
    }

    internal static class ForkDirect
    {
        internal static bool Heard(string text, out string tool, out ToolArguments args)
        {
            if (SpotifyLibrary.Asks.IsMatch(text ?? ""))
            {
                tool = "spotify";
                args = new ToolArguments { Action = "sign_in" };
                return true;
            }
            string liking = SpotifyLibrary.LikeAsked(text);
            if (liking != null)
            {
                tool = "spotify";
                args = new ToolArguments { Action = liking };
                return true;
            }
            return Mixer.Heard(text, out tool, out args);
        }
    }
}
