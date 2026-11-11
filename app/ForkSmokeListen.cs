using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeListen(List<string> checks, string folder)
        {
            SmokeListenCut();
            Dictionary<uint, KeyValuePair<uint, string>> processes = new Dictionary<uint, KeyValuePair<uint, string>>();
            processes[100] = new KeyValuePair<uint, string>(1, "explorer.exe");
            processes[201] = new KeyValuePair<uint, string>(200, "Discord.exe");
            processes[200] = new KeyValuePair<uint, string>(100, "Discord.exe");
            processes[300] = new KeyValuePair<uint, string>(100, "League of Legends.exe");
            if (LoopbackCapture.Root(processes) != 200 || LoopbackCapture.Root(new Dictionary<uint, KeyValuePair<uint, string>>()) != 0) throw new Exception("Listening mode picked the wrong Discord process to hear.");
            ListenBook.Clear();
            DateTime at = new DateTime(2026, 11, 3, 23, 10, 0);
            if (ListenBook.Add(at, ListenBook.Friend, "ok") || !ListenBook.Add(at, ListenBook.Him, "aš rytoj dirbu nuo devynių") || !ListenBook.Add(at.AddMinutes(1), ListenBook.Friend, "Pieter got a new racing wheel")) throw new Exception("Listening mode kept a one-word line or dropped a real one.");
            List<ListenLine> taken = ListenBook.Take();
            string transcript = ListenBook.Transcript(taken, new List<string> { "Rudiminas", "Pieter" });
            if (ListenBook.Count != 0 || taken.Count != 2 || !transcript.Contains("In the call with him: Rudiminas, Pieter") || !transcript.Contains("23:10 Him: aš rytoj dirbu nuo devynių") || !transcript.Contains("23:11 a friend: Pieter got a new racing wheel") || !transcript.Contains("in English"))
                throw new Exception("What she overheard was framed wrong: " + transcript);
            LocalEars lithuanian = ListenEars.Make(Path.Combine(folder, "listen-ears"));
            if (lithuanian.Language != "lt" || lithuanian.Model != LocalEars.EuropeanPack) throw new Exception("Listening mode's ears are not the pack that hears Lithuanian.");
            if (new ForkSettings().ListenMode) throw new Exception("Listening mode was on by default.");
            foreach (string talk in new[] { "I start learning guitar this week and my exam is on Friday", "I start learning Lithuanian songs" })
                if (Tuning.Asked(talk, at)) throw new Exception("Plain talk was taken as a settings change: " + talk);
            SmokeOverheard(folder, transcript);
            ListenTrace.Line();
            foreach (string mine in new[] { "Capri, pause the music", "Kathy pause the music", "we push mid now", "", "kathy again" }) ListenTrace.Note(mine);
            string mic = ListenTrace.Line();
            if (mic != "4 lines from his mic, her name in 1, near her name: kathy" || ListenTrace.Line() != null) throw new Exception("Her listening trace counted his mic wrong or printed his words: " + mic);
            checks.Add("PASS listening trace: while listening mode is on, every 10 minutes one line counts what her ears heard from his mic, how many had her name and which single words were near her name (never his sentences), and each line with her name logs the moment it was heard");
            checks.Add("PASS listening mode (Capri+ toggle, off by default): Discord's own sound is cut into spoken lines (a click is not a line, a long talk is cut at 15 s), its root process is the one heard, overheard lines stay in memory and are framed with who is in his call and in English, her listening ears are the pack that hears Lithuanian, and what she learns only goes into People notes that already exist");
        }

        private static void SmokeListenCut()
        {
            ListenSegments cut = new ListenSegments();
            List<short[]> found = new List<short[]>();
            Random noise = new Random(7);
            Func<int, int, short[]> sound = delegate(int count, int loud)
            {
                short[] made = new short[count];
                for (int i = 0; i < count; i++) made[i] = (short)(loud > 0 ? Math.Sin(i * 0.05) * loud : noise.Next(-20, 20));
                return made;
            };
            List<short> stream = new List<short>();
            stream.AddRange(sound(Ears.Rate, 0));
            stream.AddRange(sound(Ears.Rate, 8000));
            stream.AddRange(sound(Ears.Rate * 2, 0));
            stream.AddRange(sound(Ears.Rate / 40, 8000));
            stream.AddRange(sound(Ears.Rate * 2, 0));
            short[] all = stream.ToArray();
            for (int at = 0; at < all.Length; at += 1600)
            {
                short[] chunk = new short[Math.Min(1600, all.Length - at)];
                Array.Copy(all, at, chunk, 0, chunk.Length);
                found.AddRange(cut.Feed(chunk));
            }
            if (found.Count != 1 || found[0].Length < Ears.Rate || found[0].Length > Ears.Rate * 5 / 2) throw new Exception("Listening mode cut Discord's sound wrong: " + found.Count + " lines" + (found.Count > 0 ? ", first " + found[0].Length + " samples" : ""));
            ListenSegments longTalk = new ListenSegments();
            List<short[]> cutLong = new List<short[]>();
            short[] talk = sound(Ears.Rate * 20, 8000);
            for (int at = 0; at < talk.Length; at += 1600)
            {
                short[] chunk = new short[1600];
                Array.Copy(talk, at, chunk, 0, 1600);
                cutLong.AddRange(longTalk.Feed(chunk));
            }
            if (cutLong.Count != 1 || cutLong[0].Length < ListenSegments.MostSamples) throw new Exception("A long stretch of talk was not cut at 15 seconds: " + cutLong.Count);
        }

        private static void SmokeOverheard(string folder, string transcript)
        {
            string vault = Path.Combine(folder, "listen-vault");
            if (Directory.Exists(vault)) Directory.Delete(vault, true);
            Directory.CreateDirectory(Path.Combine(vault, "People"));
            File.WriteAllText(Path.Combine(vault, "me.md"), "# me\n- Kasper\n");
            string rudce = Path.Combine(vault, "People", "Rudiminas.md");
            File.WriteAllText(rudce, "Rudiminas is a friend of Kasper.\n- Also called: Rudce\n");
            string keptRoot = Vault.Root;
            Func<string, string> keptExtract = PeopleNotes.Extract;
            string asked = null;
            try
            {
                Vault.Root = vault;
                PeopleNotes.Extract = delegate(string frame) { asked = frame; return "Rudiminas | Plays | Tarkov\nZygimantas | Plays | Chess"; };
                PeopleResult result = PeopleNotes.Overheard(null, transcript, new DateTime(2026, 11, 3, 23, 20, 0));
                if (asked == null || !asked.Contains("Overheard voice chat")) throw new Exception("Listening mode's memory pass did not get what she overheard.");
                if (!PeopleNotes.OverheardRules.Contains("a friend line may give a fact about that friend only when it names") || !PeopleNotes.OverheardRules.Contains("Lithuanian")) throw new Exception("Her memory judge is not told how to read overheard voice chat.");
                if (result == null || !File.ReadAllText(rudce).Contains("Tarkov")) throw new Exception("A fact she overheard about a friend in his notes was not kept.");
                if (File.Exists(Path.Combine(vault, "People", "Zygimantas.md")) || !result.Held.Exists(delegate(string held) { return held.StartsWith("Zygimantas", StringComparison.Ordinal); }))
                    throw new Exception("Listening mode made a People note for someone who is not in his notes.");
            }
            finally
            {
                Vault.Root = keptRoot;
                PeopleNotes.Extract = keptExtract;
            }
            Directory.Delete(vault, true);
        }
    }
}
