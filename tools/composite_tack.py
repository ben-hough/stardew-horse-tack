#!/usr/bin/env python3
"""
composite_tack.py - pre-composite Stardew Valley horse skins with tack overlays.

Makes one full horse spritesheet per (horse, saddle, [pad], bridle) combination so a
"flat folder" skin picker such as Multiplayer Horse Reskin (every PNG in assets/ = one
skin) can offer horses that already wear their saddle/bridle.

Layer order (bottom -> top), same as Elle's Cuter Horses content.json:
    horse -> saddle -> pad -> bridle
Every layer except the horse also gets a "none" option unless --no-none is given.

Requirements: Python 3.8+, Pillow  (pip install pillow)

Examples
--------
# Count only (nothing written): every horse x every saddle x every bridle
python composite_tack.py --horses "Elle/assets/Horse" \
    --saddles "Elle/assets/Saddles/Saddle_*.png" \
    --bridles "Elle/assets/Saddles/Bridle_*.png" --out out --dry-run

# A curated set: 3 horses, 2 saddle colours, matching pads, 2 bridles + preview sheet
python composite_tack.py --horses Elle/assets/Horse --only-horses SolidBrown,PintoSilver,Fresian \
    --saddles "Elle/assets/Saddles/Saddle_*.png" --only-saddles Brown,Black \
    --pads "Elle/assets/Saddles/Pad_*.png" --only-pads Red \
    --bridles "Elle/assets/Saddles/Bridle_*.png" --only-bridles Black,BrightRed \
    --out out --preview out/_preview.png

# Exact combos only (saddle,pad,bridle; use 'none' to skip a layer)
python composite_tack.py --horses Elle/assets/Horse --only-horses SolidBrown \
    --saddles "Elle/assets/Saddles/Saddle_*.png" --pads "Elle/assets/Saddles/Pad_*.png" \
    --bridles "Elle/assets/Saddles/Bridle_*.png" \
    --combo Brown,Red,Black --combo none,none,BrightRed --out out

Output names:  <horse>__<saddle>__<bridle>.png           (no --pads given)
               <horse>__<saddle>__<pad>__<bridle>.png    (--pads given)
Layer prefixes such as "Saddle_" / "Bridle_" are stripped from the names.
"""
import argparse
import glob
import itertools
import os
import sys

from PIL import Image, ImageDraw

FRAME = 32  # vanilla Animals/horse frames are 32x32, 7 columns x 4 rows = 224x128
OVERLAY_OPAQUE_THRESHOLD = 0.15  # < 15% opaque pixels => treat a "horse" file as an overlay


def list_pngs(spec):
    """spec may be a folder or a glob pattern."""
    if spec is None:
        return []
    if os.path.isdir(spec):
        files = glob.glob(os.path.join(spec, "*.png"))
    else:
        files = glob.glob(spec)
    return sorted(f for f in files if f.lower().endswith(".png"))


def strip_prefix_names(files):
    """Map display-name -> path, dropping a shared 'Prefix_' (e.g. Saddle_Brown -> Brown)."""
    stems = [os.path.splitext(os.path.basename(f))[0] for f in files]
    prefixes = {s.split("_", 1)[0] + "_" for s in stems if "_" in s}
    if len(prefixes) == 1 and all("_" in s for s in stems):
        p = prefixes.pop()
        stems = [s[len(p):] for s in stems]
    out = {}
    for s, f in zip(stems, files):
        if s.lower() == "none":
            sys.exit(f"ERROR: '{f}' would be named 'none', which is reserved.")
        out[s] = f
    return out


def filter_names(mapping, only, label):
    if not only:
        return mapping
    wanted = [w.strip() for w in only.split(",") if w.strip()]
    missing = [w for w in wanted if w not in mapping and w.lower() != "none"]
    if missing:
        sys.exit(f"ERROR: unknown {label}: {missing}. Available: {sorted(mapping)}")
    return {k: v for k, v in mapping.items() if k in wanted}


def opaque_fraction(img):
    a = img.getchannel("A")
    hist = a.histogram()
    return 1.0 - hist[0] / float(img.width * img.height)


class Cache:
    def __init__(self):
        self._c = {}

    def get(self, path):
        if path not in self._c:
            self._c[path] = Image.open(path).convert("RGBA")
        return self._c[path]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--horses", required=True, help="folder or glob of full horse spritesheets")
    ap.add_argument("--saddles", help="folder or glob of saddle overlays")
    ap.add_argument("--pads", help="folder or glob of saddle-pad overlays (optional layer)")
    ap.add_argument("--bridles", help="folder or glob of bridle overlays")
    ap.add_argument("--only-horses", help="comma list of horse names to include")
    ap.add_argument("--only-saddles", help="comma list (may include 'none')")
    ap.add_argument("--only-pads", help="comma list (may include 'none')")
    ap.add_argument("--only-bridles", help="comma list (may include 'none')")
    ap.add_argument("--combo", action="append", default=[],
                    help="explicit tack combo 'saddle,bridle' (or 'saddle,pad,bridle' with --pads); repeatable")
    ap.add_argument("--no-none", action="store_true", help="do not add a 'none' option for each tack layer")
    ap.add_argument("--allow-pad-without-saddle", action="store_true",
                    help="by default a pad is only used together with a saddle (Elle's rule)")
    ap.add_argument("--out", required=True, help="output folder")
    ap.add_argument("--max-outputs", type=int, default=2000, help="safety cap (default 2000)")
    ap.add_argument("--dry-run", action="store_true", help="only report how many files would be written")
    ap.add_argument("--overwrite", action="store_true", help="overwrite existing output files")
    ap.add_argument("--preview", help="also write a preview sheet PNG to this path")
    ap.add_argument("--preview-scale", type=int, default=3)
    ap.add_argument("--preview-cols", type=int, default=3)
    ap.add_argument("--preview-limit", type=int, default=12, help="max combos drawn in the preview")
    args = ap.parse_args()

    cache = Cache()

    horses = strip_prefix_names(list_pngs(args.horses))
    if not horses:
        sys.exit("ERROR: no horse PNGs found")
    # skip overlay-like files that live in the horse folder (e.g. Elle's PrismaticOverlay.png)
    for name in list(horses):
        frac = opaque_fraction(cache.get(horses[name]))
        if frac < OVERLAY_OPAQUE_THRESHOLD:
            print(f"note: skipping '{name}' in horses (only {frac:.1%} opaque - looks like an overlay)")
            del horses[name]
    horses = filter_names(horses, args.only_horses, "horses")

    layers = []  # (label, mapping)
    for label, spec, only in (("saddle", args.saddles, args.only_saddles),
                              ("pad", args.pads, args.only_pads),
                              ("bridle", args.bridles, args.only_bridles)):
        if spec is None:
            continue
        m = strip_prefix_names(list_pngs(spec))
        if not m:
            sys.exit(f"ERROR: no PNGs matched for --{label}s {spec}")
        m = filter_names(m, only, label + "s")
        opts = [(k, v) for k, v in sorted(m.items())]
        want_none = not args.no_none
        if only:
            want_none = any(w.strip().lower() == "none" for w in only.split(","))
        if want_none:
            opts = [("none", None)] + opts
        layers.append((label, dict(opts)))

    if not layers:
        sys.exit("ERROR: give at least one of --saddles / --pads / --bridles")
    labels = [l for l, _ in layers]

    # build tack combos
    if args.combo:
        combos = []
        for c in args.combo:
            parts = [p.strip() for p in c.split(",")]
            if len(parts) != len(layers):
                sys.exit(f"ERROR: --combo '{c}' needs {len(layers)} parts ({','.join(labels)})")
            for (label, m), p in zip(layers, parts):
                if p not in m and p.lower() != "none":
                    sys.exit(f"ERROR: combo '{c}': unknown {label} '{p}'")
            combos.append(tuple("none" if p.lower() == "none" else p for p in parts))
    else:
        combos = list(itertools.product(*[list(m.keys()) for _, m in layers]))
        if "pad" in labels and "saddle" in labels and not args.allow_pad_without_saddle:
            si, pi = labels.index("saddle"), labels.index("pad")
            combos = [c for c in combos if not (c[si] == "none" and c[pi] != "none")]

    total = len(horses) * len(combos)
    print(f"{len(horses)} horses x {len(combos)} tack combos ({' x '.join(labels)}) = {total} images")
    if args.dry_run:
        return
    if total > args.max_outputs:
        sys.exit(f"ERROR: {total} outputs exceeds --max-outputs {args.max_outputs}; narrow with --only-* or raise the cap")

    os.makedirs(args.out, exist_ok=True)
    layer_maps = [dict(m) for _, m in layers]
    written, preview_items = 0, []
    for hname, hpath in sorted(horses.items()):
        base = cache.get(hpath)
        for combo in combos:
            img = base.copy()
            for m, choice in zip(layer_maps, combo):
                if choice == "none":
                    continue
                ov = cache.get(m[choice])
                if ov.size != img.size:
                    sys.exit(f"ERROR: size mismatch {m[choice]} {ov.size} vs horse {hpath} {img.size}")
                img = Image.alpha_composite(img, ov)
            name = "__".join((hname,) + tuple(combo)) + ".png"
            dest = os.path.join(args.out, name)
            if os.path.exists(dest) and not args.overwrite:
                print(f"skip existing {name} (use --overwrite)")
            else:
                img.save(dest, optimize=True)
                written += 1
            if len(preview_items) < args.preview_limit:
                preview_items.append((name[:-4], img))
    print(f"wrote {written} files to {args.out}")

    if args.preview and preview_items:
        write_preview(preview_items, args.preview, args.preview_scale, args.preview_cols)
        print(f"preview: {args.preview}")


def checker(w, h, s=8):
    bg = Image.new("RGBA", (w, h), (120, 170, 90, 255))  # grass-ish
    d = ImageDraw.Draw(bg)
    for y in range(0, h, s):
        for x in range(0, w, s):
            if (x // s + y // s) % 2:
                d.rectangle([x, y, x + s - 1, y + s - 1], fill=(108, 156, 80, 255))
    return bg


def write_preview(items, path, scale, cols=3):
    w, h = items[0][1].size
    cw, ch = w * scale, h * scale
    label_h = 18
    cols = max(1, min(cols, len(items)))
    rows = (len(items) + cols - 1) // cols
    pad = 10
    sheet = Image.new("RGBA", (cols * (cw + pad) + pad, rows * (ch + label_h + pad) + pad), (30, 30, 30, 255))
    d = ImageDraw.Draw(sheet)
    for i, (name, img) in enumerate(items):
        r, c = divmod(i, cols)
        x = pad + c * (cw + pad)
        y = pad + r * (ch + label_h + pad)
        d.text((x, y + 3), name, fill=(255, 255, 255, 255))
        tile = checker(cw, ch)
        tile.alpha_composite(img.resize((cw, ch), Image.NEAREST))
        sheet.paste(tile, (x, y + label_h))
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    sheet.convert("RGB").save(path)


if __name__ == "__main__":
    main()
