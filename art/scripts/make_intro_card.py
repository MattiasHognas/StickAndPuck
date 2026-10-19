"""Builds the pre-game stats card art (A22) into art/ui/, for the card W139 draws over the arena flyover:

  intro_card.png    300x192 versus panel: a dusk-blue header with a crest well for each club (the
                    game draws the 16x16 crest from crests.png at 2x into it), a name slot and a
                    club colour stripe well under each, and a gold-on-blue "VS" in the middle;
                    under it ten stat rows, each with a centre chip for its icon and a value area
                    left (home) and right (away). The five rating rows have bar wells that fill
                    from the centre outwards.
  intro_icons.png   the small icons: 8x8 stat icons (overall, attack, defence, speed, goalie,
                    record, place, form, top scorer, style), 8x8 form dots (win, loss, overtime loss,
                    none yet) and 12x12 playstyle badges (balanced, physical, shoot from
                    anywhere, defensive shell, forecheck aggressive and trap, entry carry and
                    dump and chase).
  intro_card.json   every rect the game needs: the card's slots (crests, names, stripes) and
                    rows (icon, value and bar slots per side), and the icon cells by name.

Every colour is on art/palette/stickandpuck_dusk.gpl. make_intro_card.lua turns the PNGs into
.aseprite sources (the source of truth after hand edits) and exports them back.

Run: python3 art/scripts/make_intro_card.py [sheet <out.png>]
"""
import json
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_ui  # noqa: E402
from make_ui import CLEAR, P, bitmap, rect  # noqa: E402

UI = make_ui.UI

INK, NIGHT, PLUM, PLUM2 = P("#18131f"), P("#211b28"), P("#2a2230"), P("#33293a")
NAVY, STONE_D, TEAL_D = P("#2a2c40"), P("#2a2c36"), P("#27383e")
STEEL_D, STEEL, BLUE, SKY = P("#31556e"), P("#3e5875"), P("#2f5f8f"), P("#7f9cae")
GREY, GREY_L, SILVER = P("#74666b"), P("#8a8790"), P("#a7a3b0")
BRICK_D, BRICK, CORAL, CORAL_L = P("#613b3a"), P("#a34e4d"), P("#c8574a"), P("#e96f5d")
GOLD_D, GOLD, GOLD_L, CREAM = P("#c8963c"), P("#f2c14e"), P("#f9da93"), P("#efe0c2")
GREEN_D, GREEN, GREEN_L = P("#1e4a3a"), P("#2c6e4a"), P("#56965c")

HEADER = 64             # header band height
ROW = 12                # stat row height
STYLE_ROW = 16          # the playstyle row, tall enough for the 12x12 badges
CHIP_W = 20             # centre icon chip
CREST_WELL = 36         # 32x32 crest (2x) plus a 2 px frame

STATS = ["overall", "attack", "defence", "speed", "goalie"]          # rows with bars
TEXT_ROWS = ["record", "place", "form", "scorer"]                    # rows with text or dots
ROWS = STATS + TEXT_ROWS + ["style"]

W = 300
H = HEADER + 4 + ROW * (len(ROWS) - 1) + STYLE_ROW + 4
POSITION = ((480 - W) // 2, (270 - H) // 2)


def frame(img, x, y, w, h, col):
    make_ui.frame(img, x, y, w, h, col)


def rounded(img, x, y, w, h, edge, fill):
    """A box with an edge colour and its corners cut by one pixel."""
    rect(img, x + 1, y, w - 2, h, edge)
    rect(img, x, y + 1, w, h - 2, edge)
    rect(img, x + 1, y + 1, w - 2, h - 2, fill)


# ---------------------------------------------------------------- VS, in the title logo's style

VS_GLYPHS = {
    "V": ["###...###", "###...###", "###...###", "###...###", "###...###",
          ".###.###.", ".###.###.", "..#####..", "..#####..", "...###..."],
    "S": [".#######.", "#########", "###...###", "###......", "########.",
          ".########", "......###", "###...###", "#########", ".#######."],
}
GOLD_FACE = [GOLD_L, GOLD_L, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD_D, GOLD_D, GOLD_D]
BLUE_SIDE = [BLUE, BLUE, STEEL_D, NAVY, NAVY]


def vs_mask():
    """'VS' doubled and slanted one pixel per three rows, like the title logo's letters."""
    gap = 2
    rows = 10
    width = sum(len(VS_GLYPHS[c][0]) for c in "VS") + gap
    m = [[0] * width for _ in range(rows)]
    x = 0
    for c in "VS":
        for y, row in enumerate(VS_GLYPHS[c]):
            for i, ch in enumerate(row):
                if ch == "#":
                    m[y][x + i] = 1
        x += len(VS_GLYPHS[c][0]) + gap
    m = [[v for v in row for _ in range(2)] for row in m for _ in range(2)]
    h, w = len(m), len(m[0])
    out = [[0] * (w + (h - 1) // 3) for _ in range(h)]
    for y in range(h):
        dx = (h - 1 - y) // 3
        for x in range(w):
            if m[y][x]:
                out[y][x + dx] = 1
    return out


def build_vs():
    """Gold faces with a light top edge, an ink ring and a deep blue extrusion down-right."""
    mask = vs_mask()
    h, w = len(mask), len(mask[0])
    depth = 4
    img = Image.new("RGBA", (w + depth + 2, h + depth + 2), CLEAR)

    def put(x, y, c):
        if 0 <= x < img.width and 0 <= y < img.height:
            img.putpixel((x, y), c)

    ox, oy = 1, 1
    for d in range(depth, 0, -1):
        c = BLUE_SIDE[min(d - 1, len(BLUE_SIDE) - 1)]
        for y in range(h):
            for x in range(w):
                if mask[y][x]:
                    put(ox + x + d, oy + y + d, c)
    for y in range(h):
        for x in range(w):
            if mask[y][x]:
                c = GOLD_L if y == 0 or not mask[y - 1][x] else GOLD_FACE[min(y * len(GOLD_FACE) // h, 9)]
                put(ox + x, oy + y, c)
    # ink round everything
    src = img.copy()
    for y in range(img.height):
        for x in range(img.width):
            if src.getpixel((x, y))[3]:
                continue
            if any(0 <= x + dx < img.width and 0 <= y + dy < img.height and src.getpixel((x + dx, y + dy))[3]
                   for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                img.putpixel((x, y), INK)
    return img


# ---------------------------------------------------------------- the card

def build_card():
    img = Image.new("RGBA", (W, H), CLEAR)
    slots, rows = {}, []
    # body: plum, ink edge with cut corners, a light inner top-left like the HUD panels
    rounded(img, 0, 0, W, H, INK, PLUM)
    # gold trim one pixel in, then ink, then the fill
    frame(img, 1, 1, W - 2, H - 2, GOLD_D)
    rect(img, 2, 2, W - 4, 1, GOLD)
    frame(img, 2, 3, W - 4, H - 5, INK)

    # header band: navy with a steel light line, and a gold rule under it
    rect(img, 3, 4, W - 6, HEADER - 4, NAVY)
    rect(img, 3, 4, W - 6, 1, STEEL)
    for y in range(5, HEADER, 2):                    # faint scanline stripes for a broadcast feel
        rect(img, 3, y, W - 6, 1, STONE_D)
    rect(img, 3, HEADER, W - 6, 1, INK)
    rect(img, 3, HEADER + 1, W - 6, 1, GOLD_D)
    rect(img, 3, HEADER + 2, W - 6, 1, INK)

    half = (W - 6) // 2
    for side, cx in (("home", 3 + half // 2), ("away", 3 + half + half // 2)):
        wx, wy = cx - CREST_WELL // 2, 6
        rounded(img, wx - 1, wy - 1, CREST_WELL + 2, CREST_WELL + 2, GOLD_D, INK)
        rect(img, wx, wy, CREST_WELL, CREST_WELL, NIGHT)
        frame(img, wx, wy, CREST_WELL, CREST_WELL, PLUM2)
        slots[f"{side}_crest"] = [wx + 2, wy + 2, 32, 32]
        # name plate under the crest
        nw = half - 16
        nx = cx - nw // 2
        ny = wy + CREST_WELL + 3
        rect(img, nx, ny, nw, 11, INK)
        rect(img, nx + 1, ny + 1, nw - 2, 9, NIGHT)
        slots[f"{side}_name"] = [nx + 2, ny + 2, nw - 4, 7]
        # club colour stripe well under the plate
        sw = 40
        rect(img, cx - sw // 2 - 1, ny + 12, sw + 2, 4, INK)
        rect(img, cx - sw // 2, ny + 13, sw, 2, PLUM2)
        slots[f"{side}_stripe"] = [cx - sw // 2, ny + 13, sw, 2]

    vs = build_vs()
    vx, vy = (W - vs.width) // 2 + 1, 14
    img.alpha_composite(vs, (vx, vy))
    slots["vs"] = [vx, vy, vs.width, vs.height]

    # stat rows
    y = HEADER + 4
    mid = W // 2
    for i, name in enumerate(ROWS):
        h = STYLE_ROW if name == "style" else ROW
        band = PLUM if i % 2 == 0 else PLUM2
        rect(img, 4, y, W - 8, h, band)
        cx0 = mid - CHIP_W // 2
        rounded(img, cx0, y + 1, CHIP_W, h - 2, INK, NAVY)
        rect(img, cx0 + 1, y + 2, CHIP_W - 2, 1, STEEL)
        icon = [mid - 4, y + (h - 8) // 2, 8, 8]
        row = {"name": name, "y": y, "height": h, "icon": icon}
        if name in STATS:
            bw = 96
            for side, bx in (("home", cx0 - 4 - bw), ("away", cx0 + CHIP_W + 4)):
                rect(img, bx - 1, y + 3, bw + 2, 6, INK)
                rect(img, bx, y + 4, bw, 4, NIGHT)
                rect(img, bx, y + 7, bw, 1, STONE_D)
                for t in range(1, 4):                # quarter ticks
                    tx = bx + t * bw // 4
                    img.putpixel((tx, y + 8), PLUM2)
                row[f"{side}_bar"] = [bx, y + 4, bw, 4]
            row["home_value"] = [8, y + 3, cx0 - 4 - bw - 1 - 2 - 8, 7]
            row["away_value"] = [cx0 + CHIP_W + 4 + bw + 3, y + 3, W - 8 - (cx0 + CHIP_W + 4 + bw + 3), 7]
            row["barFill"] = "home fills right to left from the chip, away left to right; 0..100 maps to the well width"
        elif name == "form":
            for side in ("home", "away"):
                dots = []
                for k in range(5):
                    # oldest game furthest from the chip, newest next to it
                    dx = cx0 - 6 - 9 * (4 - k) - 8 if side == "home" else cx0 + CHIP_W + 6 + 9 * k
                    dots.append([dx, y + 2, 8, 8])
                row[f"{side}_dots"] = dots
        elif name == "style":
            for side in ("home", "away"):
                badges = []
                for k in range(3):
                    bx = cx0 - 6 - 14 * (k + 1) + 2 if side == "home" else cx0 + CHIP_W + 6 + 14 * k
                    badges.append([bx, y + 2, 12, 12])
                row[f"{side}_badges"] = badges
                tx = (8, y + 5, badges[-1][0] - 4 - 8, 7) if side == "home" else \
                    (badges[-1][0] + 16, y + 5, W - 8 - badges[-1][0] - 16, 7)
                row[f"{side}_value"] = list(tx)
        if name not in STATS and name != "style":
            row["home_value"] = [8, y + 3, cx0 - 6 - 8, 7]
            row["away_value"] = [cx0 + CHIP_W + 6, y + 3, W - 8 - (cx0 + CHIP_W + 6), 7]
        rows.append(row)
        y += h
    return img, slots, rows


# ---------------------------------------------------------------- icons

STAT_ICONS = {
    # name: (rows, {char: colour})
    "overall": ([
        "...#....",
        "..###...",
        "#######.",
        ".#####..",
        "..###...",
        ".##.##..",
        ".#...#..",
        "........"], {"#": GOLD}),
    "attack": ([
        "......#.",
        ".....#..",
        "....#...",
        "...#....",
        "..#.....",
        "##..hh..",
        "##.hhhh.",
        "....hh.."], {"#": GOLD_L, "h": CORAL_L}),
    "defence": ([
        "########",
        "#cccccc#",
        "#cc##cc#",
        "#c####c#",
        ".#c##c#.",
        ".#cccc#.",
        "..#cc#..",
        "...##..."], {"#": INK, "c": SKY}),
    "speed": ([
        "....##..",
        "...##...",
        "..####..",
        ".####...",
        "...##...",
        "..##....",
        ".##.....",
        ".#......"], {"#": GOLD}),
    "goalie": ([
        ".######.",
        "#cccccc#",
        "#c#c#c##",
        "#cccccc#",
        "#c#c#c##",
        "#cccccc#",
        ".#cccc#.",
        "..####.."], {"#": INK, "c": CREAM}),
    "record": ([
        ".######.",
        "#cccccc#",
        "#c#c#cc#",
        "#cccccc#",
        "#c##c#c#",
        "#cccccc#",
        "#c#c#cc#",
        ".######."], {"#": INK, "c": SILVER}),
    "place": ([
        "#.####.#",
        "#.####.#",
        ".######.",
        "..####..",
        "...##...",
        "...##...",
        "..####..",
        ".######."], {"#": GOLD}),
    "form": ([
        "......##",
        ".....#.#",
        "....#...",
        "#..#....",
        ".##.....",
        "........",
        "g.c.g.g.",
        "........"], {"#": GOLD_L, "g": GREEN_L, "c": CORAL_L}),
    "scorer": ([
        "..#..#..",
        "########",
        "##cccc##",
        "#.c##c.#",
        "..c##c..",
        "..cccc..",
        "..cccc..",
        "........"], {"#": CORAL, "c": CREAM}),
    "style": ([
        "########",
        "#gggggg#",
        "#xgxgog#",
        "#gxgogo#",
        "#xgxgog#",
        "#gggggg#",
        "########",
        "..#..#.."], {"#": GOLD_D, "g": GREEN_D, "x": CREAM, "o": GOLD_L}),
}

FORM_DOTS = {
    # name: (rim, fill, light)
    "win": (INK, GREEN, GREEN_L),
    "loss": (INK, BRICK, CORAL_L),
    "otloss": (INK, GOLD_D, GOLD_L),
    "none": (INK, NIGHT, PLUM2),
}
FORM_DOT = [
    ".####.",
    "######",
    "######",
    "######",
    "######",
    ".####.",
]

BADGE = [
    "....####....",
    "..########..",
    ".##########.",
    ".##########.",
    "############",
    "############",
    "############",
    "############",
    ".##########.",
    ".##########.",
    "..########..",
    "....####....",
]

STYLE_BADGES = {
    # name: (badge colour, symbol rows (8x8, centred), symbol colour)
    "balanced": (STEEL, [
        "...##...",
        "########",
        "#..##..#",
        "#..##..#",
        "##.##.##",
        "...##...",
        "..####..",
        "........"], CREAM),
    "physical": (BRICK, [
        "#..#...#",
        ".#.#..#.",
        "..####..",
        "####.###",
        "..####..",
        ".#.#.#..",
        "#..#..#.",
        "........"], GOLD_L),
    "shootFromAnywhere": (GOLD_D, [
        "...##...",
        "..#..#..",
        ".#.##.#.",
        "###..###",
        "###..###",
        ".#.##.#.",
        "..#..#..",
        "...##..."], INK),
    "defensiveShell": (STEEL_D, [
        "########",
        "#......#",
        "#.####.#",
        "#.####.#",
        ".#.##.#.",
        ".#....#.",
        "..#..#..",
        "...##..."], CREAM),
    "forecheck_aggressive": (CORAL, [
        "........",
        "#...#...",
        ".#...#..",
        "..#...#.",
        "..#...#.",
        ".#...#..",
        "#...#...",
        "........"], CREAM),
    "forecheck_trap": (GREEN, [
        "#.#.#.#.",
        "#.#.#.#.",
        "#.#.#.#.",
        "#.#.#.#.",
        "#.#.#.#.",
        "#.#.#.#.",
        "#.#.#.#.",
        "........"], CREAM),
    "entry_carry": (BLUE, [
        "........",
        ".....#..",
        ".....##.",
        "#######.",
        "#######.",
        ".....##.",
        ".....#..",
        "........"], CREAM),
    "entry_dumpAndChase": (GREY, [
        "..####..",
        ".#....#.",
        "#......#",
        "#......#",
        "......###",
        ".......#.",
        "........",
        "##......"], CREAM),
}
# Aliases the game can look up straight from the tactics enums.
STYLE_ALIASES = {"forecheck_balanced": "balanced"}


def build_icons():
    sw = max(8 * len(STAT_ICONS), 8 * len(FORM_DOTS), 12 * len(STYLE_BADGES))
    img = Image.new("RGBA", (sw, 8 + 8 + 12), CLEAR)
    stat, form, style = {}, {}, {}
    for i, (name, (rows, cols)) in enumerate(STAT_ICONS.items()):
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in cols:
                    img.putpixel((i * 8 + x, y), cols[ch])
        stat[name] = [i * 8, 0, 8, 8]
    for i, (name, (rim, fill, light)) in enumerate(FORM_DOTS.items()):
        ox, oy = i * 8 + 1, 8 + 1
        bitmap(img, ox, oy, FORM_DOT, rim)
        rect(img, ox + 1, oy + 1, 4, 4, fill)
        rect(img, ox + 1, oy + 1, 2, 1, light)
        img.putpixel((ox + 1, oy + 2), light)
        form[name] = [i * 8, 8, 8, 8]
    for i, (name, (col, rows, sym)) in enumerate(STYLE_BADGES.items()):
        ox, oy = i * 12, 16
        for y, row in enumerate(BADGE):
            for x, ch in enumerate(row):
                if ch != "#":
                    continue
                edge = any(not (0 <= x + dx < 12 and 0 <= y + dy < 12 and BADGE[y + dy][x + dx] == "#")
                           for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                img.putpixel((ox + x, oy + y), INK if edge else col)
        for y, row in enumerate(rows):
            for x, ch in enumerate(row[:8]):
                if ch == "#":
                    img.putpixel((ox + 2 + x, oy + 2 + y), sym)
        style[name] = [ox, oy, 12, 12]
    for alias, target in STYLE_ALIASES.items():
        style[alias] = style[target]
    return img, stat, form, style


# ---------------------------------------------------------------- sheet (for review, not shipped)

def sheet(out, card, icons, meta):
    """The card with every slot outlined, beside the icons at 4x, on a dark ground."""
    s = 3
    pad = 12
    big = card.resize((card.width * s, card.height * s), Image.NEAREST)
    ic = icons.resize((icons.width * 4, icons.height * 4), Image.NEAREST)
    w = big.width + pad * 3 + ic.width
    h = max(big.height, ic.height) + pad * 2
    img = Image.new("RGBA", (w, h), NIGHT)
    img.alpha_composite(big, (pad, pad))
    img.alpha_composite(ic, (big.width + pad * 2, pad))
    d = ImageDraw.Draw(img)

    def box(r, col):
        x, y, rw, rh = r
        d.rectangle([pad + x * s, pad + y * s, pad + (x + rw) * s - 1, pad + (y + rh) * s - 1], outline=col)

    for k, r in meta["card"]["slots"].items():
        if k != "vs":
            box(r, (127, 156, 174, 255))
    for row in meta["card"]["rows"]:
        for k, v in row.items():
            if k.endswith("_value"):
                box(v, (86, 150, 92, 255))
    img.save(out)


def main():
    card, slots, rows = build_card()
    icons, stat, form, style = build_icons()
    meta = {
        "card": {
            "file": "art/ui/intro_card.png",
            "size": [W, H],
            "position": list(POSITION),
            "slots": slots,
            "rows": rows,
            "note": ("Card-local rects. Crests: crests.png cell drawn at 2x into *_crest. Text slots are the 7 px "
                     "capital box (draw at slot y - 2): home values right-aligned towards the chip, away values "
                     "left-aligned. Bars: 0..100 of the well width, home growing left from the chip, away right. "
                     "Form dots: oldest first, the home row's newest next to the chip. Style badges: the style, "
                     "forecheck and entry badge per side, the home row's first next to the chip."),
        },
        "icons": {"file": "art/ui/intro_icons.png", "stat": stat, "form": form, "style": style,
                  "note": "Stat and form cells are 8x8, style badges 12x12; style keys are the PlayStyle names in camelCase, "
                          "forecheck_<value> and entry_<value>."},
        "colours": {"text": "#efe0c2", "value": "#f2c14e", "muted": "#8a8790", "barHome": "#f2c14e", "barAway": "#7f9cae"},
    }
    if len(sys.argv) > 2 and sys.argv[1] == "sheet":
        sheet(sys.argv[2], card, icons, meta)
        return
    card.save(os.path.join(UI, "intro_card.png"))
    icons.save(os.path.join(UI, "intro_icons.png"))
    with open(os.path.join(UI, "intro_card.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
