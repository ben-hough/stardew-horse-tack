#!/usr/bin/env python3
"""Generate docs/icon.png: an original pixel-art saddle + horseshoe (no game or Elle art)."""
import os
from PIL import Image, ImageDraw

N = 32  # pixel grid, scaled x8 -> 256px
img = Image.new('RGBA', (N, N), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
OUT = (58, 32, 20, 255)
BG1, BG2 = (246, 214, 142, 255), (228, 186, 110, 255)
LEATHER, LEATHER_D, LEATHER_L = (156, 84, 44, 255), (112, 58, 30, 255), (198, 118, 64, 255)
PAD, PAD_D = (196, 52, 60, 255), (140, 34, 44, 255)
GOLD, GOLD_D = (250, 200, 70, 255), (196, 140, 36, 255)
STEEL, STEEL_D = (190, 200, 214, 255), (120, 130, 150, 255)

# rounded badge background
d.rounded_rectangle([1, 1, 30, 30], radius=6, fill=BG1, outline=OUT)
for y in range(2, 30):
    for x in range(2, 30):
        if (x + y) % 6 == 0 and img.getpixel((x, y)) == BG1:
            img.putpixel((x, y), BG2)

# saddle pad (blanket) under the saddle
d.polygon([(6, 13), (25, 13), (26, 22), (5, 22)], fill=PAD, outline=OUT)
d.line([(7, 21), (24, 21)], fill=PAD_D)
for x in range(7, 25, 3):
    img.putpixel((x, 22), GOLD)

# saddle seat
d.polygon([(8, 12), (11, 9), (16, 11), (21, 8), (24, 11), (23, 17), (9, 17)], fill=LEATHER, outline=OUT)
d.line([(11, 10), (16, 12), (21, 9)], fill=LEATHER_L)
d.line([(10, 16), (22, 16)], fill=LEATHER_D)
# horn
d.rectangle([21, 5, 23, 8], fill=LEATHER_D, outline=OUT)
# stirrup strap + stirrup
d.line([(15, 17), (15, 23)], fill=LEATHER_D)
d.rectangle([13, 23, 17, 26], outline=GOLD_D)
d.line([(14, 26), (16, 26)], fill=GOLD)

# horseshoe (bottom-left)
d.arc([3, 20, 11, 29], start=180, end=360, fill=STEEL, width=2)
d.line([(3, 25), (3, 28)], fill=STEEL); d.line([(4, 25), (4, 28)], fill=STEEL)
d.line([(10, 25), (10, 28)], fill=STEEL); d.line([(11, 25), (11, 28)], fill=STEEL_D)
for p in [(4, 23), (7, 21), (10, 23)]:
    img.putpixel(p, STEEL_D)

# sparkle (styling)
for p in [(27, 4), (26, 5), (28, 5), (27, 6), (27, 5)]:
    img.putpixel(p, (255, 255, 255, 255))
img.putpixel((5, 5), (255, 240, 180, 255))

os.makedirs(os.path.join(os.path.dirname(__file__), '..', 'docs'), exist_ok=True)
out = os.path.join(os.path.dirname(__file__), '..', 'docs', 'icon.png')
img.resize((256, 256), Image.NEAREST).save(out)
print('wrote', os.path.abspath(out))
