#!/usr/bin/env python3
"""Block C — full asset generation (everyone except the girl: see
generate_girl.py). Tiles, items, guard and male cast.

Run from repo root:  python3 tools/generate_sprites.py
Overwrites PNGs in Assets/Resources/Sprites (PPU auto = png width).
"""
import math
import random

from sprite_lib import *

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
        # C-075 "girl" живёт в generate_girl.py
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
    # анимация подбора предмета — пока только у игрока
    player = chars["player"]
    for stage in (1, 2):
        char_pickup(player, "front", stage).save(f"player_pickup_{stage}.png")
        char_pickup(player, "side", stage).save(f"player_side_pickup_{stage}.png")
        char_pickup(player, "up", stage).save(f"player_up_pickup_{stage}.png")
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
    # gen_door()  # door_metal.png заменён на AI-арт (детальная дверь блока C), не генерим
    gen_console()
    gen_crate()
    gen_pit()
    gen_dirt()
    gen_rock()
    gen_saw()
    gen_lines()
    gen_items()
    print("done")
