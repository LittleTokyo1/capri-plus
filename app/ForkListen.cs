using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal const int ListenPassMinutes = 10;
        internal const int ListenLeastLines = 6;
        internal const int ListenQueueMost = 30;
        internal const int ListenCheckSeconds = 30;

        private sealed class ListenPiece
        {
            internal string Who;
            internal short[] Samples;
            internal string Heard;
        }

        private readonly object listenGate = new object();
        private readonly Queue<ListenPiece> listenQueue = new Queue<ListenPiece>();
        private readonly ListenSegments listenCut = new ListenSegments();
        private volatile bool listenOn;
        private bool listenWorking;
        private bool listenInstalling;
        private LocalEars listenEars;
        private LoopbackCapture listenLoop;
        private string listenProblem;
        private DateTime listenPassAt;
        private DateTime listenCheckAt;

        private void ListenTick()
        {
            if (testing) return;
            FriendModeTick();
            bool wanted = ForkSettings.Current.ListenMode || FriendMode.Active || ForkSettings.Current.CallMode;
            if (wanted && !listenOn) ListenStart();
            else if (!wanted && listenOn) ListenStop();
            if (!listenOn) return;
            DateTime now = DateTime.Now;
            if ((now - listenCheckAt).TotalSeconds >= ListenCheckSeconds) { listenCheckAt = now; ListenKeepLoop(); }
            if ((now - listenPassAt).TotalMinutes >= ListenPassMinutes) ListenLearn(now, ListenLeastLines);
        }

        private void ListenStart()
        {
            listenOn = true;
            listenPassAt = DateTime.Now;
            ForkSettings settings = ForkSettings.Current;
            if (settings.ListenCapriWas == 0) { settings.ListenCapriWas = preferences.ListenCapri ? 2 : 1; ForkSettings.Save(); }
            if (!preferences.ListenCapri) ToggleListenCapri();
            if (listenEars == null) listenEars = ListenEars.Make(Path.GetDirectoryName(settingsPath));
            if (!listenEars.Ready) ListenInstall();
            ListenKeepLoop();
            ActivityLog.Write("listening", "on", listenEars.Ready ? "ears ready" : "ears downloading");
        }

        private void ListenStop()
        {
            listenOn = false;
            if (listenLoop != null) { listenLoop.Dispose(); listenLoop = null; }
            lock (listenGate) listenQueue.Clear();
            ListenTraceWrite();
            ListenLearn(DateTime.Now, 1);
            ForkSettings settings = ForkSettings.Current;
            if (settings.ListenCapriWas == 1 && preferences.ListenCapri)
            {
                preferences.ListenCapri = false;
                nameOver = false;
                SavePreferences();
                mainframe.Sync();
                pill.ListenForName = false;
            }
            settings.ListenCapriWas = 0;
            ForkSettings.Save();
            LocalEars done = listenEars;
            listenEars = null;
            if (done != null) ThreadPool.QueueUserWorkItem(delegate { done.Dispose(); });
            ActivityLog.Write("listening", "off", null);
        }

        private void ListenInstall()
        {
            if (listenInstalling) return;
            listenInstalling = true;
            LocalEars installing = listenEars;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string said;
                try { installing.Install(delegate(string what, long got, long total) { }); said = "ready"; }
                catch (Exception error) { said = "failed: " + Voice.Short(error.Message); }
                Post(delegate { listenInstalling = false; ActivityLog.Write("listening", "ears download", said); });
            });
        }

        private void ListenKeepLoop()
        {
            int discord = LoopbackCapture.DiscordRoot();
            LoopbackCapture was = listenLoop;
            if (was != null && was.Running && was.Process == discord) return;
            if (was != null)
            {
                if (was.Problem != null && was.Problem != listenProblem) { listenProblem = was.Problem; ActivityLog.Write("listening", "discord sound", was.Problem); }
                was.Dispose();
                listenLoop = null;
            }
            if (discord == 0) return;
            listenLoop = new LoopbackCapture(discord, ListenDiscordSound);
            listenLoop.Start();
        }

        private void ListenDiscordSound(short[] samples)
        {
            if (!listenOn) return;
            CallOverHeard(samples);
            List<short[]> said;
            lock (listenCut) said = listenCut.Feed(samples);
            foreach (short[] piece in said) ListenQueue(new ListenPiece { Who = ListenBook.Friend, Samples = piece });
        }

        private void ListenMine(short[] samples, string heard)
        {
            if (!listenOn || pushing || Hands.HeardName(heard ?? "")) return;
            ListenQueue(new ListenPiece { Who = ListenBook.Him, Samples = samples, Heard = heard });
        }

        private void ListenQueue(ListenPiece piece)
        {
            lock (listenGate)
            {
                if (listenQueue.Count >= ListenQueueMost) listenQueue.Dequeue();
                listenQueue.Enqueue(piece);
                if (listenWorking) return;
                listenWorking = true;
            }
            ThreadPool.QueueUserWorkItem(delegate { ListenWork(); });
        }

        private void ListenWork()
        {
            ThreadPriority before = Thread.CurrentThread.Priority;
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            try
            {
                while (true)
                {
                    ListenPiece piece;
                    lock (listenGate)
                    {
                        if (listenQueue.Count == 0 || !listenOn) { listenQueue.Clear(); listenWorking = false; return; }
                        piece = listenQueue.Dequeue();
                    }
                    string text = piece.Heard;
                    LocalEars ears = listenEars;
                    if (ears != null && ears.Ready && listenOn)
                    {
                        try { text = Hearing.Clean(ears.Hear(piece.Samples)); }
                        catch (Exception error) { Brain.Log("listening", -1, Voice.Short(error.Message)); }
                    }
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    ListenBook.Add(DateTime.Now, piece.Who, text);
                    CallHeard(piece.Who, text);
                }
            }
            finally { Thread.CurrentThread.Priority = before; }
        }

        private static void ListenTraceWrite()
        {
            string mic = ListenTrace.Line();
            if (mic != null) ActivityLog.Write("listening", "his mic", mic);
        }

        private void ListenLearn(DateTime now, int least)
        {
            if (ListenBook.Count < least) return;
            ListenTraceWrite();
            listenPassAt = now;
            List<ListenLine> taken = ListenBook.Take();
            if (taken.Count == 0) return;
            string transcript = ListenBook.Transcript(taken, DiscordPresence.InCallWithHim(ForkSettings.Current.DiscordOwner));
            Cloud remote = brain.Remote;
            ActivityLog.Write("listening", "heard", taken.Count + " lines");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { PeopleNotes.Overheard(remote, transcript, now); }
                catch (Exception error) { Brain.Log("listening", -1, Voice.Short(error.Message)); }
            });
        }
    }
}
