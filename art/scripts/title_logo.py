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
