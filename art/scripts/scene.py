"""The full v2 cast in the dusk broadcast scene: ten skaters, two goalies and a referee,
each frozen on one pose of its own animation clips, rendered the way the game will draw
it (480x270, three-step toon light, nearest texels). Opens arena_dusk.blend (with the
pixel-font boards from ads_pixel.py), saves broadcast.blend, renders renders/broadcast_beauty.png.
Needs the skinned, animated <role>.blend files from anim.py."""
import bpy, math, os
from mathutils import Vector as V
from bpy_extras.object_utils import world_to_camera_view

# Where art/ is: ART passed as a global (exec), SAP_ART in the environment, or two levels up
# from this script when it is run as a file; no path to anyone's machine is baked in.
def _art_dir():
    a = globals().get('ART') or os.environ.get('SAP_ART')
    if not a and '__file__' in globals(): a = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if not a or not os.path.isdir(a): raise SystemExit('set ART (global) or SAP_ART (env) to the art/ folder')
    return os.path.abspath(a)
ART = _art_dir()
SK = ART + '/player'
scene = bpy.context.scene
C = bpy.data.collections['Players']
puck = bpy.data.objects['Puck']
for ob in list(C.all_objects):
    if ob is not puck: bpy.data.objects.remove(ob)

IMG = {}
def kit_image(role, kit):
    if (role, kit) not in IMG:
        img = bpy.data.images.load('%s/%s_%s_64.png' % (SK, role, kit)); img.pack(); IMG[role, kit] = img
    return IMG[role, kit]

def toonify_tex(m, img):
    nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
    diff = nt.nodes.new('ShaderNodeBsdfDiffuse'); s2r = nt.nodes.new('ShaderNodeShaderToRGB')
    ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements
    els[0].position = 0.0; els[0].color = (0.42, 0.40, 0.48, 1)
    els[1].position = 0.22; els[1].color = (0.70, 0.68, 0.72, 1)
    e = els.new(0.55); e.color = (1, 1, 1, 1)
    mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1.0
    em = nt.nodes.new('ShaderNodeEmission'); out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(diff.outputs[0], s2r.inputs[0]); nt.links.new(s2r.outputs['Color'], ramp.inputs['Fac'])
    nt.links.new(tex.outputs['Color'], mul.inputs[6]); nt.links.new(ramp.outputs['Color'], mul.inputs[7])
    nt.links.new(mul.outputs[2], em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs['Surface'])

def freeze(rig, clip, pose):
    """sample one pose of a clip and keep it as the rig's static pose (each rig gets its own moment)"""
    ad = rig.animation_data
    tr = next(t for t in ad.nla_tracks if t.name == clip); act = tr.strips[0].action
    ad.action = act
    if hasattr(ad, 'action_slot') and act.slots: ad.action_slot = act.slots[0]
    hold = {'skate': 2, 'idle': 6, 'shoot': 3, 'ready': 5, 'butterfly': 3}[clip] if 'referee' not in rig.name else {'skate': 3, 'idle': 8}[clip]
    scene.frame_set(pose * hold)
    snap = {p.name: (p.location.copy(), p.rotation_quaternion.copy()) for p in rig.pose.bones}
    ad.action = None
    for p in rig.pose.bones: p.location, p.rotation_quaternion = snap[p.name]

def add(role, kit, pos, face_deg, clip, pose):
    with bpy.data.libraries.load('%s/%s.blend' % (SK, role), link=False) as (src, dst):
        dst.objects = list(src.objects)
    rig = None
    for ob in dst.objects:
        C.objects.link(ob)
        if ob.type == 'ARMATURE': rig = ob
        if ob.type == 'MESH':
            for m in ob.data.materials:
                if m: toonify_tex(m, kit_image(role, kit))
    rig.name = '%s_%s' % (role, kit)
    rig.location = pos; rig.rotation_euler = (0, 0, math.radians(face_deg)); rig.scale = (1.15,) * 3
    freeze(rig, clip, pose)
    return rig

home = [((9.0, -1.0, 0), 80, 'skate', 0), ((15.5, 7.0, 0), 120, 'skate', 2), ((5.0, -7.5, 0), 60, 'skate', 5),
        ((2.5, 4.0, 0), 95, 'shoot', 1), ((17.0, -8.0, 0), 140, 'idle', 0)]
away = [((13.2, -0.4, 0), -100, 'skate', 3), ((19.5, 3.8, 0), -120, 'idle', 2), ((18.5, -5.5, 0), -60, 'skate', 1),
        ((8.5, 6.8, 0), -150, 'skate', 6), ((22.0, 0.6, 0), -90, 'skate', 4)]
rigs = [add('skater', 'home', *h) for h in home] + [add('skater', 'away', *a) for a in away]
rigs += [add('goalie', 'away', (24.6, 0.3, 0), -95, 'ready', 1), add('goalie', 'home', (-25.6, 0.0, 0), 90, 'ready', 0),
         add('referee', 'ref', (11.5, 8.3, 0), 60, 'skate', 2)]
bpy.context.view_layer.update()

# the puck sits against the carrier's blade: lowest points of the stick, after skinning
carrier = rigs[0]; body = next(o for o in carrier.children if o.type == 'MESH')
gi = body.vertex_groups['hand.R'].index
ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
pts = [body.matrix_world @ ev.vertices[v.index].co for v in body.data.vertices if any(g.group == gi for g in v.groups)]
blade = [p for p in pts if p.z < min(q.z for q in pts) + 0.06]
bc = sum(blade, V()) / len(blade)
pc = sum((v.co for v in puck.data.vertices), V()) / len(puck.data.vertices)
puck.location = (bc.x - pc.x + 0.10, bc.y - pc.y - 0.10, 0)

scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 480, 270, 100
scene.render.use_freestyle = False; scene.render.filter_size = 0.0
scene.eevee.taa_render_samples = 128
D = os.path.dirname(bpy.data.filepath)
bpy.ops.wm.save_as_mainfile(filepath=D + '/broadcast.blend')
scene.render.filepath = D + '/renders/broadcast_beauty.png'
bpy.ops.render.render(write_still=True)
on_screen = {r.name: [round(c, 2) for c in world_to_camera_view(scene, scene.camera, r.matrix_world.translation)[:2]] for r in rigs[10:]}
result = {'rigs': len(rigs), 'objects': len(C.all_objects), 'extras_on_screen_uv': on_screen}
