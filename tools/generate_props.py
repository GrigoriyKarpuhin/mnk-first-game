#!/usr/bin/env python3
"""generate_props — высокодетальные пропы карты Block C (камера/кровать/дверь + B/C/D).

Отдельный модуль от generate_sprites.py: его main() перегенерил бы AI-арт
героя/охраны/инмейта, поэтому пропы карты живут здесь и генерятся ПО ОДНОМУ
(или `all`). Стиль и палитра — из sprite_lib (Design/ART_STYLE.md): мрачная
тюрьма, холодные зелёные тени, ржавчина — единственный тёплый акцент.

Все пропы — top-down, более высокое разрешение, чем блочные 64px-пропы.
PPU импортируется автоматически = ширина PNG (Assets/Editor/PixelArtSpriteImporter),
поэтому абсолютный размер холста неважен — важна пропорция; финальный размер
в мире задаёт C#-код (доля CellSize).

Использование:
    python3 tools/generate_props.py camera bed door
    python3 tools/generate_props.py all
"""

import math
import random
import shutil
import sys
from pathlib import Path

from sprite_lib import C, ramp, mul, noise_fill, GREEN, STENCIL, OUT

# ------------------------------------------------------------- палитры --
STEEL = ramp((96, 102, 104))      # холодная нерж. сталь
DSTEEL = ramp((52, 56, 62))       # тёмный гунметал / каркас
CONCR = ramp((62, 66, 60))        # бетон скамьи/пол
RUST = ramp((118, 62, 36))        # тёплый ржавый акцент (единственный тёплый)
MATT = ramp((92, 94, 82))         # матрас грязно-олив
BLANK = ramp((72, 80, 72))        # одеяло
YEL = ramp((150, 132, 58))        # выцветшая жёлтая разметка
DARK = (10, 13, 11)


def disc(c, cx, cy, r, col, edge=None):
    """Залитый круг; edge — тонкая тёмная кромка."""
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                c.s(x, y, edge if (edge and d > r - 1.3) else col)


def ring(c, cx, cy, r, n, col):
    for k in range(n):
        a = k / n * math.tau
        c.s(int(round(cx + math.cos(a) * r)), int(round(cy + math.sin(a) * r)), col)


def rust_streaks(c, seed, x0, y0, x1, y1, n=8, length=8):
    rng = random.Random(seed)
    for _ in range(n):
        x = rng.randrange(x0, x1)
        y = rng.randrange(y0, y1)
        col = RUST["d1"] if rng.random() < 0.5 else RUST["m"]
        for i in range(rng.randrange(3, length)):
            c.blend(x + rng.randint(-1, 1), y + i, col, 0.35)


def rivets(c, pts, lit, dark):
    for x, y in pts:
        c.s(x, y, lit)
        c.s(x + 1, y + 1, dark)


# =====================================================================
# A. ЯДРО
# =====================================================================
def gen_camera():
    """Настенная PTZ-камера: кронштейн, корпус, объектив с зелёным glow."""
    c = C(128, 128)
    # кронштейн на стене (сверху)
    c.r(52, 8, 76, 16, DSTEEL["d1"])
    c.r(52, 8, 76, 9, DSTEEL["l1"])
    c.r(52, 15, 76, 16, DSTEEL["d2"])
    c.r(60, 16, 68, 30, DSTEEL["m"])          # шея-крепление
    c.vline(60, 16, 30, DSTEEL["l1"])
    c.vline(68, 16, 30, DSTEEL["d2"])
    # корпус
    c.r(34, 28, 98, 76, STEEL["m"])
    c.r(34, 28, 98, 33, STEEL["l1"])
    c.r(34, 72, 98, 76, STEEL["d2"])
    c.vline(34, 28, 76, STEEL["l1"])
    c.vline(98, 28, 76, STEEL["d1"])
    noise_fill(c, STEEL["m"], 4, 11, 37, 36, 95, 70)
    c.hline(34, 98, 50, STEEL["d1"])          # панельный шов
    rivets(c, [(38, 32), (94, 32), (38, 72), (94, 72)], STEEL["l2"], STEEL["d2"])
    c.s(88, 36, (70, 210, 100))               # LED питания
    c.s(42, 36, RUST["m"])
    # объектив (нижний перёд)
    cx, cy = 66, 88
    disc(c, cx, cy, 24, DSTEEL["m"], DSTEEL["d2"])
    disc(c, cx, cy, 19, DARK)
    disc(c, cx, cy, 13, GREEN["d"])
    disc(c, cx, cy, 9, GREEN["m"])
    disc(c, cx, cy, 4, GREEN["hi"])
    c.s(cx - 4, cy - 4, GREEN["glow"])        # блик
    c.s(cx - 5, cy - 3, GREEN["glow"])
    ring(c, cx, cy, 16, 10, (150, 44, 40))    # ИК-диоды
    rust_streaks(c, 7, 40, 60, 92, 72, n=7)
    c.rim_light()
    c.outline()
    c.save("camera.png")


def gen_bed():
    """Тюремная шконка top-down: рама, матрас, подушка, скомканное одеяло."""
    c = C(192, 120)
    FR = ramp((84, 90, 96))
    # рама
    c.r(8, 16, 184, 110, FR["d1"])
    c.r(8, 16, 184, 21, FR["l1"])
    c.r(8, 105, 184, 110, FR["d2"])
    c.vline(8, 16, 110, FR["l1"])
    c.vline(184, 16, 110, FR["d1"])
    # матрас
    c.r(16, 24, 176, 102, MATT["m"])
    noise_fill(c, MATT["m"], 5, 3, 16, 24, 176, 102)
    c.r(16, 24, 176, 28, MATT["l1"])
    c.r(16, 98, 176, 102, MATT["d1"])
    # пятна
    rng = random.Random(5)
    for _ in range(9):
        x, y, r = rng.randrange(28, 168), rng.randrange(34, 94), rng.randrange(4, 11)
        for yy in range(y - r, y + r):
            for xx in range(x - r, x + r):
                if (xx - x) ** 2 + (yy - y) ** 2 <= r * r:
                    c.blend(xx, yy, (66, 62, 48), 0.22)
    # подушка (голова слева)
    c.r(22, 30, 58, 96, MATT["l1"])
    c.r(22, 30, 58, 34, MATT["l2"])
    c.r(22, 92, 58, 96, MATT["d1"])
    c.vline(58, 30, 96, MATT["d1"])
    # одеяло, наброшено справа, со складками
    c.r(68, 28, 172, 98, BLANK["m"])
    c.r(68, 28, 172, 32, BLANK["l1"])
    c.r(68, 94, 172, 98, BLANK["d2"])
    c.vline(68, 28, 98, BLANK["l1"])
    for fx in range(80, 168, 15):
        c.vline(fx, 30, 96, BLANK["d1"])
        c.vline(fx + 1, 30, 96, BLANK["l1"])
    rust_streaks(c, 9, 12, 24, 22, 104, n=5)
    rust_streaks(c, 13, 178, 24, 184, 104, n=5)
    c.rim_light()
    c.outline()
    c.save("bed.png")


def gen_door():
    """Стальная створка УЖЕ проёма: лист с боковыми пустыми полями (виден косяк).

    Лист ~70% ширины холста по центру; прозрачные поля по бокам показывают
    косяк/стену. Аспект ~1:1, чтобы при заполнении ширины проёма дверь не
    оказалась выше клетки. Механика «уезжает в левый косяк» сохранена в C#.
    """
    c = C(176, 176)
    M = ramp((58, 63, 68))             # gunmetal створки
    L = 28                             # левый край листа
    R = 148                            # правый край листа (≈68% ширины)
    c.r(L, 8, R, 168, M["m"])
    c.r(L, 8, R, 13, M["l2"])          # верхняя фаска
    c.r(L, 162, R, 168, M["d2"])       # нижняя тень
    c.vline(L, 8, 168, M["l1"])        # светлый левый край
    c.vline(L + 1, 8, 168, M["l1"])
    c.vline(R, 8, 168, M["d2"])        # тёмный правый край (косяк)
    c.vline(R - 1, 8, 168, M["d1"])
    noise_fill(c, M["m"], 3, 21, L + 3, 16, R - 3, 160)
    # вертикальный центральный шов
    c.vline(87, 12, 164, M["d2"])
    c.vline(88, 12, 164, mul(M["d2"], 0.7))
    c.vline(89, 12, 164, M["l1"])
    # поперечные ребра жёсткости
    for y in (44, 120):
        c.r(L + 4, y, R - 4, y + 4, M["d1"])
        c.hline(L + 4, R - 4, y, M["l1"])
        c.hline(L + 4, R - 4, y + 4, M["d2"])
    # окошко с армированным зелёным стеклом
    c.r(56, 58, 120, 92, M["d2"])
    c.r(60, 62, 116, 88, GREEN["d2"])
    c.r(62, 64, 114, 70, GREEN["d"])
    for gx in range(64, 114, 8):       # сетка-армировка
        c.vline(gx, 64, 86, mul(GREEN["d"], 0.7))
    c.hline(64, 112, 75, GREEN["m"])   # световая щель
    c.s(72, 75, GREEN["hi"])
    c.s(96, 75, GREEN["hi"])
    # кейпад
    c.r(108, 126, 138, 152, (16, 19, 18))
    c.r(110, 128, 136, 134, GREEN["d"])
    c.s(113, 131, GREEN["glow"])
    c.s(118, 131, GREEN["m"])
    for yy in (138, 143, 148):
        for xx in (112, 119, 126, 133):
            c.s(xx, yy, M["l1"])
            c.s(xx + 1, yy + 1, M["d2"])
    # болты по углам
    rivets(c, [(L + 6, 16), (R - 6, 16), (L + 6, 160), (R - 6, 160)],
           M["l2"], M["d2"])
    rust_streaks(c, 25, L + 4, 96, R - 4, 160, n=10, length=12)
    # царапины
    rng = random.Random(31)
    for _ in range(14):
        x, y = rng.randrange(L + 4, R - 4), rng.randrange(16, 160)
        for i in range(rng.randrange(2, 6)):
            c.s(x + i, y + i // 2, M["l1"])
    c.rim_light()
    c.outline()
    c.save("door_metal.png")


# =====================================================================
# B. ЗАМЕНА CRT-ЗАГЛУШЕК
# =====================================================================
def gen_stairs():
    """Пролёт лестницы top-down: ступени уходят вверх, тетивы по краям."""
    c = C(128, 128)
    M = ramp((78, 82, 84))
    c.r(14, 8, 114, 120, M["d1"])             # колодец
    c.r(20, 12, 108, 116, M["m"])
    # ступени: ближняя (низ) светлее, дальняя темнее
    n = 9
    for i in range(n):
        y0 = 14 + i * 11
        t = i / (n - 1)
        base = mul(M["m"], 1.18 - t * 0.5)
        c.r(22, y0, 106, y0 + 8, base)
        c.hline(22, 106, y0, mul(base, 1.25))     # передняя кромка-блик
        c.hline(22, 106, y0 + 8, mul(base, 0.55))  # тень подступёнка
    # тетивы/перила
    c.r(14, 8, 21, 120, M["d2"])
    c.r(107, 8, 114, 120, M["d2"])
    c.vline(18, 8, 120, M["l1"])
    c.vline(110, 8, 120, M["d2"])
    for y in range(16, 118, 16):              # стойки перил
        c.s(17, y, M["l2"])
        c.s(111, y, M["d1"])
    rust_streaks(c, 17, 22, 60, 106, 116, n=7)
    c.rim_light()
    c.outline()
    c.save("stairs.png")


def gen_observation_dome():
    """Подвесной купол-паноптикум: тёмная полусфера, зелёный визор, мигалка."""
    c = C(192, 192)
    cx, cy = 96, 100
    # внешний кожух
    disc(c, cx, cy, 84, DSTEEL["d1"], DSTEEL["d2"])
    disc(c, cx, cy, 78, DSTEEL["m"])
    # купол (полусфера с градиентом к низу)
    for y in range(cy - 78, cy + 78):
        for x in range(cx - 78, cx + 78):
            d = math.hypot(x - cx, y - cy)
            if d <= 76:
                shade = 0.55 + 0.6 * max(0.0, 1 - math.hypot(x - (cx - 22), y - (cy - 26)) / 96)
                c.s(x, y, mul((40, 44, 50), shade))
    # лента тонированного зелёного стекла (визор)
    for y in range(cy + 6, cy + 30):
        for x in range(cx - 70, cx + 70):
            d = math.hypot(x - cx, y - cy)
            if 58 <= d <= 72:
                c.s(x, y, GREEN["d"] if (x % 3) else GREEN["m"])
    c.s(cx - 30, cy + 16, GREEN["hi"])        # блики на стекле
    c.s(cx + 24, cy + 18, GREEN["hi"])
    # струты-подвес
    for sx in (cx - 40, cx, cx + 40):
        c.r(sx - 2, 8, sx + 2, cy - 60, DSTEEL["d1"])
        c.vline(sx - 2, 8, cy - 60, DSTEEL["m"])
    c.r(cx - 50, 6, cx + 50, 12, DSTEEL["d2"])  # потолочная база
    # верхний блик купола
    disc(c, cx - 26, cy - 30, 10, mul((60, 66, 74), 1.3))
    c.s(cx + 36, cy - 6, (200, 60, 50))       # красная мигалка
    c.blend(cx + 36, cy - 6, (255, 120, 90), 0.6)
    rust_streaks(c, 19, cx - 50, cy + 30, cx + 50, cy + 60, n=8)
    c.rim_light()
    c.outline()
    c.save("observation_dome.png")


def gen_keypad():
    """Настенный кейпад-замок: стальная панель, кнопки, зелёный экранчик, слот."""
    c = C(96, 112)
    M = ramp((70, 75, 80))
    c.r(14, 8, 82, 104, M["d1"])              # корпус
    c.r(18, 12, 78, 100, M["m"])
    c.r(18, 12, 78, 15, M["l1"])
    c.r(18, 97, 78, 100, M["d2"])
    # экран
    c.r(24, 18, 72, 34, (12, 20, 14))
    c.r(26, 20, 70, 32, GREEN["d2"])
    c.hline(28, 66, 24, GREEN["m"])
    c.hline(28, 54, 28, GREEN["hi"])
    c.s(28, 22, GREEN["glow"])
    # кнопки 4x3
    for r in range(4):
        for col in range(3):
            bx = 26 + col * 16
            by = 40 + r * 14
            c.r(bx, by, bx + 11, by + 10, M["d2"])
            c.r(bx + 1, by + 1, bx + 10, by + 9, M["l1"])
            c.hline(bx + 1, bx + 10, by + 9, M["d2"])
            c.s(bx + 5, by + 4, M["d1"])
    # картоприёмник + LED
    c.r(24, 98, 72, 100, (20, 24, 22))
    c.s(74, 16, (70, 210, 100))
    rust_streaks(c, 23, 16, 80, 80, 100, n=4)
    c.rim_light()
    c.outline()
    c.save("keypad.png")


def gen_smoke_spot():
    """Угол-курилка: бетонная лавка, ведро-пепельница с окурками, бычки."""
    c = C(128, 128)
    # лавка
    c.r(16, 64, 112, 92, CONCR["m"])
    c.r(16, 64, 112, 68, CONCR["l1"])
    c.r(16, 88, 112, 92, CONCR["d2"])
    noise_fill(c, CONCR["m"], 5, 4, 18, 66, 110, 90)
    c.r(20, 92, 30, 110, CONCR["d1"])         # ножки
    c.r(98, 92, 108, 110, CONCR["d1"])
    # ведро-пепельница
    c.r(78, 40, 104, 70, DSTEEL["m"])
    c.r(78, 40, 104, 44, DSTEEL["l1"])
    c.r(78, 66, 104, 70, DSTEEL["d2"])
    c.vline(78, 40, 70, DSTEEL["l1"])
    c.vline(104, 40, 70, DSTEEL["d1"])
    c.r(80, 42, 102, 48, (18, 16, 14))        # песок/пепел сверху
    rng = random.Random(8)
    for _ in range(7):                        # бычки в ведре
        x, y = rng.randrange(82, 100), rng.randrange(43, 47)
        c.s(x, y, (210, 200, 180))
        c.s(x + 1, y, (150, 70, 40))
    for _ in range(8):                        # бычки на полу
        x, y = rng.randrange(24, 100), rng.randrange(96, 116)
        c.s(x, y, (200, 192, 172))
        c.s(x + 1, y, (140, 64, 38))
    rust_streaks(c, 29, 80, 44, 104, 68, n=5)
    c.rim_light()
    c.outline()
    c.save("smoke_spot.png")


# =====================================================================
# C. МЕБЕЛЬ
# =====================================================================
def gen_toilet():
    """Стальной тюремный унитаз top-down."""
    c = C(96, 112)
    c.r(28, 10, 68, 30, STEEL["d1"])          # бачок/задняя секция
    c.r(30, 12, 66, 28, STEEL["m"])
    c.r(30, 12, 66, 14, STEEL["l1"])
    c.s(48, 20, STEEL["l2"])                  # кнопка слива
    # чаша (овал)
    cx, cy = 48, 68
    for y in range(cy - 30, cy + 30):
        for x in range(cx - 26, cx + 26):
            if ((x - cx) / 26) ** 2 + ((y - cy) / 30) ** 2 <= 1:
                c.s(x, y, STEEL["m"])
    for y in range(cy - 24, cy + 24):
        for x in range(cx - 20, cx + 20):
            if ((x - cx) / 20) ** 2 + ((y - cy) / 24) ** 2 <= 1:
                c.s(x, y, STEEL["l1"])         # ободок-свет
    for y in range(cy - 17, cy + 17):
        for x in range(cx - 13, cx + 13):
            if ((x - cx) / 13) ** 2 + ((y - cy) / 17) ** 2 <= 1:
                c.s(x, y, (24, 30, 30))        # тёмное жерло
    c.s(cx - 6, cy - 8, STEEL["l2"])
    rust_streaks(c, 33, 30, 40, 66, 92, n=5)
    c.rim_light()
    c.outline()
    c.save("toilet.png")


def gen_sink():
    """Стальная раковина top-down: чаша, кран, слив."""
    c = C(96, 96)
    c.r(14, 18, 82, 80, STEEL["d1"])          # столешница
    c.r(18, 22, 78, 76, STEEL["m"])
    c.r(18, 22, 78, 25, STEEL["l1"])
    # чаша
    cx, cy = 48, 52
    for y in range(cy - 22, cy + 22):
        for x in range(cx - 26, cx + 26):
            if ((x - cx) / 26) ** 2 + ((y - cy) / 22) ** 2 <= 1:
                c.s(x, y, STEEL["d1"])
    for y in range(cy - 17, cy + 17):
        for x in range(cx - 21, cx + 21):
            if ((x - cx) / 21) ** 2 + ((y - cy) / 17) ** 2 <= 1:
                c.s(x, y, (30, 36, 36))
    disc(c, cx, cy + 4, 3, (16, 20, 20))      # слив
    c.s(cx - 8, cy - 6, STEEL["l2"])          # блик воды
    # кран (сзади)
    c.r(44, 20, 52, 30, STEEL["m"])
    c.r(46, 14, 50, 22, STEEL["l1"])
    c.s(48, 30, GREEN["m"])
    rust_streaks(c, 37, 20, 26, 76, 74, n=4)
    c.rim_light()
    c.outline()
    c.save("sink.png")


def gen_desk():
    """Откидной настенный стол top-down: столешница, кронштейн, бумаги."""
    c = C(160, 96)
    W = ramp((96, 86, 66))                    # дерево/ламинат
    c.r(12, 20, 148, 78, W["m"])
    c.r(12, 20, 148, 24, W["l1"])
    c.r(12, 72, 148, 78, W["d2"])
    c.vline(12, 20, 78, W["l1"])
    c.vline(148, 20, 78, W["d1"])
    noise_fill(c, W["m"], 5, 12, 16, 26, 144, 72)
    for gx in range(20, 146, 5):              # доски
        c.vline(gx, 26, 72, mul(W["m"], 0.9))
    # кронштейны к стене
    c.r(28, 78, 40, 92, DSTEEL["d1"])
    c.r(120, 78, 132, 92, DSTEEL["d1"])
    # бумаги
    c.r(30, 30, 64, 60, (188, 188, 174))
    c.r(34, 34, 68, 64, (172, 172, 158))
    c.hline(38, 60, 40, (120, 120, 110))
    c.hline(38, 56, 46, (120, 120, 110))
    c.s(120, 36, GREEN["m"])                  # карандаш/маркер
    rust_streaks(c, 41, 16, 26, 144, 74, n=4)
    c.rim_light()
    c.outline()
    c.save("desk.png")


def gen_stool():
    """Стальной табурет top-down: круглый сиденье + крестовина ножек."""
    c = C(72, 72)
    cx, cy = 36, 36
    for sx, sy in ((cx, 10), (cx, 62), (10, cy), (62, cy)):  # ножки-тени
        c.vline(sx, min(sy, cy), max(sy, cy), DSTEEL["d2"]) if sx == cx else \
            c.hline(min(sx, cx), max(sx, cx), sy, DSTEEL["d2"])
    disc(c, cx, cy, 22, STEEL["d1"], DSTEEL["d2"])
    disc(c, cx, cy, 19, STEEL["m"])
    disc(c, cx, cy, 12, STEEL["l1"])
    c.s(cx - 6, cy - 6, STEEL["l2"])
    rust_streaks(c, 43, 18, 18, 54, 54, n=3)
    c.rim_light()
    c.outline()
    c.save("stool.png")


def gen_locker():
    """Стальной шкаф-локер top-down: дверца, вентиляция, защёлка, номер."""
    c = C(88, 120)
    c.r(12, 8, 76, 112, STEEL["d1"])
    c.r(16, 12, 72, 108, STEEL["m"])
    c.r(16, 12, 72, 16, STEEL["l1"])
    c.r(16, 104, 72, 108, STEEL["d2"])
    c.vline(16, 12, 108, STEEL["l1"])
    c.vline(72, 12, 108, STEEL["d1"])
    noise_fill(c, STEEL["m"], 3, 14, 18, 18, 70, 106)
    c.vline(44, 14, 106, STEEL["d2"])         # шов дверцы
    c.vline(45, 14, 106, STEEL["l1"])
    for vy in range(24, 44, 4):               # вентиляция
        c.hline(24, 40, vy, STEEL["d2"])
        c.hline(50, 66, vy, STEEL["d2"])
    c.r(48, 60, 54, 72, DSTEEL["d1"])         # ручка-защёлка
    c.s(50, 64, STEEL["l2"])
    c.s(36, 18, STENCIL)                      # номер-трафарет
    c.s(38, 18, STENCIL)
    c.s(36, 19, STENCIL)
    c.s(38, 19, STENCIL)
    rust_streaks(c, 45, 18, 30, 70, 104, n=6)
    c.rim_light()
    c.outline()
    c.save("locker.png")


def gen_table_canteen():
    """Длинный стол-столовая top-down: столешница + лавки по сторонам."""
    c = C(176, 96)
    W = ramp((88, 90, 80))
    c.r(20, 30, 156, 66, W["m"])              # столешница
    c.r(20, 30, 156, 34, W["l1"])
    c.r(20, 62, 156, 66, W["d2"])
    noise_fill(c, W["m"], 4, 15, 24, 36, 152, 62)
    c.r(16, 14, 160, 24, DSTEEL["m"])         # лавка верхняя
    c.r(16, 14, 160, 16, DSTEEL["l1"])
    c.r(16, 72, 160, 82, DSTEEL["m"])         # лавка нижняя
    c.r(16, 80, 160, 82, DSTEEL["d2"])
    for bx in (30, 88, 146):                  # болты/опоры
        c.s(bx, 48, W["d1"])
    rust_streaks(c, 47, 24, 36, 152, 62, n=5)
    c.rim_light()
    c.outline()
    c.save("table_canteen.png")


# =====================================================================
# D. АТМОСФЕРА / ДЕКАЛИ
# =====================================================================
def gen_window_barred():
    """Зарешёченное окно: тёмный проём, вертикальные прутья, рама."""
    c = C(144, 96)
    c.r(8, 10, 136, 86, DSTEEL["d1"])         # рама
    c.r(14, 16, 130, 80, (20, 28, 26))        # тёмное стекло
    for y in range(16, 80):                   # слабый ночной свет
        for x in range(14, 130):
            t = 1 - abs(x - 72) / 60
            if t > 0:
                c.blend(x, y, (40, 60, 52), t * 0.25)
    for bx in range(24, 126, 16):             # прутья
        c.r(bx, 14, bx + 2, 82, DSTEEL["m"])
        c.vline(bx, 14, 82, DSTEEL["l1"])
        c.vline(bx + 2, 14, 82, DSTEEL["d2"])
    c.r(8, 10, 136, 14, DSTEEL["l1"])         # верхняя фаска рамы
    c.r(8, 82, 136, 86, DSTEEL["d2"])
    rust_streaks(c, 51, 16, 18, 128, 78, n=7)
    c.rim_light()
    c.outline()
    c.save("window_barred.png")


def gen_wall_lamp():
    """Настенный LED-плафон: корпус + ярко-зеленовато-белая панель + halo."""
    c = C(112, 64)
    c.r(10, 14, 102, 50, DSTEEL["d1"])        # корпус
    c.r(14, 18, 98, 46, (210, 224, 206))      # светящаяся панель
    c.r(16, 20, 96, 24, (236, 248, 232))
    for x in range(16, 96, 6):                # сегменты лампы
        c.vline(x, 20, 44, (196, 214, 196))
    c.r(10, 14, 102, 16, DSTEEL["l1"])
    c.r(10, 48, 102, 50, DSTEEL["d2"])
    for y in range(8, 58):                    # halo свечения
        for x in range(4, 108):
            d = abs(x - 56) / 56 + abs(y - 32) / 26
            if d < 1:
                c.blend(x, y, (180, 210, 180), max(0.0, 0.22 - d * 0.18))
    c.outline()
    c.save("wall_lamp.png")


def gen_pipes():
    """Трубный прогон вдоль стены: 3 параллельные трубы, хомуты, вентиль."""
    c = C(176, 72)
    P = ramp((92, 86, 70))
    for i, y in enumerate((14, 34, 54)):
        base = P["m"] if i != 1 else mul(P["m"], 0.85)
        c.r(6, y, 170, y + 12, base)
        c.hline(6, 170, y, mul(base, 1.3))    # верхний блик
        c.hline(6, 170, y + 11, mul(base, 0.5))  # нижняя тень
    for bx in (28, 86, 146):                  # хомуты
        c.r(bx, 10, bx + 6, 68, DSTEEL["d1"])
        c.vline(bx, 10, 68, DSTEEL["l1"])
    disc(c, 120, 40, 9, RUST["m"], RUST["d2"])  # вентиль
    c.r(118, 32, 122, 48, DSTEEL["d1"])
    rust_streaks(c, 53, 10, 12, 168, 64, n=12, length=10)
    c.rim_light()
    c.outline()
    c.save("pipes.png")


def gen_drain_grate():
    """Ливневая решётка пола: квадратная рама + прорези, влажный блеск."""
    c = C(96, 96)
    c.r(8, 8, 88, 88, DSTEEL["d1"])           # рама
    c.r(12, 12, 84, 84, (20, 24, 22))         # тёмная глубина
    for sx in range(18, 80, 9):               # прорези
        c.r(sx, 16, sx + 4, 80, DSTEEL["m"])
        c.vline(sx, 16, 80, DSTEEL["l1"])
        c.vline(sx + 4, 16, 80, DSTEEL["d2"])
    c.r(8, 8, 88, 11, DSTEEL["l1"])
    c.r(8, 85, 88, 88, DSTEEL["d2"])
    c.blend(60, 64, (90, 120, 100), 0.3)      # лужица-блеск
    c.blend(62, 66, (90, 120, 100), 0.3)
    rust_streaks(c, 55, 14, 14, 82, 82, n=6)
    c.save("drain_grate.png")                 # без outline — лежит на полу


def gen_poster_obey():
    """Плакат-пропаганда: выцветшая бумага, трафаретные строки, мотив глаза."""
    c = C(96, 128)
    PAP = ramp((118, 120, 106))
    c.r(8, 8, 88, 120, PAP["d1"])             # лист
    c.r(11, 11, 85, 117, PAP["m"])
    noise_fill(c, PAP["m"], 6, 16, 12, 12, 84, 116)
    # глаз-мотив сверху
    cx, cy = 48, 40
    for y in range(cy - 14, cy + 14):
        for x in range(cx - 22, cx + 22):
            if ((x - cx) / 22) ** 2 + ((y - cy) / 13) ** 2 <= 1:
                c.s(x, y, (40, 46, 42))
    disc(c, cx, cy, 9, GREEN["d"])
    disc(c, cx, cy, 5, GREEN["m"])
    c.s(cx - 2, cy - 2, GREEN["hi"])
    # строки-лозунги (трафаретные бруски)
    for i, y in enumerate((72, 88, 104)):
        w = (26, 34, 22)[i]
        c.r(48 - w, y, 48 + w, y + 8, (44, 50, 46))
        for bx in range(48 - w + 2, 48 + w - 1, 5):
            c.vline(bx, y + 2, y + 6, PAP["l1"])  # «буквы» как просветы
    c.s(10, 10, PAP["d2"])                    # потрёпанные углы
    c.s(86, 118, PAP["d2"])
    rust_streaks(c, 57, 12, 14, 84, 116, n=4)
    c.outline()
    c.save("poster_obey.png")


def gen_floor_stencil():
    """Жёлтая напольная каёмка-предупреждение: диагональные шевроны, скол краски."""
    c = C(176, 96)
    # фон прозрачный: рисуем только краску
    c.r(6, 10, 170, 16, YEL["d1"])            # верхняя сплошная полоса
    c.r(6, 10, 170, 11, YEL["l1"])
    c.r(6, 80, 170, 86, YEL["d1"])            # нижняя сплошная полоса
    c.r(6, 80, 170, 81, YEL["l1"])
    # центральная полоса диагональной штриховки (caution band)
    band_top, band_bot = 24, 72
    period = 22
    for x in range(0, 176 + 48):
        # рисуем жёлтую диагональную ленту шириной ~11px с шагом period
        if (x % period) < 11:
            for y in range(band_top, band_bot):
                px = x - (y - band_top)        # наклон 45°
                if 6 <= px <= 170:
                    c.s(px, y, YEL["m"])
    # верхний блик на каждом шевроне
    for x in range(0, 176 + 48):
        if (x % period) == 0:
            for y in range(band_top, band_bot):
                px = x - (y - band_top)
                if 6 <= px <= 170:
                    c.s(px, y, YEL["l1"])
    # скол краски / истирание поверх
    rng = random.Random(61)
    for _ in range(420):
        x, y = rng.randrange(6, 170), rng.randrange(10, 86)
        if c.opaque(x, y) and rng.random() < 0.5:
            if rng.random() < 0.45:
                c.clear(x, y)
            else:
                c.blend(x, y, (42, 42, 34), 0.5)
    c.save("floor_stencil.png")               # без outline — разметка на полу


# =====================================================================
REGISTRY = {
    "camera": gen_camera, "bed": gen_bed, "door": gen_door,
    "stairs": gen_stairs, "observation_dome": gen_observation_dome,
    "keypad": gen_keypad, "smoke_spot": gen_smoke_spot,
    "toilet": gen_toilet, "sink": gen_sink, "desk": gen_desk,
    "stool": gen_stool, "locker": gen_locker, "table_canteen": gen_table_canteen,
    "window_barred": gen_window_barred, "wall_lamp": gen_wall_lamp,
    "pipes": gen_pipes, "drain_grate": gen_drain_grate,
    "poster_obey": gen_poster_obey, "floor_stencil": gen_floor_stencil,
}


def _backup_door():
    """Сохранить AI-версию door_metal.png перед перезаписью (один раз)."""
    src = OUT / "door_metal.png"
    bak_dir = Path(__file__).resolve().parent / "_backup"
    bak = bak_dir / "door_metal_ai_backup.png"
    if src.exists() and not bak.exists():
        bak_dir.mkdir(exist_ok=True)
        shutil.copy2(src, bak)
        print("backed up AI door ->", bak)


def main(argv):
    if not argv:
        print("usage: generate_props.py <name ...> | all")
        print("names:", ", ".join(REGISTRY))
        return 1
    names = list(REGISTRY) if argv == ["all"] else argv
    for name in names:
        if name not in REGISTRY:
            print("unknown prop:", name)
            return 2
        if name == "door":
            _backup_door()
        REGISTRY[name]()
    print("done")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
