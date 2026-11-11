"""Candlepath static decoration sprites (Library and Chapel maps).

Renders Poly Haven (CC0) props to transparent PNGs from the same camera as the other sprites
(orthographic, 60 deg elevation, 2.4 world units across 256 px, see generate_sprites.py).

The props must already be in the open Blender file. Import them first with the Blender MCP
(import_asset, source polyhaven, asset_type models):
    Library: wooden_bookshelf_worn  Shelf_01  round_wooden_table_02  book_encyclopedia_set_01  wooden_crate_02
    Rusys:   wine_barrel_01  wooden_bucket_01  wooden_bucket_02  wine_bottles_01  wicker_basket_02  wooden_crate_01
    Chapel:  painted_wooden_bench  marble_bust_01  brass_candleholders
then run this script in Blender's Scripting tab (or through the MCP):
    OUT defaults to <project>/Assets/Decor; override with  blender ... -P generate_decor.py -- <dir>

Each prop is scaled to a footprint of 1 or 2 grid cells (1 cell = 1 world unit), its base is put on the
ground at the origin, and the sprite is rendered in a separate scene, so the open scene is left alone.
"""
import math
import os
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PROJECT = "/home/benas/Dokumentai/Universitetas/IV_kursas/KŽPK/Candlepath/Candlepath"
OUT = os.path.abspath(argv[0] if argv else os.path.join(PROJECT, "Assets", "Decor"))
os.makedirs(OUT, exist_ok=True)

RES, ORTHO, ELEV = 256, 2.4, 60.0

# sprite name -> (object-name prefix, footprint width in world units along X, rotation about Z in degrees)
PROPS = {
    # Library
    "decor_bookshelf": ("wooden_bookshelf_worn", 1.3, 0),
    "decor_shelf": ("Shelf_01", 0.95, 0),
    "decor_reading_table": ("round_wooden_table_02", 0.9, 0),
    "decor_books": ("book_encyclopedia_set_01", 0.8, 0),
    "decor_crate": ("wooden_crate_02", 0.95, 0),
    # Rusys (cellar)
    "decor_barrel": ("wine_barrel_01", 0.85, 0),
    "decor_bucket": ("wooden_bucket_01", 0.5, 0),
    "decor_tub": ("wooden_bucket_02", 0.85, 0),
    "decor_bottles": ("wine_bottles_01", 0.7, 0),
    "decor_basket": ("wicker_basket_02", 0.6, 0),
    "decor_crate_small": ("wooden_crate_01", 0.95, 0),
    # Chapel
    "decor_pew": ("painted_wooden_bench", 1.9, 0),
    "decor_bust": ("marble_bust_01", 0.55, 0),
    "decor_candleholders": ("brass_candleholder", 1.1, 0),
}

# re-running: bake and drop the rigs of an earlier run (bounding boxes are re-measured below)
for r in [o for o in bpy.data.objects if o.name.startswith("rig_")]:
    for c in list(r.children):
        mw = c.matrix_world.copy()
        c.parent = None
        c.matrix_world = mw
    bpy.data.objects.remove(r, do_unlink=True)

sc = bpy.data.scenes.get("Candlepath_Decor") or bpy.data.scenes.new("Candlepath_Decor")
for o in list(sc.collection.objects):
    sc.collection.objects.unlink(o)

groups = {}
for name, (prefix, width, rot) in PROPS.items():
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(prefix)]
    if not objs:
        print("missing", prefix)
        continue
    for o in objs:
        sc.collection.objects.link(o)
    groups[name] = (objs, width, rot)


def world_bbox(objs):
    pts = [o.matrix_world @ mathutils.Vector(c) for o in objs for c in o.bound_box]
    lo = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


# a rig empty per prop: origin at the bottom centre of the prop, scaled so the prop is `width` wide
rigs = {}
for name, (objs, width, rot) in groups.items():
    bpy.context.view_layer.update()
    lo, hi = world_bbox(objs)
    base = mathutils.Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    rig = bpy.data.objects.new("rig_" + name, None)
    sc.collection.objects.link(rig)
    rig.location = base
    bpy.context.view_layer.update()
    for o in objs:
        if o.parent is None:
            o.parent = rig
            o.matrix_parent_inverse = rig.matrix_world.inverted()
    s = width / max(hi.x - lo.x, 1e-4)
    rig.scale = (s, s, s)
    rig.rotation_euler = (0, 0, math.radians(rot))
    rig.location = (0, 0, 0)
    rigs[name] = (rig, objs)

cam_d = bpy.data.cameras.get("DecorCam") or bpy.data.cameras.new("DecorCam")
cam_d.type = 'ORTHO'
cam_d.ortho_scale = ORTHO
cam = bpy.data.objects.get("DecorCam") or bpy.data.objects.new("DecorCam", cam_d)
if cam.name not in sc.collection.objects:
    sc.collection.objects.link(cam)
tgt = mathutils.Vector((0, 0, 0.55))
el = math.radians(ELEV)
cam.location = tgt + mathutils.Vector((0, -10 * math.cos(el), 10 * math.sin(el)))
cam.rotation_euler = (math.radians(90 - ELEV), 0, 0)
sc.camera = cam

sun_d = bpy.data.lights.get("DecorSun") or bpy.data.lights.new("DecorSun", 'SUN')
sun_d.energy = 2.6
sun = bpy.data.objects.get("DecorSun") or bpy.data.objects.new("DecorSun", sun_d)
if sun.name not in sc.collection.objects:
    sc.collection.objects.link(sun)
sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))

w = bpy.data.worlds.get("DecorWorld") or bpy.data.worlds.new("DecorWorld")
w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.55, 0.55, 0.7, 1)
bg.inputs["Strength"].default_value = 0.9
sc.world = w

for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
    try:
        sc.render.engine = eng
        break
    except TypeError:
        pass
sc.render.film_transparent = True
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.resolution_percentage = 100
sc.render.image_settings.file_format = 'PNG'
sc.render.image_settings.color_mode = 'RGBA'
sc.view_settings.view_transform = 'Standard'

for name, (rig, objs) in rigs.items():
    for other, (_, oo) in rigs.items():
        for o in oo:
            o.hide_render = other != name
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True, scene=sc.name)
    print("wrote", sc.render.filepath)

for _, (rig, objs) in rigs.items():
    for o in objs:
        o.hide_render = False
