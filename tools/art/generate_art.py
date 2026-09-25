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
