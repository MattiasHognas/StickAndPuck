"""Bare-head skater atlases (A11): the same kit with the helmet strip painted as hair, for bench
figures with the helmet off.

    python3 art/kits/make_bare.py

1. The base texture: art/player/skater_{home|away}_bare.aseprite is the source once it exists and
   is only exported to skater_{kit}_bare_64.png. Without it, the hair is painted over
   skater_{kit}_64.png and both the png and the .aseprite are written (repaint in Aseprite after).
2. Every club kit art/kits/{league}/{club}_{home|away}_skater_64.png gets {club}_{kit}_skater_bare_64.png:
   the helmet strip and the ear-guard swatch copied from the base home bare atlas, with its three
   hair tones swapped for the club's hair set, so the hair is drawn once and only recoloured.

The helmet strip (build_player.py R['helmet'], x 32, y 24, 32x8) wraps the shell: right side at
column 32, front 40, left side 48, back 56; the top row is the crown, the bottom row the rim,
which in front sits just above the brows (a fringe there) and drops over the ears and nape at the
sides and back. The ear guards use the navy swatch (28, 44), so it turns to shaded skin: ears.
All colours are dusk palette colours (art/palette/stickandpuck_dusk.gpl), hard pixels; hair tones
stay mid to light so a head reads against the dark bench wall. Needs Pillow; aseprite optional.
"""
import glob
import os
import shutil
import subprocess

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
PLAYER = os.path.join(ART, 'player')
HELMET = (32, 24, 32, 8)
EARS = (28, 44, 4, 4)
SKIN, SKIN_SHADE = '#f0b48a', '#c47262'

# (shadow, mid, light) per hair set; the base texture is drawn in the first
HAIR = {'brown': ('#613b3a', '#9e655c', '#d2ad7f'), 'blond': ('#c8963c', '#d2ad7f', '#f9da93'),
        'auburn': ('#613b3a', '#a34e4d', '#c8574a'), 'dark': ('#423236', '#613b3a', '#9e655c')}
BASE_HAIR = 'brown'
ORDER = ['brown', 'blond', 'dark', 'auburn']   # clubs take hair sets in turn, alphabetically


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def paint_hair(im, tones):
    dark, mid, light = (rgb(t) for t in tones)
    x0, y0, w, h = HELMET
    for r in range(h):
        for i in range(w):
            c = x0 + i
            front = 37 <= c <= 43                   # the face side of the shell
            col = mid
            if r <= 3 and (i + 2 * r) % 7 == 0: col = light          # a few highlight strands on the crown
            if 2 <= r <= 5 and (3 * i + r) % 5 == 0: col = dark      # darker strands down the sides
            if r == 0 and i % 4 == 1: col = light
            if r >= 6 and not front: col = dark                       # hairline shadow over ears and nape
            if front and r == 6: col = dark if i % 2 else mid
            if front and r == 7: col = rgb(SKIN) if c % 2 == 0 else dark   # a ragged fringe over the brows
            im.putpixel((c, y0 + r), col)
    ex, ey, ew, eh = EARS
    for y in range(ey, ey + eh):
        for x in range(ex, ex + ew): im.putpixel((x, y), rgb(SKIN_SHADE))
    return im


def aseprite(src, dst):
    if not shutil.which('aseprite'): return False
    subprocess.run(['aseprite', '-b', src, '--save-as', dst], check=True, capture_output=True, timeout=120)
    return True


def base(kit):
    ase = os.path.join(PLAYER, 'skater_%s_bare.aseprite' % kit)
    png = os.path.join(PLAYER, 'skater_%s_bare_64.png' % kit)
    if os.path.exists(ase):
        aseprite(ase, png)
    else:
        paint_hair(Image.open(os.path.join(PLAYER, 'skater_%s_64.png' % kit)).convert('RGB'), HAIR[BASE_HAIR]).save(png)
        aseprite(png, ase)
    return Image.open(png).convert('RGB')


def recolour(region, tones):
    swap = {rgb(a): rgb(b) for a, b in zip(HAIR[BASE_HAIR], tones)}
    out = region.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            px[x, y] = swap.get(px[x, y], px[x, y])
    return out


def main():
    home = base('home'); base('away')
    box = lambda r: (r[0], r[1], r[0] + r[2], r[1] + r[3])
    hair, ears = home.crop(box(HELMET)), home.crop(box(EARS))
    n = 0
    club_count = 0
    for folder in sorted(glob.glob(os.path.join(HERE, '*'))):
        if not os.path.isdir(folder): continue
        kits = sorted(glob.glob(os.path.join(folder, '*_skater_64.png')))
        clubs = sorted({os.path.basename(p).rsplit('_', 3)[0] for p in kits})
        club_count += len(clubs)
        for p in kits:
            club = os.path.basename(p).rsplit('_', 3)[0]
            tones = HAIR[ORDER[clubs.index(club) % len(ORDER)]]
            im = Image.open(p).convert('RGB')
            im.paste(recolour(hair, tones), HELMET[:2]); im.paste(ears, EARS[:2])
            im.save(p.replace('_skater_64.png', '_skater_bare_64.png'), optimize=True); n += 1
    print('%d bare-head club atlases, %d clubs' % (n, club_count))


if __name__ == '__main__':
    main()
