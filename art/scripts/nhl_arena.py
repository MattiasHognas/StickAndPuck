"""League dressing for the arena (A17): the swappable layers export_arena.py adds to arena.glb as parts
of their own, so the game can show or hide each one per match:
- Trapezoid: the NHL's trapezoid behind each goal line (W72's goalie limit), two red lines (10 cm, to read from the broadcast camera) from
  5.49 m apart on the goal line to 8.53 m apart at the end boards. Usable in either league; the game
  shows it when the trapezoid rule is on (for now SAP_TRAPEZOID=1).
- Banners: championship-style banners hung in the rafters over the stands, made-up and league-neutral
  (a club-less field, a cream stripe, a gold emblem and hanging rod), in dusk palette colours. The
  game hangs them in NHL arenas only; they show in the flyover and the wide shots, above the gameplay frame.
The centre-ice mark is the third layer: the 'SHL' Logo (centre_logo.py) stays its own part so an
NHL match can drop it for the home club's crest from art/ui/crests.png, which the game draws itself.
Blender coordinates: Z up, x along the rink, +y the far side (the game's -z).
Used by export_arena.py:  import nhl_arena; nhl_arena.build_trapezoid(collection, material); nhl_arena.build_banners(collection)"""
import math
import bpy, bmesh
from mathutils import Matrix, Vector

# the rink outline from build_scene.py: 60 x 25.5 m with 8.5 m corners, goal lines at +-26.7
L, W, R = 60.0, 25.5, 8.5
HL, HW = L / 2, W / 2
GOAL_X = 26.7
TRAP_GOAL_HALF = 5.49 / 2    # 18 ft apart where the trapezoid meets the goal line
TRAP_BOARD_HALF = 8.53 / 2   # 28 ft apart at the end boards
LINE = 0.1                   # twice the rule's 2 inch line: the broadcast camera squashes lines across the rink to dots
Z = 0.004                    # the goal line's height above the ice

# dusk palette colours (art/palette/stickandpuck_dusk.gpl)
BANNER_FIELDS = ['#a34e4d', '#31556e', '#27383e', '#613b3a', '#2b3d6b', '#6e2438']
BANNER_STRIPE, BANNER_GOLD, BANNER_ROD = '#efe0c2', '#f2c14e', '#c8963c'


def lin(h):
    c = [int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)


def material(name, h):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = lin(h); b.inputs['Roughness'].default_value = 0.85
    m.diffuse_color = lin(h)
    return m


def end_board_x(y):
    """the end boards' x at a distance y across the rink (the corner arc past HW - R)"""
    dy = abs(y) - (HW - R)
    return HL if dy <= 0 else (HL - R) + math.sqrt(max(0.0, R * R - dy * dy))


def build_trapezoid(collection, mat):
    """Creates the 'Trapezoid' mesh object: two slanted lines behind each goal line, in the given material."""
    bm = bmesh.new()
    for sx in (-1, 1):
        for sy in (-1, 1):
            a = Vector((sx * GOAL_X, sy * TRAP_GOAL_HALF, Z))
            end_x = end_board_x(TRAP_BOARD_HALF) - 0.02
            b = Vector((sx * end_x, sy * TRAP_BOARD_HALF, Z))
            d = (b - a).normalized(); n = Vector((-d.y, d.x, 0)) * (LINE / 2)
            bm.faces.new([bm.verts.new(p) for p in (a - n, b - n, b + n, a + n)])
    for f in bm.faces:                            # every face up, toward the camera
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    me = bpy.data.meshes.new('Trapezoid'); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new('Trapezoid', me); collection.objects.link(ob)
    return ob


def _box(bm, centre, size, mi):
    ret = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(centre) @ Matrix.Diagonal((*size, 1)))
    for f in {f for v in ret['verts'] for f in v.link_faces}:
        f.material_index = mi


def _banner(bm, centre, along, field):
    """one banner hanging from its rod, face square to the rink; along is the unit vector across its face"""
    w, h, t = 1.6, 3.2, 0.04
    ax, ay = along

    def at(u, z):
        return Vector((centre.x + ax * u, centre.y + ay * u, z))

    def size(width, height, thick):
        return (abs(ax) * width + abs(ay) * thick, abs(ay) * width + abs(ax) * thick, height)

    top = centre.z
    _box(bm, at(0, top - h / 2), size(w, h, t), field)                       # the field
    _box(bm, at(0, top - h + 0.55), size(w, 0.22, t + 0.02), 1)              # the cream stripe near the hem
    _box(bm, at(0, top - h + 0.18), size(w, 0.08, t + 0.02), 1)              # and a thin one at the hem
    _box(bm, at(0, top - 1.15), size(0.62, 0.62, t + 0.02), 2)               # the gold emblem
    _box(bm, at(0, top - 1.15), size(0.3, 0.3, t + 0.04), 1)                 # its cream heart
    _box(bm, at(0, top + 0.03), size(w + 0.3, 0.07, 0.07), 3)                # the hanging rod


def build_banners(collection):
    """Creates the 'Banners' mesh object: banners over both end stands and the far side stands, high
    above the upper rows (bottom 8.8 m, top 12 m) and above the top of the gameplay camera's frame, square to the rink."""
    mats = [material('banner_%d' % i, h) for i, h in enumerate(BANNER_FIELDS)]
    extra = [material('banner_stripe', BANNER_STRIPE), material('banner_gold', BANNER_GOLD), material('banner_rod', BANNER_ROD)]
    # material slots: 0..3 are what _banner indexes (field, stripe, gold, rod); the field slot is remapped per banner below
    bm = bmesh.new(); top = 12.0; k = 0
    fields = []
    for sx in (-1, 1):                                                       # the ends, 8 m out over the stands
        for i in range(6):
            fields.append((Vector((sx * (HL + 8.0), -6.25 + i * 2.5, top)), (0.0, 1.0)))
    for i in range(10):                                                      # the far side; the near side's would hang
        fields.append((Vector((-18.0 + i * 4.0, HW + 8.0, top)), (1.0, 0.0)))  # between the broadcast camera and the ice
    slots = mats + extra                                                     # 0..5 fields, 6 stripe, 7 gold, 8 rod
    for centre, along in fields:
        first = len(bm.faces)
        _banner(bm, centre, along, 0)
        field = k % len(mats); k += 1
        bm.faces.ensure_lookup_table()
        for f in bm.faces[first:]:
            f.material_index = field if f.material_index == 0 else len(mats) + f.material_index - 1
    me = bpy.data.meshes.new('Banners'); bm.to_mesh(me); bm.free()
    for m in slots:
        me.materials.append(m)
    ob = bpy.data.objects.new('Banners', me); collection.objects.link(ob)
    return ob
