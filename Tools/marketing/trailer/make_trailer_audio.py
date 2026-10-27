"""
Music and sound effects for the trailer, synthesized from scratch (no samples, so no licences to
worry about), timed by timeline.py:
  0-4 s    a cheesy, sunny little tune that tape-stops ("...the weather was perfectly fine")
  4-6 s    wind and a distant rumble ("one engine said:")
  6-8 s    a thunder crack ("NOPE.") and a riser into
  8 s      the drop on the logo: a 120 BPM groove under the montage (A minor: Am F C G)
  22-24 s  tension (no kick) for "charge the core...", 24 s back in for "...unleash the storm!"
  28-32 s  breakdown for the warning gag (siren), riser into
  32-40 s  the final chorus, and a big end hit at 40 s that rings out under the end card
Game actions on screen (strikes, machine pops, level-ups, decorations, skins) get their own sounds.

Output: Tools/marketing/trailer/out/trailer_audio.wav (44.1 kHz, 16-bit stereo)
Usage: python Tools/marketing/trailer/make_trailer_audio.py
"""
import os
import wave

import numpy as np
from scipy import signal

import timeline as tl

SR = 44100
N = int(SR * tl.LENGTH)
BEAT = tl.BEAT
BAR = BEAT * 4
OUT = os.path.join(tl.HERE, 'out')
rng = np.random.default_rng(11)


# ------------------------------------------------------------------ building blocks

def ts(dur):
    return np.arange(int(SR * dur)) / SR


def midi(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def noise(dur):
    return rng.standard_normal(int(SR * dur))


def filt(x, kind, freq, order=2):
    nyq = SR / 2
    if kind == 'band':
        lo, hi = freq
        sos = signal.butter(order, [max(lo, 20) / nyq, min(hi, nyq * 0.95) / nyq], btype='band', output='sos')
    else:
        sos = signal.butter(order, min(freq, nyq * 0.95) / nyq, btype=kind, output='sos')
    return signal.sosfilt(sos, x)


def saw(freq, dur, detune_cents=0.0):
    """Band-limited sawtooth (polyBLEP)."""
    n = int(SR * dur)
    f = freq * 2 ** (detune_cents / 1200.0)
    dt = f / SR
    ph = (rng.random() + dt * np.arange(n)) % 1.0
    y = 2.0 * ph - 1.0
    m = ph < dt
    x = ph[m] / dt
    y[m] -= x + x - x * x - 1.0
    m = ph > 1.0 - dt
    x = (ph[m] - 1.0) / dt
    y[m] -= x * x + x + x + 1.0
    return y


def sine_sweep(f0, f1, dur, curve=2.0):
    t = ts(dur)
    k = (t / dur) ** curve if dur > 0 else t
    f = f0 + (f1 - f0) * k
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def env(n, attack, decay=None):
    """Linear attack, then exponential decay (rate per second)."""
    t = np.arange(n) / SR
    e = np.minimum(1.0, t / attack) if attack > 0 else np.ones(n)
    if decay:
        e *= np.exp(-np.maximum(0.0, t - attack) * decay)
    return e


def place(buf, x, at, gain=1.0, pan=0.0):
    """Mixes mono x into stereo buf at `at` seconds (constant-power pan, -1 left .. 1 right)."""
    if x.ndim == 1:
        a = np.pi * (pan + 1) / 4
        x = np.stack([x * np.cos(a) * 1.414, x * np.sin(a) * 1.414], axis=1)
    i = int(round(at * SR))
    if i >= len(buf):
        return
    j0 = max(0, -i)
    i = max(0, i)
    n = min(len(x) - j0, len(buf) - i)
    if n > 0:
        buf[i:i + n] += x[j0:j0 + n] * gain


def reverb(x, seconds=1.6, mix=0.25, damp=3.0):
    """Cheap stereo reverb: convolution with decaying noise."""
    n = int(SR * seconds)
    t = np.arange(n) / SR
    out = np.zeros_like(x)
    for ch in range(2):
        ir = rng.standard_normal(n) * np.exp(-t * damp)
        ir = filt(ir, 'low', 5000)
        ir /= np.sqrt(np.sum(ir ** 2))
        out[:, ch] = signal.fftconvolve(x[:, ch], ir)[:len(x)]
    return x * (1 - mix) + out * mix


def sweep_noise(dur, f0, f1, width=0.6):
    """Noise through a band-pass that slides from f0 to f1 (overlap-added 40 ms grains)."""
    n = int(SR * dur)
    src = rng.standard_normal(n + SR)
    hop = int(SR * 0.02)
    win = np.hanning(hop * 2)
    out = np.zeros(n + hop * 2)
    for i in range(0, n, hop):
        k = i / max(1, n)
        fc = f0 * (f1 / f0) ** k
        seg = filt(src[i:i + hop * 2], 'band', (fc * (1 - width / 2), fc * (1 + width / 2)))
        out[i:i + hop * 2] += seg * win
    return out[:n]


# ------------------------------------------------------------------ instruments

def kick(gain=1.0):
    t = ts(0.5)
    f = 46 + 110 * np.exp(-t * 30)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 11)
    click = filt(noise(0.5), 'high', 2500) * np.exp(-t * 300) * 0.45
    return (body + click) * gain


def snare(gain=1.0):
    t = ts(0.3)
    n = filt(noise(0.3), 'band', (1500, 7000)) * np.exp(-t * 20)
    tone = np.sin(2 * np.pi * 190 * t) * np.exp(-t * 35) * 0.6
    return (n * 0.9 + tone) * gain


def clap(gain=1.0):
    out = np.zeros(int(SR * 0.35))
    for d in (0.0, 0.011, 0.023):
        b = filt(noise(0.02), 'band', (900, 4500)) * np.hanning(int(SR * 0.02))
        i = int(SR * d)
        out[i:i + len(b)] += b
    t = ts(0.35)
    out += filt(noise(0.35), 'band', (900, 4000)) * np.exp(-t * 16) * 0.6
    return out * gain


def hat(open_=False, gain=1.0):
    dur = 0.3 if open_ else 0.06
    t = ts(dur)
    return filt(noise(dur), 'high', 7500) * np.exp(-t * (12 if open_ else 80)) * gain


def crash(gain=1.0):
    t = ts(2.2)
    return filt(noise(2.2), 'high', 3500) * np.exp(-t * 2.2) * gain


def bass_note(m, dur, gain=1.0):
    f = midi(m)
    x = saw(f, dur) * 0.75 + np.sin(2 * np.pi * f * ts(dur)) * 0.45
    x = filt(filt(x, 'low', 1100), 'high', 55)
    return x * env(len(x), 0.004, decay=3.0) * gain


def stab(notes, dur, gain=1.0, cutoff=3800):
    x = np.zeros(int(SR * dur))
    for m in notes:
        for d in (-9, 0, 9):
            x += saw(midi(m), dur, d)
    x = filt(x, 'low', cutoff)
    return x * env(len(x), 0.005, decay=4.5) * gain / (len(notes) * 3)


def pad(notes, dur, gain=1.0, cutoff=1100, attack=0.6):
    x = np.zeros(int(SR * dur))
    for m in notes:
        for d in (-12, -4, 5, 13):
            x += saw(midi(m), dur, d)
    x = filt(x, 'low', cutoff)
    e = np.minimum(1.0, ts(dur) / attack) * np.minimum(1.0, (dur - ts(dur)) / 0.4)
    return x * e * gain / (len(notes) * 4)


def lead_note(m, dur, gain=1.0):
    t = ts(dur + 0.25)
    f = midi(m) * (1 + 0.004 * np.sin(2 * np.pi * 5.5 * t) * np.minimum(1, t / 0.15))
    ph = np.cumsum(f) / SR
    x = np.sign(np.sin(2 * np.pi * ph)) * 0.35 + (2 * (ph % 1) - 1) * 0.45
    x = filt(x, 'low', 5000)
    e = np.minimum(1, t / 0.006) * np.exp(-t * 2.2) * np.where(t > dur, np.exp(-(t - dur) * 25), 1)
    return x * e * gain


def marimba(m, gain=1.0):
    t = ts(0.7)
    f = midi(m)
    x = np.sin(2 * np.pi * f * t) * np.exp(-t * 7) + 0.25 * np.sin(2 * np.pi * f * 4 * t) * np.exp(-t * 25)
    return x * np.minimum(1, t / 0.002) * gain


# ------------------------------------------------------------------ sound effects

def thunder(gain=1.0, length=2.6, crack=1.0):
    t = ts(length)
    rumble = filt(np.cumsum(noise(length)) * 0.02, 'low', 260)
    rumble = filt(rumble, 'high', 25)
    mod = 0.6 + 0.4 * np.sin(2 * np.pi * 1.7 * t + rng.random() * 6) * np.sin(2 * np.pi * 0.6 * t)
    rumble *= mod * np.exp(-t * (2.2 / length * 2)) * np.minimum(1, t / 0.05)
    rumble /= np.max(np.abs(rumble)) + 1e-9
    cr = filt(noise(length), 'band', (900, 9000)) * np.exp(-t * 28) * crack
    for _ in range(5):
        i = int(rng.random() * 0.5 * SR)
        b = filt(noise(0.03), 'band', (1500, 7000)) * np.hanning(int(SR * 0.03)) * 0.7 * crack
        cr[i:i + len(b)] += b
    return (filt(rumble, 'high', 60) * 0.55 + cr * 0.8) * gain


def boom(gain=1.0):
    t = ts(2.4)
    f = 38 + 70 * np.exp(-t * 6)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 1.6)
    x = filt(x, 'high', 45) * 0.7
    x += filt(noise(2.4), 'band', (300, 4000)) * np.exp(-t * 5) * 0.45
    return x * gain


def zap(gain=1.0):
    t = ts(0.25)
    s = sine_sweep(2600, 260, 0.25, 0.5) * np.exp(-t * 16)
    n = filt(noise(0.25), 'band', (2000, 9000)) * np.exp(-t * 40)
    return (s * 0.6 + n * 0.7) * gain


def pop(pitch=1.0, gain=1.0):
    t = ts(0.18)
    s = sine_sweep(380 * pitch, 980 * pitch, 0.18, 0.6) * np.exp(-t * 22)
    n = filt(noise(0.18), 'band', (1500, 5000)) * np.exp(-t * 90) * 0.4
    return (s + n) * gain


def ding(gain=1.0):
    t = ts(0.9)
    x = sum(a * np.sin(2 * np.pi * midi(m) * t) * np.exp(-t * d) for m, a, d in ((88, 1, 4), (95, 0.5, 6), (100, 0.3, 9)))
    return x * np.minimum(1, t / 0.002) * gain


def swish(gain=1.0):
    x = sweep_noise(0.22, 800, 7000) * np.hanning(int(SR * 0.22)) * 1.4
    p = pop(1.3, 0.6)
    x[:len(p)] += p
    return x * gain


def whoomp(gain=1.0):
    t = ts(0.5)
    x = sine_sweep(220, 70, 0.5, 0.7) * np.exp(-t * 6)
    x += sweep_noise(0.5, 3000, 300) * np.hanning(int(SR * 0.5)) * 0.8
    return x * gain


def charge_whine(dur, gain=1.0):
    t = ts(dur)
    x = sine_sweep(180, 1500, dur, 1.6) * (0.6 + 0.4 * np.sin(2 * np.pi * 14 * t))
    return x * (t / dur) ** 1.5 * gain


def riser(dur, gain=1.0):
    t = ts(dur)
    x = sweep_noise(dur, 300, 9000, 0.5) * (t / dur) ** 2
    x += sine_sweep(200, 1400, dur, 2.0) * (t / dur) ** 2.5 * 0.25
    return x * gain


def whoosh(gain=1.0):
    x = sweep_noise(0.32, 500, 4000, 0.8) * np.hanning(int(SR * 0.32))
    return x * gain


def siren(gain=1.0):
    dur = 0.82
    t = ts(dur)
    f = np.where((t // 0.2) % 2 == 0, 660.0, 880.0)
    x = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * 0.5
    x = filt(x, 'low', 2500) * np.minimum(1, t / 0.01) * np.minimum(1, (dur - t) / 0.05)
    return x * gain


def tape_stop(x, start, dur):
    """Slows a stereo buffer to a halt from `start` over `dur` seconds (pitch drops with it)."""
    i0, n = int(start * SR), int(dur * SR)
    speed = np.linspace(1.0, 0.0, n) ** 1.3
    pos = i0 + np.cumsum(speed)
    out = x.copy()
    for ch in range(2):
        out[i0:i0 + n, ch] = np.interp(pos, np.arange(len(x)), x[:, ch]) * np.linspace(1, 0.2, n)
    out[i0 + n:] = 0
    return out


# ------------------------------------------------------------------ the track

CHORDS = [  # one chord per bar: Am F C G
    ([57, 60, 64], 45), ([53, 57, 60], 41), ([55, 60, 64], 48), ([55, 59, 62], 43),
]
HOOK = [  # (beat in a 2-bar phrase, midi, beats long) - A minor pentatonic
    (0.0, 76, 0.5), (0.75, 76, 0.25), (1.0, 79, 0.5), (1.5, 81, 0.5), (2.0, 79, 0.5), (2.5, 76, 0.5),
    (3.0, 74, 0.75), (4.0, 72, 0.5), (4.5, 74, 0.5), (5.0, 76, 0.5), (5.5, 79, 0.25), (6.0, 81, 0.75), (7.0, 84, 0.75),
]
CALM_TUNE = [72, 76, 79, 76, 81, 79, 76, 79, 72, 76, 79, 84, 83, 79, 76, 74]


def calm_music():
    buf = np.zeros((N, 2))
    place(buf, pad([60, 64, 67, 71], 2.1, 0.5, 1400, 0.3), 0.0, 1.0)
    place(buf, pad([65, 69, 72, 76], 2.1, 0.5, 1400, 0.3), 2.0, 1.0)
    for i, m in enumerate(CALM_TUNE):
        place(buf, marimba(m, 0.45), i * BEAT / 2, 1.0, 0.25 if i % 2 else -0.25)
    for b in range(8):
        place(buf, kick(0.25), b * BEAT, 1.0)
        place(buf, hat(False, 0.12), b * BEAT + BEAT / 2, 1.0, 0.3)
    buf = reverb(buf, 1.2, 0.18)
    return tape_stop(buf, 3.72, 0.45)


def groove(start, end, lead_from=None, no_kick=(), fill_bars=()):
    """Drums, bass and chord stabs from `start` to `end` (whole bars)."""
    drums = np.zeros((N, 2))
    buf = np.zeros((N, 2))
    duck = np.ones(N)
    bars = int(round((end - start) / BAR))
    for b in range(bars):
        t0 = start + b * BAR
        chord, root = CHORDS[b % 4]
        silent_kick = t0 in no_kick
        for k in range(4):
            tb = t0 + k * BEAT
            if not silent_kick:
                place(drums, kick(0.75), tb)
                i = int(tb * SR)
                n = int(0.32 * SR)
                if i < N:
                    seg = 1 - 0.55 * np.exp(-np.arange(min(n, N - i)) / SR * 9)
                    duck[i:i + len(seg)] = np.minimum(duck[i:i + len(seg)], seg)
            if k in (1, 3):
                place(drums, clap(0.75), tb, 1.0, 0.1)
                place(drums, snare(0.45), tb, 1.0, -0.1)
            place(drums, hat(False, 0.42), tb + BEAT / 2, 1.0, 0.35)
            for q in (0.25, 0.75):
                place(drums, hat(False, 0.16), tb + BEAT * q, 1.0, -0.35)
            if k == 3 and b % 2 == 1:
                place(drums, hat(True, 0.32), tb + BEAT / 2, 1.0, 0.35)
        for e in range(8):
            m = root + (12 if e % 2 else 0)
            place(buf, bass_note(m, BEAT / 2 * 0.95, 0.42), t0 + e * BEAT / 2)
        for e in range(8):
            if e in (0, 3, 6) or (e == 7 and b % 2):
                place(buf, stab(chord, 0.42, 0.55), t0 + e * BEAT / 2, 1.0, 0.0)
        if t0 in fill_bars:
            for s in range(8):
                place(drums, snare(0.18 + 0.05 * s), t0 + 2 * BEAT + s * BEAT / 4, 1.0)
    if lead_from is not None:
        lead = np.zeros((N, 2))
        phrases = int(round((end - lead_from) / (BAR * 2)))
        for p in range(phrases):
            for beat, m, length in HOOK:
                at = lead_from + p * BAR * 2 + beat * BEAT
                place(lead, lead_note(m, length * BEAT, 0.42), at, 1.0, 0.15)
                place(lead, lead_note(m, length * BEAT, 0.16), at + 0.375, 1.0, -0.4)
        buf += reverb(lead, 1.4, 0.22)
    return drums + buf * duck[:, None]


def build():
    music = np.zeros((N, 2))
    sfx = np.zeros((N, 2))

    # Cold open.
    music += calm_music()
    w = filt(noise(4.0), 'band', (250, 1200)) * 0.05 * np.sin(np.linspace(0, np.pi, int(SR * 4.0)))
    place(sfx, w, 4.0, 1.0, -0.2)
    place(sfx, thunder(0.18, 2.5, 0.0), 4.9, 1.0, 0.3)
    place(sfx, thunder(0.9, 3.0, 1.2), 6.0, 1.0)
    place(sfx, boom(0.55), 6.0)
    place(music, riser(2.0, 0.35), 6.0, 1.0)
    for s in range(16):
        at = 7.0 + s * 0.0625
        place(music, snare(0.12 + 0.02 * s), at, 1.0)

    # Drop on the logo, groove under the montage; "charge the core" (22 s) holds the kick back.
    music += groove(8.0, 28.0, lead_from=12.0, no_kick=(22.0,), fill_bars=(26.0,))
    place(music, riser(2.0, 0.22), 22.0)
    for at in (8.0, 24.0):
        place(music, crash(0.45), at, 1.0, 0.2)

    # Breakdown: the warning gag.
    place(music, pad([57, 60, 64], 2.1, 0.6, 900, 0.2), 28.0)
    place(music, pad([53, 57, 60], 2.1, 0.6, 900, 0.2), 30.0)
    for at in (28.0, 28.5, 29.0, 29.5, 30.0, 30.5, 31.0, 31.5):
        place(music, kick(0.6 if at % 1 == 0 else 0.35), at)
    place(sfx, siren(0.22), 28.1, 1.0, 0.0)
    place(sfx, thunder(0.5, 2.5, 0.6), 29.0, 1.0, -0.3)
    place(music, riser(2.0, 0.35), 30.0)
    for s in range(16):
        place(music, snare(0.1 + 0.025 * s), 31.0 + s * 0.0625, 1.0)

    # Final chorus and the end hit.
    music += groove(32.0, 40.0, lead_from=32.0, fill_bars=(38.0,))
    for at in (32.0, 36.0):
        place(music, crash(0.45), at, 1.0, -0.2)
    end = np.zeros((N, 2))
    place(end, stab([57, 60, 64, 71], 1.0, 1.4, 3000), 40.0)
    place(end, pad([57, 60, 64, 71], 6.0, 1.1, 1500, 0.05), 40.0)
    place(end, bass_note(33, 2.5, 0.7), 40.0)
    music += reverb(end, 2.5, 0.35)
    place(music, crash(0.6), 40.0)

    # Hits on the logo slam and the end card.
    for at, strength in tl.HITS:
        place(sfx, boom(0.9 * strength), at)
        place(sfx, thunder(0.8 * strength, 3.2, 1.0), at)

    # Cuts in the montage get a light whoosh.
    for start, _, _, _, _ in tl.SEGMENTS:
        if 12.0 <= start < 28.0 or 32.0 < start < 40.0:
            place(sfx, whoosh(0.12), start - 0.12, 1.0, rng.uniform(-0.5, 0.5))

    # What happens on screen.
    pitches = [1.0, 1.12, 1.26, 1.33, 1.5, 1.68]
    count = 0
    for at, action in tl.sound_events():
        pan = rng.uniform(-0.35, 0.35)
        if action == 'strike':
            place(sfx, zap(0.32), at, 1.0, pan)
            place(sfx, thunder(0.22, 1.4, 0.5), at + 0.02, 1.0, pan)
        elif action == 'bigStrike':
            if any(abs(at - h) < 0.05 for h, _ in tl.HITS):
                continue
            place(sfx, zap(0.45), at, 1.0, pan)
            place(sfx, thunder(0.55, 2.2, 1.0), at, 1.0, pan)
            place(sfx, boom(0.35), at)
        elif action in ('buy', 'decor'):
            place(sfx, pop(pitches[count % len(pitches)], 0.42), at, 1.0, pan)
            count += 1
        elif action == 'level':
            place(sfx, ding(0.22), at, 1.0, pan)
            place(sfx, pop(1.6, 0.25), at, 1.0, pan)
        elif action == 'equip':
            place(sfx, swish(0.35), at, 1.0, pan)
        elif action == 'chargeRamp':
            place(sfx, charge_whine(1.9, 0.12), at)
        elif action == 'skyRamp':
            place(sfx, thunder(0.25, 2.0, 0.0), at, 1.0, pan)
    # World changes (equip in the "worlds" segment) also get a deeper whoomp.
    for at, action in tl.sound_events():
        if action == 'equip' and 20.0 <= at < 22.0:
            place(sfx, whoomp(0.3), at)

    mix = music * 0.8 + sfx * 0.85
    mix = np.stack([filt(mix[:, 0], 'high', 35), filt(mix[:, 1], 'high', 35)], axis=1)
    mix = np.tanh(mix * 1.15) / np.tanh(1.15)
    fade = np.ones(N)
    k = int(1.8 * SR)
    fade[-k:] = np.linspace(1, 0, k) ** 2
    mix *= fade[:, None]
    peak = np.max(np.abs(mix))
    mix = mix / peak * 0.89
    return mix


def main():
    os.makedirs(OUT, exist_ok=True)
    mix = build()
    data = (mix * 32767).astype(np.int16)
    path = os.path.join(OUT, 'trailer_audio.wav')
    with wave.open(path, 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(data.tobytes())
    rms = np.sqrt(np.mean(mix ** 2))
    print('wrote', path, '%.1f s' % (len(mix) / SR), 'rms %.3f (%.1f dBFS)' % (rms, 20 * np.log10(rms)))


if __name__ == '__main__':
    main()
