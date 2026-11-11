using System;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int MeaningCheckSeconds = 60;
        private const int MeaningBatch = 300;
        private const int MeaningFetchHours = 24;
        private const int MeaningRetryHours = 1;
        private DateTime meaningChecked = DateTime.MinValue;
        private DateTime meaningFetched = DateTime.MinValue;
        private volatile bool meaningBusy;

        private void ForkMeaningTick()
        {
            if (testing || meaningBusy) return;
            DateTime now = DateTime.Now;
            if ((now - meaningChecked).TotalSeconds < MeaningCheckSeconds) return;
            meaningChecked = now;
            Meaning.On = ForkSettings.Current.MeaningOn;
            bool game = Lockout.Game != null;
            Meaning.Calm(now, game || !Meaning.On);
            if (!Meaning.On || game) return;
            string root = Vault.Root;
            bool busy = speech != null || talkState != TalkState.Off || typer.Visible || dragging || (chatting && !ChatIdle()) || job != null || working != null;
            if (root == null || busy) return;
            if (!Meaning.Downloaded)
            {
                if ((now - meaningFetched).TotalHours < MeaningFetchHours || !Meaning.HasRuntime) return;
                meaningFetched = now;
                MeaningWork(delegate
                {
                    ActivityLog.Write("meaning", "download", "bge-base-en-v1.5, about 440 MB");
                    try { Meaning.Fetch(delegate { return quitting || Lockout.Game != null; }); }
                    catch (Exception) { meaningFetched = DateTime.Now.AddHours(MeaningRetryHours - MeaningFetchHours); throw; }
                    ActivityLog.Write("meaning", "download", "done");
                });
                return;
            }
            if (!Meaning.Ready) return;
            MeaningWork(delegate
            {
                int made = Meaning.Fill(Lookback.Meant(root), MeaningBatch, delegate { return quitting || Lockout.Game != null; });
                if (made > 0) ActivityLog.Write("meaning", "vectors made", made + ", " + Meaning.Stored + " kept");
            });
        }

        private void MeaningWork(Action work)
        {
            meaningBusy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { work(); }
                catch (Exception error) { ActivityLog.Write("meaning", "background work failed", error.Message); }
                finally { meaningBusy = false; }
            });
        }
    }
}
