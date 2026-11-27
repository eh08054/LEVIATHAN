"""UI kit v2 (2026-11-27): game-HUD pieces for the B layout (Tools/Art/proto/ui_mock.html?v=b), made with GPT Image.
Paid: ask before generating.

    python Tools/Art/ui_kit2.py gen [keys...]   # skips existing; sheets go to Tools/Art/painted/ui_kit2/<key>.png
    python Tools/Art/ui_kit2.py cut             # free: cuts each sheet into its pieces by alpha (parts/<key>_<n>.png + contact sheet)
    python Tools/Art/ui_kit2.py install         # free: copies the chosen pieces to Assets/Resources/UiKit2/ (see PICK)

References: the B mock-up capture (shapes and layout) and the model look target (colour and mood).
"""
import os
import subprocess
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GEN = "C:/Users/eh080/.claude/plugins/cache/wuyoscar-skills/gpt-image/0.3.0/skills/gpt-image/scripts/generate.py"
OUT = os.path.join(ROOT, "Tools", "Art", "painted", "ui_kit2")
MOCK = os.path.join(ROOT, "Tools", "Art", "painted", "ui_mock", "ui_b.png")
LOOK = os.path.join(ROOT, "Tools", "Art", "painted", "model", "model_target.png")

STYLE = (
    "Game UI art for a near-future city-management game where the player is the city's governing AI (2071). "
    "Polished, premium sci-fi game HUD like a modern strategy game: dark navy translucent glass (#0A1A30 to #132C4C) "
    "with a thin bright cyan rim light (#35C9E8), subtle bevels, soft inner glow, small precise tech details (tick "
    "marks, notches, tiny corner brackets), clean and calm, not cluttered, not grungy, not cyberpunk neon. "
    "Flat front view, no perspective, no text, no letters, no numbers, no icons inside the pieces, transparent "
    "background around every piece, each piece separate with generous empty space between pieces."
)
PROMPTS = {
    "kit": (
        "A sprite sheet of separate empty HUD frames, laid out in a loose grid: (1) a large rounded-rectangle panel "
        "frame with an empty dark glass centre, (2) a wide notification card frame with a round socket on its left "
        "for a picture, (3) a square command button tile with rounded corners, rotated 45 degrees like a diamond, "
        "dark blue, (4) the same diamond button in its active state, bright blue glass with a strong blue glow, "
        "(5) a circular gauge bezel: a ring with tick marks and an empty dark centre, (6) a wide pill-shaped warning "
        "banner frame in amber and dark brown with a round socket on its left, (7) a wide horizontal plaque for a "
        "clock, symmetrical with small wings, (8) a round resource chip, pill-shaped with a round icon socket. "
        "The first reference shows where these pieces sit on the screen; the second sets colour and mood. " + STYLE
    ),
    "emblem": (
        "The emblem of LEVIATHAN, the city's governing AI: a circular emblem, a glowing pale-cyan eye-like core in the "
        "centre, concentric rings of fine tech segments around it, one thin outer ring that could double as a progress "
        "gauge, a calm watchful and slightly unsettling presence, elegant and simple enough to read at small size. "
        "Centred, filling most of the square. " + STYLE
    ),
    "bezel": (
        "A monitor bezel frame for the moment the player switches to the AI's 3D model view: a wide screen frame of "
        "dark brushed metal and dark glass with thin cyan light lines along its inner edge, small mechanical details "
        "at the four corners, a slim instrument strip along the bottom edge with a few empty slots; the large screen "
        "area inside the frame is completely EMPTY and TRANSPARENT (a 16:9 opening), and the area outside the frame "
        "is transparent too. Symmetrical, front view, the frame fills the whole image width. " + STYLE
    ),
}
SIZE = {"kit": "1536x1024", "emblem": "1024x1024", "bezel": "1536x1024"}


def gen(keys):
    os.makedirs(OUT, exist_ok=True)
    for key in keys:
        path = os.path.join(OUT, f"{key}.png")
        with open(path[:-4] + ".prompt.txt", "w", encoding="utf-8") as f:
            f.write(PROMPTS[key])
        if os.path.exists(path):
            print(key, "exists")
            continue
        refs = ["-i", MOCK, "-i", LOOK] if key == "kit" else ["-i", LOOK]
        r = subprocess.run(["uv", "run", GEN, "--model", "gpt-image-2.6-flare", "-p", PROMPTS[key], *refs,
                            "--size", SIZE[key], "--quality", "high", "--background", "transparent", "-f", path],
                           capture_output=True, text=True)
        print(key, r.returncode, r.stderr[-400:] if r.returncode else "ok", flush=True)


def cut():
    """Cut the kit sheet into its pieces: connected areas of alpha > 24 (8-neighbour), bigger than 40 x 40."""
    import numpy as np
    from scipy import ndimage
    os.makedirs(os.path.join(OUT, "parts"), exist_ok=True)
    for key in ("kit",):
        im = Image.open(os.path.join(OUT, f"{key}.png")).convert("RGBA")
        a = np.asarray(im)[..., 3] > 24
        a = ndimage.binary_closing(a, iterations=3)
        lab, n = ndimage.label(a, structure=np.ones((3, 3)))
        boxes = [b for b in ndimage.find_objects(lab) if (b[0].stop - b[0].start) > 40 and (b[1].stop - b[1].start) > 40]
        boxes.sort(key=lambda b: (b[0].start // 200, b[1].start))
        sheet = im.copy(); d = ImageDraw.Draw(sheet)
        for i, b in enumerate(boxes):
            x0, y0, x1, y1 = b[1].start, b[0].start, b[1].stop, b[0].stop
            im.crop((x0, y0, x1, y1)).save(os.path.join(OUT, "parts", f"{key}_{i}.png"))
            d.rectangle((x0, y0, x1, y1), outline=(255, 60, 60, 255), width=3); d.text((x0 + 6, y0 + 4), str(i), fill=(255, 255, 0, 255))
        sheet.save(os.path.join(OUT, f"{key}_parts.png"))
        print(key, len(boxes), "parts")


# kit_<n> piece → installed name (the cut order of the 2026-11-27 sheet)
PICK = {0: "panel", 1: "card", 2: "button_on", 3: "button", 4: "gauge", 5: "banner", 6: "plaque", 7: "chip"}


def install():
    dst = os.path.join(ROOT, "Assets", "Resources", "UiKit2")
    os.makedirs(dst, exist_ok=True)
    for i, name in PICK.items():
        Image.open(os.path.join(OUT, "parts", f"kit_{i}.png")).save(os.path.join(dst, name + ".png"))
    Image.open(os.path.join(OUT, "emblem.png")).resize((384, 384), Image.LANCZOS).save(os.path.join(dst, "emblem.png"))
    Image.open(os.path.join(OUT, "bezel.png")).save(os.path.join(dst, "bezel.png"))
    print("installed", len(PICK) + 2, "pieces")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "gen":
        gen(sys.argv[2:] or list(PROMPTS))
    elif cmd == "cut":
        cut()
    elif cmd == "install":
        install()
    else:
        print(__doc__)
