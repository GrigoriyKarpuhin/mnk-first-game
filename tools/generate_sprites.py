#!/usr/bin/env python3
"""Block C — moodboard-grade pixel art generator (64x64).

Dark industrial prison: green-grey concrete, black guards with square white
visors, grey prison uniforms with stencil numbers, CRT-green glow accents.
Characters follow the Block C dossier concept sheets:
  C-4821 player      — open-collar shirt, rolled sleeves, drawstring pants,
                        thigh cargo patch with number
  C-3071 programmer  — hooded coverall, round glasses, gadget belt with green
                        mini-CRT, pens in chest pocket, wrist device
  C-075  prisoner2   — tied-off shirt, bare midriff, dog tags, chain on belt,
                        tall laced boots
Realistic ~1:6 proportions, no contact shadows under characters.

Each character: 3 views x 3 frames:
    <base>.png, <base>_walk_1/2.png            (front / down)
    <base>_side[_walk_1/2].png                 (profile, faces LEFT)
    <base>_up[_walk_1/2].png                   (back)

Run from repo root:  python3 tools/generate_sprites.py
Overwrites PNGs in Assets/Resources/Sprites (PPU auto = png width).
"""
import math
import random
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "Sprites"

# ---------------------------------------------------------------- palette --
def cl(v):
    return max(0, min(255, int(v)))


def mul(c, f, gshift=0.0):
    """Scale brightness; shadows drift toward green (gshift>0)."""
    r, g, b = c[:3]
    return (cl(r * f * (1 - gshift)), cl(g * f), cl(b * f * (1 - gshift * 0.6)))


def ramp(c):
    """5-tone ramp: d2 d1 m l1 l2, green-tinted shadows, cool highlights."""
    return {
        "d2": mul(c, 0.45, 0.18), "d1": mul(c, 0.68, 0.10), "m": c,
        "l1": mul(c, 1.25, -0.04), "l2": mul(c, 1.55, -0.08),
    }


OUTLINE = (6, 9, 7)
RIM = (96, 128, 102)            # cold green rim light (top-left key light)
DEEP = (10, 13, 11)

CONCRETE = ramp((44, 50, 44))
GREEN = {"glow": (130, 235, 150), "hi": (88, 196, 112),
         "m": (52, 128, 74), "d": (24, 52, 34), "d2": (14, 30, 20)}

SHIRT = ramp((94, 96, 84))      # prison shirt, worn grey-olive
PANTS = ramp((72, 74, 64))      # darker cargo pants
COVERALL = ramp((54, 64, 52))   # programmer hooded coverall
BLACK = ramp((24, 26, 30))      # guard fatigues
HELMET = ramp((17, 18, 21))
VISOR_W = (212, 216, 207)
SKIN = ramp((164, 134, 110))
STEEL = ramp((110, 116, 114))
GUNMETAL = ramp((62, 66, 72))
STENCIL = (158, 160, 144)
PAPER = ramp((150, 150, 137))
BOOT = ramp((34, 33, 29))


# ----------------------------------------------------------------- canvas --
class C:
    def __init__(self, w, h, bg=(0, 0, 0, 0)):
        self.im = Image.new("RGBA", (w, h), bg)
        self.px = self.im.load()
        self.w, self.h = w, h

    def s(self, x, y, c, a=255):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = (c[0], c[1], c[2], a)

    def r(self, x0, y0, x1, y1, c, a=255):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.s(x, y, c, a)

    def dith(self, x0, y0, x1, y1, c1, c2):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.s(x, y, c1 if (x + y) % 2 else c2)

    def vline(self, x, y0, y1, c):
        self.r(x, y0, x, y1, c)

    def hline(self, x0, x1, y, c):
        self.r(x0, y, x1, y, c)

    def blend(self, x, y, c, t):
        if 0 <= x < self.w and 0 <= y < self.h:
            p = self.px[x, y]
            if p[3] == 0:
                return
            self.px[x, y] = (cl(p[0] + (c[0] - p[0]) * t),
                             cl(p[1] + (c[1] - p[1]) * t),
                             cl(p[2] + (c[2] - p[2]) * t), p[3])

    def opaque(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h and self.px[x, y][3] > 0

    def clear(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = (0, 0, 0, 0)

    def chamfer(self, x0, y0, x1, y1, top=2, bottom=1):
        """Knock off box corners so heads/helmets read round."""
        for i in range(top):
            for j in range(top - i):
                self.clear(x0 + j, y0 + i)
                self.clear(x1 - j, y0 + i)
        for i in range(bottom):
            for j in range(bottom - i):
                self.clear(x0 + j, y1 - i)
                self.clear(x1 - j, y1 - i)

    def rim_light(self, c=RIM, t=0.75):
        """Lighten opaque pixels whose top/left neighbour is transparent."""
        hits = [(x, y) for y in range(self.h) for x in range(self.w)
                if self.opaque(x, y)
                and (not self.opaque(x - 1, y) or not self.opaque(x, y - 1))]
        for x, y in hits:
            self.blend(x, y, c, t)

    def outline(self, c=OUTLINE):
        add = [(x, y) for y in range(self.h) for x in range(self.w)
               if not self.opaque(x, y)
               and any(self.opaque(x + dx, y + dy)
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
        for x, y in add:
            self.s(x, y, c)

    def shadow_ellipse(self, cx, cy, rx, ry, a=90):
        for y in range(cy - ry, cy + ry + 1):
            for x in range(cx - rx, cx + rx + 1):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1:
                    if not self.opaque(x, y):
                        self.s(x, y, DEEP, a)

    def save(self, name):
        self.im.save(OUT / name)
        print("wrote", name, self.im.size)


def limb(c, pts, w, col, edge=None, shade=None):
    """Thick poly-segment limb: pts [(x,y),...] hip->knee->foot."""
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = max(1, abs(y1 - y0))
        for i in range(n + 1):
            y = y0 + (1 if y1 >= y0 else -1) * i
            x = round(x0 + (x1 - x0) * i / n)
            c.r(x, y, x + w - 1, y, col)
            if edge:
                c.s(x, y, edge)
            if shade:
                c.s(x + w - 1, y, shade)


def noise_fill(c, base, amp, seed, x0=0, y0=0, x1=None, y1=None):
    rng = random.Random(seed)
    x1 = c.w - 1 if x1 is None else x1
    y1 = c.h - 1 if y1 is None else y1
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            n = rng.randint(-amp, amp)
            c.s(x, y, tuple(cl(v + n) for v in base))
    return rng


def stencil_text(c, x, y, cols, col=STENCIL):
    for i, on in enumerate(cols):
        if on:
            c.s(x + i, y, col)
            c.s(x + i, y + 1, col)


# ------------------------------------------------------------- characters --
# Realistic ~1:6 proportions. Ground y=59, centre x=31/32. No foot shadows.
GROUND = 59


def boots_front(c, x0, x1, ytop, tall=False, P=None):
    B = BOOT
    if tall:                                       # laced shaft above foot
        c.r(x0, ytop - 5, x1, ytop - 1, B["d1"])
        c.vline(x0, ytop - 5, ytop - 1, B["m"])
        for yy in range(ytop - 5, ytop - 1, 2):    # lace dots
            c.s((x0 + x1) // 2, yy, B["l1"])
    c.r(x0, ytop, x1, ytop + 4, B["m"])
    c.hline(x0, x1, ytop, B["l1"])                 # ankle catch-light
    c.hline(x0, x1, ytop + 4, B["d2"])             # sole
    c.s(x0, ytop + 3, B["l1"])                     # toe scuff


def front_legs(c, P, frame, boots="low", thigh_patch=False, female=False):
    """y34 hips .. y59 ground."""
    if female:                                    # wider, rounded hips
        c.r(25, 34, 38, 37, P["m"])
        c.clear(25, 34); c.clear(38, 34)
        c.vline(25, 35, 37, P["l1"])
        c.vline(38, 35, 37, P["d1"])
    else:
        c.r(26, 34, 37, 37, P["m"])
        c.vline(26, 34, 37, P["l1"])
        c.vline(37, 34, 37, P["d1"])
    c.vline(31, 35, 38, P["d2"])                  # crotch crease
    c.vline(32, 35, 38, P["d1"])
    tall = boots == "tall"
    for x0, x1, idx in ((26, 30, 0), (33, 37, 1)):
        lifted = (frame == 1 and idx == 0) or (frame == 2 and idx == 1)
        yb = 50 if lifted else 54
        c.r(x0, 37, x1, yb, P["m"])
        c.vline(x0, 37, yb, P["l1"] if idx == 0 else P["m"])
        c.vline(x1, 37, yb, P["d1"])
        if lifted:
            c.hline(x0 + 1, x1, 43, P["d1"])      # bent knee crease
            c.hline(x0, x1, 44, P["d2"])
        else:
            c.hline(x0 + 1, x1 - 1, 45, P["d1"])  # knee
        if idx == 1:                              # shadow-side dither
            c.dith(x1 - 1, 38, x1, yb - 1, P["d1"], P["m"])
        if thigh_patch and idx == 1:              # cargo patch w/ number
            c.r(33, 40, 36, 44, P["d1"])
            c.hline(33, 36, 40, P["d2"])
            c.s(34, 42, STENCIL); c.s(36, 42, STENCIL)
        boots_front(c, x0, x1, yb, tall=tall, P=P)


def front_torso(c, S, bob, crop=31, female=False, bust=True):
    """Shirt down to y<crop> (24 for tied shirt, 31 full)."""
    o = 1 if female else 0                        # narrower frame
    c.r(24 + o, 15 + bob, 39 - o, 17 + bob, S["m"])        # shoulders
    c.hline(24 + o, 39 - o, 15 + bob, S["l1"])
    c.r(25 + o, 17 + bob, 38 - o, min(26, crop) + bob, S["m"])   # chest
    if crop > 26:
        c.r(26 + o, 26 + bob, 37 - o, crop + bob, S["m"])  # waist taper
        c.vline(26 + o, 26 + bob, crop + bob, S["l1"])
        c.vline(37 - o, 26 + bob, crop + bob, S["d1"])
        c.hline(27 + o, 36 - o, 28 + bob, S["d1"])         # waist crease
    c.vline(25 + o, 17 + bob, min(26, crop) + bob, S["l1"])
    c.vline(38 - o, 17 + bob, min(26, crop) + bob, S["d1"])
    c.dith(36 - o, 18 + bob, 37 - o, min(26, crop) + bob, S["d1"], S["m"])
    if female and bust:                           # bust shading
        c.s(28, 19 + bob, S["l1"]); c.s(29, 19 + bob, S["l1"])
        c.s(33, 19 + bob, S["m"]); c.s(34, 19 + bob, S["m"])
        c.hline(28, 30, 21 + bob, S["d1"])        # under-bust shadow
        c.hline(33, 35, 21 + bob, S["d1"])
        c.s(31, 20 + bob, S["d1"]); c.s(32, 20 + bob, S["d1"])


def belt_front(c, kind, P, bob=0):
    if kind == "drawstring":
        c.r(26, 32, 37, 33, P["d1"])
        c.hline(26, 37, 32, P["m"])
        c.vline(31, 33, 36, P["l1"])              # hanging strings
        c.vline(33, 33, 35, P["l1"])
    elif kind == "chain":
        c.r(26, 32, 37, 33, BOOT["m"])
        c.hline(26, 37, 32, BOOT["l1"])
        for x, y in ((26, 34), (25, 35), (25, 36), (26, 37), (27, 37)):
            c.s(x, y, STEEL["l1"])                # hanging chain loop
            if y % 2:
                c.s(x, y, STEEL["d1"])
    elif kind == "gear":
        c.r(26, 32, 37, 33, HELMET["m"])
        c.hline(26, 37, 32, HELMET["l1"])
        c.r(34, 29 + bob, 37, 33, GUNMETAL["d1"])  # belt device
        c.r(35, 30 + bob, 36, 31 + bob, GREEN["d"])
        c.s(35, 30 + bob, GREEN["glow"])          # mini-CRT
        c.vline(37, 34, 37, GUNMETAL["d2"])       # cable
    else:                                          # buckle
        c.r(26, 32, 37, 33, BOOT["m"])
        c.hline(26, 37, 32, BOOT["l1"])
        c.s(31, 32, STEEL["m"]); c.s(32, 32, STEEL["d1"])


def front_arms(c, S, frame, bob, sleeves="long", glove=None, wrist=False):
    """Opposite arm-leg swing; +fwd = hand lower."""
    sw = {0: (0, 0), 1: (-2, 2), 2: (2, -2)}[frame]
    for idx, ax in enumerate((22, 39)):
        dy = sw[idx]
        if sleeves == "short":                    # bare arm, sleeve stub
            c.r(ax, 16 + bob, ax + 2, 19 + bob, S["d1" if idx else "m"])
            c.hline(ax, ax + 2, 19 + bob, S["d2"])
            c.r(ax, 20 + bob, ax + 2, 26 + bob, SKIN["d1" if idx else "m"])
            c.vline(ax if idx == 0 else ax + 2, 20 + bob, 26 + bob,
                    SKIN["l1"] if idx == 0 else SKIN["d2"])
        else:
            c.r(ax, 16 + bob, ax + 2, 26 + bob, S["d1" if idx else "m"])
            c.vline(ax if idx == 0 else ax + 2, 16 + bob, 26 + bob,
                    S["l1"] if idx == 0 else S["d2"])
        fx = ax if idx == 0 else ax + 1            # forearm 2px
        if sleeves == "long":
            c.r(fx, 26 + bob, fx + 1, 35 + bob + dy, S["d1"])
            c.vline(fx if idx == 0 else fx + 1, 26 + bob, 35 + bob + dy,
                    S["m"] if idx == 0 else S["d2"])
            c.hline(fx, fx + 1, 35 + bob + dy, S["d2"])    # cuff
        else:                                      # rolled / short: skin
            if sleeves == "rolled":
                c.hline(fx, fx + 1, 26 + bob, S["l1"])     # rolled cuff
            c.r(fx, 27 + bob, fx + 1, 35 + bob + dy,
                SKIN["m"] if idx == 0 else SKIN["d1"])
            c.vline(fx if idx == 0 else fx + 1, 27 + bob, 35 + bob + dy,
                    SKIN["l1"] if idx == 0 else SKIN["d2"])
        hcol = glove if glove else SKIN["m"]
        c.r(fx, 36 + bob + dy, fx + 1, 38 + bob + dy, hcol)
        c.s(fx, 36 + bob + dy, mul(hcol, 1.2))
        if wrist and idx == 0:                    # wrist device, left arm
            c.r(fx - 1, 31 + bob + dy, fx + 2, 33 + bob + dy, GUNMETAL["d1"])
            c.s(fx, 32 + bob + dy, GREEN["hi"])
            c.s(fx + 1, 32 + bob + dy, GREEN["d"])


def front_head(c, hair, hairstyle, bob, glasses=False):
    S = SKIN
    c.r(30, 12 + bob, 33, 15 + bob, S["d1"])      # neck in shadow
    c.r(28, 5 + bob, 35, 12 + bob, S["m"])        # face
    c.vline(28, 6 + bob, 11 + bob, S["l1"])
    c.vline(35, 6 + bob, 12 + bob, S["d1"])
    c.hline(29, 34, 12 + bob, S["d1"])            # jaw
    c.s(29, 7 + bob, S["d1"]); c.s(34, 7 + bob, S["d1"])  # brow corners
    c.s(29, 8 + bob, (26, 22, 20)); c.s(30, 8 + bob, (26, 22, 20))   # eyes
    c.s(33, 8 + bob, (26, 22, 20)); c.s(34, 8 + bob, (26, 22, 20))
    c.s(31, 9 + bob, S["d1"]); c.s(32, 9 + bob, S["d1"])             # nose
    c.s(34, 10 + bob, S["d2"])                    # cheek core shadow
    c.s(31, 11 + bob, S["d1"]); c.s(32, 11 + bob, S["d1"])           # mouth
    H = ramp(hair) if hair else None
    if H:
        if hairstyle in ("messy", "curly"):
            c.r(27, 3 + bob, 36, 5 + bob, H["m"])
            c.hline(28, 35, 2 + bob, H["m"])
            c.r(27, 5 + bob, 28, 8 + bob, H["d1"])         # temples
            c.r(35, 5 + bob, 36, 8 + bob, H["d1"])
            for x in (29, 32, 34):
                c.s(x, 5 + bob, H["m"])           # ragged fringe
            c.r(27, 3 + bob, 28, 4 + bob, H["l1"])
            if hairstyle == "curly":              # extra volume
                c.hline(28, 35, 1 + bob, H["m"])
                c.s(27, 2 + bob, H["m"]); c.s(36, 2 + bob, H["d1"])
                for x in (28, 31, 34):
                    c.s(x, 2 + bob, H["l1"])      # curl glints
        elif hairstyle == "bald":
            c.r(28, 3 + bob, 35, 5 + bob, S["m"])          # bare crown
            c.hline(29, 34, 2 + bob, S["m"])
            c.s(29, 3 + bob, S["l1"]); c.s(30, 3 + bob, S["l1"])
            c.r(27, 6 + bob, 28, 10 + bob, H["m"])         # hair ring
            c.r(35, 6 + bob, 36, 10 + bob, H["d1"])
        elif hairstyle == "tied":
            c.r(27, 3 + bob, 36, 5 + bob, H["m"])
            c.hline(28, 35, 2 + bob, H["m"])
            c.r(29, 1 + bob, 34, 2 + bob, H["m"])          # updo mass
            c.s(30, 0 + bob, H["m"]); c.s(33, 0 + bob, H["d1"])
            c.hline(28, 35, 5 + bob, H["d1"])              # slick line
            c.r(27, 5 + bob, 27, 9 + bob, H["m"])
            c.r(36, 5 + bob, 36, 9 + bob, H["d1"])
            c.s(28, 6 + bob, H["d1"])                      # loose strand
    if glasses:
        for gx in (28, 33):
            c.r(gx, 7 + bob, gx + 2, 8 + bob, (28, 32, 30))
            c.s(gx + 1, 7 + bob, GREEN["d"])
            c.s(gx + 1, 8 + bob, GREEN["hi"])     # CRT glint
        c.s(31, 7 + bob, (28, 32, 30)); c.s(32, 7 + bob, (28, 32, 30))
    c.chamfer(27, 2 + bob, 36, 12 + bob, top=2, bottom=1)


def char_front(cfg, frame=0):
    c = C(64, 64)
    bob = -1 if frame else 0
    S, P = cfg["shirt"], cfg["pants"]
    fem = cfg.get("female", False)
    front_legs(c, P, frame, cfg.get("boots", "low"), cfg.get("thigh_patch"),
               female=fem)
    midriff = cfg.get("midriff")
    front_torso(c, S, bob, crop=24 if midriff else 31, female=fem)
    if midriff:
        w0, w1 = (28, 35) if fem else (27, 36)    # waist dip
        c.r(w0, 25 + bob, w1, 31, SKIN["m"])      # bare midriff
        c.vline(w0, 25 + bob, 31, SKIN["l1"])
        c.vline(w1, 25 + bob, 31, SKIN["d1"])
        c.hline(30, 33, 28, SKIN["d1"])           # abs hint
        c.s(31, 30, SKIN["d1"])                   # navel
        c.r(30, 23 + bob, 33, 25 + bob, S["d1"])  # shirt knot
        c.s(31, 24 + bob, S["l1"])
    belt_front(c, cfg.get("belt", "buckle"), P, bob)
    if cfg.get("hoodie"):
        c.r(23, 14 + bob, 40, 18 + bob, S["d1"])  # bunched hood
        c.hline(24, 39, 14 + bob, S["l1"])
        c.hline(23, 40, 18 + bob, S["d2"])
        c.vline(29, 19 + bob, 24 + bob, S["d2"])  # drawstrings
        c.vline(34, 19 + bob, 24 + bob, S["d2"])
    else:
        # open collar V with bare chest
        c.r(30, 15 + bob, 33, 17 + bob, SKIN["d1"])
        c.s(31, 18 + bob, SKIN["d1"]); c.s(32, 18 + bob, SKIN["d2"])
        c.vline(29, 15 + bob, 17 + bob, S["d2"])  # collar flaps
        c.vline(34, 15 + bob, 17 + bob, S["d2"])
        for by in (20, 23, 26, 29):               # button line
            if by < (24 if midriff else 31):
                c.s(31, by + bob, S["d2"])
    if cfg.get("dogtag"):
        c.s(30, 16 + bob, STEEL["d1"]); c.s(33, 16 + bob, STEEL["d1"])
        c.s(31, 19 + bob, STEEL["m"]); c.s(32, 20 + bob, STEEL["l1"])
    if cfg.get("pens"):                           # chest pocket w/ pens
        c.r(27, 22 + bob, 29, 24 + bob, S["d1"])
        c.hline(27, 29, 22 + bob, S["d2"])
        c.s(27, 21 + bob, STEEL["l1"])
        c.s(29, 21 + bob, (170, 110, 70))
    if cfg.get("number") and not cfg.get("hoodie"):
        stencil_text(c, 33, 19 + bob, (1, 0, 1, 1))        # chest number
    if cfg.get("number") and cfg.get("hoodie"):
        stencil_text(c, 34, 20 + bob, (1, 0, 1, 1))
    front_arms(c, S, frame, bob, cfg.get("sleeves", "long"),
               wrist=cfg.get("wrist"))
    front_head(c, cfg["hair"], cfg["hairstyle"], bob, cfg.get("glasses"))
    c.rim_light()
    c.outline()
    return c


def char_back(cfg, frame=0):
    c = C(64, 64)
    bob = -1 if frame else 0
    S, P = cfg["shirt"], cfg["pants"]
    fem = cfg.get("female", False)
    front_legs(c, P, frame, cfg.get("boots", "low"), female=fem)
    if fem:                                       # rounded seat
        c.vline(31, 34, 38, P["d2"])              # seat split
        c.vline(32, 34, 38, P["d1"])
        c.hline(26, 30, 38, P["d1"])              # under-curve
        c.hline(33, 37, 38, P["d2"])
        c.s(27, 34, P["l1"]); c.s(28, 34, P["l1"])         # top highlights
        c.s(34, 34, P["m"]); c.s(35, 34, P["m"])
    midriff = cfg.get("midriff")
    front_torso(c, S, bob, crop=24 if midriff else 31, female=fem,
                bust=False)
    if midriff:
        w0, w1 = (28, 35) if fem else (27, 36)
        c.r(w0, 25 + bob, w1, 31, SKIN["m"])
        c.vline(w0, 25 + bob, 31, SKIN["l1"])
        c.vline(w1, 25 + bob, 31, SKIN["d1"])
        c.vline(31, 26 + bob, 30, SKIN["d1"])     # spine line
        c.hline(28, 35, 24 + bob, S["d2"])        # shirt hem
    belt_front(c, cfg.get("belt", "buckle"), P, bob)
    c.hline(25, 38, 19 + bob, S["d1"])            # back yoke seam
    if cfg.get("hoodie"):
        c.r(26, 14 + bob, 37, 24 + bob, S["d1"])  # hanging hood
        c.hline(26, 37, 24 + bob, S["d2"])
        c.hline(27, 36, 14 + bob, S["l1"])
        c.vline(27, 15 + bob, 23 + bob, S["d2"])
    elif midriff:                                  # small back number
        stencil_text(c, 29, 17 + bob, (1, 0, 1, 1, 0, 1))
    else:                                          # big dossier stencil
        stencil_text(c, 27, 21 + bob, (1, 1, 0, 0, 1, 0, 1, 1, 0, 1))
        stencil_text(c, 28, 24 + bob, (1, 0, 1, 1, 0, 1, 1, 0))
    front_arms(c, S, frame, bob, cfg.get("sleeves", "long"),
               wrist=cfg.get("wrist"))
    # back of head
    Sk = SKIN
    c.r(30, 12 + bob, 33, 15 + bob, Sk["d1"])     # neck
    hair, hairstyle = cfg["hair"], cfg["hairstyle"]
    H = ramp(hair) if hair else None
    if H:
        c.r(27, 2 + bob, 36, 11 + bob, H["m"])    # full hair mass
        c.vline(27, 3 + bob, 10 + bob, H["l1"])
        c.vline(36, 3 + bob, 11 + bob, H["d1"])
        c.hline(27, 36, 11 + bob, H["d1"])
        if hairstyle == "curly":
            c.hline(28, 35, 1 + bob, H["m"])
            for x in (29, 32, 35):
                c.s(x, 2 + bob, H["l1"])
        elif hairstyle == "bald":
            c.r(28, 2 + bob, 35, 8 + bob, Sk["m"])
            c.s(29, 3 + bob, Sk["l1"]); c.s(30, 3 + bob, Sk["l1"])
            c.hline(28, 35, 8 + bob, H["d1"])     # hair ring low
        elif hairstyle == "tied":
            c.r(29, 1 + bob, 34, 6 + bob, H["d1"])         # bun mass
            c.r(30, 2 + bob, 33, 5 + bob, H["m"])
            c.hline(29, 34, 7 + bob, (58, 62, 56))         # tie
            c.s(28, 9 + bob, H["d1"]); c.s(35, 10 + bob, H["d1"])  # strands
        elif hairstyle == "messy":
            for x in (28, 31, 34):                # ragged nape
                c.s(x, 12 + bob, H["m"])
    c.chamfer(27, 2 + bob, 36, 12 + bob, top=2, bottom=1)
    c.rim_light()
    c.outline()
    return c


def side_boot_shaft(c, x0, x1, ytop):
    B = BOOT
    c.r(x0, ytop - 5, x1, ytop - 1, B["d1"])
    c.vline(x0, ytop - 5, ytop - 1, B["m"])
    for yy in range(ytop - 5, ytop - 1, 2):
        c.s(x0, yy, B["l1"])                      # front laces


def side_legs(c, P, frame, boots="low", female=False):
    """Profile facing left. Hips y34..36 at x27..35."""
    c.r(27, 34, 35, 36, P["m"])
    c.vline(27, 34, 36, P["l1"])
    c.vline(35, 34, 36, P["d1"])
    if female:                                    # seat curve at back
        c.vline(36, 33, 37, P["m"])
        c.s(36, 33, P["d1"]); c.s(36, 37, P["d1"])
        c.s(37, 34, P["d1"]); c.s(37, 35, P["d1"]); c.s(37, 36, P["d2"])
        c.s(35, 38, P["d2"])                      # under-seat shadow
    B = BOOT
    tall = boots == "tall"
    if frame == 0:
        limb(c, ((29, 36), (29, 45), (29, 53)), 4, P["m"], P["l1"], P["d1"])
        limb(c, ((31, 36), (32, 45), (32, 52)), 4, P["d1"], None, P["d2"])
        if tall:
            side_boot_shaft(c, 29, 32, 54)
        c.r(27, 54, 33, 58, B["m"])               # front boot, toe left
        c.hline(27, 33, 58, B["d2"])
        c.s(27, 56, B["l1"])
        c.r(32, 53, 36, 56, B["d1"])              # back boot behind
        c.hline(32, 36, 56, B["d2"])
    elif frame == 1:                              # full stride
        limb(c, ((30, 36), (27, 44), (24, 52)), 4, P["m"], P["l1"], P["d1"])
        if tall:
            side_boot_shaft(c, 24, 27, 53)
        c.r(22, 53, 28, 57, B["m"])               # planted front boot
        c.hline(22, 28, 57, B["d2"]); c.s(22, 55, B["l1"])
        limb(c, ((32, 36), (36, 44), (39, 50)), 4, P["d1"], None, P["d2"])
        c.r(38, 50, 43, 53, B["d1"])              # back heel raised
        c.hline(38, 43, 53, B["d2"])
        c.hline(27, 30, 44, P["d1"])              # knee creases
        c.hline(36, 39, 44, P["d2"])
    else:                                          # passing pose
        limb(c, ((30, 36), (29, 45), (29, 54)), 4, P["m"], P["l1"], P["d1"])
        if tall:
            side_boot_shaft(c, 29, 32, 54)
        c.r(27, 54, 33, 58, B["m"])
        c.hline(27, 33, 58, B["d2"]); c.s(27, 56, B["l1"])
        limb(c, ((32, 36), (34, 43), (34, 48)), 4, P["d1"], None, P["d2"])
        c.r(33, 48, 37, 51, B["d1"])              # trailing toe down
        c.hline(33, 37, 51, B["d2"])


def side_torso(c, S, P, bob, cfg):
    midriff = cfg.get("midriff")
    fem = cfg.get("female", False)
    crop = 24 if midriff else 31
    c.r(27, 15 + bob, 35, crop + bob, S["m"])
    c.vline(27, 15 + bob, crop + bob, S["l1"])    # chest light
    c.vline(35, 15 + bob, crop + bob, S["d1"])    # back shadow
    c.dith(34, 17 + bob, 35, crop + bob - 1, S["d1"], S["m"])
    c.hline(27, 35, 15 + bob, S["l1"])
    if fem:                                       # bust profile
        c.vline(26, 19 + bob, 21 + bob, S["m"])
        c.s(26, 19 + bob, S["l1"])
        c.s(26, 22 + bob, S["d1"])                # under-bust
        c.s(27, 23 + bob, S["d1"])
    if midriff:
        c.hline(27, 35, 24 + bob, S["d2"])        # hem
        w0 = 28 if fem else 27                    # waist dip
        c.r(w0, 25 + bob, 35, 31, SKIN["m"])      # bare waist
        c.vline(w0, 25 + bob, 31, SKIN["l1"])
        c.vline(35, 25 + bob, 31, SKIN["d1"])
    else:
        c.hline(28, 34, 24 + bob, S["d1"])        # fabric fold
    # belt
    kind = cfg.get("belt", "buckle")
    if kind == "gear":
        c.r(27, 32, 35, 33, HELMET["m"])
        c.hline(27, 35, 32, HELMET["l1"])
        c.r(25, 29 + bob, 28, 33, GUNMETAL["d1"])  # hip device front
        c.s(26, 30 + bob, GREEN["glow"])
        c.s(27, 31 + bob, GREEN["d"])
    elif kind == "chain":
        c.r(27, 32, 35, 33, BOOT["m"])
        c.hline(27, 35, 32, BOOT["l1"])
        for x, y in ((27, 34), (26, 35), (26, 36), (27, 37)):
            c.s(x, y, STEEL["l1"] if y % 2 else STEEL["d1"])
    elif kind == "drawstring":
        c.r(27, 32, 35, 33, P["d1"])
        c.hline(27, 35, 32, P["m"])
        c.vline(28, 33, 35, P["l1"])              # string
    else:
        c.r(27, 32, 35, 33, BOOT["m"])
        c.hline(27, 35, 32, BOOT["l1"])


def side_arm(c, S, frame, bob, sleeves="long", glove=None, wrist=False):
    sw = {0: ((30, 17), (29, 26), (29, 34)),
          1: ((30, 17), (26, 25), (24, 32)),
          2: ((30, 17), (32, 26), (34, 33))}[frame]
    pts = [(x, y + bob) for x, y in sw]
    if frame == 1:                                # far arm hint behind
        c.r(34, 18 + bob, 35, 28 + bob, S["d2"])
    if sleeves == "long":
        limb(c, pts, 3, S["d1"], S["m"], S["d2"])
    elif sleeves == "rolled":
        limb(c, pts[:2], 3, S["d1"], S["m"], S["d2"])      # sleeve to elbow
        limb(c, pts[1:], 3, SKIN["d1"], SKIN["m"], SKIN["d2"])
        ex, ey = pts[1]
        c.hline(ex, ex + 2, ey, S["l1"])          # rolled cuff
    else:                                          # short: bare arm
        x0, y0 = pts[0]
        c.r(x0 - 1, y0, x0 + 2, y0 + 3, S["d1"])  # sleeve stub
        c.hline(x0 - 1, x0 + 2, y0 + 3, S["d2"])
        limb(c, [(pts[0][0], pts[0][1] + 3)] + pts[1:], 3,
             SKIN["d1"], SKIN["m"], SKIN["d2"])
    ex, ey = pts[-1]
    hcol = glove if glove else SKIN["m"]
    c.r(ex, ey + 1, ex + 2, ey + 3, hcol)
    c.s(ex, ey + 1, mul(hcol, 1.2))
    if wrist:                                     # wrist device
        c.r(ex - 1, ey - 2, ex + 2, ey, GUNMETAL["d1"])
        c.s(ex, ey - 1, GREEN["hi"])


def side_head(c, hair, hairstyle, bob, glasses=False):
    S = SKIN
    c.r(30, 12 + bob, 34, 15 + bob, S["d1"])      # neck
    c.r(27, 4 + bob, 35, 12 + bob, S["m"])        # skull
    c.vline(27, 5 + bob, 11 + bob, S["l1"])       # face front light
    c.s(26, 8 + bob, S["m"]); c.s(26, 9 + bob, S["m"])   # nose
    c.s(26, 10 + bob, S["d1"])
    c.s(28, 8 + bob, (26, 22, 20))                # eye
    c.s(27, 11 + bob, S["d1"]); c.s(28, 11 + bob, S["d1"])   # mouth
    c.s(27, 12 + bob, S["d1"])                    # chin
    H = ramp(hair) if hair else None
    if H:
        c.r(28, 2 + bob, 36, 5 + bob, H["m"])     # crown
        c.r(33, 5 + bob, 36, 12 + bob, H["d1"])   # back mass
        c.r(28, 2 + bob, 29, 4 + bob, H["l1"])
        if hairstyle in ("messy", "curly"):
            c.r(28, 5 + bob, 30, 6 + bob, H["m"])          # fringe
            c.s(36, 13 + bob, H["d1"])            # nape tuft
            if hairstyle == "curly":
                c.hline(29, 36, 1 + bob, H["m"])
                c.s(30, 1 + bob, H["l1"]); c.s(34, 1 + bob, H["l1"])
        elif hairstyle == "bald":
            c.r(28, 2 + bob, 35, 6 + bob, S["m"])          # bare crown
            c.s(29, 3 + bob, S["l1"])
            c.r(34, 7 + bob, 36, 12 + bob, H["m"])         # ring at back
            c.r(31, 8 + bob, 33, 10 + bob, S["d1"])        # ear
            c.s(32, 9 + bob, S["d2"])
        elif hairstyle == "tied":
            c.hline(28, 35, 5 + bob, H["d1"])
            c.r(33, 1 + bob, 36, 4 + bob, H["m"])          # updo bun high
            c.s(36, 2 + bob, H["d1"])
            c.s(35, 13 + bob, H["d1"])            # loose nape strand
    if glasses:
        c.r(26, 7 + bob, 29, 8 + bob, (28, 32, 30))
        c.s(27, 7 + bob, GREEN["hi"])
        c.vline(30, 7 + bob, 7 + bob, (28, 32, 30))
    c.chamfer(27, 1 + bob, 36, 12 + bob, top=2, bottom=1)


def char_side(cfg, frame=0):
    c = C(64, 64)
    bob = -1 if frame else 0
    S, P = cfg["shirt"], cfg["pants"]
    side_legs(c, P, frame, cfg.get("boots", "low"),
              female=cfg.get("female", False))
    side_torso(c, S, P, bob, cfg)
    if cfg.get("hoodie"):
        c.r(33, 13 + bob, 37, 22 + bob, S["d1"])  # hood at back
        c.vline(37, 14 + bob, 21 + bob, S["d2"])
        c.hline(27, 35, 14 + bob, S["d1"])
    if cfg.get("number"):
        c.s(28, 19 + bob, STENCIL); c.s(30, 19 + bob, STENCIL)  # arm number
    side_arm(c, S, frame, bob, cfg.get("sleeves", "long"),
             wrist=cfg.get("wrist"))
    side_head(c, cfg["hair"], cfg["hairstyle"], bob, cfg.get("glasses"))
    c.rim_light()
    c.outline()
    return c


# ------------------------------------------------------------------ guard --
def guard_helmet_front(c, bob):
    H = HELMET
    c.r(26, 2 + bob, 37, 13 + bob, H["m"])        # full dome incl. jaw
    c.hline(27, 36, 1 + bob, H["m"])
    c.vline(26, 3 + bob, 11 + bob, H["l1"])
    c.vline(37, 3 + bob, 13 + bob, H["d1"])
    c.r(28, 5 + bob, 35, 10 + bob, VISOR_W)       # square white frame
    c.r(29, 6 + bob, 34, 9 + bob, (15, 19, 17))   # dark glass
    c.s(30, 7 + bob, (54, 68, 58))                # reflections
    c.s(33, 8 + bob, (34, 46, 38))
    c.hline(28, 35, 10 + bob, mul(VISOR_W, 0.72))
    c.chamfer(26, 1 + bob, 37, 13 + bob, top=2, bottom=1)


def guard_vest_front(c, bob):
    B = BLACK
    c.r(25, 18 + bob, 38, 27 + bob, B["l1"])      # plate carrier
    c.hline(25, 38, 18 + bob, B["m"])
    c.hline(25, 38, 27 + bob, B["d2"])
    c.vline(27, 18 + bob, 27 + bob, B["d2"])      # straps
    c.vline(36, 18 + bob, 27 + bob, B["d2"])
    for px_ in ((28, 23), (33, 23)):              # pouches
        c.r(px_[0], px_[1] + bob, px_[0] + 2, px_[1] + 3 + bob, B["m"])
        c.hline(px_[0], px_[0] + 2, px_[1] + bob, B["d2"])
    # triangle emblem left chest
    c.s(27, 20 + bob, STEEL["m"])
    c.s(26, 21 + bob, STEEL["m"]); c.s(28, 21 + bob, STEEL["m"])


def guard_rifle_front(c, bob):
    G = GUNMETAL
    x0, x1, y, lean = 19, 43, 25, 4
    n = x1 - x0
    for i in range(n + 1):
        x = x0 + i
        yy = y + bob + lean - (i * lean) // n
        c.r(x, yy, x, yy + 2, G["m"])
        c.s(x, yy, G["l1"])
        c.s(x, yy + 2, G["d2"])
    c.r(x0 - 3, y + bob + lean, x0, y + bob + lean + 1, G["d1"])  # muzzle
    c.s(x0 - 3, y + bob + lean, G["l1"])
    c.r(x1, y + bob - 1, x1 + 1, y + bob + 3, G["d1"])            # stock
    c.r(x0 + 9, y + bob + lean - 1, x0 + 11, y + bob + lean + 4, G["d1"])
    c.s(x0 + 7, y + bob + lean - 2, GREEN["hi"])                  # sight
    c.r(x0 + 8, y + bob + lean, x0 + 9, y + bob + lean + 2, HELMET["l1"])
    c.r(x0 + 18, y + bob + 1, x0 + 19, y + bob + 3, HELMET["l1"])  # hands


def guard_front(frame=0):
    c = C(64, 64)
    bob = -1 if frame else 0
    B = BLACK
    front_legs(c, B, frame)
    front_torso(c, B, bob)
    belt_front(c, "buckle", B, bob)
    guard_vest_front(c, bob)
    front_arms(c, B, frame, bob, glove=HELMET["l1"])
    guard_rifle_front(c, bob)
    guard_helmet_front(c, bob)
    c.rim_light()
    c.outline()
    return c


def guard_back(frame=0):
    c = C(64, 64)
    bob = -1 if frame else 0
    B = BLACK
    front_legs(c, B, frame)
    front_torso(c, B, bob)
    belt_front(c, "buckle", B, bob)
    c.r(25, 18 + bob, 38, 27 + bob, B["l1"])      # vest back panel
    c.vline(27, 18 + bob, 27 + bob, B["d2"])
    c.vline(36, 18 + bob, 27 + bob, B["d2"])
    c.hline(27, 36, 22 + bob, B["d2"])            # cross strap
    front_arms(c, B, frame, bob, glove=HELMET["l1"])
    # slung rifle diagonal
    G = GUNMETAL
    for i in range(16):
        x = 26 + i
        y = 30 + bob - i // 2
        c.r(x, y, x + 1, y + 1, G["m"])
        c.s(x, y, G["l1"])
    c.r(40, 18 + bob, 41, 23 + bob, G["d1"])      # barrel over shoulder
    c.s(40, 18 + bob, G["l1"])
    # helmet back: plain dome + small white square
    H = HELMET
    c.r(26, 2 + bob, 37, 13 + bob, H["m"])
    c.hline(27, 36, 1 + bob, H["m"])
    c.vline(26, 3 + bob, 11 + bob, H["l1"])
    c.vline(37, 3 + bob, 13 + bob, H["d1"])
    c.r(29, 5 + bob, 34, 9 + bob, VISOR_W)
    c.r(30, 6 + bob, 33, 8 + bob, H["d2"])
    c.chamfer(26, 1 + bob, 37, 13 + bob, top=2, bottom=1)
    c.rim_light()
    c.outline()
    return c


def guard_side(frame=0):
    c = C(64, 64)
    bob = -1 if frame else 0
    B = BLACK
    side_legs(c, B, frame)
    side_torso(c, B, B, bob, {"belt": "buckle"})
    c.r(28, 18 + bob, 34, 27 + bob, B["l1"])      # vest wrap
    c.hline(28, 34, 27 + bob, B["d2"])
    c.r(33, 22 + bob, 35, 26 + bob, B["m"])       # back pouch
    # rifle angled down-forward
    G = GUNMETAL
    for i in range(15):
        x = 33 - i
        y = 23 + bob + (i * 2) // 3
        c.r(x, y, x + 1, y + 1, G["m"])
        c.s(x, y, G["l1"])
    c.r(16, 33 + bob, 19, 34 + bob, G["d1"])      # muzzle
    c.s(16, 33 + bob, G["l1"])
    c.r(33, 21 + bob, 35, 25 + bob, G["d1"])      # stock at shoulder
    c.r(25, 27 + bob, 27, 31 + bob, G["d1"])      # grip + mag
    # arm reaching to grip
    limb(c, ((30, 17 + bob), (27, 23 + bob), (25, 27 + bob)), 3,
         B["d1"], B["m"], B["d2"])
    c.r(24, 27 + bob, 26, 29 + bob, HELMET["l1"])  # glove
    # helmet profile
    H = HELMET
    c.r(27, 2 + bob, 37, 13 + bob, H["m"])
    c.hline(28, 36, 1 + bob, H["m"])
    c.vline(27, 3 + bob, 11 + bob, H["l1"])
    c.vline(37, 3 + bob, 13 + bob, H["d1"])
    c.r(27, 5 + bob, 30, 10 + bob, VISOR_W)       # visor edge-on
    c.r(28, 6 + bob, 29, 9 + bob, (15, 19, 17))
    c.s(28, 7 + bob, (54, 68, 58))
    c.chamfer(27, 1 + bob, 37, 13 + bob, top=2, bottom=1)
    c.rim_light()
    c.outline()
    return c


def gen_characters():
    chars = {
        # C-4821: open collar, rolled sleeves, drawstring, thigh number
        "player": dict(shirt=SHIRT, pants=PANTS, hair=(48, 38, 30),
                       hairstyle="messy", sleeves="rolled",
                       belt="drawstring", thigh_patch=True, number=True),
        # generic older inmate: standard uniform, long sleeves
        "prisoner_generic": dict(shirt=SHIRT, pants=PANTS,
                                 hair=(118, 118, 112), hairstyle="bald",
                                 sleeves="long", belt="buckle", number=True),
        # C-075: tied shirt, bare midriff, dog tags, chain, tall boots
        "girl": dict(shirt=SHIRT, pants=ramp((66, 68, 58)),
                     hair=(26, 24, 28), hairstyle="tied", female=True,
                     sleeves="short", midriff=True, dogtag=True,
                     belt="chain", boots="tall", number=True),
        # C-3071: hooded coverall, glasses, gadget belt, pens, wrist device
        "npc_programmer": dict(shirt=COVERALL, pants=COVERALL,
                               hair=(36, 31, 28), hairstyle="curly",
                               sleeves="long", hoodie=True, glasses=True,
                               belt="gear", pens=True, wrist=True,
                               number=True),
    }
    for name, cfg in chars.items():
        for f, suf in ((0, ""), (1, "_walk_1"), (2, "_walk_2")):
            char_front(cfg, frame=f).save(f"{name}{suf}.png")
            char_side(cfg, frame=f).save(f"{name}_side{suf}.png")
            char_back(cfg, frame=f).save(f"{name}_up{suf}.png")
    for f, suf in ((0, ""), (1, "_walk_1"), (2, "_walk_2")):
        guard_front(f).save(f"guard{suf}.png")
        guard_side(f).save(f"guard_side{suf}.png")
        guard_back(f).save(f"guard_up{suf}.png")


# ------------------------------------------------------------------ tiles --
def gen_floor():
    c = C(64, 64)
    rng = noise_fill(c, mul(CONCRETE["d1"], 1.15), 4, 21)
    for _ in range(5):                            # broad stains
        sx, sy = rng.randrange(64), rng.randrange(64)
        r = rng.randrange(4, 9)
        for y in range(sy - r, sy + r):
            for x in range(sx - r, sx + r):
                if (x - sx) ** 2 + (y - sy) ** 2 <= r * r and rng.random() < .6:
                    c.blend(x % 64, y % 64, CONCRETE["d2"], 0.45)
    x, y = rng.randrange(14, 50), 0               # one hairline crack
    while y < 44:
        c.blend(x, y, CONCRETE["d2"], 0.7)
        x = max(2, min(61, x + rng.choice((-1, 0, 0, 1))))
        y += rng.choice((1, 1, 2))
    for _ in range(40):                           # speckle
        c.s(rng.randrange(64), rng.randrange(64),
            CONCRETE["m"] if rng.random() < .5 else mul(CONCRETE["d2"], .9))
    c.hline(0, 63, 0, CONCRETE["m"])              # tile seam light/shadow
    c.vline(0, 0, 63, CONCRETE["m"])
    c.hline(0, 63, 63, mul(CONCRETE["d2"], 0.85))
    c.vline(63, 0, 63, mul(CONCRETE["d2"], 0.85))
    c.save("floor_concrete.png")


def gen_wall_top():
    c = C(64, 64)
    rng = noise_fill(c, CONCRETE["m"], 3, 22)
    for row in range(4):                          # 16px block courses
        y0 = row * 16
        off = 0 if row % 2 == 0 else 16
        for bx in range(-1, 3):
            x0 = bx * 32 + off
            dv = rng.randint(-6, 6)               # per-block variation
            for y in range(y0, y0 + 15):
                for x in range(max(0, x0), min(64, x0 + 31)):
                    p = c.px[x, y]
                    c.s(x, y, (cl(p[0] + dv), cl(p[1] + dv), cl(p[2] + dv)))
            if 0 <= x0 <= 63:
                c.vline(x0, y0, y0 + 14, mul(CONCRETE["d2"], 0.9))
                if x0 + 1 <= 63:
                    c.vline(x0 + 1, y0, y0 + 14, CONCRETE["d1"])
        c.hline(0, 63, y0 + 15, mul(CONCRETE["d2"], 0.9))
        c.hline(0, 63, y0, CONCRETE["l1"])        # top catch-light
        for _ in range(6):                        # chipped edges
            c.s(rng.randrange(64), y0, CONCRETE["m"])
    for _ in range(8):                            # vertical grime
        x = rng.randrange(64)
        y = rng.randrange(40)
        for i in range(rng.randrange(4, 12)):
            c.blend(x, y + i, CONCRETE["d2"], 0.3)
    c.save("wall_top.png")


def gen_wall_side():
    c = C(64, 40)
    rng = noise_fill(c, mul(CONCRETE["d1"], 0.85), 3, 23)
    for y in (12, 26):                            # panel seams
        c.hline(0, 63, y, mul(CONCRETE["d2"], 0.8))
        c.hline(0, 63, y + 1, CONCRETE["m"])
    for x in range(2, 64, 12):                    # rivets
        for y in (14, 28):
            c.s(x, y, CONCRETE["l1"]); c.s(x, y + 1, CONCRETE["d2"])
    for _ in range(14):                           # leak streaks from top
        x = rng.randrange(64)
        ln = rng.randrange(4, 16)
        for i in range(ln):
            c.blend(x, i, mul(CONCRETE["d2"], 0.7), 0.5 - i / (2 * ln))
    c.hline(0, 63, 0, CONCRETE["l1"])
    for y in range(36, 40):                       # floor contact shadow
        for x in range(64):
            c.blend(x, y, DEEP, 0.18 * (y - 35))
    c.save("wall_side.png")


def gen_door():
    c = C(64, 64)
    M = ramp((48, 53, 56))
    c.r(0, 0, 63, 63, M["d2"])                    # frame
    c.r(3, 3, 60, 63, M["m"])
    noise_fill(c, M["m"], 2, 24, 3, 3, 60, 63)
    c.r(3, 3, 60, 4, M["l1"])
    c.r(3, 3, 4, 63, M["l1"])
    c.r(59, 3, 60, 63, M["d1"])
    c.r(30, 5, 33, 63, M["d2"])                   # central split
    c.vline(31, 5, 63, mul(M["d2"], 0.7))
    c.vline(32, 5, 63, M["d1"])
    for y in (16, 44):                            # cross braces
        c.r(5, y, 58, y + 2, M["d1"])
        c.hline(5, 58, y, M["l1"])
    c.r(18, 24, 45, 34, M["d2"])                  # window recess
    c.r(20, 26, 43, 32, GREEN["d2"])              # glass: green interior
    c.r(21, 27, 42, 29, GREEN["d"])
    c.hline(22, 35, 28, GREEN["m"])               # light slit
    c.s(24, 28, GREEN["hi"]); c.s(30, 28, GREEN["hi"])
    c.r(48, 38, 55, 47, (18, 20, 20))             # keypad
    c.r(49, 39, 54, 41, GREEN["d"])
    c.s(50, 40, GREEN["glow"]); c.s(52, 40, GREEN["m"])
    for yy in (43, 45):
        for xx in (50, 52, 54):
            c.s(xx, yy, M["l1"])
    for x, y in ((7, 8), (56, 8), (7, 58), (56, 58)):  # corner bolts
        c.s(x, y, M["l1"]); c.s(x + 1, y + 1, M["d2"])
    rng = random.Random(25)
    for _ in range(10):                           # scratches
        x, y = rng.randrange(6, 58), rng.randrange(6, 60)
        for i in range(rng.randrange(2, 5)):
            c.s(x + i, y + i // 2, M["l1"])
    for y in range(37, 49):                       # keypad glow halo
        for x in range(46, 58):
            d = abs(x - 51.5) + abs(y - 40)
            if d < 7:
                c.blend(x, y, GREEN["m"], max(0.0, 0.16 - d * 0.02))
    c.save("door_metal.png")


def gen_console():
    c = C(64, 32)
    B = ramp((30, 34, 32))
    c.r(0, 2, 63, 31, B["m"])
    c.r(0, 2, 63, 3, B["l1"])
    c.r(1, 0, 62, 1, B["d1"])                     # top bezel
    c.r(0, 30, 63, 31, B["d2"])
    c.r(4, 4, 59, 19, (10, 18, 13))               # CRT bezel
    c.r(5, 5, 58, 18, GREEN["d2"])                # screen
    rng = random.Random(26)
    for y in range(6, 18):                        # code lines
        if y % 2 == 0:
            continue                              # scanline gap
        x = 7
        while x < 55:
            ln = rng.randrange(3, 9)
            if rng.random() < 0.8:
                col = GREEN["m"] if rng.random() < .72 else GREEN["hi"]
                c.hline(x, min(55, x + ln - 1), y, col)
                if rng.random() < .12:
                    c.s(x, y, GREEN["glow"])
            x += ln + rng.randrange(1, 4)
    c.r(5, 5, 58, 5, GREEN["d"])                  # screen top falloff
    for y in range(5, 19):                        # vignette sides
        c.blend(5, y, GREEN["d2"], 0.5)
        c.blend(58, y, GREEN["d2"], 0.5)
    for x in range(4, 60):                        # glow spill on bezel
        c.blend(x, 20, GREEN["d"], 0.35)
    c.r(6, 22, 45, 27, B["d1"])                   # keyboard shelf
    for y in (23, 25):
        for x in range(7, 44, 2):
            c.s(x, y, B["l1"])
    c.r(48, 22, 57, 27, B["d2"])                  # side panel
    c.s(50, 23, GREEN["glow"])                    # LEDs
    c.s(53, 23, (180, 90, 60))
    c.s(50, 25, GREEN["m"])
    c.save("console.png")


def gen_crate():
    c = C(64, 64)
    M = ramp((54, 58, 47))
    noise_fill(c, M["m"], 3, 27)
    c.r(0, 0, 63, 3, M["l1"])                     # top edge
    c.r(0, 60, 63, 63, M["d2"])
    c.r(0, 0, 3, 63, M["l1"])
    c.r(60, 0, 63, 63, M["d1"])
    for i in range(4, 60):                        # X brace
        for j in (i, 63 - i):
            if 4 <= j <= 59:
                c.s(i, j, M["d1"]); c.s(i + 1, j, M["d2"])
    c.r(20, 26, 43, 37, M["d1"])                  # stencil plate
    c.hline(20, 43, 26, M["d2"])
    stencil_text(c, 23, 29, (1, 1, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1))
    stencil_text(c, 26, 33, (1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1))
    for x, y in ((6, 6), (57, 6), (6, 57), (57, 57)):
        c.r(x, y, x + 1, y + 1, M["l2"])          # corner bolts
        c.s(x + 1, y + 1, M["d2"])
    rng = random.Random(28)
    for _ in range(12):                           # edge wear chips
        if rng.random() < 0.5:
            c.s(rng.randrange(64), rng.randrange(2), M["l2"])
        else:
            c.s(rng.randrange(2), rng.randrange(64), M["l2"])
    c.save("crate.png")


def gen_pit():
    c = C(64, 64)
    for y in range(64):                           # depth gradient
        f = min(1.0, y / 20)
        col = mul((20, 24, 21), 1 - f * 0.7, 0.1)
        for x in range(64):
            c.s(x, y, col)
    c.r(0, 0, 63, 0, CONCRETE["l1"])              # lip catch-light
    c.r(0, 1, 63, 1, CONCRETE["d1"])
    rng = random.Random(29)
    for x in range(0, 64, 8):                     # worn hazard chevrons
        if rng.random() < 0.75:
            for i in range(4):
                c.s(x + i, 2, (104, 98, 54))
                c.s(x + i + 1, 3, (84, 79, 44))
    for _ in range(10):                           # rubble hints in dark
        c.s(rng.randrange(64), rng.randrange(8, 20), (24, 28, 25))
    c.r(0, 30, 63, 63, (5, 7, 6))                 # bottomless
    c.save("pit.png")


def gen_dirt():
    c = C(64, 64)
    rng = noise_fill(c, (46, 42, 35), 4, 30)
    for band in (10, 34):                         # tire ruts
        for y in range(band, band + 5):
            for x in range(64):
                c.blend(x, y, (32, 29, 24), 0.4)
        c.hline(0, 63, band + 5, (56, 51, 42))
    for _ in range(26):                           # pebbles
        x, y = rng.randrange(64), rng.randrange(64)
        c.s(x, y, (60, 56, 47)); c.s(x + 1, y, (38, 35, 29))
    for _ in range(18):
        c.s(rng.randrange(64), rng.randrange(64), (30, 27, 22))
    c.save("race_dirt.png")


def gen_rock():
    c = C(64, 64)
    R = ramp((72, 76, 71))
    # hand-authored silhouette: (y, x0, x1) spans of a squat boulder
    spans = []
    profile = [(18, 27, 36), (19, 24, 40), (20, 22, 43), (22, 20, 45),
               (24, 18, 47), (28, 16, 48), (34, 15, 49), (40, 15, 49),
               (46, 16, 48), (50, 17, 47), (53, 19, 45), (55, 22, 42),
               (57, 26, 38)]
    for i in range(len(profile) - 1):
        y0, a0, b0 = profile[i]
        y1, a1, b1 = profile[i + 1]
        for y in range(y0, y1):
            t = (y - y0) / max(1, y1 - y0)
            spans.append((y, round(a0 + (a1 - a0) * t),
                          round(b0 + (b1 - b0) * t)))
    rng = random.Random(31)
    for y, x0, x1 in spans:
        for x in range(x0, x1 + 1):
            lt = (x - 32) * 0.6 + (y - 38) * 0.85
            if lt < -14:
                col = R["l1"]
            elif lt < -6:
                col = R["m"] if (x + y) % 2 else R["l1"]
            elif lt < 8:
                col = R["m"]
            elif lt < 15:
                col = R["d1"] if (x + y) % 2 else R["m"]
            else:
                col = R["d1"]
            if x >= x1 - 1 and lt > 0:
                col = R["d1"]
            c.s(x, y, col)
    for sx, sy, ln in ((28, 24, 10), (40, 33, 9)):  # facet cracks
        x, y = sx, sy
        for _ in range(ln):
            c.s(x, y, R["d2"])
            if rng.random() < 0.4:
                c.s(x + 1, y, R["d1"])
            x += rng.choice((0, 1)); y += 1
    for _ in range(16):                           # pocking
        x, y = rng.randrange(20, 46), rng.randrange(28, 54)
        c.s(x, y, R["d1"])
    c.hline(22, 42, 56, R["d2"])                  # base self-shadow
    c.rim_light(t=0.45)
    c.outline()
    c.save("rock.png")


def gen_saw():
    c = C(64, 64)
    S = ramp((120, 124, 121))
    cx = cy = 31.5
    for y in range(64):
        for x in range(64):
            d = math.hypot(x - cx, y - cy)
            a = math.atan2(y - cy, x - cx)
            tooth = 26 + 4 * abs(math.sin(a * 9))
            if d < tooth:
                col = S["m"]
                if int(d) % 7 == 0:               # faint brushed ring
                    col = S["d1"] if (x + y) % 2 else S["m"]
                if d > tooth - 2.5:
                    col = S["d1"]
                c.s(x, y, col)
                if -2.3 < a < -1.8 and 14 < d < tooth - 3:
                    c.blend(x, y, S["l1"], 0.5)   # subtle glint
                if 0.8 < a < 1.3 and 14 < d < tooth - 3:
                    c.blend(x, y, S["d1"], 0.4)   # opposite shade
    rng = random.Random(33)
    for _ in range(26):                           # rust specks
        ang = rng.uniform(0, 6.28)
        d = rng.uniform(10, 24)
        c.s(int(cx + d * math.cos(ang)), int(cy + d * math.sin(ang)),
            (96, 78, 58))
    c.r(26, 26, 37, 37, S["d1"])                  # hub
    c.r(28, 28, 35, 35, S["d2"])
    c.r(30, 30, 33, 33, (20, 22, 21))             # bolt
    c.s(30, 30, S["m"])
    c.rim_light(t=0.4)
    c.outline()
    c.save("saw.png")


def gen_lines():
    c = C(64, 16)                                 # start: worn hazard
    rng = random.Random(34)
    for x in range(64):
        for y in range(16):
            on = ((x + y) // 8) % 2 == 0
            c.s(x, y, (108, 101, 55) if on else (22, 24, 21))
    for _ in range(70):                           # chipped paint
        c.s(rng.randrange(64), rng.randrange(16), (30, 33, 29))
    c.hline(0, 63, 0, (130, 122, 70))
    c.save("start_line.png")

    c = C(64, 16)                                 # finish: scuffed checker
    rng = random.Random(35)
    for x in range(64):
        for y in range(16):
            on = (x // 8 + y // 8) % 2 == 0
            c.s(x, y, (198, 200, 190) if on else (24, 26, 24))
    for _ in range(60):
        c.blend(rng.randrange(64), rng.randrange(16), (90, 92, 86), 0.6)
    c.save("finish_line.png")


# ------------------------------------------------------------------ items --
def gen_items():
    # keycard badge
    c = C(32, 32)
    B = ramp((34, 38, 36))
    c.r(4, 7, 27, 25, B["m"])
    c.r(4, 7, 27, 8, B["l1"])
    c.r(4, 24, 27, 25, B["d1"])
    c.r(4, 11, 27, 13, GREEN["m"])                # stripe
    c.hline(4, 27, 11, GREEN["hi"])
    c.r(7, 16, 13, 22, B["d2"])                   # photo
    c.r(8, 17, 12, 21, SHIRT["d1"])
    c.r(9, 17, 11, 18, SKIN["d1"])                # mugshot
    c.r(16, 17, 25, 18, STENCIL)                  # name bar
    c.r(16, 21, 22, 21, mul(STENCIL, 0.6))
    c.r(24, 9, 25, 10, (12, 14, 13))              # punch hole
    c.rim_light(t=0.5)
    c.outline()
    c.save("item_badge.png")

    # implant: glass vial with glowing chip
    c = C(32, 32)
    for y in range(6, 27):                        # vial body
        for x in range(10, 22):
            c.s(x, y, (40, 50, 44), 230)
    c.r(10, 6, 21, 7, STEEL["d1"])                # cap
    c.r(10, 4, 21, 5, STEEL["m"])
    c.r(13, 14, 18, 21, GREEN["d"])               # chip
    c.r(14, 15, 17, 20, GREEN["m"])
    c.r(15, 16, 16, 18, GREEN["glow"])
    for x, y in ((12, 12), (19, 23), (12, 23), (19, 12)):
        c.s(x, y, GREEN["m"])                     # contact wires
    c.vline(11, 8, 25, (120, 140, 128))           # glass highlight
    for y in range(10, 26):                       # glow halo in liquid
        for x in range(11, 21):
            d = abs(x - 15.5) + abs(y - 17.5)
            if d > 4:
                c.blend(x, y, GREEN["d"], max(0, .5 - d * 0.05))
    c.rim_light(t=0.4)
    c.outline()
    c.save("item_implant.png")

    # manifest: clipboard
    c = C(32, 32)
    W = ramp((96, 78, 56))
    c.r(5, 4, 26, 29, W["d1"])                    # board
    c.r(7, 6, 24, 27, PAPER["m"])
    c.r(7, 6, 24, 7, PAPER["l1"])
    c.r(12, 2, 19, 5, STEEL["d1"])                # clip
    c.hline(13, 18, 3, STEEL["m"])
    for y in (11, 14, 17, 20, 23):
        c.hline(9, 22, y, mul(PAPER["d2"], 0.9))
    c.hline(9, 15, 11, (60, 62, 58))              # heading darker
    c.r(18, 22, 23, 26, GREEN["d"])               # stamp
    c.s(19, 23, GREEN["m"]); c.s(21, 25, GREEN["m"])
    c.rim_light(t=0.4)
    c.outline()
    c.save("item_manifest.png")

    # reports: stuffed folder
    c = C(32, 32)
    F = ramp((58, 66, 54))
    c.r(4, 12, 27, 27, F["d1"])                   # back cover
    c.r(6, 9, 24, 12, PAPER["m"])                 # papers sticking out
    c.hline(7, 23, 10, PAPER["d1"])
    c.r(4, 13, 27, 28, F["m"])                    # front cover
    c.r(4, 13, 27, 14, F["l1"])
    c.r(4, 8, 12, 12, F["m"])                     # tab
    c.hline(4, 12, 8, F["l1"])
    c.r(8, 18, 23, 20, mul((150, 60, 50), 0.9))   # CLASSIFIED bar
    c.hline(9, 20, 19, (190, 90, 70))
    c.hline(8, 19, 23, F["d2"])
    c.hline(8, 16, 25, F["d2"])
    c.rim_light(t=0.4)
    c.outline()
    c.save("item_reports.png")

    # screwdriver 45°
    c = C(32, 32)
    for i in range(13):                           # steel shaft
        x, y = 5 + i, 26 - i
        c.s(x, y, STEEL["m"])
        c.s(x + 1, y, STEEL["l1"])
        c.s(x, y + 1, STEEL["d1"])
    c.s(4, 27, (225, 228, 222)); c.s(5, 27, STEEL["l2"])     # tip glint
    for i in range(9):                            # ribbed grip
        x, y = 18 + i, 13 - i
        w = 2 if i in (0, 8) else 3
        c.r(x, y - 1, x + w, y + w - 1, (34, 38, 36))
        if i % 2:
            c.s(x + 1, y, (52, 58, 54))           # ribs
    c.r(18, 11, 20, 13, GREEN["d"])               # collar ring
    c.s(19, 12, GREEN["m"])
    c.rim_light(t=0.45)
    c.outline()
    c.save("item_screwdriver.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    gen_characters()
    gen_floor()
    gen_wall_top()
    gen_wall_side()
    gen_door()
    gen_console()
    gen_crate()
    gen_pit()
    gen_dirt()
    gen_rock()
    gen_saw()
    gen_lines()
    gen_items()
    print("done")
