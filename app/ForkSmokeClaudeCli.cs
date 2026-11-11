using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeClaudeCli(List<string> checks)
        {
            Action<int> keptWait = ClaudeJob.Wait;
            try
            {
                int waits = 0;
                ClaudeJob.Wait = delegate { waits++; };
                int calls = 0;
                string got = ClaudeJob.Retried(delegate { if (++calls < ClaudeJob.CliTries) throw new Win32Exception(2, "The system cannot find the file specified"); return "ok"; });
                if (got != "ok" || calls != ClaudeJob.CliTries || waits != ClaudeJob.CliTries - 1) throw new Exception("A claude CLI call missing its exe for a moment (his 2026-11-11 11:01 mail job, during a Claude Code update) was not tried again: " + got + " " + calls + " " + waits);
                calls = 0;
                bool thrown = false;
                try { ClaudeJob.Retried(delegate { calls++; throw new Win32Exception(2, "The system cannot find the file specified"); }); }
                catch (Win32Exception) { thrown = true; }
                if (!thrown || calls != ClaudeJob.CliTries) throw new Exception("A claude CLI that stays missing was tried forever or swallowed: " + calls);
                calls = 0;
                thrown = false;
                try { ClaudeJob.Retried(delegate { calls++; throw new InvalidOperationException("other"); }); }
                catch (InvalidOperationException) { thrown = true; }
                if (!thrown || calls != 1) throw new Exception("A different CLI error was retried.");
            }
            finally
            {
                ClaudeJob.Wait = keptWait;
            }
            checks.Add("PASS claude cli retry: a background job's claude call that can't find the exe for a moment (a Claude Code update) is tried again up to " + ClaudeJob.CliTries + " times instead of losing the job, and any other error is not retried");
        }
    }
}
