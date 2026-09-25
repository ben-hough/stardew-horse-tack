"""Builds the Nexus page images for Horse Tack & Styling 1.4.0.

Only HorseTack's own art is used: the 16 coats and the tack/styling overlays in the
repo's assets/ folder (the same PNGs that ship in the 1.4.0 zip). No vanilla horse
sheet, no Elle's Cuter Horses art, no other third-party art, no generative image model.
Every look respects the wizard's rules: one coat, one styling, one pad (only with a
saddle), one saddle, one bridle, drawn coat -> styling -> pad -> saddle -> bridle.
Sprites are 32x32 frames upscaled with nearest-neighbour. Backgrounds are drawn with
Pillow. Fonts: Pixelify Sans / Silkscreen (SIL OFL 1.1, in tools/fonts).

usage: python3 make_images.py [assets_dir] [out_dir]
"""
import os, sys, random
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
_REPO_ASSETS = os.path.normpath(os.path.join(HERE, "..", "..", "assets"))  # when run from the repo's nexus-page/tools
ASSETS = sys.argv[1] if len(sys.argv) > 1 else (_REPO_ASSETS if os.path.isdir(_REPO_ASSETS) else "/workspace/codex-horsetack/stardew-horse-tack/assets")
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.dirname(HERE)
F_TITLE = os.path.join(HERE, "fonts", "PixelifySans.ttf")
F_SMALL = os.path.join(HERE, "fonts", "Silkscreen-Regular.ttf")
F_BOLD = os.path.join(HERE, "fonts", "Silkscreen-Bold.ttf")
FONT = {}
def font(path, size):
    k = (path, size)
    if k not in FONT: FONT[k] = ImageFont.truetype(path, size)
    return FONT[k]

FRONT, SIDE, REAR, EAT = 0, 7, 14, 23
LAYERS = ["coats", "styles", "pads", "saddles", "bridles"]
USED = set()  # every PNG used, printed at the end as a provenance list

# ---------- sprites ----------
def art(layer, stem, season=None):
    d = os.path.join(ASSETS, layer)
    cands = ([f"{stem}.{season}.png"] if season else []) + [f"{stem}.png"]
    for c in cands:
        p = os.path.join(d, c)
        if os.path.exists(p):
            USED.add(f"{layer}/{c}")
            return Image.open(p).convert("RGBA")
    raise FileNotFoundError(f"{layer}/{stem}")

def look(coat, style=None, pad=None, saddle=None, bridle=None, season=None):
    """Composite one legal look (one piece per layer) in the mod's draw order."""
    assert coat, "every look uses one of HorseTack's own coats (never the vanilla sheet)"
    assert not (pad and not saddle), "the pad step only exists when a saddle is chosen"
    img = art("coats", coat, season)
    for layer, stem in (("styles", style), ("pads", pad), ("saddles", saddle), ("bridles", bridle)):
        if stem:
            img = Image.alpha_composite(img, art(layer, stem, season))
    for w, v in ((224, img.width), (128, img.height)):
        assert w == v, "HorseTack sheets are 224x128"
    return img

def frame(sheet, f, scale):
    x, y = (f % 7) * 32, (f // 7) * 32
    return sheet.crop((x, y, x + 32, y + 32)).resize((32 * scale, 32 * scale), Image.NEAREST)

def put_horse(canvas, sheet, f, scale, cx, bottom, shadow=True):
    """Draw frame f so the horse's feet stand on y=bottom, centred on cx, with a soft blocky shadow."""
    small = sheet.crop(((f % 7) * 32, (f // 7) * 32, (f % 7) * 32 + 32, (f // 7) * 32 + 32))
    l, t, r, b = small.getchannel("A").getbbox()
    x, y = cx - (l + r) * scale // 2, bottom - b * scale
    if shadow:
        sw = max(6, int((r - l) * 0.8))
        k = 4  # shadow drawn at 4x the sprite resolution so it reads as an oval, not a slab
        sh = Image.new("RGBA", (sw * k, 3 * k), (0, 0, 0, 0))
        ImageDraw.Draw(sh).ellipse([0, 0, sw * k - 1, 3 * k - 1], fill=(60, 44, 30, 50))
        sh = sh.resize((sw * scale, 3 * scale), Image.NEAREST)
        canvas.alpha_composite(sh, (cx - sh.width // 2, bottom - 2 * scale))
    canvas.alpha_composite(small.resize((32 * scale, 32 * scale), Image.NEAREST), (x, y))

# ---------- drawing helpers ----------
def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))

def text(d, xy, s, f, fill, shadow=None, off=3, anchor="la"):
    if shadow:
        d.text((xy[0] + off, xy[1] + off), s, font=f, fill=shadow, anchor=anchor)
    d.text(xy, s, font=f, fill=fill, anchor=anchor)

INK = (58, 40, 30, 255)
SUB = (120, 88, 60, 255)
CREAM = (246, 239, 222)

def panel_bg(w=1920, h=1080, title="", subtitle=""):
    """Clean gallery background: cream paper, pixel border, title band."""
    px = 8
    lw, lh = w // px, h // px
    img = Image.new("RGBA", (lw, lh), CREAM + (255,))
    d = ImageDraw.Draw(img)
    random.seed(w * 7 + len(title))
    for _ in range(lw * lh // 60):  # faint paper speckle
        img.putpixel((random.randrange(lw), random.randrange(lh)), (238, 229, 208, 255))
    d.rectangle([0, 0, lw - 1, lh - 1], outline=(150, 108, 70, 255), width=2)
    d.rectangle([3, 3, lw - 4, lh - 4], outline=(214, 190, 150, 255), width=1)
    d.rectangle([2, 2, lw - 3, 17], fill=(96, 64, 44, 255))
    d.line([(2, 18), (lw - 3, 18)], fill=(150, 108, 70, 255))
    img = img.resize((w, h), Image.NEAREST)
    d = ImageDraw.Draw(img)
    text(d, (56, 70), title, font(F_TITLE, 64), (252, 240, 214, 255), shadow=(50, 32, 22, 255), off=4, anchor="lm")
    if subtitle:
        text(d, (w - 56, 72), subtitle, font(F_SMALL, 24), (236, 214, 176, 255), anchor="rm")
    return img

def meadow(w, h, px, seed=3, horizon=0.74, clear=(0.0, 0.0)):
    """Pixel-art golden-hour meadow (drawn here, no third-party art)."""
    random.seed(seed)
    lw, lh = w // px, h // px
    img = Image.new("RGBA", (lw, lh))
    d = ImageDraw.Draw(img)
    gy = int(lh * horizon)
    top, mid, low = (70, 124, 204), (122, 174, 226), (200, 228, 242)
    bands = 12
    for y in range(gy):
        q = round(y / gy * bands) / bands
        c = lerp(top, mid, q / 0.55) if q < 0.55 else lerp(mid, low, (q - 0.55) / 0.45)
        d.line([(0, y), (lw, y)], fill=c + (255,))
    for _ in range(max(3, lw // 40)):  # blocky clouds
        cx, cy = random.randrange(lw), random.randrange(int(gy * 0.55))
        if clear[0] * lw - 12 < cx < clear[1] * lw + 12: continue  # keep clouds away from the title
        for k in range(random.randint(3, 5)):
            r = random.randint(2, 4)
            ox = cx + k * r - 4
            d.ellipse([ox - r * 2, cy - r, ox + r * 2, cy + r], fill=(250, 244, 236, 255))
    # hills
    for layer_i, col in enumerate([(116, 150, 110), (94, 136, 88)]):
        base = gy - 6 + layer_i * 3
        amp = 5 - layer_i * 2
        pts = [(0, lh)]
        for x in range(0, lw + 8, 8):
            pts.append((x, base - random.randint(0, amp)))
        pts.append((lw, lh))
        d.polygon(pts, fill=col + (255,))
    # wooden fence along the horizon
    fy = gy - 3
    for x in range(1, lw, 9):
        d.rectangle([x, fy - 5, x + 1, fy + 2], fill=(128, 88, 56, 255))
    d.line([(0, fy - 4), (lw, fy - 4)], fill=(150, 106, 68, 255))
    d.line([(0, fy - 1), (lw, fy - 1)], fill=(150, 106, 68, 255))
    grass = [(98, 158, 74), (86, 146, 66), (108, 168, 80)]
    for y in range(gy, lh):
        for x in range(lw):
            img.putpixel((x, y), grass[(x * 5 + y * 3 + random.randint(0, 2)) % 3] + (255,))
    for _ in range(lw // 3):  # flowers
        x, y = random.randrange(lw), random.randrange(gy + 1, lh)
        img.putpixel((x, y), random.choice([(250, 240, 120), (250, 250, 250), (236, 140, 170)]) + (255,))
    return img.resize((w, h), Image.NEAREST)

# ---------- looks (all legal wizard selections, HorseTack pieces only) ----------
WITCHY_A = dict(coat="Witchy_MidnightFamiliar", style="Witchy_Hat", pad="Witchy_StarryHex", saddle="Witchy_Grimoire", bridle="Witchy_CrescentCharm")
WITCHY_B = dict(coat="Witchy_MidnightFamiliar", style="Witchy_Familiar", pad="Witchy_MossMushroom", saddle="Witchy_Broomstick", bridle="Witchy_PotionVials")
FOREST_A = dict(coat="ForestSpirit_Spirit", style="ForestSpirit_AntlerCrown", pad="ForestSpirit_MossCloak", saddle="ForestSpirit_Heartwood", bridle="ForestSpirit_Vine")
FOREST_B = dict(coat="ForestSpirit_Spirit", style="ForestSpirit_WispHalo", pad="ForestSpirit_MossCloak", saddle="ForestSpirit_RootStone", bridle="ForestSpirit_GlowingRunes")
FESTIVE = dict(coat="Prismatic_Pearl", style="FlowerDance_Crown", pad="EggFestival_Pastel", saddle="Prismatic_Shard", bridle="Fair_BlueRibbon")
WINTER = dict(coat="Breeds_DappleGrey", style="WinterStar_SnowDusting", pad="FestivalOfIce_Snowflake", saddle="WinterStar_Holly", bridle="WinterStar_JingleBells")
SPOOKY = dict(coat="SpiritsEve_Ghost", style="SpiritsEve_BatWings", pad="Jellies_Moonlight", saddle="SpiritsEve_Pumpkin", bridle="NightMarket_Pearl")

NAMES = {  # display names as the wizard shows them (collections.json names or split file names)
    "Witchy_MidnightFamiliar": "Midnight Familiar", "Witchy_Hat": "Witch Hat", "Witchy_StarryHex": "Starry Hex",
    "Witchy_Grimoire": "Grimoire", "Witchy_CrescentCharm": "Crescent Charm", "Witchy_Familiar": "Familiar",
    "Witchy_MossMushroom": "Moss & Mushroom", "Witchy_Broomstick": "Broomstick", "Witchy_PotionVials": "Potion Vials",
    "ForestSpirit_Spirit": "Spirit Coat", "ForestSpirit_AntlerCrown": "Antler Crown", "ForestSpirit_MossCloak": "Moss Cloak",
    "ForestSpirit_Heartwood": "Heartwood", "ForestSpirit_Vine": "Vine", "ForestSpirit_WispHalo": "Wisp Halo",
    "ForestSpirit_RootStone": "Root & Stone", "ForestSpirit_GlowingRunes": "Glowing Runes",
}

def piece_line(lk, keys=("style", "pad", "saddle", "bridle")):
    return "  +  ".join(NAMES.get(lk[k], lk[k]) for k in keys if lk.get(k))

# ---------- images ----------
def banner(path, w=1300, h=372):
    img = meadow(w, h, px=4, seed=5, horizon=0.80, clear=(0.0, 0.45))
    gy = int(h // 4 * 0.80) * 4
    d = ImageDraw.Draw(img)
    # three looks, side view, standing on the grass on the right
    for lk, cx, s, by in ((SPOOKY, 700, 6, gy + 40), (FOREST_A, 935, 7, gy + 56), (WITCHY_A, 1165, 6, gy + 40)):
        put_horse(img, look(**lk, season="fall" if lk is FOREST_A else None), SIDE, s, cx, by)
    d = ImageDraw.Draw(img)
    text(d, (44, 52), "Horse Tack", font(F_TITLE, 84), (255, 250, 236, 255), shadow=(70, 48, 40, 255), off=5)
    text(d, (48, 140), "& Styling", font(F_TITLE, 66), (255, 232, 176, 255), shadow=(70, 48, 40, 255), off=4)
    text(d, (48, 226), "Coats, saddles, pads, bridles & styling", font(F_TITLE, 28), (255, 250, 236, 255), shadow=(50, 70, 40, 255), off=2)
    text(d, (48, 318), "A SMAPI mod for Stardew Valley 1.6  -  by MrGlim", font(F_SMALL, 17), (250, 244, 220, 255), shadow=(40, 60, 34, 255), off=2)
    img.convert("RGB").save(path, optimize=True)

def thumbnail(path, w=1920, h=1080):
    img = meadow(w, h, px=8, seed=11, horizon=0.60, clear=(0.12, 0.88))
    gy = int(h // 8 * 0.60) * 8
    looks = [(WITCHY_A, None), (FOREST_A, "fall"), (FESTIVE, None), (dict(WINTER, coat="Breeds_SilverBay"), None)]
    for i, (lk, season) in enumerate(looks):
        put_horse(img, look(**lk, season=season), SIDE, 12, 290 + i * 447, gy + 300)
    d = ImageDraw.Draw(img)
    text(d, (w // 2, 150), "Horse Tack & Styling", font(F_TITLE, 150), (255, 250, 236, 255), shadow=(70, 48, 40, 255), off=8, anchor="mm")
    text(d, (w // 2, 280), "Dress up your horse at the stable", font(F_TITLE, 62), (255, 232, 176, 255), shadow=(70, 48, 40, 255), off=4, anchor="mm")
    text(d, (w // 2, h - 70), "57 original pieces  -  live preview  -  multiplayer synced", font(F_SMALL, 38), (255, 250, 236, 255), shadow=(40, 60, 34, 255), off=3, anchor="mm")
    img.convert("RGB").save(path, optimize=True)

def gallery_witchy(path):
    img = panel_bg(title="Witchy collection", subtitle="9 original pieces")
    d = ImageDraw.Draw(img)
    s = 8
    for row, (lk, name) in enumerate(((WITCHY_A, "Look 1"), (WITCHY_B, "Look 2"))):
        sheet = look(**lk)
        top = 160 + row * 450
        text(d, (80, top + 10), f"{name}:  {NAMES[lk['coat']]} coat", font(F_BOLD, 30), INK)
        text(d, (80, top + 50), piece_line(lk), font(F_TITLE, 30), SUB)
        for i, (f, lab) in enumerate(((FRONT, "front"), (SIDE, "side"), (REAR, "back"), (EAT, "grazing"))):
            cx = 290 + i * 450
            put_horse(img, sheet, f, s, cx, top + 90 + 32 * s)
            text(d, (cx, top + 100 + 32 * s), lab, font(F_SMALL, 22), SUB, anchor="mt")
    img.convert("RGB").save(path, optimize=True)

def gallery_forest(path):
    img = panel_bg(title="Forest Spirit through the seasons", subtitle="8 original pieces, seasonal trims")
    d = ImageDraw.Draw(img)
    s = 8
    seasons = ["spring", "summer", "fall", "winter"]
    for row, lk in enumerate((FOREST_A, FOREST_B)):
        top = 170 + row * 440
        text(d, (80, top - 10), f"{NAMES[lk['coat']]}  +  {piece_line(lk)}", font(F_TITLE, 32), SUB)
        for i, se in enumerate(seasons):
            cx = 290 + i * 450
            put_horse(img, look(**lk, season=se), SIDE, s, cx, top + 60 + 30 * s)
            text(d, (cx, top + 70 + 30 * s), se.capitalize() if se != "fall" else "Fall", font(F_BOLD, 28), INK, anchor="mt")
    text(d, (960, 1040), "Moss Cloak and Antler Crown switch trims with the farm's season (summer uses the base art).", font(F_SMALL, 20), SUB, anchor="mm")
    img.convert("RGB").save(path, optimize=True)

FEST_CELLS = [
    ("Egg Festival", dict(coat="Breeds_SilverBay", pad="EggFestival_Pastel", saddle="Ranch_Sage")),
    ("Flower Dance", dict(coat="Breeds_GoldChampagne", style="FlowerDance_Crown")),
    ("Luau", dict(coat="Breeds_RedDun", style="Luau_Lei", pad="Luau_Tropical", saddle="Ranch_Sage")),
    ("Moonlight Jellies", dict(coat="Prismatic_Night", pad="Jellies_Moonlight", saddle="Iridium_Shine")),
    ("Stardew Valley Fair", dict(coat="Breeds_Sabino", bridle="Fair_BlueRibbon")),
    ("Spirit's Eve", dict(coat="SpiritsEve_Ghost", style="SpiritsEve_BatWings", saddle="SpiritsEve_Pumpkin")),
    ("Festival of Ice", dict(coat="FestivalOfIce_Frost", pad="FestivalOfIce_Snowflake", saddle="Winter_Frostglass")),
    ("Night Market", dict(coat="Prismatic_Night", bridle="NightMarket_Pearl")),
    ("Feast of the Winter Star", dict(coat="Breeds_DappleGrey", style="WinterStar_SnowDusting", saddle="WinterStar_Holly", bridle="WinterStar_JingleBells")),
    ("Ginger Island", dict(coat="GoldenWalnut_Gilded")),
    ("Junimo", dict(coat="Breeds_Grulla", style="Junimo_Buddy", saddle="Junimo_Leaf")),
    ("Iridium", dict(coat="Iridium_Shimmer", saddle="Iridium_Shine")),
    ("Prismatic", dict(coat="Prismatic_Pearl", saddle="Prismatic_Shard")),
    ("Galaxy", dict(coat="Galaxy_Stardust", pad="Galaxy_Starfield", saddle="Iridium_Shine")),
    ("Stardrop", dict(coat="Breeds_Brindle", bridle="Stardrop_Star")),
    ("Mr. Qi", dict(coat="Prismatic_Night", saddle="MrQi_Casino")),
    ("Joja", dict(coat="Breeds_SilverBay", pad="Joja_Corporate", saddle="Ranch_Sage")),
    ("Lewis", dict(coat="Breeds_RedDun", pad="Lewis_LuckyPurpleShorts", saddle="Ranch_Sage")),
]

def gallery_festivals(path):
    img = panel_bg(title="Festivals & valley favourites", subtitle="18 collections")
    d = ImageDraw.Draw(img)
    s, cols, cw, ch = 7, 6, 300, 292
    for n, (name, lk) in enumerate(FEST_CELLS):
        cx = 60 + cw // 2 + (n % cols) * cw
        top = 140 + (n // cols) * ch
        put_horse(img, look(**lk), SIDE, s, cx, top + 30 * s)
        text(d, (cx, top + 30 * s + 10), name, font(F_TITLE, 28 if len(name) < 20 else 22), INK, anchor="mt")
    text(d, (960, 1040), "Each look: that collection's pieces on a HorseTack coat. Pads need a saddle, so a plain one is added where needed.", font(F_SMALL, 19), SUB, anchor="mm")
    img.convert("RGB").save(path, optimize=True)

BREEDS = ["SilverBay", "RedDun", "Grulla", "DappleGrey", "Brindle", "Sabino", "GoldChampagne"]
BREED_NAMES = {"SilverBay": "Silver Bay", "RedDun": "Red Dun", "DappleGrey": "Dapple Grey", "GoldChampagne": "Gold Champagne"}
OTHER_COATS = [("Prismatic_Night", "Prismatic Night"), ("Prismatic_Pearl", "Prismatic Pearl"), ("Galaxy_Stardust", "Stardust"),
               ("Iridium_Shimmer", "Iridium Shimmer"), ("GoldenWalnut_Gilded", "Gilded"), ("FestivalOfIce_Frost", "Frost"),
               ("SpiritsEve_Ghost", "Ghost"), ("Witchy_MidnightFamiliar", "Midnight Familiar"), ("ForestSpirit_Spirit", "Spirit Coat")]

def gallery_coats(path):
    img = panel_bg(title="16 original coats", subtitle="7 breeds + 9 themed")
    d = ImageDraw.Draw(img)
    s = 6
    text(d, (80, 150), "Breeds", font(F_BOLD, 30), INK)
    for i, b in enumerate(BREEDS):
        cx = 170 + i * 263
        put_horse(img, look(coat="Breeds_" + b), SIDE, s, cx, 200 + 32 * s)
        text(d, (cx, 208 + 32 * s), BREED_NAMES.get(b, b), font(F_TITLE, 28), INK, anchor="mt")
    text(d, (80, 560), "Themed coats", font(F_BOLD, 30), INK)
    for i, (c, name) in enumerate(OTHER_COATS):
        cx = 150 + i * 203
        put_horse(img, look(coat=c), SIDE, s, cx, 610 + 32 * s)
        text(d, (cx, 618 + 32 * s), name, font(F_TITLE, 24 if len(name) < 15 else 21), INK, anchor="mt")
    text(d, (960, 1040), "Pick a coat, or Keep current to keep whatever your horse looks like now.", font(F_SMALL, 19), SUB, anchor="mm")
    img.convert("RGB").save(path, optimize=True)

MIX = [
    dict(coat="Breeds_Grulla", style="Witchy_Hat", pad="Luau_Tropical", saddle="Junimo_Leaf", bridle="Stardrop_Star"),
    dict(coat="Galaxy_Stardust", style="ForestSpirit_WispHalo", pad="Galaxy_Starfield", saddle="MrQi_Casino", bridle="Witchy_CrescentCharm"),
    dict(coat="Breeds_Sabino", style="FlowerDance_Crown", pad="Lewis_LuckyPurpleShorts", saddle="Ranch_Sage", bridle="Fair_BlueRibbon"),
    dict(coat="Iridium_Shimmer", style="Junimo_Buddy", pad="Winter_Frostglass", saddle="Prismatic_Shard", bridle="NightMarket_Pearl"),
    dict(coat="Breeds_GoldChampagne", style="Luau_Lei", pad="EggFestival_Pastel", saddle="ForestSpirit_Heartwood", bridle="ForestSpirit_Vine"),
    dict(coat="Witchy_MidnightFamiliar", style="SpiritsEve_BatWings", pad="Witchy_MossMushroom", saddle="SpiritsEve_Pumpkin", bridle="Witchy_PotionVials"),
    dict(coat="Breeds_Brindle", style="Witchy_Familiar", pad="Joja_Corporate", saddle="Iridium_Shine", bridle="Ranch_Sage"),
    dict(coat="Prismatic_Pearl", style="WinterStar_SnowDusting", pad="Jellies_Moonlight", saddle="Winter_Frostglass", bridle="WinterStar_JingleBells"),
    dict(coat="GoldenWalnut_Gilded", style="ForestSpirit_AntlerCrown", pad="Witchy_StarryHex", saddle="Witchy_Grimoire", bridle="ForestSpirit_GlowingRunes"),
    dict(coat="Breeds_RedDun", pad="ForestSpirit_MossCloak", saddle="ForestSpirit_RootStone", bridle="Stardrop_Star"),
    dict(coat="SpiritsEve_Ghost", style="Witchy_Hat", pad="FestivalOfIce_Snowflake", saddle="Witchy_Broomstick", bridle="Witchy_CrescentCharm"),
    dict(coat="Breeds_SilverBay", style="Luau_Lei", pad="Luau_Tropical", saddle="WinterStar_Holly", bridle="Fair_BlueRibbon"),
]

def gallery_mix(path):
    img = panel_bg(title="Mix & match any pieces", subtitle="one piece per layer")
    d = ImageDraw.Draw(img)
    # top: build-up of one look, layer by layer, in the mod's draw order
    base = dict(coat="Breeds_DappleGrey")
    steps = [("Coat", dict(base)),
             ("+ Saddle", dict(base, saddle="Junimo_Leaf")),
             ("+ Saddle pad", dict(base, saddle="Junimo_Leaf", pad="Witchy_StarryHex")),
             ("+ Bridle", dict(base, saddle="Junimo_Leaf", pad="Witchy_StarryHex", bridle="Stardrop_Star")),
             ("+ Styling", dict(base, saddle="Junimo_Leaf", pad="Witchy_StarryHex", bridle="Stardrop_Star", style="FlowerDance_Crown"))]
    s = 6
    for i, (lab, lk) in enumerate(steps):
        cx = 230 + i * 365
        put_horse(img, look(**lk), SIDE, s, cx, 140 + 30 * s)
        text(d, (cx, 150 + 30 * s), lab, font(F_TITLE, 30), INK, anchor="mt")
        if i:
            text(d, (cx - 182, 140 + 16 * s), ">", font(F_TITLE, 48), SUB, anchor="mm")
    d.line([(80, 420), (1840, 420)], fill=(214, 190, 150, 255), width=4)
    s, cols = 6, 6
    for n, lk in enumerate(MIX):
        cx = 60 + 150 + (n % cols) * 300
        bottom = 650 + (n // cols) * 300
        put_horse(img, look(**lk), SIDE, s, cx, bottom)
    text(d, (960, 1040), "Any HorseTack coat with any saddle, pad, bridle and styling - pads draw under saddles.", font(F_SMALL, 20), SUB, anchor="mm")
    img.convert("RGB").save(path, optimize=True)

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    jobs = [("banner.png", banner), ("thumbnail.png", thumbnail),
            ("gallery-2-witchy.png", gallery_witchy), ("gallery-3-forest-spirit-seasons.png", gallery_forest),
            ("gallery-4-festivals-and-valley.png", gallery_festivals), ("gallery-5-coats.png", gallery_coats),
            ("gallery-6-mix-and-match.png", gallery_mix)]
    for name, fn in jobs:
        fn(os.path.join(OUT, name))
        print("wrote", name)
    print("art used (%d files, all from HorseTack assets/):" % len(USED))
    for u in sorted(USED): print("  " + u)
