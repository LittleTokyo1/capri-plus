using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace CapriDesktop
{
    internal static class ListenEval
    {
        internal static int Live(string seconds, string output, string data)
        {
            StringBuilder found = new StringBuilder();
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
                LocalEars ears = ListenEars.Make(data);
                if (!ears.Installed)
                {
                    Stopwatch fetching = Stopwatch.StartNew();
                    ears.Install(delegate(string what, long got, long total) { });
                    found.Append("ears installed in ").Append(fetching.ElapsedMilliseconds / 1000).Append(" s\n");
                }
                found.Append("ears ready ").Append(ears.Ready).Append(", model ").Append(ears.Model).Append('\n');
                string clip = Path.Combine(data, "ears", LocalEars.Pack, "test_wavs", "0.wav");
                if (ears.Ready && File.Exists(clip))
                {
                    short[] samples = EarsEval.Samples(clip);
                    Stopwatch took = Stopwatch.StartNew();
                    string heard = ears.Hear(samples);
                    found.Append("clip ").Append(samples.Length / Ears.Rate).Append(" s heard in ").Append(took.ElapsedMilliseconds).Append(" ms: ").Append(heard).Append('\n');
                }
                int discord = LoopbackCapture.DiscordRoot();
                found.Append("discord root ").Append(discord).Append('\n');
                if (discord != 0)
                {
                    int frames = 0, packets = 0;
                    double peak = 0;
                    ListenSegments cut = new ListenSegments();
                    List<short[]> said = new List<short[]>();
                    object gate = new object();
                    using (LoopbackCapture loop = new LoopbackCapture(discord, delegate(short[] samples)
                    {
                        lock (gate)
                        {
                            packets++;
                            frames += samples.Length;
                            peak = Math.Max(peak, ListenSegments.Level(samples));
                            said.AddRange(cut.Feed(samples));
                        }
                    }))
                    {
                        loop.Start();
                        Thread.Sleep(Math.Max(1, Math.Min(600, int.Parse(seconds))) * 1000);
                        found.Append("loopback running ").Append(loop.Running).Append(", problem ").Append(loop.Problem ?? "none").Append('\n');
                    }
                    lock (gate)
                    {
                        found.Append("loopback packets ").Append(packets).Append(", samples ").Append(frames).Append(", peak ").Append(peak.ToString("0.000")).Append(", lines ").Append(said.Count).Append('\n');
                        foreach (short[] line in said)
                        {
                            Stopwatch took = Stopwatch.StartNew();
                            string heard = ears.Ready ? Hearing.Clean(ears.Hear(line)) : "";
                            found.Append("line ").Append(line.Length * 1000 / Ears.Rate).Append(" ms heard in ").Append(took.ElapsedMilliseconds).Append(" ms: ").Append(heard).Append('\n');
                        }
                    }
                }
                ears.Dispose();
                File.WriteAllText(output, found.ToString(), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception error)
            {
                found.Append("failed: ").Append(error.GetType().Name).Append(": ").Append(error.Message).Append('\n');
                File.WriteAllText(output, found.ToString(), new UTF8Encoding(false));
                return 1;
            }
        }
    }
}
