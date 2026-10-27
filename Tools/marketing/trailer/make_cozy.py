"""
Reel 7 (cozy_timeline.py): three rainy-night takes joined by soft dissolves, a gentle vignette, native-looking
text (make_loop.caption) and a pulsing "link in bio" at the end. Vertical 1080x1920 at 120 fps.

Output: out/storm_engine_reel_cozy_120fps.mp4
Usage: python make_cozy.py <cozy recorder folder> [--lift 1.0] [--preview sheet.jpg]
"""
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image

import cozy_timeline as ct
from make_loop import caption
from make_trailer import FORMATS, Takes, back_out, paste_scaled

FMT = dict(FORMATS['reels'], fps=ct.FPS)
W, H = FMT['size']
FPS = ct.FPS
TEXT_Y, LINK_Y = 0.22, 0.31
OUT = os.path.join(ct.HERE, 'out')
FILE = 'storm_engine_reel_cozy_120fps.mp4'


def arg(name, default):
    return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else default


def vignette():
    y, x = np.mgrid[0:H, 0:W]
    r = np.sqrt(((x - W / 2) / (W / 2)) ** 2 + ((y - H / 2) / (H / 2)) ** 2)
    alpha = np.clip((r - 0.65) / 0.75, 0, 1) ** 1.6 * 110
    img = np.zeros((H, W, 4), np.uint8)
    img[..., :3] = (8, 10, 24)
    img[..., 3] = alpha.astype(np.uint8)
    return Image.fromarray(img, 'RGBA')


class Composer:
    def __init__(self, folder, lift=1.0):
        self.takes = Takes(folder, FMT)
        self.text = {line: caption(line) for _, _, line in ct.TEXT}
        self.link = caption(ct.LINK[2])
        self.shade = vignette()
        self.lift = lift

    def shot(self, i, t):
        start, _, take = ct.SHOTS[i]
        return self.takes.frame(take, t - start)

    def frame(self, t):
        frame = None
        for i, (start, end, _) in enumerate(ct.SHOTS):
            if not (start <= t < end):
                continue
            img = self.shot(i, t)
            frame = img if frame is None else Image.blend(frame, img, min(1.0, (t - start) / ct.FADE))
        frame.alpha_composite(self.shade)
        for start, end, line in ct.TEXT:
            if start <= t < end:
                p = 1.0 if start == 0 else min(1.0, (t - start) / 0.25)
                fade = min(1.0, (end - t) / 0.2) if end < ct.LENGTH else 1.0
                paste_scaled(frame, self.text[line], W / 2, H * TEXT_Y, 1.0 + 0.06 * (1.0 - back_out(p)), min(p * 2, fade))
        start, end, _ = ct.LINK
        if t >= start:
            p = min(1.0, (t - start) / 0.25)
            bob = 1.0 + 0.035 * math.sin((t - start) * math.pi * 2 / ct.BEAT / 2)
            paste_scaled(frame, self.link, W / 2, H * LINK_Y, (1.0 + 0.3 * (1.0 - back_out(p))) * bob, min(1.0, p * 2))

        arr = np.asarray(frame.convert('RGB'), np.float32)
        if self.lift != 1.0:
            arr = 255.0 * (arr / 255.0) ** (1.0 / self.lift)
        fade = min(1.0, (ct.LENGTH - t) / 0.8)
        if fade < 1.0:
            arr *= max(0.0, fade)
        return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')


def preview(composer, path, times=(0.4, 2.5, 4.5, 5.9, 7.5, 8.5, 10.0, 11.4)):
    tiles = [composer.frame(t).resize((216, 384), Image.LANCZOS) for t in times]
    sheet = Image.new('RGB', (216 * len(tiles), 384))
    for i, tile in enumerate(tiles):
        sheet.paste(tile, (216 * i, 0))
    sheet.save(path, quality=85)
    print('wrote', path)


def main():
    composer = Composer(sys.argv[1], float(arg('--lift', '1.0')))
    if '--preview' in sys.argv:
        preview(composer, arg('--preview', 'sheet.jpg'))
        return
    os.makedirs(OUT, exist_ok=True)
    master = os.path.join(OUT, FILE)
    audio = os.path.join(OUT, 'cozy_audio.wav')
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
