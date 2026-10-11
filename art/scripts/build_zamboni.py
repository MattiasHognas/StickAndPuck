"""Stick & Puck Zamboni (A7): a chunky low-poly ice resurfacer with a seated driver, built the
way the players are: boxes and 8-sided lofts (a few hundred triangles) whose detail lives in one
64x64 pixel texture read with nearest filtering, so it can be repainted in Aseprite.
Blender Z up, the machine drives toward -Y (so it faces +Z in the game, like a player at heading 0),
origin on the ice at the centre of the wheelbase. About 4.6 m long, 2.1 m wide, 2.5 m to the driver's cap.
Writes art/zamboni/zamboni.blend, zamboni.glb (one mesh, no armature, no clips), zamboni_64.png and,
when Aseprite is on the PATH and no source exists yet, zamboni.aseprite (indexed to the dusk palette
with a hidden UV-guides layer). Once zamboni.aseprite exists it is the texture source and is exported, never repainted.
RENDER (global, default True) also renders renders/zamboni_turn_<view>.png (orthographic turnaround)
and renders/zamboni_rink.png, the machine beside a skater from the game's straight camera at 480x270.
Run in Blender:  exec(open(script).read(), {'__file__': script})  or with ART / SAP_ART set."""
import bpy, bmesh, math, os, sys, glob, subprocess, shutil, tempfile
from mathutils import Vector, Matrix
try: import numpy                        # the glTF exporter needs numpy; a Blender run from a venv may not see it
except ImportError: sys.path += glob.glob('/usr/lib/python3*/site-packages')

# Where art/ is: ART passed as a global (exec), SAP_ART in the environment, or two levels up
# from this script when it is run as a file; no path to anyone's machine is baked in.
def _art_dir():
    a = globals().get('ART') or os.environ.get('SAP_ART')
    if not a and '__file__' in globals(): a = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if not a or not os.path.isdir(a): raise SystemExit('set ART (global) or SAP_ART (env) to the art/ folder')
    return os.path.abspath(a)
ART = _art_dir()
OUT = ART + '/zamboni'
RENDER = globals().get('RENDER', True)
os.makedirs(OUT + '/renders', exist_ok=True)
scene = bpy.context.scene
for ob in list(bpy.data.objects): bpy.data.objects.remove(ob)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.armatures, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.images):
    for d in list(coll): coll.remove(d)
for c in list(scene.collection.children): scene.collection.children.unlink(c)
V = lambda *a: Vector(a)
TEX = 64
NAME = 'zamboni'

# ---------------- texture atlas: pixel rects (x, y from the top, w, h) ----------------
R = {'side': (0, 0, 64, 16),       # body side, the length of the machine: cream, blue band, dark skirt
     'end': (0, 16, 32, 16),       # front and back of the body
     'top': (32, 16, 16, 8),       # every upward face of the body
     'tyre': (48, 16, 16, 8),      # tread around the wheel
     'tank': (32, 24, 32, 16),     # the snow tank, cream with a dark lid and vents
     'hub': (0, 32, 8, 8), 'steel': (8, 32, 8, 8), 'seat': (16, 32, 8, 8), 'amber': (24, 32, 8, 8),
     'head': (0, 40, 16, 8),       # the driver's head, face at the front
     'navy': (32, 40, 8, 8), 'jacket': (40, 40, 8, 8), 'skin': (48, 40, 8, 8), 'trouser': (56, 40, 8, 8),
     'cap': (16, 40, 8, 8), 'wheel': (24, 40, 8, 8), 'board': (0, 48, 32, 8)}   # board: a sponsor strip on the tank
# dusk palette entries only (art/palette/stickandpuck_dusk.gpl): no pure white or black
P = dict(cream='#efe0c2', cream_d='#e3d8c6', blue='#2f5f8f', blue_d='#2a2c40', skirt='#33293a', dark='#211b28',
         tyre='#2a2230', tyre_l='#423236', steel='#555f6b', steel_l='#8a8790', amber='#f2c14e', amber_d='#c8963c',
         seat='#423236', skin='#f0b48a', skin_d='#c47262', jacket='#c8574a', jacket_d='#a34e4d', trouser='#33293a',
         eye='#211b28', coral='#e96f5d', lamp='#f9da93')
DIG = {'I': ['###', '.#.', '.#.', '.#.', '###'], 'C': ['.##', '#..', '#..', '#..', '.##'], 'E': ['###', '#..', '##.', '#..', '###'],
       'S': ['.##', '#..', '.#.', '..#', '##.'], 'H': ['#.#', '#.#', '###', '#.#', '#.#'], 'L': ['#..', '#..', '#..', '#..', '###']}

def paint_atlas():
    # once an Aseprite file exists it is the source: export it and use it, never repaint over it
    src = OUT + '/%s.aseprite' % NAME; png = OUT + '/%s_64.png' % NAME
    if os.path.exists(src):
        subprocess.run(['aseprite', '-b', src, '--save-as', png], check=True, capture_output=True, timeout=120)
        img = bpy.data.images.load(png); img.name = NAME; img.pack(); img.use_fake_user = True
        return img
    px = [[P['dark']] * TEX for _ in range(TEX)]
    def fill(key, fn):
        x0, y0, w, h = R[key]
        for y in range(h):
            for x in range(w):
                px[y0 + y][x0 + x] = fn((x + 0.5) / w, 1 - (y + 0.5) / h, x, y)
    def text(key, s, x, y, col):
        x0, y0 = R[key][:2]
        for i, ch in enumerate(s):
            for r, row in enumerate(DIG[ch]):
                for c, bit in enumerate(row):
                    if bit == '#': px[y0 + y + r][x0 + x + i * 4 + c] = col
    def livery(u, t, x, y):          # cream body, a blue band low down, a dark skirt, a thin highlight on top
        if t > 0.92: return P['cream_d']
        if t < 0.18: return P['skirt']
        if 0.22 < t < 0.5: return P['blue'] if not (0.26 < t < 0.3) else P['blue_d']
        return P['cream']
    fill('side', livery); fill('end', livery)
    text('side', 'ICE', 4, 3, P['blue'])           # a generic mark on the flank, no real brand
    fill('top', lambda u, t, x, y: P['cream_d'] if (x + y) % 4 == 0 else P['cream'])
    fill('tyre', lambda u, t, x, y: P['tyre_l'] if (x // 2) % 2 == 0 and 0.15 < t < 0.85 else P['tyre'])
    def tank(u, t, x, y):
        if t > 0.88: return P['steel']                                     # lid
        if 0.55 < t < 0.8 and x % 4 in (1, 2) and 0.3 < u < 0.7: return P['cream_d']   # vent slats
        return P['cream']
    fill('tank', tank)
    fill('hub', lambda u, t, x, y: P['steel'] if (x - 3.5) ** 2 + (y - 3.5) ** 2 > 7 else (P['dark'] if (x - 3.5) ** 2 + (y - 3.5) ** 2 < 1.5 else P['steel_l']))
    for k in ('steel', 'seat', 'amber', 'navy', 'jacket', 'skin', 'trouser', 'cap', 'wheel'):
        col = {'steel': P['steel'], 'seat': P['seat'], 'amber': P['amber'], 'navy': P['blue_d'], 'jacket': P['jacket'],
               'skin': P['skin'], 'trouser': P['trouser'], 'cap': P['blue_d'], 'wheel': P['dark']}[k]
        fill(k, lambda u, t, x, y, col=col: col)
    fill('amber', lambda u, t, x, y: P['lamp'] if 2 <= x <= 5 and 2 <= y <= 5 else P['amber'])
    fill('jacket', lambda u, t, x, y: P['jacket_d'] if y == 0 or x in (3, 4) else P['jacket'])
    fill('head', lambda u, t, x, y: P['skin'] if abs(((u - 0.25 + 0.5) % 1.0) - 0.5) < 0.2 else P['skin_d'])
    hx, hy = R['head'][:2]
    for x in (hx + 2, hx + 5): px[hy + 3][x] = P['eye']
    px[hy + 5][hx + 3] = P['skin_d']; px[hy + 5][hx + 4] = P['skin_d']
    fill('board', lambda u, t, x, y: P['blue_d'] if y in (0, 7) else P['cream_d'])
    text('board', 'SHL', 2, 1, P['blue']); text('board', 'ICE', 17, 1, P['coral'])
    img = bpy.data.images.new(NAME, TEX, TEX, alpha=False)
    flat = []
    for y in range(TEX - 1, -1, -1):
        for x in range(TEX):
            h = px[y][x]; flat += [int(h[1:3], 16) / 255, int(h[3:5], 16) / 255, int(h[5:7], 16) / 255, 1.0]
    img.pixels = flat
    img.filepath_raw = png; img.file_format = 'PNG'; img.save()
    img.pack(); img.use_fake_user = True
    if shutil.which('aseprite'):            # make the editable source: indexed to the dusk palette, UV guides on a hidden layer
        rects = ','.join('{%d,%d,%d,%d}' % r for r in R.values())
        lua = ('local p = app.params\nlocal spr = app.open(p["in"])\nspr:setPalette(Palette{ fromFile = p["pal"] })\n'
               'app.command.ChangePixelFormat{ format = "indexed", dithering = "none" }\nspr.layers[1].name = "paint"\n'
               'local rects = {%s}\nlocal guide = spr:newLayer(); guide.name = "UV guides"; guide.opacity = 160\n'
               'local img = Image(spr.spec)\nlocal idx = 22\nfor _, r in ipairs(rects) do\n'
               '  local x0, y0, w, h = r[1], r[2], r[3], r[4]\n'
               '  for x = x0, x0 + w - 1 do img:drawPixel(x, y0, idx); img:drawPixel(x, y0 + h - 1, idx) end\n'
               '  for y = y0, y0 + h - 1 do img:drawPixel(x0, y, idx); img:drawPixel(x0 + w - 1, y, idx) end\nend\n'
               'spr:newCel(guide, 1, img, Point(0, 0))\nguide.isVisible = false\nspr:saveAs(p["out"])\n') % rects
        with tempfile.NamedTemporaryFile('w', suffix='.lua', delete=False) as f: f.write(lua); lua_path = f.name
        subprocess.run(['aseprite', '-b', '--script-param', 'in=' + png, '--script-param', 'out=' + src,
                        '--script-param', 'pal=' + ART + '/palette/stickandpuck_dusk.gpl', '--script', lua_path],
                       check=True, capture_output=True, timeout=120)
        os.unlink(lua_path)
    return img

IMG = paint_atlas()
mat = bpy.data.materials.new(NAME); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes['Principled BSDF']; bsdf.inputs['Roughness'].default_value = 0.9
tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = IMG; tex.interpolation = 'Closest'
nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])

def uv_of(key, u, t):
    x0, y0, w, h = R[key]
    t = min(1 - 0.5 / h, max(0.5 / h, t)); u = min(1 - 0.5 / w, max(0.5 / w, u))   # stay on texel centres at the rect edges
    return ((x0 + u * w) / TEX, 1 - (y0 + (1 - t) * h) / TEX)

parts = []
def new_obj(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob); parts.append(ob)
    return ob

def box(name, c, size, keys, M=None):
    """An axis-aligned box centred on c (then placed by matrix M when given) with planar UVs per face:
    keys = {'x': side faces, 'y': end faces, 'z': top and bottom} or one key for all. Each face spans its rect fully."""
    if isinstance(keys, str): keys = {'x': keys, 'y': keys, 'z': keys}
    sx, sy, sz = [s / 2 for s in size]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new()
    vs = {}
    for ix in (-1, 1):
        for iy in (-1, 1):
            for iz in (-1, 1): vs[ix, iy, iz] = bm.verts.new(V(ix * sx, iy * sy, iz * sz))
    # each face: its outward normal, its uv key, the four corners counter-clockwise seen from outside, and a planar projection
    faces = [((1, 0, 0), 'x', [(1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)], lambda v: (v.y / sy, v.z / sz)),
             ((-1, 0, 0), 'x', [(-1, 1, -1), (-1, -1, -1), (-1, -1, 1), (-1, 1, 1)], lambda v: (-v.y / sy, v.z / sz)),
             ((0, -1, 0), 'y', [(-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1)], lambda v: (v.x / sx, v.z / sz)),
             ((0, 1, 0), 'y', [(1, 1, -1), (-1, 1, -1), (-1, 1, 1), (1, 1, 1)], lambda v: (-v.x / sx, v.z / sz)),
             ((0, 0, 1), 'z', [(-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)], lambda v: (v.x / sx, v.y / sy)),
             ((0, 0, -1), 'z', [(-1, 1, -1), (1, 1, -1), (1, -1, -1), (-1, -1, -1)], lambda v: (v.x / sx, -v.y / sy))]
    for n, k, corners, proj in faces:
        f = bm.faces.new([vs[cn] for cn in corners])
        for lp in f.loops:
            a, b = proj(lp.vert.co); lp[uvl].uv = uv_of(keys[k], (a + 1) / 2, (b + 1) / 2)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.transform((M if M is not None else Matrix.Identity(4)) @ Matrix.Translation(c))
    return new_obj(name, bm)

def cyl(name, c, axis, r, w, key_side, key_cap, n=8):
    """An n-sided cylinder of radius r and length w along axis (a unit Vector), caps planar over key_cap."""
    A = axis.normalized(); ref = V(0, 0, 1) if abs(A.z) < 0.9 else V(1, 0, 0)
    Xp = (ref - A * A.dot(ref)).normalized(); Yp = A.cross(Xp).normalized()
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new()
    rings = []
    for s in (-0.5, 0.5):
        row = []
        for k in range(n + 1):
            ang = 2 * math.pi * (k % n) / n + math.pi / n
            row.append(bm.verts.new(c + A * (s * w) + (Xp * math.cos(ang) + Yp * math.sin(ang)) * r))
        rings.append(row)
    for k in range(n):
        f = bm.faces.new([rings[0][k], rings[0][k + 1], rings[1][k + 1], rings[1][k]])
        for lp, (u, t) in zip(f.loops, [(k / n, 0), ((k + 1) / n, 0), ((k + 1) / n, 1), (k / n, 1)]): lp[uvl].uv = uv_of(key_side, u, t)
    for row in rings:
        f = bm.faces.new(row[:n])
        for lp in f.loops:
            d = lp.vert.co - c; lp[uvl].uv = uv_of(key_cap, (d.dot(Xp) / r + 1) / 2, (d.dot(Yp) / r + 1) / 2)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_obj(name, bm)

def frame(x, y, z, c):
    m = Matrix.Identity(4)
    for i in range(3): m[i][0], m[i][1], m[i][2] = x[i], y[i], z[i]
    m.translation = c
    return m

def beam(name, a, b, w, t, key):
    """A box from point a to point b (its long axis), w across and t thick."""
    d = (b - a); L = d.length; d.normalize()
    side = d.cross(V(0, 0, 1)).normalized() if abs(d.z) < 0.99 else V(1, 0, 0)
    up = side.cross(d).normalized()
    return box(name, V(0, 0, 0), (w, t, L), key, M=frame(side, up, d, (a + b) / 2))

# ---------------- the machine (Z up, front toward -Y) ----------------
WX, WY, WR, WW = 0.86, 1.45, 0.37, 0.3                       # wheel positions, radius, width
for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
    cyl('Wheel%d' % i, V(sx * WX, sy * WY, WR), V(1, 0, 0), WR, WW, 'tyre', 'hub')
box('Chassis', V(0, 0.05, 0.74), (1.9, 4.3, 0.78), {'x': 'side', 'y': 'end', 'z': 'top'})      # z 0.35 .. 1.13
box('Bumper', V(0, -2.25, 0.55), (1.95, 0.2, 0.32), 'steel')
for sx in (-1, 1):
    box('Lamp%+d' % sx, V(sx * 0.68, -2.26, 0.98), (0.3, 0.14, 0.2), 'amber')
box('Tank', V(0, -1.05, 1.6), (1.96, 2.3, 0.96), {'x': 'tank', 'y': 'tank', 'z': 'top'})       # snow tank, z 1.12 .. 2.08
box('TankLid', V(0, -1.05, 2.1), (1.7, 2.0, 0.08), 'steel')
box('Board', V(0, -1.05, 1.72), (2.0, 1.8, 0.3), {'x': 'board', 'y': 'board', 'z': 'board'})   # sponsor strip around the tank
box('Dash', V(0, 0.25, 1.4), (1.75, 0.3, 0.55), {'x': 'tank', 'y': 'tank', 'z': 'steel'})     # dashboard behind the tank
box('Seat', V(0.32, 1.55, 1.27), (0.6, 0.55, 0.28), 'seat')
box('SeatBack', V(0.32, 1.85, 1.6), (0.6, 0.14, 0.5), 'seat')
box('Rail', V(-0.6, 1.0, 1.33), (0.12, 1.9, 0.4), 'steel')                                     # left-side rail of the deck
for sy in (0.7, 2.05):                                                                          # roll bar behind the driver
    for sx in (-1, 1): box('Post%+d_%d' % (sx, int(sy * 10)), V(sx * 0.9, sy, 1.85), (0.1, 0.1, 1.45), 'navy')
box('Bar', V(0, 2.05, 2.62), (1.9, 0.1, 0.1), 'navy'); box('Bar2', V(0, 0.7, 2.62), (1.9, 0.1, 0.1), 'navy')
for sx in (-1, 1): box('BarSide%+d' % sx, V(sx * 0.9, 1.375, 2.62), (0.1, 1.45, 0.1), 'navy')
box('Beacon', V(0, 2.05, 2.78), (0.26, 0.26, 0.22), 'amber')
box('WashTank', V(-0.45, 1.9, 1.33), (0.75, 0.7, 0.4), {'x': 'tank', 'y': 'tank', 'z': 'steel'})   # wash-water tank left of the seat
box('Conditioner', V(0, 2.35, 0.2), (2.15, 0.5, 0.26), 'steel')                                  # the blade and towel box at the ice
box('Spreader', V(0, 2.55, 0.42), (2.0, 0.15, 0.2), 'navy')
# steering wheel: a thin 8-sided disc tilted toward the driver
tilt = Matrix.Rotation(math.radians(-55), 4, 'X')
sw = cyl('Wheel', V(0, 0, 0), V(0, 0, 1), 0.24, 0.05, 'wheel', 'wheel'); sw.matrix_world = Matrix.Translation(V(0.32, 0.72, 1.62)) @ tilt
cyl('Column', V(0.32, 0.55, 1.35), V(0, 0.55, 0.8), 0.04, 0.6, 'steel', 'steel', n=6)

# ---------------- the driver, seated, hands on the wheel ----------------
HIP = V(0.32, 1.55, 1.45)
box('Hips', HIP + V(0, 0, 0.02), (0.5, 0.42, 0.22), 'trouser')
box('Torso', HIP + V(0, 0.08, 0.38), (0.52, 0.34, 0.5), 'jacket')
for sx in (-1, 1):
    beam('Thigh%+d' % sx, HIP + V(sx * 0.14, -0.1, 0.02), HIP + V(sx * 0.15, -0.6, 0.0), 0.2, 0.2, 'trouser')
    beam('Shin%+d' % sx, HIP + V(sx * 0.15, -0.6, 0.0), HIP + V(sx * 0.15, -0.66, -0.42), 0.17, 0.17, 'trouser')
    box('Boot%+d' % sx, HIP + V(sx * 0.15, -0.74, -0.46), (0.17, 0.3, 0.12), 'navy')
    sh = HIP + V(sx * 0.32, 0.08, 0.56); el = HIP + V(sx * 0.36, -0.22, 0.42); hand = V(0.32 + sx * 0.2, 0.78, 1.72)
    beam('Upper%+d' % sx, sh, el, 0.16, 0.16, 'jacket'); beam('Fore%+d' % sx, el, hand, 0.13, 0.13, 'jacket')
    box('Hand%+d' % sx, hand, (0.14, 0.12, 0.14), 'skin')
neck = HIP + V(0, 0.06, 0.66)
box('Neck', neck, (0.14, 0.14, 0.1), 'skin')
head = box('Head', neck + V(0, 0, 0.2), (0.32, 0.3, 0.32), {'x': 'head', 'y': 'head', 'z': 'skin'})
box('Cap', neck + V(0, -0.02, 0.38), (0.36, 0.34, 0.1), 'cap')
box('Peak', neck + V(0, -0.25, 0.35), (0.3, 0.16, 0.04), 'cap')

tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in parts)
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join(); body = bpy.context.view_layer.objects.active; body.name = body.data.name = 'Zamboni'
bbox = [body.matrix_world @ V(*c) for c in body.bound_box]
size = tuple(round(max(v[i] for v in bbox) - min(v[i] for v in bbox), 2) for i in range(3))
bpy.ops.wm.save_as_mainfile(filepath=OUT + '/%s.blend' % NAME)
try:
    bpy.ops.export_scene.gltf(filepath=OUT + '/%s.glb' % NAME, export_format='GLB', export_apply=True, export_animations=False, export_skins=False)
    glb = os.path.getsize(OUT + '/%s.glb' % NAME)
except Exception as e:
    glb = 'export failed: %s' % e
result = {'tris': tris, 'glb': glb, 'size_blender_xyz_m': size}

# ---------------- renders: turnaround and a rink-distance view beside a skater ----------------
def toonify(m):
    nt = m.node_tree
    img = next(n.image for n in nt.nodes if n.type == 'TEX_IMAGE')
    for n in list(nt.nodes): nt.nodes.remove(n)
    tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
    diff = nt.nodes.new('ShaderNodeBsdfDiffuse'); s2r = nt.nodes.new('ShaderNodeShaderToRGB')
    ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements
    els[0].position = 0.0; els[0].color = (0.42, 0.40, 0.48, 1)
    els[1].position = 0.22; els[1].color = (0.70, 0.68, 0.72, 1)
    e = els.new(0.55); e.color = (1, 1, 1, 1)
    mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1.0
    em = nt.nodes.new('ShaderNodeEmission'); out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(diff.outputs[0], s2r.inputs[0]); nt.links.new(s2r.outputs['Color'], ramp.inputs['Fac'])
    nt.links.new(tex.outputs['Color'], mul.inputs[6]); nt.links.new(ramp.outputs['Color'], mul.inputs[7])
    nt.links.new(mul.outputs[2], em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs['Surface'])

if RENDER:
    toonify(mat)
    sun = bpy.data.lights.new('Key', 'SUN'); sun.energy = 3.0; sun.angle = 0.0
    so = bpy.data.objects.new('Key', sun); scene.collection.objects.link(so)
    so.rotation_euler = (V(0, 0, 0) - V(-0.45, -0.5, 0.75)).to_track_quat('-Z', 'Y').to_euler()   # the game's lightDir (-0.45, 0.75, 0.5), Y-up
    scene.world = scene.world or bpy.data.worlds.new('W'); scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.0
    scene.render.engine = 'BLENDER_EEVEE'; scene.eevee.taa_render_samples = 1
    scene.render.resolution_percentage = 100; scene.render.filter_size = 0.0; scene.render.use_freestyle = False
    scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
    scene.render.image_settings.color_mode = 'RGBA'
    shots = {}
    # turnaround: orthographic, slightly from above, transparent background
    cam_d = bpy.data.cameras.new('Turn'); cam_d.type = 'ORTHO'; cam_d.ortho_scale = 6.0
    cam = bpy.data.objects.new('Turn', cam_d); scene.collection.objects.link(cam); scene.camera = cam
    el = math.radians(18); tgt = V(0, 0, 1.3)
    cam.location = tgt + V(0, -20 * math.cos(el), 20 * math.sin(el))
    cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.resolution_x = scene.render.resolution_y = 128; scene.render.film_transparent = True
    for name, deg in [('front', 0), ('front34', 35), ('side', 90), ('back34', 145), ('back', 180)]:
        body.rotation_euler = (0, 0, math.radians(deg))
        scene.render.filepath = '%s/renders/%s_turn_%s.png' % (OUT, NAME, name)
        bpy.ops.render.render(write_still=True); shots['turn_' + name] = os.path.getsize(scene.render.filepath)
    # rink distance: the game's straight camera (Raylib (0, 17.5, 29.67), 28.2 degrees down, 50 mm on 24 mm) at 480x270,
    # the machine turned three-quarter on a flat ice plane with a skater beside it for scale
    body.rotation_euler = (0, 0, math.radians(35)); body.location = V(0, 3.0, 0)
    ice = bpy.data.materials.new('Ice'); ice.use_nodes = True
    n = ice.node_tree.nodes; n['Principled BSDF'].inputs['Base Color'].default_value = (0.49, 0.47, 0.54, 1)
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30)
    me = bpy.data.meshes.new('Ice'); bm.to_mesh(me); bm.free(); me.materials.append(ice)
    ob = bpy.data.objects.new('Ice', me); scene.collection.objects.link(ob); ob.scale = (1, 0.425, 1)
    sk = ART + '/player/skater.blend'
    if os.path.exists(sk):
        with bpy.data.libraries.load(sk, link=False) as (src, dst): dst.objects = list(src.objects)
        for o in dst.objects:
            if o is None: continue
            scene.collection.objects.link(o)
            if o.type == 'MESH':
                for m in o.data.materials:
                    if m and m.use_nodes and any(x.type == 'TEX_IMAGE' for x in m.node_tree.nodes) and not any(x.type == 'EMISSION' for x in m.node_tree.nodes): toonify(m)
        rig = next((o for o in dst.objects if o and o.type == 'ARMATURE'), None)
        if rig: rig.location = V(3.4, 3.0, 0); rig.rotation_euler = (0, 0, math.radians(-30))
    gcam_d = bpy.data.cameras.new('Game'); gcam_d.sensor_fit = 'VERTICAL'; gcam_d.sensor_height = 24; gcam_d.lens = 50
    gcam = bpy.data.objects.new('Game', gcam_d); scene.collection.objects.link(gcam); scene.camera = gcam
    gcam.location = (0, -29.67, 17.5); gcam.rotation_euler = (math.radians(90 - 28.2), 0, 0)
    scene.render.resolution_x, scene.render.resolution_y = 480, 270; scene.render.film_transparent = False
    scene.render.filepath = '%s/renders/%s_rink.png' % (OUT, NAME)
    bpy.ops.render.render(write_still=True); shots['rink'] = os.path.getsize(scene.render.filepath)
    result['renders'] = shots
