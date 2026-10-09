"""Arena export for the game. Opens art/scene/arena_dusk.blend (never saves it) and writes
art/scene/arena.glb plus art/scene/arena_meshes.json, the list of meshes in the order Raylib
loads them, since a Raylib Model keeps no mesh or material names.
1. points the scene camera at the game's straight gameplay camera (BroadcastCamera.Straight)
   at 480x270 and reruns ads_pixel.py with FIT and EMIT, so the ad strip is sized to the far
   boards' pixel footprint for the camera the game actually uses (rewrites art/ads/),
2. drops players, puck, lights and cameras, flattens the ice to its base colour (the game draws
   the light bands, W2 / W18), turns any text objects into meshes (the SHL centre mark is already a mesh, see centre_logo.py),
3. joins the scenery into one object per part, each part one named mesh: Ice, Floor, Markings,
   Boards, Glass, Posts, AdBoards, Nets, Stands, LED, Fans, Lanterns. Glass is its own mesh so the game can
   set its tone from the lighting; AdBoards is the only mesh with the ad_pixel material, which
   exports as KHR_materials_unlit,
4. exports a Y-up GLB with transforms applied and the ad strip embedded with nearest filtering,
   and checks every primitive stays under Raylib's 65536-vertex limit for 16-bit indices.
Run in Blender:  exec(open(script).read(), {'__file__': script})  or with ART / SAP_ART set."""
import bpy, math, os, json, struct, sys, glob
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

# ---- 3. one object and one named mesh per part ----
PARTS = [('Ice', ['Ice']), ('Floor', ['Floor']), ('Markings', ['Markings', 'Logo']), ('Boards', ['Boards']),
         ('Glass', ['Glass']), ('Posts', ['Posts']), ('AdBoards', ['Ad']), ('Nets', ['NetFrame', 'Net']),
         ('Stands', ['Stands']), ('LED', ['LED']), ('Fans', ['Fans']), ('Lanterns', ['Lanterns', 'Cable'])]
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
result = {'glb_bytes': len(raw), 'meshes': len(order), 'ads': ads, 'parts': [n for n, _ in PARTS],
          'max_vertices': max(o['vertices'] for o in order)}
