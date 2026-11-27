"""City picture v9b (2026-11-27): sharper buildings. The final picture is in the Unity camera's own frame (the v9 home
view: dist 238, target y 0.45, tilt 8), cut into 4 x 4 overlapping tiles. Each tile is drawn by GPT Image from two
references: the Unity model rendered for exactly that tile at 4K (shapes: straight walls, true footprints and
heights) and the v9 day picture warped into the camera's frame and cropped to the tile (look: colours, materials,
light). Paid: ask before generating.

    uv run --with opencv-python-headless --with numpy --with pillow python Tools/Art/city_v9b.py look        # free
    python Tools/Art/city_v9b.py gen <i ...>       # day tiles (needs shape_<i>.png from the Unity snippet)
    python Tools/Art/city_v9b.py night <i ...>     # the same tiles at night
    python Tools/Art/city_v9b.py stitch            # free: tiles -> day_8k.png / night_8k.png
    python Tools/Art/city_v9b.py install           # free: copies them to Assets/Resources/Macro (then city_v9_zones.py v9b)

Shape tiles: run Tools/Art/city_tile_capture.cs.txt through Tools/Unity/exec.sh while ModelCity.unity plays, with the
params line 'x0 y0 w h 3840 2160 <out>' from `python Tools/Art/city_v9b.py params <i>`.
Since the picture is in the camera's frame, Tools/Art/painted/city_v9/zones.txt fits it without any homography.
"""
import os
import subprocess
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GEN = "C:/Users/eh080/.claude/plugins/cache/wuyoscar-skills/gpt-image/0.3.0/skills/gpt-image/scripts/generate.py"
OUT = os.path.join(ROOT, "Tools", "Art", "painted", "city_v9b")
V9 = os.path.join(ROOT, "Tools", "Art", "painted", "city_v9")
W, H = 3840, 2160
GRID, TW, TH = 4, 1056, 594
TILES = [(round(c * (W - TW) / (GRID - 1)), round(r * (H - TH) / (GRID - 1))) for r in range(GRID) for c in range(GRID)]
# the city plate's corners on v9 day.png (1920 x 1080), as in city_v9_zones.py
DAY = [(175, 244), (1730, 243), (1908, 1032), (6, 1032)]

DAYP = (
    "Finish this tile of an aerial city render. The FIRST reference is a plain 3D model of exactly this view: keep its "
    "camera, framing (the image edges are the same edges), and every building's exact shape, footprint, height, "
    "straight edges and position, every street and plaza; ignore its tiny white specks. The SECOND reference is "
    "ONLY a style sample (its buildings are arranged differently, do not follow its layout): take from it the "
    "colours, facade materials, late-afternoon light, glass, rooftop details and mood. Render it as a crisp, high-end architectural visualisation: clean straight walls, regular window grids "
    "with mullions and reflections, floor slabs, balcony rails, rooftop units, solar panel cells, planters, street "
    "markings, tiny pods and people. No painterly brush strokes, no smearing, no melted shapes. Do not add, remove or "
    "reshape any building. No text, no labels."
)
# the top row shows the ground beyond the city: no buildings there, only haze
TOP = (" Keep every building of the first reference, including the tall towers at the far edge of the city. Only the "
       "empty ground beyond the city is to be a soft, hazy, featureless plain fading into warm late-afternoon mist, with "
       "no skyline and nothing that draws the eye.")
NIGHTP = (
    "The same image at night: keep EVERY building, street and detail exactly in place (same framing), only change the "
    "time to a clear night. Deep blue night haze, warm window lights in about half the windows, cyan light strips and "
    "drone-pad rings glowing, the AI centre's core and ring bright cyan if visible, streams of tiny pod lights along "
    "streets, street lights on the avenues, the elevated line glowing, solar fields dark, soft bloom. Calm, not neon "
    "cyberpunk. Crisp, no smearing. No text, no labels."
)


def look():
    """v9 day picture warped into the Unity camera's frame, then cut into look tiles."""
    import cv2
    import numpy as np
    plate = next(l.split() for l in open(os.path.join(V9, "zones.txt"), encoding="utf-8") if l.startswith("plate"))
    cap = [(float(plate[1 + i * 2]) * 1920, float(plate[2 + i * 2]) * 1080) for i in range(4)]
    m = cv2.getPerspectiveTransform(np.float32(DAY), np.float32(cap))   # day -> camera frame (1920 x 1080)
    day = np.asarray(Image.open(os.path.join(V9, "day.png")).convert("RGB"))
    s = W / 1920
    m = np.diag([s, s, 1]) @ m @ np.diag([1 / s, 1 / s, 1])          # the same map at 4K
    warped = cv2.warpPerspective(day, m, (W, H), flags=cv2.INTER_LANCZOS4, borderMode=cv2.BORDER_REPLICATE)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(warped).save(os.path.join(OUT, "look_full.png"))
    for i, (x, y) in enumerate(TILES):
        Image.fromarray(warped[y:y + TH, x:x + TW]).resize((1920, 1080), Image.LANCZOS).save(os.path.join(OUT, f"look_{i}.png"))
    print("look tiles written")


def run(prompt, refs, path):
    with open(path[:-4] + ".prompt.txt", "w", encoding="utf-8") as f:
        f.write(prompt)
    if os.path.exists(path):
        print(os.path.basename(path), "exists")
        return
    args = ["uv", "run", GEN, "--model", "gpt-image-2.6-flare", "-p", prompt, "--size", f"{W}x{H}", "--quality", "high", "-f", path]
    for r in refs:
        args += ["-i", r]
    r = subprocess.run(args, capture_output=True, text=True)
    print(os.path.basename(path), r.returncode, r.stderr[-400:] if r.returncode else "ok", flush=True)


def gen(ids, night=False):
    for i in ids:
        if night:
            run(NIGHTP + (" The hazy plain beyond the city stays dark and empty." if TILES[i][1] == 0 else ""),
                [os.path.join(OUT, f"day_{i}.png")], os.path.join(OUT, f"night_{i}.png"))
            continue
        shape = os.path.join(OUT, f"shape_{i}.png")
        small = os.path.join(OUT, f"shape_{i}_1920.png")
        if not os.path.exists(small):
            Image.open(shape).resize((1920, 1080), Image.LANCZOS).save(small)
        prompt = DAYP + (TOP if TILES[i][1] == 0 else "")
        run(prompt, [small, os.path.join(OUT, f"look_{i}.png")], os.path.join(OUT, f"day_{i}.png"))


def stitch(prefix, out):
    import numpy as np
    S = 2
    acc = np.zeros((H * S, W * S, 3), np.float32)
    wsum = np.zeros((H * S, W * S, 1), np.float32)
    tw, th, ramp = TW * S, TH * S, 48 * S
    wx = np.minimum(1, np.minimum(np.arange(tw) + 1, tw - np.arange(tw)) / ramp)
    wy = np.minimum(1, np.minimum(np.arange(th) + 1, th - np.arange(th)) / ramp)
    for i, (x, y) in enumerate(TILES):
        tile = np.asarray(Image.open(os.path.join(OUT, f"{prefix}_{i}.png")).convert("RGB").resize((tw, th), Image.LANCZOS), np.float32)
        ax, ay = wx.copy(), wy.copy()
        if x == 0: ax[:ramp] = 1
        if x + TW >= W: ax[-ramp:] = 1
        if y == 0: ay[:ramp] = 1
        if y + TH >= H: ay[-ramp:] = 1
        w = (ay[:, None] * ax[None, :])[..., None]
        acc[y * S:y * S + th, x * S:x * S + tw] += tile * w
        wsum[y * S:y * S + th, x * S:x * S + tw] += w
    Image.fromarray((acc / np.maximum(wsum, 1e-6)).clip(0, 255).astype(np.uint8)).save(os.path.join(OUT, out))
    print("stitched", out)


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    ids = [int(a) for a in sys.argv[2:]] or list(range(len(TILES)))
    if cmd == "look":
        look()
    elif cmd == "params":
        x, y = TILES[ids[0]]
        print(f"{x / W:.5f} {y / H:.5f} {TW / W:.5f} {TH / H:.5f} 3840 2160 {os.path.join(OUT, f'shape_{ids[0]}.png').replace(os.sep, '/')}")
    elif cmd == "gen":
        gen(ids)
    elif cmd == "night":
        gen(ids, night=True)
    elif cmd == "install":
        import shutil
        dst = os.path.join(ROOT, "Assets", "Resources", "Macro")
        shutil.copyfile(os.path.join(OUT, "day_8k.png"), os.path.join(dst, "city_main.png"))
        shutil.copyfile(os.path.join(OUT, "night_8k.png"), os.path.join(dst, "city_main_night.png"))
        print("installed; now run city_v9_zones.py v9b")
    elif cmd == "stitch":
        stitch("day", "day_8k.png")
        if all(os.path.exists(os.path.join(OUT, f"night_{i}.png")) for i in range(len(TILES))):
            stitch("night", "night_8k.png")
    else:
        print(__doc__)
