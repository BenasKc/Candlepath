"""Preview the MapBuilder decor layouts without Unity, before running Candlepath > Build Maps / Decorate Rusys.

Composites the floor and path tiles and the decoration sprites like Art/make_map_previews.py, but reads the props,
path nodes and lift constants straight from Assets/Editor/MapBuilder.cs (Rusys' path from Map_1's Path tilemap).
Prints props that would be skipped (on the path / off the map) or that overlap another prop.

    python3 Art/preview_layouts.py [out dir]        (needs Pillow; writes biblioteka.png, koplycia.png, rusys.png)
"""
import math
import os
import re
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = sys.argv[1] if len(sys.argv) > 1 else "."
ENV = os.path.join(ROOT, "Assets", "Environment")
W, H, CELL, CELL_W = 1920, 1080, 96, 0.98
SPRITE_PX, SPRITE_PPU = 256, 107
TOP = (H - 10 * CELL) // 2

src = open(os.path.join(ROOT, "Assets", "Editor", "MapBuilder.cs")).read()


def lift(v):
    if not v:
        return 0.0
    if v[0].isalpha():   # named constant, e.g. TableTop = 0.55f
        v = re.search(v + r"\s*=\s*(-?[\d.]+)f", src).group(1)
    return float(v.rstrip("f"))


def props_in(text):
    out = []
    for m in re.finditer(r'P\("(\w+)",\s*(-?\d+),\s*(-?\d+)(?:,\s*(\d+))?(?:,\s*(-?[\d.]+f|TableTop|AltarTop))?\)', text):
        out.append((m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4) or 1), lift(m.group(5))))
    return out


def nodes_in(text):
    nodes = {}
    for m in re.finditer(r'N\("(\w+)",\s*(-?\d+),\s*(-?\d+)((?:,\s*"\w+")*)\)', text):
        nodes[m.group(1)] = ((int(m.group(2)), int(m.group(3))), re.findall(r'"(\w+)"', m.group(4)))
    return nodes


def seg_dist(p, a, b):
    abx, aby = b[0] - a[0], b[1] - a[1]
    l2 = abx * abx + aby * aby
    t = 0 if l2 < 1e-6 else max(0, min(1, ((p[0] - a[0]) * abx + (p[1] - a[1]) * aby) / l2))
    return math.hypot(p[0] - a[0] - abx * t, p[1] - a[1] - aby * t)


def path_cells(nodes):
    cells = set()
    for (a, nxt) in nodes.values():
        for n in nxt:
            b = nodes[n][0]
            for x in range(min(a[0], b[0]) - 1, max(a[0], b[0]) + 2):
                for y in range(min(a[1], b[1]) - 1, max(a[1], b[1]) + 2):
                    if seg_dist((x, y), a, b) <= 0.72:
                        cells.add((x, y))
    return cells


def rusys_path_cells():
    text = open(os.path.join(ROOT, "Assets", "Maps", "Map_1.unity")).read()
    docs = re.split(r"(?m)^--- ", text)
    go = next(re.search(r"&(\d+)", d).group(1) for d in docs if d.startswith("!u!1 ") and re.search(r"m_Name: Path\n", d))
    tm = next(d for d in docs if d.startswith("!u!1839735485 ") and f"m_GameObject: {{fileID: {go}}}" in d)
    return {(int(a), int(b)) for a, b in re.findall(r"first: \{x: (-?\d+), y: (-?\d+)", tm)}


def sprite(name):
    return os.path.join(ROOT, "Assets", "Decor", name + ".png")


def render(name, props, cells, floor=None, path=None, lamp=None):
    f = Image.open(os.path.join(ENV, floor + ".png")).convert("RGBA").resize((CELL, CELL), Image.LANCZOS)
    p = Image.open(os.path.join(ENV, path + ".png")).convert("RGBA").resize((CELL, CELL), Image.LANCZOS)
    img = Image.new("RGBA", (W, H))
    for x in range(0, W, CELL):
        for y in range(-CELL, H, CELL):
            img.paste(f, (x, y + TOP % CELL))
    for (cx, cy) in cells:
        img.paste(p, ((cx + 10) * CELL, TOP + (4 - cy) * CELL))

    problems, used = [], {}
    ppu_x, ppu_y = CELL / CELL_W, CELL
    size = SPRITE_PX / SPRITE_PPU
    drawn = []
    for i, (spr, x, y, w, lift) in enumerate(props):
        cs = [(x + k, y) for k in range(w)]
        if any(c in cells or not (-10 <= c[0] <= 9 and -5 <= c[1] <= 4) for c in cs):
            problems.append(f"{spr} at ({x},{y}) on path/off map: skipped in game")
            continue
        if lift == 0:
            for c in cs:
                if c in used:
                    problems.append(f"{spr} at ({x},{y}) overlaps {used[c]}")
                used[c] = spr
        drawn.append((-y * 4 + (1 if lift > 0 else 0), i, spr, x, y, w, lift))
    if lamp:
        drawn.append((-lamp[1] * 4, 999, "__lamp", lamp[0], lamp[1], 1, 0))
    for order, _, spr, x, y, w, lift in sorted(drawn):
        png = os.path.join(ROOT, "Assets", "Sprites", "eternal_lamp 1.png") if spr == "__lamp" else sprite(spr)
        im = Image.open(png).convert("RGBA").resize((round(size * ppu_x), round(size * ppu_y)), Image.LANCZOS)
        wx = ((x + (x + w - 1)) / 2 + 0.5) * CELL_W
        wy = y + 0.5 + lift
        cx, cy = W / 2 + wx * ppu_x, TOP + (5 - wy) * ppu_y
        img.alpha_composite(im, (round(cx - im.width / 2), round(cy - im.height / 2)))
    out = os.path.join(OUT, name + ".png")
    img.convert("RGB").save(out)
    print("wrote", out, len(drawn), "props")
    for p in problems:
        print("  !", p)


lib = src[src.index("static readonly MapLayout Library"):src.index("static readonly MapLayout Chapel")]
cha = src[src.index("static readonly MapLayout Chapel"):src.index("static readonly Prop[] RusysDecor")]
rus = src[src.index("static readonly Prop[] RusysDecor"):src.index("[MenuItem(\"Candlepath/Build Maps\")]")]

ln, cn = nodes_in(lib), nodes_in(cha)
render("biblioteka", props_in(lib.split("Nodes")[0]), path_cells(ln), "floor_library", "path_library", lamp=ln["Lamp"][0])
render("koplycia", props_in(cha.split("Nodes")[0]), path_cells(cn), "floor_chapel", "path_chapel", lamp=cn["Lamp"][0])
render("rusys", props_in(rus), rusys_path_cells(), "floor_cellar", "path_stone")
