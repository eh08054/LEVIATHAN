"""Block observation scenes (2026-11-26): pictures, walkable areas and occluders for the eleven districts.

    python Tools/Art/blocks_data.py            # writes Tools/Art/proto/blocks.js and installs into Unity
    python Tools/Art/blocks_data.py --preview  # also writes painted/pixel_parts/blocks_preview/<key>.png

One source for the browser prototype (Tools/Art/proto/observe.html) and the game (SimPanel). All numbers are pixels
of the 1536 x 1024 block pictures; rectangles are [x0, y0, x1, y1].
  walk   areas people may stand in          solid  areas inside them nobody crosses (water, trees, kiosks)
  occ    things that stand in front of people behind them (cut out of the picture, sorted by their bottom edge)
  front  / far  the two walking lines on the near / far pavement
  gather where a crowd forms     watch  where people stand to look at the screen
  queue  where a queue starts    screen the blank screen that shows the AI's line [x, y, w, h]
  shops  lit shop fronts (darkened when shops close)
Install: Assets/Resources/Art/Blocks/<key>.png (+ <key>_night.png), <key>_occ<i>.png, layout.txt.
"""
import json
import os
import shutil
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
PARTS = os.path.join(HERE, "painted", "pixel_parts")
DST = os.path.join(ROOT, "Assets", "Resources", "Art", "Blocks")

PAVE = [0, 716, 1536, 792]
PLANTERS = [[205, 735, 385, 770], [620, 735, 712, 770], [1015, 735, 1125, 770], [1345, 735, 1490, 770]]

# key: (picture, night picture or None, game zone index)
PICTURES = {
    "culture": ("block_culture_v2.png", None, 0),
    "shop": ("block_shop_final.png", None, 1),
    "admin": ("block_admin_final.png", None, 2),
    "home": ("block_home_clean_final.png", "block_home_night_final.png", 3),
    "edu": ("block_edu_final.png", None, 4),
    "logistics": ("block_logistics.png", None, 5),
    "energy": ("block_energy.png", None, 6),
    "research": ("block_research.png", None, 7),
    "medical": ("block_medical.png", None, 8),
    "security": ("block_security.png", None, 9),
    "site": ("block_site.png", None, 10),
}

BLOCKS = {
    "home": dict(
        occ=[[515, 305, 630, 470], [738, 310, 822, 466], [20, 485, 252, 716], [278, 492, 472, 716], [500, 492, 692, 716],
             [975, 488, 1252, 716], [1284, 478, 1512, 716], [65, 278, 446, 412], [1108, 262, 1472, 412]],
        walk=[[440, 385, 1125, 500], [80, 398, 470, 448], [692, 495, 716, 735], [956, 495, 980, 735], PAVE],
        solid=[[505, 422, 632, 472], [736, 426, 826, 470], [846, 386, 944, 556], [976, 426, 1100, 472],
               [205, 735, 385, 770], [620, 735, 712, 770], [1015, 735, 1125, 770], [1240, 735, 1290, 770], [1345, 735, 1490, 770]],
        front=[722, 788], far=[918, 936], gather=[640, 470], watch=[1395, 735],
        queue=[365, 418], screen=[1328, 532, 136, 72],
        shops=[[40, 636, 200, 712], [290, 636, 455, 712], [522, 636, 652, 712], [1020, 636, 1152, 712], [1318, 636, 1492, 712]]),
    "shop": dict(
        occ=[[455, 362, 578, 474], [938, 362, 1118, 474], [700, 300, 864, 456], [8, 506, 1524, 716]],
        walk=[[470, 360, 1110, 512], [760, 505, 840, 720], PAVE],
        solid=[[705, 405, 860, 455], [455, 365, 575, 472], [940, 365, 1115, 472], [615, 455, 690, 495], [890, 455, 950, 490],
               [205, 735, 385, 770], [620, 735, 712, 770], [1015, 735, 1125, 770], [1350, 735, 1490, 770]],
        front=[724, 788], far=[918, 936], gather=[790, 480], watch=[560, 490], screen=[128, 88, 160, 126],
        shops=[[28, 615, 188, 712], [212, 615, 372, 712], [392, 615, 530, 712], [550, 615, 690, 712], [910, 615, 1060, 712],
               [1230, 615, 1360, 712], [1370, 615, 1512, 712]]),
    "culture": dict(
        occ=[[18, 452, 462, 706], [488, 466, 852, 706], [984, 486, 1532, 706], [88, 262, 402, 432], [1336, 278, 1432, 422],
             [698, 72, 952, 246]],
        walk=[[80, 235, 1520, 300], [400, 290, 1300, 460], [855, 455, 1000, 705], [0, 704, 1536, 792]],
        solid=[[525, 290, 1145, 410], [90, 265, 400, 430], [420, 350, 475, 382], [1200, 350, 1255, 382], [1335, 390, 1430, 422],
               [1120, 650, 1420, 724], [1155, 740, 1275, 772], [1345, 740, 1465, 772]],
        front=[730, 790], far=[918, 936], gather=[835, 440], watch=[830, 290], screen=[736, 100, 194, 84],
        shops=[[95, 640, 345, 700], [540, 640, 700, 700], [1135, 580, 1450, 700]]),
    "edu": dict(
        occ=[[4, 452, 496, 716], [514, 524, 892, 716], [918, 508, 1202, 716], [1224, 492, 1532, 716], [502, 318, 582, 457],
             [776, 443, 952, 516], [534, 212, 1002, 406]],
        walk=[[60, 395, 1040, 452], [495, 452, 1040, 515], [110, 330, 440, 400], [495, 515, 525, 712], [895, 515, 925, 712],
              [1200, 515, 1230, 712], PAVE],
        solid=[[505, 400, 580, 458], [340, 425, 440, 447], [610, 486, 745, 507], [1010, 408, 1042, 462], [778, 445, 950, 514],
               [205, 735, 385, 768], [620, 735, 712, 768], [1015, 735, 1125, 768], [1240, 735, 1325, 768], [1345, 735, 1490, 768],
               [1270, 640, 1460, 724]],
        front=[727, 788], far=[918, 936], gather=[650, 470], watch=[890, 430], screen=[806, 146, 172, 48],
        shops=[[140, 600, 470, 710], [540, 600, 860, 710], [985, 600, 1150, 710], [1240, 590, 1500, 710]]),
    "admin": dict(
        occ=[[18, 398, 562, 722], [594, 474, 1026, 722], [1078, 442, 1526, 722], [358, 238, 554, 344], [983, 238, 1177, 344],
             [688, 182, 852, 332], [222, 212, 302, 336]],
        walk=[[40, 250, 1500, 398], [560, 398, 630, 470], [910, 398, 1060, 470], [560, 470, 600, 720], [1025, 470, 1080, 720],
              [0, 722, 1536, 792]],
        solid=[[630, 295, 910, 468], [690, 245, 850, 300], [360, 245, 552, 342], [985, 245, 1175, 342], [225, 298, 300, 334],
               [1400, 402, 1478, 457], [1485, 298, 1536, 346], [590, 724, 700, 748], [910, 724, 1015, 748], [1080, 690, 1150, 722]],
        front=[760, 788], far=[918, 936], gather=[470, 420], watch=[800, 760], queue=[345, 372], screen=[698, 538, 212, 58]),
}
# the six later blocks: set after looking at their pictures (see NEW below)
from blocks_new import NEW  # noqa: E402

BLOCKS.update(NEW)


def write_js():
    path = os.path.join(HERE, "proto", "blocks.js")
    with open(path, "w", encoding="utf-8") as f:
        f.write("// generated by Tools/Art/blocks_data.py; do not edit\n")
        f.write("const BLOCK_PICTURES = " + json.dumps({k: [v[0].replace("block_", "block_"), v[1]] for k, v in PICTURES.items()}) + ";\n")
        f.write("const BLOCKS = " + json.dumps(BLOCKS) + ";\n")
    for k, (pic, night, _) in PICTURES.items():
        for name in (pic, night):
            if name and os.path.exists(os.path.join(PARTS, name)):
                shutil.copy(os.path.join(PARTS, name), os.path.join(HERE, "proto", name))
    print("wrote", path)


def install():
    os.makedirs(DST, exist_ok=True)
    lines = []
    for key, (pic, night, zone) in PICTURES.items():
        src = os.path.join(PARTS, pic)
        if not os.path.exists(src) or key not in BLOCKS:
            print("skip", key)
            continue
        im = Image.open(src).convert("RGBA").resize((1536, 1024))
        im.save(os.path.join(DST, f"{key}.png"))
        b = BLOCKS[key]
        if night and os.path.exists(os.path.join(PARTS, night)):
            nim = Image.open(os.path.join(PARTS, night)).convert("RGBA").resize((1536, 1024))
            nim.save(os.path.join(DST, f"{key}_night.png"))
            for i, r in enumerate(b.get("occ", [])):
                nim.crop(tuple(r)).save(os.path.join(DST, f"{key}_night_occ{i}.png"))
        lines.append(f"block {key} {zone}")
        for i, r in enumerate(b.get("occ", [])):
            im.crop(tuple(r)).save(os.path.join(DST, f"{key}_occ{i}.png"))
        for field in ("walk", "solid", "occ", "shops"):
            for r in b.get(field, []):
                lines.append(f"{field} " + " ".join(str(int(v)) for v in r))
        for field in ("front", "far", "gather", "watch", "queue", "screen"):
            if field in b:
                lines.append(f"{field} " + " ".join(str(int(v)) for v in b[field]))
    with open(os.path.join(DST, "layout.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("installed", len([l for l in lines if l.startswith("block")]), "blocks into", DST)


def preview():
    out = os.path.join(PARTS, "blocks_preview")
    os.makedirs(out, exist_ok=True)
    for key, (pic, _, _) in PICTURES.items():
        if key not in BLOCKS or not os.path.exists(os.path.join(PARTS, pic)):
            continue
        im = Image.open(os.path.join(PARTS, pic)).convert("RGBA").resize((1536, 1024))
        ov = Image.new("RGBA", im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(ov)
        b = BLOCKS[key]
        for r in b["walk"]:
            d.rectangle(r, fill=(0, 255, 120, 70))
        for r in b["solid"]:
            d.rectangle(r, fill=(255, 40, 40, 90))
        for r in b.get("occ", []):
            d.rectangle(r, outline=(255, 60, 200, 255), width=3)
        Image.alpha_composite(im, ov).convert("RGB").save(os.path.join(out, f"{key}.png"))
    print("previews in", out)


if __name__ == "__main__":
    write_js()
    install()
    if "--preview" in sys.argv:
        preview()
