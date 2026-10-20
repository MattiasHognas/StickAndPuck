"""Stick & Puck supporter art (A19): every club's tifo, scarves, hand flags and slogan banner, drawn
from its crest (art/ui/crests.png, located through art/ui/ui.json) and its home colours and supporter
slogans (data/clubs/{league}/{id}.json, the `supporters` block), in the dusk palette
(art/palette/stickandpuck_dusk.gpl, never the kit block; every pixel is checked).

Writes one 256x256 sheet per club, art/crowd/supporters/{league}/{id}.png, all with the same layout,
which art/crowd/supporters.json describes for the game (rects in texels, frame order, suggested size
in metres):
  - tifo: 2 frames of 160x120, flat and rippled: the big cloth the home end raises over a stand section,
    crest in the middle on sun rays, the club's tifo lines below (the first line twice the font size
    when it fits).
  - scarf: 3 frames of 64x16, held taut over the head and two waving phases: the club's scarf text
    between two stripes, fringes at both ends.
  - flag: 3 frames of 28x24, a hand flag on a stick with the crest, waving.
  - banner: 2 frames of 96x20, a sign two fans hold up between two sticks with the banner slogan,
    taut and sagging.
Text uses the HUD pixel font's glyphs (ui_glyphs.py), so the slogans may use its capitals, digits,
Å Ä Ö É Ü and the other accented capitals; a slogan that does not fit or uses another character fails.

Also writes art/crowd/renders/supporters_contact.png: every club's flat tifo, scarf, flag and banner.

Run: python3 art/scripts/make_supporters.py [club ...]   (no ids: every club of both leagues; ids:
  only those clubs' sheets, keeping the rest as they are, e.g. hand-edited ones). Then make the
  .aseprite sources with make_supporters.lua (see its header) and, after hand edits, export them back.
"""
import glob
import json
import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ui_glyphs  # noqa: E402

ART = os.environ.get("SAP_ART") or os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
ROOT = os.path.dirname(ART)
DATA = os.path.join(ROOT, "data", "clubs")
OUT = os.path.join(ART, "crowd", "supporters")
LAYOUT = os.path.join(ART, "crowd", "supporters.json")
CONTACT = os.path.join(ART, "crowd", "renders", "supporters_contact.png")
LEAGUES = ["shl", "nhl"]

SHEET = 256
TIFO_W, TIFO_H = 160, 120
SCARF_W, SCARF_H = 96, 16
FLAG_W, FLAG_H = 28, 24
BANNER_W, BANNER_H = 96, 20
LAYOUT_RECTS = {
    "tifo": [[0, 0, TIFO_W, TIFO_H], [0, TIFO_H, TIFO_W, TIFO_H]],
    "scarf": [[160, i * SCARF_H, SCARF_W, SCARF_H] for i in range(3)],
    "flag": [[160 + i * FLAG_W, 48, FLAG_W, FLAG_H] for i in range(3)],
    "banner": [[160, 72 + i * BANNER_H, BANNER_W, BANNER_H] for i in range(2)],
}
FRAMES = {"tifo": ["flat", "ripple"], "scarf": ["held", "wave0", "wave1"],
          "flag": ["wave0", "wave1", "wave2"], "banner": ["taut", "sag"]}
# Suggested size in the stands, metres (width, height): the crowd is 16 texels a metre, the tifo
# covers a stand section (its width set by the game, the height following the 4:3 cloth).
METRES = {"tifo": [14.0, 10.5], "scarf": [2.4, 0.4], "flag": [0.9, 0.75], "banner": [2.4, 0.5]}


# ---------------------------------------------------------------- palette

def palette():
    """The dusk palette above the kit block: the colours the arena and crowd may snap to."""
    out = []
    for line in open(os.path.join(ART, "palette", "stickandpuck_dusk.gpl"), encoding="utf-8"):
        if line.startswith("# kit colours:"):
            break
        parts = line.split()
        if len(parts) >= 3 and all(p.isdigit() for p in parts[:3]):
            out.append(tuple(int(p) for p in parts[:3]))
    return out


PAL = palette()
CLEAR = (0, 0, 0, 0)


def dist(a, b):
    return 2 * (a[0] - b[0]) ** 2 + 4 * (a[1] - b[1]) ** 2 + 3 * (a[2] - b[2]) ** 2


def nearest(rgb, exclude=()):
    return min((c for c in PAL if c not in exclude), key=lambda c: dist(c, rgb))


def hexrgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def P(h):
    c = hexrgb(h)
    assert c in PAL, f"{h} is not on the palette"
    return c + (255,)


INK = P("#18131f")
NIGHT = P("#211b28")
WOOD = P("#9e655c")
WOOD_D = P("#613b3a")
WHITE = P("#efe0c2")


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def shade(c):
    """A darker palette neighbour of c: cloth in the folds."""
    want = tuple(int(v * 0.66) for v in c[:3])

    def off_hue(p):   # keeps the fold the cloth's own hue: a grey-blue never folds into teal
        d = [p[i] - want[i] for i in range(3)]
        return (d[0] - d[1]) ** 2 + (d[1] - d[2]) ** 2

    s = min((p for p in PAL if lum(p) < lum(c) - 8), key=lambda p: dist(p, want) + 6 * off_hue(p),
            default=INK[:3])
    return s + (255,)


def rays_on_black(c):
    """A black field has no darker shade: its rays go a step lighter instead."""
    want = tuple(min(255, int(v * 1.5) + 22) for v in c[:3])
    return nearest(want, exclude=(c[:3],)) + (255,)


def contrast(a, b):
    return abs(lum(a) - lum(b))


def colours(club):
    """(field, band, text, trim) from the club's home colours on the palette: the field is the
    main colour, the band and text the second colour (or whichever of trim, cream and ink reads
    best against the field)."""
    home = club["home"]
    field = nearest(hexrgb(home["primary"])) + (255,)
    second = nearest(hexrgb(home["secondary"])) + (255,)
    accent = nearest(hexrgb(home["accent"])) + (255,)
    band = second if contrast(second, field) >= 40 else accent
    if contrast(band, field) < 40:
        band = WHITE if lum(field) < 128 else INK
    # the lettering is light on its ink outline: the second colour or trim when light and readable, else cream
    text = next((c for c in (second, accent) if contrast(c, field) >= 70 and lum(c) >= 125), WHITE)
    trim = accent if accent not in (field, band) and contrast(accent, band) >= 30 else (
        INK if lum(band) > 90 else WHITE)
    return field, band, text, trim


# ---------------------------------------------------------------- text

GLYPHS = ui_glyphs.glyphs()
SPACE_ADVANCE = 3


def advance(ch):
    return SPACE_ADVANCE if ch == " " else len(GLYPHS[ch][0]) + 1


def text_width(s, scale=1):
    return (sum(advance(c) for c in s) - (1 if s else 0)) * scale


def check_text(s, where):
    bad = sorted({c for c in s if c not in GLYPHS})
    assert not bad, f"{where}: the pixel font has no {''.join(bad)!r} in {s!r}"


def ink_mask(s, scale):
    """Set of (x, y) inked by s, x from 0, y from the top of the accent rows."""
    pts = set()
    x = 0
    for ch in s:
        for ry, row in enumerate(GLYPHS[ch]):
            for rx, v in enumerate(row):
                if v == "#":
                    for sy in range(scale):
                        for sx in range(scale):
                            pts.add((x + rx * scale + sx, ry * scale + sy))
        x += advance(ch) * scale
    return pts


def draw_text(img, cx, top, s, col, scale=1, outline=None):
    """Draws s centred on cx with the accent rows' top at top; outline rings it one texel wide."""
    pts = ink_mask(s, scale)
    x0 = cx - text_width(s, scale) // 2
    if outline:
        ring = {(x + dx, y + dy) for x, y in pts for dx in (-1, 0, 1) for dy in (-1, 0, 1)} - pts
        for x, y in ring:
            put(img, x0 + x, top + y, outline)
    for x, y in pts:
        put(img, x0 + x, top + y, col)


def put(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def rect(img, x, y, w, h, c):
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            put(img, xx, yy, c)


# ---------------------------------------------------------------- crests

def load_crests():
    meta = json.load(open(os.path.join(ART, "ui", "ui.json"), encoding="utf-8"))["crests"]
    sheet = Image.open(os.path.join(ROOT, meta["file"])).convert("RGBA")
    cells = {cid: sheet.crop((x, y, x + w, y + h)) for cid, (x, y, w, h) in meta["cells"].items()}
    return cells, meta["fallback"]


def snap_image(img):
    """Every opaque pixel onto the palette (the crests already are; this is the check)."""
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            c = px[x, y]
            if c[3] == 0:
                continue
            assert c[3] == 255, "supporter art is opaque or clear"
            if c[:3] not in PAL:
                px[x, y] = nearest(c[:3]) + (255,)
    return img


# ---------------------------------------------------------------- pieces

def tifo(club, crest, ripple):
    """The big cloth: a frame in the trim and band colours, sun rays in the field's shade from the crest,
    the crest four (or, over two big lines, three) times its HUD size in an ink ring, and the tifo
    lines below it, each twice the font size when it fits."""
    field, band, text, trim = colours(club)
    dark = shade(field) if lum(field) >= 50 else rays_on_black(field)
    lines = club["supporters"]["tifo"]
    assert 1 <= len(lines) <= 2, f"{club['id']}: tifo has one or two lines"
    inner = TIFO_W - 16
    scales = []
    for line in lines:
        check_text(line, club["id"] + " tifo")
        s = 2 if text_width(line, 2) <= inner else 1
        assert text_width(line, s) <= inner, f"{club['id']}: tifo line {line!r} is too wide"
        scales.append(s)
    text_h = sum(11 * s for s in scales) + (len(lines) - 1)
    room = TIFO_H - 16                       # inside the 6-texel frame, with a texel or two of margin
    k = 4 if 64 + 2 + text_h <= room else 3
    crest_h = 16 * k
    y0 = 8 + (room - (crest_h + 2 + text_h)) // 2
    cx, cy = TIFO_W // 2, y0 + crest_h // 2
    img = Image.new("RGBA", (TIFO_W, TIFO_H), trim)
    rect(img, 1, 1, TIFO_W - 2, TIFO_H - 2, band)
    rect(img, 5, 5, TIFO_W - 10, TIFO_H - 10, trim)
    rays = 16
    for y in range(6, TIFO_H - 6):
        for x in range(6, TIFO_W - 6):
            a = math.atan2(y - cy + 0.5, x - cx + 0.5)
            ray = int(math.floor((a / (2 * math.pi) + 1) * rays * 2)) % 2
            img.putpixel((x, y), dark if ray else field)
    big = crest.resize((16 * k, 16 * k), Image.NEAREST)
    ox, oy = cx - big.width // 2, y0
    alpha = big.getchannel("A")
    for y in range(-2, big.height + 2):
        for x in range(-2, big.width + 2):
            if any(0 <= x + dx < big.width and 0 <= y + dy < big.height and alpha.getpixel((x + dx, y + dy))
                   for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2)):
                put(img, ox + x, oy + y, INK)
    img.alpha_composite(big, (ox, oy))
    if ripple:
        img = folds(img)   # the lettering stays clean over the folds
    top = y0 + crest_h + 2
    for line, s in zip(lines, scales):
        draw_text(img, cx, top, line, text, s, outline=INK)
        top += 11 * s + 1
    if ripple:
        img = lifted(img)
    return snap_image(img)


def fold(x, y):
    """The cloth billowing over the hands under it: a diagonal wave, -1 in a fold, 1 on a crest."""
    return math.sin(x * 0.11 + y * 0.19)


def folds(img):
    """The folds in shade."""
    out = img.copy()
    px = out.load()
    cache = {}
    for y in range(img.height):
        for x in range(img.width):
            if fold(x, y) < -0.72:
                c = px[x, y]
                if c not in cache:
                    cache[c] = shade(c)
                px[x, y] = cache[c]
    return out


def lifted(img):
    """The cloth lifted a texel along each crest of a fold."""
    out = img.copy()
    src = img.load()
    dst = out.load()
    for y in range(img.height):
        for x in range(img.width):
            if fold(x, y) > 0.55:
                dst[x, y] = src[x, min(img.height - 1, y + 1)]
    return out


def scarf(club, frame):
    """A supporter's scarf: stripes, the scarf text, fringes. frame 0 is held taut overhead, 1 and 2
    wave (each column dropped along a travelling sine)."""
    field, band, text, trim = colours(club)
    words = club["supporters"]["scarf"]
    check_text(words, club["id"] + " scarf")
    body_w, body_h, fringe = 88, 13, 4
    assert text_width(words) <= body_w - 4, f"{club['id']}: scarf text {words!r} is too wide"
    flat = Image.new("RGBA", (SCARF_W, body_h), CLEAR)
    rect(flat, fringe, 0, body_w, body_h, field)
    rect(flat, fringe, 0, body_w, 1, band)
    rect(flat, fringe, body_h - 1, body_w, 1, band)
    for x in (fringe, fringe + 1, fringe + body_w - 2, fringe + body_w - 1):
        rect(flat, x, 1, 1, body_h - 2, band)
    draw_text(flat, SCARF_W // 2, 1, words, text)
    for y in range(1, body_h - 1, 2):   # tassels: every other row
        for x in list(range(0, fringe)) + list(range(SCARF_W - fringe, SCARF_W)):
            put(flat, x, y, band if (y // 2) % 2 else field)
    img = Image.new("RGBA", (SCARF_W, SCARF_H), CLEAR)
    for x in range(SCARF_W):
        if frame == 0:
            dy = 0 if 3 <= x < SCARF_W - 3 else 1     # the ends droop off the hands
        else:
            dy = round(1.1 * math.sin(x * 0.16 + (frame - 1) * math.pi) + 1.1 * (1 - math.cos(x / SCARF_W * 2 * math.pi)) / 2)
        for y in range(body_h):
            c = flat.getpixel((x, y))
            if c[3]:
                put(img, x, 1 + y + dy, c)
    return snap_image(img)


def flag(club, crest, frame):
    """A hand flag: a wooden stick, the cloth in the club colours with the crest, waving out from the stick."""
    field, band, text, trim = colours(club)
    cw, ch = 22, 18
    cloth = Image.new("RGBA", (cw, ch), field)
    rect(cloth, 0, 0, cw, 1, band)
    rect(cloth, 0, ch - 1, cw, 1, band)
    rect(cloth, cw - 2, 0, 2, ch, band)
    cloth.alpha_composite(crest, (3, 1))
    img = Image.new("RGBA", (FLAG_W, FLAG_H), CLEAR)
    rect(img, 1, 0, 2, FLAG_H, WOOD)
    rect(img, 2, 0, 1, FLAG_H, WOOD_D)
    put(img, 1, 0, INK)
    put(img, 2, 0, INK)
    cache = {}
    for x in range(cw):
        t = x / (cw - 1)
        dy = round(2.2 * t * math.sin(x * 0.45 - frame * 2 * math.pi / 3))
        for y in range(ch):
            c = cloth.getpixel((x, y))
            if math.sin(x * 0.45 - frame * 2 * math.pi / 3) < -0.6 and x > 2:
                cache.setdefault(c, shade(c))
                c = cache[c]
            put(img, 3 + x, 1 + y + dy + 1, c)
    return snap_image(img)


def banner(club, frame):
    """A sign two fans hold up: the banner slogan on the club colours between two sticks; frame 1 sags."""
    field, band, text, trim = colours(club)
    words = club["supporters"]["banner"]
    check_text(words, club["id"] + " banner")
    cw, ch = BANNER_W - 6, 13
    assert text_width(words) <= cw - 4, f"{club['id']}: banner text {words!r} is too wide"
    cloth = Image.new("RGBA", (cw, ch), field)
    rect(cloth, 0, 0, cw, 1, band)
    rect(cloth, 0, ch - 1, cw, 1, band)
    draw_text(cloth, cw // 2, 1, words, text)
    img = Image.new("RGBA", (BANNER_W, BANNER_H), CLEAR)
    for sx in (1, BANNER_W - 3):
        rect(img, sx, 1, 2, BANNER_H - 1, WOOD)
        rect(img, sx + 1, 1, 1, BANNER_H - 1, WOOD_D)
    for x in range(cw):
        dy = 0 if frame == 0 else round(2 * math.sin(math.pi * x / (cw - 1)))
        for y in range(ch):
            put(img, 3 + x, 2 + y + dy, cloth.getpixel((x, y)))
    return snap_image(img)


# ---------------------------------------------------------------- sheets

def clubs():
    out = []
    for league in LEAGUES:
        for path in sorted(glob.glob(os.path.join(DATA, league, "*.json"))):
            club = json.load(open(path, encoding="utf-8"))
            club.setdefault("id", os.path.splitext(os.path.basename(path))[0])
            club["_league"] = league
            sup = club.get("supporters")
            assert sup, f"{path} has no supporters block"
            for key in ("tifo", "scarf", "banner"):
                assert sup.get(key), f"{path}: supporters.{key} is missing"
            out.append(club)
    return out


def sheet(club, crests, fallback):
    crest = crests.get(club["id"]) or crests[fallback]
    img = Image.new("RGBA", (SHEET, SHEET), CLEAR)
    for i, r in enumerate(LAYOUT_RECTS["tifo"]):
        img.paste(tifo(club, crest, ripple=i == 1), (r[0], r[1]))
    for i, r in enumerate(LAYOUT_RECTS["scarf"]):
        img.paste(scarf(club, i), (r[0], r[1]))
    for i, r in enumerate(LAYOUT_RECTS["flag"]):
        img.paste(flag(club, crest, i), (r[0], r[1]))
    for i, r in enumerate(LAYOUT_RECTS["banner"]):
        img.paste(banner(club, i), (r[0], r[1]))
    return img


def write_layout():
    meta = {
        "note": "A19 supporter art, from art/scripts/make_supporters.py. One 256x256 sheet per club at "
                "file (with {league} and {id} filled in), every sheet in this layout. Rects are [x, y, w, h] "
                "texels, in frame order; metres is a suggested [width, height] in the stands.",
        "file": "art/crowd/supporters/{league}/{id}.png",
        "size": SHEET,
        "pieces": {k: {"frames": FRAMES[k], "rects": LAYOUT_RECTS[k], "metres": METRES[k]} for k in LAYOUT_RECTS},
    }
    with open(LAYOUT, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")


def contact(all_clubs):
    """Every club: the flat tifo, then the held scarf, a flag and the taut banner beside it."""
    cols, cw, ch = 4, 264, 128
    rows = (len(all_clubs) + cols - 1) // cols
    img = Image.new("RGBA", (cols * cw + 8, rows * ch + 8), INK)
    for i, club in enumerate(all_clubs):
        s = Image.open(os.path.join(OUT, club["_league"], club["id"] + ".png")).convert("RGBA")
        x, y = 8 + (i % cols) * cw, 8 + (i // cols) * ch
        rect(img, x - 2, y - 2, cw - 4, ch - 4, NIGHT)
        img.alpha_composite(s.crop((0, 0, TIFO_W, TIFO_H)), (x, y))
        r = LAYOUT_RECTS["scarf"][0]
        img.alpha_composite(s.crop((r[0], r[1], r[0] + r[2], r[1] + r[3])), (x + TIFO_W + 4, y + 2))
        r = LAYOUT_RECTS["flag"][0]
        img.alpha_composite(s.crop((r[0], r[1], r[0] + r[2], r[1] + r[3])), (x + TIFO_W + 4, y + 22))
        r = LAYOUT_RECTS["flag"][1]
        img.alpha_composite(s.crop((r[0], r[1], r[0] + r[2], r[1] + r[3])), (x + TIFO_W + 34, y + 22))
        r = LAYOUT_RECTS["banner"][0]
        img.alpha_composite(s.crop((r[0], r[1], r[0] + r[2], r[1] + r[3])), (x + TIFO_W - 4, y + 52))
        r = LAYOUT_RECTS["banner"][1]
        img.alpha_composite(s.crop((r[0], r[1], r[0] + r[2], r[1] + r[3])), (x + TIFO_W - 4, y + 76))
    os.makedirs(os.path.dirname(CONTACT), exist_ok=True)
    img.save(CONTACT)


def main():
    only = set(sys.argv[1:])
    all_clubs = clubs()
    known = {c["id"] for c in all_clubs}
    assert only <= known, f"unknown clubs: {sorted(only - known)}"
    crests, fallback = load_crests()
    for club in all_clubs:
        if only and club["id"] not in only:
            continue
        folder = os.path.join(OUT, club["_league"])
        os.makedirs(folder, exist_ok=True)
        sheet(club, crests, fallback).save(os.path.join(folder, club["id"] + ".png"))
        print("wrote", club["_league"], club["id"])
    write_layout()
    contact(all_clubs)
    print("wrote", os.path.relpath(LAYOUT, ROOT), "and", os.path.relpath(CONTACT, ROOT))


if __name__ == "__main__":
    main()
