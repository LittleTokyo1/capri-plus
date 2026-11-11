using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int CallLevelFresh = 300;
        private CallWindow callWindow;
        private CallPill callPill;
        private bool callCaptions = true;
        private DateTime callBegan = DateTime.MinValue;
        private DateTime callEndedAt = DateTime.MinValue;
        private double callDiscordLevel;
        private int callDiscordAt;

        private void CallLevel(short[] samples)
        {
            callDiscordLevel = Speaker.Loudness(samples, 0, samples.Length);
            callDiscordAt = Environment.TickCount;
        }

        private void CallPanelTick()
        {
            if (testing) return;
            DateTime now = DateTime.Now;
            if (ForkSettings.Current.CallMode)
            {
                if (callBegan == DateTime.MinValue) callBegan = now;
                if (callWindow == null) MakeCallWindow();
                if (callEndedAt != DateTime.MinValue) { callEndedAt = DateTime.MinValue; callWindow.Begin(); }
                if (!callWindow.Visible && (callPill == null || !callPill.Visible)) callWindow.Show();
                return;
            }
            if (callWindow == null) return;
            if (callBegan != DateTime.MinValue)
            {
                callWindow.End(now - callBegan);
                callBegan = DateTime.MinValue;
                callEndedAt = now;
                CallFriends.Deaf = false;
                if (callPill != null) callPill.Hide();
                if (!callWindow.Visible) callWindow.Show();
            }
            if (callEndedAt != DateTime.MinValue && (now - callEndedAt).TotalSeconds >= CallLook.EndedSeconds)
            {
                callEndedAt = DateTime.MinValue;
                callWindow.Hide();
            }
        }

        private void MakeCallWindow()
        {
            callWindow = new CallWindow(CallViewNow);
            callPill = new CallPill(CallViewNow);
            Rectangle work = Screen.FromControl(this).WorkingArea;
            callWindow.Location = new Point(work.Right - callWindow.Width - 40, work.Top + 80);
            callWindow.Pressed += CallKey;
            callPill.Opened += delegate { callPill.Hide(); callWindow.Show(); };
        }

        private void CallKey(int key)
        {
            if (key == CallWindow.Mic) ToggleMic();
            else if (key == CallWindow.Deafen)
            {
                CallFriends.Deaf = !CallFriends.Deaf;
                ActivityLog.Write("call mode", "deafen", CallFriends.Deaf ? "not hearing Discord" : "hearing Discord again");
            }
            else if (key == CallWindow.CaptionKey) callCaptions = !callCaptions;
            else if (key == CallWindow.HangUp)
            {
                ForkSettings.Current.CallMode = false;
                ForkSettings.Save();
                ActivityLog.Write("call mode", "hung up on the call panel", "off");
                CallPanelTick();
            }
            else if (key == CallWindow.Shrink)
            {
                callWindow.Hide();
                callPill.Location = new Point(callWindow.Right - callPill.Width, callWindow.Top);
                callPill.Show();
            }
            if (callWindow != null) callWindow.Invalidate();
        }

        private CallView CallViewNow()
        {
            DateTime now = DateTime.Now;
            string call = CallFriends.Transcript();
            bool fresh = Environment.TickCount - callDiscordAt < CallLevelFresh;
            return new CallView
            {
                You = Math.Max(0, MicNow()) / 1000,
                Muted = ears != null && ears.Muted,
                Capri = clock.Elapsed.TotalSeconds < voiceUntil ? Math.Max(CallLook.Quiet * 3, speaker.Level()) : 0,
                Discord = CallFriends.Deaf || !fresh ? 0 : callDiscordLevel,
                Deaf = CallFriends.Deaf,
                Live = CallFriends.Live(now),
                Captions = callCaptions,
                Length = callBegan == DateTime.MinValue ? TimeSpan.Zero : now - callBegan,
                Lines = call.Length == 0 ? new string[0] : call.Split('\n')
            };
        }
    }
}
