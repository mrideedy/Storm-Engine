"""
Reel 2, "Today's forecast: 9000% chance of lightning" (forecast_timeline.py): the recorded takes
(shots_forecast.json) with a TV weather report on top - a forecast card that updates from 0% to
9000% to "YES!", an "UPDATE!" stamp, a LIVE / BREAKING NEWS banner with a scrolling ticker - then
the logo and "COMING SOON" end card. Vertical 1080x1920 at 120 fps, plus a 60 fps copy to upload.

Output: out/storm_engine_reel_forecast_120fps.mp4 and out/storm_engine_reel_forecast_60fps.mp4
Usage: python make_forecast.py <recorder folder> [--60]   (--60 also writes the 60 fps copy)
"""
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy import ndimage

import forecast_timeline as ft
from make_trailer import (END_LINE, END_SUB, FONT, FORMATS, INK, LOGO, Captions, Takes, back_out, end_shade,
                          game_line, paste_scaled)

FMT = dict(FORMATS['reels'])
W, H = FMT['size']
FPS = ft.FPS
OUT = os.path.join(ft.HERE, 'out')
FILES = ['storm_engine_reel_forecast_120fps.mp4', 'storm_engine_reel_forecast_60fps.mp4']

NAVY = (20, 32, 58)
RED = (222, 52, 40)
YELLOW = (255, 200, 61)
CARD_W, CARD_H = 900, 400
CARD_AT = (W / 2, H * 0.27)
BANNER_Y = 1336


def font(size):
    return ImageFont.truetype(FONT, size)


# ------------------------------------------------------------------ icons (game style: flat fill, ink outline)

def disk(r):
    y, x = np.mgrid[-r:r + 1, -r:r + 1]
    return x * x + y * y <= r * r


def outlined(mask, color, ink):
    m = np.asarray(mask) > 127
    ring = ndimage.binary_dilation(m, structure=disk(ink))
    img = np.zeros(m.shape + (4,), np.uint8)
    img[ring] = INK + (255,)
    img[m] = color + (255,)
    return Image.fromarray(img, 'RGBA')


def cloud_mask(s, ox=0.0, oy=0.0, k=1.0):
    m = Image.new('L', (s, s), 0)
    d = ImageDraw.Draw(m)
    for cx, cy, r in ((0.3, 0.55, 0.17), (0.5, 0.42, 0.23), (0.7, 0.55, 0.17)):
        cx, cy, r = (ox + cx * k) * s, (oy + cy * k) * s, r * k * s
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    d.rounded_rectangle(((ox + 0.13 * k) * s, (oy + 0.5 * k) * s, (ox + 0.87 * k) * s, (oy + 0.72 * k) * s),
                        radius=0.11 * k * s, fill=255)
    return m


BOLT = [(0.12, 0.0), (-0.30, 0.52), (-0.02, 0.52), (-0.16, 1.0), (0.32, 0.40), (0.04, 0.40), (0.24, 0.0)]


def bolt_mask(s, cx, top, h):
    m = Image.new('L', (s, s), 0)
    ImageDraw.Draw(m).polygon([((cx + x * h) * s, (top + y * h) * s) for x, y in BOLT], fill=255)
    return m


def icon(kind, size=230):
    ss = 4
    s = size * ss
    ink = int(5 * ss)
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    if kind == 'sun':
        rays = Image.new('L', (s, s), 0)
        d = ImageDraw.Draw(rays)
        c = s / 2
        for i in range(8):
            a = i * math.pi / 4
            pts = []
            for ang, rad in ((a - 0.17, 0.3), (a, 0.47), (a + 0.17, 0.3)):
                pts.append((c + math.cos(ang) * rad * s, c + math.sin(ang) * rad * s))
            d.polygon(pts, fill=255)
        img.alpha_composite(outlined(rays, (255, 150, 40), ink))
        core = Image.new('L', (s, s), 0)
        ImageDraw.Draw(core).ellipse((c - 0.25 * s, c - 0.25 * s, c + 0.25 * s, c + 0.25 * s), fill=255)
        img.alpha_composite(outlined(core, (255, 214, 64), ink))
    elif kind == 'cloud':
        img.alpha_composite(outlined(cloud_mask(s, 0.0, 0.08), (240, 245, 255), ink))
    elif kind == 'rain':
        drops = Image.new('L', (s, s), 0)
        d = ImageDraw.Draw(drops)
        for x, y in ((0.32, 0.74), (0.52, 0.8), (0.72, 0.74)):
            r = 0.055 * s
            d.ellipse((x * s - r, y * s - r, x * s + r, y * s + r), fill=255)
            d.polygon([(x * s - r * 0.95, y * s - r * 0.3), (x * s, y * s - r * 2.6), (x * s + r * 0.95, y * s - r * 0.3)], fill=255)
        img.alpha_composite(outlined(drops, (90, 175, 255), ink))
        img.alpha_composite(outlined(cloud_mask(s, 0.0, -0.06), (176, 188, 214), ink))
    elif kind in ('bolt', 'storm'):
        bolts = bolt_mask(s, 0.5, 0.42, 0.55)
        if kind == 'storm':
            bolts = Image.fromarray(np.maximum(np.asarray(bolt_mask(s, 0.32, 0.45, 0.45)),
                                               np.asarray(bolt_mask(s, 0.72, 0.42, 0.5))))
        img.alpha_composite(outlined(bolts, (255, 214, 64), ink))
        img.alpha_composite(outlined(cloud_mask(s, 0.0, -0.12), (150, 158, 196) if kind == 'bolt' else (98, 104, 150), ink))
    return img.resize((size, size), Image.LANCZOS)


# ------------------------------------------------------------------ the forecast card

def rounded_mask(w, h, r):
    m = Image.new('L', (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, w - 1, h - 1), radius=r, fill=255)
    return m


def vertical_gradient(w, h, top, bottom):
    t = np.linspace(0, 1, h)[:, None, None]
    arr = np.array(top, np.float32) + (np.array(bottom, np.float32) - np.array(top, np.float32)) * t
    return Image.fromarray(np.repeat(arr, w, axis=1).astype(np.uint8), 'RGB').convert('RGBA')


def card_base(kind, alarm):
    """The card without its number: drawn at twice the size, then reduced for clean edges."""
    ss = 2
    w, h, pad = CARD_W * ss, CARD_H * ss, 30 * ss
    out = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    shadow = Image.new('RGBA', out.size, (0, 0, 0, 0))
    shadow.paste(Image.new('RGBA', (w, h), (9, 16, 36, 140)), (pad, pad + 16 * ss), rounded_mask(w, h, 50 * ss))
    out.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(12 * ss)))
    ink = 9 * ss
    out.paste(Image.new('RGBA', (w, h), NAVY + (255,)), (pad, pad), rounded_mask(w, h, 50 * ss))
    inner = (w - ink * 2, h - ink * 2)
    top, bottom = ((255, 238, 232), (255, 186, 166)) if alarm else ((250, 253, 255), (212, 230, 252))
    body = vertical_gradient(*inner, top, bottom)
    band = int(86 * ss)
    ImageDraw.Draw(body).rectangle((0, 0, inner[0], band), fill=RED + (255,) if alarm else NAVY + (255,))
    out.paste(body, (pad + ink, pad + ink), rounded_mask(*inner, 42 * ss))
    d = ImageDraw.Draw(out)
    d.text((pad + ink + 34 * ss, pad + ink + band / 2), "TODAY'S FORECAST", font=font(44 * ss), fill=(255, 255, 255),
           anchor='lm')
    d.text((pad + ink + 360 * ss, pad + ink + band + 52 * ss), 'CHANCE OF STORMS', font=font(36 * ss),
           fill=RED if alarm else NAVY, anchor='lm')
    ic = icon(kind, 230 * ss // 2).resize((230 * ss, 230 * ss), Image.LANCZOS)
    out.alpha_composite(ic, (pad + ink + 50 * ss, pad + ink + band + 22 * ss))
    return out.resize((out.width // ss, out.height // ss), Image.LANCZOS)


_numbers = {}


def number(text, alarm):
    key = (text, alarm)
    if key not in _numbers:
        size = 150
        img = game_line(text, 'hot' if alarm else 'punch', size)
        while img.width > 500 and size > 60:
            size -= 8
            img = game_line(text, 'hot' if alarm else 'punch', size)
        _numbers[key] = img
        if len(_numbers) > 600:
            _numbers.clear()
    return _numbers[key]


def stamp():
    ss = 2
    w, h = 250 * ss, 78 * ss
    img = Image.new('RGBA', (w + 20 * ss, h + 20 * ss), (0, 0, 0, 0))
    img.paste(Image.new('RGBA', (w, h), NAVY + (255,)), (10 * ss, 10 * ss), rounded_mask(w, h, 22 * ss))
    img.paste(Image.new('RGBA', (w - 12 * ss, h - 12 * ss), RED + (255,)), (16 * ss, 16 * ss), rounded_mask(w - 12 * ss, h - 12 * ss, 18 * ss))
    ImageDraw.Draw(img).text((10 * ss + w / 2, 10 * ss + h / 2), 'UPDATE!', font=font(44 * ss), fill=(255, 255, 255), anchor='mm')
    img = img.rotate(8, resample=Image.BICUBIC, expand=True)
    return img.resize((img.width // ss, img.height // ss), Image.LANCZOS)


# ------------------------------------------------------------------ the news banner

def banner(mode, flash):
    """LIVE tag + bar (STORM ENGINE WEATHER, or a flashing BREAKING NEWS)."""
    w, h = 900, 96
    img = Image.new('RGBA', (w + 16, h + 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    bar = (RED if not flash else (255, 90, 70)) if mode == 'breaking' else NAVY
    d.rounded_rectangle((8, 8, 8 + w, 8 + h), radius=18, fill=NAVY)
    d.rounded_rectangle((12, 12, 4 + w, 4 + h), radius=15, fill=bar)
    d.rounded_rectangle((12, 12, 170, 4 + h), radius=15, fill=(230, 40, 30) if mode == 'live' else NAVY)
    d.text((108, 8 + h / 2), 'LIVE', font=font(40), fill=(255, 255, 255), anchor='mm')
    text = 'BREAKING NEWS' if mode == 'breaking' else 'STORM ENGINE WEATHER'
    color = (255, 226, 92) if mode == 'breaking' else (255, 255, 255)
    d.text((196, 8 + h / 2), text, font=font(44 if mode == 'breaking' else 40), fill=color, anchor='lm')
    return img


_strips = {}


def ticker(text, t):
    """The yellow ticker, scrolling left."""
    w, h = 900, 58
    if text not in _strips:
        f = font(36)
        tw = int(f.getlength(text))
        strip = Image.new('RGB', (tw * 3, h), YELLOW)
        d = ImageDraw.Draw(strip)
        for i in range(3):
            d.text((i * tw, h / 2), text, font=f, fill=NAVY, anchor='lm')
        _strips[text] = (strip, tw)
    strip, tw = _strips[text]
    x = int((t * 240) % tw)
    view = strip.crop((x, 0, x + w - 8, h))
    out = Image.new('RGBA', (w + 16, h + 12), (0, 0, 0, 0))
    m = rounded_mask(w, h, 14)
    plate = Image.new('RGBA', (w, h), NAVY + (255,))
    out.paste(plate, (8, 0), m)
    inner = Image.new('RGBA', (w - 8, h - 6), (0, 0, 0, 0))
    inner.paste(view.convert('RGBA'), (0, 0))
    out.paste(inner, (12, 0), rounded_mask(w - 8, h - 6, 11))
    return out


# ------------------------------------------------------------------ the edit

def segment_at(t):
    for seg in ft.SEGMENTS:
        if seg[0] <= t < seg[1]:
            return seg
    return ft.SEGMENTS[-1]


def count_value(t):
    p = min(1.0, max(0.0, (t - 10.0) / 3.85))
    return int(100 + (9000 - 100) * p ** 2.2)


class Composer:
    """Builds any frame of the reel on its own (so single moments can be previewed)."""

    def __init__(self, folder):
        self.fmt = dict(FMT, fps=FPS)
        self.takes = Takes(folder, self.fmt)
        self.captions = Captions(self.fmt)
        logo = Image.open(LOGO).convert('RGBA')
        ex, ey, ew = self.fmt['end_logo']
        self.logo_end = logo.resize((ew, int(logo.height * ew / logo.width)), Image.LANCZOS)
        self.shade = end_shade(self.fmt)
        self.bases = {}
        self.upd = stamp()
        self.rng = np.random.default_rng(5)

    def frame(self, t):
        fmt, rng, captions = self.fmt, self.rng, self.captions
        start, end, take, take_in = segment_at(t)
        frame = self.takes.frame(take, take_in + (t - start))

        for at, strength in ft.HITS:
            if 0 <= t - at < 0.4:
                k = math.exp(-(t - at) * 9) * 9 * strength
                frame = frame.transform(frame.size, Image.AFFINE, (1, 0, rng.uniform(-k, k), 0, 1, rng.uniform(-k, k)),
                                        resample=Image.BILINEAR)

        if t < ft.SHOW_UNTIL:
            # News banner.
            since, mode, line = ft.stage_at(ft.NEWS, t)
            slide = 1.0 - (1.0 - min(1.0, max(0.0, (t - 0.15) / 0.35))) ** 3
            bx = 40 - (1.0 - slide) * 1000
            flash = mode == 'breaking' and int(t * 4) % 2 == 0
            frame.alpha_composite(banner(mode, flash), (int(bx) - 8, BANNER_Y - 8))
            if int(t * 2) % 2 == 0:
                ImageDraw.Draw(frame).ellipse((int(bx) + 20, BANNER_Y + 41, int(bx) + 34, BANNER_Y + 55), fill=(255, 255, 255))
            frame.alpha_composite(ticker(line, t), (int(bx) - 8, BANNER_Y + 96))

            # Forecast card.
            since, kind, value, alarm = ft.stage_at(ft.FORECAST, t)
            if value == 'count':
                value = '%d%%' % count_value(t)
            key = (kind, alarm)
            if key not in self.bases:
                self.bases[key] = card_base(kind, alarm)
            card = self.bases[key].copy()
            num = number(value, alarm)
            card.alpha_composite(num, (int(30 + 360 + 260 - num.width / 2), int(30 + 9 + 86 + 175 - num.height / 2)))
            p_in = min(1.0, max(0.0, (t - ft.CARD_IN) / 0.45))
            cy = CARD_AT[1] - (1.0 - back_out(p_in)) * 700 if p_in < 1 else CARD_AT[1]
            scale, angle, cx = 1.0, 0.0, CARD_AT[0]
            for u in ft.UPDATES:
                if 0 <= t - u < 0.3:
                    scale *= 1.0 + 0.18 * (1.0 - back_out((t - u) / 0.3))
            if alarm:
                amp = 3.0 if since < 10.0 else (10.0 if since < 14.0 else 6.0)
                cx += rng.uniform(-amp, amp)
                cy += rng.uniform(-amp, amp)
                angle = rng.uniform(-amp, amp) * 0.25 + (-4.0 if since >= 14.0 else 0.0)
                if since >= 14.0:
                    beat = ((t - 14.0) % ft.BEAT) / ft.BEAT
                    scale *= 1.0 + 0.05 * math.exp(-beat * 7)
            if p_in > 0:
                paste_scaled(frame, card, cx, cy, scale, min(1.0, p_in * 3), angle)
            for u in ft.UPDATES:
                if 0 <= t - u < 1.6:
                    q = min(1.0, (t - u) / 0.18)
                    fade = min(1.0, (1.6 - (t - u)) / 0.25)
                    paste_scaled(frame, self.upd, CARD_AT[0] + 330, CARD_AT[1] - 205, 1.6 + (1.0 - 1.6) * back_out(q), q * fade)

        for c_start, c_end, text, style, position in ft.CAPTIONS:
            if c_start <= t < c_end:
                img = captions.get(text, style)
                fade = min(1.0, (t - c_start) / 0.25, (c_end - t) / 0.25)
                px, py = fmt['positions'][position]
                paste_scaled(frame, img, W * px, H * py, 1.0 + 0.05 * (t - c_start), fade)

        if t >= ft.END_CARD:
            frame.alpha_composite(self.shade)
            ex, ey, _ = fmt['end_logo']
            p = min(1.0, (t - ft.END_CARD) / 0.3)
            paste_scaled(frame, self.logo_end, W * ex, H * ey, 2.0 + (1.0 - 2.0) * back_out(p), min(1.0, p * 4))
            x, y, s = fmt['end_line']
            if t >= ft.END_CARD + 0.55:
                q = min(1.0, (t - ft.END_CARD - 0.55) / 0.2)
                pulse = 1.0 + 0.03 * math.sin((t - ft.END_CARD) * math.pi * 2 / ft.BEAT / 2)
                paste_scaled(frame, captions.get(END_LINE, 'hot', False), W * x, H * y,
                             (1.5 + (1.0 - 1.5) * back_out(q)) * s * pulse, min(1.0, q * 3), -2.0)
            x, y, s = fmt['end_sub']
            if t >= ft.END_CARD + 0.85:
                q = min(1.0, (t - ft.END_CARD - 0.85) / 0.2)
                paste_scaled(frame, captions.get(END_SUB, 'punch', False), W * x, H * y,
                             (1.5 + (1.0 - 1.5) * back_out(q)) * s, min(1.0, q * 3))

        rgb = frame.convert('RGB')
        white = 0.0
        for at, strength in ft.HITS:
            if t >= at:
                white = max(white, strength * math.exp(-(t - at) * 7))
        fade = min(1.0, t / 0.3, (ft.LENGTH - t) / 0.6)
        if white > 0.01 or fade < 1.0:
            arr = np.asarray(rgb, np.float32)
            if white > 0.01:
                arr = arr + (255 - arr) * min(1.0, white)
            if fade < 1.0:
                arr *= max(0.0, fade)
            rgb = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
        return rgb


def render(folder):
    composer = Composer(folder)
    for n in range(int(ft.LENGTH * FPS)):
        yield composer.frame(n / FPS).tobytes()


def main():
    folder = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)
    master = os.path.join(OUT, FILES[0])
    audio = os.path.join(OUT, 'forecast_audio.wav')
    cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (W, H),
           '-r', str(FPS), '-i', '-', '-i', audio, '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
           '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'aac', '-b:a', '192k', '-shortest',
           '-movflags', '+faststart', master]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for i, data in enumerate(render(folder)):
        proc.stdin.write(data)
        if i % (FPS * 4) == 0:
            print('  %.0f s' % (i / FPS), flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', master, 'exit', proc.returncode)
    if proc.returncode == 0 and '--60' in sys.argv:
        copy = os.path.join(OUT, FILES[1])
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', master, '-vf', 'fps=60', '-c:v', 'libx264',
                        '-preset', 'slow', '-crf', '17', '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-c:a', 'copy',
                        '-movflags', '+faststart', copy], check=True)
        print('wrote', copy)


if __name__ == '__main__':
    main()
