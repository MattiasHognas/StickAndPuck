"""The centre-ice mark: 'SHL' drawn with the HUD pixel font (ui_glyphs.py) as one flat block per
font pixel, so it reads as pixel art like the rest of the HUD instead of a vector font.
build_scene.py calls build_logo(); run on its own in Blender it replaces the Logo object in
art/scene/arena.blend and arena_dusk.blend and saves both, so export_arena.py picks it up:
    exec(open(script).read(), {'__file__': script, 'RUN_LOGO': True})"""
import os, sys
import bpy, bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(globals().get('__file__', '.'))))
import ui_glyphs

TEXT = 'SHL'
BLOCK = 0.3          # metres per font pixel across the rink: 'SHL' is 17 x 7 pixels, 5.1 m wide
DEPTH = 0.6          # metres per font pixel along the camera's view: the 28 degree gameplay camera
                     # squashes the ice about 2x, so rows are twice as deep to read square (4.2 m)
Z = 0.006            # just above the ice, like the other markings


def build_logo(collection, material, text=TEXT, block=BLOCK, depth=DEPTH):
    """Creates the 'Logo' mesh object (Blender Z up, readable from the camera at -Y)."""
    g = ui_glyphs.glyphs()
    rows = [g[c] for c in text]
    width = sum(len(r[0]) + 1 for r in rows) - 1
    top, height = 2, 7                        # capital rows of the 11-row glyph cells
    bm = bmesh.new()
    x0 = 0
    for r in rows:
        for gy in range(top, top + height):
            for gx, v in enumerate(r[gy]):
                if v != '#':
                    continue
                px = (x0 + gx - width / 2) * block
                py = (height / 2 - (gy - top)) * depth
                q = [bm.verts.new(Vector((px + dx, py - dy, Z))) for dx, dy in ((0, 0), (0, depth), (block, depth), (block, 0))]
                bm.faces.new(q)
        x0 += len(r[0]) + 1
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    me = bpy.data.meshes.new('Logo'); bm.to_mesh(me); bm.free()
    me.materials.append(material)
    ob = bpy.data.objects.new('Logo', me); collection.objects.link(ob)
    return ob


def replace_in(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    old = bpy.data.objects.get('Logo')
    if old is None:
        raise SystemExit(f'no Logo object in {path}')
    coll, mat = old.users_collection[0], old.data.materials[0]
    data = old.data
    bpy.data.objects.remove(old)
    if data.users == 0:
        (bpy.data.curves if isinstance(data, bpy.types.Curve) else bpy.data.meshes).remove(data)
    build_logo(coll, mat)
    bpy.ops.wm.save_mainfile(filepath=path)


if __name__ == '__main__' or globals().get('RUN_LOGO'):
    art = globals().get('ART') or os.environ.get('SAP_ART') or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    for name in ('arena.blend', 'arena_dusk.blend'):
        replace_in(os.path.join(art, 'scene', name))
