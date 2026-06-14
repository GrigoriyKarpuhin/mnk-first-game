# spritekit

Превращает AI-сгенерированные листы персонажей (белый **или** прозрачный фон) в
чистые игровые спрайты: вырезает фон, закрывает «карманы», убирает светлую кромку,
режет лист на отдельные фигуры и кладёт их на квадратный холст ступнями к низу с
крупным («честным») пикселем.

Зависимости: `Pillow`, `numpy`, `scipy`.

## CLI

Запуск из корня репозитория.

```bash
# 1) Просто очистить одну картинку (вырезать фон + обрезать по фигуре):
python3 -m tools.spritekit clean hero.png hero_clean.png

# 2) Лист «N фигур в ряд» -> стоячие кадры одного роста (idle / walk):
python3 -m tools.spritekit sheet sheet.png \
    --names player player_side player_up \
    --out-dir Assets/Resources/Sprites --normalize 232

# 3) Лист приседа/наклона -> тот же масштаб, БЕЗ нормализации высоты
#    (короткая поза остаётся короткой, верх добивается пустотой):
python3 -m tools.spritekit sheet pickup.png \
    --names player_pickup_1 player_side_pickup_1 player_up_pickup_1 \
    --out-dir Assets/Resources/Sprites --scale 0.337
```

`--normalize N` и `--scale F` взаимоисключающие. Стоячие кадры — `--normalize`
(один рост для всех). Присед/наклон — `--scale` тем же значением, что у стойки
(`рост_стойки_px / высота_фигуры_в_исходнике`), чтобы тело не раздувалось.

### Полезные флаги чистки
- `--white-threshold 236` — порог «белого» (выше = строже).
- `--pocket-min-area 24` — мин. площадь замкнутого кармана в px (мельче = глаза/
  блики, не трогаем). Подними/опусти под своё разрешение.
- `--no-pockets`, `--no-defringe` — отключить шаги.
- `--defringe-threshold 205`, `--defringe-iterations 2` — настройка среза кромки.
- `--align feet|head|bbox` — горизонтальная привязка фигуры (по умолчанию `feet`).

## Как библиотека

```python
from PIL import Image
from tools.spritekit import remove_background, slice_sheet, place_on_canvas

for fig, name in zip(slice_sheet(Image.open("sheet.png")),
                     ["player", "player_side", "player_up"]):
    clean = remove_background(fig, pocket_min_area=24)
    place_on_canvas(clean, target_fig_h=232).save(f"{name}.png")
```

## Модули
- `background.py` — `remove_background()`: вырез фона (заливка от краёв) +
  закрытие замкнутых карманов по площади + дефриндж светлой кромки.
- `sheet.py` — `find_figures()` / `slice_sheet()`: разрез листа по фоновым зазорам.
- `layout.py` — `place_on_canvas()`: chunky-ресайз (premultiplied box + nearest)
  и раскладка на холсте со ступнями у низа.

## Замечания
- Фон удаляется заливкой ОТ КРАЁВ, поэтому внутренние белые детали (глаза, блики)
  сохраняются; крупные замкнутые карманы убираются отдельным шагом по площади.
- Дефриндж снимает только почти-белые пиксели на границе с прозрачностью —
  тёмный контур персонажа не страдает.
- Для крупного пикселя важен `point`-фильтр (filterMode 0) и PPU = размер холста
  на стороне движка.
