"""Renders PlaySense Asset Store key images (icon, card, cover, social) with Pillow.

Art: a top-down level with a time-spent heatmap and paths from eight simulated
playtests, one highlighted with its view cone, the same scene as the landing page.
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
os.makedirs(OUT, exist_ok=True)
FONT = os.path.join(HERE, "fonts", "var", "files", "archivo-latin-wdth-normal.woff2")
BODY = os.path.join(HERE, "fonts", "fontsource-ibm-plex-sans-5.1.0", "files", "ibm-plex-sans-latin-500-normal.woff")

BG = (12, 17, 32)
GRID = (26, 34, 54)
WALL = (138, 145, 160)
COLD, MID, HOT = (61, 111, 245), (245, 210, 40), (255, 90, 43)
LOOK = (61, 220, 132)
PATHS = [(108, 155, 255), (255, 157, 92), (155, 229, 100), (233, 139, 255), (95, 214, 232), (255, 209, 102), (255, 123, 156)]

# ---------------- level + simulated playtests (level units 1040 x 650) ----------------
LW, LH = 1040, 650
WALLS = [
    (40, 40, 960, 14), (40, 596, 960, 14), (40, 40, 14, 570), (986, 40, 14, 570),
    (360, 54, 14, 210), (360, 360, 14, 236), (660, 54, 14, 160), (660, 310, 14, 286),
    (54, 320, 200, 14), (674, 410, 160, 14), (900, 410, 86, 14),
]
TARGETS = [(367, 312, 17), (160, 140, 14), (520, 510, 15), (850, 150, 16), (935, 520, 17)]
ROUTE = [
    (120, 520, 0.6, None, False), (160, 420, 0, None, False), (175, 220, 2.0, 1, True),
    (280, 250, 0, None, False), (315, 312, 1.6, 0, False), (460, 312, 0, None, False),
    (515, 455, 1.3, 2, False), (560, 270, 0, None, False), (720, 262, 0, None, False),
    (830, 205, 2.6, 3, False), (760, 360, 0, None, False), (860, 490, 0.8, None, False),
    (935, 505, 1.0, 4, False),
]


class Rng:
    def __init__(self, seed):
        self.s = seed

    def __call__(self):
        self.s = (self.s * 1664525 + 1013904223) & 0xFFFFFFFF
        return self.s / 4294967296


def turn(a, b, rate):
    return a + math.atan2(math.sin(b - a), math.cos(b - a)) * rate


def simulate(seed, primary):
    r = Rng(seed)
    route = []
    for x, y, wait, look, skippable in ROUTE:
        if not primary and skippable and r() < 0.8:
            continue
        j = 0 if primary else 38
        route.append((x + (r() - 0.5) * j, y + (r() - 0.5) * j, wait * (1 if primary else 0.5 + r()), look))
    out, h, dt = [], -math.pi / 2, 1 / 30
    speed = 160 if primary else 130 + r() * 70
    for i, (x, y, wait, look) in enumerate(route):
        if i:
            ax, ay = route[i - 1][0], route[i - 1][1]
            steps = max(1, round(math.hypot(x - ax, y - ay) / speed / dt))
            want = math.atan2(y - ay, x - ax)
            for s in range(1, steps + 1):
                f = s / steps
                e = f * f * (3 - 2 * f)
                h = turn(h, want, 0.18)
                out.append((ax + (x - ax) * e, ay + (y - ay) * e, h, -1))
        for k in range(round(wait / dt)):
            if look is not None:
                tx, ty, _ = TARGETS[look]
                w = math.atan2(ty - y, tx - x) + math.sin(k / 9) * 0.12
            else:
                w = h + math.sin(k / 7) * 0.06
            h = turn(h, w, 0.12)
            out.append((x + math.sin(k / 5) * 2, y + math.cos(k / 6) * 2, h, look if look is not None else -1))
    return out


PRIMARY = simulate(7, True)
CROWD = [simulate(101 + c * 977, False) for c in range(7)]
CELL = 26
COLS, ROWS = math.ceil(LW / CELL), math.ceil(LH / CELL)
HEAT = [0.0] * (COLS * ROWS)
for run in CROWD + [PRIMARY]:
    for x, y, _, _ in run:
        cx, cy = int(x // CELL), int(y // CELL)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                ix, iy = cx + dx, cy + dy
                if 0 <= ix < COLS and 0 <= iy < ROWS:
                    HEAT[iy * COLS + ix] += 0.034 if (dx == 0 and dy == 0) else 0.012
HEAT_MAX = max(HEAT)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def heat_color(f):
    return lerp(COLD, MID, f * 2) if f < 0.5 else lerp(MID, HOT, (f - 0.5) * 2)


# ---------------- art ----------------
def draw_level(w, h, focus=0.45, margin=0.0, crowd=True, labels=False):
    """Draws the level fitted into w x h (supersampled by caller). focus = how far the highlighted run has played."""
    img = Image.new("RGB", (w, h), BG)
    over = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    scale = min(w / LW, h / LH) * (1 - margin)
    ox, oy = (w - LW * scale) / 2, (h - LH * scale) / 2

    def P(x, y):
        return ox + x * scale, oy + y * scale

    # grid
    step = CELL * scale
    x = ox + 54 * scale
    while x < ox + 986 * scale:
        d.line([(x, oy + 54 * scale), (x, oy + 596 * scale)], fill=GRID + (255,), width=max(1, int(scale)))
        x += step
    y = oy + 54 * scale
    while y < oy + 596 * scale:
        d.line([(ox + 54 * scale, y), (ox + 986 * scale, y)], fill=GRID + (255,), width=max(1, int(scale)))
        y += step

    # heat
    for iy in range(ROWS):
        for ix in range(COLS):
            v = HEAT[iy * COLS + ix]
            if v < 0.02:
                continue
            f = math.sqrt(min(1, v / HEAT_MAX))
            c = heat_color(f)
            a = int(255 * (0.16 + f * 0.62))
            x0, y0 = P(ix * CELL + 1, iy * CELL + 1)
            x1, y1 = P(ix * CELL + CELL - 1, iy * CELL + CELL - 1)
            d.rounded_rectangle([x0, y0, x1, y1], radius=max(1, 3 * scale), fill=c + (a,))
    img = Image.alpha_composite(img.convert("RGBA"), over)

    # soft glow of the heat
    glow = img.filter(ImageFilter.GaussianBlur(radius=10 * scale))
    img = Image.blend(img, glow, 0.35).convert("RGB")
    d = ImageDraw.Draw(img, "RGBA")

    if crowd:
        for i, run in enumerate(CROWD):
            pts = [P(x, y) for x, y, _, _ in run[::2]]
            d.line(pts, fill=PATHS[i] + (95,), width=max(2, int(2.2 * scale)), joint="curve")

    for x, y, ww, hh in WALLS:
        x0, y0 = P(x, y)
        x1, y1 = P(x + ww, y + hh)
        d.rectangle([x0, y0, x1, y1], fill=WALL)

    upto = int(len(PRIMARY) * focus)
    cx, cy, ch, look = PRIMARY[upto]

    for i, (tx, ty, tr) in enumerate(TARGETS):
        a, b = P(tx - tr, ty - tr), P(tx + tr, ty + tr)
        on = i == look
        d.ellipse([a, b], fill=BG, outline=LOOK if on else (184, 188, 196), width=max(2, int((4 if on else 2) * scale)))

    pts = [P(x, y) for x, y, _, _ in PRIMARY[:upto:2]] + [P(cx, cy)]
    d.line(pts, fill=(255, 255, 255, 240), width=max(3, int(4.5 * scale)), joint="curve")

    # view cone (attention angle 15 degrees either side)
    half, length = math.radians(15), 150
    cone = [P(cx, cy)]
    for k in range(25):
        a = ch - half + 2 * half * k / 24
        cone.append(P(cx + math.cos(a) * length, cy + math.sin(a) * length))
    d.polygon(cone, fill=LOOK + (70,) if look >= 0 else (255, 255, 255, 34))
    if look >= 0:
        tx, ty, _ = TARGETS[look]
        dash(d, P(cx, cy), P(tx, ty), LOOK, max(2, int(2.5 * scale)), 9 * scale)

    r = 12 * scale
    px, py = P(cx, cy)
    d.ellipse([px - r, py - r, px + r, py + r], fill=(255, 255, 255), outline=(76, 127, 255), width=max(2, int(4 * scale)))
    return img


def dash(d, a, b, color, width, seg):
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    n = int(length // (seg * 2))
    for i in range(n):
        t0, t1 = (2 * i) * seg / length, (2 * i + 1) * seg / length
        d.line([(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0), (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)], fill=color, width=width)


def font(size, weight=850, width=125):
    f = ImageFont.truetype(FONT, size)
    f.set_variation_by_axes([weight, width])
    return f


def vignette(img, solid=0.30, clear=0.66):
    w, h = img.size
    grad = Image.new("L", (w, 1))
    for x in range(w):
        t = x / (w - 1)
        k = 1.0 if t <= solid else max(0.0, 1 - (t - solid) / (clear - solid))
        grad.putpixel((x, 0), int(255 * (k * k * (3 - 2 * k))))
    grad = grad.resize((w, h))
    dark = Image.new("RGBA", (w, h), BG + (255,))
    return Image.composite(dark, img.convert("RGBA"), grad)


def heat_text(base, xy, text, fnt):
    """Draws text filled with the cold-mid-hot heat gradient."""
    mask = Image.new("L", base.size, 0)
    ImageDraw.Draw(mask).text(xy, text, font=fnt, fill=255)
    bbox = mask.getbbox()
    grad = Image.new("RGBA", base.size)
    gd = ImageDraw.Draw(grad)
    x0, x1 = bbox[0], bbox[2]
    for x in range(x0, x1 + 1):
        t = (x - x0) / max(1, x1 - x0)
        gd.line([(x, bbox[1]), (x, bbox[3])], fill=heat_color(t) + (255,))
    base.paste(grad, (0, 0), mask)


def save(img, size, name):
    img.convert("RGB").resize(size, Image.LANCZOS).save(os.path.join(OUT, name), optimize=True)
    print("wrote", name, size)


SS = 2  # supersample

# ---- Cover 1950 x 1300: title + tag line allowed ----
W, H = 1950 * SS, 1300 * SS
art = draw_level(W, H, focus=0.47, margin=-0.05)
art = vignette(art, 0.30, 0.62)
d = ImageDraw.Draw(art)
d.text((130 * SS, 470 * SS), "PlaySense", font=font(176 * SS), fill=(237, 241, 248))
heat_text(art, (136 * SS, 700 * SS), "Every playtest,", font(84 * SS, 760, 116))
heat_text(art, (136 * SS, 800 * SS), "replayed.", font(84 * SS, 760, 116))
save(art, (1950, 1300), "PlaySense_Cover_1950x1300.png")

# ---- Card 420 x 280: title only ----
W, H = 420 * 4, 280 * 4
art = draw_level(W, H, focus=0.47, margin=-0.02)
art = vignette(art, 0.28, 0.68)
d = ImageDraw.Draw(art)
d.text((84, 420), "PlaySense", font=font(176), fill=(237, 241, 248))
heat_text(art, (90, 640), "Playtest analytics", font(80, 700, 112))
save(art, (420, 280), "PlaySense_Card_420x280.png")

# ---- Social 1200 x 630: no text ----
W, H = 1200 * SS, 630 * SS
art = draw_level(W, H, focus=0.47, margin=-0.06)
save(art, (1200, 630), "PlaySense_Social_1200x630.png")

# ---- Icon 160 x 160: no text, the heat-grid mark ----
S = 160 * 8
icon = Image.new("RGBA", (S, S), BG + (255,))
d = ImageDraw.Draw(icon)
pad = 70
d.rounded_rectangle([pad, pad, S - pad, S - pad], radius=230, fill=(27, 36, 64))
cell = 230
cells = [
    (0, 2, COLD, 1.0), (1, 2, MID, 0.55), (1, 1, MID, 1.0), (2, 1, HOT, 0.6), (2, 0, HOT, 1.0), (0, 1, COLD, 0.45), (1, 0, MID, 0.4),
]
x0 = (S - cell * 3 - 40) // 2
y0 = x0
for cx_, cy_, col, alpha in cells:
    x = x0 + cx_ * (cell + 20)
    y = y0 + cy_ * (cell + 20)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(layer).rounded_rectangle([x, y, x + cell, y + cell], radius=46, fill=col + (int(255 * alpha),))
    icon = Image.alpha_composite(icon, layer)
d = ImageDraw.Draw(icon)
# path through the heat, ending in the player dot
path = [(x0 + 0.5 * cell, y0 + 2.5 * cell + 40), (x0 + 1.5 * cell + 20, y0 + 1.5 * cell + 20), (x0 + 2.5 * cell + 40, y0 + 0.5 * cell)]
d.line(path, fill=(255, 255, 255), width=46, joint="curve")
px, py = path[-1]
d.ellipse([px - 70, py - 70, px + 70, py + 70], fill=(255, 255, 255), outline=(12, 17, 32), width=26)
save(icon, (160, 160), "PlaySense_Icon_160x160.png")
