using System;
using static StormEngine.Audio.SynthKit;

namespace StormEngine.Audio
{
    /// <summary>Sound designs for every effect and the weather ambiences (music lives in <see cref="MusicLibrary"/>).</summary>
    public static class SoundBank
    {
        public const int SfxRate = 44100;
        public const int AmbienceRate = 22050;

        static float[] Finish(float[] buf, int rate, double reverb, float peak, double room = 0.82)
        {
            if (reverb > 0) Reverb(buf, rate, reverb, room);
            Fade(buf, rate, 0.001, 0.03);
            Normalize(buf, peak);
            return buf;
        }

        // ------------------------------------------------------------------ UI

        public static float[] Click()
        {
            var b = Buffer(0.07, SfxRate);
            Glide(b, SfxRate, 0, 1500, 650, 0.03, 0.8, 0.0008, 110, 0.07);
            NoiseSweep(b, SfxRate, 0, 0.01, 0.25, 6000, 4000, 2, 0.7, t => Perc(t, 0.0005, 300), 11);
            return Finish(b, SfxRate, 0, 0.55f);
        }

        public static float[] Popup()
        {
            var b = Buffer(0.3, SfxRate);
            Glide(b, SfxRate, 0, 330, 880, 0.07, 0.7, 0.004, 22, 0.25);
            Glide(b, SfxRate, 0, 660, 1760, 0.07, 0.15, 0.004, 30, 0.2);
            return Finish(b, SfxRate, 0.12, 0.5f);
        }

        public static float[] Deny()
        {
            var b = Buffer(0.3, SfxRate);
            Glide(b, SfxRate, 0, 240, 190, 0.08, 0.7, 0.003, 24, 0.12);
            Glide(b, SfxRate, 0, 720, 570, 0.08, 0.18, 0.003, 30, 0.1);
            Glide(b, SfxRate, 0.1, 190, 150, 0.1, 0.7, 0.003, 18, 0.18);
            Glide(b, SfxRate, 0.1, 570, 450, 0.1, 0.15, 0.003, 24, 0.14);
            return Finish(b, SfxRate, 0.05, 0.5f);
        }

        public static float[] Buy()
        {
            var b = Buffer(0.7, SfxRate);
            Bell(b, SfxRate, 0, Midi(88), 0.7, 11, 2.0, 1.4, 0.6);
            Bell(b, SfxRate, 0.065, Midi(95), 0.8, 9, 2.0, 1.4, 0.6);
            NoiseSweep(b, SfxRate, 0.06, 0.05, 0.12, 9000, 7000, 2, 0.7, t => Perc(t, 0.001, 70), 3);
            return Finish(b, SfxRate, 0.18, 0.62f);
        }

        public static float[] Upgrade()
        {
            var b = Buffer(1.2, SfxRate);
            int[] notes = { 72, 76, 79, 84 };
            for (int i = 0; i < notes.Length; i++) Bell(b, SfxRate, i * 0.075, Midi(notes[i]), 0.6 + i * 0.1, 6, 3.0, 1.6, 1.0);
            return Finish(b, SfxRate, 0.28, 0.65f);
        }

        public static float[] Equip()
        {
            var b = Buffer(0.6, SfxRate);
            NoiseSweep(b, SfxRate, 0, 0.18, 0.5, 1800, 7000, 1, 1.4, t => Math.Sin(Math.PI * Math.Min(1, t / 0.18)), 21);
            Bell(b, SfxRate, 0.12, Midi(91), 0.5, 8, 2.0, 1.2, 0.5);
            return Finish(b, SfxRate, 0.2, 0.55f);
        }

        // ------------------------------------------------------------------ gameplay

        public static float[] Boost()
        {
            var b = Buffer(0.9, SfxRate);
            NoiseSweep(b, SfxRate, 0, 0.6, 0.9, 350, 4200, 1, 2.2, t => Math.Sin(Math.PI * Math.Min(1, t / 0.6)) * (1 - t / 0.9), 5);
            Glide(b, SfxRate, 0.02, 220, 1320, 0.35, 0.35, 0.01, 5, 0.6);
            Bell(b, SfxRate, 0.3, Midi(86), 0.3, 7, 3.5, 1.6, 0.6);
            return Finish(b, SfxRate, 0.22, 0.7f);
        }

        /// <summary>A bright, airy swell with a small "whoomp": exciting without deep bass.</summary>
        public static float[] StormStart()
        {
            var b = Buffer(1.8, SfxRate);
            NoiseSweep(b, SfxRate, 0, 1.4, 0.55, 300, 1400, 1, 0.9, t => Gate(t, 0.6, 0.2, 0.6), 9);
            NoiseSweep(b, SfxRate, 0, 1.2, 0.3, 900, 4200, 1, 1.5, t => Gate(t, 0.8, 0.1, 0.3), 10);
            Pad(b, SfxRate, 0, Midi(57), 0.4, 0.6, 0.3, 0.7, 2200, 12, 0, 1.0);
            Pad(b, SfxRate, 0, Midi(64), 0.3, 0.6, 0.3, 0.7, 2200, 12, 0, 1.0);
            Glide(b, SfxRate, 0.8, 220, 120, 0.2, 0.45, 0.003, 8, 0.6);
            return Finish(b, SfxRate, 0.25, 0.8f, 0.8);
        }

        public static float[] StormEnd()
        {
            var b = Buffer(2.2, SfxRate);
            int[] notes = { 72, 76, 79, 84, 88 };
            for (int i = 0; i < notes.Length; i++) Bell(b, SfxRate, i * 0.03, Midi(notes[i]), 0.45, 3.2, 3.0, 1.2, 2.0);
            Pad(b, SfxRate, 0, Midi(60), 0.25, 0.05, 0.4, 1.2, 2200, 8);
            return Finish(b, SfxRate, 0.35, 0.65f);
        }

        public static float[] Charged()
        {
            var b = Buffer(1.1, SfxRate);
            Bell(b, SfxRate, 0, Midi(88), 0.6, 6, 3.5, 1.8, 0.9);
            Bell(b, SfxRate, 0.11, Midi(93), 0.7, 5, 3.5, 1.8, 0.9);
            return Finish(b, SfxRate, 0.3, 0.55f);
        }

        public static float[] Discovery()
        {
            var b = Buffer(2.6, SfxRate);
            int[] run = { 72, 74, 76, 79, 81, 84, 86, 88, 91, 96 };
            for (int i = 0; i < run.Length; i++) Bell(b, SfxRate, i * 0.055, Midi(run[i]), 0.5, 5, 3.5, 2.0, 1.5);
            // Shimmer.
            var shimmer = Buffer(2.6, SfxRate);
            double ph = 0;
            for (int i = 0; i < shimmer.Length; i++)
            {
                double t = i / (double)SfxRate;
                double f = 2600 + 1400 * Math.Sin(t * 3.1);
                ph += f / SfxRate;
                shimmer[i] = (float)(Math.Sin(TwoPi * ph) * (0.5 + 0.5 * Math.Sin(TwoPi * 11 * t)) * Gate(t, 0.4, 0.6, 1.2) * 0.08);
            }
            for (int i = 0; i < b.Length; i++) b[i] += shimmer[i];
            Pad(b, SfxRate, 0.5, Midi(60), 0.2, 0.3, 0.5, 1.2, 2500, 10);
            Pad(b, SfxRate, 0.5, Midi(67), 0.2, 0.3, 0.5, 1.2, 2500, 10);
            return Finish(b, SfxRate, 0.42, 0.75f, 0.88);
        }

        public static float[] Goal()
        {
            var b = Buffer(1.2, SfxRate);
            Bell(b, SfxRate, 0, Midi(79), 0.6, 5, 3.0, 1.6, 1.0);
            Bell(b, SfxRate, 0.12, Midi(84), 0.7, 4, 3.0, 1.6, 1.1);
            return Finish(b, SfxRate, 0.3, 0.6f);
        }

        public static float[] LevelUp()
        {
            var b = Buffer(2.6, SfxRate);
            int[] arp = { 60, 64, 67 };
            for (int i = 0; i < arp.Length; i++)
            {
                Pad(b, SfxRate, i * 0.1, Midi(arp[i]), 0.35, 0.01, 0.07, 0.1, 3200, 6, 0.004);
                Bell(b, SfxRate, i * 0.1, Midi(arp[i] + 12), 0.35, 8, 2.0, 1.4, 0.5);
            }
            int[] chord = { 60, 64, 67, 72 };
            foreach (int n in chord) Pad(b, SfxRate, 0.32, Midi(n), 0.3, 0.02, 0.7, 0.9, 3000, 8, 0.006);
            foreach (int n in new[] { 84, 88, 91, 96 }) Bell(b, SfxRate, 0.32, Midi(n), 0.3, 3.5, 3.5, 1.8, 1.8);
            Glide(b, SfxRate, 0.32, 90, 45, 0.15, 0.6, 0.003, 9, 0.5);
            return Finish(b, SfxRate, 0.32, 0.8f, 0.86);
        }

        // ------------------------------------------------------------------ ambience

        /// <summary>Breezy meadow: soft wind, gusts and distant birds.</summary>
        public static float[] Calm(int seed = 12)
        {
            const int rate = AmbienceRate;
            const double length = 24;
            int loopLength = (int)(length * rate);
            int fade = (int)(3.0 * rate);
            var b = Buffer(length + 3.0, rate);
            var rng = new Random(seed);

            var lp = new Svf();
            var bp = new Svf();
            lp.Set(380, 0.6, rate);
            double gust = 0.5, gustTarget = 0.5;
            int counter = 0;
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                if (--counter <= 0)
                {
                    counter = (int)(rate * (1.5 + rng.NextDouble() * 3));
                    gustTarget = 0.25 + rng.NextDouble() * 0.75;
                }
                gust += (gustTarget - gust) * 0.00008;
                if ((i & 31) == 0) bp.Set(700 + 500 * gust + 200 * Math.Sin(t * 0.7), 5, rate);
                double w = rng.NextDouble() * 2 - 1;
                b[i] = (float)(lp.Low(w) * 0.9 * gust + bp.Band(w) * 0.06 * gust * gust);
            }

            // Birds.
            double at = 0.8;
            while (at < length + 1)
            {
                int notes = 2 + rng.Next(0, 4);
                double baseF = 2400 + rng.NextDouble() * 1800;
                double gain = 0.05 + rng.NextDouble() * 0.06;
                for (int n = 0; n < notes; n++)
                {
                    double start = at + n * (0.09 + rng.NextDouble() * 0.05);
                    double f0 = baseF * (1 + (rng.NextDouble() - 0.3) * 0.25);
                    Chirp(b, rate, start, f0, f0 * (0.8 + rng.NextDouble() * 0.5), gain);
                }
                at += 1.6 + rng.NextDouble() * 3.5;
            }
            Reverb(b, rate, 0.2, 0.8, 0.4);
            var loop = MakeLoop(b, loopLength, fade);
            Normalize(loop, 0.7f);
            return loop;
        }

        static void Chirp(float[] buf, int rate, double start, double f0, double f1, double gain)
        {
            int s0 = (int)(start * rate);
            int n = (int)(0.11 * rate);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j >= buf.Length) break;
                double t = i / (double)rate;
                double k = t / 0.11;
                double f = (f0 + (f1 - f0) * k) * (1 + 0.06 * Math.Sin(TwoPi * 32 * t));
                ph += f / rate;
                buf[j] += (float)(Math.Sin(TwoPi * ph) * Math.Sin(Math.PI * k) * gain);
            }
        }

        /// <summary>Steady rain: hiss, low roar and random droplets.</summary>
        public static float[] Rain(int seed = 31)
        {
            const int rate = AmbienceRate;
            const double length = 16;
            int loopLength = (int)(length * rate);
            int fade = (int)(2.0 * rate);
            var b = Buffer(length + 2.0, rate);
            var rng = new Random(seed);
            var hiss = new Svf();
            var roar = new Svf();
            hiss.Set(3200, 0.5, rate);
            roar.Set(420, 0.6, rate);
            double swell = 0.7;
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                swell = 0.75 + 0.25 * Math.Sin(t * 0.45) * Math.Sin(t * 0.17 + 1);
                double w = rng.NextDouble() * 2 - 1;
                b[i] = (float)((hiss.Band(w) * 0.35 + roar.Low(w) * 0.55) * swell);
            }
            int drops = (int)(length * 55);
            for (int d = 0; d < drops; d++)
            {
                double start = rng.NextDouble() * (length + 1.5);
                double f = 1600 + rng.NextDouble() * 4200;
                Glide(b, rate, start, f, f * 0.7, 0.01, 0.02 + rng.NextDouble() * 0.05, 0.0005, 160, 0.03);
            }
            Reverb(b, rate, 0.15, 0.78, 0.4);
            var loop = MakeLoop(b, loopLength, fade);
            Normalize(loop, 0.75f);
            return loop;
        }
    }
}
