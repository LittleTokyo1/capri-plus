using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class CloneHook
    {
        internal const string Off = "My clones are switched off in Capri+ (CLONES AND CHAOS), choom.";
        internal const string InGame = "Not while you're in a game, choom. My clones wait until it's over.";
        internal static Func<string, int, string> Run = delegate { return "Couldn't do that just now."; };

        internal static string Refusal(ForkSettings settings, bool locked, bool game)
        {
            if (settings == null || !settings.Clones) return Off;
            if (locked || game) return InGame;
            return null;
        }
    }

    internal sealed partial class Companion
    {
        private readonly Queue<Action> ownLines = new Queue<Action>();

        private bool LineBusy
        {
            get { return speaker.Busy || chatTail.Enabled || clock.Elapsed.TotalSeconds < voiceUntil || pushing || transcribing || talkState == TalkState.Thinking || talkState == TalkState.Talking; }
        }

        private bool cloneGame;

        private string CloneRefusal() { return CloneHook.Refusal(ForkSettings.Current, Lockout.Active, Computer.GameOpen() != null); }

        private string CloneRefusalSeen() { return CloneHook.Refusal(ForkSettings.Current, cloneGame, false); }

        private void ClonesStart(string data)
        {
            CloneHook.Run = delegate(string action, int count)
            {
                cloneCalm = false;
                CloneWindow open = cloneWindow;
                if (open != null) open.Swarm.Quiet = false;
                return CloneTool(action, count);
            };
            if (testing) return;
            CloneWindow.Marker = Path.Combine(data, "moved-windows.txt");
            int back = CloneWindow.Recover();
            if (back > 0) ActivityLog.Write("clones", "start after a crash", "put " + back + " moved window(s) back");
            Charm.RudeHeard = delegate { Post(delegate { if (UpsetTool()) ActivityLog.Write("clones", "he was rude to her", "chaos when upset set her clones off"); }); };
        }

        private void ClonesGuardTick()
        {
            if (cloneWindow == null) return;
            if (!LineBusy && ownLines.Count > 0) ownLines.Dequeue()();
            cloneGame = Lockout.Active || Computer.GameOpen() != null;
            string refused = CloneRefusalSeen();
            if (refused == null) return;
            ClonesOff();
            ActivityLog.Write("clones", "cleared", refused == CloneHook.InGame ? "a game started" : "switched off");
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox clones, upsetChaos;

        private void ClonesPage(ForkSettings current)
        {
            Section("CLONES AND CHAOS  (her copies on my desktop)");
            clones = Check("Clones: when I ask, she makes copies of herself that wander, chat, grab my cursor and drag my windows around; holding Esc clears them and puts every window back", current.Clones);
            upsetChaos = Check("Chaos when upset: being rude to her can set her clones off (1 in 3, then 2 in 3, always by the third)", current.UpsetChaos);
            Note("Never in a game or while the game lockout is on: a game starting clears them at once. Say \"clone yourself\", \"make 5 clones\" or \"stop the clones\".");
        }

        private void FillClones()
        {
            settings.Clones = clones.Checked;
            settings.UpsetChaos = upsetChaos.Checked;
        }
    }
}
