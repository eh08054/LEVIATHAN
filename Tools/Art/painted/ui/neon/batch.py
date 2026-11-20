"""Neon boundary lines painted by GPT Image, one district at a time (see Dialog 2026-11-20).

For each district: crop the painting around it (3:2, scaled to 1536x1024), ask for an unmasked edit that only adds
the line, then extract.py keeps what the edit brightened near the district's outline as the line layer.
    python Tools/Art/painted/ui/neon/batch.py 0 1 2 ...     # skips districts whose _gen.png exists
"""
import json, subprocess, sys, importlib.util
from PIL import Image
spec = importlib.util.spec_from_file_location("dl", "Tools/Art/district_layers.py"); dl = importlib.util.module_from_spec(spec); spec.loader.exec_module(dl)
GEN = "C:/Users/eh080/.claude/plugins/cache/wuyoscar-skills/gpt-image/0.3.0/skills/gpt-image/scripts/generate.py"
N = "Tools/Art/painted/ui/neon"
WHAT = {
    0: ("warm yellow", "the round culture plaza in the middle (curved building with big screens around a crystal fountain)",
        "along the inner curb of the ring road that circles the plaza, all the way round"),
    1: ("magenta", "the commercial block lit in pink and magenta with a hologram spire in its plaza", None),
    2: ("teal", "the central AI tower standing on the roundabout island", "around the outer edge of the roundabout island"),
    4: ("violet", "the education campus with the sports field and the glowing geodesic dome", None),
    5: ("golden", "the logistics yard with warehouses, stacked containers and helipads", None),
    6: ("orange", "the power plant with glowing orange domes and smoking chimneys", None),
    7: ("sky-blue", "the research campus with glass domes and tall blue towers", None),
    8: ("mint green", "the hospital campus with the helipad cross and green gardens", None),
    9: ("blue", "the security compound with searchlight towers and a drone pad", None),
    10: ("silver-white", "the construction site with cranes and a deep round pit", None),
}
DEFAULT_PATH = ("exactly along the outer edge of its sidewalk, where the sidewalk meets the road, following the "
                "block's real outline in perspective, and around the far side only where the ground is visible "
                "between buildings")


def crop_box(i):
    pts = dl.ground_outline(dl.DISTRICTS[i])
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1 = min(xs) - 0.035, max(xs) + 0.035
    y0, y1 = min(ys) - dl.DISTRICTS[i].get("rise", 0.1) * 0.8, max(ys) + 0.04
    W, H = 4096, 2731
    w, h = (x1 - x0) * W, (y1 - y0) * H
    if w / h < 1.5: w = h * 1.5
    else: h = w / 1.5
    cx, cy = (x0 + x1) / 2 * W, (y0 + y1) / 2 * H
    bx0 = min(max(0, cx - w / 2), W - w); by0 = min(max(0, cy - h / 2), H - h)
    return [round(bx0), round(by0), round(bx0 + w), round(by0 + h)]


for i in map(int, sys.argv[1:]):
    out = f"{N}/z{i}"
    box = crop_box(i)
    im = Image.open(dl.PAINTING).convert("RGB")
    im.crop(box).resize((1536, 1024), Image.LANCZOS).save(out + "_src.png")
    json.dump({"box": box, "scale": 1536 / (box[2] - box[0])}, open(out + "_box.json", "w"))
    colour, what, path = WHAT[i]
    prompt = (f"Add one thing to this painting: a thin, crisp {colour} neon boundary line lying flat on the ground "
              f"around {what}. The line runs {path or DEFAULT_PATH}. Bright thin core, soft gentle glow with a faint "
              "reflection on the wet asphalt. Where a building or tree stands in front of the line, the line is "
              "hidden behind it. The line never floats and never crosses roofs or walls. Change nothing else: keep "
              "every building, sidewalk, tree, road, light and the composition, framing and painting style exactly "
              "the same.")
    open(out + ".prompt.txt", "w", encoding="utf-8").write(prompt)
    import os
    if not os.path.exists(out + "_gen.png"):
        r = subprocess.run(["uv", "run", GEN, "--model", "gpt-image-2.6-flare", "-p", prompt, "-i", out + "_src.png",
                            "--size", "1536x1024", "--quality", "high", "-f", out + "_gen.png"], capture_output=True, text=True)
        print(i, "gen", r.returncode, r.stderr[-300:] if r.returncode else "", flush=True)
    r = subprocess.run([sys.executable, f"{N}/extract.py", str(i)], capture_output=True, text=True)
    print(i, "extract", r.stdout.strip(), r.stderr[-300:], flush=True)
