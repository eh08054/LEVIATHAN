"""District outlines, pins, roads and the AI ring for the v9 city picture (2026-11-27, free).

    uv run --with opencv-python-headless --with numpy --with pillow python Tools/Art/city_v9_zones.py

Tools/Art/painted/city_v9/zones.txt is projected from the Unity camera (capture space). GPT redraws the view a little
wider, so the ground is carried onto day.png by the homography between the city plate's corners in both (CAP from
the 'plate' line, DAY read off gridded crops of day.png). Writes Assets/Resources/Map/zones_v8.txt (the file OpsMap
reads; name kept): 'zone <z> x y ...' outlines, 'pin <z> x y', 'center x y', 'road <wide> x0 y0 x1 y1', 'ring cx cy
rx' in image fractions. Preview: Tools/Art/painted/city_v9/zones_preview.png.
"""
import os

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
D = os.path.join(ROOT, "Tools", "Art", "painted", "city_v9")
OUT = os.path.join(ROOT, "Assets", "Resources", "Map", "zones_v8.txt")
W, H = 1920, 1080
ZONE = {"U": 0, "C": 1, "A": 2, "H": 3, "Dd": 4, "L": 5, "E": 6, "R": 7, "M": 8, "K": 9, "S": 10}
# the plate's corners on day.png (1920 x 1080 px): far-left, far-right, near-right, near-left
DAY = [(175, 244), (1730, 243), (1908, 1032), (6, 1032)]

lines = [l.split() for l in open(os.path.join(D, "zones.txt"), encoding="utf-8") if l.strip() and not l.startswith("#")]
plate = next(l for l in lines if l[0] == "plate")
CAP = [(float(plate[1 + i * 2]) * W, float(plate[2 + i * 2]) * H) for i in range(4)]
Hm = cv2.getPerspectiveTransform(np.float32(CAP), np.float32(DAY))


def carry(x, y):
    p = cv2.perspectiveTransform(np.float32([[[x * W, y * H]]]), Hm)[0, 0]
    return p[0] / W, p[1] / H


SW, SH = 3840, 2160
quads = {k: [] for k in ZONE}
out, pins = [], {}
for f in lines:
    if f[0] in ZONE:
        quads[f[0]].append(np.array([[c[0] * SW, c[1] * SH] for c in (carry(float(f[i]), float(f[i + 1])) for i in range(1, 9, 2))], np.float32))
    elif f[0] == "pin":
        pins[f[1]] = carry(float(f[2]), float(f[3]))
    elif f[0] == "road":
        a, b = carry(float(f[2]), float(f[3])), carry(float(f[4]), float(f[5]))
        out.append(f"road {f[1]} {a[0]:.4f} {a[1]:.4f} {b[0]:.4f} {b[1]:.4f}")
    elif f[0] == "ring":
        c, e = carry(float(f[1]), float(f[2])), carry(float(f[3]), float(f[4]))
        out.append(f"ring {c[0]:.4f} {c[1]:.4f} {abs(e[0] - c[0]):.4f}")

shapes = {}
for k, z in ZONE.items():
    mask = np.zeros((SH, SW), np.uint8)
    for q in quads[k]:
        cv2.fillPoly(mask, [q.astype(np.int32)], 255)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (71, 51)))
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    for c in sorted(contours, key=cv2.contourArea, reverse=True):
        if cv2.contourArea(c) < 2000:
            continue
        poly = cv2.approxPolyDP(c, 4.0, True).reshape(-1, 2)
        shapes.setdefault(z, []).append(poly)
        out.insert(0, f"zone {z} " + " ".join(f"{x / SW:.4f} {y / SH:.4f}" for x, y in poly))
for k, z in ZONE.items():
    out.append(f"pin {z} {pins[k][0]:.4f} {pins[k][1]:.4f}")
out.append(f"center {pins['A'][0]:.4f} {pins['A'][1]:.4f}")
# largest polygon of each district first (OpsMap takes the first per district)
zl = sorted([l for l in out if l.startswith("zone")], key=lambda l: (int(l.split()[1]), -len(l)))
rest = [l for l in out if not l.startswith("zone")]
with open(OUT, "w", encoding="utf-8") as f:
    f.write("# v9 city picture (Resources/Macro/city_main): districts, pins, roads, AI ring in image fractions; Tools/Art/city_v9_zones.py\n")
    f.write("\n".join(zl + rest) + "\n")
print("wrote", OUT)

COLORS = [(255, 214, 110), (255, 70, 220), (40, 225, 215), (255, 232, 180), (170, 110, 255), (255, 190, 50),
          (255, 130, 40), (110, 200, 255), (110, 255, 200), (60, 120, 255), (225, 230, 240)]
im = Image.open(os.path.join(D, "day.png")).convert("RGB").resize((SW, SH))
d = ImageDraw.Draw(im)
for z, polys in shapes.items():
    for p in polys:
        d.line([tuple(v) for v in p] + [tuple(p[0])], fill=COLORS[z], width=5)
for l in rest:
    f = l.split()
    if f[0] == "road":
        d.line([(float(f[2]) * SW, float(f[3]) * SH), (float(f[4]) * SW, float(f[5]) * SH)], fill=(255, 255, 255) if f[1] == "1" else (120, 220, 255), width=2)
im.resize((1920, 1080), Image.LANCZOS).save(os.path.join(D, "zones_preview.png"))
