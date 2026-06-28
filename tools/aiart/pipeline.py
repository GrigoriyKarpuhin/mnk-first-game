"""RAW-рендер -> чистый игровой ассет.

Переиспользует tools/spritekit (вырез фона, резка листа, холст со ступнями).
Конвенции из ART_STYLE: персонаж на холсте 256 со ступнями у низа (factor=2 =
чанки как у героя; factor=1 = чёткие 256 как у охраны), профиль смотрит ВЛЕВО
(если рендер смотрит вправо — --flip); тайлы 64×64 без прозрачности; PPU
выставит Unity-импортёр по ширине PNG.
"""
from pathlib import Path

from PIL import Image

from .client import REPO_ROOT
from tools.spritekit import remove_background, slice_sheet, place_on_canvas
from tools.spritekit.layout import _resize_premult

SPRITES_DIR = REPO_ROOT / "Assets" / "Resources" / "Sprites"
CONCEPT_DIR = REPO_ROOT / "Design" / "concept"


def _save(img, out_dir, name):
    out_dir.mkdir(parents=True, exist_ok=True)
    dst = out_dir / f"{name}.png"
    img.save(dst)
    print(f"OUT: {dst}  ({img.width}x{img.height} {img.mode})")
    return dst


def process_character(raw_path, name, *, size=256, factor=2, flip=False,
                      target_fig_h=232):
    """Один ракурс персонажа -> Sprites/<name>.png (холст size, ступни у низа)."""
    fig = remove_background(Image.open(raw_path))
    if flip:
        fig = fig.transpose(Image.FLIP_LEFT_RIGHT)
    canvas = place_on_canvas(fig, size=size, target_fig_h=target_fig_h,
                             factor=factor, align="feet")
    return _save(canvas, SPRITES_DIR, name)


def process_sheet(raw_path, names, *, size=256, factor=2, flip_side=False,
                  target_fig_h=232):
    """Лист «3 фигуры в ряд» -> три спрайта одного роста.

    Порядок полос слева-направо обязан совпадать с порядком `names`
    (по нашему промпту: спереди, сбоку-влево, со спины). Полоса со «side» в
    имени отзеркаливается при flip_side.
    """
    bands = slice_sheet(Image.open(raw_path))
    if len(bands) != len(names):
        print(f"ВНИМАНИЕ: найдено {len(bands)} фигур(ы), а имён {len(names)}. "
              f"Обработаю первые {min(len(bands), len(names))}. "
              f"Проверь RAW {raw_path} (зазоры между фигурами?).")
    out = []
    for band, nm in zip(bands, names):
        fig = remove_background(band)
        if flip_side and "side" in nm:
            fig = fig.transpose(Image.FLIP_LEFT_RIGHT)
        canvas = place_on_canvas(fig, size=size, target_fig_h=target_fig_h,
                                 factor=factor, align="feet")
        out.append(_save(canvas, SPRITES_DIR, nm))
    return out


def process_tile(raw_path, name, *, size=64):
    """Текстура -> Sprites/<name>.png квадрат size×size, RGB без альфы."""
    im = Image.open(raw_path).convert("RGB")
    if im.size != (size, size):
        im = im.resize((size, size), Image.LANCZOS)
    return _save(im, SPRITES_DIR, name)


def _center_prop(fig, size, factor, margin_frac=0.06):
    """Вписать очищенную фигуру `fig` по ЦЕНТРУ прозрачного холста size×size."""
    avail = max(1, int(size * (1 - 2 * margin_frac)))
    s = avail / max(fig.width, fig.height)
    lw = max(1, round(fig.width * s / factor))
    lh = max(1, round(fig.height * s / factor))
    small = _resize_premult(fig, lw, lh, Image.BOX)
    big = small.resize((lw * factor, lh * factor), Image.NEAREST)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(big, ((size - big.width) // 2, (size - big.height) // 2), big)
    return canvas


def process_prop(raw_path, name, *, size=128, factor=2, margin_frac=0.06):
    """Проп -> Sprites/<name>.png: вырез фона, вписать по центру холста size×size.

    В отличие от персонажа, проп центрируется по ОБЕИМ осям (пивот спрайта
    (0.5,0.5)), а не ставится ступнями к низу.
    """
    fig = remove_background(Image.open(raw_path))  # уже обрезан по bbox
    return _save(_center_prop(fig, size, factor, margin_frac), SPRITES_DIR, name)


def process_prop_sheet(raw_path, names, *, size=128, factor=2):
    """Лист из нескольких пропов на белом фоне -> отдельные спрайты.

    Объекты ищутся как связные области не-белого (scipy.label + дилатация для
    слияния частей одного объекта), упорядочиваются построчно (сверху-вниз,
    слева-направо) и сохраняются под `names`. Каждый вырез также пишется в
    Design/ai_raw/prop_sheet/crop_<idx>.png для ручной проверки/переименования.
    """
    import numpy as np
    from scipy import ndimage

    im = Image.open(raw_path).convert("RGBA")
    arr = np.asarray(im).astype(np.int16)
    minc = arr[..., :3].min(axis=2)
    fg = (minc < 236) & (arr[..., 3] >= 16)          # не-белое и непрозрачное
    fg = ndimage.binary_dilation(fg, iterations=6)   # слить части одного объекта
    lbl, n = ndimage.label(fg)
    H, W = fg.shape

    comps = []
    for i in range(1, n + 1):
        ys, xs = np.where(lbl == i)
        if len(ys) < H * W * 0.001:                  # отбросить шум
            continue
        comps.append((int(ys.min()), int(ys.max()), int(xs.min()), int(xs.max())))

    # построчная сортировка: кластеризуем по Y, внутри строки сортируем по X
    comps.sort(key=lambda c: (c[0] + c[1]) / 2)
    heights = sorted(c[1] - c[0] for c in comps) or [1]
    med_h = heights[len(heights) // 2]
    rows, cur, last = [], [], None
    for c in comps:
        cy = (c[0] + c[1]) / 2
        if last is not None and cy - last > med_h * 0.6:
            rows.append(cur); cur = []
        cur.append(c); last = cy
    if cur:
        rows.append(cur)
    order = [c for row in rows for c in sorted(row, key=lambda c: c[2])]

    print(f"Найдено объектов: {len(order)} (имён: {len(names)})")
    raw_dir = REPO_ROOT / "Design" / "ai_raw" / "prop_sheet"
    raw_dir.mkdir(parents=True, exist_ok=True)
    out = []
    for idx, (y0, y1, x0, x1) in enumerate(order):
        pad = 4
        crop = im.crop((max(0, x0 - pad), max(0, y0 - pad),
                        min(W, x1 + pad), min(H, y1 + pad)))
        crop.save(raw_dir / f"crop_{idx}.png")       # для инспекции/переименования
        nm = names[idx] if idx < len(names) else f"prop_extra_{idx}"
        fig = remove_background(crop)
        out.append(_save(_center_prop(fig, size, factor), SPRITES_DIR, nm))
    return out


def process_concept(raw_path, name):
    """Концепт-арт -> Design/concept/<name>.png (без обработки, opaque)."""
    im = Image.open(raw_path).convert("RGB")
    return _save(im, CONCEPT_DIR, name)


def run(kind, raw_path, *, name=None, names=None, size=None, factor=2,
        flip=False, flip_side=False):
    """Диспетчер постобработки по виду ассета. Возвращает список путей результата."""
    from . import presets
    spec = presets.PRESETS[kind]
    final = size if size is not None else spec["final_size"]
    proc = spec["process"]

    if proc == "character":
        return [process_character(raw_path, name, size=final or 256,
                                  factor=factor, flip=flip)]
    if proc == "sheet":
        return process_sheet(raw_path, names, size=final or 256,
                             factor=factor, flip_side=flip_side)
    if proc == "tile":
        return [process_tile(raw_path, name, size=final or 64)]
    if proc == "prop":
        return [process_prop(raw_path, name, size=final or 128, factor=factor)]
    if proc == "prop_sheet":
        return process_prop_sheet(raw_path, names, size=final or 128, factor=factor)
    if proc == "concept":
        return [process_concept(raw_path, name)]
    raise SystemExit(f"pipeline.run: неизвестная постобработка '{proc}'")
