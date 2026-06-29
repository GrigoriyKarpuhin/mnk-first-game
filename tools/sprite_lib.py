#!/usr/bin/env python3
"""sprite_lib — shared pixel-art framework for Block C sprites (64x64).

Palette, canvas, rim light/outline, and the realistic ~1:6 humanoid body
builders (front/side/back views, 3 walk frames, female silhouette flag).
Used by generate_sprites.py (cast + tiles) and generate_girl.py (C-075).
"""

import math
import random
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "Sprites"


# ------------------------------------------------------------ unity .meta --
# Минимальный .meta тайла-спрайта: point-фильтр + PPU + single sprite. Нужен для
# НОВЫХ PNG — иначе Unity импортит их дефолтом (Bilinear, PPU 100) и размывает
# чанк-пиксель. Существующие .meta не трогаем. Остальное Unity догенерит сам.
_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  bumpmap:
    convertToNormalMap: 0
  isReadable: 0
  streamingMipmaps: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {spid}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def ensure_sprite_meta(path, ppu=64):
    """Написать рядом с PNG корректный .meta (point/PPU/sprite), если его ещё нет.

    Возвращает True, если файл создан. guid детерминирован по имени файла, чтобы
    повторный прогон не плодил новые id.
    """
    p = Path(path)
    meta = Path(str(p) + ".meta")
    if meta.exists():
        return False
    rng = random.Random(p.name)
    guid = "%032x" % rng.getrandbits(128)
    spid = "%032x" % rng.getrandbits(128)
    meta.write_text(_META_TEMPLATE.format(guid=guid, ppu=int(ppu), spid=spid))
    print("meta", meta.name)
    return True


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


LIP = (138, 90, 82)
EYE = (26, 22, 20)


def front_head(c, hair, hairstyle, bob, glasses=False, female=False):
    S = SKIN
    if female:                                    # slim lit neck
        c.r(30, 13 + bob, 32, 15 + bob, S["m"])
        c.vline(33, 13 + bob, 15 + bob, S["d1"])
        c.vline(30, 13 + bob, 15 + bob, S["l1"])
    else:
        c.r(30, 12 + bob, 33, 15 + bob, S["d1"])  # neck in shadow
    c.r(28, 5 + bob, 35, 12 + bob, S["m"])        # face
    c.vline(28, 6 + bob, 11 + bob, S["l1"])
    c.vline(35, 6 + bob, 12 + bob, S["d1"])
    c.hline(30, 33, 12 + bob, S["d1"])            # jaw
    c.s(29, 8 + bob, EYE); c.s(34, 8 + bob, EYE)  # eyes, 1px each
    c.s(29, 7 + bob, S["d1"]); c.s(34, 7 + bob, S["d1"])   # brows
    c.s(32, 9 + bob, S["d1"])                     # nose, 1px
    if female:
        c.s(31, 11 + bob, LIP)                    # soft lips
        c.s(32, 11 + bob, mul(LIP, 0.85))
        c.clear(28, 12 + bob); c.clear(35, 12 + bob)       # narrow chin
        c.s(28, 11 + bob, S["d1"]); c.s(35, 11 + bob, S["d1"])
    else:
        c.s(31, 11 + bob, S["d1"]); c.s(32, 11 + bob, S["d1"])   # mouth
        c.s(34, 10 + bob, S["d2"])                # cheek core shadow
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
        if fem:                                   # narrow bright V, no merge
            c.r(31, 15 + bob, 32, 16 + bob, SKIN["m"])     # with the chin
            c.s(31, 17 + bob, SKIN["m"]); c.s(32, 17 + bob, SKIN["d1"])
            c.vline(30, 15 + bob, 17 + bob, S["d2"])       # collar flaps
            c.vline(33, 15 + bob, 17 + bob, S["d2"])
        else:
            c.r(30, 15 + bob, 33, 17 + bob, SKIN["d1"])
            c.s(31, 18 + bob, SKIN["d1"]); c.s(32, 18 + bob, SKIN["d2"])
            c.vline(29, 15 + bob, 17 + bob, S["d2"])
            c.vline(34, 15 + bob, 17 + bob, S["d2"])
        for by in (20, 23, 26, 29):               # button line
            if by < (24 if midriff else 31):
                c.s(31, by + bob, S["d2"])
    if cfg.get("dogtag"):                         # thin chain + tag lower
        c.s(30, 17 + bob, STEEL["l1"]); c.s(33, 17 + bob, STEEL["l1"])
        c.r(31, 19 + bob, 32, 20 + bob, STEEL["m"])
        c.s(31, 19 + bob, STEEL["l2"])
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
    front_head(c, cfg["hair"], cfg["hairstyle"], bob, cfg.get("glasses"),
               female=fem)
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
        c.vline(31, 35, 37, P["d2"])              # short seat split
        c.vline(32, 35, 37, P["d1"])
        c.hline(27, 29, 38, P["d1"])              # under-curve per cheek
        c.hline(34, 36, 38, P["d2"])
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
    back_head(c, cfg, bob)
    c.rim_light()
    c.outline()
    return c


def back_head(c, cfg, bob):
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
        c.r(31, 53, 36, 56, B["d1"])              # back boot: toe left too
        c.hline(31, 36, 56, B["d2"])
        c.s(31, 54, B["m"])                       # toe tip catch-light
    elif frame == 1:                              # full stride
        limb(c, ((30, 36), (27, 44), (24, 52)), 4, P["m"], P["l1"], P["d1"])
        if tall:
            side_boot_shaft(c, 24, 27, 53)
        c.r(22, 53, 28, 57, B["m"])               # planted front boot
        c.hline(22, 28, 57, B["d2"]); c.s(22, 55, B["l1"])
        limb(c, ((32, 36), (36, 44), (39, 50)), 4, P["d1"], None, P["d2"])
        c.r(37, 51, 41, 53, B["d1"])              # raised foot: toe left,
        c.r(40, 49, 42, 51, B["d1"])              # heel up behind
        c.hline(37, 41, 53, B["d2"])
        c.s(37, 51, B["m"])                       # toe tip
        c.hline(27, 30, 44, P["d1"])              # knee creases
        c.hline(36, 39, 44, P["d2"])
    else:                                          # passing pose
        limb(c, ((30, 36), (29, 45), (29, 54)), 4, P["m"], P["l1"], P["d1"])
        if tall:
            side_boot_shaft(c, 29, 32, 54)
        c.r(27, 54, 33, 58, B["m"])
        c.hline(27, 33, 58, B["d2"]); c.s(27, 56, B["l1"])
        limb(c, ((32, 36), (34, 43), (34, 48)), 4, P["d1"], None, P["d2"])
        c.r(32, 49, 36, 51, B["d1"])              # trailing foot, toe left
        c.r(35, 47, 37, 49, B["d1"])              # lifted heel
        c.hline(32, 36, 51, B["d2"])
        c.s(32, 49, B["m"])                       # toe tip


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
        for x, y in ((30, 34), (29, 35), (29, 36), (30, 37)):  # на бедре
            c.s(x, y, STEEL["l1"] if y % 2 else STEEL["d1"])
    elif kind == "drawstring":
        c.r(27, 32, 35, 33, P["d1"])
        c.hline(27, 35, 32, P["m"])
        c.vline(28, 33, 35, P["l1"])              # string
    else:
        c.r(27, 32, 35, 33, BOOT["m"])
        c.hline(27, 35, 32, BOOT["l1"])


def side_arm(c, S, frame, bob, sleeves="long", glove=None, wrist=False):
    # рука заканчивается у пояса: кисть никогда не висит на бёдрах/ягодицах
    sw = {0: ((30, 17), (29, 24), (28, 30)),
          1: ((30, 17), (26, 24), (24, 29)),
          2: ((30, 17), (32, 24), (33, 30))}[frame]
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
    c.r(ex, ey + 1, ex + 1, ey + 3, hcol)         # компактная кисть 2px
    c.s(ex, ey + 1, mul(hcol, 1.15))
    c.s(ex + 1, ey + 3, mul(hcol, 0.8, 0.1))      # тень под кистью
    if wrist:                                     # wrist device
        c.r(ex - 1, ey - 2, ex + 2, ey, GUNMETAL["d1"])
        c.s(ex, ey - 1, GREEN["hi"])


def side_head(c, hair, hairstyle, bob, glasses=False, female=False):
    S = SKIN
    c.r(30, 12 + bob, 34, 15 + bob, S["d1"])      # neck
    c.r(27, 4 + bob, 35, 12 + bob, S["m"])        # skull
    c.vline(27, 5 + bob, 11 + bob, S["l1"])       # face front light
    if female:
        c.vline(26, 8 + bob, 10 + bob, S["m"])    # nose, clear protrusion
        c.s(25, 9 + bob, S["m"])                  # nose tip forward
        c.s(26, 10 + bob, S["d1"])
        c.s(27, 11 + bob, LIP)                    # lips
        c.clear(27, 12 + bob)                     # soft chin
        c.s(28, 12 + bob, S["d1"])
    else:
        c.s(26, 8 + bob, S["m"]); c.s(26, 9 + bob, S["m"])   # nose
        c.s(25, 9 + bob, S["m"])                  # nose tip forward
        c.s(26, 10 + bob, S["d1"])
        c.s(27, 11 + bob, S["d1"]); c.s(28, 11 + bob, S["d1"])   # mouth
        c.s(27, 12 + bob, S["d1"])                # chin
    c.s(28, 8 + bob, EYE)                         # eye
    c.s(28, 7 + bob, S["d1"])                     # brow
    H = ramp(hair) if hair else None
    if H:
        c.r(28, 2 + bob, 36, 5 + bob, H["m"])     # crown
        c.r(33, 5 + bob, 36, 13 + bob, H["d1"])   # back mass covers nape
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
            c.hline(28, 35, 5 + bob, H["d1"])     # slicked back
            c.r(34, 6 + bob, 36, 13 + bob, H["m"])         # gathered back
            c.r(36, 4 + bob, 38, 7 + bob, H["m"])          # bun BEHIND skull
            c.s(38, 4 + bob, H["d1"]); c.s(38, 7 + bob, H["d1"])   # rounding
            c.s(37, 5 + bob, H["l1"])             # bun catch-light
            c.s(36, 8 + bob, (58, 62, 56))        # hair tie
            c.s(35, 14 + bob, H["d1"])            # loose nape strand
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
    side_head(c, cfg["hair"], cfg["hairstyle"], bob, cfg.get("glasses"),
              female=cfg.get("female", False))
    c.rim_light()
    c.outline()
    return c


# ---------------------------------------------------------------- pickup --
def _pickup_belt(c, P, d):
    c.r(26, 32 + d, 37, 33 + d, BOOT["m"])
    c.hline(26, 37, 32 + d, BOOT["l1"])
    c.s(31, 32 + d, STEEL["m"]); c.s(32, 32 + d, STEEL["d1"])


def _pickup_legs_front(c, P, stage):
    """Compressed/bent legs, ground stays at y58."""
    if stage == 1:                                # half bend, d=3
        c.r(26, 37, 37, 40, P["m"])               # hips
        c.vline(26, 37, 40, P["l1"]); c.vline(37, 37, 40, P["d1"])
        for x0, x1, idx in ((26, 30, 0), (33, 37, 1)):
            c.r(x0, 40, x1, 54, P["m" if idx == 0 else "d1"])
            c.vline(x0, 40, 54, P["l1"] if idx == 0 else P["m"])
            c.vline(x1, 40, 54, P["d1"] if idx == 0 else P["d2"])
            c.hline(x0 + 1, x1 - 1, 47, P["d1"])  # bent knee crease
            c.hline(x0 + 1, x1 - 1, 48, P["d2"])
            boots_front(c, x0, x1, 54)
        c.vline(31, 38, 42, P["d2"]); c.vline(32, 38, 42, P["d1"])
    else:                                          # deep crouch, d=7
        c.r(26, 41, 37, 44, P["m"])               # hips low
        c.vline(26, 41, 44, P["l1"]); c.vline(37, 41, 44, P["d1"])
        # knees pushed outward
        limb(c, ((27, 44), (24, 49)), 4, P["m"], P["l1"], P["d1"])
        limb(c, ((24, 49), (24, 53)), 4, P["m"], P["l1"], P["d1"])
        limb(c, ((33, 44), (36, 49)), 4, P["d1"], P["m"], P["d2"])
        limb(c, ((36, 49), (36, 53)), 4, P["d1"], P["m"], P["d2"])
        c.hline(24, 27, 49, P["d2"])              # knee folds
        c.hline(36, 39, 49, P["d2"])
        boots_front(c, 23, 27, 54)
        boots_front(c, 35, 39, 54)
        c.r(30, 44, 32, 48, P["d2"])              # crotch shadow

def _pickup_legs_side(c, P, stage):
    if stage == 1:                                # half bend
        c.r(27, 37, 35, 39, P["m"])               # hips
        c.vline(27, 37, 39, P["l1"]); c.vline(35, 37, 39, P["d1"])
        c.r(28, 39, 32, 54, P["m"])               # front leg
        c.vline(28, 39, 54, P["l1"]); c.vline(32, 39, 54, P["d1"])
        c.r(31, 39, 35, 52, P["d1"])              # back leg
        c.vline(35, 39, 52, P["d2"])
        c.hline(28, 32, 47, P["d1"])              # knee crease
        c.r(27, 54, 33, 58, BOOT["m"])            # boots toe-left
        c.hline(27, 33, 58, BOOT["d2"]); c.s(27, 56, BOOT["l1"])
        c.r(31, 53, 36, 56, BOOT["d1"])
        c.hline(31, 36, 56, BOOT["d2"])
    else:                                          # deep crouch, вертикальный
        c.r(27, 41, 35, 43, P["m"])               # hips low
        c.vline(27, 41, 43, P["l1"]); c.vline(35, 41, 43, P["d1"])
        c.r(24, 43, 31, 47, P["m"])               # бедро вперёд, колено слева
        c.hline(24, 31, 43, P["l1"])
        c.hline(25, 31, 47, P["d1"])              # сгиб колена
        c.r(24, 47, 28, 53, P["m"])               # голень вертикально
        c.vline(24, 47, 53, P["l1"]); c.vline(28, 47, 53, P["d1"])
        c.r(31, 43, 35, 53, P["d1"])              # задняя нога позади
        c.vline(35, 43, 53, P["d2"])
        c.hline(31, 35, 48, P["d2"])              # сгиб
        c.r(23, 54, 29, 58, BOOT["m"])            # boots toe-left
        c.hline(23, 29, 58, BOOT["d2"]); c.s(23, 56, BOOT["l1"])
        c.r(31, 54, 36, 58, BOOT["d1"])
        c.hline(31, 36, 58, BOOT["d2"]); c.s(31, 55, BOOT["m"])


def char_pickup(cfg, view, stage):
    """One pickup frame: view in {front, side, up}, stage 1 (bend) / 2 (crouch).
    Ground stays at y58; body drops by d, reaching arm goes to the floor."""
    c = C(64, 64)
    S, P = cfg["shirt"], cfg["pants"]
    fem = cfg.get("female", False)
    d = 3 if stage == 1 else 7

    if view in ("front", "up"):
        _pickup_legs_front(c, P, stage)
        front_torso(c, S, d, female=fem, bust=(view == "front"))
        _pickup_belt(c, P, d)
        if view == "front":
            # collar + buttons как в обычном фронте
            c.r(30, 15 + d, 33, 17 + d, SKIN["d1"])
            c.vline(29, 15 + d, 17 + d, S["d2"])
            c.vline(34, 15 + d, 17 + d, S["d2"])
            for by in (20, 23, 26):
                c.s(31, by + d, S["d2"])
            if cfg.get("number"):
                stencil_text(c, 33, 19 + d, (1, 0, 1, 1))
        # левая рука у бедра (чуть согнута)
        c.r(22, 16 + d, 24, 26 + d, S["m"])
        c.vline(22, 16 + d, 26 + d, S["l1"])
        sl = cfg.get("sleeves", "long")
        fcol = SKIN if sl in ("rolled", "short") else S
        c.r(23, 27 + d, 24, 33 + d, fcol["d1"])
        c.r(23, 34 + d, 24, 36 + d, SKIN["m"])    # кисть
        # правая рука тянется вниз к полу
        c.r(39, 16 + d, 41, 24 + d, S["d1"])
        c.vline(41, 16 + d, 24 + d, S["d2"])
        reach = 44 if stage == 1 else 49
        c.r(39, 25 + d, 40, reach, fcol["d1"] if sl == "long" else SKIN["d1"])
        c.vline(39, 25 + d, reach, fcol["m"] if sl == "long" else SKIN["m"])
        c.r(38, reach + 1, 39, reach + 3, SKIN["m"])       # кисть у пола
        c.s(38, reach + 1, SKIN["l1"])
        if view == "front":
            front_head(c, cfg["hair"], cfg["hairstyle"], d,
                       cfg.get("glasses"), female=fem)
        else:
            back_head(c, cfg, d)
    else:                                          # side, смотрит влево
        _pickup_legs_side(c, P, stage)
        # торс вертикальный, опущен на d
        c.r(27, 15 + d, 35, 31 + d, S["m"])
        c.vline(27, 15 + d, 31 + d, S["l1"])
        c.vline(35, 15 + d, 31 + d, S["d1"])
        c.hline(27, 35, 15 + d, S["l1"])
        c.hline(28, 34, 24 + d, S["d1"])          # складка
        c.r(27, 32 + d, 35, 33 + d, BOOT["m"])
        c.hline(27, 35, 32 + d, BOOT["l1"])
        # рука тянется вперёд-вниз к полу
        sl = cfg.get("sleeves", "long")
        acol = S if sl == "long" else SKIN
        if stage == 1:
            limb(c, ((30, 20), (27, 30), (26, 38)), 3,
                 acol["d1"], acol["m"], acol["d2"])
            c.r(25, 39, 26, 41, SKIN["m"])        # кисть
        else:
            limb(c, ((30, 24), (26, 36), (23, 47)), 3,
                 acol["d1"], acol["m"], acol["d2"])
            c.r(22, 48, 23, 50, SKIN["m"])        # кисть у пола
            c.s(22, 48, SKIN["l1"])
        # голова со сдвигом наклона
        side_head(c, cfg["hair"], cfg["hairstyle"], d,
                  cfg.get("glasses"), female=fem)
    c.rim_light()
    c.outline()
    return c
