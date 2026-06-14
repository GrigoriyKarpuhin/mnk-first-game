"""spritekit — превращение AI-листов персонажей в игровые спрайты.

Вход: PNG с белым ИЛИ прозрачным фоном (одна фигура или несколько в ряд).
Выход: чистый спрайт на прозрачном квадратном холсте, ступни у низа.

Этапы (каждый — отдельный модуль, можно дёргать по отдельности):
  background.remove_background — вырезать фон, закрыть карманы, убрать кромку;
  sheet.find_figures / slice_sheet — разрезать лист «N фигур в ряд» по зазорам;
  layout.place_on_canvas — ужать (chunky pixel) и положить на холст со ступнями.

Зависимости: Pillow, numpy, scipy.

Пример (одна картинка):
    from tools.spritekit import remove_background, place_on_canvas
    from PIL import Image
    fig = remove_background(Image.open("hero.png"))
    place_on_canvas(fig, target_fig_h=232).save("player.png")

CLI: `python3 -m tools.spritekit --help`
"""
from .background import remove_background
from .sheet import find_figures, slice_sheet
from .layout import place_on_canvas

__all__ = ["remove_background", "find_figures", "slice_sheet", "place_on_canvas"]
