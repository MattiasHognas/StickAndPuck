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

NAME = os.path.basename(bpy.data.filepath).rsplit('.', 1)[0]      # skater, goalie, referee or staff
OUT = os.path.dirname(bpy.data.filepath)
scene = bpy.context.scene; scene.render.fps = 24
rig = bpy.data.objects['Rig']; rig.location = (0, 0, 0); rig.rotation_euler = (0, 0, 0)
PB = rig.pose.bones; B = rig.data.bones

# ---------------- 1. one skinned mesh ----------------
parts = [o for o in scene.objects if o.type == 'MESH' and o.parent == rig and o.parent_type == 'BONE']
if parts and NAME == 'skater':
    # the skater's stick gets its own bone under the top hand, so fight clips can drop it: scaled to
    # nothing at the grip, it vanishes into the glove
    with bpy.context.temp_override(active_object=rig, object=rig, selected_objects=[rig], selected_editable_objects=[rig]):
        bpy.ops.object.mode_set(mode='EDIT')
        eb = rig.data.edit_bones; h = eb['hand.R']
        sb = eb.new('stick'); sb.head = h.tail; sb.tail = h.tail + (h.tail - h.head).normalized() * 0.1; sb.parent = h
        bpy.ops.object.mode_set(mode='OBJECT')
    for o in parts:
        if o.name.split('.')[0] in ('Shaft', 'Knob', 'Blade'):
            mw = o.matrix_world.copy(); o.parent_bone = 'stick'; bpy.context.view_layer.update(); o.matrix_world = mw
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
        p.rotation_mode = 'QUATERNION'; p.rotation_quaternion = (1, 0, 0, 0); p.location = (0, 0, 0); p.scale = (1, 1, 1)
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

SB = 'stick' if 'stick' in body.vertex_groups else 'hand.R'      # the bone that carries the stick
gi = body.vertex_groups[SB].index if SB in body.vertex_groups else -1
STICK = [v.co.copy() for v in body.data.vertices if any(g.group == gi for g in v.groups)] if NAME not in ('referee', 'staff') else []
def stick_low():
    m = PB[SB].matrix @ B[SB].matrix_local.inverted()
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
if STICK and NAME == 'skater':
    # the shaft in rest: from the top glove down to the blade; GRIP is where the glove holds it,
    # SLIDE how far down the shaft its middle (knob to heel) lies from there
    S0 = REST[SB][0]; low = min(c.z for c in STICK)
    D0 = (sum((c for c in STICK if c.z < low + 0.04), V()) / len([c for c in STICK if c.z < low + 0.04]) - S0).normalized()
    T = [(c - S0).dot(D0) for c in STICK]; tg = ((REST['hand.R'][0] + REST['hand.R'][1]) / 2 - S0).dot(D0)
    GRIP = S0 + D0 * tg; SLIDE = (min(T) + max(T)) / 2 - tg
def grip_mid(d):
    """the top glove alone holds the stick at the middle of the shaft, the blade end pointing along d"""
    p = PB[SB]; m = p.matrix @ B[SB].matrix_local.inverted(); w = V(d).normalized()
    G = m @ GRIP; rd = (m.to_3x3() @ D0).normalized().rotation_difference(w).to_matrix().to_4x4()
    p.matrix = Matrix.Translation(G) @ rd @ Matrix.Translation(-G) @ p.matrix; upd()
    p.matrix = Matrix.Translation(-w * SLIDE) @ p.matrix; upd()

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

def skater_windup(i):                       # a one-timer waiting on the pass: stick drawn back and up over the shoulder, weight on the back leg, held
    u = [0.35, 0.7, 1.0][i]; spin = 0.95 * u
    shift(x=0.06 * u, z=-0.08 - 0.04 * u); turn('pelvis', x=0.16, z=spin * 0.3)
    leg('L', ANKLE['L'] + V((0.08, 0.02, 0)), pole=(0.2, -1, 0)); leg('R', ANKLE['R'] + V((-0.06, -0.03, 0)), pole=(-0.2, -1, 0))
    turn('spine', x=0.14, z=spin * 0.5); turn('chest', z=spin * 0.5)
    turn('neck', x=-0.2); turn('head', z=-spin * 0.6)
    reach('R', V((WU_HAND[0], WU_HAND[1], WU_HAND[2] + 0.3 * u)), pole=(-1, 0.3, -0.6))
    lo, hi = V((0.3, -0.4, -0.85)), V(WU_DIR)
    d = lo.lerp(hi, u); stick_dir(d, floor=0.03 if i == 0 else None)   # the first pose keeps the blade on the ice
    m = PB[SB].matrix @ B[SB].matrix_local.inverted()
    reach('L', m @ GRIP + d.normalized() * WU_LOW, pole=(1, 0.3, -0.6))
WU_HAND, WU_DIR, WU_LOW = (0.02, -0.28, 0.72), (0.7, 0.55, 0.3), 0.38

def faceoff_stance(i, drop, bend, hand, d):     # set for the draw: knees bent, back over, head up at the dot, both gloves on the stick, blade flat on the ice
    bob = [0, 1][i]
    shift(y=0.10 * bend, z=-drop - 0.012 * bob); turn('pelvis', x=bend * 0.45)
    plant(spread=0.10, pole=(0, -1, 0))
    turn('spine', x=bend * 0.55); turn('chest', x=bend * 0.15)
    turn('neck', x=-bend * 0.65); turn('head', x=-bend * 0.25)
    reach('R', V(hand) + V((0, 0, -0.01 * bob)), pole=(-1, 0.3, -0.6))
    stick_dir(d, floor=0.0)
    m = PB[SB].matrix @ B[SB].matrix_local.inverted()
    reach('L', m @ GRIP + V(d).normalized() * FO_LOW, pole=(1, 0.3, -0.6))
FO_LOW = 0.40
def skater_faceoff_crouch(i):               # a winger or defenceman set at the circle, held
    faceoff_stance(i, 0.19, 0.55, (0.02, -0.34, 0.58), (0.25, -0.55, -0.8))
def skater_faceoff_centre(i):               # the centre over the dot, lowest, the blade reaching out toward it
    faceoff_stance(i, 0.23, 0.95, (0.03, -0.42, 0.50), (0.15, -0.75, -0.65))

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

def arm(s, upper, fore, hand=None):
    """point one arm: upper arm, forearm and hand along armature-space directions given for the
    character's left side (x is mirrored for the right)"""
    sx = 1 if s == 'L' else -1; m = lambda d: (sx * d[0], d[1], d[2])
    aim('upperarm.' + s, m(upper)); aim('forearm.' + s, m(fore)); aim('hand.' + s, m(hand or fore))

def skater_pass(i):                         # a short sweep of the stick across the body, low follow-through
    spin = [0.35, 0.55, 0.0, -0.4, -0.15][i]
    shift(z=-0.07); turn('pelvis', x=0.16, z=spin * 0.25)
    plant(spread=0.04, pole=(0, -1, 0))
    turn('spine', x=0.16, z=spin * 0.4); turn('chest', z=spin * 0.4)
    turn('neck', x=-0.2); turn('head', z=-spin * 0.5)
    lift(ARMS, [0.0, 0.04, 0.0, 0.12, 0.03][i])

def skater_check(i):                        # load low, drive the left shoulder through, recover
    twist = [0.0, -0.45, -0.7, -0.3][i]; lean = [0.30, 0.36, 0.42, 0.24][i]
    shift(y=[0, -0.04, -0.10, -0.03][i], z=[-0.14, -0.10, -0.08, -0.08][i]); turn('pelvis', x=lean * 0.6, z=twist * 0.4)
    leg('L', ANKLE['L'] + V((0.04, [0, -0.06, -0.10, -0.03][i], 0)), pole=(0.3, -1, 0))
    leg('R', ANKLE['R'] + V((-0.04, [0, 0.16, 0.26, 0.08][i], [0, 0.02, 0.06, 0.0][i])), pole=(-0.3, -1, 0))
    turn('spine', x=lean * 0.5, z=twist * 0.5); turn('chest', z=twist * 0.4)
    turn('neck', x=-0.3); turn('head', z=-twist * 0.6)
    lift(ARMS, [0.0, 0.08, 0.15, 0.04][i])
    if i in (1, 2): turn('upperarm.L', z=0.25, y=-0.2)      # elbow tucked in, shoulder leads

def skater_celebrate(i):                    # both arms up in a V, pumping, the stick high in the top glove alone, one knee up on the glide
    shift(z=[-0.02, 0.02, -0.02, 0.0][i]); turn('pelvis', x=-0.04)
    leg('L', ANKLE['L'] + V((0, [0, -0.14, 0, -0.06][i], [0, 0.26, 0, 0.12][i])), pole=(0.3, -1, 0))
    leg('R', ANKLE['R'], pole=(0, -1, 0))
    turn('spine', x=-0.10); turn('neck', x=0.10); turn('head', x=0.18)
    up = [0.75, 1.0, 0.8, 0.95][i]
    for s in 'LR': arm(s, (0.5, -0.1, up), (0.3, -0.15, 1.0))
    grip_mid(([0.55, 0.45, 0.55, 0.45][i], -0.10, 0.85))

FIGHT_GUARD = ((0.30, -0.45, -0.75), (-0.10, -0.55, 0.85))     # elbow low and out, fist up by the chin

def fight_base(crouch=0.06, lean=0.10, drop=True):
    """gloves off: the stick shrinks into the top glove, feet wide, knees bent, fists up"""
    if drop: PB['stick'].scale = (1e-3, 1e-3, 1e-3); upd()
    shift(z=-crouch); turn('pelvis', x=lean)
    plant(spread=0.07, pole=(0, -1, 0))
    turn('spine', x=lean * 0.6); turn('neck', x=-0.12)

def skater_punch(i):                        # right hand: guard, wind back, full extension, pull back, guard
    twist = [0.0, -0.35, 0.45, 0.2, 0.0][i]
    fight_base(lean=[0.10, 0.06, 0.20, 0.14, 0.10][i])
    turn('spine', z=twist * 0.5); turn('chest', z=twist * 0.5); turn('head', z=-twist * 0.4)
    arm('L', *FIGHT_GUARD)
    if i == 1: arm('R', (0.35, 0.35, -0.75), (-0.15, -0.35, 0.9))
    elif i == 2: arm('R', (-0.12, -1.0, 0.05), (-0.08, -1.0, 0.08))
    elif i == 3: arm('R', (0.15, -0.7, -0.45), (-0.05, -0.8, 0.5))
    else: arm('R', *FIGHT_GUARD)

def skater_block(i):                        # forearms come up crossed over the face, head tucked, lean away
    fight_base(lean=[0.10, 0.02, -0.04][i])
    turn('neck', x=[0.0, 0.18, 0.28][i])
    if i == 0:
        arm('L', *FIGHT_GUARD); arm('R', *FIGHT_GUARD)
    else:
        up = [0, 0.6, 0.85][i]
        arm('L', (0.30, -0.55, -0.55 + up * 0.5), (-0.75, -0.45, 0.45 + up * 0.3))
        arm('R', (0.30, -0.65, -0.55 + up * 0.5), (-0.75, -0.40, 0.40 + up * 0.3))

def skater_grab(i):                         # left hand holds the other sweater and tugs, right hand cocked
    tug = [0.0, 1.0, 0.4, 1.0][i]
    fight_base(lean=0.12 - 0.05 * tug)
    shift(y=0.03 * tug); turn('chest', z=0.15 + 0.08 * tug)
    arm('L', (0.10, -0.95 + 0.5 * tug, -0.2 - 0.3 * tug), (0.02, -1.0, 0.05 + 0.15 * tug), (0.0, -1.0, -0.3))
    arm('R', (0.35, 0.30, -0.70), (-0.10, -0.25, 0.95))

def ground():
    """drop or raise the whole body so its lowest deformed point rests on the ice"""
    ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get()); me = ev.to_mesh()
    lo = min((body.matrix_world @ v.co).z for v in me.vertices); ev.to_mesh_clear()
    PB['pelvis'].location += B['pelvis'].matrix_local.to_3x3().inverted() @ V((0, 0, -lo)); upd()

def skater_fall(i):                         # rocked back, legs go, down flat on the back on the ice
    back = [-0.30, -0.85, -1.60, -1.80][i]; drop = [-0.02, -0.25, -0.70, -0.76][i]
    if i == 0: fight_base(lean=0.0, drop=False)
    shift(y=[0.05, 0.12, 0.18, 0.20][i], z=drop); turn('pelvis', x=back)
    if i == 0:
        plant(spread=0.07, pole=(0, -1, 0))
    else:
        for s in 'LR':
            sx = 1 if s == 'L' else -1
            turn('thigh.' + s, x=[0, -0.6, 0.0, 0.15][i], y=sx * 0.12); turn('shin.' + s, x=[0, 0.9, 0.25, 0.1][i])
    turn('neck', x=[0.2, 0.3, 0.45, 0.35][i])
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        turn('upperarm.' + s, y=sx * [0.3, 0.7, 1.1, 1.2][i], x=[-0.4, -0.9, -0.2, 0.0][i])
    PB['stick'].scale = (1e-3, 1e-3, 1e-3); upd()
    ground()

def goalie_glove(i):                        # catcher flashes up and out to the left, snaps shut, shows the catch
    reach = [0.0, 1.0, 0.85, 0.5][i]
    shift(x=0.06 * reach, z=-0.04 * reach); turn('pelvis', y=0.10 * reach)
    leg('L', ANKLE['L'] + V((0.10 * reach, 0, 0)), pole=(0, -1, 0))
    leg('R', ANKLE['R'] + V((0.02 * reach, 0, 0.0)), pole=(0, -1, 0))
    turn('chest', z=0.15 * reach); turn('head', z=0.35 * reach)
    if i == 0: turn('upperarm.L', z=0.04)
    elif i == 1: arm('L', (0.85, -0.35, 0.40), (0.45, -0.35, 0.82))
    elif i == 2: arm('L', (0.75, -0.40, 0.30), (0.20, -0.55, 0.80))
    else: arm('L', (0.55, -0.35, -0.10), (0.05, -0.55, 0.83))
    lift(['hand.R'], 0.0)

def goalie_blocker(i):                      # blocker punches out to the right and down, deflects, comes back
    reach = [0.0, 1.0, 0.7][i]
    shift(x=-0.05 * reach, z=-0.05 * reach); turn('pelvis', y=-0.08 * reach)
    leg('R', ANKLE['R'] + V((-0.08 * reach, 0, 0)), pole=(0, -1, 0)); leg('L', ANKLE['L'], pole=(0, -1, 0))
    turn('chest', z=-0.15 * reach); turn('head', z=-0.30 * reach)
    turn('upperarm.L', z=0.04)
    if i: arm('R', [None, (0.85, -0.35, -0.25), (0.6, -0.5, -0.45)][i], [None, (0.75, -0.45, 0.35), (0.35, -0.75, 0.2)][i])
    else: lift(['hand.R'], 0.0)

def goalie_shuffle(i):                      # a side step in the stance: lead pad pushes out, trail pad closes
    out = [0.0, 0.16, 0.06, 0.0][i]
    shift(x=0.05 * [0, 1, 1.5, 0.5][i], z=-0.02 - 0.015 * (i % 2))
    leg('L', ANKLE['L'] + V((out, 0, 0.03 if i == 1 else 0)), pole=(0, -1, 0))
    leg('R', ANKLE['R'] + V((0.05 * [0, 0, 1, 0.5][i], 0, 0.03 if i == 2 else 0)), pole=(0, -1, 0))
    turn('upperarm.L', z=0.04)
    lift(['hand.R'], 0.0)

def ref_stand():
    shift(z=-0.01); plant(pole=(0, -1, 0))
    for s in 'LR': turn('upperarm.' + s, z=-(1 if s == 'L' else -1) * 0.06)

REF_UP = ((0.08, -0.05, 1.0), (0.02, -0.05, 1.0))
def ref_armup(i):                           # delayed penalty: right arm straight up, held while play goes on
    ref_stand(); arm('R', *REF_UP); turn('head', z=[0, 0.2][i])

def ref_whistle(i):                         # whistle to the mouth, arm up
    ref_stand(); arm('R', *REF_UP)
    arm('L', (0.25, -0.55, -0.55), (-0.45, -0.55, 0.70))
    turn('neck', x=[0, -0.08][i])

def ref_tripping(i):                        # right hand sweeps across below the knee, body bends
    ref_stand(); turn('spine', x=[0.2, 0.45][i]); turn('chest', x=0.1)
    arm('R', (0.2, -0.55, -0.8), ((0.9, -0.3, -0.35) if i else (-0.3, -0.5, -0.8)))

def ref_hooking(i):                         # both hands pull back toward the stomach, as if pulling on a stick
    ref_stand(); turn('spine', x=0.08)
    k = [0.0, 1.0][i]
    for s in 'LR': arm(s, (0.15, -0.9 + 0.8 * k, -0.4 - 0.2 * k), (-0.3 * k, -1.0, 0.1 + 0.4 * k))

def ref_slashing(i):                        # right hand chops down across the left forearm
    ref_stand()
    arm('L', (0.25, -0.75, -0.6), (-0.6, -0.8, 0.0))
    arm('R', (0.25, -0.7, -0.2 + 0.7 * (1 - i)), (0.55, -0.8, 0.6 - 0.9 * i))

def ref_goal(i):                            # points into the net with the right arm, held
    ref_stand(); turn('chest', z=-0.15 * i)
    arm('R', ((-0.4, -0.85, -0.2) if i else (0.2, -0.45, -0.85)), ((-0.5, -0.85, 0.0) if i else (-0.2, -0.6, -0.75)))

def ref_drop(i):                            # puck held out over the dot, bent forward, then thrown down
    bend = [0.25, 0.55, 0.55, 0.35][i]
    shift(z=-0.04 - 0.08 * (bend > 0.4)); plant(spread=0.05, pole=(0, -1, 0)); turn('pelvis', x=bend * 0.4)
    turn('spine', x=bend * 0.6); turn('neck', x=-bend * 0.4)
    turn('upperarm.L', z=0.06)
    arm('R', [(0.05, -0.75, -0.45), (0.0, -0.85, -0.40), (0.0, -0.55, -0.85), (0.1, -0.35, -0.9)][i],
        [(-0.1, -0.85, 0.05), (-0.05, -0.95, -0.15), (-0.05, -0.30, -0.95), (0.0, 0.0, -1.0)][i])

def ref_set(i):                             # set at the dot for the drop: bent well in over it, the puck held out low, held
    bend = 0.75 + 0.03 * i
    shift(z=-0.13); plant(spread=0.08, pole=(0, -1, 0)); turn('pelvis', x=bend * 0.4)
    turn('spine', x=bend * 0.6); turn('neck', x=-bend * 0.5)
    turn('upperarm.L', z=0.06)
    arm('R', (0.0, -0.85, -0.45), (-0.05, -0.95, -0.20))

# ---------------- bench clips (A11) ----------------
# Bench figures stand or sit behind the far boards (board top 1.07 m above the soles), so only the
# helmet and shoulders (seated, ~8 px) or the chest up (standing, ~14 px) show: the upper body is
# posed big, the legs only sane. SEATED: the hip joints come down to 0.48 m, which puts the seat of
# the pants, so the bench top, at about 0.45 m above the skate soles; thighs level, shins upright,
# feet flat on the floor. STANDING AT THE BOARDS: the board face is 0.45 m in front of the origin
# (-Y), its top at BOARD_TOP. Cheer reuses celebrate.
SEAT_DROP = 0.38                            # 0.86 m rest hip height down to 0.48
BOARD_TOP, BOARD_Y = 1.07, -0.45

def hide_stick(): PB['stick'].scale = (1e-3, 1e-3, 1e-3); upd()

def reach(s, P, pole=None, hand=None):
    """two-bone arm IK: the wrist at armature-space point P, elbow toward the pole, hand along hand"""
    sx = 1 if s == 'L' else -1
    pole = V(pole or (sx * 1.0, 0.3, -0.6))
    H = PB['upperarm.' + s].head.copy(); A = V(P)
    L1, L2 = B['upperarm.' + s].length, B['forearm.' + s].length
    d = A - H; dist = min(d.length, L1 + L2 - 1e-4); n = d.normalized()
    a = (L1 * L1 - L2 * L2 + dist * dist) / (2 * dist); h = math.sqrt(max(0.0, L1 * L1 - a * a))
    pv = (pole - n * pole.dot(n)).normalized()
    K = H + n * a + pv * h
    aim('upperarm.' + s, K - H); aim('forearm.' + s, H + n * dist - PB['forearm.' + s].head)
    aim('hand.' + s, hand or (H + n * dist - K))

def stick_dir(d, floor=None):
    """turn the stick about the top glove so the blade end points along d; with floor, slide it
    through the glove until the blade's lowest point rests at that height"""
    p = PB[SB]; m = p.matrix @ B[SB].matrix_local.inverted(); w = V(d).normalized()
    G = m @ GRIP; rd = (m.to_3x3() @ D0).normalized().rotation_difference(w).to_matrix().to_4x4()
    p.matrix = Matrix.Translation(G) @ rd @ Matrix.Translation(-G) @ p.matrix; upd()
    if floor is not None and abs(w.z) > 1e-3:
        p.matrix = Matrix.Translation(w * (floor - stick_low()) / w.z) @ p.matrix; upd()

def seat(lean=0.0, spread=0.0):
    """down on the bench: hips at 0.48, thighs forward, shins down to flat feet"""
    shift(z=-SEAT_DROP)
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        leg(s, V((sx * (0.19 + spread), -0.40, 0.12)), pole=(sx * 0.15, -1.0, 1.0))
    turn('spine', x=lean)

def knee(s): return PB['shin.' + s].head.copy()

def stick_between_knees(z=0.74):
    """the stick stands up between the knees, the top glove on the shaft above them, blade on the floor"""
    reach('R', V((-0.13, -0.46, z)), pole=(-1, 0.2, -0.4))
    stick_dir((0.20, -0.22, -1.0), floor=0.0)              # the knob leans back past the right shoulder, clear of the face

def skater_sit(i):                          # watching the play: head follows it, a small bob
    seat(lean=0.08 + 0.02 * (i % 2)); turn('chest', x=-0.04)
    turn('neck', x=-0.06 + 0.04 * (i % 2)); turn('neck', z=[0.0, 0.2, 0.2, -0.2][i]); turn('head', z=[0.0, 0.4, 0.4, -0.35][i], x=[0, 0.1, 0, 0.1][i])
    stick_between_knees()
    reach('L', knee('L') + V((-0.02, -0.04, 0.06)), pole=(1, 0.3, -0.3))

def skater_sit_tired(i):                    # bent far forward, forearms on the knees, chest heaving
    heave = [0.0, 1.0, 0.4][i]
    seat(lean=0.55 - 0.05 * heave); turn('chest', x=0.25 - 0.12 * heave)
    for s in 'LR': turn('clavicle.' + s, y=(1 if s == 'L' else -1) * (-0.10 * heave))
    turn('neck', x=-0.45); turn('head', x=-0.1 + 0.08 * heave)
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        reach(s, knee(s) + V((-sx * 0.13, -0.16, -0.02)), pole=(sx * 0.4, 0.2, -1.0), hand=(-sx * 0.3, -0.5, -0.8))
    stick_dir((-0.35, -0.55, -0.75), floor=0.0)

def skater_slump(i):                        # head hung, shoulders dropped, hands loose between the knees
    seat(lean=0.38 + 0.03 * i); turn('chest', x=0.15)
    for s in 'LR': turn('clavicle.' + s, y=(1 if s == 'L' else -1) * 0.30)
    turn('neck', x=0.55 + 0.05 * i); turn('head', x=0.25, z=[0.0, -0.12][i])
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        reach(s, V((sx * 0.05, -0.48, 0.42)), pole=(sx * 0.6, 0.4, -1.0), hand=(0, -0.2, -1))
    stick_dir((-0.25, -0.75, -0.6), floor=0.0)

def skater_chat(i):                         # turned to the neighbour on the left, the left hand talking
    seat(lean=0.10); yaw = 0.35
    turn('spine', z=yaw * 0.5); turn('chest', z=yaw * 0.5); turn('neck', z=yaw * 0.6); turn('head', z=0.35 + [0, 0.08, 0, -0.06][i], x=[0, -0.08, 0, 0.04][i])
    stick_between_knees()
    hand = [(0.40, -0.42, 1.00), (0.48, -0.36, 1.18), (0.36, -0.48, 0.92), (0.52, -0.30, 1.10)][i]
    reach('L', V(hand), pole=(1, 0.4, -0.8), hand=[(0.5, -0.6, 0.6), (0.7, -0.2, 0.7), (0.4, -0.8, 0.3), (0.8, 0.1, 0.6)][i])

def skater_helmet_off(i):                   # seated: left hand up to the helmet, lifts it off, brings it down (held)
    seat(lean=0.10); stick_between_knees()
    turn('neck', x=[0.0, 0.10, -0.15, 0.0][i]); turn('head', x=[0, 0.15, -0.1, 0][i])
    if i == 0: reach('L', knee('L') + V((-0.02, -0.04, 0.06)), pole=(1, 0.3, -0.3))
    else:
        top = PB['head'].tail
        hand = [None, top + V((0.12, -0.02, -0.02)), top + V((0.16, -0.06, 0.30)), V((0.42, -0.30, 0.70))][i]
        reach('L', hand, pole=(1, 0.2, -0.3 if i == 3 else 0.4), hand=[None, (-0.6, 0, 0.8), (-0.2, 0, 1), (0.2, -0.6, -0.7)][i])

def stand_lean(lean=0.30):
    shift(y=0.04, z=-0.03); turn('pelvis', x=0.08); plant(spread=0.03, pole=(0, -1, 0))
    turn('spine', x=lean)

def skater_stand_boards(i):                 # leaning on the boards, both gloves on the top of them
    stand_lean(0.30 + 0.03 * i); turn('neck', x=-0.25); turn('head', z=[0.0, 0.2][i])
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        reach(s, V((sx * 0.24, BOARD_Y - 0.02, BOARD_TOP + 0.05)), pole=(sx * 1.0, 0.2, -0.3), hand=(sx * 0.1, -1.0, -0.3))
    stick_dir((0.1, 0.12, -1.0), floor=0.02)

def skater_bang(i):                         # stick up high in the top glove, then hammered down on the board top
    up = [1.0, 0.0, 0.6, 0.0][i]
    stand_lean(0.22 + 0.10 * (1 - up)); turn('chest', z=0.12); turn('neck', x=-0.2 - 0.1 * up); turn('head', x=-0.1 * up)
    reach('L', V((0.24, BOARD_Y - 0.02, BOARD_TOP + 0.05)), pole=(1, 0.2, -0.3), hand=(0.1, -1.0, -0.3))
    reach('R', V((-0.18, -0.28 + 0.06 * up, 1.24 + 0.30 * up)), pole=(-1, 0.2, -0.5 + 0.6 * up))
    stick_dir((0.15, -0.45 + 0.25 * up, 0.88) if up > 0.5 else (0.10, -0.80, -0.60))   # down: the shaft lands on the board top

def skater_argue(i):                        # at the boards, arms flung wide at the ice, then a jab and a fist up
    hide_stick(); stand_lean(0.12)
    turn('chest', z=[0.0, -0.2, 0.15, -0.1][i]); turn('neck', x=-0.25); turn('head', x=-0.15, z=[0, -0.2, 0.1, 0][i])
    if i in (0, 2):                         # arms flung out wide, palms up
        w = [1.0, 0, 0.8, 0][i]
        for s in 'LR':
            sx = 1 if s == 'L' else -1
            arm(s, (0.8, -0.45, 0.05 + 0.2 * w), (0.75, -0.55, 0.35 * w + 0.1))
    else:                                   # right arm jabs a point out at the ice; the left fist cocked, then pumped up
        arm('R', (0.55, -0.75, 0.35), (0.6, -0.7, 0.4))
        if i == 1: arm('L', (0.45, -0.1, -0.6), (-0.1, -0.6, 0.8))
        else: arm('L', (0.35, -0.1, 0.9), (0.1, -0.05, 1.0))

# ---------------- bench staff clips (A12) ----------------
# The coach and the trainer stand on the step behind the bench, seen chest up over the seated
# players at ~14 px, so the arms are posed big. Shoes on the floor: plant keeps the soles at 0.
def staff_stand(): plant(pole=(0, -1, 0))

def fold_arms():
    reach('L', V((-0.09, -0.27, 1.21)), pole=(1, 0.2, -0.5), hand=(-1, 0.2, 0.0))       # each hand tucked at the other elbow
    reach('R', V((0.09, -0.30, 1.17)), pole=(-1, 0.2, -0.5), hand=(1, 0.2, 0.0))

def staff_idle(i):                          # arms folded, weight shifting from foot to foot, the head following the play
    shift(x=0.025 * [0, 1, 0, -1][i]); staff_stand()
    turn('spine', z=0.04 * [0, 1, 0, -1][i]); fold_arms()
    turn('neck', x=-0.05); turn('head', z=[0.0, 0.3, 0.3, -0.2][i])

def staff_clap(i):                          # hands apart, then together in front of the chest
    staff_stand(); turn('spine', x=0.06); turn('neck', x=-0.08)
    w = [0.20, 0.0][i % 2]
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        reach(s, V((sx * (0.035 + w), -0.40, 1.20 + 0.03 * (i % 2))), pole=(sx * 1, 0.3, -0.8), hand=(-sx, -0.4, 0.4))

def staff_arms_up(i):                       # both arms up high in a V, pumping, up on the toes
    up = [1.0, 0.4, 1.0, 0.4][i]
    shift(z=0.03 * up); plant(pole=(0, -1, 0)); turn('spine', x=-0.08 * up); turn('neck', x=-0.15 * up)
    for s in 'LR': arm(s, (0.35 + 0.25 * (1 - up), -0.1, 0.93 - 0.4 * (1 - up)), (0.22, -0.12, 0.97))
    turn('head', x=-0.1 * up)

def staff_point(i):                         # the right arm comes up, then points forward and down at the ice, leaning in
    staff_stand(); lean = [0.0, 0.10, 0.28, 0.32][i]
    turn('spine', x=lean); turn('neck', x=-0.12 * (i > 1)); turn('head', x=0.1 * (i > 1))
    reach('L', V((0.30, 0.02, 0.98 - 0.05 * lean)), pole=(1, 0.6, 0), hand=(-0.4, -0.3, -0.8))      # left hand on the hip
    if i == 0: arm('R', (0.05, 0.0, -1.0), (0.05, -0.1, -1.0))
    elif i == 1: arm('R', (0.25, -0.70, 0.55), (0.20, -0.70, 0.70))
    else: arm('R', (0.10, -0.92, -0.20 - 0.05 * (i - 2)), (0.05, -0.85, -0.50), (0.0, -0.8, -0.6))

def staff_lean_talk(i):                     # leaning over a seated player: right hand on his shoulder, head down to him, left hand talking
    shift(y=0.05); staff_stand()
    turn('spine', x=0.42); turn('chest', x=0.15); turn('neck', x=0.15); turn('head', x=0.25 + [0, 0.08, 0, 0.05][i], z=[0, 0.1, 0, -0.08][i])
    reach('R', V((-0.12, -0.45, 1.0)), pole=(-1, 0.3, -0.2), hand=(0, -0.4, -0.9))
    hand = [(0.30, -0.50, 1.12), (0.34, -0.60, 1.25), (0.26, -0.55, 1.05), (0.38, -0.45, 1.20)][i]
    reach('L', V(hand), pole=(1, 0.4, -0.6), hand=[(0.2, -0.8, 0.4), (0.5, -0.6, 0.6), (0.1, -0.9, 0.1), (0.6, -0.4, 0.5)][i])

def staff_walk(i):                          # in place, the game moves it: heel strike, pass, push off; arms swing against the legs
    ph = 2 * math.pi * i / 8
    shift(z=-0.045 + 0.02 * abs(math.sin(ph)))
    for s, o in (('L', 0.0), ('R', math.pi)):
        c = math.cos(ph + o); sw = math.sin(ph + o) < -1e-6           # the swing half: lifted and coming forward
        leg(s, ANKLE[s] + V((0, -0.17 * c, 0.07 * abs(math.sin(ph + o)) if sw else 0.0)), pole=(0, -1, 0))
        turn('upperarm.' + s, x=0.38 * c); turn('forearm.' + s, x=-0.25 - 0.15 * max(0.0, -c))
    turn('chest', z=0.08 * math.cos(ph)); turn('head', z=-0.06 * math.cos(ph))

# ---------------- season ending clips ----------------
# The end-of-season scenes (Season/Endings) pose a few figures up close: the captain lifting the
# trophy, a beaten player down on one knee, the handshake line, and two staff walks carrying
# something (the coach's box, the relegated player's hockey bag). The game puts the trophy, the box
# and the bag between the hands, so the hand points here are mirrored in Season/Endings/EndingPoses.cs.
TROPHY_HANDS = (0.24, -0.10, (1.88, 2.02))        # x either side, y forward, z low and high per pose

def skater_trophy(i):                       # the trophy held high overhead in both gloves, lifted and lowered, head back, the stick dropped
    hide_stick(); z = TROPHY_HANDS[2][i]
    shift(z=[-0.02, 0.01][i]); turn('pelvis', x=-0.04); plant(spread=0.05, pole=(0, -1, 0))
    turn('spine', x=-0.10); turn('chest', x=-0.06); turn('neck', x=0.12); turn('head', x=0.22)
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        reach(s, V((sx * TROPHY_HANDS[0], TROPHY_HANDS[1], z)), pole=(sx * 1.0, 0.4, -0.2), hand=(-sx * 0.3, 0.0, 1.0))

def skater_kneel(i):                        # down on the right knee at the end of a lost final, both gloves on the stick's knob, head hung
    shift(z=-0.36 - 0.01 * i, y=0.04); turn('pelvis', x=0.10)
    leg('L', V((0.17, -0.42, 0.09)), pole=(0.2, -1.0, 0.6), toe=0.1)
    leg('R', V((-0.15, 0.36, 0.06)), pole=(-0.1, -1.0, -0.6))
    turn('spine', x=0.32 + 0.03 * i); turn('chest', x=0.12)
    for s in 'LR': turn('clavicle.' + s, y=(1 if s == 'L' else -1) * 0.18)
    turn('neck', x=0.55); turn('head', x=0.30)
    reach('R', V((-0.02, -0.46, 0.80 - 0.01 * i)), pole=(-1, 0.3, -0.5))
    stick_dir((0.02, -0.18, -1.0), floor=0.0)
    m = PB[SB].matrix @ B[SB].matrix_local.inverted()
    reach('L', m @ GRIP + V((0.03, -0.02, 0.07)), pole=(1, 0.3, -0.5), hand=(-0.2, -0.3, -0.9))
    ground()

def skater_handshake(i):                    # the handshake line: gloves off and the stick gone, upright on the glide, the right hand out to the man passing on that side
    hide_stick()
    shift(z=-0.04); turn('pelvis', x=0.06); plant(spread=0.04, pole=(0, -1, 0))
    turn('spine', x=0.10); turn('neck', x=0.18); turn('head', x=0.10, z=-0.25)
    pump = [0.0, 1.0][i]
    arm('R', (0.30, -0.80, -0.40 + 0.10 * pump), (0.25, -0.95, 0.02 + 0.10 * pump))
    arm('L', (0.10, 0.05, -1.0), (0.12, -0.25, -0.95))

def skater_pump(i):                         # one fist pumping high, the stick held low in the other glove, a little hop on the up
    up = [0.2, 1.0, 0.45, 0.95][i]
    shift(z=0.02 * up - 0.02); turn('pelvis', x=-0.02)
    plant(spread=0.05, pole=(0, -1, 0))
    turn('spine', x=-0.06 * up, z=0.10); turn('neck', x=0.06); turn('head', x=0.12 * up, z=0.15)
    arm('L', (0.45, -0.2, -0.3 + 1.2 * up), (0.25, -0.25, 0.2 + 0.8 * up))
    lift(['clavicle.R'], 0.0)

def skater_stick_up(i):                     # the stick raised high in one glove like a salute, the other arm out wide
    shift(z=[0.0, 0.015][i]); plant(spread=0.06, pole=(0, -1, 0))
    turn('spine', x=-0.08); turn('neck', x=0.08); turn('head', x=0.2, z=-0.1)
    reach('R', V((-0.22, -0.06, 2.02 - 0.04 * i)), pole=(-1, 0.3, -0.2))
    stick_dir((-0.05, 0.12, 1.0))
    arm('L', (0.85, -0.25, 0.25 + 0.1 * i), (0.8, -0.35, 0.45 + 0.1 * i))

def skater_hug(i):                          # arms round a teammate in front, leaning in, the stick dropped
    hide_stick()
    shift(y=-0.04, z=-0.03); turn('pelvis', x=0.08); plant(spread=0.06, pole=(0, -1, 0))
    turn('spine', x=0.22 + 0.03 * i, z=[0.06, -0.06][i]); turn('chest', x=0.05); turn('head', z=0.35, x=0.1)
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        reach(s, V((sx * -0.10, -0.52, 1.30 - 0.03 * i)), pole=(sx * 1.0, 0.0, -0.4), hand=(-sx, 0.2, 0.0))

def skater_jump(i):                         # a jump for joy: crouch, up in the air with the knees tucked and both arms up, land
    h = [0.0, 0.32, 0.26, 0.0][i]; crouch = [0.16, 0.0, 0.0, 0.10][i]
    shift(z=h - crouch); turn('pelvis', x=0.1 * (crouch > 0))
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        tuck = 0.18 if h > 0 else 0.0
        leg(s, ANKLE[s] + V((sx * 0.03, -0.05 if h else 0.0, h - crouch + tuck)), pole=(0, -1, 0))
    up = 1.0 if h > 0 else 0.6
    for s in 'LR': arm(s, (0.45, -0.1, up), (0.3, -0.15, 1.0))
    turn('head', x=0.15 * up)
    grip_mid((0.5, -0.1, 0.85))

def carry_walk(i):                          # the staff walk's legs and bob, the head down, for the carrying walks
    ph = 2 * math.pi * i / 8
    shift(z=-0.045 + 0.02 * abs(math.sin(ph)))
    for s, o in (('L', 0.0), ('R', math.pi)):
        c = math.cos(ph + o); sw = math.sin(ph + o) < -1e-6
        leg(s, ANKLE[s] + V((0, -0.15 * c, 0.06 * abs(math.sin(ph + o)) if sw else 0.0)), pole=(0, -1, 0))
    turn('chest', z=0.05 * math.cos(ph))
    return ph

CARRY_BOX = (0.19, -0.30, 1.02)             # each hand at a side of the box, x mirrored
def staff_carry_box(i):                     # walking out with a cardboard box held in both hands at the waist
    carry_walk(i); turn('spine', x=-0.04); turn('neck', x=0.22); turn('head', x=0.12)
    for s in 'LR':
        sx = 1 if s == 'L' else -1
        reach(s, V((sx * CARRY_BOX[0], CARRY_BOX[1], CARRY_BOX[2])), pole=(sx * 1.0, 0.6, -0.8), hand=(-sx * 1.0, -0.4, 0.0))

def staff_carry_bag(i):                     # walking away with a hockey bag on the right shoulder, the right hand on the strap, the left arm swinging, head down
    ph = carry_walk(i); turn('spine', x=0.08); turn('neck', x=0.38); turn('head', x=0.18)
    turn('clavicle.R', y=0.10)
    reach('R', V((-0.17, -0.16, 1.30)), pole=(-0.3, 0.2, -1.0), hand=(0.2, -0.2, 1.0))
    turn('upperarm.L', x=0.30 * math.cos(ph)); turn('forearm.L', x=-0.25 - 0.12 * max(0.0, -math.cos(ph)))

# name: (poses, frames each pose is held at 24 fps, loops?)
CLIPS = {'skater': {'skate': (skater_skate, 8, 2, True), 'idle': (skater_idle, 4, 6, True), 'shoot': (skater_shoot, 6, 3, False), 'windup': (skater_windup, 3, 2, False),
                    'pass': (skater_pass, 5, 2, False), 'check': (skater_check, 4, 3, False), 'celebrate': (skater_celebrate, 4, 5, True),
                    'punch': (skater_punch, 5, 3, False), 'block': (skater_block, 3, 3, False), 'grab': (skater_grab, 4, 5, True),
                    'fall': (skater_fall, 4, 4, False),
                    'sit': (skater_sit, 4, 14, True), 'sit_tired': (skater_sit_tired, 3, 8, True),
                    'stand_boards': (skater_stand_boards, 2, 20, True), 'slump': (skater_slump, 2, 24, True),
                    'argue': (skater_argue, 4, 8, True), 'bang': (skater_bang, 4, 4, True),
                    'chat': (skater_chat, 4, 8, True), 'helmet_off': (skater_helmet_off, 4, 6, False),
                    'faceoff_crouch': (skater_faceoff_crouch, 2, 18, True), 'faceoff_centre': (skater_faceoff_centre, 2, 18, True),
                    'trophy': (skater_trophy, 2, 10, True), 'kneel': (skater_kneel, 2, 30, True), 'handshake': (skater_handshake, 2, 5, True),
                    'pump': (skater_pump, 4, 4, True), 'stick_up': (skater_stick_up, 2, 9, True), 'hug': (skater_hug, 2, 12, True),
                    'jump': (skater_jump, 4, 4, True)},
         'goalie': {'ready': (goalie_ready, 4, 5, True), 'butterfly': (goalie_butterfly, 5, 3, False),
                    'glove': (goalie_glove, 4, 3, False), 'blocker': (goalie_blocker, 3, 3, False), 'shuffle': (goalie_shuffle, 4, 3, True)},
         'referee': {'skate': (ref_skate, 8, 3, True), 'idle': (ref_idle, 4, 8, True),
                     'armup': (ref_armup, 2, 12, True), 'whistle': (ref_whistle, 2, 6, False), 'tripping': (ref_tripping, 2, 6, True),
                     'hooking': (ref_hooking, 2, 6, True), 'slashing': (ref_slashing, 2, 6, True), 'goal': (ref_goal, 2, 6, False),
                     'drop': (ref_drop, 4, 4, False), 'set': (ref_set, 2, 18, True)},
         'staff': {'idle': (staff_idle, 4, 12, True), 'clap': (staff_clap, 4, 4, True), 'arms_up': (staff_arms_up, 4, 5, True),
                   'point': (staff_point, 4, 6, False), 'lean_talk': (staff_lean_talk, 4, 8, True), 'walk': (staff_walk, 8, 3, True),
                   'carry_box': (staff_carry_box, 8, 3, True), 'carry_bag': (staff_carry_bag, 8, 3, True)}}[NAME]

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
            p.keyframe_insert('scale', frame=f, group=p.name)
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
    kit = {'referee': 'ref', 'staff': 'coach'}.get(NAME, 'home')
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
