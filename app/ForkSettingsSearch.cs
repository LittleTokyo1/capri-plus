using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Tone = CapriDesktop.Look;

namespace CapriDesktop
{
    internal sealed partial class ForkSettingsForm
    {
        private sealed class SettingGroup
        {
            internal string Section = "";
            internal Control Page;
            internal bool Header;
            internal string Text = "";
            internal readonly List<Control> Controls = new List<Control>();
        }

        private const int SearchHeight = 38;
        private const int NothingHeight = 22;
        private readonly KitFlow strip = new KitFlow();
        private readonly KitPanel host = new KitPanel();
        private readonly List<Control> pages = new List<Control>();
        private KitEntry search;
        private KitPanel bar;
        private KitLabel count;
        private KitLabel nothing;
        private Control shownPage;
        private readonly List<SettingGroup> groups = new List<SettingGroup>();

        internal string[] TabNames
        {
            get
            {
                List<string> names = new List<string>();
                foreach (Control page in pages) names.Add(page.Text);
                return names.ToArray();
            }
        }

        internal void SelectTab(string name)
        {
            foreach (Control page in pages) if (page.Text == name) ShowTab(page);
        }

        internal string SelectedTabName { get { return shownPage == null ? "" : shownPage.Text; } }

        internal string MatchNote { get { return count == null ? "" : count.Text; } }

        private static string TabName(string title)
        {
            string name = title.Split('(')[0].Trim().Split(' ')[0].ToLowerInvariant();
            return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private void StyleTabs()
        {
            host.Dock = DockStyle.Fill;
            host.Margin = Padding.Empty;
            strip.FlowDirection = FlowDirection.LeftToRight;
            strip.WrapContents = true;
            strip.AutoSize = true;
            strip.Dock = DockStyle.Top;
            strip.Margin = Padding.Empty;
            strip.Padding = new Padding(22, 4, 22, 8);
        }

        private void AddTab(Control page)
        {
            KitKey button = new KitKey { Text = page.Text, Height = 26, Font = Kit.Type(9.5f, true), Margin = new Padding(0, 0, 6, 6), AccessibleName = page.Text + " tab" };
            button.Width = TextRenderer.MeasureText(page.Text, button.Font).Width + 24;
            button.Click += delegate { ShowTab(page); };
            page.Tag = button;
            strip.Controls.Add(button);
        }

        private void ShowTab(Control shown)
        {
            if (shown == null) return;
            shownPage = shown;
            foreach (Control page in pages)
            {
                page.Visible = page == shown;
                KitKey button = page.Tag as KitKey;
                if (button == null) continue;
                button.Loud = page == shown;
                button.Accent = page == shown ? Neon.Cyan : Neon.Rose;
                button.Invalidate();
            }
        }

        private void SearchBox()
        {
            bar = new KitPanel { Dock = DockStyle.Top, Height = SearchHeight, Margin = Padding.Empty };
            search = new KitEntry(false) { Prompt = "> find:", Height = 28, AccessibleName = "Find a setting" };
            search.Cue = "type to find a setting";
            count = new KitLabel { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, ForeColor = Neon.Faint, Font = Kit.Type(8.5f, true), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            nothing = new KitLabel { Text = "Nothing matches. Try a shorter word.", ForeColor = Neon.Faint, Font = Kit.Type(8.5f), Visible = false };
            search.Box.TextChanged += delegate { Filter(search.Box.Text); };
            search.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { FirstMatch(); e.SuppressKeyPress = true; } };
            bar.Controls.Add(search);
            bar.Controls.Add(count);
            bar.Controls.Add(nothing);
            bar.Resize += delegate { PlaceSearch(); };
            root.Controls.Add(bar, 0, 0);
            PlaceSearch();
        }

        private void PlaceSearch()
        {
            int left = 22, wide = 110;
            search.SetBounds(left, 4, Math.Max(80, bar.Width - left * 2 - wide - 8), 28);
            count.SetBounds(bar.Width - left - wide, 4, wide, 28);
            nothing.Location = new Point(left, 4 + 28 + 4);
        }

        private void FirstMatch()
        {
            if (shownPage == null) return;
            foreach (Control control in shownPage.Controls[0].Controls)
            {
                if (!control.Visible || !control.CanSelect || control is Label) continue;
                control.Select();
                ((ScrollableControl)shownPage.Controls[0]).ScrollControlIntoView(control);
                return;
            }
        }

        private void Grouped()
        {
            foreach (Control page in pages)
            {
                string section = "";
                SettingGroup current = null;
                foreach (Control control in page.Controls[0].Controls)
                {
                    Label label = control as Label;
                    string tag = control.Tag as string;
                    bool header = label != null && tag == Kit.Header;
                    bool caption = label != null && tag == Kit.Caption;
                    if (header)
                    {
                        section = label.Text;
                        current = new SettingGroup { Section = section, Page = page, Header = true, Text = label.Text };
                        groups.Add(current);
                    }
                    else if (caption || control is CheckBox || current == null || current.Header)
                    {
                        current = new SettingGroup { Section = section, Page = page };
                        groups.Add(current);
                    }
                    current.Controls.Add(control);
                    if (label != null || control is CheckBox) current.Text += " " + control.Text;
                }
            }
        }

        internal string[] Filter(string query)
        {
            string wanted = (query ?? "").Trim().ToLowerInvariant();
            List<string> shown = new List<string>();
            List<string> shownSections = new List<string>();
            List<Control> hit = new List<Control>();
            Control first = null;
            foreach (SettingGroup group in groups)
            {
                if (group.Header) continue;
                bool show = wanted.Length == 0 || group.Text.ToLowerInvariant().Contains(wanted) || group.Section.ToLowerInvariant().Contains(wanted);
                foreach (Control control in group.Controls) control.Visible = show;
                if (!show) continue;
                shown.Add(group.Text.Trim());
                if (first == null) first = group.Page;
                if (!hit.Contains(group.Page)) hit.Add(group.Page);
                if (!shownSections.Contains(group.Section)) shownSections.Add(group.Section);
            }
            foreach (SettingGroup group in groups)
                if (group.Header) foreach (Control control in group.Controls) control.Visible = wanted.Length == 0 || shownSections.Contains(group.Section);
            foreach (Control page in pages)
            {
                KitKey button = page.Tag as KitKey;
                if (button == null) continue;
                button.Mark = wanted.Length > 0 && hit.Contains(page);
                button.Faded = wanted.Length > 0 && !hit.Contains(page);
                button.Invalidate();
                page.Controls[0].Invalidate(true);
            }
            bool none = wanted.Length > 0 && shown.Count == 0;
            count.Text = wanted.Length == 0 ? "" : none ? "NO MATCH" : shown.Count == 1 ? "1 MATCH" : shown.Count + " MATCHES";
            count.ForeColor = none ? Tone.Yellow : Neon.Faint;
            nothing.Visible = none;
            bar.Height = none ? SearchHeight + NothingHeight : SearchHeight;
            if (wanted.Length > 0 && first != null && shownPage != first) ShowTab(first);
            return shown.ToArray();
        }
    }
}
