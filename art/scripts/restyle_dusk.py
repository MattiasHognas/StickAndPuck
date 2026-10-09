"""Dusk restyle of the broadcast still: warm peach
key light, cool slate shadows, muted coral / gold / teal / cream colours, no pure white or
black, and matte ice with flat diagonal light bands instead of glossy glare.
Opens arena.blend, saves arena_dusk.blend, renders renders/arena_dusk_beauty.png."""
import bpy, math, os

def lin(h):
    c = [int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)

def setcol(m, h):
    nt = m.node_tree
    for n in nt.nodes:
        if n.type == 'BSDF_PRINCIPLED': n.inputs['Base Color'].default_value = lin(h); m.diffuse_color = lin(h)
        if n.type == 'VECT_MATH': n.inputs[1].default_value = lin(h)[:3]   # toon player material

MAP = {
    'red_line': '#c8574a', 'blue_line': '#3e5875', 'crease': '#7f9cae', 'boards': '#eadcc0', 'kick': '#e0a34a',
    'cap': '#3e5875', 'post': '#8a8790', 'glass': '#d8dcd2', 'net': '#e8dcc8', 'net_frame': '#c8574a',
    'stand_a': '#2a2230', 'stand_b': '#33293a', 'fascia': '#211b28', 'floor': '#18131f', 'puck': '#18131f',
}
SHIRTS = ['#e96f5d', '#c47262', '#f2c14e', '#3e5875', '#31556e', '#27383e', '#a34e4d', '#eec88a', '#596b6c', '#7b4c47', '#d2ad7f']
SKINS = ['#f0b48a', '#d0906a', '#9e655c', '#f6c9a0']
dim = lambda h, k=0.72: '#%02x%02x%02x' % tuple(int(int(h[i:i + 2], 16) * k) for i in (1, 3, 5))
for i, h in enumerate(SHIRTS): MAP['shirt%d' % i] = dim(h)
for i, h in enumerate(SKINS): MAP['skin%d' % i] = h
# sponsor panels: cream, coral, deep teal, plum, gold and brick instead of white / red / navy / black
AD = {(1.0, 1.0, 1.0): '#efe0c2', (0.578, 0.014, 0.027): '#c8574a', (0.014, 0.078, 0.392): '#27383e',
      (0.006, 0.007, 0.016): '#2a2230', (0.871, 0.527, 0.08): '#e0a34a', (0.042, 0.023, 0.016): '#613b3a',
      (0.012, 0.028, 0.156): '#27383e', (0.888, 0.761, 0.578): '#f9da93', (0.815, 0.072, 0.028): '#e96f5d',
      (0.042, 0.105, 0.434): '#3e5875'}
KIT = {'yellow': '#f2c14e', 'blue': '#2f5f8f', 'boot': '#2a2430', 'grip': '#2a2c36', 'shaft': '#3a3340',
       'tape': '#22202a', 'holder': '#d9d0c4', 'steel': '#8a8f9c', 'visor': '#27383e', 'skin': '#f0b48a'}
AWAY = {'yellow': '#d0564a', 'blue': '#2a2c40'}

for m in bpy.data.materials:
    if not m.use_nodes: continue
    stem, _, suf = m.name.partition('.')
    if m.name in MAP: setcol(m, MAP[m.name])
    elif m.name.startswith('ad_'):
        b = m.node_tree.nodes.get('Principled BSDF')
        key = tuple(round(x, 3) for x in b.inputs['Base Color'].default_value[:3])
        best = min(AD, key=lambda k: sum((a - c) ** 2 for a, c in zip(k, key)))
        setcol(m, AD[best])
    elif stem in KIT:
        away = suf.isdigit() and int(suf) >= 5          # .005 to .009 are the away players
        setcol(m, AWAY.get(stem, KIT[stem]) if away else KIT[stem])

# LED band becomes a warm lantern strip
led = bpy.data.materials['led'].node_tree.nodes['Principled BSDF']
led.inputs['Base Color'].default_value = lin('#f9da93'); led.inputs['Emission Color'].default_value = lin('#fba771')
led.inputs['Emission Strength'].default_value = 2.0

# ---- ice: matte, with flat diagonal light bands in world space ----
ice = bpy.data.materials['ice']; nt = ice.node_tree; b = nt.nodes['Principled BSDF']
b.inputs['Roughness'].default_value = 0.6
for k in ('Specular IOR Level', 'Specular'):
    if k in b.inputs: b.inputs[k].default_value = 0.15
geo = nt.nodes.new('ShaderNodeNewGeometry'); sep = nt.nodes.new('ShaderNodeSeparateXYZ')
nt.links.new(geo.outputs['Position'], sep.inputs[0])
ANG = math.radians(-36); PERIOD = 8.0     # bands lean across the rink like the street's light shafts
def math_node(op, a=None, b_=None):
    n = nt.nodes.new('ShaderNodeMath'); n.operation = op
    for i, v in enumerate((a, b_)):
        if v is None: continue
        if isinstance(v, (int, float)): n.inputs[i].default_value = v
        else: nt.links.new(v, n.inputs[i])
    return n.outputs[0]
u = math_node('ADD', math_node('MULTIPLY', sep.outputs['X'], math.cos(ANG)), math_node('MULTIPLY', sep.outputs['Y'], math.sin(ANG)))
f = math_node('FRACT', math_node('DIVIDE', u, PERIOD))
wide = math_node('LESS_THAN', f, 0.16)                                            # a broad band
thin = math_node('MULTIPLY', math_node('GREATER_THAN', f, 0.27), math_node('LESS_THAN', f, 0.305))   # and a thin line beside it
mask = math_node('MAXIMUM', wide, thin)
mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'
nt.links.new(mask, mix.inputs['Factor'])
mix.inputs[6].default_value = lin('#c9c6cf'); mix.inputs[7].default_value = lin('#e3d8c6')   # cool ice, warm light on it
nt.links.new(mix.outputs[2], b.inputs['Base Color'])

# ---- light: warm peach key, cool slate fill, no glossy streak lights ----
scene = bpy.context.scene
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = lin('#1f1a26')
for ob in scene.objects:
    if ob.type != 'LIGHT': continue
    L = ob.data
    if ob.name == 'Sun': L.color = (1.0, 0.86, 0.70); L.energy = 1.7
    elif ob.name == 'Fill': L.color = (0.62, 0.70, 0.92); L.energy = 0.55
    elif ob.name.startswith('Rig'): L.color = (1.0, 0.85, 0.66); L.energy = 700
    elif ob.name.startswith('Far'): L.energy = 0.0

# ---- a string of warm lanterns over the far glass, like the street's hanging lights ----
import bmesh
from mathutils import Vector as V, Matrix
C = bpy.data.collections['Rink']
def lamp_mat(name, h, emit=None, k=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = lin(h); b.inputs['Roughness'].default_value = 0.8
    if emit: b.inputs['Emission Color'].default_value = lin(emit); b.inputs['Emission Strength'].default_value = k
    return m
glow, cap, cable = lamp_mat('lantern', '#f9da93', '#fba771', 1.1), lamp_mat('lantern_cap', '#2a2230'), lamp_mat('cable', '#211b28')
bm = bmesh.new(); bmc = bmesh.new()
Y, Z0, SAG, SPAN = 13.4, 4.9, 0.6, 6.0     # just behind the far glass, above its top edge
x = -27.0; n = 0
while x < 27.0:
    for k in range(12):              # sagging cable segments between two hang points
        t0, t1 = k / 12, (k + 1) / 12
        p0 = V((x + t0 * SPAN, Y, Z0 - SAG * 4 * t0 * (1 - t0))); p1 = V((x + t1 * SPAN, Y, Z0 - SAG * 4 * t1 * (1 - t1)))
        d = p1 - p0; mid = (p0 + p1) / 2
        r = bmesh.ops.create_cube(bmc, size=1, matrix=Matrix.Translation(mid) @ d.to_track_quat('X', 'Z').to_matrix().to_4x4() @ Matrix.Diagonal((d.length, 0.09, 0.09, 1)))
    for t in (0.25, 0.5, 0.75):      # three lanterns hang on each span
        c = V((x + t * SPAN, Y, Z0 - SAG * 4 * t * (1 - t) - 0.42))
        r = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(c) @ Matrix.Diagonal((0.36, 0.36, 0.48, 1)))
        for f in {f for v in r['verts'] for f in v.link_faces}: f.material_index = 0
        r = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(c + V((0, 0, 0.3))) @ Matrix.Diagonal((0.44, 0.44, 0.12, 1)))
        for f in {f for v in r['verts'] for f in v.link_faces}: f.material_index = 1
        if n % 2 == 0:
            pl = bpy.data.lights.new('Lantern%d' % n, 'POINT'); pl.energy = 60; pl.color = (1.0, 0.72, 0.45); pl.shadow_soft_size = 0.3
            po = bpy.data.objects.new('Lantern%d' % n, pl); po.location = c; C.objects.link(po)
        n += 1
    x += SPAN
for name, b_, mats in (('Lanterns', bm, [glow, cap]), ('Cable', bmc, [cable])):
    me = bpy.data.meshes.new(name); b_.to_mesh(me); b_.free()
    for m in mats: me.materials.append(m)
    ob = bpy.data.objects.new(name, me); C.objects.link(ob)
for p in bpy.data.objects['Cable'].data.polygons: p.material_index = 0

bpy.ops.wm.save_as_mainfile(filepath=os.path.dirname(bpy.data.filepath) + '/arena_dusk.blend')
scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 640, 360, 100
scene.render.use_freestyle = False; scene.render.filter_size = 0.0
scene.eevee.taa_render_samples = 128
scene.render.filepath = os.path.dirname(bpy.data.filepath) + '/renders/arena_dusk_beauty.png'
bpy.ops.render.render(write_still=True)
result = {'ok': True, 'lanterns': n}
