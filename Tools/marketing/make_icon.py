"""
Builds the STORM ENGINE app icon ("Storm Core") and the store feature graphic from in-game renders.

Icon: the game's engine, a storm cloud above it and a lightning bolt striking its antenna, on a
purple-to-blue sky with a glow behind the core and a thick ink outline around the art, so it reads
at every launcher size. Renders (Tools/marketing/raw), all from the same camera:
  core_icon_black.png  on black, without bloom      } a difference matte gives clean edges
  core_icon_white.png  on white, without bloom      }
  core_icon_bloom.png  on black, with bloom: the glow of the bolt and the core
Feature graphic: the island (island_icon_black/white.png) and the logo (make_logo.py) over a sky.

Outputs:
  Assets/StormEngine/Branding/Icons/icon_background.png   1024  adaptive icon background (sky)
  Assets/StormEngine/Branding/Icons/icon_foreground.png   1024  adaptive icon foreground (art, inside the safe zone)
  Assets/StormEngine/Branding/Icons/icon_legacy.png       1024  rounded square (Android 7 and older)
  Assets/StormEngine/Branding/Icons/icon_round.png        1024  circle (round launchers)
  Tools/marketing/store/play_icon_512.png                 512  Google Play listing icon (full bleed)
  Tools/marketing/store/feature_graphic.png               1024x500 Google Play feature graphic
  Tools/marketing/store/preview_adaptive_round.png        how a round launcher shows the adaptive icon

Usage: python Tools/marketing/make_logo.py, then python Tools/marketing/make_icon.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RAW = os.path.join(HERE, 'raw')
ICONS = os.path.join(ROOT, 'Assets', 'StormEngine', 'Branding', 'Icons')
LOGO = os.path.join(ROOT, 'Assets', 'StormEngine', 'Resources', 'StormEngine', 'logo.png')
STORE = os.path.join(HERE, 'store')

INK = (20, 32, 58)
ICON_TOP = np.array([70, 36, 170], np.float32)
ICON_BOTTOM = np.array([36, 122, 236], np.float32)
SKY_TOP = np.array([66, 136, 228], np.float32)
SKY_BOTTOM = np.array([184, 226, 255], np.float32)
CLOUD = (252, 254, 255, 255)
CLOUD_SHADE = (212, 232, 252, 255)

# The visible part of an adaptive icon: the middle 72 of its 108 dp.
ADAPTIVE = (1 / 6, 2 / 3)
FULL = (0.0, 1.0)


def load(name):
    return np.asarray(Image.open(os.path.join(RAW, name)).convert('RGB'), np.float32) / 255.0


def cutout(prefix, bloom=False):
    """
    The art cut out of its black/white renders: straight-alpha colours and alpha, plus (optionally)
    the bloom glow as added light. Floating specks (ambient particles) are dropped.
    """
    black, white = load(prefix + '_black.png'), load(prefix + '_white.png')
    k = np.median(black[:16, :16].reshape(-1, 3), axis=0)
    w = np.median(white[:16, :16].reshape(-1, 3), axis=0)
    a = np.clip(1.0 - ((white - black) / np.maximum(w - k, 1e-3)).mean(axis=2), 0.0, 1.0)
    labels, n = ndimage.label(ndimage.binary_dilation(a > 0.05, iterations=3))
    sizes = ndimage.sum(np.ones_like(a), labels, range(1, n + 1))
    a *= np.isin(labels, 1 + np.nonzero(sizes >= sizes.max() * 0.01)[0])
    aa = a[..., None]
    rgb = np.clip(np.where(aa > 1e-3, (black - (1.0 - aa) * k) / np.maximum(aa, 1e-3), 0.0), 0.0, 1.0)
    glow = np.clip(load(prefix + '_bloom.png') - black, 0.0, 1.0) if bloom else None
    return rgb, a, glow


def to_image(rgb, a):
    return Image.fromarray((np.dstack([rgb, a]) * 255.0 + 0.5).astype(np.uint8), 'RGBA')


def gradient(w, h, top, bottom, frame):
    off, scale = frame
    t = np.clip(((np.arange(h, dtype=np.float32) + 0.5) / h - off) / scale, 0.0, 1.0)[:, None]
    rows = top + (bottom - top) * t
    return Image.fromarray(np.repeat(rows[:, None, :], w, axis=1).astype(np.uint8), 'RGB').convert('RGBA')


def soft_disc(size, cx, cy, r, color, alpha, blur):
    layer = Image.new('RGBA', size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse((cx - r, cy - r, cx + r, cy + r), fill=color + (alpha,))
    return layer.filter(ImageFilter.GaussianBlur(blur))


# ------------------------------------------------------------------ icon

# A classic lightning bolt, x across and y down in units of its height; the lower tip is (-0.16, 1).
BOLT = [(0.12, 0.0), (-0.30, 0.52), (-0.02, 0.52), (-0.16, 1.0), (0.32, 0.40), (0.04, 0.40), (0.24, 0.0)]
BOLT_TOP = np.array([255, 244, 160], np.float32) / 255.0
BOLT_BOTTOM = np.array([255, 190, 40], np.float32) / 255.0


def bolt_shape(n, solid):
    """
    Coverage (0..1) of a bolt from under the cloud down onto the antenna tip: the cloud and the
    engine are the two big parts of the render; the antenna tip is the engine's highest point.
    """
    labels, count = ndimage.label(solid)
    sizes = ndimage.sum(solid, labels, range(1, count + 1))
    big = 1 + np.argsort(sizes)[-2:]
    centres = ndimage.center_of_mass(solid, labels, big)
    cloud_id, engine_id = (big[0], big[1]) if centres[0][0] < centres[1][0] else (big[1], big[0])
    ey, ex = np.nonzero(labels == engine_id)
    tip_y = ey.min()
    tip_x = float(ex[ey <= tip_y + 3].mean())
    cy, cx = np.nonzero(labels == cloud_id)
    near = np.abs(cx - tip_x) < n * 0.03
    cloud_bottom = cy[near].max() if near.any() else cy.max()
    gap = tip_y - cloud_bottom
    top = cloud_bottom - 0.35 * gap
    h = (tip_y - 0.02 * gap) - top
    left = tip_x + 0.16 * h
    ss = 3
    img = Image.new('L', (n * ss, n * ss), 0)
    ImageDraw.Draw(img).polygon([((left + x * h) * ss, (top + y * h) * ss) for x, y in BOLT], fill=255)
    cover = np.asarray(img.resize((n, n), Image.LANCZOS), np.float32) / 255.0
    return cover, top, h, (tip_x, tip_y)


def icon_art(size):
    """
    The icon art on a transparent square of `size`, framed as rendered (the art fills the middle
    60%): the rendered cloud and engine, a bolt between them, and a thick ink outline under it all.
    Returns (art, glow as float RGB, core position 0..1).
    """
    rgb, a, glow = cutout('core_icon', bloom=True)
    n = a.shape[0]
    solid = a > 0.5
    bolt, bolt_top, bolt_h, tip = bolt_shape(n, solid)
    # Thick ink outline around the solid parts (cloud, bolt, engine): reads at launcher sizes.
    ring = ndimage.binary_dilation(solid | (bolt > 0.5), iterations=max(2, int(n * 0.011)))
    outline = ndimage.gaussian_filter(ring.astype(np.float32), n * 0.0012)
    ys, xs = np.nonzero(solid)
    top, bottom = ys.min(), ys.max()
    low = ys > top + (bottom - top) * 0.7
    core = (float(xs[low].mean()) / n, (top + (bottom - top) * 0.78) / n)

    ink = np.zeros((n, n, 4), np.float32)
    ink[..., :3] = np.array(INK, np.float32) / 255.0
    ink[..., 3] = outline
    art = Image.fromarray((ink * 255 + 0.5).astype(np.uint8), 'RGBA')
    t = np.clip((np.arange(n, dtype=np.float32) - bolt_top) / bolt_h, 0.0, 1.0)[:, None, None]
    bolt_rgb = np.broadcast_to(BOLT_TOP + (BOLT_BOTTOM - BOLT_TOP) * t, (n, n, 3))
    art.alpha_composite(to_image(bolt_rgb, bolt))
    art.alpha_composite(to_image(rgb, a))
    # The bolt glows, brightest where it strikes the antenna.
    halo = ndimage.gaussian_filter(bolt, n * 0.022)[..., None] * np.array([1.0, 0.9, 0.5], np.float32) * 0.9
    yy, xx = np.mgrid[0:n, 0:n]
    spark = np.exp(-((xx - tip[0]) ** 2 + (yy - tip[1]) ** 2) / (2 * (n * 0.035) ** 2))[..., None]
    # Light falls on the sky around the bolt, not over the cloud or the engine.
    clear = (1.0 - a)[..., None]
    glow = np.clip(glow + (halo + spark * np.array([1.0, 0.95, 0.75], np.float32) * 0.55) * clear, 0.0, 1.0)
    art = art.resize((size, size), Image.LANCZOS)
    glow_img = Image.fromarray((glow * 255 + 0.5).astype(np.uint8), 'RGB').resize((size, size), Image.LANCZOS)
    return art, np.asarray(glow_img, np.float32) / 255.0, core


def art_box(n, frame, zoom):
    """Pixel size and top-left corner of the art square: centred in the design square."""
    off, scale = frame
    s = int(round(n * scale * zoom))
    x = int(round(n * (off + scale * 0.5) - s / 2))
    return s, x


def core_point(n, frame, zoom, core):
    s, x = art_box(n, frame, zoom)
    return x + core[0] * s, x + core[1] * s


def icon_background(n, frame, zoom, core):
    """Purple-to-blue sky with a soft light behind the core and a fainter one behind the cloud."""
    off, scale = frame
    img = gradient(n, n, ICON_TOP, ICON_BOTTOM, frame)
    cx, cy = core_point(n, frame, zoom, core)
    r = 0.3 * scale * n * zoom
    img = Image.alpha_composite(img, soft_disc(img.size, cx, cy, r, (120, 220, 255), 130, r * 0.5))
    s, x = art_box(n, frame, zoom)
    img = Image.alpha_composite(img, soft_disc(img.size, cx, x + s * 0.32, r * 0.9, (205, 175, 255), 60, r * 0.55))
    return img


def art_layer(n, art, glow, frame, zoom):
    """The art on a transparent canvas of `n`, its glow folded in as light (premultiplied over)."""
    s, x = art_box(n, frame, zoom)
    a = np.asarray(art.resize((s, s), Image.LANCZOS), np.float32) / 255.0
    g = np.asarray(Image.fromarray((glow * 255).astype(np.uint8), 'RGB').resize((s, s), Image.LANCZOS), np.float32) / 255.0
    alpha = a[..., 3:4]
    premul = a[..., :3] * alpha + g * 0.9
    out_a = np.clip(np.maximum(alpha, premul.max(axis=2, keepdims=True)), 0.0, 1.0)
    color = np.where(out_a > 1e-3, premul / np.maximum(out_a, 1e-3), 0.0)
    layer = np.zeros((n, n, 4), np.float32)
    # Clip the art square to the canvas (the full icons zoom it past the edges).
    sx0, sy0 = max(0, -x), max(0, -x)
    dx0, dx1 = max(0, x), min(n, x + s)
    w = dx1 - dx0
    layer[dx0:dx1, dx0:dx1, :3] = np.clip(color[sy0:sy0 + w, sx0:sx0 + w], 0, 1)
    layer[dx0:dx1, dx0:dx1, 3:] = out_a[sy0:sy0 + w, sx0:sx0 + w]
    return Image.fromarray((layer * 255 + 0.5).astype(np.uint8), 'RGBA')


def rounded(img, radius):
    mask = Image.new('L', (img.width * 4, img.height * 4), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, mask.width - 1, mask.height - 1), radius=radius * 4, fill=255)
    out = img.copy()
    out.putalpha(mask.resize(img.size, Image.LANCZOS))
    return out


def circle(img):
    mask = Image.new('L', (img.width * 4, img.height * 4), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, mask.width - 1, mask.height - 1), fill=255)
    out = img.copy()
    out.putalpha(mask.resize(img.size, Image.LANCZOS))
    return out


# ------------------------------------------------------------------ feature graphic

def clouds(img, puffs, ss=3):
    """A two-tone cartoon cloud bank: (x, y, r) in fractions of the image (radius of its width)."""
    w, h = img.size
    layer = Image.new('RGBA', (w * ss, h * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for color, dy, k in ((CLOUD, 0.0, 1.0), (CLOUD_SHADE, 0.065, 0.8)):
        for x, y, r in puffs:
            X, Y, R = x * w * ss, (y + dy) * h * ss, r * k * w * ss
            d.ellipse((X - R, Y - R, X + R, Y + R), fill=color)
        y0 = (max(p[1] for p in puffs) + dy) * h * ss
        if y0 < h * ss:
            d.rectangle((0, y0, w * ss, h * ss), fill=color)
    return Image.alpha_composite(img, layer.resize((w, h), Image.LANCZOS))


def feature_graphic():
    """1024x500: the logo on the left, the island and its engine on the right, above the clouds."""
    fw, fh = 1024, 500
    rgb, a, _ = cutout('island_icon')
    island = to_image(rgb, a)
    island = island.crop(island.getbbox())
    img = gradient(fw, fh, SKY_TOP, SKY_BOTTOM, FULL)
    img = Image.alpha_composite(img, soft_disc(img.size, fw * 0.69, fh * 0.45, fh * 0.42, (255, 255, 255), 70, fh * 0.12))
    img = clouds(img, [(-0.05 + i * 0.075, 0.9 + 0.04 * (i % 2), 0.065) for i in range(16)])
    ih = int(fh * 0.8)
    iw = int(island.width * ih / island.height)
    img.alpha_composite(island.resize((iw, ih), Image.LANCZOS), (int(fw * 0.69 - iw / 2), int(fh * 0.47 - ih / 2)))
    if os.path.exists(LOGO):
        logo = Image.open(LOGO).convert('RGBA')
        lw = int(fw * 0.4)
        logo = logo.resize((lw, int(logo.height * lw / logo.width)), Image.LANCZOS)
        img.alpha_composite(logo, (int(fw * 0.04), int(fh * 0.44 - logo.height / 2)))
    return img.convert('RGB')


def main():
    os.makedirs(ICONS, exist_ok=True)
    os.makedirs(STORE, exist_ok=True)
    n = 1024
    art, glow, core = icon_art(2048)

    # Adaptive icon: launchers show only the middle 72 of 108 dp, in a shape of their choice.
    # The render frames the art in its middle 60%; this zoom keeps it inside the safe zone.
    zoom_adaptive = 1.32
    background = icon_background(n, ADAPTIVE, zoom_adaptive, core)
    background.convert('RGB').save(os.path.join(ICONS, 'icon_background.png'), optimize=True)
    foreground = art_layer(n, art, glow, ADAPTIVE, zoom_adaptive)
    foreground.save(os.path.join(ICONS, 'icon_foreground.png'), optimize=True)
    alpha = np.asarray(foreground.getchannel('A')) > 128
    ys, xs = np.nonzero(alpha)
    reach = np.percentile(np.hypot(xs - n / 2, ys - n / 2), 99.9) / n
    print('art reaches %.3f of the canvas from the centre (safe zone 0.306, visible 0.333)' % reach)

    # Full icons: the art fills about 86% of the square.
    zoom_full = 1.43
    full = Image.alpha_composite(icon_background(n, FULL, zoom_full, core), art_layer(n, art, glow, FULL, zoom_full))
    rounded(full, n * 0.18).save(os.path.join(ICONS, 'icon_legacy.png'), optimize=True)
    circle(full).save(os.path.join(ICONS, 'icon_round.png'), optimize=True)
    full.convert('RGB').resize((512, 512), Image.LANCZOS).save(os.path.join(STORE, 'play_icon_512.png'), optimize=True)

    preview = Image.alpha_composite(background, foreground)
    m = int(n / 6)
    circle(preview.crop((m, m, n - m, n - m))).resize((432, 432), Image.LANCZOS).save(
        os.path.join(STORE, 'preview_adaptive_round.png'), optimize=True)
    feature_graphic().save(os.path.join(STORE, 'feature_graphic.png'), optimize=True)
    print('icons written to', ICONS, 'and', STORE)


if __name__ == '__main__':
    main()
