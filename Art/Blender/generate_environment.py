"""Candlepath PoC environment generator (Rusys / cellar map).

Grades two CC0 Poly Haven textures into the game palette (design doc, 8 pav.) and assembles:
  - floor_cellar.png       seamless dark flagstone floor tile (512x512)
  - path_stone.png         seamless lighter cobblestone path tile (512x512)
  - rusys_background.png   full 1920x1080 map background: floor + winding path
  - rusys_path.json        path waypoints (grid cells and pixel centres) for the path/enemy code

Sources (CC0, see Art/Source/polyhaven/README.md):
  rock_tile_floor  (Charlotte Baglioni)  -> floor
  stone_floor      (Dimitrios Savva)     -> path

The background is drawn at normal brightness; darkness and candle light are the game's job.

Usage (needs Blender for its bundled numpy):
    blender -b -P Art/Blender/generate_environment.py -- Assets/Environment
"""
import json
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "Source", "polyhaven")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else "Assets/Environment")
os.makedirs(OUT, exist_ok=True)

TILE = 512                 # exported tile size (px)
CELL = 120                 # map grid cell (px); 16 x 9 cells = 1920 x 1080
GRID_W, GRID_H = 16, 9
FLOOR_PX = 480             # size one floor texture repeat takes on the map
PATH_PX = 600              # size one path texture repeat takes on the map

# Path as grid corners (col, row); consecutive corners are joined by straight segments.
# Kept inside columns 0-12 so the right-hand tower panel (design doc, fig. 4) never covers it,
# and inside rows 1-7 so the top and bottom bars do not either.
PATH_CORNERS = [(0, 2), (3, 2), (3, 6), (7, 6), (7, 2), (11, 2), (11, 6), (12, 6)]


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32) / 255


TAMSA, AKMUO, SESELIAI = rgb("#0E0C16"), rgb("#2E2B3A"), rgb("#4A2F6B")
PATH_LIGHT = rgb("#7A7592")  # lighter stone so the path reads against the floor


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def blur(a, sigma):
    """Gaussian blur through the FFT (wraps around the borders)."""
    h, w = a.shape
    fy, fx = np.meshgrid(np.fft.fftfreq(h), np.fft.fftfreq(w), indexing="ij")
    k = np.exp(-2 * (np.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * k))


def load_scaled(name, size):
    im = bpy.data.images.load(os.path.join(SRC, name))
    im.scale(size, size)
    a = np.empty(size * size * 4, np.float32)
    im.pixels.foreach_get(a)
    return a.reshape(size, size, 4)[::-1, :, :3].copy()     # Blender rows run bottom-to-top


def grade(tex, dark, light, lo=2, hi=98):
    """Map the texture's luminance onto a dark->light palette ramp, keeping its detail."""
    lum = tex @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    a, b = np.percentile(lum, [lo, hi])
    t = np.clip((lum - a) / (b - a + 1e-6), 0, 1)[..., None]
    return dark * (1 - t) + light * t


def path_cells():
    cells = []
    for (c0, r0), (c1, r1) in zip(PATH_CORNERS, PATH_CORNERS[1:]):
        for c in range(min(c0, c1), max(c0, c1) + 1):
            for r in range(min(r0, r1), max(r0, r1) + 1):
                if (c, r) not in cells:
                    cells.append((c, r))
    return cells


def tile_over(tile, W, H):
    reps_y, reps_x = -(-H // tile.shape[0]), -(-W // tile.shape[1])
    return np.tile(tile, (reps_y, reps_x, 1))[:H, :W]


def build_background(floor_tile, path_tile):
    W, H = GRID_W * CELL, GRID_H * CELL
    floor = tile_over(floor_tile, W, H)
    path = tile_over(path_tile, W, H)
    mask = np.zeros((H, W), np.float32)
    for c, r in path_cells():
        mask[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL] = 1
    mask = blur(mask, 1.2)                                  # slightly soften the cell edges
    img = floor * (1 - mask[..., None]) + path * mask[..., None]
    # darken the path's inner rim and cast a soft shadow onto the surrounding floor
    inner = blur(1 - mask, 5) * mask
    img *= (1 - 0.5 * np.clip(inner * 1.6, 0, 1))[..., None]
    shadow = blur(mask, 10) * (1 - mask)
    img *= (1 - 0.6 * np.clip(shadow * 1.4, 0, 1))[..., None]
    # faint violet rim light along the path edge (shadow-creature palette)
    rim = np.clip(blur(1 - mask, 2.5) * mask - 0.1, 0, 1)
    img = img + SESELIAI * 0.2 * rim[..., None]
    # soft vignette towards the map border
    yy, xx = np.mgrid[0:H, 0:W]
    v = np.maximum(np.abs(xx / W - 0.5), np.abs(yy / H - 0.5)) * 2
    img *= (1 - 0.35 * smoothstep(0.55, 1.0, v))[..., None]
    return np.clip(img, 0, 1)


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


floor_src = load_scaled("rock_tile_floor_diff_1k.png", 1024)
path_src = load_scaled("stone_floor_diff_1k.png", 1024)

# graded, seamless tiles (the sources tile seamlessly; grading is per-pixel so that is preserved)
floor_full = grade(floor_src, TAMSA * 1.4, AKMUO * 1.35)
path_full = grade(path_src, AKMUO * 1.2, PATH_LIGHT)


def resized(arr, size):
    im = bpy.data.images.new("tmp", arr.shape[1], arr.shape[0], alpha=True)
    rgba = np.dstack([arr, np.ones(arr.shape[:2], np.float32)])[::-1]
    im.pixels.foreach_set(rgba.astype(np.float32).ravel())
    im.scale(size, size)
    a = np.empty(size * size * 4, np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(size, size, 4)[::-1, :, :3].copy()


save_png(resized(floor_full, TILE), "floor_cellar")
save_png(resized(path_full, TILE), "path_stone")
save_png(build_background(resized(floor_full, FLOOR_PX), resized(path_full, PATH_PX)), "rusys_background")

with open(os.path.join(OUT, "rusys_path.json"), "w") as f:
    json.dump({
        "map_size_px": [GRID_W * CELL, GRID_H * CELL],
        "cell_px": CELL,
        "origin": "top-left, y down (convert for Unity: y_unity = height - y)",
        "corner_cells": [list(c) for c in PATH_CORNERS],
        "waypoints_px": [[c * CELL + CELL // 2, r * CELL + CELL // 2] for c, r in PATH_CORNERS],
        "path_cells": [list(c) for c in path_cells()],
        "enemy_start_cell": list(PATH_CORNERS[0]),
        "eternal_lamp_cell": list(PATH_CORNERS[-1]),
    }, f, indent=2)
print("wrote path json")
