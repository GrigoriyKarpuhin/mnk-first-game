#!/usr/bin/env python3
"""Импорт высоко-детального арта героя в спрайты player* нужного размера.

Берёт исходный PNG (большой рендер с белым/почти-белым фоном), вырезает фон,
кропит по фигуре, премультипликат-ресайзит и кладёт на квадратный холст:
фигура по центру (или по центру головы), ступни у низа.

Вырез фона:
  * белое убирается ГЛОБАЛЬНО (min(R,G,B) >= порога) — так уходят и замкнутые
    «карманы» фона между рукой и телом, а не только фон от краёв;
  * силуэт эродится на --erode пикселей, чтобы срезать светлое антиалиас-кольцо
    («белую обводку»).

Мировой размер героя в игре нормализуется в Player.cs независимо от разрешения
спрайта, поэтому разрешение влияет только на чёткость. Профиль (_side) смотрит
влево. Для поз подбора/приседа высоту НЕ нормализуют (фигура ниже стойки) —
передаётся фиксированный --scale, калиброванный по ширине головы.

Примеры:
    python3 tools/import_player_art.py SRC.png player_side --size 256 [--flip]
    python3 tools/import_player_art.py SRC.png player_pickup_1 --scale 0.296 --center head
"""
import argparse
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter

OUT = Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "Sprites"
WHITE_THR = 236


def cut_white_bg(im, erode=2):
    """RGBA с прозрачным фоном. Белое — глобально (вкл. замкнутые карманы),
    силуэт эродится на `erode` px (срезает светлую обводку)."""
    rgb = im.convert("RGB")
    r, g, b = rgb.split()
    mn = ImageChops.darker(ImageChops.darker(r, g), b)         # per-pixel min
    alpha = mn.point(lambda v: 0 if v >= WHITE_THR else 255)   # 0 = фон
    if erode > 0:
        alpha = alpha.filter(ImageFilter.MinFilter(2 * erode + 1))
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    bb = alpha.getbbox()
    return out.crop(bb)


def premultiplied_resize(char, nw, nh):
    """LANCZOS-ресайз без белого ореола (через премультипликат-альфу)."""
    cw, ch = char.size
    cp = char.load()
    prem = Image.new("RGBA", char.size)
    pp = prem.load()
    for y in range(ch):
        for x in range(cw):
            r, g, b, a = cp[x, y]
            f = a / 255
            pp[x, y] = (round(r * f), round(g * f), round(b * f), a)
    prem = prem.resize((nw, nh), Image.LANCZOS)
    pp = prem.load()
    out = Image.new("RGBA", (nw, nh))
    op = out.load()
    for y in range(nh):
        for x in range(nw):
            r, g, b, a = pp[x, y]
            if a > 0:
                f = 255 / a
                op[x, y] = (min(255, round(r * f)), min(255, round(g * f)),
                            min(255, round(b * f)), a)
            else:
                op[x, y] = (0, 0, 0, 0)
    return out


def head_centroid_x(img):
    """X-центр головы (по верхним 20% непрозрачных пикселей)."""
    nw, nh = img.size
    px = img.load()
    rows = max(1, int(nh * 0.20))
    xs = [x for y in range(rows) for x in range(nw) if px[x, y][3] > 40]
    return sum(xs) / len(xs) if xs else nw / 2


def import_art(src, name, size=256, flip=False, scale=None, center="bbox", erode=2):
    char = cut_white_bg(Image.open(src), erode=erode)
    cw, ch = char.size
    if scale is None:                              # нормализуем по высоте
        target_h = round(size * 58 / 64)
        nw, nh = max(1, round(cw * target_h / ch)), target_h
    else:                                          # фикс. масштаб (присед)
        nw, nh = max(1, round(cw * scale)), max(1, round(ch * scale))
    char = premultiplied_resize(char, nw, nh)
    if flip:
        char = char.transpose(Image.FLIP_LEFT_RIGHT)
    bottom = round(size * 2 / 64)
    oy = size - bottom - nh
    if center == "head":
        ox = round(size / 2 - head_centroid_x(char))
    else:
        ox = (size - nw) // 2
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(char, (ox, oy), char)
    dst = OUT / f"{name}.png"
    canvas.save(dst)
    print(f"wrote {dst.name}  {size}x{size}  char {nw}x{nh} at ({ox},{oy})")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("name", help="имя спрайта без .png")
    ap.add_argument("--size", type=int, default=256)
    ap.add_argument("--flip", action="store_true", help="отзеркалить (профиль -> влево)")
    ap.add_argument("--scale", type=float, default=None,
                    help="фикс. масштаб src->out (для приседа, без нормализации высоты)")
    ap.add_argument("--center", choices=("bbox", "head"), default="bbox")
    ap.add_argument("--erode", type=int, default=2)
    a = ap.parse_args()
    import_art(a.src, a.name, a.size, a.flip, a.scale, a.center, a.erode)
