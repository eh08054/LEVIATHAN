"""Art-style trial ③-C (2026-11-25): pixel-art parts generated as sheets, assembled by code. Paid: GPT Image.

    python Tools/Art/pixel_parts.py gen [keys...]   # ref first (style reference), then sheets that follow it; skips existing
    python Tools/Art/pixel_parts.py cut             # no paid calls: snaps sheets to the pixel grid and cuts parts out
    python Tools/Art/pixel_parts.py mock | city     # no paid calls: block and city mock images
    python Tools/Art/pixel_parts.py install         # copies the parts into Assets/TrialPixel/Resources/PixelParts

Sheets go to Tools/Art/painted/pixel_parts/<key>.png (prompt beside it); cut parts to .../parts/<key>_<n>.png with a
numbered contact sheet. See Dialog 2026-11-25.
"""
import os
import subprocess
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GEN = "C:/Users/eh080/.claude/plugins/cache/wuyoscar-skills/gpt-image/0.3.0/skills/gpt-image/scripts/generate.py"
OUT = os.path.join(ROOT, "Tools", "Art", "painted", "pixel_parts")

STYLE = (
    "Crisp, clean pixel art for a calm city-management game, seen from a 3/4 top-down view (as in classic top-down "
    "pixel RPGs and city sims: we see roofs from above and the front facades facing us, no side faces, no isometric "
    "diamond). Every art pixel is a square block of exactly 4 x 4 image pixels, hard edges, no anti-aliasing, no "
    "blur, no gradients, a limited palette of about 32 colours. Near future, year 2041, calm and believable, NOT "
    "cyberpunk: white and pale grey buildings with softly rounded corners, deep terraces with plants, light warm "
    "wood accents, blue-cyan glass, thin solar canopies, rooftop gardens, an occasional thin cyan light strip. Soft "
    "daylight from the upper left, short shadows falling down-right. Gentle, slightly desaturated colours."
)
FOLLOW = ("\n\nThe reference image fixes the style: match its camera, pixel size, palette, light and level of detail "
          "exactly so every piece fits into that city.")
SHEET = ("\n\nThis is a SPRITE SHEET on a fully transparent background: separate pieces laid out in a tidy grid with "
         "wide empty transparent gaps between them, nothing touching, no labels, no text, no frame.")
PROMPTS = {
    "ref": STYLE + "\n\nA game screenshot of ONE city block filling the image: a few mid-rise apartment buildings of "
           "different heights and widths along the back, a row of two- and three-storey buildings with shops and "
           "cafes with awnings at street level along the front, a green courtyard strip with trees in between, "
           "sidewalks, a road with a crosswalk along the bottom, street lamps, a few parked autonomous pods and tiny "
           "pedestrians. No text, no UI.",
    "strips": STYLE + SHEET + "\n\nMODULAR BUILDING PIECES for stacking vertically into buildings of any height, "
              "all in the terraced white style. Three columns by width: narrow (about 16 art pixels), medium (about "
              "28) and wide (about 40). In each column, from top to bottom: (1) a ROOF piece: the flat roof seen "
              "from above with its parapet and the top edge of the facade; (2) a MIDDLE FLOOR piece: exactly one "
              "storey of facade with windows, a balcony rail and planters, designed to repeat seamlessly above and "
              "below itself; (3) a second MIDDLE FLOOR variant; (4) a GROUND FLOOR with a shop front, glass and an "
              "awning; (5) a GROUND FLOOR with a lobby entrance. All pieces in a column have exactly the same width.",
    "props": STYLE + SHEET + "\n\nSMALL PIECES to decorate the city, each separate: rooftop garden patches (3, "
             "different sizes), rooftop solar canopy panels (2), a rooftop drone landing pad, rooftop air units (2), "
             "street trees (4 kinds: round, tall, flowering, small), a bench, a street lamp, a planter box, a bus "
             "shelter with a small screen, a bicycle rack, a delivery robot, two small autonomous pods (cars) seen "
             "from the front and from the side.",
    "ground": STYLE + SHEET + "\n\nGROUND TILES seen from straight above, each a square tile of 32 x 32 art pixels "
              "that repeats seamlessly: asphalt road running left-right with a dashed lane line, the same road "
              "running up-down, a road crossing, a crosswalk, pale paved sidewalk, plaza paving with a subtle pattern, "
              "grass, grass with small flowers, bare earth of an empty lot, gravel, a pond piece with water, a sports "
              "field piece. Also one curb strip piece between sidewalk and road.",
}


# Stage 2: more facade styles, landmarks, construction states, transit, and a night copy of the strips sheet.
STRIP_LAYOUT = ("\n\nThe second reference image is an existing sheet of terraced pieces: copy its exact layout, piece "
                "sizes and storey height (three widths, five rows: roof, middle floor, middle floor variant, ground "
                "floor shop, ground floor lobby), only change the architecture as described.")
PROMPTS.update({
    "strips_glass": STYLE + SHEET + "\n\nMODULAR BUILDING PIECES in a GLASS style: continuous horizontal bands of "
                    "blue-cyan glass between slim white floor slabs, softly rounded white corners, a thin cyan light "
                    "line on some slabs, a few indoor plants visible behind the glass." + STRIP_LAYOUT,
    "strips_wood": STYLE + SHEET + "\n\nMODULAR BUILDING PIECES in a WARM WOOD style: pale white walls with "
                   "vertical light-wood louvres, deep recessed windows, small balconies with climbing plants, a "
                   "rounded wooden canopy over the entrances." + STRIP_LAYOUT,
    "tower": STYLE + SHEET + "\n\nONE large sprite: the city's operations tower, the tallest building in the "
             "district (about four times as tall as a five-storey apartment), slim and elegant with softly rounded "
             "corners, white and pale glass, two thin cyan light rings around its upper part, a small garden plaza "
             "at its foot, an antenna at the top. Quiet and refined, not a glowing beacon. Same 3/4 top-down view.",
    "facilities_a": STYLE + SHEET + "\n\nTHREE large city-facility sprites, each standing on its own small plot: "
                    "(1) a compact clean-energy plant with a rounded white turbine hall, a battery block and solar "
                    "canopies; (2) a logistics hub with low white warehouses, loading bays, parked autonomous trucks "
                    "and a drone dock on the roof; (3) a research campus building with a curved glass atrium and "
                    "rooftop terraces.",
    "facilities_b": STYLE + SHEET + "\n\nTHREE large city-facility sprites, each standing on its own small plot: "
                    "(1) a calm modern hospital with a white wing, a glass entrance hall and a small rooftop "
                    "helipad; (2) a neighbourhood security station, white and pale blue, low and friendly, with two "
                    "parked patrol pods; (3) a closed construction site for a future facility behind a neat tall "
                    "fence wrapped in a pale cyan mesh, a crane and a half-built frame inside.",
    "construction": STYLE + SHEET + "\n\nBUILDING STATES for one MEDIUM-width plot (about 28 art pixels wide), "
                    "same scale as the reference: (1) an empty lot with bare earth and a low fence; (2) the lot "
                    "excavated with a small digger; (3) a foundation slab with stacked materials; (4) a two-storey "
                    "concrete frame with scaffolding and a slim crane; (5) a nearly finished building wrapped in a "
                    "pale cyan mesh; (6) a vacant two-storey building with dark windows and a 'for lease' board "
                    "(no readable text); (7) a closed shop ground floor with a lowered shutter.",
    "transit": STYLE + SHEET + "\n\nTRANSIT PIECES: an elevated light-rail track segment seen from the front "
               "(slim white beam, repeatable left-right), a slim white pillar, a three-car light-rail train in white "
               "with a cyan stripe seen from the side (front car, middle car, rear car as separate pieces), a small "
               "electric bus seen from the side, three small autonomous cars seen from the side in white, pale blue "
               "and soft coral, a delivery drone, a bike-share bicycle.",
    "strips_night": "The reference image is a pixel-art sprite sheet of building pieces. Repaint EXACTLY the same "
                    "sheet, same pieces in the same places with the same pixel grid, but at NIGHT: walls and roofs in "
                    "calm dark blue-grey shades, about half of the windows lit with a soft warm yellow light showing "
                    "curtains and room interiors, the others dark with faint reflections, shop windows glowing warmly, "
                    "the thin cyan light lines glowing. Keep the transparent background.",
})
# Stage 3: the two other facade styles again, this time described in words only (a second reference sheet made
# the model copy its style too), and night copies of the landmark sheets.
LAYOUT_WORDS = ("\n\nLayout: three columns by width: narrow (about 16 art pixels), medium (about 28) and wide (about 40). "
                "In each column, from top to bottom: (1) a ROOF piece: the flat roof seen from above with its parapet "
                "and the top edge of the facade; (2) a MIDDLE FLOOR piece: exactly one storey, designed to repeat "
                "seamlessly above and below itself; (3) a second MIDDLE FLOOR variant; (4) a GROUND FLOOR with a shop "
                "front; (5) a GROUND FLOOR with a lobby entrance. All pieces in a column have exactly the same width. "
                "A storey is about 9 art pixels tall, like the buildings in the reference.")
NIGHT_EDIT = ("The reference image is a pixel-art sprite sheet. Repaint EXACTLY the same sheet, same pieces in the same "
              "places with the same pixel grid, but at NIGHT: walls and roofs in calm dark blue-grey shades, some "
              "windows lit with a soft warm yellow light, the others dark with faint reflections, entrance halls "
              "glowing warmly, small warm lamps along paths, the thin cyan light lines glowing softly. Keep the "
              "transparent background.")
PROMPTS.update({
    "strips_glass2": STYLE + SHEET + "\n\nMODULAR BUILDING PIECES in a GLASS OFFICE style, clearly different from "
                     "terraced apartments: no balconies and no planters on the floors; each storey is one continuous "
                     "band of blue-cyan glass from edge to edge between thin white floor slabs, slim vertical white "
                     "mullions, softly rounded white corners, a thin cyan light line along every second slab; the "
                     "ground floors are tall glass lobbies and glass shop fronts without awnings." + LAYOUT_WORDS,
    "strips_wood2": STYLE + SHEET + "\n\nMODULAR BUILDING PIECES in a WARM TIMBER style, clearly different from "
                    "white terraced apartments: facades clad in warm light-wood vertical louvres over most of the "
                    "surface, small square deep-set windows, a few climbing plants, pale sand-coloured walls instead "
                    "of white, rounded wooden canopies over the ground-floor doors and shop fronts with fabric awnings "
                    "in muted terracotta and olive." + LAYOUT_WORDS,
    "tower_night": NIGHT_EDIT,
    "facilities_a_night": NIGHT_EDIT,
    "facilities_b_night": NIGHT_EDIT,
    "strips_glass2_night": NIGHT_EDIT,
    "strips_wood2_night": NIGHT_EDIT,
})
EDIT_OF = {"strips_night": "strips.png", "tower_night": "tower.png", "facilities_a_night": "facilities_a.png",
           "facilities_b_night": "facilities_b.png", "strips_glass2_night": "strips_glass2.png",
           "strips_wood2_night": "strips_wood2.png"}
SECOND_REF = {"strips_glass": "strips.png", "strips_wood": "strips.png"}
TALL = {"tower": "1024x1536", "tower_night": "1024x1536", "kit_home_b": "1024x1536"}

# Stage 4 (2026-11-26): target pictures for a more developed city, one per direction. The ref only fixes the pixel
# style; the city itself is much denser and taller than the ref's block. Not sheets: full opaque scenes.
TARGET = ("\n\nThe reference image fixes ONLY the pixel style: the same 3/4 top-down camera, the same 4 x 4 pixel "
          "size, palette, light and level of detail. Do NOT copy its low-rise block: this city is far more developed. "
          "\n\nA game screenshot of a whole developed DISTRICT of a near-future city (2041), filling the image, no "
          "text, no UI, no labels. Three clear height levels: low two- and three-storey shop streets, mid-rise "
          "ten- to fifteen-storey housing and office slabs, and a few slender high-rise towers of thirty storeys and "
          "more standing on wide podiums with roof gardens. The city has layers: elevated pedestrian decks and glass "
          "skybridges joining buildings above the streets, an elevated light-rail line with a station, and broad "
          "streets below. Infrastructure looks like real facilities, each filling a block: an energy park with "
          "battery halls and solar arrays, an automated logistics hub with drone pads and autonomous trucks, a "
          "hospital campus. Streets of the future: small autonomous shuttle pods instead of cars, delivery robots, "
          "slim smart poles instead of lamp posts, holographic shop signs without readable text, delivery drones in "
          "the air along clear lanes, plazas with water channels and trees, tiny pedestrians. Calm, bright, orderly, "
          "believable, NOT cyberpunk, no neon clutter.")
PROMPTS.update({
    "target_a": STYLE + TARGET + "\n\nDirection A, ORDERED HIGH DENSITY: a precise grid of blocks, clean white and "
                "glass buildings in regular rows, wide straight boulevards, everything tidy and efficient, a "
                "city that is clearly planned and managed.",
    "target_b": STYLE + TARGET + "\n\nDirection B, VERTICAL GARDEN CITY: greenery climbs every building, terraces "
                "overflow with trees, green roofs and planted skybridges, towers wrapped in garden balconies, the "
                "streets are shaded green corridors; the city feels lush yet still dense and modern.",
    "target_c": STYLE + TARGET + "\n\nDirection C, MEGASTRUCTURE CORE: one huge calm landmark, the city's AI centre, "
                "a large white rounded structure with a glowing cyan core stands in the middle; wide elevated decks "
                "and skybridges radiate from it and tie the surrounding blocks together, so the whole district reads "
                "as one connected system around it.",
})

# Stage 5 (2026-11-26, draft, not generated yet): Sector α after the worldview (GDD 1). Reference: target_a, whose
# drawing was chosen. Layout is in words; see Dialog 2026-11-26 for the place list.
ALPHA = ("\n\nThe reference image fixes the pixel style only: match its 3/4 top-down camera, pixel size, palette, "
         "light and level of detail exactly. Do not copy its layout or its many trees.\n\n"
         "Setting: year 2071, two years after a ten-year world war. SECTOR ALPHA is a brand-new test city built in "
         "two years by a governing AI on the flattened ruins of an old city centre. Inside its boundary the city is "
         "finished, clean, bright and calm, fifteen years ahead of the world: white and glass buildings, almost NO "
         "trees (paved plazas, straight water channels, rows of glowing light pillars; only a few real trees shown "
         "like exhibits in planters, and a few translucent cyan HOLOGRAM trees). Public screens, facade displays and "
         "a plaza hologram are everywhere but show only abstract light patterns, no readable text or letters. "
         "Transport: a sleek elevated maglev ring line with stations, glowing lanes for small driverless pods (no "
         "ordinary cars), faint light lines of drone corridors in the air, vertiports on tower tops, delivery "
         "robots. Not cyberpunk: no neon clutter, no flying cars, no rain.")
PROMPTS.update({
    "alpha_city": STYLE + ALPHA + "\n\nA game screenshot of the WHOLE CITY, filling the image, no text, no UI. "
                  "In the middle, the AI CENTRE: a large calm white rounded structure with a softly glowing cyan "
                  "core and data halls, a wide plaza in front of it with a big hologram. Around it, four mixed-use "
                  "city blocks, each different: (top-left) a finished high-density block with two slender "
                  "residential towers with rooftop vertiports, mid-rise housing slabs and ground-floor shops; "
                  "(top-right) a commercial block with glass office buildings, facade screens and holographic shop "
                  "signs; (bottom-left) a block where some repaired OLD pre-war brick and concrete buildings stand "
                  "between new ones; (bottom-right) a half-built block with a school, empty fenced lots and a "
                  "construction site with a crane. Around the blocks, a ring of city facilities, each filling its "
                  "own site: an energy park with battery halls and solar canopies, a logistics hub with drone pads "
                  "and a tunnel ramp going underground, a glass research campus, a hospital campus with an "
                  "ambulance drone pad, a security control building with patrol drones, and a sealed construction "
                  "site behind white panels. The maglev ring loops around the blocks. The city ends at a clean "
                  "white boundary wall with a GATE on the left side. OUTSIDE the wall, along the top and the left "
                  "edge, the human world two years after the war: grey container housing, half-repaired buildings, "
                  "idle cranes, a ration queue, and a few ruined towers kept as a memorial; dull and dusty, but "
                  "people live there. The contrast across the wall is the point of the picture.",
    "alpha_block": STYLE + ALPHA + "\n\nA closer game screenshot of ONE BLOCK of the city at street level, "
                   "filling the image, no text, no UI: ground-floor shops and cafes along a paved street with "
                   "pod lanes, mid-rise housing above them, one slender tower rising out of the top of the image, "
                   "a glass skybridge, an elevated maglev station at one corner, a small plaza with a water "
                   "channel, one real tree in a planter and two hologram trees, a wall screen and a slim smart pole "
                   "with a small display, delivery robots and many tiny pedestrians. Wide open walkable streets "
                   "and plaza so people can walk around.",
})

# Stage 6 (2026-11-26): the city view as a LANDSCAPE, not a map: perspective, horizon, haze, scale. The top-down
# pixel map looked like a toy; these test whether a pixel-art panorama gives the vastness the overview needs.
PANO = ("Grand, awe-inspiring PIXEL ART panorama of a vast near-future city, the kind of breathtaking establishing "
        "shot that opens a game. The reference image fixes ONLY the pixel-art rendering (crisp square pixels, about "
        "4 x 4 image pixels each, hard edges, limited palette) and the architecture style (white rounded buildings, "
        "blue-cyan glass, thin cyan light lines); its top-down camera must NOT be copied. CAMERA: a high aerial view "
        "at a LOW oblique angle with real perspective, the city stretching far away to a distant horizon, so it feels "
        "enormous. DEPTH in layers: huge structures close to the camera cut off by the frame edges, a dense mid-ground "
        "forest of towers and mid-rises, and far away hundreds of buildings fading into blue atmospheric haze. SCALE: "
        "supertall towers of eighty storeys and more with rings and vertiports, sky bridges between them, an elevated "
        "maglev line sweeping between towers, streams of tiny pods on broad avenues, swarms of tiny drones along "
        "faint light corridors, specks of people on plazas, so the buildings feel immense. Almost NO trees: paved "
        "plazas, straight water channels, light pillars; a few cyan hologram trees. Screens and holograms show only "
        "abstract light, no readable text. Setting: SECTOR ALPHA in 2071, a test city built in two years by a "
        "governing AI two years after a ten-year world war. Not cyberpunk: calm, clean, orderly, monumental, a little "
        "unsettling in its perfection. No text, no UI, no logo.")
PROMPTS.update({
    "pano_a": PANO + "\n\nComposition A, LOOKING IN FROM THE WALL at golden late afternoon: in the foreground the "
              "dusty grey outside world (container homes, a ration queue, a ruined building kept as a memorial), "
              "then the immense clean white boundary wall with its gate, and beyond it the shining city rising "
              "higher and higher toward its centre, where the AI CENTRE stands, a colossal calm white rounded "
              "structure with a glowing cyan core, the tallest thing on the horizon. Warm low sunlight from the "
              "left, long shadows, golden haze over the city.",
    "pano_b": PANO + "\n\nComposition B, OVER THE CITY AT BLUE HOUR: the camera hangs high above the main axis "
              "avenue that runs straight toward the AI CENTRE, a colossal calm white rounded structure with a "
              "glowing cyan core and a giant ring of light hovering above its plaza. Thousands of warm and cyan "
              "windows are lighting up, light lines trace the avenues and maglev, the last daylight on the horizon, "
              "a long straight water channel reflecting the sky along the axis. At the far edges of the frame the "
              "city ends at a wall, and beyond it the dark outside world with only a few scattered lights.",
})

# Stage 7 (2026-11-26): the block kit, round 1 (common 6 + homes 2 + shops 2). Reference alpha_block fixes the style
# AND the scale. Blocks are the eleven functional districts (GDD 2); see Dialog 2026-11-26 for the full list.
KIT = ("Crisp pixel art game assets for the near-future city SECTOR ALPHA (2071), seen from the same 3/4 top-down "
       "camera as the reference image (roofs seen from above, front facades facing us, no side faces, no isometric "
       "diamond). Match the reference exactly: same pixel size (each art pixel a square of 4 x 4 image pixels, hard "
       "edges, no anti-aliasing, no blur), same palette (white and pale grey, blue-cyan glass, warm light wood "
       "accents, thin cyan light lines), same soft light from the upper left. SCALE as in the reference: a person is "
       "about 9 art pixels tall, one storey about 10 art pixels. Almost no plants: no balcony planters, no rooftop "
       "gardens, no trees unless asked. Screens and signs carry NO text and no letters, only abstract light. No "
       "cars of today: vehicles are small rounded driverless capsules.")
PROMPTS.update({
    "kit_ground": KIT + SHEET + "\n\nGROUND TILES, each a seamless square of 16 x 16 art pixels unless stated: "
                  "two kinds of pale paving, a paving with a thin cyan light stripe, a pavement with kerb on its "
                  "front edge, a road straight horizontal with a glowing cyan pod-lane line, the same road "
                  "vertical, a four-way crossing, a crosswalk, a straight water channel horizontal with a stone rim, "
                  "the same vertical, a channel end, a small footbridge over the channel, a plaza paving with a "
                  "circular pattern (32 x 32 art pixels).",
    "kit_props": KIT + SHEET + "\n\nSTREET FURNITURE, separate small pieces: a slim smart pole with a tiny "
                 "display, a smart pole with a light, a white bench, a long bench, a light pillar, a row of three "
                 "short light bollards, an information kiosk, a pod stop shelter with a glass roof, one real tree in "
                 "a square white planter, a smaller planter, a translucent cyan HOLOGRAM TREE on a round projector "
                 "base (two sizes), a drinking fountain, a recycling unit, a delivery locker wall.",
    "kit_screens": KIT + SHEET + "\n\nSCREENS AND HOLOGRAMS, all showing only soft abstract light patterns, no "
                   "text: a free-standing street screen on a post, a wide wall screen for a facade (three sizes), a "
                   "vertical banner screen, a holographic shop sign floating over a doorway (three shapes), a large "
                   "round plaza hologram projector with a translucent cyan ring of light above it, a floor projector "
                   "with a small floating icon.",
    "kit_transit": KIT + SHEET + "\n\nTRANSPORT AND MACHINES: a small rounded driverless pod seen from the "
                   "front-above and from the side (white, and pale blue), a two-seat pod, a cargo pod, a small "
                   "wheeled delivery robot, a quadcopter delivery drone, a white patrol drone with a cyan eye, an "
                   "elevated maglev track segment designed to repeat left-right, a slim track pillar, a sleek "
                   "three-car maglev train in side view, a small elevated maglev station with a glass roof and "
                   "stairs, a pod charging pad.",
    "kit_build": KIT + SHEET + "\n\nCONSTRUCTION AND DECLINE STAGES of one building lot, each the same lot size "
                 "(about 40 x 30 art pixels of ground) so they swap in place: an empty fenced lot with pale gravel, "
                 "excavated ground with a small robot excavator, a foundation slab with rebar, a rising frame with "
                 "a slim white robotic crane, a building wrapped in white construction panels with a thin cyan "
                 "line, and separately: an empty shop front with dark glass, a closed shop front behind a pale "
                 "shutter, a vacant building with dark windows.",
    "kit_backdrop": KIT + "\n\nThis is a BACKDROP sheet on a fully transparent background: three very wide "
                    "horizontal strips of distant city skyline, stacked from top to bottom with transparent gaps "
                    "between them, nothing else. Strip 1: the farthest skyline, only pale hazy blue-grey "
                    "silhouettes of countless towers with tiny lights. Strip 2: middle distance towers, slightly "
                    "clearer, white and glass with cyan light lines, a far maglev line. Strip 3: near background "
                    "of tall towers cut off at the bottom edge, clear and detailed. Each strip designed to repeat "
                    "seamlessly left-right.",
    "kit_home_a": KIT + SHEET + "\n\nMODULAR RESIDENTIAL BUILDING PIECES for stacking vertically into housing "
                  "slabs of any height, clean white walls with softly rounded corners, deep window bands of blue "
                  "glass, slim balconies with glass rails, warm light-wood panels between windows, a thin cyan line "
                  "on some slabs, NO planters." + LAYOUT_WORDS,
    "kit_home_b": KIT + SHEET + "\n\nRESIDENTIAL LANDMARKS: a supertall housing tower built from stackable "
                  "pieces: (1) a wide podium base with a lobby, (2) a slender shaft segment of exactly three storeys "
                  "designed to repeat seamlessly above and below itself, (3) a crown with a ring of light and a "
                  "small vertiport on top; then separately a ration and supply centre (a low white hall with a "
                  "covered queue lane and pickup windows), a care centre for children and the elderly (a low "
                  "rounded building with a small play court), a small residents' plaza with a hologram tree.",
    "kit_shop_a": KIT + SHEET + "\n\nSHOP FRONTS for ground floors, each piece about 28 art pixels wide and one "
                  "storey tall, in PAIRS side by side: the same shop OPEN (lit interior, awning, small tables or "
                  "goods outside, a holographic sign above the door) and the same shop CLOSED DOWN (dark glass, pale "
                  "shutter, sign switched off). Pairs: a cafe, a noodle restaurant, a grocery, a clothing shop, a "
                  "repair shop, a pharmacy, a bakery, a bar.",
    "kit_shop_b": KIT + SHEET + "\n\nMODULAR COMMERCIAL BUILDING PIECES: glass office floors, each storey one "
                  "continuous band of blue-cyan glass between thin white slabs, slim white mullions, a thin cyan "
                  "light line along every second slab, plus a version of the middle floor carrying a large blank "
                  "wall screen across its facade." + LAYOUT_WORDS + " Then separately: a fabric awning (three "
                  "colours: white, muted terracotta, olive), a set of outdoor cafe tables with chairs and a parasol.",
})

# Stage 8 (2026-11-26): code assembly looked like a tile scene, so each block becomes one painting (like
# alpha_block) with swappable spots edited later and life drawn by code. First: the residential block.
PROMPTS.update({
    "block_home": "Crisp pixel art game screen of ONE RESIDENTIAL BLOCK of SECTOR ALPHA, a near-future test city "
                  "(2071) run by an AI, two years after a ten-year world war. Match the reference image exactly in "
                  "camera (3/4 top-down: roofs from above, front facades facing us, no side faces, no isometric "
                  "diamond), pixel size (each art pixel a square of 4 x 4 image pixels, hard edges, no blur), "
                  "palette and light (soft daylight from the upper left). A person is about 9 art pixels tall. "
                  "No text, no UI, no letters anywhere; screens show only abstract light.\n\n"
                  "Composition, from the top of the image down: (1) a strip of pale sky with the hazy skyline of "
                  "the rest of the city far behind; (2) an elevated maglev line with a sleek train crossing the "
                  "block's back edge; (3) the back of the block: tall white housing slabs of ten to fifteen "
                  "storeys with long glass window bands and slim glass balconies, and one supertall residential "
                  "tower rising out of the top edge of the image with a ring of light; (4) a wide open COURTYARD "
                  "facing us: pale paving with a circular pattern, a straight water channel, two translucent cyan "
                  "hologram trees on round projector bases and only ONE real tree in a white planter, benches, "
                  "slim smart poles; on the left a RATION AND SUPPLY CENTRE (a low white hall with pickup "
                  "windows) with a short orderly queue of people, on the right a CARE CENTRE for children and the "
                  "elderly (a low rounded building with a small play court); (5) along the front, a row of "
                  "separate two- and three-storey buildings with SHOPS on the ground floor (a cafe, a grocery, a "
                  "pharmacy, a noodle bar, each its own clear storefront), with a clear gap in the middle opening "
                  "the courtyard to the street, and ONE EMPTY FENCED LOT among them; a large blank wall screen on "
                  "one building; (6) a pavement and a road along the bottom with glowing cyan pod lanes and small "
                  "rounded driverless pods. Delivery drones in the air, a delivery robot, small groups of people. "
                  "Clean, bright, calm, orderly, slightly too perfect. Almost no plants, no flying cars.",
    "block_home_b": "Redraw the SAME residential block of the reference image, the same places and the same pixel "
                    "art style (crisp square pixels about 4 x 4 image pixels, hard edges, same palette and light), "
                    "but with a DIFFERENT CAMERA and TALLER BUILDINGS. Camera: a high viewpoint looking DIAGONALLY "
                    "DOWN at the block from one corner, at about 40 degrees, with real perspective, so the streets "
                    "run diagonally into the distance and the skyline of the rest of the city fills the top third "
                    "of the image all the way to a hazy horizon; a wide field of view. Buildings: the housing slabs "
                    "are now twenty to thirty storeys, the residential tower is a slender supertall of sixty "
                    "storeys with a ring of light near the top, more towers of the city stand behind in the haze, "
                    "so the block feels like one corner of a vast vertical city. Keep: the courtyard with the "
                    "circular paving, the water channel, two cyan hologram trees and one real tree; the ration "
                    "centre with its queue; the care centre with the play court; the row of ground-floor shops "
                    "(cafe, grocery, pharmacy, noodle bar) with the empty fenced lot among them; the large blank "
                    "wall screen; the maglev line and train; the road with glowing pod lanes and rounded "
                    "driverless pods; drones; small people. Remove the trees at the edges and the rooftop "
                    "greenery. No text, no letters, no UI, no flying cars.",
    "overview": "Crisp pixel art game screen: the CITY OVERVIEW of SECTOR ALPHA, a near-future test city (2071) "
                "of about 100,000 people run by an AI. Two reference images. IMAGE 1 is a plain 3D massing model: "
                "follow its LAYOUT exactly (camera angle and framing, the straight street grid of many city blocks, "
                "where each district sits, the height and footprint of every building mass, the water plazas, the "
                "maglev lines); its flat colours, grid lines and labels are NOT to be drawn. IMAGE 2 fixes the "
                "DRAWING STYLE: the same pixel art (crisp square pixels, about 4 x 4 image pixels each, hard edges, "
                "no blur), palette (white and pale grey buildings with softly rounded corners, blue-cyan glass, "
                "thin cyan light lines, warm light-wood accents), light (soft daylight from the upper left, short "
                "shadows) and level of detail.\n\nThe city fills the whole frame; nothing outside the city is "
                "shown except the top of the white boundary wall at the very top edge. Districts, as in image 1: "
                "at the back and the left, tall slender housing towers of thirty to forty storeys with slim "
                "balconies and rooftop vertiports; at the back centre a hospital campus with an ambulance drone "
                "pad; at the back right a sealed construction site behind white panels with one crane (no sign); "
                "on the left an energy district with battery halls and solar canopies; in the middle the AI "
                "CENTRE, a large calm white rounded landmark with a softly glowing cyan core and a ring of light, "
                "with a wide plaza and a big water basin in front of it and a large plaza hologram; around it "
                "commercial blocks of glass office towers with blank facade screens and holographic shop signs; "
                "on the right a research campus of low glass buildings; in front, a logistics hub with drone pads "
                "and a tunnel ramp, a security headquarters with a control tower and patrol drones, a culture "
                "district of open plazas with long water basins and a floating stage, an education district with "
                "school buildings and sports courts, and mid-rise housing on the right. Two elevated maglev lines "
                "with sleek trains, straight boulevards with glowing cyan pod lanes and small rounded driverless "
                "pods, delivery drones along faint light corridors, tiny people on the plazas. Almost NO trees: "
                "paved plazas, straight water channels, light pillars, a few cyan hologram trees. Screens show "
                "only abstract light. No text, no letters, no labels, no UI, no flying cars. Calm, bright, "
                "orderly, vast, slightly too perfect.",
    "block_home_night": "Repaint the reference image at NIGHT, keeping exactly the same picture: the same camera, "
                        "every building, window, tree, pole and paving stone in the same place with the same pixel "
                        "grid. Night: deep blue sky glow above, walls and paving in calm dark blue-grey, about half "
                        "of the housing windows lit warm yellow and the rest dark, the cyan light lines and the "
                        "hologram trees glowing, the smart poles casting soft cyan pools of light on the pavement, "
                        "shop fronts lit warmly from inside, the wall screen a soft blank glow, the water reflecting "
                        "the lights. Still no people, no vehicles. No text, no letters, no UI.",
    "block_home_clean": "Repaint the reference image as an EMPTY BACKGROUND for a game, keeping exactly the same "
                        "camera, layout, pixel art style, palette and light, with two changes.\n\n(1) The HOUSING "
                        "BUILDINGS at the back of the block become MUCH TALLER: the white housing slabs behind the "
                        "courtyard rise thirty to forty storeys and run off the top edge of the image (their tops are "
                        "not visible), with many more rows of glass window bands, slim glass balconies and thin cyan "
                        "light lines, so the block is a deep canyon of tall housing; the low ration centre on the "
                        "left, the care centre on the right, the courtyard, the shops along the front, the empty "
                        "fenced lot, the wall screen and the road all stay where they are and the same size. The "
                        "skyline strip at the top is mostly covered by the tall housing now.\n\n(2) REMOVE ALL "
                        "PEOPLE, all vehicles and pods, all drones, the delivery robots, the maglev train (keep the "
                        "track) and the cafe chairs' occupants: the streets, pavements, courtyard and road are empty "
                        "and clean, painted as bare paving and asphalt where they stood. Keep the hologram trees, the "
                        "one real tree, benches, smart poles, planters and the play court equipment. The wall screen "
                        "stays blank. No text, no letters, no UI.",
    "block_home_zoom":"Crisp pixel art game screen: a ZOOMED-IN view of the residential district of SECTOR ALPHA "
                       "(2071), a near-future test city run by an AI. IMAGE 1 is an enlarged, blurry crop of the "
                       "city overview: keep its camera, framing and the position, footprint and height of every "
                       "building and street exactly, as if the camera zoomed in on that part of the overview, but "
                       "redraw it sharply with far more detail. IMAGE 2 fixes the detail level and pixel style: "
                       "crisp square pixels about 4 x 4 image pixels each, hard edges, no blur, the same palette and "
                       "soft light from the upper left, people about 9 art pixels tall.\n\nWhat the zoomed view "
                       "shows: tall white housing towers with long glass window bands, slim glass balconies, thin "
                       "cyan light lines and rooftop vertiports; at street level each block has ground-floor shops "
                       "(cafes, grocery, pharmacy, noodle bar) and a paved courtyard between its towers; in one "
                       "courtyard a ration and supply centre with an orderly queue, in another a care centre with a "
                       "small play court; two cyan hologram trees and only one real tree in a planter; a blank wall "
                       "screen on one tower; straight streets with crosswalks, glowing cyan pod lanes and small "
                       "rounded driverless pods; delivery robots, drones, small groups of people. Almost no plants. "
                       "No text, no letters, no UI, no flying cars.",
    "block_home_c": "Redraw the SAME residential block of the reference image with the SAME camera (high "
                    "viewpoint looking diagonally down from one corner at about 40 degrees, real perspective, the "
                    "skyline filling the top third to a hazy horizon) and the same pixel art style, palette and "
                    "light. Change only two things. (1) ALL ROADS ARE STRAIGHT: no curves at all; the roads, "
                    "pavements, pod lanes and kerbs run in straight lines along the two diagonal directions of a "
                    "regular street grid, meeting at a right-angle crossing with straight crosswalks; the block "
                    "itself is a clean rectangle seen from its corner. (2) The HOUSING BUILDINGS ARE TALLER: the "
                    "slabs around the courtyard are thirty to forty storeys, slender and tall, with the supertall "
                    "tower with the ring of light behind them, so the block is a canyon of tall housing around an "
                    "open courtyard. Keep everything else as it is: the courtyard with the circular paving, the "
                    "water channel, two cyan hologram trees and one real tree, the ration centre with its queue, "
                    "the care centre with its play court, the row of ground-floor shops with the empty fenced lot "
                    "among them, the large blank wall screen, the maglev line and train, rounded driverless pods, "
                    "drones, small people. No text, no letters, no UI, no flying cars.",
})

# Stage 9 (2026-11-26): the other districts as empty observation backgrounds, all drawn like block_home_clean
# (same camera, scale and pixel drawing; tall buildings at the back running off the top edge, an open area in the
# middle with the district's own landmark, a front row with ground-floor uses, a road along the bottom).
# People, pods, drones and screen text are drawn by code on top, so the pictures are empty.
BLOCK_COMMON = ("Paint a NEW city block of SECTOR ALPHA (2071) as an EMPTY GAME BACKGROUND in exactly the drawing of "
                "the reference image: the same camera (3/4 top-down, front facades facing us), the same pixel art "
                "(crisp square pixels about 4 x 4 image pixels, hard edges), palette, light and SCALE (same storey "
                "height, same road and pavement widths), and the same composition: tall buildings at the back "
                "running off the top edge of the image, an open paved area in the middle with this district's own "
                "landmark, a front row of buildings with ground-floor uses and a gap opening to the middle, a "
                "pavement, a road with glowing cyan pod lanes and a far pavement along the bottom. NO people, NO "
                "vehicles, NO drones: empty, clean streets and plazas. Almost no trees (one or two at most, plus "
                "cyan hologram trees). Screens are blank glowing panels. No text, no letters, no signs with "
                "writing, no UI.\n\nThis block: ")
BLOCKS = {
    "block_shop": BLOCK_COMMON + "the COMMERCIAL DISTRICT. At the back, glass office towers with huge blank facade "
                  "screens and holographic shop-sign frames. In the middle, a lively shopping plaza: a circular "
                  "plaza with a big round plaza hologram projector, an arcade of small kiosks and food stalls under "
                  "glass canopies, outdoor cafe tables. The front row is a continuous street of shop fronts (cafe, "
                  "bakery, clothing, electronics, noodle bar, pharmacy, a bar), each with its own awning colour, "
                  "and one empty shop with a pale shutter. An elevated maglev station with a glass roof at one side.",
    "block_culture": BLOCK_COMMON + "the CULTURE AND LEISURE DISTRICT. At the back, mid-rise buildings and a "
                     "curved white concert hall. In the middle, the biggest open space of the city: a wide square "
                     "with a long reflecting pool and a floating stage on the water, tiered stone seating steps "
                     "down to the pool, a ring of light pillars, a large plaza screen, a small exhibition pavilion "
                     "of glass. The front row: a museum entrance, a cinema with a blank marquee, a cafe. This is "
                     "where crowds gather.",
    "block_edu": BLOCK_COMMON + "the EDUCATION DISTRICT. At the back, a tall glass lecture building and student "
                 "housing towers. In the middle, a white primary school with a rounded roof and a covered entrance, "
                 "a fenced sports court with markings, a running track loop, a small amphitheatre of steps, a "
                 "bus-pod stop shelter. The front row: a learning centre with large windows, a library pavilion, a "
                 "small stationery shop, a cafeteria.",
    "block_admin": BLOCK_COMMON + "the ADMINISTRATION DISTRICT, the heart of the city. At the back, the AI "
                   "CENTRE: a colossal calm white rounded building with a tall glowing cyan core and a ring of "
                   "light, rising off the top edge, flanked by data halls. In the middle, a formal ceremonial "
                   "plaza with a giant floating plaza hologram, a long straight water basin on the axis, rows of "
                   "light pillars, a line of service kiosks (the AI's public counters) with queue rails. The front "
                   "row: the World Union liaison office (a sober white building with a flag-less entrance "
                   "canopy), a records office, a small police post.",
}
PROMPTS.update(BLOCKS)

# Stage 10 (2026-11-26): the first four copied too much of the reference (the same housing wall at the back, the
# same pools along the bottom). The reference now fixes only the camera, scale and drawing; every building is
# this district's own. The road strip stays the same in every block so walking paths line up.
BLOCK_COMMON2 = ("Paint a NEW, DIFFERENT city block of SECTOR ALPHA (2071) as an EMPTY GAME BACKGROUND. The reference "
                 "image fixes ONLY the camera (3/4 top-down, front facades facing us), the pixel art (crisp square "
                 "pixels about 4 x 4 image pixels, hard edges), the palette family, the light and the SCALE (storey "
                 "height, pavement and road widths). DO NOT COPY ITS BUILDINGS: no wall of white housing slabs at the "
                 "back, no copied shops, no copied pools. Every building here belongs to this district and looks "
                 "different from the reference. Keep only this shared strip at the bottom: a pavement, then a road "
                 "with glowing cyan pod lanes and a crosswalk, then a far pavement, at the same height in the image as "
                 "in the reference. Composition: the district's tall or large buildings at the back (they may run off "
                 "the top edge), an open walkable area in the middle with the district's landmark, a front row facing "
                 "the pavement. NO people, NO vehicles, NO drones. At most one or two trees. Screens are blank glowing "
                 "panels. No text, no letters, no UI.\n\nThis block: ")
BLOCKS.update({
    "block_shop_v2": BLOCK_COMMON2 + "the COMMERCIAL DISTRICT, the busiest place in the city. At the back, a cluster "
                     "of glass office towers of different heights and shapes (one curved, one stepped, one slim "
                     "with a crown), their facades covered in huge blank screens and media walls, linked by glass "
                     "skybridges; an elevated maglev station with a glass roof on the right. In the middle, a "
                     "sunken shopping plaza: steps down to a lower level with an arcade of small kiosks and food "
                     "stalls under glass canopies, a big round plaza hologram projector in the centre, outdoor cafe "
                     "tables. The front row: a long two-storey shopping arcade with many narrow shop fronts, each "
                     "with its own awning colour (terracotta, olive, mustard, white, blue), one shop shuttered.",
    "block_culture_v2": BLOCK_COMMON2 + "the CULTURE AND LEISURE DISTRICT. At the back, a large curved white concert "
                        "hall with a sweeping roof, and beside it a low art museum with a glass atrium; no housing. "
                        "In the middle, the biggest open space of the city: a wide square with stone seating steps "
                        "descending toward a long reflecting pool with a floating stage, a ring of light pillars, a "
                        "large plaza screen on a frame, a glass exhibition pavilion. The front row: a cinema with a "
                        "blank marquee, a small music hall entrance, a cafe with tables, an open gap leading in.",
    "block_edu_v2": BLOCK_COMMON2 + "the EDUCATION DISTRICT. At the back, a university-like campus: a tall glass "
                    "lecture tower with stepped terraces, a long low science building with a dome observatory on its "
                    "roof, and a mid-rise student dormitory with coloured window panels; no copied housing. In the "
                    "middle, a white primary school with a rounded roof and a covered entrance, a fenced sports "
                    "court with markings, a red running track around a green field, a small amphitheatre of steps, "
                    "a pod-bus stop shelter. The front row: a learning centre with large windows, a library "
                    "pavilion with a wooden roof, a stationery shop, a cafeteria.",
    "block_admin_v2": BLOCK_COMMON2 + "the ADMINISTRATION DISTRICT, the heart of the city. At the back, the AI "
                      "CENTRE alone: a colossal calm white rounded building with a tall glowing cyan core and a ring "
                      "of light, rising off the top edge, flanked by two low windowless data halls with cooling "
                      "vents; nothing else behind it. In the middle, a formal ceremonial plaza: a giant floating "
                      "plaza hologram, a long straight water basin on the central axis with walkways on both sides, "
                      "rows of light pillars, a line of service kiosks (the AI's public counters) with queue rails. "
                      "The front row: the World Union liaison office (a sober white building with columns and a "
                      "canopy, no flags), a records office, a small police post.",
})
PROMPTS.update(BLOCKS)

# Stage 10b (2026-11-26): the remaining six districts, same rules; reference is the finished residential picture
# (no pools along the bottom), and each block brings its own bottom strip below the far pavement.
BLOCK_REST = ("Below the far pavement, along the very bottom edge, show this district's own ground (NOT water pools). "
              "\n\nThis block: ")
BLOCKS.update({
    "block_research": BLOCK_COMMON2.replace("\n\nThis block: ", "") + BLOCK_REST + "the RESEARCH AND DEVELOPMENT "
                      "DISTRICT. At the back, a long low DATA CENTRE with rows of cooling units and blinking status "
                      "lights, a tall glass laboratory tower with a rooftop antenna array and a white radio dome. In "
                      "the middle, a quiet campus lawn-less courtyard of pale stone with a sculpture of a ring, "
                      "outdoor glass meeting pods and benches, a drone test pad with painted markings. The front "
                      "row: a glass research institute entrance with a turnstile gate, a prototype showroom with a "
                      "big window, a small cafe. Bottom strip: a loading lane with equipment crates.",
    "block_energy": BLOCK_COMMON2.replace("\n\nThis block: ", "") + BLOCK_REST + "the ENERGY DISTRICT. At the back, "
                    "a field of tilted solar canopies on tall frames, three round white battery storage halls with "
                    "cyan charge bands, a slim cooling tower and a substation of transformers behind a mesh fence. "
                    "In the middle, a service yard with a power control building that has a wall of status screens, "
                    "cable trenches with grated covers, a maintenance robot garage. The front row: a visitors' "
                    "energy pavilion, a workers' canteen, a guard booth at a gate. Few people come here; it is wide "
                    "and technical. Bottom strip: a fenced verge with junction boxes.",
    "block_logistics": BLOCK_COMMON2.replace("\n\nThis block: ", "") + BLOCK_REST + "the LOGISTICS AND INDUSTRY "
                       "DISTRICT. At the back, a huge automated warehouse with a flat roof covered in drone pads and "
                       "landing lights, a robotic sorting hall with conveyor bridges between buildings. In the "
                       "middle, a loading yard with stacked containers, autonomous cargo pod bays with numbered "
                       "doors, and the wide ramp of a logistics tunnel going underground. The front row: a workers' "
                       "entrance with lockers, a parts depot, a small noodle bar for the shift workers. Bottom strip: "
                       "a truck-less service lane with charging pads for cargo pods.",
    "block_medical": BLOCK_COMMON2.replace("\n\nThis block: ", "") + BLOCK_REST + "the MEDICAL AND LIFE SCIENCE "
                     "DISTRICT. At the back, a large white hospital with curved wings and a rooftop ambulance drone "
                     "pad marked with a cross symbol (no letters), a glass life-science laboratory beside it. In the "
                     "middle, a calm healing garden plaza of pale stone with a quiet fountain basin, benches under "
                     "shade sails, a covered drop-off canopy at the main entrance. The front row: a pharmacy, a "
                     "clinic, a care centre for the elderly with large windows. Bottom strip: a quiet ambulance pod "
                     "bay with soft marked lanes.",
    "block_security": BLOCK_COMMON2.replace("\n\nThis block: ", "") + BLOCK_REST + "the SECURITY DISTRICT. At the "
                      "back, a tall grey-white control tower with a ring of dark observation windows and a crown of "
                      "sensors, a police headquarters building, a drone hangar with rows of docked patrol drones on "
                      "its roof. In the middle, a hard open parade square with a raised control kiosk, retractable "
                      "bollards, a checkpoint gate with scanners. The front row: a police service counter, a "
                      "detention entrance with a heavy door, a small guard post. Orderly, clean, a little cold. "
                      "Bottom strip: a row of security barriers and parked patrol pods' bays (empty).",
    "block_site": BLOCK_COMMON2.replace("\n\nThis block: ", "") + BLOCK_REST + "a SEALED CONSTRUCTION SITE with no "
                  "name. At the back, a large structure under construction hidden by tall white panels with a thin "
                  "cyan line, two slim white robotic cranes rising off the top edge, scaffolding. In the middle, a "
                  "fenced work yard with stacked materials, a site office cabin, a guarded gate with a turnstile and "
                  "a blank sign board (no letters). The front row: a long white hoarding wall with a single blank "
                  "screen, a security booth. Nothing tells what is being built. Bottom strip: a closed service lane.",
})
for k in ("block_research", "block_energy", "block_logistics", "block_medical", "block_security", "block_site"):
    PROMPTS[k] = BLOCKS[k]

# Stage 11 (2026-11-26): every block had the same pools along the bottom edge. Only culture keeps water; the others
# get their own strip below the far pavement. Edits of the finished pictures, everything above stays.
BOTTOM_EDIT = ("Repaint the reference image keeping EVERYTHING exactly the same (same camera, every building, the road, "
               "both pavements, every pole, pixel for pixel), except the strip of WATER POOLS along the very bottom "
               "edge below the far pavement: remove the water entirely and replace that strip with ")
BOTTOM = {
    "block_home_bottom": ("block_home_clean.png", "the edge of the next residential block: a low hedge-free paved "
                          "forecourt with bike racks, a row of parcel lockers and the tops of entrance canopies of "
                          "the housing beyond."),
    "block_home_night_bottom": ("block_home_night.png", "the edge of the next residential block at night: a paved "
                                "forecourt with bike racks, a row of softly lit parcel lockers and lit entrance "
                                "canopies, same night lighting as the rest of the picture."),
    "block_shop_bottom": ("block_shop_v2.png", "the edge of the next commercial street: a row of pod parking bays "
                          "with glowing charging posts, a delivery robot dock and the tops of shop awnings beyond."),
    "block_edu_bottom": ("block_edu_v2.png", "the edge of a school drop-off zone: a covered pod-bus stop, painted "
                         "pedestrian markings, a row of bicycle racks and a low fence of a playground beyond."),
    "block_admin_bottom": ("block_admin_v2.png", "a formal stone forecourt: wide paving with an inlaid line pattern, "
                           "a row of short light bollards and the top of a ceremonial stair beyond."),
}
for k, (_, what) in BOTTOM.items():
    PROMPTS[k] = BOTTOM_EDIT + what + " No people, no vehicles, no text, no letters, no UI."


def gen(keys):
    os.makedirs(OUT, exist_ok=True)
    ref = os.path.join(OUT, "ref.png")
    for key in keys:
        path = os.path.join(OUT, f"{key}.png")
        prompt = PROMPTS[key] + ("" if key == "ref" or key in EDIT_OF or key.startswith(("target_", "alpha_", "pano_", "block_")) else FOLLOW)
        with open(path[:-4] + ".prompt.txt", "w", encoding="utf-8") as f:
            f.write(prompt)
        if os.path.exists(path):
            print(key, "exists")
            continue
        if key == "ref":
            extra = []
        elif key in EDIT_OF:
            extra = ["-i", os.path.join(OUT, EDIT_OF[key]), "--background", "transparent"]
        elif key.startswith("target_"):
            extra = ["-i", ref]  # a full scene, not a sheet
        elif key.startswith("kit_"):
            extra = ["-i", os.path.join(OUT, "alpha_block.png"), "--background", "transparent"]
        elif key == "overview":
            # layout from the massing guide (Tools/Art/city_guide.py), drawing from alpha_city
            extra = ["-i", os.path.join(OUT, "city_guide.png"), "-i", os.path.join(OUT, "alpha_city.png")]
        elif key in BOTTOM:
            extra = ["-i", os.path.join(OUT, BOTTOM[key][0])]  # the same picture, only the bottom strip changes
        elif key in ("block_research", "block_energy", "block_logistics", "block_medical", "block_security", "block_site"):
            extra = ["-i", os.path.join(OUT, "block_home_clean_final.png")]
        elif key == "block_home_night" or key in BLOCKS:
            extra = ["-i", os.path.join(OUT, "block_home_clean.png")]  # same camera, scale and drawing
        elif key == "block_home_clean":
            extra = ["-i", os.path.join(OUT, "block_home.png")]  # the observation background: same block
        elif key == "block_home_zoom":
            # the housing corner of the overview, enlarged: same places, redrawn in detail
            extra = ["-i", os.path.join(OUT, "overview_crop_home.png"), "-i", os.path.join(OUT, "block_home.png")]
        elif key == "block_home_b":
            extra = ["-i", os.path.join(OUT, "block_home.png")]  # the same block, a new camera
        elif key == "block_home_c":
            extra = ["-i", os.path.join(OUT, "block_home_b.png")]  # same camera, straight roads, taller housing
        elif key.startswith("block_"):
            extra = ["-i", os.path.join(OUT, "alpha_block.png")]  # a whole block in the alpha_block drawing
        elif key.startswith("pano_"):
            extra = ["-i", os.path.join(OUT, "alpha_block.png")]  # palette and pixel size only; the camera differs
        elif key.startswith("alpha_"):
            extra = ["-i", os.path.join(OUT, "target_a.png")]  # Sector α follows target A's drawing
        else:
            extra = ["-i", ref, "--background", "transparent"]
            if key in SECOND_REF:
                extra += ["-i", os.path.join(OUT, SECOND_REF[key])]
        r = subprocess.run(["uv", "run", GEN, "--model", "gpt-image-2.6-flare", "-p", prompt, *extra,
                            "--size", TALL.get(key, "1536x1024"), "--quality", "medium", "-f", path],
                           capture_output=True, text=True)
        print(key, r.returncode, r.stderr[-400:] if r.returncode else "ok", flush=True)


# The kit sheets came out at different scales (the prompt's "person 9, storey 10" was ignored). Each sheet is put
# back on the pixel grid with its own cell size so that, after cutting, a person is about 15 art pixels and a storey
# about 22 everywhere (kit_home_b and kit_shop_b already are). Measured on the round-1 sheets, 2026-11-26.
CELL = {"kit_home_a": 5.6, "kit_shop_a": 6.4, "kit_props": 6.3, "kit_screens": 6.5, "kit_transit": 8.0,
        "kit_build": 5.1, "kit_ground": 5.5}


def snap(im, cell=4, colors=40):
    """back onto the art-pixel grid: average each cell, then reduce the palette"""
    small = im.resize((round(im.width / cell), round(im.height / cell)), Image.BOX)
    a = small.split()[3].point(lambda v: 255 if v > 128 else 0)
    rgb = small.convert("RGB").quantize(colors, method=Image.Quantize.MEDIANCUT).convert("RGB")
    out = rgb.convert("RGBA")
    out.putalpha(a)
    return out


def cut():
    os.makedirs(os.path.join(OUT, "parts"), exist_ok=True)
    for key in ("strips", "props", "ground", "strips_glass", "strips_wood", "strips_night", "tower", "facilities_a", "facilities_b", "construction", "transit",
                "strips_glass2", "strips_wood2", "tower_night", "facilities_a_night", "facilities_b_night",
                "strips_glass2_night", "strips_wood2_night", *sorted(k for k in PROMPTS if k.startswith("kit_"))):
        path = os.path.join(OUT, f"{key}.png")
        if not os.path.exists(path):
            continue
        # kit sheets keep more colours: at 40 the one green (the potted tree) was lost to grey
        im = snap(Image.open(path).convert("RGBA"), CELL.get(key, 4), 64 if key.startswith("kit_") else 40)
        im.save(os.path.join(OUT, f"{key}_snapped.png"))
        # connected pieces by alpha (8-neighbour flood fill on the small image)
        w, h = im.size
        a = im.split()[3].load()
        seen = [[False] * w for _ in range(h)]
        boxes = []
        for y in range(h):
            for x in range(w):
                if a[x, y] and not seen[y][x]:
                    stack, x0, y0, x1, y1, n = [(x, y)], x, y, x, y, 0
                    seen[y][x] = True
                    while stack:
                        cx, cy = stack.pop(); n += 1
                        x0, y0, x1, y1 = min(x0, cx), min(y0, cy), max(x1, cx), max(y1, cy)
                        for dx in (-1, 0, 1):
                            for dy in (-1, 0, 1):
                                nx, ny = cx + dx, cy + dy
                                if 0 <= nx < w and 0 <= ny < h and a[nx, ny] and not seen[ny][nx]:
                                    seen[ny][nx] = True; stack.append((nx, ny))
                    if n > 12:
                        boxes.append((x0, y0, x1 + 1, y1 + 1))
        boxes.sort(key=lambda b: (b[1] // 12, b[0]))
        sheet = im.resize((w * 4, h * 4), Image.NEAREST).convert("RGBA")
        d = ImageDraw.Draw(sheet)
        for i, b in enumerate(boxes):
            im.crop(b).save(os.path.join(OUT, "parts", f"{key}_{i:02d}.png"))
            d.rectangle([b[0] * 4, b[1] * 4, b[2] * 4, b[3] * 4], outline=(255, 0, 80))
            d.text((b[0] * 4 + 2, b[1] * 4 + 2), str(i), fill=(255, 0, 80))
        bg = Image.new("RGBA", sheet.size, (40, 44, 56, 255))
        Image.alpha_composite(bg, sheet).convert("RGB").save(os.path.join(OUT, f"{key}_index.png"))
        print(key, len(boxes), "parts")


# ---------- mock: one block assembled from the cut parts ----------

MW, MH, MSCALE = 640, 360, 3
# part numbers in parts/ (see the *_index.png contact sheets)
ROOF, MID_A, MID_B, SHOP, LOBBY = (0, 1, 2), (3, 4, 5), (6, 7, 8), (9, 10, 11), (12, 13, 14)
TREES, LAMP, BENCH, CAR_F, CAR_S, PLANTERS = ("06", "07", "08", "12"), "09", "13", "17", "18", ("20", "21", "22")
G_ROAD, G_PAVE, G_PAVE2, G_GRASS, G_GRASS2, G_DIRT, G_RAIL = "00", "04", "05", "06", "07", "08", "39"


def part(key, n):
    return Image.open(os.path.join(OUT, "parts", f"{key}_{int(n):02d}.png")).convert("RGBA")


def is_glass(c):
    r, g, b = c[:3]
    return b > r + 25 and b > 80 and g < b + 10


class Mock:
    def __init__(self, seed):
        import random
        self.rnd = random.Random(seed)
        self.img = Image.new("RGBA", (MW, MH), (200, 200, 196, 255))
        self.lit = Image.new("L", (MW, MH), 0)      # window pixels that light up at night
        self.lamps = []

    def put(self, im, x, y, lit=False):
        x, y = round(x), round(y)
        self.img.alpha_composite(im, (x, y))
        if lit:
            m = Image.new("L", im.size, 0)
            px, mp = im.load(), m.load()
            for yy in range(im.height):
                for xx in range(im.width):
                    if px[xx, yy][3] > 128 and is_glass(px[xx, yy]):
                        mp[xx, yy] = 255
            self.lit.paste(m, (x, y), m)

    def tile(self, key, x0, y0, x1, y1, size=40, alt=None):
        t = part("ground", key).resize((size, size), Image.NEAREST)
        a = part("ground", alt).resize((size, size), Image.NEAREST) if alt else t
        for y in range(y0, y1, size):
            for x in range(x0, x1, size):
                self.img.alpha_composite((a if self.rnd.random() < 0.3 else t).crop((0, 0, min(size, x1 - x), min(size, y1 - y))), (x, y))

    def building(self, x, yb, w, floors, shop):
        """stack: ground floor, middle floors (alternating), roof; returns the width used"""
        rnd = self.rnd
        g = part("strips", (SHOP if shop else LOBBY)[w])
        y = yb - g.height
        pieces = [(g, y, rnd.random() < 0.7)]
        for f in range(floors):
            m = part("strips", (MID_A if f % 2 == 0 else MID_B)[w])
            y -= m.height - 3
            pieces.append((m, y, rnd.random() < 0.55))
        r = part("strips", ROOF[w])
        y -= r.height - 4
        pieces.append((r, y, False))
        for im, py, lit in reversed(pieces):  # roof first, so lower floors overlap its bottom ledge
            pass
        for im, py, lit in pieces[::-1]:
            pass
        # draw from the top down so each lower piece covers the ledge of the one above
        for im, py, lit in sorted(pieces, key=lambda t: t[1]):
            self.put(im, x, py, lit)
        return g.width

    def render(self, empty_lot):
        rnd = self.rnd
        self.tile(G_PAVE, 0, 0, MW, MH, alt=G_PAVE2)
        self.tile(G_GRASS, 0, 140, MW, 190, alt=G_GRASS2)
        # road along the front
        road = part("ground", G_ROAD).resize((40, 40), Image.NEAREST)
        for x in range(0, MW, 40):
            self.img.alpha_composite(road, (x, MH - 42))
        # back row: taller apartments
        x = 8
        while x < MW - 60:
            w = rnd.choice((1, 2, 2, 0))
            if x + (55, 84, 127)[w] > MW - 4:
                w = 0
            used = self.building(x, 168, w, rnd.randint(1, 3), shop=False)
            x += used + rnd.choice((10, 16, 22))
        # trees in the courtyard
        for x in range(20, MW - 20, 46):
            t = part("props", rnd.choice(TREES))
            self.put(t, x + rnd.randint(-6, 6) - t.width / 2, 196 - t.height)
        # front row: shops and low buildings, one lot left empty or built
        import random
        x, lot = 6, 0
        while x < MW - 60:
            rnd = self.rnd = random.Random(1000 + lot)  # each lot its own dice, so before/after differ only at the lot
            w = rnd.choice((0, 1, 1, 2))
            if x + (55, 84, 127)[w] > MW - 4:
                w = 0
            if lot == 2:
                if empty_lot:
                    self.tile(G_DIRT, x, 250, x + 84, MH - 42)
                    rail = part("ground", G_RAIL)
                    for k in range(0, 84, rail.width):
                        self.put(rail, x + k, MH - 42 - rail.height + 2)
                    used = 84
                else:
                    used = self.building(x, MH - 40, 1, 1, shop=True)
            else:
                used = self.building(x, MH - 40, w, rnd.randint(0, 1), shop=rnd.random() < 0.75)
            x += used + rnd.choice((6, 10, 14))
            lot += 1
        # street furniture and traffic
        for x in range(30, MW, 110):
            l = part("props", LAMP)
            self.put(l, x, MH - 44 - l.height + 6)
            self.lamps.append((x + l.width // 2, MH - 44 - l.height + 9))
        for x in (90, 380):
            c = part("props", CAR_S)
            self.put(c, x, MH - 24 - c.height // 2)
        return self.img

    def night(self):
        day = self.img.convert("RGB")
        dp, lp = day.load(), self.lit.load()
        out = Image.new("RGB", day.size)
        op = out.load()
        for y in range(MH):
            for x in range(MW):
                r, g, b = dp[x, y]
                if lp[x, y]:
                    l = (r + g + b) / 765
                    op[x, y] = (int(120 + 135 * l), int(88 + 120 * l), int(48 + 92 * l))  # keeps the window detail
                elif g > 150 and b > 170 and r < 140:  # cyan light strips keep glowing
                    op[x, y] = (r, g, b)
                else:
                    op[x, y] = (int(r * 0.26 + 8), int(g * 0.30 + 10), int(b * 0.42 + 22))
        glow = Image.new("RGBA", day.size, (0, 0, 0, 0))
        gd = ImageDraw.Draw(glow)
        for x, y in self.lamps:
            for rad, a in ((22, 25), (14, 40), (7, 70)):
                gd.ellipse([x - rad, y - rad, x + rad, y + rad], fill=(255, 214, 150, a // 2))
                gd.ellipse([x - rad * 1.4, MH - 46 - rad // 3, x + rad * 1.4, MH - 46 + rad // 3], fill=(255, 214, 150, a))
            gd.ellipse([x - 3, y - 3, x + 3, y + 3], fill=(255, 236, 190, 230))
        return Image.alpha_composite(out.convert("RGBA"), glow).convert("RGB")


def darken(im):
    """night version of a day part: dark blue-grey, cyan accents and warm lamps kept"""
    import numpy as np
    a = np.asarray(im.convert("RGBA")).astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    keep = (g > 150) & (b > 170) & (r < 140)
    out = a.copy()
    out[..., 0] = np.where(keep, r, r * 0.26 + 8)
    out[..., 1] = np.where(keep, g, g * 0.30 + 10)
    out[..., 2] = np.where(keep, b, b * 0.42 + 22)
    return Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")


NIGHT_OF = {"strips": "strips_night", "tower": "tower_night", "facilities_a": "facilities_a_night",
            "facilities_b": "facilities_b_night", "strips_glass2": "strips_glass2_night",
            "strips_wood2": "strips_wood2_night"}
STYLES = ("strips", "strips_glass2", "strips_wood2")  # terraced, glass, timber


def night_lit(im, lit):
    """night for a facade piece that has no generated night copy: darken, and light the glass of lit floors"""
    import numpy as np
    a = np.asarray(im.convert("RGBA")).astype(np.float32)
    d = np.asarray(darken(im)).astype(np.float32)
    if lit:
        r, g, b = a[..., 0], a[..., 1], a[..., 2]
        glass = (b > r + 25) & (b > 80) & (g < b + 10)
        l = (r + g + b) / 765
        warm = np.stack([120 + 135 * l, 88 + 120 * l, 48 + 92 * l, a[..., 3]], -1)
        d = np.where(glass[..., None], warm, d)
    return Image.fromarray(d.clip(0, 255).astype(np.uint8), "RGBA")


class CityMock:
    """a slice of the city: facilities at the back, the tower and the light rail over the avenue, two blocks in front"""
    W, H = 960, 640

    def __init__(self, night):
        import random
        self.night = night
        self.rnd = random.Random(8)
        self.img = Image.new("RGBA", (self.W, self.H), (0, 0, 0, 255))
        self.items = []
        self.lamps = []

    def p(self, key, n):
        if self.night and key in NIGHT_OF:
            return part(NIGHT_OF[key], n)
        im = part(key, n)
        return darken(im) if self.night else im

    def facade(self, style, n, lit):
        if not self.night or style in NIGHT_OF:
            return self.p(style, n)
        return night_lit(part(style, n), lit)

    def add(self, im, x, base, shadow=True):
        """queue a sprite whose bottom edge stands at y = base"""
        self.items.append((base, x, im, shadow))

    def tiles(self, key, x0, y0, x1, y1, alt=None):
        t = self.p("ground", key).resize((40, 40), Image.NEAREST)
        a = self.p("ground", alt).resize((40, 40), Image.NEAREST) if alt else t
        for y in range(y0, y1, 40):
            for x in range(x0, x1, 40):
                self.img.alpha_composite((a if self.rnd.random() < 0.3 else t).crop((0, 0, min(40, x1 - x), min(40, y1 - y))), (x, y))

    def road(self, y):
        t = self.p("ground", G_ROAD).resize((40, 40), Image.NEAREST)
        for x in range(0, self.W, 40):
            self.img.alpha_composite(t, (x, y))

    def building(self, x, base, w, floors, shop, style):
        rnd = self.rnd
        g = self.facade(style, (SHOP if shop else LOBBY)[w], rnd.random() < 0.7)
        pieces, y = [(g, base - g.height)], base - g.height
        for f in range(floors):
            m = self.facade(style, (MID_A if f % 2 == 0 else MID_B)[w], rnd.random() < 0.5)
            y -= m.height - 3
            pieces.append((m, y))
        r = self.facade(style, ROOF[w], False)
        y -= r.height - 4
        pieces.append((r, y))
        sprite = Image.new("RGBA", (max(im.width for im, _ in pieces), base - y), (0, 0, 0, 0))
        for im, py in sorted(pieces, key=lambda t: t[1]):
            sprite.alpha_composite(im, (0, py - y))
        self.add(sprite, x, base)
        return g.width

    def row(self, x0, x1, base, floors, shop_chance, styles, gap=(12, 20, 28), special=None):
        rnd, x, k = self.rnd, x0, 0
        while x < x1 - 56:
            if special and k in special:
                im = self.p("construction", special[k])
                self.add(im, x, base + 4)
                x += im.width + 8
            else:
                w = rnd.choice((0, 1, 1, 2))
                while w and x + (55, 84, 127)[w] > x1:
                    w -= 1
                x += self.building(x, base, w, rnd.randint(*floors), rnd.random() < shop_chance, rnd.choice(styles)) + rnd.choice(gap)
            k += 1

    def render(self):
        rnd, W, H = self.rnd, self.W, self.H
        self.tiles(G_PAVE, 0, 0, W, H, alt=G_PAVE2)
        self.road(158)
        self.road(320)
        self.road(H - 40)
        self.tiles(G_GRASS, 0, 476, W, 512, alt=G_GRASS2)
        # back: city facilities
        x = 0
        for key, n in (("facilities_a", 0), ("facilities_a", 1), ("facilities_a", 2), ("facilities_b", 0), ("facilities_b", 2)):
            im = self.p(key, n)
            self.add(im, x, 162)
            x += im.width - 8
        # middle: low offices and shops either side of the tower
        self.row(0, 370, 314, (0, 2), 0.4, ("strips_glass2", "strips_glass2", "strips"))
        self.row(580, W, 314, (0, 2), 0.4, ("strips_glass2", "strips_wood2"))
        tower = self.p("tower", 0)
        self.add(tower, 475 - tower.width // 2, 318)
        # the light rail over the avenue
        track = self.p("transit", 0)
        rail = Image.new("RGBA", (W, track.height), (0, 0, 0, 0))
        for xx in range(-20, W, track.width - 4):
            rail.alpha_composite(track, (xx, 0))
        self.add(rail, 0, 352, shadow=False)
        train = self.p("transit", 2)
        self.add(train, 560, 352 - track.height + 12, shadow=False)
        # front: two blocks; homes at the back of each, shops along the street, the right one with lots at every stage
        self.row(0, 450, 470, (1, 2), 0.1, ("strips", "strips", "strips_wood2"))
        self.row(500, W, 470, (1, 2), 0.1, ("strips", "strips_wood2"))
        for xx in range(16, W, 56):
            t = self.p("props", rnd.choice(TREES))
            self.add(t, xx + rnd.randint(-6, 6), 512)
        self.row(0, 450, H - 40, (0, 1), 0.8, ("strips", "strips_wood2", "strips_glass2"))
        self.row(500, W, H - 40, (0, 1), 0.5, ("strips",), special={0: 1, 1: 2, 2: 0, 3: 4, 4: 5})
        for xx in range(30, W, 110):
            l = self.p("props", LAMP)
            self.add(l, xx, H - 38, shadow=False)
            self.lamps.append((xx + l.width // 2, H - 38 - l.height + 3))
        for key, n, xx, base in (("transit", 4, 120, H - 6), ("transit", 6, 700, H - 6), ("transit", 3, 380, 356),
                                 ("transit", 5, 60, 196), ("transit", 4, 600, 196)):
            self.add(self.p(key, n), xx, base, shadow=False)
        # soft shadows to the lower right, then sprites back to front
        sh = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        sd = ImageDraw.Draw(sh)
        for base, x, im, shadow in self.items:
            if shadow:
                w = im.width
                sd.polygon([(x + 6, base - 2), (x + w + 4, base - 2), (x + w + 16, base + 8), (x + 18, base + 8)], fill=(10, 20, 40, 70))
        self.img = Image.alpha_composite(self.img, sh)
        for base, x, im, _ in sorted(self.items, key=lambda t: t[0]):
            self.img.alpha_composite(im, (round(x), round(base - im.height)))
        if self.night:
            glow = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
            gd = ImageDraw.Draw(glow)
            for x, y in self.lamps:
                gd.ellipse([x - 6, y - 5, x + 6, y + 5], fill=(255, 214, 150, 110))       # halo round the head
                gd.ellipse([x - 26, H - 46, x + 26, H - 36], fill=(255, 210, 140, 70))    # pool of light on the pavement
            from PIL import ImageFilter
            glow = glow.filter(ImageFilter.GaussianBlur(6))
            for x, y in self.lamps:
                ImageDraw.Draw(glow).rectangle([x - 1, y - 1, x + 1, y + 1], fill=(255, 240, 200, 255))
            self.img = Image.alpha_composite(self.img, glow)
        return self.img.convert("RGB")


def install():
    """copy the cut parts into the Unity pixel trial (Assets/TrialPixel/Resources/PixelParts)"""
    import shutil
    dst = os.path.join(ROOT, "Assets", "TrialPixel", "Resources", "PixelParts")
    os.makedirs(dst, exist_ok=True)
    n = 0
    for f in os.listdir(os.path.join(OUT, "parts")):
        if f.endswith(".png"):
            shutil.copy(os.path.join(OUT, "parts", f), os.path.join(dst, f)); n += 1
    print(n, "parts installed")


def mock_city():
    for name, night in (("day", False), ("night", True)):
        CityMock(night).render().resize((CityMock.W * 2, CityMock.H * 2), Image.NEAREST).save(os.path.join(OUT, f"mock_city_{name}.png"))
    print("city mocks written")


def mock():
    for name, empty in (("before", True), ("after", False)):
        m = Mock(seed=5)
        day = m.render(empty).convert("RGB")
        day.resize((MW * MSCALE, MH * MSCALE), Image.NEAREST).save(os.path.join(OUT, f"mock_{name}_day.png"))
        m.night().resize((MW * MSCALE, MH * MSCALE), Image.NEAREST).save(os.path.join(OUT, f"mock_{name}_night.png"))
    print("mocks written")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "cut"
    if cmd == "gen":
        gen(sys.argv[2:] or list(PROMPTS))
    elif cmd == "mock":
        mock()
    elif cmd == "city":
        mock_city()
    elif cmd == "install":
        install()
    else:
        cut()
