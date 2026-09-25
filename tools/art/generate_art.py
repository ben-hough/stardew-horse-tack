#!/usr/bin/env python3
"""Generate Horse Tack & Styling's original art (224x128 horse layers).

usage: python generate_art.py <vanilla_horse.png> [--alt <other_horse.png>] [--out ../../assets]

- <vanilla_horse.png>: Content/Animals/horse unpacked from your own game (not included in the repo).
  Coats are recolours of this ConcernedApe sheet with the built-in saddle painted out.
- --alt: optional second horse shape (e.g. a coat from Elle's Cuter Horses). Only its silhouette
  and frame offsets are read, to write "<name>@elle.png" fit variants of the overlays; none of its
  pixels are copied.
All tack and styling pixels are drawn from the templates below (newly drawn, 1-px outlines,
flat Stardew-style shading).
"""
import argparse
import hashlib
import math
import os

import numpy as np
from PIL import Image

from horse_geometry import Anchors, FRAME, ICON_FRAMES, VIEW_OF, load, origin

W, H = 224, 128


def hexc(h, a=255):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def rnd(*k):
    """Deterministic 0..1 hash (stable patterns across runs)."""
    return int(hashlib.md5(repr(k).encode()).hexdigest()[:8], 16) / 0xFFFFFFFF


def tpl(s):
    rows = s.strip('\n').split('\n')
    ind = min(len(r) - len(r.lstrip(' ')) for r in rows if r.strip())
    return [r[ind:] for r in rows]


class Layer:
    def __init__(self):
        self.a = np.zeros((H, W, 4), dtype=np.uint8)
        self.sym = np.full((H, W), '', dtype=object)

    def put(self, f, x, y, c, sym=''):
        if c is None or not (0 <= x < FRAME and 0 <= y < FRAME):
            return
        ox, oy = origin(f)
        self.a[oy + y, ox + x] = c
        self.sym[oy + y, ox + x] = sym

    def has(self, f, x, y):
        if not (0 <= x < FRAME and 0 <= y < FRAME):
            return False
        ox, oy = origin(f)
        return self.a[oy + y, ox + x, 3] > 0

    def clear(self, f, x, y):
        ox, oy = origin(f)
        self.a[oy + y, ox + x] = 0
        self.sym[oy + y, ox + x] = ''

    def save(self, path):
        Image.fromarray(self.a, 'RGBA').save(path)


def stamp(layer, f, rows, ox, oy, pal, pattern=None):
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch in '. ':
                continue
            x, y = ox + i, oy + j
            s = pattern(f, x, y, ch, i, j) if pattern else ch
            layer.put(f, x, y, pal.get(s, pal.get(ch)), s)


def line(layer, f, x0, y0, x1, y1, c, skip_ends=0):
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    x, y = x0, y0
    while True:
        pts.append((x, y))
        if x == x1 and y == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x += sx
        if e2 <= dx:
            err += dx
            y += sy
    for x, y in pts[skip_ends:len(pts) - skip_ends if skip_ends else None]:
        layer.put(f, x, y, c, 'R')


def solid_at(sheet, f, x, y):
    if not (0 <= x < FRAME and 0 <= y < FRAME):
        return False
    ox, oy = origin(f)
    return sheet[oy + y, ox + x, 3] == 255


def clip_to_body(layer, sheet, pal_outline):
    """Drop pixels outside the horse's silhouette and outline the new edges (for pads/blankets)."""
    for f in range(28):
        for y in range(FRAME):
            for x in range(FRAME):
                if layer.has(f, x, y) and not solid_at(sheet, f, x, y):
                    layer.clear(f, x, y)
        for y in range(FRAME):
            for x in range(FRAME):
                if layer.has(f, x, y):
                    if any(not solid_at(sheet, f, x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                        ox, oy = origin(f)
                        layer.a[oy + y, ox + x] = pal_outline


# ---------------------------------------------------------------------------------------------
# Templates (reference-frame coordinates of the vanilla sheet; see horse_geometry.py)
# O outline, D dark, M mid, L light, H highlight, A/B/C accents, S metal
# ---------------------------------------------------------------------------------------------
SADDLE = {
    'side': ((10, 11), tpl('''
        .........O..
        .OO.....OHO.
        .OLOOOOOOLO.
        .OMLLLLLLMO.
        ..OMMMMMMO..
        ..OMMMMMMO..
        ...ODMMDO...
        ....OOOO....
        .....O......
        .....O......
        ....OSO.....
        ....OOO.....
    ''')),
    'down': ((9, 11), tpl('''
        ...OOOOOOOOO...
        ..OLLLLLLLLLO..
        .OLMMMMMMMMMLO.
        OLMDOOOOOOODMLO
        OLM.........MLO
        OLM.........MLO
        OLM.........MLO
        OLM.........MLO
        ODM.........MDO
        .OM.........MO.
        .OO.........OO.
    ''')),
    'up': ((10, 11), tpl('''
        ...OOOOOOO...
        .OOLLLLLLLOO.
        .OLMMMMMMMLO.
        OLMMMMMMMMMLO
        OMMMMMMMMMMMO
        OMDDDDDDDDDMO
        OMOOOOOOOOOMO
        OMO.......OMO
        ODO.......ODO
        OOO.......OOO
    ''')),
}

PAD = {
    'side': ((9, 14), tpl('''
        ..OOOOOOOOOO..
        .OLLLLLLLLLLO.
        OLMMMMMMMMMMLO
        OLMMMMMMMMMMLO
        OLMMMMMMMMMMLO
        OLMMMMMMMMMMLO
        OLMMMMMMMMMMLO
        ODMMMMMMMMMMDO
        .ODDDDDDDDDDO.
        ..OOOOOOOOOO..
    ''')),
    'down': ((9, 11), tpl('''
        ..OOOOOOOOOOO..
        .OLLLLLLLLLLLO.
        OLMMMMMMMMMMMLO
        OLMO.......OMLO
        OLMO.......OMLO
        OLMO.......OMLO
        OLMO.......OMLO
        OLMO.......OMLO
        OLMO.......OMLO
        OLMO.......OMLO
        OLMO.......OMLO
        ODDO.......ODDO
        .OO.........OO.
    ''')),
    'up': ((10, 12), tpl('''
        ..OOOOOOOOO..
        .OLLLLLLLLLO.
        OLMMMMMMMMMLO
        OMMMMMMMMMMMO
        OMMMMMMMMMMMO
        OLLLLLLLLLLLO
        OMMMMMMMMMMMO
        ODDDDDDDDDDDO
        OMOOOOOOOOOMO
        OMO.......OMO
        ODO.......ODO
        OOO.......OOO
    ''')),
}
REIN_END = {'side': (20, 13)}  # body space: where the reins meet the saddle

# bridle pixels in head space: (x, y, symbol)
BRIDLE = {
    'side': [(25, y, 'M') for y in range(9, 16)] + [(26, 9, 'M'), (27, 9, 'M'), (24, 8, 'M')]
            + [(x, 16, 'M') for x in range(26, 30)] + [(25, 16, 'A')],
    'down': [(x, 25, 'M') for x in range(14, 19)] + [(13, 25, 'A'), (19, 25, 'A')]
            + [(13, y, 'M') for y in range(21, 25)] + [(19, y, 'M') for y in range(21, 25)]
            + [(x, 20, 'M') for x in range(14, 19)],
    'up': [(x, 6, 'M') for x in range(14, 19)],
}
BIT = {'side': (24, 17)}

CROWN = {
    'side': [(24, 7, 'A'), (25, 7, 'G'), (26, 7, 'B'), (27, 7, 'G'), (25, 6, 'C'), (27, 6, 'A'), (22, 8, 'G'), (23, 8, 'C')],
    'down': [(14, 15, 'A'), (15, 14, 'G'), (16, 14, 'B'), (17, 14, 'G'), (18, 15, 'C'), (15, 15, 'G'), (17, 15, 'G'), (16, 13, 'C')],
    'up': [(14, 5, 'A'), (15, 5, 'G'), (16, 5, 'B'), (17, 5, 'G'), (18, 5, 'C'), (16, 4, 'A')],
}

LEI = {
    'side': [(20, 11, 'A'), (21, 12, 'B'), (22, 13, 'C'), (22, 14, 'A'), (23, 15, 'B'), (24, 16, 'C'), (24, 17, 'A'), (25, 18, 'B'),
             (21, 11, 'G'), (22, 12, 'G'), (23, 14, 'G'), (24, 15, 'G'), (25, 17, 'G')],
    'down': [(11, 20, 'A'), (12, 21, 'B'), (11, 21, 'G'), (21, 20, 'A'), (20, 21, 'B'), (21, 21, 'G'),
             (12, 22, 'C'), (20, 22, 'C')],
    'up': [(12, 9, 'A'), (13, 10, 'B'), (14, 10, 'C'), (15, 10, 'A'), (16, 10, 'B'), (17, 10, 'C'), (18, 10, 'A'), (19, 10, 'B'), (20, 9, 'C'),
           (13, 9, 'G'), (19, 9, 'G')],
}
LEI_HEAD_VIEW = {'side': 'side', 'down': 'down', 'up': None}  # lei follows the head on side/down, body on up

JUNIMO = tpl('''
    ...O...
    ..OOO..
    .OLLMO.
    OMEMEMO
    OMMMMMO
    .OMMMO.
    ..O.O..
''')
JUNIMO_BACK = tpl('''
    ...O...
    ..OOO..
    .OLLMO.
    OMLMMMO
    OMMMMMO
    .OMMMO.
    ..O.O..
''')
JUNIMO_POS = {'side': (5, 7), 'down': (13, 3), 'up': (13, 16)}

WING_SIDE = tpl('''
    O.......
    OO.....O
    OMO...OM
    OMMO.OMM
    OMMMOMMM
    .OMMMMMM
    ..OMOMOM
''')
WING_LEFT = tpl('''
    O.....
    OO....
    OMOO..
    OMMMOO
    OMMMMM
    .OMOMM
    ..O.OO
''')
WING_POS = {'side': (4, 6), 'down': ((4, 10), (22, 10)), 'up': ((4, 10), (22, 10))}


# ---------------------------------------------------------------------------------------------
# Drawing helpers per layer type
# ---------------------------------------------------------------------------------------------
def body_views(anchors):
    for f, (view, dx, dy) in anchors.body.items():
        yield f, view, dx, dy


def draw_saddle(anchors, pal, pattern=None):
    L = Layer()
    for f, view, dx, dy in body_views(anchors):
        (ox, oy), rows = SADDLE[view]
        stamp(L, f, rows, ox + dx, oy + dy, pal, pattern)
    return L


def saddle_footprint(anchors):
    """Where a saddle sits in every frame: our saddle template, plus any extra saddle masks given
    on the command line (pads are drawn above saddles, so they leave these pixels free)."""
    fp = np.zeros((H, W), dtype=bool)
    for f, view, dx, dy in body_views(anchors):
        (ox, oy), rows = SADDLE[view]
        fx, fy = origin(f)
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                x, y = ox + i + dx, oy + j + dy
                if ch not in '. ' and 0 <= x < 32 and 0 <= y < 32:
                    fp[fy + y, fx + x] = True
    if anchors.extra_saddle_mask is not None:
        fp |= anchors.extra_saddle_mask
    return fp


def draw_pad(anchors, pal, pattern=None):
    L = Layer()
    for f, view, dx, dy in body_views(anchors):
        (ox, oy), rows = PAD[view]
        stamp(L, f, rows, ox + dx, oy + dy, pal, pattern)
    clip_to_body(L, anchors.sheet, pal['O'])
    fp = saddle_footprint(anchors)
    L.a[fp] = 0
    return L


def head_items(anchors, f):
    return anchors.head.get(f, [])


def draw_bridle(anchors, pal, accent_fn=None):
    L = Layer()
    for f in range(28):
        is_icon = f in ICON_FRAMES
        for view, hx, hy in head_items(anchors, f):
            for x, y, s in BRIDLE.get(view, []):
                L.put(f, x + hx, y + hy, pal.get(s), s)
            if accent_fn:
                accent_fn(L, f, view, hx, hy, is_icon)
            # reins from the bit back to the saddle (not on icon heads)
            if not is_icon and view == 'side' and f in anchors.body:
                bx, by = BIT['side']
                _, bdx, bdy = anchors.body[f]
                rx, ry = REIN_END['side']
                line(L, f, bx + hx, by + hy, rx + bdx, ry + bdy, pal['R'])
    return L


def draw_points(anchors, table, pal, head_views=('side', 'down', 'up'), body_fallback=()):
    L = Layer()
    for f in range(28):
        placed = set()
        for view, hx, hy in head_items(anchors, f):
            if view in head_views:
                for x, y, s in table.get(view, []):
                    L.put(f, x + hx, y + hy, pal.get(s), s)
                placed.add(view)
        if f in anchors.body:
            view, dx, dy = anchors.body[f]
            if view in body_fallback and view not in placed:
                for x, y, s in table.get(view, []):
                    L.put(f, x + dx, y + dy, pal.get(s), s)
    return L


def up_head_anchor(anchors, f):
    """Rear view has no head matching; use the body offset (ears move with the body there)."""
    return anchors.body[f][1], anchors.body[f][2]


# ---------------------------------------------------------------------------------------------
# Palettes
# ---------------------------------------------------------------------------------------------
def pal(**kw):
    p = {k: (hexc(v) if isinstance(v, str) else v) for k, v in kw.items()}
    p.setdefault('R', p.get('D'))
    return p


PALS = {
    # saddles
    'SpiritsEve_Pumpkin': pal(O='3b1d0e', D='b3470f', M='e8741c', L='ffa640', H='ffd27a', A='2f7a22', B='5a3a1a', S='c9c9c9'),
    'Junimo_Leaf': pal(O='1d3b12', D='3f8a27', M='5fb33a', L='8fd65a', H='c6f08a', A='ffe14d', B='fff7b0', S='d8d8d8'),
    'MrQi_Casino': pal(O='1c0f2e', D='4a2080', M='6b35b0', L='3ee0c8', H='a8fff2', A='ffd23f', B='ffffff', S='3ee0c8'),
    'Iridium_Shine': pal(O='2a1238', D='6a2d8a', M='9446b5', L='c07ae0', H='f2c8ff', A='ffffff', B='ff8ad8', S='e0e0f0'),
    'WinterStar_Holly': pal(O='3a0d10', D='8f1a1f', M='c9262c', L='f2c14e', H='fff0a0', A='2e7d32', B='ff3b3b', S='f2c14e'),
    'Prismatic_Shard': pal(O='2b1f3a', D='5b4a7a', M='888888', L='ffffff', H='ffffff', A='ffffff', B='ffffff', S='e0e0f0'),
    # pads
    'EggFestival_Pastel': pal(O='4a5a4a', D='8fcfb0', M='b8ecd2', L='e6fff2', A='ff9ecb', B='ffe680', C='9ecbff'),
    'Luau_Tropical': pal(O='0f3a3a', D='138a86', M='22b5ae', L='7fe0d6', A='ff4f7b', B='ffd84d', C='3fa34d'),
    'Jellies_Moonlight': pal(O='0b1030', D='16235e', M='1f3480', L='6fe8ff', A='a8fbff', B='4fd6ff', C='ffffff'),
    'FestivalOfIce_Snowflake': pal(O='23407a', D='6fa8e0', M='a9d4ff', L='e8f6ff', A='ffffff', B='ffffff', C='cfe9ff'),
    'LuckyPurpleShorts': pal(O='2a1040', D='5b2a91', M='7c3fc4', L='a877ea', A='fff2a8', B='ffffff', C='ffd23f'),
    'Joja_Corporate': pal(O='0d2240', D='1c4a86', M='2a66b8', L='ffffff', A='e02b2b', B='ffffff', C='9cc3f0'),
    'Galaxy_Starfield': pal(O='0a0618', D='1a1240', M='2b1d63', L='5a3fa6', A='ffffff', B='ffe98a', C='9ad8ff'),
    # bridles
    'Fair_BlueRibbon': pal(O='0f1f4a', D='1d3f9e', M='2f63d8', A='f2c14e', B='ffffff', C='c62828', R='1d3f9e'),
    'Stardrop_Star': pal(O='2a0f3a', D='6b2aa0', M='9b4fd6', A='ffd23f', B='fff5b0', C='ff8ad8', R='6b2aa0'),
    'WinterStar_JingleBells': pal(O='3a0d10', D='8f1a1f', M='d6282e', A='f7c948', B='fff0a0', C='2e7d32', R='8f1a1f'),
    'NightMarket_Pearl': pal(O='3a3050', D='b9b0d8', M='f4f0ff', A='d8c8ff', B='ffffff', C='7fd6d0', R='8c80b0'),
    # styles
    'FlowerDance_Crown': pal(A='ff7eb6', B='fff27a', C='b69cff', G='3f9a3a'),
    'Luau_Lei': pal(A='ff4f7b', B='ffd84d', C='ff9a3c', G='3fa34d'),
    'Junimo_Buddy': pal(O='1d4a12', L='a8e86a', M='6fbf3b', E='1a1a1a'),
    'SpiritsEve_BatWings': pal(O='140a1c', M='3b2350', D='2a1838'),
    'WinterStar_SnowDusting': pal(W='ffffff', L='dff1ff'),
}


# ---------------------------------------------------------------------------------------------
# Patterns
# ---------------------------------------------------------------------------------------------
def pumpkin_ribs(f, x, y, ch, i, j):
    if ch in 'ML' and x % 3 == 0:
        return 'D'
    if (i, j) in ((9, 0), (9, 1)) and VIEW_OF.get(f) == 'side':
        return 'A'  # green stem on the pommel
    return ch


def junimo_star(f, x, y, ch, i, j):
    v = VIEW_OF.get(f)
    star = {'side': {(4, 5), (6, 5), (5, 4), (5, 6), (5, 5)}, 'up': {(6, 3), (5, 4), (7, 4), (6, 4)}, 'down': {(6, 2), (7, 2), (8, 2)}}
    if (i, j) in star.get(v, ()) and ch in 'MLD':
        return 'A' if (i, j) != (5, 5) else 'B'
    return ch


def qi_trim(f, x, y, ch, i, j):
    if ch == 'M' and (i + j) % 4 == 0:
        return 'A' if rnd('qi', f, i, j) > 0.5 else 'M'
    return ch


def iridium_shine(f, x, y, ch, i, j):
    if ch in 'ML' and (i - j) % 5 == 0:
        return 'H'
    if ch == 'M' and rnd('ir', i, j) > 0.93:
        return 'A'
    return ch


def winter_holly(f, x, y, ch, i, j):
    v = VIEW_OF.get(f)
    spots = {'side': {(4, 5): 'A', (5, 5): 'B', (6, 5): 'A'}, 'up': {(5, 3): 'A', (6, 3): 'B', (7, 3): 'A'}, 'down': {(6, 2): 'A', (7, 2): 'B', (8, 2): 'A'}}
    return spots.get(v, {}).get((i, j), ch)


RAINBOW = [hexc(c) for c in ('ff4f4f', 'ff9f3c', 'ffe14d', '5fd35f', '4fb8ff', '8f6bff', 'ff6bd6')]


def prismatic(f, x, y, ch, i, j):
    if ch in 'MLD':
        return ('P', (x + y) // 2 % len(RAINBOW), ch)
    return ch


def dots(sym_cycle, density, key):
    def fn(f, x, y, ch, i, j):
        if ch == 'M' and rnd(key, i, j) < density:
            return sym_cycle[int(rnd(key, 'c', i, j) * len(sym_cycle)) % len(sym_cycle)]
        return ch
    return fn


def eggs(f, x, y, ch, i, j):
    if ch == 'M' and (i + 2 * j) % 4 == 0:
        return 'ABC'[(i // 2 + j) % 3]
    return ch


def tropical(f, x, y, ch, i, j):
    if ch == 'M' and (i + j) % 3 == 0:
        return 'A' if (i // 3 + j) % 2 else 'B'
    if ch == 'M' and (i * 7 + j * 3) % 11 == 0:
        return 'C'
    return ch


def jellies(f, x, y, ch, i, j):
    if ch == 'M' and (i % 4 == 1) and (j % 3 == 1):
        return 'A'
    if ch == 'M' and (i % 4 == 3) and (j % 3 == 2):
        return 'B'
    return ch


def snowflakes(f, x, y, ch, i, j):
    if ch == 'M' and (i % 4 == 1 and j % 3 == 0):
        return 'A'
    if ch == 'M' and (i % 4 == 3 and j % 3 == 2):
        return 'C'
    return ch


def shorts(f, x, y, ch, i, j):
    if ch == 'L':
        return 'A' if i % 2 == 0 else 'L'
    if ch == 'M' and i % 5 == 2:
        return 'L'
    return ch


def joja(f, x, y, ch, i, j):
    v = VIEW_OF.get(f)
    stripe_row = {'side': 4, 'down': 2, 'up': 6}.get(v)
    if ch in 'ML' and j == stripe_row:
        return 'L'
    if ch == 'M' and j == (stripe_row or 0) + 1 and i % 4 == 1:
        return 'A'
    return ch


def starfield(f, x, y, ch, i, j):
    if ch == 'M':
        r = rnd('gal', x, y, f // 7)
        if r < 0.10:
            return 'A'
        if r < 0.15:
            return 'B'
        if r < 0.20:
            return 'C'
    return ch


# bridle accents
def rosette(L, f, view, hx, hy, icon):
    p = PALS['Fair_BlueRibbon']
    if view == 'side':
        cx, cy = 25 + hx, 10 + hy
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            L.put(f, cx + dx, cy + dy, p['M'])
        L.put(f, cx, cy, p['A'])
        L.put(f, cx - 1, cy + 2, p['C'])
        L.put(f, cx + 1, cy + 2, p['C'])
    elif view == 'down':
        for cx in (13 + hx, 19 + hx):
            cy = 21 + hy
            L.put(f, cx, cy, p['A'])
            L.put(f, cx, cy - 1, p['M'])


def stardrop(L, f, view, hx, hy, icon):
    p = PALS['Stardrop_Star']
    if view == 'side':
        cx, cy = 27 + hx, 9 + hy
    elif view == 'down':
        cx, cy = 16 + hx, 20 + hy
    else:
        return
    for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
        L.put(f, cx + dx, cy + dy, p['A'])
    L.put(f, cx, cy, p['B'])


def bells(L, f, view, hx, hy, icon):
    p = PALS['WinterStar_JingleBells']
    pts = {'side': [(27, 17), (29, 17), (25, 12)], 'down': [(14, 26), (16, 26), (18, 26)], 'up': [(14, 7), (18, 7)]}.get(view, [])
    for x, y in pts:
        L.put(f, x + hx, y + hy, p['A'])


def pearls(L, f, view, hx, hy, icon):
    p = PALS['NightMarket_Pearl']
    for x, y, s in BRIDLE.get(view, []):
        if (x + y) % 2 == 0:
            L.put(f, x + hx, y + hy, p['B'])
    if view == 'side':
        L.put(f, 25 + hx, 16 + hy, p['C'])


# ---------------------------------------------------------------------------------------------
# Styles
# ---------------------------------------------------------------------------------------------
def draw_crown(anchors, p):
    L = Layer()
    for f in range(28):
        for view, hx, hy in head_items(anchors, f):
            for x, y, s in CROWN.get(view, []):
                L.put(f, x + hx, y + hy, p[s], s)
        if VIEW_OF.get(f) == 'up':
            hx, hy = up_head_anchor(anchors, f)
            for x, y, s in CROWN['up']:
                L.put(f, x + hx, y + hy, p[s], s)
    return L


def draw_lei(anchors, p):
    L = Layer()
    for f in range(28):
        if f in ICON_FRAMES:
            continue
        for view, hx, hy in head_items(anchors, f):
            if view in ('side', 'down'):
                for x, y, s in LEI[view]:
                    L.put(f, x + hx, y + hy, p[s], s)
        if VIEW_OF.get(f) == 'up':
            hx, hy = up_head_anchor(anchors, f)
            for x, y, s in LEI['up']:
                L.put(f, x + hx, y + hy, p[s], s)
    return L


def draw_junimo(anchors, p):
    L = Layer()
    for f, view, dx, dy in body_views(anchors):
        ox, oy = JUNIMO_POS[view]
        stamp(L, f, JUNIMO_BACK if view == 'up' else JUNIMO, ox + dx, oy + dy, p)
    return L


def draw_wings(anchors, p):
    L = Layer()
    for f, view, dx, dy in body_views(anchors):
        if view == 'side':
            ox, oy = WING_POS['side']
            stamp(L, f, WING_SIDE, ox + dx, oy + dy, p)
        else:
            (lx, ly), (rx, ry) = WING_POS[view]
            stamp(L, f, WING_LEFT, lx + dx, ly + dy, p)
            stamp(L, f, [r[::-1] for r in WING_LEFT], rx + dx, ry + dy, p)
    return L


def draw_snow(anchors, p):
    L = Layer()
    sheet = anchors.sheet
    for f in range(28):
        for x in range(FRAME):
            for y in range(FRAME):
                if solid_at(sheet, f, x, y) and not solid_at(sheet, f, x, y - 1):
                    L.put(f, x, y, p['W'], 'W')
                    if solid_at(sheet, f, x, y + 1) and rnd('snow', f, x) < 0.45:
                        L.put(f, x, y + 1, p['L'], 'L')
    return L


# ---------------------------------------------------------------------------------------------
# Coats (recolours of the vanilla sheet with its saddle painted out)
# ---------------------------------------------------------------------------------------------
V = {'#': (81, 32, 0), 'm': (198, 133, 53), 's': (163, 99, 19), 'd': (127, 71, 3), 'l': (236, 173, 97), 'h': (249, 201, 144),
     'N': (99, 40, 22), 'n': (142, 75, 39), 'G': (112, 83, 31), 'g': (142, 120, 39)}
REMOVE = {
    'side': ((11, 13), tpl('''
        tt........
        ooooooooo.
        rrrrrrrrrr
        rrrrrrrrrr
        rrrrrrrrrr
        .rrrrrrrr.
        ..rrrrrr..
        ...rrrr...
    ''')),
    'down': ((9, 11), tpl('''
        ....rrrrrrr....
        ..rrrrrrrrrrr..
        ..rrrrrrrrrrr..
        .rr.........rr.
        .rr.........rr.
        .rr.........rr.
        .rr.........rr.
        .rr.........rr.
        .rr.........rr.
        ..r.........r..
        ..r.........r..
    ''')),
    'up': ((10, 11), tpl('''
        ...rrrrrrr...
        ..rrrrrrrrr..
        ..rrrrrrrrr..
        .rrrrrrrrrrr.
        .rrrrrrrrrrr.
        .rrrrrrrrrrr.
        .rrrrrrrrrrr.
        .r.........r.
    ''')),
}


def saddleless_vanilla(vanilla, anchors):
    """Return a copy of the vanilla sheet (as symbol letters) with the built-in saddle painted out."""
    rev = {v: k for k, v in V.items()}
    sym = np.full((H, W), '', dtype=object)
    for y in range(H):
        for x in range(W):
            p = vanilla[y, x]
            if p[3] == 0:
                sym[y, x] = '.'
            elif p[3] < 255:
                sym[y, x] = ','
            else:
                sym[y, x] = rev.get(tuple(int(c) for c in p[:3]), '?')
    for f, (view, dx, dy) in anchors.body.items():
        (ox, oy), rows = REMOVE[view]
        fx, fy = origin(f)
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                x, y = ox + i + dx, oy + j + dy
                if ch == '.' or not (0 <= x < 32 and 0 <= y < 32):
                    continue
                gx, gy = fx + x, fy + y
                if ch == 't':
                    sym[gy, gx] = '.'
                elif ch == 'o':
                    sym[gy, gx] = '#'
                elif ch == 'r':
                    if view == 'side':
                        yy = y - dy
                        sym[gy, gx] = 'm' if yy <= 16 else ('s' if (x + yy) % 3 else 'm') if yy <= 18 else 's'
                    elif view == 'up':
                        d = abs(x - dx - 16)
                        sym[gy, gx] = 'm' if d <= 2 else ('s' if d <= 4 else 'd')
                    else:
                        sym[gy, gx] = 's' if (x - dx) in (10, 11, 21, 22) else 'm'
    # any body pixel now touching transparency becomes outline (keeps a clean 1-px edge)
    out = sym.copy()
    for y in range(H):
        for x in range(W):
            if sym[y, x] not in ('.', ',', '?', '#'):
                fx, fy = (x // 32) * 32, (y // 32) * 32
                for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + ddx, y + ddy
                    if not (fx <= nx < fx + 32 and fy <= ny < fy + 32) or sym[ny, nx] in ('.', ','):
                        out[y, x] = '#'
                        break
    return out


COATS = {
    # name: (outline, h, l, m, s, d, maneDark, maneLight, pattern)
    'Galaxy_Stardust': ('0b0820', 'a78bfa', '6d4fd1', '3d2a8f', '2a1d6b', '1c1450', 'ff6bd6', '6fe8ff', 'stars'),
    'Iridium_Shimmer': ('2a1238', 'f4d4ff', 'd49af0', 'a45ccc', '7e3aa6', '5a2580', '3ee0c8', '9bf5e8', 'shine'),
    'GoldenWalnut_Gilded': ('4a2a06', 'fff3b0', 'ffd95a', 'f2b92e', 'd18f1a', 'a86a10', '6b3e12', '8f5a1e', 'speckle'),
    'SpiritsEve_Ghost': ('4a4066', 'ffffff', 'f4f0ff', 'e2dcf2', 'c8c0e0', 'a89ec8', '8fe8ff', 'd8fbff', 'wisp'),
    'FestivalOfIce_Frost': ('1e3a6e', 'ffffff', 'dff1ff', 'a9d4ff', '7fb6ec', '5a92d0', 'ffffff', 'e0f2ff', 'dapple'),
}


def make_coat(sym, spec):
    o, h, l, m, s, d, nd, nl, pattern = spec
    cmap = {'#': hexc(o), 'h': hexc(h), 'l': hexc(l), 'm': hexc(m), 's': hexc(s), 'd': hexc(d), 'N': hexc(nd), 'n': hexc(nl)}
    out = np.zeros((H, W, 4), dtype=np.uint8)
    for y in range(H):
        for x in range(W):
            c = sym[y, x]
            if c == '.':
                continue
            if c in cmap:
                col = cmap[c]
                if c in 'msdlh':
                    col = coat_pattern(pattern, x, y, c, cmap)
                out[y, x] = col
    return out


def coat_pattern(pattern, x, y, c, cmap):
    r = rnd(pattern, x, y)
    if pattern == 'stars':
        if r < 0.06:
            return hexc('ffffff')
        if r < 0.09:
            return hexc('ffe98a')
    elif pattern == 'shine':
        if c in 'ml' and (x - y) % 7 == 0:
            return cmap['h']
    elif pattern == 'speckle':
        if r < 0.07:
            return cmap['d']
    elif pattern == 'wisp':
        if c in 'sd' and r < 0.25:
            return cmap['m']
    elif pattern == 'dapple':
        if c in 'msd' and (x // 2 + y // 2) % 3 == 0 and r < 0.6:
            return cmap['l']
    return cmap[c]


def coat_copy_extras(out, vanilla):
    """Keep the game's shadow, carrots and horseshoes exactly as the vanilla sheet has them."""
    keep = {(71, 48, 39), (157, 177, 183), (96, 116, 122), (153, 72, 5), (255, 132, 0), (43, 201, 16), (44, 119, 14), (255, 175, 0)}
    for y in range(H):
        for x in range(W):
            p = vanilla[y, x]
            if p[3] > 0 and tuple(int(c) for c in p[:3]) in keep:
                out[y, x] = p


# ---------------------------------------------------------------------------------------------
# Breed coats (1.3.0): real-world colourings painted region by region on the saddle-less vanilla
# sheet (body / legs / head / mane+tail), with markings placed in reference-frame coordinates.
# Colours are chosen from general knowledge of horse colours; no other mod's art is used.
# ---------------------------------------------------------------------------------------------
SHADES = 'hlmsd'


def shade_set(*hexes):
    """Five shades, light to dark: h, l, m, s, d."""
    return dict(zip(SHADES, (hexc(h) for h in hexes)))


def mix(a, b, t):
    return tuple(int(round(a[i] * (1 - t) + b[i] * t)) for i in range(3)) + (255,)


def rainbow_at(k, lighten=0.0, darken=0.0):
    c = RAINBOW[k % len(RAINBOW)]
    if lighten:
        c = mix(c, (255, 255, 255, 255), lighten)
    if darken:
        c = mix(c, (0, 0, 0, 255), darken)
    return c


HEAD_BOX = {'side': (19, 6, 31, 20), 'down': (12, 13, 21, 29), 'up': (11, 2, 22, 10)}


def in_head(view, hx, hy):
    x0, y0, x1, y1 = HEAD_BOX[view]
    if not (x0 <= hx < x1 and y0 <= hy < y1):
        return False
    if view == 'side':
        return (hx >= 20 and hy <= 13) or hx >= 25
    return True


def classify(anchors, f, x, y):
    """Return (view, part, rx, ry, head) for a pixel in frame f. part: head, leg, lowleg or body;
    rx/ry: body reference coords; head: (view, hx, hy) head reference coords or None."""
    heads = anchors.head.get(f, [])
    for hv, hdx, hdy in heads:
        hx, hy = x - hdx, y - hdy
        if in_head(hv, hx, hy):
            if f in ICON_FRAMES or f not in anchors.body:
                return hv, 'head', hx, hy, (hv, hx, hy)
            view, dx, dy = anchors.body[f]
            if view != 'up':
                return view, 'head', x - dx, y - dy, (hv, hx, hy)
    if f not in anchors.body:
        return None, 'head', x, y, None
    view, dx, dy = anchors.body[f]
    rx, ry = x - dx, y - dy
    if view == 'up' and ry <= 9:
        return view, 'head', rx, ry, ('up', rx, ry)
    leg = {'side': ry >= 22, 'down': ry >= 22 and (rx <= 12 or rx >= 20), 'up': ry >= 19 and (rx <= 14 or rx >= 18)}[view]
    low = {'side': ry >= 25, 'down': ry >= 26, 'up': ry >= 26}[view]
    return view, ('lowleg' if (leg and low) else 'leg' if leg else 'body'), rx, ry, None


def face_front(sym, f):
    """Per frame and row, the front-most face pixel of side-view heads (facing right)."""
    fx, fy = origin(f)
    out = {}
    for y in range(32):
        xs = [x for x in range(32) if sym[fy + y, fx + x] in tuple(SHADES)]
        if xs:
            out[y] = max(xs)
    return out


BREED_COATS = {
    # name: dict(outline, body, legs, lowlegs, head, mane(N, n), marks...)
    'Breeds_SilverBay': dict(
        outline='3a1a0c', body=shade_set('d99a64', 'c27a44', 'a55f30', '8a4a22', '6b3818'),
        legs=shade_set('8a6a58', '6e5244', '5a4034', '4a342a', '3a2820'),
        mane=('c9c2b4', 'f2eee4'), mane_streak='a8a092'),
    'Breeds_RedDun': dict(
        outline='4a2410', body=shade_set('f4d2a6', 'e8b882', 'd9a06a', 'c48652', 'a86c3e'),
        legs=shade_set('d49a6a', 'c07e50', 'a86a40', '905634', '744428'),
        mane=('8a3a1c', 'b0582e'), dorsal='6e2a12', bars='8a3e1c'),
    'Breeds_Grulla': dict(
        outline='1e1a18', body=shade_set('b8ada0', 'a09486', '8a7e72', '74685e', '5e544c'),
        legs=shade_set('4a4440', '3a3532', '2e2a28', '26221f', '1e1b19'),
        head=shade_set('7a7068', '665c55', '554c46', '463f3a', '3a3430'),
        mane=('1c1a18', '3a3634'), dorsal='2a2624', bars='2e2a28'),
    'Breeds_DappleGrey': dict(
        outline='2a2c30', body=shade_set('e6e8ea', 'cfd3d6', 'a9aeb3', '8e949a', '71777e'),
        legs=shade_set('7a8086', '646a70', '52575c', '43474c', '36393d'),
        head=shade_set('eef0f1', 'dadddf', 'c2c6c9', 'a8adb1', '8e9398'),
        mane=('9aa0a6', 'e4e6e8'), dapple=True),
    'Breeds_Brindle': dict(
        outline='2a1a10', body=shade_set('d8b07a', 'c89a60', 'b0824a', '94693a', '78542c'),
        legs=shade_set('6a4a30', '5a3e28', '4a3220', '3c281a', '301f14'),
        mane=('2a1a10', '4a3020'), brindle='5a3a1e'),
    'Breeds_Sabino': dict(
        outline='4a1e0c', body=shade_set('e89a5c', 'd67e40', 'bf6a30', 'a45626', '86441c'),
        legs=shade_set('ffffff', 'f6f3ee', 'e8e2da', 'd8d0c6', 'c4bab0'),
        mane=('7a2e12', 'a8481e'), blaze='fbf8f2', belly='fbf8f2', white_legs=True),
    'Breeds_GoldChampagne': dict(
        outline='5a3a16', body=shade_set('fff0c0', 'f6dc98', 'e8c474', 'd4a85a', 'b88c44'),
        legs=shade_set('e8c474', 'd4a85a', 'c09450', 'a87e42', '8e6834'),
        mane=('c8923e', 'f2d08a'), muzzle='d8a090', sheen=True),
    'Prismatic_Night': dict(
        outline='0a0a12', body=shade_set('3a3a52', '2a2a3e', '1e1e2e', '171724', '10101a'),
        legs=shade_set('2a2a3e', '1e1e2e', '171724', '12121c', '0c0c14'),
        mane='aurora', shimmer=0.0),
    'Prismatic_Pearl': dict(
        outline='6a6480', body=shade_set('ffffff', 'fbf9ff', 'eeeaf6', 'dcd6ea', 'c6bedc'),
        legs=shade_set('fbf9ff', 'eeeaf6', 'dcd6ea', 'c6bedc', 'b0a8c8'),
        mane='opal', shimmer=0.45),
}


def make_breed_coat(sym, anchors, spec, key):
    out = np.zeros((H, W, 4), dtype=np.uint8)
    outline = hexc(spec['outline'])
    body = spec['body']
    legs = spec.get('legs', body)
    lowlegs = spec.get('lowlegs', legs)
    head = spec.get('head', body)
    fronts = {f: face_front(sym, f) for f in range(28)}
    for f in range(28):
        fx, fy = origin(f)
        for y in range(32):
            for x in range(32):
                c = sym[fy + y, fx + x]
                if c in ('.', ',', '?', ''):
                    continue
                gx, gy = fx + x, fy + y
                if c == '#':
                    out[gy, gx] = outline
                    continue
                view, part, rx, ry, hd = classify(anchors, f, x, y)
                if c in ('G', 'g'):
                    out[gy, gx] = legs['d']
                    continue
                if c in ('N', 'n'):
                    out[gy, gx] = mane_colour(spec, c, gx, gy, key)
                    continue
                pal_ = {'head': head, 'leg': legs, 'lowleg': lowlegs}.get(part, body)
                col = pal_[c]
                col = breed_marks(spec, key, col, c, f, x, y, view, part, rx, ry, hd, fronts[f], sym, fx, fy) or col
                out[gy, gx] = col
    return out


def mane_colour(spec, c, gx, gy, key):
    m = spec['mane']
    if m == 'aurora':
        # night: one smooth teal -> violet -> magenta gradient down the mane and tail, with star glints
        t = min(1.0, max(0.0, ((gy % 32) - 4) / 22.0))
        stops = [hexc('1f8a8a'), hexc('4a3aa8'), hexc('a0408c')]
        col = mix(stops[0], stops[1], t * 2) if t < 0.5 else mix(stops[1], stops[2], (t - 0.5) * 2)
        if c == 'n':
            col = mix(col, (255, 255, 255, 255), 0.25)
        if rnd(key, 'star', gx, gy) < 0.07:
            col = hexc('e8ecff')
        return col
    if m == 'opal':
        # pearl: silver-white mane with soft mother-of-pearl flecks (aqua, lilac, blush)
        col = hexc('d6d2e6') if c == 'N' else hexc('f4f2fa')
        r = rnd(key, 'opal', gx, gy)
        if r < 0.4:
            col = mix(col, hexc(('8fd8d4', 'b8a0ec', 'eca8c8')[int(r * 10) % 3]), 0.7)
        return col
    col = hexc(m[0] if c == 'N' else m[1])
    if spec.get('mane_streak') and rnd(key, 'streak', gx, gy) < spec.get('streak_rate', 0.22):
        col = hexc(spec['mane_streak'])
    return col


def breed_marks(spec, key, col, c, f, x, y, view, part, rx, ry, hd, front, sym, fx, fy):
    r = rnd(key, f, x, y)
    # dorsal stripe (duns)
    if spec.get('dorsal') and part == 'body':
        if view == 'side' and 8 <= rx <= 21 and sym[fy + y - 1, fx + x] == '#' and ry <= 16:
            return hexc(spec['dorsal'])
        if view == 'up' and rx == 16 and 11 <= ry <= 17:
            return hexc(spec['dorsal'])
    # primitive leg bars (duns)
    if spec.get('bars') and part in ('leg', 'lowleg') and ry in (23, 25):
        return mix(col, hexc(spec['bars']), 0.7)
    # dapples: soft light rings on the barrel and hindquarters
    if spec.get('dapple') and part == 'body' and c in 'msd':
        cx, cy = (rx + (ry // 3) % 2 * 2) % 4, ry % 3
        if (cx, cy) in ((1, 1), (2, 1)):
            return spec['body']['l'] if c != 'd' else spec['body']['m']
        if (cx, cy) in ((1, 0), (2, 2)) and r < 0.5:
            return spec['body']['h'] if c == 'm' else None
    # brindle: fine wavy vertical stripes on the body
    if spec.get('brindle') and part == 'body' and c in 'mlsd':
        wave = int(round(1.2 * math.sin(ry / 1.7)))
        if (rx + wave) % 3 == 0 and r < 0.85:
            return mix(col, hexc(spec['brindle']), 0.75)
    # sabino: white stockings, blaze, belly roaning, chin
    if spec.get('white_legs') and part == 'leg' and r < 0.35:
        return spec['legs'][c]
    if spec.get('white_legs') and part == 'leg':
        return mix(spec['body'][c], spec['legs'][c], 0.45)
    if spec.get('belly') and part == 'body' and view == 'side' and ry >= 19 and r < 0.55:
        return hexc(spec['belly'])
    if spec.get('blaze') and hd:
        hv, hx, hy = hd
        if hv == 'side' and 10 <= hy <= 18 and front.get(y) == x:
            return hexc(spec['blaze'])
        if hv == 'down' and ((hx == 16 and 17 <= hy <= 26) or (hx in (15, 17) and 21 <= hy <= 25)):
            return hexc(spec['blaze'])
    # champagne: pinkish mottled skin around the muzzle, metallic sheen
    if spec.get('muzzle') and hd:
        hv, hx, hy = hd
        if hv == 'side' and 15 <= hy <= 18 and front.get(y, -9) - x <= 1 and r < 0.8:
            return hexc(spec['muzzle'])
        if hv == 'down' and 25 <= hy <= 27 and 14 <= hx <= 18 and r < 0.7:
            return hexc(spec['muzzle'])
    if spec.get('sheen') and c in 'ml' and part != 'head' and (rx - ry) % 6 == 0:
        return spec['body']['h']
    # prismatic shimmer on highlights
    if 'shimmer' in spec and c in 'hl':
        k = (x + y + f) // 2
        if spec['shimmer'] == 0.0:
            # night: a cool violet sheen, with prismatic glints only on the brightest highlights
            if c == 'h' and part != 'head' and r < 0.6:
                return rainbow_at(k, lighten=0.2)
            return hexc('4a4a7e') if c == 'h' else hexc('38385e')
        return rainbow_at(k, lighten=1 - spec['shimmer'] * 0.6) if (c == 'h' or r < 0.6) else None
    return None


# ---------------------------------------------------------------------------------------------
# Ranch / winter tack (1.3.0)
# ---------------------------------------------------------------------------------------------
PALS.update({
    'Ranch_Sage': pal(O='27301c', D='4a5a36', M='6b7d4f', L='8fa06c', H='d6b04a', A='c9a13b', B='efe0b0', S='c9a13b'),
    'Winter_Frostglass': pal(O='2a4a6e', D='5d8fc0', M='8ec5ec', L='d4efff', H='ffffff', A='ffffff', B='b8e4ff', S='dfe9f2'),
    'Winter_FrostglassPad': pal(O='2d4f73', D='9cc9ea', M='c8e6fa', L='f2fbff', A='ffffff', B='a8d8f5', C='7fb6e0'),
    'Ranch_SageBridle': pal(O='27301c', D='4a5a36', M='6b7d4f', A='d6b04a', B='efe0b0', C='8fa06c', R='4a5a36'),
})


def sage_stitch(f, x, y, ch, i, j):
    if ch == 'M' and (i + j) % 2 == 0 and j in (3, 4) and VIEW_OF.get(f) == 'side':
        return 'B' if i % 4 == 1 else ch
    if ch == 'M' and rnd('sage', f // 7, i, j) > 0.95:
        return 'A'
    return ch


def frost_glass(f, x, y, ch, i, j):
    if ch in 'M' and (i - j) % 4 == 0:
        return 'L'
    if ch == 'M' and rnd('frost', i, j) > 0.9:
        return 'A'
    return ch


def frost_fern(f, x, y, ch, i, j):
    if ch == 'M' and (i + j) % 4 == 0:
        return 'L'
    if ch == 'M' and (i + 2 * j) % 7 == 0:
        return 'A'
    return ch


def sage_buckles(L, f, view, hx, hy, icon):
    p = PALS['Ranch_SageBridle']
    if view == 'side':
        L.put(f, 25 + hx, 12 + hy, p['A'])
        L.put(f, 25 + hx, 9 + hy, p['A'])


# ---------------------------------------------------------------------------------------------
# Witchy collection (1.3.0): deep purples, moss green, mushroom red, aged brass, moon silver.
# Original designs; style inspiration only (a witchy farm), nothing copied from any mod.
# ---------------------------------------------------------------------------------------------
PALS.update({
    'Witchy_Broomstick': pal(O='2a1a0e', D='4a2e16', M='6b4423', L='8a5a30', H='d0aa55', A='d0aa55', B='5b2e8a', S='a8843a',
                             Y='f0d27a', y='c9a24a', T='8a6a2a', b='5b2e8a'),
    'Witchy_Grimoire': pal(O='1e1030', D='3a1f5c', M='5b2e8a', L='efe4c8', H='d0aa55', A='d0aa55', B='c8322a', S='a8843a'),
    'Witchy_MossMushroom': pal(O='2a3a14', D='4f6b2a', M='6f8f3a', L='93b45a', A='d8362c', B='f4ecd8', C='465f22'),
    'Witchy_StarryHex': pal(O='140a24', D='2a1646', M='3f2266', L='b8bfd0', A='f4f6fb', B='d6dbe6', C='8a58c4'),
    'Witchy_PotionVials': pal(O='1e1208', M='3a2418', A='d0aa55', R='3a2418', G='6fe05a', P='f06ac0', U='5ac0f0', K='8a5a30', W='ffffff'),
    'Witchy_CrescentCharm': pal(O='140a24', M='4a2a78', A='c9cfdc', R='4a2a78', S='f4f6fb'),
    'Witchy_Hat': pal(O='160c26', M='3f2266', L='6a3fa0', A='5f7f32', B='d8b25a'),
    'Witchy_Familiar': pal(K='1a1420', V='7a58b0', Y='f2d23c'),
})

BRISTLES = {
    'side': ((5, 11), tpl("""
        .T...b
        TyY.Tb
        yYyYyb
        TyYyYb
        .yTyTb
        ..T...
    """)),
    'up': ((13, 17), tpl("""
        bbbbbbb
        yYyYyYy
        TyYyYyT
        .TyTyT.
        ..T.T..
    """)),
    'down': ((13, 9), tpl("""
        .T.T.T.
        TyTyTyT
    """)),
}


def wood_grain(f, x, y, ch, i, j):
    if ch == 'M' and (i + (j // 2)) % 3 == 0:
        return 'D'
    if ch == 'L' and i % 4 == 2:
        return 'H'
    return ch


def draw_broomstick(anchors):
    p = PALS['Witchy_Broomstick']
    L = draw_saddle(anchors, p, wood_grain)
    for f, view, dx, dy in body_views(anchors):
        (ox, oy), rows = BRISTLES[view]
        stamp(L, f, rows, ox + dx, oy + dy, p)
    return L


def grimoire(f, x, y, ch, i, j):
    v = VIEW_OF.get(f)
    clasp = {'side': {(5, 4): 'A', (6, 4): 'A', (5, 5): 'A'}, 'up': {(6, 3): 'A', (6, 4): 'A'}, 'down': {(7, 2): 'A'}}
    ribbon = {'side': {(8, 5): 'B', (8, 6): 'B'}, 'up': {(9, 4): 'B', (9, 5): 'B'}, 'down': {(4, 2): 'B'}}
    if (i, j) in clasp.get(v, {}):
        return clasp[v][(i, j)]
    if (i, j) in ribbon.get(v, {}):
        return ribbon[v][(i, j)]
    if ch == 'M' and v == 'side' and (i, j) in ((3, 3), (8, 3), (3, 5), (7, 5)):
        return 'A'  # brass corners
    return ch


MUSHROOMS = {
    'side': {(2, 6): 'A', (3, 6): 'A', (2, 7): 'B', (10, 5): 'A', (11, 5): 'A', (11, 6): 'B', (6, 7): 'A', (6, 8): 'B'},
    'down': {(2, 5): 'A', (2, 6): 'B', (12, 8): 'A', (12, 9): 'B', (1, 9): 'A', (13, 4): 'A'},
    'up': {(3, 2): 'A', (4, 2): 'A', (4, 3): 'B', (9, 4): 'A', (10, 4): 'A', (9, 5): 'B', (6, 6): 'A'},
}


def moss_mushroom(f, x, y, ch, i, j):
    v = VIEW_OF.get(f)
    if (i, j) in MUSHROOMS.get(v, {}) and ch in 'MLD':
        return MUSHROOMS[v][(i, j)]
    if ch == 'M':
        r = rnd('moss', f // 7, i, j)
        if r < 0.22:
            return 'C'
        if r < 0.36:
            return 'L'
    return ch


def starry_hex(f, x, y, ch, i, j):
    v = VIEW_OF.get(f)
    moon = {'side': {(4, 5): 'B', (5, 6): 'B', (4, 7): 'B'}, 'up': {(3, 3): 'B', (4, 4): 'B', (3, 5): 'B'}, 'down': {(2, 6): 'B', (2, 7): 'B'}}
    if (i, j) in moon.get(v, {}) and ch in 'MLD':
        return moon[v][(i, j)]
    if ch == 'M':
        r = rnd('hex', f // 7, i, j)
        if r < 0.08:
            return 'A'
        if (i * 2 + j * 3) % 9 == 0:
            return 'C'
    return ch


def potion_vials(L, f, view, hx, hy, icon):
    p = PALS['Witchy_PotionVials']
    if view == 'side':
        for (x, y, c) in ((24, 13, 'K'), (24, 14, 'G'), (24, 15, 'G'), (22, 14, 'K'), (22, 15, 'P'), (22, 16, 'P'),
                          (26, 17, 'K'), (26, 18, 'U')):
            L.put(f, x + hx, y + hy, p[c])
    elif view == 'down':
        for (x, y, c) in ((13, 25, 'K'), (13, 26, 'G'), (13, 27, 'G'), (19, 25, 'K'), (19, 26, 'P'), (19, 27, 'P')):
            L.put(f, x + hx, y + hy, p[c])


CRESCENT = {
    'side': ((21, 15), tpl("""
        .O.
        .O.
        OSO
        OSAO
        OSO.
        .OO.
    """)),
    'down': ((14, 20), tpl("""
        S...S
        OSASO
        .OOO.
    """)),
}


def crescent_charm(L, f, view, hx, hy, icon):
    p = PALS['Witchy_CrescentCharm']
    if view not in CRESCENT or (icon and view == 'side'):
        return  # the throat pendant would float off the small head icons
    (ox, oy), rows = CRESCENT[view]
    for j, row in enumerate(rows):
        for i, c in enumerate(row):
            if c != '.':
                L.put(f, ox + i + hx, oy + j + hy, p[c])


HAT = {
    'side': ((21, -1), tpl("""
        ..OO.......
        .OLMO......
        ..OLMO.....
        ...OLMO....
        ...OLMMO...
        ..OLMMMMO..
        ..OAABAAO..
        OOMMMMMMMOO
        .OOOOOOOOO.
    """)),
    'down': ((11, 6), tpl("""
        .....OO....
        .....OMO...
        ....OLMO...
        ...OLMMO...
        ...OLMMMO..
        ..OLMMMMO..
        ..OAABAAO..
        OOMMMMMMMOO
        .OOOOOOOOO.
    """)),
    'up': ((11, 1), tpl("""
        ....OMO....
        ...OLMMO...
        ..OAAAAAO..
        OOMMMMMMMOO
        .OOOOOOOOO.
    """)),
}


def draw_witch_hat(anchors):
    p = PALS['Witchy_Hat']
    L = Layer()
    for f in range(28):
        if f in ICON_FRAMES:
            continue
        for view, hx, hy in head_items(anchors, f):
            if view in ('side', 'down'):
                (ox, oy), rows = HAT[view]
                stamp(L, f, rows, ox + hx, oy + hy, p)
        if VIEW_OF.get(f) == 'up':
            hx, hy = up_head_anchor(anchors, f)
            (ox, oy), rows = HAT['up']
            stamp(L, f, rows, ox + hx, oy + hy, p)
    return L


FAMILIAR = {
    'side': ((5, 7), tpl("""
        ...K.K
        ...KKK
        ...VKY
        .KVKKK
        KVKKKK
        K.KKKK
        KK.KK.
    """)),
    'up': ((14, 18), tpl("""
        K...K
        KKKKK
        VKKKV
        .KKK.
        KKKKK
        .KKKKK
    """)),
    'down': ((15, 8), tpl("""
        K.K
        KKK
        YKY
    """)),
}


def draw_familiar(anchors):
    p = PALS['Witchy_Familiar']
    L = Layer()
    for f, view, dx, dy in body_views(anchors):
        (ox, oy), rows = FAMILIAR[view]
        stamp(L, f, rows, ox + dx, oy + dy, p)
    return L


BREED_COATS['Witchy_MidnightFamiliar'] = dict(
    outline='0c0814', body=shade_set('56407e', '3e2c5e', '2c2040', '231a34', '1a1328'),
    legs=shade_set('4a3470', '2c2040', '231a34', '1c1529', '15101f'),
    mane=('1a1226', '4a2e78'), mane_streak='7a4ab8', streak_rate=0.12, sheen=True)


# ---------------------------------------------------------------------------------------------
def resolve(p):
    """Turn prismatic tuples into colours after drawing."""
    return p


def draw_prismatic_saddle(anchors):
    base = PALS['Prismatic_Shard']
    L = Layer()
    for f, view, dx, dy in body_views(anchors):
        (ox, oy), rows = SADDLE[view]
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch in '. ':
                    continue
                x, y = ox + i + dx, oy + j + dy
                if ch in 'MLD':
                    c = RAINBOW[((x - dx) + (y - dy)) // 2 % len(RAINBOW)]
                    if ch == 'L':
                        c = tuple(min(255, int(v * 0.6 + 255 * 0.4)) for v in c[:3]) + (255,)
                    elif ch == 'D':
                        c = tuple(int(v * 0.75) for v in c[:3]) + (255,)
                    L.put(f, x, y, c, ch)
                else:
                    L.put(f, x, y, base[ch], ch)
    return L


def build_all(anchors, include_coats, vanilla):
    out = {}
    P = PALS
    out['saddles/SpiritsEve_Pumpkin'] = draw_saddle(anchors, P['SpiritsEve_Pumpkin'], pumpkin_ribs)
    out['saddles/Junimo_Leaf'] = draw_saddle(anchors, P['Junimo_Leaf'], junimo_star)
    out['saddles/MrQi_Casino'] = draw_saddle(anchors, P['MrQi_Casino'], qi_trim)
    out['saddles/Iridium_Shine'] = draw_saddle(anchors, P['Iridium_Shine'], iridium_shine)
    out['saddles/WinterStar_Holly'] = draw_saddle(anchors, P['WinterStar_Holly'], winter_holly)
    out['saddles/Prismatic_Shard'] = draw_prismatic_saddle(anchors)

    out['pads/EggFestival_Pastel'] = draw_pad(anchors, P['EggFestival_Pastel'], eggs)
    out['pads/Luau_Tropical'] = draw_pad(anchors, P['Luau_Tropical'], tropical)
    out['pads/Jellies_Moonlight'] = draw_pad(anchors, P['Jellies_Moonlight'], jellies)
    out['pads/FestivalOfIce_Snowflake'] = draw_pad(anchors, P['FestivalOfIce_Snowflake'], snowflakes)
    out['pads/Lewis_LuckyPurpleShorts'] = draw_pad(anchors, P['LuckyPurpleShorts'], shorts)
    out['pads/Joja_Corporate'] = draw_pad(anchors, P['Joja_Corporate'], joja)
    out['pads/Galaxy_Starfield'] = draw_pad(anchors, P['Galaxy_Starfield'], starfield)

    out['bridles/Fair_BlueRibbon'] = draw_bridle(anchors, P['Fair_BlueRibbon'], rosette)
    out['bridles/Stardrop_Star'] = draw_bridle(anchors, P['Stardrop_Star'], stardrop)
    out['bridles/WinterStar_JingleBells'] = draw_bridle(anchors, P['WinterStar_JingleBells'], bells)
    out['bridles/NightMarket_Pearl'] = draw_bridle(anchors, P['NightMarket_Pearl'], pearls)

    out['saddles/Ranch_Sage'] = draw_saddle(anchors, P['Ranch_Sage'], sage_stitch)
    out['saddles/Winter_Frostglass'] = draw_saddle(anchors, P['Winter_Frostglass'], frost_glass)
    out['pads/Winter_Frostglass'] = draw_pad(anchors, P['Winter_FrostglassPad'], frost_fern)
    out['bridles/Ranch_Sage'] = draw_bridle(anchors, P['Ranch_SageBridle'], sage_buckles)

    out['saddles/Witchy_Broomstick'] = draw_broomstick(anchors)
    out['saddles/Witchy_Grimoire'] = draw_saddle(anchors, P['Witchy_Grimoire'], grimoire)
    out['pads/Witchy_MossMushroom'] = draw_pad(anchors, P['Witchy_MossMushroom'], moss_mushroom)
    out['pads/Witchy_StarryHex'] = draw_pad(anchors, P['Witchy_StarryHex'], starry_hex)
    out['bridles/Witchy_PotionVials'] = draw_bridle(anchors, P['Witchy_PotionVials'], potion_vials)
    out['bridles/Witchy_CrescentCharm'] = draw_bridle(anchors, P['Witchy_CrescentCharm'], crescent_charm)
    out['styles/Witchy_Hat'] = draw_witch_hat(anchors)
    out['styles/Witchy_Familiar'] = draw_familiar(anchors)

    out['styles/FlowerDance_Crown'] = draw_crown(anchors, P['FlowerDance_Crown'])
    out['styles/Luau_Lei'] = draw_lei(anchors, P['Luau_Lei'])
    out['styles/Junimo_Buddy'] = draw_junimo(anchors, P['Junimo_Buddy'])
    out['styles/SpiritsEve_BatWings'] = draw_wings(anchors, P['SpiritsEve_BatWings'])
    out['styles/WinterStar_SnowDusting'] = draw_snow(anchors, P['WinterStar_SnowDusting'])

    if include_coats:
        sym = saddleless_vanilla(vanilla, anchors)
        for name, spec in COATS.items():
            a = make_coat(sym, spec)
            coat_copy_extras(a, vanilla)
            L = Layer()
            L.a = a
            out['coats/' + name] = L
        for name, spec in BREED_COATS.items():
            a = make_breed_coat(sym, anchors, spec, name)
            coat_copy_extras(a, vanilla)
            L = Layer()
            L.a = a
            out['coats/' + name] = L
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('vanilla')
    ap.add_argument('--alt', help='second horse shape for @elle fit variants (position reference only)')
    ap.add_argument('--saddle-mask', action='append', default=[],
                    help='extra saddle sheet(s) whose footprint pads should leave free (alpha only is read)')
    ap.add_argument('--out', default=os.path.join(os.path.dirname(__file__), '..', '..', 'assets'))
    args = ap.parse_args()

    vanilla = load(args.vanilla)
    extra = None
    for m in args.saddle_mask:
        a = load(m)[..., 3] > 0
        extra = a if extra is None else (extra | a)
    va = Anchors(vanilla)
    va.extra_saddle_mask = extra
    written = []
    for key, layer in build_all(va, True, vanilla).items():
        path = os.path.join(args.out, key + '.png')
        os.makedirs(os.path.dirname(path), exist_ok=True)
        layer.save(path)
        written.append(path)
    if args.alt:
        alt = load(args.alt)
        # the alternative shape's side view sits 2 px lower on the back and its head 2 px further left
        aa = Anchors(alt, head_shift=(-2, 0), body_shift={'side': (0, 2)})
        aa.extra_saddle_mask = extra
        for key, layer in build_all(aa, False, vanilla).items():
            path = os.path.join(args.out, key + '@elle.png')
            layer.save(path)
            written.append(path)
    print(f'wrote {len(written)} files')


if __name__ == '__main__':
    main()
