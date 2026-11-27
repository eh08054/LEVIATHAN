"""Model view textures (2026-11-27, free): turns the GPT texture sheets (model_look.py gen tex_*) into tiling textures
for the Unity city model (Assets/ModelCity/Resources/ModelCity/).

    python Tools/Art/model_textures.py

Facades: the bay period is found by autocorrelation of the column/row brightness, the sheet is cropped to a whole
number of bays (so it tiles without a seam) and resized to 1024; the night map lights the glass of about half the
bays (warm, a few cool). bays.txt records how many bays each texture spans (ModelCity sets its UVs from it). The roof
is copied as is (Unity mirrors it when tiling).
"""
import os
import random

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Tools", "Art", "painted", "model")
DST = os.path.join(ROOT, "Assets", "ModelCity", "Resources", "ModelCity")


def period(profile, lo, hi):
    p = profile - profile.mean()
    best, bp = -1e18, lo
    for lag in range(lo, hi):
        c = float(np.dot(p[:-lag], p[lag:])) / (len(p) - lag)
        if c > best:
            best, bp = c, lag
    return bp


def facade(key, out):
    im = Image.open(os.path.join(SRC, key + ".png")).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    lum = a.mean(axis=2)
    w, h = im.size
    px = period(lum.mean(axis=0), w // 12, w // 3)
    py = period(lum.mean(axis=1), h // 14, h // 3)
    nx, ny = w // px, h // py
    # start on a mullion (the brightest column within the first period) so the cut falls on a frame line
    x0 = int(np.argmax(lum.mean(axis=0)[:px])); y0 = int(np.argmax(lum.mean(axis=1)[:py]))
    if x0 + nx * px > w: nx -= 1
    if y0 + ny * py > h: ny -= 1
    crop = im.crop((x0, y0, x0 + nx * px, y0 + ny * py)).resize((1024, 1024), Image.LANCZOS)
    crop.save(os.path.join(DST, out + ".png"))
    # night: glass is bluish and darker than the frames; light it per bay
    c = np.asarray(crop).astype(np.float32)
    glass = (c[..., 2] > c[..., 0] + 12) & (c.mean(axis=2) < 215)
    emit = np.zeros_like(c)
    rnd = random.Random(7)
    bw, bh = 1024 / nx, 1024 / ny
    for j in range(ny):
        for i in range(nx):
            if rnd.random() > 0.55:
                continue
            col = np.array([255, 196, 120]) if rnd.random() < 0.82 else np.array([215, 232, 255])
            col = col * (0.65 + rnd.random() * 0.35)
            ys, xs = slice(int(j * bh), int((j + 1) * bh)), slice(int(i * bw), int((i + 1) * bw))
            emit[ys, xs][glass[ys, xs]] = col
    Image.fromarray(emit.clip(0, 255).astype(np.uint8)).save(os.path.join(DST, out + "_lit.png"))
    print(key, "period", px, py, "bays", nx, ny, "crop at", x0, y0)
    return nx, ny


def main():
    os.makedirs(DST, exist_ok=True)
    lines = []
    for key, out in (("tex_facade_glass", "facade_glass"), ("tex_facade_home", "facade_home")):
        nx, ny = facade(key, out)
        lines.append(f"{out} {nx} {ny}")
    Image.open(os.path.join(SRC, "tex_roof.png")).convert("RGB").save(os.path.join(DST, "roof.png"))
    with open(os.path.join(DST, "bays.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
