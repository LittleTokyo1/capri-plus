using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CapriDesktop
{
    // A small CS per minute strip in the very top-left corner of the League game (his pick). Click-through, never takes focus,
    // only while the game (or one of her own windows) is in front.
    internal sealed class CsHud : Overlay
    {
        internal const string Game = "League of Legends";
        internal static Func<Rectangle> GameWindow = FindGame;
        private static readonly uint Mine = (uint)Process.GetCurrentProcess().Id;
        private string shown = "";
        private Point placed = new Point(int.MinValue, int.MinValue);
        internal int Drawn;

        [StructLayout(LayoutKind.Sequential)] private struct Box { internal int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Box box);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        internal CsHud() : base("Capri CS per minute") { }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.ExStyle |= 0x00000020;
                return value;
            }
        }

        internal string Shown { get { return Visible ? shown : ""; } }

        internal static string Label(double perMinute)
        {
            return "CS/MIN " + Math.Max(0, perMinute).ToString("0.0", CultureInfo.InvariantCulture);
        }

        // The very top-left corner of the game.
        internal static Point Spot(Rectangle game, Size size)
        {
            return new Point(game.Left, game.Top);
        }

        // In front means the game itself, or one of her own windows (her bubble or a toast must not hide the strip).
        private static bool InFront(IntPtr window)
        {
            IntPtr front = GetForegroundWindow();
            if (front == window) return true;
            uint owner;
            GetWindowThreadProcessId(front, out owner);
            return owner != 0 && owner == Mine;
        }

        // The game's window while it is in front, or an empty rectangle.
        private static Rectangle FindGame()
        {
            Process[] found = Process.GetProcessesByName(Game);
            try
            {
                foreach (Process process in found)
                {
                    IntPtr window = process.MainWindowHandle;
                    Box box;
                    if (window == IntPtr.Zero || !InFront(window) || IsIconic(window) || !GetWindowRect(window, out box)) continue;
                    if (box.Right - box.Left < 200 || box.Bottom - box.Top < 200) continue;
                    return Rectangle.FromLTRB(box.Left, box.Top, box.Right, box.Bottom);
                }
            }
            finally { foreach (Process process in found) process.Dispose(); }
            return Rectangle.Empty;
        }

        internal void Show(double perMinute)
        {
            Rectangle game = GameWindow();
            if (game.Width <= 0) { Stop(); return; }
            float s = Math.Max(0.7f, game.Height / 1080f);
            string text = Label(perMinute);
            if (Visible && text == shown && Spot(game, Size) == placed) return;
            using (Font font = Look.Mono(13 * s, FontStyle.Regular, GraphicsUnit.Pixel, text))
            {
                float wide = text.Length * Look.CharWidth(font) + 22 * s;
                float tall = 22 * s;
                float margin = 2 * s;
                Size size = new Size((int)Math.Ceiling(wide + margin * 2), (int)Math.Ceiling(tall + margin * 2));
                RectangleF plate = new RectangleF(margin, margin, wide, tall);
                Bitmap image = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(image))
                using (Brush ink = new SolidBrush(Neon.White))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Kit.Panel(graphics, plate, s, 5 * s, 3 * s, 10 * s, 6 * s);
                    graphics.DrawString(text, font, ink, plate.Left + 11 * s, plate.Top + (tall - 15 * s) / 2f, StringFormat.GenericTypographic);
                }
                shown = text;
                placed = Spot(game, size);
                Drawn++;
                Present(image, placed);
            }
        }

        internal void Stop()
        {
            shown = "";
            placed = new Point(int.MinValue, int.MinValue);
            if (Visible) Hide();
        }
    }
}
