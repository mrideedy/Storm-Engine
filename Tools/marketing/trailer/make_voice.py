"""
Reel 4, "Hi, welcome to Storm Engine!" (voice_timeline.py): fresh takes cut to the voice-over, a short
caption per line for muted viewers, then the logo and "COMING SOON" end card. Vertical 1080x1920, 120 fps.

Output: out/storm_engine_reel_welcome_120fps.mp4
Usage: python make_voice.py <voice recorder folder> <reels recorder folder> [--preview sheet.jpg]
"""
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image

import voice_timeline as vt
from make_trailer import (END_LINE, END_SUB, FORMATS, LOGO, Captions, Takes, back_out, end_shade, paste_scaled)

FMT = dict(FORMATS['reels'], fps=vt.FPS)
W, H = FMT['size']
FPS = vt.FPS
OUT = os.path.join(vt.HERE, 'out')
FILE = 'storm_engine_reel_welcome_120fps.mp4'


class Composer:
    """Builds any frame of the reel on its own (so single moments can be previewed)."""

    def __init__(self, voice, reels):
        self.segments, self.captions_at, self.end_card, self.length = vt.edit()
        self.hits = [(self.end_card, 0.7)]
        self.takes = {'voice': Takes(voice, FMT), 'reels': Takes(reels, FMT)}
        self.captions = Captions(FMT)
        logo = Image.open(LOGO).convert('RGBA')
        ex, ey, ew = FMT['end_logo']
        self.logo_end = logo.resize((ew, int(logo.height * ew / logo.width)), Image.LANCZOS)
        self.shade = end_shade(FMT)
        self.rng = np.random.default_rng(11)

    def segment_at(self, t):
        for seg in self.segments:
            if seg[0] <= t < seg[1]:
                return seg
        return self.segments[-1]

    def frame(self, t):
        rng, captions, end_card = self.rng, self.captions, self.end_card
        start, end, rec, take, take_in = self.segment_at(t)
        frame = self.takes[rec].frame(take, take_in + (t - start))

        for at, strength in self.hits:
            if 0 <= t - at < 0.4:
                k = math.exp(-(t - at) * 9) * 9 * strength
                frame = frame.transform(frame.size, Image.AFFINE, (1, 0, rng.uniform(-k, k), 0, 1, rng.uniform(-k, k)),
                                        resample=Image.BILINEAR)

        for c_start, c_end, text in self.captions_at:
            if c_start <= t < c_end:
                img = captions.get(text, 'punch')
                fade = min(1.0, (c_end - t) / 0.15) if c_start == 0 else min(1.0, (t - c_start) / 0.15, (c_end - t) / 0.15)
                pop_in = 1.0 + 0.12 * (1.0 - back_out(min(1.0, (t - c_start) / 0.2)))
                px, py = FMT['positions']['low']
                paste_scaled(frame, img, W * px, H * py, pop_in * (1.0 + 0.04 * (t - c_start)), fade)

        if t >= end_card:
            frame.alpha_composite(self.shade)
            ex, ey, _ = FMT['end_logo']
            p = min(1.0, (t - end_card) / 0.3)
            paste_scaled(frame, self.logo_end, W * ex, H * ey, 2.0 + (1.0 - 2.0) * back_out(p), min(1.0, p * 4))
            x, y, s = FMT['end_line']
            if t >= end_card + 0.55:
                q = min(1.0, (t - end_card - 0.55) / 0.2)
                pulse = 1.0 + 0.03 * math.sin((t - end_card) * math.pi * 2)
                paste_scaled(frame, captions.get(END_LINE, 'hot', False), W * x, H * y,
                             (1.5 + (1.0 - 1.5) * back_out(q)) * s * pulse, min(1.0, q * 3), -2.0)
            x, y, s = FMT['end_sub']
            if t >= end_card + 0.85:
                q = min(1.0, (t - end_card - 0.85) / 0.2)
                paste_scaled(frame, captions.get(END_SUB, 'punch', False), W * x, H * y,
                             (1.5 + (1.0 - 1.5) * back_out(q)) * s, min(1.0, q * 3))

        rgb = frame.convert('RGB')
        white = 0.0
        for at, strength in self.hits:
            if t >= at:
                white = max(white, strength * math.exp(-(t - at) * 7))
        fade = min(1.0, (self.length - t) / 0.6)
        if white > 0.01 or fade < 1.0:
            arr = np.asarray(rgb, np.float32)
            if white > 0.01:
                arr = arr + (255 - arr) * min(1.0, white)
            if fade < 1.0:
                arr *= max(0.0, fade)
            rgb = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
        return rgb


def preview(composer, path):
    times = [(s + e) / 2 for s, e, *_ in composer.segments[:-1]] + [composer.end_card + 1.4]
    tiles = [composer.frame(t).resize((216, 384), Image.LANCZOS) for t in times]
    sheet = Image.new('RGB', (216 * len(tiles), 384))
    for i, tile in enumerate(tiles):
        sheet.paste(tile, (216 * i, 0))
    sheet.save(path, quality=85)
    print('wrote', path, 'length %.2f s' % composer.length)


def main():
    composer = Composer(sys.argv[1], sys.argv[2])
    if '--preview' in sys.argv:
        preview(composer, sys.argv[sys.argv.index('--preview') + 1])
        return
    os.makedirs(OUT, exist_ok=True)
    master = os.path.join(OUT, FILE)
    audio = os.path.join(OUT, 'voice_audio.wav')
    cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (W, H),
           '-r', str(FPS), '-i', '-', '-i', audio, '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
           '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'aac', '-b:a', '192k', '-shortest',
           '-movflags', '+faststart', master]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(int(composer.length * FPS)):
        proc.stdin.write(composer.frame(n / FPS).tobytes())
        if n % (FPS * 4) == 0:
            print('  %.0f s' % (n / FPS), flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', master, 'exit', proc.returncode)


if __name__ == '__main__':
    main()
