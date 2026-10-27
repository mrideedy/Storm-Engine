"""
Reel 3, "Me: just 5 minutes" (clock_timeline.py): the recorded takes with a clock that jumps from 9 PM to
midnight and then races to 3 AM while the storms get bigger, captions, then the logo and "COMING SOON"
end card. Vertical 1080x1920 at 120 fps.

Output: out/storm_engine_reel_5min_120fps.mp4
Usage: python make_clock.py <reels recorder folder> <forecast recorder folder> [--preview sheet.jpg]
"""
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image

import clock_timeline as ct
import make_trailer as mt
from make_trailer import (END_LINE, END_SUB, FORMATS, LOGO, Captions, Takes, back_out, end_shade, game_line,
                          paste_scaled)

mt.STYLES['alarm'] = ((255, 160, 130), (232, 46, 34))
FMT = dict(FORMATS['reels'], fps=ct.FPS)
W, H = FMT['size']
FPS = ct.FPS
OUT = os.path.join(ct.HERE, 'out')
FILE = 'storm_engine_reel_5min_120fps.mp4'
CLOCK_AT = (W / 2, H * 0.2)

_clocks = {}


def clock_image(text, style):
    key = (text, style)
    if key not in _clocks:
        _clocks[key] = game_line(text, style, 124)
    return _clocks[key]


def segment_at(t):
    for seg in ct.SEGMENTS:
        if seg[0] <= t < seg[1]:
            return seg
    return ct.SEGMENTS[-1]


class Composer:
    """Builds any frame of the reel on its own (so single moments can be previewed)."""

    def __init__(self, reels, forecast):
        self.takes = {'reels': Takes(reels, FMT), 'forecast': Takes(forecast, FMT)}
        self.captions = Captions(FMT)
        logo = Image.open(LOGO).convert('RGBA')
        ex, ey, ew = FMT['end_logo']
        self.logo_end = logo.resize((ew, int(logo.height * ew / logo.width)), Image.LANCZOS)
        self.shade = end_shade(FMT)
        self.rng = np.random.default_rng(7)

    def frame(self, t):
        rng, captions = self.rng, self.captions
        start, end, rec, take, take_in = segment_at(t)
        frame = self.takes[rec].frame(take, take_in + (t - start))

        for at, strength in ct.HITS:
            if 0 <= t - at < 0.4:
                k = math.exp(-(t - at) * 9) * 9 * strength
                frame = frame.transform(frame.size, Image.AFFINE, (1, 0, rng.uniform(-k, k), 0, 1, rng.uniform(-k, k)),
                                        resample=Image.BILINEAR)

        if t < ct.CLOCK_UNTIL:
            minutes = ct.minutes_at(t)
            style = 'alarm' if t >= ct.THREE_AM else ('hot' if minutes >= 24 * 60 else 'punch')
            img = clock_image(ct.clock_text(minutes), style)
            cx, cy = CLOCK_AT
            scale, angle = 1.0, 0.0
            for u in ct.JUMPS:
                if 0 <= t - u < 0.3:
                    scale *= 1.0 + 0.3 * (1.0 - back_out((t - u) / 0.3))
            if t >= ct.THREE_AM:
                cx += rng.uniform(-7, 7)
                cy += rng.uniform(-7, 7)
                angle = rng.uniform(-3, 3)
            paste_scaled(frame, img, cx, cy, scale, 1.0, angle)

        for c_start, c_end, text, style in ct.CAPTIONS:
            if c_start <= t < c_end:
                img = captions.get(text, style)
                fade = min(1.0, (c_end - t) / 0.2) if c_start == 0 else min(1.0, (t - c_start) / 0.2, (c_end - t) / 0.2)
                px, py = FMT['positions']['low']
                paste_scaled(frame, img, W * px, H * py, 1.0 + 0.05 * (t - c_start), fade)

        if t >= ct.END_CARD:
            frame.alpha_composite(self.shade)
            ex, ey, _ = FMT['end_logo']
            p = min(1.0, (t - ct.END_CARD) / 0.3)
            paste_scaled(frame, self.logo_end, W * ex, H * ey, 2.0 + (1.0 - 2.0) * back_out(p), min(1.0, p * 4))
            x, y, s = FMT['end_line']
            if t >= ct.END_CARD + 0.55:
                q = min(1.0, (t - ct.END_CARD - 0.55) / 0.2)
                pulse = 1.0 + 0.03 * math.sin((t - ct.END_CARD) * math.pi * 2 / ct.BEAT / 2)
                paste_scaled(frame, captions.get(END_LINE, 'hot', False), W * x, H * y,
                             (1.5 + (1.0 - 1.5) * back_out(q)) * s * pulse, min(1.0, q * 3), -2.0)
            x, y, s = FMT['end_sub']
            if t >= ct.END_CARD + 0.85:
                q = min(1.0, (t - ct.END_CARD - 0.85) / 0.2)
                paste_scaled(frame, captions.get(END_SUB, 'punch', False), W * x, H * y,
                             (1.5 + (1.0 - 1.5) * back_out(q)) * s, min(1.0, q * 3))

        rgb = frame.convert('RGB')
        white = 0.0
        for at, strength in ct.HITS:
            if t >= at:
                white = max(white, strength * math.exp(-(t - at) * 7))
        fade = min(1.0, (ct.LENGTH - t) / 0.6)
        if white > 0.01 or fade < 1.0:
            arr = np.asarray(rgb, np.float32)
            if white > 0.01:
                arr = arr + (255 - arr) * min(1.0, white)
            if fade < 1.0:
                arr *= max(0.0, fade)
            rgb = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
        return rgb


def preview(composer, path, times=(0.5, 2.5, 3.5, 5.0, 6.15, 8.0, 10.6, 12.2, 14.5)):
    tiles = [composer.frame(t).resize((270, 480), Image.LANCZOS) for t in times]
    sheet = Image.new('RGB', (270 * len(tiles), 480))
    for i, tile in enumerate(tiles):
        sheet.paste(tile, (270 * i, 0))
    sheet.save(path, quality=85)
    print('wrote', path)


def main():
    reels, forecast = sys.argv[1], sys.argv[2]
    composer = Composer(reels, forecast)
    if '--preview' in sys.argv:
        preview(composer, sys.argv[sys.argv.index('--preview') + 1])
        return
    os.makedirs(OUT, exist_ok=True)
    master = os.path.join(OUT, FILE)
    audio = os.path.join(OUT, 'clock_audio.wav')
    cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (W, H),
           '-r', str(FPS), '-i', '-', '-i', audio, '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
           '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'aac', '-b:a', '192k', '-shortest',
           '-movflags', '+faststart', master]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(int(ct.LENGTH * FPS)):
        proc.stdin.write(composer.frame(n / FPS).tobytes())
        if n % (FPS * 4) == 0:
            print('  %.0f s' % (n / FPS), flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', master, 'exit', proc.returncode)


if __name__ == '__main__':
    main()
