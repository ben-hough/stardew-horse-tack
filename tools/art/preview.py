#!/usr/bin/env python3
"""Preview helpers: composite layers in the mod's order and save zoomed sheets / contact sheets."""
import os
import sys
from PIL import Image, ImageDraw

ORDER = ['coats', 'styles', 'pads', 'saddles', 'bridles']  # file layer folders in draw order (coat, style, pad, saddle, bridle; pads go under saddles since 1.4.0)


def composite(base, layers):
    img = Image.open(base).convert('RGBA') if isinstance(base, str) else base.copy()
    for p in layers:
        if p:
            img = Image.alpha_composite(img, Image.open(p).convert('RGBA'))
    return img


def zoom(img, scale=4, bg=(236, 232, 214)):
    b = Image.new('RGBA', img.size, bg + (255,))
    b.alpha_composite(img)
    return b.resize((img.width * scale, img.height * scale), Image.NEAREST)


def label(img, text, h=18):
    out = Image.new('RGBA', (img.width, img.height + h), (255, 255, 255, 255))
    out.paste(img, (0, h))
    ImageDraw.Draw(out).text((4, 3), text, fill=(30, 30, 30, 255))
    return out


def grid(images, cols):
    w = max(i.width for i in images)
    h = max(i.height for i in images)
    rows = (len(images) + cols - 1) // cols
    out = Image.new('RGBA', (cols * (w + 6), rows * (h + 6)), (255, 255, 255, 255))
    for n, im in enumerate(images):
        out.paste(im, ((n % cols) * (w + 6), (n // cols) * (h + 6)))
    return out


def frames(img, idx, scale=6):
    """Pick frames by index and lay them out in a row."""
    tiles = []
    for f in idx:
        x, y = (f % 7) * 32, (f // 7) * 32
        tiles.append(zoom(img.crop((x, y, x + 32, y + 32)), scale))
    return grid(tiles, len(tiles))
