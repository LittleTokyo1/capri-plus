using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private const int FetchBytes = 256 * 1024;
        private const int FetchFirst = 64 * 1024;

        private static TcpListener StallingServer(byte[] body, ManualResetEvent finished, List<string> asked)
        {
            TcpListener server = new TcpListener(IPAddress.Loopback, 0);
            server.Start();
            ThreadPool.QueueUserWorkItem(delegate
            {
                int served = 0;
                while (true)
                {
                    TcpClient client;
                    try { client = server.AcceptTcpClient(); }
                    catch (Exception) { return; }
                    int number = ++served;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            using (client)
                            using (NetworkStream stream = client.GetStream())
                            {
                                StreamReader reader = new StreamReader(stream, Encoding.ASCII);
                                long from = 0;
                                string line;
                                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                                    if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) from = long.Parse(line.Substring(line.IndexOf('=') + 1).TrimEnd('-'));
                                lock (asked) asked.Add(number + ":" + from);
                                string head = from > 0
                                    ? "HTTP/1.1 206 Partial Content\r\nContent-Length: " + (body.Length - from) + "\r\nContent-Range: bytes " + from + "-" + (body.Length - 1) + "/" + body.Length + "\r\nConnection: close\r\n\r\n"
                                    : "HTTP/1.1 200 OK\r\nContent-Length: " + body.Length + "\r\nAccept-Ranges: bytes\r\nConnection: close\r\n\r\n";
                                byte[] bytes = Encoding.ASCII.GetBytes(head);
                                stream.Write(bytes, 0, bytes.Length);
                                if (number == 1)
                                {
                                    stream.Write(body, 0, FetchFirst);
                                    stream.Flush();
                                    finished.WaitOne(15000);
                                    return;
                                }
                                stream.Write(body, (int)from, body.Length - (int)from);
                            }
                        }
                        catch (Exception) { }
                    });
                }
            });
            return server;
        }

        private void SmokeFetchStall(List<string> checks)
        {
            byte[] body = new byte[FetchBytes];
            new Random(45).NextBytes(body);
            string sha;
            using (SHA256 hash = SHA256.Create()) sha = BitConverter.ToString(hash.ComputeHash(body)).Replace("-", "").ToLowerInvariant();
            string folder = Path.Combine(Path.GetTempPath(), "capri-fetch-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            int keptStall = Engine.StallSeconds;
            ManualResetEvent finished = new ManualResetEvent(false);
            List<string> asked = new List<string>();
            List<string> retries = new List<string>();
            TcpListener server = StallingServer(body, finished, asked);
            try
            {
                Engine.StallSeconds = 1;
                string url = "http://127.0.0.1:" + ((IPEndPoint)server.LocalEndpoint).Port + "/model.onnx";
                string target = Path.Combine(folder, "model.onnx");
                DateTime started = DateTime.Now;
                Engine.Fetch(url, target, body.Length, sha, "the test model", delegate { }, delegate { return false; }, delegate(string why) { retries.Add(why); });
                double took = (DateTime.Now - started).TotalSeconds;
                if (!File.Exists(target) || File.Exists(target + ".part") || !string.Equals(Engine.Hash(target), sha, StringComparison.OrdinalIgnoreCase)) throw new Exception("A download that stalled once did not finish with the right file.");
                if (asked.Count != 2 || asked[0] != "1:0" || asked[1] != "2:" + FetchFirst) throw new Exception("A stalled download did not resume from where it stopped: " + string.Join(", ", asked.ToArray()));
                if (retries.Count != 1 || !retries[0].Contains("0.1 of 0.3 MB") || !retries[0].Contains("timed out")) throw new Exception("The retry of a stalled download was not reported with how far it got and why: " + string.Join(" | ", retries.ToArray()));
                if (took > 10) throw new Exception("A stalled download took " + took.ToString("0") + " s to recover with a 1 s stall limit.");

                string stopped = Path.Combine(folder, "stopped.onnx");
                bool cancelled = false;
                try { Engine.Fetch(url, stopped, body.Length, sha, "the test model", delegate { }, delegate { return true; }, null); }
                catch (OperationCanceledException) { cancelled = true; }
                if (!cancelled || File.Exists(stopped)) throw new Exception("A download asked to stop did not stop.");
            }
            finally
            {
                Engine.StallSeconds = keptStall;
                finished.Set();
                server.Stop();
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
            if (Engine.StallSeconds != 60) throw new Exception("The stall limit is not 60 seconds by default.");
            checks.Add("PASS download stall: a download whose connection goes quiet is retried after the stall limit (60 s, 1 s in the test), resumes with a ranged request from the byte it stopped at, finishes with the right checksum, reports each retry with how far it got and why (the meaning model logs it as meaning  download retry, plus a line every 50 MB), and still stops when asked");
        }
    }
}
