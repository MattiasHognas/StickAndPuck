"""Flat id render for the outline pass: every mesh part of every player gets a unique
solid colour, everything else is hidden, so the post pass can find silhouettes and the
boundaries between parts (stick, limbs, skates) by plain colour comparison."""
import bpy, os, colorsys
OUT = os.path.dirname(bpy.data.filepath) + '/renders'
scene, vl = bpy.context.scene, bpy.context.view_layer
scene.render.resolution_percentage = 100   # keeps the blend's own resolution
scene.render.use_freestyle = False; scene.render.filter_size = 0.0
scene.eevee.taa_render_samples = 1
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
scene.render.image_settings.color_depth = '8'
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.0

players = set(bpy.data.collections['Players'].all_objects)

def solid(name, rgb):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs['Color'].default_value = rgb + (1,)
    out = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(em.outputs[0], out.inputs['Surface'])
    return m
WHITE, BLACK = solid('id_ice', (1.0, 1.0, 1.0)), solid('id_other', (0.0, 0.0, 0.0))
# the ice is pure white so the post pass can give it its own short ramp; everything else
# (markings, boards, crowd) is black but still drawn, so it hides the ice behind it
for ob in scene.objects:
    if ob in players or ob.type not in {'MESH', 'CURVE', 'FONT', 'SURFACE'}: continue
    if 'lass' in ob.name: ob.hide_render = True; continue
    ob.data.materials.clear(); ob.data.materials.append(WHITE if ob.name == 'Ice' else BLACK)

def flat(i):
    r, g, b = colorsys.hsv_to_rgb((i * 0.137) % 1.0, 0.95, 1.0 if i % 2 else 0.55)
    m = bpy.data.materials.new('id%d' % i); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs['Color'].default_value = (r, g, b, 1)
    out = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(em.outputs[0], out.inputs['Surface'])
    return m

roots, mats = {}, {}
for ob in sorted([o for o in players if o.type == 'MESH'], key=lambda o: o.name):
    ob.hide_render = False
    r = ob
    while r.parent: r = r.parent
    if r.name not in roots:
        roots[r.name] = len(roots) + 1; mats[r.name] = flat(roots[r.name])
    ob.data.materials.clear(); ob.data.materials.append(mats[r.name])
n = len(roots)
scene.render.filepath = OUT + '/' + os.path.basename(bpy.data.filepath).rsplit('.', 1)[0] + '_id.png'   # broadcast.blend -> broadcast_id.png
bpy.ops.render.render(write_still=True)
result = {'players': n, 'roots': sorted(roots), 'file': os.path.getsize(scene.render.filepath)}
