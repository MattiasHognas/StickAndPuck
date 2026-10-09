"""Pixel-font ad boards. Replaces the vector ad text on the dasher boards with one small
texture strip per sponsor, lettered in a 3x5 pixel font and sampled with nearest filtering.
Run in Blender on art/scene/arena_dusk.blend after restyle_dusk.py; saves it in place and
writes art/ads/ads_<w>x<h>.png (all ten strips stacked) for the game to reuse.
Sponsors are made up; keep them generic, never real league or brand marks.
Options, passed as globals:
  FIT    size each strip to the far boards' on-screen pixel size for the scene camera, so one
         texel is one screen pixel and no letter row or column doubles (default True; needs a
         camera that faces the far boards square, since the boards then share one size). The far
         boards' UVs are then laid out in screen pixels, so this holds for that camera position;
         a camera that pans keeps it only when it moves in whole pixels at the far boards
  EMIT   boards are unlit, so the strip colours arrive exactly as painted instead of being
         darkened by the dusk lights and snapped to muddier palette entries (default True)
  SCALE  extra texels per screen pixel (default 1), FILTER texture sampling (default 'Closest'),
  SAVE   save the blend in place afterwards (default True)"""
import bpy, os
from mathutils import Vector as V
from bpy_extras.object_utils import world_to_camera_view as w2c

# Where art/ is: ART passed as a global (exec), SAP_ART in the environment, or two levels up
# from this script when it is run as a file; no path to anyone's machine is baked in.
def _art_dir():
    a = globals().get('ART') or os.environ.get('SAP_ART')
    if not a and '__file__' in globals(): a = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if not a or not os.path.isdir(a): raise SystemExit('set ART (global) or SAP_ART (env) to the art/ folder')
    return os.path.abspath(a)
ART = _art_dir()
# (name, board colour, letter colour), dusk palette only
ADS = [('ICE COLA', '#c8574a', '#efe0c2'), ('FROSTBANK', '#efe0c2', '#2f5f8f'), ('GLACIER', '#3e5875', '#efe0c2'),
       ('TOP SHELF', '#211b28', '#f2c14e'), ('RINK RADIO', '#e3d8c6', '#a34e4d'), ('VOLT', '#f2c14e', '#211b28'),
       ('COLD BREW', '#613b3a', '#f9da93'), ('BLUE LINE TV', '#efe0c2', '#31556e'), ('SNAPSHOT', '#e96f5d', '#efe0c2'),
       ('NORDIC AIR', '#e3d8c6', '#3e5875')]
FONT = {   # 3x5 glyphs, rows top to bottom
    'A': ['.#.', '#.#', '###', '#.#', '#.#'], 'B': ['##.', '#.#', '##.', '#.#', '##.'], 'C': ['.##', '#..', '#..', '#..', '.##'],
    'D': ['##.', '#.#', '#.#', '#.#', '##.'], 'E': ['###', '#..', '##.', '#..', '###'], 'F': ['###', '#..', '##.', '#..', '#..'],
    'G': ['.##', '#..', '#.#', '#.#', '.##'], 'H': ['#.#', '#.#', '###', '#.#', '#.#'], 'I': ['###', '.#.', '.#.', '.#.', '###'],
    'J': ['..#', '..#', '..#', '#.#', '.#.'], 'K': ['#.#', '#.#', '##.', '#.#', '#.#'], 'L': ['#..', '#..', '#..', '#..', '###'],
    'M': ['#.#', '###', '###', '#.#', '#.#'], 'N': ['##.', '#.#', '#.#', '#.#', '#.#'], 'O': ['.#.', '#.#', '#.#', '#.#', '.#.'],
    'P': ['##.', '#.#', '##.', '#..', '#..'], 'Q': ['.#.', '#.#', '#.#', '##.', '.##'], 'R': ['##.', '#.#', '##.', '#.#', '#.#'],
    'S': ['.##', '#..', '.#.', '..#', '##.'], 'T': ['###', '.#.', '.#.', '.#.', '.#.'], 'U': ['#.#', '#.#', '#.#', '#.#', '###'],
    'V': ['#.#', '#.#', '#.#', '#.#', '.#.'], 'W': ['#.#', '#.#', '###', '###', '#.#'], 'X': ['#.#', '#.#', '.#.', '#.#', '#.#'],
    'Y': ['#.#', '#.#', '.#.', '.#.', '.#.'], 'Z': ['###', '..#', '.#.', '#..', '###'], '&': ['.#.', '#.#', '.#.', '#.#', '.##'],
}
S = int(globals().get('SCALE', 1)); FILTER = globals().get('FILTER', 'Closest')
EMIT = globals().get('EMIT', True); FIT = globals().get('FIT', True)
scene = bpy.context.scene
bpy.context.view_layer.update()                  # a camera moved by the caller must settle before it is measured

texts = [o for o in bpy.data.objects if o.name.startswith('AdText')]
boards = [o for o in bpy.data.objects if o.type == 'MESH' and (o.name == 'Ad' or o.name.startswith('Ad.'))]
def centre(o): return sum((o.matrix_world @ v.co for v in o.data.vertices), V()) / len(o.data.vertices)
def px_size(b):
    """the board's width and height in screen pixels for the scene camera"""
    uv = [w2c(scene, scene.camera, b.matrix_world @ v.co) for v in b.data.vertices]
    return ((max(p.x for p in uv) - min(p.x for p in uv)) * scene.render.resolution_x,
            (max(p.y for p in uv) - min(p.y for p in uv)) * scene.render.resolution_y)
if FIT:
    far = [b for b in boards if centre(b).y > 0 and abs(centre(b).x) < 20]      # the row the broadcast camera reads
    ref = min(far, key=lambda b: abs(centre(b).x))
    w, h = px_size(ref)
    TW, TH = max(48, round(w)) * S, max(8, round(h)) * S
    k = round(h) / h                               # trim every board to a whole number of pixels tall, so no row doubles
    for b in boards:
        cz = sum(v.co.z for v in b.data.vertices) / len(b.data.vertices)
        for v in b.data.vertices: v.co.z = cz + (v.co.z - cz) * k
    bpy.context.view_layer.update()
else:
    TW, TH = 48 * S, 8 * S

def hexrgb(h): return tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))
def strip(text, bg, fg):
    """letters centred in the strip, a 1-texel accent line in the letter colour along the bottom"""
    px = [[bg] * TW for _ in range(TH)]
    w = (sum(2 if c == ' ' else 4 for c in text) - 1) * S
    x = (TW - w) // 2; y0 = max(S, (TH - 6 * S) // 2)
    for c in text:
        if c == ' ': x += 2 * S; continue
        for r, row in enumerate(FONT[c]):
            for k, bit in enumerate(row):
                if bit == '#':
                    for dy in range(S):
                        for dx in range(S): px[y0 + r * S + dy][x + k * S + dx] = fg
        x += 4 * S
    for dy in range(S): px[TH - 1 - dy] = [fg] * TW
    return px

# ---------------- the strip atlas ----------------
rows = []
for name, bg, fg in ADS: rows += strip(name, bg, fg)
H = len(rows)
img = bpy.data.images.get('ads') or bpy.data.images.new('ads', TW, H, alpha=False)
img.scale(TW, H)
flat = []
for y in range(H - 1, -1, -1):              # Blender images start at the bottom row
    for c in rows[y]: flat += [*hexrgb(c), 1.0]
img.pixels = flat
os.makedirs(ART + '/ads', exist_ok=True)
img.filepath_raw = ART + '/ads/ads_%dx%d.png' % (TW, H); img.file_format = 'PNG'; img.save()
img.pack(); img.use_fake_user = True

# ---------------- one material for every board ----------------
m = bpy.data.materials.get('ad_pixel') or bpy.data.materials.new('ad_pixel')
m.use_nodes = True; nt = m.node_tree
for n in list(nt.nodes): nt.nodes.remove(n)
tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = FILTER
out = nt.nodes.new('ShaderNodeOutputMaterial')
if EMIT:
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs['Strength'].default_value = 1.0
    nt.links.new(tex.outputs['Color'], em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs['Surface'])
else:
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled'); bsdf.inputs['Roughness'].default_value = 0.6
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color']); nt.links.new(bsdf.outputs[0], out.inputs['Surface'])

# ---------------- map each board to its sponsor's strip ----------------
names = [a[0] for a in ADS]
done = 0
for b in boards:
    c = centre(b)
    t = min(texts, key=lambda t: (t.matrix_world.translation - c).length) if texts else None
    idx = names.index(t.data.body) if t and t.data.body in names else done % len(ADS)
    me = b.data
    if me.uv_layers and me.materials and me.materials[0].name == 'ad_pixel':     # rerun: keep the sponsor this board already had
        idx = int(round((1 - max(l.uv.y for l in me.uv_layers[0].data)) * len(ADS)))
    me.materials.clear(); me.materials.append(m)
    uv = me.uv_layers.get('UVMap') or me.uv_layers.new(name='UVMap')
    poly = me.polygons[0]
    n = (b.matrix_world.to_3x3() @ poly.normal).normalized()
    if n.dot(V((-c.x, -c.y, 0))) < 0: n = -n            # letters face the rink
    side = V((0, 0, 1)).cross(n).normalized()
    ws = [b.matrix_world @ me.vertices[i].co for i in poly.vertices]
    us = [(w - c).dot(side) for w in ws]; zs = [w.z for w in ws]
    v0, v1 = 1 - (idx + 1) * TH / H, 1 - idx * TH / H
    if FIT and c.y > 0 and abs(n.x) < 1e-3:
        # a board square to the camera: pin texels to screen pixels, measured from the whole pixel
        # nearest its corner, so each pixel centre samples one texel centre and no column doubles.
        # u is read along the board's middle row and v down its centre line, so both stay affine
        # in the board plane and perspective-correct interpolation keeps them on the pixels
        zm = sum(zs) / len(zs)
        sx = [w2c(scene, scene.camera, V((w.x, w.y, zm))).x * scene.render.resolution_x for w in ws]
        sy = [w2c(scene, scene.camera, V((c.x, w.y, w.z))).y * scene.render.resolution_y for w in ws]
        x0, y0 = round(min(sx)), round(min(sy))
        for li, px_, py_ in zip(poly.loop_indices, sx, sy):
            uv.data[li].uv = ((px_ - x0) / TW, v0 + (py_ - y0) / TH * (v1 - v0))
        done += 1
        continue
    for li, w, u_, z_ in zip(poly.loop_indices, ws, us, zs):
        u = (u_ - min(us)) / (max(us) - min(us)); v = (z_ - min(zs)) / (max(zs) - min(zs))
        u = 0.5 / TW + u * (1 - 1 / TW); v = 0.5 / TH + v * (1 - 1 / TH)     # stay on texel centres, never bleed into the next strip
        uv.data[li].uv = (u, v0 + v * (v1 - v0))
    done += 1
for t in texts: bpy.data.objects.remove(t)
if globals().get('SAVE', True): bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
result = {'boards': done, 'texts_removed': len(texts), 'atlas': [TW, H], 'fit': FIT, 'emit': EMIT}
