"""
Lofi music for reel 7 (cozy_timeline.py), synthesized from scratch, nothing shared with the storm groove:
  80 BPM, Fmaj9 - Em7 - Dm9 - Cmaj9 on a warm electric piano with a little tape wobble, a soft sine bass,
  dusty swung drums from the second bar, a small bell melody, vinyl crackle, steady rain and one distant
  thunder roll, then a vinyl-style slow-down at the end.

Output: Tools/marketing/trailer/out/cozy_audio.wav (44.1 kHz, 16-bit stereo)
Usage: python make_cozy_audio.py
"""
import os
import wave

import numpy as np

import cozy_timeline as ct
import make_forecast_audio as mfa
import make_trailer_audio as mta
from make_trailer_audio import SR, filt, hat, kick, marimba, midi, noise, place, reverb, rng, snare, thunder, ts

N = int(SR * ct.LENGTH)
B = ct.BEAT
OUT = os.path.join(ct.HERE, 'out')
CHORDS = [  # electric piano voicing, bass root
    ([65, 69, 72, 76, 79], 41),  # Fmaj9
    ([64, 67, 71, 74], 40),      # Em7
    ([62, 65, 69, 72, 76], 38),  # Dm9
    ([60, 64, 67, 71, 74], 36),  # Cmaj9
]
MELODY = [(0.0, 76), (0.5, 79), (1.0, 81), (2.0, 79), (2.5, 76), (4.0, 74), (4.5, 72), (5.0, 76), (6.0, 74)]  # beats


def soft_bass(m, dur, gain=1.0):
    t = ts(dur + 0.15)
    f = midi(m)
    x = np.sin(2 * np.pi * f * t) + 0.25 * np.sin(4 * np.pi * f * t)
    env = np.minimum(1, t / 0.012) * np.exp(-t * 1.1) * np.where(t > dur, np.exp(-(t - dur) * 18), 1)
    return filt(x * env, 'low', 480) * gain


def swing(beat):
    """Off-beat eighths land late, like a lazy drummer."""
    whole, frac = divmod(beat, 1.0)
    return (whole + (0.62 if abs(frac - 0.5) < 1e-6 else frac)) * B


def build():
    keys = np.zeros((N, 2))
    drums = np.zeros((N, 2))
    amb = np.zeros((N, 2))

    for bar, (chord, root) in enumerate(CHORDS):
        t0 = bar * ct.BAR
        for beat, voices, length in ((0.0, chord, 2.6), (1.5, chord[1:], 1.1), (3.0, chord[2:], 0.9)):
            for k, m in enumerate(voices):
                place(keys, mfa.epiano(m, length * B, 0.12, 0.22), t0 + swing(beat) + k * 0.014, 1.0, -0.3 + 0.15 * k)
        place(keys, soft_bass(root, 1.4 * B, 0.24), t0)
        place(keys, soft_bass(root, 1.2 * B, 0.2), t0 + swing(2.5))
        if bar >= 1:  # the beat comes in on the second bar
            for beat in (0.0, 2.5):
                place(drums, filt(filt(kick(0.36), 'low', 3000), 'high', 55), t0 + swing(beat))
            for beat in (1.0, 3.0):
                place(drums, filt(snare(0.32), 'band', (180, 3500)), t0 + swing(beat), 1.0, -0.05)
            for e in range(8):
                place(drums, filt(hat(False, 0.15 if e % 2 else 0.21), 'low', 6000), t0 + swing(e * 0.5), 1.0, 0.25)
        if bar >= 2:  # a small bell melody over the last two bars
            for beat, m in MELODY:
                at = 2 * ct.BAR + swing(beat)
                if at < ct.LENGTH:
                    place(keys, marimba(m, 0.17), at, 1.0, 0.2)

    music = reverb(keys, 1.8, 0.28) + reverb(drums, 0.8, 0.12)
    music = np.stack([filt(music[:, 0], 'low', 9000), filt(music[:, 1], 'low', 9000)], axis=1)
    music = mta.tape_stop(music, ct.LENGTH - 0.75, 0.6)  # vinyl slow-down at the very end

    rain = filt(noise(ct.LENGTH + 0.2), 'band', (1200, 9000))[:N] * 0.075
    amb[:, 0] += rain
    amb[:, 1] += np.roll(rain, 2205)
    hiss = filt(noise(ct.LENGTH + 0.2), 'low', 4000)[:N] * 0.006
    amb += hiss[:, None]
    for _ in range(int(ct.LENGTH * 14)):  # vinyl crackle
        click = filt(noise(0.004), 'high', 2000) * rng.uniform(0.01, 0.05)
        place(amb, click, rng.uniform(0, ct.LENGTH - 0.01), 1.0, rng.uniform(-0.6, 0.6))
    place(amb, thunder(0.25, 3.0, 0.2), ct.STRIKE, 1.0, -0.2)

    mix = music * 0.9 + amb
    mix = np.stack([filt(mix[:, 0], 'high', 35), filt(mix[:, 1], 'high', 35)], axis=1)
    mix = np.tanh(mix * 1.1) / np.tanh(1.1)
    k = int(1.0 * SR)
    mix[-k:] *= (np.linspace(1, 0, k) ** 2)[:, None]
    return mix / np.max(np.abs(mix)) * 0.89


def main():
    os.makedirs(OUT, exist_ok=True)
    mix = build()
    path = os.path.join(OUT, 'cozy_audio.wav')
    with wave.open(path, 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes((mix * 32767).astype(np.int16).tobytes())
    rms = np.sqrt(np.mean(mix ** 2))
    print('wrote', path, '%.1f s' % (len(mix) / SR), 'rms %.1f dBFS' % (20 * np.log10(rms)))


if __name__ == '__main__':
    main()
