using System;
using System.Collections.Generic;
using System.Threading;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeMicMeter(List<string> checks)
        {
            int keptFresh = micFresh;
            int keptAt = micAt;
            try
            {
                micFresh = 60000;
                micPeak = 0;
                ForkLevel(0.035);
                double got = MicNow();
                if (ears != null && ears.Muted) { if (got != -2) throw new Exception("A muted mic did not say so: " + got); }
                else if (Math.Abs(got - 35) > 0.5) throw new Exception("The mic level was wrong: " + got);
                ForkLevel(0.005);
                if (!(ears != null && ears.Muted) && Math.Abs(MicNow() - 35) > 0.5) throw new Exception("A dip between words dropped the meter instead of holding the level (his 2026-11-09 report: 'as low as 5 while I speak'): " + MicNow());
                micPeakAt = Environment.TickCount - MicHoldMs - 50;
                if (!(ears != null && ears.Muted) && Math.Abs(MicNow() - 5) > 0.5) throw new Exception("The meter held a peak for too long: " + MicNow());
                ForkLevel(0.5);
                if (!(ears != null && ears.Muted) && MicNow() != 60) throw new Exception("A very loud mic was not clamped to the bar.");
                micFresh = -1;
                if (!(ears != null && ears.Muted) && MicNow() != -1) throw new Exception("A stale mic level still showed.");
                micFresh = 60000;
                micFresh = 60000;
                micAt = 0;
                if (!(ears != null && ears.Muted) && MicNow() != -1) throw new Exception("A mic that never spoke still showed a level.");
            }
            finally
            {
                micFresh = keptFresh;
                micAt = keptAt;
            }
            ForkSettings picked = new ForkSettings();
            picked.MicThreshold = 20;
            using (ForkSettingsForm form = new ForkSettingsForm(picked))
            {
                form.MicLevel = delegate { return 35.0; };
                form.Metered();
                if (form.MeterValue != 35 || !form.MeterText.Contains("35") || !form.MeterText.Contains("she hears this")) throw new Exception("A loud level read wrong on the meter: " + form.MeterText);
                form.MicLevel = delegate { return 5.0; };
                form.Metered();
                if (form.MeterValue != 5 || !form.MeterText.Contains("too quiet")) throw new Exception("A quiet level read wrong on the meter: " + form.MeterText);
                form.MicLevel = delegate { return 250.0; };
                form.Metered();
                if (form.MeterValue != 60) throw new Exception("The meter went past full.");
                form.MicLevel = delegate { return -2.0; };
                form.Metered();
                if (form.MeterValue != 0 || !form.MeterText.Contains("muted")) throw new Exception("A muted mic was shown as too quiet: " + form.MeterText);
                form.MicLevel = delegate { return -1.0; };
                form.Metered();
                if (form.MeterValue != 0 || !form.MeterText.Contains("voice chat")) throw new Exception("The meter did not say it needs voice chat: " + form.MeterText);
            }
            checks.Add("PASS mic level meter: voice chat's mic chunks give a level in the same units as the sensitivity slider, held at its peak for a third of a second so dips between words don't drop it (stale or silent mic reads as no level), and the Capri+ settings window shows it live under the slider with 'she hears this' or 'too quiet', clamped to the bar, and says to start voice chat when there is no level");
        }
    }
}
