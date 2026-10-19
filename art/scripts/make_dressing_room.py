"""Builds the dressing room art for the screen between season games (A20) into art/ui/:

  dressing_room.png         480x270 backdrop: wooden stalls with hanging jerseys, helmets and
                            gloves on the shelves, a bench, sticks in a rack, a tactics board and
                            warm ceiling lights, all in the dusk palette.
  dressing_room_jersey.png  480x270 white ink mask of the jerseys' body; the game tints it with
                            the user club's primary kit colour and draws it over the backdrop.
  dressing_room_trim.png    480x270 white ink mask of the jerseys' stripes and numbers, tinted
                            with the club's secondary colour.
  coach_portrait.png        64x80 coach, neutral (the portrait the screen shows by default).
  coach_moods.png           192x80 the coach as neutral, pleased and angry, 64x80 cells left to right.

Every opaque colour in the backdrop and portraits is on art/palette/stickandpuck_dusk.gpl; the
masks are white and greys (shade), like the font. make_dressing_room.lua turns the PNGs into
.aseprite sources (the source of truth after hand edits) and exports them back.

Run: python3 art/scripts/make_dressing_room.py [sheet <out.png>]
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_ui  # noqa: E402
from make_ui import P, CLEAR, rect  # noqa: E402

UI = make_ui.UI
W, H = 480, 270

INK, NIGHT, PLUM, PLUM2 = P("#18131f"), P("#211b28"), P("#2a2230"), P("#33293a")
SLATE, GREY, GREY_L = P("#423236"), P("#74666b"), P("#8a8790")
NAVY, TEAL_D = P("#2a2c40"), P("#27383e")
STONE_D, STONE = P("#2a2c36"), P("#555f6b")
SILVER, SILVER_L, PEARL, BOARD = P("#a7a3b0"), P("#bdb9c4"), P("#ccc9d2"), P("#e3d8c6")
STEEL, BLUE, SKY = P("#3e5875"), P("#2f5f8f"), P("#7f9cae")
WOOD_D, WOOD, WOOD_M, WOOD_L = P("#613b3a"), P("#9e655c"), P("#c47262"), P("#d2ad7f")
BRICK, CORAL = P("#a34e4d"), P("#c8574a")
GOLD_D, GOLD, GOLD_L, CREAM = P("#c8963c"), P("#f2c14e"), P("#f9da93"), P("#efe0c2")
SKIN, SKIN_D, PEACH = P("#f0b48a"), P("#c47262"), P("#fba771")
GREEN_D = P("#1e4a3a")
WHITE = (255, 255, 255, 255)
SHADE = (190, 190, 190, 255)     # mask shade: folds and the side away from the light
DEEP = (140, 140, 140, 255)

STALL_W, STALL_TOP, STALL_BOT = 40, 46, 184
LIGHTS = (60, 180, 300, 420)     # x of the ceiling lights


def dither(img, x, y, w, h, a, b, level):
    """Fills a box with a over b in an ordered (Bayer 2x2) pattern; level 0..4 is how much is a."""
    order = ((0, 2), (3, 1))
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            if 0 <= xx < img.width and 0 <= yy < img.height:
                img.putpixel((xx, yy), a if order[yy % 2][xx % 2] < level else b)


def vgrad(img, x, y, w, h, bands):
    """Vertical bands of (colour, colour, level) from top to bottom, evenly split."""
    n = len(bands)
    for i, (a, b, lvl) in enumerate(bands):
        y0, y1 = y + i * h // n, y + (i + 1) * h // n
        dither(img, x, y0, w, y1 - y0, a, b, lvl)


def light_at(x):
    """0..1: how close x is to a ceiling light (warm pools on the wall)."""
    d = min(abs(x - lx) for lx in LIGHTS)
    return max(0.0, 1.0 - d / 70.0)


# ---------------------------------------------------------------- backdrop

def jersey_shape(cx, top):
    """Pixel rows of a jersey on a hanger: a T with sleeves, 26 wide, 30 tall."""
    rows = []
    for y in range(30):
        if y < 3:
            half = 5 + y            # shoulders off the hanger
        elif y < 12:
            half = 13               # sleeves
        else:
            half = 9                # body
        rows.append((cx - half, cx + half - 1))
    return [(top + y, x0, x1) for y, (x0, x1) in enumerate(rows)]


DIGITS = {
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["###", "..#", "###", "#..", "###"], "3": ["###", "..#", ".##", "..#", "###"],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "5": ["###", "#..", "###", "..#", "###"],
    "6": ["###", "#..", "###", "#.#", "###"], "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"], "9": ["###", "#.#", "###", "..#", "###"],
}
NUMBERS = ["1", "4", "7", "9", "10", "13", "17", "19", "21", "24", "28", "33"]


def build_backdrop():
    img = Image.new("RGBA", (W, H), NIGHT)
    jersey = Image.new("RGBA", (W, H), CLEAR)
    trim = Image.new("RGBA", (W, H), CLEAR)
    d = ImageDraw.Draw(img)

    # ceiling: dark panels with a beam, then the light strips with a warm glow under them
    vgrad(img, 0, 0, W, 34, [(PLUM, INK, 1), (PLUM, NIGHT, 2), (PLUM2, PLUM, 1)])
    for x in range(0, W, 60):
        rect(img, x, 0, 1, 30, INK)
    rect(img, 0, 30, W, 2, INK)
    rect(img, 0, 32, W, 2, SLATE)

    # back wall above the stalls: plaster with light pools, a club banner strip
    for x in range(W):
        lvl = int(light_at(x) * 4.5)
        for y in range(34, STALL_TOP):
            b = PLUM2 if y > 38 else SLATE
            img.putpixel((x, y), GREY if (lvl >= 3 and (x + y) % 2 == 0 and y < 40) else (SLATE if (x + y) % 4 < lvl else b))
    rect(img, 0, STALL_TOP - 3, W, 3, WOOD_D)
    rect(img, 0, STALL_TOP - 3, W, 1, WOOD_M)

    for lx in LIGHTS:               # fixtures and glow
        rect(img, lx - 22, 26, 44, 4, INK)
        rect(img, lx - 21, 27, 42, 2, GOLD_L)
        rect(img, lx - 17, 27, 34, 1, CREAM)
        dither(img, lx - 26, 30, 52, 2, GOLD_D, SLATE, 1)

    # stalls: open wooden cubbies, a shelf with a helmet, a jersey on a hanger, skates below
    stalls = W // STALL_W
    for i in range(stalls):
        x0 = i * STALL_W
        glow = light_at(x0 + STALL_W // 2)
        back_hi, back_lo = (WOOD, WOOD_D) if glow > 0.45 else (WOOD_D, SLATE)
        # back panel with vertical boards
        vgrad(img, x0, STALL_TOP, STALL_W, STALL_BOT - STALL_TOP,
              [(back_hi, back_lo, 3), (back_hi, back_lo, 2), (back_hi, back_lo, 1), (back_lo, PLUM2, 2)])
        for bx in range(x0 + 9, x0 + STALL_W - 2, 10):
            rect(img, bx, STALL_TOP + 14, 1, STALL_BOT - STALL_TOP - 14, back_lo if back_lo != SLATE else PLUM2)
        # posts between stalls
        rect(img, x0, STALL_TOP, 3, STALL_BOT - STALL_TOP, WOOD_M)
        rect(img, x0, STALL_TOP, 1, STALL_BOT - STALL_TOP, WOOD_L)
        rect(img, x0 + 3, STALL_TOP, 1, STALL_BOT - STALL_TOP, INK)
        # top shelf with a helmet and gloves
        sy = STALL_TOP + 12
        rect(img, x0 + 3, sy, STALL_W - 3, 3, WOOD_M)
        rect(img, x0 + 3, sy, STALL_W - 3, 1, WOOD_L)
        rect(img, x0 + 3, sy + 3, STALL_W - 3, 1, INK)
        hx = x0 + 10
        d.ellipse([hx, sy - 9, hx + 13, sy + 2], fill=INK)
        d.ellipse([hx + 1, sy - 8, hx + 12, sy + 1], fill=NAVY)
        rect(img, hx + 3, sy - 7, 4, 1, STEEL)
        rect(img, hx + 1, sy - 1, 12, 1, INK)
        rect(img, x0 + 26, sy - 5, 8, 5, INK)                  # gloves
        rect(img, x0 + 27, sy - 4, 6, 3, BRICK if i % 2 else STEEL)
        rect(img, x0 + 27, sy - 4, 6, 1, CORAL if i % 2 else SKY)
        # name plate
        rect(img, x0 + 10, STALL_TOP + 2, 20, 6, INK)
        rect(img, x0 + 11, STALL_TOP + 3, 18, 4, GOLD_D)
        rect(img, x0 + 12, STALL_TOP + 4, 4 + (i * 5) % 10, 1, WOOD_D)
        # hanger rail and hanger
        cx, jt = x0 + 22, STALL_TOP + 26
        rect(img, x0 + 4, jt - 4, STALL_W - 4, 1, SILVER)
        rect(img, cx - 1, jt - 3, 2, 2, SILVER_L)
        # jersey: base art in greys (what shows untinted), outline in ink, masks for the tint
        shape = jersey_shape(cx, jt)
        for y, a, b in shape:
            rect(img, a - 1, y, b - a + 3, 1, INK)
        rect(img, cx - 10, jt + 30, 20, 1, INK)
        for y, a, b in shape:
            for x in range(a, b + 1):
                edge_right = x > b - 3
                sleeve = (y - jt) < 12 and abs(x - cx) > 8
                fold = x == cx - 5 or x == cx + 4
                shade = edge_right or (sleeve and (y - jt) > 9) or fold
                img.putpixel((x, y), GREY if shade else GREY_L)
                jersey.putpixel((x, y), SHADE if shade else WHITE)
        # stripes: sleeve cuffs, hem bands, and the number
        for y, a, b in shape:
            r = y - jt
            if r in (9, 10):
                for x in list(range(a, cx - 9)) + list(range(cx + 9, b + 1)):
                    trim.putpixel((x, y), WHITE)
            if r in (24, 25):
                for x in range(a, b + 1):
                    trim.putpixel((x, y), SHADE if x > b - 3 else WHITE)
        num = NUMBERS[i % len(NUMBERS)]
        nx = cx - (len(num) * 4 - 1) // 2
        for k, ch in enumerate(num):
            for ry, row in enumerate(DIGITS[ch]):
                for rx, v in enumerate(row):
                    if v == "#":
                        trim.putpixel((nx + k * 4 + rx, jt + 14 + ry), WHITE)
        # V collar
        for k in range(3):
            img.putpixel((cx - 2 + k, jt + k), INK)
            img.putpixel((cx + 1 - k, jt + k), INK)
            jersey.putpixel((cx - 2 + k, jt + k), CLEAR)
            jersey.putpixel((cx + 1 - k, jt + k), CLEAR)
        # pants and socks on the hook under the jersey
        rect(img, cx - 8, jt + 33, 16, 10, INK)
        rect(img, cx - 7, jt + 34, 14, 8, NAVY)
        rect(img, cx - 7, jt + 34, 14, 1, STEEL)
        rect(img, cx, jt + 36, 1, 6, INK)
        # skates on the floor of the stall
        sk = STALL_BOT - 8
        for k, kx in enumerate((x0 + 8, x0 + 22)):
            rect(img, kx, sk, 11, 6, INK)
            rect(img, kx + 1, sk + 1, 7, 3, NIGHT if k else NAVY)
            rect(img, kx, sk + 6, 12, 1, SILVER_L)

    # tactics board on the wall in the middle (covers two stalls' upper half)
    bx, by, bw, bh = 200, 50, 80, 62
    rect(img, bx - 2, by - 2, bw + 4, bh + 4, INK)
    rect(img, bx - 1, by - 1, bw + 2, bh + 2, SILVER)
    rect(img, bx, by, bw, bh, BOARD)
    dither(img, bx, by + bh - 12, bw, 12, PEARL, BOARD, 2)
    rink = (bx + 8, by + 6, bw - 16, bh - 14)
    rx, ry, rw, rh = rink
    d.rounded_rectangle([rx, ry, rx + rw, ry + rh], radius=8, outline=GREY_L)
    rect(img, rx + rw // 2, ry + 1, 1, rh - 1, CORAL)
    rect(img, rx + rw // 3, ry + 1, 1, rh - 1, BLUE)
    rect(img, rx + 2 * rw // 3, ry + 1, 1, rh - 1, BLUE)
    d.ellipse([rx + rw // 2 - 6, ry + rh // 2 - 6, rx + rw // 2 + 6, ry + rh // 2 + 6], outline=BLUE)
    for px, py in ((rx + 10, ry + 12), (rx + 14, ry + 32), (rx + 26, ry + 20)):   # X's
        for k in range(-2, 3):
            img.putpixel((px + k, py + k), CORAL)
            img.putpixel((px + k, py - k), CORAL)
    for px, py in ((rx + 42, ry + 12), (rx + 48, ry + 34), (rx + 56, ry + 22)):   # O's
        d.ellipse([px - 2, py - 2, px + 2, py + 2], outline=NAVY)
    d.line([rx + 14, ry + 32, rx + 22, ry + 28, rx + 32, ry + 32], fill=INK)        # play arrows
    d.line([rx + 26, ry + 20, rx + 36, ry + 14], fill=INK)
    img.putpixel((rx + 35, ry + 13), INK)
    img.putpixel((rx + 36, ry + 16), INK)
    rect(img, bx + 6, by + bh, 30, 2, SILVER)                 # marker tray
    rect(img, bx + 10, by + bh - 1, 6, 1, CORAL)
    rect(img, bx + 20, by + bh - 1, 6, 1, BLUE)
    # clear the jerseys and trim behind the board
    for m in (jersey, trim):
        for y in range(by - 2, by + bh + 3):
            for x in range(bx - 2, bx + bw + 3):
                m.putpixel((x, y), CLEAR)

    # floor: rubber mat with a lighter centre where the lights fall
    fy = STALL_BOT
    rect(img, 0, fy, W, 2, INK)
    vgrad(img, 0, fy + 2, W, H - fy - 2, [(TEAL_D, STONE_D, 2), (STONE_D, NAVY, 2), (NAVY, PLUM, 2), (PLUM, NIGHT, 2)])
    for x in range(0, W, 24):
        d.line([x, fy + 2, x - 40 + (x - W // 2) // 6, H], fill=STONE_D)
    # centre logo on the floor: a gold ring with a puck
    d.ellipse([W // 2 - 60, 226, W // 2 + 60, 262], outline=GOLD_D)
    d.ellipse([W // 2 - 56, 229, W // 2 + 56, 259], outline=WOOD_D)
    d.ellipse([W // 2 - 14, 238, W // 2 + 14, 250], fill=INK)
    d.ellipse([W // 2 - 13, 238, W // 2 + 13, 247], fill=PLUM2)

    # bench along the stalls: seat, front board and legs
    by0 = fy + 6
    rect(img, 0, by0, W, 6, WOOD_D)
    rect(img, 0, by0, W, 2, WOOD_L)
    rect(img, 0, by0 + 2, W, 1, WOOD_M)
    rect(img, 0, by0 + 6, W, 1, INK)
    rect(img, 0, by0 + 7, W, 6, SLATE)
    rect(img, 0, by0 + 13, W, 1, INK)
    for x in range(14, W, 52):
        rect(img, x, by0 + 14, 3, 6, INK)
    # things on the bench: a towel, water bottles and a bucket of pucks
    for tx in (36, 156, 332, 444):
        rect(img, tx, by0 - 3, 18, 4, INK)
        rect(img, tx + 1, by0 - 2, 16, 2, CREAM)
        rect(img, tx + 1, by0, 16, 1, PEARL)
    for wx in (110, 258, 392):
        for k in range(2):
            rect(img, wx + k * 6, by0 - 9, 5, 9, INK)
            rect(img, wx + k * 6 + 1, by0 - 8, 3, 7, SKY if k else GREEN_D)
            rect(img, wx + k * 6 + 2, by0 - 11, 1, 2, INK)
    bkx = 214
    rect(img, bkx, by0 + 16, 22, 14, INK)
    rect(img, bkx + 1, by0 + 17, 20, 12, BLUE)
    rect(img, bkx + 1, by0 + 17, 20, 2, SKY)
    for k in range(5):
        rect(img, bkx + 2 + k * 4, by0 + 14 - (k % 2), 4, 3, INK)

    # sticks leaning in the corners
    for sx in (6, 466):
        for k in range(4):
            x = sx + k * 3
            d.line([x, 70 + k * 3, x + (4 if sx < W // 2 else -4), STALL_BOT + 4], fill=INK, width=2)
            d.line([x, 70 + k * 3, x + (4 if sx < W // 2 else -4), STALL_BOT + 3], fill=WOOD_L if k % 2 else CREAM)
            rect(img, x + (4 if sx < W // 2 else -9), STALL_BOT + 2, 6, 2, INK)

    # a soft vignette into the corners so the edges sit back behind the panel
    for y in range(H):
        for x in range(W):
            e = min(x, W - 1 - x, y, H - 1 - y)
            if e < 6 and (x + y) % (e + 2) == 0:
                img.putpixel((x, y), INK)

    img.save(os.path.join(UI, "dressing_room.png"))
    jersey.save(os.path.join(UI, "dressing_room_jersey.png"))
    trim.save(os.path.join(UI, "dressing_room_trim.png"))
    return img, jersey, trim


# ---------------------------------------------------------------- coach

def coach(mood):
    """The coach from the chest up in a 64x80 frame: grey hair, a navy suit and a coral tie."""
    img = Image.new("RGBA", (64, 80), INK)
    d = ImageDraw.Draw(img)
    vgrad(img, 1, 1, 62, 78, [(STEEL, NAVY, 1), (NAVY, PLUM2, 2), (PLUM2, PLUM, 2), (PLUM, NIGHT, 2)])
    rect(img, 1, 1, 62, 1, GOLD_D)
    # a glimpse of the tactics board behind the left shoulder
    rect(img, 3, 10, 14, 22, GREY_L)
    rect(img, 4, 11, 12, 20, BOARD)
    rect(img, 9, 12, 1, 18, CORAL)

    # shoulders and suit
    d.polygon([(6, 79), (10, 60), (22, 54), (42, 54), (54, 60), (58, 79)], fill=INK)
    d.polygon([(7, 79), (11, 61), (23, 55), (41, 55), (53, 61), (57, 79)], fill=NAVY)
    d.polygon([(40, 56), (52, 62), (56, 79), (46, 79)], fill=STONE_D)       # far side in shade
    d.line([(15, 64), (12, 79)], fill=STEEL)                                 # lit edge
    # shirt and tie
    d.polygon([(25, 55), (39, 55), (32, 72)], fill=CREAM)
    d.polygon([(25, 55), (28, 54), (32, 62), (24, 60)], fill=BOARD)          # collar
    d.polygon([(39, 55), (36, 54), (32, 62), (40, 60)], fill=PEARL)
    d.polygon([(30, 58), (34, 58), (35, 73), (32, 77), (29, 73)], fill=CORAL)
    rect(img, 30, 58, 4, 2, BRICK)
    d.line([(25, 55), (32, 74)], fill=INK)                                   # lapels
    d.line([(39, 55), (32, 74)], fill=INK)
    rect(img, 44, 64, 5, 3, CREAM)                                           # pocket square
    # neck
    rect(img, 27, 47, 10, 8, SKIN_D)
    rect(img, 27, 47, 7, 6, SKIN)

    # head
    d.ellipse([18, 14, 46, 50], fill=INK)
    d.ellipse([19, 15, 45, 49], fill=SKIN_D)                                 # jaw and left side in shade
    d.ellipse([21, 15, 45, 47], fill=SKIN)
    d.ellipse([36, 22, 43, 34], fill=PEACH)                                  # lit cheek
    d.ellipse([35, 21, 42, 35], fill=SKIN)
    # ears
    for ex in (16, 44):
        rect(img, ex, 29, 4, 8, INK)
        rect(img, ex + 1, 30, 2, 6, SKIN_D)
    # hair: short grey, receding, with a darker side part
    d.chord([18, 12, 46, 36], 180, 360, fill=GREY)
    d.chord([20, 14, 44, 32], 190, 350, fill=GREY_L)
    rect(img, 24, 17, 16, 2, SKIN)                                           # high forehead
    rect(img, 26, 19, 12, 2, SKIN)
    rect(img, 18, 22, 3, 10, GREY)                                           # sides
    rect(img, 43, 22, 3, 10, GREY)
    rect(img, 19, 23, 1, 8, SILVER)
    rect(img, 30, 14, 1, 3, INK)                                             # part

    # face by mood: brows, eyes, mouth, cheeks
    ey = 29
    if mood == "angry":
        for k in range(6):          # brows down into a V
            img.putpixel((23 + k, ey - 5 + k // 2), INK)
            img.putpixel((23 + k, ey - 4 + k // 2), GREY)
            img.putpixel((40 - k, ey - 5 + k // 2), INK)
            img.putpixel((40 - k, ey - 4 + k // 2), GREY)
        rect(img, 30, ey - 3, 1, 3, SKIN_D)                                  # furrow
        rect(img, 33, ey - 3, 1, 3, SKIN_D)
        for exx in (24, 35):
            rect(img, exx, ey, 5, 2, CREAM)
            rect(img, exx + 2, ey, 2, 2, INK)
            rect(img, exx, ey - 1, 5, 1, INK)
        # shouting mouth
        d.rectangle([27, 39, 37, 44], fill=INK)
        rect(img, 28, 40, 9, 1, CREAM)
        rect(img, 29, 43, 7, 1, BRICK)
        for cx0 in (21, 38):        # red in the face
            dither(img, cx0, 34, 5, 4, CORAL, SKIN, 2)
        rect(img, 41, 18, 2, 3, SKY)                                         # a bead of sweat
    else:
        lift = 1 if mood == "pleased" else 0
        for exx in (23, 35):        # brows: flat, or raised a pixel when pleased
            rect(img, exx, ey - 4 - lift, 6, 1, INK)
            rect(img, exx, ey - 3 - lift, 6, 1, GREY)
        if mood == "pleased":
            for exx in (24, 36):    # eyes squint into arcs
                rect(img, exx, ey, 4, 1, INK)
                img.putpixel((exx - 1, ey + 1), INK)
                img.putpixel((exx + 4, ey + 1), INK)
            # smile with teeth
            d.chord([25, 36, 39, 46], 0, 180, fill=INK)
            rect(img, 27, 41, 11, 1, CREAM)
            img.putpixel((24, 39), INK)
            img.putpixel((40, 39), INK)
            for cx0 in (21, 38):    # warm cheeks
                dither(img, cx0, 35, 5, 3, PEACH, SKIN, 2)
        else:
            for exx in (24, 35):
                rect(img, exx, ey, 5, 2, CREAM)
                rect(img, exx + 2, ey, 2, 2, INK)
                rect(img, exx, ey - 1, 5, 1, SKIN_D)
            rect(img, 27, 42, 10, 1, INK)                                    # flat, firm mouth
            rect(img, 28, 43, 8, 1, SKIN_D)
    # nose
    rect(img, 31, 31, 2, 6, SKIN_D)
    rect(img, 30, 36, 4, 1, SKIN_D)
    rect(img, 33, 32, 1, 4, PEACH)

    # frame: ink edge with a gold inner line
    for k, col in ((0, INK), (1, GOLD_D)):
        rect(img, k, k, 64 - 2 * k, 1, col)
        rect(img, k, 79 - k, 64 - 2 * k, 1, col)
        rect(img, k, k, 1, 80 - 2 * k, col)
        rect(img, 63 - k, k, 1, 80 - 2 * k, col)
    rect(img, 2, 2, 60, 1, GOLD_L)
    return img


MOODS = ("neutral", "pleased", "angry")


def build_coach():
    sheet = Image.new("RGBA", (64 * len(MOODS), 80), CLEAR)
    for i, m in enumerate(MOODS):
        sheet.paste(coach(m), (64 * i, 0))
    sheet.save(os.path.join(UI, "coach_moods.png"))
    coach("neutral").save(os.path.join(UI, "coach_portrait.png"))
    return sheet


def pixels(img):
    return img.get_flattened_data() if hasattr(img, "get_flattened_data") else img.getdata()


def check_palette(img, name):
    pal = {c + (255,) for c in make_ui.PAL}
    off = {p for p in pixels(img) if p[3] and p not in pal}
    assert not off, f"{name}: colours off the palette: {sorted(off)[:5]}"


def build_sheet(out):
    """A review sheet: the backdrop untinted and tinted in two club colours, and the coach moods, at 2x."""
    room = Image.open(os.path.join(UI, "dressing_room.png")).convert("RGBA")
    jersey = Image.open(os.path.join(UI, "dressing_room_jersey.png")).convert("RGBA")
    trim = Image.open(os.path.join(UI, "dressing_room_trim.png")).convert("RGBA")
    moods = Image.open(os.path.join(UI, "coach_moods.png")).convert("RGBA")

    def tinted(primary, secondary):
        out = room.copy()
        for mask, col in ((jersey, primary), (trim, secondary)):
            c = make_ui.snap(col)
            layer = Image.new("RGBA", mask.size, CLEAR)
            px = [(c[0] * p[0] // 255, c[1] * p[1] // 255, c[2] * p[2] // 255, p[3]) for p in pixels(mask)]
            layer.putdata(px)
            out.alpha_composite(layer)
        return out

    tiles = [room, tinted("#c8574a", "#efe0c2"), tinted("#2f5f8f", "#f2c14e")]
    sheet = Image.new("RGBA", (W * 2 + 8, H * 2 + 8), NIGHT)
    sheet.paste(tiles[0], (0, 0))
    sheet.paste(tiles[1], (W + 8, 0))
    sheet.paste(tiles[2], (0, H + 8))
    sheet.paste(moods, (W + 8, H + 8))
    sheet.paste(moods.resize((moods.width * 2, moods.height * 2), Image.NEAREST), (W + 8, H + 8 + 84))
    sheet = sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST)
    sheet.save(out)


def main():
    if len(sys.argv) > 2 and sys.argv[1] == "sheet":
        build_sheet(sys.argv[2])
        return
    os.makedirs(UI, exist_ok=True)
    room, _, _ = build_backdrop()
    check_palette(room, "dressing_room")
    check_palette(build_coach(), "coach_moods")


if __name__ == "__main__":
    main()
