"""Five-view turnaround of skater v2 (front, 3/4 front, side, 3/4 back, back), rendered the
way the game will: tiny, flat 3-step toon light, nearest texels, no anti-aliasing.
Writes art/player/renders/turn_<view>.png with a transparent background."""
import bpy, math, os
from mathutils import Vector as V
OUT = os.path.dirname(bpy.data.filepath) + '/renders'
scene = bpy.context.scene

def toonify_tex(m):
    nt = m.node_tree
    img = next(n.image for n in nt.nodes if n.type == 'TEX_IMAGE')
    for n in list(nt.nodes): nt.nodes.remove(n)
    tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
    diff = nt.nodes.new('ShaderNodeBsdfDiffuse'); s2r = nt.nodes.new('ShaderNodeShaderToRGB')
    ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements
    els[0].position = 0.0; els[0].color = (0.42, 0.40, 0.48, 1)        # shadow, a little cool
    els[1].position = 0.18; els[1].color = (0.70, 0.68, 0.72, 1)
    e = els.new(0.5); e.color = (1, 1, 1, 1)
    mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1.0
    em = nt.nodes.new('ShaderNodeEmission'); out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(diff.outputs[0], s2r.inputs[0]); nt.links.new(s2r.outputs['Color'], ramp.inputs['Fac'])
    nt.links.new(tex.outputs['Color'], mul.inputs[6]); nt.links.new(ramp.outputs['Color'], mul.inputs[7])
    nt.links.new(mul.outputs[2], em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs['Surface'])
NAME = os.path.basename(bpy.data.filepath).rsplit('.', 1)[0]      # skater, goalie or referee
MAT = next(m for m in bpy.data.materials if m.use_nodes and any(n.type == 'TEX_IMAGE' for n in m.node_tree.nodes))
KITS = sorted(i.name.split('_', 1)[1] for i in bpy.data.images if i.name.startswith(NAME + '_'))
toonify_tex(MAT)

sun = bpy.data.lights.new('Key', 'SUN'); sun.energy = 3.0; sun.angle = 0.0
so = bpy.data.objects.new('Key', sun); scene.collection.objects.link(so)
so.rotation_euler = (V((0, 0, 0)) - V((-1.2, -1.6, 2.2))).to_track_quat('-Z', 'Y').to_euler()
scene.world = scene.world or bpy.data.worlds.new('W'); scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.0

cam_d = bpy.data.cameras.new('Turn'); cam_d.type = 'ORTHO'; cam_d.ortho_scale = 2.7
cam = bpy.data.objects.new('Turn', cam_d); scene.collection.objects.link(cam); scene.camera = cam
el = math.radians(14); tgt = V((0.05, 0, 0.9))
cam.location = tgt + V((0, -10 * math.cos(el), 10 * math.sin(el)))
cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()

scene.render.engine = 'BLENDER_EEVEE'; scene.eevee.taa_render_samples = 1
scene.render.resolution_x = scene.render.resolution_y = 108; scene.render.resolution_percentage = 100
scene.render.filter_size = 0.0; scene.render.film_transparent = True; scene.render.use_freestyle = False
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
scene.render.image_settings.color_mode = 'RGBA'

rig = bpy.data.objects['Rig']
VIEWS = [('front', 0), ('front34', 35), ('side', 90), ('back34', 145), ('back', 180)]
shots = {}
for kit in (KITS if globals().get('TURN', True) else []):     # TURN=False: only set up camera, light and toon material
    MAT.node_tree.nodes['Image Texture'].image = bpy.data.images[NAME + '_' + kit]
    for name, deg in VIEWS:
        rig.rotation_euler = (0, 0, math.radians(deg))
        scene.render.filepath = '%s/turn_%s_%s_%s.png' % (OUT, NAME, kit, name)
        bpy.ops.render.render(write_still=True); shots[kit + '_' + name] = os.path.getsize(scene.render.filepath)
result = shots
