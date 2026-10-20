"""Street clothes for the staff model, one atlas per club, from the club data files (the relegated
ending, Season/Endings: a player leaving the arena at night in his own clothes).

    python3 art/scripts/make_street.py [--league shl|nhl] [club ...]

Writes art/kits/{league}/{club}_street_64.png: the staff atlas (art/player/staff_trainer_64.png, so
the face and the bare hands stay as drawn there) repainted as a quilted puffer jacket in the club's
home colour with a zip and collar in its second colour, jeans, dark trainers with light soles, and
a beanie in the club's colours over the hair cap. The colours snap to the kit bases as the kits do
(make_kits.py), so they hold their hue under every toon tone. Needs Pillow.
"""
import argparse
import json
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_kits as K  # noqa: E402  (the kit colours and the palette, shared with the kits)

# the staff atlas's rects (art/scripts/build_player.py R): x, y, w, h; u runs right side 0, front .25,
# left .5, back .75; rows run from the part's end (top row, t = 1) to its start
R = {'torso': (0, 0, 32, 20), 'pants': (32, 0, 32, 10), 'thigh': (32, 10, 16, 10), 'shin': (48, 10, 16, 14),
     'upper': (0, 20, 16, 8), 'fore': (16, 20, 16, 8), 'helmet': (32, 24, 32, 8), 'boot': (48, 32, 16, 8)}
DENIM, SEAM, HEM = '#3e5875', '#31556e', '#2a2c40'
SHOE, SOLE = '#2a2230', '#bdb9c4'


def near(u, c, half):
    return abs(((u - c + 0.5) % 1.0) - 0.5) < half


def street(template, c):
    img = template.copy()
    px = img.load()
    primary, second = c['primary'], c['secondary']
    if K.dist(K.rgb(primary), K.rgb(second)) < 40:     # a second colour too close to read: cream trim
        second = K.CREAM
    quilt = c['primary mid']                             # the jacket's quilted seams: its own mid tone

    def fill(key, fn):
        x0, y0, w, h = R[key]
        for y in range(h):
            for x in range(w):
                px[x0 + x, y0 + y] = K.rgb(fn((x + 0.5) / w, 1 - (y + 0.5) / h, y))

    def jacket(u, t, row):
        if t > 0.9: return second                        # collar
        if near(u, 0.25, 0.02): return second            # zip
        return quilt if row % 4 == 3 else primary
    fill('torso', jacket)
    fill('upper', lambda u, t, row: quilt if row % 4 == 3 else primary)
    fill('fore', lambda u, t, row: HEM if t > 0.86 else (quilt if row % 4 == 1 else primary))
    fill('pants', lambda u, t, row: SEAM if near(u, 0.0, 0.03) or near(u, 0.5, 0.03) else (HEM if t > 0.85 else DENIM))
    fill('thigh', lambda u, t, row: SEAM if near(u, 0.0, 0.04) or near(u, 0.5, 0.04) else DENIM)
    fill('shin', lambda u, t, row: HEM if t < 0.08 else (SEAM if near(u, 0.0, 0.04) or near(u, 0.5, 0.04) else DENIM))
    fill('boot', lambda u, t, row: SOLE if t < 0.22 else (second if near(u, 0.0, 0.08) or near(u, 0.5, 0.08) else SHOE))

    def beanie(u, t, row):                               # a folded cuff, a band, a stripe up the crown
        if t < 0.32: return second
        if 0.5 < t < 0.62: return second
        return primary
    fill('helmet', beanie)
    return img


def main(args):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--league', default='shl')
    parser.add_argument('clubs', nargs='*')
    o = parser.parse_args(args)
    folder = os.path.join(K.CLUBS, o.league)
    out = os.path.join(K.OUT, o.league)
    template = Image.open(os.path.join(K.ART, 'player', 'staff_trainer_64.png')).convert('RGB')
    for f in sorted(os.listdir(folder)):
        if not f.endswith('.json'): continue
        club = json.load(open(os.path.join(folder, f), encoding='utf-8'))
        if o.clubs and club['id'] not in o.clubs: continue
        path = os.path.join(out, '%s_street_64.png' % club['id'])
        K.indexed(street(template, K.colours_for(club, 'home'))).save(path)
        print('  wrote', os.path.relpath(path, K.ROOT))


if __name__ == '__main__':
    main(sys.argv[1:])
