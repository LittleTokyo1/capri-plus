using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeClaudeToken(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (fresh.ClaudeToken != null) throw new Exception("A fresh install already has a Claude token.");
            if (Array.IndexOf(Tuning.Internal, "ClaudeToken") < 0) throw new Exception("His Claude token must stay out of her setting tool.");
            string fake = ClaudeToken.Prefix + "01-" + Guid.NewGuid().ToString("N") + "Smoke_Token-" + Guid.NewGuid().ToString("N");
            SmokeClaudeTokenPaste(fake);
            SmokeClaudeTokenStore(fake);
            SmokeClaudeTokenGive(fake);
            SmokeClaudeTokenLogs(fake);
            checks.Add("PASS long-lived Claude token: none by default and never by voice; a pasted token (also with export or set in front, or wrapped over lines) is kept only encrypted, clear removes it, junk keeps the old one; it goes to her hidden Claude runs (judge, school mail, plan jobs) only as an environment variable, never to a shell start, another plan account or her Remote Control sessions, and never reaches fork.json, activity.log or her action notes in plain text");
        }

        private static void SmokeClaudeTokenPaste(string fake)
        {
            if (ClaudeToken.Clean(fake) != fake || ClaudeToken.Clean("  \"" + fake + "\"  ") != fake) throw new Exception("A pasted token wasn't read.");
            if (ClaudeToken.Clean("export " + ClaudeToken.Variable + "=" + fake) != fake || ClaudeToken.Clean("set " + ClaudeToken.Variable + "=" + fake) != fake || ClaudeToken.Clean("$env:" + ClaudeToken.Variable + " = '" + fake + "'") != fake) throw new Exception("A token with the variable name in front wasn't read.");
            string wrapped = fake.Substring(0, 30) + "\r\n" + fake.Substring(30);
            if (ClaudeToken.Clean(wrapped) != fake) throw new Exception("A token wrapped over two lines wasn't read.");
            if (ClaudeToken.Clean("hello") != null || ClaudeToken.Clean("sk-ant-api03-" + Guid.NewGuid().ToString("N")) != null || ClaudeToken.Clean(ClaudeToken.Prefix + "01") != null || ClaudeToken.Clean(null) != null) throw new Exception("Something that isn't a Claude token was taken as one.");
        }

        private static void SmokeClaudeTokenStore(string fake)
        {
            string saved = ClaudeToken.Take(fake, null);
            if (saved == null || saved.Contains(fake) || Voice.Unprotect(saved) != fake) throw new Exception("The token wasn't kept encrypted.");
            if (ClaudeToken.Take("", saved) != saved || ClaudeToken.Take("   ", saved) != saved) throw new Exception("An empty field lost the saved token.");
            if (ClaudeToken.Take("junk", saved) != saved) throw new Exception("Junk in the field replaced the saved token.");
            if (ClaudeToken.Take(ClaudeToken.ClearWord, saved) != null || ClaudeToken.Take(" CLEAR ", saved) != null) throw new Exception("Clear didn't remove the token.");
            ForkSettings holding = new ForkSettings();
            holding.Tidy();
            holding.ClaudeToken = saved;
            string json;
            using (MemoryStream stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(ForkSettings)).WriteObject(stream, holding);
                json = Encoding.UTF8.GetString(stream.ToArray());
            }
            if (json.Contains(fake) || json.Contains(ClaudeToken.Prefix) || !json.Contains("ClaudeToken")) throw new Exception("fork.json would hold the token in plain text.");
        }

        private static void SmokeClaudeTokenGive(string fake)
        {
            ProcessStartInfo hidden = new ProcessStartInfo("claude", "-p hi") { UseShellExecute = false };
            if (!ClaudeToken.Give(hidden, null, fake) || hidden.EnvironmentVariables[ClaudeToken.Variable] != fake || hidden.Arguments.Contains(fake)) throw new Exception("A hidden Claude run didn't get the token as an environment variable.");
            ProcessStartInfo shown = new ProcessStartInfo("claude", "--remote-control x") { UseShellExecute = true };
            if (ClaudeToken.Give(shown, null, fake)) throw new Exception("A shell start (her Remote Control sessions) was given the token.");
            ProcessStartInfo other = new ProcessStartInfo("cmd.exe") { UseShellExecute = false };
            if (ClaudeToken.Give(other, @"C:\other-account", fake) || other.EnvironmentVariables[ClaudeToken.Variable] == fake) throw new Exception("A plan job on another Claude account got his token.");
            ProcessStartInfo none = new ProcessStartInfo("claude") { UseShellExecute = false };
            if (ClaudeToken.Give(none, null, null) || ClaudeToken.Give(none, null, "")) throw new Exception("A run was given an empty token.");
            ProjectEntry entry = ProjectRun.Entries(new[] { @"C:\x\demo" })[0];
            if (string.Join(" ", ProjectRun.Arguments(entry, null, "s-1", "run-1", new DateTime(2026, 11, 5), @"C:\h\hooks.json")).Contains(ClaudeToken.Prefix)) throw new Exception("Her session arguments hold the token.");
        }

        private static void SmokeClaudeTokenLogs(string fake)
        {
            if (ClaudeToken.Hide("got " + fake + " back").Contains(fake) || !ClaudeToken.Hide("got " + fake).Contains(ClaudeToken.Hidden) || ClaudeToken.Hide(null) != null || ClaudeToken.Hide("plain line") != "plain line") throw new Exception("A line with the token wasn't masked.");
            string folder = Path.Combine(Path.GetTempPath(), "capri-token-smoke-" + Guid.NewGuid().ToString("N"));
            string logWas = ActivityLog.FilePath;
            ForkSettings settingsWas = ForkSettings.Current;
            try
            {
                ForkSettings logging = new ForkSettings();
                logging.Tidy();
                logging.LogActions = true;
                ForkSettings.Current = logging;
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                ActivityLog.Write("smoke token", "set " + fake, "echoed " + fake);
                string written = File.ReadAllText(ActivityLog.FilePath);
                if (written.Contains(fake) || written.Contains(fake.Substring(0, 30)) || !written.Contains(ClaudeToken.Hidden)) throw new Exception("activity.log got the token: " + written);
            }
            finally
            {
                ActivityLog.FilePath = logWas;
                ForkSettings.Current = settingsWas;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }
    }
}
