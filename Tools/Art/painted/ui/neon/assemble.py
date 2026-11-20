"""Put every district's extracted line (z<i>_line.png, crop space) back at full size over the untouched painting.

Writes, into Tools/Art/drafts/: v26_neon_lines.png (all lines, transparent, 4096 x 2731, white with alpha; tinted
here only for the preview), v26_neon.png (preview, 2048 wide, each line in its colour), v26_neon_full.png.
    python Tools/Art/painted/ui/neon/assemble.py
"""
import importlib.util
import json
import os

import numpy as np
from PIL import Image

spec = importlib.util.spec_from_file_location("dl", "Tools/Art/district_layers.py")
dl = importlib.util.module_from_spec(spec)
spec.loader.exec_module(dl)
N = "Tools/Art/painted/ui/neon"

base = Image.open(dl.PAINTING).convert("RGB")
W, H = base.size
out = np.asarray(base).astype(np.float32) / 255
layer = np.zeros((H, W, 4), np.float32)
for i in range(11):
    if not os.path.exists(f"{N}/z{i}_line.png"):
        continue
    x0, y0, x1, y1 = json.load(open(f"{N}/z{i}_box.json"))["box"]
    a = Image.open(f"{N}/z{i}_line.png").split()[3].resize((x1 - x0, y1 - y0), Image.LANCZOS)
    a = np.asarray(a).astype(np.float32) / 255
    col = np.array(dl.COLORS[i], np.float32) / 255
    region = out[y0:y1, x0:x1]
    # additive glow: the core goes towards white, the halo towards the colour
    lit = np.clip(region + col * a[..., None] * 1.1 + a[..., None] ** 2 * 0.35, 0, 1)
    out[y0:y1, x0:x1] = lit
    layer[y0:y1, x0:x1, 3] = np.maximum(layer[y0:y1, x0:x1, 3], a)
layer[..., :3] = 1
Image.fromarray((layer * 255).astype(np.uint8), "RGBA").save("Tools/Art/drafts/v26_neon_lines.png")
img = Image.fromarray((out * 255).astype(np.uint8))
img.save("Tools/Art/drafts/v26_neon_full.png")
img.resize((2048, round(H * 2048 / W)), Image.LANCZOS).save("Tools/Art/drafts/v26_neon.png")
print("ok")
