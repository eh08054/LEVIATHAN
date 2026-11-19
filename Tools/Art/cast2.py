"""Character and simulation art for the operations game, in the style of the two reference images
(Tools/Art/refs/style_lineup.png for full-body lineups, style_portrait.png for half-body portraits). GPT Image.

    python Tools/Art/cast2.py lineup      # full-body lineups: lineup_a (c01-c05), lineup_b (c06-c10)
    python Tools/Art/cast2.py portraits   # half-body portraits, neutral (each from its lineup crop)
    python Tools/Art/cast2.py faces       # happy / sad edits of each portrait
    python Tools/Art/cast2.py sprites     # pixel walking sheets: c01-c10 and generic residents s1-s6
    python Tools/Art/cast2.py scenes      # pixel scenes for the simulation panel (street, office) after plaza_v2
    python Tools/Art/cast2.py install     # crop/resize into Assets/Resources/Art/Cast and Art/Sim

Every step skips files that already exist. Generation costs money (OpenAI API): ask before running more.
Originals stay in Tools/Art/painted/cast2/ and Tools/Art/painted/sim/.
"""
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(ROOT, "Tools", "Art")
CAST_DIR = os.path.join(ART, "painted", "cast2")
SIM_DIR = os.path.join(ART, "painted", "sim")
REFS = os.path.join(ART, "refs")
RES = os.path.join(ROOT, "Assets", "Resources", "Art")
SKILL = os.path.expanduser("~/.claude/plugins/cache/wuyoscar-skills/gpt-image/0.3.0/skills/gpt-image/scripts/generate.py")
MODEL = "gpt-image-2.6-flare"
QUALITY = os.environ.get("PAINT_QUALITY", "high")

# Look, pose for the lineup, mood. Ordinary 2041 people; one thing each you can spot from across the plaza.
CAST = {
    "c01": ("32-year-old man, district office landscaping officer. Messy brown hair, thin rectangular glasses, green work "
            "vest with a small district emblem over a white shirt with rolled-up sleeves, dark work trousers, brown work "
            "shoes, gardening gloves tucked in a pocket", "earnest, a little anxious"),
    "c02": ("27-year-old woman, food delivery rider. Wavy wine-red hair tied back, light freckles, black and teal delivery "
            "jacket with reflective stripes, phone holder strap across the chest, cargo pants, sneakers", "tired, sharp"),
    "c03": ("68-year-old man, retired local newspaper reporter. Weathered tan face, grey hair under an olive flat cap, grey "
            "moustache, slightly stooped, brown corduroy jacket over a dark sweater, reading glasses on a cord, a small "
            "notebook in the breast pocket", "skeptical, warm underneath"),
    "c04": ("24-year-old woman, young job seeker. Short choppy hair dyed blue, small red ear stud, oversized plum hoodie, "
            "black jeans, headphones around the neck", "restless, unsure"),
    "c05": ("41-year-old man, head of the local merchants' association who runs an alley restaurant. Broad and a little "
            "heavy, slicked-back black hair, stubble, dark shirt with rolled sleeves under a navy apron, a red armband",
            "proud, defiant"),
    "c06": ("35-year-old woman, expectant mother, visibly pregnant (about seven months). Chestnut hair in a low bun with a "
            "teal ribbon, soft coral knit cardigan, teal scarf, long cream skirt, flat shoes", "gentle, quietly worried"),
    "c07": ("45-year-old woman, city hall supervisor assigned to oversee the AI pilot. Neat black bob, thin black-framed "
            "glasses, grey trouser suit, city hall ID lanyard, a tablet", "composed, tired, precise"),
    "c08": ("38-year-old man, neighbourhood police officer. Broad build, short black hair, navy Korean police uniform with "
            "a cap, a reflective vest and a radio on the shoulder", "steady, kind, conflicted"),
    "c09": ("29-year-old woman, elementary school teacher. Brown ponytail with a mustard scrunchie, teal cardigan over a "
            "cream blouse, beige slacks, a school name-tag lanyard", "bright, caring"),
    "c10": ("22-year-old man, street musician. Lanky, messy hair dyed blond, red hoodie, ripped black jeans, high-top "
            "sneakers, over-ear headphones around the neck, an acoustic guitar on his back", "dreamy, eager"),
}
LINEUP_POSE = {
    "c01": "hands in pockets", "c02": "a motorcycle helmet held under one arm", "c03": "leaning on a wooden cane",
    "c04": "a tote bag full of résumés on one shoulder", "c05": "arms crossed",
    "c06": "one hand resting on her belly", "c07": "holding a tablet against her chest",
    "c08": "standing straight, thumbs in his duty belt", "c09": "holding a stack of picture books",
    "c10": "one hand on the guitar strap",
}
LINEUPS = {"lineup_a": ["c01", "c02", "c03", "c04", "c05"], "lineup_b": ["c06", "c07", "c08", "c09", "c10"]}

# Generic residents: pixel sprites only (odd = women).
GENERIC = {
    "s1": "a woman in her twenties, office worker, light beige trench coat over a blouse, black slacks, shoulder bag, long dark hair",
    "s2": "a man in his fifties, salaryman, grey suit, loosened navy tie, briefcase, short greying hair",
    "s3": "a woman in her seventies, quilted purple padded vest, sun visor, floral trousers, pulling a small shopping trolley",
    "s4": "a teenage boy, school uniform with a navy hoodie under the blazer, backpack, earbuds",
    "s5": "a woman in her forties, denim jacket, striped shirt, wide trousers, a tote bag, shoulder-length wavy hair",
    "s6": "a man in his thirties, black hoodie and baseball cap, joggers, sneakers, a backpack, holding a coffee cup",
}

PIXEL_SHEET = ("Pixel art character sprite sheet for a three-quarter top-down pixel-art city game. Chibi-leaning but not "
               "super-deformed proportions, about 2.5 heads tall. Exactly 4 columns and 3 rows on a transparent "
               "background, every cell the same size, the character centered in each cell with feet on the same "
               "baseline, same scale in every cell. Row 1: walking toward the camera, 4 frames (contact, passing, "
               "contact, passing). Row 2: walking to the right, 4 frames. Row 3: walking away from the camera (back "
               "view), 4 frames. Crisp hand-placed pixels with a visible pixel grid, limited palette, dark outline, no "
               "anti-aliasing blur, no shadows on the ground, no text, no grid lines.")


def lineup_prompt(ids):
    positions = ["1 (far left)", "2", "3 (center)", "4", "5 (far right)"]
    people = " ".join(f"{positions[i]}: {CAST[c][0]}, {LINEUP_POSE[c]}. {CAST[c][1].capitalize()}." for i, c in enumerate(ids))
    return ("Match the art style of the reference image exactly: the same semi-realistic anime game illustration "
            "rendering, fine line art with soft painterly shading, muted navy-violet palette with small accent colors, "
            "soft even front lighting, realistic adult proportions (7 to 7.5 heads tall), and the same dark navy "
            "background with a faint blueprint grid and thin cyan HUD corner brackets. Draw NEW people, not the ones "
            "in the reference. Character lineup sheet: exactly 5 full-body characters standing in one row, evenly "
            "spaced, each centered in its own fifth of the image, same scale, feet on the same baseline, neutral "
            "standing poses facing slightly toward the viewer, a faint shadow under the feet. They are ordinary "
            "everyday Korean people in the year 2041, NOT idealized models: show their real age, body type and "
            f"tiredness. {people} No text, no names, no labels, no logos.")


def portrait_prompt(cid):
    look, mood = CAST[cid]
    return ("Match the art style of the first reference image: semi-realistic anime game illustration, fine line art "
            "with soft painterly rendering, glossy fabric highlights, desaturated navy and violet palette, cool rim "
            "light and soft bloom, blurred blue city architecture with faint data panels in the background. Draw the "
            "same person as in the second reference image: same face, hair, clothes, accessories, age and build "
            f"({look}). Keep their ordinary look exactly as in the second image: do NOT beautify, do NOT make them "
            "younger or slimmer, do NOT change their clothes into techwear. Half-body portrait from the waist up, tall "
            "vertical framing, the head in the upper third, slight low angle, three-quarter view, looking just past "
            f"the viewer, mouth closed. Expression: {mood}. No text, no logos.")


FACES = {
    "happy": "a warm, genuine smile with the mouth gently curved, eyes slightly narrowed, relaxed brows",
    "sad": "downcast eyes, brows drawn together and raised in the middle, mouth closed and turned slightly down, close to tears but composed",
}


def face_prompt(key):
    return ("Edit this image. Keep everything identical: the same person, face shape, hair, glasses and clothes, the "
            "same pose, framing, background, art style and lighting. Change only the facial expression to: "
            f"{FACES[key]}. Keep the identity exactly the same.")


def sprite_prompt(desc, from_reference):
    who = ("the person in the second reference image (" + desc + ")") if from_reference else desc
    return (f"{PIXEL_SHEET} The character is {who}. Match the pixel art style, proportions and level of detail of the "
            "first reference image (a sprite sheet of another character).")


def scene_prompt(place):
    return ("Same pixel art style, the same three-quarter top-down camera angle, the same evening light and the same "
            "era as the reference image (a futuristic, fully managed smart district: glass canopies, solar trees, "
            "glowing LED ribbons in the paving, sleek modern facades, a monorail at the back). Draw a different place in "
            f"the same district: {place} Keep a wide open walkable area in the middle and lower part of the image for "
            "characters. Crisp hand-placed pixels with a visible pixel grid, limited palette, clean outlines, no "
            "anti-aliasing blur, no people, no characters, no text, no letters.")


SCENES = {
    "street_v2": "a pedestrian commercial street lined with small shops and cafés on both sides, glowing holographic "
                 "shop signs, outdoor seating, planters and benches down the middle, delivery lockers, a crosswalk.",
    "office_v2": "the front plaza of the modern glass district office that houses the city's AI operations center: wide "
                 "steps, a reception canopy, a big public screen on the facade, security gates, neat planting, benches.",
    # 2026-11-18: the city has six zones, all watched from the start.
    "homes_v2": "the courtyard of a mid-rise apartment complex: low hedges, a small playground, benches under trees, "
                "parcel lockers, bicycle racks, balconies with laundry and plants, warm windows.",
    "school_v2": "the street in front of an elementary school and a row of after-school academies: a school gate with "
                 "a crossing guard post, a small yard fence, academy signs, a bus stop, a stationery shop.",
    "works_v2": "a logistics and power yard at the edge of the district: stacked containers, a loading dock with "
                "delivery robots parked, a fenced electrical substation with humming transformers, cooling units, "
                "warehouse doors, safety lines on the ground.",
    # 2026-11-18: eleven districts; camera scenes for the ones where people show up.
    "lab_v2": "the courtyard of a research campus: glass lab buildings, a data center wall with glowing server racks "
              "seen through windows, rooftop cooling units, a sculpture of a DNA helix, benches and bike racks.",
    "clinic_v2": "the front of a modern hospital and care center: an ambulance bay with a parked autonomous ambulance, "
                 "a glass entrance with a canopy, a small healing garden, wheelchairs, a pharmacy kiosk.",
    "police_v2": "the forecourt of a district police station and public safety control center: patrol cars, a "
                 "drone landing pad with two drones, antennas on the roof, barriers, a big wall of monitors visible inside.",
}


def run(out, prompt, refs=(), size="1024x1024", background=None):
    if os.path.exists(out):
        return f"{os.path.basename(out)}: exists"
    os.makedirs(os.path.dirname(out), exist_ok=True)
    cmd = ["uv", "run", SKILL, "--model", MODEL, "--quality", QUALITY, "--size", size, "-f", out, "-p", prompt]
    for r in refs:
        cmd += ["-i", r]
    if background:
        cmd += ["--background", background]
    for attempt in range(6):
        r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
        if r.returncode == 0 and os.path.exists(out):
            return f"{os.path.basename(out)}: ok"
        if "rate" in (r.stderr + r.stdout).lower():
            time.sleep(25 + attempt * 10)
            continue
        return f"{os.path.basename(out)}: failed {(r.stderr or r.stdout)[-300:]}"
    return f"{os.path.basename(out)}: failed after retries"


def crop_path(cid):
    return os.path.join(CAST_DIR, f"{cid}_full.png")


def make_crops():
    from PIL import Image
    for name, ids in LINEUPS.items():
        path = os.path.join(CAST_DIR, name + ".png")
        if not os.path.exists(path):
            continue
        img = Image.open(path)
        w, h = img.size
        for i, cid in enumerate(ids):
            if not os.path.exists(crop_path(cid)):
                img.crop((int(w * i / 5), int(h * 0.04), int(w * (i + 1) / 5), int(h * 0.96))).save(crop_path(cid))


def parallel(jobs, workers=3):
    with ThreadPoolExecutor(workers) as pool:
        for line in pool.map(lambda job: run(**job), jobs):
            print(line, flush=True)


def lineup():
    parallel([dict(out=os.path.join(CAST_DIR, n + ".png"), prompt=lineup_prompt(ids),
                   refs=[os.path.join(REFS, "style_lineup.png")], size="1536x1024") for n, ids in LINEUPS.items()], 2)
    make_crops()


def portraits():
    make_crops()
    parallel([dict(out=os.path.join(CAST_DIR, f"{c}_portrait.png"), prompt=portrait_prompt(c),
                   refs=[os.path.join(REFS, "style_portrait.png"), crop_path(c)], size="1024x1536") for c in CAST])


def faces():
    parallel([dict(out=os.path.join(CAST_DIR, f"{c}_{k}.png"), prompt=face_prompt(k),
                   refs=[os.path.join(CAST_DIR, f"{c}_portrait.png")], size="1024x1536") for c in CAST for k in FACES])


def sprites():
    style = os.path.join(SIM_DIR, "c01_sheet_test.png")
    jobs = [dict(out=os.path.join(SIM_DIR, f"{c}_sheet.png"), prompt=sprite_prompt(CAST[c][0], True),
                 refs=[style, crop_path(c)], size="1536x1024", background="transparent") for c in CAST]
    jobs += [dict(out=os.path.join(SIM_DIR, f"{s}_sheet.png"), prompt=sprite_prompt(d, False), refs=[style],
                  size="1536x1024", background="transparent") for s, d in GENERIC.items()]
    parallel(jobs)


def scenes():
    parallel([dict(out=os.path.join(SIM_DIR, f"{n}.png"), prompt=scene_prompt(p),
                   refs=[os.path.join(SIM_DIR, "plaza_v2.png")], size="1536x1024") for n, p in SCENES.items()], 2)


# ----------------------------------------------------------------- install

SIM_W, SIM_H = 768, 432   # simulation panel grid (pixels)
SPRITE_H = 60             # walking sprite height on that grid


def install():
    from PIL import Image
    cast_out = os.path.join(RES, "Cast")
    os.makedirs(cast_out, exist_ok=True)
    for c in CAST:
        for key in ("portrait", "happy", "sad"):
            src = os.path.join(CAST_DIR, f"{c}_{key}.png")
            if not os.path.exists(src):
                continue
            img = Image.open(src).convert("RGB")
            w, h = img.size
            face = "neutral" if key == "portrait" else key
            img.resize((512, 768), Image.LANCZOS).save(os.path.join(cast_out, f"{c}_{face}_tall.png"))
            # square bust from the upper part of the portrait
            img.crop((0, int(h * 0.02), w, int(h * 0.02) + w)).resize((384, 384), Image.LANCZOS).save(
                os.path.join(cast_out, f"{c}_{face}.png"))
        full = crop_path(c)
        if os.path.exists(full):
            img = Image.open(full).convert("RGB")
            img.thumbnail((384, 1024), Image.LANCZOS)
            img.save(os.path.join(cast_out, f"{c}_full.png"))

    sim_out = os.path.join(RES, "Sim")
    for name, zone in (("plaza_v2", "plaza"), ("street_v2", "cafe"), ("office_v2", "lobby"),
                       ("homes_v2", "homes"), ("school_v2", "school"), ("works_v2", "works"),
                       ("lab_v2", "lab"), ("clinic_v2", "clinic"), ("police_v2", "police")):
        src = os.path.join(SIM_DIR, name + ".png")
        if not os.path.exists(src):
            continue
        img = Image.open(src).convert("RGB")
        w, h = img.size
        crop_h = int(w * SIM_H / SIM_W)
        top = (h - crop_h) // 2
        img = img.crop((0, top, w, top + crop_h)).resize((SIM_W, SIM_H), Image.BOX)
        img.save(os.path.join(sim_out, f"{zone}_v2.png"))

    sprite_out = os.path.join(sim_out, "Sprites")
    os.makedirs(sprite_out, exist_ok=True)
    for sid in list(CAST) + list(GENERIC):
        src = os.path.join(SIM_DIR, f"{sid}_sheet.png" if sid != "c01" or os.path.exists(os.path.join(SIM_DIR, "c01_sheet.png"))
                           else "c01_sheet_test.png")
        if not os.path.exists(src):
            continue
        slice_sheet(Image.open(src).convert("RGBA"), sid, sprite_out)
    print("installed")


def slice_sheet(sheet, sid, out):
    """4 x 3 cells -> <sid>_<front|right|back><0-3>.png, all scaled by one factor so the tallest frame is SPRITE_H,
    each frame padded to a common canvas with the feet on the bottom row and the body centered."""
    from PIL import Image
    w, h = sheet.size
    cw, ch = w / 4, h / 3
    frames, boxes = {}, []
    for row, name in enumerate(("front", "right", "back")):
        for col in range(4):
            cell = sheet.crop((int(col * cw), int(row * ch), int((col + 1) * cw), int((row + 1) * ch)))
            alpha = cell.split()[3].point(lambda a: 255 if a > 40 else 0)
            box = alpha.getbbox()
            if not box:
                continue
            frames[f"{name}{col}"] = (cell, box)
            boxes.append(box)
    if not boxes:
        return
    tallest = max(b[3] - b[1] for b in boxes)
    widest = max(b[2] - b[0] for b in boxes)
    scale = SPRITE_H / tallest
    canvas_w = int(round(widest * scale)) + 4
    for key, (cell, (x0, y0, x1, y1)) in frames.items():
        part = cell.crop((x0, y0, x1, y1))
        part = part.resize((max(1, round((x1 - x0) * scale)), max(1, round((y1 - y0) * scale))), Image.BOX)
        # clean the alpha so the pixels stay crisp
        r, g, b, a = part.split()
        part = Image.merge("RGBA", (r, g, b, a.point(lambda v: 255 if v > 110 else 0)))
        canvas = Image.new("RGBA", (canvas_w, SPRITE_H + 2), (0, 0, 0, 0))
        canvas.alpha_composite(part, ((canvas_w - part.width) // 2, SPRITE_H + 2 - part.height))
        canvas.save(os.path.join(out, f"{sid}_{key}.png"))


def main():
    steps = {"lineup": lineup, "portraits": portraits, "faces": faces, "sprites": sprites, "scenes": scenes,
             "install": install}
    if len(sys.argv) < 2 or sys.argv[1] not in steps:
        print(__doc__)
        return
    for step in sys.argv[1:]:
        steps[step]()


if __name__ == "__main__":
    main()
