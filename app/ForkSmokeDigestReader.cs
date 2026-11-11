using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeDigestReader(List<string> checks, string shots)
        {
            string keptLog = ActivityLog.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-digest-reader-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Digest.Reset();
                DateTime now = DateTime.Now;
                Digest.Add("discord", "Casey: are you coming tonight?", now.AddMinutes(-30));
                Digest.Add("study", "A school mail about Dutch level 2 needs a reply.", now.AddMinutes(-20));
                Digest.Add("remind", "Water the plants.", now.AddMinutes(-10));
                Digest.Clear();
                if (Digest.Count != 0 || Digest.Items().Count != 0) throw new Exception("Closing the away box left items to pop up again.");
                List<DigestItem> all = Digest.All();
                if (all.Count != 3 || !all[0].Seen) throw new Exception("Closing the away box deleted what he missed instead of keeping it to read again.");
                Digest.MarkReplied(all[1]);
                Digest.Reset();
                all = Digest.All();
                if (all.Count != 3 || !all[1].Replied || all[0].Replied) throw new Exception("A reply tick was not kept for the right item after a restart.");
                string line = DigestReader.ReplyLine(all[0], "yes, at 8");
                if (line.IndexOf("Casey: are you coming tonight?", StringComparison.Ordinal) < 0 || line.IndexOf("yes, at 8", StringComparison.Ordinal) < 0 || line.IndexOf("discord", StringComparison.Ordinal) < 0) throw new Exception("A reply from the away window does not carry the item it answers: " + line);
                using (DigestReader reader = new DigestReader())
                {
                    reader.Show(all, 1);
                    if (reader.FullText != all[1].Text || reader.Rows.Count != 3 || reader.Rows[1].IndexOf(DigestReader.RepliedMark, StringComparison.Ordinal) < 0 || reader.Rows[0].IndexOf(DigestReader.RepliedMark, StringComparison.Ordinal) >= 0)
                        throw new Exception("The away window does not show the whole picked message or its reply tick.");
                    DigestItem answered = null;
                    string said = null;
                    reader.Replied += delegate(DigestItem item, string text) { answered = item; said = text; };
                    reader.Pick(0);
                    reader.Send("");
                    if (answered != null) throw new Exception("An empty reply was sent.");
                    reader.Send("  yes, at 8  ");
                    if (answered == null || answered.Text != all[0].Text || said != "yes, at 8" || reader.FullText != all[0].Text) throw new Exception("SEND in the away window did not reply to the picked item.");
                    reader.Pick(2);
                    reader.Send("done");
                    if (answered.Text != all[2].Text) throw new Exception("He can't reply to a second item in the same window.");
                    if (reader.FormBorderStyle != System.Windows.Forms.FormBorderStyle.None || reader.Waiting != "2 WAITING") throw new Exception("The away window is not a borderless kit window, or its waiting count is wrong: " + reader.Waiting);
                    if (DigestReader.NextOpen(all, 1) != 2 || DigestReader.NextOpen(all, 2) != 0 || DigestReader.NextOpen(new List<DigestItem>(), 0) != -1)
                        throw new Exception("After a reply the next unanswered message does not open.");
                    if (DigestReader.When(new DigestItem { When = now.Date.AddDays(-1).AddHours(22) }, now) != "YESTERDAY 22:00") throw new Exception("An item from yesterday does not say so.");
                    string pictures = Environment.GetEnvironmentVariable("CAPRI_SHOTS");
                    if (!string.IsNullOrEmpty(pictures) && Directory.Exists(pictures)) shots = pictures;
                    if (shots != null && Directory.Exists(shots) && pictures != null)
                    {
                        reader.Pick(0);
                        reader.Hover(1);
                        using (System.Drawing.Bitmap shot = new System.Drawing.Bitmap(reader.Width, reader.Height))
                        {
                            reader.Draw(shot);
                            shot.Save(Path.Combine(shots, "away-window.png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                        reader.Hover(-1);
                    }
                    reader.Show(Digest.All(), 0);
                    if (!reader.CanClear) throw new Exception("CLEAR REPLIED is off while a replied message is in the list.");
                    System.Drawing.Rectangle row = new System.Drawing.Rectangle(0, 0, 300, 70);
                    System.Drawing.Rectangle cross = DigestReader.Cross(row, 1f);
                    if (!row.Contains(cross) || cross.Left < row.Width / 2 || cross.Top > row.Height / 2) throw new Exception("The X on a card is not in its top right corner: " + cross);
                    reader.Drop(0);
                    all = Digest.All();
                    if (all.Count != 2 || reader.Rows.Count != 2 || all[0].Source != "study" || reader.Picked != 0 || reader.FullText != all[0].Text) throw new Exception("The X did not remove that one message, or the next one did not open.");
                    reader.DropReplied();
                    all = Digest.All();
                    if (all.Count != 1 || all[0].Source != "remind" || reader.Rows.Count != 1 || reader.CanClear || reader.Waiting != "1 WAITING") throw new Exception("CLEAR REPLIED did not remove only the replied messages: " + all.Count + " left, " + reader.Waiting);
                    reader.Drop(0);
                    reader.Drop(0);
                    if (reader.Rows.Count != 0 || reader.Picked != -1 || reader.FullText.Length != 0) throw new Exception("Removing the last message left something on screen.");
                    Digest.Reset();
                    if (Digest.All().Count != 0) throw new Exception("A removed message came back after a restart.");
                }
                if (!DigestReader.Asked("show me what I missed") || !DigestReader.Asked("Capri, open the while you were away box") || !DigestReader.Asked("what did I miss?") || DigestReader.Asked("show me the weather") || DigestReader.Asked("what did I miss in the game last night, the score?"))
                    throw new Exception("Asking to see what he missed again was read wrong.");
                checks.Add("PASS away window: closing the away box keeps the last day's items (no more pop-ups), a click on a line opens them in a window with the whole message, a reply box answers that exact item (one after another, each ticked as replied, kept across restarts), the window is a borderless red-kit window with a waiting count and the next unanswered message opens after each reply, and show me what I missed opens it again; an X on each card (or Delete) removes that message for good, CLEAR REPLIED removes every answered one");
            }
            finally
            {
                ActivityLog.FilePath = keptLog;
                Digest.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private void SmokeDigestMenu(List<string> checks)
        {
            System.Windows.Forms.ToolStripItem[] found = menu.Items.Find(DigestMenuName, false);
            if (found.Length != 1) throw new Exception("Her right-click menu has no way to open the while-you-were-away window.");
            if (DigestMenuText(0) != "While you were away" || DigestMenuText(3) != "While you were away (3 waiting)") throw new Exception("The menu line does not show how many are waiting: " + DigestMenuText(3));
            checks.Add("PASS away window from her menu: right-clicking her (or the tray icon) has While you were away, with the waiting count, so he can open it again after closing it");
        }
    }
}
