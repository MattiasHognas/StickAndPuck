"""Stick & Puck crowd frames (A18): one chunky low-poly spectator posed as sit, stand, cheer, wave
and boo, two frames each, in four looks (hair, beanie), rendered front-on with an orthographic
camera at 16 px per metre into 16x32 cells, the feet on the bottom row (the tread of the stand).
Each body part gets a lit and a shade material picked per face from a fixed key light, and the
Workbench engine renders them flat with no anti-aliasing, so every pixel is one part and one tone:
an id image, not a picture. art/scripts/crowd_atlas.py turns the ids into palette colours and packs
the atlas the game draws (art/crowd/crowd_256.png). The shirt (and the beanie) is its own part so
the game can tint it with the home club's colours.
Writes art/crowd/renders/ids/<look>_<frame>.png, 4 looks x 10 frames (renders/ is not committed).
Run: blender -b --python-exit-code 1 --python art/scripts/build_crowd.py   (or with ART / SAP_ART set)"""
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
OUT = ART + '/crowd/renders/ids'
os.makedirs(OUT, exist_ok=True)
V = lambda *a: Vector(a)

CELL_W, CELL_H, PX_PER_M = 16, 32, 16          # a cell is 1 m wide and 2 m tall
FRAMES = ['sit0', 'sit1', 'stand0', 'stand1', 'cheer0', 'cheer1', 'wave0', 'wave1', 'boo0', 'boo1']
LOOKS = ['short', 'long', 'beanie', 'cropped']
# part -> (lit key, shade key): flat Workbench colours crowd_atlas.py reads back (sRGB, nearest wins)
KEYS = {'skin': ((1, 0, 0), (0.35, 0, 0)), 'hair': ((0, 1, 0), (0, 0.35, 0)), 'shirt': ((0, 0, 1), (0, 0, 0.35)),
        'trousers': ((1, 1, 0), (0.35, 0.35, 0)), 'shoes': ((1, 0, 1), (0.35, 0, 0.35))}
LIGHT = V(-0.45, -0.7, 0.55).normalized()     # key light from the upper left, toward the rink
LIT_FROM = 0.2                                 # faces facing the light more than this are lit

scene = bpy.context.scene
for ob in list(bpy.data.objects): bpy.data.objects.remove(ob)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for d in list(coll): coll.remove(d)

MATS = []      # material index -> (part, tone)
for part, (lit, shade) in KEYS.items():
    for tone, rgb in (('lit', lit), ('shade', shade)):
        m = bpy.data.materials.new('%s_%s' % (part, tone)); m.diffuse_color = (*rgb, 1.0); MATS.append((m, part, tone))
def mi(part, tone): return next(i for i, (_, p, t) in enumerate(MATS) if p == part and t == tone)

def tag(bm, verts, part):
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mi(part, 'lit' if f.normal.normalized().dot(LIGHT) > LIT_FROM else 'shade')

def limb(bm, a, b, r, part, seg=8):
    d = b - a
    ret = bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=d.length,
                                matrix=Matrix.Translation((a + b) / 2) @ V(0, 0, 1).rotation_difference(d.normalized()).to_matrix().to_4x4())
    bm.normal_update(); tag(bm, ret['verts'], part)
    joint(bm, b, r, part)

def joint(bm, c, r, part, sub=1):
    ret = bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r, matrix=Matrix.Translation(c))
    bm.normal_update(); tag(bm, ret['verts'], part)

def block(bm, lo, hi, part):
    c = (lo + hi) / 2; s = hi - lo
    ret = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(c) @ Matrix.Diagonal((s.x, s.y, s.z, 1)))
    bm.normal_update(); tag(bm, ret['verts'], part)

# ---------------- poses: x to the right on screen, -y toward the rink (camera), z up from the tread ----------------
def skeleton(seated, lift=0.0):
    if seated:
        k = dict(hip=0.48, torso=(0.44, 0.98), shoulder=0.93, head=1.14, knee=(-0.42, 0.52), ankle=(-0.46, 0.08))
    else:
        k = dict(hip=0.80, torso=(0.76, 1.32), shoulder=1.26, head=1.47, knee=(-0.02, 0.44), ankle=(0.0, 0.08))
    for key in ('hip', 'shoulder', 'head'): k[key] += lift
    k['torso'] = (k['torso'][0] + lift, k['torso'][1] + lift)
    k['knee'] = (k['knee'][0], k['knee'][1] + lift); k['ankle'] = (k['ankle'][0], k['ankle'][1] + lift)
    return k

# arms as (elbow, hand) per side, relative to the shoulder height (dz) so seated and standing share them
ARMS = {
    'sit0': {'l': ((-0.27, -0.12, -0.28), (-0.15, -0.36, -0.42)), 'r': ((0.27, -0.12, -0.28), (0.15, -0.36, -0.42))},
    'sit1': {'l': ((-0.27, -0.12, -0.28), (-0.15, -0.36, -0.42)), 'r': ((0.28, -0.2, -0.22), (0.07, -0.18, 0.12))},
    'stand0': {'l': ((-0.28, 0.0, -0.28), (-0.29, -0.02, -0.52)), 'r': ((0.28, 0.0, -0.28), (0.29, -0.02, -0.52))},
    'stand1': {'l': ((-0.28, -0.12, -0.26), (-0.04, -0.32, -0.12)), 'r': ((0.28, -0.12, -0.26), (0.04, -0.32, -0.12))},
    'cheer0': {'l': ((-0.33, 0.0, 0.25), (-0.42, 0.0, 0.52)), 'r': ((0.33, 0.0, 0.25), (0.42, 0.0, 0.52))},
    'cheer1': {'l': ((-0.40, 0.0, 0.04), (-0.33, -0.06, 0.36)), 'r': ((0.40, 0.0, 0.04), (0.33, -0.06, 0.36))},
    'wave0': {'l': ((-0.33, 0.0, 0.24), (-0.43, 0.0, 0.52)), 'r': ((0.14, 0.0, 0.3), (0.0, 0.0, 0.58))},
    'wave1': {'l': ((-0.14, 0.0, 0.3), (0.0, 0.0, 0.58)), 'r': ((0.33, 0.0, 0.24), (0.43, 0.0, 0.52))},
    'boo0': {'l': ((-0.30, -0.14, -0.12), (-0.07, -0.2, 0.16)), 'r': ((0.30, -0.14, -0.12), (0.07, -0.2, 0.16))},
    'boo1': {'l': ((-0.30, -0.14, -0.12), (-0.07, -0.2, 0.16)), 'r': ((0.38, 0.0, 0.10), (0.30, 0.0, 0.40))},
}
SEATED = {'sit0', 'sit1'}
LIFT = {'cheer0': 1.0 / PX_PER_M}     # a hop: one pixel up

def build(frame, look):
    k = skeleton(frame in SEATED, LIFT.get(frame, 0.0))
    bm = bmesh.new()
    t0, t1 = k['torso']
    block(bm, V(-0.19, -0.11, t0), V(0.19, 0.11, t1), 'shirt')
    joint(bm, V(0, 0, t1 + 0.02), 0.07, 'skin')                                    # neck
    for sx in (-1, 1):
        hip = V(sx * 0.1, 0.0, k['hip']); knee = V(sx * 0.11, k['knee'][0], k['knee'][1]); ankle = V(sx * 0.11, k['ankle'][0], k['ankle'][1])
        joint(bm, hip, 0.09, 'trousers'); limb(bm, hip, knee, 0.085, 'trousers'); limb(bm, knee, ankle, 0.075, 'trousers')
        block(bm, V(sx * 0.11 - 0.07, ankle.y - 0.16, 0.0 + k['ankle'][1] - 0.08), V(sx * 0.11 + 0.07, ankle.y + 0.07, k['ankle'][1] + 0.0), 'shoes')
        side = 'l' if sx < 0 else 'r'
        (ex, ey, ez), (hx, hy, hz) = ARMS[frame][side]
        sh = V(sx * 0.24, 0.0, k['shoulder']); el = V(ex, ey, k['shoulder'] + ez); ha = V(hx, hy, k['shoulder'] + hz)
        joint(bm, sh, 0.075, 'shirt'); limb(bm, sh, el, 0.065, 'shirt'); limb(bm, el, ha, 0.06, 'shirt')   # long sleeves
        joint(bm, ha, 0.07, 'skin')
    head = V(0, 0, k['head'])
    joint(bm, head, 0.15, 'skin', 2)
    if look == 'short':                                                            # the hair cap shows over the brow
        joint(bm, head + V(0, 0.0, 0.07), 0.15, 'hair', 2)
    elif look == 'long':                                                           # and down past the ears
        joint(bm, head + V(0, 0.0, 0.07), 0.155, 'hair', 2)
        block(bm, head + V(-0.17, 0.0, -0.2), head + V(0.17, 0.12, 0.06), 'hair')
    elif look == 'beanie':                                                         # a beanie in the shirt colour
        joint(bm, head + V(0, 0.0, 0.08), 0.155, 'shirt', 2)
        joint(bm, head + V(0, 0.0, 0.25), 0.05, 'shirt')
    else:                                                                          # cropped: a thin cap of hair
        joint(bm, head + V(0, 0.02, 0.09), 0.135, 'hair', 2)
    me = bpy.data.meshes.new('fan'); bm.to_mesh(me); bm.free()
    for m, _, _ in MATS: me.materials.append(m)
    ob = bpy.data.objects.new('fan', me); scene.collection.objects.link(ob)
    return ob

# ---------------- camera and flat render ----------------
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam); scene.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = CELL_H / PX_PER_M
cam.location = (0.0, -10.0, CELL_H / PX_PER_M / 2); cam.rotation_euler = (math.radians(90), 0.0, 0.0)
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = CELL_W, CELL_H, 100
scene.render.film_transparent = True; scene.render.filter_size = 0.0
scene.render.image_settings.file_format = 'PNG'; scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
scene.display.render_aa = 'OFF'
sh = scene.display.shading
sh.light = 'FLAT'; sh.color_type = 'MATERIAL'
for attr in ('show_object_outline', 'show_cavity', 'show_shadows', 'show_specular_highlight', 'show_xray'):
    if hasattr(sh, attr): setattr(sh, attr, False)

n = 0
for look in LOOKS:
    for frame in FRAMES:
        ob = build(frame, look)
        scene.render.filepath = '%s/%s_%s.png' % (OUT, look, frame)
        bpy.ops.render.render(write_still=True); n += 1
        bpy.data.objects.remove(ob)
print('crowd: rendered %d id frames to %s' % (n, OUT))
