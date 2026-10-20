"""Arena export for the game. Opens art/scene/arena_dusk.blend (never saves it) and writes
art/scene/arena.glb plus art/scene/arena_meshes.json, the list of meshes in the order Raylib
loads them, since a Raylib Model keeps no mesh or material names.
1. points the scene camera at the game's straight gameplay camera (BroadcastCamera.Straight)
   at 480x270 and reruns ads_pixel.py with FIT and EMIT, so the ad strip is sized to the far
   boards' pixel footprint for the camera the game actually uses (rewrites art/ads/),
2. drops players, puck, lights and cameras, flattens the ice to its base colour (the game draws
   the light bands, W2 / W18), turns any text objects into meshes (the SHL centre mark is already a mesh, see centre_logo.py),
3. joins the scenery into one object per part, each part one named mesh: Ice, Floor, Markings,
   Boards, Glass, Posts, AdBoards, Nets, Stands, LED, Lanterns, Bench, BenchDoors (2b), PenaltyBox, BoxDoors (2c), plus the
   league layers the game swaps per match (A17): Logo (the SHL centre mark), Trapezoid and Banners (nhl_arena.py). Glass is its own mesh so the game can
   set its tone from the lighting; AdBoards is the only mesh with the ad_pixel material, which
   exports as KHR_materials_unlit. The static Fans are not exported (A18): after the bench and box
   trim, every fan's seat goes to art/scene/crowd_seats.json and the game draws the animated crowd
   (art/crowd/, Arena/CrowdRenderer.cs) there instead,
4. exports a Y-up GLB with transforms applied and the ad strip embedded with nearest filtering,
   and checks every primitive stays under Raylib's 65536-vertex limit for 16-bit indices.
Run in Blender:  exec(open(script).read(), {'__file__': script})  or with ART / SAP_ART set."""
import bpy, bmesh, math, os, json, struct, sys, glob
from mathutils import Matrix, Vector
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

def lin(h):
    c = [int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)

if os.path.basename(bpy.data.filepath) != 'arena_dusk.blend':
    bpy.ops.wm.open_mainfile(filepath=ART + '/scene/arena_dusk.blend')
scene = bpy.context.scene

# ---- 1. the game's straight camera: Raylib (0, 17.5, 29.67) is Blender (0, -29.67, 17.5), 28.2 degrees down ----
cam = scene.camera
cam.location = (0.0, -29.67, 17.5)
cam.rotation_mode = 'XYZ'; cam.rotation_euler = (math.radians(90 - 28.2), 0.0, 0.0)
cam.data.lens = 50.0; cam.data.sensor_fit = 'VERTICAL'; cam.data.sensor_height = 24.0
scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 480, 270, 100
bpy.context.view_layer.update()
ads_script = ART + '/scripts/ads_pixel.py'
g = {'__file__': ads_script, 'ART': ART, 'FIT': True, 'EMIT': True, 'SAVE': False}
exec(compile(open(ads_script).read(), ads_script, 'exec'), g)
ads = g['result']

# ---- 2. only the scenery goes to the game ----
for ob in list(bpy.data.objects):
    if ob.type in ('LIGHT', 'CAMERA', 'ARMATURE') or ob.name == 'Puck':
        bpy.data.objects.remove(ob)
for c in list(bpy.data.collections):
    if c.name == 'Players':
        for ob in list(c.all_objects): bpy.data.objects.remove(ob)
for ob in list(bpy.data.objects):
    if ob.type not in ('MESH', 'FONT') or ob.name not in scene.objects: bpy.data.objects.remove(ob)

ice = bpy.data.materials['ice']; b = ice.node_tree.nodes['Principled BSDF']
for l in list(b.inputs['Base Color'].links): ice.node_tree.links.remove(l)
b.inputs['Base Color'].default_value = lin('#c9c6cf')     # the ice's base shade from restyle_dusk.py

# the exporter writes an Emission surface as an emissive texture over black; a Background surface
# becomes KHR_materials_unlit with the strip as the base colour texture, which is what Raylib loads
ad = bpy.data.materials['ad_pixel'].node_tree
em = next(n for n in ad.nodes if n.type == 'EMISSION'); tex = next(n for n in ad.nodes if n.type == 'TEX_IMAGE')
tex.image = bpy.data.images.load(ART + '/ads/ads_%dx%d.png' % tuple(ads['atlas']))   # the strip ads_pixel.py just wrote,
tex.interpolation = 'Closest'                                                           # never a stale packed copy
bg = ad.nodes.new('ShaderNodeBackground')
ad.links.new(tex.outputs['Color'], bg.inputs['Color']); ad.links.new(bg.outputs[0], next(n for n in ad.nodes if n.type == 'OUTPUT_MATERIAL').inputs['Surface'])
ad.nodes.remove(em)

# ---- 2b. the bench area behind the far boards (Blender +Y; the game's -z) ----
# two 10 m benches centred on the doors at x = -6 (home) and +6 (away): deck, one row of seats on a
# 0.2 m riser, a low dark back wall with a light cap that shows over the board top from the broadcast
# camera, end walls, and a 0.3 m standing step behind for the coaches. No glass or posts in front, and
# the front fan row behind them is trimmed so the bench reads as its own space.
BOARD_Y, BENCH_Y1, DOOR_X, BENCH_HALF = 12.75, 14.25, 6.0, 5.0
def bench_spans(): return [(cx - BENCH_HALF, cx + BENCH_HALF) for cx in (-DOOR_X, DOOR_X)]
def in_bench(x): return any(x0 < x < x1 for x0, x1 in bench_spans())

def box(bm, x0, x1, y0, y1, z0, z1, mi):
    ret = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)) @ Matrix.Diagonal((x1 - x0, y1 - y0, z1 - z0, 1)))
    for f in {f for v in ret['verts'] for f in v.link_faces}: f.material_index = mi

def part_mesh(name, bm, colours):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for key, h in colours:
        if key in bpy.data.materials: me.materials.append(bpy.data.materials[key]); continue   # a material another part already has
        m = bpy.data.materials.new(key); m.use_nodes = True
        bsdf = m.node_tree.nodes['Principled BSDF']; bsdf.inputs['Base Color'].default_value = lin(h); bsdf.inputs['Roughness'].default_value = 0.85
        m.diffuse_color = lin(h); me.materials.append(m)
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)

def build_benches():
    bm = bmesh.new()   # 0 deck, 1 seats, 2 back and end walls, 3 wall cap, 4 standing step
    for x0, x1 in bench_spans():
        box(bm, x0, x1, 13.01, BENCH_Y1, -0.05, 0.0, 0)
        box(bm, x0 + 0.1, x1 - 0.1, 13.05, 13.5, 0.0, 0.2, 2); box(bm, x0 + 0.1, x1 - 0.1, 13.05, 13.5, 0.2, 0.62, 1)
        box(bm, x0, x1, 13.55, 13.7, 0.0, 1.5, 2); box(bm, x0, x1, 13.53, 13.72, 1.5, 1.56, 3)
        for ex in (x0, x1 - 0.12): box(bm, ex, ex + 0.12, 13.01, 13.55, 0.0, 1.3, 2)
        box(bm, x0, x1, 13.75, BENCH_Y1, 0.0, 0.3, 4)
    part_mesh('Bench', bm, [('bench_deck', '#211b28'), ('bench_seat', '#555f6b'), ('bench_wall', '#2a2c36'),
                            ('bench_cap', '#8a8790'), ('bench_step', '#33293a')])
    # door outlines on the boards' rink face, kept clear of the ad strip (z 0.26 to 0.98) so it stays
    # pixel-exact: a dark kick panel with edge bars, and a dark cap over the 1 m door
    bm = bmesh.new(); fy = BOARD_Y - 0.006
    for cx in (-DOOR_X, DOOR_X):
        box(bm, cx - 0.5, cx + 0.5, fy - 0.002, fy, 0.02, 0.22, 0)
        for ex in (cx - 0.5, cx + 0.42): box(bm, ex, ex + 0.08, fy - 0.004, fy - 0.002, 0.0, 0.25, 1)
        box(bm, cx - 0.5, cx + 0.5, 12.725, 13.015, 1.065, 1.155, 1)
    part_mesh('BenchDoors', bm, [('door_panel', '#74666b'), ('door_edge', '#423236')])

def components(bm):
    """the mesh's connected pieces as lists of verts"""
    seen, out = set(), []
    for v in bm.verts:
        if v.index in seen: continue
        stack, piece = [v], []; seen.add(v.index)
        while stack:
            a = stack.pop(); piece.append(a)
            for e in a.link_edges:
                b_ = e.other_vert(a)
                if b_.index not in seen: seen.add(b_.index); stack.append(b_)
        out.append(piece)
    return out

# ---- 2c. the penalty boxes behind the near boards (Blender -Y; the game's +z) ----
# the SHL spot, across from the benches either side of centre: home at x = -2.5, away at +2.5, the
# doors Penalties.BoxDoor puts skaters through and the seat they park on (game z 13.95). Each box is
# 3.8 m wide and 1.5 m deep: deck, a seat on a riser under the parked skater, a back wall low enough
# that the broadcast camera behind it still sees him, end walls, and a glass partition on the inner
# end where the timekeeper sits between the boxes. The rink glass and posts in front stay.
BOX_X, BOX_HALF, BOX_Y1 = 2.5, 1.9, -14.55
def box_spans(): return [(cx - BOX_HALF, cx + BOX_HALF) for cx in (-BOX_X, BOX_X)]
def in_box(x): return any(x0 < x < x1 for x0, x1 in box_spans())

def build_penalty_boxes():
    bm = bmesh.new()   # 0 deck, 1 seat, 2 walls and riser, 3 wall cap
    for x0, x1 in box_spans():
        box(bm, x0, x1, BOX_Y1, -13.01, -0.05, 0.0, 0)
        box(bm, x0 + 0.15, x1 - 0.15, -14.25, -13.75, 0.0, 0.2, 2); box(bm, x0 + 0.15, x1 - 0.15, -14.25, -13.75, 0.2, 0.48, 1)
        box(bm, x0, x1, BOX_Y1, -14.4, 0.0, 1.0, 2); box(bm, x0, x1, BOX_Y1 - 0.02, -14.38, 1.0, 1.06, 3)
        for ex in (x0, x1 - 0.12): box(bm, ex, ex + 0.12, -14.4, -13.01, 0.0, 1.0, 2)
    part_mesh('PenaltyBox', bm, [('box_deck', '#211b28'), ('box_seat', '#555f6b'), ('box_wall', '#2a2c36'), ('box_cap', '#8a8790')])
    # door outlines on the near boards' rink face, as on the bench doors: clear of the ad strip
    bm = bmesh.new(); fy = -BOARD_Y + 0.006
    for cx in (-BOX_X, BOX_X):
        box(bm, cx - 0.5, cx + 0.5, fy, fy + 0.002, 0.02, 0.22, 0)
        for ex in (cx - 0.5, cx + 0.42): box(bm, ex, ex + 0.08, fy + 0.002, fy + 0.004, 0.0, 0.25, 1)
        box(bm, cx - 0.5, cx + 0.5, -13.015, -12.725, 1.065, 1.155, 1)
    part_mesh('BoxDoors', bm, [('door_panel', '#74666b'), ('door_edge', '#423236')])
    # the glass partitions on the inner end walls, joined into Glass so the game tints them with the rink glass
    bm = bmesh.new()
    for x0, x1 in box_spans():
        ex = x1 - 0.12 if x1 <= 0 else x0
        box(bm, ex + 0.05, ex + 0.07, -14.4, -13.01, 1.0, 1.9, 0)
    part_mesh('BoxGlass', bm, [('glass', '#d8dcd2')])

def trim_for_benches():
    for name, lo, hi, inside in (('Posts', 12.0, 13.5, in_bench), ('Fans', 14.3, 15.4, in_bench),   # posts on the far glass line; the front fan row
                                 ('Fans', -15.4, -14.3, in_box)):                                 # and the near front fans behind the boxes
        me = bpy.data.objects[name].data; bm = bmesh.new(); bm.from_mesh(me); bm.verts.index_update()
        gone = [v for piece in components(bm) for c in [sum((p.co for p in piece), Vector()) / len(piece)]
                if lo < c.y < hi and inside(c.x) for v in piece]
        bmesh.ops.delete(bm, geom=gone, context='VERTS'); bm.to_mesh(me); bm.free()
    me = bpy.data.objects['Glass'].data; bm = bmesh.new(); bm.from_mesh(me)
    for x in sorted({x for span in bench_spans() for x in span}):
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(x, 0, 0), plane_no=(1, 0, 0))
    gone = [f for f in bm.faces if f.calc_center_median().y > 12.0 and in_bench(f.calc_center_median().x)]
    bmesh.ops.delete(bm, geom=gone, context='FACES'); bm.to_mesh(me); bm.free()

# ---- 3. one object and one named mesh per part ----
PARTS = [('Ice', ['Ice']), ('Floor', ['Floor']), ('Markings', ['Markings']), ('Logo', ['Logo']), ('Trapezoid', ['Trapezoid']),
         ('Banners', ['Banners']), ('Boards', ['Boards']),
         ('Glass', ['Glass', 'BoxGlass']), ('Posts', ['Posts']), ('AdBoards', ['Ad']), ('Nets', ['NetFrame', 'Net']),
         ('Stands', ['Stands']), ('LED', ['LED']), ('Fans', ['Fans']), ('Lanterns', ['Lanterns', 'Cable']),
         ('Bench', ['Bench']), ('BenchDoors', ['BenchDoors']), ('PenaltyBox', ['PenaltyBox']), ('BoxDoors', ['BoxDoors'])]
build_benches(); build_penalty_boxes()
# the league layers (A17, nhl_arena.py): the trapezoid in its own lighter red (W72), and the rafter banners
sys.path.insert(0, ART + '/scripts'); import nhl_arena
nhl_arena.build_trapezoid(scene.collection); nhl_arena.build_banners(scene.collection)
def stem(name): return name.split('.')[0]
for ob in scene.objects:                 # unparent, keeping world placement, so joins and applies are exact
    if ob.parent: mw = ob.matrix_world.copy(); ob.parent = None; ob.matrix_world = mw
bpy.ops.object.select_all(action='DESELECT')
for ob in [o for o in scene.objects if o.type == 'FONT']:
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.convert(target='MESH'); ob.select_set(False)
used = set()
for name, stems in PARTS:
    obs = [o for o in scene.objects if o.type == 'MESH' and stem(o.name) in stems]
    if not obs: raise SystemExit('arena part missing: ' + name)
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.make_single_user(object=True, obdata=True)
    if len(obs) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.name = name; ob.data.name = name; used.add(name)
trim_for_benches()

# ---- 3b. the crowd's seats (A18): one per static fan, then the static fans go ----
# Each fan in build_scene.py is a body cube (8 verts) standing on its row's tread at 0.5 + row * 0.55
# and a head; the cube's centre gives the seat and its bottom the row. Written in game coordinates
# (Blender (x, y, z) is the game's (x, z, -y)), back rows first so the game can draw them in order.
# Sections split the stands for the crowd's poses: the two ends past the corner arcs, and each side
# in thirds; far is behind the benches (Blender +y, the game's -z), near behind the penalty boxes.
CROWD_SECTIONS = ['far_left', 'far_centre', 'far_right', 'near_left', 'near_centre', 'near_right', 'left_end', 'right_end']
def crowd_section(x, y):
    if x < -21.5: return 'left_end'
    if x > 21.5: return 'right_end'
    third = 'left' if x < -7.17 else 'right' if x > 7.17 else 'centre'
    return ('far_' if y > 0 else 'near_') + third

def write_seats():
    me = bpy.data.objects['Fans'].data; bm = bmesh.new(); bm.from_mesh(me); bm.verts.index_update()
    seats = []
    for piece in components(bm):
        if len(piece) != 8: continue                       # a head
        c = sum((v.co for v in piece), Vector()) / 8; bottom = min(v.co.z for v in piece)
        row = int(math.floor((bottom - 0.52) / 0.55)); tread = 0.5 + row * 0.55
        seats.append([round(c.x, 3), round(tread, 3), round(-c.y, 3), row, CROWD_SECTIONS.index(crowd_section(c.x, c.y))])
    bm.free()
    seats.sort(key=lambda s: (-s[3], s[0], s[2]))
    meta = {'source': 'art/scripts/export_arena.py', 'note': 'one seat per spectator: [x, tread height y, z, row, section], game metres, back rows first',
            'sections': CROWD_SECTIONS, 'rows': max(s[3] for s in seats) + 1, 'seats': seats}
    with open(ART + '/scene/crowd_seats.json', 'w') as f: json.dump(meta, f, separators=(',', ':'))
    return len(seats)
seat_count = write_seats()
bpy.data.objects.remove(bpy.data.objects['Fans'])
PARTS = [(n, s) for n, s in PARTS if n != 'Fans']
left = [o.name for o in scene.objects if o.name not in used]
for n in left: bpy.data.objects.remove(bpy.data.objects[n])

# ---- 4. export (the exporter orders nodes by name; arena_meshes.json records the order Raylib sees) ----
out = ART + '/scene/arena.glb'
bpy.ops.object.select_all(action='DESELECT')
for name, _ in PARTS: bpy.data.objects[name].select_set(True)
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', use_selection=True, export_yup=True,
                          export_apply=True, export_lights=False, export_cameras=False, export_animations=False,
                          export_materials='EXPORT', export_image_format='AUTO')

raw = open(out, 'rb').read(); js = json.loads(raw[20:20 + struct.unpack('<I', raw[12:16])[0]])
mats = js.get('materials', [])
order = []
# Raylib (rmodels.c LoadGLTF) walks the nodes in file order and adds one Mesh per triangle primitive
for node in js['nodes']:
    if 'mesh' not in node: continue
    for p in js['meshes'][node['mesh']]['primitives']:
        verts = js['accessors'][p['attributes']['POSITION']]['count']
        m = mats[p['material']] if 'material' in p else {}
        order.append({'index': len(order), 'part': node['name'], 'material': m.get('name'), 'vertices': verts,
                      'unlit': 'KHR_materials_unlit' in m.get('extensions', {}),
                      'textured': 'baseColorTexture' in m.get('pbrMetallicRoughness', {})})
too_big = [o for o in order if o['vertices'] >= 65536]
if too_big: raise SystemExit('primitives over 65535 vertices: %s' % too_big)
ad = [o for o in order if o['material'] == 'ad_pixel']
if not ad or not all(o['unlit'] and o['textured'] for o in ad): raise SystemExit('ad_pixel did not export unlit and textured')
samplers = js.get('samplers', [])
if not samplers or any(s.get('magFilter') != 9728 for s in samplers): raise SystemExit('ad strip sampler is not NEAREST: %s' % samplers)
png = bytes(raw[20 + struct.unpack('<I', raw[12:16])[0] + 8:])
img = js['bufferViews'][js['images'][0]['bufferView']]; png = png[img.get('byteOffset', 0):][:img['byteLength']]
if list(struct.unpack('>II', png[16:24])) != list(ads['atlas']): raise SystemExit('embedded ad strip is not the new %s atlas' % ads['atlas'])
meta = {'source': 'art/scripts/export_arena.py', 'glb': 'art/scene/arena.glb', 'ads': ads,
        'note': 'meshes in Raylib load order (Model.Meshes[index]); Y-up metres, x along the rink, +z toward the camera',
        'meshes': order}
with open(ART + '/scene/arena_meshes.json', 'w') as f: json.dump(meta, f, indent=1)
result = {'glb_bytes': len(raw), 'meshes': len(order), 'ads': ads, 'parts': [n for n, _ in PARTS], 'crowd_seats': seat_count,
          'max_vertices': max(o['vertices'] for o in order)}
