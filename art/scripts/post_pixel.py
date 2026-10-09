"""Post pass that makes the render read as pixel art: hard 1px player outlines taken from
the flat id render, then every colour snapped to a hand-picked fixed palette, with no
anti-aliasing and no soft gradients left anywhere. Run with python3 on the device:
python3 post_pixel.py broadcast  (after scene.py and render_ids.py on scene/broadcast.blend)"""
import colorsys, numpy as np
from PIL import Image

import os
# Where art/ is: ART passed as a global (exec), SAP_ART in the environment, or two levels up
# from this script when it is run as a file; no path to anyone's machine is baked in.
def _art_dir():
    a = globals().get('ART') or os.environ.get('SAP_ART')
    if not a and '__file__' in globals(): a = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if not a or not os.path.isdir(a): raise SystemExit('set ART (global) or SAP_ART (env) to the art/ folder')
    return os.path.abspath(a)
ART = _art_dir()
R = ART + '/scene/renders/'
l2s = lambda c: np.where(c <= 0.0031308, c * 12.92, 1.055 * np.clip(c, 0, None) ** (1 / 2.4) - 0.055)
hexes = lambda hs: np.array([[int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)] for h in hs], np.float32)

OUTLINE_HEX = '#18131f'

# dusk palette after the pixel street reference: plum and slate darks, brick, coral, peach,
# gold and cream, a cool ice ramp with one warm band tone, and both kits
PAL_DUSK = ['#18131f', '#211b28', '#2a2230', '#33293a', '#2a2c36', '#27383e', '#423236', '#555f6b',
            '#74666b', '#8a8790', '#31556e', '#3e5875', '#2f5f8f', '#7f9cae', '#613b3a', '#a34e4d',
            '#c8574a', '#e96f5d', '#c47262', '#fba771', '#c8963c', '#f2c14e', '#f9da93', '#d2ad7f',
            '#f0b48a', '#9e655c', '#a7a3b0', '#bdb9c4', '#c9c6cf', '#e3d8c6', '#efe0c2', '#2a2c40']
# the ice gets its own short ramp so the light bands come out as one flat lighter shade
ICE_DUSK = ['#8f8a9c', '#a7a3b0', '#bdb9c4', '#ccc9d2', '#e3d8c6']
import sys
NAME = sys.argv[1] if len(sys.argv) > 1 else 'broadcast'    # reads NAME_beauty.png and NAME_id.png, writes NAME.png and NAME_x3.png

beauty = np.asarray(Image.open(R + NAME + '_beauty.png').convert('RGB'), np.float32) / 255.0
idimg = np.asarray(Image.open(R + NAME + '_id.png').convert('RGB'), np.float32) / 255.0
H, W, _ = beauty.shape

# ---- label every player pixel by matching the id render against its known flat colours ----
REF = l2s(np.array([colorsys.hsv_to_rgb((i * 0.137) % 1.0, 0.95, 1.0 if i % 2 else 0.55)
                    for i in range(1, 17)], np.float32))
flat = idimg.reshape(-1, 3)
lab = np.argmin(((flat[:, None, :] - REF[None]) ** 2).sum(2), 1).astype(np.int16) + 1
icemask = (flat.min(1) > 0.9).reshape(H, W)      # the ice is rendered pure white in the id pass
lab[flat.max(1) < 0.10] = 0                       # background
lab[flat.min(1) > 0.9] = 0
lab = lab.reshape(H, W)

def sh(a, dy, dx):
    o = np.zeros_like(a)
    o[max(dy, 0):H + min(dy, 0), max(dx, 0):W + min(dx, 0)] = a[max(-dy, 0):H + min(-dy, 0), max(-dx, 0):W + min(-dx, 0)]
    return o
edge = np.zeros((H, W), bool)
# the line goes around each player, so a 30 px player keeps every pixel of kit and face
for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
    n = sh(lab, dy, dx)
    edge |= (lab == 0) & (n > 0)
    edge |= (lab > 0) & (n > 0) & (n != lab) & (lab < n)   # where two players overlap, one pixel between them

def snap(img, pal):
    px = img.reshape(-1, 3)
    out = np.empty_like(px)
    for i in range(0, len(px), 60000):            # chunked so the distance matrix stays small
        c = px[i:i + 60000]
        out[i:i + 60000] = pal[np.argmin(((c[:, None, :] - pal[None]) ** 2).sum(2), 1)]
    return out.reshape(img.shape)

result = {'size': [W, H], 'player_px': int((lab > 0).sum()), 'edge_px': int(edge.sum()), 'colours': {}}
q = snap(beauty, hexes(PAL_DUSK))
# snap the ice first, then a 5x5 majority vote over ice pixels only: removes the
# speckle where the shading crosses a ramp step, and never touches lines or players
iq = snap(beauty, hexes(ICE_DUSK))
code = np.argmin(((iq[..., None, :] - hexes(ICE_DUSK)[None, None]) ** 2).sum(-1), -1)
votes = np.zeros((len(ICE_DUSK), H, W), np.int16)
for dy in range(-2, 3):
    for dx in range(-2, 3):
        c = sh(code + 1, dy, dx); m = sh(icemask.astype(np.int16), dy, dx) > 0
        for j in range(len(ICE_DUSK)): votes[j] += (c == j + 1) & m
q[icemask] = hexes(ICE_DUSK)[votes.argmax(0)][icemask]
q[edge] = hexes([OUTLINE_HEX])[0]
im = Image.fromarray(np.clip(q * 255.0 + 0.5, 0, 255).astype(np.uint8))
im.save(R + NAME + '.png', optimize=True)
im.resize((W * 3, H * 3), Image.NEAREST).save(R + NAME + '_x3.png', optimize=True)
result['colours'] = len(np.unique(np.asarray(im).reshape(-1, 3), axis=0))
print(result)
