"""Irregular district layout (v2) for a large, continuous cyberpunk city (no paid generation).

    python Tools/Art/district_layout_v2.py

Writes Tools/Art/painted/ui/district_layout_v2.png: a big city seen from a three-quarter aerial view, with the
eleven managed districts scattered irregularly through ordinary city fabric (grey blocks), joined by avenues and
skyways rather than packed into a grid. The game shows part of it and the player pans and zooms.
"""
import os
import random
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Tools", "Art", "painted", "ui", "district_layout_v2.png")
FONT = os.path.join(ROOT, "Assets", "Fonts", "Pretendard", "Pretendard-Bold.ttf")
W, H = 1920, 1080

# center (x, y in 0..1), rough radius (x, y), light color, landmark. Far = top.
DISTRICTS = [
    ("관리", (0.50, 0.42), (0.085, 0.10), (53, 201, 232), "AI 타워 (가장 높음)"),
    ("문화·여가", (0.47, 0.66), (0.10, 0.10), (255, 205, 120), "원형 광장·홀로그램 분수"),
    ("상업", (0.25, 0.58), (0.11, 0.09), (255, 90, 190), "네온 시장 골목"),
    ("주거", (0.24, 0.82), (0.14, 0.09), (255, 225, 170), "스택형 주거 타워"),
    ("교육", (0.73, 0.83), (0.09, 0.08), (170, 140, 255), "학교·빛나는 운동장"),
    ("치안·보안", (0.76, 0.59), (0.085, 0.08), (90, 120, 255), "감시탑·드론 격납고"),
    ("의료·생명", (0.75, 0.33), (0.08, 0.08), (120, 230, 170), "백색 의료 포드"),
    ("연구·개발", (0.55, 0.19), (0.09, 0.07), (200, 225, 255), "서버 첨탑·연구 돔"),
    ("에너지·발전", (0.20, 0.20), (0.10, 0.08), (255, 150, 60), "반응로 돔·에너지 도관"),
    ("물류·산업", (0.17, 0.38), (0.09, 0.08), (230, 200, 60), "화물 모듈·드론 도크"),
    ("유전 설계 (후반)", (0.82, 0.15), (0.07, 0.06), (150, 150, 160), "봉쇄된 공사 구덩이"),
]


def blob(cx, cy, rx, ry, seed):
    rnd = random.Random(seed)
    pts = []
    n = 9
    for i in range(n):
        a = i / n * 6.2832
        k = rnd.uniform(0.78, 1.15)
        import math
        pts.append(((cx + math.cos(a) * rx * k) * W, (cy + math.sin(a) * ry * k) * H))
    return pts


def main():
    img = Image.new("RGB", (W, H), (12, 18, 32))
    d = ImageDraw.Draw(img, "RGBA")
    big = ImageFont.truetype(FONT, 26)
    small = ImageFont.truetype(FONT, 14)
    rnd = random.Random(7)
    # ordinary city fabric everywhere: grey blocks of varying size and height
    for _ in range(900):
        x, y = rnd.uniform(0, W), rnd.uniform(0, H)
        s = rnd.uniform(10, 34) * (0.6 + 0.6 * y / H)
        g = rnd.randint(34, 58)
        d.rectangle([x, y, x + s, y + s * 0.7], fill=(g, g + 6, g + 18))
    # main avenues and skyways (irregular)
    for (a, b) in [((0.0, 0.47), (1.0, 0.52)), ((0.38, 0.0), (0.42, 1.0)), ((0.62, 0.0), (0.66, 1.0)),
                   ((0.0, 0.72), (1.0, 0.70)), ((0.0, 0.28), (1.0, 0.24)), ((0.12, 1.0), (0.30, 0.0)),
                   ((0.90, 1.0), (0.85, 0.0))]:
        d.line([(a[0] * W, a[1] * H), (b[0] * W, b[1] * H)], fill=(70, 110, 160, 160), width=10)
    hub = (0.50 * W, 0.42 * H)
    for name, (cx, cy), (rx, ry), color, landmark in DISTRICTS:
        d.line([hub, (cx * W, cy * H)], fill=(53, 201, 232, 90), width=2)
    for i, (name, (cx, cy), (rx, ry), color, landmark) in enumerate(DISTRICTS):
        poly = blob(cx, cy, rx, ry, i + 3)
        d.polygon(poly, fill=color + (80,), outline=color + (255,))
        d.text((cx * W, cy * H - 12), name, font=big, fill=(240, 245, 255), anchor="mm")
        d.text((cx * W, cy * H + 18), landmark, font=small, fill=(210, 220, 235), anchor="mm")
    # default view frame: the game opens on the middle, the rest by panning
    box = [0.18 * W, 0.14 * H, 0.82 * W, 0.90 * H]
    d.rectangle(box, outline=(255, 255, 255, 150), width=2)
    d.text((box[0] + 8, box[1] + 6), "처음 보이는 범위 (나머지는 드래그·축소로)", font=small, fill=(255, 255, 255, 210))
    d.text((W - 20, 20), "SECTOR 7 배치도 v2 · 불규칙 배치 (회색 = 일반 시가지)", font=big, fill=(240, 245, 255), anchor="ra")
    img.save(OUT)
    print(OUT)


if __name__ == "__main__":
    main()
