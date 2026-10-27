"""
Reel 5 (loop_timeline.py): the orbit take with text styled like a caption typed in the Instagram editor
(white boxes, dark text), no logo or end card. The take's tail is blended into the first frames, so the
video loops without a visible cut. Vertical 1080x1920 at 120 fps.

Output: out/storm_engine_reel_loop_120fps.mp4
Usage: python make_loop.py <loop recorder folder> [--preview sheet.jpg]
"""
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

import loop_timeline as lt
from make_trailer import FORMATS, Takes, back_out, paste_scaled

FMT = dict(FORMATS['reels'], fps=lt.FPS)
W, H = FMT['size']
FPS = lt.FPS
FONT = r'C:\Windows\Fonts\segoeuib.ttf'
TEXT_Y = 0.27
OUT = os.path.join(lt.HERE, 'out')
FILE = 'storm_engine_reel_loop_120fps.mp4'


def wrap(line, font, max_w):
    lines, current = [], ''
    for word in line.split():
        test = (current + ' ' + word).strip()
        if current and font.getlength(test) > max_w:
            lines.append(current)
            current = word
        else:
            current = test
    return lines + [current]


def caption(line):
    """One white rounded box per line, joined into one shape, dark text: the native caption look."""
    ss = 2
    font = ImageFont.truetype(FONT, 64 * ss)
    lines = wrap(line, font, 900 * ss)
    asc, desc = font.getmetrics()
    lh, px, py, join = asc + desc, 30 * ss, 10 * ss, 12 * ss
    widths = [int(font.getlength(text)) for text in lines]
    box_h = lh + py * 2
    img = Image.new('RGBA', (max(widths) + px * 2 + 8, box_h * len(lines) - join * (len(lines) - 1) + 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    y = 4
    for text, w in zip(lines, widths):
        x0 = (img.width - w - px * 2) // 2
        d.rounded_rectangle((x0, y, x0 + w + px * 2, y + box_h), radius=20 * ss, fill=(255, 255, 255, 250))
        y += box_h - join
    y = 4
    for text, w in zip(lines, widths):
        x0 = (img.width - w - px * 2) // 2
        d.text((x0 + px, y + py), text, font=font, fill=(18, 18, 22, 255))
        y += box_h - join
    return img.resize((img.width // ss, img.height // ss), Image.LANCZOS)


class Composer:
    def __init__(self, folder):
        self.takes = Takes(folder, FMT)
        self.text = {line: caption(line) for _, _, line in lt.TEXT}

    def frame(self, t):
        frame = self.takes.frame('loop', t)
        if t < lt.OVERLAP:
            frame = Image.blend(self.takes.frame('loop', lt.LENGTH + t), frame, t / lt.OVERLAP)
        for start, end, line in lt.TEXT:
            if start <= t < end:
                p = 1.0 if start == 0 else min(1.0, (t - start) / 0.18)
                paste_scaled(frame, self.text[line], W / 2, H * TEXT_Y, 1.0 + 0.08 * (1.0 - back_out(p)),
                             min(1.0, p * 2))
        return frame.convert('RGB')


def preview(composer, path, times=(0.1, 2.0, 3.6, 4.05, 6.0, 7.6, 8.6, 9.9)):
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
    audio = os.path.join(OUT, 'loop_audio.wav')
    cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (W, H),
           '-r', str(FPS), '-i', '-', '-i', audio, '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
           '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'aac', '-b:a', '192k', '-shortest',
           '-movflags', '+faststart', master]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(int(lt.LENGTH * FPS)):
        proc.stdin.write(composer.frame(n / FPS).tobytes())
        if n % (FPS * 4) == 0:
            print('  %.0f s' % (n / FPS), flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', master, 'exit', proc.returncode)


if __name__ == '__main__':
    main()
