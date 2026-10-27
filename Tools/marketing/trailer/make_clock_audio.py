"""
Music and sound effects for reel 3 (clock_timeline.py), synthesized from scratch:
  0-6 s    cozy jazz ("just 5 minutes"), a tick-tock at every clock jump, a riser and a snare roll into
  6 s      the trailer's storm groove: lightning, skin swaps and fast ticks while the clock races
  12 s     the 3:00 AM hit
  13 s     a big hit and a brass chord on the end card, ringing out

Output: Tools/marketing/trailer/out/clock_audio.wav (44.1 kHz, 16-bit stereo)
Usage: python make_clock_audio.py
"""
import os
import wave

import numpy as np

import clock_timeline as ct
import make_forecast_audio as mfa
import make_trailer_audio as mta
from make_trailer_audio import (SR, boom, crash, ding, filt, noise, place, pop, reverb, riser, rng, snare, swish,
                                thunder, ts, whoosh, zap)

N = int(SR * ct.LENGTH)
OUT = os.path.join(ct.HERE, 'out')


def tock(gain=1.0):
    """The lower half of a tick-tock."""
    return filt(noise(0.04), 'band', (900, 2600)) * np.exp(-ts(0.04) * 140) * gain


def build():
    music = np.zeros((N, 2))
    sfx = np.zeros((N, 2))

    # Cozy "just 5 minutes" jazz, gone by the drop; a riser and a snare roll into it.
    jazz = mfa.jazz(0.0, 3, gain=0.95)[:N]
    env = np.ones(N)
    a, b = int(5.6 * SR), int(6.0 * SR)
    env[a:b] = np.linspace(1, 0, b - a)
    env[b:] = 0
    music += jazz * env[:, None]
    place(music, riser(2.0, 0.32), 4.0)
    for s in range(16):
        place(music, snare(0.1 + 0.022 * s), 5.0 + s * 0.0625)

    # The storm: the trailer's groove from the drop, with a fill into 3:00 AM.
    storm = mta.groove(6.0, 13.0, lead_from=8.0, fill_bars=(10.0,))[:N]
    for at in (6.0, 8.0, 10.0, 12.0):
        place(storm, crash(0.45), at, 1.0, 0.2)
    music += storm

    # The end card: a brass chord ringing out.
    end = np.zeros((N, 2))
    place(end, mfa.brass([60, 64, 67, 72], 2.6, 1.0), 13.0)
    music += reverb(end, 2.5, 0.35) * 0.85

    for at, strength in ct.HITS:
        place(sfx, boom(0.75 * strength), at)
        place(sfx, thunder(0.7 * strength, 2.6, 1.0), at)
    for at in ct.JUMPS[:3]:
        place(sfx, mfa.tick(0.3), at, 1.0, 0.3)
        place(sfx, tock(0.3), at + 0.25, 1.0, 0.3)
    t = ct.RACE[0]
    while t < ct.RACE[1]:  # the clock racing
        place(sfx, mfa.tick(0.12), t, 1.0, 0.35)
        t += 0.0625
    for start, *_ in ct.SEGMENTS[1:]:
        place(sfx, whoosh(0.12), start - 0.12, 1.0, rng.uniform(-0.4, 0.4))

    for at, action in ct.sound_events():
        pan = rng.uniform(-0.35, 0.35)
        if action == 'strike':
            place(sfx, zap(0.3), at, 1.0, pan)
            place(sfx, thunder(0.2, 1.4, 0.5), at + 0.02, 1.0, pan)
        elif action == 'bigStrike':
            if any(abs(at - h) < 0.05 for h, _ in ct.HITS):
                continue
            place(sfx, zap(0.42), at, 1.0, pan)
            place(sfx, thunder(0.5, 2.2, 1.0), at, 1.0, pan)
            place(sfx, boom(0.3), at)
        elif action == 'equip':
            place(sfx, swish(0.3), at, 1.0, pan)
            place(sfx, mfa.glitch(0.25), at + 0.03, 1.0, -pan)
        elif action == 'buy':
            place(sfx, pop(1.0 + rng.uniform(-0.1, 0.2), 0.35), at, 1.0, pan)
        elif action == 'level':
            place(sfx, ding(0.3), at, 1.0, pan)

    mix = music * 0.8 + sfx * 0.85
    mix = np.stack([filt(mix[:, 0], 'high', 35), filt(mix[:, 1], 'high', 35)], axis=1)
    mix = np.tanh(mix * 1.15) / np.tanh(1.15)
    k = int(1.6 * SR)
    mix[-k:] *= (np.linspace(1, 0, k) ** 2)[:, None]
    return mix / np.max(np.abs(mix)) * 0.89


def main():
    os.makedirs(OUT, exist_ok=True)
    mix = build()
    path = os.path.join(OUT, 'clock_audio.wav')
    with wave.open(path, 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes((mix * 32767).astype(np.int16).tobytes())
    rms = np.sqrt(np.mean(mix ** 2))
    print('wrote', path, '%.1f s' % (len(mix) / SR), 'rms %.1f dBFS' % (20 * np.log10(rms)))


if __name__ == '__main__':
    main()
