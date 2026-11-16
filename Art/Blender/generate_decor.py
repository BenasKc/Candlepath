"""Candlepath static decoration sprites (all three maps).

Renders Poly Haven (CC0) props to transparent PNGs from the same camera as the other sprites
(orthographic, 60 deg elevation, 2.4 world units across 256 px, see generate_sprites.py).

The props must already be in the open Blender file. Import them first with the Blender MCP
(import_asset, source polyhaven, asset_type models):
    Library: wooden_bookshelf_worn  Shelf_01  round_wooden_table_02  book_encyclopedia_set_01  wooden_crate_02
    Rusys:   wine_barrel_01  wooden_bucket_01  wooden_bucket_02  wine_bottles_01  wicker_basket_02  wooden_crate_01
    Chapel:  painted_wooden_bench  marble_bust_01  brass_candleholders
    Second batch (Library / Chapel / Rusys extras):
             GothicCabinet_01  GothicCommode_01  vintage_cabinet_01  WoodenTable_02  WoodenChair_01  chess_set
             vintage_oil_lamp  ArmChair_01  antique_ceramic_vase_01  wooden_candlestick  brass_vase_02
             CheeseBox_01  treasure_chest
then run this script in Blender's Scripting tab (or through the MCP):
    OUT defaults to <project>/Assets/Decor; override with  blender ... -P generate_decor.py -- <dir>

Or headless, importing Poly Haven glTF downloads (one folder per asset id, as from the 1k glTF zip):
    blender -b --factory-startup -P Art/Blender/generate_decor.py -- <out dir> --import <downloads dir>
Props whose models aren't loaded are skipped ("missing ..."), so a run can render just one batch.

Each prop is scaled to a footprint of 1 or 2 grid cells (1 cell = 1 world unit), its base is put on the
ground at the origin, and the sprite is rendered in a separate scene, so the open scene is left alone.
"""
import math
import os
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
IMPORT = None
if "--import" in argv:
    i = argv.index("--import")
    IMPORT = argv[i + 1]
    del argv[i:i + 2]
PROJECT = "/home/benas/Dokumentai/Universitetas/IV_kursas/KŽPK/Candlepath/Candlepath"
OUT = os.path.abspath(argv[0] if argv else os.path.join(PROJECT, "Assets", "Decor"))
os.makedirs(OUT, exist_ok=True)

# Headless: import each <IMPORT>/<asset id>/*.gltf. Meshes are renamed to start with the asset id
# (chess_set's parts are just "board", "piece_...") so the prefixes below find them.
if IMPORT:
    import glob
    for gltf in sorted(glob.glob(os.path.join(IMPORT, "*", "*.gltf"))):
        asset = os.path.basename(os.path.dirname(gltf))
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=gltf)
        for o in set(bpy.data.objects) - before:
            if o.type == 'MESH' and not o.name.startswith(asset):
                o.name = f"{asset}_{o.name}"

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
    # Library, second batch: dark furniture that reads from above, and things to put on tables
    "decor_cabinet": ("GothicCabinet_01", 1.4, 0),     # tall: wider than this clips the top of the frame
    "decor_commode": ("GothicCommode_01", 1.7, 0),
    "decor_vintage_cabinet": ("vintage_cabinet_01", 0.95, 0),
    "decor_table": ("WoodenTable_02", 0.85, 0),        # round pedestal table, one cell
    "decor_chair": ("WoodenChair_01", 0.5, 0),
    "decor_chess": ("chess_set", 0.45, 0),
    "decor_oil_lamp": ("vintage_oil_lamp", 0.28, 0),
    "decor_armchair": ("ArmChair_01", 0.85, 0),
    "decor_vase": ("antique_ceramic_vase_01", 0.4, 0),
    # Chapel, second batch: candlesticks and vases for the altar (decor_commode) instead of floor lamps
    "decor_candlestick": ("wooden_candlestick", 0.3, 0),
    "decor_brass_vase": ("brass_vase_02", 0.35, 0),
    # Rusys extras
    "decor_cheese_box": ("CheeseBox_01", 0.6, 0),
    "decor_chest": ("treasure_chest", 0.9, 0),
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
