"""Map-select card images for Biblioteka and Koplyčia (like Assets/Environment/rusys_background.png).

Composites the floor and path tiles and the decoration sprites of a generated map scene into a 1920x1080 picture at
normal brightness (darkness and candle light are the game's job). Run after Candlepath > Build Maps has written the
scenes, then run Build Maps once more so MapDefinition.Preview picks the images up.

    python3 Art/make_map_previews.py            (needs Pillow)
"""
import glob
import os
import re

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
ENV = os.path.join(ROOT, "Assets", "Environment")
W, H = 1920, 1080
CELL = 96                 # px per grid cell; 20 x 10 cells = 1920 x 960, centred vertically
CELL_W = 0.98             # grid cell width in world units (cell height is 1)
SPRITE_PX, SPRITE_PPU = 256, 107

MAPS = {
    "Map_2": ("floor_library", "path_library", "biblioteka_background"),
    "Map_3": ("floor_chapel", "path_chapel", "koplycia_background"),
}


def docs(text):
    return re.split(r"(?m)^--- ", text)[1:]


def render(scene, floor, path, out):
    text = open(os.path.join(ROOT, "Assets", "Maps", scene + ".unity")).read()
    ds = docs(text)

    tilemap = next(d for d in ds if d.startswith("!u!1839735485 &1900117753"))
    cells = {(int(a), int(b)) for a, b in re.findall(r"first: \{x: (-?\d+), y: (-?\d+)", tilemap)}

    f = Image.open(os.path.join(ENV, floor + ".png")).convert("RGBA").resize((CELL, CELL), Image.LANCZOS)
    p = Image.open(os.path.join(ENV, path + ".png")).convert("RGBA").resize((CELL, CELL), Image.LANCZOS)
    img = Image.new("RGBA", (W, H))
    for x in range(0, W, CELL):                       # floor over the whole canvas, including the margins
        for y in range(-CELL, H, CELL):
            img.paste(f, (x, y + (H - 10 * CELL) // 2 % CELL))
    top = (H - 10 * CELL) // 2
    for (cx, cy) in cells:
        img.paste(p, ((cx + 10) * CELL, top + (4 - cy) * CELL))

    guid_to_png = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets", "Decor", "*.png.meta")):
        guid_to_png[re.search(r"guid: (\w+)", open(meta).read()).group(1)] = meta[:-5]

    pos = {}
    for d in ds:
        if d.startswith("!u!4 "):
            go = re.search(r"m_GameObject: \{fileID: (\d+)", d).group(1)
            m = re.search(r"m_LocalPosition: \{x: (-?[\d.e-]+), y: (-?[\d.e-]+)", d)
            pos[go] = (float(m.group(1)), float(m.group(2)))

    sprites = []
    for d in ds:
        if d.startswith("!u!212 "):
            g = re.search(r"m_Sprite: \{fileID: \d+, guid: (\w+)", d)
            if g and g.group(1) in guid_to_png:
                go = re.search(r"m_GameObject: \{fileID: (\d+)", d).group(1)
                order = int(re.search(r"m_SortingOrder: (-?\d+)", d).group(1))
                sprites.append((order, guid_to_png[g.group(1)], pos[go]))

    ppu_x, ppu_y = CELL / CELL_W, CELL
    size = SPRITE_PX / SPRITE_PPU
    for _, png, (wx, wy) in sorted(sprites, key=lambda s: s[0]):
        im = Image.open(png).convert("RGBA").resize((round(size * ppu_x), round(size * ppu_y)), Image.LANCZOS)
        cx, cy = W / 2 + wx * ppu_x, top + (5 - wy) * ppu_y
        img.alpha_composite(im, (round(cx - im.width / 2), round(cy - im.height / 2)))

    img.convert("RGB").save(os.path.join(ENV, out + ".png"))
    print("wrote", out + ".png", len(sprites), "props")


for scene, (fl, pa, out) in MAPS.items():
    render(scene, fl, pa, out)
