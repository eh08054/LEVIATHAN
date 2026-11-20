# extract.py i : the neon line from z<i>_gen vs z<i>_src -> z<i>_line.png (white + alpha, crop size) and checks
import sys, json, importlib.util
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
spec = importlib.util.spec_from_file_location("dl", "Tools/Art/district_layers.py"); dl = importlib.util.module_from_spec(spec); spec.loader.exec_module(dl)
i = int(sys.argv[1]); N = f"Tools/Art/painted/ui/neon/z{i}"
import os
if not os.path.exists(N + "_src.png"):   # the crop is not kept in git: cut it again from the painting
    Image.open(dl.PAINTING).convert("RGB").crop(json.load(open(N + "_box.json"))["box"]).resize((1536, 1024), Image.LANCZOS).save(N + "_src.png")
src = np.asarray(Image.open(N + "_src.png").convert("RGB")).astype(np.float32) / 255
gen = np.asarray(Image.open(N + "_gen.png").convert("RGB").resize((1536, 1024))).astype(np.float32) / 255
box = json.load(open(N + "_box.json"))
lum = lambda a: a @ np.array([0.299, 0.587, 0.114], np.float32)
# the original, spread a little so a slight shift of its own lights does not count
# brightness the edit added; noise from re-rendering is fine-grained, the line is a long smooth ridge
gain = np.clip(lum(gen) - lum(src), 0, 1)
gain = np.asarray(Image.fromarray((gain * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255
# warm, saturated glow (the line's colour), not grey re-rendering noise
r, g, b = gen[..., 0], gen[..., 1], gen[..., 2]
a = np.clip((gain - 0.07) * 4.5, 0, 1)
# only near the district's outline (prior band)
W, H = 4096, 2731; s = box["scale"]; x0, y0 = box["box"][:2]
pts = [((x*W-x0)*s, (y*H-y0)*s) for x, y in dl.ground_outline(dl.DISTRICTS[i]) + dl.ground_outline(dl.DISTRICTS[i])[:1]]
band = Image.new("L", (1536, 1024), 0); ImageDraw.Draw(band).line(pts + [pts[0]], fill=255, width=110, joint="curve")
band = np.asarray(band.filter(ImageFilter.GaussianBlur(15))).astype(np.float32) / 255
a = a * band
a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(0.8))).astype(np.float32) / 255
# keep only long pieces (the line), drop specks of re-rendering noise
from skimage import measure, morphology
lab = measure.label(morphology.binary_dilation(a > 0.25, morphology.disk(4)))
keep = np.zeros_like(a, bool)
for rp in measure.regionprops(lab):
    if rp.major_axis_length > 60:
        keep |= lab == rp.label
keep = np.asarray(Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3))).astype(np.float32) / 255
a = a * np.clip(keep * 1.5, 0, 1)
Image.fromarray((np.dstack([np.ones_like(a)] * 3 + [a]) * 255).astype(np.uint8), "RGBA").save(N + "_line.png")
# checks: the mask alone, and the line laid back on the untouched original
Image.fromarray((a * 255).astype(np.uint8)).save(N + "_alpha.png")
col = np.array([1.0, 0.88, 0.6], np.float32)
over = src * (1 - a[..., None]) + np.clip(src + col * 1.0, 0, 1) * a[..., None]
Image.fromarray((over * 255).astype(np.uint8)).save(N + "_onsrc.png")
print("ok", a.max(), (a > 0.2).sum())
