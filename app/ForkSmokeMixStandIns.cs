using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private sealed class StandInMic : IMixMic
        {
            private readonly Func<double> clock;
            private readonly double opened;
            private readonly int rate, period;
            private readonly double hertz, level, fast, late;
            private long given;
            private readonly long click;
            internal volatile bool Lost, Disposed;

            internal StandInMic(Func<double> clock, int rate, double hertz, double level, double fast, double phase, long click, double late)
            {
                this.clock = clock;
                this.rate = rate;
                this.hertz = hertz;
                this.level = level;
                this.fast = fast;
                this.click = click;
                period = rate / 100;
                this.late = Math.Min(late, 0.9 * period / (rate * fast));
                opened = clock() - phase;
            }

            public int Rate { get { return rate; } }
            public int Period { get { return period; } }
            public WaitHandle Ready { get { return null; } }

            internal double HeardAt(long frame) { return opened + frame / (rate * fast); }

            private double Lands(long packet)
            {
                double shake = Math.Abs(Math.Sin(packet * 12.9898) * 43758.5453);
                return HeardAt((packet + 1) * period) + late * (shake - Math.Floor(shake));
            }

            internal double NextPacket { get { return Lands(given / period); } }

            public int Read(float[] into)
            {
                if (Lost) throw new MixLost("Stand-in mic unplugged (0x88890004).");
                double now = clock();
                int count = 0;
                while (count + period <= into.Length && Lands((given + count) / period) <= now) count += period;
                for (int i = 0; i < count; i++)
                {
                    long at = given + i;
                    into[i] = (float)(level * Math.Sin(2 * Math.PI * hertz * at / rate)) + (at == click ? 0.5f : 0f);
                }
                given += count;
                return count;
            }

            public void Dispose() { Disposed = true; }
        }

        private sealed class StandInCable : IMixCable
        {
            private readonly Func<double> clock;
            private readonly double opened;
            private readonly int rate, period;
            private readonly object gate = new object();
            private readonly List<float> kept = new List<float>();
            private readonly List<long> takenFrom = new List<long>();
            private readonly List<double> takenAt = new List<double>();
            private long played, pulls;
            internal int Starved;
            internal volatile bool Lost, Disposed;

            internal StandInCable(Func<double> clock, int rate)
            {
                this.clock = clock;
                this.rate = rate;
                period = rate / 100;
                opened = clock();
            }

            public int Rate { get { return rate; } }
            public int Period { get { return period; } }
            public WaitHandle Ready { get { return null; } }

            internal double NextTake { get { return opened + (Math.Floor((clock() - opened) * rate / period) + 1) * period / rate; } }

            internal double OpenedAt { get { return opened; } }

            private void Take()
            {
                long due = (long)Math.Floor((clock() - opened) * rate / period);
                for (; pulls < due; pulls++)
                {
                    long have = kept.Count - played, take = Math.Min(period, have);
                    takenFrom.Add(played);
                    takenAt.Add(opened + (double)(pulls + 1) * period / rate);
                    played += take;
                    if (take < period && kept.Count > 0) Starved++;
                }
            }

            public int Queued()
            {
                if (Lost) throw new MixLost("Stand-in cable gone (0x88890004).");
                lock (gate) { Take(); return (int)(kept.Count - played); }
            }

            public void Write(float[] mono, int count)
            {
                if (Lost) throw new MixLost("Stand-in cable gone (0x88890004).");
                lock (gate)
                {
                    Take();
                    for (int i = 0; i < count; i++) kept.Add(mono[i]);
                }
            }

            internal float[] Kept { get { lock (gate) return kept.ToArray(); } }

            internal double PlayedAt(long frame)
            {
                lock (gate)
                {
                    int found = takenFrom.BinarySearch(frame);
                    if (found < 0) found = ~found - 1;
                    while (found >= 0 && found + 1 < takenFrom.Count && takenFrom[found + 1] <= frame) found++;
                    return found < 0 || frame >= played ? double.NaN : takenAt[found];
                }
            }

            public void Dispose() { Disposed = true; }
        }

        private sealed class StandInDevices : IMixDevices
        {
            internal Func<double> Clock;
            internal MixDevice CableThere;
            internal readonly List<MixDevice> Mics = new List<MixDevice>();
            internal string DefaultMic;
            internal int MicRate = 48000, CableRate = 48000;
            internal double Level = 0.25, Fast = 1, Phase, Late;
            internal long Click = -1;
            internal readonly Dictionary<string, double> Hums = new Dictionary<string, double>();
            internal bool CableBusy;
            internal volatile bool FarMuted, FarUnread;
            internal double FarBoost;
            internal readonly List<StandInMic> Opened = new List<StandInMic>();
            internal readonly List<StandInCable> Cables = new List<StandInCable>();

            internal StandInDevices()
            {
                Stopwatch own = Stopwatch.StartNew();
                Clock = delegate { return own.Elapsed.TotalSeconds; };
            }

            internal Func<MixDevice> CableRead;

            public MixDevice Cable(string chosen) { return CableRead == null ? CableThere : CableRead(); }

            List<MixDevice> IMixDevices.Cables()
            {
                List<MixDevice> cables = new List<MixDevice>();
                if (CableThere != null) cables.Add(CableThere);
                return cables;
            }

            internal readonly List<string> Listeners = new List<string>();

            public List<string> Listening(string mic) { lock (Listeners) return new List<string>(Listeners); }

            public bool Boost(MixDevice cable, out double boost, out bool muted) { boost = FarBoost; muted = FarMuted; return cable != null && cable.Far != null && !FarUnread; }

            public MixDevice Mic(string chosen) { lock (Mics) return MicMix.MicFor(Mics, chosen, DefaultMic); }

            public IMixMic OpenMic(MixDevice mic)
            {
                double hum;
                if (!Hums.TryGetValue(mic.Id, out hum)) hum = 440;
                StandInMic made = new StandInMic(Clock, MicRate, hum, Level, Fast, Phase, Click, Late);
                lock (Opened) Opened.Add(made);
                return made;
            }

            public IMixCable OpenCable(MixDevice cable)
            {
                if (CableBusy) throw new MixLost("Stand-in cable busy (0x88890008).");
                StandInCable made = new StandInCable(Clock, CableRate);
                lock (Cables) Cables.Add(made);
                return made;
            }
        }

    }
}
