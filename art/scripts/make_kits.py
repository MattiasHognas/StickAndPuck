"""Builds every club's kit atlases from the club data files, never by hand.

    python3 art/scripts/make_kits.py palette        # refresh the kit colours at the end of the palette
    python3 art/scripts/make_kits.py [club ...]     # write art/kits/{league}/{club}_{home|away}_{skater|goalie}_64.png
    python3 art/scripts/make_kits.py check [club ...]   # only report how each kit snaps, write nothing
    Add --league shl (the default), --league nhl, or another data/clubs/ folder name.

Besides the club kit, every rostered player gets his own atlas with his number on the back,
art/kits/{league}/numbers/{club}_{home|away}_{skater|goalie}_{number}_64.png (A4); the game picks it by number.

Each kit is a recolour of the generic home kit in art/player/{role}_home_64.png (exported from its
.aseprite source), so the sweater cut, stripes, number and face stay as drawn there. The template's
four kit colours are swapped per body part for the club's colours from data/clubs/{league}/{id}.json,
each snapped to one of the kit bases below. The palette keeps a reserved block of kit colours:
for every kit base, the mid and shadow tones the toon shader turns it into, so a lit, half-lit or
shadowed sweater still snaps to its own hue in the post pass. Needs Pillow; aseprite is optional.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.environ.get('SAP_ART') or os.path.dirname(HERE)
ROOT = os.path.dirname(ART)
GPL = os.path.join(ART, 'palette', 'stickandpuck_dusk.gpl')
CLUBS = os.path.join(ROOT, 'data', 'clubs')
OUT = os.path.join(ART, 'kits')
MARK = '# kit colours: written by art/scripts/make_kits.py palette, do not edit below'
MAX_COLOURS = 64                     # the post shader's palette size (src/Render/Palette.cs)

# The toon shader's three tones (shaders/toon.fs): full light, mid, shadow.
TONES = [(1.0, 1.0, 1.0), (0.70, 0.68, 0.72), (0.42, 0.40, 0.48)]

# Colours a club colour may snap to. The first block is already in the dusk palette; the second
# is the reserved kit set, hand-picked in the dusk mood to cover the 14 SHL clubs' colours.
# A club colour that fits none of these well gets a warning: add a base here, then rerun palette.
KIT_BASES = {
    'cream': '#efe0c2', 'gold': '#f2c14e', 'slate blue': '#2f5f8f', 'green': '#2c6e4a', 'black': '#2a2c36',
    'orange': '#e07a3a',
    'kit red': '#b8323c', 'kit bright red': '#d42a22', 'kit royal blue': '#2e6db0', 'kit navy': '#2b3d6b',
    'kit bright green': '#2f8a4e', 'kit yellow': '#f4d63e',
}
RESERVED = [k for k in KIT_BASES if k.startswith('kit ')]
NEAR = 12.0                          # Lab distance under which an existing colour already serves a tone

# Template kit colours (art/scripts/build_player.py KITS['home']) and atlas rects (x, y, w, h).
Y, YD, B, BD = '#f2c14e', '#c8963c', '#2f5f8f', '#2a2c40'
CREAM = '#efe0c2'
R = {'torso': (0, 0, 32, 20), 'pants': (32, 0, 32, 10), 'helmet': (32, 24, 32, 8), 'head': (0, 28, 32, 14),
     'upper': (0, 20, 16, 8), 'glove': (32, 32, 16, 8),
     'blocker': (32, 40, 16, 8), 'catcher': (48, 40, 16, 8), 'pad': (0, 48, 32, 16)}

# The back number (A4). The torso unwraps around the body with the back centred on x 24, and its
# rows are squashed to about 0.6 screen pixels at rink distance, so digits are 3 wide and 8 tall
# with doubled bars: a 3x5 glyph whose bar rows are drawn twice. Rows 5-12 sit between the yoke
# and the hem stripes; the box is cleared of the template's sample number first.
DIGITS = {'0': ['###', '#.#', '#.#', '#.#', '###'], '1': ['.#.', '##.', '.#.', '.#.', '###'], '2': ['###', '..#', '###', '#..', '###'],
          '3': ['###', '..#', '.##', '..#', '###'], '4': ['#.#', '#.#', '###', '..#', '..#'], '5': ['###', '#..', '###', '..#', '###'],
          '6': ['###', '#..', '###', '#.#', '###'], '7': ['###', '..#', '..#', '.#.', '.#.'], '8': ['###', '#.#', '###', '#.#', '###'],
          '9': ['###', '#.#', '###', '..#', '###']}
DIGIT_ROWS = [0, 0, 1, 2, 2, 3, 4, 4]          # glyph row for each of the 8 texel rows
NUMBER_TOP, NUMBER_X = 5, 24                    # first row and centre column of the back number
NUMBER_BOX = (18, 4, 13, 10)                    # x, y, w, h cleared to the sweater colour

# The helmet (A4): at rink distance a plain shell read as hair. Now a dark two-row rim over the
# brows and ears, a light ridge stripe from the front over the crown to the back and a glint on
# each side, so the top
# of the head reads as a hard shell from the broadcast camera. Strip columns: right side 32,
# front 40, left side 48, back 56; the top row is the crown.
HELMET_RIM_ROWS = (30, 31)
HELMET_RIDGE_COLS = (38, 39, 40, 54, 55, 56)
HELMET_RIDGE_ROWS = range(24, 29)
HELMET_SHEEN = {(34, 26), (35, 26), (45, 26), (46, 26)}     # a glint on each side above the ear


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def hexc(c):
    return '#%02x%02x%02x' % tuple(c)


def lab(c):
    def lin(v):
        v /= 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(v) for v in c)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def dist(a, b):
    return sum((p - q) ** 2 for p, q in zip(lab(a), lab(b))) ** 0.5


def toned(c, tone):
    return tuple(min(255, int(round(v * t))) for v, t in zip(c, tone))


def read_palette():
    """Returns (lines before the kit block, colours before it, colours in it)."""
    head, base, kit, in_kit = [], [], [], False
    for line in open(GPL, encoding='utf-8').read().splitlines():
        if line.strip() == MARK:
            in_kit = True
            continue
        parts = line.split()
        is_colour = len(parts) >= 3 and parts[0].isdigit()
        if in_kit:
            if is_colour: kit.append(tuple(int(p) for p in parts[:3]))
            continue
        head.append(line)
        if is_colour: base.append(tuple(int(p) for p in parts[:3]))
    return head, base, kit


def snap_rgb(c, pal):
    # the post shader's rule: nearest palette colour in plain RGB
    return min(pal, key=lambda p: sum((a - b) ** 2 for a, b in zip(c, p)))


def make_palette():
    head, base, _ = read_palette()
    kit = []
    for name in KIT_BASES:
        c = rgb(KIT_BASES[name])
        for i, tone in enumerate(TONES):
            t = toned(c, tone)
            have = base + kit
            if name in RESERVED and i == 0:
                if t not in have: kit.append(t)      # reserved bases are always in, as picked
            elif min(dist(t, p) for p in have) > NEAR:
                kit.append(t)
    total = len(base) + len(kit)
    if total > MAX_COLOURS: sys.exit('palette would have %d colours; the post shader takes %d' % (total, MAX_COLOURS))
    lines = head + [MARK] + ['%3d %3d %3d\t%s' % (c + (hexc(c),)) for c in kit]
    open(GPL, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')
    print('palette: %d colours (%d before the kit block, %d kit colours)' % (total, len(base), len(kit)))


def kit_base(h):
    c = rgb(h)
    name = min(KIT_BASES, key=lambda k: dist(c, rgb(KIT_BASES[k])))
    d = dist(c, rgb(KIT_BASES[name]))
    return KIT_BASES[name], name, d


def luma(h):
    r, g, b = rgb(h)
    return 0.299 * r + 0.587 * g + 0.114 * b


def colours_for(club, side):
    """The kit's colours, snapped to kit bases: the roles the recolour below uses."""
    k = club[side]
    snap = {role: kit_base(k[role]) for role in ('primary', 'secondary', 'accent', 'helmet')}
    for role, (h, name, d) in snap.items():
        if d > 30: print('  warning: %s %s %s %s is far from every kit base (nearest %s, %.0f)' % (club['id'], side, role, k[role], name, d))
    c = {role: v[0] for role, v in snap.items()}
    # pants are the same on both kits: the home sweater colour, or its secondary when that is light
    home = [kit_base(club['home'][role])[0] for role in ('primary', 'secondary')]
    c['pants'] = home[0] if luma(home[0]) <= 110 else min(home, key=luma)
    c['dark'] = min((c['primary'], c['secondary'], c['helmet']), key=luma)
    if luma(c['dark']) > 110: c['dark'] = KIT_BASES['black']
    # goalie pads and gloves are cream: their bands take whichever kit colour stands out on cream
    c['trim'] = max((c['primary'], c['secondary']), key=lambda h: dist(rgb(h), rgb(CREAM)))
    c['primary mid'] = hexc(snap_rgb(toned(rgb(c['primary']), TONES[1]), palette_all()))
    # the back number and the helmet ridge take whichever kit colour (or cream) stands out most
    contrast = lambda against: max(dict.fromkeys((c['secondary'], c['accent'], CREAM, KIT_BASES['black'])),
                                   key=lambda h: dist(rgb(h), rgb(c[against])))
    c['number'] = contrast('primary')
    c['ridge'] = CREAM if luma(c['helmet']) <= 140 else KIT_BASES['black']   # a sheen line, the same on every club
    return c


_PAL = None


def palette_all():
    global _PAL
    if _PAL is None:
        _, base, kit = read_palette()
        _PAL = base + kit
    return _PAL


def inside(x, y, key):
    x0, y0, w, h = R[key]
    return x0 <= x < x0 + w and y0 <= y < y0 + h


def accent_rows(px):
    """The torso's middle hem stripe: full-width template-B rows, middle group of three."""
    x0, y0, w, h = R['torso']
    rows = [y for y in range(y0 + h // 2, y0 + h) if all(px[x, y] == rgb(B) for x in range(x0, x0 + w))]
    groups = []
    for y in rows:
        if groups and groups[-1][-1] == y - 1: groups[-1].append(y)
        else: groups.append([y])
    return set(groups[len(groups) // 2]) if len(groups) >= 3 else set()


def recolour(template, c, role, number=None):
    src = template.convert('RGB')
    px = src.load()
    acc = accent_rows(px)
    out = Image.new('RGB', src.size)
    po = out.load()
    for y in range(src.size[1]):
        for x in range(src.size[0]):
            h = hexc(px[x, y])
            n = h
            if role == 'skater' and inside(x, y, 'helmet'):
                rim = c['dark'] if c['dark'] != c['helmet'] else KIT_BASES['black']
                if rim == c['helmet']: rim = KIT_BASES['slate blue']   # a black shell gets a slate rim
                n = rim if y in HELMET_RIM_ROWS else c['ridge'] if (x in HELMET_RIDGE_COLS and y in HELMET_RIDGE_ROWS) or (x, y) in HELMET_SHEEN else c['helmet']
            elif inside(x, y, 'helmet') or (role == 'goalie' and inside(x, y, 'head')):
                # a plain shell: the template's front stripe reads as a stray dot at rink distance
                n = {B: c['helmet'], Y: c['helmet'], BD: c['dark'] if c['dark'] != c['helmet'] else KIT_BASES['black']}.get(h, h)
            elif inside(x, y, 'pants'):
                n = {B: c['pants'], BD: KIT_BASES['black'], Y: c['pants']}.get(h, h)   # plain: a thin side stripe only flickers
            elif inside(x, y, 'glove') and role == 'skater':
                n = {B: c['pants'], BD: KIT_BASES['black'], Y: c['primary']}.get(h, h)   # gloves match the pants
            elif inside(x, y, 'upper'):
                n = {B: c['primary'], Y: c['primary']}.get(h, h)   # plain upper sleeve; the stripes sit on the forearm
            elif role == 'goalie' and any(inside(x, y, k) for k in ('pad', 'blocker', 'catcher')):
                n = {B: c['trim'], Y: c['accent'] if c['accent'] != CREAM else c['trim']}.get(h, h)
            elif number is not None and in_number_box(x, y) and h in (B, BD):
                n = c['primary']        # the template's sample number goes; the player's is drawn below
            elif inside(x, y, 'torso') and y in acc and h == B:
                n = c['accent']
            elif inside(x, y, 'torso') and y > R['torso'][1] and y < R['torso'][1] + 4 and h in (B, YD):
                n = c['primary']        # shoulders in the sweater colour under a collar row, not a contrast yoke
            else:
                n = {Y: c['primary'], YD: c['primary mid'], B: c['secondary'], BD: c['dark']}.get(h, h)
            po[x, y] = rgb(n)
    if number is not None: draw_number(po, number, c['number'])
    return out


def in_number_box(x, y):
    bx, by, bw, bh = NUMBER_BOX
    return bx <= x < bx + bw and by <= y < by + bh


def draw_number(po, number, colour):
    text = str(number)
    x0 = NUMBER_X - (len(text) * 4 - 1) // 2
    for i, ch in enumerate(text):
        for r, gr in enumerate(DIGIT_ROWS):
            for col, bit in enumerate(DIGITS[ch][gr]):
                if bit == '#': po[x0 + i * 4 + col, NUMBER_TOP + r] = rgb(colour)


def indexed(img):
    """The atlas as an indexed PNG carrying the full palette, like the Aseprite exports."""
    pal = palette_all()
    flat = [v for c in pal for v in c] + [0] * (3 * (256 - len(pal)))
    idx = {c: i for i, c in enumerate(pal)}
    out = Image.new('P', img.size)
    out.putpalette(flat)
    src = img.load()
    missing = set()
    data = []
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            p = src[x, y]
            if p not in idx: missing.add(p); p = snap_rgb(p, pal)
            data.append(idx[p])
    if missing: print('  warning: snapped off-palette template colours %s' % sorted(hexc(m) for m in missing))
    out.putdata(data)
    return out


def template(role, output):
    """The generic home kit for a role, exported fresh from its .aseprite source when aseprite is here."""
    png = os.path.join(ART, 'player', '%s_home_64.png' % role)
    src = os.path.join(ART, 'player', '%s_home.aseprite' % role)
    if shutil.which('aseprite') and os.path.exists(src):
        tmp = os.path.join(output, '.template_%s.png' % role)
        subprocess.run(['aseprite', '-b', src, '--save-as', tmp], check=True, capture_output=True, timeout=120)
        img = Image.open(tmp).convert('RGB')
        os.remove(tmp)
        return img
    return Image.open(png).convert('RGB')


def report(club, side, c):
    """How the sweater's colours look under each toon tone after the post pass snaps them."""
    pal = palette_all()
    for role in ('primary', 'secondary', 'accent', 'helmet'):
        tones = [hexc(snap_rgb(toned(rgb(c[role]), t), pal)) for t in TONES]
        print('  %-10s %-9s %s -> %s  lit/mid/shadow %s' % (club['id'], side, role, c[role], ' '.join(tones)))
    for t in TONES:
        a = snap_rgb(toned(rgb(c['primary']), t), pal); b = snap_rgb(toned(rgb(c['secondary']), t), pal)
        if a == b: print('  warning: %s %s primary and secondary merge in tone %s' % (club['id'], side, t))


def main(args):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--league', default='shl', choices=sorted(
        name for name in os.listdir(CLUBS) if os.path.isdir(os.path.join(CLUBS, name))))
    parser.add_argument('actions', nargs='*', help='palette, check, or club ids')
    options = parser.parse_args(args)
    args = options.actions
    club_folder = os.path.join(CLUBS, options.league)
    output = os.path.join(OUT, options.league)
    numbers = os.path.join(output, 'numbers')
    if args[:1] == ['palette']:
        make_palette()
        return
    check = args[:1] == ['check']
    if check: args = args[1:]
    files = sorted(f for f in os.listdir(club_folder) if f.endswith('.json'))
    clubs = [json.load(open(os.path.join(club_folder, f), encoding='utf-8')) for f in files]
    if args: clubs = [c for c in clubs if c['id'] in args]
    if not check: os.makedirs(numbers, exist_ok=True)
    if not check and not args:            # a full run rewrites every numbered atlas, so dropped players go
        for f in os.listdir(numbers):
            if f.endswith('_64.png'): os.remove(os.path.join(numbers, f))
    templates = {} if check else {role: template(role, output) for role in ('skater', 'goalie')}
    for club in clubs:
        for side in ('home', 'away'):
            c = colours_for(club, side)
            report(club, side, c)
            if check: continue
            for role in ('skater', 'goalie'):
                path = os.path.join(output, '%s_%s_%s_64.png' % (club['id'], side, role))
                indexed(recolour(templates[role], c, role)).save(path)
                print('  wrote', os.path.relpath(path, ROOT))
                players = [p for p in club['roster'] if (p['position'] == 'G') == (role == 'goalie')]
                for p in players:
                    path = os.path.join(numbers, '%s_%s_%s_%d_64.png' % (club['id'], side, role, p['number']))
                    indexed(recolour(templates[role], c, role, p['number'])).save(path)
                print('  wrote %d numbered %s atlases' % (len(players), role))


if __name__ == '__main__':
    main(sys.argv[1:])
