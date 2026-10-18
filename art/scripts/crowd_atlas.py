"""Stick & Puck crowd atlas (A18): reads the flat id frames build_crowd.py rendered
(art/crowd/renders/ids/<look>_<frame>.png), gives every part of every look its lit and shade colour
from the dusk palette (art/palette/stickandpuck_dusk.gpl, never the kit block), and packs
art/crowd/crowd_256.png for the game (Arena/CrowdRenderer.cs):
  - 16x32 cells, 16 columns by 8 rows. Column = frame: sit0 sit1 stand0 stand1 cheer0 cheer1
    wave0 wave1 boo0 boo1 (columns 10-15 are empty). Rows 0-3 = the four looks (short, long,
    beanie, cropped) in their own everyday clothes; rows 4-7 = the same looks' shirt mask,
    white where lit and grey in shade, which the game draws over the shirt tinted with a club colour.
  - every opaque pixel of rows 0-3 is a palette colour (checked), the feet on the bottom row of the cell.
Also writes art/crowd/renders/crowd_contact.png: the five poses (both frames) for every look, in
their own clothes and tinted with a few clubs' home colours as the game draws them.
Run with python3 (Pillow, numpy):  python3 art/scripts/crowd_atlas.py   after build_crowd.py."""
import os
import numpy as np
from PIL import Image, ImageDraw

def _art_dir():
    a = os.environ.get('SAP_ART') or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if not os.path.isdir(a): raise SystemExit('set SAP_ART to the art/ folder')
    return os.path.abspath(a)
ART = _art_dir()
IDS = ART + '/crowd/renders/ids'
CELL_W, CELL_H, COLS, ROWS = 16, 32, 16, 8
FRAMES = ['sit0', 'sit1', 'stand0', 'stand1', 'cheer0', 'cheer1', 'wave0', 'wave1', 'boo0', 'boo1']
LOOKS = ['short', 'long', 'beanie', 'cropped']
MASK_ROW = 4
MASK = {'lit': (255, 255, 255), 'shade': (176, 176, 176)}   # the game multiplies the club colour by these

# id key colours from build_crowd.py (sRGB after the Standard view transform)
KEYS = {('skin', 'lit'): (255, 0, 0), ('skin', 'shade'): (159, 0, 0), ('hair', 'lit'): (0, 255, 0), ('hair', 'shade'): (0, 159, 0),
        ('shirt', 'lit'): (0, 0, 255), ('shirt', 'shade'): (0, 0, 159), ('trousers', 'lit'): (255, 255, 0),
        ('trousers', 'shade'): (159, 159, 0), ('shoes', 'lit'): (255, 0, 255), ('shoes', 'shade'): (159, 0, 159)}

# each look's everyday colours, (lit, shade) per part, all dusk palette entries
LOOK_COLOURS = {
    'short': {'skin': ('#f0b48a', '#c47262'), 'hair': ('#613b3a', '#423236'), 'shirt': ('#bdb9c4', '#8f8a9c'),
              'trousers': ('#2a2c40', '#211b28'), 'shoes': ('#211b28', '#18131f')},
    'long': {'skin': ('#d2ad7f', '#9e655c'), 'hair': ('#c8963c', '#613b3a'), 'shirt': ('#a34e4d', '#613b3a'),
             'trousers': ('#3e5875', '#31556e'), 'shoes': ('#33293a', '#211b28')},
    'beanie': {'skin': ('#9e655c', '#613b3a'), 'hair': ('#2a2230', '#18131f'), 'shirt': ('#3e5875', '#2a2c40'),
               'trousers': ('#555f6b', '#2a2c36'), 'shoes': ('#211b28', '#18131f')},
    'cropped': {'skin': ('#fba771', '#c47262'), 'hair': ('#2a2230', '#18131f'), 'shirt': ('#f2c14e', '#c8963c'),
                'trousers': ('#33293a', '#211b28'), 'shoes': ('#211b28', '#18131f')},
}

def hexrgb(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))

def palette():
    """the dusk palette above the kit block: the colours the arena and crowd may snap to"""
    out = []
    for line in open(ART + '/palette/stickandpuck_dusk.gpl'):
        if line.startswith('# kit colours:'): break
        parts = line.split()
        if len(parts) >= 3 and line.strip()[0].isdigit(): out.append(tuple(int(p) for p in parts[:3]))
    return out
PAL = palette()

def classify(path):
    """(h, w) array of (part, tone) or None from one id frame"""
    a = np.array(Image.open(path).convert('RGBA')).astype(int)
    if a.shape[:2] != (CELL_H, CELL_W): raise SystemExit('%s is %s, not %dx%d' % (path, a.shape[:2], CELL_W, CELL_H))
    keys = list(KEYS.items())
    out = [[None] * CELL_W for _ in range(CELL_H)]
    for y in range(CELL_H):
        for x in range(CELL_W):
            r, g, b, al = a[y, x]
            if al < 128: continue
            out[y][x] = min(keys, key=lambda kv: sum((c - k) ** 2 for c, k in zip((r, g, b), kv[1])))[0]
    return out

def build():
    atlas = Image.new('RGBA', (COLS * CELL_W, ROWS * CELL_H), (0, 0, 0, 0))
    px = atlas.load()
    for li, look in enumerate(LOOKS):
        cols = LOOK_COLOURS[look]
        for fi, frame in enumerate(FRAMES):
            ids = classify('%s/%s_%s.png' % (IDS, look, frame))
            for y in range(CELL_H):
                for x in range(CELL_W):
                    if ids[y][x] is None: continue
                    part, tone = ids[y][x]
                    px[fi * CELL_W + x, li * CELL_H + y] = hexrgb(cols[part][0 if tone == 'lit' else 1]) + (255,)
                    if part == 'shirt':
                        px[fi * CELL_W + x, (MASK_ROW + li) * CELL_H + y] = MASK[tone] + (255,)
    a = np.array(atlas)
    base = a[:MASK_ROW * CELL_H]
    used = {tuple(c[:3]) for c in base.reshape(-1, 4) if c[3] == 255}
    off = used - set(PAL)
    if off: raise SystemExit('colours off the palette: %s' % sorted(off))
    if any(c[3] not in (0, 255) for c in a.reshape(-1, 4)): raise SystemExit('atlas alpha is not 0 or 255')
    os.makedirs(ART + '/crowd', exist_ok=True)
    atlas.save(ART + '/crowd/crowd_256.png')
    return atlas

def nearest(rgb):
    return min(PAL, key=lambda p: sum((a - b) ** 2 for a, b in zip(p, rgb)))

def contact(atlas, scale=5):
    """the five poses for every look: own clothes, then tinted with a club's home colour as the game draws it"""
    clubs = {}
    import json, glob
    for f in glob.glob(os.path.dirname(ART) + '/data/clubs/*.json'):
        d = json.load(open(f)); clubs[d['id']] = d['home']['primary']
    shown = [c for c in ('frolunda', 'brynas', 'lulea', 'djurgarden') if c in clubs]
    pad, head = 6, 18
    cw, ch = CELL_W * scale, CELL_H * scale
    W = 110 + len(FRAMES) * (cw + pad) + pad
    H = head + len(LOOKS) * 2 * (ch + pad) + pad
    sheet = Image.new('RGB', (W, H), hexrgb('#2a2230'))
    d = ImageDraw.Draw(sheet)
    for i in range(5):
        d.text((110 + pad + i * 2 * (cw + pad) + cw // 2, 4), ['sit', 'stand', 'cheer', 'wave', 'boo'][i], fill=hexrgb('#efe0c2'))
    a = np.array(atlas).astype(float)
    row = 0
    for li, look in enumerate(LOOKS):
        for name, primary in [('own clothes', None)] + [(c, clubs[c]) for c in shown[li % len(shown):][:1]]:
            y0 = head + row * (ch + pad)
            d.text((6, y0 + ch // 2 - 12), look, fill=hexrgb('#efe0c2')); d.text((6, y0 + ch // 2), name, fill=hexrgb('#a7a3b0'))
            for fi in range(len(FRAMES)):
                cell = a[li * CELL_H:(li + 1) * CELL_H, fi * CELL_W:(fi + 1) * CELL_W].copy()
                if primary:
                    m = a[(MASK_ROW + li) * CELL_H:(MASK_ROW + li + 1) * CELL_H, fi * CELL_W:(fi + 1) * CELL_W]
                    tint = np.array(nearest(hexrgb(primary)), float)
                    on = m[..., 3] > 0
                    cell[on, :3] = m[on, :3] / 255.0 * tint
                    for y, x in zip(*np.nonzero(on)): cell[y, x, :3] = nearest(tuple(cell[y, x, :3]))   # the post pass snaps it
                img = Image.fromarray(cell.astype(np.uint8), 'RGBA').resize((cw, ch), Image.NEAREST)
                sheet.paste(img, (110 + pad + fi * (cw + pad), y0), img)
            row += 1
    os.makedirs(ART + '/crowd/renders', exist_ok=True)
    sheet.save(ART + '/crowd/renders/crowd_contact.png')

atlas = build()
contact(atlas)
print('crowd: wrote crowd_256.png and renders/crowd_contact.png')
