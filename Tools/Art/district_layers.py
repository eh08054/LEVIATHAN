"""Per-district layers cut from the city painting, so each district can be lit, outlined or picked on its own.

The painting stays one image (Resources/Macro/city_main). For every district this writes, into Resources/Map/:
  z<i>_edge.png  its boundary: a glow along the part of the ground edge the camera sees (`seen`), fading out where
                 it runs behind the district's buildings, white (tinted in Unity), thinner far away, thicker near
  z<i>_cut.png   the district itself: ground and the buildings standing on it, cut out of the painting
  z<i>_lit.png   the same cut, lit up (for hover)
and Map/layers.txt: one line per district, `i x0 y0 x1 y1 ; hull` (image fractions: the layers' rectangle, then the
outline used for hit testing).

Each district is set up on its own in DISTRICTS below: its curb corners (or an ellipse), how high its buildings
may rise (`rise`) and optional hand-drawn `extra` silhouette parts. Edit one, rerun.

    python Tools/Art/district_layers.py               # build the layers (needs the depth map)
    python Tools/Art/district_layers.py preview.png   # also write a check image
    python Tools/Art/district_layers.py --only 6 7    # rebuild some districts
    python Tools/Art/district_layers.py --cs          # print the ground outlines for OpsMap.ZoneShapes

The depth map (city_main_v7_depth.png) comes from Depth Anything V2 Small, run once in an isolated env:
    uv run --no-project --managed-python --python 3.12 --with torch --with transformers --with pillow --with numpy \
        --index-url https://download.pytorch.org/whl/cpu --extra-index-url https://pypi.org/simple \
        python Tools/Art/district_layers.py --depth
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

PAINTING = "Tools/Art/painted/ui/city_main_v7.png"
DEPTH = "Tools/Art/painted/ui/city_main_v7_depth.png"
OUT = os.environ.get("MAP_OUT", "Assets/Resources/Map")  # MAP_OUT: build somewhere else to check first

NAMES = ["문화·여가", "상업", "관리", "주거", "교육", "물류·산업", "에너지·발전", "연구·개발", "의료·생명", "치안·보안", "유전 설계"]

# corners: the outer edge of each district's sidewalk/tree strip where it meets the road, at its left, right and front
# (bottom) corners (the 2026-11-19 ones were traced from the user's own red sketch) (image fractions,
# read off gridded close-ups; the hidden back corner is completed as a parallelogram), or ellipse:
# centre and radii. rise: how far above its far curb the district's buildings may reach (image height fraction).
# extra: optional polygons added to the silhouette by hand. seen: which part of the boundary glows, (a, b) in corner
# units (corner i at i; quads go left, top, right, bottom; ellipses right, bottom, left, top); default the front two
# edges with fading sides. A low district whose back edge is in view (the building pit) shows all of it.
#
# edge (2026-11-20, replaces corners where given): the visible boundary traced by hand on gridded close-ups of the
# painting, the line where the district's sidewalk meets the road, only where the camera sees it, as polylines (image
# fractions, left to right along the front). The ends fade where the line goes behind the buildings. The ground
# footprint (silhouette, hit test, OpsMap.ZoneShapes) is that line closed by the hidden back corner (a parallelogram
# from its leftmost, rightmost and lowest points).
DISTRICTS = {
    0: dict(edge=[[(0.4344, 0.489), (0.4242, 0.5019), (0.4167, 0.5175), (0.4128, 0.5365), (0.4161, 0.556), (0.4291, 0.5706), (0.4548, 0.5799), (0.5092, 0.5828), (0.5636, 0.5799), (0.5896, 0.5706), (0.6026, 0.556), (0.6056, 0.5365), (0.6013, 0.5175), (0.5942, 0.5019), (0.5844, 0.489), (0.5758, 0.4731), (0.5636, 0.4585), (0.5474, 0.4487), (0.5092, 0.4444), (0.471, 0.4487), (0.4548, 0.4585), (0.4426, 0.4731), (0.4344, 0.489)]], rise=0.1),
    1: dict(edge=[[(0.2089, 0.4597), (0.1944, 0.4706), (0.1889, 0.4788), (0.1817, 0.4869), (0.1813, 0.4964), (0.1871, 0.5032), (0.2306, 0.5236), (0.2669, 0.544), (0.2941, 0.5603), (0.3031, 0.5617), (0.314, 0.5549), (0.3575, 0.5304), (0.3938, 0.5059), (0.4101, 0.4842), (0.4173, 0.4624), (0.4137, 0.4516), (0.4083, 0.4434), (0.3992, 0.4339)]], rise=0.1,
            extra=[[(0.19, 0.49), (0.2, 0.445), (0.26, 0.43), (0.3, 0.405), (0.335, 0.4), (0.37, 0.385), (0.39, 0.385), (0.405, 0.44), (0.412, 0.468)]]),
    2: dict(ellipse=((0.5052, 0.4096), 0.0617, 0.0304), seen=(-0.6, 2.6), rise=0.18,
            extra=[[(0.497, 0.245), (0.513, 0.245), (0.522, 0.29), (0.53, 0.345), (0.552, 0.37), (0.552, 0.405), (0.462, 0.405), (0.462, 0.36), (0.484, 0.33), (0.49, 0.29)]]),
    3: dict(inset=22, edge=[[(0.2057, 0.6433), (0.1944, 0.6588), (0.184, 0.6764), (0.1764, 0.6857), (0.1779, 0.6965), (0.2119, 0.7228), (0.2428, 0.746), (0.2738, 0.7692), (0.2944, 0.7847), (0.3027, 0.7924), (0.315, 0.7955), (0.3254, 0.7909), (0.346, 0.7779), (0.3769, 0.7593), (0.4079, 0.7398), (0.4388, 0.7203), (0.4491, 0.7089), (0.4522, 0.6965), (0.4471, 0.6872), (0.4388, 0.6774), (0.4223, 0.6665)]], rise=0.1),
    4: dict(edge=[[(0.6101, 0.6622), (0.5931, 0.6765), (0.5772, 0.6917), (0.5698, 0.6997), (0.5708, 0.7099), (0.5783, 0.715), (0.625, 0.7427), (0.6675, 0.7673), (0.7058, 0.7896), (0.7143, 0.7947), (0.7249, 0.7937), (0.7738, 0.7682), (0.8163, 0.7437), (0.845, 0.7214), (0.8493, 0.7109), (0.844, 0.7013), (0.8355, 0.6893), (0.8184, 0.6765)]], rise=0.1),
    5: dict(edge=[[(0.1798, 0.3212), (0.1635, 0.3288), (0.1454, 0.3402), (0.1341, 0.3524), (0.1363, 0.3611), (0.1834, 0.3883), (0.2288, 0.415), (0.2378, 0.4204), (0.2487, 0.4171), (0.3013, 0.3959), (0.3466, 0.3796), (0.3838, 0.366), (0.391, 0.3601), (0.3883, 0.353), (0.3792, 0.3452), (0.3647, 0.3365)]], rise=0.1),
    6: dict(edge=[[(0.225, 0.2224), (0.216, 0.2269), (0.21, 0.2325), (0.2025, 0.2392), (0.2032, 0.246), (0.225, 0.2577), (0.255, 0.2703), (0.282, 0.2815), (0.291, 0.2838), (0.3, 0.2808), (0.33, 0.2712), (0.36, 0.2577), (0.375, 0.2494), (0.3765, 0.2433), (0.3727, 0.2381), (0.3675, 0.2302), (0.36, 0.2224)]], rise=0.075, gap=5),
    7: dict(edge=[[(0.4654, 0.2121), (0.4544, 0.2172), (0.4461, 0.2224), (0.4406, 0.2271), (0.442, 0.2327), (0.4681, 0.243), (0.497, 0.2543), (0.5272, 0.2677), (0.5382, 0.2698), (0.552, 0.2657), (0.585, 0.2523), (0.618, 0.2378), (0.6276, 0.2327), (0.6262, 0.2275), (0.6207, 0.2244), (0.6125, 0.2193), (0.6029, 0.2141)]], rise=0.1,
            extra=[[(0.508, 0.145), (0.529, 0.145), (0.529, 0.225), (0.508, 0.225)], [(0.534, 0.17), (0.549, 0.17), (0.549, 0.225), (0.534, 0.225)], [(0.572, 0.183), (0.593, 0.183), (0.593, 0.235), (0.572, 0.235)], [(0.466, 0.188), (0.48, 0.188), (0.48, 0.225), (0.466, 0.225)], [(0.45, 0.2), (0.49, 0.2), (0.49, 0.225), (0.45, 0.225)], [(0.49, 0.19), (0.52, 0.19), (0.52, 0.215), (0.49, 0.215)], [(0.548, 0.19), (0.588, 0.19), (0.588, 0.222), (0.548, 0.222)]]),
    8: dict(edge=[[(0.6243, 0.3156), (0.612, 0.3219), (0.6006, 0.3306), (0.5919, 0.3397), (0.591, 0.3476), (0.598, 0.3534), (0.64, 0.3744), (0.6925, 0.3962), (0.7083, 0.4041), (0.7205, 0.4054), (0.7363, 0.3975), (0.7713, 0.3791), (0.8028, 0.3613), (0.8124, 0.3516), (0.8124, 0.3437), (0.8063, 0.3371), (0.7975, 0.3287), (0.7844, 0.3201)]], rise=0.1),
    9: dict(open=True, edge=[[(0.604, 0.4585), (0.6, 0.4765), (0.607, 0.49), (0.618, 0.5035), (0.652, 0.541), (0.686, 0.5815), (0.695, 0.5875), (0.76, 0.5815), (0.8201, 0.5734), (0.8441, 0.568), (0.8491, 0.562), (0.8471, 0.5485), (0.8421, 0.5365)]], rise=0.1),
    10: dict(edge=[[(0.7041, 0.2027), (0.6949, 0.2076), (0.6897, 0.2125), (0.6864, 0.2204), (0.6884, 0.2332), (0.7225, 0.2509), (0.7697, 0.2731), (0.775, 0.2725), (0.8393, 0.246), (0.8406, 0.2381), (0.8393, 0.2302), (0.834, 0.2223), (0.8275, 0.2145)]], rise=0.1),
}


# The line's look, for every district unless its `line` dict overrides some of it: core width and blur (px at the
# nearest edge, thinner far away), glow width, blur and strength.
LINE = dict(core=3, core_blur=0.6, glow=10, glow_blur=7, glow_gain=0.55)

# Each district's neon colour (the preview; Unity tints the white edge layers with OpsMap.ZoneTints).
COLORS = {
    0: (255, 214, 110),   # 문화·여가: warm yellow
    1: (255, 70, 220),    # 상업: magenta
    2: (40, 225, 215),    # 관리: teal
    3: (255, 232, 180),   # 주거: warm white / pale gold
    4: (170, 110, 255),   # 교육: violet
    5: (255, 190, 50),    # 물류·산업: gold
    6: (255, 130, 40),    # 에너지·발전: orange
    7: (110, 200, 255),   # 연구·개발: sky blue
    8: (110, 255, 200),   # 의료·생명: mint
    9: (60, 120, 255),    # 치안·보안: blue
    10: (225, 230, 240),  # 유전 설계: silver white
}


def _inset(cfg):
    """Pull a traced run `inset` px in toward the district's middle (closer to its buildings and green)."""
    k = cfg.get("inset")
    if not k:
        return
    run = cfg["edge"][0]
    cx = sum(p[0] for p in run) / len(run) * 4096
    cy = sum(p[1] for p in run) / len(run) * 2731
    out = []
    for x, y in run:
        dx, dy = cx - x * 4096, cy - y * 2731
        d = math.hypot(dx, dy) or 1
        out.append((round((x * 4096 + dx / d * k) / 4096, 5), round((y * 2731 + dy / d * k) / 2731, 5)))
    cfg["edge"][0] = out


for _cfg in DISTRICTS.values():
    _inset(_cfg)


# ---------------------------------------------------------------- depth (run once, needs torch)

def make_depth():
    import torch
    from transformers import AutoImageProcessor, AutoModelForDepthEstimation
    name = "depth-anything/Depth-Anything-V2-Small-hf"
    proc = AutoImageProcessor.from_pretrained(name)
    model = AutoModelForDepthEstimation.from_pretrained(name).eval()
    im = Image.open(PAINTING).convert("RGB")
    small = im.resize((2048, round(im.height * 2048 / im.width)), Image.LANCZOS)
    inp = proc(images=small, return_tensors="pt", size={"height": 1022, "width": 1526})
    with torch.no_grad():
        d = model(**inp).predicted_depth
    d = torch.nn.functional.interpolate(d[:, None], size=(small.height, small.width), mode="bicubic")[0, 0].numpy()
    d = (d - d.min()) / (d.max() - d.min())
    Image.fromarray((d * 65535).astype(np.uint16)).save(DEPTH)
    print("wrote", DEPTH)


# ---------------------------------------------------------------- shapes

def rounded(pts, cut=0.07, steps=4):
    """Straight edges, corners rounded by a quadratic curve that starts `cut` of the way along each edge."""
    out = []
    n = len(pts)
    for i in range(n):
        (ax, ay), (bx, by), (cx, cy) = pts[i - 1], pts[i], pts[(i + 1) % n]
        p0 = (bx + (ax - bx) * cut, by + (ay - by) * cut)
        p2 = (bx + (cx - bx) * cut, by + (cy - by) * cut)
        for k in range(steps + 1):
            t = k / steps
            out.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * bx + t * t * p2[0],
                        (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * by + t * t * p2[1]))
    return out


def corners(cfg):
    """A quad's corners, left, top, right, bottom. The back (top) corner is hidden behind the district's buildings, so
    unless given it is completed from the three visible ones: the ground is close to a parallelogram in this view."""
    if "corners" in cfg:
        c = list(cfg["corners"])
    else:  # from the traced edge: its leftmost, rightmost and lowest points
        pts = [p for run in cfg["edge"] for p in run]
        c = [min(pts), max(pts), max(pts, key=lambda p: p[1])]
    if len(c) == 3:
        (lx, ly), (rx, ry), (bx, by) = c
        c = [(lx, ly), (lx + rx - bx, ly + ry - by), (rx, ry), (bx, by)]
    return c


def ground_outline(cfg, grow=0.0):
    """The district's ground edge (image fractions), pushed `grow` outward (the y offset is scaled by the aspect)."""
    if "ellipse" in cfg:
        (cx, cy), rx, ry = cfg["ellipse"]
        rx, ry = rx + grow, ry + grow * 0.6
        return [(cx + rx * math.cos(t), cy + ry * math.sin(t)) for t in (k * math.tau / 48 for k in range(48))]
    if "edge" in cfg:  # the traced line closed by the hidden back corner (a loop traced all round is its own)
        return loop(cfg)[:-1]
    pts = corners(cfg)
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    grown = []
    for x, y in pts:
        dx, dy = x - cx, (y - cy) / 0.6
        length = math.hypot(dx, dy) or 1
        grown.append((x + dx / length * grow, y + dy / length * grow * 0.6))
    return rounded(grown)


FADE = 0.18  # corner units over which a visible run fades in and out


def outline_param(cfg):
    """The rounded outline as (t, x, y), t in corner units: corner i at t = i (left, top, right, bottom for quads;
    right, bottom, left, top for ellipses), so t runs 0..n around the district."""
    if "ellipse" in cfg:
        (cx, cy), rx, ry = cfg["ellipse"]
        return [(k / 48 * 4, cx + rx * math.cos(k / 48 * math.tau), cy + ry * math.sin(k / 48 * math.tau)) for k in range(48)], 4
    pts = rounded(corners(cfg))
    n = len(corners(cfg))
    steps = len(pts) // n - 1
    out = []
    for i in range(n):
        for k in range(steps + 1):
            out.append((i + (2 * k / steps - 1) * 0.07, *pts[i * (steps + 1) + k]))
    out.sort(key=lambda q: q[0] % n)
    return [((t % n), x, y) for t, x, y in out], n


def seen_path(cfg):
    """The part of the boundary the camera sees, as (x, y, alpha): by default the two front edges plus a little of
    each side, fading out where the sides run behind the buildings. `seen=(a, b)` in corner units overrides it."""
    if "edge" in cfg:
        return traced_path([loop(cfg)])
    pts, n = outline_param(cfg)
    a, b = cfg.get("seen", (-0.2, 2.2) if "ellipse" in cfg else (1.82, 4.18))
    closed = b - a >= n
    ts = [q[0] for q in pts] + [pts[0][0] + n]
    xs = [q[1] for q in pts] + [pts[0][1]]
    ys = [q[2] for q in pts] + [pts[0][2]]

    def at(t):
        t %= n
        for j in range(len(ts) - 1):
            if ts[j] <= t <= ts[j + 1]:
                u = (t - ts[j]) / ((ts[j + 1] - ts[j]) or 1)
                return xs[j] + (xs[j + 1] - xs[j]) * u, ys[j] + (ys[j + 1] - ys[j]) * u
        return xs[-1], ys[-1]

    out = []
    count = int((b - a) / 0.004) + 1
    for k in range(count + 1):
        t = a + (b - a) * k / count
        if closed:
            alpha = 1.0
        else:
            e = min(t - a, b - t) / FADE
            e = max(0.0, min(1.0, e))
            alpha = e * e * (3 - 2 * e)
        out.append((*at(t), alpha))
    return out


def loop(cfg):
    """The whole boundary as a closed polyline: the traced run (front and the sides as far as traced), closed through
    the back corner (`back`, default the parallelogram's). Where the district's own buildings stand in front of it,
    build() hides it."""
    run = list(cfg["edge"][0])
    if run[0] == run[-1]:
        return run
    back = cfg.get("back") or corners(cfg)[1]
    return run + [tuple(back), run[0]]


PW_, PH_ = 4096, 2731   # the painting's size, for straight lines and offsets in pixels
MARGIN = 26             # px the boundary sits out from the traced curb, on the road
CHAMFER = 0.07          # corner cut, as a share of the shorter edge beside it


def _line_fit(pts):
    """A line (point, unit direction) through pixel points."""
    a = np.array(pts, float)
    c = a.mean(axis=0)
    _, _, v = np.linalg.svd(a - c)
    return c, v[0]


def _meet(l1, l2):
    (p, d), (q, e) = l1, l2
    m = np.array([[d[0], -e[0]], [d[1], -e[1]]])
    t = np.linalg.solve(m, q - p)[0]
    return p + d * t


def _chamfer(poly, cut=CHAMFER):
    out = []
    n = len(poly)
    for i in range(n):
        a, b, c = poly[i - 1], poly[i], poly[(i + 1) % n]
        k = cut * min(np.linalg.norm(a - b), np.linalg.norm(c - b))
        out.append(b + (a - b) / np.linalg.norm(a - b) * k)
        out.append(b + (c - b) / np.linalg.norm(c - b) * k)
    return out


def boundary(cfg):
    """The boundary drawn on the map and used for picking: straight edges with cut corners, a little out on the road
    from the traced curb (like a strategy map's region borders). Quads: the front-left and front-right edges are
    fitted to the traced run, the back edges run parallel to them through the run's two ends (`back` overrides the
    back corner). Round districts: an octagon around the ellipse or the closed trace. Image fractions, closed."""
    m = cfg.get("margin", MARGIN)
    if "ellipse" in cfg or cfg["edge"][0][0] == cfg["edge"][0][-1]:
        if "ellipse" in cfg:
            (cx, cy), rx, ry = cfg["ellipse"]
            cx, cy, rx, ry = cx * PW_, cy * PH_, rx * PW_, ry * PH_
        else:
            a = np.array([(x * PW_, y * PH_) for x, y in cfg["edge"][0]])
            (x0, y0), (x1, y1) = a.min(axis=0), a.max(axis=0)
            cx, cy, rx, ry = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
        rx, ry = rx + m, ry + m * 0.7
        k = 1 / math.cos(math.pi / 8)
        poly = [(cx + rx * k * math.cos(t), cy + ry * k * math.sin(t))
                for t in (math.pi / 8 + j * math.pi / 4 for j in range(8))]
        return [(x / PW_, y / PH_) for x, y in poly] + [(poly[0][0] / PW_, poly[0][1] / PH_)]
    run = [np.array((x * PW_, y * PH_)) for x, y in cfg["edge"][0]]
    iL = min(range(len(run)), key=lambda i: run[i][0])
    iF = max(range(len(run)), key=lambda i: run[i][1])
    iR = max(range(len(run)), key=lambda i: run[i][0])

    def middle(a, b):  # the straight middle of a traced side, away from its rounded corners
        seg = run[a:b + 1]
        k = max(1, len(seg) // 5)
        return seg[k:len(seg) - k] if len(seg) > 2 * k + 1 else seg

    fl = _line_fit(middle(iL, iF))
    fr = _line_fit(middle(iF, iR))
    bl = (run[0], fr[1])
    br = (run[-1], fl[1])
    if cfg.get("back"):
        b = np.array((cfg["back"][0] * PW_, cfg["back"][1] * PH_))
        bl, br = (b, run[0] - b), (b, run[-1] - b)
        bl = (b, bl[1] / np.linalg.norm(bl[1]))
        br = (b, br[1] / np.linalg.norm(br[1]))
    lines = [bl, fl, fr, br]                       # sides in order round: back-left, front-left, front-right, back-right
    centre = sum(run) / len(run)
    moved = []
    for p, d in lines:
        nrm = np.array((-d[1], d[0]))
        if np.dot(p - centre, nrm) < 0:
            nrm = -nrm
        moved.append((p + nrm * m, d))
    poly = [_meet(moved[i], moved[(i + 1) % 4]) for i in range(4)]   # left, front, right, back
    # cut the corners where the traced curb ends plus the margin: the side corners with a near-vertical cut, the
    # front with a near-flat one (as in the reference), the back with a small chamfer. Shallow corners would
    # otherwise reach far out along the road.
    xs = [q[0] for q in run]
    ys = [q[1] for q in run]
    limits = [("x", min(xs) - m * 1.4, -1), ("y", max(ys) + m * 1.1, 1), ("x", max(xs) + m * 1.4, 1), None]
    out = []
    for i in range(4):
        a, b, c = poly[i - 1], poly[i], poly[(i + 1) % 4]
        lim = limits[i]
        if lim is None:
            k = CHAMFER * min(np.linalg.norm(a - b), np.linalg.norm(c - b))
            out += [b + (a - b) / np.linalg.norm(a - b) * k, b + (c - b) / np.linalg.norm(c - b) * k]
            continue
        axis, target, sign = lim
        j = 0 if axis == "x" else 1
        if (b[j] - target) * sign <= 0:     # the corner is inside the limit: a small chamfer
            k = CHAMFER * min(np.linalg.norm(a - b), np.linalg.norm(c - b))
            out += [b + (a - b) / np.linalg.norm(a - b) * k, b + (c - b) / np.linalg.norm(c - b) * k]
            continue
        for e in (a, c):
            t = (target - b[j]) / ((e[j] - b[j]) or 1e-9)
            out.append(b + (e - b) * min(max(t, 0.0), 0.45))
    poly = out
    return [(x / PW_, y / PH_) for x, y in poly] + [(poly[0][0] / PW_, poly[0][1] / PH_)]


def straight_path(poly, step=2.0):
    """A straight-edged closed polygon resampled every `step` px, split into (front, back): back is what lies beyond
    the line from its leftmost to its rightmost point (the far edges, where the district's buildings can stand in
    front)."""
    px = [(x * PW_, y * PH_) for x, y in poly]
    L = min(px)
    R = max(px)
    front, back = [], []
    for (ax, ay), (bx, by) in zip(px, px[1:]):
        n = max(1, int(math.hypot(bx - ax, by - ay) / step))
        mx, my = (ax + bx) / 2, (ay + by) / 2
        far = my < L[1] + (R[1] - L[1]) * (mx - L[0]) / ((R[0] - L[0]) or 1) - 1
        part = back if far else front
        seg = [((ax + (bx - ax) * k / n) / PW_, (ay + (by - ay) * k / n) / PH_, 1.0) for k in range(n + 1)]
        if part and part[-1][:2] != seg[0][:2]:
            part.append((*part[-1][:2], 0.0))   # breaks between runs are drawn as gaps of zero alpha
            part.append((*seg[0][:2], 0.0))
        part += seg
    return front, back


def seen_parts(cfg):
    """(front, back) of the straight boundary: front drawn as is, back only where nothing stands in front."""
    if cfg.get("straight", False):
        return straight_path(boundary(cfg))
    return seen_parts_traced(cfg)


def seen_parts_traced(cfg):
    """(traced, back): the traced run solid end to end, and the closing stretch through the back corner (overlapping
    the run's last points so the two meet). A loop traced all round has no back part; `open` districts keep the run
    alone, fading at its ends."""
    if "edge" not in cfg:
        return seen_path(cfg), []
    run = list(cfg["edge"][0])
    if run[0] == run[-1]:
        return traced_path([run]), []
    if cfg.get("open"):
        return traced_path([run]), []
    solid = [(x, y, 1.0) for x, y, _ in traced_path([run])]
    back = cfg.get("back") or corners(cfg)[1]
    closing = traced_path([[run[-2], run[-1], tuple(back), run[0], run[1]]])
    closing = [(x, y, 1.0) for x, y, _ in closing]
    return solid, closing


def chaikin(pts, rounds=2, closed=False):
    """Corner cutting: the hand-traced polyline smoothed into a curve, its ends kept (a closed loop stays closed)."""
    for _ in range(rounds):
        out = [] if closed else [pts[0]]
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            out += [(ax * 0.75 + bx * 0.25, ay * 0.75 + by * 0.25), (ax * 0.25 + bx * 0.75, ay * 0.25 + by * 0.75)]
        pts = out + [out[0]] if closed else out + [pts[-1]]
    return pts


EDGE_FADE = 0.008  # length (image widths) over which an open run fades out at its ends, where it goes out of sight


def traced_path(runs):
    """Each traced run smoothed and resampled as (x, y, alpha), fading over EDGE_FADE at its ends. A run that ends
    where it starts is a closed loop and stays solid all round."""
    out = []
    for run in runs:
        closed = run[0] == run[-1]
        pts = chaikin(run, closed=closed)
        dist = [0.0]
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            dist.append(dist[-1] + math.hypot(bx - ax, (by - ay) * 2731 / 4096))
        total = dist[-1]
        count = max(2, int(total / 0.0008))
        j = 0
        if out:
            out.append((*out[-1][:2], 0.0))  # a break between runs
        for k in range(count + 1):
            s = total * k / count
            while j < len(dist) - 2 and dist[j + 1] < s:
                j += 1
            u = (s - dist[j]) / ((dist[j + 1] - dist[j]) or 1)
            e = 1.0 if closed else max(0.0, min(1.0, min(s, total - s) / EDGE_FADE))
            out.append((pts[j][0] + (pts[j + 1][0] - pts[j][0]) * u, pts[j][1] + (pts[j + 1][1] - pts[j][1]) * u,
                        e * e * (3 - 2 * e)))
    return out


NEON = "Tools/Art/painted/ui/neon"


def neon_line(i, x0, y0, x1, y1):
    """District i's painted neon line (z<i>_line.png, made by neon/batch.py), as alpha over the painting rectangle
    x0..x1, y0..y1 (px), or None if it has none."""
    import json
    if not os.path.exists(f"{NEON}/z{i}_line.png"):
        return None
    bx0, by0, bx1, by1 = json.load(open(f"{NEON}/z{i}_box.json"))["box"]
    a = Image.open(f"{NEON}/z{i}_line.png").split()[3].resize((bx1 - bx0, by1 - by0), Image.LANCZOS)
    full = Image.new("L", (x1 - x0, y1 - y0), 0)
    full.paste(a, (bx0 - x0, by0 - y0))
    return np.asarray(full).astype(np.float32) / 255


def poly_mask(pts, w, h):
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).polygon([(x * w, y * h) for x, y in pts], fill=255)
    return np.asarray(m) > 0


# ---------------------------------------------------------------- build

def height_map(depth):
    """How far each pixel stands out of the ground: depth minus the ground's depth on that row (a low percentile)."""
    ground = np.percentile(depth, 20, axis=1)
    ground = np.convolve(np.pad(ground, 20, mode="edge"), np.ones(41) / 41, mode="valid")
    return depth - ground[:, None]


def silhouette(cfg, h, depth, lum):
    """Ground plus everything standing on it, at the depth map's size.

    From each column's far ground edge it climbs while the pixel belongs to the district: it stands out of the ground
    (depth) and has not dropped back to the city behind (a fall in depth below the column's peak), or it glows like the
    district's own lights (thin spires the depth map blurs away). `extra` polygons add anything still missed.
    """
    H, W = h.shape
    foot = poly_mask(ground_outline(cfg), W, H)
    tall = max(0.015, np.percentile(h[foot], 60) * 0.4)
    bright = np.percentile(lum[foot], 55)
    rise = int(cfg.get("rise", 0.1) * H)
    sil = foot.copy()
    for x in np.where(foot.any(axis=0))[0]:
        top = np.where(foot[:, x])[0].min()
        peak = depth[top, x]
        gap = 0
        for y in range(top - 1, max(0, top - rise), -1):
            d = depth[y, x]
            standing = h[y, x] > tall and d > peak - 0.05
            glowing = lum[y, x] > bright
            if standing or glowing:
                sil[y, x] = True
                peak = max(peak, d)
                gap = 0
            else:
                gap += 1
                if gap > cfg.get("gap", 10):
                    break
    for poly in cfg.get("extra", []):
        sil |= poly_mask(poly, W, H)
    img = Image.fromarray((sil * 255).astype(np.uint8))
    img = img.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))  # close small gaps
    img = img.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))  # drop specks
    return np.asarray(img) > 127


def hull(mask, rect):
    """A simple outline of the mask for hit testing (image fractions)."""
    from skimage import measure
    H, W = mask.shape
    contour = max(measure.find_contours(np.pad(mask, 1).astype(float), 0.5), key=len)
    simple = measure.approximate_polygon(contour, tolerance=3)[:-1]
    return [((c - 1) / W, (r - 1) / H) for r, c in simple]


def build(only=None, preview=None):
    if not os.path.exists(DEPTH):
        sys.exit(f"missing {DEPTH}: run with --depth first (see the header)")
    paint = Image.open(PAINTING).convert("RGB")
    PW, PH = paint.size
    depth_img = Image.open(DEPTH)
    depth = np.asarray(depth_img).astype(np.float32) / 65535
    DH, DW = depth.shape
    h = height_map(depth)
    lum_small = np.asarray(paint.convert("L").resize((DW, DH), Image.BILINEAR)).astype(np.float32) / 255
    lum_small = np.asarray(Image.fromarray((lum_small * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5))).astype(np.float32) / 255
    # full-size height map for the boundary's occlusion
    h_full = np.asarray(Image.fromarray(h).resize((PW, PH), Image.BILINEAR))
    os.makedirs(OUT, exist_ok=True)
    lines = {}
    if os.path.exists(f"{OUT}/layers.txt"):
        for line in open(f"{OUT}/layers.txt", encoding="utf-8"):
            if line.strip():
                lines[int(line.split()[0])] = line.rstrip("\n")
    pa = np.asarray(paint).astype(np.float32) / 255
    for i, cfg in DISTRICTS.items():
        if only and i not in only:
            continue
        sil_small = silhouette(cfg, h, depth, lum_small)
        sil = Image.fromarray((sil_small * 255).astype(np.uint8)).resize((PW, PH), Image.BILINEAR)
        sil = sil.filter(ImageFilter.GaussianBlur(1.5))
        edge_pts = ground_outline(cfg)
        # the layers' rectangle: the silhouette and the boundary with room for its glow
        ys, xs = np.where(np.asarray(sil) > 8)
        ex = [p[0] * PW for p in edge_pts]
        ey = [p[1] * PH for p in edge_pts]
        whole = neon_line(i, 0, 0, PW, PH)   # room for the painted line too
        if whole is not None:
            ly, lx = np.where(whole > 0.02)
            if len(lx):
                ex += [lx.min() + 40, lx.max() - 40]
                ey += [ly.min() + 40, ly.max() - 40]
        pad = 40
        x0 = int(max(0, min(xs.min(), min(ex)) - pad))
        y0 = int(max(0, min(ys.min(), min(ey)) - pad))
        x1 = int(min(PW, max(xs.max(), max(ex)) + pad))
        y1 = int(min(PH, max(ys.max(), max(ey)) + pad))
        cw, ch = x1 - x0, y1 - y0

        alpha = np.asarray(sil.crop((x0, y0, x1, y1))).astype(np.float32) / 255
        rgb = pa[y0:y1, x0:x1]
        Image.fromarray((np.dstack([rgb, alpha]) * 255).astype(np.uint8), "RGBA").save(f"{OUT}/z{i}_cut.png")
        # lit: lifted shadows and stronger lights, a soft bloom on the bright windows
        lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
        lit = 1 - (1 - rgb) * (1 - rgb * 0.75)
        bloom = np.asarray(Image.fromarray((np.clip((lum - 0.5) * 2, 0, 1) * 255).astype(np.uint8))
                           .filter(ImageFilter.GaussianBlur(6))).astype(np.float32)[..., None] / 255
        lit = np.clip(lit + bloom * 0.35, 0, 1)
        Image.fromarray((np.dstack([lit, alpha]) * 255).astype(np.uint8), "RGBA").save(f"{OUT}/z{i}_lit.png")

        # edge: a glow along the part of the ground edge the camera sees (fading where it runs behind buildings),
        # thinner far away (top) and thicker near (bottom)
        def stroke(width_near, blur, path):
            layer = Image.new("L", (cw, ch), 0)
            d = ImageDraw.Draw(layer)
            for (ax, ay, aa), (bx, by, ba) in zip(path, path[1:]):
                al = (aa + ba) / 2
                if al <= 0.01:
                    continue
                yfrac = ((ay + by) / 2 + y0) / PH
                w = max(1, round(width_near * (0.55 + 0.75 * yfrac)))
                v = round(255 * al)
                d.line([(ax, ay), (bx, by)], fill=v, width=w)
                d.ellipse([bx - w / 2, by - w / 2, bx + w / 2, by + w / 2], fill=v)
            return layer.filter(ImageFilter.GaussianBlur(blur)) if blur else layer

        def draw(pts):
            path = [(x * PW - x0, y * PH - y0, al) for x, y, al in pts]
            st = {**LINE, **cfg.get("line", {})}
            core = np.asarray(stroke(st["core"], st["core_blur"], path)).astype(np.float32) / 255
            glow = np.asarray(stroke(st["glow"], st["glow_blur"], path)).astype(np.float32) / 255
            return np.clip(core + glow * st["glow_gain"], 0, 1)

        # the traced part is drawn as traced; the back closing it (see loop) only where nothing stands in front
        front, back = seen_parts(cfg)
        line = draw(front)
        if not back:
            back_line = None
        else:
            back_line = draw(back)
        # hide it where the district's buildings stand in front: silhouette above the ground, carried down a little
        # so it covers the line at the buildings' feet
        foot = poly_mask(ground_outline(cfg), PW, PH)[y0:y1, x0:x1]
        above = (np.asarray(sil)[y0:y1, x0:x1] > 127) & ~foot
        cover = above.copy()
        for k in range(1, 14):
            cover[k:] |= above[:-k]
        # and wherever the line would sit on something standing out of the ground (a roof, not the road)
        hh = h_full[y0:y1, x0:x1]
        tall = max(0.02, np.percentile(hh[foot], 60) * 0.5)
        cover |= hh > tall
        cover = np.asarray(Image.fromarray((cover * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3))) / 255
        if back_line is not None:
            line = np.maximum(line, back_line * (1 - cover))
        # the painted neon line (Tools/Art/painted/ui/neon: GPT Image drew it into a crop of the painting, the
        # difference kept as the line) replaces the drawn one where it exists
        painted = neon_line(i, x0, y0, x1, y1)
        if painted is not None:
            line = painted
        white = np.ones((ch, cw, 3), np.float32)
        Image.fromarray((np.dstack([white, line]) * 255).astype(np.uint8), "RGBA").save(f"{OUT}/z{i}_edge.png")

        hit = hull(np.asarray(sil) > 127, None)
        lines[i] = f"{i} {x0 / PW:.5f} {y0 / PH:.5f} {x1 / PW:.5f} {y1 / PH:.5f} ; " + \
                   " ".join(f"{x:.4f} {y:.4f}" for x, y in hit)
        print(f"{i} {NAMES[i]}: {cw}x{ch}, hull {len(hit)} points")
    with open(f"{OUT}/layers.txt", "w", encoding="utf-8", newline="\n") as f:
        for i in sorted(lines):
            f.write(lines[i] + "\n")

    if preview:
        img = paint.convert("RGBA")
        for i in sorted(lines):
            _, x0, y0, x1, y1 = lines[i].split(";")[0].split()
            box = (round(float(x0) * PW), round(float(y0) * PH))
            edge = Image.open(f"{OUT}/z{i}_edge.png")
            r, g, b, a = edge.split()
            img.alpha_composite(Image.merge("RGBA", (r.point(lambda v: 120), g.point(lambda v: 240), b, a)), box)
        img.convert("RGB").resize((2048, round(PH * 2048 / PW))).save(preview)


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--cs" in args:
        f = lambda v: f"{v:.4f}".rstrip("0").rstrip(".") + "f"
        for i, cfg in DISTRICTS.items():
            body = ", ".join(f"V({f(x)}, {f(y)})" for x, y in ground_outline(cfg))
            print(f"            new[] {{ {body} }},   // {i} {NAMES[i]}")
        sys.exit()
    if "--depth" in args:
        make_depth()
        sys.exit()
    only = None
    if "--only" in args:
        k = args.index("--only")
        only = {int(a) for a in args[k + 1:] if a.isdigit()}
        args = args[:k]
    build(only, args[0] if args else None)
