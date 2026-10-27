"""
Reel 8 (worlds_timeline.py): the orbit take with a punch on every world switch (zoom, flash and a quick
colour split), the question at the top, a numbered label in each world's colours, then a 3x3 grid of all
nine worlds with "comment your number" and "link in bio". Vertical 1080x1920 at 120 fps.

Output: out/storm_engine_reel_worlds_120fps.mp4
Usage: python make_worlds.py <worlds recorder folder> [--preview sheet.jpg]
"""
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image

import make_trailer as mt
import worlds_timeline as wt
from make_loop import caption
from make_trailer import FORMATS, Takes, back_out, game_line, paste_scaled

FMT = dict(FORMATS['reels'], fps=wt.FPS)
W, H = FMT['size']
FPS = wt.FPS
OUT = os.path.join(wt.HERE, 'out')
FILE = 'storm_engine_reel_worlds_120fps.mp4'
QUESTION_Y, COUNT_Y, NAME_Y = 0.2, 0.655, 0.715
TILE_W, TILE_H = W // 3, H // 3

for i, (_, _, colours) in enumerate(wt.WORLDS):
    mt.STYLES['w%d' % i] = colours


def fitted(text, style, size, max_w):
    img = game_line(text, style, size)
    while img.width > max_w and size > 40:
        size -= 6
        img = game_line(text, style, size)
    return img


class Composer:
    def __init__(self, folder):
        self.takes = Takes(folder, FMT)
        self.question = caption(wt.QUESTION)
        self.names = [fitted(name, 'w%d' % i, 104, 940) for i, (name, _, _) in enumerate(wt.WORLDS)]
        self.counts = [game_line('%d/9' % (i + 1), 'punch', 64) for i in range(len(wt.WORLDS))]
        self.numbers = [game_line(str(i + 1), 'w%d' % i, 92) for i in range(len(wt.WORLDS))]
        self.end_text = [(start, y, caption(line), line == 'link in bio') for start, y, line in wt.END_TEXT]
        self.tiles = [self.takes.frame('worlds9', wt.world_at(i) + 0.6).resize((TILE_W, TILE_H), Image.LANCZOS)
                      for i in range(len(wt.WORLDS))]
        self.rng = np.random.default_rng(3)

    def world_frame(self, t):
        frame = self.takes.frame('worlds9', t)
        since, index = None, -1
        for i in range(len(wt.WORLDS)):
            if wt.world_at(i) <= t:
                since, index = t - wt.world_at(i), i
        if since is not None and since < 0.3:
            zoom = 1.0 + 0.07 * (1.0 - back_out(since / 0.3))
            cw, ch = int(W / zoom), int(H / zoom)
            frame = frame.crop(((W - cw) // 2, (H - ch) // 2, (W - cw) // 2 + cw, (H - ch) // 2 + ch)).resize((W, H), Image.BILINEAR)
        if since is not None and since < 0.12:
            arr = np.asarray(frame).copy()
            k = int(12 * (1.0 - since / 0.12))
            if k > 0:
                arr[:, :, 0] = np.roll(arr[:, :, 0], k, axis=1)
                arr[:, :, 2] = np.roll(arr[:, :, 2], -k, axis=1)
            frame = Image.fromarray(arr, 'RGBA')

        fade = min(1.0, (wt.GRID - t) / 0.2)
        paste_scaled(frame, self.question, W / 2, H * QUESTION_Y, 1.0, fade)
        if index >= 0:
            p = min(1.0, since / 0.25)
            slide = (1.0 - back_out(p)) * -320
            paste_scaled(frame, self.counts[index], W / 2 + slide * 0.6, H * COUNT_Y, 1.0, min(1.0, p * 3) * fade, -3.0)
            paste_scaled(frame, self.names[index], W / 2 + slide, H * NAME_Y, 1.0 + 0.15 * (1.0 - back_out(p)),
                         min(1.0, p * 3) * fade, -3.0)
        flash = 0.0 if since is None else (0.5 if index == 0 else 0.32) * math.exp(-since * 10)
        return frame, flash

    def grid_frame(self, t):
        frame = Image.new('RGBA', (W, H), (14, 18, 40, 255))
        for i, tile in enumerate(self.tiles):
            at = wt.GRID + i * 0.06
            if t < at:
                continue
            p = min(1.0, (t - at) / 0.2)
            cx, cy = (i % 3 + 0.5) * TILE_W, (i // 3 + 0.5) * TILE_H
            paste_scaled(frame, tile, cx, cy, 0.5 + 0.5 * back_out(p), min(1.0, p * 3))
            paste_scaled(frame, self.numbers[i], cx - TILE_W / 2 + 62, cy - TILE_H / 2 + 70, 0.6 + 0.4 * back_out(p),
                         min(1.0, p * 3))
        dim = min(1.0, max(0.0, (t - wt.GRID - 0.6) / 0.3)) * 0.5
        if dim > 0:
            frame.alpha_composite(Image.new('RGBA', (W, H), (10, 12, 30, int(255 * dim))))
        for start, y, img, link in self.end_text:
            if t >= start:
                p = min(1.0, (t - start) / 0.25)
                bob = 1.0 + (0.04 * math.sin((t - start) * math.pi * 2 / wt.BEAT / 2) if link else 0.0)
                paste_scaled(frame, img, W / 2, H * y, (1.0 + 0.25 * (1.0 - back_out(p))) * bob, min(1.0, p * 2))
        last = max(0.0, 1.0 - (t - wt.GRID) / 0.06)
        return frame, 0.6 * last if t < wt.GRID + 0.1 else 0.0

    def frame(self, t):
        frame, flash = self.world_frame(t) if t < wt.GRID else self.grid_frame(t)
        if t >= wt.FINAL:
            flash = max(flash, 0.45 * math.exp(-(t - wt.FINAL) * 8))
        arr = np.asarray(frame.convert('RGB'), np.float32)
        if flash > 0.01:
            arr = arr + (255 - arr) * min(1.0, flash)
        fade = min(1.0, (wt.LENGTH - t) / 0.5)
        if fade < 1.0:
            arr *= max(0.0, fade)
        return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')


def preview(composer, path):
    times = [0.8, wt.DROP + 0.4] + [wt.world_at(i) + 0.45 for i in (2, 4, 5, 6, 7, 8)] + [wt.GRID + 0.3, wt.GRID + 2.0]
    tiles = [composer.frame(t).resize((216, 384), Image.LANCZOS) for t in times]
    sheet = Image.new('RGB', (216 * len(tiles), 384))
    for i, tile in enumerate(tiles):
        sheet.paste(tile, (216 * i, 0))
    sheet.save(path, quality=85)
    print('wrote', path)


def main():
    composer = Composer(sys.argv[1])
    if '--preview' in sys.argv:
        preview(composer, sys.argv[sys.argv.index('--preview') + 1])
        return
    os.makedirs(OUT, exist_ok=True)
    master = os.path.join(OUT, FILE)
    audio = os.path.join(OUT, 'worlds_audio.wav')
    cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (W, H),
           '-r', str(FPS), '-i', '-', '-i', audio, '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
           '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'aac', '-b:a', '192k', '-shortest',
           '-movflags', '+faststart', master]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(int(wt.LENGTH * FPS)):
        proc.stdin.write(composer.frame(n / FPS).tobytes())
        if n % (FPS * 4) == 0:
            print('  %.0f s' % (n / FPS), flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', master, 'exit', proc.returncode)


if __name__ == '__main__':
    main()
