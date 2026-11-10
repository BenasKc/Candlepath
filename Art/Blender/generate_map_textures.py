"""Seamless floor and path tiles for the Library (Biblioteka) and Chapel (Koplyčia) maps.

Grades Poly Haven (CC0) diffuse textures into a dark-to-light ramp per map, like generate_environment.py does
for the Rusys cellar, and writes four 512x512 tiles:
  floor_library.png  path_library.png   (dark wooden planks / warm wooden floor)
  floor_chapel.png   path_chapel.png    (dark irregular stone / pale checkered marble)

Sources: Art/Source/polyhaven/*_diff_1k.png (downloaded through the Blender MCP, see README.md there).

Usage (needs Blender for its bundled numpy):
    blender -b -P Art/Blender/generate_map_textures.py -- Assets/Environment
"""
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "Source", "polyhaven")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else "Assets/Environment")
os.makedirs(OUT, exist_ok=True)
TILE = 512


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32) / 255


def load_scaled(name, size):
    im = bpy.data.images.load(os.path.join(SRC, name))
    im.scale(size, size)
    a = np.empty(size * size * 4, np.float32)
    im.pixels.foreach_get(a)
    return a.reshape(size, size, 4)[::-1, :, :3].copy()     # Blender rows run bottom-to-top


def grade(tex, dark, light, lo=2, hi=98):
    """Map the texture's luminance onto a dark->light ramp, keeping its detail (per pixel, so it stays seamless)."""
    lum = tex @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    a, b = np.percentile(lum, [lo, hi])
    t = np.clip((lum - a) / (b - a + 1e-6), 0, 1)[..., None]
    return dark * (1 - t) + light * t


def save_png(arr, name):
    h, w, _ = arr.shape
    im = bpy.data.images.new(name, w, h, alpha=True)
    im.colorspace_settings.name = 'sRGB'
    rgba = np.dstack([arr, np.ones((h, w), np.float32)])[::-1]
    im.pixels.foreach_set(rgba.astype(np.float32).ravel())
    im.filepath_raw = os.path.join(OUT, name + ".png")
    im.file_format = 'PNG'
    im.save()
    print("wrote", im.filepath_raw)


# name -> (source, dark end, light end). Palette (design doc, fig. 8) pushed slightly warm for the Library
# (old wood) and slightly cold for the Chapel (stone), so each map has its own mood.
TILES = {
    "floor_library": ("dark_wooden_planks_diff_1k.png", rgb("#130F15"), rgb("#4A3A42")),
    "path_library": ("wooden_floor_02_diff_1k.png", rgb("#3B3038"), rgb("#9C8789")),
    "floor_chapel": ("stone_tiles_diff_1k.png", rgb("#0E0F18"), rgb("#3A4058")),
    "path_chapel": ("floor_tiles_06_diff_1k.png", rgb("#34324A"), rgb("#B4B1C8")),
}

for name, (src, dark, light) in TILES.items():
    tex = load_scaled(src, TILE)
    save_png(np.clip(grade(tex, dark, light), 0, 1), name)
