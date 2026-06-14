"""Ужать фигуру (chunky pixel) и положить на квадратный холст ступнями к низу."""
import numpy as np
from PIL import Image


def _resize_premult(im, w, h, resample):
    """Ресайз с премультипликат-альфой — без тёмного/светлого ореола по краям."""
    arr = np.asarray(im.convert("RGBA")).astype(np.float64)
    a = arr[..., 3:4] / 255.0
    arr[..., :3] *= a
    pim = Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA").resize((w, h), resample)
    out = np.asarray(pim).astype(np.float64)
    al = out[..., 3:4] / 255.0
    np.divide(out[..., :3], al, where=al > 0, out=out[..., :3])
    out[..., :3] = out[..., :3].clip(0, 255)
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def _anchor_x(im, where):
    """X-центр якоря: 'feet' (нижние ~6%), 'head' (верхние ~22%), 'bbox' (вся фигура)."""
    a = np.asarray(im)[..., 3] >= 16
    ys = np.where(a.any(axis=1))[0]
    if not len(ys):
        return im.width / 2
    if where == "bbox":
        xs = np.where(a.any(axis=0))[0]
    else:
        frac = 0.06 if where == "feet" else 0.22
        span = max(1, int((ys[-1] - ys[0]) * frac))
        rows = (slice(ys[-1] - span, ys[-1] + 1) if where == "feet"
                else slice(ys[0], ys[0] + span))
        xs = np.where(a[rows].any(axis=0))[0]
    return (xs.min() + xs.max()) / 2 if len(xs) else im.width / 2


def place_on_canvas(
    fig,
    *,
    size=256,
    target_fig_h=None,
    scale=None,
    factor=2,
    align="feet",
    foot_margin=None,
):
    """Положить очищенную фигуру `fig` на прозрачный холст size×size.

    Масштаб задаётся ОДНИМ из:
      target_fig_h — нормализовать высоту фигуры к этому значению (idle/walk:
                     все стоячие кадры одного роста);
      scale        — фиксированный множитель src->out (для приседа/наклона:
                     высоту НЕ нормализуем, иначе короткая поза раздуется; берём
                     тот же scale, что у стойки, и добиваем верх пустотой);
      по умолчанию — заполнить ~90% высоты холста.

    factor       — целочисленный nearest-апскейл после box-даунскейла
                   (крупный «честный» пиксель; 2 = блоки 2px).
    align        — горизонтальная привязка: 'feet' | 'head' | 'bbox'.
    foot_margin  — отступ ступней от низа (по умолчанию size*2/64).
    """
    cw, ch = fig.size
    if target_fig_h is not None:
        s = target_fig_h / ch
    elif scale is not None:
        s = scale
    else:
        s = size * 0.90 / ch

    lw = max(1, round(cw * s / factor))
    lh = max(1, round(ch * s / factor))
    small = _resize_premult(fig, lw, lh, Image.BOX)
    big = small.resize((lw * factor, lh * factor), Image.NEAREST)

    if foot_margin is None:
        foot_margin = round(size * 2 / 64)
    nw, nh = big.size
    ox = round(size / 2 - _anchor_x(big, align))
    oy = size - foot_margin - nh
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(big, (ox, oy), big)
    return canvas
