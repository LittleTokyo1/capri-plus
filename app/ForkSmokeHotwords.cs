using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeHotwords(List<string> checks)
        {
            string vocab = Hotwords.Vocab(new[] { "<unk> 0", "\u2581t 1", "\u2581C 140", "ri 201", "broken", "x y" });
            if (vocab != "<unk> -0.6931\n\u2581t -1.0986\n\u2581C -4.9558\nri -5.3132\n") throw new Exception("Her hotword vocabulary was written wrong: " + vocab.Replace("\n", " | "));
            Func<List<string>> peopleWas = Hotwords.People;
            string[] sitesWas = ForkSettings.Current.SchoolSites;
            try
            {
                Hotwords.People = delegate { return new List<string>(); };
                ForkSettings.Current.SchoolSites = new[] { "CoursePortal", "MySchool", "StudyPortal" };
                string heardList = Hotwords.List();
                if (heardList != "Capri\ncapri\nHey Capri\nCoursePortal\nMySchool\nStudyPortal\n") throw new Exception("Her hotword list changed, his school sites are missing from it, or Outlook is boosted again (she heard a bare Outlook nine times he never said, 2026-11-07).");
                Hotwords.People = delegate { return new List<string> { "Rudce", "Rudiminas", "Capri", "Kęstas", "Casey de Vos", "x", "casey", "my sister", "My sister", "Elena and Felix" }; };
                if (Hotwords.List() != "Capri\ncapri\nHey Capri\nCoursePortal\nMySchool\nStudyPortal\nRudce\nRudiminas\nCasey de Vos\n") throw new Exception("Names of his people were not added to her hotwords, or one her ears cannot spell got in: " + Hotwords.List().Replace("\n", " / "));
            }
            finally { Hotwords.People = peopleWas; ForkSettings.Current.SchoolSites = sitesWas; }
            string model = Path.Combine(Path.GetTempPath(), "capri-hotwords-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(model);
            Func<bool> onWas = Hotwords.On;
            IntPtr config = Marshal.AllocHGlobal(4096);
            List<IntPtr> held = new List<IntPtr>();
            Func<string, IntPtr> keep = delegate(string value) { IntPtr pointer = Marshal.StringToHGlobalAnsi(value); held.Add(pointer); return pointer; };
            try
            {
                for (int at = 0; at < 4096; at += 8) Marshal.WriteInt64(config, at, 0);
                Hotwords.On = delegate { return false; };
                if (Hotwords.Tune(config, model, keep) || Marshal.ReadIntPtr(config, Hotwords.DecodingAt) != IntPtr.Zero) throw new Exception("Hotwords changed her ears with the setting off.");
                Hotwords.On = delegate { return true; };
                if (Hotwords.Tune(config, model, keep) || Marshal.ReadIntPtr(config, Hotwords.DecodingAt) != IntPtr.Zero) throw new Exception("Hotwords were set up for a model with no tokens.");
                File.WriteAllText(Path.Combine(model, "tokens.txt"), "<unk> 0\n\u2581C 1\nap 2\nri 3\n");
                if (!Hotwords.Tune(config, model, keep)) throw new Exception("Hotwords were not set up for her model.");
                string unit = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(config, Hotwords.ModelingUnitAt)), decoding = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(config, Hotwords.DecodingAt)), list = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(config, Hotwords.FileAt)), bpe = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(config, Hotwords.BpeVocabAt));
                float score = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(config, Hotwords.ScoreAt)), 0);
                if (unit != "bpe" || decoding != "modified_beam_search" || Marshal.ReadInt32(config, Hotwords.PathsAt) != Hotwords.Paths || score != Hotwords.Score || !File.Exists(list) || !File.Exists(bpe) || File.ReadAllText(list) != Hotwords.List())
                    throw new Exception("Her ears' hotword settings landed in the wrong places: " + unit + " / " + decoding + " / " + score);
                if (Marshal.ReadIntPtr(config, 128) != IntPtr.Zero || Marshal.ReadIntPtr(config, 104) != IntPtr.Zero) throw new Exception("Hotwords wrote over her model settings.");
            }
            finally
            {
                Hotwords.On = onWas;
                Marshal.FreeHGlobal(config);
                foreach (IntPtr pointer in held) Marshal.FreeHGlobal(pointer);
                try { Directory.Delete(model, true); } catch (Exception) { }
            }
            if (!new ForkSettings().EarsHotwords) throw new Exception("Hotwords did not start on after they were measured (2 of 32 to 32 of 32 clips).");
            checks.Add("PASS hotwords for her name: on by default after the clip measurement; when on, her ears get beam search with Capri, capri and Hey Capri boosted from a vocabulary written next to her model from its tokens (sherpa-onnx 1.13.8 offsets 136, 144, 528 to 552), nothing for a model without tokens, and her model settings untouched");
        }
    }
}