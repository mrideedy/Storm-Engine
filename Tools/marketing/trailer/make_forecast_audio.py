"""
Music and sound effects for reel 2 (forecast_timeline.py), synthesized from scratch:
  0-8 s    a news fanfare, then cheesy smooth "weather channel" jazz (e-piano, walking bass,
           brushes) that wobbles more with every forecast update, rain and a riser into
  8 s      the storm groove from the trailer (A minor), lightning on screen, ticking counter
  14 s     "YES!" hit, the groove at full tilt
  18 s     tape stop... and the cheesy weather music is back ("...anyway.")
  20 s     big hit, the fanfare again on the end card, ringing out

Output: Tools/marketing/trailer/out/forecast_audio.wav (44.1 kHz, 16-bit stereo)
Usage: python make_forecast_audio.py
"""
import os
import wave

import numpy as np

import forecast_timeline as ft
import make_trailer_audio as mta
from make_trailer_audio import (SR, boom, clap, crash, filt, hat, kick, midi, noise, place, pop, reverb, riser, rng,
                                sine_sweep, snare, swish, thunder, ts, whoosh, zap)

N = int(SR * ft.LENGTH)
BEAT = ft.BEAT
OUT = os.path.join(ft.HERE, 'out')

# Smooth weather jazz: one chord per bar.
JAZZ = [  # (chord, walking bass)
    ([60, 64, 67, 71], [36, 40, 43, 45]),   # Cmaj7
    ([57, 60, 64, 67], [33, 36, 40, 43]),   # Am7
    ([62, 65, 69, 72], [38, 41, 45, 48]),   # Dm7
    ([55, 59, 62, 65], [31, 35, 38, 41]),   # G7
]


def epiano(m, dur, gain=1.0, wobble=0.0):
    """FM electric piano; `wobble` adds a seasick vibrato for the comedy."""
    t = ts(dur + 0.4)
    f = midi(m) * (1 + wobble * 0.012 * np.sin(2 * np.pi * 5.2 * t + rng.random() * 6))
    ph = 2 * np.pi * np.cumsum(f) / SR
    index = 1.4 * np.exp(-t * 7) + 0.2
    x = np.sin(ph + index * np.sin(ph)) * np.exp(-t * 2.4)
    x *= np.minimum(1, t / 0.004) * np.where(t > dur, np.exp(-(t - dur) * 12), 1)
    return x * gain


def upright(m, dur, gain=1.0):
    t = ts(dur + 0.1)
    f = midi(m)
    x = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(4 * np.pi * f * t) * np.exp(-t * 9)
    return filt(x * np.minimum(1, t / 0.006) * np.exp(-t * 3.5), 'high', 40) * gain


def brass(notes, dur, gain=1.0):
    """Bright brass-ish stab for the news fanfare."""
    t = ts(dur + 0.25)
    x = np.zeros(len(t))
    for m in notes:
        for d in (-6, 0, 7):
            f = midi(m) * 2 ** (d / 1200) * (1 + 0.004 * np.sin(2 * np.pi * 5.5 * t))
            ph = np.cumsum(f) / SR
            x += 2 * (ph % 1) - 1
    cutoff = 900 + 2600 * np.minimum(1, t / 0.06)
    x = filt(x, 'low', 3200) * (0.55 + 0.45 * cutoff / 3500)
    e = np.minimum(1, t / 0.012) * np.where(t > dur, np.exp(-(t - dur) * 14), 1)
    return x * e * gain / (len(notes) * 3)


def chime(gain=1.0):
    """News alert: a bright two-tone ding-dong."""
    out = np.zeros(int(SR * 1.2))
    for i, m in enumerate((88, 84)):
        t = ts(0.9)
        tone = np.sin(2 * np.pi * midi(m) * t) * np.exp(-t * 4) + 0.3 * np.sin(2 * np.pi * midi(m) * 3 * t) * np.exp(-t * 9)
        j = int(SR * 0.16 * i)
        out[j:j + len(tone)] += tone
    return out * gain


def tick(gain=1.0):
    t = ts(0.03)
    return filt(noise(0.03), 'band', (2500, 7000)) * np.exp(-t * 180) * gain


def chirp(gain=1.0):
    """A tiny synth bird for the sunny 'anyway'."""
    t = ts(0.12)
    return sine_sweep(2600, 4200, 0.12, 0.5) * np.sin(np.pi * t / 0.12) * gain


def glitch(gain=1.0):
    """Skin swaps mid-storm: a crunchy little zap."""
    x = pop(1.8, 1.0)
    x = np.round(x * 6) / 6
    return filt(x, 'band', (600, 6000)) * gain


def jazz(start, bars, wobble_from=None, gain=1.0):
    buf = np.zeros((N, 2))
    for b in range(bars):
        t0 = start + b * 2.0
        chord, walk = JAZZ[b % 4]
        wob = 0.0 if wobble_from is None else max(0.0, (t0 - wobble_from) / 2.0 + 1.0)
        for at in (t0, t0 + 0.75):  # "Charleston" comping
            for k, m in enumerate(chord):
                place(buf, epiano(m, 0.55, 0.11, wob), at + k * 0.008, 1.0, -0.25 + 0.15 * k)
        for k, m in enumerate(walk):
            place(buf, upright(m, BEAT * 0.9, 0.42), t0 + k * BEAT)
        for k in range(4):
            tb = t0 + k * BEAT
            place(buf, hat(False, 0.13), tb, 1.0, 0.3)
            place(buf, hat(False, 0.08), tb + BEAT * 0.66, 1.0, 0.3)  # swung
            if k in (1, 3):
                place(buf, filt(snare(0.12), 'high', 900), tb, 1.0, -0.1)
            if k in (0, 2):
                place(buf, kick(0.28), tb)
    return reverb(buf, 1.3, 0.2) * gain


def fanfare(at, gain=1.0):
    buf = np.zeros((N, 2))
    for i, m in enumerate((67, 72, 76)):
        place(buf, brass([m], 0.16, 0.9), at + i * 0.25, 1.0, 0.0)
    place(buf, brass([72, 76, 79, 84], 0.9, 1.2), at + 0.75)
    place(buf, crash(0.25), at + 0.75, 1.0, 0.2)
    for s in range(6):
        place(buf, snare(0.08 + 0.02 * s), at + 0.375 + s * 0.0625)
    return reverb(buf, 1.4, 0.25) * gain


def build():
    music = np.zeros((N, 2))
    sfx = np.zeros((N, 2))

    # The news open and the smooth weather music, wobbling more with every update.
    music += fanfare(0.0, 0.9)
    music += jazz(0.0, 4, wobble_from=4.0, gain=0.95)
    rain = filt(noise(14.0), 'band', (1500, 9000)) * 0.05
    rain *= np.minimum(1, ts(14.0) / 1.5)
    place(sfx, rain, 6.0, 1.0, 0.0)
    place(music, riser(2.0, 0.32), 6.0)
    for s in range(16):
        place(music, snare(0.1 + 0.022 * s), 7.0 + s * 0.0625)

    # The storm: the trailer's groove from the drop, the hook from 10 s.
    storm = mta.groove(8.0, 18.0, lead_from=10.0, fill_bars=(16.0,))[:N]
    for at in (8.0, 10.0, 14.0):
        place(storm, crash(0.45), at, 1.0, 0.2)
    music += mta.tape_stop(storm, 17.95, 0.42)

    # ...anyway: the weather music is back as if nothing happened.
    music += jazz(18.2, 1, gain=0.9)
    for at in (18.6, 18.75, 19.3, 19.42):
        place(sfx, chirp(0.08), at, 1.0, 0.5)

    # The end: a big hit and the fanfare again.
    music += fanfare(20.0, 1.0)
    end = np.zeros((N, 2))
    place(end, brass([60, 64, 67, 72], 3.6, 1.0), 20.75)
    music += reverb(end, 2.5, 0.35) * 0.8

    for at, strength in ft.HITS:
        place(sfx, boom(0.75 * strength), at)
        place(sfx, thunder(0.7 * strength, 2.6, 1.0), at)
    for at in ft.UPDATES:
        place(sfx, chime(0.22), at + 0.02, 1.0, 0.0)
    for start, *_ in ft.SEGMENTS[1:]:
        place(sfx, whoosh(0.12), start - 0.12, 1.0, rng.uniform(-0.4, 0.4))
    # The counter rolling up to 9000%: ticks that speed up.
    t = 10.0
    while t < 14.0:
        place(sfx, tick(0.18), t, 1.0, 0.3)
        t += 0.11 - 0.085 * (t - 10.0) / 4.0

    for at, action in ft.sound_events():
        pan = rng.uniform(-0.35, 0.35)
        if action == 'strike':
            place(sfx, zap(0.3), at, 1.0, pan)
            place(sfx, thunder(0.2, 1.4, 0.5), at + 0.02, 1.0, pan)
        elif action == 'bigStrike':
            if any(abs(at - h) < 0.05 for h, _ in ft.HITS):
                continue
            place(sfx, zap(0.42), at, 1.0, pan)
            place(sfx, thunder(0.5, 2.2, 1.0), at, 1.0, pan)
            place(sfx, boom(0.3), at)
        elif action == 'equip':
            place(sfx, swish(0.3), at, 1.0, pan)
            place(sfx, glitch(0.25), at + 0.03, 1.0, -pan)

    mix = music * 0.8 + sfx * 0.85
    mix = np.stack([filt(mix[:, 0], 'high', 35), filt(mix[:, 1], 'high', 35)], axis=1)
    mix = np.tanh(mix * 1.15) / np.tanh(1.15)
    k = int(1.6 * SR)
    mix[-k:] *= (np.linspace(1, 0, k) ** 2)[:, None]
    return mix / np.max(np.abs(mix)) * 0.89


def main():
    os.makedirs(OUT, exist_ok=True)
    mix = build()
    path = os.path.join(OUT, 'forecast_audio.wav')
    with wave.open(path, 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes((mix * 32767).astype(np.int16).tobytes())
    rms = np.sqrt(np.mean(mix ** 2))
    print('wrote', path, '%.1f s' % (len(mix) / SR), 'rms %.1f dBFS' % (20 * np.log10(rms)))


if __name__ == '__main__':
    main()
