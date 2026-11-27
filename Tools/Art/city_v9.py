"""City picture v9 (2026-11-27): the main screen's painting, closer than v8 so the city fills the screen (the outside
only a thin hazy band), and 8K so zooming in stays sharp. Paid: GPT Image, ask before generating.

    python Tools/Art/city_v9.py gen day          # one 3840 x 2160 picture over the Unity capture (layout)
    python Tools/Art/city_v9.py gen tiles        # day.png cut into 2 x 2 overlapping tiles, each redrawn at 3840 x 2160
    python Tools/Art/city_v9.py gen night        # the same four tiles at night
    python Tools/Art/city_v9.py stitch           # free: tiles -> day_8k.png / night_8k.png (7680 x 4320, feathered seams)
    python Tools/Art/city_v9.py install          # free: copies the 8K pictures to Assets/Resources/Macro/

Inputs in Tools/Art/painted/city_v9/: capture.png (Unity ModelCity, dist 238, target y 0.45, tilt 8, 16:9; rendered by
the editor snippet Tools/Art/city_capture.cs.txt) and zones.txt (block corners, roads and pins in image fractions).
"""
import os
import shutil
import subprocess
import sys

from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GEN = "C:/Users/eh080/.claude/plugins/cache/wuyoscar-skills/gpt-image/0.3.0/skills/gpt-image/scripts/generate.py"
OUT = os.path.join(ROOT, "Tools", "Art", "painted", "city_v9")
LOOK = os.path.join(ROOT, "Tools", "Art", "painted", "city_v8", "day.png")
W, H = 3840, 2160
# 2 x 2 tiles of the 16:9 picture with an overlap, each itself 16:9 so it can be redrawn at 3840 x 2160
TW, TH = 2112, 1188
TILES = [(0, 0), (W - TW, 0), (0, H - TH), (W - TW, H - TH)]

DAY = (
    "A polished, high-detail real-time 3D render of SECTOR ALPHA, a near-future test city (2071) governed by an AI, "
    "seen from high above as the main screen of a city-management game. The first reference is a rough 3D layout of "
    "this exact view: keep its camera, framing, perspective and layout EXACTLY, block for block (the grid of square "
    "blocks and streets, the wide avenues, the elevated ring line, the three-tier round AI centre tower with its "
    "glowing cyan ring, the plaza and long pool, the tower clusters, the solar fields and battery tanks on the left, "
    "the round green courts, the red-and-green sports fields, the domed research buildings and the construction site "
    "with cranes at the back right, the dense housing on the right). Every building stands where a box stands in the "
    "reference with about the same footprint and height. The second reference sets the look: draw it in that style "
    "and finish. The city fills the picture; beyond its far edge only a thin band of soft haze, no distant skyline, no "
    "landmarks outside, nothing that draws the eye away from the city. Late afternoon light, long soft shadows, "
    "first window lights, clean stylised realism, calm and orderly. No text, no labels, no interface."
)
TILE = (
    "This is one part of a larger city picture. Redraw it at higher resolution with much finer detail, keeping "
    "EVERYTHING exactly where it is: same framing (the edges of this image are the same edges), same buildings, "
    "streets, colours, light and shadows. Add only fine detail: crisp window grids, balcony rails, rooftop units, "
    "solar panel lines, tiny pods and street markings, people-scale details, sharp edges. Do not add, remove or move "
    "any building. No text, no labels."
)
NIGHT = (
    "The same image at night: keep EVERY building, street and detail exactly in place (same framing), only change the "
    "time to a clear night. Deep blue night haze, the city lit from within: warm window lights in about half the "
    "windows, cyan light strips and drone-pad rings glowing, the AI centre's core and ring bright cyan if visible, "
    "streams of tiny pod lights along streets, street lights on the avenues, the elevated line glowing, solar fields "
    "dark, soft bloom around lights. Calm, not neon cyberpunk. No text, no labels."
)


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
    print(os.path.basename(path), r.returncode, r.stderr[-500:] if r.returncode else "ok", flush=True)


def gen(what):
    os.makedirs(OUT, exist_ok=True)
    if what == "day":
        run(DAY, [os.path.join(OUT, "capture_1920.png"), LOOK], os.path.join(OUT, "day.png"))
    elif what in ("tiles", "night"):
        day = Image.open(os.path.join(OUT, "day.png")).convert("RGB")
        for i, (x, y) in enumerate(TILES):
            crop = os.path.join(OUT, f"crop_{i}.png")
            if not os.path.exists(crop):
                day.crop((x, y, x + TW, y + TH)).resize((1920, 1080), Image.LANCZOS).save(crop)
            if what == "tiles":
                run(TILE, [crop], os.path.join(OUT, f"tile_{i}.png"))
            else:
                run(NIGHT, [os.path.join(OUT, f"tile_{i}.png")], os.path.join(OUT, f"night_{i}.png"))


def stitch(prefix, out):
    """Each tile covers TW x TH of the 4K picture; at 8K that is 2*TW x 2*TH. Overlaps blend with a linear ramp."""
    import numpy as np
    S = 2
    acc = np.zeros((H * S, W * S, 3), np.float64)
    wsum = np.zeros((H * S, W * S, 1), np.float64)
    tw, th = TW * S, TH * S
    ramp = 160 * S
    wx = np.minimum(1, np.minimum(np.arange(tw) + 1, tw - np.arange(tw)) / ramp)
    wy = np.minimum(1, np.minimum(np.arange(th) + 1, th - np.arange(th)) / ramp)
    for i, (x, y) in enumerate(TILES):
        tile = np.asarray(Image.open(os.path.join(OUT, f"{prefix}_{i}.png")).convert("RGB").resize((tw, th), Image.LANCZOS), np.float64)
        # tiles at the picture's border keep full weight on that border
        ax = wx.copy(); ay = wy.copy()
        if x == 0: ax[:ramp] = 1
        if x + TW == W: ax[-ramp:] = 1
        if y == 0: ay[:ramp] = 1
        if y + TH == H: ay[-ramp:] = 1
        w = (ay[:, None] * ax[None, :])[..., None]
        acc[y * S:y * S + th, x * S:x * S + tw] += tile * w
        wsum[y * S:y * S + th, x * S:x * S + tw] += w
    img = (acc / np.maximum(wsum, 1e-6)).clip(0, 255).astype(np.uint8)
    Image.fromarray(img).save(os.path.join(OUT, out))
    print("stitched", out)


def install():
    dst = os.path.join(ROOT, "Assets", "Resources", "Macro")
    shutil.copyfile(os.path.join(OUT, "day_8k.png"), os.path.join(dst, "city_main.png"))
    if os.path.exists(os.path.join(OUT, "night_8k.png")):
        shutil.copyfile(os.path.join(OUT, "night_8k.png"), os.path.join(dst, "city_main_night.png"))
    print("installed")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "gen":
        gen(sys.argv[2])
    elif cmd == "stitch":
        stitch("tile", "day_8k.png")
        if all(os.path.exists(os.path.join(OUT, f"night_{i}.png")) for i in range(4)):
            stitch("night", "night_8k.png")
    elif cmd == "install":
        install()
    else:
        print(__doc__)
