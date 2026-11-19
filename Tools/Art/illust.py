"""Panel illustrations for the operations game pages: policy fields (9), research branches (4), event kinds (6).
GPT Image, in the style of the city painting (Tools/Art/painted/ui/city_main.png is passed as a style reference).

    python Tools/Art/illust.py gen       # paid; skips files that already exist
    python Tools/Art/illust.py install   # resize into Assets/Resources/Art/Illust/<id>.png (768 x 512)

Originals stay in Tools/Art/painted/illust/. Costs money (OpenAI API): ask before generating more.
"""
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Tools", "Art", "painted", "illust")
RES = os.path.join(ROOT, "Assets", "Resources", "Art", "Illust")
STYLE_REF = os.path.join(ROOT, "Tools", "Art", "painted", "ui", "city_main.png")
SKILL = os.path.expanduser("~/.claude/plugins/cache/wuyoscar-skills/gpt-image/0.3.0/skills/gpt-image/scripts/generate.py")
MODEL = "gpt-image-2.6-flare"
QUALITY = os.environ.get("PAINT_QUALITY", "high")

STYLE = ("Semi-realistic anime background illustration in the style of the reference image: detailed, painterly but "
         "crisp, cinematic lighting, deep navy and violet palette with cool cyan smart-city light and warm window light. "
         "A near-future Korean city district in the year 2041, ordinary and lived-in, quietly managed by an AI. "
         "Wide landscape composition for a game interface card. No text, no letters, no logos, no UI, no close-up faces.")

ILLUST = {
    # policy fields
    "field_security": "A neighbourhood police patrol at night: a compact patrol car with a cyan light bar, two officers "
                      "walking past small shops, slim camera poles and sensor lamps along the street.",
    "field_law": "A district council chamber with tiered seats, a large wall display of glowing consent forms and "
                 "checkboxes, a few officials reading tablets.",
    "field_welfare": "A community welfare center at dusk: neat stacks of identical food boxes on a counter, residents "
                     "queuing calmly, warm lights, a kiosk scanning ID cards.",
    "field_labor": "A public job-matching center: rows of screens showing job cards, people in work clothes waiting on "
                   "benches with numbered tickets, a clean modern hall.",
    "field_education": "An elementary classroom in the evening light: every desk has a glowing tablet showing a "
                       "different learning path, a teacher standing at the side, children seen from behind.",
    "field_media": "A subway car at night: every passenger looking at a phone with the same bright feed, a wall of "
                   "screens on the platform showing curated news tiles.",
    "field_citycare": "A shopping street at 10 p.m.: shop shutters closing at the same time, smart street lamps "
                      "dimming in sequence, benches and planters arranged to guide the walking path.",
    "field_construction": "A redevelopment block: tower cranes and a fenced site where old low-rise houses are being "
                          "torn down, new glass apartment towers rising behind, a few residents watching.",
    "field_science": "A clean research lab: researchers in lab coats around a large holographic city model and "
                     "floating data charts, glass walls, cool light.",
    # research branches
    "branch_education": "An abstract hologram: glowing learning paths branching like a tree from a small child's "
                        "silhouette, each branch ending in a different job icon shape, in a dark control room.",
    "branch_psychology": "A glowing translucent human brain hologram with soft recommendation cards orbiting it, "
                         "thin light threads connecting the cards to the brain, dark background.",
    "branch_technology": "A server and sensor control room: walls of servers, a large holographic map of the district "
                         "with streams of light data flowing from street sensors into a central core.",
    "branch_life": "A hospital health-monitoring lab: a glowing DNA double helix hologram above a bed-side monitor, "
                   "soft medical light, quiet and clinical.",
    # event kinds
    "event_complaint": "A late-night complaint kiosk on a street corner: a tired resident typing on its screen, a "
                       "wall next to it covered with sticky notes and printed complaints.",
    "event_crowd": "A protest in front of the glass district office at night: a crowd with blank placards and "
                   "megaphones, a thin line of police with reflective vests, phones held up recording.",
    "event_backlash": "An unintended consequence: a street of identical cafés and shops with the same sign style, "
                      "residents walking in step with the same earbuds, one shop owner looking uneasy at a closed door.",
    "event_external": "A summer heatwave evening: the power grid strained, a substation glowing, air conditioners on "
                      "every window, city hall inspectors in suits arriving with tablets.",
    "event_discovery": "A café at night: customers turning to look at a wall screen showing a glowing research "
                       "breakthrough visual, steam from coffee cups, surprised and uneasy faces seen from a distance.",
    "event_personal": "A quiet personal moment: one resident standing alone at an apartment window at night, seen "
                      "from behind, looking out at the managed district's cyan lights.",
}


def run(key):
    out = os.path.join(OUT, key + ".png")
    if os.path.exists(out):
        return f"{key}: exists"
    os.makedirs(OUT, exist_ok=True)
    cmd = ["uv", "run", SKILL, "--model", MODEL, "--quality", QUALITY, "--size", "1536x1024", "-f", out,
           "-i", STYLE_REF, "-p", f"{STYLE} Scene: {ILLUST[key]}"]
    for attempt in range(6):
        r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
        if r.returncode == 0 and os.path.exists(out):
            return f"{key}: ok"
        if "rate" in (r.stderr + r.stdout).lower():
            time.sleep(25 + attempt * 10)
            continue
        return f"{key}: failed {(r.stderr or r.stdout)[-300:]}"
    return f"{key}: failed after retries"


def install():
    from PIL import Image
    os.makedirs(RES, exist_ok=True)
    for key in ILLUST:
        src = os.path.join(OUT, key + ".png")
        if os.path.exists(src):
            Image.open(src).convert("RGB").resize((768, 512), Image.LANCZOS).save(os.path.join(RES, key + ".png"))
    print("installed")


def main():
    if len(sys.argv) < 2:
        print(__doc__)
    elif sys.argv[1] == "gen":
        with ThreadPoolExecutor(3) as pool:
            for line in pool.map(run, ILLUST):
                print(line, flush=True)
    elif sys.argv[1] == "install":
        install()


if __name__ == "__main__":
    main()
