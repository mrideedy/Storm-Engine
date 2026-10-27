"""
Draws the STORM ENGINE logo (title of the main menu and the launch intro) in Bungee
(SIL Open Font License, Tools/marketing/fonts/Bungee-OFL.txt): two stacked lines with a gradient
fill, a thick ink outline and a chunky drop for depth, in the game's colours.

Output: Assets/StormEngine/Resources/StormEngine/logo.png (transparent, 1200 px wide)
Usage: python Tools/marketing/make_logo.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
FONT = os.path.join(HERE, 'fonts', 'Bungee-Regular.ttf')
OUT = os.path.join(ROOT, 'Assets', 'StormEngine', 'Resources', 'StormEngine', 'logo.png')

INK = (20, 32, 58, 255)
DROP = (9, 16, 36, 255)
LINES = [
    ('STORM', ((255, 255, 255), (178, 222, 255))),
    ('ENGINE', ((255, 226, 92), (255, 140, 46))),
]
WIDTH = 1200


def gradient(size, top, bottom):
    w, h = size
    col = Image.new('RGB', (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        col.putpixel((0, y), tuple(int(round(top[i] + (bottom[i] - top[i]) * t)) for i in range(3)))
    return col.resize((w, h)).convert('RGBA')


def line_masks(text, size, stroke):
    """Fill and outline masks of one line, on a canvas with room for the outline and the drop."""
    font = ImageFont.truetype(FONT, size)
    l, t, r, b = font.getbbox(text, stroke_width=stroke)
    pad = stroke * 2
    w, h = r - l + pad * 2, b - t + pad * 2 + stroke * 2
    fill = Image.new('L', (w, h), 0)
    outline = Image.new('L', (w, h), 0)
    xy = (pad - l, pad - t)
    ImageDraw.Draw(fill).text(xy, text, font=font, fill=255)
    ImageDraw.Draw(outline).text(xy, text, font=font, fill=255, stroke_width=stroke, stroke_fill=255)
    return fill, outline


def render_line(text, colors, width_px):
    """One line scaled so its letters span `width_px`, with outline, drop and gradient fill."""
    probe = ImageFont.truetype(FONT, 400)
    l, _, r, _ = probe.getbbox(text)
    size = int(400 * width_px / (r - l))
    stroke = max(2, int(size * 0.085))
    drop = int(size * 0.075)
    fill, outline = line_masks(text, size, stroke)
    w, h = fill.size
    img = Image.new('RGBA', (w, h + drop), (0, 0, 0, 0))
    # Chunky drop: the outline repeated straight down, darker.
    for dy in range(drop, 0, -max(1, drop // 6)):
        img.paste(Image.new('RGBA', (w, h), DROP), (0, dy), outline)
    img.paste(Image.new('RGBA', (w, h), INK), (0, 0), outline)
    # Gradient over the letter height only.
    bbox = fill.getbbox()
    top, bottom = bbox[1], bbox[3]
    grad = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    grad.paste(gradient((w, bottom - top), *colors), (0, top))
    img.paste(grad, (0, 0), fill)
    return img


def main():
    scale = 2  # draw large, then downsample for clean edges
    inner = int(WIDTH * scale * 0.92)
    top = render_line(LINES[0][0], LINES[0][1], int(inner * 0.86))
    bottom = render_line(LINES[1][0], LINES[1][1], inner)
    gap = -int(top.height * 0.2)
    w = max(top.width, bottom.width)
    h = top.height + bottom.height + gap
    logo = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    # The lower line goes first so the upper line's drop sits on top of it.
    logo.alpha_composite(bottom, ((w - bottom.width) // 2, top.height + gap))
    logo.alpha_composite(top, ((w - top.width) // 2, 0))
    logo = logo.crop(logo.getbbox())
    out = logo.resize((WIDTH, int(logo.height * WIDTH / logo.width)), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT, optimize=True)
    print('logo written to', OUT, out.size)


if __name__ == '__main__':
    main()
