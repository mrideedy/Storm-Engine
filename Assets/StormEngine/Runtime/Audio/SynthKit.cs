using System;

namespace StormEngine.Audio
{
    /// <summary>
    /// Small offline synthesizer used to render every sound in the game at startup: FM bells,
    /// plucks, detuned pads, filtered noise, a state-variable filter and a Freeverb-style reverb.
    /// Pure C# with no Unity calls, so rendering can run on a worker thread.
    /// </summary>
    public static class SynthKit
    {
        public const double TwoPi = Math.PI * 2;

        public static float[] Buffer(double seconds, int rate) => new float[Math.Max(1, (int)Math.Ceiling(seconds * rate))];

        public static double Midi(double note) => 440.0 * Math.Pow(2, (note - 69) / 12.0);

        // ------------------------------------------------------------------ envelopes

        /// <summary>Linear attack then exponential decay (k per second). Zero before start.</summary>
        public static double Perc(double t, double attack, double k)
        {
            if (t < 0) return 0;
            if (t < attack) return t / attack;
            return Math.Exp(-(t - attack) * k);
        }

        /// <summary>Attack, hold at 1, then linear release over the given seconds.</summary>
        public static double Gate(double t, double attack, double hold, double release)
        {
            if (t < 0) return 0;
            if (t < attack) return t / attack;
            if (t < attack + hold) return 1;
            double r = (t - attack - hold) / Math.Max(1e-4, release);
            return r >= 1 ? 0 : 1 - r;
        }

        // ------------------------------------------------------------------ voices (added into a buffer)

        /// <summary>FM bell/chime. Ratio 1.4..3.5 gives metallic partials; index decays with the note.</summary>
        public static void Bell(float[] buf, int rate, double start, double freq, double gain, double decay = 6, double ratio = 3.5, double index = 2.2, double length = 2.5)
        {
            int s0 = (int)(start * rate);
            int n = (int)(length * rate);
            double pc = 0, pm = 0;
            double dc = freq / rate, dm = freq * ratio / rate;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)rate;
                double env = Perc(t, 0.002, decay);
                if (env < 1e-4 && t > 0.1) break;
                double idx = index * Math.Exp(-t * decay * 1.6);
                double v = Math.Sin(TwoPi * pc + idx * Math.Sin(TwoPi * pm));
                // A soft octave partial for sparkle.
                v += 0.18 * Math.Sin(TwoPi * pc * 2.0) * Math.Exp(-t * decay * 2);
                buf[j] += (float)(v * env * gain);
                pc += dc;
                pm += dm;
                if (pc > 1) pc -= 1;
                if (pm > 1) pm -= 1;
            }
        }

        /// <summary>Soft FM pluck (marimba / kalimba-like).</summary>
        public static void Pluck(float[] buf, int rate, double start, double freq, double gain, double decay = 7, double index = 1.4)
        {
            int s0 = (int)(start * rate);
            int n = (int)(Math.Min(3.0, 7.0 / decay) * rate);
            double pc = 0, pm = 0;
            double dc = freq / rate, dm = freq * 1.0 / rate;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)rate;
                double env = Perc(t, 0.004, decay);
                double idx = index * Math.Exp(-t * 18);
                double v = Math.Sin(TwoPi * pc + idx * Math.Sin(TwoPi * pm)) + 0.25 * Math.Sin(TwoPi * pc * 3) * Math.Exp(-t * 30);
                buf[j] += (float)(v * env * gain);
                pc += dc;
                pm += dm;
                if (pc > 1) pc -= 1;
                if (pm > 1) pm -= 1;
            }
        }

        /// <summary>Sine with an exponential pitch glide (kicks, bloops, zaps).</summary>
        public static void Glide(float[] buf, int rate, double start, double f0, double f1, double glideSeconds, double gain, double attack, double decay, double length)
        {
            int s0 = (int)(start * rate);
            int n = (int)(length * rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)rate;
                double k = Math.Min(1, t / Math.Max(1e-4, glideSeconds));
                double f = f0 * Math.Pow(f1 / f0, k);
                phase += f / rate;
                buf[j] += (float)(Math.Sin(TwoPi * phase) * Perc(t, attack, decay) * gain);
            }
        }

        /// <summary>Band-limited sawtooth (polyBLEP).</summary>
        static double Saw(double phase, double dt)
        {
            double v = 2 * phase - 1;
            if (phase < dt)
            {
                double x = phase / dt;
                v -= x + x - x * x - 1;
            }
            else if (phase > 1 - dt)
            {
                double x = (phase - 1) / dt;
                v -= x * x + x + x + 1;
            }
            return v;
        }

        /// <summary>Warm pad or brass: detuned saws through a low-pass with optional vibrato.</summary>
        public static void Pad(float[] buf, int rate, double start, double freq, double gain, double attack, double hold, double release,
            double cutoff, double detuneCents = 7, double vibrato = 0, double resonance = 0.7)
        {
            int s0 = (int)(start * rate);
            int n = (int)((attack + hold + release) * rate);
            double d = Math.Pow(2, detuneCents / 1200.0);
            double p1 = 0, p2 = 0.37, p3 = 0.71;
            var f = new Svf();
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)rate;
                double vib = vibrato > 0 ? 1 + vibrato * Math.Sin(TwoPi * 5.2 * t) * Math.Min(1, t * 2) : 1;
                double fr = freq * vib;
                double dt1 = fr / rate, dt2 = fr * d / rate, dt3 = fr / d / rate;
                double v = Saw(p1, dt1) + Saw(p2, dt2) * 0.8 + Saw(p3, dt3) * 0.8;
                p1 += dt1; if (p1 >= 1) p1 -= 1;
                p2 += dt2; if (p2 >= 1) p2 -= 1;
                p3 += dt3; if (p3 >= 1) p3 -= 1;
                double env = Gate(t, attack, hold, release);
                if ((i & 15) == 0) f.Set(cutoff * (0.6 + 0.4 * env), resonance, rate);
                buf[j] += (float)(f.Low(v * 0.33) * env * gain);
            }
        }

        /// <summary>Filtered noise with a cutoff sweep (whooshes, wind, rain). Mode: 0 low, 1 band, 2 high.</summary>
        public static void NoiseSweep(float[] buf, int rate, double start, double length, double gain, double f0, double f1, int mode, double q,
            Func<double, double> envelope, int seed)
        {
            int s0 = (int)(start * rate);
            int n = (int)(length * rate);
            var rng = new Random(seed);
            var f = new Svf();
            for (int i = 0; i < n; i++)
            {
                int j = s0 + i;
                if (j < 0) continue;
                if (j >= buf.Length) break;
                double t = i / (double)rate;
                double k = t / length;
                if ((i & 15) == 0) f.Set(f0 * Math.Pow(f1 / f0, k), q, rate);
                double x = rng.NextDouble() * 2 - 1;
                double y = mode == 0 ? f.Low(x) : mode == 1 ? f.Band(x) : f.High(x);
                buf[j] += (float)(y * envelope(t) * gain);
            }
        }

        // ------------------------------------------------------------------ weather

        /// <summary>
        /// Thunder, kept light and playful: a bright crack, a short round "boom" in the low mids and a
        /// quick roll. Far strikes (distance near 1) are just a soft, short roll. Everything below about
        /// 110 Hz is filtered out, so storms never sound heavy or boomy, even on headphones.
        /// </summary>
        public static float[] Thunder(int rate, int seed, double distance)
        {
            var rng = new Random(seed);
            double near = Math.Max(0, 1 - distance * 0.9);
            double length = 1.6 + distance * 0.8;
            var buf = Buffer(length, rate);

            // Crack: a bright tear and a few crackles.
            if (near > 0.15)
            {
                NoiseSweep(buf, rate, 0, 0.3, 1.1 * near, 7200, 1800, 2, 0.6, t => Perc(t, 0.0008, 20), seed);
                int crackles = 6 + (int)(12 * near);
                for (int c = 0; c < crackles; c++)
                {
                    double at = 0.005 + Math.Pow(rng.NextDouble(), 1.7) * 0.35;
                    double g = (0.3 + 0.7 * rng.NextDouble()) * near * (1 - at) * 0.8;
                    NoiseSweep(buf, rate, at, 0.05, g, 5200, 1600, 1, 1.3, t => Perc(t, 0.0004, 90), seed + c * 7);
                }

                // Boom: a short, round bump (a cartoon "boom", not a sub-bass hit).
                Glide(buf, rate, 0.01, 220, 110, 0.2, 0.55 * near, 0.004, 8, 0.5);
                Glide(buf, rate, 0.01, 440, 220, 0.2, 0.2 * near, 0.004, 10, 0.4);
            }

            // Roll: brown noise band-passed around 250-330 Hz with a light rolling amplitude, over quickly.
            var body = new Svf();
            body.Set(330 - distance * 80, 0.8, rate);
            double brown = 0, roll = 0.6, rollTarget = 1;
            int rollCounter = 0;
            double rollStart = 0.02 + distance * 0.12;
            double rollDecay = 3.0 - distance * 1.2;
            for (int i = 0; i < buf.Length; i++)
            {
                double t = i / (double)rate - rollStart;
                if (t < 0) continue;
                double w = rng.NextDouble() * 2 - 1;
                brown = (brown + 0.03 * w) * 0.997;
                if (--rollCounter <= 0)
                {
                    rollCounter = (int)(rate * (0.04 + rng.NextDouble() * 0.12));
                    rollTarget = 0.35 + rng.NextDouble() * 0.9;
                }
                roll += (rollTarget - roll) * 0.002;
                double env = Perc(t, 0.02 + distance * 0.12, rollDecay);
                buf[i] += (float)(body.Band(brown * 12) * env * roll);
            }

            // Nothing deep: cut everything under about 110 Hz.
            var hp = new Svf();
            hp.Set(110, 0.7, rate);
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)hp.High(buf[i]);

            Reverb(buf, rate, 0.14, 0.72, 0.3);
            SoftClip(buf, 1.2f);
            Fade(buf, rate, 0.0005, 0.15);
            Normalize(buf, 0.9f);
            return buf;
        }

        // ------------------------------------------------------------------ processing

        /// <summary>Topology-preserving state-variable filter (stable under modulation).</summary>
        public sealed class Svf
        {
            double _a1, _a2, _a3, _k, _ic1, _ic2, _v1, _v2;

            public void Set(double cutoff, double q, int rate)
            {
                cutoff = Math.Max(10, Math.Min(cutoff, rate * 0.45));
                double g = Math.Tan(Math.PI * cutoff / rate);
                _k = 1 / Math.Max(0.05, q);
                _a1 = 1 / (1 + g * (g + _k));
                _a2 = g * _a1;
                _a3 = g * _a2;
            }

            void Step(double x)
            {
                double v3 = x - _ic2;
                _v1 = _a1 * _ic1 + _a2 * v3;
                _v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
                _ic1 = 2 * _v1 - _ic1;
                _ic2 = 2 * _v2 - _ic2;
            }

            public double Low(double x)
            {
                Step(x);
                return _v2;
            }

            public double Band(double x)
            {
                Step(x);
                return _v1;
            }

            public double High(double x)
            {
                Step(x);
                return x - _k * _v1 - _v2;
            }
        }

        /// <summary>
        /// Mono Freeverb: six damped combs into three all-passes, mixed in place. A non-zero
        /// <paramref name="spread"/> (samples) detunes the delay lines for the second stereo channel.
        /// </summary>
        public static void Reverb(float[] buf, int rate, double wet, double room = 0.84, double damp = 0.3, int spread = 0)
        {
            double scale = rate / 44100.0;
            int[] combLen = { (int)((1116 + spread) * scale), (int)((1188 + spread) * scale), (int)((1277 + spread) * scale), (int)((1356 + spread) * scale), (int)((1422 + spread) * scale), (int)((1491 + spread) * scale) };
            int[] apLen = { (int)((556 + spread) * scale), (int)((441 + spread) * scale), (int)((341 + spread) * scale) };
            var combs = new double[combLen.Length][];
            var combIdx = new int[combLen.Length];
            var combStore = new double[combLen.Length];
            for (int c = 0; c < combLen.Length; c++) combs[c] = new double[Math.Max(1, combLen[c])];
            var aps = new double[apLen.Length][];
            var apIdx = new int[apLen.Length];
            for (int a = 0; a < apLen.Length; a++) aps[a] = new double[Math.Max(1, apLen[a])];

            for (int i = 0; i < buf.Length; i++)
            {
                double input = buf[i] * 0.2;
                double outSum = 0;
                for (int c = 0; c < combs.Length; c++)
                {
                    var line = combs[c];
                    double y = line[combIdx[c]];
                    combStore[c] = y * (1 - damp) + combStore[c] * damp;
                    line[combIdx[c]] = input + combStore[c] * room;
                    if (++combIdx[c] >= line.Length) combIdx[c] = 0;
                    outSum += y;
                }
                for (int a = 0; a < aps.Length; a++)
                {
                    var line = aps[a];
                    double b = line[apIdx[a]];
                    double o = -outSum + b;
                    line[apIdx[a]] = outSum + b * 0.5;
                    if (++apIdx[a] >= line.Length) apIdx[a] = 0;
                    outSum = o;
                }
                buf[i] = (float)(buf[i] * (1 - wet * 0.5) + outSum * wet);
            }
        }

        /// <summary>Feedback echo (for plucks and chimes).</summary>
        public static void Echo(float[] buf, int rate, double seconds, double feedback, double mix)
        {
            int d = Math.Max(1, (int)(seconds * rate));
            var line = new float[d];
            int idx = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                float delayed = line[idx];
                line[idx] = (float)(buf[i] + delayed * feedback);
                buf[i] += (float)(delayed * mix);
                if (++idx >= d) idx = 0;
            }
        }

        public static void Normalize(float[] buf, float peak)
        {
            float max = 1e-6f;
            for (int i = 0; i < buf.Length; i++) max = Math.Max(max, Math.Abs(buf[i]));
            float g = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        /// <summary>Gentle tanh saturation, keeps peaks under 1.</summary>
        public static void SoftClip(float[] buf, float drive = 1.2f)
        {
            double norm = Math.Tanh(drive);
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)(Math.Tanh(buf[i] * drive) / norm);
        }

        /// <summary>Short fades so clips never click.</summary>
        public static void Fade(float[] buf, int rate, double fadeIn = 0.002, double fadeOut = 0.02)
        {
            int a = Math.Min(buf.Length, (int)(fadeIn * rate));
            int b = Math.Min(buf.Length, (int)(fadeOut * rate));
            for (int i = 0; i < a; i++) buf[i] *= i / (float)a;
            for (int i = 0; i < b; i++) buf[buf.Length - 1 - i] *= i / (float)b;
        }

        /// <summary>Adds everything past <paramref name="loopLength"/> (ringing notes, reverb) onto the start.</summary>
        public static float[] WrapTail(float[] buf, int loopLength)
        {
            var loop = new float[loopLength];
            for (int i = 0; i < buf.Length; i++) loop[i % loopLength] += buf[i];
            return loop;
        }

        /// <summary>
        /// Folds the audio past <paramref name="loopLength"/> back onto the start with an equal-power
        /// crossfade, returning a seamless loop.
        /// </summary>
        public static float[] MakeLoop(float[] buf, int loopLength, int crossfade)
        {
            var loop = new float[loopLength];
            Array.Copy(buf, loop, Math.Min(loopLength, buf.Length));
            crossfade = Math.Min(crossfade, Math.Min(loopLength, buf.Length - loopLength));
            for (int i = 0; i < crossfade; i++)
            {
                double w = i / (double)crossfade;
                double a = Math.Sin(w * Math.PI * 0.5), b = Math.Cos(w * Math.PI * 0.5);
                loop[i] = (float)(buf[i] * a + buf[loopLength + i] * b);
            }
            return loop;
        }
    }
}
