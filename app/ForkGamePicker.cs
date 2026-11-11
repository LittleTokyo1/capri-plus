using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Tone = CapriDesktop.Look;

namespace CapriDesktop
{
    internal sealed partial class ForkSettingsForm
    {
        internal const string AddRunningName = "addRunningGame";
        internal const string AddFileName = "addGameFile";
        private const int TitleLength = 48;

        internal static string[] AddGame(string[] lines, string entry)
        {
            List<string> names = new List<string>(lines ?? new string[0]);
            string name = (entry ?? "").Trim().Trim('"').Trim();
            try { name = Path.GetFileName(name); }
            catch (ArgumentException) { }
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4).Trim();
            if (name.Length == 0) return names.ToArray();
            foreach (string had in names)
                if (string.Equals(had.Trim(), name, StringComparison.OrdinalIgnoreCase)) return names.ToArray();
            names.Add(name);
            return names.ToArray();
        }

        internal static List<string[]> Windowed()
        {
            List<string[]> found = new List<string[]>();
            Process self = Process.GetCurrentProcess();
            string own = self.ProcessName;
            int ownId = self.Id;
            self.Dispose();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == ownId || string.Equals(process.ProcessName, own, StringComparison.OrdinalIgnoreCase)) continue;
                    string title = process.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    string name = process.ProcessName;
                    if (found.Exists(delegate(string[] item) { return string.Equals(item[0], name, StringComparison.OrdinalIgnoreCase); })) continue;
                    found.Add(new[] { name, title.Length > TitleLength ? title.Substring(0, TitleLength) + "..." : title });
                }
                catch (Exception) { }
                finally { process.Dispose(); }
            }
            found.Sort(delegate(string[] a, string[] b) { return string.Compare(a[0], b[0], StringComparison.OrdinalIgnoreCase); });
            return found;
        }

        private void GameKeys(TextBox target)
        {
            KitFlow row = new KitFlow { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            KitKey running = new KitKey { Text = "ADD RUNNING PROGRAM", Chevron = true, Name = AddRunningName, AccessibleName = "Add a running program", Size = new Size(220, 28), Margin = new Padding(0, 0, 8, 0) };
            KitKey file = new KitKey { Text = "ADD GAME FILE", Name = AddFileName, AccessibleName = "Add a game file", Size = new Size(160, 28), Margin = new Padding(0) };
            running.Click += delegate { RunningMenu(running, target); };
            file.Click += delegate { PickGameFile(target); };
            row.Controls.Add(running);
            row.Controls.Add(file);
            column.Controls.Add(row);
        }

        private static void AddTo(TextBox target, string name)
        {
            target.Text = string.Join(Environment.NewLine, AddGame(Lines(target.Text), name));
        }

        private void RunningMenu(KitKey key, TextBox target)
        {
            float s = Kit.Unit(key);
            ToolStripDropDownMenu menu = new ToolStripDropDownMenu { ShowImageMargin = false, ShowCheckMargin = false, Renderer = Kit.Menu(), Padding = new Padding(0, (int)(4 * s), 0, (int)(4 * s)) };
            List<string[]> programs = Windowed();
            foreach (string[] program in programs)
            {
                string name = program[0];
                ToolStripMenuItem item = new ToolStripMenuItem(name + "   " + program[1]) { Font = key.Font, ForeColor = Neon.White, AutoSize = false, Size = new Size(Math.Max(key.Width * 2, (int)(420 * s)), (int)(26 * s)), Padding = new Padding((int)(8 * s), 0, 0, 0) };
                item.Click += delegate { AddTo(target, name); };
                menu.Items.Add(item);
            }
            if (programs.Count == 0) menu.Items.Add(new ToolStripMenuItem("No program with a window is open") { Font = key.Font, ForeColor = Neon.White, Enabled = false });
            menu.Closed += delegate { BeginInvoke((MethodInvoker)delegate { menu.Dispose(); }); };
            menu.Show(key, new Point(0, key.Height));
        }

        private void PickGameFile(TextBox target)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Pick the game's program file";
                dialog.Filter = "Programs (*.exe)|*.exe";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) AddTo(target, dialog.FileName);
            }
        }
    }
}
