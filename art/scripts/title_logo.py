"""The title logo: 'STICK & PUCK' in chunky italic block letters, gold faces over a deep blue 3D
extrusion with a red ampersand. Every colour is on art/palette/stickandpuck_dusk.gpl.

Called from make_ui.py (build_title); returns an RGBA image trimmed to the letters plus a 1 px margin.
"""
from PIL import Image


def _hex(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


INK = _hex("#18131f")
NAVY = _hex("#2a2c40")
STEEL = _hex("#31556e")
BLUE = _hex("#2f5f8f")
GOLD_D = _hex("#c8963c")
GOLD = _hex("#f2c14e")
GOLD_L = _hex("#f9da93")
RED_D = _hex("#a34e4d")
RED = _hex("#c8574a")
RED_L = _hex("#e96f5d")
BRICK = _hex("#613b3a")
CLEAR = (0, 0, 0, 0)

# Block letters, 10 rows tall with 3 px strokes; drawn at 2x and slanted.
GLYPHS = {
    "S": [".#######.", "#########", "###...###", "###......", "########.",
          ".########", "......###", "###...###", "#########", ".#######."],
    "T": ["#########", "#########"] + ["...###..."] * 8,
    "I": ["###"] * 10,
    "C": [".#######.", "#########", "###...###", "###......", "###......",
          "###......", "###......", "###...###", "#########", ".#######."],
    "K": ["###...###", "###..###.", "###.###..", "######...", "#####....",
          "######...", "###.###..", "###..###.", "###...###", "###...###"],
    "P": ["########.", "#########", "###...###", "###...###", "#########",
          "########.", "###......", "###......", "###......", "###......"],
    "U": ["###...###"] * 8 + ["#########", ".#######."],
}
AMP = [".###..", "##.##.", "##.##.", ".###..", "####.#", "##.###", "##..##", ".####."]
GAP = 3                    # px between letters before doubling
SLANT = 3                  # one px of italic shift per this many rows

GOLD_FACE = [GOLD_L, GOLD_L, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD_D, GOLD_D, GOLD_D]
BLUE_SIDE = [BLUE, BLUE, STEEL, NAVY, NAVY]
RED_FACE = [RED_L, RED_L, RED, RED, RED, RED_D]
RED_SIDE = [BRICK, BRICK, INK]


def _word(text):
    rows = len(GLYPHS[text[0]])
    width = sum(len(GLYPHS[c][0]) for c in text) + GAP * (len(text) - 1)
    mask = [[0] * width for _ in range(rows)]
    x = 0
    for c in text:
        for y, row in enumerate(GLYPHS[c]):
            for i, ch in enumerate(row):
                if ch == "#":
                    mask[y][x + i] = 1
        x += len(GLYPHS[c][0]) + GAP
    return mask


def _double(mask):
    return [[v for v in row for _ in range(2)] for row in mask for _ in range(2)]


def _slant(mask):
    h, w = len(mask), len(mask[0])
    out = [[0] * (w + (h - 1) // SLANT) for _ in range(h)]
    for y in range(h):
        dx = (h - 1 - y) // SLANT
        for x in range(w):
            if mask[y][x]:
                out[y][x + dx] = 1
    return out


def _letters(text_or_rows):
    rows = [[1 if c == "#" else 0 for c in r] for r in text_or_rows] if isinstance(text_or_rows, list) \
        else _word(text_or_rows)
    return _slant(_double(rows))


def _put(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def _outline(img, col=INK):
    """Ink round every opaque pixel (8-neighbour)."""
    src = img.copy()
    for y in range(img.height):
        for x in range(img.width):
            if src.getpixel((x, y))[3]:
                continue
            if any(0 <= x + dx < img.width and 0 <= y + dy < img.height and src.getpixel((x + dx, y + dy))[3]
                   for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                img.putpixel((x, y), col)


def _stamp(img, mask, ox, oy, face, depth, side, top):
    """Letters with a solid extrusion down-right (side colours front to back), an ink ring round
    the face, the face coloured by row, and a light top edge."""
    h, w = len(mask), len(mask[0])
    for d in range(depth, 0, -1):
        c = side[min(d - 1, len(side) - 1)]
        for y in range(h):
            for x in range(w):
                if mask[y][x]:
                    _put(img, ox + x + d, oy + y + d, c)
    for y in range(-1, h + 1):
        for x in range(-1, w + 1):
            if 0 <= y < h and 0 <= x < w and mask[y][x]:
                continue
            if any(0 <= y + dy < h and 0 <= x + dx < w and mask[y + dy][x + dx]
                   for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                _put(img, ox + x, oy + y, INK)
    for y in range(h):
        for x in range(w):
            if mask[y][x]:
                c = top if y == 0 or not mask[y - 1][x] else face[min(y * len(face) // h, len(face) - 1)]
                _put(img, ox + x, oy + y, c)


def build():
    w, h = 292, 42
    out = Image.new("RGBA", (w, h), CLEAR)
    _stamp(out, _letters("STICK"), 8, 4, GOLD_FACE, 5, BLUE_SIDE, GOLD_L)
    _stamp(out, _letters(AMP), 122, 9, RED_FACE, 3, RED_SIDE, GOLD_L)
    _stamp(out, _letters("PUCK"), 146, 4, GOLD_FACE, 5, BLUE_SIDE, GOLD_L)
    _outline(out)
    left, top, right, bottom = out.getbbox()
    return out.crop((left - 1, top - 1, right + 1, bottom + 1))


# ---------------------------------------------------------------- the letters one by one, for the title intro

def _pieces():
    """Each piece of the logo drawn on its own, in the order the intro drops them, as (char, sprite, x, y)
    with (x, y) where the sprite sits on build()'s untrimmed canvas."""
    pieces = []

    def piece(c, mask, face, depth, side, x, y):
        spr = Image.new("RGBA", (len(mask[0]) + 8, len(mask) + 8), CLEAR)
        _stamp(spr, mask, 2, 2, face, depth, side, GOLD_L)    # room for the ink ring and the outline round it
        _outline(spr)
        pieces.append((c, spr, x - 2, y - 2))

    def word(text, ox, oy):
        x = 0
        for c in text:
            piece(c, _letters(c), GOLD_FACE, 5, BLUE_SIDE, ox + x, oy)
            x += (len(GLYPHS[c][0]) + GAP) * 2

    word("STICK", 8, 4)
    piece("&", _letters(AMP), RED_FACE, 3, RED_SIDE, 122, 9)
    word("PUCK", 146, 4)
    return pieces


def build_letters():
    """The title intro's art: every piece of the logo trimmed to itself, side by side on one sheet, then a
    white mask of the assembled logo's gold (where the shine sweeps). Returns (sheet, pieces, shine) with
    pieces as [{char, rect, offset}] in drop order (offset: the piece's top left in the trimmed logo that
    build() returns) and shine as the mask's [x, y, w, h], the size of that logo."""
    full = Image.new("RGBA", (292, 42), CLEAR)
    _stamp(full, _letters("STICK"), 8, 4, GOLD_FACE, 5, BLUE_SIDE, GOLD_L)
    _stamp(full, _letters(AMP), 122, 9, RED_FACE, 3, RED_SIDE, GOLD_L)
    _stamp(full, _letters("PUCK"), 146, 4, GOLD_FACE, 5, BLUE_SIDE, GOLD_L)
    _outline(full)
    left, top, _, _ = full.getbbox()
    ox, oy = left - 1, top - 1
    logo = build()

    trimmed = []
    for c, spr, x, y in _pieces():
        # where a letter's outline ink lands on the extrusion of the letter before it, the logo keeps the
        # extrusion: leave that ink out so the letters assemble into exactly the logo
        for yy in range(spr.height):
            for xx in range(spr.width):
                lx, ly = x + xx - ox, y + yy - oy
                if spr.getpixel((xx, yy)) == INK and 0 <= lx < logo.width and 0 <= ly < logo.height \
                        and logo.getpixel((lx, ly)) not in (INK, CLEAR):
                    spr.putpixel((xx, yy), CLEAR)
        l, t, r, b = spr.getbbox()
        trimmed.append((c, spr.crop((l, t, r, b)), x + l - ox, y + t - oy))

    w = max(sum(s.width + 1 for _, s, _, _ in trimmed), logo.width)
    h = max(s.height for _, s, _, _ in trimmed) + 1 + logo.height
    sheet = Image.new("RGBA", (w, h), CLEAR)
    meta, x = [], 0
    for c, spr, px, py in trimmed:
        sheet.alpha_composite(spr, (x, 0))
        meta.append({"char": c, "rect": [x, 0, spr.width, spr.height], "offset": [px, py]})
        x += spr.width + 1
    sy = h - logo.height
    gold = {GOLD, GOLD_D, GOLD_L}
    for yy in range(logo.height):
        for xx in range(logo.width):
            if logo.getpixel((xx, yy)) in gold:
                sheet.putpixel((xx, sy + yy), (255, 255, 255, 255))
    return sheet, meta, [0, sy, logo.width, logo.height]


def assemble(sheet, pieces, size):
    """The logo put back together from the sheet's pieces in drop order, to compare with build()."""
    out = Image.new("RGBA", tuple(size), CLEAR)
    for p in pieces:
        x, y, w, h = p["rect"]
        out.alpha_composite(sheet.crop((x, y, x + w, y + h)), tuple(p["offset"]))
    return out


def same_picture(a, b):
    """Whether two RGBA images show the same pixels (any colour under full transparency counts as clear)."""
    if a.size != b.size:
        return False
    return all(x == y or (x[3] == 0 and y[3] == 0) for x, y in zip(_pixels(a), _pixels(b)))


def _pixels(img):
    px = img.load()
    return [px[x, y] for y in range(img.height) for x in range(img.width)]


def colour_map(built, shipped):
    """How the shipped title.png (exported through Aseprite's indexed palette) recoloured build()'s colours,
    as {built colour: shipped colour}; None when shipped is not the same drawing."""
    if built.size != shipped.size:
        return None
    m = {}
    for x, y in zip(_pixels(built), _pixels(shipped)):
        if x[3] == 0 and y[3] == 0:
            continue
        if m.setdefault(x, y) != y or (x[3] == 0) != (y[3] == 0):
            return None
    return m


def recolour(sheet, pieces, mapping):
    """The sheet with its pieces (not the shine mask) in the shipped colours."""
    out = sheet.copy()
    px = out.load()
    for p in pieces:
        x0, y0, w, h = p["rect"]
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                if px[x, y][3]:
                    px[x, y] = mapping.get(px[x, y], px[x, y])
    return out
