"""UI kit textures for the operations screen: 9-slice panel frames with cut corners and glow, buttons,
gradient fills and screen shades. Written to Assets/Resources/UiKit/ (imported smooth, uncompressed).

    python Tools/Art/ui_kit.py
"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "UiKit")

CYAN = (64, 208, 224)
SKY = (120, 190, 245)
ORANGE = (247, 147, 59)
NAVY = (8, 16, 32)

SS = 4  # supersampling for smooth diagonals


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))


def chamfer(w, h, cut, inset=0.0):
    """Polygon of a w x h box with the top-left and bottom-right corners cut off."""
    i = inset
    return [(i + cut, i), (w - i, i), (w - i, h - i - cut), (w - i - cut, h - i), (i, h - i), (i, i + cut)]


def frame(name, size=96, cut=12, pad=6, fill=(8, 16, 32, 200), top=(22, 44, 72, 215), edge=CYAN, edge_alpha=150,
          glow=70, accent=True):
    """A panel: glow outside, gradient fill, 1px edge, bright accents on the cut corners. pad = glow margin."""
    W = size * SS
    inner = (size - 2 * pad) * SS
    o = pad * SS

    # gradient fill clipped to the chamfered shape
    grad = Image.new("RGBA", (inner, inner))
    gd = ImageDraw.Draw(grad)
    for y in range(inner):
        t = y / max(1, inner - 1)
        c = tuple(int(top[k] + (fill[k] - top[k]) * min(1.0, t * 1.6)) for k in range(4))
        gd.line([(0, y), (inner, y)], fill=c)
    mask = Image.new("L", (inner, inner), 0)
    ImageDraw.Draw(mask).polygon(chamfer(inner, inner, cut * SS), fill=255)
    body = Image.new("RGBA", (inner, inner), (0, 0, 0, 0))
    body.paste(grad, (0, 0), mask)

    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    # glow: blurred edge outline
    if glow:
        g = Image.new("RGBA", (W, W), (0, 0, 0, 0))
        ImageDraw.Draw(g).polygon([(x + o, y + o) for x, y in chamfer(inner, inner, cut * SS)], outline=edge + (glow,), width=3 * SS)
        g = g.filter(ImageFilter.GaussianBlur(3 * SS))
        img.alpha_composite(g)
    img.alpha_composite(body, (o, o))
    d = ImageDraw.Draw(img)
    pts = [(x + o, y + o) for x, y in chamfer(inner, inner, cut * SS, inset=SS / 2)]
    d.line(pts + [pts[0]], fill=edge + (edge_alpha,), width=SS)
    if accent:
        c = cut * SS
        # bright strokes along the two cut corners and a short tick on the top edge
        d.line([(o, o + c + 10 * SS), (o, o + c), (o + c, o), (o + c + 10 * SS, o)], fill=edge + (255,), width=2 * SS)
        e = o + inner
        d.line([(e, e - c - 10 * SS), (e, e - c), (e - c, e), (e - c - 10 * SS, e)], fill=edge + (255,), width=2 * SS)
    save(img.resize((size, size), Image.LANCZOS), name)


def button(name, base, edge, size=48, cut=7):
    W = size * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    grad = Image.new("RGBA", (W, W))
    gd = ImageDraw.Draw(grad)
    for y in range(W):
        t = y / (W - 1)
        k = 1.0 - 0.45 * t
        gd.line([(0, y), (W, y)], fill=(int(base[0] * k), int(base[1] * k), int(base[2] * k), base[3]))
    mask = Image.new("L", (W, W), 0)
    ImageDraw.Draw(mask).polygon(chamfer(W, W, cut * SS), fill=255)
    img.paste(grad, (0, 0), mask)
    d = ImageDraw.Draw(img)
    pts = chamfer(W, W, cut * SS, inset=SS / 2)
    d.line(pts + [pts[0]], fill=edge, width=SS)
    d.line([(cut * SS + 2 * SS, 2 * SS), (W - 4 * SS, 2 * SS)], fill=(255, 255, 255, 50), width=SS)
    save(img.resize((size, size), Image.LANCZOS), name)


def hgrad(name, left, right, w=256, h=8):
    img = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(img)
    for x in range(w):
        t = x / (w - 1)
        d.line([(x, 0), (x, h)], fill=tuple(int(left[k] + (right[k] - left[k]) * t) for k in range(4)))
    save(img, name)


def vgrad(name, top, bottom, w=8, h=256):
    img = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / (h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(top[k] + (bottom[k] - top[k]) * t) for k in range(4)))
    save(img, name)


def band(name, color, w=8, h=64):
    """A soft horizontal band: transparent, color in the middle, transparent."""
    img = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = 1.0 - abs(y / (h - 1) * 2.0 - 1.0)
        d.line([(0, y), (w, y)], fill=color[:3] + (int(color[3] * t ** 2),))
    save(img, name)


def main():
    frame("panel")
    frame("panel_strong", fill=(6, 12, 26, 235), top=(18, 40, 66, 240), edge_alpha=190, glow=90)
    frame("panel_warn", edge=ORANGE, top=(46, 30, 24, 220), glow=80)
    frame("chip", size=40, cut=6, pad=3, fill=(10, 20, 38, 210), top=(18, 36, 60, 220), edge=SKY, edge_alpha=110, glow=0, accent=False)
    button("button", (20, 90, 120, 230), CYAN + (220,))
    button("button_soft", (16, 34, 60, 220), SKY + (120,))
    button("button_hot", (120, 64, 24, 230), ORANGE + (230,))
    hgrad("fill_cyan", (40, 140, 200, 255), (90, 235, 245, 255))
    hgrad("fill_sky", (70, 110, 190, 255), (150, 205, 250, 255))
    hgrad("fill_orange", (190, 80, 40, 255), (250, 170, 80, 255))
    hgrad("fill_red", (170, 40, 50, 255), (245, 110, 100, 255))
    hgrad("title_line", CYAN + (200,), CYAN + (0,))
    vgrad("page_bg", (20, 34, 60, 255), (11, 20, 38, 255))
    hgrad("shade_left", (8, 16, 30, 215), (8, 16, 30, 0))
    hgrad("shade_right", (8, 16, 30, 0), (8, 16, 30, 215))
    vgrad("shade_top", (2, 6, 14, 235), (2, 6, 14, 0))
    vgrad("shade_bottom", (2, 6, 14, 0), (2, 6, 14, 240))
    band("scan_band", (53, 201, 232, 70))
    print("ui kit written to", OUT)


if __name__ == "__main__":
    main()
