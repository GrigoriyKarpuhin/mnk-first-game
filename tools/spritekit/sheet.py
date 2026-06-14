"""Разрезка листа «N фигур в ряд» на отдельные фигуры по фоновым зазорам."""
import numpy as np
from PIL import Image


def find_figures(im, *, white_threshold=236, min_band_frac=0.02):
    """Список (x0, x1) — горизонтальные полосы с фигурами, разделённые фоном.

    Колонка считается «пустой», если в ней нет ни одного непрозрачного
    не-белого пикселя. Узкие полосы (< min_band_frac ширины) отбрасываются как
    мусор/точки.
    """
    arr = np.asarray(im.convert("RGBA")).astype(np.int16)
    minc = arr[..., :3].min(axis=2)
    fg = (minc < white_threshold) & (arr[..., 3] >= 16)
    col_has = fg.any(axis=0)
    w = len(col_has)

    bands, x = [], 0
    while x < w:
        if col_has[x]:
            x0 = x
            while x < w and col_has[x]:
                x += 1
            bands.append((x0, x))
        else:
            x += 1
    return [b for b in bands if (b[1] - b[0]) > w * min_band_frac]


def slice_sheet(im, *, white_threshold=236, min_band_frac=0.02):
    """Список вырезанных по полосам изображений (фон НЕ удалён — это делает
    background.remove_background отдельным шагом)."""
    bands = find_figures(im, white_threshold=white_threshold, min_band_frac=min_band_frac)
    return [im.crop((x0, 0, x1, im.height)) for x0, x1 in bands]
