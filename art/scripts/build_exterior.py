"""The arena from outside at night (the relegated ending, Season/Endings): a small exterior set the game
renders through its pixel pipeline like the arena. Low-poly boxes and cylinders in flat dusk palette
colours, no textures: the facade with pilasters, a cornice and a row of high windows (two still lit),
the glass entrance under a canopy with an exit light, a dark name board over it (the game prints the
club's name and crest on it), a street lamp, bollards, a bin, the pavement with its slab joints and
puddles, the kerb and the road.
Blender Z up; as everywhere, Blender (x, y, z) becomes the game's (x, z, -y), so the facade, at
Blender y = 10, faces the game's +z camera from z = -10.
Writes art/scene/exterior.glb and art/scene/exterior_meshes.json (the meshes in Raylib load order,
with their part, material and whether they are unlit). Parts the game picks out by name: Sign (the
name board the letters go on), LampLight (the lamp's glass), and every part whose material is unlit.
Run:  blender -b --python-exit-code 1 --python art/scripts/build_exterior.py   (or with ART / SAP_ART set)"""
import bpy, bmesh, math, os, json, struct, sys, glob
from mathutils import Vector
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

scene = bpy.context.scene
for ob in list(bpy.data.objects): bpy.data.objects.remove(ob)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.images):
    for d in list(coll): coll.remove(d)

def lin(h):
    c = [int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)

MATS = {}
def mat(name, hexc, unlit=False):
    """a flat palette colour; unlit ones export as KHR_materials_unlit (a Background surface)"""
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    if unlit:
        bg = nt.nodes.new('ShaderNodeBackground'); bg.inputs['Color'].default_value = lin(hexc)
        nt.links.new(bg.outputs['Background'], out.inputs['Surface'])
    else:
        b = nt.nodes.new('ShaderNodeBsdfPrincipled'); b.inputs['Base Color'].default_value = lin(hexc)
        b.inputs['Roughness'].default_value = 0.9
        nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
    MATS[name] = m
    return m

PARTS = {}                                 # part name -> list of (bmesh builder calls)
def part(name):
    if name not in PARTS:
        me = bpy.data.meshes.new(name); ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
        PARTS[name] = (ob, bmesh.new())
    return PARTS[name]

def box(name, m, x0, x1, y0, y1, z0, z1):
    """an axis-aligned box in Blender coordinates (y away from the camera, z up)"""
    ob, bm = part(name)
    if m.name not in [s.name for s in ob.data.materials]: ob.data.materials.append(m)
    mi = [s.name for s in ob.data.materials].index(m.name)
    vs = [bm.verts.new((x, y, z)) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    f = lambda a, b, c, d: bm.faces.new((vs[a], vs[b], vs[c], vs[d]))
    for face in (f(0, 1, 3, 2), f(4, 6, 7, 5), f(0, 4, 5, 1), f(2, 3, 7, 6), f(0, 2, 6, 4), f(1, 5, 7, 3)):
        face.material_index = mi

def cyl(name, m, cx, cy, z0, z1, r, n=8):
    ob, bm = part(name)
    if m.name not in [s.name for s in ob.data.materials]: ob.data.materials.append(m)
    mi = [s.name for s in ob.data.materials].index(m.name)
    lo = [bm.verts.new((cx + r * math.cos(2 * math.pi * k / n), cy + r * math.sin(2 * math.pi * k / n), z0)) for k in range(n)]
    hi = [bm.verts.new((cx + r * math.cos(2 * math.pi * k / n), cy + r * math.sin(2 * math.pi * k / n), z1)) for k in range(n)]
    for k in range(n):
        bm.faces.new((lo[k], lo[(k + 1) % n], hi[(k + 1) % n], hi[k])).material_index = mi
    bm.faces.new(list(reversed(lo))).material_index = mi
    bm.faces.new(hi).material_index = mi

def disc(name, m, cx, cy, z, rx, ry, n=10):
    ob, bm = part(name)
    if m.name not in [s.name for s in ob.data.materials]: ob.data.materials.append(m)
    mi = [s.name for s in ob.data.materials].index(m.name)
    vs = [bm.verts.new((cx + rx * math.cos(2 * math.pi * k / n), cy + ry * math.sin(2 * math.pi * k / n), z)) for k in range(n)]
    bm.faces.new(vs).material_index = mi

# ---------------- palette ----------------
ASPHALT, PAVE, JOINT, KERB = mat('asphalt', '#2a2230'), mat('pavement', '#33293a'), mat('joint', '#2a2230'), mat('kerb', '#74666b')
PUDDLE = mat('puddle', '#3e5875')
WALL, PILASTER, CORNICE, PLINTH = mat('wall', '#423236'), mat('pilaster', '#555f6b'), mat('cornice', '#33293a'), mat('plinth', '#2a2c36')
GLASS, MULLION, CANOPY = mat('glass', '#31556e'), mat('mullion', '#211b28'), mat('canopy', '#33293a')
SIGN = mat('sign', '#211b28')
METAL = mat('metal', '#555f6b')
LOBBY = mat('lobby', '#9e655c', unlit=True)          # a dim warm glow behind the doors: the night staff's light
EXIT = mat('exit', '#2c6e4a', unlit=True)
WINDOW_LIT = mat('window_lit', '#c8963c', unlit=True)
LAMP = mat('lamp', '#f9da93', unlit=True)

FACE = 10.0                                          # the facade's front, Blender y (the game's z = -10)

# ---------------- ground ----------------
box('Road', ASPHALT, -60, 60, -30, -4.0, -0.2, 0.0)
box('Pavement', PAVE, -60, 60, -4.0, FACE + 0.01, -0.2, 0.0)
box('Kerb', KERB, -60, 60, -4.15, -3.85, 0.0, 0.12)
for k in range(-30, 31):                             # slab joints across the pavement, and two lines along it
    box('Joints', JOINT, k * 2.0 - 0.03, k * 2.0 + 0.03, -3.85, FACE, 0.0, 0.004)
for y in (-1.0, 2.5, 6.0):
    box('Joints', JOINT, -60, 60, y - 0.03, y + 0.03, 0.0, 0.004)
for (x, y, rx, ry) in ((3.0, -2.2, 1.6, 0.6), (8.5, 0.6, 2.2, 0.8), (-4.0, 1.4, 1.4, 0.5), (13.0, -1.6, 1.2, 0.45),
                       (-12.0, -2.6, 2.0, 0.6), (1.0, 4.4, 1.0, 0.4), (19.0, 2.0, 1.8, 0.6)):
    disc('Puddles', PUDDLE, x, y, 0.006, rx, ry)

# ---------------- the arena's facade ----------------
box('Facade', WALL, -24, 24, FACE, FACE + 20, 0.0, 10.0)
box('Facade', PLINTH, -24, 24, FACE - 0.08, FACE, 0.0, 0.6)
box('Facade', CORNICE, -24.5, 24.5, FACE - 0.5, FACE + 0.5, 9.2, 10.2)
for k in range(13):                                  # pilasters every 4 m, skipping the entrance and the sign
    x = -24 + k * 4.0
    if -15.0 < x < 3.0: continue
    box('Facade', PILASTER, x - 0.3, x + 0.3, FACE - 0.3, FACE, 0.0, 9.2)
for k in range(8):                                   # the high windows over the right wing
    x0 = 4.6 + k * 2.4
    box('Windows', GLASS, x0, x0 + 1.6, FACE - 0.04, FACE, 6.2, 7.8)
for x0 in (9.4, 16.6):                               # two still lit
    box('WindowsLit', WINDOW_LIT, x0 + 0.1, x0 + 1.5, FACE - 0.06, FACE - 0.04, 6.3, 7.7)

# ---------------- the entrance ----------------
EX0, EX1 = -11.0, -1.0
box('Entrance', GLASS, EX0, EX1, FACE - 0.05, FACE, 0.0, 3.4)
box('Lobby', LOBBY, -7.2, -4.8, FACE - 0.07, FACE - 0.05, 0.0, 2.5)          # the lit doors
for x in (EX0, -8.6, -7.2, -6.0, -4.8, -3.4, EX1):                          # mullions
    box('Entrance', MULLION, x - 0.07, x + 0.07, FACE - 0.12, FACE, 0.0, 3.4)
box('Entrance', MULLION, EX0, EX1, FACE - 0.12, FACE, 2.5, 2.62)
box('Entrance', MULLION, EX0, EX1, FACE - 0.12, FACE, 3.3, 3.45)
box('Exit', EXIT, -6.35, -5.65, FACE - 0.14, FACE - 0.12, 2.7, 2.95)
box('Canopy', CANOPY, EX0 - 0.6, EX1 + 0.6, FACE - 3.0, FACE, 3.5, 3.85)
for x in (EX0 - 0.3, EX1 + 0.3):
    cyl('Canopy', METAL, x, FACE - 2.7, 0.0, 3.5, 0.08)

# ---------------- the name board (the game draws the letters and crest on its front, at y = FACE - 0.36) ----------------
box('Sign', SIGN, -15.0, 3.0, FACE - 0.35, FACE, 4.6, 7.4)
box('Facade', MULLION, -15.15, 3.15, FACE - 0.4, FACE - 0.3, 4.45, 4.6)       # its ledge

# ---------------- street furniture ----------------
LX, LY = 6.0, -3.2                                   # the street lamp, at the kerb
cyl('Lamp', METAL, LX, LY, 0.0, 6.2, 0.09)
cyl('Lamp', METAL, LX, LY, 0.0, 0.6, 0.16)
box('Lamp', METAL, LX - 0.06, LX + 0.06, LY, LY + 1.5, 6.05, 6.17)
box('Lamp', METAL, LX - 0.28, LX + 0.28, LY + 1.1, LY + 1.8, 5.9, 6.15)
box('LampLight', LAMP, LX - 0.22, LX + 0.22, LY + 1.16, LY + 1.74, 5.86, 5.9)
for x in (-14.0, -11.5, -9.0, -3.0, -0.5, 2.0):                            # bollards along the plaza
    cyl('Bollards', KERB, x, -2.8, 0.0, 0.85, 0.11)
cyl('Bin', METAL, 9.0, -2.9, 0.0, 0.95, 0.28)

# ---------------- build and export ----------------
for name, (ob, bm) in PARTS.items():
    bm.to_mesh(ob.data); bm.free()
    for p in ob.data.polygons: p.use_smooth = False

out = ART + '/scene/exterior.glb'
bpy.ops.object.select_all(action='DESELECT')
for name, (ob, _) in PARTS.items(): ob.select_set(True)
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', use_selection=True, export_yup=True,
                          export_apply=True, export_lights=False, export_cameras=False, export_animations=False,
                          export_materials='EXPORT')
raw = open(out, 'rb').read(); js = json.loads(raw[20:20 + struct.unpack('<I', raw[12:16])[0]])
mats = js.get('materials', [])
order = []
# Raylib (rmodels.c LoadGLTF) walks the nodes in file order and adds one Mesh per triangle primitive
for node in js['nodes']:
    if 'mesh' not in node: continue
    for p in js['meshes'][node['mesh']]['primitives']:
        m = mats[p['material']] if 'material' in p else {}
        order.append({'index': len(order), 'part': node['name'], 'material': m.get('name'),
                      'vertices': js['accessors'][p['attributes']['POSITION']]['count'],
                      'unlit': 'KHR_materials_unlit' in m.get('extensions', {})})
if not any(o['part'] == 'LampLight' and o['unlit'] for o in order): raise SystemExit('the lamp glass did not export unlit')
meta = {'source': 'art/scripts/build_exterior.py', 'glb': 'art/scene/exterior.glb',
        'note': 'meshes in Raylib load order (Model.Meshes[index]); Y-up metres, the facade at z = -10 facing +z',
        'sign': {'x0': -15.0, 'x1': 3.0, 'y0': 4.6, 'y1': 7.4, 'z': -(FACE - 0.36)},
        'lamp': {'x': LX, 'z': -(LY + 1.45), 'y': 5.86},
        'meshes': order}
with open(ART + '/scene/exterior_meshes.json', 'w') as f: json.dump(meta, f, indent=1)
result = {'glb_bytes': len(raw), 'meshes': len(order), 'parts': list(PARTS)}
print('RESULT', result)
