#!/usr/bin/env python3
"""Процедурная боковая «юбка» стены (wall_edge.png) для 3D-эффекта в top-down.

Короткая горизонтальная грань стены: сверху (у крышки стены) — светлая ловящая
свет кромка, ниже — тёмная бетонная передняя грань, снизу (у пола) — почти чёрная
контактная тень. Свет сверху-слева. Бесшовно по X (тайлится в ряд вдоль стены).
GameGrid.AddWallEdge сам масштабирует и ставит спрайт у южной кромки клетки-стены.

Не-разрушающий: пишет только wall_edge.png (+ .meta, если его нет).
Запуск из корня репозитория:  python3 tools/wall_edge.py
"""
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sprite_lib import C, cl, OUT, ensure_sprite_meta  # noqa: E402

W, H = 64, 22
HI_H = 12                     # высота светлой кромки

TOP_LIGHT = (122, 128, 122)   # верхняя кромка грани ловит свет
FACE = (60, 64, 64)           # бетонная передняя грань
FACE_DK = (40, 43, 44)        # нижняя часть грани
CONTACT = (16, 18, 20)        # контактная тень у пола
RUST = (110, 60, 36)
HILIGHT = (170, 176, 166)     # холодный блик на верх-левых кромках


def lerp(a, b, t):
    return tuple(cl(a[i] + (b[i] - a[i]) * t) for i in range(3))


def make_dark():
    """Тёмная грань (юг/восток): сверху светлая кромка -> низ контактная тень."""
    c = C(W, H)
    rng = random.Random(11)
    for y in range(H):
        t = y / (H - 1)                       # 0 верх .. 1 низ
        if t < 0.12:
            base = lerp(TOP_LIGHT, FACE, t / 0.12)
        elif t < 0.75:
            base = lerp(FACE, FACE_DK, (t - 0.12) / 0.63)
        else:
            base = lerp(FACE_DK, CONTACT, (t - 0.75) / 0.25)
        for x in range(W):
            sx = (x / (W - 1) - 0.5) * -10     # свет сверху-слева: лево светлее
            n = rng.randint(-5, 5)
            c.s(x, y, tuple(cl(base[i] + sx + n) for i in range(3)), 255)
    for _ in range(5):                        # редкие потёки/ржавчина
        rx = rng.randrange(W)
        col = RUST if rng.random() < 0.3 else (28, 30, 30)
        for y in range(rng.randrange(2, H)):
            c.blend(rx, y, col, 0.3)
    c.save("wall_edge.png")
    ensure_sprite_meta(OUT / "wall_edge.png", ppu=W)


def make_hi():
    """Светлый блик (север/запад): яркая полупрозрачная кромка у ВЕРХА (бизнес-конец
    спрайта +y), плавно в ноль вниз. Накладывается на верх-левые грани приподнятой
    стены (свет сверху-слева)."""
    c = C(W, HI_H)
    rng = random.Random(17)
    for y in range(HI_H):
        a = int(195 * (1 - y / (HI_H - 1)) ** 1.3)   # ярче у y=0, в ноль вниз
        for x in range(W):
            n = rng.randint(-4, 4)
            c.s(x, y, tuple(cl(HILIGHT[i] + n) for i in range(3)), max(0, a))
    c.save("wall_edge_hi.png")
    ensure_sprite_meta(OUT / "wall_edge_hi.png", ppu=W)


def main():
    make_dark()
    make_hi()


if __name__ == "__main__":
    main()
