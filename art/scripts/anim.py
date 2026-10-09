"""Stepped animation clips for the v2 players, and skinning for Raylib.
Run in Blender on art/player/<role>.blend right after build_player.py. It
1. joins the rigid parts into one mesh, each part weighted 1.0 to the bone it hung on
   (Raylib only animates skinned meshes, and one mesh is one draw call),
2. keys every clip as whole poses held with CONSTANT interpolation, so motion is stepped,
3. saves the blend and exports the GLB with each clip as a named glTF animation,
4. unless RENDER=False, renders every pose from the turnaround camera into renders/anim_*.png.
Poses are written in armature space (character faces -Y, +X is its left) with a small two-bone
leg IK, so skates stay on the ice and both hands stay on the stick."""
import bpy, math, os, json, struct, sys, glob
try: import numpy                        # the glTF exporter needs numpy; a Blender run from a venv may not see it
except ImportError: sys.path += glob.glob('/usr/lib/python3*/site-packages')
from mathutils import Vector as V, Matrix

NAME = os.path.basename(bpy.data.filepath).rsplit('.', 1)[0]      # skater, goalie or referee
OUT = os.path.dirname(bpy.data.filepath)
scene = bpy.context.scene; scene.render.fps = 24
rig = bpy.data.objects['Rig']; rig.location = (0, 0, 0); rig.rotation_euler = (0, 0, 0)
PB = rig.pose.bones; B = rig.data.bones

# ---------------- 1. one skinned mesh ----------------
parts = [o for o in scene.objects if o.type == 'MESH' and o.parent == rig and o.parent_type == 'BONE']
if parts:
    for o in parts:
        vg = o.vertex_groups.new(name=o.parent_bone); vg.add(range(len(o.data.vertices)), 1.0, 'REPLACE')
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    with bpy.context.temp_override(active_object=parts[0], object=parts[0], selected_objects=parts, selected_editable_objects=parts):
        bpy.ops.object.join()
    body = parts[0]; body.name = NAME; body.data.name = NAME
    with bpy.context.temp_override(active_object=body, object=body, selected_objects=[body], selected_editable_objects=[body]):
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    body.parent = rig; body.matrix_parent_inverse.identity()
    body.modifiers.new('Rig', 'ARMATURE').object = rig
body = next(o for o in scene.objects if o.type == 'MESH' and o.parent == rig)

# ---------------- pose helpers ----------------
def upd(): bpy.context.view_layer.update()
def reset():
    for p in PB:
        p.rotation_mode = 'QUATERNION'; p.rotation_quaternion = (1, 0, 0, 0); p.location = (0, 0, 0)
    upd()
def turn(name, x=0.0, y=0.0, z=0.0):
    """rotate a bone and everything below it about its head, by armature-space angles"""
    p = PB[name]; cq = p.matrix.to_quaternion()
    rw = (Matrix.Rotation(z, 3, 'Z') @ Matrix.Rotation(y, 3, 'Y') @ Matrix.Rotation(x, 3, 'X')).to_quaternion()
    p.rotation_quaternion = p.rotation_quaternion @ (cq.inverted() @ rw @ cq); upd()
def aim(name, d):
    """swing a bone so it points along armature-space direction d"""
    p = PB[name]; cq = p.matrix.to_quaternion()
    rd = (cq @ V((0, 1, 0))).rotation_difference(V(d).normalized())
    p.rotation_quaternion = p.rotation_quaternion @ (cq.inverted() @ rd @ cq); upd()
def shift(x=0.0, y=0.0, z=0.0):
    PB['pelvis'].location = B['pelvis'].matrix_local.to_3x3().inverted() @ V((x, y, z)); upd()

REST = {k: (B[k].head_local.copy(), B[k].tail_local.copy()) for k in B.keys()}
ANKLE = {s: REST['shin.' + s][1] for s in 'LR'}
def leg(s, ankle, pole=None, toe=0.0):
    """two-bone IK: put the ankle at an armature-space point, knee toward the pole, blade flat"""
    sx = 1 if s == 'L' else -1
    pole = V(pole or (sx * 0.3, -1.0, 0.0))
    H = PB['thigh.' + s].head.copy(); A = V(ankle)
    L1, L2 = B['thigh.' + s].length, B['shin.' + s].length
    d = A - H; dist = min(d.length, L1 + L2 - 1e-4); n = d.normalized()
    a = (L1 * L1 - L2 * L2 + dist * dist) / (2 * dist); h = math.sqrt(max(0.0, L1 * L1 - a * a))
    pv = (pole - n * pole.dot(n)).normalized()
    K = H + n * a + pv * h
    aim('thigh.' + s, K - H); aim('shin.' + s, H + n * dist - PB['shin.' + s].head)
    aim('foot.' + s, Matrix.Rotation(sx * toe, 3, 'Z') @ (REST['foot.' + s][1] - REST['foot.' + s][0]))
def plant(spread=0.0, **kw):
    for s in 'LR': leg(s, ANKLE[s] + V(((1 if s == 'L' else -1) * spread, 0, 0)), **kw)

gi = body.vertex_groups['hand.R'].index if 'hand.R' in body.vertex_groups else -1
STICK = [v.co.copy() for v in body.data.vertices if any(g.group == gi for g in v.groups)] if NAME != 'referee' else []
def stick_low():
    m = PB['hand.R'].matrix @ B['hand.R'].matrix_local.inverted()
    return min((m @ c).z for c in STICK)
def lift(bones, z=0.0):
    """swing the stick arm(s) about X until the blade's lowest point sits at height z"""
    if not STICK: return
    saved = {b: PB[b].rotation_quaternion.copy() for b in bones}; lo, hi = -1.0, 1.0
    for _ in range(14):
        mid = (lo + hi) / 2
        for b in bones: PB[b].rotation_quaternion = saved[b]
        upd()
        for b in bones: turn(b, x=mid)
        if stick_low() < z: hi = mid
        else: lo = mid
ARMS = ['clavicle.L', 'clavicle.R']      # their heads share one X line, so turning both is rigid

# ---------------- clips ----------------
# skate stroke for the left foot, relative to its rest ankle: out and back on the ice, then lifted
# and brought forward under the body; the right foot runs the mirror image half a cycle later
STROKE = [(0.0, -0.08, 0.0), (0.07, 0.07, 0.0), (0.16, 0.22, 0.0), (0.25, 0.36, 0.0),
          (0.18, 0.36, 0.11), (0.08, 0.16, 0.14), (0.0, -0.04, 0.10), (-0.01, -0.14, 0.03)]
def skate_legs(i, amp=1.0):
    for s, ph in (('L', i), ('R', (i + 4) % 8)):
        sx = 1 if s == 'L' else -1; x, y, z = STROKE[ph]
        leg(s, ANKLE[s] + V((sx * x * amp, y * amp, z * amp)), toe=0.35 * x / 0.25)
    return math.sin(math.pi * (i + 0.5) / 4)                # +1 while the left foot pushes

def skater_skate(i):
    side = math.sin(math.pi * (i + 0.5) / 4)
    shift(x=-0.05 * side, z=-0.10 - 0.02 * abs(math.sin(math.pi * i / 4)))
    turn('pelvis', x=0.22, z=0.08 * side)
    skate_legs(i)
    turn('spine', x=0.16, z=-0.10 * side); turn('chest', x=0.06)
    turn('neck', x=-0.25); turn('head', x=-0.12)
    lift(ARMS, 0.0)

def skater_idle(i):
    breathe = [0, 1, 1, 0][i]
    shift(z=-0.03 - 0.012 * breathe); turn('pelvis', x=0.10)
    plant(pole=(0, -1, 0))
    turn('spine', x=0.06); turn('chest', x=-0.03 * breathe)
    turn('head', z=[0, 0, 0.18, 0.12][i])
    lift(ARMS, 0.0)

def skater_shoot(i):                        # left-handed wrist shot: load, sweep, follow through high
    spin = [0.5, 0.85, 0.1, -0.6, -0.95, -0.4][i]
    shift(z=-0.08); turn('pelvis', x=0.18, z=spin * 0.3)
    plant(spread=0.06, pole=(0, -1, 0))
    turn('spine', x=0.18, z=spin * 0.5); turn('chest', z=spin * 0.5)
    turn('neck', x=-0.2); turn('head', z=-spin * 0.6)
    lift(ARMS, [0.0, 0.06, 0.0, 0.35, 0.85, 0.25][i])

def goalie_ready(i):
    bob = [0, 1, 2, 1][i]
    shift(z=-0.012 * bob); plant(pole=(0, -1, 0))
    turn('chest', x=0.02 * bob); turn('upperarm.L', z=0.04 * bob)
    lift(['hand.R'], 0.0)

def goalie_butterfly(i):                    # drop to the knees, pads flared flat along the ice
    drop = [0.0, 0.14, 0.27, 0.27, 0.27][i]
    shift(z=-drop); turn('pelvis', x=0.08 * min(i, 2))
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        if i == 0: leg(s, ANKLE[s], pole=(0, -1, 0))
        else: leg(s, V((sx * [0, 0.44, 0.48, 0.48, 0.48][i], 0.0, [0, 0.10, 0.08, 0.08, 0.08][i])),
                  pole=(-sx * 0.6, -1.0, -0.8), toe=0.6)
    turn('chest', x=-0.04 * min(i, 2))
    if i >= 3: turn('upperarm.L', z=0.25, x=-0.25)   # catcher up
    lift(['hand.R'], 0.0)

def ref_skate(i):
    side = math.sin(math.pi * (i + 0.5) / 4)
    shift(x=-0.03 * side, z=-0.05); turn('pelvis', x=0.08)
    skate_legs(i, amp=0.65)
    turn('spine', x=0.06, z=-0.06 * side); turn('neck', x=-0.06)
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        turn('upperarm.' + s, x=-0.35 * sx * math.sin(math.pi * i / 4), z=-sx * 0.08)

def ref_idle(i):
    shift(z=-0.01 * [0, 1, 1, 0][i]); plant(pole=(0, -1, 0))
    for s in 'LR': turn('upperarm.' + s, z=-(1 if s == 'L' else -1) * 0.06)
    turn('head', z=[0, 0.25, 0.25, -0.15][i])

# name: (poses, frames each pose is held at 24 fps, loops?)
CLIPS = {'skater': {'skate': (skater_skate, 8, 2, True), 'idle': (skater_idle, 4, 6, True), 'shoot': (skater_shoot, 6, 3, False)},
         'goalie': {'ready': (goalie_ready, 4, 5, True), 'butterfly': (goalie_butterfly, 5, 3, False)},
         'referee': {'skate': (ref_skate, 8, 3, True), 'idle': (ref_idle, 4, 8, True)}}[NAME]

# ---------------- 2. key the clips, stepped ----------------
def fcurves(act):
    if hasattr(act, 'fcurves'): return list(act.fcurves)
    return [fc for ly in act.layers for st in ly.strips for cb in st.channelbags for fc in cb.fcurves]
bpy.context.preferences.edit.keyframe_new_interpolation_type = 'CONSTANT'
ad = rig.animation_data_create()
for tr in list(ad.nla_tracks): ad.nla_tracks.remove(tr)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for cname, (fn, n, hold, loop) in CLIPS.items():
    act = bpy.data.actions.new('%s_%s' % (NAME, cname)); act.use_fake_user = True
    ad.action = act
    keys = [(i * hold, i) for i in range(n)] + [(n * hold, 0 if loop else n - 1)]   # last key closes the loop / holds the end
    for f, i in keys:
        reset(); fn(i)
        for p in PB:
            p.keyframe_insert('rotation_quaternion', frame=f, group=p.name)
            p.keyframe_insert('location', frame=f, group=p.name)
    for fc in fcurves(act):
        for k in fc.keyframe_points: k.interpolation = 'CONSTANT'
    tr = ad.nla_tracks.new(); tr.name = cname; tr.strips.new(cname, 0, act); tr.mute = True
    ad.action = None
reset()

# ---------------- 3. save and export ----------------
bpy.ops.wm.save_as_mainfile(filepath=OUT + '/%s.blend' % NAME)
glb = OUT + '/%s.glb' % NAME
bpy.ops.export_scene.gltf(filepath=glb, export_format='GLB', export_animation_mode='ACTIONS',
                          export_force_sampling=True, export_frame_step=1)
raw = open(glb, 'rb').read(); js = json.loads(raw[20:20 + struct.unpack('<I', raw[12:16])[0]])
info = {'tris': sum(len(p.vertices) - 2 for p in body.data.polygons), 'meshes': len(js['meshes']),
        'skins': len(js.get('skins', [])), 'joints': len(js['skins'][0]['joints']) if js.get('skins') else 0,
        'anims': {a['name']: round(max(js['accessors'][s['input']]['max'][0] for s in a['samplers']), 3) for a in js.get('animations', [])},
        'bytes': len(raw)}

# ---------------- 4. review renders ----------------
if globals().get('RENDER', True):
    tp = os.path.join(os.path.dirname(OUT), 'scripts', 'turnaround.py'); g = {'__name__': '__main__', 'TURN': False}
    exec(compile(open(tp).read(), tp, 'exec'), g)
    kit = 'ref' if NAME == 'referee' else 'home'
    g['MAT'].node_tree.nodes['Image Texture'].image = bpy.data.images['%s_%s' % (NAME, kit)]
    n_out = 0
    for cname, (fn, n, hold, loop) in CLIPS.items():
        act = bpy.data.actions['%s_%s' % (NAME, cname)]; ad.action = act
        if hasattr(ad, 'action_slot') and act.slots: ad.action_slot = act.slots[0]
        for i in range(n):
            scene.frame_set(i * hold)
            for view, deg in (('front34', 35), ('side', 90)):
                rig.rotation_euler = (0, 0, math.radians(deg))
                scene.render.filepath = '%s/renders/anim_%s_%s_%s_%02d.png' % (OUT, NAME, cname, view, i)
                bpy.ops.render.render(write_still=True); n_out += 1
    info['renders'] = n_out
result = info
