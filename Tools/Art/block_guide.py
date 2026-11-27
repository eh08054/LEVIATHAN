"""Layout guide for a block painting (2026-11-26): a simple 3D massing model rendered from the block camera.

    python Tools/Art/block_guide.py [out.png]

An image edit kept copying the reference picture (curved roads stayed curved), so the geometry is fixed here
instead: a straight street grid, the block as a clean rectangle seen from its corner, building boxes with their
heights, the courtyard. The picture is then generated with this guide as the layout reference and a finished
block painting as the style reference. Camera: high, looking diagonally down at about 40 degrees.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

W, H = 1536, 1024
# camera: yaw 45 degrees (looking along the block's diagonal), pitch 40 degrees down, field of view 60
CAM = (-70.0, -70.0, 150.0)
YAW, PITCH, FOV = math.radians(45), math.radians(30), math.radians(70)
F = (W / 2) / math.tan(FOV / 2)


def proj(x, y, z):
    """world (x east, y north, z up) to screen"""
    dx, dy, dz = x - CAM[0], y - CAM[1], z - CAM[2]
    # yaw: rotate so the camera looks along +y'
    cx = dx * math.cos(YAW) - dy * math.sin(YAW)
    cy = dx * math.sin(YAW) + dy * math.cos(YAW)
    # pitch: tilt down
    fy = cy * math.cos(PITCH) - dz * math.sin(PITCH)
    fz = cy * math.sin(PITCH) + dz * math.cos(PITCH)
    if fy < 1:
        fy = 1
    return (W / 2 + F * cx / fy, H / 2 - F * fz / fy)


def poly(d, pts, fill, outline=(60, 70, 90)):
    d.polygon([proj(*p) for p in pts], fill=fill, outline=outline)


def box(d, x0, y0, x1, y1, h, fill=(236, 238, 242), side=(200, 206, 216), top=(250, 250, 252)):
    """a building box; faces drawn back to front (the camera is at -x, -y, so the -y and -x faces show)"""
    poly(d, [(x0, y0, 0), (x1, y0, 0), (x1, y0, h), (x0, y0, h)], fill)          # south face
    poly(d, [(x0, y0, 0), (x0, y1, 0), (x0, y1, h), (x0, y0, h)], side)          # west face
    poly(d, [(x0, y0, h), (x1, y0, h), (x1, y1, h), (x0, y1, h)], top)           # roof
    # floor lines every 3.2 units on the visible faces
    for z in [k * 3.2 for k in range(1, int(h / 3.2))]:
        d.line([proj(x0, y0, z), proj(x1, y0, z)], fill=(150, 170, 200))
        d.line([proj(x0, y0, z), proj(x0, y1, z)], fill=(140, 160, 190))


def main(out):
    img = Image.new("RGB", (W, H), (200, 222, 244))
    d = ImageDraw.Draw(img)
    # ground far and wide
    poly(d, [(-2000, -2000, 0), (3000, -2000, 0), (3000, 3000, 0), (-2000, 3000, 0)], (214, 214, 210), None)
    # straight street grid: blocks of 120 x 120 with 24-wide roads
    B, R = 120, 24
    for k in range(-6, 12):
        a = k * (B + R) - R
        poly(d, [(a, -2000, 0.01), (a + R, -2000, 0.01), (a + R, 3000, 0.01), (a, 3000, 0.01)], (120, 132, 150), None)
        poly(d, [(-2000, a, 0.01), (3000, a, 0.01), (3000, a + R, 0.01), (-2000, a + R, 0.01)], (120, 132, 150), None)
    for k in range(-6, 12):
        a = k * (B + R) - R / 2
        d.line([proj(a, -2000, 0.02), proj(a, 3000, 0.02)], fill=(90, 225, 245))
        d.line([proj(-2000, a, 0.02), proj(3000, a, 0.02)], fill=(90, 225, 245))
    # the block: (0,0)-(120,120); courtyard in the middle opening to the south-west corner
    poly(d, [(0, 0, 0.03), (B, 0, 0.03), (B, B, 0.03), (0, B, 0.03)], (226, 226, 222))
    poly(d, [(34, 34, 0.04), (86, 34, 0.04), (86, 86, 0.04), (34, 86, 0.04)], (236, 234, 228))   # courtyard
    poly(d, [(56, 40, 0.05), (64, 40, 0.05), (64, 80, 0.05), (56, 80, 0.05)], (110, 180, 220))   # water channel
    # city around: boxes of mixed height on the other blocks (only those behind and beside the camera's view)
    import random
    rnd = random.Random(5)
    others = []
    for bx in range(-3, 8):
        for by in range(-3, 8):
            if (bx, by) == (0, 0) or bx + by < 0:
                continue
            ox, oy = bx * (B + R), by * (B + R)
            for k in range(rnd.randint(2, 4)):
                w, l = rnd.randint(22, 40), rnd.randint(22, 40)
                x, y = ox + rnd.randint(0, B - w), oy + rnd.randint(0, B - l)
                h = rnd.choice((40, 60, 80, 100, 130, 180))
                others.append((x, y, x + w, y + l, h))
    # this block: tall housing slabs on the far (north and east) sides, mid-rise buildings with shops on the
    # ground floor along the near sides (six to eight storeys, 3.2 a storey)
    mine = [
        (6, 96, 34, 116, 120), (40, 98, 80, 116, 110), (88, 92, 114, 116, 130),      # north side
        (96, 40, 116, 84, 115), (96, 6, 116, 34, 100),                               # east side
        (60, 100, 70, 110, 220),                                                     # the supertall
        (6, 6, 26, 26, 26), (30, 6, 46, 22, 20), (74, 6, 90, 22, 23),               # south shops (gap 46-74: lot)
        (6, 32, 22, 52, 24), (6, 66, 22, 86, 19),                                    # west shops
        (36, 70, 52, 86, 10), (70, 40, 88, 56, 8),                                   # ration hall, care centre
    ]
    allb = others + mine
    # draw far to near: sort by distance of the box centre from the camera
    allb.sort(key=lambda b: -math.hypot((b[0] + b[2]) / 2 - CAM[0], (b[1] + b[3]) / 2 - CAM[1]))
    for b in allb:
        mine_b = b in mine
        box(d, *b, fill=(240, 242, 246) if mine_b else (214, 224, 236),
            side=(206, 212, 222) if mine_b else (190, 202, 218))
    # the empty lot
    poly(d, [(46, 6, 0.06), (74, 6, 0.06), (74, 22, 0.06), (46, 22, 0.06)], (176, 150, 110), (90, 80, 60))
    # haze toward the horizon
    haze = Image.new("RGB", (W, H), (210, 226, 242))
    mask = Image.new("L", (W, H), 0)
    md = ImageDraw.Draw(mask)
    for y in range(H):
        md.line([0, y, W, y], fill=int(max(0, 170 - y * 0.35)))
    img = Image.composite(haze, img, mask)
    img.save(out)
    print("written", out)


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "painted", "pixel_parts", "block_guide.png"))
