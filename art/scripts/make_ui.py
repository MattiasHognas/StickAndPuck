"""Builds the HUD art in art/ui/ as PNGs plus metadata: the pixel font (font.png + font.fnt),
the scorebug frame, message panels, icons, club crests and the title logo. Every colour is
snapped to art/palette/stickandpuck_dusk.gpl; the font is a white ink mask that the game tints
with a palette colour.

These PNGs are only the starting point. make_ui.lua turns them into .aseprite files, which are
the source of truth from then on; after editing one in Aseprite, export it back to its PNG and
run `python3 make_ui.py fnt` if the font changed, so font.fnt follows the new glyph widths.

Run: python3 art/scripts/make_ui.py [all|fnt|font|title|crests [club ...]]  (font adds glyphs new to CHARS to
  font.png and rewrites font.fnt, leaving existing cells alone; then run make_ui.lua only=font;
  title rebuilds only title.png, the intro's title_letters.png and their ui.json entries; crests only
  crests.png and its entry: clubs new to CRESTS, plus any named, are drawn)
"""
import json
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import title_logo  # noqa: E402
import ui_glyphs  # noqa: E402

ART = os.environ.get("SAP_ART") or os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
UI = os.path.join(ART, "ui")
PAL_FILE = os.path.join(ART, "palette", "stickandpuck_dusk.gpl")


def load_palette():
    cols = []
    for line in open(PAL_FILE, encoding="utf-8"):
        parts = line.split()
        if len(parts) >= 3 and all(p.isdigit() for p in parts[:3]):
            cols.append(tuple(int(p) for p in parts[:3]))
    return cols


PAL = load_palette()


def snap(hexcol):
    """Nearest palette colour (weighted RGB) to a hex colour."""
    h = hexcol.lstrip("#")
    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    return min(PAL, key=lambda c: 2 * (c[0] - r) ** 2 + 4 * (c[1] - g) ** 2 + 3 * (c[2] - b) ** 2)


def P(hexcol):
    """An exact palette colour; fails if it is not on the palette."""
    c = snap(hexcol)
    assert "#%02x%02x%02x" % c == hexcol.lower(), f"{hexcol} is not on the palette"
    return c + (255,)


# Named palette colours used below.
INK = P("#18131f")        # darkest plum: outlines
NIGHT = P("#211b28")
PLUM = P("#2a2230")
PLUM2 = P("#33293a")
SLATE = P("#423236")
STEEL = P("#3e5875")
BLUE = P("#2f5f8f")
GREY = P("#74666b")
BRICK = P("#a34e4d")
CORAL = P("#c8574a")
GOLD_D = P("#c8963c")
GOLD = P("#f2c14e")
GOLD_L = P("#f9da93")
CREAM = P("#efe0c2")
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)

GLYPHS = ui_glyphs.glyphs()
# New glyphs go on the end, so existing cells keep their place in font.png (see `font` mode).
CHARS = " !\"#%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ_ÅÄÖÉÜÁÍÓÚÝÈČŠŽŘÔËÑØŁÇ"
CELL_W, CELL_H, COLS = 6, ui_glyphs.HEIGHT, 16
SPACE_ADVANCE = 3


def advance(ch):
    return SPACE_ADVANCE if ch == " " else len(GLYPHS[ch][0]) + 1


def text_width(s):
    return sum(advance(c) for c in s) - (1 if s else 0)


def draw_text(img, x, y, s, col, scale=1):
    """Draws text with its top-of-capitals at y (accents go above)."""
    for ch in s:
        rows = GLYPHS[ch]
        for ry, row in enumerate(rows):
            for rx, v in enumerate(row):
                if v == "#":
                    for sy in range(scale):
                        for sx in range(scale):
                            px, py = x + rx * scale + sx, y + (ry - 2) * scale + sy
                            if 0 <= px < img.width and 0 <= py < img.height:
                                img.putpixel((px, py), col)
        x += advance(ch) * scale


def rect(img, x, y, w, h, col):
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            img.putpixel((xx, yy), col)


def frame(img, x, y, w, h, col):
    rect(img, x, y, w, 1, col)
    rect(img, x, y + h - 1, w, 1, col)
    rect(img, x, y, 1, h, col)
    rect(img, x + w - 1, y, 1, h, col)


def bitmap(img, x, y, rows, col):
    for ry, row in enumerate(rows):
        for rx, v in enumerate(row):
            if v == "#":
                img.putpixel((x + rx, y + ry), col)


# ---------------------------------------------------------------- font

def build_font():
    rows = (len(CHARS) + COLS - 1) // COLS
    img = Image.new("RGBA", (COLS * CELL_W, rows * CELL_H), CLEAR)
    for i, ch in enumerate(CHARS):
        cx, cy = (i % COLS) * CELL_W, (i // COLS) * CELL_H
        bitmap(img, cx, cy, GLYPHS[ch], WHITE)
    img.save(os.path.join(UI, "font.png"))


def extend_font():
    """Adds the CHARS that font.fnt lacks to font.png, keeping every existing (maybe hand-edited)
    cell as it is: the old sheet is pasted at the top-left and only the new cells are drawn."""
    old_chars = set()
    for line in open(os.path.join(UI, "font.fnt"), encoding="utf-8"):
        if line.startswith("char id="):
            old_chars.add(chr(int(line.split()[1].split("=")[1])))
    count = len(old_chars)
    assert set(CHARS[:count]) == old_chars, "CHARS must only grow at the end"
    old = Image.open(os.path.join(UI, "font.png")).convert("RGBA")
    rows = (len(CHARS) + COLS - 1) // COLS
    img = Image.new("RGBA", (COLS * CELL_W, rows * CELL_H), CLEAR)
    img.paste(old, (0, 0))
    for i, ch in enumerate(CHARS[count:], start=count):
        cx, cy = (i % COLS) * CELL_W, (i // COLS) * CELL_H
        bitmap(img, cx, cy, GLYPHS[ch], WHITE)
    img.save(os.path.join(UI, "font.png"))


def write_fnt():
    """Writes font.fnt (BMFont text format, which Raylib's LoadFont reads) from font.png,
    measuring each glyph's width from its ink so hand edits in Aseprite carry over."""
    img = Image.open(os.path.join(UI, "font.png")).convert("RGBA")
    lines = [
        'info face="StickAndPuck" size=%d bold=0 italic=0 charset="" unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=1,1' % CELL_H,
        "common lineHeight=%d base=%d scaleW=%d scaleH=%d pages=1 packed=0" % (CELL_H + 1, ui_glyphs.BASE, img.width, img.height),
        'page id=0 file="font.png"',
        "chars count=%d" % len(CHARS),
    ]
    for i, ch in enumerate(CHARS):
        cx, cy = (i % COLS) * CELL_W, (i // COLS) * CELL_H
        inked = [x for x in range(CELL_W) for y in range(CELL_H) if img.getpixel((cx + x, cy + y))[3] > 0]
        w = max(inked) + 1 if inked else (SPACE_ADVANCE - 1)
        adv = SPACE_ADVANCE if ch == " " else w + 1
        lines.append("char id=%d x=%d y=%d width=%d height=%d xoffset=0 yoffset=0 xadvance=%d page=0 chnl=15"
                     % (ord(ch), cx, cy, w, CELL_H, adv))
    with open(os.path.join(UI, "font.fnt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


# ---------------------------------------------------------------- scorebug

def build_scorebug(meta):
    """One dark bar: [crest | name | score] x2, then [period | clock]. The game draws crests,
    text and the club colour stripe into the slots listed in ui.json."""
    h = 18
    crest, name_w, score_w = 16, text_width("WWW"), text_width("88") + 6
    team_w = 1 + crest + 3 + name_w + 3 + score_w
    per_w, clock_w = text_width("OT") + 6, text_width("20:00") + 6
    w = 1 + team_w + 1 + team_w + 1 + per_w + clock_w + 1
    img = Image.new("RGBA", (w, h), CLEAR)
    rect(img, 0, 0, w, h, PLUM)
    frame(img, 0, 0, w, h, INK)
    rect(img, 1, 1, w - 2, 1, PLUM2)            # top light edge
    slots = {}
    x = 1
    for side in ("home", "away"):
        slots[f"{side}_crest"] = [x + 1, 1, crest, crest]
        nx = x + 1 + crest + 3
        slots[f"{side}_name"] = [nx, 4, name_w, 7]
        rect(img, nx, 13, name_w, 2, NIGHT)      # stripe well; the game fills it with the club colour
        slots[f"{side}_stripe"] = [nx, 13, name_w, 2]
        sx = nx + name_w + 3
        rect(img, sx, 1, score_w, h - 2, NIGHT)
        rect(img, sx, 1, 1, h - 2, INK)
        slots[f"{side}_score"] = [sx + 3, 6, score_w - 6, 7]
        x = sx + score_w
        rect(img, x, 1, 1, h - 2, INK)
        x += 1
    rect(img, x, 1, per_w + clock_w, h - 2, SLATE)
    rect(img, x, 1, per_w + clock_w, 1, GREY)
    slots["period"] = [x + 3, 6, per_w - 6, 7]
    rect(img, x + per_w, 3, 1, h - 6, PLUM2)
    slots["clock"] = [x + per_w + 3, 6, clock_w - 6, 7]
    img.save(os.path.join(UI, "scorebug.png"))
    meta["scorebug"] = {"file": "art/ui/scorebug.png", "size": [w, h], "position": [8, 8],
                        "slots": slots, "note": "Text slots are the 7 px capital box: draw text at slot y - 2.", "textColour": "#efe0c2", "scoreColour": "#f2c14e"}
    return img


# ---------------------------------------------------------------- panels (9-slice)

def build_panels(meta):
    """9x9 panels with a 3 px border; the game stretches the centre (Raylib NPatchInfo)."""
    out = {}
    for name, (edge, fill, light) in {
        "panel": (INK, PLUM, PLUM2),
        "panel_gold": (INK, GOLD_D, GOLD),
        "panel_red": (INK, BRICK, CORAL),
    }.items():
        img = Image.new("RGBA", (9, 9), CLEAR)
        rect(img, 1, 0, 7, 9, edge)
        rect(img, 0, 1, 9, 7, edge)
        rect(img, 1, 1, 7, 7, fill)
        rect(img, 1, 1, 7, 1, light)
        rect(img, 1, 1, 1, 7, light)
        img.save(os.path.join(UI, f"{name}.png"))
        out[name] = {"file": f"art/ui/{name}.png", "border": [3, 3, 3, 3]}
    meta["panels"] = out


# ---------------------------------------------------------------- icons (8x8 cells)

ICONS = {
    "puck": ["........", "........", "..####..", ".######.", "#......#", ".######.", "..####..", "........"],
    "arrow_right": ["........", "..#.....", "..##....", "..###...", "..####..", "..###...", "..##....", "..#....."],
    "arrow_left": ["........", ".....#..", "....##..", "...###..", "..####..", "...###..", "....##..", ".....#.."],
    "check": ["........", ".......#", "......##", "#....##.", "##..##..", ".####...", "..##....", "........"],
    "cross": ["........", "##....##", ".##..##.", "..####..", "...##...", "..####..", ".##..##.", "##....##"],
    "star": ["...#....", "...#....", "..###...", "#######.", ".#####..", "..###...", ".##.##..", ".#...#.."],
    "whistle": ["........", "........", ".#####..", "#.....#.", "#.....##", "#.....#.", ".#####..", "........"],
    "stick": [".......#", "......#.", ".....#..", "....#...", "...#....", "..#.....", "##......", "###....."],
}
ICON_COLOURS = {"puck": (INK, PLUM2), "arrow_right": (GOLD, None), "arrow_left": (GOLD, None),
                "check": (GOLD, None), "cross": (CORAL, None), "star": (GOLD, None),
                "whistle": (CREAM, None), "stick": (GOLD_L, None)}


def build_icons(meta):
    names = list(ICONS)
    img = Image.new("RGBA", (8 * len(names), 8), CLEAR)
    cells = {}
    for i, n in enumerate(names):
        col, inner = ICON_COLOURS[n]
        bitmap(img, i * 8, 0, ICONS[n], col)
        if n == "puck":   # fill the puck's face, keep a dark rim
            rect(img, i * 8 + 1, 4, 6, 1, inner)
        cells[n] = [i * 8, 0, 8, 8]
    img.save(os.path.join(UI, "icons.png"))
    meta["icons"] = {"file": "art/ui/icons.png", "cells": cells}


# ---------------------------------------------------------------- crests (16x16)

SHIELD = [
    "################",
    "################",
    "################",
    "################",
    "################",
    "################",
    "################",
    "################",
    "################",
    ".##############.",
    ".##############.",
    "..############..",
    "...##########...",
    "....########....",
    ".....######.....",
    "......####......",
]
ROUND = [
    ".....######.....",
    "...##########...",
    "..############..",
    ".##############.",
    ".##############.",
    "################",
    "################",
    "################",
    "################",
    "################",
    "################",
    ".##############.",
    ".##############.",
    "..############..",
    "...##########...",
    ".....######.....",
]

EMBLEMS = {
    "crown": ["#..#..#", "#.###.#", "#######"],
    "clover": ["...##...", "..####..", "...##...", ".##..##.", "########", ".##..##.", "...##...", "...#...."],
    "pine": ["...#...", "..###..", ".#####.", "..###..", ".#####.", "#######", "...#...", "...#..."],
    "hawk": ["...####..", "..######.", ".##.#####", "####.####", "#..######", "....#####", "....####.", ".....##.."],
    "lion": ["..##......", ".####.....", "####......", ".####..#..", "..#####.#.", "..######..", "..#####...", ".##..##...", ".#....#...", "##...##..."],
    "leaf": ["...#...", "..###..", ".#####.", "###.###", "###.###", ".##.##.", "..#.#..", "...#...", "...#..."],
    "puck": ["..####..", ".######.", "#......#", ".######.", "..####.."],
    # NHL (A16): made-up symbols that evoke each club, not its real logo
    "snowflake": ["#..#..#", ".#.#.#.", "..###..", "#######", "..###..", ".#.#.#.", "#..#..#"],
    "quill": [".......#", "......##", ".....###", "....####", "...####.", "..####..", ".#.##...", ".#......", "#......."],
    "star": ["...#...", "...#...", "#######", ".#####.", "..###..", ".##.##.", "##...##"],
    "note": ["...#...", "...##..", "...#.#.", "...#..#", "...#...", ".###...", "####...", ".##...."],
    "fleur": ["....#....", "...###...", "#..###..#", "##.###.##", ".#######.", "#########", "..#.#.#..", "...###...", "....#...."],
    "tail": ["#.......#", "###...###", ".#######.", "...###...", "....#....", "....#....", "....#...."],
    "trident": ["#..#..#", "#..#..#", "#######", "...#...", "...#...", "...#...", "...#...", "...#..."],
    "foot": ["#..#..#", "#..#..#", "##.#.##", ".#####.", ".#####.", "..###..", "...#...", "...#..."],
    "flame": ["..#....", "..##...", ".###..#", ".####.#", "#######", "###.###", "##...##", ".##.##.", "..###.."],
    "visor": ["..###..", ".#####.", "#######", "#.#.#.#", "#######", "#######", ".#####."],
    "storm": ["..####.", ".##....", "##.##..", "#.#.#.#", "..##.##", "....##.", ".####.."],
    "lighthouse": ["..#..", ".###.", "..#..", ".###.", ".#.#.", ".###.", ".#.#.", "#####"],
    "jet": ["....#....", "...###...", "...###...", ".#######.", "#########", "...###...", "..#####.."],
    "anchor": ["..###..", "..#.#..", "..###..", "...#...", "#######", "...#...", "#..#..#", ".#.#.#.", "..###.."],
    "bolt": ["...##", "..##.", ".##..", "#####", "..##.", ".##..", "##...", "#...."],
    "peaks": ["...#......", "..###.....", ".#####.#..", "#######.#.", "#########.", "##########"],
    "maple": ["....#....", "...###...", "#.#####.#", "#########", ".#######.", "..#####..", "#.#####.#", "....#....", "....#...."],
    "drop": ["..#..", "..#..", ".###.", ".###.", "#####", "#####", "#####", ".###."],
    "paw": ["..#.#..", "#.#.#.#", "#.....#", "..###..", ".#####.", ".#####.", "..###.."],
    "triangle": ["....#....", "...###...", "..##.##..", ".##...##.", "#########"],
    "fangs": ["#######", "#######", ".##.##.", ".##.##.", ".#...#.", ".#...#."],
    "sabres": ["#.......#", ".#.....#.", "..#...#..", "...#.#...", "....#....", "...#.#...", "..#...#..", "##.....##", "##.....##"],
    "helmet": ["...#...", "..###..", ".#####.", "#######", "#.#.#.#", "#.....#", "##...##"],
    "fin": ["...#....", "...##...", "...###..", "..####..", ".######.", "########"],
    "wing": ["##.......", "####.....", "######...", ".########", "..#######", "...#.#.#."],
}

# Club colours from each club's current crest (shl.se team logos), as hex. They are snapped to
# the palette when drawn; where the dusk palette has no close match the value is already the
# palette colour we want (navy reads as steel blue, bright reds as coral). Change them here and
# rerun; the four greens and the orange at the end of the palette were added for these crests.
CRESTS = [
    # id,          shape,  field,     band,      emblem,              emblem colour
    ("brynas",     ROUND,  "#111111", "#f0c000", ("clover", None),    "#efe0c2"),
    ("djurgarden", SHIELD, "#f0e000", "#c8963c", ("diagonal", None),  "#c8574a"),
    ("farjestad",  SHIELD, "#008050", "#f0c000", ("text", "F"),       "#efe0c2"),
    ("frolunda",   ROUND,  "#005030", "#efe0c2", ("bars", "F"),       "#efe0c2"),
    ("hv71",       SHIELD, "#3e5875", "#f0d000", ("text", "HV"),      "#f0d000"),
    ("linkoping",  ROUND,  "#3e5875", "#efe0c2", ("lion", None),      "#efe0c2"),
    ("lulea",      ROUND,  "#111111", "#f0c000", ("text", "L"),       "#c8574a"),
    ("malmo",      SHIELD, "#111111", "#c8574a", ("hawk", None),      "#efe0c2"),
    ("orebro",     SHIELD, "#c8574a", "#111111", ("text", "Ö"),       "#efe0c2"),
    ("rogle",      ROUND,  "#007030", "#efe0c2", ("text", "R"),       "#efe0c2"),
    ("skelleftea", ROUND,  "#f2c14e", "#111111", ("text", "A"),       "#111111"),
    ("timra",      SHIELD, "#efe0c2", "#2f5f8f", ("pine", None),      "#c8574a"),
    ("vaxjo",      ROUND,  "#3e5875", "#c8963c", ("lion", None),      "#e07a3a"),
    ("bjorkloven", ROUND,  "#005040", "#f0d000", ("leaf", None),      "#f0d000"),
    ("generic",    ROUND,  "#423236", "#74666b", ("puck", None),      "#efe0c2"),
    # NHL (A16): each club's colours (on the kit block of the palette) and a simple symbol that
    # evokes the club. Made up for the game: none copies a real NHL logo.
    ("avalanche",      ROUND,  "#6e2438", "#2f5f8f", ("snowflake", None),  "#efe0c2"),
    ("blackhawks",     ROUND,  "#d42a22", "#111111", ("quill", None),      "#efe0c2"),
    ("blue_jackets",   SHIELD, "#2b3d6b", "#d42a22", ("star", None),       "#efe0c2"),
    ("blues",          ROUND,  "#1c3a9e", "#f2c14e", ("note", None),       "#efe0c2"),
    ("bruins",         ROUND,  "#111111", "#f2c14e", ("text", "B"),        "#f2c14e"),
    ("canadiens",      SHIELD, "#b8323c", "#2b3d6b", ("fleur", None),      "#efe0c2"),
    ("canucks",        ROUND,  "#2b3d6b", "#2f8a4e", ("tail", None),       "#efe0c2"),
    ("capitals",       SHIELD, "#d42a22", "#2b3d6b", ("text", "W"),        "#efe0c2"),
    ("devils",         ROUND,  "#d42a22", "#111111", ("trident", None),    "#111111"),
    ("ducks",          SHIELD, "#111111", "#f0581c", ("foot", None),       "#f0581c"),
    ("flames",         ROUND,  "#d42a22", "#f2c14e", ("flame", None),      "#f2c14e"),
    ("flyers",         ROUND,  "#f0581c", "#111111", ("text", "P"),        "#111111"),
    ("golden_knights", SHIELD, "#2a2c36", "#b49a5e", ("visor", None),      "#b49a5e"),
    ("hurricanes",     ROUND,  "#d42a22", "#111111", ("storm", None),      "#efe0c2"),
    ("islanders",      ROUND,  "#2e6db0", "#e07a3a", ("lighthouse", None), "#efe0c2"),
    ("jets",           ROUND,  "#2b3d6b", "#a7a3b0", ("jet", None),        "#efe0c2"),
    ("kings",          SHIELD, "#111111", "#a7a3b0", ("crown", None),      "#a7a3b0"),
    ("kraken",         ROUND,  "#2a2c36", "#9ad2d0", ("anchor", None),     "#9ad2d0"),
    ("lightning",      ROUND,  "#2b3d6b", "#efe0c2", ("bolt", None),       "#efe0c2"),
    ("mammoth",        SHIELD, "#111111", "#6aaedf", ("peaks", None),      "#6aaedf"),
    ("maple_leafs",    ROUND,  "#2b3d6b", "#efe0c2", ("maple", None),      "#efe0c2"),
    ("oilers",         ROUND,  "#2b3d6b", "#f0581c", ("drop", None),       "#f0581c"),
    ("panthers",       SHIELD, "#b8323c", "#2b3d6b", ("paw", None),        "#f2c14e"),
    ("penguins",       ROUND,  "#111111", "#f2c14e", ("triangle", None),   "#f2c14e"),
    ("predators",      SHIELD, "#f2c14e", "#2b3d6b", ("fangs", None),      "#2b3d6b"),
    ("rangers",        SHIELD, "#1c3a9e", "#d42a22", ("text", "NY"),       "#efe0c2"),
    ("red_wings",      ROUND,  "#d42a22", "#efe0c2", ("wing", None),       "#efe0c2"),
    ("sabres",         ROUND,  "#1c3a9e", "#f2c14e", ("sabres", None),     "#efe0c2"),
    ("senators",       ROUND,  "#111111", "#d42a22", ("helmet", None),     "#c8963c"),
    ("sharks",         ROUND,  "#14727a", "#111111", ("fin", None),        "#efe0c2"),
    ("stars",          SHIELD, "#2c6e4a", "#a7a3b0", ("star", None),       "#efe0c2"),
    ("wild",           ROUND,  "#2c6e4a", "#b8323c", ("pine", None),       "#efe0c2"),
]
CREST_COLUMNS = 16          # crests.png is a grid of 16x16 cells, CREST_COLUMNS to a row


def build_crest(shape, field, band, emblem, ecol):
    img = Image.new("RGBA", (16, 16), CLEAR)
    f, b, e = snap(field) + (255,), snap(band) + (255,), snap(ecol) + (255,)
    if b == f:            # keep the band visible when both colours snap to the same entry
        b = INK
    if e == f:
        e = CREAM if f != CREAM else INK
    inside = lambda x, y: 0 <= x < 16 and 0 <= y < 16 and shape[y][x] == "#"
    for y, row in enumerate(shape):
        for x, v in enumerate(row):
            if v != "#":
                continue
            edge = any(not inside(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            near = any(not inside(x + dx, y + dy)
                       for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2) if abs(dx) + abs(dy) <= 2)
            img.putpixel((x, y), INK if edge else (b if near else f))
    kind, arg = emblem
    top = 1 if shape is SHIELD else 0      # shields sit higher: their point takes the bottom rows
    if kind == "text":
        w = text_width(arg)
        draw_text(img, 8 - (w + 1) // 2 + (1 if w % 2 == 0 else 0), (5 if arg == "Ö" else 4) - top, arg, e)
    elif kind == "diagonal":
        # yellow, red and blue thirds split on the diagonal, inside the gold band
        red, blue = e, BLUE
        for y in range(16):
            for x in range(16):
                if img.getpixel((x, y)) == f:
                    d = x + y
                    img.putpixel((x, y), f if d < 13 else (red if d < 19 else blue))
    elif kind == "bars":
        for x in (4, 6, 9, 11):
            rect(img, x, 3, 1, 5, e)
        draw_text(img, 6, 9, arg, e)
    else:
        rows = EMBLEMS[kind]
        ew, eh = len(rows[0]), len(rows)
        bitmap(img, (16 - ew) // 2, (16 - eh) // 2 - top, rows, e)
    if kind == "pine":   # blue base under the tree
        for y in range(11, 16):
            for x in range(16):
                if img.getpixel((x, y)) == f:
                    img.putpixel((x, y), b)
    return img


def build_crests(meta, keep=None):
    """Lays every crest in CRESTS out on a grid, in list order. keep maps club ids to a 16x16
    crest to use as it is instead of drawing it (the hand-edited cells of crests.png)."""
    keep = keep or {}
    rows = (len(CRESTS) + CREST_COLUMNS - 1) // CREST_COLUMNS
    sheet = Image.new("RGBA", (16 * min(len(CRESTS), CREST_COLUMNS), 16 * rows), CLEAR)
    cells = {}
    for i, (cid, shape, field, band, emblem, ecol) in enumerate(CRESTS):
        x, y = i % CREST_COLUMNS * 16, i // CREST_COLUMNS * 16
        sheet.paste(keep[cid] if cid in keep else build_crest(shape, field, band, emblem, ecol), (x, y))
        cells[cid] = [x, y, 16, 16]
    sheet.save(os.path.join(UI, "crests.png"))
    meta["crests"] = {"file": "art/ui/crests.png", "cells": cells, "fallback": "generic",
                      "note": "Keys are club ids from data/clubs; unknown ids use the fallback."}


# ---------------------------------------------------------------- title logo

def build_title(meta):
    """The title logo, drawn by title_logo.py: gold italic block letters over a blue extrusion,
    a red ampersand, and a gold-rimmed puck sliding out after the K."""
    out = title_logo.build()
    path = os.path.join(UI, "title.png")
    # title.png goes through Aseprite (make_ui.lua), whose indexed palette may shift a colour; when the
    # drawing has not changed, keep that export and give the letters its colours
    shipped = Image.open(path).convert("RGBA") if os.path.exists(path) else None
    mapping = title_logo.colour_map(out, shipped) if shipped else None
    if mapping is None:
        out.save(path)
        shipped, mapping = out, {}
    meta["title"] = {"file": "art/ui/title.png", "size": list(out.size)}
    # the same logo cut into its letters for the title intro, and the mask its shine sweeps over
    sheet, pieces, shine = title_logo.build_letters()
    sheet = title_logo.recolour(sheet, pieces, mapping)
    if not title_logo.same_picture(title_logo.assemble(sheet, pieces, out.size), shipped):
        raise SystemExit("title_letters.png does not assemble into title.png")
    sheet.save(os.path.join(UI, "title_letters.png"))
    meta["title_letters"] = {
        "file": "art/ui/title_letters.png",
        "note": "The title intro's pieces in drop order: rect on the sheet, offset of its top left in title.png. "
                "shine: a white mask of title.png's gold, the same size.",
        "pieces": pieces,
        "shine": shine,
    }


def build_crests_only(redraw):
    """Rebuilds crests.png and its ui.json entry: a crest that already has a cell keeps its pixels
    (crests.png is exported from the hand-edited crests.aseprite), a club in CRESTS without one is
    drawn, and so is every club id named in redraw. Run make_ui.lua with only=crests afterwards."""
    path = os.path.join(UI, "ui.json")
    with open(path, encoding="utf-8") as f:
        meta = json.load(f)
    old = Image.open(os.path.join(UI, "crests.png")).convert("RGBA")
    keep = {cid: old.crop((x, y, x + w, y + h)) for cid, (x, y, w, h) in meta["crests"]["cells"].items()
            if cid not in redraw}
    build_crests(meta, keep)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")


def main():
    os.makedirs(UI, exist_ok=True)
    mode = sys.argv[1] if len(sys.argv) > 1 else "all"
    if mode == "fnt":
        write_fnt()
        return
    if mode == "font":             # only glyphs new to CHARS; keeps the existing (hand-edited) cells
        extend_font()
        write_fnt()
        return
    if mode == "title":            # only the logo; keeps every other (hand-edited) PNG as it is
        path = os.path.join(UI, "ui.json")
        with open(path, encoding="utf-8") as f:
            meta = json.load(f)
        build_title(meta)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(meta, f, indent=2, ensure_ascii=False)
            f.write("\n")
        return
    if mode == "crests":           # only the crest sheet; keeps every other (hand-edited) PNG as it is
        build_crests_only(sys.argv[2:])
        return
    meta = {"font": {"file": "art/ui/font.fnt", "image": "art/ui/font.png", "lineHeight": CELL_H + 1,
                     "capHeight": 7,
                     "note": "White ink: LoadFont(font.fnt), DrawTextEx at size 12, spacing 0, tinted with a palette colour. Text y is the top of the accent rows; capitals start 2 px lower."}}
    build_font()
    write_fnt()
    build_scorebug(meta)
    build_panels(meta)
    build_icons(meta)
    build_crests(meta)
    build_title(meta)
    with open(os.path.join(UI, "ui.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main()
