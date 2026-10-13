# Stick & Puck players v2 (skater, goalie, referee), built the way the design sheet asks: a low-poly model (8-sided
# lofts and boxes, roughly a thousand triangles) whose detail lives in one 64x64 pixel
# texture read with nearest filtering, so it can be repainted in Aseprite. Chunky
# proportions: big helmet, wide shoulders, big gloves. Z up, the player faces -Y, so the
# player's left is +X. Left-handed shot. Same bone names as v1, so scene poses still apply.
import sys
if '/usr/lib/python3.14/site-packages' not in sys.path: sys.path.append('/usr/lib/python3.14/site-packages')
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

# Where art/ is: ART passed as a global (exec), SAP_ART in the environment, or two levels up
# from this script when it is run as a file; no path to anyone's machine is baked in.
def _art_dir():
    a = globals().get('ART') or os.environ.get('SAP_ART')
    if not a and '__file__' in globals(): a = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if not a or not os.path.isdir(a): raise SystemExit('set ART (global) or SAP_ART (env) to the art/ folder')
    return os.path.abspath(a)
ART = _art_dir()
OUT = ART + "/player"
os.makedirs(OUT + "/renders", exist_ok=True)
scene = bpy.context.scene
for ob in list(bpy.data.objects): bpy.data.objects.remove(ob)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.armatures, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.images):
    for d in list(coll): coll.remove(d)
for c in list(scene.collection.children): scene.collection.children.unlink(c)
V = lambda *a: Vector(a)
X, F = V(1, 0, 0), V(0, -1, 0)
TEX = 64
ROLE = globals().get('ROLE', 'skater')            # set ROLE before exec: 'skater', 'goalie' or 'referee'
NAME = ROLE

# texture atlas: pixel rects (x, y from the top, w, h). Each loft part unwraps around its
# axis: u = 0 right side, .25 front, .5 left side, .75 back; rows run from the part's end (top row) to its start
R = {'torso': (0, 0, 32, 20), 'pants': (32, 0, 32, 10), 'thigh': (32, 10, 16, 10), 'shin': (48, 10, 16, 14),
     'upper': (0, 20, 16, 8), 'fore': (16, 20, 16, 8), 'helmet': (32, 24, 32, 8), 'head': (0, 28, 32, 14),
     'glove': (32, 32, 16, 8), 'boot': (48, 32, 16, 8)}
R.update({'blocker': (32, 40, 16, 8), 'catcher': (48, 40, 16, 8), 'pad': (0, 48, 32, 16)})
SW = ['wood', 'tape', 'cream', 'steel', 'boot', 'blue', 'yellow', 'navy']     # 4x4 solid swatches on row 44
for i, k in enumerate(SW): R['sw_' + k] = (i * 4, 44, 4, 4)

KITS = {
    'home': dict(Y='#f2c14e', Yd='#c8963c', B='#2f5f8f', Bd='#2a2c40', num='13'),
    'away': dict(Y='#c8574a', Yd='#a34e4d', B='#2a2c40', Bd='#18131f', num='27'),
}
if ROLE == 'goalie':
    KITS = {'home': dict(KITS['home'], num='1'), 'away': dict(KITS['away'], num='30')}
if ROLE == 'referee':
    KITS = {'ref': dict(Y='#efe0c2', Yd='#bdb9c4', B='#211b28', Bd='#18131f', num='')}
COMMON = dict(S='#f0b48a', Sd='#c47262', H='#613b3a', E='#211b28', C='#efe0c2', W='#d2ad7f',
              K='#2a2230', Kl='#423236', St='#8a8790', T='#211b28')
DIG = {'0': ['###', '#.#', '#.#', '#.#', '###'], '1': ['.#.', '##.', '.#.', '.#.', '###'], '2': ['##.', '..#', '.#.', '#..', '###'],
       '3': ['###', '..#', '.##', '..#', '###'], '4': ['#.#', '#.#', '###', '..#', '..#'], '5': ['###', '#..', '##.', '..#', '##.'],
       '6': ['.##', '#..', '###', '#.#', '###'], '7': ['###', '..#', '.#.', '.#.', '.#.'], '8': ['###', '#.#', '###', '#.#', '###'],
       '9': ['###', '#.#', '###', '..#', '##.']}

def paint_goalie(P, px, fill, near):
    # mask: kit-coloured shell with a stripe, and a cage over the face
    def mask(u, t):
        if near(u, 0.25, 0.17) and t < 0.72:
            return P['C'] if (int(u * 32) % 2 == 0 or int(t * 14) % 3 == 0) else P['E']
        if near(u, 0.75, 0.04) or near(u, 0.25, 0.03): return P['Y']
        return P['B'] if t > 0.15 else P['Bd']
    fill('head', mask)
    fill('helmet', lambda u, t: P['B'])
    # leg pads: cream with two kit bands and a dark knee roll
    fill('pad', lambda u, t: P['B'] if (0.3 < t < 0.38 or 0.6 < t < 0.68) else (P['Kl'] if t > 0.9 else P['C']))
    fill('blocker', lambda u, t: P['B'] if near(u, 0.25, 0.12) and 0.2 < t < 0.8 else P['C'])
    fill('catcher', lambda u, t: P['Y'] if near(u, 0.25, 0.1) and 0.3 < t < 0.7 else (P['B'] if t > 0.85 else P['C']))

def paint_referee(P, px, fill, near):
    # black and white vertical stripes, black collar and cuffs, orange armbands, black trousers
    stripes = lambda u, t: P['B'] if int(u * 32 + 6) % 8 < 4 else P['C']   # 4-texel stripes stay legible at 480x270
    fill('torso', lambda u, t: P['B'] if t > 0.9 or t < 0.06 else stripes(u, t))
    fill('upper', lambda u, t: '#fba771' if 0.4 < t < 0.65 else stripes(u, t))
    fill('fore', lambda u, t: P['B'] if t > 0.8 else stripes(u, t))
    for k in ('pants', 'thigh', 'shin'): fill(k, lambda u, t: '#423236' if near(u, 0.25, 0.04) else '#33293a')
    fill('glove', lambda u, t: P['B'] if t < 0.25 else P['S'])
    def helmet(u, t):   # slate shell, black rim, two grey ridges so it reads as a helmet, not hair
        if t < 0.15: return P['Bd']
        if t > 0.7: return '#555f6b'          # flat sheen band on the crown
        if t > 0.4 and (near(u, 0.18, 0.02) or near(u, 0.32, 0.02)): return '#555f6b'
        return '#2a2c36'
    fill('helmet', helmet)

def paint_atlas(kit):
    # once an Aseprite file exists it is the source: export it and use it, never repaint over it
    src = OUT + '/%s_%s.aseprite' % (NAME, kit)
    if os.path.exists(src):
        import subprocess
        png = OUT + '/%s_%s_64.png' % (NAME, kit)
        subprocess.run(['aseprite', '-b', src, '--save-as', png], check=True, capture_output=True, timeout=120)
        img = bpy.data.images.load(png); img.name = NAME + '_' + kit
        img.pack(); img.use_fake_user = True
        return img
    P = dict(COMMON, **KITS[kit])
    px = [['#211b28'] * TEX for _ in range(TEX)]     # unused space stays dark, free for goalie or extra detail
    def fill(key, fn):
        x0, y0, w, h = R[key]
        for y in range(h):
            for x in range(w):
                px[y0 + y][x0 + x] = fn((x + 0.5) / w, 1 - (y + 0.5) / h)
    near = lambda u, c, half: abs(((u - c + 0.5) % 1.0) - 0.5) < half
    def torso(u, t):    # hem stripes, a blue yoke over the shoulders
        if t < 0.06 or 0.12 < t < 0.2 or 0.24 < t < 0.29 or t > 0.84: return P['B']
        return P['Yd'] if t > 0.8 else P['Y']
    fill('torso', torso)
    x0, y0, w, h = R['torso']
    for dy in range(-2, 3):                          # crest: a blue roundel with a gold centre, on the chest
        for dx in range(-2, 3):
            if dx * dx + dy * dy <= 5: px[y0 + 6 + dy][x0 + 8 + dx] = P['B']
    px[y0 + 6][x0 + 8] = P['Y']
    num = P['num']; nx = 24 - (len(num) * 4 - 1) // 2   # number centred on the back
    for i, ch in enumerate(num):
        for r, row in enumerate(DIG[ch]):
            for c, bit in enumerate(row):
                if bit == '#': px[y0 + 5 + r][x0 + nx + i * 4 + c] = P['B']
    fill('pants', lambda u, t: P['Bd'] if t > 0.8 else (P['Y'] if near(u, 0.0, 0.04) or near(u, 0.5, 0.04) else P['B']))
    fill('thigh', lambda u, t: P['B'] if t < 0.62 else (P['Bd'] if t < 0.7 else P['Y']))
    fill('shin', lambda u, t: P['B'] if (0.22 < t < 0.34 or 0.42 < t < 0.5) else P['Y'])
    fill('upper', lambda u, t: P['B'] if t < 0.3 else P['Y'])
    fill('fore', lambda u, t: P['B'] if (0.5 < t < 0.64 or 0.74 < t < 0.84) else P['Y'])
    fill('helmet', lambda u, t: P['Bd'] if t < 0.2 else (P['Y'] if near(u, 0.25, 0.035) and 0.3 < t < 0.6 else P['B']))
    fill('head', lambda u, t: P['S'] if near(u, 0.25, 0.2) else P['H'])
    hx, hy, hw, hh = R['head']; c0 = hx + 8                               # the face, pixel by pixel
    for x in (c0 - 3, c0 - 2, c0 + 1, c0 + 2): px[hy + 5][x] = P['H']     # brows
    for x in (c0 - 2, c0 + 1): px[hy + 7][x] = P['E']                     # eyes
    px[hy + 8][c0 - 1] = P['Sd']; px[hy + 8][c0] = P['Sd']                # nose
    px[hy + 10][c0 - 1] = P['Sd']; px[hy + 10][c0] = P['Sd']              # mouth
    for x in (hx, hx + 15): px[hy + 7][x] = P['Sd']                       # ears
    fill('glove', lambda u, t: P['Y'] if t < 0.28 else (P['B'] if near(u, 0.75, 0.15) else P['Bd']))
    fill('boot', lambda u, t: P['C'] if near(u, 0.75, 0.05) and 0.3 < t < 0.8 else (P['Kl'] if near(u, 0.5, 0.06) or near(u, 0.0, 0.06) else P['K']))
    for k, col in zip(SW, (P['W'], P['T'], P['C'], P['St'], P['K'], P['B'], P['Y'], P['Bd'])):
        fill('sw_' + k, lambda u, t, col=col: col)
    if ROLE == 'goalie': paint_goalie(P, px, fill, near)
    if ROLE == 'referee': paint_referee(P, px, fill, near)
    img = bpy.data.images.new(NAME + '_' + kit, TEX, TEX, alpha=False)
    flat = []
    for y in range(TEX - 1, -1, -1):                                     # Blender rows run bottom to top
        for x in range(TEX):
            hx_ = px[y][x]; flat += [int(hx_[1:3], 16) / 255, int(hx_[3:5], 16) / 255, int(hx_[5:7], 16) / 255, 1.0]
    img.pixels = flat
    img.filepath_raw = OUT + '/%s_%s_64.png' % (NAME, kit); img.file_format = 'PNG'; img.save()
    img.pack(); img.use_fake_user = True       # keep the away texture even while no material uses it
    return img

IMGS = {k: paint_atlas(k) for k in KITS}
mat = bpy.data.materials.new(NAME + '_kit'); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes['Principled BSDF']; bsdf.inputs['Roughness'].default_value = 0.9
tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = IMGS[next(iter(KITS))]; tex.interpolation = 'Closest'
nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])

def uv_of(key, u, t):
    x0, y0, w, h = R[key]
    t = min(1 - 0.5 / h, max(0.5 / h, t)); u = min(1 - 0.5 / w, max(0.5 / w, u))   # stay on texel centres at the rect edges
    return ((x0 + u * w) / TEX, 1 - (y0 + (1 - t) * h) / TEX)

def new_obj(name, bm, normals=None):
    if normals is not None:
        for f in bm.faces: f.smooth = True
        bm.verts.index_update(); nl = [normals[v].to_tuple() for v in bm.verts]
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    if normals is not None: me.normals_split_custom_set_from_vertices(nl)
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    return ob

CAP_LEAN = 0.6          # how far a loft's end ring normals lean toward its cap (0 = straight out from the axis)

def loft(name, a, b, rings, key, n=8, zoff=None, up=None, e=1.0):
    """rings: (t, rx, ry) along a->b; rx across the body, ry front to back.
    zoff(u, t) moves a ring vertex along the axis (the helmet rim sits lower at the back)."""
    A = b - a; L = A.length; A.normalize()
    ref = up if up is not None else X
    Xp = (ref - A * A.dot(ref)).normalized(); Fp = Xp.cross(A).normalized()
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new()
    grid = []
    for (t, rx, ry) in rings:
        row = []
        for k in range(n + 1):
            u = k / n; ang = 2 * math.pi * u
            cs, sn = math.cos(ang), math.sin(ang)
            d = -math.copysign(abs(cs) ** e, cs) * Xp * rx + math.copysign(abs(sn) ** e, sn) * Fp * ry
            tt = t + (zoff(u, t) if zoff else 0.0)
            row.append((bm.verts.new(a + A * (tt * L) + d), u, t, d.length))
        grid.append(row)
    for i in range(len(grid) - 1):
        for k in range(n):
            q = [grid[i][k], grid[i][k + 1], grid[i + 1][k + 1], grid[i + 1][k]]
            f = bm.faces.new([v for v, _, _, _ in q])
            for lp, (_, u, t, _) in zip(f.loops, q): lp[uvl].uv = uv_of(key, u, t)
    # A8, painted shading: hand-set normals instead of facet normals. Each ring vertex takes the normal
    # of a smooth ellipse round the axis (squared-off parts shade as if rounded too), tilted by how
    # the profile widens or narrows, and the end rings lean toward their caps, so the three toon
    # bands curve round every part instead of stepping face by face.
    nrm = {}
    for i, row in enumerate(grid):
        i0, i1 = max(i - 1, 0), min(i + 1, len(grid) - 1)
        span = max((rings[i1][0] - rings[i0][0]) * L, 1e-4); rx, ry = rings[i][1], rings[i][2]
        for k, (v, u, t, r) in enumerate(row):
            ang = 2 * math.pi * u
            nr = (-math.cos(ang) / rx * Xp + math.sin(ang) / ry * Fp).normalized()
            slope = (grid[i1][k][3] - grid[i0][k][3]) / span
            tip = -CAP_LEAN if i == 0 else (CAP_LEAN if i == len(grid) - 1 else 0.0)
            nrm[v] = (nr - A * slope + A * tip).normalized()
    for row, axis in ((grid[0], -A), (grid[-1], A)):                    # close both ends with a fan
        c = bm.verts.new(sum((v.co for v, _, _, _ in row[:-1]), Vector()) / n); nrm[c] = axis.copy()
        for k in range(n):
            f = bm.faces.new([row[k][0], row[k + 1][0], c])
            for lp in f.loops: lp[uvl].uv = uv_of(key, 0.5, row[0][2])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_obj(name, bm, nrm)

def box(name, M, size, key):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=M @ Matrix.Diagonal((*size, 1)))
    for f in bm.faces:
        for lp in f.loops: lp[uvl].uv = uv_of(key, 0.5, 0.5)
    return new_obj(name, bm)

def frame(x, y, z, c):
    m = Matrix.Identity(4)
    for i in range(3): m[i][0], m[i][1], m[i][2] = x[i], y[i], z[i]
    m.translation = c
    return m

# ---------------- skeleton, chunky proportions ----------------
J = {
    'pelvis': V(0, 0.04, 0.90), 'spine': V(0, -0.03, 1.08), 'chest': V(0, -0.10, 1.25), 'neck': V(0, -0.15, 1.43), 'head': V(0, -0.19, 1.60),
    'shoulder.L': V(0.27, -0.10, 1.35), 'shoulder.R': V(-0.27, -0.10, 1.35),
    'hip.L': V(0.13, 0.05, 0.86), 'knee.L': V(0.17, -0.15, 0.52), 'ankle.L': V(0.18, -0.02, 0.15),
    'hip.R': V(-0.13, 0.05, 0.86), 'knee.R': V(-0.16, -0.11, 0.52), 'ankle.R': V(-0.17, 0.04, 0.15),
}
TOP = V(-0.13, -0.40, 1.02); HEEL = V(0.46, -1.00, 0.0); SHAFT_END = HEEL + V(0, 0, 0.075)
U = (SHAFT_END - TOP).normalized()
J['grip.R'] = TOP + U * 0.07
J['grip.L'] = TOP + U * 0.40
for s in 'LR':
    sx = 1 if s == 'L' else -1
    sh, g = J['shoulder.' + s], J['grip.' + s]
    J['wrist.' + s] = g + (sh - g).normalized() * 0.10
    J['elbow.' + s] = sh.lerp(J['wrist.' + s], 0.5) + V(sx * 0.10, 0.05, -0.05)
FOOT_DIR = {s: V(math.sin(a), -math.cos(a), 0) for s, a in {'L': 0.26, 'R': -0.22}.items()}
if ROLE == 'goalie':      # wide crouch, catcher out to the left, blocker and stick on the right
    J.update({'pelvis': V(0, 0.08, 0.70), 'spine': V(0, 0.0, 0.88), 'chest': V(0, -0.10, 1.05), 'neck': V(0, -0.18, 1.22), 'head': V(0, -0.24, 1.37),
              'shoulder.L': V(0.31, -0.10, 1.14), 'shoulder.R': V(-0.31, -0.10, 1.14),
              'hip.L': V(0.15, 0.08, 0.66), 'knee.L': V(0.34, -0.20, 0.40), 'ankle.L': V(0.38, -0.02, 0.12),
              'hip.R': V(-0.15, 0.08, 0.66), 'knee.R': V(-0.34, -0.20, 0.40), 'ankle.R': V(-0.38, -0.02, 0.12),
              'elbow.L': V(0.44, -0.22, 0.95), 'wrist.L': V(0.50, -0.40, 0.90), 'grip.L': V(0.52, -0.50, 0.90),
              'elbow.R': V(-0.42, -0.22, 0.92), 'wrist.R': V(-0.44, -0.40, 0.80), 'grip.R': V(-0.44, -0.50, 0.76)})
    TOP = V(-0.45, -0.46, 0.86); SHAFT_END = V(-0.18, -0.80, 0.06); U = (SHAFT_END - TOP).normalized()
    FOOT_DIR = {s: V(math.sin(a), -math.cos(a), 0) for s, a in {'L': 0.55, 'R': -0.55}.items()}
if ROLE == 'referee':     # upright, arms relaxed at the sides, no stick
    J.update({'pelvis': V(0, 0.02, 0.95), 'spine': V(0, 0.0, 1.12), 'chest': V(0, -0.03, 1.30), 'neck': V(0, -0.05, 1.48), 'head': V(0, -0.08, 1.64),
              'shoulder.L': V(0.25, -0.03, 1.40), 'shoulder.R': V(-0.25, -0.03, 1.40),
              'hip.L': V(0.12, 0.02, 0.92), 'knee.L': V(0.14, -0.06, 0.53), 'ankle.L': V(0.15, 0.0, 0.15),
              'hip.R': V(-0.12, 0.02, 0.92), 'knee.R': V(-0.14, -0.06, 0.53), 'ankle.R': V(-0.15, 0.0, 0.15),
              'elbow.L': V(0.31, 0.0, 1.12), 'wrist.L': V(0.33, -0.04, 0.90), 'grip.L': V(0.33, -0.07, 0.82),
              'elbow.R': V(-0.31, 0.0, 1.12), 'wrist.R': V(-0.33, -0.04, 0.90), 'grip.R': V(-0.33, -0.07, 0.82)})
    FOOT_DIR = {s: V(math.sin(a), -math.cos(a), 0) for s, a in {'L': 0.15, 'R': -0.15}.items()}
TS = {'goalie': 1.15, 'referee': 0.95}.get(ROLE, 1.0)      # torso bulk: goalie chest protector, slimmer referee
PV = J['pelvis']

# ---------------- body parts ----------------
parts = {}
parts['Torso'] = (loft('Torso', V(0, PV.y, PV.z - 0.02), J['neck'] + V(0, 0, 0.02),
                       [(t, rx * TS, ry * TS) for t, rx, ry in [(0.0, 0.25, 0.19), (0.3, 0.24, 0.18), (0.62, 0.30, 0.21), (0.82, 0.32, 0.20), (0.94, 0.22, 0.15), (1.0, 0.11, 0.09)]], 'torso'), 'spine')
PK = 0.8 if ROLE == 'referee' else 1.0                       # referee trousers are slimmer than hockey pants
parts['Pants'] = (loft('Pants', V(0, PV.y + 0.03, PV.z - 0.18), V(0, PV.y, PV.z + 0.10), [(0.0, 0.26 * PK, 0.21 * PK), (0.5, 0.29 * PK, 0.23 * PK), (1.0, 0.26 * PK, 0.2 * PK)], 'pants'), 'pelvis')
for s in 'LR':
    sx = 1 if s == 'L' else -1
    parts['Thigh.' + s] = (loft('Thigh.' + s, J['hip.' + s], J['knee.' + s], [(-0.05, 0.14, 0.15), (0.5, 0.135, 0.14), (1.05, 0.105, 0.11)], 'thigh'), 'thigh.' + s)
    parts['Shin.' + s] = (loft('Shin.' + s, J['knee.' + s], J['ankle.' + s], [(-0.08, 0.105, 0.115), (0.4, 0.1, 0.11), (1.0, 0.08, 0.085)], 'shin'), 'shin.' + s)
    parts['Upper.' + s] = (loft('Upper.' + s, J['shoulder.' + s] + V(0, 0, 0.02), J['elbow.' + s], [(-0.1, 0.115, 0.11), (0.5, 0.1, 0.1), (1.05, 0.088, 0.088)], 'upper', n=6), 'upperarm.' + s)
    parts['Fore.' + s] = (loft('Fore.' + s, J['elbow.' + s], J['wrist.' + s], [(-0.05, 0.088, 0.088), (1.0, 0.085, 0.085)], 'fore', n=6), 'forearm.' + s)
    gdir = (J['grip.' + s] - J['wrist.' + s]).normalized()
    parts['Glove.' + s] = (loft('Glove.' + s, J['wrist.' + s] - gdir * 0.05, J['grip.' + s] + gdir * 0.07,
                               [(0.0, 0.125, 0.125), (0.3, 0.125, 0.125), (0.35, 0.105, 0.11), (1.0, 0.1, 0.105)], 'glove', n=6), 'hand.' + s)
    d = FOOT_DIR[s]; a = J['ankle.' + s]
    heel, toe = V(a.x, a.y, 0.11) - d * 0.09, V(a.x, a.y, 0.10) + d * 0.22
    parts['Boot.' + s] = (loft('Boot.' + s, heel, toe, [(0.0, 0.06, 0.07), (0.25, 0.065, 0.08), (0.8, 0.06, 0.06), (1.0, 0.045, 0.04)], 'boot', n=6, up=V(0, 0, 1)), 'foot.' + s)
    parts['Cuff.' + s] = (loft('Cuff.' + s, V(a.x, a.y, 0.12), V(a.x, a.y, 0.27), [(0.0, 0.075, 0.09), (1.0, 0.08, 0.09)], 'boot', n=6), 'foot.' + s)
    sd = V(-d.y, d.x, 0); mid = V(a.x, a.y, 0) + d * 0.06
    parts['Holder.' + s] = (box('Holder.' + s, frame(sd, d, V(0, 0, 1), mid + V(0, 0, 0.04)), (0.035, 0.30, 0.05), 'sw_cream'), 'foot.' + s)
    parts['Steel.' + s] = (box('Steel.' + s, frame(sd, d, V(0, 0, 1), mid + V(0, 0, 0.01)), (0.01, 0.32, 0.02), 'sw_steel'), 'foot.' + s)
hc = J['head']
HS = 1.25 if ROLE == 'goalie' else 1.15                            # big-helmet proportions; the goalie mask is bigger still
parts['Head'] = (loft('Head', hc + V(0, 0, -0.15 * HS), hc + V(0, 0, 0.13 * HS),
                      [(0.0, 0.07 * HS, 0.07 * HS), (0.12, 0.115 * HS, 0.12 * HS), (0.45, 0.14 * HS, 0.145 * HS), (0.8, 0.14 * HS, 0.145 * HS), (1.0, 0.1 * HS, 0.1 * HS)], 'head'), 'head')
# hockey helmet: a boxy shell longer front to back, its rim just above the brows in front,
# dropping over the ears at the sides and down to the nape at the back; ear guards and a chin strap
def rim_drop(u, t):
    if t > 0.0: return 0.0
    front = math.sin(2 * math.pi * u)                     # 1 at the front, -1 at the back
    return -0.065 * HS * (1 - front) / (0.2 * HS)
if ROLE != 'goalie': parts['Helmet'] = (loft('Helmet', hc + V(0, 0.005, 0.045 * HS), hc + V(0, 0.005, 0.245 * HS),
                        [(0.0, 0.178 * HS, 0.192 * HS), (0.45, 0.185 * HS, 0.2 * HS), (0.8, 0.172 * HS, 0.19 * HS),
                         (0.93, 0.14 * HS, 0.16 * HS), (1.0, 0.08 * HS, 0.1 * HS)], 'helmet', n=12, zoff=rim_drop, e=0.7), 'head')
for sx in ((1, -1) if ROLE != 'goalie' else ()):
    ear = box('Ear%+d' % sx, Matrix.Translation(hc + V(sx * 0.168 * HS, 0.0, -0.03 * HS)), (0.045, 0.1, 0.11), 'sw_navy')
    parts['Ear%+d' % sx] = (ear, 'head')
    a_, b_ = hc + V(sx * 0.15 * HS, -0.02, -0.085 * HS), hc + V(sx * 0.04, -0.125 * HS, -0.165 * HS)
    dd = (b_ - a_); side_ = dd.cross(V(0, 0, 1)).normalized() if abs(dd.normalized().z) < 0.99 else X
    parts['Strap%+d' % sx] = (box('Strap%+d' % sx, frame(side_, dd.normalized().cross(side_), dd.normalized(), (a_ + b_) / 2), (0.02, 0.02, dd.length), 'sw_tape'), 'head')

# ---------------- goalie gear: leg pads, blocker, catcher ----------------
if ROLE == 'goalie':
    for s_ in 'LR':
        k, an = J['knee.' + s_], J['ankle.' + s_]
        parts['Pad.' + s_] = (loft('Pad.' + s_, k + V(0, -0.05, 0.16), an + V(0, -0.06, -0.06),
                                   [(0.0, 0.15, 0.11), (0.12, 0.17, 0.13), (1.0, 0.15, 0.12)], 'pad', e=0.55), 'shin.' + s_)
    w = J['wrist.R'] + V(-0.10, -0.04, 0.02)
    parts['Blocker'] = (loft('Blocker', w - V(0, 0, 0.17), w + V(0, 0, 0.17), [(0.0, 0.035, 0.12), (1.0, 0.035, 0.12)], 'blocker', e=0.4), 'hand.R')
    w = J['wrist.L']
    parts['Catcher'] = (loft('Catcher', w + V(0.02, 0.0, 0.0), w + V(0.05, -0.2, 0.03),
                             [(0.0, 0.12, 0.12), (0.4, 0.18, 0.17), (1.0, 0.13, 0.12)], 'catcher', e=0.8), 'hand.L')

# ---------------- stick: light wood shaft, dark taped knob and blade (wide paddle for the goalie) ----------------
side = V(-U.y, U.x, 0).normalized(); across = U.cross(side).normalized()
def beam(name, a, b, w, t, key):
    return box(name, frame(across, side, U, (a + b) / 2), (w, t, (b - a).length), key)
stick_parts = []
if ROLE == 'skater':
    stick_parts = [beam('Shaft', TOP, SHAFT_END, 0.036, 0.026, 'sw_wood'), beam('Knob', TOP - U * 0.005, TOP + U * 0.16, 0.044, 0.034, 'sw_tape')]
    bdir = V(U.x, U.y, 0).normalized(); bn = V(-bdir.y, bdir.x, 0)
    if bn.dot(X) > 0: bn = -bn
    stick_parts.append(box('Blade', frame(bn, bdir, V(0, 0, 1), HEEL + bdir * 0.14 + bn * 0.01 + V(0, 0, 0.04)), (0.02, 0.34, 0.075), 'sw_tape'))
if ROLE == 'goalie':
    mid = TOP.lerp(SHAFT_END, 0.55)
    stick_parts = [beam('Shaft', TOP, mid, 0.036, 0.028, 'sw_wood'), beam('Knob', TOP - U * 0.005, TOP + U * 0.1, 0.044, 0.034, 'sw_tape'),
                   beam('Paddle', mid, SHAFT_END, 0.09, 0.03, 'sw_wood')]
    bd = V(1, -0.15, 0).normalized()                      # blade lies flat on the ice toward the goalie's left
    stick_parts.append(box('Blade', frame(V(-bd.y, bd.x, 0), bd, V(0, 0, 1), V(SHAFT_END.x, SHAFT_END.y, 0.045) + bd * 0.2), (0.02, 0.42, 0.09), 'sw_tape'))

# ---------------- armature (same bone names as v1) ----------------
arm = bpy.data.armatures.new('Rig'); rig = bpy.data.objects.new('Rig', arm); scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); eb = arm.edit_bones
def bone(name, h, t, parent=None, connect=False):
    b = eb.new(name); b.head = h; b.tail = t
    if parent: b.parent = eb[parent]; b.use_connect = connect
bone('pelvis', J['pelvis'], J['spine']); bone('spine', J['spine'], J['chest'], 'pelvis', True)
bone('chest', J['chest'], J['neck'], 'spine', True); bone('neck', J['neck'], J['head'], 'chest', True)
bone('head', J['head'], J['head'] + V(0, -0.03, 0.18), 'neck', True)
for s in 'LR':
    sx = 1 if s == 'L' else -1
    bone('clavicle.' + s, J['chest'] + V(sx * 0.05, 0, 0.06), J['shoulder.' + s], 'chest')
    bone('upperarm.' + s, J['shoulder.' + s], J['elbow.' + s], 'clavicle.' + s, True)
    bone('forearm.' + s, J['elbow.' + s], J['wrist.' + s], 'upperarm.' + s, True)
    bone('hand.' + s, J['wrist.' + s], J['grip.' + s], 'forearm.' + s, True)
    bone('thigh.' + s, J['hip.' + s], J['knee.' + s], 'pelvis')
    bone('shin.' + s, J['knee.' + s], J['ankle.' + s], 'thigh.' + s, True)
    bone('foot.' + s, J['ankle.' + s], J['ankle.' + s] + FOOT_DIR[s] * 0.17 + V(0, 0, -0.07), 'shin.' + s, True)
bpy.ops.object.mode_set(mode='OBJECT')

def attach(ob, bname):
    mw = ob.matrix_world.copy()
    ob.parent = rig; ob.parent_type = 'BONE'; ob.parent_bone = bname
    bpy.context.view_layer.update(); ob.matrix_world = mw
for name, (ob, bname) in parts.items(): attach(ob, bname)
for ob in stick_parts: attach(ob, 'hand.R')

tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in scene.objects if o.type == 'MESH')
bpy.ops.wm.save_as_mainfile(filepath=OUT + '/%s.blend' % NAME)
try:
    bpy.ops.export_scene.gltf(filepath=OUT + '/%s.glb' % NAME, export_format='GLB')
    glb = os.path.getsize(OUT + '/%s.glb' % NAME)
except Exception as e:
    glb = 'export failed: %s' % e
result = {'tris': tris, 'meshes': len([o for o in scene.objects if o.type == 'MESH']), 'glb': glb}
