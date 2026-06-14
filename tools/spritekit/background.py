"""Удаление фона: вырез белого/прозрачного, закрытие карманов, дефриндж кромки.

Три типичные болячки AI-листов и как мы их лечим:

1. Внешний фон (белый или уже прозрачный) — заливка от краёв кадра.
2. Замкнутые «карманы» фона (например, белое между рукой и туловищем) — заливка
   от краёв их не достаёт. Удаляем отдельно: замкнутые белые области, у которых
   площадь >= pocket_min_area. Глаза/блики мелкие и сохраняются.
3. Светлая кромка-ореол по контуру (anti-alias между белым фоном и тёмным
   контуром) — таргетный дефриндж: убираем ТОЛЬКО почти-белые пиксели на границе
   с прозрачностью. Тёмный контур персонажа не трогается.
"""
import numpy as np
from PIL import Image
from scipy import ndimage

_CONN4 = np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]], dtype=bool)


def remove_background(
    im,
    *,
    white_threshold=236,
    fill_pockets=True,
    pocket_min_area=24,
    defringe=True,
    defringe_threshold=205,
    defringe_iterations=2,
    crop=True,
):
    """RGBA с прозрачным фоном.

    white_threshold   — пиксель считается «фоном», если min(R,G,B) >= порога
                        (или уже прозрачный). Выше порог = строже к белому.
    fill_pockets      — удалять замкнутые белые карманы (между рукой и телом).
    pocket_min_area   — мин. площадь кармана в пикселях (мелкое = глаза/блики,
                        НЕ трогаем). Замерь под своё разрешение при необходимости.
    defringe          — срезать светлую кромку-ореол по контуру.
    defringe_threshold— порог «почти-белого» для кромки (ниже white_threshold,
                        чтобы поймать сероватый ореол).
    defringe_iterations— сколько слоёв кромки снять (1–2 обычно достаточно).
    crop              — обрезать по bbox фигуры.
    """
    im = im.convert("RGBA")
    arr = np.asarray(im).astype(np.int16)
    minc = arr[..., :3].min(axis=2)
    alpha = arr[..., 3].copy()

    cand = (minc >= white_threshold) | (alpha < 16)      # кандидаты в фон
    lbl, n = ndimage.label(cand, structure=_CONN4)

    remove_label = np.zeros(n + 1, dtype=bool)           # какие метки убрать
    if n:
        border = np.concatenate([lbl[0, :], lbl[-1, :], lbl[:, 0], lbl[:, -1]])
        ext = np.unique(border[border > 0])
        remove_label[ext] = True                         # внешний фон
        if fill_pockets:
            sizes = np.bincount(lbl.ravel(), minlength=n + 1)
            enclosed = np.ones(n + 1, dtype=bool)
            enclosed[0] = False
            enclosed[ext] = False
            remove_label |= enclosed & (sizes >= pocket_min_area)
    remove = remove_label[lbl]
    alpha[remove] = 0

    if defringe:
        near_white = minc >= defringe_threshold
        for _ in range(defringe_iterations):
            transparent = alpha < 16
            touches_bg = ndimage.binary_dilation(transparent, structure=_CONN4)
            fringe = near_white & touches_bg & (alpha >= 16)
            alpha[fringe] = 0

    out = arr.astype(np.uint8)
    out[..., 3] = alpha.astype(np.uint8)
    res = Image.fromarray(out, "RGBA")
    if crop:
        bb = res.getbbox()
        if bb:
            res = res.crop(bb)
    return res
