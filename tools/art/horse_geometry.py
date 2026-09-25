"""Per-frame anchors for the 224x128 horse sheet (7x4 frames of 32x32).

Everything in tools/art is drawn in the coordinates of three reference frames of the
VANILLA sheet (Content/Animals/horse by ConcernedApe): frame 0 (facing down), frame 7
(facing right) and frame 14 (facing up). For each target sheet (the vanilla horse, or an
optional second horse shape used only to place pixels) we find how far the body and the
head moved in every other frame by matching the silhouette, then draw the same shapes there.
No pixels are copied from the target sheets; they're only used as position references.
"""
import numpy as np
from PIL import Image

FRAME = 32
VIEW_OF = {**{f: 'down' for f in range(0, 7)}, **{f: 'side' for f in range(7, 14)},
           **{f: 'up' for f in range(14, 21)}, **{f: 'side' for f in range(21, 25)}, 25: 'up'}
REF = {'down': 0, 'side': 7, 'up': 14}
ICON_FRAMES = (26, 27)

# windows (x0, y0, x1, y1), in reference-frame coordinates
BODY_WIN = {'down': (9, 9, 24, 22), 'side': (4, 12, 21, 21), 'up': (9, 10, 24, 22)}
HEAD_WIN = {'side': (19, 5, 31, 20), 'down': (10, 14, 23, 29), 'up': (11, 3, 22, 11)}


def load(path):
    return np.array(Image.open(path).convert('RGBA')).astype(int)


def origin(f):
    return (f % 7) * FRAME, (f // 7) * FRAME


def frame(sheet, f):
    x, y = origin(f)
    return sheet[y:y + FRAME, x:x + FRAME]


def classes(fr):
    """0 = empty, 1 = shadow/semi, 2 = dark outline, 3 = body."""
    a = fr[..., 3]
    lum = fr[..., :3].mean(axis=2)
    c = np.where(a == 0, 0, np.where(a < 255, 1, np.where(lum < 70, 2, 3)))
    return c


def solid(fr):
    return fr[..., 3] == 255


def match(sheet, ref_f, win, f, dxs, dys, ref_sheet=None):
    """Best (dx, dy) so that window `win` of ref frame (in ref_sheet) matches frame f of sheet."""
    ref = classes(frame(ref_sheet if ref_sheet is not None else sheet, ref_f))
    tgt = classes(frame(sheet, f))
    x0, y0, x1, y1 = win
    best, best_score = (0, 0), -1e9
    for dy in dys:
        for dx in dxs:
            score = 0
            for y in range(y0, y1):
                ty = y + dy
                if not 0 <= ty < FRAME:
                    continue
                for x in range(x0, x1):
                    tx = x + dx
                    if not 0 <= tx < FRAME:
                        continue
                    r, t = ref[y, x], tgt[ty, tx]
                    if r in (0, 1) and t in (0, 1):
                        continue
                    score += 2 if r == t else -1
            score -= 0.01 * (abs(dx) + abs(dy))
            if score > best_score:
                best, best_score = (dx, dy), score
    return best, best_score


class Anchors:
    """Body and head offsets for every frame of one sheet."""

    def __init__(self, sheet, head_shift=(0, 0), body_shift=None):
        self.sheet = sheet
        self.extra_saddle_mask = None
        self.body = {}
        self.head = {}
        body_shift = body_shift or {}
        for f, view in VIEW_OF.items():
            ref = REF[view]
            (dx, dy), _ = match(sheet, ref, BODY_WIN[view], f, [0], range(-3, 4))
            sx, sy = body_shift.get(view, (0, 0))
            self.body[f] = (view, dx + sx, dy + sy)
            if view in HEAD_WIN:
                (hx, hy), _ = match(sheet, ref, HEAD_WIN[view], f, range(-3, 4), range(-3, 10))
                self.head[f] = [(view, hx + (head_shift[0] if view == 'side' else 0), hy)]
        # icon frames: find every head copy
        for f in ICON_FRAMES:
            found = []
            for view in ('side', 'down'):
                ref = REF[view]
                x0, y0, x1, y1 = HEAD_WIN[view]
                cands = []
                for dy in range(-y1, FRAME - y0):
                    for dx in range(-x1, FRAME - x0):
                        (bx, by), s = match(sheet, ref, HEAD_WIN[view], f, [dx], [dy])
                        cands.append((s, dx, dy))
                area = (x1 - x0) * (y1 - y0)
                cands.sort(reverse=True)
                for s, dx, dy in cands:
                    if s < area * 0.7:
                        break
                    if any(v == view and abs(dx - ox) < 8 and abs(dy - oy) < 8 for v, ox, oy in found):
                        continue
                    found.append((view, dx, dy))
            self.head[f] = [(v, dx + (head_shift[0] if v == 'side' else 0), dy) for v, dx, dy in found]
