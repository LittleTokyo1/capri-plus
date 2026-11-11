using System;
using System.Globalization;

namespace CapriDesktop
{
    internal static class ToolClock
    {
        internal static string Line(DateTime now)
        {
            return "\nRight now it's " + now.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " (24-hour clock) where they are; as a start time that is " + now.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture) + ".";
        }
    }
}
