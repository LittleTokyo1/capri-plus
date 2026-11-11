using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CapriDesktop
{
    internal static class PhoneShortcuts
    {
        internal const int MostShare = 8 * 1024 * 1024;
        internal const int MostShareText = 2000;
        internal const string Device = "Shortcuts";
        internal static readonly string[] Events = { "home", "left", "alarm", "focus", "sleep", "steps" };
        private const int MostSteps = 200000;
        private const int MostName = 40;

        internal static bool IsJson(string type) { return (type ?? "").IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0; }

        internal static string Value(string body, bool json, string name) { return json ? JsonField(body, name) : PhoneServer.Field(body, name); }

        internal static bool Yes(string value)
        {
            string asked = (value ?? "").Trim().ToLowerInvariant();
            return asked == "1" || asked == "true" || asked == "yes" || asked == "on";
        }

        internal static bool EventValue(string kind, string value, out string normal)
        {
            normal = null;
            string asked = (value ?? "").Trim();
            if (kind == "home" || kind == "left" || kind == "alarm") return true;
            if (kind == "focus")
            {
                string lower = asked.ToLowerInvariant();
                if (Yes(lower)) { normal = "on"; return true; }
                if (lower == "0" || lower == "false" || lower == "no" || lower == "off") { normal = "off"; return true; }
                return false;
            }
            if (kind != "sleep" && kind != "steps") return false;
            double number;
            if (!double.TryParse(asked.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return false;
            if (kind == "sleep" && number >= 0 && number <= 24) { normal = Math.Round(number, 1).ToString(CultureInfo.InvariantCulture); return true; }
            if (kind == "steps" && number >= 0 && number <= MostSteps) { normal = Math.Round(number).ToString(CultureInfo.InvariantCulture); return true; }
            return false;
        }

        internal static string Name(string name)
        {
            StringBuilder kept = new StringBuilder();
            foreach (char one in (name ?? "").Trim())
                if ((char.IsLetterOrDigit(one) || one == ' ' || one == '-') && kept.Length < MostName) kept.Append(one);
            return kept.ToString().Trim();
        }

        internal static string JsonField(string body, string name)
        {
            string text = body ?? "";
            int at = Skip(text, 0);
            if (at >= text.Length || text[at] != '{') return null;
            at++;
            while (at < text.Length)
            {
                at = Skip(text, at);
                if (at >= text.Length || text[at] == '}') return null;
                if (text[at] == ',') { at++; continue; }
                if (text[at] != '"') return null;
                string key = Quoted(text, ref at);
                at = Skip(text, at);
                if (key == null || at >= text.Length || text[at] != ':') return null;
                at = Skip(text, at + 1);
                if (at >= text.Length) return null;
                string value = null;
                if (text[at] == '"') { value = Quoted(text, ref at); if (value == null) return null; }
                else if (text[at] == '{' || text[at] == '[') { if (!Nested(text, ref at)) return null; }
                else
                {
                    int end = at;
                    while (end < text.Length && text[end] != ',' && text[end] != '}') end++;
                    value = text.Substring(at, end - at).Trim();
                    if (value == "null") value = null;
                    at = end;
                }
                if (key == name) return value;
            }
            return null;
        }

        private static int Skip(string text, int at)
        {
            while (at < text.Length && char.IsWhiteSpace(text[at])) at++;
            return at;
        }

        private static string Quoted(string text, ref int at)
        {
            StringBuilder value = new StringBuilder();
            int i = at + 1;
            while (i < text.Length)
            {
                char one = text[i];
                if (one == '"') { at = i + 1; return value.ToString(); }
                if (one != '\\') { value.Append(one); i++; continue; }
                if (i + 1 >= text.Length) return null;
                char next = text[i + 1];
                if (next == 'u')
                {
                    int code;
                    if (i + 5 >= text.Length || !int.TryParse(text.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) return null;
                    value.Append((char)code);
                    i += 6;
                    continue;
                }
                value.Append(next == 'n' ? '\n' : next == 'r' ? '\r' : next == 't' ? '\t' : next == 'b' ? '\b' : next == 'f' ? '\f' : next);
                i += 2;
            }
            return null;
        }

        private static bool Nested(string text, ref int at)
        {
            int depth = 0;
            int i = at;
            while (i < text.Length)
            {
                char one = text[i];
                if (one == '"')
                {
                    int inside = i;
                    if (Quoted(text, ref inside) == null) return false;
                    i = inside;
                    continue;
                }
                if (one == '{' || one == '[') depth++;
                else if (one == '}' || one == ']') { depth--; if (depth == 0) { at = i + 1; return true; } }
                i++;
            }
            return false;
        }
    }

    internal static class PhoneFacts
    {
        private const int FocusHours = 12;
        private static readonly object gate = new object();
        private static readonly Dictionary<string, KeyValuePair<DateTime, string>> facts = new Dictionary<string, KeyValuePair<DateTime, string>>();

        internal static void Note(string kind, string value, string name, DateTime now)
        {
            string clock = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            string fact;
            if (kind == "sleep") fact = "slept " + value + " h";
            else if (kind == "steps") fact = value + " steps today";
            else if (kind == "alarm") fact = "alarm stopped at " + clock;
            else if (kind == "focus") fact = "Focus " + (string.IsNullOrEmpty(name) ? "" : name + " ") + value + " since " + clock;
            else if (kind == "home") fact = "came home at " + clock;
            else if (kind == "left") fact = "left home at " + clock;
            else return;
            string slot = kind == "home" || kind == "left" ? "where" : kind;
            lock (gate) facts[slot] = new KeyValuePair<DateTime, string>(now, fact);
        }

        internal static List<string> Lines(DateTime now)
        {
            List<string> lines = new List<string>();
            lock (gate)
                foreach (string slot in new[] { "sleep", "steps", "alarm", "focus", "where" })
                {
                    KeyValuePair<DateTime, string> fact;
                    if (!facts.TryGetValue(slot, out fact) || fact.Key > now) continue;
                    bool fresh = slot == "focus" ? (now - fact.Key).TotalHours < FocusHours : fact.Key.Date == now.Date;
                    if (fresh) lines.Add(fact.Value);
                }
            return lines;
        }

        internal static void Clear() { lock (gate) facts.Clear(); }
    }

    internal static class PhoneShare
    {
        internal const int KeepSeconds = 120;
        internal const string Asked = "I shared a picture from my phone.";
        internal const string Wonder = " What do you think?";
        internal const string Note = "(The picture attached is one he shared from his iPhone, not his screen. Look at it and answer about it.)\n";
        private static readonly object gate = new object();
        private static byte[] picture;
        private static DateTime keptAt = DateTime.MinValue;

        internal static bool Jpeg(byte[] bytes) { return bytes != null && bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF; }

        internal static void Keep(byte[] jpeg, DateTime now)
        {
            lock (gate) { picture = Jpeg(jpeg) ? jpeg : null; keptAt = now; }
        }

        internal static byte[] Take(DateTime now)
        {
            lock (gate)
            {
                byte[] got = picture != null && Math.Abs((now - keptAt).TotalSeconds) < KeepSeconds ? picture : null;
                picture = null;
                return got;
            }
        }
    }
}
