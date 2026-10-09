# Stick & Puck: broadcast still. Rink, arena and two teams built from skater_swe.blend, rendered in EEVEE.
# Blender is Z up; the broadcast camera sits at -Y looking toward +Y, so the far boards are at +Y.
import sys
if '/usr/lib/python3.14/site-packages' not in sys.path: sys.path.append('/usr/lib/python3.14/site-packages')
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix

random.seed(7)
# Where art/ is: ART passed as a global (exec), SAP_ART in the environment, or two levels up
# from this script when it is run as a file; no path to anyone's machine is baked in.
def _art_dir():
    a = globals().get('ART') or os.environ.get('SAP_ART')
    if not a and '__file__' in globals(): a = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if not a or not os.path.isdir(a): raise SystemExit('set ART (global) or SAP_ART (env) to the art/ folder')
    return os.path.abspath(a)
ART = _art_dir()
ROOT = ART
OUT = ROOT + "/scene"
SKATER = ROOT + "/player/skater_swe.blend"
os.makedirs(OUT + "/renders", exist_ok=True)
scene = bpy.context.scene
for ob in list(bpy.data.objects): bpy.data.objects.remove(ob)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.armatures, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
    for d in list(coll): coll.remove(d)
for c in list(scene.collection.children): scene.collection.children.unlink(c)
V = lambda *a: Vector(a)

def lin(h):
    h = h.lstrip('#'); out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (*out, 1.0)

def mat(name, hex_, rough=0.8, metal=0.0, alpha=None, emit=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = lin(hex_); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metal
    m.diffuse_color = lin(hex_)
    if alpha is not None:
        b.inputs['Alpha'].default_value = alpha
        try: m.surface_render_method = 'BLENDED'
        except Exception: m.blend_method = 'BLEND'
    if emit:
        b.inputs['Emission Color'].default_value = lin(emit); b.inputs['Emission Strength'].default_value = 1.0
    return m

def coll(name):
    c = bpy.data.collections.new(name); scene.collection.children.link(c); return c
C_RINK, C_CROWD, C_PLAYERS = coll('Rink'), coll('Crowd'), coll('Players')

def mesh_obj(name, bm, mats, c):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    ob = bpy.data.objects.new(name, me); c.objects.link(ob); return ob

# ---------------- rink outline helpers ----------------
L, W, R = 60.0, 25.5, 8.5
HL, HW = L / 2, W / 2
def rr(off, per=12):
    """rounded-rectangle outline of the rink, grown outward by off, counter-clockwise"""
    pts = []; r = R + off
    for cx, cy, a0 in ((HL - R, HW - R, 0), (-(HL - R), HW - R, 90), (-(HL - R), -(HW - R), 180), (HL - R, -(HW - R), 270)):
        for i in range(per + 1):
            a = math.radians(a0 + 90 * i / per); pts.append(V(cx + math.cos(a) * r, cy + math.sin(a) * r, 0))
    return pts

def ring(bm, off0, off1, z0, z1, mi=0):
    """closed wall/ledge between two outlines: top, inner face and outer face"""
    a, b = rr(off0), rr(off1); n = len(a)
    A0 = [bm.verts.new(p + V(0, 0, z0)) for p in a]; A1 = [bm.verts.new(p + V(0, 0, z1)) for p in a]
    B0 = [bm.verts.new(p + V(0, 0, z0)) for p in b]; B1 = [bm.verts.new(p + V(0, 0, z1)) for p in b]
    for i in range(n):
        j = (i + 1) % n
        for f in ((A1[i], B1[i], B1[j], A1[j]), (A0[i], A1[i], A1[j], A0[j]), (B0[j], B1[j], B1[i], B0[i])):
            bm.faces.new(f).material_index = mi

# ---------------- materials ----------------
M = {
    'ice': mat('ice', '#b3b8d2', 0.11), 'red_line': mat('red_line', '#c8414f', 0.3), 'blue_line': mat('blue_line', '#3a5bb0', 0.3),
    'crease': mat('crease', '#8fb0e8', 0.3), 'boards': mat('boards', '#f3f4f7', 0.5), 'kick': mat('kick', '#ffcd00', 0.5), 'cap': mat('cap', '#005bac', 0.5),
    'glass': mat('glass', '#d8e8ff', 0.05, alpha=0.12), 'post': mat('post', '#d5dae4', 0.4), 'stand_a': mat('stand_a', '#1a1f33', 0.9), 'stand_b': mat('stand_b', '#20263c', 0.9),
    'floor': mat('floor', '#0d1019', 0.9), 'net_frame': mat('net_frame', '#c8202e', 0.4), 'net': mat('net', '#f2f2f2', 0.8), 'puck': mat('puck', '#101012', 0.4),
    'fascia': mat('fascia', '#0f1322', 0.8), 'led': mat('led', '#2b5fd0', 0.5, emit='#2b5fd0'),
}

# ---------------- ice and markings ----------------
bm = bmesh.new(); vs = [bm.verts.new(p) for p in rr(0.0)]; bm.faces.new(vs)
ice = mesh_obj('Ice', bm, [M['ice']], C_RINK)
floor_bm = bmesh.new(); bmesh.ops.create_grid(floor_bm, x_segments=1, y_segments=1, size=120, matrix=Matrix.Translation((0, 0, -0.6)))
mesh_obj('Floor', floor_bm, [M['floor']], C_RINK)

def half_chord(x):
    dx = abs(x) - (HL - R)
    return HW if dx <= 0 else (HW - R) + math.sqrt(max(0.0, R * R - dx * dx))
def strip(bm, x, width, mi, z):
    h = half_chord(x) - 0.02
    vs = [bm.verts.new(V(x + sx * width / 2, sy * h, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    bm.faces.new(vs).material_index = mi
def annulus(bm, cx, cy, r0, r1, mi, z, a0=0.0, a1=360.0, n=64):
    steps = max(4, int(n * (a1 - a0) / 360))
    inner, outer = [], []
    for i in range(steps + 1):
        a = math.radians(a0 + (a1 - a0) * i / steps)
        inner.append(bm.verts.new(V(cx + math.cos(a) * r0, cy + math.sin(a) * r0, z))); outer.append(bm.verts.new(V(cx + math.cos(a) * r1, cy + math.sin(a) * r1, z)))
    for i in range(steps): bm.faces.new((inner[i], outer[i], outer[i + 1], inner[i + 1])).material_index = mi
def disc(bm, cx, cy, r, mi, z, a0=0.0, a1=360.0, n=40):
    steps = max(4, int(n * (a1 - a0) / 360)); c = bm.verts.new(V(cx, cy, z))
    ring_ = [bm.verts.new(V(cx + math.cos(math.radians(a0 + (a1 - a0) * i / steps)) * r, cy + math.sin(math.radians(a0 + (a1 - a0) * i / steps)) * r, z)) for i in range(steps + 1)]
    for i in range(steps): bm.faces.new((c, ring_[i], ring_[i + 1])).material_index = mi

bm = bmesh.new()  # 0 red, 1 blue, 2 crease
strip(bm, 0, 0.3, 0, 0.004)
for x in (-7.62, 7.62): strip(bm, x, 0.3, 1, 0.004)
for x in (-26.7, 26.7): strip(bm, x, 0.05, 0, 0.004)
annulus(bm, 0, 0, 4.52, 4.57, 1, 0.005); disc(bm, 0, 0, 0.15, 1, 0.006)
for sx in (-1, 1):
    disc(bm, sx * 26.7 - sx * 0.0, 0, 1.83, 2, 0.003, *((90, 270) if sx > 0 else (-90, 90)))
    for sy in (-1, 1):
        annulus(bm, sx * 20.1, sy * 6.7, 4.52, 4.57, 0, 0.005); disc(bm, sx * 20.1, sy * 6.7, 0.3, 0, 0.006)
        disc(bm, sx * 6.1, sy * 6.7, 0.3, 0, 0.006)
mesh_obj('Markings', bm, [M['red_line'], M['blue_line'], M['crease']], C_RINK)
# centre ice mark
sys.path.insert(0, ART + '/scripts'); from centre_logo import build_logo
build_logo(C_RINK, M['blue_line'])

# ---------------- boards, ads, glass, posts ----------------
bm = bmesh.new(); ring(bm, 0.0, 0.2, 0.0, 0.22, 1); ring(bm, 0.0, 0.2, 0.22, 1.07, 0); ring(bm, -0.02, 0.26, 1.07, 1.15, 2)
mesh_obj('Boards', bm, [M['boards'], M['kick'], M['cap']], C_RINK)
bm = bmesh.new(); ring(bm, 0.1, 0.12, 1.15, 2.95, 0); mesh_obj('Glass', bm, [M['glass']], C_RINK)
bm = bmesh.new()
path = rr(0.11, 40); acc = 0.0
for i in range(len(path)):
    p, q = path[i], path[(i + 1) % len(path)]; seg = (q - p).length
    while acc < seg:
        c = p.lerp(q, acc / seg); bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(c + V(0, 0, 2.05)) @ Matrix.Diagonal((0.07, 0.07, 1.8, 1)))
        acc += 2.6
    acc -= seg
mesh_obj('Posts', bm, [M['post']], C_RINK)

ADS = [('ICE COLA', '#c8202e', '#ffffff'), ('FROSTBANK', '#ffffff', '#1c2f6e'), ('GLACIER', '#1f4fa8', '#ffffff'), ('TOP SHELF', '#111522', '#f0c050'),
       ('RINK RADIO', '#ffffff', '#c8202e'), ('VOLT', '#f0c050', '#111522'), ('COLD BREW', '#3a2a22', '#f2e2c8'), ('BLUE LINE TV', '#ffffff', '#1f4fa8'),
       ('SNAPSHOT', '#e94c2f', '#ffffff'), ('NORDIC AIR', '#ffffff', '#3a5bb0')]
ad_mats = {}
def ad_panel(center, normal, width, idx):
    txt, bg, fg = ADS[idx % len(ADS)]
    key = bg + fg
    if key not in ad_mats: ad_mats[key] = (mat('ad_bg_' + str(len(ad_mats)), bg, 0.6), mat('ad_fg_' + str(len(ad_mats)), fg, 0.6))
    mb, mf = ad_mats[key]
    up = V(0, 0, 1); side = up.cross(normal).normalized()
    bm = bmesh.new()
    vs = [bm.verts.new(center + side * sx * width / 2 + up * sz * 0.36) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    bm.faces.new(vs); ob = mesh_obj('Ad', bm, [mb], C_RINK)
    cu = bpy.data.curves.new('adtxt', 'FONT'); cu.body = txt; cu.size = 0.42; cu.align_x = 'CENTER'; cu.align_y = 'CENTER'; cu.materials.append(mf)
    t = bpy.data.objects.new('AdText', cu); C_RINK.objects.link(t)
    t.matrix_world = Matrix((side.to_4d(), up.to_4d(), normal.to_4d(), (0, 0, 0, 1))).transposed() @ Matrix.Identity(4)
    t.location = center + normal * 0.004
    t.scale = (min(1.0, width * 0.85 / (0.42 * 0.62 * len(txt))),) * 3
straight = HL - R
for k in range(10):   # far side, readable from the broadcast camera
    x = -straight + (k + 0.5) * (2 * straight / 10); ad_panel(V(x, HW - 0.005, 0.62), V(0, -1, 0), 2 * straight / 10 - 0.02, k)
for k in range(10):   # near side
    x = -straight + (k + 0.5) * (2 * straight / 10); ad_panel(V(x, -HW + 0.005, 0.62), V(0, 1, 0), 2 * straight / 10 - 0.02, k + 5)
for sx in (-1, 1):
    for k in range(2):
        y = -(HW - R) + (k + 0.5) * (HW - R); ad_panel(V(sx * (HL - 0.005), y, 0.62), V(-sx, 0, 0), HW - R - 0.02, k + 3 + sx)

# ---------------- nets ----------------
for sx in (1, -1):
    bm = bmesh.new(); gx = sx * 26.7
    def pipe(a, b, r=0.03):
        d = b - a; bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=r, radius2=r, depth=d.length,
            matrix=Matrix.Translation((a + b) / 2) @ Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4())
    pipe(V(gx, -0.915, 0), V(gx, -0.915, 1.22)); pipe(V(gx, 0.915, 0), V(gx, 0.915, 1.22)); pipe(V(gx, -0.915, 1.22), V(gx, 0.915, 1.22))
    back = gx + sx * 1.0
    pipe(V(gx, -0.915, 0.02), V(back, -0.6, 0.02), 0.02); pipe(V(gx, 0.915, 0.02), V(back, 0.6, 0.02), 0.02); pipe(V(back, -0.6, 0.02), V(back, 0.6, 0.02), 0.02)
    mesh_obj('NetFrame', bm, [M['net_frame']], C_RINK)
    bm = bmesh.new()
    corners = [V(gx, -0.915, 1.22), V(gx, 0.915, 1.22), V(back, 0.6, 0.02), V(back, -0.6, 0.02)]
    vs = [bm.verts.new(c) for c in corners]; bm.faces.new(vs)
    for y in (-1, 1):
        vs = [bm.verts.new(c) for c in (V(gx, y * 0.915, 0.0), V(gx, y * 0.915, 1.22), V(back, y * 0.6, 0.02))]; bm.faces.new(vs)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=10, use_grid_fill=True)
    net = mesh_obj('Net', bm, [M['net']], C_RINK)
    wf = net.modifiers.new('wire', 'WIREFRAME'); wf.thickness = 0.012

# ---------------- stands and crowd ----------------
ROWS = 11
bm = bmesh.new()
for i in range(ROWS):
    o0 = 1.6 + i * 0.9; top = 0.5 + i * 0.55
    ring(bm, o0, o0 + 0.9, 0.0, top, i % 2)
ring(bm, 1.6 + ROWS * 0.9, 1.6 + ROWS * 0.9 + 0.5, 0.0, 0.5 + ROWS * 0.55 + 1.6, 2)
stands = mesh_obj('Stands', bm, [M['stand_a'], M['stand_b'], M['fascia']], C_CROWD)
bm = bmesh.new(); ring(bm, 1.6 + ROWS * 0.9 - 0.02, 1.6 + ROWS * 0.9, 0.5 + ROWS * 0.55 + 0.7, 0.5 + ROWS * 0.55 + 1.0, 0); mesh_obj('LED', bm, [M['led']], C_CROWD)

SHIRTS = ['#ffcd00', '#ffcd00', '#ffcd00', '#005bac', '#005bac', '#c8202e', '#e8ecf4', '#2a2d3a', '#3b3f52', '#5a6a8a', '#14213d']
SKINS = ['#e9b48e', '#d39a72', '#a8704c', '#f2c9a8']
dim = lambda h: '#%02x%02x%02x' % tuple(int(int(h[i:i + 2], 16) * 0.62) for i in (1, 3, 5))
fan_mats = [mat('shirt%d' % i, dim(h), 0.9) for i, h in enumerate(SHIRTS)] + [mat('skin%d' % i, h, 0.8) for i, h in enumerate(SKINS)]
bm = bmesh.new(); fans = 0
for i in range(ROWS):
    o = 1.6 + i * 0.9 + 0.5; top = 0.5 + i * 0.55
    pts = rr(o, 40); acc = 0.0
    for k in range(len(pts)):
        p, q = pts[k], pts[(k + 1) % len(pts)]; seg = (q - p).length
        while acc < seg:
            c = p.lerp(q, acc / seg); acc += 0.55
            if random.random() < 0.05: continue
            c = c + V(random.uniform(-0.04, 0.04), random.uniform(-0.04, 0.04), top)
            lift = 0.25 if random.random() < 0.12 else random.uniform(-0.04, 0.06)
            ret = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(c + V(0, 0, 0.36 + lift)) @ Matrix.Diagonal((0.34 * random.uniform(0.85, 1.1), 0.26, 0.44 * random.uniform(0.85, 1.1), 1)))
            si = random.randrange(len(SHIRTS))
            for f in {f for v in ret['verts'] for f in v.link_faces}: f.material_index = si
            ret = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.13, matrix=Matrix.Translation(c + V(0, 0, 0.72 + lift)))
            ki = len(SHIRTS) + random.randrange(len(SKINS))
            for f in {f for v in ret['verts'] for f in v.link_faces}: f.material_index = ki
            fans += 1
        acc -= seg
mesh_obj('Fans', bm, fan_mats, C_CROWD)

# ---------------- players: append the Swedish skater, recolour the away side ----------------
def toonify(m):
    """flat 3-step shading from the scene lights, tinted by the material's own colour"""
    nt = m.node_tree; base = nt.nodes['Principled BSDF'].inputs['Base Color'].default_value[:]
    for n in list(nt.nodes): nt.nodes.remove(n)
    diff = nt.nodes.new('ShaderNodeBsdfDiffuse'); diff.inputs['Color'].default_value = (1, 1, 1, 1)
    s2r = nt.nodes.new('ShaderNodeShaderToRGB'); ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements; els[0].position = 0.0; els[0].color = (0.36, 0.36, 0.36, 1); els[1].position = 0.22; els[1].color = (0.66, 0.66, 0.66, 1)
    e = els.new(0.55); e.color = (1, 1, 1, 1)
    mul = nt.nodes.new('ShaderNodeVectorMath'); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = base[:3]
    em = nt.nodes.new('ShaderNodeEmission'); out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(diff.outputs[0], s2r.inputs[0]); nt.links.new(s2r.outputs['Color'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], mul.inputs[0]); nt.links.new(mul.outputs[0], em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs['Surface'])

AWAY = {'yellow': '#c8202e', 'blue': '#14213d'}
def add_player(team, pos, face_deg, pose=None):
    with bpy.data.libraries.load(SKATER, link=False) as (src, dst):
        dst.objects = list(src.objects)
    rig = None; seen = set()
    for ob in dst.objects:
        C_PLAYERS.objects.link(ob)
        if ob.type == 'ARMATURE': rig = ob
        if ob.type == 'MESH':
            for m in ob.data.materials:
                if m and m.name not in seen:
                    seen.add(m.name)
                    stem = m.name.split('.')[0]
                    if team == 'away' and stem in AWAY:
                        m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = lin(AWAY[stem])
                    toonify(m)
    rig.location = pos; rig.rotation_euler = (0, 0, math.radians(face_deg)); rig.scale = (1.3, 1.3, 1.3)
    if pose:
        for bname, rot in pose.items():
            pb = rig.pose.bones[bname]; pb.rotation_mode = 'XYZ'; pb.rotation_euler = rot
    return rig

# facing angle: the model faces -Y; +90 faces +X (attacking right), -90 faces -X
STRIDE_L = {'thigh.L': (0.45, 0, 0), 'shin.L': (-0.25, 0, 0), 'thigh.R': (-0.35, 0, 0)}
STRIDE_R = {'thigh.R': (0.45, 0, 0), 'shin.R': (-0.25, 0, 0), 'thigh.L': (-0.35, 0, 0)}
LEAN = {'spine': (0.15, 0, 0), 'chest': (0.1, 0, 0)}
home = [((9.0, -1.0, 0), 80, None), ((15.5, 7.0, 0), 120, STRIDE_L), ((5.0, -7.5, 0), 60, STRIDE_R), ((2.5, 4.0, 0), 95, LEAN), ((17.0, -8.0, 0), 140, None)]
away = [((13.2, -0.4, 0), -100, LEAN), ((19.5, 3.8, 0), -120, None), ((18.5, -5.5, 0), -60, STRIDE_L), ((8.5, 6.8, 0), -150, STRIDE_R), ((22.0, 0.6, 0), -90, LEAN)]
rigs = [add_player('home', *h) for h in home] + [add_player('away', *a) for a in away] if os.path.exists(SKATER) else []   # v1 skater retired: scene.py adds the v2 cast
bpy.context.view_layer.update()
puck_pos = rigs[0].matrix_world @ V(0.55, -1.0, 0.0) if rigs else V(9.5, -1.6, 0.0)
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=0.12, radius2=0.12, depth=0.05, matrix=Matrix.Translation((puck_pos.x, puck_pos.y, 0.025)))
mesh_obj('Puck', bm, [M['puck']], C_PLAYERS)

# ---------------- lights, camera, render settings ----------------
world = bpy.data.worlds.new('Arena'); scene.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = lin('#0b0e1a'); world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0
sun = bpy.data.lights.new('Sun', 'SUN'); sun.energy = 1.55; sun.angle = math.radians(2.0); sun.color = (1, 0.98, 0.96)
so = bpy.data.objects.new('Sun', sun); so.rotation_euler = (math.radians(28), math.radians(-14), math.radians(20)); scene.collection.objects.link(so)
for k in range(5):   # long light rigs over the ice, they streak across the glossy ice
    lt = bpy.data.lights.new('Rig%d' % k, 'AREA'); lt.shape = 'RECTANGLE'; lt.size = 40; lt.size_y = 0.8; lt.energy = 900
    lo = bpy.data.objects.new('Rig%d' % k, lt); lo.location = (0, -10 + k * 5, 15); scene.collection.objects.link(lo)
for k in range(12):  # tall light columns over the far stands: their reflections streak down the ice toward the camera
    lt = bpy.data.lights.new('Far%d' % k, 'AREA'); lt.shape = 'RECTANGLE'; lt.size = 0.35 + random.random() * 0.5; lt.size_y = 9; lt.energy = 1300 + random.random() * 1300
    lo = bpy.data.objects.new('Far%d' % k, lt); lo.location = (-16 + k * 4.6 + random.uniform(-1, 1), 38, 21); scene.collection.objects.link(lo)
    lo.rotation_euler = (V(-14 + k * 6, 0, 0) - lo.location).to_track_quat('-Z', 'Y').to_euler()
hemi = bpy.data.lights.new('Fill', 'SUN'); hemi.energy = 0.35; hemi.color = (0.8, 0.84, 1.0)
ho = bpy.data.objects.new('Fill', hemi); ho.rotation_euler = (math.radians(-40), 0, math.radians(200)); scene.collection.objects.link(ho)

cam_data = bpy.data.cameras.new('Broadcast'); cam_data.sensor_fit = 'VERTICAL'; cam_data.angle_y = math.radians(27); cam_data.clip_start = 0.5; cam_data.clip_end = 300
cam = bpy.data.objects.new('Broadcast', cam_data); scene.collection.objects.link(cam); scene.camera = cam
focus = V(10.0, 1.5, 0.4); yaw = -0.24
cam.location = (focus.x + math.sin(yaw) * 29, focus.y - 1.5 - math.cos(yaw) * 29 - 1.5, 17.5)
cam.rotation_euler = (focus - cam.location).to_track_quat('-Z', 'Y').to_euler()

scene.render.engine = 'BLENDER_EEVEE'
ee = scene.eevee
for attr, val in (('taa_render_samples', 32), ('use_raytracing', True), ('use_shadows', True)):
    try: setattr(ee, attr, val)
    except Exception: pass
try: ee.ray_tracing_options.resolution_scale = '1'
except Exception: pass
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
scene.render.film_transparent = False
scene.render.use_freestyle = True; scene.render.line_thickness_mode = 'ABSOLUTE'; scene.render.line_thickness = 1.0
fs = bpy.context.view_layer.freestyle_settings
ls = fs.linesets[0] if len(fs.linesets) else fs.linesets.new('Players')
ls.select_by_collection = True; ls.collection = C_PLAYERS; ls.select_by_visibility = True
ls.select_silhouette = True; ls.select_border = True; ls.select_crease = False
ls.linestyle.color = (0.04, 0.035, 0.07); ls.linestyle.thickness = 1.0

bpy.ops.wm.save_as_mainfile(filepath=OUT + '/arena.blend')
shots = {}
for name, (w, h, thick) in {'arena_pixel': (640, 360, 1.0), 'arena_hd': (1920, 1080, 2.2)}.items():
    scene.render.resolution_x = w; scene.render.resolution_y = h; scene.render.resolution_percentage = 100
    scene.render.filter_size = 0.5 if name == 'arena_pixel' else 1.5
    ls.linestyle.thickness = thick
    scene.render.filepath = f"{OUT}/renders/{name}.png"
    bpy.ops.render.render(write_still=True); shots[name] = os.path.getsize(scene.render.filepath)
result = {'fans': fans, 'shots': shots, 'players': len(rigs)}
