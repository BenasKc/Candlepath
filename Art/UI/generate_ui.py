"""Generates the medieval build-panel art in Assets/Resources/UI/.

Run from the project root (needs numpy + Pillow):

    python3 Art/UI/generate_ui.py

Inputs:  Art/Source/polyhaven/old_planks_02_diff_1k.png  (CC0, Poly Haven)
Outputs: ui_wood, ui_frame, ui_parchment, ui_glow, ui_well, ui_stud, ui_divider, ui_flourish, ui_drop (.png)

Everything except the wood is drawn procedurally: hammered iron, rivets, aged parchment with
darkened edges, brass studs. Textures are authored at 2x and imported at 200 pixels per unit,
so they stay crisp on large screens. The 9-slice borders are set in CandlePanel.cs (UiArt).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Art", "Source", "polyhaven", "old_planks_02_diff_1k.png")
OUT = os.path.join(ROOT, "Assets", "Resources", "UI")

rng = np.random.default_rng(7)


# ---------------------------------------------------------------- helpers

def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def save(name, rgb, alpha=None):
    rgb = np.clip(rgb, 0, 1)
    if alpha is None:
        img = Image.fromarray((rgb * 255 + 0.5).astype(np.uint8), "RGB")
    else:
        a = np.clip(alpha, 0, 1)
        img = Image.fromarray((np.dstack([rgb, a]) * 255 + 0.5).astype(np.uint8), "RGBA")
    img.save(os.path.join(OUT, name + ".png"))
    print("wrote", name, img.size)


def value_noise(h, w, scale, seed):
    """Smooth noise in [0,1]: random grid upsampled bicubically."""
    r = np.random.default_rng(seed)
    gh, gw = max(2, int(h / scale) + 2), max(2, int(w / scale) + 2)
    grid = (r.random((gh, gw)) * 255).astype(np.uint8)
    img = Image.fromarray(grid, "L").resize((int(gw * scale), int(gh * scale)), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32)[:h, :w] / 255.0


def fbm(h, w, base, octaves, seed):
    total, amp, norm = np.zeros((h, w), np.float32), 1.0, 0.0
    for o in range(octaves):
        total += amp * value_noise(h, w, base / (2 ** o), seed + o * 31)
        norm += amp
        amp *= 0.5
    return total / norm


def grid(h, w):
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    return x + 0.5, y + 0.5


def rounded_box_sdf(x, y, cx, cy, hx, hy, r):
    qx = np.abs(x - cx) - hx + r
    qy = np.abs(y - cy) - hy + r
    outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    return outside + np.minimum(np.maximum(qx, qy), 0) - r


def smoothstep(a, b, v):
    t = np.clip((v - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def blur(arr, radius):
    img = Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), "L")
    return np.asarray(img.filter(ImageFilter.GaussianBlur(radius)), np.float32) / 255.0


def metal_shade(nx, ny, base, light=(-0.55, -0.65), spec=0.55, shininess=18):
    """Simple lambert + spec shading from a 2D normal (nx, ny); z is derived."""
    nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
    lx, ly = light
    lz = np.sqrt(max(0.0, 1 - lx * lx - ly * ly))
    lam = np.clip(nx * lx + ny * ly + nz * lz, 0, 1)
    hx, hy, hz = lx, ly, lz + 1
    hn = np.sqrt(hx * hx + hy * hy + hz * hz)
    s = np.clip((nx * hx + ny * hy + nz * hz) / hn, 0, 1) ** shininess
    return base * (0.35 + 0.75 * lam[..., None]) + spec * s[..., None]


def dome(x, y, cx, cy, r):
    """Normal + mask for a hemispherical rivet/stud."""
    dx, dy = (x - cx) / r, (y - cy) / r
    d = np.hypot(dx, dy)
    mask = np.clip((1 - d) * r + 0.5, 0, 1)
    k = np.clip(d, 0, 0.999)
    return dx * (k > 0) * 0.9, dy * (k > 0) * 0.9, mask


# ---------------------------------------------------------------- wood (tiled)

def make_wood():
    src = Image.open(SRC).convert("RGB").resize((512, 512), Image.LANCZOS)
    a = np.asarray(src, np.float32) / 255.0
    lum = a @ np.array([0.3, 0.55, 0.15])
    lum = (lum - lum.mean()) / (lum.std() + 1e-6)          # normalized grain
    t = np.clip(0.5 + lum * 0.2, 0, 1)[..., None]
    dark, mid, light = hexc("140d09"), hexc("3a2619"), hexc("6a4a32")
    rgb = np.where(t < 0.5, dark + (mid - dark) * (t / 0.5), mid + (light - mid) * ((t - 0.5) / 0.5))
    rgb *= 0.92
    save("ui_wood", rgb)


# ---------------------------------------------------------------- iron frame (9-sliced overlay)

FRAME = 256          # texture size
BAND = 18            # iron band width (texels)
BRACKET = 72         # corner bracket arm length
BRACKET_W = 28       # corner bracket width


def iron_base(h, w, seed):
    n = fbm(h, w, 24, 4, seed)
    pits = (value_noise(h, w, 3, seed + 5) > 0.82).astype(np.float32) * 0.08
    base = hexc("2c2a2e")[None, None, :] * (0.8 + 0.4 * n[..., None]) - pits[..., None]
    return base, n


def make_frame():
    s = FRAME
    x, y = grid(s, s)
    alpha = np.zeros((s, s), np.float32)
    rgb = np.zeros((s, s, 3), np.float32)

    # Outer silhouette (slightly rounded) and the band
    outer = rounded_box_sdf(x, y, s / 2, s / 2, s / 2 - 1, s / 2 - 1, 10)
    inner = rounded_box_sdf(x, y, s / 2, s / 2, s / 2 - BAND, s / 2 - BAND, 4)
    band = np.clip(0.5 - outer, 0, 1) * np.clip(inner + 0.5, 0, 1)

    # Corner brackets: L-shaped plates sitting on the band
    def l_plate(cx, cy, sx, sy):
        # sx, sy = +-1: which direction the arms run from the corner
        ax = (x - cx) * sx
        ay = (y - cy) * sy
        horiz = (ax >= 0) & (ax <= BRACKET) & (ay >= 0) & (ay <= BRACKET_W)
        vert = (ay >= 0) & (ay <= BRACKET) & (ax >= 0) & (ax <= BRACKET_W)
        # chamfer the arm tips
        tip_h = ax - BRACKET + (BRACKET_W - ay) * 0.5
        tip_v = ay - BRACKET + (BRACKET_W - ax) * 0.5
        m = (horiz & (tip_h < 0)) | (vert & (tip_v < 0))
        return m.astype(np.float32)

    plates = np.zeros((s, s), np.float32)
    for cx, cy, sx, sy in [(4, 4, 1, 1), (s - 4, 4, -1, 1), (4, s - 4, 1, -1), (s - 4, s - 4, -1, -1)]:
        plates = np.maximum(plates, l_plate(cx, cy, sx, sy))
    plates = blur(plates, 0.7)

    base, n = iron_base(s, s, 11)

    # Bevel from distance to the band edges: light top-left, dark bottom-right
    edge_out = np.clip(-outer / 5, 0, 1)
    edge_in = np.clip(inner / 5, 0, 1)
    bevel = (1 - edge_out) * 0.25 - (1 - edge_in) * 0.18
    ny = np.gradient(n * 6, axis=0) * 0.6
    nx = np.gradient(n * 6, axis=1) * 0.6
    band_rgb = metal_shade(nx, ny, base, spec=0.18, shininess=10) + bevel[..., None]

    # Plates: a touch lighter with their own bevel
    pg_y, pg_x = np.gradient(blur(plates, 2.0))
    plate_rgb = metal_shade(-pg_x * 3 + nx, -pg_y * 3 + ny, base * 1.15, spec=0.35, shininess=14)

    rgb = band_rgb * (1 - plates[..., None]) + plate_rgb * plates[..., None]
    alpha = np.maximum(band, plates * np.clip(0.5 - outer, 0, 1))

    # Rivets: two per plate arm, plus one in each corner
    rivets = []
    for cx, cy, sx, sy in [(4, 4, 1, 1), (s - 4, 4, -1, 1), (4, s - 4, 1, -1), (s - 4, s - 4, -1, -1)]:
        c = BRACKET_W / 2
        rivets += [(cx + sx * c, cy + sy * c), (cx + sx * (BRACKET - 14), cy + sy * c), (cx + sx * c, cy + sy * (BRACKET - 14))]
    for (rx, ry) in rivets:
        dnx, dny, m = dome(x, y, rx, ry, 6.0)
        shade = metal_shade(dnx, dny, hexc("3a383c")[None, None, :] * np.ones_like(rgb), spec=0.8, shininess=24)
        # a small dark ring where the rivet meets the plate
        ring = np.clip(1 - np.abs(np.hypot(x - rx, y - ry) - 6.6) / 1.2, 0, 1) * 0.5
        rgb = rgb * (1 - m[..., None]) + shade * m[..., None]
        rgb *= (1 - ring[..., None] * (1 - m[..., None]))

    # Inner shadow cast onto the wood just inside the band
    inside = np.clip(-inner, 0, None)
    shadow = np.exp(-inside / 9.0) * (inner < 0) * 0.65
    shadow = np.maximum(shadow, 0)
    total_alpha = np.maximum(alpha, shadow * (1 - alpha))
    rgb = rgb * (alpha / np.maximum(total_alpha, 1e-6))[..., None]   # shadow pixels are black
    save("ui_frame", rgb, total_alpha)


# ---------------------------------------------------------------- parchment slip (9-sliced)

def make_parchment():
    w, h = 608, 200
    x, y = grid(h, w)
    big = fbm(h, w, 140, 4, 21)
    fine = fbm(h, w, 6, 2, 22)
    fibre = value_noise(h, w * 4, 3, 23)[:, ::4]   # streaky horizontal fibres
    base = hexc("dcc597")
    shade = 0.86 + 0.16 * big + 0.05 * (fine - 0.5) + 0.03 * (fibre - 0.5)
    rgb = base[None, None, :] * shade[..., None]

    # Faint stains
    for i in range(5):
        cx, cy = rng.uniform(40, w - 40), rng.uniform(20, h - 20)
        r = rng.uniform(18, 46)
        d = np.hypot(x - cx, (y - cy) * 1.3) / r
        stain = np.clip(1 - d, 0, 1) ** 2 * rng.uniform(0.04, 0.09)
        rgb *= (1 - stain[..., None] * np.array([0.6, 0.85, 1.2]))

    # Ragged edge + aged, darkened border
    sdf = rounded_box_sdf(x, y, w / 2, h / 2, w / 2 - 6, h / 2 - 6, 8)
    rag = (fbm(h, w, 10, 3, 24) - 0.5) * 7
    edge = sdf + rag
    alpha = np.clip(0.5 - edge, 0, 1)
    burn = smoothstep(-34, 0, edge)                   # 0 inside .. 1 at the edge
    burnt = hexc("6e4524")
    rgb = rgb * (1 - 0.55 * burn[..., None]) + burnt * (0.35 * burn ** 2)[..., None]
    # thin dark rim right at the tear
    rim = np.clip(1 - np.abs(edge + 1.5) / 2.0, 0, 1) * 0.35
    rgb *= (1 - rim[..., None])
    save("ui_parchment", rgb, alpha)


# ---------------------------------------------------------------- glow (9-sliced, tinted in Unity)

def make_glow():
    s = 160
    x, y = grid(s, s)
    sdf = rounded_box_sdf(x, y, s / 2, s / 2, s / 2 - 40, s / 2 - 40, 10)
    # soft falloff outside the card's edge, fading to nothing well before the texture border
    a = np.where(sdf > 0, np.exp(-(sdf / 11.0) ** 2), 0.0)
    a *= np.clip((s / 2 - 3 - np.maximum(np.abs(x - s / 2), np.abs(y - s / 2))) / 6, 0, 1)
    save("ui_glow", np.ones((s, s, 3)), a)


# ---------------------------------------------------------------- icon well (iron ring with dark recess)

def make_well():
    s = 160
    x, y = grid(s, s)
    c = s / 2
    d = np.hypot(x - c, y - c)
    R, r_in = 76.0, 67.0
    mask = np.clip(R + 0.5 - d, 0, 1)

    # recess: dark, slightly warm, with an inner shadow from the top
    rec_t = np.clip(d / r_in, 0, 1)
    recess = hexc("1a120d") * (1 - 0.55 * rec_t[..., None] ** 2)
    top_shadow = np.clip((r_in - d) / 18, 0, 1) * np.clip(1 - (y - (c - r_in)) / (r_in * 0.9), 0, 1) * 0.6
    recess = recess * (1 - top_shadow[..., None])

    # ring: hammered iron with a rounded profile
    base, n = iron_base(s, s, 31)
    t = np.clip((d - r_in) / (R - r_in), 0, 1)
    prof = (t - 0.5) * 2               # -1 inner edge .. +1 outer edge
    nx = (x - c) / np.maximum(d, 1e-3) * prof * 0.75 + np.gradient(n, axis=1) * 2
    ny = (y - c) / np.maximum(d, 1e-3) * prof * 0.75 + np.gradient(n, axis=0) * 2
    ring_rgb = metal_shade(nx, ny, base * 1.05, spec=0.35, shininess=14)
    ring_m = np.clip(d - r_in + 0.5, 0, 1)

    rgb = recess * (1 - ring_m[..., None]) + ring_rgb * ring_m[..., None]

    # four rivets on the ring
    for ang in (45, 135, 225, 315):
        a = np.radians(ang)
        rx, ry = c + np.cos(a) * (r_in + R) / 2, c + np.sin(a) * (r_in + R) / 2
        dnx, dny, m = dome(x, y, rx, ry, 4.5)
        sh = metal_shade(dnx, dny, hexc("3c3a3e")[None, None, :] * np.ones_like(rgb), spec=0.8, shininess=24)
        rgb = rgb * (1 - m[..., None]) + sh * m[..., None]

    save("ui_well", rgb, mask)


# ---------------------------------------------------------------- brass stud (hotkey badge)

def make_stud():
    s = 64
    x, y = grid(s, s)
    c = s / 2
    R = 29.0
    d = np.hypot(x - c, y - c)
    mask = np.clip(R + 0.5 - d, 0, 1)
    # flat face with a rounded rim
    t = np.clip((d - (R - 7)) / 7, 0, 1)
    nx = (x - c) / np.maximum(d, 1e-3) * t * 0.85
    ny = (y - c) / np.maximum(d, 1e-3) * t * 0.85
    n = fbm(s, s, 8, 2, 41)
    brass = hexc("a9823f") * (0.9 + 0.2 * n[..., None])
    rgb = metal_shade(nx, ny, brass, spec=0.45, shininess=16)
    # dark outer edge so it reads on parchment and wood
    rim = np.clip(1 - np.abs(d - (R - 0.8)) / 1.6, 0, 1)
    rgb *= (1 - 0.6 * rim[..., None])
    save("ui_stud", rgb, mask)


# ---------------------------------------------------------------- ornaments

def brass_shape(mask, seed):
    h, w = mask.shape
    m = blur(mask, 0.6)
    gy, gx = np.gradient(blur(mask, 1.5))
    n = fbm(h, w, 6, 2, seed)
    base = hexc("9c7a41") * (0.9 + 0.2 * n[..., None])
    rgb = metal_shade(-gx * 2.5, -gy * 2.5, base, spec=0.35, shininess=12)
    return rgb, m


def make_divider():
    w, h = 608, 28
    x, y = grid(h, w)
    c = w / 2
    # bar thins toward the ends
    half = 2.2 * (1 - np.abs(x - c) / c) ** 0.6 + 0.6
    bar = (np.abs(y - h / 2) < half) & (np.abs(x - c) < c - 4)
    diamond = (np.abs(x - c) / 13 + np.abs(y - h / 2) / 10) < 1
    hole = (np.abs(x - c) / 5 + np.abs(y - h / 2) / 4) < 1
    dots = np.zeros_like(x, bool)
    for off in (26, 40):
        for sx in (-1, 1):
            dots |= np.hypot(x - (c + sx * off), y - h / 2) < (3.4 if off == 26 else 2.4)
    mask = ((bar | diamond | dots) & ~hole).astype(np.float32)
    rgb, a = brass_shape(mask, 51)
    save("ui_divider", rgb, a)


def make_flourish():
    """Short line ending in a small diamond, used either side of section titles (mirrored)."""
    w, h = 120, 20
    x, y = grid(h, w)
    line = (np.abs(y - h / 2) < 1.3 * (x / w) + 0.4) & (x < w - 12)
    diamond = (np.abs(x - (w - 9)) / 7 + np.abs(y - h / 2) / 6) < 1
    mask = (line | diamond).astype(np.float32)
    rgb, a = brass_shape(mask, 61)
    save("ui_flourish", rgb, a)


def make_drop():
    w, h = 40, 54
    x, y = grid(h, w)
    cx, cy, r = w / 2, h - 18.0, 15.0
    d = np.hypot(x - cx, y - cy)
    circle = r - d
    tip_y = 3.0
    t = np.clip((cy - y) / (cy - tip_y), 0, 1)
    half = r * 0.9 * (1 - t) ** 1.1
    tip = np.where((y < cy) & (y > tip_y), half - np.abs(x - cx), -9)
    inside = np.maximum(circle, tip)
    mask = np.clip(inside + 0.5, 0, 1)
    # rounded shading: normal from the blurred mask
    gy, gx = np.gradient(blur(mask, 3.0))
    wax = hexc("efe3c2")
    rgb = metal_shade(-gx * 4, -gy * 4, wax[None, None, :] * np.ones((h, w, 3)), spec=0.6, shininess=20)
    rim = np.clip(1 - np.abs(inside - 0.8) / 1.4, 0, 1) * 0.55
    rgb *= (1 - rim[..., None])
    save("ui_drop", rgb, mask)


if __name__ == "__main__":
    if not os.path.exists(SRC):
        sys.exit(f"missing source texture: {SRC}")
    os.makedirs(OUT, exist_ok=True)
    make_wood()
    make_frame()
    make_parchment()
    make_glow()
    make_well()
    make_stud()
    make_divider()
    make_flourish()
    make_drop()
