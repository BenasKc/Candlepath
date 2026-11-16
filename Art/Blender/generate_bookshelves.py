"""Candlepath library sprites: bookshelves full of books and a pile of books, built procedurally.

Poly Haven has no filled bookshelf, so the frame is modelled here (dark planks texture) and filled with
leather-bound books of random height, colour and lean. Same camera and light as generate_decor.py
(orthographic, 60 deg elevation, 2.4 world units across 256 px), so the sprites match the other decor.

    blender -b --factory-startup -P Art/Blender/generate_bookshelves.py -- [out dir]

Writes decor_bookshelf_tall.png (2 cells, against a wall), decor_bookshelf_low.png (2 cells, freestanding)
and decor_book_stack.png (a small pile for tables and floors). Seeded, so reruns give the same books.
"""
import math
import os
import random
import sys

import bpy
import mathutils

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(PROJECT, "Assets", "Decor"))
os.makedirs(OUT, exist_ok=True)
WOOD = os.path.join(PROJECT, "Art", "Source", "polyhaven", "dark_wooden_planks_diff_1k.png")

RES, ORTHO, ELEV = 256, 2.4, 60.0

# muted leather: oxblood, bottle green, navy, tan, umber, black, ochre, plum
LEATHER = [(0.32, 0.06, 0.05), (0.08, 0.18, 0.10), (0.07, 0.10, 0.22), (0.45, 0.28, 0.14), (0.22, 0.13, 0.07),
           (0.05, 0.04, 0.04), (0.50, 0.36, 0.12), (0.22, 0.08, 0.16)]

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
sc = bpy.context.scene


def material(name, color, rough=0.6, metal=0.0, tex=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if tex and os.path.exists(tex):
        t = m.node_tree.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(tex, check_existing=True)
        mix = m.node_tree.nodes.new("ShaderNodeMix")
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[6].default_value = (*color, 1)
        m.node_tree.links.new(t.outputs["Color"], mix.inputs[7])
        m.node_tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return m


wood = material("Wood", (0.55, 0.40, 0.30), 0.7, tex=WOOD)
gold = material("Gold", (0.85, 0.62, 0.28), 0.35, 1.0)
pages = material("Pages", (0.30, 0.24, 0.15), 0.9)
# the colours above are picked by eye (sRGB); Blender wants linear
leathers = [material(f"Leather{i}", tuple(v ** 2.2 for v in c), 0.55) for i, c in enumerate(LEATHER)]


def box(name, size, loc, mat, parent, rot_y=0.0, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    o.rotation_euler = (0, rot_y, 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        b = o.modifiers.new("bevel", 'BEVEL')
        b.width = bevel
        b.segments = 2
    o.data.materials.append(mat)
    o.parent = parent
    return o


def book(parent, x, z, w, h, d, y, lean=0.0):
    """A book standing on its bottom edge with its spine facing -Y; x is its left edge."""
    leather = random.choice(leathers)
    cx = x + w / 2 + math.sin(lean) * h / 2
    cz = z + h / 2 * math.cos(lean)
    box("book", (w, d, h), (cx, y, cz), leather, parent, lean, bevel=0.004)
    # page block shows as a pale stripe on top; gilt bands across the spine
    box("pages", (w * 0.8, d * 0.96, 0.006), (cx, y + 0.004, cz + h / 2 * math.cos(lean) + 0.001), pages, parent, lean)
    if random.random() < 0.75:
        for f in (0.18, 0.8):
            box("band", (w * 1.02, 0.004, h * 0.03), (cx + math.sin(lean) * h * (f - 0.5), y - d / 2 - 0.001,
                                                       cz + (f - 0.5) * h * math.cos(lean)), gold, parent, lean)


def fill_shelf(parent, x0, x1, z, height, depth, y):
    x = x0 + random.uniform(0, 0.03)
    while x < x1 - 0.03:
        r = random.random()
        if r < 0.06:                     # gap
            x += random.uniform(0.05, 0.12)
            continue
        if r < 0.14 and x1 - x > 0.25:   # a few lying flat
            n = random.randint(2, 4)
            w = random.uniform(0.18, 0.24)
            for i in range(n):
                t = random.uniform(0.03, 0.05)
                box("flat", (w, depth * 0.85, t), (x + w / 2, y, z + t / 2 + i * t * 1.02), random.choice(leathers),
                    parent, bevel=0.004)
            x += w + 0.01
            continue
        w = random.uniform(0.035, 0.07)
        h = random.uniform(0.62, 0.9) * height
        if x + w > x1:
            break
        lean = 0.0
        if r > 0.95 and x1 - x > 0.2:     # one leaning on its neighbour
            lean = -random.uniform(0.2, 0.35)
            x += math.sin(-lean) * h
        book(parent, x, z, w, h, depth * random.uniform(0.75, 0.92), y, lean)
        x += w + random.uniform(0.0, 0.006)


def pile(parent, x, y, z, n):
    """Books lying flat on top of the shelf, slightly askew."""
    for i in range(n):
        w, d, t = random.uniform(0.2, 0.28), random.uniform(0.15, 0.2), random.uniform(0.035, 0.05)
        o = box("flat", (w, d, t), (x + random.uniform(-0.02, 0.02), y, z + t / 2), random.choice(leathers), parent,
                bevel=0.004)
        o.rotation_euler = (0, 0, random.uniform(-0.3, 0.3))
        z += t


def bookshelf(name, width, height, depth, shelves, crown=True):
    rig = bpy.data.objects.new(name, None)
    sc.collection.objects.link(rig)
    t = 0.05
    box("side_l", (t, depth, height), (-width / 2 + t / 2, 0, height / 2), wood, rig)
    box("side_r", (t, depth, height), (width / 2 - t / 2, 0, height / 2), wood, rig)
    box("back", (width, 0.02, height), (0, depth / 2 - 0.01, height / 2), wood, rig)
    box("plinth", (width + 0.04, depth + 0.04, 0.08), (0, 0, 0.04), wood, rig, bevel=0.01)
    top = height + (0.05 if crown else 0.03)
    box("top", (width + (0.1 if crown else 0.04), depth + (0.08 if crown else 0.04), top - height + 0.03),
        (0, -0.01, height + (top - height) / 2 - 0.015), wood, rig, bevel=0.012)
    inner = width - 2 * t
    pitch = (height - 0.08) / shelves
    for i in range(shelves):
        z = 0.08 + i * pitch
        if i:
            box("shelf", (inner, depth - 0.02, 0.03), (0, -0.01, z), wood, rig)
        fill_shelf(rig, -inner / 2, inner / 2, z + (0.015 if i else 0), pitch - 0.04, depth - 0.06, -0.02)
    # break up the plain top: a pile at one end, a couple of books at the other
    pile(rig, -width / 2 + 0.3, 0, top, random.randint(2, 4))
    pile(rig, width / 2 - 0.45, 0.02, top, random.randint(1, 2))
    return rig


def book_stack(name):
    rig = bpy.data.objects.new(name, None)
    sc.collection.objects.link(rig)
    z = 0.0
    for i in range(5):
        w, d, t = random.uniform(0.2, 0.28), random.uniform(0.15, 0.2), random.uniform(0.035, 0.06)
        o = box("flat", (w, d, t), (random.uniform(-0.02, 0.02), random.uniform(-0.02, 0.02), z + t / 2),
                random.choice(leathers), rig, bevel=0.004)
        o.rotation_euler = (0, 0, random.uniform(-0.35, 0.35))
        z += t
    return rig


random.seed(7)
props = {
    "decor_bookshelf_tall": bookshelf("tall", 1.85, 1.6, 0.3, 5),
    "decor_bookshelf_low": bookshelf("low", 1.85, 0.8, 0.3, 2, crown=False),
    "decor_book_stack": book_stack("stack"),
}

# camera, sun and sky as in generate_decor.py
cam_d = bpy.data.cameras.new("DecorCam")
cam_d.type = 'ORTHO'
cam_d.ortho_scale = ORTHO
cam = bpy.data.objects.new("DecorCam", cam_d)
sc.collection.objects.link(cam)
tgt = mathutils.Vector((0, 0, 0.55))
el = math.radians(ELEV)
cam.location = tgt + mathutils.Vector((0, -10 * math.cos(el), 10 * math.sin(el)))
cam.rotation_euler = (math.radians(90 - ELEV), 0, 0)
sc.camera = cam

sun_d = bpy.data.lights.new("DecorSun", 'SUN')
sun_d.energy = 2.6
sun = bpy.data.objects.new("DecorSun", sun_d)
sc.collection.objects.link(sun)
sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))

w = bpy.data.worlds.new("DecorWorld")
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

for name, rig in props.items():
    for other in props.values():
        for c in other.children:
            c.hide_render = other is not rig
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("wrote", sc.render.filepath)
