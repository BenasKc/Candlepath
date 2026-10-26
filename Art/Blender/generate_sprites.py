"""Candlepath PoC sprite generator.

Builds the sprites (tea/long/spark candles, candelabra, lantern, Eternal Lamp, wax puddle,
shadow enemies) in a separate Blender scene and renders each one to a transparent PNG.

Usage (headless):
    blender -b -P Art/Blender/generate_sprites.py -- Assets/Sprites [name-prefix ...] [--anim]
or paste into Blender's Scripting tab (output defaults to ./Assets/Sprites next to the .blend).

With --anim every sprite except the wax puddle is rendered as FRAMES looping frames
(flame flicker, spark drift, wing flap, wisp sway, waddle, stomp, blinking eyes, ...).

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
ANIM_MODE = "--anim" in argv  # render looping frame sequences into <out>/Anim/<sprite>/ instead of stills
ONLY = [a for a in argv[1:] if a != "--anim"]  # optional name prefixes; render just those sprites (e.g. candle_long candelabra)
FRAMES = 8  # frames per animation loop
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
    ember=mat("ember_wax", hexc(PAL["zarija"]), 0.5),
    spark=mat("spark", hexc("#FFE9B0"), 0.5, emit=hexc("#FFE9B0"), strength=2.0),
    wing=mat("wing", hexc("#6E4DA0"), 0.8),
    ghost=mat("ghost", hexc("#8FA3D6"), 0.4, alpha=0.62),
    dark=mat("dark_stone", hexc("#3B3650"), 0.9),
    crack=mat("crack", hexc("#9B6BE0"), 0.5, emit=hexc("#9B6BE0"), strength=1.6),
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


ANIM = False  # True while rendering animation frames; stills are rendered with every wave() at 0
PH = 0.0      # loop phase 0..1 of the frame being built


def wave(f=1, off=0.0):
    """sin of the loop phase (f whole cycles per loop keeps the loop seamless); 0 for stills."""
    return math.sin(2 * math.pi * (PH * f + off)) if ANIM else 0.0


def blink():
    return 0.12 if ANIM and 0.80 <= PH < 0.90 else 1.0


def flame(p, z, s=1.0, x=0.0, y=0.0, seed=0.0):
    fl = 1 + 0.10 * wave(1, seed) + 0.05 * wave(2, seed + 0.3)  # flicker height
    sway = 0.03 * s * wave(1, seed + 0.25)
    w = 1 / math.sqrt(fl)
    prim("uv_sphere", M["flame"], (x + sway * 0.5, y, z + 0.12 * s * fl), (0.09 * s * w, 0.09 * s * w, 0.13 * s * fl), p, segments=16, ring_count=10)
    c = prim("cone", M["flame"], (x + sway * 0.5, y, z + 0.20 * s + 0.13 * s * fl), (1, 1, 1), p, vertices=16, radius1=0.09 * s * w, depth=0.26 * s * fl)
    c.rotation_euler = (0, 0.18 * wave(1, seed + 0.25), 0)
    prim("uv_sphere", M["core"], (x + sway * 0.3, y, z + 0.09 * s), (0.05 * s * w, 0.05 * s * w, 0.09 * s * fl), p, segments=12, ring_count=8)


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


def candle_at(p, x, y, z0, r, h, fs, wax=None, seed=0.0):
    prim("cylinder", wax or M["wax"], (x, y, z0 + h / 2), (1, 1, 1), p, vertices=32, radius=r, depth=h)
    prim("cylinder", M["wick"], (x, y, z0 + h + 0.04), (1, 1, 1), p, vertices=8, radius=0.012, depth=0.08)
    top = z0 + h + 0.07
    flame(p, top, fs, x, y, seed)
    return top


def long_candle(name, wax_h):
    """Ilgoji zvake: tall thin candle in a small brass holder."""
    p = root(name)
    prim("cylinder", M["brass"], (0, 0, 0.05), (1, 1, 1), p, vertices=32, radius=0.30, depth=0.10)
    prim("torus", M["brass"], (0, 0, 0.10), (1, 1, 1), p, major_radius=0.30, minor_radius=0.025)
    prim("cylinder", M["brass"], (0, 0, 0.17), (1, 1, 1), p, vertices=24, radius=0.15, depth=0.14)  # socket
    candle_at(p, 0, 0, 0.24, 0.115, wax_h, 1.3)
    for a in range(2):  # a couple of wax drips down the side
        t = math.radians(-70 + a * 140)
        prim("uv_sphere", M["wax"], (0.115 * math.cos(t), 0.115 * math.sin(t), 0.24 + wax_h - 0.10 - a * 0.06),
             (0.035, 0.035, 0.09), p, segments=12, ring_count=8)
    return p


def candelabra(name, side_h, mid_h):
    """Zvakide: brass stand with three candles."""
    p = root(name)
    prim("cylinder", M["brass"], (0, 0, 0.05), (1, 1, 1), p, vertices=32, radius=0.28, depth=0.10)
    prim("torus", M["brass"], (0, 0, 0.10), (1, 1, 1), p, major_radius=0.28, minor_radius=0.03)
    prim("cylinder", M["brass"], (0, 0, 0.50), (1, 1, 1), p, vertices=16, radius=0.045, depth=0.80)
    prim("uv_sphere", M["brass"], (0, 0, 0.30), (0.09, 0.09, 0.07), p, segments=16, ring_count=8)
    arm = prim("cylinder", M["brass"], (0, 0, 0.74), (1, 1, 1), p, vertices=12, radius=0.025, depth=0.64)
    arm.rotation_euler = (0, math.radians(90), 0)
    prim("cylinder", M["brass"], (0, 0, 0.90), (1, 1, 1), p, vertices=24, radius=0.10, depth=0.04)  # centre cup
    candle_at(p, 0, 0, 0.92, 0.075, mid_h, 0.9, seed=0.0)
    for i, sx in enumerate((-1, 1)):
        prim("cylinder", M["brass"], (sx * 0.32, 0, 0.80), (1, 1, 1), p, vertices=12, radius=0.025, depth=0.14)
        prim("cylinder", M["brass"], (sx * 0.32, 0, 0.88), (1, 1, 1), p, vertices=24, radius=0.095, depth=0.04)
        candle_at(p, sx * 0.32, 0, 0.90, 0.07, side_h, 0.9, seed=0.33 + 0.33 * i)
    return p


def spark_candle(name, wax_h):
    """Ziezirbine zvake: stubby ember-red candle that throws sparks."""
    p = root(name)
    prim("cylinder", M["brass"], (0, 0, 0.06), (1, 1, 1), p, vertices=40, radius=0.34, depth=0.12)
    prim("torus", M["brass"], (0, 0, 0.13), (1, 1, 1), p, major_radius=0.34, minor_radius=0.03)
    top = candle_at(p, 0, 0, 0.12, 0.20, wax_h, 1.4, wax=M["ember"])
    prim("torus", M["wax"], (0, 0, 0.12 + wax_h * 0.55), (1, 1, 1), p, major_radius=0.20, minor_radius=0.025)  # pale band
    fz = top + 0.2
    for i, (a, r, dz, s) in enumerate([(20, 0.26, 0.05, 1.0), (95, 0.30, 0.22, 0.8), (170, 0.24, 0.40, 1.1),
                                       (235, 0.32, 0.12, 0.7), (300, 0.27, 0.32, 0.9), (340, 0.20, 0.55, 0.6)]):
        if ANIM:  # each spark rises, drifts round and fades, staggered so the loop is continuous
            life = (PH + i / 6) % 1.0
            a, dz, s = a + 50 * life, 0.04 + 0.60 * life, s * max(0.1, math.sin(math.pi * life) ** 0.7)
        t = math.radians(a)
        prim("ico_sphere", M["spark"], (r * math.cos(t), r * math.sin(t), fz + dz), (s, s, s), p,
             subdivisions=1, radius=0.04)
    return p


def eyes(p, y, z, x=0.12, rx=0.055, rz=0.075):
    for sx in (-1, 1):
        prim("uv_sphere", M["eye"], (sx * x, y, z), (rx, 0.04, rz * blink()), p, segments=16, ring_count=8)


def moth(name):
    """Kandis: hovering moth, drawn to light."""
    p = root(name)
    p.location.z = 0.04 * wave(1, 0.25)  # hover bob
    prim("uv_sphere", M["shadow"], (0, 0, 0.42), (0.13, 0.20, 0.13), p, segments=24, ring_count=12)
    prim("uv_sphere", M["shadow"], (0, -0.17, 0.46), (0.11, 0.10, 0.10), p, segments=20, ring_count=10)
    flap = math.radians(22 + 40 * wave(1))  # wing tilt: 22 deg at rest, flaps up/down
    for sx in (-1, 1):
        for k, (dy, w, d) in enumerate([(-0.04, 0.30, 0.34), (0.14, 0.22, 0.22)]):
            x = (0.22 + 0.05 * k) * math.cos(flap) / math.cos(math.radians(22))
            z = 0.50 + w * 0.5 * (math.sin(flap) - math.sin(math.radians(22)))
            wg = prim("uv_sphere", M["wing"], (sx * x, dy, z), (w, d, 0.025), p, segments=24, ring_count=10)
            wg.rotation_euler = (0, -flap * sx, math.radians(14) * sx * (1 if k == 0 else -1))
        an = prim("cylinder", M["shadow"], (sx * 0.09, -0.27, 0.62), (1, 1, 1), p, vertices=8, radius=0.012, depth=0.2)
        an.rotation_euler = (math.radians(-25 + 6 * wave(1, 0.2)), math.radians(20) * sx, 0)
    eyes(p, -0.255, 0.48, x=0.055, rx=0.035, rz=0.045)
    return p


def wind_spirit(name):
    """Vejo dvasia: wispy translucent spirit with a curling tail."""
    p = root(name)
    p.location.z = 0.04 * wave(1)
    prim("uv_sphere", M["ghost"], (0, 0, 0.62), (0.26, 0.24, 0.27), p, segments=32, ring_count=16)
    for i in range(1, 6):  # tail swoops down and out to the side, shrinking; a wave runs along it
        t = i / 5
        r = 0.25 * (1 - 0.65 * t)
        prim("uv_sphere", M["ghost"], (0.50 * t + 0.05 + 0.06 * t * wave(1, -t * 0.6 + 0.25), -0.10 * t,
                                       0.58 - 0.38 * t + 0.08 * t * wave(1, -t * 0.6)),
             (r, r, r * 0.9), p, segments=20, ring_count=10)
    eyes(p, -0.22, 0.66)
    return p


def wax_gnawer(name):
    """Vasko grauzikas: plump rodent with buck teeth, dripping wax."""
    p = root(name)
    p.rotation_euler = (0, 0.07 * wave(1), 0)  # waddle: roll side to side and hop twice per loop
    p.location.z = 0.035 * abs(wave(1))
    prim("uv_sphere", M["shadow"], (0, 0.08, 0.24), (0.27, 0.40, 0.24), p, segments=28, ring_count=14)  # body
    prim("uv_sphere", M["shadow"], (0, -0.30, 0.26), (0.19, 0.19, 0.18), p, segments=24, ring_count=12)  # head
    prim("cone", M["shadow"], (0, -0.48, 0.24), (1, 1, 1), p, vertices=16, radius1=0.12, depth=0.16).rotation_euler = (math.radians(90), 0, 0)
    prim("uv_sphere", M["wick"], (0, -0.57, 0.25), (0.035, 0.03, 0.03), p, segments=12, ring_count=8)  # nose
    for sx in (-1, 1):
        prim("uv_sphere", M["wing"], (sx * 0.15, -0.26, 0.42), (0.08, 0.03, 0.10), p, segments=16, ring_count=8)  # ears
        prim("cube", M["wax"], (sx * 0.025, -0.53, 0.15), (0.022, 0.018, 0.045), p, size=2)  # buck teeth
    eyes(p, -0.45, 0.33, x=0.08, rx=0.04, rz=0.05)
    for sx, y in ((-1, 0.0), (1, 0.18)):  # wax drips on the back
        prim("uv_sphere", M["wax"], (sx * 0.17, y, 0.34), (0.06, 0.06, 0.08), p, segments=12, ring_count=8)
    for i in range(1, 5):  # tail curving out and up behind
        t = i / 4
        r = 0.065 * (1.15 - 0.6 * t)
        prim("uv_sphere", M["shadow"], (0.05 * i + 0.05 * t * wave(1, -t * 0.5), 0.46 + 0.07 * i, 0.14 + 0.06 * i), (r, r, r), p, segments=12, ring_count=8)
    return p


def golem(name):
    """Tamsos golemas: hulking dark-stone brute with glowing cracks."""
    p = root(name)
    p.location.z = -0.03 * abs(wave(1))  # heavy stomp
    p.rotation_euler = (0, 0.03 * wave(1), 0)
    bsdf = next(n for n in M["crack"].node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Emission Strength"].default_value = 1.6 + 1.0 * wave(1, 0.25)  # pulsing cracks
    for sx in (-1, 1):
        lift = 0.07 * max(0.0, sx * wave(1))  # legs lift alternately
        swing = -sx * 0.05 * wave(1)  # arms swing against the legs
        prim("cube", M["dark"], (sx * 0.22, 0, 0.22 + lift), (0.15, 0.17, 0.22), p, size=2)  # legs
        prim("cube", M["dark"], (sx * 0.52, -0.02 + swing, 0.66), (0.12, 0.14, 0.30), p, size=2)  # arms
        prim("uv_sphere", M["dark"], (sx * 0.52, -0.02, 0.98), (0.17, 0.17, 0.16), p, segments=16, ring_count=8)  # shoulders
        prim("cube", M["dark"], (sx * 0.52, -0.06 + swing, 0.30), (0.16, 0.16, 0.13), p, size=2)  # fists
    prim("cube", M["dark"], (0, 0, 0.78), (0.40, 0.26, 0.34), p, size=2)  # torso
    prim("cube", M["dark"], (0, -0.04, 1.22), (0.19, 0.17, 0.15), p, size=2)  # head
    prim("cube", M["crack"], (0.0, -0.265, 0.80), (0.012, 0.01, 0.26), p, size=2)  # chest cracks
    prim("cube", M["crack"], (0.10, -0.265, 0.86), (0.07, 0.01, 0.012), p, size=2).rotation_euler = (0, math.radians(-30), 0)
    prim("cube", M["crack"], (-0.12, -0.265, 0.70), (0.08, 0.01, 0.012), p, size=2).rotation_euler = (0, math.radians(25), 0)
    for sx in (-1, 1):
        prim("cube", M["crack"], (sx * 0.52, -0.16 - sx * 0.05 * wave(1), 0.66), (0.01, 0.012, 0.15), p, size=2)
    eyes(p, -0.215, 1.25, x=0.08, rx=0.05, rz=0.04)
    return p


def boss(name):
    """Tamsos seselis: giant shadow with horns, a tattered tail and extra eyes."""
    p = root(name)
    p.location.z = 0.04 + 0.04 * wave(1, 0.25)  # slow hover
    b = 0.03 * wave(1)  # looming pulse
    prim("uv_sphere", M["shadow"], (0, 0, 0.72), (0.58 * (1 + b), 0.52 * (1 + b), 0.52 * (1 - b)), p, segments=40, ring_count=20)
    prim("cone", M["shadow"], (0, 0, 0.30), (1, 1, 1), p, vertices=40, radius1=0.58, radius2=0.22, depth=0.60)
    for k, a in enumerate((-60, -20, 25, 65)):  # ragged hem of the tail
        t = math.radians(a)
        prim("cone", M["shadow"], (0.5 * math.sin(t) + 0.035 * wave(1, k * 0.25), -0.1 * math.cos(t), 0.08), (1, 1, 1), p, vertices=16, radius1=0.12, depth=0.3 + 0.05 * (k % 2)).rotation_euler = (math.pi, 0, 0)
    for sx in (-1, 1):
        h = prim("cone", M["dark"], (sx * 0.38, 0, 1.26), (1, 1, 1), p, vertices=16, radius1=0.10, depth=0.48)
        h.rotation_euler = (0, math.radians(28) * sx, 0)
        prim("uv_sphere", M["eye"], (sx * 0.20, -0.46, 0.84), (0.10, 0.05, 0.13 * blink()), p, segments=20, ring_count=10)
        prim("uv_sphere", M["eye"], (sx * 0.36, -0.38, 0.62), (0.045, 0.035, 0.06 * blink()), p, segments=12, ring_count=8)
    prim("uv_sphere", M["eye"], (0, -0.50, 1.02), (0.05, 0.035, 0.065 * blink()), p, segments=12, ring_count=8)  # third eye
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
    b = wave(1)  # breathing pulse, squashes and stretches around a fixed base
    prim("uv_sphere", M["shadow"], (0, 0, 0.32 + 0.021 * b), (0.34 * (1 - 0.04 * b), 0.34 * (1 - 0.04 * b), 0.30 * (1 + 0.07 * b)), p, segments=32, ring_count=16)
    prim("cone", M["shadow"], (0, 0, 0.12), (1, 1, 1), p, vertices=32, radius1=0.34, radius2=0.18, depth=0.24)
    eyes(p, -0.28, 0.38 + 0.03 * b)
    return p


SPRITES = {
    "candle_tea_1_full": lambda n: tea_candle(n, 0.40, 0.26),
    "candle_tea_2_mid": lambda n: tea_candle(n, 0.30, 0.15),
    "candle_tea_3_low": lambda n: tea_candle(n, 0.19, 0.06),
    "lantern_1_full": lambda n: lantern(n, 0.42),
    "lantern_2_mid": lambda n: lantern(n, 0.26),
    "lantern_3_low": lambda n: lantern(n, 0.10),
    "candle_long_1_full": lambda n: long_candle(n, 0.95),
    "candle_long_2_mid": lambda n: long_candle(n, 0.55),
    "candle_long_3_low": lambda n: long_candle(n, 0.22),
    "candelabra_1_full": lambda n: candelabra(n, 0.34, 0.46),
    "candelabra_2_mid": lambda n: candelabra(n, 0.20, 0.30),
    "candelabra_3_low": lambda n: candelabra(n, 0.08, 0.14),
    "candle_spark_1_full": lambda n: spark_candle(n, 0.50),
    "candle_spark_2_mid": lambda n: spark_candle(n, 0.30),
    "candle_spark_3_low": lambda n: spark_candle(n, 0.12),
    "enemy_moth": moth,
    "enemy_wind_spirit": wind_spirit,
    "enemy_wax_gnawer": wax_gnawer,
    "enemy_golem": golem,
    "enemy_boss": boss,
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

def clear_sprites():
    for o in list(sc.objects):
        if o.type in ('CAMERA', 'LIGHT'):
            continue
        d = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if isinstance(d, bpy.types.Mesh) and d.users == 0:
            bpy.data.meshes.remove(d)


# --- animation: rebuild each sprite at every loop phase and render the frames
if ANIM_MODE:
    clear_sprites()
    for k, build in SPRITES.items():
        if k == "wax_puddle" or (ONLY and not any(k.startswith(o) for o in ONLY)):
            continue
        for f in range(FRAMES):
            ANIM, PH = True, f / FRAMES
            build(k)
            sc.render.filepath = os.path.join(OUT, "Anim", k, f"{k}_{f:02d}.png")
            bpy.ops.render.render(write_still=True, scene=sc.name)
            clear_sprites()
        print("wrote", FRAMES, "frames for", k)
    ANIM, PH = False, 0.0
    roots = {k: f(k) for k, f in SPRITES.items()}

# --- render each sprite on its own
for k in ([] if ANIM_MODE else roots):
    if ONLY and not any(k.startswith(o) for o in ONLY):
        continue
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
    r.location = ((i - len(roots) // 2) * 1.8, 0, 0)
