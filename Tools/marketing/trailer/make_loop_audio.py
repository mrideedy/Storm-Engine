"""
Music and sound for reel 5 (loop_timeline.py), made to loop:
  calm     a soft pad, birds and wind (the pad from the end carries over the loop point)
  0.6-4 s  the storm builds: charge hum, rain, rumbles, a riser and a snare roll
  4-8 s    the trailer's groove drops with the lightning on screen
  8-10 s   it clears: the rain fades, a few marimba notes, the pad returns
Everything past 10 s wraps round into the start, so the loop has no seam.

Output: Tools/marketing/trailer/out/loop_audio.wav (44.1 kHz, 16-bit stereo)
Usage: python make_loop_audio.py
"""
import os
import wave

import numpy as np

import loop_timeline as lt
import make_forecast_audio as mfa
import make_trailer_audio as mta
from make_trailer_audio import (SR, boom, crash, filt, marimba, noise, pad, place, riser, rng, sine_sweep, snare,
                                thunder, ts, zap)

N = int(SR * lt.LENGTH)
TOTAL = N + int(SR * 5.0)
OUT = os.path.join(lt.HERE, 'out')


def envelope(points):
    """Piecewise-linear gain over the buffer from [(time, gain)]."""
    t = np.arange(TOTAL) / SR
    xs, ys = zip(*points)
    return np.interp(t, xs, ys)


def build():
    calm = np.zeros((TOTAL, 2))
    music = np.zeros((TOTAL, 2))
    sfx = np.zeros((TOTAL, 2))

    # Calm: one pad from the clearing, carried over the loop point into the build.
    place(calm, pad([60, 64, 67, 71, 74], 6.2, 0.55, 1400, 0.9), lt.CLEAR + 0.2)
    for at in (0.35, 0.6, 1.5, 1.68, 9.05, 9.3, 9.75):
        place(calm, mfa.chirp(0.07), at, 1.0, rng.uniform(-0.6, 0.6))
    wind = filt(noise(TOTAL / SR + 0.1), 'band', (300, 1400))[:TOTAL] * 0.035
    calm[:, 0] += wind
    calm[:, 1] += np.roll(wind, 2000)

    # The build: charge hum, rain and rumbles, a riser and a snare roll into the drop.
    hum = sine_sweep(320, 880, 3.3, 1.5) * np.minimum(1, ts(3.3) / 0.5) * 0.05
    place(sfx, hum, 0.6)
    rain = filt(noise(TOTAL / SR + 0.1), 'band', (1500, 9000))[:TOTAL] * 0.07
    rain *= envelope([(0, 0), (1.0, 0), (3.8, 0.8), (4.0, 1.0), (8.1, 1.0), (9.8, 0), (TOTAL / SR, 0)])
    sfx[:, 0] += rain
    sfx[:, 1] += np.roll(rain, 1500)
    for at in (2.0, 3.0):
        place(sfx, thunder(0.22, 2.4, 0.15), at, 1.0, rng.uniform(-0.4, 0.4))
    place(music, riser(2.6, 0.3), 1.4)
    for s in range(16):
        place(music, snare(0.1 + 0.022 * s), 3.0 + s * 0.0625)

    # The drop: the groove with its hook, lightning on screen, a big hit as it ends.
    storm = mta.groove(lt.DROP, lt.CLEAR, lead_from=lt.DROP)[:TOTAL]
    music[:len(storm)] += storm
    for at in (lt.DROP, 6.0):
        place(music, crash(0.45), at, 1.0, 0.2)
    place(sfx, boom(0.6), lt.DROP)
    for at, big in lt.STRIKES:
        pan = rng.uniform(-0.35, 0.35)
        if big:
            place(sfx, zap(0.4), at, 1.0, pan)
            place(sfx, thunder(0.5, 2.2, 1.0), at, 1.0, pan)
            place(sfx, boom(0.3), at)
        else:
            place(sfx, zap(0.28), at, 1.0, pan)
            place(sfx, thunder(0.18, 1.4, 0.5), at + 0.02, 1.0, pan)
    place(music, crash(0.5), lt.CLEAR, 1.0, -0.2)
    place(sfx, boom(0.45), lt.CLEAR)

    # The clearing: a few bright marimba notes as the sun comes back.
    for i, m in enumerate((72, 76, 79, 84)):
        place(music, marimba(m, 0.22), lt.CLEAR + 0.6 + i * 0.25, 1.0, -0.3 + 0.2 * i)

    mix = calm * 0.9 + music * 0.8 + sfx * 0.85
    loop = mix[:N].copy()
    loop += mix[N:N * 2] if TOTAL >= N * 2 else np.pad(mix[N:], ((0, N * 2 - TOTAL), (0, 0)))
    loop = np.stack([filt(loop[:, 0], 'high', 35), filt(loop[:, 1], 'high', 35)], axis=1)
    loop = np.tanh(loop * 1.15) / np.tanh(1.15)
    return loop / np.max(np.abs(loop)) * 0.89


def main():
    os.makedirs(OUT, exist_ok=True)
    mix = build()
    path = os.path.join(OUT, 'loop_audio.wav')
    with wave.open(path, 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes((mix * 32767).astype(np.int16).tobytes())
    rms = np.sqrt(np.mean(mix ** 2))
    print('wrote', path, '%.1f s' % (len(mix) / SR), 'rms %.1f dBFS' % (20 * np.log10(rms)))


if __name__ == '__main__':
    main()
