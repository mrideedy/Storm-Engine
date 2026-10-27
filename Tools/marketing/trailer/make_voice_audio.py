"""
Voice, music and sound effects for reel 4 (voice_timeline.py):
  - the voice-over, spoken by Kokoro (Apache-2.0 open model) line by line into out/voice/line_NN.wav
  - the trailer's storm groove as a light bed that ducks under the voice
  - game sounds (machine pops, lightning, style swaps) and a big hit with a brass chord on the end card

Output: Tools/marketing/trailer/out/voice_audio.wav (44.1 kHz, 16-bit stereo)
Usage: python make_voice_audio.py [--kokoro <folder with kokoro-v1.0.fp16.onnx and voices-v1.0.bin>]
                                  [--voice af_heart] [--speed 1.05]
"""
import os
import sys
import wave

import numpy as np
from scipy import signal

import make_forecast_audio as mfa
import make_trailer_audio as mta
import voice_timeline as vt
from make_trailer_audio import SR, boom, filt, place, pop, reverb, rng, swish, thunder, whoosh, zap

KOKORO = r"D:\Projects\Don't Answer\Tools\kokoro"
OUT = os.path.join(vt.HERE, 'out')


def arg(name, default):
    return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else default


def write_wav(path, x, rate, channels):
    with wave.open(path, 'wb') as f:
        f.setnchannels(channels)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def read_wav(path):
    with wave.open(path) as f:
        return np.frombuffer(f.readframes(f.getnframes()), np.int16).astype(np.float32) / 32767


def speak():
    """Speak every line at 44.1 kHz, trimmed to the voice, a little brighter and evened out."""
    from kokoro_onnx import Kokoro
    folder = arg('--kokoro', KOKORO)
    tts = Kokoro(os.path.join(folder, 'kokoro-v1.0.fp16.onnx'), os.path.join(folder, 'voices-v1.0.bin'))
    voice, speed = arg('--voice', 'af_heart'), float(arg('--speed', '1.05'))
    os.makedirs(vt.VOICE_DIR, exist_ok=True)
    for i, line in enumerate(vt.LINES):
        samples, rate = tts.create(line[3], voice=voice, speed=speed, lang='en-us')
        x = signal.resample_poly(np.asarray(samples, np.float64), SR, rate)
        loud = np.flatnonzero(np.abs(x) > 0.02)
        x = x[max(0, loud[0] - int(0.03 * SR)):loud[-1] + int(0.08 * SR)]
        x = filt(x, 'high', 90)
        x = x + 0.25 * filt(x, 'band', (2500, 6000))  # presence
        x = np.tanh(x / np.max(np.abs(x)) * 1.6) / np.tanh(1.6) * 0.9  # gentle compression
        write_wav(vt.line_path(i), x, SR, 1)
        print('line %d: %.2f s  %s' % (i, len(x) / SR, line[3]))


def build():
    segments, captions, end_card, length = vt.edit()
    n = int(SR * length)
    voice = np.zeros(n)
    for (start, _), i in zip(vt.spans(), range(len(vt.LINES))):
        x = read_wav(vt.line_path(i))
        a = int(start * SR)
        voice[a:a + len(x)] += x[:max(0, n - a)]

    # The bed: the storm groove without its lead, ducking under the voice.
    music = mta.groove(0.0, end_card + 0.01)[:n]
    music = np.stack([filt(music[:, 0], 'high', 80), filt(music[:, 1], 'high', 80)], axis=1)
    k = int(0.02 * SR)
    level = np.convolve(np.abs(voice), np.ones(k) / k, mode='same')
    duck = np.where(level > 0.02, 0.38, 1.0)
    duck = signal.lfilter([0.00015], [1, -0.99985], duck)  # smoothed over about 0.15 s
    music *= duck[:, None]
    end = np.zeros((n, 2))
    place(end, mfa.brass([60, 64, 67, 72], 2.4, 1.0), end_card)
    music += reverb(end, 2.5, 0.35) * 0.8

    sfx = np.zeros((n, 2))
    place(sfx, boom(0.5), end_card)
    place(sfx, thunder(0.45, 2.6, 1.0), end_card)
    for start, *_ in segments[1:]:
        place(sfx, whoosh(0.1), start - 0.1, 1.0, rng.uniform(-0.4, 0.4))
    for at, action in vt.sound_events(segments):
        pan = rng.uniform(-0.35, 0.35)
        if action == 'strike':
            place(sfx, zap(0.22), at, 1.0, pan)
            place(sfx, thunder(0.16, 1.4, 0.5), at + 0.02, 1.0, pan)
        elif action == 'bigStrike':
            if abs(at - end_card) < 0.05:
                continue
            place(sfx, zap(0.32), at, 1.0, pan)
            place(sfx, thunder(0.38, 2.2, 1.0), at, 1.0, pan)
            place(sfx, boom(0.22), at)
        elif action == 'equip':
            place(sfx, swish(0.12), at, 1.0, pan)
        elif action in ('buy', 'decor'):
            place(sfx, pop(1.0 + rng.uniform(-0.1, 0.25), 0.3), at, 1.0, pan)

    mix = music * 0.5 + sfx * 0.6 + voice[:, None] * np.array([1.0, 1.0])
    mix = np.stack([filt(mix[:, 0], 'high', 35), filt(mix[:, 1], 'high', 35)], axis=1)
    mix = np.tanh(mix * 1.1) / np.tanh(1.1)
    k = int(1.2 * SR)
    mix[-k:] *= (np.linspace(1, 0, k) ** 2)[:, None]
    return mix / np.max(np.abs(mix)) * 0.89


def main():
    if '--no-speak' not in sys.argv:
        speak()
    mix = build()
    path = os.path.join(OUT, 'voice_audio.wav')
    write_wav(path, mix.reshape(-1), SR, 2)
    rms = np.sqrt(np.mean(mix ** 2))
    print('wrote', path, '%.1f s' % (len(mix) / SR), 'rms %.1f dBFS' % (20 * np.log10(rms)))


if __name__ == '__main__':
    main()
