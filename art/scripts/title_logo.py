"""The title logo: 'STICK & PUCK' in chunky italic block letters, gold faces over a deep blue 3D
extrusion with a red ampersand, and a puck with a gold rim sliding out after the K with speed lines
and a few ice chips behind it. Every colour is on art/palette/stickandpuck_dusk.gpl.

Called from make_ui.py (build_title); returns an RGBA image trimmed to the logo plus a 1 px margin.
"""
from PIL import Image


def _hex(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


INK = _hex("#18131f")
NAVY = _hex("#2a2c40")
STEEL = _hex("#31556e")
BLUE = _hex("#2f5f8f")
SKY = _hex("#7f9cae")
FROST = _hex("#bdb9c4")
SNOW = _hex("#ccc9d2")
CREAM = _hex("#efe0c2")
GOLD_D = _hex("#c8963c")
GOLD = _hex("#f2c14e")
GOLD_L = _hex("#f9da93")
RED_D = _hex("#a34e4d")
RED = _hex("#c8574a")
RED_L = _hex("#e96f5d")
BRICK = _hex("#613b3a")
CLEAR = (0, 0, 0, 0)
COLOURS = [INK, NAVY, STEEL, BLUE, SKY, FROST, SNOW, CREAM, GOLD_D, GOLD, GOLD_L, RED_D, RED, RED_L, BRICK]

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


def _ellipse(img, cx, cy, rx, ry, col):
    for y in range(cy - ry, cy + ry + 1):
        for x in range(cx - rx, cx + rx + 1):
            if ((x - cx) / (rx + 0.5)) ** 2 + ((y - cy) / (ry + 0.5)) ** 2 <= 1:
                _put(img, x, y, col)


def _puck(img, cx, cy):
    """A puck seen a little from above: dark side, steel top face with a centred blue face,
    a glint, and a gold rim round the upper half of the top face."""
    rx, ry = 11, 4
    for dy in range(4, 0, -1):
        _ellipse(img, cx, cy + dy, rx, ry, INK if dy > 1 else NAVY)
    _ellipse(img, cx, cy, rx, ry, STEEL)
    _ellipse(img, cx, cy - 1, rx - 3, ry - 2, BLUE)
    for x in range(cx - 6, cx - 1):
        _put(img, x, cy - 2, CREAM)
    _put(img, cx - 7, cy - 1, SNOW)
    for y in range(cy - ry - 1, cy + ry + 2):
        for x in range(cx - rx - 1, cx + rx + 2):
            d = ((x - cx) / (rx + 0.5)) ** 2 + ((y - cy) / (ry + 0.5)) ** 2
            if 0.78 < d <= 1 and y <= cy:
                _put(img, x, y, GOLD_L)


def _speed(img, x_end, cy):
    """Speed lines and ice chips behind the puck."""
    for dy, length, c in ((0, 26, SKY), (3, 38, FROST), (6, 18, SKY)):
        for i in range(length):
            _put(img, x_end - i, cy + dy, c if i < length * 2 // 3 else STEEL)
    for dx, dy, c in ((0, 0, CREAM), (2, -2, SNOW), (-2, -3, FROST), (4, -4, SNOW), (1, -5, CREAM),
                      (-4, -1, FROST), (5, -1, CREAM), (-1, -7, SNOW), (3, -7, FROST)):
        _put(img, x_end + 4 + dx, cy - 2 + dy, c)


def build():
    w, h = 292, 42
    puck = Image.new("RGBA", (w, h), CLEAR)
    _speed(puck, 258, 32)
    _puck(puck, 275, 30)
    _outline(puck)
    words = Image.new("RGBA", (w, h), CLEAR)
    _stamp(words, _letters("STICK"), 8, 4, GOLD_FACE, 5, BLUE_SIDE, GOLD_L)
    _stamp(words, _letters(AMP), 122, 9, RED_FACE, 3, RED_SIDE, GOLD_L)
    _stamp(words, _letters("PUCK"), 146, 4, GOLD_FACE, 5, BLUE_SIDE, GOLD_L)
    _outline(words)
    out = Image.new("RGBA", (w, h), CLEAR)
    out.alpha_composite(puck)
    out.alpha_composite(words)
    left, top, right, bottom = out.getbbox()
    return out.crop((left - 1, top - 1, right + 1, bottom + 1))
