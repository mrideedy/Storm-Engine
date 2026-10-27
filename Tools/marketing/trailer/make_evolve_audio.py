"""
Music and sound for reel 6 (evolve_timeline.py), growing with the island:
  day 1    a soft pad and a single marimba note
  day 3    marimba arpeggios and light hats, a pop for every machine
  day 7    the trailer's groove (drums and bass) comes in, upgrade dings
  day 30   the hook on top, lightning and thunder, a big final hit that rings out

Output: Tools/marketing/trailer/out/evolve_audio.wav (44.1 kHz, 16-bit stereo)
Usage: python make_evolve_audio.py
"""
import os
import wave

import numpy as np

import evolve_timeline as et
import make_trailer_audio as mta
from make_trailer_audio import (SR, boom, crash, ding, filt, hat, marimba, pad, place, pop, reverb, riser, rng, snare,
                                swish, thunder, zap)

N = int(SR * et.LENGTH)
OUT = os.path.join(et.HERE, 'out')
ARP = [60, 64, 67, 72, 67, 64, 60, 64]  # C major, two beats of 8ths


def build():
    music = np.zeros((N, 2))
    sfx = np.zeros((N, 2))

    # Day 1 and 3: a pad, then marimba arpeggios and light hats until the groove arrives.
    place(music, pad([60, 64, 67, 71], 4.4, 0.45, 1300, 0.5), 0.0)
    place(music, marimba(72, 0.3), 0.9, 1.0, 0.0)
    for b in range(4):  # 2.0 to 4.0 s: a C major arpeggio in 16ths
        for e in range(4):
            m = ARP[(b * 4 + e) % len(ARP)] + (12 if b % 2 else 0)
            place(music, marimba(m, 0.16), 2.0 + b * 0.5 + e * 0.125, 1.0, -0.3 + 0.2 * e)
        place(music, hat(False, 0.12), 2.25 + b * 0.5, 1.0, 0.3)
    place(music, riser(1.0, 0.22), 3.0)
    for s in range(8):
        place(music, snare(0.08 + 0.025 * s), 3.5 + s * 0.0625)

    # Day 7 onwards: the groove, with the hook from day 30's storm.
    storm = mta.groove(et.GROOVE[0], et.GROOVE[1], lead_from=et.LEAD_FROM)[:N]
    music += storm[:N]
    for at in (et.GROOVE[0], 7.0, et.LEAD_FROM):
        place(music, crash(0.4), at, 1.0, 0.2)
    end = np.zeros((N, 2))
    place(end, mta.stab([60, 64, 67, 72], 0.9, 0.9), et.FINAL)
    music += reverb(end, 2.4, 0.4)

    for t, action in et.CUES:
        kind = action.split(':')[0]
        pan = rng.uniform(-0.4, 0.4)
        if kind in ('buy', 'decor'):
            place(sfx, pop(1.0 + rng.uniform(-0.1, 0.3), 0.32), t, 1.0, pan)
        elif kind == 'level':
            place(sfx, ding(0.3), t, 1.0, pan)
        elif kind == 'equip':
            place(sfx, swish(0.25), t, 1.0, pan)
        elif kind == 'strike':
            place(sfx, zap(0.28), t, 1.0, pan)
            place(sfx, thunder(0.18, 1.4, 0.5), t + 0.02, 1.0, pan)
        elif kind == 'bigStrike':
            place(sfx, zap(0.4), t, 1.0, pan)
            place(sfx, thunder(0.5, 2.2, 1.0), t, 1.0, pan)
            place(sfx, boom(0.35 if t < et.FINAL else 0.7), t)
    for at, _ in et.STAGES[1:]:
        place(sfx, mta.whoosh(0.1), at - 0.1, 1.0, rng.uniform(-0.4, 0.4))

    mix = music * 0.8 + sfx * 0.85
    mix = np.stack([filt(mix[:, 0], 'high', 35), filt(mix[:, 1], 'high', 35)], axis=1)
    mix = np.tanh(mix * 1.15) / np.tanh(1.15)
    k = int(0.8 * SR)
    mix[-k:] *= (np.linspace(1, 0, k) ** 2)[:, None]
    return mix / np.max(np.abs(mix)) * 0.89


def main():
    os.makedirs(OUT, exist_ok=True)
    mix = build()
    path = os.path.join(OUT, 'evolve_audio.wav')
    with wave.open(path, 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes((mix * 32767).astype(np.int16).tobytes())
    rms = np.sqrt(np.mean(mix ** 2))
    print('wrote', path, '%.1f s' % (len(mix) / SR), 'rms %.1f dBFS' % (20 * np.log10(rms)))


if __name__ == '__main__':
    main()
