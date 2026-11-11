using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;

namespace CapriDesktop
{
    internal sealed class ChatLine
    {
        internal ulong Id;
        internal string Author;
        internal string Text;
    }

    // Reads and writes chat windows. Discord's messages come through Windows accessibility in the background, without touching
    // the screen; any other app is read from a picture of its window. Writing brings the window forward only to type the reply.
    internal static class ChatReader
    {
        // "Alex7:22 PMWednesday, October 28, 2026 7:22 PMhey" : name, short time, the full timestamp ending in that same time, then the text.
        private static readonly Regex Stamp = new Regex(@"^(?<author>.*?)(?<time>\d{1,2}:\d{2}(?:\s?[AP]M)?)(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|Today|Yesterday)[^\n]*?(?<!\d)\k<time>(?<text>.*)$", RegexOptions.Singleline);
        private static readonly Regex Tail = new Regex(@":[a-z0-9_]+:Click to react.*$|Click to react.*$|Add Reaction.*$|\(edited\)", RegexOptions.Singleline);
        private const uint UnicodeKey = 0x0004, KeyUp = 0x0002;

        [StructLayout(LayoutKind.Sequential)] private struct KeyInput { internal ushort Key; internal ushort Scan; internal uint Flags; internal uint Time; internal IntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] internal KeyInput Key; [FieldOffset(24)] internal long Pad; }
        [StructLayout(LayoutKind.Sequential)] private struct Input { internal uint Type; internal InputUnion Data; }
        [StructLayout(LayoutKind.Sequential)] private struct Box { internal int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Box box);
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr window, out int process);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, int flags, UIntPtr extra);

        internal static string Title(IntPtr window)
        {
            StringBuilder text = new StringBuilder(512);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        internal static string Program(IntPtr window)
        {
            int id;
            GetWindowThreadProcessId(window, out id);
            try { using (Process process = Process.GetProcessById(id)) return process.ProcessName; }
            catch (Exception) { return ""; }
        }

        // "@Alex - Discord" is the DM with Alex; any other title is kept as it is.
        internal static string Conversation(string title)
        {
            string name = (title ?? "").Trim();
            if (name.EndsWith(" - Discord", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 10).Trim();
            return name.TrimStart('@', '#').Trim();
        }

        internal static IntPtr Discord()
        {
            foreach (Process process in Process.GetProcessesByName("Discord"))
            {
                try { if (process.MainWindowHandle != IntPtr.Zero && process.MainWindowTitle.EndsWith("Discord", StringComparison.Ordinal)) return process.MainWindowHandle; }
                catch (Exception) { }
                finally { process.Dispose(); }
            }
            return IntPtr.Zero;
        }

        // One Discord message from its accessibility id and name. A message without a name and time continues the last author's.
        internal static ChatLine Parse(string automationId, string name, string lastAuthor)
        {
            if (automationId == null || !automationId.StartsWith("chat-messages-", StringComparison.Ordinal)) return null;
            ulong id;
            if (!ulong.TryParse(automationId.Substring(automationId.LastIndexOf('-') + 1), out id)) return null;
            Match found = Stamp.Match(name ?? "");
            if (!found.Success) return null;
            string author = found.Groups["author"].Value.Trim();
            if (author.Length == 0) author = lastAuthor ?? "";
            string text = Regex.Replace(Tail.Replace(found.Groups["text"].Value, ""), @"\s+", " ").Trim();
            return new ChatLine { Id = id, Author = author, Text = text };
        }

        internal static List<ChatLine> ReadDiscord(IntPtr window)
        {
            List<ChatLine> lines = new List<ChatLine>();
            AutomationElement root = AutomationElement.FromHandle(window);
            AutomationElementCollection items = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            string author = null;
            foreach (AutomationElement item in items)
            {
                ChatLine line = Parse(item.Current.AutomationId, item.Current.Name, author);
                if (line == null) continue;
                author = line.Author;
                lines.Add(line);
            }
            if (lines.Count > 30) lines.RemoveRange(0, lines.Count - 30);
            return lines;
        }

        // A picture of the window even when another window covers it (not when it's minimized).
        internal static byte[] Snap(IntPtr window)
        {
            Box box;
            if (IsIconic(window) || !GetWindowRect(window, out box)) return null;
            int width = box.Right - box.Left, height = box.Bottom - box.Top;
            if (width < 50 || height < 50) return null;
            using (Bitmap shot = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(shot))
                {
                    IntPtr dc = graphics.GetHdc();
                    try { if (!PrintWindow(window, dc, 2)) return null; }
                    finally { graphics.ReleaseHdc(dc); }
                }
                return Companion.Jpeg(shot, 1280);
            }
        }

        private static AutomationElement MessageBox(IntPtr window)
        {
            AutomationElementCollection edits = AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            AutomationElement best = null;
            foreach (AutomationElement edit in edits)
            {
                string name = (edit.Current.Name ?? "").ToLowerInvariant();
                if (name.Contains("message") || name.Contains("type") || name.Contains("write") || name.Contains("reply")) return edit;
                best = edit;
            }
            return best;
        }

        // Brings the chat forward, types into its message box and presses Enter, then gives their window back.
        internal static bool Type(IntPtr window, string text)
        {
            if (Lockout.Active || !IsWindow(window) || string.IsNullOrWhiteSpace(text)) return false;
            IntPtr before = GetForegroundWindow();
            if (IsIconic(window)) ShowWindow(window, 9);
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            keybd_event(0x12, 0, 2, UIntPtr.Zero);
            SetForegroundWindow(window);
            try
            {
                AutomationElement box = MessageBox(window);
                if (box != null) box.SetFocus();
            }
            catch (Exception) { }
            Thread.Sleep(200);
            if (GetForegroundWindow() != window) return false;
            string flat = Regex.Replace(text, @"\s*[\r\n]+\s*", " ").Trim();
            List<Input> keys = new List<Input>();
            foreach (char letter in flat)
            {
                keys.Add(new Input { Type = 1, Data = new InputUnion { Key = new KeyInput { Scan = letter, Flags = UnicodeKey } } });
                keys.Add(new Input { Type = 1, Data = new InputUnion { Key = new KeyInput { Scan = letter, Flags = UnicodeKey | KeyUp } } });
            }
            keys.Add(new Input { Type = 1, Data = new InputUnion { Key = new KeyInput { Key = 0x0D } } });
            keys.Add(new Input { Type = 1, Data = new InputUnion { Key = new KeyInput { Key = 0x0D, Flags = KeyUp } } });
            uint sent = SendInput((uint)keys.Count, keys.ToArray(), Marshal.SizeOf(typeof(Input)));
            Thread.Sleep(250);
            if (before != IntPtr.Zero && before != window && IsWindow(before)) SetForegroundWindow(before);
            return sent == keys.Count;
        }

        internal static int InputSize { get { return Marshal.SizeOf(typeof(Input)); } }
    }
}
