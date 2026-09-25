#!/usr/bin/env python3
"""Unit check for Framework/Compositor.cs against Pillow's alpha_composite.

usage: python check_compositing.py [base.png|-] [overlay.png ...] [--out out_dir]
Composes a base sheet plus overlays in the mod's order (coat, style, pad, saddle, bridle)
with the C# code and compares with Pillow. Without arguments it uses generated 224x128 test
sheets (an opaque base, hard-edged tack-like overlays and random semi-transparent overlays),
so no art files are needed. Pass e.g. the vanilla horse sheet and your own assets PNGs to
check real art too. Needs Pillow + numpy + the .NET SDK. Writes previews to out_dir
(default ./data, git-ignored).
"""
import os, subprocess, sys
import numpy as np
from PIL import Image

here = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[1:]
out = os.path.join(here, 'data')
if '--out' in args:
    i = args.index('--out')
    out = args[i + 1]
    del args[i:i + 2]
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

W, H = 224, 128
rng = np.random.default_rng(42)

def save(name, arr):
    path = os.path.join(out, name + '.png')
    Image.fromarray(arr.astype(np.uint8), 'RGBA').save(path)
    return path

# generated sheets: opaque base, two hard-edged "tack" overlays (alpha 0/255), two random semi-transparent overlays
base_arr = rng.integers(0, 256, size=(H, W, 4)); base_arr[..., 3] = 255
gen_base = save('gen_base', base_arr)
hard = []
for i in range(2):
    arr = rng.integers(0, 256, size=(H, W, 4)); arr[..., 3] = np.where(rng.random((H, W)) < 0.2, 255, 0)
    hard.append(save(f'gen_tack_{i}', arr))
soft = [save(f'gen_soft_{i}', rng.integers(0, 256, size=(H, W, 4))) for i in range(2)]

cases = [
    ('Generated_tack', gen_base, hard),
    ('Generated_semitransparent', gen_base, soft),
    ('Generated_all', gen_base, hard + soft),
]
if args and args[0] != '-':
    cases.append(('Given_base', args[0], args[1:] or hard))
elif len(args) > 1:
    cases.append(('Given_overlays', gen_base, args[1:]))

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
