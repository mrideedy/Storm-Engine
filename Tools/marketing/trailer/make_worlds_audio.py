"""
Music for reel 8 (worlds_timeline.py): a bright future-bass pop track, synthesized from scratch and sharing
nothing with the earlier reels' music. 128 BPM, G - D - Em - C:
  intro    filtered supersaw chord, riser and snare roll
  drop     pumping supersaw chords, plucky arpeggios, a vowel-like synth hook, four-on-the-floor; a whoosh and
           a lightning zap on every world switch
  grid     the drums break, nine tile pops climb in pitch, a roll into the final hit and a ringing G chord

Output: Tools/marketing/trailer/out/worlds_audio.wav (44.1 kHz, 16-bit stereo)
Usage: python make_worlds_audio.py
"""
import os
import wave

import numpy as np

import worlds_timeline as wt
from make_trailer_audio import (SR, boom, clap, crash, filt, hat, kick, midi, place, pop, reverb, riser, rng, snare,
                                thunder, ts, whoosh, zap)

N = int(SR * wt.LENGTH)
B = wt.BEAT
OUT = os.path.join(wt.HERE, 'out')
CHORDS = {'G': ([59, 62, 67, 71], 43), 'D': ([57, 62, 66, 69], 38), 'Em': ([59, 64, 67, 71], 40),
          'C': ([60, 64, 67, 72], 36)}
BARS = ['G', 'G', 'D', 'Em', 'C', 'G']  # bar 0 is the intro
HOOK = [  # (bar, beat, note, beats)
    (1, 0, 74, .5), (1, .5, 71, .5), (1, 1, 74, .5), (1, 1.5, 76, .5), (1, 2, 74, 1), (1, 3, 71, .75),
    (2, 0, 69, .5), (2, .5, 66, .5), (2, 1, 69, .5), (2, 1.5, 71, .5), (2, 2, 69, 1), (2, 3, 66, .75),
    (3, 0, 67, .5), (3, .5, 71, .5), (3, 1, 76, .75), (3, 2, 74, .5), (3, 2.5, 71, .5), (3, 3, 71, .75),
    (4, 0, 72, .5), (4, .5, 76, .5), (4, 1, 79, .75), (4, 2, 76, .5), (4, 2.5, 74, .5), (4, 3, 72, 1),
    (5, 0, 74, .5), (5, .5, 71, .5), (5, 1, 67, 1),
]


def supersaw(notes, dur, gain=1.0, cutoff=5200):
    t = ts(dur + 0.25)
    x = np.zeros(len(t))
    for m in notes:
        for d in (-14, -7, 0, 7, 14):
            f = midi(m) * 2 ** (d / 1200)
            x += 2 * ((f * t + rng.random()) % 1) - 1
    env = np.minimum(1, t / 0.015) * np.where(t > dur, np.exp(-(t - dur) * 10), 1)
    return filt(x, 'low', cutoff) * env * gain / (len(notes) * 5)


def pluck(m, gain=1.0):
    t = ts(0.3)
    f = midi(m)
    x = 2 * ((f * t) % 1) - 1
    return filt(x, 'low', 3500) * np.exp(-t * 16) * np.minimum(1, t / 0.002) * gain


def vox(m, dur, gain=1.0):
    """A vowel-like lead ('ah'): a saw through three formant filters, with a little vibrato."""
    t = ts(dur + 0.12)
    f = midi(m) * (1 + 0.006 * np.sin(2 * np.pi * 5.5 * t) * np.minimum(1, t / 0.15))
    x = 2 * ((np.cumsum(f) / SR) % 1) - 1
    y = filt(x, 'band', (650, 950)) + 0.7 * filt(x, 'band', (1050, 1400)) + 0.25 * filt(x, 'band', (2400, 3000))
    env = np.minimum(1, t / 0.012) * np.where(t > dur, np.exp(-(t - dur) * 25), 1) * (0.85 + 0.15 * np.exp(-t * 6))
    return y * env * gain


def bass(m, dur, gain=1.0):
    t = ts(dur + 0.05)
    f = midi(m)
    x = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t) + 0.12 * (2 * ((f * t) % 1) - 1)
    env = np.minimum(1, t / 0.008) * np.where(t > dur, np.exp(-(t - dur) * 30), 1)
    return filt(x * env, 'low', 900) * gain


def pump(start, end):
    t = np.arange(N) / SR
    g = np.ones(N)
    on = (t >= start) & (t < end)
    g[on] = 1 - 0.7 * np.exp(-((t[on] - start) % B) / 0.09)
    return g


def build():
    chords = np.zeros((N, 2))
    lead = np.zeros((N, 2))
    drums = np.zeros((N, 2))
    sfx = np.zeros((N, 2))

    # Intro: the G chord through a closed filter, a riser and a snare roll into the drop.
    place(chords, supersaw(CHORDS['G'][0], wt.BAR, 0.9, 900), 0.0)
    place(sfx, riser(wt.BAR, 0.3), 0.0)
    for s in range(8):
        place(drums, snare(0.1 + 0.04 * s), 2 * B + s * B / 4)

    # The drop: pumping chords, arpeggios, the hook, four-on-the-floor.
    for bar in range(1, 6):
        t0 = bar * wt.BAR
        notes, root = CHORDS[BARS[bar]]
        length = wt.BAR if bar < 5 else wt.BAR / 2
        place(chords, supersaw(notes, length, 1.0), t0)
        for k in range(int(length / (B / 4))):
            place(lead, pluck(notes[k % len(notes)] + 12, 0.1), t0 + k * B / 4, 1.0, -0.4 + 0.8 * (k % 2))
        place(chords, bass(root, length * 0.95, 0.28), t0)
        for k in range(int(length / B)):
            tb = t0 + k * B
            place(drums, kick(0.5), tb)
            if k % 2 == 1:
                place(drums, clap(0.55), tb, 1.0, 0.1)
            place(drums, hat(False, 0.22), tb + B / 2, 1.0, 0.3)
    for bar, beat, m, beats in HOOK:
        place(lead, vox(m, beats * B * 0.92, 0.32), bar * wt.BAR + beat * B, 1.0, 0.0)
    for at in (wt.DROP, 3 * wt.BAR):
        place(drums, crash(0.45), at, 1.0, 0.2)
    chords *= pump(wt.DROP, wt.GRID)[:, None]

    # World switches: a whoosh into each one and the lightning that comes with it.
    place(sfx, boom(0.5), wt.DROP)
    for i in range(1, len(wt.WORLDS)):
        at = wt.world_at(i)
        place(sfx, whoosh(0.1), at - 0.12, 1.0, rng.uniform(-0.4, 0.4))
        big = i == len(wt.WORLDS) - 1
        place(sfx, zap(0.3 if big else 0.2), at + 0.02, 1.0, rng.uniform(-0.3, 0.3))
        place(sfx, thunder(0.35 if big else 0.14, 1.6, 0.4), at + 0.02, 1.0)
        if big:
            place(sfx, boom(0.25), at)

    # The grid: tiles pop in climbing, a roll, the final hit and a ringing chord.
    for i in range(9):
        place(sfx, pop(1.0 + 0.1 * i, 0.3), wt.GRID + i * 0.06, 1.0, -0.5 + 0.125 * i)
    place(chords, supersaw(CHORDS['G'][0], wt.FINAL - wt.GRID, 0.7, 1600), wt.GRID)
    for s in range(8):
        place(drums, snare(0.1 + 0.04 * s), wt.FINAL - 2 * B + s * B / 4)
    place(chords, supersaw(CHORDS['G'][0] + [74], 1.6, 1.1), wt.FINAL)
    place(chords, bass(43, 1.2, 0.3), wt.FINAL)
    place(lead, vox(79, 1.3, 0.3), wt.FINAL)
    place(drums, crash(0.55), wt.FINAL, 1.0, -0.2)
    place(drums, kick(0.6), wt.FINAL)
    place(sfx, boom(0.55), wt.FINAL)

    music = reverb(chords, 1.6, 0.22) + reverb(lead, 1.4, 0.2) + drums
    mix = music * 0.85 + sfx * 0.7
    mix = np.stack([filt(mix[:, 0], 'high', 40), filt(mix[:, 1], 'high', 40)], axis=1)
    mix = np.tanh(mix * 1.15) / np.tanh(1.15)
    k = int(0.8 * SR)
    mix[-k:] *= (np.linspace(1, 0, k) ** 2)[:, None]
    return mix / np.max(np.abs(mix)) * 0.89


def balance(x):
    mono = x.mean(axis=1)
    spec = np.abs(np.fft.rfft(mono)) ** 2
    freqs = np.fft.rfftfreq(len(mono), 1 / SR)
    total = spec.sum()
    return ' '.join('%d-%dHz %.0f%%' % (a, b, 100 * spec[(freqs >= a) & (freqs < b)].sum() / total)
                    for a, b in ((20, 250), (250, 2000), (2000, 20000)))


def main():
    os.makedirs(OUT, exist_ok=True)
    mix = build()
    path = os.path.join(OUT, 'worlds_audio.wav')
    with wave.open(path, 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes((mix * 32767).astype(np.int16).tobytes())
    rms = np.sqrt(np.mean(mix ** 2))
    print('wrote', path, '%.1f s' % (len(mix) / SR), 'rms %.1f dBFS' % (20 * np.log10(rms)), '|', balance(mix))


if __name__ == '__main__':
    main()
