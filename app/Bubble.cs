using System;
using System.Drawing;

namespace CapriDesktop
{
    internal static class Bubble
    {
        internal const string Sys = "> sys: ";
        internal const string Discord = "> discord: ";
        internal const string Failed = "(failed)";
        private const string DiscordNote = "SENT ON DISCORD";
        private const string FromNote = "FROM DISCORD";

        internal static bool System(string prompt, string heard)
        {
            return prompt == Sys && heard != null && heard.StartsWith("(", StringComparison.Ordinal);
        }

        internal static bool Alert(string heard)
        {
            return heard == "(reminder)" || heard == "(now)" || (heard ?? "").StartsWith("(in ", StringComparison.Ordinal);
        }

        internal static Color Rail(string prompt, string heard)
        {
            if (prompt == Discord) return Kit.Violet;
            if (!System(prompt, heard)) return Look.Red;
            return heard == Failed ? Look.Red : Look.Yellow;
        }

        internal static Color Edge(string prompt, string heard)
        {
            if (!System(prompt, heard)) return Look.Cyan;
            if (heard == Failed) return Look.Red;
            return Alert(heard) ? Look.Yellow : Look.Cyan;
        }

        internal static string Footer(string prompt, string heard)
        {
            if (prompt != Discord) return null;
            return (heard ?? "").StartsWith("(from ", StringComparison.Ordinal) ? FromNote : DiscordNote;
        }

        internal static float Extra(string prompt, string heard, float line)
        {
            return Footer(prompt, heard) == null ? 0 : line;
        }

        internal static void Foot(Graphics graphics, string prompt, string heard, RectangleF panel, float s)
        {
            string note = Footer(prompt, heard);
            if (note == null) return;
            using (Font small = Look.Mono(12 * s, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush dim = new SolidBrush(Look.Dim))
            {
                SizeF size = graphics.MeasureString(note, small, PointF.Empty, StringFormat.GenericTypographic);
                graphics.DrawString(note, small, dim, panel.Right - 14 * s - size.Width, panel.Bottom - 25 * s, StringFormat.GenericTypographic);
            }
        }

        internal static string Outcome(string result, string worked)
        {
            string said = result ?? "";
            bool failed = said.Length == 0 || said.StartsWith("I couldn't", StringComparison.Ordinal) || said.StartsWith("Google didn't", StringComparison.Ordinal) || said.StartsWith("That area isn't", StringComparison.Ordinal);
            return failed ? Failed : worked;
        }
    }
}
