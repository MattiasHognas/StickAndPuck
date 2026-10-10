"""Draws the skate scratches on the ice as a mask, never by hand (A4).

    python3 art/scripts/ice_scratches.py        # writes art/scene/ice_scratches.png

The mask covers the rink at 8 texels a metre: x along the rink (-30..30 m) left to right, z
(-12.75..12.75 m, far boards to near boards) top to bottom. Scratches are white on transparent;
the game tints them with the ice band tone of the lighting, so they stay a palette colour and
follow the mood. They gather where skaters turn and stop (in front of the nets, round the faceoff
dots, along the boards) and are sparse in the neutral zone. A fixed seed keeps the file stable.
Needs Pillow.
"""
import math
import os
import random

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.environ.get('SAP_ART') or os.path.dirname(HERE)
OUT = os.path.join(ART, 'scene', 'ice_scratches.png')

PER_M = 8
HALF_L, HALF_W, CORNER = 30.0, 12.75, 8.5      # src/Core/RinkSpec.cs
GOAL_X, DOTS_X, DOTS_Z = 26.7, 22.0, 6.7        # goal line; end-zone faceoff dots
SEED = 2026

W, H = int(2 * HALF_L * PER_M), int(2 * HALF_W * PER_M)


def on_ice(x, z, margin=0.3):
    ax, az = abs(x), abs(z)
    if ax > HALF_L - margin or az > HALF_W - margin: return False
    cx, cz = HALF_L - CORNER, HALF_W - CORNER
    if ax > cx and az > cz: return math.hypot(ax - cx, az - cz) < CORNER - margin
    return True


def texel(x, z):
    return int((x + HALF_L) * PER_M), int((z + HALF_W) * PER_M)


def arc(px, rng, x, z, length, heading, bend, gaps):
    """One skate cut: a shallow arc of single texels, with a few breaks where the blade lifted."""
    steps = int(length * PER_M * 1.5)
    for i in range(steps):
        t = i / steps
        if gaps and rng.random() < 0.08: continue
        a = heading + bend * t
        x += math.cos(a) / (PER_M * 1.5); z += math.sin(a) / (PER_M * 1.5)
        if not on_ice(x, z): return
        u, v = texel(x, z)
        if 0 <= u < W and 0 <= v < H: px[u, v] = (255, 255, 255, 255)


def cluster(px, rng, cx, cz, spread, count, length):
    for _ in range(count):
        x = cx + rng.gauss(0, spread); z = cz + rng.gauss(0, spread * 0.7)
        heading = rng.uniform(0, 2 * math.pi)
        arc(px, rng, x, z, rng.uniform(*length), heading, rng.uniform(-1.4, 1.4), True)


def main():
    rng = random.Random(SEED)
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = img.load()
    for end in (-1, 1):
        cluster(px, rng, end * (GOAL_X - 2.5), 0, 1.6, 14, (0.8, 2.2))        # in front of each net
        for dz in (-DOTS_Z, DOTS_Z):
            cluster(px, rng, end * DOTS_X, dz, 1.4, 8, (0.8, 2.0))              # round the end-zone dots
        cluster(px, rng, end * 12, 0, 4.0, 6, (1.5, 3.5))                       # the blue-line zones
    cluster(px, rng, 0, 0, 2.2, 6, (1.0, 2.5))                                   # centre faceoff
    for _ in range(18):                                                          # long glides along the boards
        x = rng.uniform(-HALF_L + CORNER, HALF_L - CORNER); side = rng.choice((-1, 1))
        z = side * (HALF_W - rng.uniform(0.8, 2.5))
        arc(px, rng, x, z, rng.uniform(3, 7), rng.choice((0, math.pi)) + rng.uniform(-0.1, 0.1), rng.uniform(-0.2, 0.2), True)
    for _ in range(10):                                                          # a few cuts anywhere
        x, z = rng.uniform(-HALF_L, HALF_L), rng.uniform(-HALF_W, HALF_W)
        if on_ice(x, z): arc(px, rng, x, z, rng.uniform(1.5, 4), rng.uniform(0, 2 * math.pi), rng.uniform(-0.8, 0.8), True)
    img.save(OUT, optimize=True)
    print('wrote', os.path.relpath(OUT, os.path.dirname(ART)), '%dx%d' % (W, H))


if __name__ == '__main__':
    main()
