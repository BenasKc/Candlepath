"""Candlepath PoC sprite generator.

Builds the PoC sprites (candles, lantern, Eternal Lamp, wax puddle, shadow enemy)
in a separate Blender scene and renders each one to a transparent PNG.

Usage (headless):
    blender -b -P Art/Blender/generate_sprites.py -- Assets/Sprites
or paste into Blender's Scripting tab (output defaults to ./Assets/Sprites next to the .blend).

Camera: orthographic, ELEV degrees above the ground (design doc, 6.1). Same scale for all sprites.
Palette: design doc, 8 pav.
"""
import math
import os
import random
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.path.dirname(bpy.data.filepath or "."), "Assets", "Sprites"))
os.makedirs(OUT, exist_ok=True)

RES, ORTHO, ELEV = 256, 2.4, 60.0  # px, world units across the frame, camera elevation (deg)

PAL = dict(tamsa="#0E0C16", akmuo="#2E2B3A", seseliai="#4A2F6B", vaskas="#EFE3C2",
           liepsna="#FF9A3C", zarija="#D9481C", zalvaris="#C8A25A")


def hexc(h):
    h = h.lstrip("#")
    r, g, b = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = lambda c: ((c + 0.055) / 1.055) ** 2.4 if c > 0.04045 else c / 12.92
    return (lin(r), lin(g), lin(b), 1)


# --- separate scene, so the user's own scene is never touched
sc = bpy.data.scenes.get("Candlepath_Sprites") or bpy.data.scenes.new("Candlepath_Sprites")
if bpy.context.window:
    bpy.context.window.scene = sc
for o in list(sc.objects):
    bpy.data.objects.remove(o, do_unlink=True)


def mat(name, color, rough=0.6, metal=0.0, emit=None, strength=1.3, alpha=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    b.inputs["Alpha"].default_value = alpha
    b.inputs["Emission Color"].default_value = emit if emit else (0, 0, 0, 1)
    b.inputs["Emission Strength"].default_value = strength if emit else 0.0
    if alpha < 1:
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    return m


M = dict(
    wax=mat("wax", hexc(PAL["vaskas"]), 0.55),
    brass=mat("brass", hexc(PAL["zalvaris"]), 0.45, 0.6),
    stone=mat("stone", hexc(PAL["akmuo"]), 0.9),
    flame=mat("flame", hexc(PAL["liepsna"]), 0.5, emit=hexc(PAL["liepsna"]), strength=1.1),
    core=mat("flame_core", hexc("#FFD58A"), 0.5, emit=hexc("#FFD58A"), strength=1.4),
    wick=mat("wick", hexc(PAL["tamsa"]), 0.9),
    glass=mat("glass", hexc("#BFD6E8"), 0.05, alpha=0.14),
    shadow=mat("shadow", hexc(PAL["seseliai"]), 0.8),
    eye=mat("eye", hexc("#FFE36B"), 0.5, emit=hexc("#FFE36B"), strength=1.5),
    puddle=mat("puddle", hexc(PAL["vaskas"]), 0.3),
)


def prim(kind, m, loc=(0, 0, 0), scale=(1, 1, 1), parent=None, **kw):
    getattr(bpy.ops.mesh, f"primitive_{kind}_add")(location=loc, **kw)
    o = bpy.context.active_object
    # primitive ops link into the active collection; move into our scene
    for c in list(o.users_collection):
        c.objects.unlink(o)
    sc.collection.objects.link(o)
    o.scale = scale
    o.data.materials.append(m)
    bpy.ops.object.shade_smooth()
    if parent:
        o.parent = parent
    return o


def root(name):
    r = bpy.data.objects.new(name, None)
    sc.collection.objects.link(r)
    return r


def flame(p, z, s=1.0):
    prim("uv_sphere", M["flame"], (0, 0, z + 0.12 * s), (0.09 * s, 0.09 * s, 0.13 * s), p, segments=16, ring_count=10)
    prim("cone", M["flame"], (0, 0, z + 0.33 * s), (1, 1, 1), p, vertices=16, radius1=0.09 * s, depth=0.26 * s)
    prim("uv_sphere", M["core"], (0, 0, z + 0.09 * s), (0.05 * s, 0.05 * s, 0.09 * s), p, segments=12, ring_count=8)


def tea_candle(name, wax_r, wax_h):
    p = root(name)
    prim("cylinder", M["brass"], (0, 0, 0.07), (1, 1, 1), p, vertices=40, radius=0.44, depth=0.14)
    prim("torus", M["brass"], (0, 0, 0.15), (1, 1, 1), p, major_radius=0.44, minor_radius=0.03)
    prim("cylinder", M["wick"], (0, 0, 0.145), (1, 1, 1), p, vertices=40, radius=0.41, depth=0.01)  # cup interior
    top = 0.14 + wax_h
    prim("cylinder", M["wax"], (0, 0, 0.14 + wax_h / 2), (1, 1, 1), p, vertices=40, radius=wax_r, depth=wax_h)
    prim("cylinder", M["wick"], (0, 0, top + 0.05), (1, 1, 1), p, vertices=8, radius=0.015, depth=0.1)
    flame(p, top + 0.09, 1.5)
    return p


def lantern(name, wax_h):
    p = root(name)
    prim("cylinder", M["brass"], (0, 0, 0.06), (1, 1, 1), p, vertices=32, radius=0.40, depth=0.12)
    prim("cylinder", M["wax"], (0, 0, 0.12 + wax_h / 2), (1, 1, 1), p, vertices=32, radius=0.14, depth=wax_h)
    top = 0.12 + wax_h
    prim("cylinder", M["wick"], (0, 0, top + 0.04), (1, 1, 1), p, vertices=8, radius=0.015, depth=0.08)
    flame(p, top + 0.07, 1.4)
    for a in range(4):
        t = a * math.pi / 2 + math.pi / 4
        prim("cylinder", M["brass"], (0.37 * math.cos(t), 0.37 * math.sin(t), 0.62), (1, 1, 1), p, vertices=8, radius=0.03, depth=1.0)
    prim("cylinder", M["glass"], (0, 0, 0.62), (1, 1, 1), p, vertices=32, radius=0.37, depth=1.0)
    prim("torus", M["brass"], (0, 0, 1.12), (1, 1, 1), p, major_radius=0.38, minor_radius=0.04)  # open top rim
    prim("torus", M["brass"], (0, 0, 1.35), (1, 1, 1), p, major_radius=0.11, minor_radius=0.02)  # carry ring
    for a in range(2):
        t = a * math.pi
        o = prim("cylinder", M["brass"], (0.2 * math.cos(t), 0, 1.24), (1, 1, 1), p, vertices=8, radius=0.012, depth=0.26)
        o.rotation_euler = (0, math.radians(35) * (1 if a == 0 else -1), 0)
    return p


def eternal_lamp(name):
    p = root(name)
    prim("cylinder", M["stone"], (0, 0, 0.2), (1, 1, 1), p, vertices=6, radius=0.62, depth=0.4)
    prim("cylinder", M["stone"], (0, 0, 0.5), (1, 1, 1), p, vertices=6, radius=0.48, depth=0.2)
    prim("cylinder", M["brass"], (0, 0, 0.65), (1, 1, 1), p, vertices=32, radius=0.46, depth=0.1)
    prim("cylinder", M["wick"], (0, 0, 0.71), (1, 1, 1), p, vertices=8, radius=0.025, depth=0.1)
    flame(p, 0.74, 3.0)
    for a in range(6):
        t = a * math.pi / 3
        prim("cylinder", M["brass"], (0.43 * math.cos(t), 0.43 * math.sin(t), 1.15), (1, 1, 1), p, vertices=8, radius=0.035, depth=0.9)
    prim("cylinder", M["glass"], (0, 0, 1.15), (1, 1, 1), p, vertices=32, radius=0.42, depth=0.9)
    prim("torus", M["brass"], (0, 0, 1.6), (1, 1, 1), p, major_radius=0.43, minor_radius=0.05)
    prim("torus", M["brass"], (0, 0, 1.75), (1, 1, 1), p, major_radius=0.12, minor_radius=0.025)
    return p


def wax_puddle(name):
    p = root(name)
    random.seed(7)
    prim("cylinder", M["puddle"], (0, 0, 0.03), (1.0, 0.85, 1), p, vertices=48, radius=0.42, depth=0.06)
    for i in range(6):
        t = i * math.pi / 3 + random.random() * 0.5
        r = 0.34 + random.random() * 0.08
        prim("cylinder", M["puddle"], (r * math.cos(t), r * math.sin(t) * 0.85, 0.025), (1, 1, 1), p,
             vertices=24, radius=0.12 + random.random() * 0.07, depth=0.05)
    prim("uv_sphere", M["puddle"], (0.05, -0.02, 0.07), (0.16, 0.14, 0.05), p)
    return p


def shadow_enemy(name):
    p = root(name)
    prim("uv_sphere", M["shadow"], (0, 0, 0.32), (0.34, 0.34, 0.30), p, segments=32, ring_count=16)
    prim("cone", M["shadow"], (0, 0, 0.12), (1, 1, 1), p, vertices=32, radius1=0.34, radius2=0.18, depth=0.24)
    for sx in (-1, 1):
        prim("uv_sphere", M["eye"], (sx * 0.12, -0.28, 0.38), (0.055, 0.04, 0.075), p, segments=16, ring_count=8)
    return p


SPRITES = {
    "candle_tea_1_full": lambda n: tea_candle(n, 0.40, 0.26),
    "candle_tea_2_mid": lambda n: tea_candle(n, 0.30, 0.15),
    "candle_tea_3_low": lambda n: tea_candle(n, 0.19, 0.06),
    "lantern_1_full": lambda n: lantern(n, 0.42),
    "lantern_2_mid": lambda n: lantern(n, 0.26),
    "lantern_3_low": lambda n: lantern(n, 0.10),
    "eternal_lamp": eternal_lamp,
    "wax_puddle": wax_puddle,
    "enemy_shadow": shadow_enemy,
}
roots = {k: f(k) for k, f in SPRITES.items()}

# --- camera, light, render settings
cam_d = bpy.data.cameras.new("SpriteCam")
cam_d.type = 'ORTHO'
cam_d.ortho_scale = ORTHO
cam = bpy.data.objects.new("SpriteCam", cam_d)
sc.collection.objects.link(cam)
tgt = mathutils.Vector((0, 0, 0.55))
el = math.radians(ELEV)
cam.location = tgt + mathutils.Vector((0, -10 * math.cos(el), 10 * math.sin(el)))
cam.rotation_euler = (math.radians(90 - ELEV), 0, 0)
sc.camera = cam

sun_d = bpy.data.lights.new("Sun", 'SUN')
sun_d.energy = 2.6
sun = bpy.data.objects.new("Sun", sun_d)
sc.collection.objects.link(sun)
sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))

w = bpy.data.worlds.new("SpriteWorld")
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

# --- render each sprite on its own
for k in roots:
    for kk, rr in roots.items():
        hide = kk != k
        rr.hide_render = hide
        for c in rr.children:
            c.hide_render = hide
    sc.render.filepath = os.path.join(OUT, f"{k}.png")
    bpy.ops.render.render(write_still=True, scene=sc.name)
    print("wrote", sc.render.filepath)

# leave everything visible, laid out in a row, for inspection in the viewport
for i, r in enumerate(roots.values()):
    r.hide_render = False
    for c in r.children:
        c.hide_render = False
    r.location = ((i - 4) * 1.8, 0, 0)
