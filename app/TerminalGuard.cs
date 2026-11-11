using System;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class TerminalGuard
    {
        internal static readonly string[] Terminals = { "WindowsTerminal", "OpenConsole", "conhost", "powershell", "pwsh", "cmd", "wsl", "bash", "claude", "node" };
        private static readonly Regex Bare = new Regex(@"^\W*(?:(?:yeah|yes|yep|yup|ya|ok|okay|sure|alright|right|fine|no|nope|nah|it|its|that|thats|this|looks?|good|great|perfect|nice|go|ahead|do|send|is|all|and|so|then|just|please|now|one|two|three|1|2|3|enter|continue|proceed|approve|accept|confirm|submit)\W*)+$", RegexOptions.IgnoreCase);
        private static readonly Regex Named = new Regex(@"\b(?:claude|terminal|console|powershell|cmd|command|code|session|shell|prompt)\b", RegexOptions.IgnoreCase);
        internal const string Line = "Your Claude Code terminal is in front, so I didn't type that into it: a bare yes there answers Claude Code, not a mail or a chat. Tell me which app or draft you meant, or say answer Claude Code and the words.";

        internal static bool Holds(string task, string program)
        {
            string said = (task ?? "").Replace("'", "").Replace("’", "");
            if (string.IsNullOrWhiteSpace(said) || Named.IsMatch(said) || !Bare.IsMatch(said)) return false;
            string front = (program ?? "").Trim();
            foreach (string one in Terminals) if (string.Equals(one, front, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
