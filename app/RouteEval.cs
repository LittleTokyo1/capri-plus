using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    // "Capri.exe --route-eval lines.txt out.txt": every line through her quick routes (no brain, nothing run), one result per
    // line, so a looser match can be measured against his real chat before it ships.
    internal static class RouteEval
    {
        internal static int Live(string input, string output)
        {
            try
            {
                StringBuilder found = new StringBuilder();
                foreach (string raw in File.ReadAllLines(input, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    found.Append(Route(line)).Append('\t').Append(line).Append('\n');
                }
                File.WriteAllText(output, found.ToString(), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception error)
            {
                try { File.WriteAllText(output, "error\t" + error.Message + "\n", new UTF8Encoding(false)); }
                catch (Exception) { }
                return 1;
            }
        }

        internal static string Route(string line)
        {
            string tool;
            ToolArguments args;
            if (!Hands.Heard(line, out tool, out args)) return "-";
            List<string> parts = new List<string> { tool };
            foreach (string part in new[] { args.Action, args.Name, args.Song, args.App, args.Site, args.Task })
                if (!string.IsNullOrEmpty(part)) parts.Add(part);
            return string.Join("|", parts.ToArray());
        }
    }
}
