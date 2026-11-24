"""Building sprites for the district screen (OpsDistrictUi.cs).

Sources are GPT Image renders in Tools/Art/painted/district/<id>.png (prompts beside them): one building on its square
plot, true 2:1 isometric, transparent background, the plot's corners touching the image's sides and bottom.
This trims the transparent margin (so the image's width is the plot diamond's width and its bottom the plot's front
corner, which is how the screen places it) and installs Resources/Art/District/<id>.png.

    python Tools/Art/district_sprites.py
"""
import glob
import os

from PIL import Image

SRC = "Tools/Art/painted/district"
OUT = "Assets/Resources/Art/District"
WIDTH = 512

os.makedirs(OUT, exist_ok=True)
for path in sorted(glob.glob(f"{SRC}/*.png")):
    name = os.path.splitext(os.path.basename(path))[0]
    if name.startswith("ref_"):
        continue
    im = Image.open(path).convert("RGBA")
    alpha = im.split()[3].point(lambda v: 255 if v > 24 else 0)
    box = alpha.getbbox()
    im = im.crop(box)
    im = im.resize((WIDTH, round(im.height * WIDTH / im.width)), Image.LANCZOS)
    im.save(f"{OUT}/{name}.png")
    print(name, im.size)
