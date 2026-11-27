"""Layout guide for the city overview (2026-11-26): a 3D massing model of Sector α seen like alpha_city.png.

    python Tools/Art/city_guide.py [out.png]

The eleven functional districts (GDD 2) are no longer one cell each: the city is a 9 x 6 grid of city blocks and
each district covers several of them (housing the most), so the city reads as a city, not a campus. Tall housing
stands at the back and on the sides (it hides nothing there), mid-rise commerce rings the AI centre in the middle,
low facilities and mid-rise housing are in front. The gate is in the left wall beside logistics and security; the
city fills the frame and the outside world is left out. Districts are tinted and labelled so the generated painting can
be checked against the guide. Used as the layout reference, with alpha_city.png as the style reference.
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFont

W, H = 1536, 1024
CAM = (560.0, -120.0, 800.0)
PITCH, FOV = math.radians(60), math.radians(56)
F = (W / 2) / math.tan(FOV / 2)


def proj(x, y, z):
    """world (x east, y north, z up) to screen; the camera looks north and down"""
    dx, dy, dz = x - CAM[0], y - CAM[1], z - CAM[2]
    fy = dy * math.cos(PITCH) - dz * math.sin(PITCH)
    fz = dy * math.sin(PITCH) + dz * math.cos(PITCH)
    fy = max(fy, 1)
    return (W / 2 + F * dx / fy, H / 2 - F * fz / fy)


def poly(d, pts, fill, outline=(70, 80, 100)):
    d.polygon([proj(*p) for p in pts], fill=fill, outline=outline)


def box(d, x0, y0, x1, y1, h, col):
    side = tuple(int(c * 0.86) for c in col)
    top = tuple(min(255, int(c * 1.06)) for c in col)
    poly(d, [(x0, y0, 0), (x1, y0, 0), (x1, y0, h), (x0, y0, h)], col)                 # south face
    if (x0 + x1) / 2 < CAM[0]:
        poly(d, [(x1, y0, 0), (x1, y1, 0), (x1, y1, h), (x1, y0, h)], side)            # east face
    else:
        poly(d, [(x0, y0, 0), (x0, y1, 0), (x0, y1, h), (x0, y0, h)], side)            # west face
    poly(d, [(x0, y0, h), (x1, y0, h), (x1, y1, h), (x0, y1, h)], top)
    for z in [k * 3.2 for k in range(1, int(h / 3.2))]:
        d.line([proj(x0, y0, z), proj(x1, y0, z)], fill=tuple(int(c * 0.7) for c in col))


CELL, ROAD = 100, 22
# one letter per city block, row 0 at the back (north)
PLAN = [
    "HHHMMHHHS",
    "HHCCMHHRR",
    "EECAACCRR",
    "ELCAACUUH",
    "LLKUUDDHH",
    "LKKUUDDHH",
]
ROWS, COLS = len(PLAN), len(PLAN[0])
DISTRICTS = {
    "H": ("housing", (236, 238, 244)), "M": ("medical", (244, 232, 232)), "S": ("sealed site", (226, 214, 190)),
    "C": ("commerce", (220, 234, 250)), "R": ("research", (226, 242, 234)), "E": ("energy", (214, 226, 240)),
    "A": ("AI centre", (230, 248, 252)), "L": ("logistics", (222, 222, 228)), "U": ("culture", (236, 244, 240)),
    "K": ("security", (226, 226, 240)), "D": ("education", (244, 240, 228)),
}


def cell(c, r):
    x0 = c * (CELL + ROAD) + ROAD
    y0 = (ROWS - 1 - r) * (CELL + ROAD) + ROAD
    return x0, y0, x0 + CELL, y0 + CELL


def masses_for(c, r, rnd):
    kind = PLAN[r][c]
    name, col = DISTRICTS[kind]
    x0, y0, x1, y1 = cell(c, r)
    out = []
    if kind == "H":
        lo, hi = ((90, 140) if r <= 1 else (60, 95) if r <= 3 else (25, 45))
        for a in (0, 1):
            for b in (0, 1):
                bx, by = x0 + 6 + a * 50, y0 + 6 + b * 50
                out.append((bx, by, bx + 38, by + (14 if b == 0 else 38), rnd.randint(lo, hi), col))
    elif kind == "C":
        for a in (0, 1):
            for b in (0, 1):
                bx, by = x0 + 6 + a * 50, y0 + 6 + b * 50
                out.append((bx, by, bx + 40, by + 38, rnd.choice((30, 45, 60, 80)), col))
    elif kind == "M":
        out += [(x0 + 8, y0 + 8, x1 - 8, y0 + 40, 30, col), (x0 + 20, y0 + 52, x1 - 20, y1 - 8, 20, col)]
    elif kind == "E":
        out += [(x0 + 8, y0 + 8, x0 + 46, y0 + 46, 10, col), (x0 + 54, y0 + 8, x1 - 8, y0 + 46, 10, col),
                (x0 + 8, y0 + 56, x1 - 8, y1 - 8, 3, (120, 150, 200))]
    elif kind == "R":
        out += [(x0 + 8, y0 + 8, x0 + 50, y0 + 44, 18, col), (x0 + 58, y0 + 16, x1 - 8, y0 + 50, 14, col),
                (x0 + 18, y0 + 60, x1 - 18, y1 - 10, 12, col)]
    elif kind == "L":
        out += [(x0 + 6, y0 + 8, x1 - 6, y0 + 58, 14, col)]
    elif kind == "K":
        out += [(x0 + 10, y0 + 10, x0 + 56, y0 + 48, 16, col)]
        if rnd.random() < 0.5:
            out.append((x0 + 66, y0 + 20, x0 + 80, y0 + 34, 46, col))
    elif kind == "U":
        out += [(x0 + 28, y0 + 64, x1 - 28, y1 - 12, 12, col)] if rnd.random() < 0.5 else []
    elif kind == "D":
        out += [(x0 + 8, y0 + 10, x0 + 56, y0 + 40, 14, col), (x0 + 62, y0 + 58, x1 - 8, y1 - 8, 18, col)]
    elif kind == "S":
        out += [(x0 + 30, y0 + 30, x0 + 34, y0 + 34, 60, (220, 170, 60))]
    return out


def main(out):
    rnd = random.Random(9)
    img = Image.new("RGB", (W, H), (176, 160, 136))
    d = ImageDraw.Draw(img)
    cw, chh = COLS * (CELL + ROAD) + ROAD, ROWS * (CELL + ROAD) + ROAD
    poly(d, [(-1500, -600, 0), (cw + 1500, -600, 0), (cw + 1500, chh + 1500, 0), (-1500, chh + 1500, 0)], (176, 160, 136), None)
    poly(d, [(0, 0, 0), (cw, 0, 0), (cw, chh, 0), (0, chh, 0)], (122, 132, 148), None)
    for r in range(ROWS):
        for c in range(COLS):
            kind = PLAN[r][c]
            x0, y0, x1, y1 = cell(c, r)
            fill = (196, 176, 140) if kind == "S" else (214, 216, 212)
            poly(d, [(x0, y0, 0.1), (x1, y0, 0.1), (x1, y1, 0.1), (x0, y1, 0.1)], fill)
            if kind == "U":
                poly(d, [(x0 + 12, y0 + 12, 0.2), (x1 - 12, y0 + 12, 0.2), (x1 - 12, y0 + 52, 0.2), (x0 + 12, y0 + 52, 0.2)], (120, 180, 220))
    # the AI centre: one round-ish landmark across its four blocks, a plaza with water in front
    ax0, ay0 = cell(3, 3)[0], cell(3, 3)[1]
    ax1, ay1 = cell(4, 2)[2], cell(4, 2)[3]
    poly(d, [(ax0, ay0, 0.15), (ax1, ay0, 0.15), (ax1, ay1, 0.15), (ax0, ay1, 0.15)], (226, 230, 230))
    poly(d, [(ax0 + 40, ay0 + 14, 0.2), (ax1 - 40, ay0 + 14, 0.2), (ax1 - 40, ay0 + 70, 0.2), (ax0 + 40, ay0 + 70, 0.2)], (140, 196, 230))
    # maglev ring inside the wall and one line across the middle
    ring = [(10, 10), (cw - 10, 10), (cw - 10, chh - 10), (10, chh - 10), (10, 10)]
    for a, b in zip(ring, ring[1:]):
        d.line([proj(a[0], a[1], 10), proj(b[0], b[1], 10)], fill=(90, 225, 245), width=3)
    ym = cell(0, 2)[1] - ROAD / 2
    d.line([proj(10, ym, 10), proj(cw - 10, ym, 10)], fill=(90, 225, 245), width=3)
    masses = []
    # the outside world is not shown: the city fills the frame (2026-11-26)
    gy = cell(0, 4)[1] - ROAD / 2
    T, WH = 8, 16
    masses += [(-T, -T, cw + T, 0, WH, (246, 246, 244)), (-T, chh, cw + T, chh + T, WH, (246, 246, 244)),
               (-T, 0, 0, gy - 12, WH, (246, 246, 244)), (-T, gy + 12, 0, chh, WH, (246, 246, 244)),
               (cw, 0, cw + T, chh, WH, (246, 246, 244))]
    for r in range(ROWS):
        for c in range(COLS):
            if PLAN[r][c] != "A":
                masses += masses_for(c, r, rnd)
    cx, cy = (ax0 + ax1) / 2, (ay0 + ay1) / 2
    masses += [(cx - 70, cy - 20, cx + 70, cy + 70, 40, DISTRICTS["A"][1]),
               (cx - 36, cy + 4, cx + 36, cy + 56, 90, DISTRICTS["A"][1])]
    masses.sort(key=lambda m: -((m[1] + m[3]) / 2))
    for m in masses:
        box(d, *m)
    # district labels (one per district, on its first block)
    try:
        font = ImageFont.truetype("arial.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    seen = set()
    for r in range(ROWS):
        for c in range(COLS):
            kind = PLAN[r][c]
            if kind in seen:
                continue
            seen.add(kind)
            x0, y0, _, _ = cell(c, r)
            px, py = proj(x0 + 4, y0 + 2, 0)
            d.text((px, py - 20), DISTRICTS[kind][0], fill=(20, 30, 60), font=font)
    img.save(out)
    print("written", out)


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "painted", "pixel_parts", "city_guide.png"))
