"""
Reel 6, "day 1 vs day 30 in my game" (evolve_timeline.py): the glow-up take with native-looking text at the
top (make_loop.caption) and a big DAY counter that pops at every stage. Vertical 1080x1920 at 120 fps.

Output: out/storm_engine_reel_glowup_120fps.mp4
Usage: python make_evolve.py <evolve recorder folder> [--preview sheet.jpg]
"""
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image

import evolve_timeline as et
from make_loop import caption
from make_trailer import FORMATS, Takes, back_out, game_line, paste_scaled

FMT = dict(FORMATS['reels'], fps=et.FPS)
W, H = FMT['size']
FPS = et.FPS
TEXT_Y, COUNTER_Y = 0.2, 0.71
OUT = os.path.join(et.HERE, 'out')
FILE = 'storm_engine_reel_glowup_120fps.mp4'


class Composer:
    def __init__(self, folder):
        self.takes = Takes(folder, FMT)
        self.text = {line: caption(line) for _, _, line in et.TEXT}
        self.counter = {name: game_line(name, 'hot' if name == 'DAY 30' else 'punch', 150) for _, name in et.STAGES}

    def frame(self, t):
        frame = self.takes.frame('evolve', t)
        for start, end, line in et.TEXT:
            if start <= t < end:
                p = 1.0 if start == 0 else min(1.0, (t - start) / 0.18)
                paste_scaled(frame, self.text[line], W / 2, H * TEXT_Y, 1.0 + 0.08 * (1.0 - back_out(p)), min(1.0, p * 2))
        since = max(at for at, _ in et.STAGES if at <= t)
        p = min(1.0, (t - since) / 0.25) if since > 0 else 1.0
        scale = 1.0 + 0.45 * (1.0 - back_out(p))
        if et.stage_at(t) == 'DAY 30':
            scale *= 1.0 + 0.04 * math.exp(-((t - since) % et.BEAT) / et.BEAT * 6)  # pulses on the beat
        paste_scaled(frame, self.counter[et.stage_at(t)], W / 2, H * COUNTER_Y, scale, min(1.0, p * 3), -3.0)

        rgb = frame.convert('RGB')
        fade = min(1.0, (et.LENGTH - t) / 0.5)
        if fade < 1.0:
            rgb = Image.fromarray((np.asarray(rgb, np.float32) * max(0.0, fade)).astype(np.uint8), 'RGB')
        return rgb


def preview(composer, path, times=(0.4, 1.3, 3.0, 4.8, 6.8, 8.7, 9.6, 11.1, 12.3)):
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
    audio = os.path.join(OUT, 'evolve_audio.wav')
    cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (W, H),
           '-r', str(FPS), '-i', '-', '-i', audio, '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
           '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'aac', '-b:a', '192k', '-shortest',
           '-movflags', '+faststart', master]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(int(et.LENGTH * FPS)):
        proc.stdin.write(composer.frame(n / FPS).tobytes())
        if n % (FPS * 4) == 0:
            print('  %.0f s' % (n / FPS), flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', master, 'exit', proc.returncode)


if __name__ == '__main__':
    main()
