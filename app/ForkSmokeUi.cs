using System;
using System.Collections.Generic;
using System.Drawing;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeUi(List<string> checks)
        {
            SmokeBubbleKinds(checks);
            SmokeOfferBox(checks);
            SmokeSettingsLook(checks);
            SmokeLeagueLook(checks);
            SmokeDigestAndChips(checks);
            UiShots();
        }

        private void UiShots()
        {
            string folder = Environment.GetEnvironmentVariable("CAPRI_SHOTS");
            if (string.IsNullOrEmpty(folder)) return;
            System.IO.Directory.CreateDirectory(folder);
            Rectangle area = new Rectangle(0, 0, 1600, 900);
            Point head = new Point(800, 600), feet = new Point(800, 800);
            string[][] bubbles = { new[] { "> you: ", "what's up", "chat" }, new[] { Bubble.Sys, "(heartbeat)", "system" }, new[] { Bubble.Sys, "(reminder)", "alert" }, new[] { Bubble.Sys, Bubble.Failed, "failed" }, new[] { Bubble.Discord, "order the usual", "discord" } };
            foreach (string[] kind in bubbles)
                using (SpeechBox speech = new SpeechBox())
                {
                    speech.Prompt = kind[0];
                    speech.Heard = kind[1];
                    speech.Reply = "Done, choom. The dentist is on your calendar for Tuesday at ten.";
                    speech.Revealed = 200;
                    speech.State = TalkState.Online;
                    speech.Update(10, 1, head, feet, area);
                    speech.Image.Save(System.IO.Path.Combine(folder, "bubble-" + kind[2] + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                    speech.Hide();
                }
            using (OfferBox offer = new OfferBox())
            {
                offer.Open("Want me to add the dentist tomorrow at 10 to your calendar?", 0);
                offer.Update(45, 1, head, feet, area, Rectangle.Empty);
                offer.Image.Save(System.IO.Path.Combine(folder, "offer.png"), System.Drawing.Imaging.ImageFormat.Png);
                offer.Hide();
            }
            List<DigestItem> items = new List<DigestItem>();
            string[] sources = { "remind", "checkin", "heads", "offer" };
            for (int i = 0; i < 4; i++) items.Add(new DigestItem { When = new DateTime(2026, 10, 30, 14, 5 * i, 0), Source = sources[i], Text = "Something that happened while you were out number " + i });
            using (DigestBox digest = new DigestBox())
            {
                digest.Open(items, 0);
                digest.Update(0, 1, head, feet, area, Rectangle.Empty);
                digest.Image.Save(System.IO.Path.Combine(folder, "digest.png"), System.Drawing.Imaging.ImageFormat.Png);
                digest.Hide();
            }
            using (Chip toast = new Chip())
            {
                toast.Set("MIC MUTED", Chip.Colour("mic", true), 0);
                toast.Update(0, 1, head, area, Rectangle.Empty);
                toast.Image.Save(System.IO.Path.Combine(folder, "toast.png"), System.Drawing.Imaging.ImageFormat.Png);
                toast.Hide();
            }
            using (LeaguePanel panel = new LeaguePanel())
            {
                panel.Fill("Ahri", new List<string> { "1) Electrocute + Sorcery: Luden's > Sorc boots > Shadowflame, Flash + Ignite. 5 of 12 pro games, like Faker 5/1/7 (win).", "2) First Strike + Inspiration: Malignance > Shadowflame, Flash + Teleport. 3 of 12 pro games, like Chovy 3/0/9 (win).", "3) Arcane Comet + Sorcery: Luden's > Rabadon's, Flash + Barrier. 2 of 12 pro games, like Caps 2/4/6 (loss)." }, true);
                panel.Location = new Point(-4000, -4000);
                panel.Show();
                panel.Status("Build 1 is in your client.");
                panel.Refresh();
                using (Bitmap picture = new Bitmap(panel.Width, panel.Height))
                {
                    panel.DrawToBitmap(picture, new Rectangle(0, 0, panel.Width, panel.Height));
                    picture.Save(System.IO.Path.Combine(folder, "league.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
                panel.Hide();
            }
        }

        private void SmokeDigestAndChips(List<string> checks)
        {
            Digest.Clear();
            DateTime start = new DateTime(2026, 10, 30, 14, 0, 0);
            string[] sources = { "remind", "checkin", "heads", "job", "offer" };
            for (int i = 0; i < 9; i++) Digest.Add(sources[i % sources.Length], "Item number " + i + " with a long tail that goes on and on past the cut", start.AddMinutes(i * 7));
            List<DigestItem> held = Digest.Items();
            List<string> rows = Digest.Rows(held, Digest.MostShown);
            if (Digest.Count != 9 || rows.Count != Digest.MostShown || rows[0] != "+3 more" || !rows[1].StartsWith("14:21  job     Item number 3", StringComparison.Ordinal) || !rows[rows.Count - 1].EndsWith("...", StringComparison.Ordinal) || rows[rows.Count - 1].Length > 5 + 2 + 7 + 1 + 36) throw new Exception("The digest rows were wrong: " + string.Join(" | ", rows.ToArray()));
            if (Digest.Intro(1) != "1 thing while you were out." || Digest.Intro(3) != "3 things while you were out.") throw new Exception("The digest line was wrong: " + Digest.Intro(3));
            if (!Digest.May(false, false, false, false, false, false, 3) || Digest.May(true, false, false, false, false, false, 3) || Digest.May(false, true, false, false, false, false, 3) || Digest.May(false, false, true, false, false, false, 3) || Digest.May(false, false, false, true, false, false, 3) || Digest.May(false, false, false, false, true, false, 3) || Digest.May(false, false, false, false, false, true, 3) || Digest.May(false, false, false, false, false, false, 0)) throw new Exception("The digest showed while he was away, in a game, paused, in the quiet hours, hidden, while she was still talking or with nothing to show.");
            using (DigestBox box = new DigestBox())
            {
                Rectangle area = new Rectangle(0, 0, 1600, 900);
                box.Open(held, 10);
                box.Update(10, 1, new Point(800, 600), new Point(800, 800), area, Rectangle.Empty);
                int clicked = 0;
                box.Picked += delegate { clicked++; };
                if (!(box is Overlay) || !area.Contains(box.PanelOnScreen) || box.PanelOnScreen.Bottom > 600) throw new Exception("The digest panel was not an overlay above her head.");
                string full = "Item number 8 with a long tail that goes on and on past the cut";
                string hidden = Digest.Full(held, Digest.MostShown, 0) ?? "";
                if (Digest.Full(held, Digest.MostShown, rows.Count - 1) != full || !hidden.Contains("14:00 Item number 0 with a long tail that goes on and on past the cut") || !hidden.Contains("Item number 2") || hidden.Contains("Item number 3") || Digest.Full(held, Digest.MostShown, rows.Count) != null || Digest.Full(held, Digest.MostShown, -1) != null) throw new Exception("A digest row did not give its whole text: " + Digest.Full(held, Digest.MostShown, rows.Count - 1) + " | " + hidden);
                string read = null;
                box.Read += delegate(string text) { read = text; };
                Rectangle close = box.CloseBox;
                Rectangle last = box.RowBox(rows.Count - 1);
                Point closeAt = new Point(close.X + close.Width / 2, close.Y + close.Height / 2);
                Point lastAt = new Point(last.X + last.Width / 2, last.Y + last.Height / 2);
                if (close.Width < 16 || last.Height < 10 || close.IntersectsWith(box.RowBox(0)) || box.Hit(closeAt) != -2 || box.Hit(lastAt) != rows.Count - 1 || box.Hit(new Point(1, 1)) != -1) throw new Exception("The digest close key or rows were not where a click lands.");
                box.Press(lastAt);
                if (read != full || clicked != 0) throw new Exception("Clicking a digest row did not read its whole text, or it cleared the box.");
                box.Press(new Point(1, 1));
                if (clicked != 0) throw new Exception("A click beside the rows cleared the digest.");
                box.Press(closeAt);
                if (clicked != 1) throw new Exception("The digest close key did not clear it.");
                box.Hide();
            }
            Digest.Clear();
            if (Digest.Count != 0) throw new Exception("Clearing the digest left items.");
            string[][] expect = { new[] { "mic", "True", "MIC MUTED" }, new[] { "mic", "False", "MIC ON" }, new[] { "paused", "True", "PAUSED" }, new[] { "paused", "False", "UNPAUSED" }, new[] { "voice", "True", "VOICE ON" }, new[] { "voice", "False", "VOICE OFF" }, new[] { "autonomy", "True", "AUTONOMY ON" }, new[] { "autonomy", "False", "AUTONOMY OFF" }, new[] { "blackwall", "True", "BLACKWALL ON" }, new[] { "blackwall", "False", "BLACKWALL OFF" } };
            foreach (string[] one in expect) if (Chip.For(one[0], one[1] == "True") != one[2]) throw new Exception("A toast said " + Chip.For(one[0], one[1] == "True") + " for " + one[0]);
            if (!Same(Chip.Colour("mic", true), Neon.Hot) || !Same(Chip.Colour("paused", true), Neon.Hot) || !Same(Chip.Colour("autonomy", true), Look.Yellow) || !Same(Chip.Colour("blackwall", true), Look.Yellow) || !Same(Chip.Colour("autonomy", false), Neon.Cyan) || !Same(Chip.Colour("voice", true), Neon.Cyan) || !Same(Chip.Colour("voice", false), Neon.Hot)) throw new Exception("A toast had the wrong colour.");
            string overlays = RedOverlays();
            if (overlays != null) throw new Exception(overlays);
            using (Chip chip = new Chip())
            {
                System.Reflection.PropertyInfo made = typeof(System.Windows.Forms.Form).GetProperty("CreateParams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Windows.Forms.CreateParams style = (System.Windows.Forms.CreateParams)made.GetValue(chip, null);
                chip.Set("SETTINGS SAVED", Neon.Cyan, 5);
                chip.Set("AUTONOMY ON", Look.Yellow, 5.5);
                chip.Update(5.6, 1, new Point(800, 600), new Rectangle(0, 0, 1600, 900), Rectangle.Empty);
                if (chip.Text != "AUTONOMY ON" || chip.Expired(6.9) || !chip.Expired(7.2) || (style.ExStyle & 0x20) == 0 || (style.ExStyle & 0x08000000) == 0) throw new Exception("A toast did not replace the old one, last 1.6 seconds and let clicks through.");
                chip.Hide();
            }
            checks.Add("PASS away digest and toasts: what went to his phone while he was away is kept and shown once he is home (never in a game, paused, quiet hours or hidden) as one panel of at most seven rows (+N more, time, source, text cut at 36) with one short spoken line; a click on a row puts its whole text in her bubble (+N more gives the hidden ones), the x key or him talking clears it, a click beside the rows does nothing; toasts say MIC MUTED, PAUSED, VOICE ON or OFF, AUTONOMY and BLACKWALL ON or OFF in their colours, one at a time for 1.6 seconds, and let clicks through; the offer box, digest and toasts are drawn in her red look (hot red frame, no old cyan edge)");
        }

        private void SmokeLeagueLook(List<string> checks)
        {
            string[] parts = LeaguePanel.Parts("2) Electrocute + Sorcery: Luden's > Sorc boots > Shadowflame, Flash + Ignite. 5 of 12 pro games, like Faker 5/1/7 (win).");
            if (parts[0] != "Electrocute + Sorcery: Luden's > Sorc boots > Shadowflame, Flash + Ignite" || parts[1] != "5 of 12 pro games, like Faker 5/1/7 (win).") throw new Exception("A pro build line was not split into its build and its games: " + parts[0] + " | " + parts[1]);
            using (LeaguePanel panel = new LeaguePanel())
            {
                List<string> lines = new List<string> { "1) A + B: x > y. 3 of 9 pro games, like P1 1/2/3 (win).", "2) C + D: z. 2 of 9 pro games, like P2 4/5/6 (loss).", "3) E + F: w. 1 of 9 pro games, like P3 7/8/9 (win)." };
                panel.Fill("Ahri", lines, true);
                int tall = panel.ClientSize.Height;
                foreach (System.Windows.Forms.Button key in panel.Buttons) if (!(key is KitKey) || !key.Text.StartsWith("PUSH ", StringComparison.Ordinal)) throw new Exception("A push button is not a kit key: " + key.Text);
                System.Reflection.PropertyInfo made = typeof(System.Windows.Forms.Form).GetProperty("CreateParams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Windows.Forms.CreateParams style = (System.Windows.Forms.CreateParams)made.GetValue(panel, null);
                System.Reflection.PropertyInfo quiet = typeof(System.Windows.Forms.Form).GetProperty("ShowWithoutActivation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (panel.Buttons.Count != 3 || panel.FormBorderStyle != System.Windows.Forms.FormBorderStyle.None || panel.Region == null || (style.ExStyle & 0x08000000) == 0 || !(bool)quiet.GetValue(panel, null)) throw new Exception("The pro builds panel is not a borderless kit panel that never takes focus.");
                if (panel.HitTest(new Point(60, 16)) != 2 || panel.HitTest(new Point(60, tall - 10)) != 1) throw new Exception("The pro builds panel does not drag by its title.");
                panel.Status("The client didn't take it. Try again or set it by hand.");
                bool failed = Same(panel.StatusColour, Neon.Hot);
                panel.Status("Build 1 is in your client.");
                if (!failed || !Same(panel.StatusColour, Neon.Cyan)) throw new Exception("The pro builds panel did not colour a failed and a done push.");
                foreach (System.Windows.Forms.Button key in panel.Buttons) if (!Same(((KitKey)key).Accent, Neon.Cyan)) throw new Exception("A push key is not her cyan step key.");
                panel.Location = new Point(-4000, -4000);
                panel.Show();
                string framed = RedFrame(panel);
                panel.Hide();
                if (framed != null) throw new Exception("The pro builds panel " + framed);
                panel.Fill("Ahri", lines, false);
                if (panel.Buttons.Count != 0 || panel.Controls.Count != 1) throw new Exception("Push keys stayed with the push buttons switched off.");
            }
            checks.Add("PASS league panel look: the pro builds panel is a borderless kit panel in her red look (hot red frame, PRO BUILDS caption, PUSH kit keys, build and games on two lines) that never takes focus from the League client, drags by its title, colours a failed push red and a done push neon, and has no keys when push buttons are off; lane counters use the yellow champ select system bubble");
        }

        private static void Walk(System.Windows.Forms.Control parent, List<System.Windows.Forms.Control> found)
        {
            foreach (System.Windows.Forms.Control child in parent.Controls)
            {
                found.Add(child);
                Walk(child, found);
            }
        }

        private void SmokeSettingsLook(List<string> checks)
        {
            Color oldPink = Color.FromArgb(255, 45, 106);
            ForkSettings picked = new ForkSettings();
            picked.Tidy();
            picked.Proactive = 1;
            picked.GameVision = 2;
            picked.QuietFrom = 2;
            picked.QuietTo = 9;
            picked.CheckInGapHours = 5;
            picked.CheckInsPerDay = 3;
            picked.MicThreshold = 17;
            picked.Cap = 12.5;
            picked.VoiceBudget = 5000;
            picked.ChatSign = "- Capri";
            picked.AutoCalendar = 2;
            picked.NightlyOn = false;
            picked.HoldVk = 0x56;
            picked.HoldMods = 3;
            picked.WalkSpeed = 4;
            using (ForkSettingsForm form = new ForkSettingsForm(picked))
            {
                List<System.Windows.Forms.Control> all = new List<System.Windows.Forms.Control>();
                Walk(form, all);
                int headers = 0, locks = 0;
                foreach (System.Windows.Forms.Control control in all)
                {
                    string family = control.Font.FontFamily.Name;
                    if (!Red(family)) throw new Exception("A settings control is not in her Bahnschrift: " + family + " on " + control.GetType().Name + " " + control.Text);
                    if (control is System.Windows.Forms.ComboBox || control is System.Windows.Forms.NumericUpDown || control is System.Windows.Forms.TrackBar || control is System.Windows.Forms.ProgressBar || control is System.Windows.Forms.TabControl) throw new Exception("A stock control is still in the settings window: " + control.GetType().Name);
                    if (Same(control.ForeColor, oldPink) || Same(control.BackColor, oldPink)) throw new Exception("Pink furniture is still in the settings window: " + control.Text);
                    if (control.Tag as string == Kit.Header) { headers++; if (!Same(control.ForeColor, Neon.White) || !control.Text.EndsWith(" >", StringComparison.Ordinal)) throw new Exception("A section header is not a white title on its band: " + control.Text); }
                    if (control.Tag as string == Kit.Caption && !Same(control.ForeColor, Neon.Rose)) throw new Exception("A setting name is not rose: " + control.Text);
                    if (control is KitLock) { locks++; if (control.CanSelect || control.TabStop) throw new Exception("The lockout row can take focus."); }
                }
                if (headers != 33 || locks != 1) throw new Exception("The settings window has " + headers + " headers and " + locks + " lockout rows.");
                if (form.FormBorderStyle != System.Windows.Forms.FormBorderStyle.None || !Same(form.BackColor, Neon.Ground)) throw new Exception("The settings window still has the system frame or its own palette.");
                string framed = RedFrame(form);
                if (framed != null) throw new Exception("The settings window " + framed);
                Size size = form.ClientSize;
                if (form.HitTest(new Point(2, size.Height / 2)) != 10 || form.HitTest(new Point(size.Width - 2, size.Height - 2)) != 17 || form.HitTest(new Point(120, 20)) != 2 || form.HitTest(new Point(size.Width / 2, size.Height / 2)) != 1) throw new Exception("The borderless settings window does not drag by its title or resize by its edges.");
                if (form.Unsaved) throw new Exception("A fresh settings window said it had unsaved changes.");
                form.Apply();
                if (picked.Proactive != 1 || picked.GameVision != 2 || picked.QuietFrom != 2 || picked.QuietTo != 9 || picked.CheckInGapHours != 5 || picked.CheckInsPerDay != 3 || picked.MicThreshold != 17 || Math.Abs(picked.Cap - 12.5) > 0.001 || picked.VoiceBudget != 5000 || picked.ChatSign != "- Capri" || picked.AutoCalendar != 2 || picked.NightlyOn || picked.HoldVk != 0x56 || picked.HoldMods != 3 || picked.WalkSpeed != 4)
                    throw new Exception("The kit controls did not keep the values they were given.");
                form.Choose(2, 1, 3);
                if (!form.Unsaved) throw new Exception("A changed choice did not show unsaved changes.");
                form.Filter("heartbeat");
                string many = form.MatchNote;
                form.Filter("zzzzqq");
                string none = form.MatchNote;
                form.Filter("");
                if (!many.EndsWith(" MATCHES", StringComparison.Ordinal) || none != "NO MATCH" || form.MatchNote != "") throw new Exception("The search count was wrong: " + many + " / " + none + " / " + form.MatchNote);
            }
            KitNumber hours = new KitNumber(0, 23, 0, 5);
            hours.Commit("99");
            int high = (int)hours.Value;
            hours.Commit("abc");
            int kept = (int)hours.Value;
            hours.Commit("-4");
            KitNumber money = new KitNumber(0, 1000, 2, 10);
            money.Commit("12.345");
            KitPick pick = new KitPick(new[] { "a", "b", "c" }, 1);
            int changed = 0;
            pick.SelectedIndexChanged += delegate { changed++; };
            pick.SelectedIndex = 7;
            pick.SelectedIndex = 2;
            if (high != 23 || kept != 23 || hours.Value != 0 || hours.Small || money.Value != 12.35m || pick.SelectedIndex != 2 || changed != 1 || !new KitNumber(1, 12, 0, 3).Small) throw new Exception("Numbers and dropdowns did not clamp: " + high + " " + kept + " " + hours.Value + " " + money.Value + " " + pick.SelectedIndex + " " + changed);
            hours.Dispose();
            money.Dispose();
            pick.Dispose();
            string parts = RedParts();
            if (parts != null) throw new Exception(parts);
            checks.Add("PASS settings look: the Capri+ settings window is a borderless kit window in her red 0.7.7 look (Bahnschrift, her ground, a hot red frame with no cyan edge, white section titles on bands, rose setting names, rose keys, an OFF and ON pair with ON solid cyan, dropdowns in her Mainframe menu; no pink, no stock dropdowns, numbers, sliders, bars or tab control), drags by its title and resizes by its edges, has nine section titles, a read-only lockout row that never takes focus, keeps every value it is given, shows unsaved changes and a match count, and its numbers and dropdowns clamp");
        }

        private void SmokeOfferBox(List<string> checks)
        {
            Rectangle area = new Rectangle(0, 0, 1600, 900);
            string question = "Want me to add the dentist tomorrow at 10?";
            using (OfferBox offer = new OfferBox())
            {
                if (!(offer is Overlay)) throw new Exception("The offer box is not an overlay that never takes focus.");
                offer.Open(question, 100);
                offer.Update(100.5, 1, new Point(800, 600), new Point(800, 800), area, Rectangle.Empty);
                Point yes = offer.KeyCentre(1), no = offer.KeyCentre(2);
                if (offer.PartAt(yes) != 1 || offer.PartAt(no) != 2 || offer.PartAt(new Point(3, 3)) != 0 || offer.PartAt(new Point(yes.X, yes.Y - 40)) != 0) throw new Exception("The offer keys did not hit-test as YES, NO and outside.");
                if (Math.Abs(offer.Share(160) - 0.5) > 0.01 || offer.Expired(219) || !offer.Expired(221) || OfferBox.Seconds != 120) throw new Exception("The offer countdown was not two minutes.");
                if (offer.PanelOnScreen.Bottom > 600 || !area.Contains(offer.PanelOnScreen)) throw new Exception("The offer box was not above her head on the screen: " + offer.PanelOnScreen);
                offer.Update(101, 1, new Point(800, 600), new Point(800, 800), area, new Rectangle(560, 380, 420, 120));
                if (offer.PanelOnScreen.Bottom > 380) throw new Exception("The offer box sat on her speech bubble: " + offer.PanelOnScreen);
                offer.Close("left it.", 102);
                if (offer.PartAt(yes) != 0 || offer.Done(103.5) || !offer.Done(104.5)) throw new Exception("A closed offer kept its keys or stayed too long.");
                offer.Hide();
            }

            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath;
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "capri-offer-smoke-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            Action<string> keptSay = offerSay;
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = System.IO.Path.Combine(folder, "fork.json");
                Autonomy.Reset();
                DateTime now = DateTime.Now;
                Autonomy.Record(new AutoOffer { Area = "calendar", Text = question, Act = "add", A = "Dentist", B = now.AddDays(1).ToString("yyyy-MM-dd HH:mm"), C = "45" }, now);
                if (!Autonomy.IsOffer(question)) throw new Exception("A fresh offer was not known as an offer.");
                Autonomy.Delivered(question, now);
                if (Autonomy.IsOffer(question)) throw new Exception("An offer still counted as untold after it was delivered, so the heartbeat must check before it marks it.");
                if (!Autonomy.StillOpen(question, now.AddSeconds(5)) || Autonomy.StillOpen("Something else?", now.AddSeconds(5)) || Autonomy.StillOpen(question, now.AddMinutes(16))) throw new Exception("An open offer was not found, or a wrong or old one was.");
                List<string> said = new List<string>();
                offerSay = delegate(string text) { said.Add(text); };
                ShowOffer(question, now);
                OfferPicked(1, now.AddSeconds(5));
                if (said.Count != 1 || said[0] != "yes" || offerBox.Ended < 0) throw new Exception("Pressing YES did not answer yes once: " + string.Join(",", said.ToArray()));
                Autonomy.Answered("yes", now.AddSeconds(6));
                if (Autonomy.StillOpen(question, now.AddSeconds(7))) throw new Exception("An answered offer stayed open.");
                ShowOffer(question, now.AddSeconds(8));
                OfferPicked(2, now.AddSeconds(9));
                if (said.Count != 1) throw new Exception("A key on an offer that was already answered still sent words.");
                string plain = "Want me to look up flights to Rome for you?";
                Autonomy.Record("research", plain, now.AddMinutes(1));
                Autonomy.Delivered(plain, now.AddMinutes(1));
                Autonomy.Answered("what time is it", now.AddMinutes(1).AddSeconds(3));
                if (!Autonomy.StillOpen(plain, now.AddMinutes(1).AddSeconds(4))) throw new Exception("Another message closed an offer that has nothing to run.");
                ShowOffer(plain, now.AddMinutes(1).AddSeconds(4));
                OfferPicked(2, now.AddMinutes(1).AddSeconds(5));
                OfferPicked(1, now.AddMinutes(1).AddSeconds(5));
                if (said.Count != 2 || said[1] != "no") throw new Exception("Pressing NO did not answer no once: " + string.Join(",", said.ToArray()));
                ShowOffer(plain, now.AddMinutes(1).AddSeconds(6));
                OfferPicked(0, now.AddMinutes(1).AddSeconds(7));
                if (said.Count != 2 || offerBox.Opened >= 0) throw new Exception("A click outside the keys answered or kept the offer box.");
            }
            finally
            {
                offerSay = keptSay;
                if (offerBox != null) { offerBox.Opened = -1; offerBox.Hide(); }
                Autonomy.Reset();
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                try { System.IO.Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            checks.Add("PASS offer box: an offer on the desktop gets its own box with YES and NO keys (part hit-test, never focus) and a two-minute countdown, sits above her head and above her speech bubble, a key answers yes or no once through his own words and only while that offer is still the newest open one, a click outside closes it without answering, and the heartbeat knows an offer before marking it told");
        }

        private static bool Same(Color a, Color b) { return a.ToArgb() == b.ToArgb(); }

        private static bool Red(string family)
        {
            using (Font plain = Neon.Type(9f)) return family.StartsWith("Bahnschrift", StringComparison.Ordinal) || family == plain.FontFamily.Name;
        }

        private static int Near(Bitmap image, Color colour, int slack)
        {
            int found = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    Color at = image.GetPixel(x, y);
                    if (at.A > 200 && Math.Abs(at.R - colour.R) <= slack && Math.Abs(at.G - colour.G) <= slack && Math.Abs(at.B - colour.B) <= slack) found++;
                }
            return found;
        }

        private static string RedFrame(System.Windows.Forms.Form form)
        {
            using (Bitmap picture = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(picture, new Rectangle(0, 0, form.Width, form.Height));
                using (Bitmap top = picture.Clone(new Rectangle(0, 0, Math.Min(form.Width, 120), 4), picture.PixelFormat))
                    if (Near(top, Neon.Hot, 40) < 10) return "has no hot red stroke over its top left corner.";
                using (Bitmap rim = picture.Clone(new Rectangle(0, 0, form.Width, 3), picture.PixelFormat))
                    if (Near(rim, Look.Cyan, 30) > 0) return "still has a cyan edge.";
            }
            return null;
        }

        private static string RedParts()
        {
            using (KitKey key = new KitKey())
                if (!Same(key.Accent, Neon.Rose)) return "A plain kit key is not rose.";
            using (KitEntry entry = new KitEntry(false))
                if (!Same(entry.Box.BackColor, Neon.Well)) return "A kit text box is not her well.";
            if (!(Kit.Menu() is Mainframe.MenuLook)) return "Kit dropdowns do not use her Mainframe menu.";
            using (KitToggle toggle = new KitToggle { Text = "Test", Size = new Size(320, 24), Checked = true })
            using (Bitmap on = new Bitmap(320, 24))
            using (Bitmap off = new Bitmap(320, 24))
            {
                toggle.DrawToBitmap(on, new Rectangle(0, 0, 320, 24));
                toggle.Checked = false;
                toggle.DrawToBitmap(off, new Rectangle(0, 0, 320, 24));
                using (Bitmap onPart = on.Clone(new Rectangle(300, 3, 14, 4), on.PixelFormat))
                using (Bitmap offPart = off.Clone(new Rectangle(300, 3, 14, 4), off.PixelFormat))
                    if (Near(onPart, Neon.Cyan, 20) < 20 || Near(offPart, Neon.Cyan, 20) > 0) return "A kit toggle is not an OFF and ON pair with ON solid cyan.";
            }
            return null;
        }

        private static string RedOverlays()
        {
            Rectangle area = new Rectangle(0, 0, 1600, 900);
            Point head = new Point(800, 600), feet = new Point(800, 800);
            List<Bitmap> shown = new List<Bitmap>();
            try
            {
                using (OfferBox offer = new OfferBox())
                {
                    offer.Open("Want me to add the dentist tomorrow at 10 to your calendar?", 0);
                    offer.Update(45, 1, head, feet, area, Rectangle.Empty);
                    shown.Add(new Bitmap(offer.Image));
                    offer.Hide();
                }
                List<DigestItem> items = new List<DigestItem> { new DigestItem { When = new DateTime(2026, 10, 30, 14, 0, 0), Source = "remind", Text = "Dentist at ten" } };
                using (DigestBox digest = new DigestBox())
                {
                    digest.Open(items, 0);
                    digest.Update(0, 1, head, feet, area, Rectangle.Empty);
                    shown.Add(new Bitmap(digest.Image));
                    digest.Hide();
                }
                using (Chip toast = new Chip())
                {
                    toast.Set("MIC MUTED", Chip.Colour("mic", true), 0);
                    toast.Update(0, 1, head, area, Rectangle.Empty);
                    shown.Add(new Bitmap(toast.Image));
                    toast.Hide();
                }
                string[] names = { "offer box", "digest", "toast" };
                for (int i = 0; i < shown.Count; i++)
                    if (Near(shown[i], Neon.Hot, 30) < 12 || Near(shown[i], Look.Cyan, 12) > 0) return "The " + names[i] + " is not in her red look.";
                using (SpeechBox chat = new SpeechBox())
                {
                    chat.Reply = "Done, choom.";
                    chat.Revealed = 100;
                    chat.State = TalkState.Online;
                    chat.Heard = "hey";
                    chat.Update(10, 1, head, feet, area);
                    int cyan = Near(chat.Image, Look.Cyan, 12);
                    chat.Hide();
                    if (cyan < 20) return "Her speech bubble lost upstream's cyan edge.";
                }
            }
            finally { foreach (Bitmap image in shown) image.Dispose(); }
            return null;
        }

        private void SmokeBubbleKinds(List<string> checks)
        {
            string you = "> you: ";
            if (!Same(Bubble.Rail(you, "hey"), Look.Red) || !Same(Bubble.Edge(you, "hey"), Look.Cyan) || Bubble.Footer(you, "hey") != null) throw new Exception("A chat bubble lost its red rail and cyan edge.");
            foreach (string upstream in new[] { "startup", "pets you", "settings", "voice check", null })
                if (!Same(Bubble.Rail(Bubble.Sys, upstream), Look.Red) || !Same(Bubble.Edge(Bubble.Sys, upstream), Look.Cyan)) throw new Exception("Upstream's own line turned into a system bubble: " + upstream);
            foreach (string label in new[] { "(heartbeat)", "(game)", "(autonomy)", "(undo)", "(settings)", "(champ select)" })
                if (!Same(Bubble.Rail(Bubble.Sys, label), Look.Yellow) || !Same(Bubble.Edge(Bubble.Sys, label), Look.Cyan)) throw new Exception("A system line did not get the yellow rail and cyan edge: " + label);
            foreach (string label in new[] { "(reminder)", "(now)", "(in 30 min)" })
                if (!Same(Bubble.Rail(Bubble.Sys, label), Look.Yellow) || !Same(Bubble.Edge(Bubble.Sys, label), Look.Yellow)) throw new Exception("An alert did not get the yellow edge: " + label);
            if (!Same(Bubble.Rail(Bubble.Sys, Bubble.Failed), Look.Red) || !Same(Bubble.Edge(Bubble.Sys, Bubble.Failed), Look.Red)) throw new Exception("A failure did not get the red edge and rail.");
            if (!Same(Bubble.Rail(Bubble.Discord, "order the usual"), Kit.Violet) || !Same(Bubble.Edge(Bubble.Discord, "order the usual"), Look.Cyan) || Bubble.Footer(Bubble.Discord, "order the usual") != "SENT ON DISCORD") throw new Exception("A Discord bubble did not get the violet rail and its footer.");
            if (Bubble.Footer(Bubble.Sys, "(heartbeat)") != null) throw new Exception("A system bubble got a footer it should not have.");
            if (Bubble.Outcome("Added \"Dentist\" Tuesday 10:00. Say undo that if it's wrong.", "(autonomy)") != "(autonomy)" || Bubble.Outcome("Saved a draft to a@b.cd in Gmail", "(autonomy)") != "(autonomy)" || Bubble.Outcome("Google didn't add it.", "(autonomy)") != Bubble.Failed || Bubble.Outcome("Removed the event \"Dentist\".", "(undo)") != "(undo)" || Bubble.Outcome("I couldn't remove it: signed out", "(undo)") != Bubble.Failed || Bubble.Outcome("I have nothing of mine to undo.", "(undo)") != "(undo)")
                throw new Exception("Done and failed results were labelled wrong.");
            using (SpeechBox chat = new SpeechBox())
            using (SpeechBox discord = new SpeechBox())
            {
                Rectangle area = new Rectangle(0, 0, 1600, 900);
                chat.Reply = discord.Reply = "Done, choom.";
                chat.Revealed = discord.Revealed = 100;
                chat.State = discord.State = TalkState.Online;
                chat.Heard = discord.Heard = "order the usual";
                discord.Prompt = Bubble.Discord;
                chat.Update(10, 1, new Point(800, 600), new Point(800, 800), area);
                discord.Update(10, 1, new Point(800, 600), new Point(800, 800), area);
                int grown = discord.PanelOnScreen.Height - chat.PanelOnScreen.Height;
                chat.Hide();
                discord.Hide();
                if (grown < 15 || grown > 24) throw new Exception("The Discord footer did not add one line to the bubble: " + grown);
            }
            checks.Add("PASS bubble kinds: chat and upstream's own lines keep the red rail, the fork's system lines get a yellow rail, reminders and due alerts a yellow edge, failures a red edge and rail, Discord a violet rail and a SENT ON DISCORD footer line, and done or failed results are labelled from her answer");
        }
    }
}
