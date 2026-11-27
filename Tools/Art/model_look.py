"""Model view look (2026-11-27): GPT Image pictures that set how the main screen's 3D city model should look.
Paid: GPT Image. Ask before generating.

    python Tools/Art/model_look.py gen [keys...]    # skips existing files

The layout reference is a clean capture of the code model (Tools/Art/proto/city.html?hour=16&hud=0, cropped to
1536 x 1024 as painted/model/model_capture.png). Outputs go to Tools/Art/painted/model/<key>.png, prompt beside it.
"""
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GEN = "C:/Users/eh080/.claude/plugins/cache/wuyoscar-skills/gpt-image/0.3.0/skills/gpt-image/scripts/generate.py"
OUT = os.path.join(ROOT, "Tools", "Art", "painted", "model")

PROMPTS = {
    "model_target": (
        "A polished real-time 3D render of the city model on the main screen of a city-management game: SECTOR ALPHA, "
        "a near-future test city (2071), seen by its governing AI. The reference image is a rough code mock-up of this "
        "exact scene: keep its camera, framing, perspective and the layout exactly (the grid of square city blocks and "
        "streets, the wide avenues, the elevated ring line around the centre, the three-tier round AI centre tower with "
        "its glowing cyan ring and the plaza and long pool in front of it, the tall tower clusters, the solar fields and "
        "round battery tanks on the left, the stadium-like round green courts, the red-and-green sports fields, the "
        "domed research buildings, the construction site with cranes at the back right, the dense housing towers on the "
        "right), but render every building as a believable, finely detailed architectural model: white and pale grey "
        "facades with real window grids, terraces, rooftop gardens and solar canopies, glass in blue-cyan, thin cyan "
        "light strips, drone pads on roofs, small trees almost absent (a few hologram trees). Streets with tiny glowing "
        "pods, the elevated maglev guideway on slim pillars. Late afternoon: warm low sunlight from the left, long soft "
        "shadows, ambient occlusion, a soft bloom on the cyan lights, the first window lights coming on, light haze "
        "with distance. Clean stylised realism like a high-end modern city-builder, not photorealistic, not pixel art, "
        "not cyberpunk, calm and orderly. The upper part of the picture stays a calm hazy sky over a far horizon with "
        "the faint silhouette of the low outside city (it will hold interface data later). No text, no labels, no "
        "interface, no people visible at this distance."
    ),
}

TEX = (
    " It is a texture for a 3D game: a perfectly flat, straight-on orthographic view (no perspective, no vanishing "
    "lines, no shadows from the camera, no sky, no ground, no frame around it), evenly lit, filling the whole square "
    "edge to edge, and SEAMLESSLY TILEABLE: the left edge continues into the right edge and the top into the bottom. "
    "Near-future 2071 architecture in the look of the reference picture (white and pale grey, blue-cyan glass, thin "
    "cyan light strips), clean stylised realism, not photographic noise, not pixel art. No text, no logos, no people."
)
PROMPTS.update({
    "tex_facade_glass": (
        "A building facade texture: exactly 8 columns and 8 rows of identical-size window bays in a regular grid (each "
        "bay one window, so 64 windows), slim white mullions and floor slabs between them, blue-grey reflective glass "
        "with a faint sky gradient, every second floor slab carrying a very thin pale cyan light line. Office and "
        "research towers." + TEX
    ),
    "tex_facade_home": (
        "A residential building facade texture: exactly 8 columns and 8 rows of identical-size bays in a regular grid "
        "(64 bays), each bay a window or glass door behind a shallow white balcony with a glass rail; a few balconies "
        "with small potted plants and warm light-wood soffits; white and very pale warm-grey walls. Apartment towers "
        "of a calm, well-kept new city." + TEX
    ),
    "tex_roof": (
        "A flat rooftop texture seen from directly above: pale grey roof membrane with service hatches, small "
        "ventilation units, thin solar canopies in neat rows, a small square drone pad with a cyan ring, a few planter "
        "beds of low grass, access walkways. Evenly spread details so it tiles without an obvious centre." + TEX
    ),
})


def gen(keys):
    os.makedirs(OUT, exist_ok=True)
    for key in keys:
        path = os.path.join(OUT, f"{key}.png")
        with open(path[:-4] + ".prompt.txt", "w", encoding="utf-8") as f:
            f.write(PROMPTS[key])
        if os.path.exists(path):
            print(key, "exists")
            continue
        # the look target is the reference for the textures; the code capture is the layout for the target
        extra = ["-i", os.path.join(OUT, "model_target.png" if key.startswith("tex_") else "model_capture.png")]
        r = subprocess.run(["uv", "run", GEN, "--model", "gpt-image-2.6-flare", "-p", PROMPTS[key], *extra,
                            "--size", "1024x1024" if key.startswith("tex_") else "1536x1024", "--quality", "medium", "-f", path], capture_output=True, text=True)
        print(key, r.returncode, r.stderr[-400:] if r.returncode else "ok", flush=True)


if __name__ == "__main__":
    if len(sys.argv) >= 2 and sys.argv[1] == "gen":
        gen(sys.argv[2:] or list(PROMPTS))
    else:
        print(__doc__)
