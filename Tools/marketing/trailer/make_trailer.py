"""
Edits the trailer from the recorded takes (the trailer recorder plays shots.json or
shots_reels.json) following timeline.py: cuts on the beat, Bungee captions that punch in, the
logo slam with a flash, and the "coming soon" end card; then muxes the music from
make_trailer_audio.py with ffmpeg.

Formats:
  landscape  1920x1080, 30 fps, letterboxed cold open   -> out/storm_engine_trailer.mp4
  reels      1080x1920, 120 fps (+ a 60 fps copy for upload), captions kept inside the area
             Instagram leaves clear of its buttons       -> out/storm_engine_reels_120fps.mp4,
                                                            out/storm_engine_reels_60fps.mp4

Usage: python make_trailer.py <recorder folder> [landscape|reels] [--60]   (--60 also writes the 60 fps copy)
"""
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

import timeline as tl

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(tl.HERE)))
FONT = os.path.join(ROOT, 'Tools', 'marketing', 'fonts', 'Bungee-Regular.ttf')
LOGO = os.path.join(ROOT, 'Assets', 'StormEngine', 'Resources', 'StormEngine', 'logo.png')
OUT = os.path.join(tl.HERE, 'out')

INK = (20, 32, 58)
DROP = (9, 16, 36)
STYLES = {
    'punch': ((255, 255, 255), (178, 222, 255)),
    'hot': ((255, 226, 92), (255, 140, 46)),
}
END_LINE, END_SUB = 'COMING SOON', 'ON GOOGLE PLAY'

FORMATS = {
    'landscape': dict(
        size=(1920, 1080), fps=30, letterbox=True, caption_width=0.88, game_size=118, trailer_size=72,
        positions={'center': (0.5, 0.5), 'low': (0.5, 0.8), 'right': (0.79, 0.45), 'left': (0.21, 0.45)},
        logo=(0.5, 0.36, 980), end_logo=(0.27, 0.33, 720), end_line=(0.27, 0.66, 0.8), end_sub=(0.27, 0.81, 0.52),
        shade='left', files=['storm_engine_trailer.mp4']),
    'reels': dict(
        size=(1080, 1920), fps=120, letterbox=False, caption_width=0.84, game_size=112, trailer_size=58,
        # Instagram covers about the top 13% and bottom 20% (and buttons on the right, lower half).
        positions={'center': (0.5, 0.42), 'low': (0.5, 0.65), 'right': (0.5, 0.42), 'left': (0.5, 0.42)},
        logo=(0.5, 0.31, 900), end_logo=(0.5, 0.235, 780), end_line=(0.5, 0.70, 0.88), end_sub=(0.5, 0.775, 0.62),
        shade='reels', files=['storm_engine_reels_120fps.mp4', 'storm_engine_reels_60fps.mp4']),
}


# ------------------------------------------------------------------ text

def gradient_fill(size, top, bottom):
    w, h = size
    col = np.linspace(0, 1, h)[:, None]
    rows = np.array(top, np.float32) + (np.array(bottom, np.float32) - np.array(top, np.float32)) * col
    return Image.fromarray(np.repeat(rows[:, None, :], w, axis=1).astype(np.uint8), 'RGB')


def game_line(text, style, size):
    """Bold caption like the logo: gradient fill, thick ink outline and a chunky drop."""
    font = ImageFont.truetype(FONT, size)
    stroke = max(3, int(size * 0.11))
    drop = int(size * 0.09)
    l, t, r, b = font.getbbox(text, stroke_width=stroke)
    pad = stroke * 2
    w, h = r - l + pad * 2, b - t + pad * 2 + drop
    xy = (pad - l, pad - t)
    fill = Image.new('L', (w, h), 0)
    outline = Image.new('L', (w, h), 0)
    ImageDraw.Draw(fill).text(xy, text, font=font, fill=255)
    ImageDraw.Draw(outline).text(xy, text, font=font, fill=255, stroke_width=stroke, stroke_fill=255)
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    for dy in range(drop, 0, -max(1, drop // 5)):
        img.paste(Image.new('RGBA', (w, h), DROP + (255,)), (0, dy), outline)
    img.paste(Image.new('RGBA', (w, h), INK + (255,)), (0, 0), outline)
    box = fill.getbbox()
    grad = Image.new('RGB', (w, h), (0, 0, 0))
    grad.paste(gradient_fill((w, box[3] - box[1]), *STYLES[style]), (0, box[1]))
    img.paste(grad, (0, 0), fill)
    return img


def trailer_line(text, size):
    """Calm movie-trailer line: white, widely spaced, soft shadow."""
    font = ImageFont.truetype(FONT, size)
    spacing = int(size * 0.18)
    widths = [font.getbbox(ch)[2] - font.getbbox(ch)[0] if ch != ' ' else size * 0.4 for ch in text]
    total = int(sum(widths) + spacing * (len(text) - 1)) + size
    h = int(size * 1.6)
    layer = Image.new('L', (total, h), 0)
    d = ImageDraw.Draw(layer)
    x = size // 2
    for ch, cw in zip(text, widths):
        if ch != ' ':
            d.text((x - font.getbbox(ch)[0], int(size * 0.25)), ch, font=font, fill=255)
        x += cw + spacing
    shadow = layer.filter(ImageFilter.GaussianBlur(size * 0.12))
    img = Image.new('RGBA', layer.size, (0, 0, 0, 0))
    img.paste(Image.new('RGBA', layer.size, (5, 10, 25, 200)), (0, int(size * 0.06)), shadow)
    img.paste(Image.new('RGBA', layer.size, (255, 255, 255, 255)), (0, 0), layer)
    return img.crop(img.getbbox())


def stack(lines, overlap=0.12):
    """Stacks caption lines, centred, slightly overlapping like the logo."""
    w = max(im.width for im in lines)
    gap = -int(lines[0].height * overlap)
    h = sum(im.height for im in lines) + gap * (len(lines) - 1)
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    y = 0
    for im in lines:
        out.alpha_composite(im, ((w - im.width) // 2, y))
        y += im.height + gap
    return out


def wrap(text, render, size, max_w):
    """Greedy word wrap at `size` (explicit line breaks are kept)."""
    lines = []
    for part in text.split('\n'):
        line = ''
        for word in part.split(' '):
            trial = (line + ' ' + word).strip()
            if line and render(trial, size).width > max_w:
                lines.append(line)
                line = word
            else:
                line = trial
        lines.append(line)
    return lines


class Captions:
    def __init__(self, fmt):
        self.fmt = fmt
        self.max_w = fmt['size'][0] * fmt['caption_width']
        self.cache = {}

    def get(self, text, style, wrap_lines=True):
        """The caption image; long lines wrap unless `wrap_lines` is off (then the caller scales it)."""
        key = (text, style, wrap_lines)
        if key in self.cache:
            return self.cache[key]
        if style == 'trailer':
            size, minimum, overlap = self.fmt['trailer_size'], 30, -0.15

            def render(s, z):
                return trailer_line(s, z)
        else:
            size, minimum, overlap = self.fmt['game_size'], 50, 0.12
            if '\n' not in text and len(text) > 16 and self.fmt['size'][0] > self.fmt['size'][1]:
                size = int(size * 0.85)

            def render(s, z):
                return game_line(s, style, z)
        if not wrap_lines:
            img = self.cache[key] = render(text, size)
            return img
        while True:
            lines = wrap(text, render, size, self.max_w)
            imgs = [render(line, size) for line in lines]
            if max(im.width for im in imgs) <= self.max_w or size <= minimum:
                break
            size -= 6
        img = stack(imgs, overlap) if len(imgs) > 1 else imgs[0]
        self.cache[key] = img
        return img


# ------------------------------------------------------------------ animation helpers

def back_out(p, c=1.9):
    u = p - 1.0
    return 1 + (c + 1) * u ** 3 + c * u ** 2


def paste_scaled(frame, img, cx, cy, scale, alpha, angle=0.0):
    if alpha <= 0.01 or scale <= 0.01:
        return
    w, h = max(1, int(img.width * scale)), max(1, int(img.height * scale))
    im = img.resize((w, h), Image.BICUBIC)
    if angle:
        im = im.rotate(angle, resample=Image.BICUBIC, expand=True)
    if alpha < 0.999:
        im.putalpha(im.getchannel('A').point(lambda v: int(v * alpha)))
    frame.alpha_composite(im, (int(cx - im.width / 2), int(cy - im.height / 2)))


def draw_caption(frame, fmt, captions, t, start, end, text, style, position):
    if not (start <= t < end):
        return
    W, H = fmt['size']
    img = captions.get(text, style)
    px, py = fmt['positions'].get(position, fmt['positions']['low'])
    cx, cy = W * px, H * py
    if style == 'trailer':
        p_in = min(1.0, (t - start) / 0.3)
        p_out = min(1.0, (end - t) / 0.3)
        scale = 1.0 + 0.04 * (t - start) / max(0.1, end - start)
        paste_scaled(frame, img, cx, cy, scale, min(p_in, p_out))
        return
    p = min(1.0, (t - start) / 0.2)
    out = max(0.0, 1.0 - (end - t) / 0.12)
    scale = (1.5 + (1.0 - 1.5) * back_out(p)) * (1.0 + 0.12 * out)
    angle = -3.0 if style == 'hot' else 0.0
    # A little bump on every beat keeps the captions alive with the music.
    beat = ((t - start) % tl.BEAT) / tl.BEAT
    scale *= 1.0 + 0.025 * math.exp(-beat * 8) * (p >= 1)
    paste_scaled(frame, img, cx, cy, scale, min(1.0, p * 3) * (1.0 - out), angle)


def end_shade(fmt):
    """Darkens where the end card's words sit, so they read over the bright sky."""
    W, H = fmt['size']
    a = np.zeros((H, W), np.float32)
    if fmt['shade'] == 'left':
        a[:] = np.clip(1.0 - np.linspace(0, 1, W) / 0.62, 0, 1)[None, :] ** 1.4 * 190
    else:
        y = np.linspace(0, 1, H)[:, None]
        top = np.clip(1.0 - y / 0.42, 0, 1) ** 1.3 * 170
        mid = (fmt['end_line'][1] + fmt['end_sub'][1]) / 2
        band = np.exp(-((y - mid) / 0.1) ** 2) * 150
        a[:] = np.maximum(top, band)
    sp = np.zeros((H, W, 4), np.uint8)
    sp[..., :3] = (10, 18, 40)
    sp[..., 3] = a.astype(np.uint8)
    return Image.fromarray(sp, 'RGBA')


# ------------------------------------------------------------------ the edit

class Takes:
    def __init__(self, folder, fmt):
        self.folder = os.path.join(folder, 'takes')
        self.size = fmt['size']
        self.fps = fmt['fps']
        self.counts = {}

    def frame(self, take, seconds):
        d = os.path.join(self.folder, take)
        if take not in self.counts:
            self.counts[take] = len([f for f in os.listdir(d) if f.endswith('.jpg')])
        n = self.counts[take]
        i = min(max(0, int(round(seconds * self.fps))), n - 1)
        im = Image.open(os.path.join(d, '%05d.jpg' % i))
        if im.size != self.size:
            # Takes with the interface are filmed in a smaller window of the same shape.
            im = im.resize(self.size, Image.LANCZOS)
        return im.convert('RGBA')


def segment_at(t):
    for seg in tl.SEGMENTS:
        if seg[0] <= t < seg[1]:
            return seg
    return tl.SEGMENTS[-1]


def render(folder, fmt):
    W, H = fmt['size']
    fps = fmt['fps']
    takes = Takes(folder, fmt)
    captions = Captions(fmt)
    logo = Image.open(LOGO).convert('RGBA')
    lx, ly, lw = fmt['logo']
    logo_main = logo.resize((lw, int(logo.height * lw / logo.width)), Image.LANCZOS)
    ex, ey, ew = fmt['end_logo']
    logo_end = logo.resize((ew, int(logo.height * ew / logo.width)), Image.LANCZOS)
    shade = end_shade(fmt)
    rng = np.random.default_rng(3)
    for n in range(int(tl.LENGTH * fps)):
        t = n / fps
        start, end, take, take_in, caps = segment_at(t)
        frame = takes.frame(take, take_in + (t - start))

        # Camera kick on the big hits (on top of the game's own shake).
        for at, strength in tl.HITS:
            if 0 <= t - at < 0.45:
                k = math.exp(-(t - at) * 9) * 14 * strength * W / 1920
                dx, dy = rng.uniform(-k, k), rng.uniform(-k, k)
                frame = frame.transform(frame.size, Image.AFFINE, (1, 0, dx, 0, 1, dy), resample=Image.BILINEAR)

        if t >= tl.END_CARD:
            frame.alpha_composite(shade)
            p = min(1.0, (t - tl.END_CARD) / 0.3)
            paste_scaled(frame, logo_end, W * ex, H * ey, 2.0 + (1.0 - 2.0) * back_out(p), min(1.0, p * 4))
            x, y, s = fmt['end_line']
            if t >= tl.END_CARD + 0.55:
                q = min(1.0, (t - tl.END_CARD - 0.55) / 0.2)
                pulse = 1.0 + 0.03 * math.sin((t - tl.END_CARD) * math.pi * 2 / tl.BEAT / 2)
                paste_scaled(frame, captions.get(END_LINE, 'hot', False), W * x, H * y,
                             (1.5 + (1.0 - 1.5) * back_out(q)) * s * pulse, min(1.0, q * 3), -2.0)
            x, y, s = fmt['end_sub']
            if t >= tl.END_CARD + 0.85:
                q = min(1.0, (t - tl.END_CARD - 0.85) / 0.2)
                paste_scaled(frame, captions.get(END_SUB, 'punch', False), W * x, H * y,
                             (1.5 + (1.0 - 1.5) * back_out(q)) * s, min(1.0, q * 3))

        for c_start, c_end, text, style, position in caps:
            draw_caption(frame, fmt, captions, t, c_start, c_end, text, style, position)

        s0, s1 = tl.LOGO_SLAM
        if s0 <= t < s1:
            p = min(1.0, (t - s0) / 0.28)
            out = max(0.0, 1.0 - (s1 - t) / 0.3)
            paste_scaled(frame, logo_main, W * lx, H * ly, (2.2 + (1.0 - 2.2) * back_out(p)) * (1 + 0.25 * out),
                         min(1.0, p * 4) * (1.0 - out))

        rgb = frame.convert('RGB')
        if fmt['letterbox'] and t < tl.LETTERBOX_UNTIL + 0.3:
            bar = int(H * 0.128) * (1.0 if t < tl.LETTERBOX_UNTIL else max(0.0, 1.0 - (t - tl.LETTERBOX_UNTIL) / 0.25) ** 2)
            if bar > 0.5:
                d = ImageDraw.Draw(rgb)
                d.rectangle((0, 0, W, int(bar)), fill=(0, 0, 0))
                d.rectangle((0, H - int(bar), W, H), fill=(0, 0, 0))

        white = 0.0
        for at, strength in tl.HITS:
            if t >= at:
                white = max(white, strength * math.exp(-(t - at) * 7))
        fade = min(1.0, t / 0.4, (tl.LENGTH - t) / 0.6)
        if white > 0.01 or fade < 1.0:
            arr = np.asarray(rgb, np.float32)
            if white > 0.01:
                arr = arr + (255 - arr) * min(1.0, white)
            if fade < 1.0:
                arr *= max(0.0, fade)
            yield np.clip(arr, 0, 255).astype(np.uint8).tobytes()
        else:
            yield rgb.tobytes()


def encode(folder, fmt, path):
    W, H = fmt['size']
    audio = os.path.join(OUT, 'trailer_audio.wav')
    cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (W, H),
           '-r', str(fmt['fps']), '-i', '-', '-i', audio, '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
           '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'aac', '-b:a', '192k', '-shortest',
           '-movflags', '+faststart', path]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for i, data in enumerate(render(folder, fmt)):
        proc.stdin.write(data)
        if i % (fmt['fps'] * 5) == 0:
            print('  %.0f s' % (i / fmt['fps']), flush=True)
    proc.stdin.close()
    proc.wait()
    return proc.returncode


def main():
    folder = sys.argv[1]
    name = sys.argv[2] if len(sys.argv) > 2 else 'landscape'
    fmt = FORMATS[name]
    os.makedirs(OUT, exist_ok=True)
    master = os.path.join(OUT, fmt['files'][0])
    code = encode(folder, fmt, master)
    print('wrote', master, 'exit', code)
    if name == 'reels' and code == 0 and '--60' in sys.argv:
        # Instagram plays Reels at up to 60 fps: every other frame of the 120 fps master.
        copy = os.path.join(OUT, fmt['files'][1])
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', master, '-vf', 'fps=60', '-c:v', 'libx264',
                        '-preset', 'slow', '-crf', '17', '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'copy',
                        '-movflags', '+faststart', copy], check=True)
        print('wrote', copy)


if __name__ == '__main__':
    main()
