using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Mainframe
    {
        internal const int MineRow = 24;
        private const int MineAt = 208;
        internal const string MineName = "CLONED VOICE";
        private Rung mineRung;
        private Key fallbackPick;
        private ContextMenuStrip fallbackMenu;
        private bool mine;

        private void ForkVoiceRows()
        {
            mineRung = new Rung();
            mineRung.AutoCheck = false;
            mineRung.TabStop = true;
            mineRung.Text = MineName;
            mineRung.Hint = "Her own · GPU · default";
            mineRung.Font = Type(9.5f, true);
            mineRung.HintFont = Type(9f);
            mineRung.AccessibleName = MineName + ", her own cloned voice on the graphics card";
            mineRung.Bounds = new Rectangle(28, MineAt, 328, 24);
            mineRung.Click += delegate { if (syncing) return; capri.PickClonedVoice(); };
            Controls.Add(mineRung);
            Lower(mineRung);
            fallbackMenu = Dropdown();
            fallbackPick = Lower(Make("Fallback: " + Companion.ElevenLabs, 28, 346, 328, 28, delegate { ShowFallbacks(); fallbackMenu.Show(fallbackPick, new Point(0, fallbackPick.Height)); }));
            fallbackPick.Chevron = true;
            fallbackPick.AccessibleName = "Fallback voice when her cloned voice can't speak";
            fallbackPick.Visible = false;
        }

        private int Mine(KeyValuePair<Control, int> entry) { return entry.Value >= MineAt && entry.Key != mineRung ? MineRow : 0; }

        private void ForkVoiceSnug()
        {
            if (mineRung == null) return;
            List<Control> stack = new List<Control> { mineRung };
            foreach (Rung rung in voiceRungs) stack.Add(rung);
            for (int i = 0; i + 1 < stack.Count; i++)
                if (stack[i].Bottom > stack[i + 1].Top && stack[i + 1].Top > stack[i].Top) stack[i].Height = stack[i + 1].Top - stack[i].Top;
        }

        private void ForkVoiceTier()
        {
            if (mineRung == null) return;
            bool ready = capri.ClonedReady;
            mineRung.Enabled = ready;
            mineRung.Hint = ready ? "Her own · GPU · default" : "Not set up · see Capri+";
            mine = !off && capri.ClonedShown;
            mineRung.Checked = mine;
            fallbackPick.Visible = mine;
            if (mine)
            {
                foreach (Rung rung in voiceRungs) rung.Checked = false;
                cloudPick.Visible = keyEntry.Visible = connect.Visible = cloudVoice.Visible = false;
                voiceEntry.Visible = speechModels.Visible = freeVoices.Visible = customVoice.Visible = freeSetup.Visible = false;
                fallbackPick.Text = "Fallback: " + capri.FallbackName;
            }
            mineRung.Invalidate();
            fallbackPick.Invalidate();
            if (Fold != arrangedFold) Arrange(expanded);
        }

        private void ShowFallbacks()
        {
            fallbackMenu.Items.Clear();
            bool eleven = capri.FallbackEleven;
            fallbackMenu.Items.Add(Choice(Companion.ElevenLabs + " (her free voice if it fails)", eleven, delegate { capri.ChooseFallback(true); }));
            fallbackMenu.Items.Add(Choice("Her free voice", !eleven, delegate { capri.ChooseFallback(false); }));
        }

        internal void WantEleven(bool on)
        {
            wantEleven = on;
            wantCloud = null;
        }

        internal void PickMine() { if (mineRung.Enabled) capri.PickClonedVoice(); }

        internal bool PickFallback(bool eleven)
        {
            ShowFallbacks();
            ToolStripItem item = fallbackMenu.Items[eleven ? 0 : 1];
            if (!item.Enabled) return false;
            item.PerformClick();
            return true;
        }

        internal string MineStatus
        {
            get
            {
                List<string> on = new List<string>();
                if (mineRung.Checked) on.Add(MineName);
                foreach (Rung rung in voiceRungs) if (rung.Checked) on.Add(rung.Text);
                return string.Join("+", on.ToArray()) + " | " + (fallbackPick.Visible ? fallbackPick.Text : "-") + " | " + (freeVoices.Visible || cloudPick.Visible || customVoice.Visible ? "upstream row" : "-");
            }
        }

        internal bool MineAbove { get { return voiceRungs[0].Top - mineRung.Top >= Scaled(MineRow) - 1; } }
    }
}
