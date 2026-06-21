#!/usr/bin/env python3
"""Генерация тайла стены для плоского top-down со «встроенным» 3D (The Escapists).

Проект — чистый 2D вид строго сверху: стена рисуется ОДНИМ плоским тайлом
(`wall_top.png`), без геометрической высоты — ничего за стеной не перекрывается
(см. GameGrid.CreateWallVisual, ART_STYLE §4/§7e). Чтобы стена при этом читалась
ОБЪЁМНОЙ, иллюзию 3D даёт сама ТЕКСТУРА: бетонные блоки в перевязку, у каждого
блока светлая фаска по верх-левой кромке и тёмная по низ-правой + тёмный шов —
блок выглядит как приподнятый куб (свет сверху-слева).

Не-разрушающий: пишет ТОЛЬКО wall_top.png (в отличие от generate_sprites.py,
который перегенерирует весь каст и затирает AI-арт).

Запуск из корня репозитория:  python3 tools/wall_topdown.py
"""
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sprite_lib import C, cl  # noqa: E402

SIZE = 128
BRICK_W, BRICK_H = 64, 32          # перевязка: 2 блока в ряд, 4 ряда, сдвиг на пол-блока
MORTAR_PX = 2                      # шов между блоками
BEVEL = 3                          # ширина фаски (px)

MORTAR = (22, 25, 26)             # почти чёрный шов
FACE = (78, 84, 82)               # средний холодный бетон (грань блока)
LIGHT = (120, 126, 122)           # верх-левая фаска (ловит свет)
DARK = (40, 44, 44)               # низ-правая фаска (тень)


def tone(c, dv):
    return (cl(c[0] + dv), cl(c[1] + dv), cl(c[2] + dv))


def put(canvas, x, y, col):
    canvas.s(x % SIZE, y % SIZE, col)   # wrap по X — бесшовность при перевязке


def draw_brick(canvas, bx, by, rng):
    dv = rng.randint(-7, 7)             # вариация тона блока
    x0, y0 = bx + MORTAR_PX, by + MORTAR_PX
    x1, y1 = bx + BRICK_W - MORTAR_PX, by + BRICK_H - MORTAR_PX
    for y in range(y0, y1):
        for x in range(x0, x1):
            n = rng.randint(-5, 5)
            col = tone(FACE, dv + n)
            if x - x0 < BEVEL or y - y0 < BEVEL:          # верх/лево — свет
                col = tone(LIGHT, dv)
            if x >= x1 - BEVEL or y >= y1 - BEVEL:        # низ/право — тень
                col = tone(DARK, dv)
            put(canvas, x, y, col)
    # редкие сколы/трещины по кромке блока
    for _ in range(3):
        cx = rng.randrange(x0, x1)
        put(canvas, cx, y0, MORTAR)
        put(canvas, cx, y1 - 1, DARK)


def main():
    c = C(SIZE, SIZE)
    rng = random.Random(7)
    c.r(0, 0, SIZE - 1, SIZE - 1, MORTAR)                 # фон = шов
    rows = SIZE // BRICK_H
    for row in range(rows):
        by = row * BRICK_H
        off = (BRICK_W // 2) if row % 2 else 0
        bx = off - BRICK_W
        while bx < SIZE:
            draw_brick(c, bx, by, rng)
            bx += BRICK_W
    # редкий тёплый ржавый подтёк (единственный тёплый акцент)
    for _ in range(2):
        rx, ry = rng.randrange(SIZE), rng.randrange(SIZE)
        for i in range(rng.randrange(3, 7)):
            c.blend(rx % SIZE, (ry + i) % SIZE, (110, 60, 36), 0.4)
    c.save("wall_top.png")
    print(f"wall_top.png: 3D-фаски, {SIZE}x{SIZE}")


if __name__ == "__main__":
    main()
