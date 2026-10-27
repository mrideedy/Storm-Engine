using System;
using static StormEngine.Audio.SynthKit;

namespace StormEngine.Audio
{
    /// <summary>One background music track. Some are free, others unlock with a rewarded ad.</summary>
    public sealed class MusicTrack
    {
        public string Id;
        public string Name;
        public string Mood;
        public string Desc;
        public bool Free;
    }

    /// <summary>
    /// Soft, low-passed stereo music loops (ambient and cozy), rendered on demand. Everything is
    /// kept below ~5 kHz and has slow attacks, so it sits gently under the game instead of ringing.
    /// Pure C#: safe to render on a worker thread.
    /// </summary>
    public static class MusicLibrary
    {
        public const int Rate = 24000;

        public static readonly MusicTrack[] Tracks =
        {
            new MusicTrack { Id = "drift", Name = "Drifting Clouds", Mood = "Ambient", Free = true, Desc = "Slow, warm pads that float by." },
            new MusicTrack { Id = "hearth", Name = "Cozy Hearth", Mood = "Cozy", Free = true, Desc = "Mellow keys, round bass and a lazy beat." },
            new MusicTrack { Id = "rainy", Name = "Rainy Window", Mood = "Cozy", Free = false, Desc = "Soft piano with rain on the glass." },
            new MusicTrack { Id = "stars", Name = "Starlit Sky", Mood = "Ambient", Free = false, Desc = "Deep drones and distant twinkles." },
            new MusicTrack { Id = "meadow", Name = "Morning Meadow", Mood = "Cozy", Free = false, Desc = "Gentle guitar picking in the sun." },
        };

        public const string DefaultTrack = "drift";

        public static MusicTrack Find(string id)
        {
            foreach (var t in Tracks) if (t.Id == id) return t;
            return null;
        }

        /// <summary>Renders a track as interleaved stereo samples at <see cref="Rate"/>.</summary>
        public static float[] Render(string id)
        {
            switch (id)
            {
                case "hearth": return Hearth();
                case "rainy": return Rainy();
                case "stars": return Stars();
                case "meadow": return Meadow();
                default: return Drift();
            }
        }

        // ------------------------------------------------------------------ mixing helpers

        sealed class Mix
        {
            public readonly float[] L, R;
            public readonly int Loop;

            public Mix(double loopSeconds, double tailSeconds)
            {
                Loop = (int)(loopSeconds * Rate);
                L = Buffer(loopSeconds + tailSeconds, Rate);
                R = Buffer(loopSeconds + tailSeconds, Rate);
            }

            public float[] Layer() => new float[L.Length];

            /// <summary>Adds a mono layer with equal-power panning (-1 left .. 1 right).</summary>
            public void Add(float[] mono, double pan, double gain)
            {
                double a = (pan + 1) * Math.PI * 0.25;
                float gl = (float)(Math.Cos(a) * gain), gr = (float)(Math.Sin(a) * gain);
                for (int i = 0; i < mono.Length && i < L.Length; i++)
                {
                    L[i] += mono[i] * gl;
                    R[i] += mono[i] * gr;
                }
            }

            /// <summary>Adds a layer that drifts slowly between left and right.</summary>
            public void AddAutoPan(float[] mono, double width, double hz, double gain)
            {
                for (int i = 0; i < mono.Length && i < L.Length; i++)
                {
                    double pan = width * Math.Sin(TwoPi * hz * i / Rate);
                    double a = (pan + 1) * Math.PI * 0.25;
                    L[i] += (float)(mono[i] * Math.Cos(a) * gain);
                    R[i] += (float)(mono[i] * Math.Sin(a) * gain);
                }
            }

            /// <summary>Stereo reverb, master low-pass, tape-style saturation, loop fold and interleave.</summary>
            public float[] Finish(double reverb, double room, double lowpass, float peak)
            {
                Reverb(L, Rate, reverb, room, 0.45);
                Reverb(R, Rate, reverb, room, 0.45, 23);
                LowPass(L, lowpass);
                LowPass(R, lowpass);
                var l = WrapTail(L, Loop);
                var r = WrapTail(R, Loop);
                SoftClip(l, 1.25f);
                SoftClip(r, 1.25f);
                float max = 1e-6f;
                for (int i = 0; i < Loop; i++) max = Math.Max(max, Math.Max(Math.Abs(l[i]), Math.Abs(r[i])));
                float g = peak / max;
                var output = new float[Loop * 2];
                for (int i = 0; i < Loop; i++)
                {
                    output[i * 2] = l[i] * g;
                    output[i * 2 + 1] = r[i] * g;
                }
                return output;
            }
        }

        /// <summary>Two cascaded 12 dB low-passes: removes anything sharp or "pinchy".</summary>
        static void LowPass(float[] buf, double cutoff)
        {
            var a = new Svf();
            var b = new Svf();
            a.Set(cutoff, 0.62, Rate);
            b.Set(cutoff * 1.3, 0.55, Rate);
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)b.Low(a.Low(buf[i]));
        }

        // ------------------------------------------------------------------ voices

        /// <summary>Pure sine with a slow swell and gentle vibrato (soft choir / flute).</summary>
        static void Sine(float[] buf, double start, double freq, double gain, double attack, double hold, double release, double vibrato = 0.003)
        {
            int s0 = (int)(start * Rate);
            int n = (int)((attack + hold + release) * Rate);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)Rate;
                double f = freq * (1 + vibrato * Math.Sin(TwoPi * 4.6 * t) * Math.Min(1, t / 1.5));
                ph += f / Rate;
                double env = SmoothGate(t, attack, hold, release);
                buf[j] += (float)((Math.Sin(TwoPi * ph) + 0.08 * Math.Sin(TwoPi * ph * 2)) * env * gain);
            }
        }

        /// <summary>Gate with sine-shaped attack and release (no clicks, very soft edges).</summary>
        static double SmoothGate(double t, double attack, double hold, double release)
        {
            if (t < 0) return 0;
            if (t < attack) return 0.5 - 0.5 * Math.Cos(Math.PI * t / attack);
            if (t < attack + hold) return 1;
            double r = (t - attack - hold) / Math.Max(1e-4, release);
            return r >= 1 ? 0 : 0.5 + 0.5 * Math.Cos(Math.PI * r);
        }

        /// <summary>Mellow electric piano: FM with a low index, a soft tine and a long decay.</summary>
        static void EPiano(float[] buf, double start, double freq, double gain, double length = 2.6)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(length * Rate);
            double pc = 0, pm = 0, pt = 0;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)Rate;
                double env = Perc(t, 0.006, 1.1) * (t > length - 0.15 ? (length - t) / 0.15 : 1);
                double idx = 0.9 * Math.Exp(-t * 4.5) + 0.12;
                double v = Math.Sin(TwoPi * pc + idx * Math.Sin(TwoPi * pm));
                v += 0.12 * Math.Sin(TwoPi * pt) * Math.Exp(-t * 9);
                buf[j] += (float)(v * env * gain);
                pc += freq / Rate;
                pm += freq / Rate;
                pt += freq * 4.0 / Rate;
                if (pc > 1) pc -= 1;
                if (pm > 1) pm -= 1;
                if (pt > 1) pt -= 1;
            }
        }

        /// <summary>Felt piano: a few sine partials with a soft hammer and fast-fading overtones.</summary>
        static void Felt(float[] buf, double start, double freq, double gain, double length = 3.2)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(length * Rate);
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)Rate;
                double attack = Math.Min(1, t / 0.012);
                double tail = t > length - 0.2 ? (length - t) / 0.2 : 1;
                double v = Math.Sin(TwoPi * p1) * Math.Exp(-t * 0.9)
                         + 0.32 * Math.Sin(TwoPi * p2) * Math.Exp(-t * 2.2)
                         + 0.1 * Math.Sin(TwoPi * p3) * Math.Exp(-t * 4.5);
                buf[j] += (float)(v * attack * tail * gain);
                p1 += freq / Rate;
                p2 += freq * 2.0 / Rate;
                p3 += freq * 3.0 / Rate;
                if (p1 > 1) p1 -= 1;
                if (p2 > 1) p2 -= 1;
                if (p3 > 1) p3 -= 1;
            }
        }

        /// <summary>Plucked nylon string (Karplus-Strong) with a soft pick.</summary>
        static void Guitar(float[] buf, double start, double freq, double gain, int seed, double length = 2.8)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(length * Rate);
            int period = Math.Max(2, (int)Math.Round(Rate / freq));
            var line = new double[period];
            var rng = new Random(seed);
            // Low-passed noise burst for a soft, warm pick.
            double prev = 0;
            for (int i = 0; i < period; i++)
            {
                double x = rng.NextDouble() * 2 - 1;
                prev = prev * 0.6 + x * 0.4;
                line[i] = prev;
            }
            int idx = 0;
            double last = 0;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j >= buf.Length) break;
                double cur = line[idx];
                double avg = 0.5 * (cur + last) * 0.9965;
                last = cur;
                line[idx] = avg;
                if (++idx >= period) idx = 0;
                if (j < 0) continue;
                double t = i / (double)Rate;
                double tail = t > length - 0.25 ? (length - t) / 0.25 : 1;
                buf[j] += (float)(cur * gain * tail);
            }
        }

        /// <summary>Very soft, low kick.</summary>
        static void Kick(float[] buf, double start, double gain) => Glide(buf, Rate, start, 95, 44, 0.12, gain, 0.004, 11, 0.45);

        /// <summary>Round sine bass with a little warmth.</summary>
        static void Bass(float[] buf, double start, double freq, double gain, double length)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(length * Rate);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)Rate;
                double env = Math.Min(1, t / 0.02) * Math.Exp(-t * 0.9) * (t > length - 0.12 ? (length - t) / 0.12 : 1);
                ph += freq / Rate;
                double v = Math.Sin(TwoPi * ph);
                v = Math.Tanh(v * 1.6) / Math.Tanh(1.6);
                buf[j] += (float)(v * env * gain);
            }
        }

        /// <summary>Airy noise bed with slow swells (wind in the distance).</summary>
        static void Air(float[] buf, double gain, double cutoff, int seed)
        {
            var rng = new Random(seed);
            var f = new Svf();
            f.Set(cutoff, 0.6, Rate);
            for (int i = 0; i < buf.Length; i++)
            {
                double t = i / (double)Rate;
                double swell = 0.55 + 0.45 * Math.Sin(TwoPi * t / 13.0) * Math.Sin(TwoPi * t / 7.3 + 1.1);
                buf[i] += (float)(f.Low(rng.NextDouble() * 2 - 1) * swell * gain);
            }
        }

        /// <summary>Faint vinyl crackle, low-passed so it stays cozy.</summary>
        static void Vinyl(float[] buf, double gain, int seed)
        {
            var rng = new Random(seed);
            var f = new Svf();
            f.Set(2200, 0.7, Rate);
            for (int i = 0; i < buf.Length; i++)
            {
                double x = (rng.NextDouble() * 2 - 1) * 0.08;
                if (rng.NextDouble() < 0.0006) x += (rng.NextDouble() * 2 - 1) * 1.2;
                buf[i] += (float)(f.Low(x) * gain);
            }
        }

        /// <summary>Gentle rain on a window: soft hiss plus rounded drops.</summary>
        static void RainBed(float[] buf, double gain, int seed)
        {
            var rng = new Random(seed);
            var f = new Svf();
            f.Set(1800, 0.5, Rate);
            for (int i = 0; i < buf.Length; i++) buf[i] += (float)(f.Low(rng.NextDouble() * 2 - 1) * gain);
            double length = buf.Length / (double)Rate;
            int drops = (int)(length * 9);
            for (int d = 0; d < drops; d++)
            {
                double at = rng.NextDouble() * length;
                double fr = 900 + rng.NextDouble() * 1400;
                Glide(buf, Rate, at, fr, fr * 0.75, 0.02, gain * (0.4 + rng.NextDouble() * 0.6), 0.001, 90, 0.06);
            }
        }

        static double Hz(int midi) => Midi(midi);

        // ------------------------------------------------------------------ tracks

        /// <summary>Drifting Clouds: overlapping pad swells, a soft sine choir and a few far-off felt notes.</summary>
        static float[] Drift()
        {
            int[][] chords =
            {
                new[] { 50, 57, 61, 64, 66 }, // Dmaj9
                new[] { 47, 54, 57, 61, 64 }, // Bm11
                new[] { 43, 50, 54, 57, 61 }, // Gmaj9
                new[] { 45, 52, 57, 59, 64 }, // Asus2
                new[] { 50, 57, 61, 64, 69 }, // Dmaj9 (open)
                new[] { 42, 49, 54, 57, 61 }, // F#m7
                new[] { 43, 50, 55, 59, 62 }, // Gmaj7
                new[] { 45, 52, 57, 61, 64 }, // A6/9
            };
            const double chordLen = 8.0;
            double loop = chordLen * chords.Length;
            var mix = new Mix(loop, 7);
            var pads = mix.Layer();
            var low = mix.Layer();
            var choir = mix.Layer();
            var notes = mix.Layer();
            var air = mix.Layer();
            var rng = new Random(7);
            for (int c = 0; c < chords.Length; c++)
            {
                double t0 = c * chordLen;
                var ch = chords[c];
                for (int k = 1; k < ch.Length; k++)
                    Pad(pads, Rate, t0 - 1.2, Hz(ch[k]), 0.2, 3.2, chordLen - 2.2, 3.6, 760 + k * 60, 9);
                Sine(low, t0 - 0.8, Hz(ch[0] - 12), 0.28, 2.4, chordLen - 1.6, 2.4, 0);
                int top = ch[2 + rng.Next(0, 3)] + 12;
                Sine(choir, t0 + 1.0, Hz(top), 0.07, 2.6, chordLen - 3.5, 3.0);
                if (c % 2 == 1) Sine(choir, t0 + 4.0, Hz(ch[1] + 12), 0.05, 2.2, 2.0, 2.5);
                int count = 1 + rng.Next(0, 3);
                for (int n = 0; n < count; n++)
                {
                    double at = t0 + 0.5 + rng.NextDouble() * (chordLen - 1);
                    int note = ch[1 + rng.Next(0, ch.Length - 1)] + 12;
                    Felt(notes, at, Hz(note), 0.07 + rng.NextDouble() * 0.04, 4.0);
                }
            }
            Air(air, 0.05, 520, 3);
            Echo(notes, Rate, 0.75, 0.38, 0.35);
            mix.AddAutoPan(pads, 0.35, 0.021, 1.0);
            mix.Add(low, 0, 0.9);
            mix.AddAutoPan(choir, 0.5, 0.013, 1.0);
            mix.AddAutoPan(notes, 0.6, 0.031, 1.0);
            mix.AddAutoPan(air, 0.8, 0.008, 1.0);
            return mix.Finish(0.42, 0.9, 3600, 0.62f);
        }

        /// <summary>Cozy Hearth: mellow electric piano, round bass, a very soft kick and vinyl crackle.</summary>
        static float[] Hearth()
        {
            const double bpm = 74;
            double beat = 60 / bpm, bar = beat * 4;
            int[][] chords =
            {
                new[] { 51, 55, 58, 62 }, // Ebmaj7
                new[] { 48, 51, 55, 58, 62 }, // Cm9
                new[] { 44, 48, 51, 55 }, // Abmaj7
                new[] { 46, 50, 53, 55 }, // Bb6
            };
            int[] roots = { 39, 36, 32, 34 };
            const int bars = 16;
            double loop = bar * bars;
            var mix = new Mix(loop, 5);
            var keys = mix.Layer();
            var bass = mix.Layer();
            var drums = mix.Layer();
            var lead = mix.Layer();
            var bed = mix.Layer();
            var rng = new Random(21);
            int[] melody = { 7, -1, 9, 7, -1, 4, 2, -1, 4, -1, 7, -1, 9, 11, 9, -1 };
            for (int b = 0; b < bars; b++)
            {
                int c = b % chords.Length;
                double t0 = b * bar;
                // A few milliseconds of looseness keeps the keys human.
                foreach (int n in chords[c]) EPiano(keys, t0 + (rng.NextDouble() - 0.5) * 0.012, Hz(n + 12), 0.14, bar * 0.9);
                foreach (int n in chords[c])
                    if (n != chords[c][0]) EPiano(keys, t0 + beat * 2.5 + (rng.NextDouble() - 0.5) * 0.012, Hz(n + 12), 0.08, bar * 0.45);
                Bass(bass, t0, Hz(roots[c]), 0.42, beat * 1.8);
                Bass(bass, t0 + beat * 2, Hz(roots[c] + (b % 2 == 0 ? 7 : 12)), 0.32, beat * 1.6);
                if (b >= 2)
                {
                    Kick(drums, t0, 0.32);
                    Kick(drums, t0 + beat * 2.5, 0.2);
                    for (int s = 1; s < 4; s += 2)
                        NoiseSweep(drums, Rate, t0 + beat * s, 0.16, 0.045, 1400, 900, 1, 0.9, t => Perc(t, 0.004, 26), 300 + b * 4 + s);
                }
                if (b >= 4 && b % 4 != 3)
                    for (int s = 0; s < 4; s++)
                    {
                        int m = melody[(b % 4) * 4 + s];
                        if (m < 0) continue;
                        int note = 63 + m;
                        Sine(lead, t0 + s * beat + 0.02, Hz(note), 0.08, 0.06, beat * 0.9, 0.6, 0.005);
                    }
            }
            Vinyl(bed, 0.07, 9);
            Air(bed, 0.025, 420, 5);
            Echo(lead, Rate, beat * 0.75, 0.3, 0.3);
            mix.AddAutoPan(keys, 0.45, 1 / (bar * 2), 1.0);
            mix.Add(bass, 0, 1.0);
            mix.Add(drums, 0.05, 0.9);
            mix.AddAutoPan(lead, 0.3, 0.05, 1.0);
            mix.Add(bed, -0.2, 0.7);
            mix.Add(bed, 0.2, 0.7);
            return mix.Finish(0.3, 0.84, 3400, 0.64f);
        }

        /// <summary>Rainy Window: felt-piano arpeggios over a soft pad, with rain on the glass.</summary>
        static float[] Rainy()
        {
            const double bpm = 68;
            double beat = 60 / bpm, bar = beat * 4;
            int[][] chords =
            {
                new[] { 50, 57, 60, 64, 65 }, // Dm9
                new[] { 46, 53, 57, 60, 62 }, // Bbmaj7(9)
                new[] { 41, 48, 57, 60, 64 }, // Fmaj7
                new[] { 48, 55, 57, 62, 64 }, // C6/9
            };
            const int bars = 16;
            double loop = bar * bars;
            var mix = new Mix(loop, 6);
            var piano = mix.Layer();
            var pad = mix.Layer();
            var low = mix.Layer();
            var rain = mix.Layer();
            var rng = new Random(33);
            int[] pattern = { 0, 2, 3, 4, 3, 2, 1, 2 };
            for (int b = 0; b < bars; b++)
            {
                int c = (b / 2) % chords.Length;
                var ch = chords[c];
                double t0 = b * bar;
                for (int s = 0; s < 8; s++)
                {
                    if (b % 4 == 3 && s >= 6) continue;
                    int note = ch[1 + pattern[s] % (ch.Length - 1)] + 12;
                    double at = t0 + s * beat * 0.5 + (rng.NextDouble() - 0.5) * 0.02;
                    double vel = 0.1 + (s == 0 ? 0.05 : 0) + rng.NextDouble() * 0.03;
                    Felt(piano, at, Hz(note), vel, 3.0);
                }
                if (b % 2 == 0)
                {
                    Felt(piano, t0, Hz(ch[0]), 0.16, 4.5);
                    for (int k = 1; k < ch.Length; k++) Pad(pad, Rate, t0 - 0.6, Hz(ch[k]), 0.1, 2.0, bar * 2 - 1.6, 2.4, 700, 8);
                    Sine(low, t0, Hz(ch[0] - 12), 0.22, 1.2, bar * 2 - 1.2, 1.6, 0);
                }
            }
            RainBed(rain, 0.05, 44);
            Echo(piano, Rate, beat * 1.5, 0.25, 0.22);
            mix.AddAutoPan(piano, 0.35, 0.027, 1.0);
            mix.Add(pad, -0.3, 0.8);
            mix.Add(pad, 0.3, 0.8);
            mix.Add(low, 0, 0.9);
            mix.AddAutoPan(rain, 0.9, 0.011, 0.9);
            return mix.Finish(0.4, 0.88, 3800, 0.62f);
        }

        /// <summary>Starlit Sky: a deep drone, slow shimmering pads and distant soft twinkles.</summary>
        static float[] Stars()
        {
            int[][] chords =
            {
                new[] { 40, 52, 59, 63, 66 }, // Emaj9
                new[] { 37, 49, 56, 59, 63 }, // C#m9
                new[] { 45, 52, 56, 61, 64 }, // Amaj7(9)
                new[] { 47, 54, 59, 64, 66 }, // Bsus2
            };
            const double chordLen = 12.0;
            double loop = chordLen * chords.Length;
            var mix = new Mix(loop, 8);
            var drone = mix.Layer();
            var pads = mix.Layer();
            var twinkle = mix.Layer();
            var air = mix.Layer();
            var rng = new Random(71);
            Sine(drone, -2, Hz(28), 0.16, 2, loop + 2, 2, 0);
            Sine(drone, -2, Hz(40), 0.12, 2, loop + 2, 2, 0.001);
            for (int c = 0; c < chords.Length; c++)
            {
                double t0 = c * chordLen;
                var ch = chords[c];
                for (int k = 1; k < ch.Length; k++)
                    Pad(pads, Rate, t0 - 2.0, Hz(ch[k]), 0.16, 4.5, chordLen - 3.5, 5.0, 620 + k * 90, 11);
                int count = 3 + rng.Next(0, 3);
                for (int n = 0; n < count; n++)
                {
                    double at = t0 + rng.NextDouble() * chordLen;
                    int note = ch[1 + rng.Next(0, ch.Length - 1)] + 24;
                    Sine(twinkle, at, Hz(note), 0.035 + rng.NextDouble() * 0.02, 0.4, 0.3, 2.2, 0.002);
                }
            }
            Air(air, 0.04, 380, 17);
            Echo(twinkle, Rate, 1.1, 0.45, 0.4);
            mix.Add(drone, 0, 1.0);
            mix.AddAutoPan(pads, 0.5, 0.017, 1.0);
            mix.AddAutoPan(twinkle, 0.85, 0.043, 1.0);
            mix.AddAutoPan(air, 0.7, 0.006, 1.0);
            return mix.Finish(0.5, 0.92, 3200, 0.6f);
        }

        /// <summary>Morning Meadow: fingerpicked nylon guitar, a warm pad and a soft melody.</summary>
        static float[] Meadow()
        {
            const double bpm = 80;
            double beat = 60 / bpm, bar = beat * 4;
            int[][] chords =
            {
                new[] { 43, 50, 55, 59, 62 }, // G
                new[] { 40, 47, 52, 55, 59 }, // Em7
                new[] { 48, 52, 55, 59, 64 }, // Cmaj7
                new[] { 50, 57, 62, 64, 66 }, // D(add9)
            };
            const int bars = 16;
            double loop = bar * bars;
            var mix = new Mix(loop, 5);
            var guitar = mix.Layer();
            var pad = mix.Layer();
            var bass = mix.Layer();
            var lead = mix.Layer();
            var rng = new Random(5);
            int[] pick = { 0, 2, 3, 4, 1, 3, 2, 4 };
            int[] tune = { 71, 74, 76, 74, 71, 69, 67, -1 };
            for (int b = 0; b < bars; b++)
            {
                int c = b % chords.Length;
                var ch = chords[c];
                double t0 = b * bar;
                for (int s = 0; s < 8; s++)
                {
                    int note = ch[pick[s]];
                    double at = t0 + s * beat * 0.5 + (rng.NextDouble() - 0.5) * 0.015;
                    Guitar(guitar, at, Hz(note), 0.24 + (s % 4 == 0 ? 0.06 : 0), 1000 + b * 8 + s);
                }
                Pad(pad, Rate, t0 - 0.4, Hz(ch[2] + 12), 0.07, 1.4, bar - 0.8, 1.6, 900, 8);
                Pad(pad, Rate, t0 - 0.4, Hz(ch[3] + 12), 0.06, 1.4, bar - 0.8, 1.6, 900, 8);
                Bass(bass, t0, Hz(ch[0] - 12), 0.3, beat * 2.5);
                Bass(bass, t0 + beat * 2, Hz(ch[0] - 5), 0.2, beat * 1.8);
                if (b >= 8 && b % 2 == 0)
                    for (int s = 0; s < 4; s++)
                    {
                        int m = tune[((b / 2) % 2) * 4 + s];
                        if (m < 0) continue;
                        Sine(lead, t0 + s * beat, Hz(m), 0.06, 0.08, beat * 0.8, 0.5, 0.006);
                    }
            }
            Echo(lead, Rate, beat * 0.75, 0.3, 0.3);
            mix.Add(guitar, -0.25, 1.0);
            mix.AddAutoPan(guitar, 0.2, 0.05, 0.25);
            mix.Add(pad, 0.3, 0.9);
            mix.Add(bass, 0, 1.0);
            mix.AddAutoPan(lead, 0.4, 0.04, 1.0);
            return mix.Finish(0.32, 0.86, 4200, 0.63f);
        }
    }
}
