using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int ShotAfterSettleMs = 1500;
        private const int ShotAfterJobWaitMs = 300000;
        private const int ShotAfterKeepMinutes = 10;

        // "open capri+ settings first then send me a screenshot": the job, a joining word, then a line that only asks for the picture.
        private static readonly Regex ShotThen = new Regex(@"^(?<first>.+?)(?:\s*,\s*|\s+)(?:and\s+)?(?:then\s+|after\s+that\s+|afterwards\s+)?(?<shot>(?:send|give|show|take|get)\b.*)$", RegexOptions.IgnoreCase);
        private static readonly Regex FirstWord = new Regex(@"^\s*first(?:\s+of\s+all)?\s*,?\s*|\s*,?\s*\bfirst\s*$", RegexOptions.IgnoreCase);

        private string shotAfterWhere;
        private string shotAfterLine;
        private DateTime shotAfterAt = DateTime.MinValue;

        internal static bool ShotOnly(string line)
        {
            return Discord.OnlyShot(ShotToPhone.Replace(line ?? "", " ").Trim(), false);
        }

        // The job before a screenshot for his Discord, or null when the line is not a job and then only a screenshot.
        internal static string ShotAfterFirst(string line, out string shot)
        {
            shot = null;
            string said = (line ?? "").Trim();
            if (!Discord.WantsShot(said) || !ShotToPhone.IsMatch(said) || ShotOnly(said)) return null;
            Match split = ShotThen.Match(said);
            if (!split.Success) return null;
            string rest = split.Groups["shot"].Value.Trim();
            string first = FirstWord.Replace(split.Groups["first"].Value, "").Trim().TrimEnd(',', '.', ' ');
            if (first.Length == 0 || !Discord.WantsShot(rest) || !ShotToPhone.IsMatch(rest) || !ShotOnly(rest) || Discord.WantsShot(first)) return null;
            shot = rest;
            return first;
        }

        private const int ShotToolSettleMs = 800;
        private const int PhoneVoiceWarmMs = 40000;

        // In front of his other windows even when she is not the app in front: Windows ignores Activate then, but a window
        // that goes topmost and back stays on top of the rest.
        private static void BringForward(System.Windows.Forms.Form form)
        {
            if (form.WindowState == System.Windows.Forms.FormWindowState.Minimized) form.WindowState = System.Windows.Forms.FormWindowState.Normal;
            form.Show();
            form.TopMost = true;
            form.TopMost = false;
            form.BringToFront();
            form.Activate();
        }

        // Asleep because he is away, his own screenshot asks still work; his pause key keeps her eyes off.
        internal static string ShotRefused(bool paused, bool away) { return paused && !away ? "I'm paused, so my eyes are off. Unpause me and ask again." : null; }

        // Her discord tool's screenshot: she takes his screen and sends it to his own DM, after whatever she just did.
        private string DiscordShotTool(string monitor)
        {
            if (string.IsNullOrEmpty(ForkSettings.Current.DiscordChannel)) return "My Discord isn't connected yet, so I can't send a screenshot there. Say connect my Discord.";
            string pausedSay = ShotRefused(ForkSettings.Current.Paused, ForkSettings.Current.HomeAway);
            if (pausedSay != null) return pausedSay;
            string where = ShotPlace(string.IsNullOrWhiteSpace(monitor) ? null : Screens.Wording("screenshot of my " + monitor) ?? monitor);
            Thread.Sleep(ShotToolSettleMs);
            byte[] picture = null;
            bool refused = false;
            string line = string.IsNullOrWhiteSpace(monitor) ? "" : "send me a screenshot of my " + monitor;
            try { Invoke(new Action(delegate { refused = Blocklisted(); if (!refused) picture = DiscordShot(line, ""); })); }
            catch (InvalidOperationException) { return "I couldn't take the screenshot just now."; }
            if (refused)
            {
                ActivityLog.Write("discord", "screenshot refused", "a never-look app is in front");
                return "Not sending that: an app on your never-look list is in front.";
            }
            bool sent = false;
            try { sent = picture != null && Discord.SendFile(picture, "screen.jpg", "Here's " + where + "."); }
            catch (Exception error) { Brain.Log("discord", -1, error.GetType().Name + ": " + error.Message); }
            ActivityLog.Write("discord", "screenshot to him", where + (sent ? " sent (her tool)" : " not sent (her tool)"));
            return sent ? "Sent a screenshot of " + where + " to his Discord." : "Couldn't send the screenshot to his Discord just now.";
        }

        // A phone answer wakes her cloned voice and waits for it, instead of going silent while it loads.
        private void PhoneVoiceAwake()
        {
            if (testing || !ClonedVoiceOn() || HeavyNow() || chatterbox.State == Chatterbox.Phase.Ready) return;
            chatterbox.Warm();
            Stopwatch waited = Stopwatch.StartNew();
            while (chatterbox.State != Chatterbox.Phase.Ready && waited.ElapsedMilliseconds < PhoneVoiceWarmMs) Thread.Sleep(250);
            ActivityLog.Write("phone", "voice", chatterbox.State == Chatterbox.Phase.Ready ? "her cloned voice woke in " + (waited.ElapsedMilliseconds / 1000) + " s" : "her cloned voice still loading, a backup voice speaks");
        }

        // Her brain hears only the job; the screenshot waits for her answer.
        private void ShotAfterSplit(ref string text)
        {
            string shot;
            string first = ShotAfterFirst(text, out shot);
            if (first == null) return;
            shotAfterWhere = ShotPlace(Screens.Wording(shot));
            shotAfterLine = shot;
            shotAfterAt = DateTime.Now;
            ActivityLog.Write("discord", "screenshot after the job", Pursuit.Shorter(first, 80) + "  ->  " + shotAfterWhere);
            text = first;
        }

        // Once she answers the job (and any job she started has finished), the screen goes to his Discord.
        private void ShotAfterAnswer()
        {
            string where = shotAfterWhere, line = shotAfterLine;
            if (where == null || testing) return;
            shotAfterWhere = null;
            if ((DateTime.Now - shotAfterAt).TotalMinutes > ShotAfterKeepMinutes) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(ShotAfterSettleMs);
                Stopwatch waited = Stopwatch.StartNew();
                while (working != null && waited.ElapsedMilliseconds < ShotAfterJobWaitMs) Thread.Sleep(500);
                byte[] picture = null;
                bool refused = false;
                try { Invoke(new Action(delegate { refused = Blocklisted(); if (!refused) picture = DiscordShot(line, ""); })); }
                catch (InvalidOperationException) { return; }
                bool sent = false;
                if (!refused && picture != null && !string.IsNullOrEmpty(ForkSettings.Current.DiscordChannel))
                {
                    try { sent = Discord.SendFile(picture, "screen.jpg", "Here's " + where + "."); }
                    catch (Exception error) { Brain.Log("discord", -1, error.GetType().Name + ": " + error.Message); }
                }
                ActivityLog.Write("discord", "screenshot after the job", where + (refused ? " refused, a never-look app is in front" : sent ? " sent" : " not sent"));
                if (!sent) Post(delegate { Tell("(discord)", refused ? "Not sending that screenshot: an app on your never-look list is in front." : "I couldn't send the screenshot to your Discord just now."); });
            });
        }
    }
}
