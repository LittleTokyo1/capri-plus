using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed class ListenSegments
    {
        internal const int Frame = Ears.Rate * 30 / 1000;
        internal const int StartFrames = 3;
        internal const int EndFrames = 23;
        internal const int PrerollFrames = 10;
        internal const int LeastSamples = Ears.Rate / 2;
        internal const int MostSamples = Ears.Rate * 15;
        internal const double LeastLevel = 0.01;
        internal const double OverFloor = 3.0;

        private readonly List<short> pending = new List<short>();
        private readonly List<short[]> preroll = new List<short[]>();
        private readonly List<short> speech = new List<short>();
        private double floor = 0.002;
        private int loud;
        private int quiet;
        private bool talking;

        internal List<short[]> Feed(short[] samples)
        {
            List<short[]> done = new List<short[]>();
            if (samples == null) return done;
            pending.AddRange(samples);
            int at = 0;
            while (pending.Count - at >= Frame)
            {
                short[] frame = pending.GetRange(at, Frame).ToArray();
                at += Frame;
                short[] finished = Step(frame);
                if (finished != null) done.Add(finished);
            }
            pending.RemoveRange(0, at);
            return done;
        }

        internal static double Level(short[] frame)
        {
            if (frame.Length == 0) return 0;
            double sum = 0;
            foreach (short sample in frame) sum += (double)sample * sample;
            return Math.Sqrt(sum / frame.Length) / 32768.0;
        }

        private short[] Step(short[] frame)
        {
            double level = Level(frame);
            bool voiced = level >= Math.Max(LeastLevel, floor * OverFloor);
            if (!talking)
            {
                if (!voiced) floor = floor * 0.98 + level * 0.02;
                preroll.Add(frame);
                if (preroll.Count > PrerollFrames) preroll.RemoveAt(0);
                loud = voiced ? loud + 1 : 0;
                if (loud < StartFrames) return null;
                talking = true;
                quiet = 0;
                speech.Clear();
                foreach (short[] early in preroll) speech.AddRange(early);
                preroll.Clear();
                return null;
            }
            speech.AddRange(frame);
            quiet = voiced ? 0 : quiet + 1;
            if (quiet < EndFrames && speech.Count < MostSamples) return null;
            talking = false;
            loud = 0;
            short[] said = speech.ToArray();
            speech.Clear();
            return said.Length >= LeastSamples ? said : null;
        }
    }
}
