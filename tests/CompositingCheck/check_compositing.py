#!/usr/bin/env python3
"""Unit check for Framework/Compositor.cs against Pillow's alpha_composite.

usage: python check_compositing.py <elle_assets_dir> [vanilla_horse.png] [out_dir]
Builds tests/CompositingCheck, composes several combos in the mod's order
(coat, style, saddle, pad, bridle) with the C# code, and compares with Pillow.
Needs Pillow + numpy + the .NET SDK. Writes previews to out_dir (default ./data, git-ignored).
"""
import os, subprocess, sys
import numpy as np
from PIL import Image

here = os.path.dirname(os.path.abspath(__file__))
elle = sys.argv[1]
vanilla = sys.argv[2] if len(sys.argv) > 2 and sys.argv[2] != '-' else None
out = sys.argv[3] if len(sys.argv) > 3 else os.path.join(here, 'data')
os.makedirs(out, exist_ok=True)
game = os.environ.get('GAME_PATH')
build = ['dotnet', 'build', here, '-c', 'Release', '-o', os.path.join(out, 'bin'), '-v', 'q']
if game:
    build.append(f'-p:GamePath={game}')
subprocess.run(build, check=True, stdout=subprocess.DEVNULL)
exe = ['dotnet', os.path.join(out, 'bin', 'CompositingCheck.dll')]

def premul(img):
    a = np.array(img.convert('RGBA')).astype(np.uint32)
    rgb = (a[..., :3] * a[..., 3:4] + 127) // 255
    return np.concatenate([rgb, a[..., 3:4]], axis=2).astype(np.uint8)

def unpremul(arr):
    a = arr.astype(np.float64)
    alpha = a[..., 3:4]
    rgb = np.where(alpha > 0, np.clip(np.round(a[..., :3] * 255 / np.maximum(alpha, 1)), 0, 255), 0)
    return Image.fromarray(np.concatenate([rgb, alpha], axis=2).astype(np.uint8), 'RGBA')

E = lambda p: os.path.join(elle, p)
cases = [
    ('SolidBrown_saddle_pad_bridle', E('Horse/SolidBrown.png'), [E('Saddles/Saddle_Brown.png'), E('Saddles/Pad_Red.png'), E('Saddles/Bridle_Black.png')]),
    ('PintoSilver_prismatic_saddle_bridle', E('Horse/PintoSilver.png'), [E('Horse/PrismaticOverlay.png'), E('Saddles/Saddle_Black.png'), E('Saddles/Bridle_BrightRed.png')]),
    ('Fresian_bridle_only', E('Horse/Fresian.png'), [E('Saddles/Bridle_White.png')]),
]
if vanilla:
    cases.append(('Vanilla_saddle_pad_bridle', vanilla, [E('Saddles/Saddle_Brown.png'), E('Saddles/Pad_Red.png'), E('Saddles/Bridle_Black.png')]))
    cases.append(('Vanilla_prismatic', vanilla, [E('Horse/PrismaticOverlay.png')]))

# synthetic semi-transparent overlays to exercise the blend maths (Elle's art is mostly fully opaque/transparent)
rng = np.random.default_rng(42)
for i in range(2):
    arr = rng.integers(0, 256, size=(128, 224, 4), dtype=np.uint8)
    path = os.path.join(out, f'synthetic_{i}.png')
    Image.fromarray(arr, 'RGBA').save(path)
cases.append(('Synthetic_semitransparent', E('Horse/SolidBrown.png'), [os.path.join(out, 'synthetic_0.png'), os.path.join(out, 'synthetic_1.png')]))

worst = 0
for name, base, overlays in cases:
    raws = []
    for i, p in enumerate([base] + overlays):
        r = os.path.join(out, f'{name}_{i}.raw')
        premul(Image.open(p)).tofile(r)
        raws.append(r)
    res_raw = os.path.join(out, f'{name}_result.raw')
    subprocess.run(exe + [res_raw] + raws, check=True, stdout=subprocess.DEVNULL)
    w, h = Image.open(base).size
    got = np.fromfile(res_raw, dtype=np.uint8).reshape(h, w, 4)

    ref = Image.open(base).convert('RGBA')
    for p in overlays:
        ref = Image.alpha_composite(ref, Image.open(p).convert('RGBA'))
    exp = premul(ref)
    diff = np.abs(got.astype(int) - exp.astype(int)).max()
    worst = max(worst, diff)
    unpremul(got).save(os.path.join(out, f'{name}.png'))
    print(f'{name}: max channel diff vs Pillow = {diff}')

TOL = 3  # premultiplied maths vs Pillow's straight-alpha maths differ by rounding only
print('PASS' if worst <= TOL else 'FAIL', f'(worst diff {worst}, tolerance {TOL})')
sys.exit(0 if worst <= TOL else 1)
