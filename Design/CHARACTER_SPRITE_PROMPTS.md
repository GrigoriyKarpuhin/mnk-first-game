# Промпты генерации персонажных листов (style-guide)

Статус: рабочий стандарт
Последнее обновление: 2026-06-14

Назначение: единый способ писать промпты для генерации **листов спрайтов**
персонажей. Здесь — правила раскладки, стиля и масштаба, шаблон промпта и
готовые промпты по группам анимаций. Канон персонажей, палитра и мир — в
`Design/ART_STYLE.md` (этот документ его НЕ заменяет, а дополняет по части
промптинга).

> ⚠️ Смена детализации. Текущий герой в `ART_STYLE.md` §7a — детальный HD
> пиксель-арт (256px). Новый стандарт персонажей — **крупный «честный»
> пиксель** (как у спрайта Девушки C-075). Все новые листы делаем в нём.

---

## 1. Главный принцип: лист = группа анимации, на листе = 3 ракурса

Мы НЕ генерируем по одному спрайту. Мы генерируем **листы**, где один лист — это
**одна фаза анимации** во **всех трёх ракурсах сразу**, в одном масштабе и стиле:

```
IDLE     →  лист: [ front | side | back ]
WALK_1   →  лист: [ front | side | back ]
WALK_2   →  лист: [ front | side | back ]
PICKUP_1 →  лист: [ front | side | back ]
PICKUP_2 →  лист: [ front | side | back ]
…будущие фазы — тем же шаблоном
```

Почему так: ракурсы одного состояния обязаны совпадать по росту, толщине,
позе рук/ног и линии пола. Сгенерированные вместе на одном листе, они
получаются консистентными «из коробки». Между фазами консистентность держим
референсом (см. §6).

**Канонический порядок слева направо: `front → side → back`.** Он фиксирован для
ВСЕХ листов (иначе собьётся импорт и сравнение фаз).

---

## 2. Раскладка листа (обязательно для каждого промпта)

- Три фигуры **в один ряд**, на общем фоне, **равные по размеру и масштабу**.
- Фон **прозрачный или чистый белый**, ровный, без теней под ногами и виньетки.
- **Ступни всех трёх фигур на одной горизонтальной линии пола** (иначе кадры
  «прыгают» при склейке анимации).
- Макушки на одной линии (один и тот же рост во всех ракурсах).
- Между фигурами **широкий чистый зазор** фона — лист режется по зазорам
  (`tools/import_player_art.py`, листы «фигуры в ряд» по белым промежуткам).
- Каждая фигура отцентрована в своей колонке, целиком в кадре, ничего не обрезано.
- Кадр листа — горизонтальный, 3:1 (три квадрата в ряд).

---

## 3. Стиль — 16-BIT ПИКСЕЛЬ-АРТ

Цель — чистый **16-bit пиксель-арт (SNES-эра)**. Не гонимся ни за гигантскими
чанками, ни за HD-детализацией: 16-bit даёт нужный баланс «читаемо + аккуратно».

- **Классический 16-bit пиксель-арт**, без сглаживания (no anti-aliasing), без
  размытия и мягких градиентов.
- **КРУПНЫЙ ПИКСЕЛЬ (ключевое).** Низкая плотность пикселей — как на старых
  низкоразрешённых экранах, где «мало пикселей и всё крупновато». Пиксель должен
  быть явно крупным и заметным, а не мелким. Рабочие формулировки, которые зашли:
  *«bigger pixels, like the era of low-resolution screens with few pixels where
  everything looked coarse and chunky»* и конкретная мера *«like a 64–96 px tall
  sprite enlarged 8x–12x with nearest-neighbor scaling»* (числа модель понимает
  лучше абстракции). Без этого генератор уходит в слишком высокий рез/детализацию.
- **Плоские заливки + лёгкая 16-bit тень** (1–2 ступени, можно аккуратный
  dithering как в SNES). Тёмный контур вокруг фигуры и крупных форм.
- **ЧИСТО, НЕ шум.** Запрещаем зашумлённую/пятнистую (камуфляжную) текстуру:
  noise, grain, mottled/camo, фотошейдинг. Ткань — ровный цвет + сторона тени.
- **Хороший контраст.** Низкоконтрастная «муть» — брак.
- **Пропорции — золотая середина (критично).** Нормальный взрослый, слегка
  компактный, **~6.5–7 голов**. НЕ вытянутый/«долговязый» (тимлиду не нравится
  излишняя вытянутость), но и НЕ ужатый коротыш/чиби (~6 голов и ниже — брак).
  Натуральная человеческая фигура, просто без лишней «модельной» длины.
- Детали (карманы, заплатки, шнуровка, ID) — аккуратные, но скромные; силуэт и
  читаемость важнее мелочей.

Палитра — строго по `ART_STYLE.md` §2: тёмная, низконасыщенная, серо-оливковая
ткань, холодные тени с зеленцой, **рыжая ржавчина — единственный тёплый акцент**,
смуглая кожа `#a4866e`. Никаких ярких/тёплых цветов.

---

## 4. Ракурсы

- **front** — лицом к зрителю (idle смотрит вниз-на камеру).
- **side** — профиль, **смотрит ВЛЕВО** (вправо получаем `flipX` в движке).
- **back** — спиной к зрителю.
- Один персонаж = один рост во всех трёх (≈7.5–8 голов, взрослые пропорции,
  НЕ «приземлять» фигуру).

---

## 5. Шаблон промпта (заполнять под группу)

```
Character sprite SHEET for a 2D top-down game. THREE full-body figures of the
SAME character in one horizontal row, left to right: FRONT view, SIDE view
(profile facing LEFT), BACK view. All three identical in height, build, scale
and style; feet on one shared ground line; heads aligned; each figure centered
with a wide clean gap between them for cutting.

STYLE — CHUNKY LOW-RES PIXEL ART (match the attached girl reference, NOT the
detailed one): big visible square pixels, effective figure height ~48–64 px then
nearest-neighbor upscaled, hard stair-step edges, NO anti-aliasing, NO blur, NO
smooth gradients. Flat color fills with only 1–2 shading steps, ~1px dark
outline. Simple face (eyes/brows are a few pixels). Readable silhouette over fine
detail.

CHARACTER (keep identical across all sheets): <ОПИСАНИЕ ПЕРСОНАЖА>

POSE for this sheet — <НАЗВАНИЕ ФАЗЫ>: <ОПИСАНИЕ ПОЗЫ, одинаково применить ко
всем трём ракурсам>

PALETTE (strict): grim dystopian prison vibe, dark low-saturation colors, cold
green-tinted shadows; grey-olive worn fabric, dark boots, pale/light skin; NO
bright or warm colors except rusty-brown as the only warm accent.

TECH: transparent or clean white background, flat even light, no cast shadows, no
vignette; horizontal 3:1 frame; only the three figures, no text, no UI, no frame.
```

Меняем между группами **только блок `POSE`**. Всё остальное (персонаж, стиль,
масштаб, раскладка, палитра) — слово в слово одинаково на всех листах.

### Описания поз по группам

- **IDLE** — расслабленная стойка, вес ровно, руки опущены вдоль тела, лёгкое
  «дыхание». База для всех остальных фаз.
- **WALK_1** — середина шага: левая нога вперёд, правая назад, руки в
  противофазе; умеренная амплитуда.
- **WALK_2** — зеркальная фаза: правая нога вперёд, левая назад; **та же**
  амплитуда, что и WALK_1 (иначе хромает).
- **PICKUP_1** — наклон вперёд, тянется рукой вниз; колени чуть согнуты.
- **PICKUP_2** — глубокий присед к полу, рука у самого пола; фигура **ниже**
  стойки (высоту НЕ выравнивать со стойкой — это норма для приседа).

---

## 6. Референсы и консистентность (критично)

**Какую картинку прикладывать в генератор — решает стиль.** Референс-изображение
почти всегда побеждает текст по стилю.

- **Style-референс = спрайт Девушки C-075** (крупный чистый пиксель). Он задаёт
  размер пикселя, чистоту заливок, контур и палитру. На раннем этапе — это
  главный рычаг стиля.
- **НЕ прикладывай старый HD-арт мужика** как style-референс — он тянет в
  детально/шумно и ломает чанки-стиль. Личность C-4821 (лицо, щетина, причёска,
  комбинезон) задаём **текстом**, не картинкой.
- **⚠️ Протечка личности из style-референса.** Картинка-референс переносит не
  только «как нарисовано», но и «что нарисовано»: с девушки на мужика утекают
  ПУЧОК ВОЛОС и ТЁМНАЯ КОЖА (получается «дреды + бун + очень тёмная кожа» вместо
  канона). Поэтому: внешность (волосы, кожа) **жёстко перебиваем текстом и
  negative**, а вес style-референса держим **умеренным** (если есть регулировка).
  Если всё равно течёт — убрать референс девушки, оставить текст, а крупный
  пиксель добить пост-обработкой (см. §9).

**Между фазами:**
- Сначала генерим **IDLE** (эталон), потом всё остальное от него.
- К **каждому** новому листу прикладывай **готовый IDLE-лист** этого персонажа
  (+ при необходимости предыдущую фазу) — держим тон кожи, пропорции, причёску,
  одежду и масштаб.
- Проверяй после генерации: одинаковый рост во всех ракурсах, ступни на одной
  линии, одинаковая амплитуда в walk, side смотрит ВЛЕВО.

---

## 7. Готовый промпт: IDLE для нового ГГ (C-4821)

Персонаж — канон C-4821 из `ART_STYLE.md` §3, в чистом 16-bit пиксель-арте.
Можно прикладывать спрайт Девушки C-075 как лёгкий style-референс (умеренный
вес), но внешность мужика жёстко задаём текстом (см. §6 про протечку).

```
Clean 16-BIT PIXEL ART character sprite SHEET (SNES era) for a 2D top-down game.
THREE full-body figures of the SAME character in one horizontal row, left to
right: FRONT view, SIDE view (profile facing LEFT), BACK view. All three
identical in height, build, scale and style; feet on one shared ground line;
heads aligned; each figure centered with a wide clean gap between them for
cutting.

STYLE — strict: clean CHUNKY LOW-RES 16-BIT PIXEL ART (early 90s arcade / SNES
era) with BIG VISIBLE SQUARE PIXELS. Low pixel density — like a 64–96 px tall
sprite enlarged 8x–12x with nearest-neighbor scaling; pixels clearly large and
coarse, like old low-resolution screens. Crisp, NO anti-aliasing, NO blur, NO
soft gradients. Flat color fills with simple 16-bit shading (1–2 shade steps),
a THICK dark outline and blocky silhouette; clothing folds simplified and
readable, not finely detailed. Limited grim grey-olive palette, good contrast.
ABSOLUTELY NOT: high resolution, fine/small pixels, photorealistic or HD detail,
noise, grain, mottled or camouflage texture, soft gradients, blur, low-contrast
mush.

PROPORTIONS — strict and IMPORTANT: balanced, slightly compact natural adult,
about 6.5–7 heads. NOT elongated, NOT lanky, NOT stretched (no overly long legs).
But also NOT squished, NOT chibi, NOT short/stocky/squat. A normal realistic
human build, just not model-tall.

CHARACTER (keep identical across all sheets): young slim man with NATURAL FAIR /
WHITE skin (healthy fair caucasian tone, NOT tan, NOT olive, NOT dark, NOT black,
NOT grey or sickly). He has been imprisoned a long time and stopped taking care
of himself: DIRTY and UNKEMPT — grime and dirt smudges on his face, neck and
hands, greasy messy hair. Hair is SHORT-to-MEDIUM messy wavy black hair, loose
and tousled, hanging DOWN — NOT dreadlocks, NOT braids, NOT a bun, NOT a
ponytail, NOT an afro. Moderately TIRED, weary face: mild dark circles under the
eyes, a slightly worn look — fatigued but still alert and composed; NOT extreme,
NOT hollow/sunken, NOT gaunt, NOT broken or sickly. Dark eyes, thick brows,
short stubble. Worn,
DIRTY dark grey-olive prison jumpsuit (base ~#3b3b34, shadow ~#26261f, light
~#4d4d44), SOLID uniform fabric color with one shadow side (no pattern), covered
in dirt smudges, grime and stains; sleeves rolled to the forearms, a drawstring
tie at the waist, chest pocket, cargo pocket on the thigh, a few rusty-brown
patches. Brown worn laced leather boots. Tiny "C-4821" stencil hint on the chest
(a few pixels, not sharp text).

POSE for this sheet — IDLE: relaxed neutral standing stance, weight even, arms
hanging down along the body, calm. Front looks toward the camera, side is a clean
left-facing profile, back shows the shoulders/back of the head.

PALETTE (strict): grim dystopian prison vibe, dark low-saturation colors, cold
green-tinted shadows; grey-olive worn fabric, dark boots, pale/light skin; NO
bright or warm colors except rusty-brown as the only warm accent.

TECH: transparent or clean white background (NOT black), flat even light, no cast
shadows, no vignette; horizontal 3:1 frame; only the three figures, no text, no
UI, no frame.
```

Если генератор поддерживает **negative prompt**, продублируй туда:
`dreadlocks, braids, hair bun, ponytail, afro, dark skin, tan skin, black person,
clean tidy clothes, photorealistic, HD detail, fine pixels, high resolution,
noise, grain, camouflage texture, mottled, soft gradient, blur, low contrast,
elongated, lanky, stretched, very long legs, squished, chibi, short stocky
proportions, black background, extra limbs, text`.

> После генерации IDLE — сохрани лист как эталон и прикладывай его референсом к
> WALK_1/WALK_2/PICKUP и всем будущим фазам (см. §6).

---

## 8. Частые ошибки генерации и как чинить

| Симптом на выходе | Причина | Фикс |
|---|---|---|
| Пятнистая «камуфляжная» ткань, шум/grain | «крупный пиксель» понят как текстура; либо детальный style-референс | CLEAN FLAT fills + запрет noise/dither/camo в промпте и negative; style-реф — девушка |
| Мутно, низкий контраст, слабый контур | нет требования контраста/контура; HD-референс | «HIGH CONTRAST, bold dark outline»; не прикладывать HD-арт |
| Фигура слишком вытянутая/долговязая | переусердствовали с «tall, long legs» | целиться в баланс «slightly compact, ~6.5–7 heads, NOT elongated/lanky» + в negative «elongated, lanky, very long legs» |
| Коренастая/низкая фигура (~6 голов и ниже) | дефолт нейронки / перебор со сжатием | «~6.5–7 heads, NOT chibi/squat» + в negative «short stocky, chibi, squished» |
| Чёрный фон вместо прозрачного | модель добавила фон | явно «clean white background (NOT black)»; фон убрать на импорте |
| Ракурсы разного роста / «прыгают» | сгенерены вразнобой | один лист на 3 ракурса; «feet on one ground line, heads aligned» |
| Стиль уехал между фазами | нет эталона | прикладывать готовый IDLE-лист к каждой следующей фазе |
| Дреды / пучок / тёмная кожа вместо C-4821 | протечка внешности из референса / дефолт | перебить текстом «natural fair/white skin, short messy wavy hair DOWN», в negative «dreadlocks, bun, dark skin, tan skin» |
| Слишком HD / фотодетализация | не задан 16-bit | «clean 16-bit pixel art (SNES era)», в negative «photorealistic, HD detail» |
| Слишком мелкий пиксель / высокий рез | не задан размер пикселя | добавить «BIG CHUNKY PIXELS, low pixel density, like old low-res screens, coarse/chunky»; в negative «fine pixels, high resolution» |

---

## 9. Крупный пиксель через пост-обработку (запасной вариант)

Обычно НЕ нужно: размер пикселя добивается промптом (см. §3 «крупный пиксель»).
Этот способ — аварийный, если конкретная генерация всё же вышла слишком мелкой
по пикселю и перегенерить лень. Суть: сжать картинку до низкого «эффективного»
разрешения через **box**-фильтр, при желании вернуть вверх через
**nearest-neighbor** (жёсткие квадраты).

Быстрый прогон (Pillow), цель — фигура ~32 px (высота всего листа ~40 px):

```python
from PIL import Image
im = Image.open("sheet.png").convert("RGBA")
target_h = 40                       # ниже число = крупнее пиксель; ~40 → фигура ~32px
w = round(im.width * target_h / im.height)
small = im.resize((w, target_h), Image.BOX)        # честный даунскейл
small = small.quantize(colors=12).convert("RGBA")  # ужать палитру до ~12 цветов
small.save("sheet_pixel.png")                       # это и есть пиксель-арт
# превью вверх без сглаживания (для просмотра, в игру можно и small):
small.resize((w*10, target_h*10), Image.NEAREST).save("sheet_pixel_x10.png")
```

Подбирай `target_h` (48 → мельче, 40 → ~32px фигура, 32 → совсем крупно).
Затем — обычный импорт через `tools/import_player_art.py` (нарезка по зазорам,
кроп, ступни).
