using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class ClaudeToken
    {
        internal const string Variable = "CLAUDE_CODE_OAUTH_TOKEN";
        internal const string Prefix = "sk-ant-oat";
        internal const string Hidden = "[claude token]";
        internal const string ClearWord = "clear";
        private static readonly Regex Shape = new Regex(Prefix + @"[A-Za-z0-9_\-]{20,}");
        private static readonly Regex Any = new Regex(Prefix + @"[A-Za-z0-9_\-]*");
        private static readonly Regex Space = new Regex(@"\s+");

        internal static string Saved()
        {
            return Voice.Unprotect(ForkSettings.Current.ClaudeToken);
        }

        internal static string Clean(string pasted)
        {
            if (string.IsNullOrEmpty(pasted)) return null;
            Match token = Shape.Match(Space.Replace(pasted, ""));
            return token.Success ? token.Value : null;
        }

        internal static string Take(string pasted, string saved)
        {
            string text = (pasted ?? "").Trim();
            if (text.Length == 0) return saved;
            if (string.Equals(text, ClearWord, StringComparison.OrdinalIgnoreCase)) return null;
            string token = Clean(text);
            return token == null ? saved : Voice.Protect(token);
        }

        internal static bool Give(ProcessStartInfo start, string account)
        {
            return Give(start, account, Saved());
        }

        internal static bool Give(ProcessStartInfo start, string account, string token)
        {
            if (start == null || start.UseShellExecute || string.IsNullOrEmpty(token) || !string.IsNullOrWhiteSpace(account)) return false;
            start.EnvironmentVariables[Variable] = token;
            return true;
        }

        internal static string Hide(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Prefix, StringComparison.Ordinal) < 0) return text;
            return Any.Replace(text, Hidden);
        }
    }
}
